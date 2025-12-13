package com.zillo.terminal.loyalty.fragment

import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.Button
import android.widget.EditText
import android.widget.ProgressBar
import android.widget.TextView
import android.widget.Toast
import androidx.fragment.app.Fragment
import androidx.lifecycle.lifecycleScope
import com.google.android.material.textfield.TextInputLayout
import com.zillo.terminal.loyalty.service.BrandingService
import com.zillo.terminal.loyalty.storage.SecureStorage
import com.stripe.aod.sampleapp.R
import com.stripe.aod.sampleapp.network.ApiClient
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/**
 * Fragment for external mode terminal identification.
 * Used when the Zillo app is deployed by a partner platform (e.g., Lightspeed)
 * and needs to identify which Zillo account to connect to.
 *
 * Supports two identification methods:
 * 1. Pairing code - Simple 6-character code from Zillo dashboard (primary)
 * 2. Stripe Location ID - For advanced users who know their tml_xxx ID
 */
class ExternalModeFragment : Fragment() {

    private lateinit var pairingCodeInput: EditText
    private lateinit var pairingCodeLayout: TextInputLayout
    private lateinit var locationIdInput: EditText
    private lateinit var locationIdLayout: TextInputLayout
    private lateinit var identifyButton: Button
    private lateinit var progressBar: ProgressBar
    private lateinit var statusText: TextView
    private lateinit var pairingModeLink: TextView
    private lateinit var manualLocationLink: TextView

    private var showingLocationInput = false

    override fun onCreateView(
        inflater: LayoutInflater,
        container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View? {
        return inflater.inflate(R.layout.fragment_external_mode, container, false)
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)

        pairingCodeInput = view.findViewById(R.id.pairing_code_input)
        pairingCodeLayout = view.findViewById(R.id.pairing_code_layout)
        locationIdInput = view.findViewById(R.id.location_id_input)
        locationIdLayout = view.findViewById(R.id.location_id_layout)
        identifyButton = view.findViewById(R.id.identify_button)
        progressBar = view.findViewById(R.id.progress_bar)
        statusText = view.findViewById(R.id.status_text)
        pairingModeLink = view.findViewById(R.id.pairing_mode_link)
        manualLocationLink = view.findViewById(R.id.manual_location_link)

        // Initialize SecureStorage
        context?.let { SecureStorage.init(it.applicationContext) }

        // Toggle manual location ID input
        manualLocationLink.setOnClickListener {
            toggleLocationInput()
        }

        identifyButton.setOnClickListener {
            val pairingCode = pairingCodeInput.text.toString().trim().uppercase()
            val locationId = locationIdInput.text.toString().trim()

            // Prefer pairing code if provided
            if (pairingCode.isNotEmpty()) {
                identifyByPairingCode(pairingCode)
            } else if (locationId.isNotEmpty()) {
                identifyByLocationId(locationId)
            } else {
                Toast.makeText(context, "Please enter a pairing code", Toast.LENGTH_SHORT).show()
            }
        }

        pairingModeLink.setOnClickListener {
            // Switch to Zillo pairing mode
            (activity as? ModeSelectionListener)?.onSwitchToPairingMode()
        }
    }

    private fun toggleLocationInput() {
        showingLocationInput = !showingLocationInput
        locationIdLayout.visibility = if (showingLocationInput) View.VISIBLE else View.GONE
        manualLocationLink.text = if (showingLocationInput)
            "Hide Stripe Location ID field"
        else
            "Enter Stripe Location ID manually"
    }

    private fun identifyByPairingCode(pairingCode: String) {
        lifecycleScope.launch {
            try {
                setLoadingState(true)
                statusText.text = "Connecting with pairing code..."

                val result = withContext(Dispatchers.IO) {
                    ApiClient.identifyByPairingCode(pairingCode)
                }

                result.onSuccess { response ->
                    handleSuccessfulIdentification(response, response.stripeLocationId ?: "")
                }.onFailure { error ->
                    handleIdentifyError(error, isPairingCode = true)
                }

            } catch (e: Exception) {
                handleIdentifyError(e, isPairingCode = true)
            } finally {
                setLoadingState(false)
            }
        }
    }

    private fun identifyByLocationId(locationId: String) {
        lifecycleScope.launch {
            try {
                setLoadingState(true)
                statusText.text = "Identifying terminal by location..."

                val result = withContext(Dispatchers.IO) {
                    ApiClient.identifyByLocation(locationId)
                }

                result.onSuccess { response ->
                    handleSuccessfulIdentification(response, locationId)
                }.onFailure { error ->
                    handleIdentifyError(error, isPairingCode = false)
                }

            } catch (e: Exception) {
                handleIdentifyError(e, isPairingCode = false)
            } finally {
                setLoadingState(false)
            }
        }
    }

    private fun handleSuccessfulIdentification(response: com.zillo.terminal.loyalty.data.IdentifyResponse, stripeLocationId: String) {
        // Initialize branding from response
        BrandingService.initializeFromIdentifyResponse(response)

        // Save external mode info to secure storage
        SecureStorage.saveExternalModeInfo(
            stripeLocationId = stripeLocationId,
            accountId = response.accountId,
            companyName = response.companyName,
            slug = response.slug
        )

        // Show success message
        statusText.text = "Connected to ${response.companyName}!"
        Toast.makeText(context, "Terminal connected successfully!", Toast.LENGTH_LONG).show()

        // Notify parent activity to navigate to main screen
        (activity as? ExternalModeListener)?.onExternalModeComplete()
    }

    private fun handleIdentifyError(error: Throwable, isPairingCode: Boolean) {
        val message = when {
            error.message?.contains("404") == true || error.message?.contains("not found") == true ->
                if (isPairingCode)
                    "Invalid pairing code. Please check the code and try again."
                else
                    "Location not linked to a Zillo account. Please configure this location in your Zillo dashboard."
            error.message?.contains("network", ignoreCase = true) == true ->
                "Network error. Please check your connection."
            else ->
                "Connection failed: ${error.message}"
        }

        statusText.text = message
        Toast.makeText(context, message, Toast.LENGTH_LONG).show()
    }

    private fun setLoadingState(loading: Boolean) {
        identifyButton.isEnabled = !loading
        pairingCodeInput.isEnabled = !loading
        locationIdInput.isEnabled = !loading
        pairingModeLink.isEnabled = !loading
        manualLocationLink.isEnabled = !loading
        progressBar.visibility = if (loading) View.VISIBLE else View.GONE
    }

    /**
     * Interface for activities hosting this fragment to receive identification completion events
     */
    interface ExternalModeListener {
        fun onExternalModeComplete()
    }

    /**
     * Interface for switching between pairing and external modes
     */
    interface ModeSelectionListener {
        fun onSwitchToPairingMode()
        fun onSwitchToExternalMode()
    }
}
