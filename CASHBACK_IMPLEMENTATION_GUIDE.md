# Cashback & Points Loyalty System Implementation Guide

This document outlines the implementation of dual loyalty system support (cashback and points-based) for the Lemonade Loyalty application.

## Overview

The application now supports two types of loyalty systems:
1. **Cashback** (default): Customers earn a percentage of their purchase as cashback in dollars
2. **Points**: Customers earn points based on their purchase amount (1 point per dollar)

## Completed Work

### 1. Database Schema ✅
**File**: `database/migrations/003_add_loyalty_system_configuration.sql`

Added the following fields to the database:

**Accounts Table:**
- `loyalty_system_type` - VARCHAR(20), default 'cashback', choices: 'points' or 'cashback'
- `cashback_rate` - DECIMAL(5,2), default 5.00 (percentage, e.g., 5.00 = 5%)
- `historical_reward_days` - INTEGER, default 14 (days to look back for historical purchases)
- `welcome_incentive` - DECIMAL(10,2), default 5.00 (welcome bonus in dollars)

**Customers Table:**
- `cashback_balance` - DECIMAL(10,2), default 0.00 (cashback balance in dollars)
- `welcome_incentive_awarded` - BOOLEAN, default FALSE
- `card_linked_at` - TIMESTAMP WITH TIME ZONE (when customer linked their card)

**Transactions Table:**
- `account_id` - UUID (reference to account)
- `cashback_amount` - DECIMAL(10,2), default 0.00 (cashback for this transaction)
- Updated `type` constraint to include: 'cashback_earn', 'cashback_redeem', 'welcome_bonus'

**Payments Table:**
- `cashback_earned` - DECIMAL(10,2), default 0.00

**Database Functions:**
- `calculate_cashback_amount(account_id, purchase_amount)` - Calculates cashback based on account rate
- `calculate_points_from_amount(amount)` - Calculates points (1 point per dollar)
- `award_welcome_incentive(customer_id)` - Awards welcome bonus when customer links card
- `update_customer_balance()` - Trigger function that updates points OR cashback based on loyalty system type

### 2. Domain Models ✅
**Files Updated**:
- `LemonadeApp.Domain/Entities/Account.cs`
- `LemonadeApp.Domain/Entities/Customer.cs`
- `LemonadeApp.Domain/Entities/Transaction.cs`

Added new properties to support cashback and loyalty system configuration.

### 3. Repository Models ✅
**Files Updated**:
- `LemonadeApp.Infrastructure/Repositories/AccountRepository.cs`
- `LemonadeApp.Infrastructure/Repositories/CustomerRepository.cs`
- `LemonadeApp.Infrastructure/Repositories/TransactionRepository.cs`

Updated Postgrest models and ToEntity/FromEntity mappings to include new cashback fields.

## Remaining Work

### 4. Settings API Controller (TODO)
**File**: `LemonadeApp.Dashboard.Server/Controllers/SettingsController.cs`

**Required Changes:**
1. Update GET endpoint to return loyalty system configuration:
   - `loyaltySystemType`
   - `cashbackRate`
   - `historicalRewardDays`
   - `welcomeIncentive`

2. Update PUT/PATCH endpoint to accept and save loyalty system settings

3. Add validation:
   - Cashback rate must be between 0 and 100
   - Historical reward days must be between 0 and 365
   - Welcome incentive must be >= 0

### 5. Payment Processing Logic (TODO)
**File**: `LemonadeApp.Dashboard.Server/Controllers/TerminalController.cs` (or payment processing service)

**Required Changes:**
1. When processing payment, check account's `loyalty_system_type`
2. If `cashback`:
   - Calculate cashback: `amount * (cashback_rate / 100)`
   - Create transaction with `cashback_amount` and `type='cashback_earn'`
   - Update customer's `cashback_balance`
3. If `points`:
   - Calculate points: `FLOOR(amount)`
   - Create transaction with `points` and `type='earn'`
   - Update customer's `points_balance`

### 6. Dashboard Settings UI (TODO)
**File**: `lemonadeapp.dashboard.client/src/pages/Settings.jsx`

**Required Changes:**
1. Add new section "Loyalty System Configuration" with:
   - Radio buttons for loyalty system type (Points / Cashback)
   - When "Cashback" is selected, show cashback configuration fields:
     - Cashback Rate (%) - number input with validation (0-100)
     - Historical Reward Period (days) - number input with validation (0-365)
     - Welcome Incentive ($) - number input with validation (>= 0)
   - When "Points" is selected, hide cashback fields
   - Save button to update settings via API

**UI Layout Example:**
```jsx
<div className="border rounded-lg">
  <div className="px-6 py-4 border-b bg-muted/30">
    <h2 className="text-lg font-semibold">Loyalty System</h2>
    <p className="text-sm text-muted-foreground mt-1">
      Configure how customers earn rewards
    </p>
  </div>
  <div className="px-6 py-6 space-y-6">
    {/* Radio buttons for system type */}
    {/* Conditional cashback configuration fields */}
  </div>
</div>
```

### 7. Terminal App (TODO)
**Files**: Android Terminal App (LemonadeTerminalApp)

**Required Changes:**
1. Fetch account's loyalty system type when loading account info
2. Display appropriate rewards on terminal:
   - If cashback: Show "$X.XX cashback earned"
   - If points: Show "X points earned"
