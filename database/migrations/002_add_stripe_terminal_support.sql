-- Migration: Add Stripe Terminal Payment Tracking
-- Date: 2025-11-27
-- Description: Adds separate payments table and enhances loyalty tracking for Stripe Terminal integration

-- =============================================================================
-- 1. CREATE PAYMENTS TABLE
-- =============================================================================
-- This table tracks all Stripe Terminal payments, separate from loyalty transactions
CREATE TABLE IF NOT EXISTS payments (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
    customer_id UUID REFERENCES customers(id) ON DELETE SET NULL,

    -- Stripe identifiers
    stripe_payment_intent_id VARCHAR(255) NOT NULL UNIQUE,
    stripe_charge_id VARCHAR(255),

    -- Terminal information
    terminal_id VARCHAR(255),
    terminal_label VARCHAR(255),

    -- Payment amounts (in cents for Stripe, dollars for display)
    amount DECIMAL(10, 2) NOT NULL, -- Original transaction amount in dollars
    amount_charged DECIMAL(10, 2) NOT NULL, -- Amount actually charged (after loyalty redemption)
    loyalty_redeemed DECIMAL(10, 2) DEFAULT 0, -- Amount of loyalty discount applied
    loyalty_earned INTEGER DEFAULT 0, -- Points earned from this payment

    -- Payment status
    status VARCHAR(20) NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'completed', 'failed', 'refunded', 'partially_refunded')),

    -- Additional metadata from Stripe
    currency VARCHAR(3) DEFAULT 'nzd',
    payment_method_type VARCHAR(50),
    metadata JSONB DEFAULT '{}'::jsonb,

    -- Timestamps
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    completed_at TIMESTAMP WITH TIME ZONE,

    -- Audit fields
    refund_reason TEXT,
    refunded_at TIMESTAMP WITH TIME ZONE,
    refund_amount DECIMAL(10, 2)
);

-- =============================================================================
-- 2. UPDATE TRANSACTIONS TABLE
-- =============================================================================
-- Add fields to link transactions to payments and track Stripe payment intents
ALTER TABLE transactions
ADD COLUMN IF NOT EXISTS payment_id UUID REFERENCES payments(id) ON DELETE SET NULL,
ADD COLUMN IF NOT EXISTS stripe_payment_intent_id VARCHAR(255);

-- =============================================================================
-- 3. ADD UNIQUE CONSTRAINT ON CUSTOMERS
-- =============================================================================
-- Prevent duplicate customer emails within the same account
-- Using LOWER() to make email comparison case-insensitive
CREATE UNIQUE INDEX IF NOT EXISTS idx_customers_email_account_unique
ON customers(account_id, LOWER(email));

-- =============================================================================
-- 4. CREATE PERFORMANCE INDEXES
-- =============================================================================

-- Payments table indexes
CREATE INDEX IF NOT EXISTS idx_payments_stripe_payment_intent_id
ON payments(stripe_payment_intent_id);

CREATE INDEX IF NOT EXISTS idx_payments_customer_id
ON payments(customer_id)
WHERE customer_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_payments_account_id
ON payments(account_id);

CREATE INDEX IF NOT EXISTS idx_payments_terminal_id
ON payments(terminal_id)
WHERE terminal_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_payments_status
ON payments(status);

CREATE INDEX IF NOT EXISTS idx_payments_created_at
ON payments(created_at DESC);

-- Transactions table indexes for payment lookups
CREATE INDEX IF NOT EXISTS idx_transactions_payment_id
ON transactions(payment_id)
WHERE payment_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_transactions_stripe_payment_intent_id
ON transactions(stripe_payment_intent_id)
WHERE stripe_payment_intent_id IS NOT NULL;

-- Customer lookups by account and email
CREATE INDEX IF NOT EXISTS idx_customers_account_email
ON customers(account_id, email);

-- =============================================================================
-- 5. CREATE TRIGGER TO AUTO-UPDATE CUSTOMER POINTS BALANCE
-- =============================================================================
-- This trigger automatically updates the customer's points balance whenever a transaction is created
-- This eliminates race conditions and ensures consistency

-- Drop existing trigger if it exists (for idempotency)
DROP TRIGGER IF EXISTS trg_update_customer_points_balance ON transactions;
DROP FUNCTION IF EXISTS update_customer_points_balance();

-- Create the trigger function
CREATE OR REPLACE FUNCTION update_customer_points_balance()
RETURNS TRIGGER AS $$
BEGIN
    -- Update the customer's points balance by adding the transaction points
    UPDATE customers
    SET points_balance = points_balance + NEW.points,
        updated_at = NOW()
    WHERE id = NEW.customer_id;

    -- Log the update for debugging
    RAISE NOTICE 'Updated customer % points balance by %', NEW.customer_id, NEW.points;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create the trigger
