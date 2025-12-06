package com.stripe.aod.sampleapp.network

import android.util.Log
import com.lemonade.terminal.loyalty.data.PairingResponse
import com.stripe.aod.sampleapp.BuildConfig
import com.stripe.aod.sampleapp.Config
import com.stripe.aod.sampleapp.data.ApplyRedemptionResponse
import com.stripe.aod.sampleapp.data.CustomerCreditResponse
import com.stripe.aod.sampleapp.data.PaymentIntentCreationResponse
import com.stripe.stripeterminal.external.models.ConnectionTokenException
import okhttp3.Interceptor
import okhttp3.OkHttpClient
import okhttp3.Response
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import retrofit2.http.Field
import java.io.IOException
import java.net.SocketTimeoutException
import java.util.concurrent.TimeUnit
import java.util.concurrent.TimeoutException

object ApiClient {

    // Terminal API key that will be added to all requests as X-Terminal-API-Key header
    var terminalApiKey: String? = null

    /**
     * Interceptor to add terminal API key to requests
     */
    private class TerminalAuthInterceptor : Interceptor {
        override fun intercept(chain: Interceptor.Chain): Response {
            val original = chain.request()
            val apiKey = terminalApiKey

            // If no API key is set or this is a pairing request, proceed without auth
            if (apiKey == null || original.url.encodedPath.contains("/pair")) {
                return chain.proceed(original)
            }

            // Add X-Terminal-API-Key header
            val authenticated = original.newBuilder()
                .header("X-Terminal-API-Key", apiKey)
                .build()

            return chain.proceed(authenticated)
        }
    }

    private val client = OkHttpClient.Builder()
        .connectTimeout(30, TimeUnit.SECONDS)
        .readTimeout(30, TimeUnit.SECONDS)
        .writeTimeout(30, TimeUnit.SECONDS)
        .addInterceptor(TerminalAuthInterceptor())
        .build()

    private val retrofit: Retrofit = Retrofit.Builder()
        .baseUrl(BuildConfig.BACKEND_URL)
        .client(client)
        .addConverterFactory(GsonConverterFactory.create())
        .build()

    val backendService: BackendService = retrofit.create(BackendService::class.java)

    val backendUrl: String = BuildConfig.BACKEND_URL

    @Throws(ConnectionTokenException::class)
    internal suspend fun createConnectionToken(
        canRetry: Boolean,
    ): String {
        return try {
            val result = backendService.getConnectionToken()

            result.secret.ifEmpty {
                throw ConnectionTokenException("Empty connection token.")
            }
        } catch (e: Exception) {
            when (e) {
                is SocketTimeoutException,
                is TimeoutException,
                is IOException -> {
                    if (canRetry) {
                        Log.e(Config.TAG, "Error while creating connection token, retrying.", e)
                        createConnectionToken(canRetry = false)
                    } else {
                        throw ConnectionTokenException("Failed to create connection token.", e)
                    }
                }

                else -> {
                    throw ConnectionTokenException("Failed to create connection token.", e)
                }
            }
        }
    }

    suspend fun createPaymentIntent(createPaymentIntentParams: Map<String, String>): Result<PaymentIntentCreationResponse> =
        runCatching {
            val response = backendService.createPaymentIntent(createPaymentIntentParams.toMap())
            response ?: error("Failed to create PaymentIntent")
        }

    suspend fun updatePaymentIntent(updatePaymentIntentParams: Map<String, String>): Result<PaymentIntentCreationResponse> =
        runCatching {
            val response = backendService.updatePaymentIntent(updatePaymentIntentParams.toMap())
            response ?: error("Failed to update PaymentIntent")
        }

    suspend fun capturePaymentIntent(@Field("payment_intent_id") id: String): Result<com.stripe.aod.sampleapp.data.CapturePaymentResponse> =
        runCatching {
            val response = backendService.capturePaymentIntent(id)
            response ?: error("Failed to capture PaymentIntent")
        }

    suspend fun lookupCustomerCredit(paymentIntentId: String): Result<CustomerCreditResponse> =
        runCatching {
            val response = backendService.lookupCustomerCredit(paymentIntentId)
            response ?: error("Failed to lookup customer credit")
        }

    suspend fun applyRedemption(
        paymentIntentId: String,
        customerId: String,
        creditToRedeem: Long
    ): Result<ApplyRedemptionResponse> =
        runCatching {
            val response = backendService.applyRedemption(paymentIntentId, customerId, creditToRedeem)
            response ?: error("Failed to apply redemption")
        }

    suspend fun captureWithRedemption(
        paymentIntentId: String,
        customerId: String,
        amountToCapture: Long,
        creditRedeemed: Long
    ): Result<Boolean> =
        runCatching {
            Log.d(Config.TAG, "API: Calling capture_with_redemption - PI: $paymentIntentId, Customer: $customerId, Amount: $amountToCapture, Credit: $creditRedeemed")
            backendService.captureWithRedemption(paymentIntentId, customerId, amountToCapture, creditRedeemed)
            Log.d(Config.TAG, "API: capture_with_redemption completed successfully")
            true
        }

    /**
     * Pair terminal with a pairing code
     */
    suspend fun pairTerminal(
        pairingCode: String,
        terminalLabel: String,
        deviceModel: String?,
        deviceId: String?
    ): Result<PairingResponse> =
        runCatching {
            val body = mutableMapOf(
                "pairingCode" to pairingCode,
                "terminalLabel" to terminalLabel
            )
            deviceModel?.let { body["deviceModel"] = it }
            deviceId?.let { body["deviceId"] = it }

            Log.d(Config.TAG, "API: Pairing terminal with code: $pairingCode")
            val response = backendService.pairTerminal(body)
            Log.d(Config.TAG, "API: Terminal paired successfully: ${response.terminalId}")
            response
        }
}
