package com.zillo.terminal.loyalty.data

import com.google.gson.annotations.SerializedName

/**
 * Account configuration fetched from the backend API
 * Contains loyalty system settings
 */
data class AccountConfig(
    @SerializedName("id")
    val id: String,

    @SerializedName("companyName")
    val companyName: String,

    @SerializedName("loyaltySystemType")
    val loyaltySystemType: String?,

    @SerializedName("cashbackRate")
    val cashbackRate: Double?,

    @SerializedName("pointsRate")
    val pointsRate: Double?,

    @SerializedName("historicalRewardDays")
    val historicalRewardDays: Int?,

    @SerializedName("welcomeIncentive")
    val welcomeIncentive: Double?
) {
    companion object {
        /**
         * Default account configuration as fallback
         */
        fun default() = AccountConfig(
            id = "",
            companyName = "Zillo",
            loyaltySystemType = "cashback",
            cashbackRate = 5.0,
            pointsRate = 1.0,
            historicalRewardDays = 14,
            welcomeIncentive = 5.0
        )
    }
}
