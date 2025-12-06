# Database Migrations

## Running Migrations

### Migration 002: Add Cards and Customer Portal

This migration adds:
- `unclaimed_transactions` table for pre-registration point tracking
- `cards` table for customer card management
- Portal token fields on customers
- Signup bonus points on accounts

**To run this migration:**

1. Go to Supabase Dashboard: https://supabase.com/dashboard
2. Navigate to your project: `yxmtxutrhcstpamvxdjt`
3. Go to SQL Editor
4. Copy and paste the contents of `002_add_cards_and_customer_portal.sql`
5. Click "Run"

**Important:** This migration uses service_role RLS policies. Make sure your backend is using the service_role key (not the anon key) for these operations.
