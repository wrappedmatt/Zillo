-- Migration: Add Loyalty System Configuration
-- Date: 2025-11-28
-- Description: Adds support for both points-based and cashback loyalty systems with configurable cashback rules

-- =============================================================================
-- 1. ADD LOYALTY SYSTEM TYPE TO ACCOUNTS TABLE
-- =============================================================================

-- Add loyalty system type column (default to 'cashback')
ALTER TABLE accounts
ADD COLUMN IF NOT EXISTS loyalty_system_type VARCHAR(20) NOT NULL DEFAULT 'cashback' CHECK (loyalty_system_type IN ('points', 'cashback'));

-- =============================================================================
-- 2. ADD CASHBACK CONFIGURATION TO ACCOUNTS TABLE
-- =============================================================================

-- Cashback rate as a percentage (e.g., 5.00 means 5%)
ALTER TABLE accounts
ADD COLUMN IF NOT EXISTS cashback_rate DECIMAL(5, 2) DEFAULT 5.00 CHECK (cashback_rate >= 0 AND cashback_rate <= 100);

-- Historical period to reward in days (e.g., 14 means reward purchases from last 14 days)
ALTER TABLE accounts
ADD COLUMN IF NOT EXISTS historical_reward_days INTEGER DEFAULT 14 CHECK (historical_reward_days >= 0 AND historical_reward_days <= 365);

-- Welcome incentive amount in dollars (e.g., 5.00 means $5 welcome bonus)
ALTER TABLE accounts
ADD COLUMN IF NOT EXISTS welcome_incentive DECIMAL(10, 2) DEFAULT 5.00 CHECK (welcome_incentive >= 0);

-- =============================================================================
-- 3. UPDATE CUSTOMERS TABLE TO SUPPORT CASHBACK BALANCE
-- =============================================================================

-- Add cashback balance column (stored in dollars)
ALTER TABLE customers
ADD COLUMN IF NOT EXISTS cashback_balance DECIMAL(10, 2) DEFAULT 0.00 CHECK (cashback_balance >= 0);

-- Add flag to track if welcome incentive has been awarded
ALTER TABLE customers
ADD COLUMN IF NOT EXISTS welcome_incentive_awarded BOOLEAN DEFAULT FALSE;

-- Add timestamp for when customer linked their card
ALTER TABLE customers
ADD COLUMN IF NOT EXISTS card_linked_at TIMESTAMP WITH TIME ZONE;

-- =============================================================================
-- 4. UPDATE TRANSACTIONS TABLE TO SUPPORT BOTH LOYALTY TYPES
-- =============================================================================

-- Add cashback amount column (stored in dollars)
ALTER TABLE transactions
ADD COLUMN IF NOT EXISTS cashback_amount DECIMAL(10, 2) DEFAULT 0.00;

-- Update the type column to support new transaction types
-- Existing constraint will be replaced
ALTER TABLE transactions
DROP CONSTRAINT IF EXISTS transactions_type_check;

ALTER TABLE transactions
ADD CONSTRAINT transactions_type_check CHECK (type IN ('earn', 'redeem', 'adjustment', 'cashback_earn', 'cashback_redeem', 'welcome_bonus'));

-- =============================================================================
-- 5. UPDATE PAYMENTS TABLE TO TRACK CASHBACK
-- =============================================================================

-- Add cashback earned column to payments table
ALTER TABLE payments
ADD COLUMN IF NOT EXISTS cashback_earned DECIMAL(10, 2) DEFAULT 0.00;

-- =============================================================================
-- 6. CREATE INDEXES FOR PERFORMANCE
-- =============================================================================

-- Index for querying accounts by loyalty system type
CREATE INDEX IF NOT EXISTS idx_accounts_loyalty_system_type
ON accounts(loyalty_system_type);

