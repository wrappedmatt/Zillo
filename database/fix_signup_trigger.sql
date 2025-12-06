-- This script adds a trigger to automatically create an account record when a user signs up
-- This bypasses the RLS issue by running with elevated privileges

-- Drop existing trigger if it exists
DROP TRIGGER IF EXISTS on_auth_user_created ON auth.users;
DROP FUNCTION IF EXISTS public.handle_new_user();

-- Function to handle new user signup
-- SECURITY DEFINER means it runs with the privileges of the user who created it (bypassing RLS)
CREATE OR REPLACE FUNCTION public.handle_new_user()
RETURNS TRIGGER
SECURITY DEFINER
SET search_path = public
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO public.accounts (email, company_name, supabase_user_id)
    VALUES (
        NEW.email,
        COALESCE(NEW.raw_user_meta_data->>'company_name', 'My Company'),
        NEW.id::text
    );
    RETURN NEW;
EXCEPTION WHEN OTHERS THEN
    -- Log the error but don't fail the signup
    RAISE WARNING 'Failed to create account for user %: %', NEW.id, SQLERRM;
    RETURN NEW;
END;
$$;

-- Trigger to automatically create account on signup
CREATE TRIGGER on_auth_user_created
    AFTER INSERT ON auth.users
    FOR EACH ROW
    EXECUTE FUNCTION public.handle_new_user();
