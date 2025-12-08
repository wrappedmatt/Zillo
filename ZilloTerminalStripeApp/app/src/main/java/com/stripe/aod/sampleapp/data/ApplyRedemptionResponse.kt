package com.stripe.aod.sampleapp.data

/**
 * ApplyRedemptionResponse data model from backend
 */
data class ApplyRedemptionResponse(
    val paymentIntentId: String,
    val secret: String,
    val newAmount: Long,
    val pointsRedeemed: Long,
    val remainingPoints: Long
)
