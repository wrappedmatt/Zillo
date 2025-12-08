package com.stripe.aod.sampleapp.data

import com.google.gson.annotations.SerializedName

/**
 * Response from capturing a payment intent
 * Includes loyalty information and customer registration status
 */
data class CapturePaymentResponse(
    @SerializedName("intent")
    val intent: String,

    @SerializedName("payment_intent_id")
    val paymentIntentId: String,

    @SerializedName("loyalty_points")
    val loyaltyPoints: Int,

    @SerializedName("customer_id")
    val customerId: String?,

    @SerializedName("payment_id")
    val paymentId: String?,

    @SerializedName("card_registered")
    val cardRegistered: Boolean,

    @SerializedName("signup_url")
    val signupUrl: String?,

    @SerializedName("unclaimed_points")
    val unclaimedPoints: Int?
)
