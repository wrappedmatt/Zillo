package com.zillo.terminal.loyalty.data

import com.google.gson.annotations.SerializedName

/**
 * Response from the /api/terminal/identify endpoint
 * Used when the terminal is running in external mode (deployed by a partner platform)
 * and needs to identify which Zillo account it should connect to based on its Stripe Location ID
 */
data class IdentifyResponse(
    @SerializedName("accountId")
    val accountId: String,

    @SerializedName("companyName")
    val companyName: String,

    @SerializedName("slug")
    val slug: String,

    @SerializedName("loyaltySystemType")
    val loyaltySystemType: String?,

    @SerializedName("cashbackRate")
    val cashbackRate: Double?,

    @SerializedName("historicalRewardDays")
    val historicalRewardDays: Int?,

    @SerializedName("welcomeIncentive")
    val welcomeIncentive: Double?,

    @SerializedName("locationLabel")
    val locationLabel: String?,

    @SerializedName("platformName")
    val platformName: String?,

    @SerializedName("stripeLocationId")
    val stripeLocationId: String?,

    @SerializedName("branding")
    val branding: BrandingSettings,

    @SerializedName("signupUrl")
    val signupUrl: String
)
