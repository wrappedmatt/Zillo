-- Migration: Add Stripe Connect Support
-- Date: 2025-12-10
-- Description: Adds Stripe Connect fields to accounts table for multi-tenant payment processing

-- =============================================================================
-- 1. ADD STRIPE CONNECT FIELDS TO ACCOUNTS TABLE
-- =============================================================================

-- Stripe Connect Account ID (acct_xxx)
ALTER TABLE accounts
ADD COLUMN IF NOT EXISTS stripe_account_id VARCHAR(255) UNIQUE;

-- Onboarding status
-- 'not_started': Account has not begun Stripe Connect onboarding
-- 'pending': Onboarding started but not complete
-- 'complete': Account is fully onboarded and can accept payments
-- 'restricted': Account has restrictions (needs attention)
ALTER TABLE accounts
ADD COLUMN IF NOT EXISTS stripe_onboarding_status VARCHAR(20)
DEFAULT 'not_started';

-- Whether the account can currently accept charges
ALTER TABLE accounts
ADD COLUMN IF NOT EXISTS stripe_charges_enabled BOOLEAN DEFAULT FALSE;

-- Whether the account can receive payouts
ALTER TABLE accounts
ADD COLUMN IF NOT EXISTS stripe_payouts_enabled BOOLEAN DEFAULT FALSE;

-- Timestamp of last webhook update from Stripe
ALTER TABLE accounts
ADD COLUMN IF NOT EXISTS stripe_account_updated_at TIMESTAMP WITH TIME ZONE;

-- Application fee percentage (optional - for platform revenue)
ALTER TABLE accounts
ADD COLUMN IF NOT EXISTS platform_fee_percentage DECIMAL(5, 2) DEFAULT 0.00;

-- =============================================================================
-- 2. CREATE INDEXES
-- =============================================================================

CREATE INDEX IF NOT EXISTS idx_accounts_stripe_account_id
ON accounts(stripe_account_id)
WHERE stripe_account_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_accounts_stripe_onboarding_status
ON accounts(stripe_onboarding_status);

-- =============================================================================
-- 3. ADD STRIPE CONNECTED ACCOUNT TO PAYMENTS TABLE
-- =============================================================================
-- Track which connected account processed each payment

ALTER TABLE payments
ADD COLUMN IF NOT EXISTS stripe_connected_account_id VARCHAR(255);

CREATE INDEX IF NOT EXISTS idx_payments_stripe_connected_account_id
ON payments(stripe_connected_account_id)
WHERE stripe_connected_account_id IS NOT NULL;

-- =============================================================================
-- 4. ADD COMMENTS
-- =============================================================================

COMMENT ON COLUMN accounts.stripe_account_id IS 'Stripe Connect account ID (acct_xxx) for this merchant';
COMMENT ON COLUMN accounts.stripe_onboarding_status IS 'Current Stripe Connect onboarding status: not_started, pending, complete, restricted';
COMMENT ON COLUMN accounts.stripe_charges_enabled IS 'Whether this account can accept payments via Stripe';
COMMENT ON COLUMN accounts.stripe_payouts_enabled IS 'Whether this account can receive payouts from Stripe';
COMMENT ON COLUMN accounts.platform_fee_percentage IS 'Platform fee percentage deducted from each payment (0.00 = no fee)';
COMMENT ON COLUMN payments.stripe_connected_account_id IS 'The connected Stripe account that processed this payment';