-- Index for querying customers by card linked status
CREATE INDEX IF NOT EXISTS idx_customers_card_linked_at
ON customers(card_linked_at)
WHERE card_linked_at IS NOT NULL;

-- Index for querying customers by welcome incentive status
CREATE INDEX IF NOT EXISTS idx_customers_welcome_incentive
ON customers(welcome_incentive_awarded)
WHERE welcome_incentive_awarded = FALSE;

-- Index for transaction types including cashback
CREATE INDEX IF NOT EXISTS idx_transactions_type
ON transactions(type);

-- =============================================================================
-- 7. CREATE FUNCTION TO CALCULATE CASHBACK AMOUNT
-- =============================================================================

-- Function to calculate cashback based on account configuration
CREATE OR REPLACE FUNCTION calculate_cashback_amount(
    p_account_id UUID,
    p_purchase_amount DECIMAL
)
RETURNS DECIMAL AS $$
DECLARE
    v_cashback_rate DECIMAL;
    v_cashback_amount DECIMAL;
BEGIN
    -- Get the cashback rate for the account
    SELECT cashback_rate INTO v_cashback_rate
    FROM accounts
    WHERE id = p_account_id;

    -- Calculate cashback (purchase_amount * rate / 100)
    v_cashback_amount := ROUND(p_purchase_amount * v_cashback_rate / 100, 2);

    RETURN v_cashback_amount;
END;
$$ LANGUAGE plpgsql;

-- =============================================================================
-- 8. CREATE FUNCTION TO CALCULATE POINTS FROM AMOUNT
-- =============================================================================

-- Function to calculate points based on dollar amount (existing logic)
CREATE OR REPLACE FUNCTION calculate_points_from_amount(
    p_amount DECIMAL
)
RETURNS INTEGER AS $$
BEGIN
    -- 1 point per dollar spent (can be customized per account in future)
    RETURN FLOOR(p_amount)::INTEGER;
END;
$$ LANGUAGE plpgsql;

-- =============================================================================
-- 9. UPDATE TRIGGER FOR CUSTOMER BALANCE UPDATES
-- =============================================================================

-- Drop existing trigger
DROP TRIGGER IF EXISTS trg_update_customer_points_balance ON transactions;
DROP FUNCTION IF EXISTS update_customer_points_balance();

-- Create new trigger function that handles both points and cashback
CREATE OR REPLACE FUNCTION update_customer_balance()
RETURNS TRIGGER AS $$
DECLARE
    v_loyalty_system_type VARCHAR(20);
BEGIN
    -- Get the loyalty system type for this customer's account
    SELECT a.loyalty_system_type INTO v_loyalty_system_type
    FROM customers c
    JOIN accounts a ON c.account_id = a.id
    WHERE c.id = NEW.customer_id;

    -- Update balance based on loyalty system type
    IF v_loyalty_system_type = 'points' THEN
        -- Update points balance
        UPDATE customers
        SET points_balance = points_balance + NEW.points,
            updated_at = NOW()
        WHERE id = NEW.customer_id;

        RAISE NOTICE 'Updated customer % points balance by %', NEW.customer_id, NEW.points;
    ELSIF v_loyalty_system_type = 'cashback' THEN
        -- Update cashback balance
        UPDATE customers
        SET cashback_balance = cashback_balance + NEW.cashback_amount,
            updated_at = NOW()
        WHERE id = NEW.customer_id;

        RAISE NOTICE 'Updated customer % cashback balance by $%', NEW.customer_id, NEW.cashback_amount;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create the trigger
CREATE TRIGGER trg_update_customer_balance
AFTER INSERT ON transactions
FOR EACH ROW
EXECUTE FUNCTION update_customer_balance();

-- =============================================================================
-- 10. CREATE FUNCTION TO AWARD WELCOME INCENTIVE
-- =============================================================================

