# Stripe Terminal Integration Setup Guide

This guide explains how to set up and use the Stripe Terminal Android app with the LemonadeApp backend.

## Overview

The LemonadeApp now includes complete Stripe Terminal integration for accepting card-present payments on Stripe S700 DevKit smart readers with automatic loyalty points tracking.

## Components

### 1. Android Terminal App
**Location**: `LemonadeTerminalApp/`

A Kotlin-based Android application that runs on Stripe S700 smart readers and provides:
- Card-present payment processing
- Loyalty points calculation and display
- Customer lookup by email
- QR code generation for customer enrollment
- Receipt management

### 2. Backend API Endpoints
**Location**: `LemonadeApp.Rewards.Server/Controllers/TerminalController.cs`

REST API endpoints that handle:
- Stripe Terminal connection tokens
- Payment intent creation and management
- Loyalty point tracking and redemption
- Customer lookup and transaction recording

## Quick Start

### Step 1: Configure Stripe API Keys

1. Get your Stripe API keys from https://dashboard.stripe.com/test/apikeys

2. Update `LemonadeApp.Rewards.Server/appsettings.json`:
   ```json
   {
     "Stripe": {
       "SecretKey": "sk_test_your_actual_secret_key_here",
       "PublishableKey": "pk_test_your_actual_publishable_key_here"
     }
   }
   ```

   **⚠️ Security**: Never commit real API keys to version control!

### Step 2: Start the Backend

```bash
cd LemonadeApp.Rewards.Server
dotnet run
```

The backend will run on `http://localhost:5000` and `https://localhost:5001`.

### Step 3: Expose Backend for Terminal Access

The Android terminal needs to communicate with your backend. Choose one option:

#### Option A: Using ngrok (Recommended for Testing)

```bash
# Install ngrok if you haven't: https://ngrok.com/download
ngrok http https://localhost:5001
```

This will output a public URL like: `https://abc123.ngrok-free.app`

#### Option B: Using Your Local Network

Find your computer's IP address:
- Windows: `ipconfig` (look for IPv4 Address)
- Mac/Linux: `ifconfig` or `ip addr`

Your backend URL will be: `http://YOUR_IP:5000`

#### Option C: Using Android Emulator

If testing with an Android emulator, use: `http://10.0.2.2:5000`

### Step 4: Configure the Terminal App

1. Edit `LemonadeTerminalApp/local.properties`:
   ```properties
   # Use the URL from Step 3
   BACKEND_URL=https://your-ngrok-url.ngrok-free.app

   # Or for local network:
   # BACKEND_URL=http://YOUR_IP:5000

   # Or for emulator:
   # BACKEND_URL=http://10.0.2.2:5000

   # Your Android SDK path
   sdk.dir=C\:\\Users\\YourUsername\\AppData\\Local\\Android\\Sdk
   ```

### Step 5: Build and Install the Terminal App

1. Open Android Studio
2. Open project: `LemonadeTerminalApp/`
3. Wait for Gradle sync to complete
4. Connect your Stripe S700 device or start an emulator
5. Click **Run** (or press Shift+F10)
6. Select your device from the list

The app will install and launch automatically.

## Testing the Integration

### 1. Test Backend Endpoints

```bash
# Test connection token endpoint
curl -X POST http://localhost:5000/api/terminal/connection_token

# Expected response:
# {"secret":"pst_test_..."}

# Test create payment intent
curl -X POST http://localhost:5000/api/terminal/create_payment_intent \
  -d "amount=1000" \
  -d "currency=nzd"

# Expected response:
# {"intent":"pi_..._secret_...","payment_intent_id":"pi_..."}
```

### 2. Test Payment Flow on Terminal

1. **Launch the app** on your S700 device
2. **Tap "Start Transaction"**
3. **Enter amount** (e.g., $10.00)
4. **Tap or insert test card** (use Stripe test card: 4242 4242 4242 4242)
5. **View loyalty points earned** (10 points for $10.00)
6. **Optionally show QR code** for customer enrollment

