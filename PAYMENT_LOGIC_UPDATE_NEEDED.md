# Payment Processing Logic Updates Required

## Terminal Controller Updates

The [TerminalController.cs](LemonadeApp.Dashboard.Server/Controllers/TerminalController.cs) needs to be updated to support both cashback and points systems.

### Key Locations to Update:

#### 1. Line 246 - Points Calculation
**Current:**
```csharp
var points = (int)(paymentIntent.Amount / 100);
```

**Update to:**
```csharp
// Get account to check loyalty system type
var account = await _accountRepository.GetByIdAsync(accountId);
int points = 0;
decimal cashbackAmount = 0;

if (account.LoyaltySystemType == "cashback")
{
    // Calculate cashback based on account rate
    cashbackAmount = Math.Round((paymentIntent.Amount / 100m) * (account.CashbackRate / 100m), 2);
}
else
{
    // Calculate points (1 point per dollar)
    points = (int)(paymentIntent.Amount / 100);
}
```

#### 2. Line 313 - Unclaimed Transaction Creation
**Current:**
```csharp
Points = points,
```

**Update to:**
```csharp
Points = points,
CashbackAmount = cashbackAmount,
AccountId = accountId,
```

#### 3. Line 379-384 - Response Object
**Current:**
```csharp
loyalty_points = points,
```

**Update to:**
```csharp
loyalty_points = points,
cashback_amount = cashbackAmount,
loyalty_system_type = account.LoyaltySystemType,
```

#### 4. Line 449 - Customer Balance Response
**Current:**
```csharp
credit_balance = customer.PointsBalance * 100, // Convert points to cents (1 point = $0.01)
points_balance = customer.PointsBalance
```

**Update to:**
```csharp
credit_balance = account.LoyaltySystemType == "cashback"
    ? (long)(customer.CashbackBalance * 100)
    : customer.PointsBalance * 100,
points_balance = customer.PointsBalance,
cashback_balance = customer.CashbackBalance,
loyalty_system_type = account.LoyaltySystemType
```

#### 5. Line 568 & 588 - Redemption and Earning Transactions
**Current:**
```csharp
var pointsToRedeem = (int)(credit_redeemed / 100);
var pointsEarned = (int)(amount_to_capture / 100);
```

**Update to:**
```csharp
// For redemption
if (account.LoyaltySystemType == "cashback")
{
    var cashbackToRedeem = credit_redeemed / 100m;
    // Create cashback redemption transaction
}
else
{
    var pointsToRedeem = (int)(credit_redeemed / 100);
    // Create points redemption transaction
}

// For earning
if (account.LoyaltySystemType == "cashback")
{
    var cashbackEarned = Math.Round((amount_to_capture / 100m) * (account.CashbackRate / 100m), 2);
    // Create cashback earning transaction
}
else
{
    var pointsEarned = (int)(amount_to_capture / 100);
    // Create points earning transaction
}
```

### CreateTransactionRequest Updates

The `CreateTransactionRequest` record likely needs to be updated to include:
- `AccountId` (required)
- `CashbackAmount` (decimal, default 0)
- Update `Type` to support new transaction types: 'cashback_earn', 'cashback_redeem', 'welcome_bonus'

## UnclaimedTransaction Updates

The `UnclaimedTransaction` entity and repository also need similar updates to support cashback amounts.

## Testing Checklist

After implementing these changes:
- [ ] Test cashback system with terminal payment
- [ ] Test points system with terminal payment
- [ ] Test switching between loyalty types
- [ ] Verify correct amounts are calculated
- [ ] Verify correct transaction types are created
- [ ] Test card registration with unclaimed transactions
- [ ] Test redeeming cashback vs redeeming points
