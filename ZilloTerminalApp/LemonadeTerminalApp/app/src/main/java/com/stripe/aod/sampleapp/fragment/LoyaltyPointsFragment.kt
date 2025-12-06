package com.stripe.aod.sampleapp.fragment

import android.graphics.drawable.GradientDrawable
import android.os.Bundle
import android.util.Log
import android.view.View
import androidx.fragment.app.Fragment
import androidx.fragment.app.activityViewModels
import androidx.lifecycle.lifecycleScope
import androidx.navigation.fragment.findNavController
import androidx.navigation.fragment.navArgs
import com.google.android.material.snackbar.Snackbar
import com.lemonade.terminal.loyalty.service.BrandingService
import com.stripe.aod.sampleapp.Config
import com.stripe.aod.sampleapp.R
import com.stripe.aod.sampleapp.databinding.FragmentLoyaltyPointsBinding
import com.stripe.aod.sampleapp.model.CheckoutViewModel
import com.stripe.aod.sampleapp.network.ApiClient
import com.stripe.aod.sampleapp.utils.backToHome
import com.stripe.aod.sampleapp.utils.navOptions
import com.stripe.aod.sampleapp.utils.setThrottleClickListener
import kotlinx.coroutines.launch

class LoyaltyPointsFragment : Fragment(R.layout.fragment_loyalty_points) {
    private val args: LoyaltyPointsFragmentArgs by navArgs()
    private val checkoutViewModel by activityViewModels<CheckoutViewModel>()