CREATE OR REPLACE FUNCTION award_welcome_incentive(
    p_customer_id UUID
)
RETURNS BOOLEAN AS $$
DECLARE
    v_account_id UUID;
    v_welcome_incentive DECIMAL;
    v_loyalty_system_type VARCHAR(20);
    v_already_awarded BOOLEAN;
BEGIN
    -- Check if welcome incentive has already been awarded
    SELECT welcome_incentive_awarded, account_id INTO v_already_awarded, v_account_id
    FROM customers
    WHERE id = p_customer_id;

    IF v_already_awarded THEN
        RAISE NOTICE 'Welcome incentive already awarded to customer %', p_customer_id;
        RETURN FALSE;
    END IF;

    -- Get account configuration
    SELECT welcome_incentive, loyalty_system_type INTO v_welcome_incentive, v_loyalty_system_type
    FROM accounts
    WHERE id = v_account_id;

    -- Only award if there's a welcome incentive configured
    IF v_welcome_incentive > 0 THEN
        IF v_loyalty_system_type = 'cashback' THEN
            -- Create cashback transaction
            INSERT INTO transactions (customer_id, account_id, type, cashback_amount, points, description)
            VALUES (p_customer_id, v_account_id, 'welcome_bonus', v_welcome_incentive, 0, 'Welcome bonus');
        ELSE
            -- Create points transaction (convert dollars to points)
            INSERT INTO transactions (customer_id, account_id, type, points, cashback_amount, description)
            VALUES (p_customer_id, v_account_id, 'welcome_bonus', FLOOR(v_welcome_incentive)::INTEGER, 0, 'Welcome bonus');
        END IF;

        -- Mark welcome incentive as awarded
        UPDATE customers
        SET welcome_incentive_awarded = TRUE
        WHERE id = p_customer_id;

        RETURN TRUE;
    END IF;

    RETURN FALSE;
END;
$$ LANGUAGE plpgsql;

-- =============================================================================
-- 11. ADD COMMENTS FOR DOCUMENTATION
-- =============================================================================

COMMENT ON COLUMN accounts.loyalty_system_type IS 'Type of loyalty system: "points" or "cashback"';
COMMENT ON COLUMN accounts.cashback_rate IS 'Cashback rate as a percentage (e.g., 5.00 = 5%)';
COMMENT ON COLUMN accounts.historical_reward_days IS 'Number of days to look back for rewarding historical purchases when customer links card';
COMMENT ON COLUMN accounts.welcome_incentive IS 'Welcome bonus amount in dollars awarded when customer links their card';

COMMENT ON COLUMN customers.cashback_balance IS 'Current cashback balance in dollars (for cashback loyalty system)';
COMMENT ON COLUMN customers.points_balance IS 'Current points balance (for points loyalty system)';
COMMENT ON COLUMN customers.welcome_incentive_awarded IS 'Whether the welcome incentive has been awarded to this customer';
COMMENT ON COLUMN customers.card_linked_at IS 'Timestamp when customer linked their payment card';

COMMENT ON COLUMN transactions.cashback_amount IS 'Cashback amount in dollars (for cashback loyalty system)';
COMMENT ON COLUMN transactions.points IS 'Points amount (for points loyalty system)';

COMMENT ON FUNCTION calculate_cashback_amount(UUID, DECIMAL) IS 'Calculates cashback amount based on account configuration and purchase amount';
COMMENT ON FUNCTION calculate_points_from_amount(DECIMAL) IS 'Calculates points based on dollar amount (1 point per dollar)';
COMMENT ON FUNCTION award_welcome_incentive(UUID) IS 'Awards welcome incentive to a customer when they link their card';

-- =============================================================================
-- 12. MIGRATION COMPLETE
-- =============================================================================

-- To verify the migration was successful, run:
-- SELECT loyalty_system_type, cashback_rate, historical_reward_days, welcome_incentive FROM accounts LIMIT 5;
-- SELECT cashback_balance, points_balance, welcome_incentive_awarded FROM customers LIMIT 5;
