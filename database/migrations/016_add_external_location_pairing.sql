-- Migration: Add Pairing Codes and Transfer Support for External Locations
-- Date: 2025-12-13
-- Description: Adds pairing_code column to external_locations for simplified terminal setup,
--              and supports transferring external locations between Zillo accounts.

-- =============================================================================
-- 1. ADD PAIRING CODE COLUMN
-- =============================================================================

-- Pairing code is a simple, globally unique 6-character alphanumeric code
-- that can be used instead of the full Stripe Location ID for easier setup
ALTER TABLE external_locations ADD COLUMN IF NOT EXISTS pairing_code VARCHAR(10);

-- Pairing codes don't expire for external locations (unlike terminal pairing codes)
-- They're regenerated manually by the account owner

-- =============================================================================
-- 2. CREATE UNIQUE INDEX ON PAIRING CODE
-- =============================================================================

-- Ensure pairing codes are globally unique (only for non-null values)
CREATE UNIQUE INDEX IF NOT EXISTS idx_external_locations_pairing_code
ON external_locations(pairing_code)
WHERE pairing_code IS NOT NULL;

-- =============================================================================
-- 3. ADD COMMENTS
-- =============================================================================

COMMENT ON COLUMN external_locations.pairing_code IS 'Simple 6-character alphanumeric code for easier terminal pairing (globally unique)';

-- =============================================================================
-- 4. FUNCTION TO GENERATE PAIRING CODE
-- =============================================================================

-- Function to generate a random 6-character alphanumeric pairing code
CREATE OR REPLACE FUNCTION generate_external_location_pairing_code()
RETURNS VARCHAR(10) AS $$
DECLARE
    chars TEXT := 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'; -- Excludes confusing chars: I, O, 0, 1
    result VARCHAR(10) := '';
    i INTEGER;
    attempts INTEGER := 0;
    max_attempts INTEGER := 100;
BEGIN
    LOOP
        -- Generate 6-character code
        result := '';
        FOR i IN 1..6 LOOP
            result := result || substr(chars, floor(random() * length(chars) + 1)::integer, 1);
        END LOOP;

        -- Check if code already exists
        IF NOT EXISTS (SELECT 1 FROM external_locations WHERE pairing_code = result) THEN
            RETURN result;
        END IF;

        attempts := attempts + 1;
        IF attempts >= max_attempts THEN
            RAISE EXCEPTION 'Unable to generate unique pairing code after % attempts', max_attempts;
        END IF;
    END LOOP;
END;
$$ LANGUAGE plpgsql;

-- =============================================================================
-- 5. GENERATE PAIRING CODES FOR EXISTING EXTERNAL LOCATIONS
-- =============================================================================

-- Auto-generate pairing codes for any existing external locations without one
UPDATE external_locations
SET pairing_code = generate_external_location_pairing_code()
WHERE pairing_code IS NULL;
