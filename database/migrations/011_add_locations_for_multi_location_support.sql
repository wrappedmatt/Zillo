-- Migration: Add Locations Table for Multi-Location Support
-- Date: 2025-12-11
-- Description: Adds locations table to support accounts with multiple physical locations,
--              each with their own Stripe Terminal Location and multiple terminals

-- =============================================================================
-- 1. CREATE OR UPDATE LOCATIONS TABLE
-- =============================================================================

-- Create table if it doesn't exist (with basic structure)
CREATE TABLE IF NOT EXISTS locations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL,
    is_active BOOLEAN DEFAULT true NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL
);

-- Add new columns if they don't exist (for existing tables)
ALTER TABLE locations ADD COLUMN IF NOT EXISTS address_line1 VARCHAR(255);
ALTER TABLE locations ADD COLUMN IF NOT EXISTS address_line2 VARCHAR(255);
ALTER TABLE locations ADD COLUMN IF NOT EXISTS city VARCHAR(100);
ALTER TABLE locations ADD COLUMN IF NOT EXISTS state VARCHAR(100);
ALTER TABLE locations ADD COLUMN IF NOT EXISTS postal_code VARCHAR(20);
ALTER TABLE locations ADD COLUMN IF NOT EXISTS country VARCHAR(2) DEFAULT 'US';
ALTER TABLE locations ADD COLUMN IF NOT EXISTS stripe_terminal_location_id VARCHAR(255);

-- Make latitude/longitude nullable (they were NOT NULL in old schema)
ALTER TABLE locations ADD COLUMN IF NOT EXISTS latitude DOUBLE PRECISION;
ALTER TABLE locations ADD COLUMN IF NOT EXISTS longitude DOUBLE PRECISION;
ALTER TABLE locations ADD COLUMN IF NOT EXISTS relevant_distance DOUBLE PRECISION;

-- If columns already exist, make them nullable
ALTER TABLE locations ALTER COLUMN latitude DROP NOT NULL;
ALTER TABLE locations ALTER COLUMN longitude DROP NOT NULL;

-- =============================================================================
-- 1B. MIGRATE DATA FROM ACCOUNTS TO LOCATIONS (IF NEEDED)
-- =============================================================================

-- If accounts.stripe_terminal_location_id exists, migrate it to the default location
-- This handles the case where migration 010 was run
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'accounts'
        AND column_name = 'stripe_terminal_location_id'
    ) THEN
        -- Update default locations with the terminal location ID from accounts
        UPDATE locations l
        SET stripe_terminal_location_id = a.stripe_terminal_location_id
        FROM accounts a
        WHERE l.account_id = a.id
        AND a.stripe_terminal_location_id IS NOT NULL
        AND l.stripe_terminal_location_id IS NULL;

        -- Drop the column from accounts since it's now in locations
        ALTER TABLE accounts DROP COLUMN IF EXISTS stripe_terminal_location_id;

        -- Drop the index if it exists
        DROP INDEX IF EXISTS idx_accounts_stripe_terminal_location_id;
    END IF;
END $$;

-- =============================================================================
-- 2. ADD LOCATION REFERENCE TO TERMINALS TABLE
-- =============================================================================

-- Add location_id to terminals (nullable for backwards compatibility)
ALTER TABLE terminals
ADD COLUMN IF NOT EXISTS location_id UUID REFERENCES locations(id) ON DELETE SET NULL;

-- =============================================================================
-- 3. CREATE INDEXES
-- =============================================================================

CREATE INDEX IF NOT EXISTS idx_locations_account_id
ON locations(account_id)
WHERE is_active = true;

CREATE INDEX IF NOT EXISTS idx_locations_stripe_terminal_location_id
ON locations(stripe_terminal_location_id)
WHERE stripe_terminal_location_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_terminals_location_id
ON terminals(location_id)
WHERE location_id IS NOT NULL;

-- =============================================================================
-- 4. CREATE TRIGGER TO AUTO-UPDATE UPDATED_AT
-- =============================================================================

CREATE OR REPLACE FUNCTION update_locations_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Drop and recreate trigger (CREATE OR REPLACE doesn't work for triggers)
DROP TRIGGER IF EXISTS trg_locations_updated_at ON locations;

CREATE TRIGGER trg_locations_updated_at
BEFORE UPDATE ON locations
FOR EACH ROW
EXECUTE FUNCTION update_locations_updated_at();

-- =============================================================================
-- 5. CREATE DEFAULT LOCATION FOR EXISTING ACCOUNTS
-- =============================================================================

-- For each existing account, create a default location
INSERT INTO locations (account_id, name, is_active)
SELECT
    id,
    COALESCE(company_name, 'Main Location') AS name,
    true
FROM accounts
WHERE NOT EXISTS (
    SELECT 1 FROM locations WHERE locations.account_id = accounts.id
);

-- Link existing terminals to their account's default location
UPDATE terminals t
SET location_id = (
    SELECT l.id
    FROM locations l
    WHERE l.account_id = t.account_id
    LIMIT 1
)
WHERE location_id IS NULL;

-- =============================================================================
-- 6. CREATE VIEWS FOR LOCATION MONITORING
-- =============================================================================

-- View: Locations with terminal counts
CREATE OR REPLACE VIEW vw_locations_summary AS
SELECT
    l.id,
    l.account_id,
    a.company_name AS account_name,
    l.name AS location_name,
    l.city,
    l.state,
    l.stripe_terminal_location_id,
    l.is_active,
    COUNT(t.id) AS terminal_count,
    SUM(CASE WHEN t.is_active THEN 1 ELSE 0 END) AS active_terminal_count,
    l.created_at,
    l.updated_at
FROM locations l
JOIN accounts a ON l.account_id = a.id
LEFT JOIN terminals t ON t.location_id = l.id
GROUP BY l.id, l.account_id, a.company_name, l.name, l.city, l.state,
         l.stripe_terminal_location_id, l.is_active, l.created_at, l.updated_at;

-- =============================================================================
-- 7. ADD COMMENTS
-- =============================================================================

COMMENT ON TABLE locations IS 'Physical business locations for accounts, each with their own Stripe Terminal Location';
COMMENT ON COLUMN locations.stripe_terminal_location_id IS 'Stripe Terminal Location ID (tml_xxx) created in the connected account for this physical location';
COMMENT ON COLUMN terminals.location_id IS 'The physical location where this terminal is deployed';

-- =============================================================================
-- 8. ADD CONSTRAINT (OPTIONAL - UNCOMMENT IF DESIRED)
-- =============================================================================

-- Ensure each account has at least one active location
-- Uncomment if you want to enforce this:
-- ALTER TABLE accounts
-- ADD CONSTRAINT chk_account_has_location
-- CHECK (
--     EXISTS (
--         SELECT 1 FROM locations
--         WHERE locations.account_id = accounts.id
--         AND locations.is_active = true
--     )
-- );
