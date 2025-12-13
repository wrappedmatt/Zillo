-- Migration: Fix Account Users RLS Infinite Recursion
-- Date: 2025-12-13
-- Description: Fixes the infinite recursion error in account_users RLS policies
--              by using a security definer function instead of self-referencing queries

-- =============================================================================
-- 1. DROP PROBLEMATIC POLICIES
-- =============================================================================

DROP POLICY IF EXISTS "Owners can view account users" ON account_users;
DROP POLICY IF EXISTS "Owners can manage users" ON account_users;

-- =============================================================================
-- 2. CREATE SECURITY DEFINER FUNCTION TO CHECK OWNERSHIP
-- =============================================================================

-- This function bypasses RLS to check if a user is an owner of an account
-- SECURITY DEFINER means it runs with the privileges of the function creator
CREATE OR REPLACE FUNCTION is_account_owner(check_account_id UUID)
RETURNS BOOLEAN AS $$
BEGIN
    RETURN EXISTS (
        SELECT 1 FROM account_users
        WHERE account_id = check_account_id
        AND supabase_user_id = auth.uid()::text
        AND role = 'owner'
        AND is_active = true
    );
END;
$$ LANGUAGE plpgsql SECURITY DEFINER STABLE;

-- Grant execute permission to authenticated users
GRANT EXECUTE ON FUNCTION is_account_owner(UUID) TO authenticated;

-- =============================================================================
-- 3. CREATE NEW NON-RECURSIVE POLICIES
-- =============================================================================

-- Users can view their own account links (this policy is fine, keep it)
-- Already exists: "Users can view own account links"

-- Account owners can view all users of their accounts (using the function)
CREATE POLICY "Owners can view account users" ON account_users
    FOR SELECT USING (
        is_account_owner(account_id)
    );

-- Account owners can insert new users into their accounts
CREATE POLICY "Owners can insert account users" ON account_users
    FOR INSERT WITH CHECK (
        is_account_owner(account_id)
    );

-- Account owners can update users in their accounts
CREATE POLICY "Owners can update account users" ON account_users
    FOR UPDATE USING (
        is_account_owner(account_id)
    );

-- Account owners can delete users from their accounts
CREATE POLICY "Owners can delete account users" ON account_users
    FOR DELETE USING (
        is_account_owner(account_id)
    );

-- =============================================================================
-- 4. ADD COMMENT
-- =============================================================================

COMMENT ON FUNCTION is_account_owner(UUID) IS 'Checks if the current user is an owner of the specified account. Uses SECURITY DEFINER to bypass RLS and avoid infinite recursion.';
