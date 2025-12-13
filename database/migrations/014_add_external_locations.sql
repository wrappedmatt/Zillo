-- Migration: Add External Locations for Partner Platform Support
-- Date: 2025-12-13
-- Description: Adds external_locations table to map Stripe Terminal Location IDs from partner
--              platforms (e.g., Lightspeed) to Zillo accounts. This enables the partner model
--              where a platform deploys the Zillo APK to their merchants' S700 devices.

-- =============================================================================
-- 0. ADD TERMINAL_INTEGRATION_MODE TO ACCOUNTS
-- =============================================================================

-- "zillo" = Model A - Zillo manages Stripe Connect and terminals (default)
-- "external" = Model B - External platform (Lightspeed, etc.) manages payments, Zillo only does loyalty
ALTER TABLE accounts ADD COLUMN IF NOT EXISTS terminal_integration_mode VARCHAR(20) DEFAULT 'zillo' NOT NULL;

COMMENT ON COLUMN accounts.terminal_integration_mode IS 'Terminal integration mode: "zillo" for Zillo-managed payments, "external" for partner platform payments (e.g., Lightspeed)';

-- =============================================================================
-- 1. CREATE EXTERNAL_LOCATIONS TABLE
-- =============================================================================

CREATE TABLE IF NOT EXISTS external_locations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
    stripe_location_id VARCHAR(255) NOT NULL,  -- tml_xxx from partner's Stripe account
    platform_name VARCHAR(100),                 -- "Lightspeed", "Toast", etc. (optional)
    label VARCHAR(255),                         -- Human-readable label like "Downtown Store"
    is_active BOOLEAN DEFAULT true NOT NULL,
    last_seen_at TIMESTAMP WITH TIME ZONE,     -- Last time a terminal at this location checked in
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL
);

-- =============================================================================
-- 2. CREATE INDEXES
-- =============================================================================

-- Fast lookup by Stripe Location ID (main use case for terminal identification)
CREATE UNIQUE INDEX IF NOT EXISTS idx_external_locations_stripe_location_id
ON external_locations(stripe_location_id);

-- List external locations for an account
CREATE INDEX IF NOT EXISTS idx_external_locations_account_id
ON external_locations(account_id)
WHERE is_active = true;

-- =============================================================================
-- 3. CREATE TRIGGER TO AUTO-UPDATE UPDATED_AT
-- =============================================================================

CREATE OR REPLACE FUNCTION update_external_locations_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_external_locations_updated_at ON external_locations;

CREATE TRIGGER trg_external_locations_updated_at
BEFORE UPDATE ON external_locations
FOR EACH ROW
EXECUTE FUNCTION update_external_locations_updated_at();

-- =============================================================================
-- 4. ADD ROW LEVEL SECURITY
-- =============================================================================

ALTER TABLE external_locations ENABLE ROW LEVEL SECURITY;

-- Users can view external locations for accounts they belong to
CREATE POLICY "Users can view own account external locations" ON external_locations
    FOR SELECT USING (
        account_id IN (
            SELECT account_id FROM account_users
            WHERE supabase_user_id = auth.uid()::text
            AND is_active = true
        )
    );

-- Account owners can manage external locations
CREATE POLICY "Owners can insert external locations" ON external_locations
    FOR INSERT WITH CHECK (
        is_account_owner(account_id)
    );

CREATE POLICY "Owners can update external locations" ON external_locations
    FOR UPDATE USING (
        is_account_owner(account_id)
    );

CREATE POLICY "Owners can delete external locations" ON external_locations
    FOR DELETE USING (
        is_account_owner(account_id)
    );

-- =============================================================================
-- 5. ADD COMMENTS
-- =============================================================================

COMMENT ON TABLE external_locations IS 'Maps Stripe Terminal Location IDs from partner platforms to Zillo accounts for the partner deployment model';
COMMENT ON COLUMN external_locations.stripe_location_id IS 'Stripe Terminal Location ID (tml_xxx) from the partner platform (e.g., Lightspeed)';
COMMENT ON COLUMN external_locations.platform_name IS 'Name of the partner platform deploying the Zillo app (optional, for reference)';
COMMENT ON COLUMN external_locations.label IS 'Human-readable label for this location (e.g., "Downtown Store")';
COMMENT ON COLUMN external_locations.last_seen_at IS 'Last time a terminal at this location called the identify endpoint';
