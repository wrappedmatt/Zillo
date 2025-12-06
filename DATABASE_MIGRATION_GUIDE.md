# Database Migration Guide - Stripe Terminal Payment Tracking

This guide explains the database changes made to support Stripe Terminal integration with proper payment tracking.

## Overview

We've added a **separate payments table** to track all Stripe Terminal payments, providing:
- Complete audit trail of all payment transactions
- Link between Stripe payment intents and loyalty transactions
- Terminal identification for reporting
- Automatic points balance updates via database trigger
- Support for refunds and payment status tracking

## Migration File

**Location**: `database/migrations/002_add_stripe_terminal_support.sql`

## What Changed

### 1. New `payments` Table

A dedicated table to track all Stripe Terminal payments:

```sql
CREATE TABLE payments (
    id UUID PRIMARY KEY,
    account_id UUID NOT NULL,
    customer_id UUID,
    stripe_payment_intent_id VARCHAR(255) UNIQUE,
    stripe_charge_id VARCHAR(255),
    terminal_id VARCHAR(255),
    terminal_label VARCHAR(255),
    amount DECIMAL(10, 2),          -- Original amount
    amount_charged DECIMAL(10, 2),   -- After loyalty redemption
    loyalty_redeemed DECIMAL(10, 2),
    loyalty_earned INTEGER,
    status VARCHAR(20),              -- pending/completed/failed/refunded
    currency VARCHAR(3),
    payment_method_type VARCHAR(50),
    metadata JSONB,
    created_at TIMESTAMP,
    updated_at TIMESTAMP,
    completed_at TIMESTAMP,
    refund_reason TEXT,
    refunded_at TIMESTAMP,
    refund_amount DECIMAL(10, 2)
);
```

### 2. Updated `transactions` Table

Added fields to link transactions to payments:

```sql
ALTER TABLE transactions
ADD COLUMN payment_id UUID REFERENCES payments(id),
ADD COLUMN stripe_payment_intent_id VARCHAR(255);
```

### 3. Unique Constraint on Customers

Prevents duplicate customer emails within the same account:

```sql
CREATE UNIQUE INDEX idx_customers_email_account_unique
ON customers(account_id, LOWER(email));
```

### 4. Automatic Points Balance Updates

A database trigger automatically updates `customers.points_balance` when transactions are created:

```sql
CREATE TRIGGER trg_update_customer_points_balance
AFTER INSERT ON transactions
FOR EACH ROW
EXECUTE FUNCTION update_customer_points_balance();
```

**IMPORTANT**: This eliminates race conditions from manual updates in application code.

### 5. Points Balance Reconciliation Function

Helper function to verify points balance integrity:

```sql
SELECT * FROM reconcile_all_customer_points_balances();
```

Returns any customers whose points_balance doesn't match their transaction history.

### 6. Performance Indexes

Multiple indexes for fast queries:
- `idx_payments_stripe_payment_intent_id` - Payment lookup by Stripe ID
- `idx_payments_customer_id` - Customer payment history
- `idx_payments_terminal_id` - Terminal-specific reporting
- `idx_transactions_payment_id` - Transaction to payment lookups
- `idx_customers_email_account_unique` - Fast customer email lookups

### 7. Reporting Views

Two views for analytics:
- `vw_payment_summary` - Payment details with customer info
- `vw_terminal_metrics` - Terminal performance metrics

## How to Apply the Migration

### Step 1: Run the SQL Migration

Connect to your Supabase database and run:

```bash
psql -h YOUR_SUPABASE_HOST -U postgres -d postgres -f database/migrations/002_add_stripe_terminal_support.sql
```

Or via Supabase Dashboard:
1. Go to **SQL Editor**
2. Copy contents of `002_add_stripe_terminal_support.sql`
3. Click **Run**

### Step 2: Verify the Migration

Run these queries to confirm:

```sql
-- Check payments table exists
SELECT COUNT(*) FROM payments;

-- Check transactions table has new columns
SELECT column_name FROM information_schema.columns
WHERE table_name = 'transactions'
AND column_name IN ('payment_id', 'stripe_payment_intent_id');

-- Check trigger exists
SELECT trigger_name FROM information_schema.triggers
WHERE trigger_name = 'trg_update_customer_points_balance';

-- Check unique constraint exists
SELECT indexname FROM pg_indexes
WHERE indexname = 'idx_customers_email_account_unique';
```

### Step 3: Test the Trigger

The points balance trigger should now update automatically:

```sql
-- Get customer's current points
SELECT id, name, points_balance FROM customers WHERE email = 'test@example.com';

-- Insert a test transaction
INSERT INTO transactions (id, customer_id, points, amount, type, description, created_at)
VALUES (
    gen_random_uuid(),
    'YOUR_CUSTOMER_ID',
    10,
    10.00,
    'earn',
    'Test transaction',
    NOW()
);

-- Check points balance was automatically updated (should have increased by 10)
SELECT id, name, points_balance FROM customers WHERE email = 'test@example.com';
```

## Code Changes Summary

### Domain Layer
- **NEW**: `LemonadeApp.Domain/Entities/Payment.cs`
- **NEW**: `LemonadeApp.Domain/Interfaces/IPaymentRepository.cs`
- **UPDATED**: `LemonadeApp.Domain/Entities/Transaction.cs` (added PaymentId, StripePaymentIntentId)

### Application Layer
- **NEW**: `LemonadeApp.Application/DTOs/PaymentDTOs.cs`
- **NEW**: `LemonadeApp.Application/Services/IPaymentService.cs`
- **UPDATED**: `LemonadeApp.Application/DTOs/TransactionDTOs.cs` (added payment fields)

