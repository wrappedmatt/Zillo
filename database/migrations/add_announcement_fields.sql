-- Migration: Add announcement fields to customers table
-- Date: 2025-12-01
-- Description: Adds last_announcement_message and last_announcement_at fields to track
--              custom notifications sent to customers' Apple Wallet passes

ALTER TABLE customers
ADD COLUMN last_announcement_message TEXT,
ADD COLUMN last_announcement_at TIMESTAMP WITH TIME ZONE;

-- Add index for querying announcements
CREATE INDEX idx_customers_last_announcement_at ON customers(last_announcement_at DESC);
