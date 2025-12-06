-- Migration: Add Terminal Registration and Pairing System
-- Date: 2025-11-27
-- Description: Enables secure terminal-to-account pairing via one-time codes

-- =============================================================================
-- 1. CREATE TERMINALS TABLE
-- =============================================================================
CREATE TABLE IF NOT EXISTS terminals (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,

    -- Authentication
    api_key VARCHAR(255) NOT NULL UNIQUE,

    -- Terminal identification
    terminal_label VARCHAR(255) NOT NULL,
    stripe_terminal_id VARCHAR(255),
    device_model VARCHAR(255),
    device_id VARCHAR(255),

    -- Pairing information
    pairing_code VARCHAR(20) UNIQUE,
    pairing_expires_at TIMESTAMP WITH TIME ZONE,
    paired_at TIMESTAMP WITH TIME ZONE,

    -- Status tracking
    is_active BOOLEAN DEFAULT true NOT NULL,
    last_seen_at TIMESTAMP WITH TIME ZONE,

    -- Timestamps
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL,

    -- Constraints
    CONSTRAINT chk_api_key_format CHECK (api_key LIKE 'term_sk_%'),
    CONSTRAINT chk_pairing_code_format CHECK (
        pairing_code IS NULL OR
        (LENGTH(pairing_code) >= 4 AND LENGTH(pairing_code) <= 20)
    )
);

-- =============================================================================
-- 2. CREATE INDEXES FOR PERFORMANCE
-- =============================================================================

-- Primary lookup indexes
CREATE INDEX IF NOT EXISTS idx_terminals_api_key ON terminals(api_key) WHERE is_active = true;
CREATE INDEX IF NOT EXISTS idx_terminals_account_id ON terminals(account_id) WHERE is_active = true;
CREATE INDEX IF NOT EXISTS idx_terminals_pairing_code ON terminals(pairing_code) WHERE pairing_code IS NOT NULL;

-- Status and monitoring indexes
CREATE INDEX IF NOT EXISTS idx_terminals_last_seen ON terminals(last_seen_at DESC) WHERE is_active = true;
CREATE INDEX IF NOT EXISTS idx_terminals_active ON terminals(is_active, account_id);

-- Stripe terminal lookup
CREATE INDEX IF NOT EXISTS idx_terminals_stripe_id ON terminals(stripe_terminal_id) WHERE stripe_terminal_id IS NOT NULL;

