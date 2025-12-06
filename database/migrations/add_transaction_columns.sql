-- Add missing columns to transactions table
ALTER TABLE transactions
ADD COLUMN IF NOT EXISTS account_id UUID REFERENCES accounts(id) ON DELETE CASCADE,
ADD COLUMN IF NOT EXISTS cashback_amount DECIMAL(10, 2) DEFAULT 0 NOT NULL,
ADD COLUMN IF NOT EXISTS payment_id UUID,
ADD COLUMN IF NOT EXISTS stripe_payment_intent_id TEXT;

-- Update type check constraint to include new types
ALTER TABLE transactions DROP CONSTRAINT IF EXISTS transactions_type_check;
ALTER TABLE transactions ADD CONSTRAINT transactions_type_check
CHECK (type IN ('earn', 'redeem', 'bonus', 'cashback_earn', 'cashback_redeem', 'adjustment'));

-- Create index for account_id
CREATE INDEX IF NOT EXISTS idx_transactions_account_id ON transactions(account_id);

-- Backfill account_id from customer relationship
UPDATE transactions t
SET account_id = c.account_id
FROM customers c
WHERE t.customer_id = c.id
AND t.account_id IS NULL;

-- Make account_id NOT NULL after backfill
ALTER TABLE transactions ALTER COLUMN account_id SET NOT NULL;
