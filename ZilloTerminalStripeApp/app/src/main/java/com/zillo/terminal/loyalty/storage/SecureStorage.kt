package com.zillo.terminal.loyalty.storage

import android.content.Context
import android.content.SharedPreferences
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey

/**
 * SecureStorage handles encrypted storage of sensitive data like terminal API keys
 * using Android's EncryptedSharedPreferences backed by the Android Keystore
 */
object SecureStorage {

    private const val PREFS_FILENAME = "zillo_terminal_secure_prefs"
    private const val KEY_TERMINAL_API_KEY = "terminal_api_key"
    private const val KEY_TERMINAL_ID = "terminal_id"
    private const val KEY_ACCOUNT_ID = "account_id"
    private const val KEY_TERMINAL_LABEL = "terminal_label"
    private const val KEY_EXTERNAL_MODE = "external_mode"
    private const val KEY_STRIPE_LOCATION_ID = "stripe_location_id"
    private const val KEY_COMPANY_NAME = "company_name"
    private const val KEY_SLUG = "slug"

    private var securePrefs: SharedPreferences? = null

    /**
     * Initialize secure storage with the application context
     * Must be called before accessing any storage methods
     */
    fun init(context: Context) {
        if (securePrefs != null) return

        try {
            val masterKey = MasterKey.Builder(context)
                .setKeyScheme(MasterKey.KeyScheme.AES256_GCM)
                .build()

            securePrefs = EncryptedSharedPreferences.create(
                context,
                PREFS_FILENAME,
                masterKey,
                EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
                EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
            )
        } catch (e: Exception) {
            throw RuntimeException("Failed to initialize secure storage", e)
        }
    }

    /**
     * Save terminal pairing information
     */
    fun saveTerminalInfo(apiKey: String, terminalId: String, accountId: String, terminalLabel: String) {
        securePrefs?.edit()?.apply {
            putString(KEY_TERMINAL_API_KEY, apiKey)
            putString(KEY_TERMINAL_ID, terminalId)
            putString(KEY_ACCOUNT_ID, accountId)
            putString(KEY_TERMINAL_LABEL, terminalLabel)
            apply()
        }
    }

    /**
     * Get the stored terminal API key
     */
    fun getTerminalApiKey(): String? {
        return securePrefs?.getString(KEY_TERMINAL_API_KEY, null)
    }

    /**
     * Get the stored terminal ID
     */
    fun getTerminalId(): String? {
        return securePrefs?.getString(KEY_TERMINAL_ID, null)
    }

    /**
     * Get the stored account ID
     */
    fun getAccountId(): String? {
        return securePrefs?.getString(KEY_ACCOUNT_ID, null)
    }

    /**
     * Get the stored terminal label
     */
    fun getTerminalLabel(): String? {
        return securePrefs?.getString(KEY_TERMINAL_LABEL, null)
    }

    /**
     * Check if terminal is paired (has an API key) or identified (external mode)
     */
    fun isTerminalPaired(): Boolean {
        return !getTerminalApiKey().isNullOrEmpty() || isExternalMode()
    }

    /**
     * Clear all terminal information (unpair)
     */
    fun clearTerminalInfo() {
        securePrefs?.edit()?.clear()?.apply()
    }

    // ==================== External Mode Support ====================

    /**
     * Save external mode identification info
     * Used when terminal identifies via Stripe Location ID instead of pairing
     */
    fun saveExternalModeInfo(
        stripeLocationId: String,
        accountId: String,
        companyName: String,
        slug: String
    ) {
        securePrefs?.edit()?.apply {
            putBoolean(KEY_EXTERNAL_MODE, true)
            putString(KEY_STRIPE_LOCATION_ID, stripeLocationId)
            putString(KEY_ACCOUNT_ID, accountId)
            putString(KEY_COMPANY_NAME, companyName)
            putString(KEY_SLUG, slug)
            apply()
        }
    }

    /**
     * Check if terminal is running in external mode (partner-deployed)
     */
    fun isExternalMode(): Boolean {
        return securePrefs?.getBoolean(KEY_EXTERNAL_MODE, false) ?: false
    }

    /**
     * Get the Stripe Location ID (external mode only)
     */
    fun getStripeLocationId(): String? {
        return securePrefs?.getString(KEY_STRIPE_LOCATION_ID, null)
    }

    /**
     * Get the company name
     */
    fun getCompanyName(): String? {
        return securePrefs?.getString(KEY_COMPANY_NAME, null)
    }

    /**
     * Get the account slug
     */
    fun getSlug(): String? {
        return securePrefs?.getString(KEY_SLUG, null)
    }

    /**
     * Check if terminal is configured (either paired or identified)
     */
    fun isTerminalConfigured(): Boolean {
        return isTerminalPaired() || isExternalMode()
    }
}
