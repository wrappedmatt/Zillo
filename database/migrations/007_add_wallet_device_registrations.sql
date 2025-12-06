-- Migration: Add wallet device registrations table for Apple/Google Wallet push notifications
-- This table tracks which devices have added a customer's pass to their wallet

-- Create wallet_device_registrations table
CREATE TABLE IF NOT EXISTS wallet_device_registrations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    customer_id UUID NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    device_library_identifier VARCHAR(255) NOT NULL,
    push_token TEXT NOT NULL,
    wallet_type VARCHAR(20) NOT NULL DEFAULT 'apple' CHECK (wallet_type IN ('apple', 'google')),
    pass_identifier VARCHAR(255) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Create indexes for common queries
CREATE INDEX idx_wallet_device_registrations_customer_id ON wallet_device_registrations(customer_id);
CREATE INDEX idx_wallet_device_registrations_device_id ON wallet_device_registrations(device_library_identifier);
CREATE UNIQUE INDEX idx_wallet_device_registrations_device_pass ON wallet_device_registrations(device_library_identifier, pass_identifier);

-- Create updated_at trigger
CREATE TRIGGER update_wallet_device_registrations_updated_at
    BEFORE UPDATE ON wallet_device_registrations
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- Enable Row Level Security
ALTER TABLE wallet_device_registrations ENABLE ROW LEVEL SECURITY;

-- RLS Policies - allow service role full access (backend uses service role key)
CREATE POLICY "Service role can manage wallet registrations" ON wallet_device_registrations
    FOR ALL USING (true);
