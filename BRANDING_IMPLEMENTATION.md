# Terminal Branding Implementation

This document describes the implementation of customizable branding for the LemonadeApp terminal displays.

## Overview

The terminal branding system allows merchants to customize how their brand appears on the terminal's "unrecognized card" screen. This includes:
- Company logo
- Background and text colors
- Button colors
- Custom headline and subheadline text

## Database Changes

### Migration File
- **File**: `database/migrations/003_add_account_branding.sql`
- **New Columns** added to `accounts` table:
  - `branding_logo_url` (TEXT) - URL to company logo image
  - `branding_primary_color` (VARCHAR(7)) - Primary brand color (hex)
  - `branding_background_color` (VARCHAR(7)) - Background color for terminal screen
  - `branding_text_color` (VARCHAR(7)) - Text color on terminal screen
  - `branding_button_color` (VARCHAR(7)) - Button background color
  - `branding_button_text_color` (VARCHAR(7)) - Button text color
  - `branding_headline_text` (TEXT) - Text shown above points amount
  - `branding_subheadline_text` (TEXT) - Text shown below points amount

### Default Values
- Background Color: `#DC2626` (Red)
- Text Color: `#FFFFFF` (White)
- Button Color: `#E5E7EB` (Light Gray)
- Button Text Color: `#1F2937` (Dark Gray)
- Headline: "You've earned:"
- Subheadline: "Register now to claim your rewards and save on future visits!"

## Backend Changes

### Domain Layer
**File**: `LemonadeApp.Domain/Entities/Account.cs`
- Added 8 new branding properties to Account entity
- All properties include XML comments and default values

### Infrastructure Layer
**File**: `LemonadeApp.Infrastructure/Repositories/AccountRepository.cs`
- Updated `AccountModel` with Postgrest column attributes for all branding fields
- Updated `ToEntity()` and `FromEntity()` methods to map branding properties

### Presentation Layer

#### AccountsController
**File**: `LemonadeApp.Dashboard.Server/Controllers/AccountsController.cs`
- Updated `GET /api/accounts/me` to return all branding fields
- Updated `PUT /api/accounts/me` to accept and save all branding fields
- Updated `UpdateAccountRequest` record to include branding parameters

#### TerminalController
**File**: `LemonadeApp.Dashboard.Server/Controllers/TerminalController.cs`
- Added `GET /api/terminal/branding` endpoint
- Returns branding settings for authenticated terminal's account
- Includes company name, all color settings, text content, and signup URL
- Requires terminal authentication via `X-Terminal-API-Key` header

## Admin Dashboard Changes

### Settings Page
**File**: `lemonadeapp.dashboard.client/src/pages/Settings.jsx`

Added comprehensive Terminal Branding configuration section with:

#### Form Fields
1. **Logo URL** - Input for image URL with validation
2. **Color Pickers** - For all 4 color settings (background, text, button bg, button text)
   - Each has both a color picker and hex code input
   - Real-time preview updates as colors change
3. **Text Content** - Inputs for headline and subheadline customization
4. **Live Preview** - Real-time preview showing how the terminal screen will look

#### UI Features
- Color inputs with both visual color picker and hex code text input
- Textarea for multi-line subheadline text
- Live preview panel showing exact terminal appearance
- Logo preview with error handling for invalid URLs
- Form state management integrated with existing settings

### Form State
- All 8 branding fields added to formData state
- loadAccount() populates branding fields from API
- handleSave() includes branding fields in PUT request

## API Endpoints

### Admin Endpoints (Authenticated)
- `GET /api/accounts/me` - Returns account with branding settings
- `PUT /api/accounts/me` - Updates account including branding settings

### Terminal Endpoints (Terminal-Authenticated)
- `GET /api/terminal/branding` - Returns branding config for terminal's account

**Terminal Branding Response Format**:
```json
{
  "companyName": "Lemonade Stand",
  "logoUrl": "https://example.com/logo.png",
  "backgroundColor": "#DC2626",
  "textColor": "#FFFFFF",
  "buttonColor": "#E5E7EB",
  "buttonTextColor": "#1F2937",
  "headlineText": "You've earned:",
  "subheadlineText": "Register now to claim your rewards and save on future visits!",
  "signupUrl": "/signup/lemonade-stand"
}
```

## Android Terminal App (TODO)

