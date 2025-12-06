-- Migration: Add company_name to accounts table
-- Date: 2025-11-23
-- Description: Adds company_name column to the accounts table

-- Add company_name column (allow NULL initially for existing records)
ALTER TABLE accounts ADD COLUMN IF NOT EXISTS company_name VARCHAR(255);

-- For existing records without a company name, you can either:
-- Option 1: Set a default value
-- UPDATE accounts SET company_name = 'My Company' WHERE company_name IS NULL;

-- Option 2: Set it to the email domain
-- UPDATE accounts SET company_name = split_part(email, '@', 2) WHERE company_name IS NULL;

-- After backfilling existing data, make the column NOT NULL
-- ALTER TABLE accounts ALTER COLUMN company_name SET NOT NULL;
