-- Migration: Add cashback_amount column to unclaimed_transactions table
-- This adds support for tracking cashback in unclaimed transactions

-- Add cashback_amount column
ALTER TABLE unclaimed_transactions
ADD COLUMN IF NOT EXISTS cashback_amount BIGINT NOT NULL DEFAULT 0;

-- Add comment to document the column
COMMENT ON COLUMN unclaimed_transactions.cashback_amount IS 'Cashback amount in cents (e.g., 500 = $5.00) for cashback loyalty systems';
