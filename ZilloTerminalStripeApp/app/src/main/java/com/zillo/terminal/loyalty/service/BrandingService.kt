package com.zillo.terminal.loyalty.service

import android.util.Log
import com.zillo.terminal.loyalty.data.BrandingSettings
import com.zillo.terminal.loyalty.data.IdentifyResponse
import com.zillo.terminal.loyalty.storage.SecureStorage
import com.stripe.aod.sampleapp.Config
import com.stripe.aod.sampleapp.network.ApiClient
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

/**
 * Service to manage branding and loyalty configuration for the terminal
 * Fetches and caches settings from the backend
 */
object BrandingService {
    private val _brandingSettings = MutableStateFlow(BrandingSettings.default())
    val brandingSettings: StateFlow<BrandingSettings> = _brandingSettings.asStateFlow()

    private var isInitialized = false

    /**
     * Fetch branding and loyalty settings from the backend
     * Should be called on app startup after terminal is paired
     */
    suspend fun fetchBrandingSettings(): Result<BrandingSettings> {
        return try {
            Log.d(Config.TAG, "Fetching branding settings from backend...")
            val settings = ApiClient.backendService.getBrandingSettings()
            _brandingSettings.value = settings
            isInitialized = true
            Log.d(Config.TAG, "Branding settings loaded: ${settings.companyName}")

            // Also fetch account configuration for loyalty system
            fetchAccountConfiguration()

            Result.success(settings)
        } catch (e: Exception) {
            Log.e(Config.TAG, "Failed to fetch branding settings", e)
            // Keep using default or previously cached settings
            Result.failure(e)
        }
    }

    /**
     * Fetch account configuration including loyalty system settings
     * Updates the Config object with loyalty settings
     */
    suspend fun fetchAccountConfiguration(): Result<Unit> {
        return try {
            Log.d(Config.TAG, "Fetching account configuration from backend...")
            val accountConfig = ApiClient.backendService.getAccountConfiguration()

            // Update Config with fetched values
            Config.loyaltySystemType = accountConfig.loyaltySystemType ?: "cashback"
            Config.cashbackRate = accountConfig.cashbackRate ?: 5.0
            Config.pointsRate = accountConfig.pointsRate ?: 1.0
            Config.historicalRewardDays = accountConfig.historicalRewardDays ?: 14
            Config.welcomeIncentive = accountConfig.welcomeIncentive ?: 5.0

            Log.d(Config.TAG, "Account configuration loaded:")
            Log.d(Config.TAG, "  - Loyalty Type: ${Config.loyaltySystemType}")
            Log.d(Config.TAG, "  - Cashback Rate: ${Config.cashbackRate}%")
            Log.d(Config.TAG, "  - Points Rate: ${Config.pointsRate} points per dollar")
            Log.d(Config.TAG, "  - Welcome Incentive: $${Config.welcomeIncentive}")

            Result.success(Unit)
        } catch (e: Exception) {
            Log.e(Config.TAG, "Failed to fetch account configuration", e)
            // Keep using default values in Config
            Result.failure(e)
        }
    }

    /**
     * Refresh branding and loyalty settings from backend
     * Can be called periodically or when settings might have changed
     */
    suspend fun refreshBranding() {
        fetchBrandingSettings()
    }

    /**
     * Get current branding settings synchronously
     */
    fun getCurrentBranding(): BrandingSettings {
        return _brandingSettings.value
    }

    /**
     * Check if branding has been initialized from backend
     */
    fun isInitialized(): Boolean {
        return isInitialized
    }

    /**
     * Parse hex color string to Android color int
     * @param hexColor Color in hex format (e.g., "#DC2626")
     * @return Android color int, or fallback color if parsing fails
     */
    fun parseColor(hexColor: String, fallback: Int = android.graphics.Color.RED): Int {
        return try {
            android.graphics.Color.parseColor(hexColor)
        } catch (e: Exception) {
            Log.e(Config.TAG, "Failed to parse color: $hexColor", e)
            fallback
        }
    }

    // ==================== External Mode Support ====================

    /**
     * Initialize from external mode identification response
     * Used when terminal identifies via Stripe Location ID
     */
    fun initializeFromIdentifyResponse(response: IdentifyResponse) {
        // Set branding from identify response
        _brandingSettings.value = response.branding
        isInitialized = true
        Log.d(Config.TAG, "Branding initialized from identify response: ${response.companyName}")

        // Update Config with loyalty settings from response
        Config.loyaltySystemType = response.loyaltySystemType ?: "cashback"
        Config.cashbackRate = response.cashbackRate ?: 5.0
        Config.historicalRewardDays = response.historicalRewardDays ?: 14
        Config.welcomeIncentive = response.welcomeIncentive ?: 5.0

        Log.d(Config.TAG, "Account configuration loaded from identify:")
        Log.d(Config.TAG, "  - Loyalty Type: ${Config.loyaltySystemType}")
        Log.d(Config.TAG, "  - Cashback Rate: ${Config.cashbackRate}%")
        Log.d(Config.TAG, "  - Welcome Incentive: $${Config.welcomeIncentive}")
    }

    /**
     * Fetch settings using external mode (re-identify by location)
     * Used to refresh settings when in external mode
     */
    suspend fun fetchExternalModeSettings(): Result<IdentifyResponse> {
        val locationId = SecureStorage.getStripeLocationId()
        if (locationId.isNullOrEmpty()) {
            return Result.failure(IllegalStateException("No Stripe Location ID stored"))
        }

        return try {
            Log.d(Config.TAG, "Re-identifying terminal by location: $locationId")
            val response = ApiClient.identifyByLocation(locationId).getOrThrow()
            initializeFromIdentifyResponse(response)
            Result.success(response)
        } catch (e: Exception) {
            Log.e(Config.TAG, "Failed to re-identify terminal", e)
            Result.failure(e)
        }
    }
}
