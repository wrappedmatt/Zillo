package com.zillo.terminal.loyalty.fragment

import android.os.Build
import android.os.Bundle
import android.provider.Settings
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
import com.zillo.terminal.loyalty.storage.SecureStorage
import com.stripe.aod.sampleapp.R
import com.stripe.aod.sampleapp.network.ApiClient
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/**
 * Fragment for pairing a terminal device with the Zillo backend
 * User enters a pairing code generated from the dashboard
 */
class PairingFragment : Fragment() {

    private lateinit var pairingCodeInput: EditText
    private lateinit var terminalLabelInput: EditText
    private lateinit var pairButton: Button
    private lateinit var progressBar: ProgressBar
    private lateinit var statusText: TextView
    private lateinit var externalModeLink: TextView

    override fun onCreateView(
        inflater: LayoutInflater,
        container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View? {
        return inflater.inflate(R.layout.fragment_pairing, container, false)
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)

        pairingCodeInput = view.findViewById(R.id.pairing_code_input)
        terminalLabelInput = view.findViewById(R.id.terminal_label_input)
        pairButton = view.findViewById(R.id.pair_button)
        progressBar = view.findViewById(R.id.progress_bar)
        statusText = view.findViewById(R.id.status_text)
        externalModeLink = view.findViewById(R.id.external_mode_link)

        // Initialize SecureStorage
        context?.let { SecureStorage.init(it.applicationContext) }

        // Set default terminal label
        terminalLabelInput.setText(getDefaultTerminalLabel())

        // Handle switch to external mode
        externalModeLink.setOnClickListener {
            (activity as? ExternalModeFragment.ModeSelectionListener)?.onSwitchToExternalMode()
        }

        pairButton.setOnClickListener {
            val pairingCode = pairingCodeInput.text.toString().trim()
            val terminalLabel = terminalLabelInput.text.toString().trim()

            if (pairingCode.isEmpty()) {
                Toast.makeText(context, "Please enter a pairing code", Toast.LENGTH_SHORT).show()
                return@setOnClickListener
            }

            if (terminalLabel.isEmpty()) {
                Toast.makeText(context, "Please enter a terminal label", Toast.LENGTH_SHORT).show()
                return@setOnClickListener
            }

            pairTerminal(pairingCode, terminalLabel)
        }
    }

    private fun getDefaultTerminalLabel(): String {
        // Get device model name
        val model = Build.MODEL
        val manufacturer = Build.MANUFACTURER

        return if (manufacturer.equals("unknown", ignoreCase = true)) {
            model
        } else {
            "$manufacturer $model"
        }
    }

    private fun getDeviceId(): String {
        // Get Android device ID (unique per-device)
        return Settings.Secure.getString(
            requireContext().contentResolver,
            Settings.Secure.ANDROID_ID
        )
    }

    private fun pairTerminal(pairingCode: String, terminalLabel: String) {
        lifecycleScope.launch {
            try {
                // Show loading UI
                setLoadingState(true)
                statusText.text = "Pairing terminal..."

                // Get device information
                val deviceModel = "${Build.MANUFACTURER} ${Build.MODEL}"
                val deviceId = getDeviceId()

                // Call pairing API
                val result = withContext(Dispatchers.IO) {
                    ApiClient.pairTerminal(
                        pairingCode = pairingCode,
                        terminalLabel = terminalLabel,
                        deviceModel = deviceModel,
                        deviceId = deviceId
                    )
                }

                result.onSuccess { response ->
                    // Save terminal info to secure storage
                    SecureStorage.saveTerminalInfo(
                        apiKey = response.apiKey,
                        terminalId = response.terminalId,
                        accountId = response.accountId,
                        terminalLabel = response.terminalLabel
                    )

                    // Set API key in ApiClient for future requests
                    ApiClient.terminalApiKey = response.apiKey

                    // Show success message
                    statusText.text = "Terminal paired successfully!"
                    Toast.makeText(context, "Terminal paired successfully!", Toast.LENGTH_LONG).show()

                    // Notify parent activity to navigate to main screen
                    (activity as? PairingListener)?.onPairingComplete()
                }.onFailure { error ->
                    handlePairingError(error)
                }

            } catch (e: Exception) {
                handlePairingError(e)
            } finally {
                setLoadingState(false)
            }
        }
    }

    private fun handlePairingError(error: Throwable) {
        val message = when {
            error.message?.contains("404") == true || error.message?.contains("Invalid pairing code") == true ->
                "Invalid pairing code. Please check and try again."
            error.message?.contains("expired") == true ->
                "Pairing code has expired. Please generate a new code."
            error.message?.contains("already been used") == true ->
                "This pairing code has already been used. Please generate a new code."
            error.message?.contains("network", ignoreCase = true) == true ->
                "Network error. Please check your connection."
            else ->
                "Pairing failed: ${error.message}"
        }

        statusText.text = message
        Toast.makeText(context, message, Toast.LENGTH_LONG).show()
    }

    private fun setLoadingState(loading: Boolean) {
        pairButton.isEnabled = !loading
        pairingCodeInput.isEnabled = !loading
        terminalLabelInput.isEnabled = !loading
        progressBar.visibility = if (loading) View.VISIBLE else View.GONE
    }

    /**
     * Interface for activities hosting this fragment to receive pairing completion events
     */
    interface PairingListener {
        fun onPairingComplete()
    }
}