### 3. Test Customer Lookup

To test loyalty redemption:

1. Create a test customer in your system with some points
2. On the terminal, enter customer email when prompted
3. The terminal will look up the customer and display available points
4. Customer can redeem points to reduce payment amount

## API Endpoints Reference

All endpoints are prefixed with `/api/terminal`:

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/connection_token` | POST | Generate Stripe Terminal connection token |
| `/create_payment_intent` | POST | Create new payment intent |
| `/update_payment_intent` | POST | Update existing payment intent amount |
| `/capture_payment_intent` | POST | Capture payment and award points |
| `/lookup_customer_credit` | GET | Find customer by email and get point balance |
| `/apply_redemption` | POST | Apply loyalty points to reduce payment |
| `/capture_with_redemption` | POST | Capture payment with points redeemed |
| `/email_receipt` | POST | Email receipt to customer (not implemented) |

See `BACKEND_INTEGRATION.md` in `LemonadeTerminalApp/` for detailed API documentation.

## Backend Architecture

### Flow Diagram

```
Android Terminal App
    ↓ (HTTPS)
TerminalController
    ↓
┌─────────────┬──────────────────┐
│ Stripe API  │ LemonadeApp      │
│             │ Services         │
│ - Terminal  │ - CustomerService│
│ - Payment   │ - TransactionSvc │
│   Intents   │ - Supabase DB    │
└─────────────┴──────────────────┘
```

### Key Files

**Backend**:
- `LemonadeApp.Rewards.Server/Controllers/TerminalController.cs` - API endpoints
- `LemonadeApp.Rewards.Server/Program.cs` - Stripe configuration
- `LemonadeApp.Application/Services/ICustomerService.cs` - Customer lookup interface
- `LemonadeApp.Infrastructure/Services/CustomerService.cs` - Customer service implementation
- `LemonadeApp.Infrastructure/Repositories/CustomerRepository.cs` - Database access

**Android App**:
- `LemonadeTerminalApp/app/src/main/java/com/stripe/aod/sampleapp/`
  - `activity/MainActivity.kt` - Main activity
  - `fragment/HomeFragment.kt` - Home screen
  - `fragment/InputFragment.kt` - Amount entry
  - `fragment/LoyaltyPointsFragment.kt` - Points display
  - `network/ApiClient.kt` - Backend communication
  - `model/CheckoutViewModel.kt` - Payment state management

## Loyalty Points System

### How Points Work

- **Earning**: Customers earn **1 point per $1 spent** (rounded down)
  - $10.00 purchase = 10 points
  - $5.50 purchase = 5 points
  - $0.99 purchase = 0 points

- **Redemption**: Points can be redeemed at **1 point = $0.01**
  - 100 points = $1.00 discount
  - 500 points = $5.00 discount

### Database Structure

Points are tracked in the `transactions` table:
- **Type: "earn"** - Points added for purchases
- **Type: "redeem"** - Points deducted for redemptions
- **PointsBalance** in `customers` table - Running total (updated via triggers)

### Transaction Flow

#### Simple Purchase (No Redemption)
1. Terminal creates payment intent
2. Customer taps card
3. Payment is captured
4. Backend creates "earn" transaction
5. Customer's point balance increases

#### Purchase with Redemption
1. Terminal looks up customer by email
2. Displays available points
3. Customer chooses points to redeem
4. Payment intent amount is reduced
5. Payment is captured
6. Backend creates "redeem" transaction (negative points)
7. Backend creates "earn" transaction for remaining amount
8. Customer's point balance updates

## Troubleshooting

### Backend Won't Start

**Error**: "Stripe Secret Key is required"
- **Solution**: Update `appsettings.json` with your Stripe API key

**Error**: "Port 5000 already in use"
- **Solution**: Stop other services using port 5000, or change port in `Properties/launchSettings.json`

### Terminal Can't Connect

**Error**: "Failed to create connection token"
- Check backend is running: `curl http://localhost:5000/api/terminal/connection_token`
- Verify `BACKEND_URL` in `local.properties` is correct
- Check firewall isn't blocking connections
- If using ngrok, verify tunnel is still active

