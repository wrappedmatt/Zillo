package com.stripe.aod.sampleapp.activity

import android.Manifest
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.location.LocationManager
import android.os.Build
import android.os.Bundle
import android.provider.Settings
import android.view.ContextThemeWrapper
import androidx.activity.result.contract.ActivityResultContracts
import androidx.activity.viewModels
import androidx.annotation.RequiresApi
import androidx.appcompat.app.AlertDialog
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import androidx.lifecycle.lifecycleScope
import com.zillo.terminal.loyalty.fragment.ExternalModeFragment
import com.zillo.terminal.loyalty.fragment.PairingFragment
import com.zillo.terminal.loyalty.service.BrandingService
import com.zillo.terminal.loyalty.storage.SecureStorage
import com.stripe.aod.sampleapp.R
import kotlinx.coroutines.launch
import com.stripe.aod.sampleapp.listener.TerminalEventListener
import com.stripe.aod.sampleapp.model.MainViewModel
import com.stripe.aod.sampleapp.network.ApiClient
import com.stripe.stripeterminal.Terminal
import com.stripe.stripeterminal.external.models.TerminalException
import com.stripe.stripeterminal.log.LogLevel

class MainActivity : AppCompatActivity(),
    PairingFragment.PairingListener,
    ExternalModeFragment.ExternalModeListener,
    ExternalModeFragment.ModeSelectionListener {
    private val viewModel by viewModels<MainViewModel>()

    private val requestPermissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions(),
        ::onPermissionResult
    )

    public override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        // Initialize SecureStorage
        SecureStorage.init(applicationContext)

        // Check if terminal is configured (either paired or external mode)
        if (!SecureStorage.isTerminalConfigured()) {
            // Show pairing fragment directly without loading main layout
            showPairingFragment()
        } else {
            // Load API key for paired mode (not needed for external mode)
            if (!SecureStorage.isExternalMode()) {
                ApiClient.terminalApiKey = SecureStorage.getTerminalApiKey()
            }

            // Load main UI
            setContentView(R.layout.activity_main)
            requestPermissionsIfNecessary()

            // Fetch branding settings based on mode
            lifecycleScope.launch {
                if (SecureStorage.isExternalMode()) {
                    // In external mode, re-fetch settings using identify
                    BrandingService.fetchExternalModeSettings()
                } else {
                    // In paired mode, use normal branding fetch
                    BrandingService.fetchBrandingSettings()
                }
            }
        }
    }

    private fun showPairingFragment() {
        // Create pairing fragment as the main content (no activity_main layout)
        supportFragmentManager.beginTransaction()
            .replace(android.R.id.content, PairingFragment())
            .commit()
    }

    override fun onPairingComplete() {
        // Recreate activity to reload with paired state
        recreate()
    }

    override fun onExternalModeComplete() {
        // Recreate activity to reload with external mode configuration
        recreate()
    }

    override fun onSwitchToPairingMode() {
        // Switch to pairing fragment
        showPairingFragment()
    }

    override fun onSwitchToExternalMode() {
        // Switch to external mode fragment
        showExternalModeFragment()
    }

    private fun showExternalModeFragment() {
        supportFragmentManager.beginTransaction()
            .replace(android.R.id.content, ExternalModeFragment())
            .commit()
    }

    private fun requestPermissionsIfNecessary() {
        if (Build.VERSION.SDK_INT >= 31) {
            requestPermissionsIfNecessarySdk31()
        } else {
            requestPermissionsIfNecessarySdkBelow31()
        }
    }

    private fun isGranted(permission: String): Boolean {
        return ContextCompat.checkSelfPermission(
            this,
            permission
        ) == PackageManager.PERMISSION_GRANTED
    }

    private fun requestPermissionsIfNecessarySdkBelow31() {
        // Check for location permissions
        if (!isGranted(Manifest.permission.ACCESS_FINE_LOCATION)) {
            // If we don't have them yet, request them before doing anything else
            requestPermissionLauncher.launch(arrayOf(Manifest.permission.ACCESS_FINE_LOCATION))
        } else if (!Terminal.isInitialized() && verifyGpsEnabled()) {
            initialize()
        }
    }

    @RequiresApi(Build.VERSION_CODES.S)
    private fun requestPermissionsIfNecessarySdk31() {
        // Check for location and bluetooth permissions
        val deniedPermissions = listOf(
            Manifest.permission.ACCESS_FINE_LOCATION,
            Manifest.permission.BLUETOOTH_CONNECT,
            Manifest.permission.BLUETOOTH_SCAN
        )
            .filterNot(::isGranted)
            .toTypedArray()

        if (deniedPermissions.isNotEmpty()) {
            // If we don't have them yet, request them before doing anything else
            requestPermissionLauncher.launch(deniedPermissions)
        } else if (!Terminal.isInitialized() && verifyGpsEnabled()) {
            initialize()
        }
    }

    private fun onPermissionResult(result: Map<String, Boolean>) {
        val deniedPermissions: List<String> = result
            .filter { !it.value }
            .map { it.key }

        // If we receive a response to our permission check, initialize
        if (deniedPermissions.isEmpty() && !Terminal.isInitialized() && verifyGpsEnabled()) {
            initialize()
        }
    }

    /**
     * Initialize the [Terminal]
     */
    private fun initialize() {
        // Initialize the Terminal as soon as possible
        try {
            Terminal.init(
                context = applicationContext,
                logLevel = LogLevel.VERBOSE,
                tokenProvider = viewModel.tokenProvider,
                listener = TerminalEventListener,
                offlineListener = null,
            )

            viewModel.easyConnect()
        } catch (e: TerminalException) {
            throw RuntimeException(
                "Location services are required in order to initialize the Terminal.",
                e
            )
        }
    }

    private fun verifyGpsEnabled(): Boolean {
        val locationManager: LocationManager? =
            applicationContext.getSystemService(Context.LOCATION_SERVICE) as? LocationManager

        val gpsEnabled = runCatching {
            locationManager?.isProviderEnabled(LocationManager.GPS_PROVIDER) ?: false
        }.getOrDefault(false)

        if (!gpsEnabled) {
            // notify user
            AlertDialog.Builder(
                ContextThemeWrapper(
                    this,
                    com.google.android.material.R.style.Theme_MaterialComponents_DayNight_DarkActionBar
                )
            )
                .setMessage("Please enable location services")
                .setCancelable(false)
                .setPositiveButton("Open location settings") { _, _ ->
                    this.startActivity(Intent(Settings.ACTION_LOCATION_SOURCE_SETTINGS))
                }
                .create()
                .show()
        }

        return gpsEnabled
    }
}
