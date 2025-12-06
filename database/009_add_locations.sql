-- Migration: Add locations table for Apple Wallet pass geofencing
-- This allows passes to show notifications when customers are near store locations

-- Create locations table
CREATE TABLE IF NOT EXISTS locations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
    name TEXT NOT NULL,
    address TEXT,
    latitude DOUBLE PRECISION NOT NULL,
    longitude DOUBLE PRECISION NOT NULL,
    relevant_distance DOUBLE PRECISION DEFAULT 100, -- Distance in meters for triggering notifications
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Create index for faster lookups by account
CREATE INDEX IF NOT EXISTS idx_locations_account_id ON locations(account_id);

-- Create index for active locations
CREATE INDEX IF NOT EXISTS idx_locations_active ON locations(account_id, is_active) WHERE is_active = true;

-- Enable RLS
ALTER TABLE locations ENABLE ROW LEVEL SECURITY;

-- RLS policy: Users can only access locations for their own account
CREATE POLICY "Users can view their own locations"
    ON locations FOR SELECT
    USING (
        account_id IN (
            SELECT id FROM accounts WHERE supabase_user_id = auth.uid()::text
        )
    );

CREATE POLICY "Users can insert their own locations"
    ON locations FOR INSERT
    WITH CHECK (
        account_id IN (
            SELECT id FROM accounts WHERE supabase_user_id = auth.uid()::text
        )
    );

CREATE POLICY "Users can update their own locations"
    ON locations FOR UPDATE
    USING (
        account_id IN (
            SELECT id FROM accounts WHERE supabase_user_id = auth.uid()::text
        )
    );

CREATE POLICY "Users can delete their own locations"
    ON locations FOR DELETE
    USING (
        account_id IN (
            SELECT id FROM accounts WHERE supabase_user_id = auth.uid()::text
        )
    );

-- Service role bypass policy (for API access)
CREATE POLICY "Service role can access all locations"
    ON locations FOR ALL
    USING (auth.role() = 'service_role');
