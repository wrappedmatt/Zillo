# Android Terminal App - Dual Loyalty System Implementation Guide

This guide explains how to update the Android Terminal App to support both cashback and points loyalty systems.

## Overview

The terminal app needs to:
1. Fetch account configuration including loyalty system type and cashback rate
2. Calculate and display appropriate rewards (cashback vs points)
3. Update all UI text dynamically based on loyalty type
4. Handle API responses for both systems

## Files to Update

### 1. Config.kt (`LemonadeTerminalApp/app/src/main/java/com/stripe/aod/sampleapp/Config.kt`)

**Current State**: Hardcoded for points system
**Required Changes**: Add loyalty system configuration

```kotlin
object Config {
    const val BASE_URL = "https://your-api-url.com"
    const val TERMINAL_LOCATION_ID = "your_location_id"

    // Loyalty system configuration (fetched from API)
    var loyaltySystemType: String = "points" // "points" or "cashback"
    var cashbackRate: Double = 5.0 // Percentage (e.g., 5.0 = 5%)
    var historicalRewardDays: Int = 14
    var welcomeIncentive: Double = 5.0

    fun calculateReward(amountInCents: Long): String {
        val amountInDollars = amountInCents / 100.0

        return when (loyaltySystemType) {
            "cashback" -> {
                val cashback = amountInDollars * (cashbackRate / 100.0)
                "$${String.format("%.2f", cashback)} cashback"
            }
            else -> {
                val points = (amountInDollars).toInt()
                "$points points"
            }
        }
    }

    fun getRewardLabel(): String {
        return when (loyaltySystemType) {
            "cashback" -> "Cashback"
            else -> "Points"
        }
    }
}
```

### 2. API Service

**Add function to fetch account configuration:**

```kotlin
suspend fun fetchAccountConfiguration(authToken: String): AccountConfig? {
    return withContext(Dispatchers.IO) {
        try {
            val response = client.get("${Config.BASE_URL}/api/accounts/me") {
                header("Authorization", "Bearer $authToken")
            }

            if (response.status.isSuccess()) {
                val account = response.body<AccountConfig>()

                // Update Config with fetched values
                Config.loyaltySystemType = account.loyaltySystemType ?: "points"
                Config.cashbackRate = account.cashbackRate ?: 5.0
                Config.historicalRewardDays = account.historicalRewardDays ?: 14
                Config.welcomeIncentive = account.welcomeIncentive ?: 5.0

                account
            } else {
                null
            }
        } catch (e: Exception) {
            Log.e("API", "Error fetching account config", e)
            null
        }
    }
}

@Serializable
data class AccountConfig(
    val id: String,
    val loyaltySystemType: String?,
    val cashbackRate: Double?,
    val historicalRewardDays: Int?,
    val welcomeIncentive: Double?
)
```

### 3. Terminal Success Screen

**Location**: Payment success/completion screen
**Required Changes**: Update reward display

```kotlin
// After successful payment
val rewardText = Config.calculateReward(paymentAmount)
val rewardLabel = Config.getRewardLabel()

TextView.text = "You've earned: $rewardText"
SubtitleText.text = "Your $rewardLabel balance has been updated"
```

### 4. Customer Recognition Screen

**Location**: When card is recognized with existing customer
**Required Changes**: Display current balance based on loyalty type

```kotlin
// Fetch customer balance
suspend fun displayCustomerBalance(customerId: String, authToken: String) {
    val balanceResponse = fetchCustomerBalance(customerId, authToken)

    if (balanceResponse != null) {
        val balanceText = when (Config.loyaltySystemType) {
            "cashback" -> {
                val cashback = balanceResponse.cashback_balance / 100.0
                "$${"%.2f".format(cashback)} cashback"
            }
            else -> {
                "${balanceResponse.points_balance} points"
            }
        }

        TextView.text = "Welcome back! You have $balanceText"
    }
}

@Serializable
data class CustomerBalanceResponse(
    val customer_id: String,
    val points_balance: Int,
    val cashback_balance: Long, // In cents
    val loyalty_system_type: String
)
```

### 5. Transaction API Calls

**Update payment success API call to include cashback:**

