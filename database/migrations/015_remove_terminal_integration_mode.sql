-- Migration: Remove terminal_integration_mode column from accounts
-- The system now supports both Zillo-managed and external terminals simultaneously
-- without requiring a mode selection

ALTER TABLE accounts DROP COLUMN IF EXISTS terminal_integration_mode;
