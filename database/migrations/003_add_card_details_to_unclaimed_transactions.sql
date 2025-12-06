-- Add card detail fields to unclaimed_transactions table
-- This allows us to store card information when creating unclaimed transactions
-- and populate it when the card is later registered

ALTER TABLE unclaimed_transactions
ADD COLUMN IF NOT EXISTS card_last4 VARCHAR(4),
ADD COLUMN IF NOT EXISTS card_brand VARCHAR(50),
ADD COLUMN IF NOT EXISTS card_exp_month INTEGER,
ADD COLUMN IF NOT EXISTS card_exp_year INTEGER;

-- Create an index on card_last4 for quick lookups
CREATE INDEX IF NOT EXISTS idx_unclaimed_transactions_card_last4 ON unclaimed_transactions(card_last4);
