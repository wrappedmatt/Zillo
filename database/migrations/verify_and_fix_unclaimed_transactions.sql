-- Verify and fix unclaimed_transactions table
-- Run this script in your Supabase SQL Editor

-- Check if unclaimed_transactions table exists
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'public'
        AND tablename = 'unclaimed_transactions'
    ) THEN
        RAISE NOTICE 'Creating unclaimed_transactions table...';

        -- Create the table
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

        -- Create indexes
        CREATE INDEX idx_unclaimed_transactions_fingerprint ON unclaimed_transactions(card_fingerprint);
        CREATE INDEX idx_unclaimed_transactions_account_id ON unclaimed_transactions(account_id);
        CREATE INDEX idx_unclaimed_transactions_claimed ON unclaimed_transactions(claimed_by_customer_id, claimed_at);

        -- Enable RLS
        ALTER TABLE unclaimed_transactions ENABLE ROW LEVEL SECURITY;

        -- Create policies
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

        RAISE NOTICE 'unclaimed_transactions table created successfully!';
    ELSE
        RAISE NOTICE 'unclaimed_transactions table already exists.';
    END IF;

    -- Check if cards table exists
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'public'
        AND tablename = 'cards'
    ) THEN
        RAISE NOTICE 'Creating cards table...';

        -- Create cards table
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

        -- Create indexes
        CREATE UNIQUE INDEX idx_cards_fingerprint_customer ON cards(card_fingerprint, customer_id);
        CREATE INDEX idx_cards_customer_id ON cards(customer_id);
        CREATE INDEX idx_cards_fingerprint ON cards(card_fingerprint);

        -- Create trigger for updated_at
        CREATE TRIGGER update_cards_updated_at BEFORE UPDATE ON cards
            FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

        -- Enable RLS
        ALTER TABLE cards ENABLE ROW LEVEL SECURITY;

        -- Create policies
        CREATE POLICY "Users can view cards of their customers" ON cards
            FOR SELECT USING (
                customer_id IN (
                    SELECT c.id FROM customers c
                    INNER JOIN accounts a ON c.account_id = a.id
                    WHERE a.supabase_user_id = auth.uid()::text
                )
            );

        CREATE POLICY "Service role can read all cards" ON cards
            FOR SELECT USING (auth.jwt() ->> 'role' = 'service_role');

        CREATE POLICY "Service role can insert cards" ON cards
            FOR INSERT WITH CHECK (auth.jwt() ->> 'role' = 'service_role');

        CREATE POLICY "Service role can update cards" ON cards
            FOR UPDATE USING (auth.jwt() ->> 'role' = 'service_role');

        RAISE NOTICE 'cards table created successfully!';
    ELSE
        RAISE NOTICE 'cards table already exists.';
    END IF;
END $$;

-- Verify tables exist
SELECT 'unclaimed_transactions' as table_name, COUNT(*) as row_count FROM unclaimed_transactions
UNION ALL
SELECT 'cards' as table_name, COUNT(*) as row_count FROM cards;
