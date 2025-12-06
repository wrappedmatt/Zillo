package com.stripe.aod.sampleapp.fragment

import android.os.Bundle
import android.view.View
import androidx.fragment.app.Fragment
import androidx.fragment.app.activityViewModels
import androidx.lifecycle.lifecycleScope
import androidx.navigation.fragment.findNavController
import androidx.navigation.fragment.navArgs
import com.stripe.aod.sampleapp.Config
import com.stripe.aod.sampleapp.R
import com.stripe.aod.sampleapp.databinding.FragmentRedemptionBinding
import com.stripe.aod.sampleapp.model.CheckoutViewModel
import com.stripe.aod.sampleapp.utils.backToHome
import com.stripe.aod.sampleapp.utils.navOptions
import com.stripe.aod.sampleapp.utils.setThrottleClickListener
import kotlinx.coroutines.launch
import java.text.NumberFormat
import java.util.Locale

class RedemptionFragment : Fragment(R.layout.fragment_redemption) {
    private val args: RedemptionFragmentArgs by navArgs()
    private val checkoutViewModel by activityViewModels<CheckoutViewModel>()

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)
        val viewBinding = FragmentRedemptionBinding.bind(view)

        // Get branding settings
        val branding = com.lemonade.terminal.loyalty.service.BrandingService.getCurrentBranding()

        // Apply branding
        applyBranding(viewBinding, branding)

        // Format currency values
        val orderAmount = args.amount / 100.0

        // Display order amount (without currency symbol to avoid emoji/icon)
        viewBinding.orderAmount.text = String.format("%.2f", orderAmount)

        // Hide UI elements while loading to prevent flash before navigation
        viewBinding.headline.visibility = View.GONE
        viewBinding.transactionLabel.visibility = View.GONE
        viewBinding.orderAmount.visibility = View.GONE
        viewBinding.creditAvailableLabel.visibility = View.GONE
        viewBinding.creditAvailable.visibility = View.GONE
        viewBinding.redeemButton.visibility = View.GONE
        viewBinding.dismissButton.visibility = View.GONE
        viewBinding.redeemButton.isEnabled = false

        // Lookup customer credit
        // Payment is already confirmed, so fingerprint is guaranteed to be available
        lifecycleScope.launch {
            val customerCredit = checkoutViewModel.lookupCustomerCredit()

            if (customerCredit != null && !customerCredit.cardRegistered) {
                // Card not registered - navigate to payment success screen with payment intent ID
                // LoyaltyPointsFragment will capture the payment and show "Claim" button
                val paymentIntentId = checkoutViewModel.currentPaymentIntent.value?.id ?: ""
                android.util.Log.d("RedemptionFragment", "Card not registered, navigating to payment success screen (PI: $paymentIntentId)")

                findNavController().navigate(
                    RedemptionFragmentDirections.actionRedemptionFragmentToLoyaltyPointsFragment(
                        amount = args.amount.toInt(),
                        paymentIntentId = paymentIntentId
                    ),
                    navOptions()
                )
            } else if (customerCredit != null && customerCredit.customerId != null) {
                // Get credit balance based on loyalty system type
                val creditBalanceCents = if (Config.loyaltySystemType == "cashback") {
                    customerCredit.cashbackBalance
                } else {
                    // For points system, use creditBalance (legacy field for backward compatibility)
                    customerCredit.creditBalance
                }
                val creditAvailable = creditBalanceCents / 100.0

                android.util.Log.d("RedemptionFragment", "Customer credit loaded: ${customerCredit.customerId}, credit: $$creditAvailable (${Config.loyaltySystemType})")

                if (creditBalanceCents == 0L) {
                    // Customer has $0 balance - auto-capture and proceed
                    android.util.Log.d("RedemptionFragment", "Customer has $0 balance, auto-capturing full amount")
                    val success = checkoutViewModel.captureFullAmount()

                    if (success) {
                        // Navigate to loyalty points screen (payment already captured, pass empty ID)
                        findNavController().navigate(
                            RedemptionFragmentDirections.actionRedemptionFragmentToLoyaltyPointsFragment(
                                amount = args.amount.toInt(),
                                paymentIntentId = ""
                            ),
                            navOptions()
                        )
                    } else {
                        // Show error and return to home
                        backToHome()
                    }
                } else {
                    // Customer has credit - show redemption options
                    // Update UI with credit amount using Config helper
                    val balanceText = Config.formatBalance(customerCredit.pointsBalance, customerCredit.cashbackBalance)
                    viewBinding.creditAvailable.text = balanceText

                    // Show all UI elements now that we have data
                    viewBinding.headline.visibility = View.VISIBLE
                    viewBinding.transactionLabel.visibility = View.VISIBLE
                    viewBinding.orderAmount.visibility = View.VISIBLE
                    viewBinding.creditAvailableLabel.visibility = View.VISIBLE
                    viewBinding.creditAvailable.visibility = View.VISIBLE
                    viewBinding.redeemButton.visibility = View.VISIBLE
                    viewBinding.dismissButton.visibility = View.VISIBLE
                    viewBinding.redeemButton.isEnabled = true

                    // Store for button handlers
                    setupButtonHandlers(viewBinding, orderAmount, creditAvailable, customerCredit.customerId)
                }
            } else {
                // Unexpected error
                android.util.Log.e("RedemptionFragment", "Unexpected error: No customer credit available")
                viewBinding.creditAvailable.text = "$0.00"
                viewBinding.redeemButton.isEnabled = false
                com.google.android.material.snackbar.Snackbar.make(
                    viewBinding.root,
                    "Error loading loyalty account",
                    com.google.android.material.snackbar.Snackbar.LENGTH_LONG
                ).show()
            }
        }
    }

    private fun setupButtonHandlers(
        viewBinding: FragmentRedemptionBinding,
        orderAmount: Double,
        creditAvailable: Double,
        customerId: String
    ) {

        // Handle "Redeem" button click
        viewBinding.redeemButton.setThrottleClickListener {
            // Disable buttons and show loading state
            viewBinding.redeemButton.isEnabled = false
            viewBinding.dismissButton.isEnabled = false
            viewBinding.redeemButton.alpha = 0.6f
            viewBinding.dismissButton.alpha = 0.6f
            viewBinding.redeemButton.text = "Processing..."

            lifecycleScope.launch {
                // Calculate credit to redeem (minimum of available credit and order amount)
                val creditToRedeem = minOf(creditAvailable, orderAmount)
                val creditToRedeemCents = (creditToRedeem * 100).toLong()

                // Calculate final amount paid (after redemption)
                val finalAmountPaid = (args.amount - creditToRedeemCents).toInt()

                android.util.Log.d("RedemptionFragment", "Redeeming $creditToRedeemCents cents credit, final amount: $finalAmountPaid cents")

                // Capture payment with redemption (partial capture)
                // Payment is already authorized for full amount - capture less
                val success = checkoutViewModel.captureWithRedemption(
                    customerId,
                    creditToRedeemCents
                )

                if (success) {
                    // Navigate to loyalty points screen with actual amount paid (payment already captured, pass empty ID)
                    findNavController().navigate(
                        RedemptionFragmentDirections.actionRedemptionFragmentToLoyaltyPointsFragment(
                            amount = finalAmountPaid,
                            paymentIntentId = ""
                        ),
                        navOptions()
                    )
                } else {
                    // Re-enable buttons on error
                    viewBinding.redeemButton.isEnabled = true
                    viewBinding.dismissButton.isEnabled = true
                    viewBinding.redeemButton.alpha = 1.0f
                    viewBinding.dismissButton.alpha = 1.0f
                    viewBinding.redeemButton.text = "Use Credit"

                    // Show error and return to home
                    backToHome()
                }
            }
        }

        // Handle "Dismiss" button click - capture full amount without redemption
        viewBinding.dismissButton.setOnClickListener {
            // Disable buttons and show loading state
            viewBinding.redeemButton.isEnabled = false
            viewBinding.dismissButton.isEnabled = false
            viewBinding.redeemButton.alpha = 0.6f
            viewBinding.dismissButton.alpha = 0.6f
            viewBinding.dismissButton.text = "Processing..."

            lifecycleScope.launch {
                android.util.Log.d("RedemptionFragment", "Capturing full amount without redemption")

                val success = checkoutViewModel.captureFullAmount()

                if (success) {
                    // Navigate to loyalty points screen (payment already captured, pass empty ID)
                    findNavController().navigate(
                        RedemptionFragmentDirections.actionRedemptionFragmentToLoyaltyPointsFragment(
                            amount = args.amount.toInt(),
                            paymentIntentId = ""
                        ),
                        navOptions()
                    )
                } else {
                    // Re-enable buttons on error
                    viewBinding.redeemButton.isEnabled = true
                    viewBinding.dismissButton.isEnabled = true
                    viewBinding.redeemButton.alpha = 1.0f
                    viewBinding.dismissButton.alpha = 1.0f
                    viewBinding.dismissButton.text = "Skip"

                    // Show error and return to home
                    backToHome()
                }
            }
        }
    }

    private fun applyBranding(viewBinding: FragmentRedemptionBinding, branding: com.lemonade.terminal.loyalty.data.BrandingSettings) {
        // Apply background color
        viewBinding.root.setBackgroundColor(
            com.lemonade.terminal.loyalty.service.BrandingService.parseColor(branding.backgroundColor)
        )

        // Apply text colors
        val textColor = com.lemonade.terminal.loyalty.service.BrandingService.parseColor(branding.textColor)

        viewBinding.headline.setTextColor(textColor)

        val secondaryTextColor = android.graphics.Color.argb(
            180,
            android.graphics.Color.red(textColor),
            android.graphics.Color.green(textColor),
            android.graphics.Color.blue(textColor)
        )

        viewBinding.transactionLabel.setTextColor(secondaryTextColor)
        viewBinding.creditAvailableLabel.setTextColor(secondaryTextColor)
        viewBinding.orderAmount.setTextColor(textColor)
        viewBinding.creditAvailable.setTextColor(textColor)

        // Apply button colors
        val buttonColor = com.lemonade.terminal.loyalty.service.BrandingService.parseColor(branding.buttonColor)
        val buttonTextColor = com.lemonade.terminal.loyalty.service.BrandingService.parseColor(branding.buttonTextColor)

        viewBinding.redeemButton.apply {
            setTextColor(buttonTextColor)
            background = android.graphics.drawable.GradientDrawable().apply {
                setColor(buttonColor)
                cornerRadius = 8f * resources.displayMetrics.density
            }
        }
    }
}