**Error**: "Connection refused"
- Backend must be accessible from terminal device
- Test with: `curl http://YOUR_BACKEND_URL/api/terminal/connection_token`
- Try different BACKEND_URL option (ngrok, local IP, etc.)

### Payment Processing Issues

**Error**: "No API key provided"
- Stripe API key not configured correctly in backend
- Check `Program.cs` line 28: `Stripe.StripeConfiguration.ApiKey`

**Error**: "Rate limit exceeded"
- Too many test transactions
- Wait a few minutes or use production API keys (carefully!)

**Error**: "Payment requires authentication"
- Some test cards require 3D Secure authentication
- Use simple test card: 4242 4242 4242 4242
- See: https://stripe.com/docs/testing

### Customer Lookup Fails

**Error**: "Customer not found"
- Customer doesn't exist in database
- Email doesn't match exactly (case-sensitive)
- Create customer via dashboard or Rewards app first

**Error**: "Unable to award points"
- Check backend logs for specific error
- Verify transaction service is configured
- Check database permissions

### Build Issues

**Error**: "Cannot find Stripe.net"
- Run: `dotnet restore LemonadeApp.Rewards.Server`

**Error**: "Android SDK not found"
- Update `local.properties` with correct SDK path
- Or let Android Studio set it automatically

## Production Deployment

### Backend Checklist

- [ ] Use production Stripe API keys
- [ ] Deploy to cloud service (Azure, AWS, etc.)
- [ ] Use HTTPS with valid SSL certificate
- [ ] Set up environment variables for secrets
- [ ] Configure proper CORS policies
- [ ] Enable rate limiting
- [ ] Set up monitoring and logging
- [ ] Configure backup for Supabase database

### Terminal App Checklist

- [ ] Update `BACKEND_URL` to production URL
- [ ] Change application ID in `app/build.gradle.kts`
- [ ] Create signing certificate for release build
- [ ] Enable ProGuard for code obfuscation
- [ ] Build release APK: `./gradlew assembleRelease`
- [ ] Upload APK to Stripe Dashboard
- [ ] Deploy to production S700 devices via Stripe
- [ ] Test thoroughly with real cards (use test mode initially)

### Security Considerations

1. **Never commit secrets**: Keep `appsettings.json` and real API keys out of git
2. **Use HTTPS**: Always use TLS in production
3. **Validate inputs**: Backend validates all terminal requests
4. **Rate limiting**: Prevent API abuse
5. **Audit logging**: Log all payment and loyalty transactions
6. **PCI compliance**: Follow Stripe Terminal's compliance guidelines

## Additional Resources

- [Stripe Terminal Documentation](https://stripe.com/docs/terminal)
- [Stripe Terminal Android SDK](https://stripe.com/docs/terminal/sdk/android)
- [Apps on Devices Guide](https://stripe.com/docs/terminal/features/apps-on-devices)
- [Stripe API Reference](https://stripe.com/docs/api)
- [Stripe Test Cards](https://stripe.com/docs/testing)
- [LemonadeTerminalApp README](LemonadeTerminalApp/README.md)
- [Backend Integration Guide](LemonadeTerminalApp/BACKEND_INTEGRATION.md)

## Support

For issues:
- **Terminal app**: See `LemonadeTerminalApp/README.md`
- **Backend integration**: See `LemonadeTerminalApp/BACKEND_INTEGRATION.md`
- **Stripe Terminal**: Contact [Stripe Support](https://support.stripe.com/)
- **Project issues**: Create an issue in this repository

## Next Steps

1. ✅ Backend endpoints created
2. ✅ Android app configured
3. ⬜ Test payment flow end-to-end
4. ⬜ Add more customers for testing
5. ⬜ Test loyalty redemption flow
6. ⬜ Implement email receipt functionality
7. ⬜ Customize branding for your business
8. ⬜ Deploy to production