    // Store signup info for navigation
    private var signupUrl: String = ""
    private var unclaimedPoints: Int = 0

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)
        val viewBinding = FragmentLoyaltyPointsBinding.bind(view)

        // Clear the payment intent to prevent re-navigation when returning to InputFragment
        checkoutViewModel.clearCurrentPaymentIntent()

        // Calculate loyalty reward based on loyalty system type (amount is in cents)
        val rewardText = Config.calculateReward(args.amount.toLong())
        val points = args.amount / 100 // Keep for backward compatibility with applyBranding

        // Apply branding
        applyBranding(viewBinding, rewardText)

        // Hide entire content container until API call completes
        viewBinding.contentContainer.visibility = View.GONE

        // Capture the payment and check if card is registered
        lifecycleScope.launch {
            capturePaymentAndCheckCard(viewBinding, points)
        }

        // Handle "Claim Points" button click - navigate to QR code screen
        viewBinding.claimPointsButton.setThrottleClickListener {
            findNavController().navigate(
                LoyaltyPointsFragmentDirections.actionLoyaltyPointsFragmentToQrCodeFragment(
                    signupUrl = signupUrl,
                    unclaimedPoints = unclaimedPoints
                ),
                navOptions()
            )
        }

        // Handle "Dismiss" button click - return to home
        viewBinding.dismissButton.setOnClickListener {
            backToHome()
        }
    }

    private suspend fun capturePaymentAndCheckCard(viewBinding: FragmentLoyaltyPointsBinding, points: Int) {
        val paymentIntentId = args.paymentIntentId
        if (paymentIntentId.isEmpty()) {
            Log.d(Config.TAG, "Payment intent ID is empty - payment was already captured in redemption flow")

            // Payment was already captured, show the screen with rewards earned
            // Get customer credit to determine if card is registered
            val customerCredit = checkoutViewModel.lookupCustomerCredit()

            if (customerCredit != null && customerCredit.cardRegistered) {
                // Card is registered - hide claim points button
                viewBinding.claimPointsButton.visibility = View.GONE
                viewBinding.contentContainer.visibility = View.VISIBLE
                Log.d(Config.TAG, "Card is registered to customer ${customerCredit.customerId}")
            } else if (customerCredit != null && !customerCredit.cardRegistered) {
                // Card not registered - show claim points button
                signupUrl = customerCredit.signupUrl ?: BrandingService.getCurrentBranding().signupUrl
                unclaimedPoints = customerCredit.unclaimedPoints ?: 0

                viewBinding.claimPointsButton.visibility = View.VISIBLE
                viewBinding.contentContainer.visibility = View.VISIBLE
                Log.d(Config.TAG, "Card not registered. Unclaimed points: $unclaimedPoints, Signup URL: $signupUrl")
            } else {
                // Error case - just show the screen without claim button
                viewBinding.claimPointsButton.visibility = View.GONE
                viewBinding.contentContainer.visibility = View.VISIBLE
                Log.e(Config.TAG, "Unable to lookup customer credit")
            }

            return
        }

        Log.d(Config.TAG, "Capturing payment: $paymentIntentId")

        try {
            val result = ApiClient.capturePaymentIntent(paymentIntentId)

            result.onSuccess { captureResponse ->
                Log.d(Config.TAG, "Payment captured successfully. Card registered: ${captureResponse.cardRegistered}")

                if (captureResponse.cardRegistered) {
                    // Card is registered to a customer - show "recognized customer" screen
                    Log.d(Config.TAG, "Card is registered to customer ${captureResponse.customerId}")

                    // Hide claim points button for registered customers
                    viewBinding.claimPointsButton.visibility = View.GONE

                    // Show the container with all content
                    viewBinding.contentContainer.visibility = View.VISIBLE

                    // Show welcome back message
                    Snackbar.make(viewBinding.root, "Welcome back! Points added to your account.", Snackbar.LENGTH_LONG).show()
                } else {
                    // Card is NOT registered - show unclaimed points screen with "Claim Points" button
                    signupUrl = captureResponse.signupUrl ?: ""
                    unclaimedPoints = captureResponse.unclaimedPoints ?: 0

                    Log.d(Config.TAG, "Card not registered. Unclaimed points: $unclaimedPoints, Signup URL: $signupUrl")

                    // Show claim points button for unregistered customers
                    viewBinding.claimPointsButton.visibility = View.VISIBLE

                    // Show the container with all content
                    viewBinding.contentContainer.visibility = View.VISIBLE
                }
            }.onFailure { error ->
                Log.e(Config.TAG, "Failed to capture payment", error)

                // Show container even on error
                viewBinding.claimPointsButton.visibility = View.GONE
                viewBinding.contentContainer.visibility = View.VISIBLE

                Snackbar.make(viewBinding.root, "Error processing payment: ${error.message}", Snackbar.LENGTH_LONG).show()
            }
        } catch (e: Exception) {
            Log.e(Config.TAG, "Exception capturing payment", e)
            Snackbar.make(viewBinding.root, "Error: ${e.message}", Snackbar.LENGTH_LONG).show()
        }
    }

    private fun applyBranding(viewBinding: FragmentLoyaltyPointsBinding, rewardText: String) {
        val branding = BrandingService.getCurrentBranding()

        // Apply background color
        viewBinding.coordinatorLayout.setBackgroundColor(
            BrandingService.parseColor(branding.backgroundColor)
        )

        // Apply text colors
        val textColor = BrandingService.parseColor(branding.textColor)
        viewBinding.successMessage.setTextColor(textColor)
        viewBinding.pointsEarnedLabel.apply {
            text = branding.headlineText
            setTextColor(textColor)
        }
        viewBinding.loyaltyPoints.apply {
            text = rewardText // Use dynamic reward text based on loyalty system type
            setTextColor(textColor)
        }
        viewBinding.pointsDescription.apply {
            text = branding.subheadlineText
            setTextColor(textColor)
        }

        // Apply button colors
        val buttonColor = BrandingService.parseColor(branding.buttonColor)
        val buttonTextColor = BrandingService.parseColor(branding.buttonTextColor)

        viewBinding.claimPointsButton.apply {
            setTextColor(buttonTextColor)
            background = GradientDrawable().apply {
                setColor(buttonColor)
                cornerRadius = 8f * resources.displayMetrics.density
            }
        }
    }
}
