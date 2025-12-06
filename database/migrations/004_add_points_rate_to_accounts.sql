-- Migration: Add points_rate column to accounts table
-- This allows configurable points earning rate (e.g., 1 point per dollar, 2 points per dollar, etc.)

-- Add points_rate column with default value of 1.00 (1 point per dollar)
ALTER TABLE accounts
ADD COLUMN IF NOT EXISTS points_rate DECIMAL(10, 2) NOT NULL DEFAULT 1.00;

-- Add comment to document the column
COMMENT ON COLUMN accounts.points_rate IS 'Points per dollar spent (e.g., 1.00 = 1 point per dollar, 2.00 = 2 points per dollar)';
