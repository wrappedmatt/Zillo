package com.stripe.aod.sampleapp.model

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.stripe.aod.sampleapp.data.CreatePaymentParams
import com.stripe.aod.sampleapp.data.CustomerCreditResponse
import com.stripe.aod.sampleapp.data.EmailReceiptParams
import com.stripe.aod.sampleapp.data.toMap
import com.stripe.aod.sampleapp.network.ApiClient
import com.stripe.stripeterminal.Terminal
import com.stripe.stripeterminal.external.models.CollectPaymentIntentConfiguration
import com.stripe.stripeterminal.external.models.PaymentIntent
import com.stripe.stripeterminal.external.models.TerminalException
import com.stripe.stripeterminal.ktx.collectPaymentMethod
import com.stripe.stripeterminal.ktx.confirmPaymentIntent
import com.stripe.stripeterminal.ktx.processPaymentIntent
import com.stripe.stripeterminal.ktx.retrievePaymentIntent
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

class CheckoutViewModel : ViewModel() {
    private val _currentPaymentIntent = MutableStateFlow<PaymentIntent?>(null)
    val currentPaymentIntent = _currentPaymentIntent.asStateFlow()

    fun createPaymentIntent(
        createPaymentParams: CreatePaymentParams,
        onFailure: (FailureMessage) -> Unit
    ) {
        viewModelScope.launch {
            createAndProcessPaymentIntent(createPaymentParams.toMap())
                .fold(
                    onSuccess = { paymentIntent ->
                        _currentPaymentIntent.update { paymentIntent }
                    },
                    onFailure = {
                        val failureMessage = if (it is TerminalException) {
                            it.errorMessage
                        } else {
                            it.message ?: "Failed to collect payment"
                        }.let(::FailureMessage)
                        onFailure(failureMessage)
                    }
                )
        }
    }

    private suspend fun createAndProcessPaymentIntent(
        createPaymentIntentParams: Map<String, String>
    ): Result<PaymentIntent> {
        return ApiClient.createPaymentIntent(createPaymentIntentParams)
            .mapCatching { response ->
                val secret = response.secret

                val terminal = Terminal.getInstance()
                val paymentIntent = terminal.retrievePaymentIntent(secret)

                // We're using the processPaymentIntent as it combines both collect and confirm
                // calls. Separate collect and confirm calls are still available if you need to
                // inspect the payment intent or payment method before confirming your payment.
                terminal.processPaymentIntent(
                    collectConfig = CollectPaymentIntentConfiguration.Builder().skipTipping(false).build(),
                    intent = paymentIntent
                )
            }
    }

    fun updateReceiptEmailPaymentIntent(
        emailReceiptParams: EmailReceiptParams,
        onSuccess: () -> Unit,
        onFailure: (FailureMessage) -> Unit
    ) {
        viewModelScope.launch {
            updateAndProcessPaymentIntent(emailReceiptParams.toMap()).fold(
                onSuccess = {
                    onSuccess()
                },
                onFailure = {
                    onFailure(
                        FailureMessage("Failed to update PaymentIntent")
                    )
                }
            )
        }
    }

    private suspend fun updateAndProcessPaymentIntent(
        createPaymentIntentParams: Map<String, String>
    ): Result<Boolean> =
        ApiClient.updatePaymentIntent(createPaymentIntentParams)
            .mapCatching { response ->
                val secret = response.secret
                val paymentIntent = Terminal.getInstance().retrievePaymentIntent(secret)
                capturePaymentIntent(paymentIntent).isSuccess
            }

    private suspend fun capturePaymentIntent(paymentIntent: PaymentIntent) =
        ApiClient.capturePaymentIntent(paymentIntent.id.orEmpty())

    /**
     * Clear the current payment intent to prevent re-navigation after completing the flow
     */
    fun clearCurrentPaymentIntent() {
        _currentPaymentIntent.update { null }
    }

    // --- Redemption Flow Methods ---

    /**
     * Create payment intent for redemption flow with manual capture
     */
    fun createRedemptionPaymentIntent(
        createPaymentParams: CreatePaymentParams,
        onSuccess: (PaymentIntent) -> Unit,
        onFailure: (FailureMessage) -> Unit
    ) {
        viewModelScope.launch {
            android.util.Log.d("CheckoutViewModel", "Starting redemption payment intent creation...")
            // Create payment intent with manual capture for redemption flow
            // Note: We don't use setup_future_usage because we're just reading the card fingerprint
            // to identify the customer, not saving the payment method for future use
            val params = createPaymentParams.toMap().toMutableMap()
            params["capture_method"] = "manual"
            android.util.Log.d("CheckoutViewModel", "Params: $params")

            createAndCollectPaymentMethod(params)
                .fold(
                    onSuccess = { paymentIntent ->
                        android.util.Log.d("CheckoutViewModel", "Successfully collected payment method, updating state and calling success callback")
                        _currentPaymentIntent.update { paymentIntent }
                        onSuccess(paymentIntent)
                    },
                    onFailure = {
                        android.util.Log.e("CheckoutViewModel", "Failed to collect payment method", it)
                        val failureMessage = if (it is TerminalException) {
                            it.errorMessage
                        } else {
                            it.message ?: "Failed to collect payment method"
                        }.let(::FailureMessage)
                        onFailure(failureMessage)
                    }
                )
        }
    }

