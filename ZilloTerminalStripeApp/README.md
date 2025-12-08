# Zillo Terminal - Stripe Terminal Android App

This is the Stripe Terminal Android application for the Zillo loyalty management system. The app runs on Stripe S700 DevKit smart readers and provides a point-of-sale experience for accepting payments and managing customer loyalty points.

## Overview

This Android app integrates with the Zillo API backend to provide:
- **Card-present payments** via Stripe Terminal SDK
- **Loyalty points tracking** - automatically calculate and award points based on transaction amounts
- **Customer lookup** - find customers by email to apply loyalty rewards
- **QR code generation** - generate QR codes for customer enrollment
- **Receipt management** - email receipts to customers

## Architecture

```
ZilloTerminalStripeApp/
├── app/
│   ├── src/main/java/com/stripe/aod/sampleapp/
│   │   ├── activity/
│   │   │   └── MainActivity.kt              # Main activity
│   │   ├── fragment/
│   │   │   ├── HomeFragment.kt              # Home screen
│   │   │   ├── InputFragment.kt             # Amount entry keypad
│   │   │   ├── LoyaltyPointsFragment.kt     # Points display after payment
│   │   │   ├── QRCodeFragment.kt            # QR code for signup
│   │   │   ├── EmailFragment.kt             # Email input
│   │   │   ├── ReceiptFragment.kt           # Receipt display
│   │   │   └── RedemptionFragment.kt        # Loyalty redemption
│   │   ├── model/
│   │   │   ├── CheckoutViewModel.kt         # Payment flow state management
│   │   │   ├── InputViewModel.kt            # Input handling
│   │   │   └── MainViewModel.kt             # Main state management
│   │   ├── network/
│   │   │   ├── ApiClient.kt                 # Retrofit API client
│   │   │   ├── BackendService.kt            # API interface definitions
│   │   │   └── TokenProvider.kt             # Stripe connection token provider
│   │   ├── data/                            # Data models and DTOs
│   │   ├── listener/
│   │   │   └── TerminalEventListener.kt     # Stripe Terminal event handling
│   │   └── utils/                           # Utility extensions
│   └── build.gradle.kts                     # App dependencies and configuration
├── build.gradle.kts                         # Root build configuration
├── settings.gradle.kts                      # Project settings
├── gradle.properties                        # Gradle configuration
├── local.properties                         # Local configuration (backend URL)
└── README.md                                # This file
```

## Prerequisites

Before you begin, ensure you have:
- **Stripe S700 DevKit** smart reader (or use Android Emulator for development)
- **Android Studio Flamingo** or newer
- **JDK 17** (auto-downloaded via Gradle toolchain)
- **Zillo API** running (see main project README)
- **Stripe account** with Terminal enabled
- **Stripe test mode API key** configured in Zillo.Dashboard.Server

## Setup

### 1. Configure Backend URL

The Android app needs to communicate with the Zillo API. Edit `local.properties`:

```properties
# Production
BACKEND_URL=https://api.zillo.app

# For development with ngrok (physical device)
BACKEND_URL=https://your-ngrok-url.ngrok-free.app

# For Android Emulator only
BACKEND_URL=http://10.0.2.2:7024

# For physical device on same network
BACKEND_URL=http://YOUR_COMPUTER_IP:7024
```

#### Using ngrok (Recommended for Development)

1. Start your Zillo Dashboard backend:
   ```bash
   cd Zillo.Dashboard.Server
   dotnet run
   ```

2. In a new terminal, expose the backend with ngrok:
   ```bash
   ngrok http https://localhost:7024
   ```

3. Copy the ngrok URL (e.g., `https://abc123.ngrok-free.app`) to `local.properties`:
   ```properties
   BACKEND_URL=https://abc123.ngrok-free.app
   ```

### 2. Open in Android Studio

1. Launch Android Studio
2. Select **File → Open**
3. Navigate to `Zillo/ZilloTerminalStripeApp`
4. Click **OK** and wait for Gradle sync to complete

### 3. Build and Run

#### On Android Emulator
1. Create an emulator in Android Studio (API 28+)
2. Click **Run** (or press Shift+F10)
3. Select your emulator from the device list

#### On Stripe S700 DevKit
1. Enable USB debugging on your S700 device
2. Connect via USB or set up wireless debugging
3. Click **Run** in Android Studio
4. Select your S700 device from the list

The app will install and launch automatically.

## API Endpoints

The terminal app communicates with the Zillo API (v1):

### Stripe Terminal
```
POST /api/v1/terminal/connection-token
GET  /api/v1/terminal/config
GET  /api/v1/terminal/branding
```

### Payments
```
POST  /api/v1/payments/intents
PATCH /api/v1/payments/intents/{id}
POST  /api/v1/payments/intents/{id}/capture
POST  /api/v1/payments/intents/{id}/capture-with-redemption
POST  /api/v1/payments/intents/{id}/apply-redemption
```

### Customers
```
POST /api/v1/customers/lookup-by-payment
```

## Payment Flow

1. **Home Screen** → Clerk taps "Start Transaction"
2. **Amount Entry** → Clerk enters payment amount using on-screen keypad
3. **Payment Processing** → Customer taps/inserts card on terminal
4. **Loyalty Points** → Display points earned (1 point per dollar)
5. **QR Code** (optional) → Show signup QR if customer wants to enroll
6. **Receipt** (optional) → Email receipt to customer
7. **Return Home** → Ready for next transaction

