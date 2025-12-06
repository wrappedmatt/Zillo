# Terminal Pairing Guide

This guide explains the secure terminal registration and pairing process for the Lemonade Loyalty system.

## Overview

The terminal pairing system allows POS terminal devices to securely authenticate with the Lemonade backend. It uses a time-limited pairing code flow similar to TV device pairing (Roku, Apple TV, etc.).

## Architecture

### Components

**Backend (ASP.NET Core)**:
- `TerminalService` - Business logic for pairing and authentication
- `TerminalAuthMiddleware` - Validates API keys on every request
- `TerminalManagementController` - API endpoints for pairing operations
- `TerminalController` - Payment processing endpoints (requires authentication)

**Frontend (React Dashboard)**:
- `Terminals.jsx` - Manage terminals and generate pairing codes
- Displays pairing codes with 5-minute countdown
- Polls for new terminal connections
- Manages terminal lifecycle (revoke, view status)

**Android App**:
- `PairingFragment` - UI for entering pairing codes
- `SecureStorage` - Encrypted storage using Android Keystore
- `ApiClient` - HTTP client with automatic API key injection

**Database**:
- `terminals` table - Stores terminal registrations
- `terminal_pairing_attempts` - Audit log of pairing attempts

## Pairing Flow

### Step 1: Generate Pairing Code (Dashboard)

1. Merchant logs into Lemonade Dashboard
2. Navigates to **Terminals** page
3. Clicks **Add Terminal** button
4. Dashboard calls `POST /api/TerminalManagement/generate-pairing-code`
   ```http
   POST /api/TerminalManagement/generate-pairing-code
   X-Account-Id: {account-id}

   {
     "terminalLabel": "Terminal 1"
   }
   ```

5. Backend creates terminal record with:
   - Random 4-digit pairing code (format: `PAIR-XXXX`)
   - Pre-generated API key (format: `term_sk_live_{random}`)
   - 5-minute expiration timestamp
   - Status: `is_active = false`

6. Dashboard displays pairing code in dialog with countdown timer
7. Dashboard polls for terminal pairing every 2 seconds

### Step 2: Pair Terminal (Android App)

1. Terminal app launches and checks `SecureStorage.isTerminalPaired()`
2. If not paired, shows `PairingFragment`
3. User enters pairing code from dashboard
4. App calls `POST /api/TerminalManagement/pair` (no authentication required)
   ```http
   POST /api/TerminalManagement/pair
   Content-Type: application/json

   {
     "pairingCode": "PAIR-1234",
     "terminalLabel": "Samsung Galaxy Tab",
     "deviceModel": "Samsung SM-T970",
     "deviceId": "abc123def456"
   }
   ```

5. Backend validates:
   - Pairing code exists
   - Code hasn't expired (< 5 minutes old)
   - Code hasn't been used before (`paired_at IS NULL`)

6. Backend updates terminal:
   - Sets `paired_at = NOW()`
   - Sets `is_active = true`
   - Clears `pairing_code` (one-time use)
   - Updates `terminal_label`, `device_model`, `device_id`

7. Backend returns:
   ```json
   {
     "apiKey": "term_sk_live_xxxxxxxxxxx",
     "terminalId": "guid",
     "accountId": "guid",
     "terminalLabel": "Samsung Galaxy Tab"
   }
   ```

8. App stores credentials in `SecureStorage` (encrypted with Android Keystore)
9. App sets `ApiClient.terminalApiKey` for future requests
10. App navigates to main screen

### Step 3: Authenticated Requests

All subsequent requests to `/api/Terminal/*` endpoints include the API key:

```http
POST /api/Terminal/create_payment_intent
X-Terminal-API-Key: term_sk_live_xxxxxxxxxxx
Content-Type: application/x-www-form-urlencoded

amount=1000&currency=nzd
```

The `TerminalAuthMiddleware` intercepts requests and:
1. Reads `X-Terminal-API-Key` header
2. Validates key via `TerminalService.ValidateApiKeyAsync()` (cached 5 minutes)
3. Stores `accountId`, `terminalId`, `terminalLabel` in `HttpContext.Items`
4. Updates `last_seen_at` timestamp asynchronously
5. Returns 401 if invalid/missing

Controllers access authenticated info:
```csharp
var accountId = (Guid)HttpContext.Items["AccountId"]!;
var terminalId = (Guid)HttpContext.Items["TerminalId"]!;
```

## Security Features

### 1. Time-Limited Pairing Codes
- Codes expire after 5 minutes
- Backend automatically cleans up expired codes after 24 hours
- One-time use: code is cleared after successful pairing

### 2. Secure API Key Generation
- 32 bytes of cryptographic random data
- Base64URL encoded
- Prefix: `term_sk_live_` for easy identification
- Stored hashed in database (future enhancement)

### 3. Encrypted Local Storage
- Android Keystore-backed encryption
- EncryptedSharedPreferences (AES256-GCM)
- API keys never stored in plain text
- Automatic key rotation on device factory reset

### 4. Request Authentication
- All payment operations require valid API key
- Middleware validates on every request
- 5-minute memory cache to reduce database queries
- Invalid keys return 401 Unauthorized

### 5. Account Isolation
- Terminal scoped to single account
- Customer lookups filtered by terminal's account
- Prevents cross-account data leakage
- Row-level security in database queries

