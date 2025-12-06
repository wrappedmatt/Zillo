package com.stripe.aod.sampleapp.fragment

import android.app.AlertDialog
import android.content.Intent
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import androidx.fragment.app.Fragment
import androidx.fragment.app.activityViewModels
import androidx.lifecycle.lifecycleScope
import androidx.navigation.fragment.findNavController
import com.google.android.material.snackbar.Snackbar
import com.lemonade.terminal.loyalty.service.BrandingService
import com.lemonade.terminal.loyalty.storage.SecureStorage
import com.stripe.aod.sampleapp.R
import com.stripe.aod.sampleapp.activity.MainActivity
import com.stripe.aod.sampleapp.databinding.DialogSettingsBinding
import com.stripe.aod.sampleapp.databinding.FragmentHomeBinding
import com.stripe.aod.sampleapp.model.MainViewModel
import com.stripe.aod.sampleapp.network.ApiClient
import com.stripe.aod.sampleapp.utils.launchAndRepeatWithViewLifecycle
import com.stripe.aod.sampleapp.utils.navOptions
import com.stripe.aod.sampleapp.utils.setThrottleClickListener
import com.stripe.stripeterminal.external.models.ConnectionStatus
import com.stripe.stripeterminal.external.models.PaymentStatus
import kotlinx.coroutines.flow.filter
import kotlinx.coroutines.launch

class HomeFragment : Fragment(R.layout.fragment_home) {
    private val viewModel: MainViewModel by activityViewModels()

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)

        val viewBinding = FragmentHomeBinding.bind(view)

        viewBinding.menuSettings.setThrottleClickListener {
            showSettingsDialog()
        }

        launchAndRepeatWithViewLifecycle {
            viewModel.readerConnectStatus.collect {
                viewBinding.indicator.visibility = if (it != ConnectionStatus.CONNECTED) {
                    View.VISIBLE
                } else {
                    View.INVISIBLE
                }
            }
        }

        launchAndRepeatWithViewLifecycle {
            viewModel.readerPaymentStatus.collect {
                viewBinding.newPayment.isEnabled = (it == PaymentStatus.READY)
            }
        }

        launchAndRepeatWithViewLifecycle {
            viewModel.userMessage.filter {
                it.isNotEmpty()
            }.collect { message ->
                Snackbar.make(viewBinding.newPayment, message, Snackbar.LENGTH_SHORT).show()
            }
        }

        viewBinding.newPayment.setThrottleClickListener {
            findNavController().navigate(
                HomeFragmentDirections.actionHomeFragmentToInputFragment(),
                navOptions()
            )
        }
    }

    private fun showSettingsDialog() {
        val dialogBinding = DialogSettingsBinding.inflate(LayoutInflater.from(requireContext()))
        val dialog = AlertDialog.Builder(requireContext())
            .setView(dialogBinding.root)
            .create()

        dialogBinding.refreshBranding.setOnClickListener {
            lifecycleScope.launch {
                try {
                    val result = BrandingService.fetchBrandingSettings()
                    if (result.isSuccess) {
                        Snackbar.make(requireView(), "Branding refreshed successfully", Snackbar.LENGTH_SHORT).show()
                        dialog.dismiss()
                    } else {
                        Snackbar.make(requireView(), "Failed to refresh branding", Snackbar.LENGTH_SHORT).show()
                    }
                } catch (e: Exception) {
                    Snackbar.make(requireView(), "Error: ${e.message}", Snackbar.LENGTH_SHORT).show()
                }
            }
        }

        dialogBinding.logoutButton.setOnClickListener {
            dialog.dismiss()
            showLogoutConfirmation()
        }

        dialogBinding.cancelButton.setOnClickListener {
            dialog.dismiss()
        }

        dialog.show()
    }

    private fun showLogoutConfirmation() {
        AlertDialog.Builder(requireContext())
            .setTitle("Logout")
            .setMessage("Are you sure you want to logout? This will unpair the terminal.")
            .setPositiveButton("Logout") { _, _ ->
                performLogout()
            }
            .setNegativeButton("Cancel", null)
            .show()
    }

    private fun performLogout() {
        // Clear terminal info from secure storage
        SecureStorage.clearTerminalInfo()

        // Clear API key from ApiClient
        ApiClient.terminalApiKey = null

        // Restart the app to show pairing screen
        val intent = Intent(requireContext(), MainActivity::class.java)
        intent.flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TASK
        startActivity(intent)
        requireActivity().finish()
    }
}
