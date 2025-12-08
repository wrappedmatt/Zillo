package com.stripe.aod.sampleapp.data

import com.google.gson.annotations.SerializedName

/**
 * PaymentIntentCreationResponse data model from backend
 */
data class PaymentIntentCreationResponse(
    @SerializedName("payment_intent_id")
    val paymentIntentId: String,

    @SerializedName("intent")
    val secret: String,

    @SerializedName("payment_id")
    val paymentId: String? = null
)
