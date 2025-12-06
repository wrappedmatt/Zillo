-- Migration: Add both signup bonus fields to support points and cashback systems
-- This adds signup_bonus_cash (decimal) and signup_bonus_points (integer)

-- Add signup_bonus_cash column for cashback system
ALTER TABLE accounts
ADD COLUMN IF NOT EXISTS signup_bonus_cash DECIMAL(10, 2) NOT NULL DEFAULT 5.00;

-- Add signup_bonus_points column for points system
ALTER TABLE accounts
ADD COLUMN IF NOT EXISTS signup_bonus_points INTEGER NOT NULL DEFAULT 100;

-- Add comments to document the columns
COMMENT ON COLUMN accounts.signup_bonus_cash IS 'Signup bonus in dollars for cashback system (e.g., 5.00 = $5.00 cash)';
COMMENT ON COLUMN accounts.signup_bonus_points IS 'Signup bonus in points for points system (e.g., 100 = 100 points)';