CREATE TRIGGER trg_update_customer_points_balance
AFTER INSERT ON transactions
FOR EACH ROW
EXECUTE FUNCTION update_customer_points_balance();

-- =============================================================================
-- 6. CREATE POINTS BALANCE RECONCILIATION FUNCTION
-- =============================================================================
-- This function recalculates a customer's points balance from their transaction history
-- Useful for verifying data integrity or fixing sync issues

CREATE OR REPLACE FUNCTION calculate_customer_points_balance(customer_uuid UUID)
RETURNS INTEGER AS $$
DECLARE
    total_points INTEGER;
BEGIN
    SELECT COALESCE(SUM(points), 0) INTO total_points
    FROM transactions
    WHERE customer_id = customer_uuid;

    RETURN total_points;
END;
$$ LANGUAGE plpgsql;

-- Create a helper function to reconcile all customers' points balances
CREATE OR REPLACE FUNCTION reconcile_all_customer_points_balances()
RETURNS TABLE(customer_id UUID, old_balance INTEGER, calculated_balance INTEGER, difference INTEGER) AS $$
BEGIN
    RETURN QUERY
    SELECT
        c.id AS customer_id,
        c.points_balance AS old_balance,
        calculate_customer_points_balance(c.id) AS calculated_balance,
        (calculate_customer_points_balance(c.id) - c.points_balance) AS difference
    FROM customers c
    WHERE c.points_balance != calculate_customer_points_balance(c.id);
END;
$$ LANGUAGE plpgsql;

-- =============================================================================
-- 7. CREATE UPDATED_AT TRIGGER FOR PAYMENTS TABLE
-- =============================================================================
-- Automatically update the updated_at timestamp when a payment record changes

CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_payments_updated_at
BEFORE UPDATE ON payments
FOR EACH ROW
EXECUTE FUNCTION update_updated_at_column();

-- =============================================================================
-- 8. CREATE VIEWS FOR REPORTING
-- =============================================================================

-- View: Payment summary with customer information
CREATE OR REPLACE VIEW vw_payment_summary AS
SELECT
    p.id,
    p.stripe_payment_intent_id,
    p.account_id,
    p.customer_id,
    c.name AS customer_name,
    c.email AS customer_email,
    p.amount,
    p.amount_charged,
    p.loyalty_redeemed,
    p.loyalty_earned,
    p.status,
    p.terminal_id,
    p.terminal_label,
    p.currency,
    p.created_at,
    p.completed_at
FROM payments p
LEFT JOIN customers c ON p.customer_id = c.id;

-- View: Terminal performance metrics
CREATE OR REPLACE VIEW vw_terminal_metrics AS
SELECT
    terminal_id,
    terminal_label,
    COUNT(*) AS total_transactions,
    SUM(amount) AS total_revenue,
    SUM(amount_charged) AS total_charged,
    SUM(loyalty_redeemed) AS total_loyalty_redeemed,
    SUM(loyalty_earned) AS total_points_awarded,
    AVG(amount) AS avg_transaction_amount,
    COUNT(DISTINCT customer_id) AS unique_customers,
    MIN(created_at) AS first_transaction,
    MAX(created_at) AS last_transaction
FROM payments
WHERE status = 'completed'
GROUP BY terminal_id, terminal_label;

-- =============================================================================
-- 9. ADD COMMENTS FOR DOCUMENTATION
-- =============================================================================

COMMENT ON TABLE payments IS 'Tracks all Stripe Terminal payment transactions';
COMMENT ON COLUMN payments.stripe_payment_intent_id IS 'Unique identifier from Stripe PaymentIntent';
COMMENT ON COLUMN payments.amount IS 'Original transaction amount before loyalty redemption (in dollars)';
COMMENT ON COLUMN payments.amount_charged IS 'Actual amount charged to customer after loyalty redemption (in dollars)';
COMMENT ON COLUMN payments.loyalty_redeemed IS 'Dollar value of loyalty points redeemed (in dollars)';
COMMENT ON COLUMN payments.loyalty_earned IS 'Number of loyalty points earned from this payment';
COMMENT ON COLUMN payments.terminal_id IS 'Identifier of the terminal device that processed this payment';

COMMENT ON FUNCTION calculate_customer_points_balance(UUID) IS 'Calculates total points balance for a customer from transaction history';
COMMENT ON FUNCTION reconcile_all_customer_points_balances() IS 'Identifies customers whose points_balance does not match their transaction history';

-- =============================================================================
-- MIGRATION COMPLETE
-- =============================================================================

-- To verify the migration was successful, run:
-- SELECT * FROM payments LIMIT 1;
-- SELECT * FROM transactions WHERE payment_id IS NOT NULL LIMIT 1;
-- SELECT * FROM vw_payment_summary LIMIT 10;
-- SELECT * FROM vw_terminal_metrics;