-- =============================================================================
-- 3. CREATE PAIRING AUDIT LOG TABLE
-- =============================================================================
-- Tracks all pairing attempts for security monitoring
CREATE TABLE IF NOT EXISTS terminal_pairing_attempts (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    pairing_code VARCHAR(20) NOT NULL,
    terminal_label VARCHAR(255),
    device_id VARCHAR(255),
    ip_address INET,
    user_agent TEXT,
    success BOOLEAN NOT NULL,
    failure_reason TEXT,
    terminal_id UUID REFERENCES terminals(id) ON DELETE SET NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_pairing_attempts_code ON terminal_pairing_attempts(pairing_code);
CREATE INDEX IF NOT EXISTS idx_pairing_attempts_created ON terminal_pairing_attempts(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_pairing_attempts_success ON terminal_pairing_attempts(success, created_at);

-- =============================================================================
-- 4. CREATE TRIGGER TO AUTO-UPDATE UPDATED_AT
-- =============================================================================
CREATE OR REPLACE FUNCTION update_terminals_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_terminals_updated_at
BEFORE UPDATE ON terminals
FOR EACH ROW
EXECUTE FUNCTION update_terminals_updated_at();

-- =============================================================================
-- 5. CREATE FUNCTIONS FOR TERMINAL MANAGEMENT
-- =============================================================================

-- Function to generate unique pairing code
CREATE OR REPLACE FUNCTION generate_pairing_code()
RETURNS VARCHAR(20) AS $$
DECLARE
    code VARCHAR(20);
    code_exists BOOLEAN;
    attempts INT := 0;
    max_attempts INT := 100;
BEGIN
    LOOP
        -- Generate random 4-digit code formatted as PAIR-XXXX
        code := 'PAIR-' || LPAD(FLOOR(RANDOM() * 10000)::TEXT, 4, '0');

        -- Check if code already exists and is not expired
        SELECT EXISTS(
            SELECT 1 FROM terminals
            WHERE pairing_code = code
            AND (pairing_expires_at IS NULL OR pairing_expires_at > NOW())
        ) INTO code_exists;

        -- If code doesn't exist or is expired, we can use it
        EXIT WHEN NOT code_exists;

        attempts := attempts + 1;
        IF attempts >= max_attempts THEN
            RAISE EXCEPTION 'Unable to generate unique pairing code after % attempts', max_attempts;
        END IF;
    END LOOP;

    RETURN code;
END;
$$ LANGUAGE plpgsql;

-- Function to generate secure API key
CREATE OR REPLACE FUNCTION generate_terminal_api_key()
RETURNS VARCHAR(255) AS $$
DECLARE
    key VARCHAR(255);
    key_exists BOOLEAN;
    random_part TEXT;
    attempts INT := 0;
    max_attempts INT := 100;
BEGIN
    LOOP
        -- Generate random string (32 characters)
        SELECT STRING_AGG(
            SUBSTRING('abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789',
                     (RANDOM() * 61 + 1)::INT, 1),
            ''
        )
        FROM generate_series(1, 32)
        INTO random_part;

        -- Format as term_sk_live_...
        key := 'term_sk_live_' || random_part;

        -- Check if key already exists
        SELECT EXISTS(SELECT 1 FROM terminals WHERE api_key = key) INTO key_exists;

        EXIT WHEN NOT key_exists;

        attempts := attempts + 1;
        IF attempts >= max_attempts THEN
            RAISE EXCEPTION 'Unable to generate unique API key after % attempts', max_attempts;
        END IF;
    END LOOP;

    RETURN key;
END;
$$ LANGUAGE plpgsql;

-- Function to clean up expired pairing codes (call periodically)
CREATE OR REPLACE FUNCTION cleanup_expired_pairing_codes()
RETURNS INT AS $$
DECLARE
    deleted_count INT;
BEGIN
    -- Delete unpaired terminals with expired pairing codes
    DELETE FROM terminals
    WHERE paired_at IS NULL
    AND pairing_expires_at < NOW()
    AND created_at < NOW() - INTERVAL '24 hours';

    GET DIAGNOSTICS deleted_count = ROW_COUNT;

    RETURN deleted_count;
END;
$$ LANGUAGE plpgsql;

-- =============================================================================
-- 6. CREATE VIEWS FOR TERMINAL MONITORING
-- =============================================================================

-- View: Active terminals with account information
CREATE OR REPLACE VIEW vw_active_terminals AS
SELECT
    t.id,
    t.account_id,
    a.company_name AS account_name,
    a.slug AS account_slug,
    t.terminal_label,
    t.stripe_terminal_id,
    t.device_model,
    t.is_active,
    t.last_seen_at,
    t.paired_at,
    t.created_at,
    CASE
        WHEN t.last_seen_at > NOW() - INTERVAL '5 minutes' THEN 'online'
        WHEN t.last_seen_at > NOW() - INTERVAL '1 hour' THEN 'idle'
        ELSE 'offline'
    END AS status
FROM terminals t
JOIN accounts a ON t.account_id = a.id
WHERE t.is_active = true
ORDER BY t.last_seen_at DESC NULLS LAST;

-- View: Terminal activity summary
CREATE OR REPLACE VIEW vw_terminal_activity AS
SELECT
    t.id AS terminal_id,
    t.terminal_label,
    t.account_id,
    COUNT(p.id) AS total_payments,
    SUM(p.amount_charged) AS total_revenue,
    MIN(p.created_at) AS first_payment,
    MAX(p.created_at) AS last_payment,
    COUNT(DISTINCT p.customer_id) AS unique_customers,
    AVG(p.amount_charged) AS avg_transaction_amount
FROM terminals t
LEFT JOIN payments p ON p.terminal_id = t.id::TEXT AND p.status = 'completed'
WHERE t.is_active = true
GROUP BY t.id, t.terminal_label, t.account_id;

-- View: Pairing attempts monitoring (security)
CREATE OR REPLACE VIEW vw_pairing_security AS
SELECT
    pairing_code,
    COUNT(*) AS total_attempts,
    SUM(CASE WHEN success THEN 1 ELSE 0 END) AS successful_attempts,
    SUM(CASE WHEN NOT success THEN 1 ELSE 0 END) AS failed_attempts,
    MAX(created_at) AS last_attempt,
    ARRAY_AGG(DISTINCT ip_address) FILTER (WHERE ip_address IS NOT NULL) AS ip_addresses,
    ARRAY_AGG(DISTINCT failure_reason) FILTER (WHERE failure_reason IS NOT NULL) AS failure_reasons
FROM terminal_pairing_attempts
GROUP BY pairing_code
HAVING SUM(CASE WHEN NOT success THEN 1 ELSE 0 END) > 0
ORDER BY MAX(created_at) DESC;

-- =============================================================================
-- 7. ADD COMMENTS FOR DOCUMENTATION
-- =============================================================================

COMMENT ON TABLE terminals IS 'Registered POS terminals with secure pairing and authentication';
COMMENT ON COLUMN terminals.api_key IS 'Secure API key for terminal authentication (term_sk_live_...)';
COMMENT ON COLUMN terminals.pairing_code IS 'One-time pairing code for terminal registration (PAIR-XXXX)';
COMMENT ON COLUMN terminals.pairing_expires_at IS 'Pairing code expiration (typically 5 minutes from generation)';
COMMENT ON COLUMN terminals.last_seen_at IS 'Last time terminal made an API request (for online/offline status)';

COMMENT ON TABLE terminal_pairing_attempts IS 'Audit log of all terminal pairing attempts for security monitoring';

COMMENT ON FUNCTION generate_pairing_code() IS 'Generates unique 4-digit pairing code formatted as PAIR-XXXX';
COMMENT ON FUNCTION generate_terminal_api_key() IS 'Generates secure API key with format term_sk_live_...';
COMMENT ON FUNCTION cleanup_expired_pairing_codes() IS 'Removes unpaired terminals with expired pairing codes older than 24 hours';

-- =============================================================================
-- 8. UPDATE PAYMENTS TABLE TO LINK TERMINALS
-- =============================================================================

-- Ensure terminal_id in payments references terminals table
-- Note: terminal_id is currently VARCHAR, we'll keep it that way for flexibility
-- but add a lookup function

CREATE OR REPLACE FUNCTION validate_payment_terminal()
RETURNS TRIGGER AS $$
BEGIN
    -- If terminal_id is provided, verify it exists and is active
    IF NEW.terminal_id IS NOT NULL AND NEW.terminal_id != '' THEN
        IF NOT EXISTS(
            SELECT 1 FROM terminals
            WHERE id::TEXT = NEW.terminal_id
            AND is_active = true
        ) THEN
            RAISE EXCEPTION 'Invalid or inactive terminal_id: %', NEW.terminal_id;
        END IF;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Optional: Uncomment to enforce terminal validation on payments
-- CREATE TRIGGER trg_validate_payment_terminal
-- BEFORE INSERT OR UPDATE ON payments
-- FOR EACH ROW
-- EXECUTE FUNCTION validate_payment_terminal();

-- =============================================================================
-- 9. CREATE SCHEDULED JOB SETUP (for cleanup)
-- =============================================================================

-- Example: Call this from a cron job or scheduled task
COMMENT ON FUNCTION cleanup_expired_pairing_codes() IS
'Run this periodically (e.g., hourly) via pg_cron or external scheduler:
SELECT cleanup_expired_pairing_codes();';

-- =============================================================================
-- MIGRATION COMPLETE
-- =============================================================================

-- To verify the migration was successful, run:
-- SELECT * FROM terminals LIMIT 1;
-- SELECT * FROM terminal_pairing_attempts LIMIT 1;
-- SELECT * FROM vw_active_terminals;
-- SELECT generate_pairing_code();
-- SELECT generate_terminal_api_key();
