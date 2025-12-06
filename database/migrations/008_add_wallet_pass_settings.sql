-- Migration: Add wallet pass settings to accounts table
-- These settings allow businesses to customize their Apple/Google Wallet passes

-- Add wallet pass settings columns
ALTER TABLE accounts ADD COLUMN IF NOT EXISTS wallet_pass_enabled BOOLEAN DEFAULT true;
ALTER TABLE accounts ADD COLUMN IF NOT EXISTS wallet_pass_description TEXT;
ALTER TABLE accounts ADD COLUMN IF NOT EXISTS wallet_pass_icon_url TEXT;
ALTER TABLE accounts ADD COLUMN IF NOT EXISTS wallet_pass_strip_url TEXT;
ALTER TABLE accounts ADD COLUMN IF NOT EXISTS wallet_pass_label_color VARCHAR(7) DEFAULT '#FFFFFF';
ALTER TABLE accounts ADD COLUMN IF NOT EXISTS wallet_pass_foreground_color VARCHAR(7) DEFAULT '#FFFFFF';

-- Add comments
COMMENT ON COLUMN accounts.wallet_pass_enabled IS 'Whether wallet pass feature is enabled for this account';
COMMENT ON COLUMN accounts.wallet_pass_description IS 'Description shown on the wallet pass (e.g., "Your loyalty card for Acme Coffee")';
COMMENT ON COLUMN accounts.wallet_pass_icon_url IS 'Square icon for the pass (ideally 87x87, 174x174, 261x261)';
COMMENT ON COLUMN accounts.wallet_pass_strip_url IS 'Strip image for Apple Wallet (ideally 375x123, 750x246, 1125x369)';
COMMENT ON COLUMN accounts.wallet_pass_label_color IS 'Label text color on the pass (hex color)';
COMMENT ON COLUMN accounts.wallet_pass_foreground_color IS 'Value text color on the pass (hex color)';