```kotlin
// When processing payment
val account = fetchAccountConfiguration(authToken)

val cashbackAmount = if (account?.loyaltySystemType == "cashback") {
    (paymentAmount / 100.0) * ((account.cashbackRate ?: 5.0) / 100.0)
} else {
    0.0
}

// This is already handled by the backend via TerminalController
// Just ensure the API endpoint is called correctly
val response = client.post("${Config.BASE_URL}/api/terminal/payment-intent-webhook") {
    contentType(ContentType.Application.Json)
    header("Authorization", "Bearer $authToken")
    setBody(PaymentIntentWebhook(
        payment_intent_id = paymentIntentId,
        card_fingerprint = cardFingerprint
    ))
}
```

## Implementation Steps

### Step 1: Update Config.kt

1. Open `LemonadeTerminalApp/app/src/main/java/com/stripe/aod/sampleapp/Config.kt`
2. Add the loyalty system configuration variables
3. Add the `calculateReward()` and `getRewardLabel()` helper functions

### Step 2: Add Account Configuration API

1. Locate your API service file (likely `ApiService.kt` or similar)
2. Add the `fetchAccountConfiguration()` function
3. Add the `AccountConfig` data class with `@Serializable` annotation

### Step 3: Initialize Configuration on App Start

```kotlin
// In MainActivity.onCreate() or similar initialization
lifecycleScope.launch {
    val authToken = getAuthToken() // Your existing auth token retrieval
    if (authToken != null) {
        fetchAccountConfiguration(authToken)
    }
}
```

### Step 4: Update All Display Text

Search for hardcoded strings in your app:
- "points" → Use `Config.getRewardLabel()`
- "Points earned" → Use dynamic text based on loyalty type
- Point calculations → Use `Config.calculateReward()`

### Step 5: Update Customer Balance Display

1. Find where customer balance is displayed
2. Update to use `CustomerBalanceResponse` with both `points_balance` and `cashback_balance`
3. Display the appropriate balance based on `loyalty_system_type`

### Step 6: Test Both Systems

**Test with Points System:**
1. Set account loyalty type to "points"
2. Process a $10.00 payment
3. Verify "10 points" is displayed
4. Check customer balance shows points

**Test with Cashback System:**
1. Set account loyalty type to "cashback" with 5% rate
2. Process a $10.00 payment
3. Verify "$0.50 cashback" is displayed
4. Check customer balance shows cashback amount

## API Endpoints Used

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/accounts/me` | GET | Fetch account configuration |
| `/api/terminal/lookup-customer` | POST | Find customer by card fingerprint |
| `/api/terminal/payment-intent-webhook` | POST | Process payment and award loyalty |

## Example Response Formats

**Account Configuration Response:**
```json
{
  "id": "account-guid",
  "companyName": "Example Coffee",
  "loyaltySystemType": "cashback",
  "cashbackRate": 5.0,
  "historicalRewardDays": 14,
  "welcomeIncentive": 5.0
}
```

**Customer Balance Response:**
```json
{
  "customer_id": "customer-guid",
  "credit_balance": 500,
  "points_balance": 50,
  "cashback_balance": 500,
  "loyalty_system_type": "cashback"
}
```

## UI Text Examples

### Points System:
- "You've earned: 10 points"
- "Your Points balance has been updated"
- "Welcome back! You have 150 points"

### Cashback System:
- "You've earned: $0.50 cashback"
- "Your Cashback balance has been updated"
- "Welcome back! You have $7.50 cashback"

## Error Handling

1. **No Internet Connection**: Cache last known loyalty configuration
2. **API Error**: Fall back to points system as default
3. **Invalid Configuration**: Log error and use default values

```kotlin
try {
    fetchAccountConfiguration(authToken)
} catch (e: Exception) {
    Log.e("Config", "Failed to fetch config, using defaults", e)
    Config.loyaltySystemType = "points"
    Config.cashbackRate = 5.0
}
```

## Testing Checklist

- [ ] App initializes and fetches account configuration
- [ ] Points system displays correctly
- [ ] Cashback system displays correctly
- [ ] Customer balance shows appropriate type
- [ ] Payment success shows correct reward
- [ ] All UI text is dynamic (no hardcoded "points")
- [ ] Error handling works when API is unavailable
- [ ] Configuration refreshes periodically (optional)

## Notes

- The backend TerminalController already handles creating transactions with correct types (cashback_earn vs earn)
- The backend automatically calculates cashback amounts based on account configuration
- The terminal app only needs to display information correctly based on the fetched configuration
- Consider refreshing configuration periodically (e.g., every hour) or on app resume

## Default Values

Use these defaults if API fetch fails:
- `loyaltySystemType`: "cashback"
- `cashbackRate`: 5.0
- `historicalRewardDays`: 14
- `welcomeIncentive`: 5.0
