import { createClient } from '@supabase/supabase-js'

const supabaseUrl = 'https://yxmtxutrhcstpamvxdjt.supabase.co'
const supabaseAnonKey = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Inl4bXR4dXRyaGNzdHBhbXZ4ZGp0Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3NjM1MjQ2ODMsImV4cCI6MjA3OTEwMDY4M30.rjh-N4fH1BDQE8H4lY1kh2O9geTC0C7jDgZNvPajISg'

export const supabase = createClient(supabaseUrl, supabaseAnonKey)
