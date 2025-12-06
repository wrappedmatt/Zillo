-- This script helps fix orphaned users (users in auth.users but not in public.accounts)

-- First, let's see if there are any orphaned users
SELECT
    u.id as user_id,
    u.email,
    u.created_at,
    a.id as account_id
FROM auth.users u
LEFT JOIN public.accounts a ON u.id::text = a.supabase_user_id
WHERE a.id IS NULL;

-- To create account records for orphaned users, uncomment and run this:
/*
INSERT INTO public.accounts (email, company_name, supabase_user_id, created_at, updated_at)
SELECT
    u.email,
    COALESCE(u.raw_user_meta_data->>'company_name', 'My Company') as company_name,
    u.id::text as supabase_user_id,
    u.created_at,
    NOW() as updated_at
FROM auth.users u
LEFT JOIN public.accounts a ON u.id::text = a.supabase_user_id
WHERE a.id IS NULL;
*/

-- Or to delete orphaned users entirely, uncomment and run this:
/*
DELETE FROM auth.users
WHERE id IN (
    SELECT u.id
    FROM auth.users u
    LEFT JOIN public.accounts a ON u.id::text = a.supabase_user_id
    WHERE a.id IS NULL
);
*/
