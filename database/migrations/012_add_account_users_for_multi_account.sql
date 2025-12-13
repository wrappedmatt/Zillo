-- Migration: Add Account Users for Multi-Account Support
-- Date: 2025-12-13
-- Description: Enables users to be linked to multiple accounts with role-based access

-- =============================================================================
-- 1. CREATE ACCOUNT USERS TABLE
-- =============================================================================

CREATE TABLE IF NOT EXISTS account_users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    supabase_user_id VARCHAR(255) NOT NULL,
    account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
    email VARCHAR(255) NOT NULL,

    -- Role-based access
    -- 'owner': Full access, can manage account settings and users
    -- 'admin': Can manage customers, transactions, terminals
    -- 'user': Read-only access to dashboard
    role VARCHAR(50) NOT NULL DEFAULT 'owner',

    -- Invitation tracking
    invited_at TIMESTAMP WITH TIME ZONE,
    invited_by UUID REFERENCES accounts(id),
    joined_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),

    -- Status
    is_active BOOLEAN DEFAULT true NOT NULL,

    -- Timestamps
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL,

    -- Constraints
    CONSTRAINT unique_user_account UNIQUE(supabase_user_id, account_id),
    CONSTRAINT chk_role CHECK (role IN ('owner', 'admin', 'user'))
);

-- =============================================================================
-- 2. CREATE INDEXES
-- =============================================================================

CREATE INDEX IF NOT EXISTS idx_account_users_supabase_user_id
ON account_users(supabase_user_id)
WHERE is_active = true;

CREATE INDEX IF NOT EXISTS idx_account_users_account_id
ON account_users(account_id)
WHERE is_active = true;

CREATE INDEX IF NOT EXISTS idx_account_users_email
ON account_users(email);

-- =============================================================================
-- 3. CREATE TRIGGER TO AUTO-UPDATE UPDATED_AT
-- =============================================================================

CREATE OR REPLACE FUNCTION update_account_users_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_account_users_updated_at ON account_users;

CREATE TRIGGER trg_account_users_updated_at
BEFORE UPDATE ON account_users
FOR EACH ROW
EXECUTE FUNCTION update_account_users_updated_at();

-- =============================================================================
-- 4. MIGRATE EXISTING ACCOUNT OWNERS
-- =============================================================================

-- For each existing account, create an owner record
INSERT INTO account_users (supabase_user_id, account_id, email, role, joined_at)
SELECT
    supabase_user_id,
    id AS account_id,
    email,
    'owner' AS role,
    created_at AS joined_at
FROM accounts
WHERE supabase_user_id IS NOT NULL
AND NOT EXISTS (
    SELECT 1 FROM account_users au
    WHERE au.supabase_user_id = accounts.supabase_user_id
    AND au.account_id = accounts.id
);

-- =============================================================================
-- 5. ADD RLS POLICIES FOR ACCOUNT USERS
-- =============================================================================

ALTER TABLE account_users ENABLE ROW LEVEL SECURITY;

-- Users can view their own account links
CREATE POLICY "Users can view own account links" ON account_users
    FOR SELECT USING (auth.uid()::text = supabase_user_id);

-- Account owners can view all users of their accounts
CREATE POLICY "Owners can view account users" ON account_users
    FOR SELECT USING (
        account_id IN (
            SELECT account_id FROM account_users
            WHERE supabase_user_id = auth.uid()::text
            AND role = 'owner'
            AND is_active = true
        )
    );

-- Account owners can manage users
CREATE POLICY "Owners can manage users" ON account_users
    FOR ALL USING (
        account_id IN (
            SELECT account_id FROM account_users
            WHERE supabase_user_id = auth.uid()::text
            AND role = 'owner'
            AND is_active = true
        )
    );

-- =============================================================================
-- 6. ADD COMMENTS
-- =============================================================================

COMMENT ON TABLE account_users IS 'Links users to accounts they have access to, enabling multi-account support';
COMMENT ON COLUMN account_users.supabase_user_id IS 'The Supabase auth user ID';
COMMENT ON COLUMN account_users.role IS 'User role: owner (full access), admin (manage data), user (read-only)';
COMMENT ON COLUMN account_users.invited_by IS 'The account ID of the user who sent the invitation';

-- =============================================================================
-- 7. CREATE VIEW FOR EASY QUERYING
-- =============================================================================

CREATE OR REPLACE VIEW vw_user_accounts AS
SELECT
    au.supabase_user_id,
    au.email AS user_email,
    au.role,
    au.is_active AS user_active,
    au.joined_at,
    a.id AS account_id,
    a.company_name,
    a.slug,
    a.stripe_onboarding_status,
    a.stripe_charges_enabled
FROM account_users au
JOIN accounts a ON au.account_id = a.id
WHERE au.is_active = true;
