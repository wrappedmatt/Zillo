package com.stripe.aod.sampleapp.network

import com.lemonade.terminal.loyalty.data.AccountConfig
import com.lemonade.terminal.loyalty.data.BrandingSettings
import com.lemonade.terminal.loyalty.data.PairingResponse
import com.stripe.aod.sampleapp.data.ApplyRedemptionResponse
import com.stripe.aod.sampleapp.data.CapturePaymentResponse
import com.stripe.aod.sampleapp.data.CustomerCreditResponse
import com.stripe.aod.sampleapp.data.PaymentIntentCreationResponse
import com.stripe.aod.sampleapp.model.ConnectionToken
import retrofit2.http.Body
import retrofit2.http.Field
import retrofit2.http.FieldMap
import retrofit2.http.FormUrlEncoded
import retrofit2.http.GET
import retrofit2.http.POST

/**
 * The `BackendService` interface handles the calls we need to make to our backend.
 */
interface BackendService {

    /**
     * Get a connection token string from the backend
     */
    @POST("api/connection_token")
    suspend fun getConnectionToken(): ConnectionToken

    @FormUrlEncoded
    @POST("api/create_payment_intent")
    suspend fun createPaymentIntent(@FieldMap createPaymentIntentParams: Map<String, String>): PaymentIntentCreationResponse?

    @FormUrlEncoded
    @POST("api/update_payment_intent")
    suspend fun updatePaymentIntent(@FieldMap updatePaymentIntentParams: Map<String, String>): PaymentIntentCreationResponse?

    @FormUrlEncoded
    @POST("api/capture_payment_intent")
    suspend fun capturePaymentIntent(@Field("payment_intent_id") id: String): CapturePaymentResponse?

    @FormUrlEncoded
    @POST("api/lookup_customer_credit")
    suspend fun lookupCustomerCredit(@Field("payment_intent_id") paymentIntentId: String): CustomerCreditResponse?

    @FormUrlEncoded
    @POST("api/apply_redemption")
    suspend fun applyRedemption(
        @Field("payment_intent_id") paymentIntentId: String,
        @Field("customer_id") customerId: String,
        @Field("credit_to_redeem") creditToRedeem: Long
    ): ApplyRedemptionResponse?

    @FormUrlEncoded
    @POST("api/capture_with_redemption")
    suspend fun captureWithRedemption(
        @Field("payment_intent_id") paymentIntentId: String,
        @Field("customer_id") customerId: String,
        @Field("amount_to_capture") amountToCapture: Long,
        @Field("credit_redeemed") creditRedeemed: Long
    )

    /**
     * Pair terminal with a pairing code
     */
    @POST("api/TerminalManagement/pair")
    suspend fun pairTerminal(@Body body: Map<String, String>): PairingResponse

    /**
     * Get branding settings for the terminal
     */
    @GET("api/terminal/branding")
    suspend fun getBrandingSettings(): BrandingSettings

    /**
     * Get account configuration including loyalty system settings
     * Uses X-Terminal-Api-Key header for authentication
     */
    @GET("api/terminal/config")
    suspend fun getAccountConfiguration(): AccountConfig
}
