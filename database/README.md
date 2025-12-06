# Database Setup

## Supabase Schema

This directory contains the database schema for the Zillo App.

### Setup Instructions

1. Go to your Supabase project dashboard: https://yxmtxutrhcstpamvxdjt.supabase.co
2. Navigate to the SQL Editor
3. Copy and paste the contents of `schema.sql`
4. Run the SQL to create all tables, indexes, and Row Level Security policies

### Database Structure

#### Tables

**accounts**
- Stores user account information
- Links to Supabase Auth via `supabase_user_id`
- One account can have many customers

**customers**
- Stores customer information for the loyalty program
- Each customer belongs to one account
- Tracks points balance
- One customer can have many transactions

**transactions**
- Stores all point transactions (earn/redeem)
- Each transaction belongs to one customer
- Types: `earn` (add points) or `redeem` (subtract points)
- Optionally includes purchase amount

### Row Level Security (RLS)

All tables have RLS enabled to ensure users can only access their own data:

- Users can only see/modify accounts linked to their Supabase auth user
- Users can only see/modify customers belonging to their account
- Users can only see/create transactions for their own customers

### Indexes

The following indexes are created for optimal query performance:
- Account lookups by supabase_user_id and email
- Customer lookups by account_id and email
- Transaction lookups by customer_id
- Transaction ordering by created_at (descending)