## Loyalty Points Calculation

Points are calculated at **1 point per dollar spent** (rounded down):
- $10.00 transaction = 10 points
- $5.50 transaction = 5 points
- $0.99 transaction = 0 points

The calculation happens in `LoyaltyPointsFragment.kt`:
```kotlin
val points = args.amount / 100  // amount is in cents
```

## Customization

### Branding

Update branding elements in:
- `app/src/main/res/values/strings.xml` - App name and text
- `app/src/main/res/values/colors.xml` - Color scheme
- `app/src/main/res/drawable/` - Icons and images
- `app/src/main/res/layout/` - UI layouts

### Backend Integration

API calls are defined in:
- `network/BackendService.kt` - Retrofit interface
- `network/ApiClient.kt` - API client implementation

Modify these files to match your backend API structure.

## Building for Production

### 1. Update Configuration

Edit `app/build.gradle.kts`:
```kotlin
buildTypes {
    release {
        isMinifyEnabled = true
        proguardFiles(
            getDefaultProguardFile("proguard-android-optimize.txt"),
            "proguard-rules.pro"
        )
    }
}
```

### 2. Configure Signing

Create a keystore and add to `app/build.gradle.kts`:
```kotlin
signingConfigs {
    create("release") {
        storeFile = file("your-keystore.jks")
        storePassword = "your-password"
        keyAlias = "your-alias"
        keyPassword = "your-password"
    }
}
```

### 3. Build Release APK

```bash
./gradlew assembleRelease
```

Output: `app/build/outputs/apk/release/app-release.apk`

### 4. Deploy to Stripe Terminal

Follow [Stripe's deployment guide](https://stripe.com/docs/terminal/features/apps-on-devices/deploy) to upload your APK to the Stripe Dashboard and install it on your S700 devices.

## Troubleshooting

### Cannot Connect to Backend

**Symptoms**: "Failed to create payment intent" error

**Solutions**:
- Verify backend is running: `curl https://api.zillo.app/api/health`
- Check `local.properties` has correct `BACKEND_URL`
- For ngrok, ensure tunnel is still active
- Check firewall settings on your computer
- Review backend logs for errors

### Payment Processing Fails

**Symptoms**: Payment stays in "Processing" state

**Solutions**:
- Verify Stripe API key in backend configuration
- Check backend logs for Stripe API errors
- Ensure S700 reader is connected and in "Ready" state
- Verify Stripe account has Terminal enabled
- Check network connectivity

### Gradle Build Errors

**Symptoms**: "Cannot find a Java installation"

**Solutions**:
- Let Gradle auto-download JDK 17 (configured via Foojay Toolchains)
- Or manually set `JAVA_HOME` environment variable
- Invalidate caches: **File → Invalidate Caches / Restart**

### App Won't Install on S700

**Symptoms**: "Installation failed" in Android Studio

**Solutions**:
- Enable USB debugging on S700
- Check USB cable connection
- Try wireless debugging instead
- Verify minimum SDK version (API 28+)

## Development Tips

### Hot Reload

Android Studio supports instant run for faster development:
1. Make code changes
2. Click **Apply Changes** (Ctrl+F10)
3. Changes apply without full rebuild

### Logging

View logs in Android Studio Logcat:
```kotlin
import android.util.Log
import com.stripe.aod.sampleapp.Config

Log.d(Config.TAG, "Your debug message")
```

Filter Logcat by tag: `ZilloTerminal`

### Testing Payments

Use Stripe test cards:
- **Success**: 4242 4242 4242 4242
- **Decline**: 4000 0000 0000 0002
- **Requires Auth**: 4000 0025 0000 3155

See [Stripe test cards documentation](https://stripe.com/docs/testing)

## Security Considerations

### API Keys

- **NEVER** commit real API keys to version control
- Use `appsettings.json` (gitignored) for backend keys
- Use `local.properties` (gitignored) for local config
- Rotate keys immediately if exposed

### Production Checklist

- [ ] Update backend URL to `https://api.zillo.app`
- [ ] Use production Stripe API keys
- [ ] Enable ProGuard code obfuscation
- [ ] Configure proper signing certificate
- [ ] Test on multiple S700 devices
- [ ] Verify HTTPS endpoints
- [ ] Review Stripe Terminal compliance requirements

## Resources

- [Stripe Terminal Documentation](https://stripe.com/docs/terminal)
- [Apps on Devices Overview](https://stripe.com/docs/terminal/features/apps-on-devices/overview)
- [Stripe Terminal Android SDK](https://stripe.com/docs/terminal/sdk/android)
- [Android Studio User Guide](https://developer.android.com/studio/intro)
- [Kotlin Documentation](https://kotlinlang.org/docs/home.html)

## Support

For issues with:
- **This terminal app**: Create an issue in the Zillo repository
- **Zillo backend**: See main project documentation
- **Stripe Terminal**: Contact [Stripe Support](https://support.stripe.com/)

## License

This project is based on the [Stripe Terminal Apps on Devices sample](https://github.com/stripe-samples/terminal-apps-on-devices) and adapted for the Zillo loyalty management system.