### 6. Audit Trail
- `terminal_pairing_attempts` logs all pairing attempts
- Failed attempts tracked for security monitoring
- Terminal `last_seen_at` timestamp for activity monitoring
- Payment transactions linked to terminal ID

## Terminal Management

### View Terminals

Dashboard displays all terminals with:
- Terminal label and device model
- Status badge (Online/Idle/Offline/Revoked)
- Last seen timestamp
- Pairing date

Status logic:
- **Online**: Last seen < 5 minutes ago
- **Idle**: Last seen 5-60 minutes ago
- **Offline**: Last seen > 1 hour ago
- **Revoked**: `is_active = false`

### Revoke Terminal

1. Click **Revoke Terminal** on terminal card
2. Confirm action
3. Dashboard calls `POST /api/TerminalManagement/{id}/revoke`
4. Backend sets `is_active = false`
5. Terminal's API key immediately invalidated
6. Existing transactions preserved for audit

Terminal will receive 401 on next request and must re-pair.

### Unpair Terminal (Android)

To unpair a terminal from the device:
```kotlin
SecureStorage.clearTerminalInfo()
// Restart app - will show pairing screen
```

## API Endpoints

### Terminal Management (Dashboard/Admin)

```
POST   /api/TerminalManagement/generate-pairing-code
POST   /api/TerminalManagement/pair
GET    /api/TerminalManagement
GET    /api/TerminalManagement/{id}
PUT    /api/TerminalManagement/{id}
POST   /api/TerminalManagement/{id}/revoke
POST   /api/TerminalManagement/validate
```

### Terminal Operations (Requires X-Terminal-API-Key)

```
POST   /api/Terminal/connection_token
POST   /api/Terminal/create_payment_intent
POST   /api/Terminal/update_payment_intent
POST   /api/Terminal/capture_payment_intent
GET    /api/Terminal/lookup_customer_credit
POST   /api/Terminal/apply_redemption
POST   /api/Terminal/capture_with_redemption
POST   /api/Terminal/email_receipt
```

## Database Schema

### terminals table

```sql
CREATE TABLE terminals (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
    api_key VARCHAR(255) NOT NULL UNIQUE,
    terminal_label VARCHAR(100) NOT NULL,
    stripe_terminal_id VARCHAR(255),
    device_model VARCHAR(100),
    device_id VARCHAR(255),
    pairing_code VARCHAR(20) UNIQUE,
    pairing_expires_at TIMESTAMP WITH TIME ZONE,
    paired_at TIMESTAMP WITH TIME ZONE,
    is_active BOOLEAN NOT NULL DEFAULT true,
    last_seen_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_terminals_account ON terminals(account_id);
CREATE INDEX idx_terminals_api_key ON terminals(api_key) WHERE is_active = true;
CREATE INDEX idx_terminals_pairing_code ON terminals(pairing_code) WHERE pairing_code IS NOT NULL;
```

## Troubleshooting

### Terminal shows "Invalid API key"

1. Check if terminal was revoked in dashboard
2. Try unpairing and re-pairing terminal
3. Verify backend is reachable
4. Check `terminals` table for `is_active = false`

### Pairing code doesn't work

1. Verify code format: `PAIR-XXXX` (4 digits)
2. Check if code expired (5 minutes)
3. Ensure code hasn't been used already
4. Generate new code from dashboard

### Terminal not showing as "Online"

1. Check network connection
2. Verify API requests are being made
3. Check `last_seen_at` in database
4. Ensure middleware is updating timestamp

### Customers from wrong account showing up

This should never happen. If it does:
1. Check terminal's `account_id` in database
2. Verify `TerminalAuthMiddleware` is active
3. Check `HttpContext.Items["AccountId"]` in controller
4. Review customer lookup queries for account filtering

## Testing

### Manual Testing Checklist

- [ ] Generate pairing code from dashboard
- [ ] Enter code in terminal app
- [ ] Verify terminal appears in dashboard as "Online"
- [ ] Make test payment
- [ ] Verify payment scoped to correct account
- [ ] Revoke terminal from dashboard
- [ ] Verify terminal receives 401 on next request
- [ ] Re-pair terminal with new code
- [ ] Verify old API key no longer works

### Automated Tests

```bash
# Backend tests
dotnet test LemonadeApp.Tests/TerminalServiceTests.cs
dotnet test LemonadeApp.Tests/TerminalAuthMiddlewareTests.cs

# Android tests
./gradlew testDebugUnitTest
```

## Security Best Practices

1. **Never log API keys** - redact from logs
2. **Rotate API keys** - if compromised, revoke and re-pair
3. **Monitor pairing attempts** - watch for brute force attacks
4. **Use HTTPS only** - enforce TLS 1.2+
5. **Implement rate limiting** - on pairing endpoint
6. **Regular security audits** - review terminal access logs

## Future Enhancements

1. **API key hashing** - Store bcrypt/argon2 hash instead of plain text
2. **Certificate pinning** - Prevent MITM attacks
3. **Biometric pairing** - Require fingerprint/face for pairing
4. **Remote wipe** - Clear terminal data remotely
5. **Geofencing** - Restrict terminal use to specific locations
6. **Multi-factor auth** - Require second factor for sensitive operations

## Support

For issues or questions:
- Check logs: `LemonadeApp.Rewards.Server/logs/`
- Review database: `SELECT * FROM terminals WHERE id = '{terminal-id}'`
- Contact support: support@lemonadeloyalty.com
