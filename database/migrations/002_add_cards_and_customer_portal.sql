-- Add unclaimed_transactions table to track points before customer registration
CREATE TABLE unclaimed_transactions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
    card_fingerprint VARCHAR(255) NOT NULL,
    points INTEGER NOT NULL,
    amount DECIMAL(10, 2),
    description TEXT NOT NULL,
    payment_id UUID,
    stripe_payment_intent_id VARCHAR(255),
    claimed_by_customer_id UUID REFERENCES customers(id) ON DELETE SET NULL,
    claimed_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX idx_unclaimed_transactions_fingerprint ON unclaimed_transactions(card_fingerprint);
CREATE INDEX idx_unclaimed_transactions_account_id ON unclaimed_transactions(account_id);
CREATE INDEX idx_unclaimed_transactions_claimed ON unclaimed_transactions(claimed_by_customer_id, claimed_at);

-- Add cards table to track registered cards per customer
CREATE TABLE cards (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    customer_id UUID NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    card_fingerprint VARCHAR(255) NOT NULL,
    card_last4 VARCHAR(4),
    card_brand VARCHAR(50),
    card_exp_month INTEGER,
    card_exp_year INTEGER,
    is_primary BOOLEAN DEFAULT false,
    first_used_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    last_used_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Create unique index on card_fingerprint and customer_id
CREATE UNIQUE INDEX idx_cards_fingerprint_customer ON cards(card_fingerprint, customer_id);
CREATE INDEX idx_cards_customer_id ON cards(customer_id);
CREATE INDEX idx_cards_fingerprint ON cards(card_fingerprint);

-- Create trigger for cards updated_at
CREATE TRIGGER update_cards_updated_at BEFORE UPDATE ON cards
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- Enable RLS for cards
ALTER TABLE cards ENABLE ROW LEVEL SECURITY;

-- RLS Policies for cards
CREATE POLICY "Users can view cards of their customers" ON cards
    FOR SELECT USING (
        customer_id IN (
            SELECT c.id FROM customers c
            INNER JOIN accounts a ON c.account_id = a.id
            WHERE a.supabase_user_id = auth.uid()::text
        )
    );

CREATE POLICY "Users can insert cards for their customers" ON cards
    FOR INSERT WITH CHECK (
        customer_id IN (
            SELECT c.id FROM customers c
            INNER JOIN accounts a ON c.account_id = a.id
            WHERE a.supabase_user_id = auth.uid()::text
        )
    );

CREATE POLICY "Users can update cards of their customers" ON cards
    FOR UPDATE USING (
        customer_id IN (
            SELECT c.id FROM customers c
            INNER JOIN accounts a ON c.account_id = a.id
            WHERE a.supabase_user_id = auth.uid()::text
        )
    );

CREATE POLICY "Users can delete cards of their customers" ON cards
    FOR DELETE USING (
        customer_id IN (
            SELECT c.id FROM customers c
            INNER JOIN accounts a ON c.account_id = a.id
            WHERE a.supabase_user_id = auth.uid()::text
        )
    );

-- Add signup_bonus_points to accounts table
ALTER TABLE accounts ADD COLUMN signup_bonus_points INTEGER DEFAULT 100;

-- Add customer portal access
ALTER TABLE customers ADD COLUMN portal_token VARCHAR(255) UNIQUE;
ALTER TABLE customers ADD COLUMN portal_token_expires_at TIMESTAMP WITH TIME ZONE;

CREATE INDEX idx_customers_portal_token ON customers(portal_token);

-- Allow service role to bypass RLS for customer lookups by card
CREATE POLICY "Service role can read all customers" ON customers
    FOR SELECT USING (auth.jwt() ->> 'role' = 'service_role');

CREATE POLICY "Service role can read all cards" ON cards
    FOR SELECT USING (auth.jwt() ->> 'role' = 'service_role');

CREATE POLICY "Service role can insert cards" ON cards
    FOR INSERT WITH CHECK (auth.jwt() ->> 'role' = 'service_role');

CREATE POLICY "Service role can update cards" ON cards
    FOR UPDATE USING (auth.jwt() ->> 'role' = 'service_role');

-- Enable RLS for unclaimed_transactions
ALTER TABLE unclaimed_transactions ENABLE ROW LEVEL SECURITY;

-- RLS Policies for unclaimed_transactions
CREATE POLICY "Users can view unclaimed transactions for their account" ON unclaimed_transactions
    FOR SELECT USING (
        account_id IN (SELECT id FROM accounts WHERE supabase_user_id = auth.uid()::text)
    );

CREATE POLICY "Service role can read all unclaimed transactions" ON unclaimed_transactions
    FOR SELECT USING (auth.jwt() ->> 'role' = 'service_role');

CREATE POLICY "Service role can insert unclaimed transactions" ON unclaimed_transactions
    FOR INSERT WITH CHECK (auth.jwt() ->> 'role' = 'service_role');

CREATE POLICY "Service role can update unclaimed transactions" ON unclaimed_transactions
    FOR UPDATE USING (auth.jwt() ->> 'role' = 'service_role');