### Infrastructure Layer
- **NEW**: `LemonadeApp.Infrastructure/Repositories/PaymentRepository.cs`
- **NEW**: `LemonadeApp.Infrastructure/Services/PaymentService.cs`
- **UPDATED**: `LemonadeApp.Infrastructure/Repositories/TransactionRepository.cs` (added payment fields)
- **UPDATED**: `LemonadeApp.Infrastructure/Services/TransactionService.cs` (removed manual points balance update)

### Backend Layer
- **UPDATED**: `LemonadeApp.Rewards.Server/Controllers/TerminalController.cs` (uses Payment table)
- **UPDATED**: `LemonadeApp.Rewards.Server/Program.cs` (registered payment services)

## Important Notes

### Manual Points Balance Updates Removed

**BEFORE** (Old Code):
```csharp
// Manual update - race condition risk!
customer.PointsBalance += transaction.Points;
await _customerRepository.UpdateAsync(customer);
```

**AFTER** (New Code):
```csharp
// Database trigger handles this automatically
var created = await _transactionRepository.CreateAsync(transaction);
```

The database trigger is more reliable and prevents race conditions when multiple transactions happen simultaneously.

### Payment Flow

1. **Create Payment Intent**:
   - Terminal app calls `POST /api/terminal/create_payment_intent`
   - Backend creates Stripe PaymentIntent
   - Backend creates Payment record in database (status: "pending")

2. **Customer Taps Card**:
   - Terminal SDK processes payment via Stripe
   - Payment captured or held for manual capture

3. **Capture Payment**:
   - Terminal app calls `POST /api/terminal/capture_payment_intent`
   - Backend captures Stripe payment
   - Backend updates Payment record (status: "completed")
   - Backend creates Transaction record (linked to Payment via payment_id)
   - **Database trigger automatically updates customer points_balance**

4. **With Loyalty Redemption**:
   - Terminal app calls `POST /api/terminal/apply_redemption` first
   - Then calls `POST /api/terminal/capture_with_redemption`
   - Creates TWO transactions: one redeem (negative points), one earn (positive points)
   - Both transactions linked to same Payment record

## Rollback Plan

If you need to rollback the migration:

```sql
-- WARNING: This will delete all payment data!

-- Drop trigger and function
DROP TRIGGER IF EXISTS trg_update_customer_points_balance ON transactions;
DROP FUNCTION IF EXISTS update_customer_points_balance();
DROP FUNCTION IF EXISTS calculate_customer_points_balance(UUID);
DROP FUNCTION IF EXISTS reconcile_all_customer_points_balances();

-- Drop views
DROP VIEW IF EXISTS vw_payment_summary;
DROP VIEW IF EXISTS vw_terminal_metrics;

-- Remove columns from transactions
ALTER TABLE transactions
DROP COLUMN IF EXISTS payment_id,
DROP COLUMN IF EXISTS stripe_payment_intent_id;

-- Drop payments table
DROP TABLE IF EXISTS payments CASCADE;

-- Drop unique constraint on customers
DROP INDEX IF EXISTS idx_customers_email_account_unique;
```

**Note**: You'll also need to restore manual points balance updates in `TransactionService.cs`.

## Testing Checklist

After applying the migration:

- [ ] Run SQL migration successfully
- [ ] Verify all tables and columns exist
- [ ] Verify trigger is created
- [ ] Test trigger with manual transaction insert
- [ ] Verify points balance updates automatically
- [ ] Build backend project (should compile without errors)
- [ ] Start backend and test connection token endpoint
- [ ] Test create payment intent endpoint
- [ ] Test capture payment intent endpoint
- [ ] Test complete payment flow from Android terminal app
- [ ] Verify Payment records are created in database
- [ ] Verify Transaction records link to Payment records
- [ ] Test loyalty redemption flow
- [ ] Verify reporting views work correctly

## Troubleshooting

### Trigger Not Firing

```sql
-- Check trigger exists
SELECT * FROM information_schema.triggers
WHERE trigger_name = 'trg_update_customer_points_balance';

-- Check function exists
SELECT proname FROM pg_proc
WHERE proname = 'update_customer_points_balance';

-- Recreate trigger if needed
DROP TRIGGER IF EXISTS trg_update_customer_points_balance ON transactions;
CREATE TRIGGER trg_update_customer_points_balance
AFTER INSERT ON transactions
FOR EACH ROW
EXECUTE FUNCTION update_customer_points_balance();
```

### Points Balance Out of Sync

Use the reconciliation function:

```sql
-- Find customers with mismatched balances
SELECT * FROM reconcile_all_customer_points_balances();

-- Fix a specific customer
UPDATE customers
SET points_balance = calculate_customer_points_balance(id)
WHERE id = 'CUSTOMER_ID';
```

### Duplicate Email Error

If you get "duplicate key value violates unique constraint":

```sql
-- Find duplicate emails within accounts
SELECT account_id, LOWER(email), COUNT(*)
FROM customers
GROUP BY account_id, LOWER(email)
HAVING COUNT(*) > 1;

-- Manually resolve duplicates before applying migration
```

## Next Steps

1. ✅ Apply SQL migration to your Supabase database
2. ✅ Code changes are already complete and built successfully
3. ⬜ Test the complete payment flow end-to-end
4. ⬜ Monitor database trigger performance
5. ⬜ Set up monitoring for payment failures
6. ⬜ Implement email receipt functionality (currently stubbed)
7. ⬜ Add payment reconciliation reports
8. ⬜ Deploy to production

## Support

For issues:
- **Database migration problems**: Check Supabase logs
- **Backend errors**: Check `LemonadeApp.Rewards.Server` logs
- **Build errors**: Ensure all NuGet packages are restored
- **Terminal connection issues**: See `TERMINAL_SETUP.md`
