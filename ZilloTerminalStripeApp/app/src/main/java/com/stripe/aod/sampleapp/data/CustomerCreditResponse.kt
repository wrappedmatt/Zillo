package com.stripe.aod.sampleapp.data

import com.google.gson.annotations.SerializedName

/**
 * CustomerCreditResponse data model from backend
 * Contains customer credit/balance information for both loyalty systems
 */
data class CustomerCreditResponse(
    @SerializedName("customer_id")
    val customerId: String?,

    @SerializedName("card_registered")
    val cardRegistered: Boolean = true,

    @SerializedName("credit_balance")
    val creditBalance: Long,

    @SerializedName("points_balance")
    val pointsBalance: Int,

    @SerializedName("cashback_balance")
    val cashbackBalance: Long,

    @SerializedName("loyalty_system_type")
    val loyaltySystemType: String,

    @SerializedName("unclaimed_points")
    val unclaimedPoints: Int? = null,

    @SerializedName("signup_url")
    val signupUrl: String? = null
)