### Remaining Tasks

1. **Create TerminalSettings Activity/Fragment**
   - Settings screen in terminal app
   - Button to fetch/refresh branding from server
   - Local storage of branding settings (SharedPreferences or Room DB)
   - Display current branding configuration
   - Manual refresh button

2. **Update UnrecognizedCardActivity**
   - Load branding settings from local storage
   - Apply dynamic colors to all UI elements
   - Load and display logo from URL (with Coil or Glide)
   - Apply custom text to headlines
   - Style buttons with custom colors
   - Fallback to defaults if branding not configured

3. **Network Layer**
   - Add branding API call to existing Retrofit service
   - Include terminal API key in request headers
   - Parse branding JSON response
   - Handle errors gracefully

### Implementation Approach

#### Data Model (Kotlin)
```kotlin
data class BrandingSettings(
    val companyName: String,
    val logoUrl: String?,
    val backgroundColor: String,
    val textColor: String,
    val buttonColor: String,
    val buttonTextColor: String,
    val headlineText: String,
    val subheadlineText: String,
    val signupUrl: String
)
```

#### API Service
```kotlin
@GET("/api/terminal/branding")
suspend fun getTerminalBranding(
    @Header("X-Terminal-API-Key") apiKey: String
): BrandingSettings
```

#### Settings Storage
- Use SharedPreferences or DataStore
- Cache branding settings locally
- Fetch on app launch or manual refresh
- Key: `"terminal_branding_json"`

#### UI Application
```kotlin
// UnrecognizedCardActivity.kt
private fun applyBranding(branding: BrandingSettings) {
    // Background
    rootLayout.setBackgroundColor(Color.parseColor(branding.backgroundColor))

    // Text colors
    headlineText.setTextColor(Color.parseColor(branding.textColor))
    pointsText.setTextColor(Color.parseColor(branding.textColor))
    subheadlineText.setTextColor(Color.parseColor(branding.textColor))

    // Button styling
    claimButton.setBackgroundColor(Color.parseColor(branding.buttonColor))
    claimButton.setTextColor(Color.parseColor(branding.buttonTextColor))

    // Text content
    headlineText.text = branding.headlineText
    subheadlineText.text = branding.subheadlineText

    // Logo
    if (branding.logoUrl != null) {
        Coil.load(logoImageView, branding.logoUrl) {
            error(R.drawable.ic_default_logo)
        }
    }
}
```

## Testing Checklist

### Backend
- [ ] Run database migration to add new columns
- [ ] Verify default values populate for existing accounts
- [ ] Test GET /api/accounts/me returns all branding fields
- [ ] Test PUT /api/accounts/me saves all branding fields
- [ ] Test GET /api/terminal/branding with valid terminal API key
- [ ] Test GET /api/terminal/branding rejects unauthenticated requests

### Admin Dashboard
- [ ] Verify Settings page loads with existing branding values
- [ ] Test color pickers update both visual and hex inputs
- [ ] Test hex input validates and updates color picker
- [ ] Test logo URL preview updates in real-time
- [ ] Test invalid logo URL shows error gracefully
- [ ] Test live preview reflects all changes immediately
- [ ] Test Save button persists all changes to backend
- [ ] Test form validation for required fields

### Android Terminal App
- [ ] Terminal fetches branding on first launch
- [ ] Branding settings persist across app restarts
- [ ] Manual refresh updates branding from server
- [ ] Unrecognized card screen applies all color settings
- [ ] Logo loads and displays correctly
- [ ] Custom text appears correctly
- [ ] Button styling matches configuration
- [ ] Graceful fallback to defaults if branding unavailable
- [ ] Error handling for network failures

## Future Enhancements

- Image upload for logo (instead of URL)
- Additional customization options:
  - Font selection
  - Logo size/positioning
  - Button border radius
  - Background images or gradients
- Preview mode in admin dashboard matching exact terminal dimensions
- Multiple branding themes (light/dark mode)
- Branding inheritance for multi-location accounts

## Notes

- All branding settings are account-level (applied to all terminals for that account)
- Logo images should be square format (1:1 aspect ratio) for best results
- Hex color codes must include the # prefix
- Terminal authentication is required for branding endpoint
- Branding changes take effect immediately after save
- Terminals should cache branding settings locally to reduce API calls
