# Customer Card Fingerprint & Portal Implementation Plan

## Overview
This implementation changes the loyalty system from email-based to card fingerprint-based, allowing customers to accumulate points before registering and claim them upon signup.

## Database Changes (✅ DONE)
- Created `unclaimed_transactions` table to track points before customer registration
- Created `cards` table to track registered cards per customer (one-to-many)
- Added `portal_token` and `portal_token_expires_at` to customers table
- Added `signup_bonus_points` to accounts table
- Added RLS policies for service role access

## Domain Entities (✅ DONE)
- Created `Card` entity
- Updated `Customer` entity with Cards collection and portal token fields
- Updated `Account` entity with SignupBonusPoints field

## Flow

### 1. Payment at Terminal (Card NOT Registered)
1. Customer taps card at terminal
2. Stripe processes payment and provides card fingerprint
3. System checks if card is registered → NOT FOUND
4. Creates `unclaimed_transaction` record with:
   - card_fingerprint
   - points earned
   - account_id (merchant's account)
5. Shows customer QR code to claim points

### 2. Customer Registration
1. Customer scans QR code → goes to public signup page
2. Page shows:
   - Account's company name
   - Signup bonus points
   - Unclaimed points for their card (if any)
3. Customer enters: name, email, phone
4. System creates customer record
5. System creates card record linking fingerprint to customer
6. System claims all unclaimed_transactions for that fingerprint
7. Awards signup bonus + unclaimed points

### 3. Payment at Terminal (Card IS Registered)
1. Customer taps card at terminal
2. Stripe processes payment and provides card fingerprint
3. System looks up card → FOUND, gets customer_id
4. Awards points directly to customer account
5. Creates transaction record

## TODO Items

### Backend - Domain & Infrastructure
- [ ] Create `UnclaimedTransaction` entity
- [ ] Create `IUnclaimedTransactionRepository` interface
- [ ] Implement `UnclaimedTransactionRepository`
- [ ] Update `ICardRepository.GetByFingerprintAsync()` to include customer lookup
- [ ] Register repositories in Program.cs

### Backend - Application Layer DTOs
- [ ] Create `UnclaimedTransactionDto`
- [ ] Create `CardDto`
- [ ] Create `CustomerSignupRequest` (name, email, phone, card_fingerprint)
- [ ] Create `CustomerPortalDto` (customer info, points, cards, transactions)
- [ ] Create `ClaimPointsRequest` (card_fingerprint)

### Backend - Services
- [ ] Create `ICustomerPortalService` interface with:
  - `GeneratePortalToken(customerId)` → returns short-lived token
  - `GetPortalDataByToken(token)` → returns CustomerPortalDto
  - `RegisterCustomer(accountId, CardFingerprint, CustomerSignupRequest)` → creates customer, links card, claims points
  - `GetUnclaimedPointsByFingerprint(accountId, fingerprint)` → preview before signup

### Backend - Controllers
- [ ] Update `TerminalController.CapturePaymentIntent()`:
  - Get payment method fingerprint from Stripe
  - Check if card is registered
  - If registered: award points to customer
  - If not registered: create unclaimed_transaction
  - Return QR code data

- [ ] Create `CustomerPortalController`:
  - `POST /api/customer-portal/register` → register new customer with card
  - `GET /api/customer-portal/preview/{accountSlug}/{fingerprint}` → preview unclaimed points
  - `GET /api/customer-portal/{token}` → get portal data
  - `GET /api/customer-portal/qr-signup/{accountSlug}` → QR code endpoint

- [ ] Create `QRCodeController`:
  - `GET /api/qr/signup/{accountId}` → generates QR code image for customer signup

### Frontend - Public Customer Signup Page
- [ ] Create `/signup/:slug` route (public, no auth required)
- [ ] Page shows:
  - Company logo/name
  - "Join [Company] Loyalty Program"
  - Signup bonus: "Get X points just for signing up!"
  - If fingerprint in URL: "Plus claim Y points from your recent purchases!"
  - Form: Name, Email, Phone
  - "Sign Up" button
- [ ] On submit:
  - Calls `/api/customer-portal/register`
  - Shows success with portal token link
  - Or saves token in localStorage and redirects to portal

### Frontend - Customer Portal
- [ ] Create `/portal/:token` route (public, token-based auth)
- [ ] Layout:
  - Header with company name
  - Points balance (large, prominent)
  - Rewards/tiers section (if applicable)
  - Registered cards list
  - Recent transactions history
  - "Add Another Card" button (generates new QR code)

### Frontend - Admin Dashboard Updates
- [ ] Add "Generate Signup QR Code" button to dashboard
- [ ] Shows QR code that links to /signup/:slug
- [ ] Add "Unclaimed Points" section showing:
  - Card fingerprints with unclaimed points
  - Total unclaimed points
  - Option to manually link to customer

### Android App Updates
- [ ] Update payment capture to show QR code for unregistered cards
- [ ] Add QR code display after successful payment
- [ ] Show message: "Scan to claim your X points!"

## API Endpoints

### Terminal API (requires X-Terminal-API-Key)
- `POST /api/connection_token` ✅
- `POST /api/create_payment_intent` ✅
- `POST /api/capture_payment_intent` 🔄 UPDATE to use fingerprint
- `POST /api/update_payment_intent` ✅
- `POST /api/lookup_customer_credit?fingerprint=xxx` 🆕 NEW (by fingerprint, not email)
- `POST /api/apply_redemption` ✅
- `POST /api/capture_with_redemption` 🔄 UPDATE to use fingerprint

### Public Customer Portal API (no auth required, uses token/slug)
- `GET /api/customer-portal/preview/:slug/:fingerprint` → Preview unclaimed points before signup
- `POST /api/customer-portal/register` → Register new customer with card
- `GET /api/customer-portal/:token` → Get customer portal data (points, cards, transactions)

### Admin Dashboard API (requires auth)
- `GET /api/qr/signup/:accountId` → Get signup QR code
- `GET /api/unclaimed-transactions` → List unclaimed transactions

## Implementation Priority

1. **Phase 1: Database & Entities** ✅ DONE
   - Run migration
   - Create entities and repositories

2. **Phase 2: Payment Flow Update** 🔄 IN PROGRESS
   - Update TerminalController to use fingerprint
   - Create unclaimed transactions for unregistered cards
   - Return QR code data after payment

3. **Phase 3: Customer Registration**
   - Create CustomerPortalService
   - Create public signup page
   - Implement claim points logic

4. **Phase 4: Customer Portal**
   - Create portal page
   - Show points, cards, transactions

5. **Phase 5: Admin Features**
   - Add QR code generation to dashboard
   - Add unclaimed points management

## Testing Checklist

- [ ] Unregistered card → creates unclaimed_transaction
- [ ] Customer signup → claims all unclaimed points + bonus
- [ ] Registered card → awards points directly
- [ ] Multiple cards per customer work correctly
- [ ] Portal token expires correctly
- [ ] QR code links work
- [ ] Points balance updates correctly
