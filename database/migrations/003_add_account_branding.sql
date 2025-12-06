-- Add branding configuration fields to accounts table

ALTER TABLE accounts
ADD COLUMN IF NOT EXISTS branding_logo_url TEXT,
ADD COLUMN IF NOT EXISTS branding_primary_color VARCHAR(7) DEFAULT '#DC2626',
ADD COLUMN IF NOT EXISTS branding_background_color VARCHAR(7) DEFAULT '#DC2626',
ADD COLUMN IF NOT EXISTS branding_text_color VARCHAR(7) DEFAULT '#FFFFFF',
ADD COLUMN IF NOT EXISTS branding_button_color VARCHAR(7) DEFAULT '#E5E7EB',
ADD COLUMN IF NOT EXISTS branding_button_text_color VARCHAR(7) DEFAULT '#1F2937',
ADD COLUMN IF NOT EXISTS branding_headline_text TEXT DEFAULT 'You''ve earned:',
ADD COLUMN IF NOT EXISTS branding_subheadline_text TEXT DEFAULT 'Register now to claim your rewards and save on future visits!',
ADD COLUMN IF NOT EXISTS branding_qr_headline_text TEXT DEFAULT 'Scan to claim your rewards!',
ADD COLUMN IF NOT EXISTS branding_qr_subheadline_text TEXT DEFAULT 'Register now to claim your rewards and save on future visits!',
ADD COLUMN IF NOT EXISTS branding_qr_button_text TEXT DEFAULT 'Done',
ADD COLUMN IF NOT EXISTS branding_recognized_headline_text TEXT DEFAULT 'Welcome back!',
ADD COLUMN IF NOT EXISTS branding_recognized_subheadline_text TEXT DEFAULT 'You''ve earned:',
ADD COLUMN IF NOT EXISTS branding_recognized_button_text TEXT DEFAULT 'Skip',
ADD COLUMN IF NOT EXISTS branding_recognized_link_text TEXT DEFAULT 'Don''t show me again';

-- Update existing accounts to have default branding values
UPDATE accounts
SET
  branding_primary_color = COALESCE(branding_primary_color, '#DC2626'),
  branding_background_color = COALESCE(branding_background_color, '#DC2626'),
  branding_text_color = COALESCE(branding_text_color, '#FFFFFF'),
  branding_button_color = COALESCE(branding_button_color, '#E5E7EB'),
  branding_button_text_color = COALESCE(branding_button_text_color, '#1F2937'),
  branding_headline_text = COALESCE(branding_headline_text, 'You''ve earned:'),
  branding_subheadline_text = COALESCE(branding_subheadline_text, 'Register now to claim your rewards and save on future visits!'),
  branding_qr_headline_text = COALESCE(branding_qr_headline_text, 'Scan to claim your rewards!'),
  branding_qr_subheadline_text = COALESCE(branding_qr_subheadline_text, 'Register now to claim your rewards and save on future visits!'),
  branding_qr_button_text = COALESCE(branding_qr_button_text, 'Done'),
  branding_recognized_headline_text = COALESCE(branding_recognized_headline_text, 'Welcome back!'),
  branding_recognized_subheadline_text = COALESCE(branding_recognized_subheadline_text, 'You''ve earned:'),
  branding_recognized_button_text = COALESCE(branding_recognized_button_text, 'Skip'),
  branding_recognized_link_text = COALESCE(branding_recognized_link_text, 'Don''t show me again')
WHERE branding_primary_color IS NULL
   OR branding_background_color IS NULL
   OR branding_text_color IS NULL
   OR branding_button_color IS NULL
   OR branding_button_text_color IS NULL
   OR branding_headline_text IS NULL
   OR branding_subheadline_text IS NULL
   OR branding_qr_headline_text IS NULL
   OR branding_qr_subheadline_text IS NULL
   OR branding_qr_button_text IS NULL
   OR branding_recognized_headline_text IS NULL
   OR branding_recognized_subheadline_text IS NULL
   OR branding_recognized_button_text IS NULL
   OR branding_recognized_link_text IS NULL;