3. Update all text to be dynamic based on loyalty type:
   - "You've earned: $5.25 cashback" vs "You've earned: 5 points"

**Files to Update:**
- `LemonadeTerminalApp/app/src/main/java/com/stripe/aod/sampleapp/Config.kt`
- Terminal success/recognition screens

### 8. Customer Portal (TODO)
**File**: `lemonadeapp.dashboard.client/src/pages/CustomerPortal.jsx`

**Required Changes:**
1. Display correct balance type:
   - If cashback: Show `$${cashbackBalance.toFixed(2)}` with "Cashback Balance" label
   - If points: Show `${pointsBalance}` with "Points Balance" label
2. Update transaction history to show cashback amounts for cashback transactions
3. Update redeem button text:
   - Cashback: "Redeem Cashback"
   - Points: "Redeem Points"

### 9. Dashboard Transaction Displays (TODO)
**Files**:
- `lemonadeapp.dashboard.client/src/pages/Transactions.jsx`
- `lemonadeapp.dashboard.client/src/pages/CustomerDetails.jsx`
- `lemonadeapp.dashboard.client/src/pages/Dashboard.jsx`

**Required Changes:**
1. Update transaction lists to show appropriate value:
   - For cashback transactions: Display `$X.XX`
   - For points transactions: Display `X points`
2. Update transaction type labels:
   - 'cashback_earn' → "Cashback Earned"
   - 'cashback_redeem' → "Cashback Redeemed"
   - 'welcome_bonus' → "Welcome Bonus"
3. Customer cards/lists should show correct balance type

### 10. Customer Details Page (TODO)
**File**: `lemonadeapp.dashboard.client/src/pages/Customers.jsx` and `CustomerDetails.jsx`

**Required Changes:**
1. Display both balances with appropriate labels:
   - "Points Balance: 150 pts"
   - "Cashback Balance: $15.50"
2. Only show the active balance prominently based on account's loyalty system type
3. Transaction history should render appropriate format for each transaction type

## Migration Steps

### Step 1: Run Database Migration
```bash
# Connect to your Supabase project and run:
psql $DATABASE_URL -f database/migrations/003_add_loyalty_system_configuration.sql
```

### Step 2: Update Backend Code
The Domain and Infrastructure layers are already updated. Next:
1. Update Settings Controller to handle new loyalty fields
2. Update payment processing logic to support both systems
3. Test API endpoints with Postman/Swagger

### Step 3: Update Dashboard UI
1. Add loyalty system configuration section to Settings page
2. Update all customer/transaction displays to show correct format
3. Test switching between loyalty types

### Step 4: Update Terminal App
1. Update API calls to fetch loyalty system type
2. Update all UI text to be dynamic
3. Test with both cashback and points accounts

### Step 5: Update Customer Portal
1. Update balance display logic
2. Update transaction history rendering
3. Test portal with both loyalty types

## Configuration Examples

### Cashback Configuration
```json
{
  "loyaltySystemType": "cashback",
  "cashbackRate": 5.00,
  "historicalRewardDays": 14,
  "welcomeIncentive": 5.00
}
```

**Meaning:**
- Customers earn 5% cashback on all purchases
- When linking a card, reward purchases from the last 14 days
- Give $5.00 welcome bonus when customer first links their card

### Points Configuration
```json
{
  "loyaltySystemType": "points",
  "cashbackRate": 0.00,
  "historicalRewardDays": 0,
  "welcomeIncentive": 100.00
}
```

**Meaning:**
- Customers earn 1 point per dollar spent
- No historical rewards (set to 0)
- Give 100 points as welcome bonus

## Testing Checklist

- [ ] Database migration runs successfully
- [ ] Backend compiles without errors
- [ ] Settings API returns loyalty configuration
- [ ] Settings API saves loyalty configuration
- [ ] Payment processing calculates cashback correctly
- [ ] Payment processing calculates points correctly
- [ ] Dashboard Settings UI displays loyalty system options
- [ ] Dashboard shows correct balance types for customers
- [ ] Terminal app displays cashback amounts
- [ ] Terminal app displays points amounts
- [ ] Customer portal shows correct balance type
- [ ] Transaction history displays correct format
- [ ] Welcome incentive is awarded when customer links card
- [ ] Historical rewards are calculated correctly

## API Endpoints

### GET /api/settings
Returns account settings including loyalty configuration:
```json
{
  "companyName": "...",
  "loyaltySystemType": "cashback",
  "cashbackRate": 5.00,
  "historicalRewardDays": 14,
  "welcomeIncentive": 5.00,
  ...
}
```

### PUT /api/settings
Updates account settings including loyalty configuration:
```json
{
  "loyaltySystemType": "cashback",
  "cashbackRate": 10.00,
  "historicalRewardDays": 30,
  "welcomeIncentive": 10.00,
  ...
}
```

## Notes

- **Default**: All new accounts default to cashback system with 5% rate
- **Migration**: Existing accounts will automatically get cashback=5%, welcome=$5, historical=14 days
- **Validation**: Frontend and backend should validate cashback rate (0-100%), historical days (0-365), welcome incentive (>= 0)
- **Currency**: All cashback amounts are stored in dollars (not cents)
- **Rounding**: Cashback amounts are rounded to 2 decimal places
- **Triggers**: Database triggers automatically update customer balances based on loyalty system type
