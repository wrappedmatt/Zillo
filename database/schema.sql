-- Lemonade App Database Schema for Supabase

-- Create accounts table
CREATE TABLE accounts (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    email VARCHAR(255) NOT NULL UNIQUE,
    company_name VARCHAR(255) NOT NULL,
    slug VARCHAR(255) NOT NULL UNIQUE,
    supabase_user_id VARCHAR(255) NOT NULL UNIQUE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Create customers table
CREATE TABLE customers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL,
    email VARCHAR(255) NOT NULL,
    phone_number VARCHAR(50),
    points_balance INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Create transactions table
CREATE TABLE transactions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    customer_id UUID NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    points INTEGER NOT NULL,
    amount DECIMAL(10, 2),
    type VARCHAR(20) NOT NULL CHECK (type IN ('earn', 'redeem')),
    description TEXT NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Create indexes for better query performance
CREATE INDEX idx_accounts_supabase_user_id ON accounts(supabase_user_id);
CREATE INDEX idx_accounts_email ON accounts(email);
CREATE UNIQUE INDEX idx_accounts_slug ON accounts(slug);
CREATE INDEX idx_customers_account_id ON customers(account_id);
CREATE INDEX idx_customers_email ON customers(email);
CREATE INDEX idx_transactions_customer_id ON transactions(customer_id);
CREATE INDEX idx_transactions_created_at ON transactions(created_at DESC);

-- Create updated_at trigger function
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Create triggers for updated_at
CREATE TRIGGER update_accounts_updated_at BEFORE UPDATE ON accounts
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_customers_updated_at BEFORE UPDATE ON customers
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- Enable Row Level Security (RLS)
ALTER TABLE accounts ENABLE ROW LEVEL SECURITY;
ALTER TABLE customers ENABLE ROW LEVEL SECURITY;
ALTER TABLE transactions ENABLE ROW LEVEL SECURITY;

-- RLS Policies for accounts
CREATE POLICY "Users can view their own account" ON accounts
    FOR SELECT USING (auth.uid()::text = supabase_user_id);

CREATE POLICY "Users can update their own account" ON accounts
    FOR UPDATE USING (auth.uid()::text = supabase_user_id);

CREATE POLICY "Users can insert their own account" ON accounts
    FOR INSERT WITH CHECK (auth.uid()::text = supabase_user_id);

-- RLS Policies for customers
CREATE POLICY "Users can view their own customers" ON customers
    FOR SELECT USING (
        account_id IN (SELECT id FROM accounts WHERE supabase_user_id = auth.uid()::text)
    );

CREATE POLICY "Users can insert their own customers" ON customers
    FOR INSERT WITH CHECK (
        account_id IN (SELECT id FROM accounts WHERE supabase_user_id = auth.uid()::text)
    );

CREATE POLICY "Users can update their own customers" ON customers
    FOR UPDATE USING (
        account_id IN (SELECT id FROM accounts WHERE supabase_user_id = auth.uid()::text)
    );

CREATE POLICY "Users can delete their own customers" ON customers
    FOR DELETE USING (
        account_id IN (SELECT id FROM accounts WHERE supabase_user_id = auth.uid()::text)
    );

-- RLS Policies for transactions
CREATE POLICY "Users can view transactions of their customers" ON transactions
    FOR SELECT USING (
        customer_id IN (
            SELECT c.id FROM customers c
            INNER JOIN accounts a ON c.account_id = a.id
            WHERE a.supabase_user_id = auth.uid()::text
        )
    );

CREATE POLICY "Users can insert transactions for their customers" ON transactions
    FOR INSERT WITH CHECK (
        customer_id IN (
            SELECT c.id FROM customers c
            INNER JOIN accounts a ON c.account_id = a.id
            WHERE a.supabase_user_id = auth.uid()::text
        )
    );