    /**
     * Create payment intent and collect payment method only (don't confirm yet)
     */
    private suspend fun createAndCollectPaymentMethod(
        createPaymentIntentParams: Map<String, String>
    ): Result<PaymentIntent> {
        return ApiClient.createPaymentIntent(createPaymentIntentParams)
            .mapCatching { response ->
                val secret = response.secret
                android.util.Log.d("CheckoutViewModel", "Created payment intent, secret: ${secret.take(20)}...")

                val terminal = Terminal.getInstance()
                val retrievedPaymentIntent = terminal.retrievePaymentIntent(secret)
                android.util.Log.d("CheckoutViewModel", "Retrieved payment intent, status: ${retrievedPaymentIntent.status}")

                // Collect payment method (tap card)
                android.util.Log.d("CheckoutViewModel", "Starting collectPaymentMethod...")
                val collectedPaymentIntent = terminal.collectPaymentMethod(
                    retrievedPaymentIntent,
                    CollectPaymentIntentConfiguration.Builder().skipTipping(false).build()
                )
                android.util.Log.d("CheckoutViewModel", "Collected payment method - Status: ${collectedPaymentIntent.status}, PaymentMethodId: ${collectedPaymentIntent.paymentMethodId}")

                // Authorize for max amount immediately (before showing redemption screen)
                android.util.Log.d("CheckoutViewModel", "Confirming payment intent to authorize for max amount...")
                val confirmedPaymentIntent = terminal.confirmPaymentIntent(collectedPaymentIntent)
                android.util.Log.d("CheckoutViewModel", "Confirmed payment intent - Status: ${confirmedPaymentIntent.status}, Amount: ${confirmedPaymentIntent.amount}, PaymentMethodId: ${confirmedPaymentIntent.paymentMethodId}")

                confirmedPaymentIntent
            }
    }

    /**
     * Lookup customer credit based on payment intent
     */
    suspend fun lookupCustomerCredit(): CustomerCreditResponse? {
        val paymentIntent = _currentPaymentIntent.value
        android.util.Log.d("CheckoutViewModel", "lookupCustomerCredit - PI status: ${paymentIntent?.status}, ID: ${paymentIntent?.id}, PaymentMethod: ${paymentIntent?.paymentMethodId}")

        val paymentIntentId = paymentIntent?.id ?: run {
            android.util.Log.e("CheckoutViewModel", "No payment intent ID available")
            return null
        }

        val result = ApiClient.lookupCustomerCredit(paymentIntentId)
        android.util.Log.d("CheckoutViewModel", "lookupCustomerCredit result: ${result.getOrNull()}")
        return result.getOrNull()
    }

    /**
     * Capture payment with redemption (partial capture)
     * Payment is already authorized for max amount - capture less with amount_to_capture
     */
    suspend fun captureWithRedemption(customerId: String, creditToRedeem: Long): Boolean {
        return try {
            val paymentIntent = _currentPaymentIntent.value ?: run {
                android.util.Log.e("CheckoutViewModel", "No payment intent available for capture")
                return false
            }

            val maxAmount = paymentIntent.amount
            val finalAmount = maxAmount - creditToRedeem

            android.util.Log.d("CheckoutViewModel", "Capturing with redemption - Max: $maxAmount, Credit: $creditToRedeem, Final: $finalAmount")

            // Capture partial amount via backend (uses amount_to_capture)
            val result = ApiClient.captureWithRedemption(
                paymentIntent.id.orEmpty(),
                customerId,
                finalAmount,
                creditToRedeem
            )

            if (result.isSuccess) {
                android.util.Log.d("CheckoutViewModel", "Successfully captured $finalAmount cents")
                true
            } else {
                android.util.Log.e("CheckoutViewModel", "Failed to capture payment - Result failure: ${result.exceptionOrNull()?.message}")
                result.exceptionOrNull()?.printStackTrace()
                false
            }
        } catch (e: Exception) {
            android.util.Log.e("CheckoutViewModel", "Exception during capture with redemption: ${e.message}", e)
            e.printStackTrace()
            false
        }
    }

    /**
     * Capture full amount without redemption
     * Payment is already authorized - just capture the full authorized amount
     */
    suspend fun captureFullAmount(): Boolean {
        return try {
            val paymentIntent = _currentPaymentIntent.value ?: run {
                android.util.Log.e("CheckoutViewModel", "No payment intent available for capture")
                return false
            }

            android.util.Log.d("CheckoutViewModel", "Capturing full amount: ${paymentIntent.amount}")

            // Capture full amount via backend
            val result = ApiClient.capturePaymentIntent(paymentIntent.id.orEmpty())

            if (result.isSuccess) {
                android.util.Log.d("CheckoutViewModel", "Successfully captured full amount")
                true
            } else {
                android.util.Log.e("CheckoutViewModel", "Failed to capture full payment - Result failure: ${result.exceptionOrNull()?.message}")
                result.exceptionOrNull()?.printStackTrace()
                false
            }
        } catch (e: Exception) {
            android.util.Log.e("CheckoutViewModel", "Exception during full capture: ${e.message}", e)
            e.printStackTrace()
            false
        }
    }
}
