-- Migration: Add slug field to accounts table
-- This allows each merchant to have their own custom rewards URL

-- Add the slug column
ALTER TABLE accounts ADD COLUMN IF NOT EXISTS slug VARCHAR(255);

-- Create unique index on slug for fast lookups
CREATE UNIQUE INDEX IF NOT EXISTS idx_accounts_slug ON accounts(slug);

-- Optional: If you want to generate slugs from existing company names, run this:
-- UPDATE accounts SET slug = LOWER(REGEXP_REPLACE(company_name, '[^a-zA-Z0-9]+', '-', 'g')) WHERE slug IS NULL OR slug = '';

-- Note: For new accounts, the slug should be provided during signup
-- The rewards app URL will be: https://rewards.yourdomain.com/{slug}
