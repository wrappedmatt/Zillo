package com.lemonade.terminal.loyalty.data

import com.google.gson.annotations.SerializedName

/**
 * Branding settings fetched from the backend API
 * Used to customize the appearance of terminal screens
 */
data class BrandingSettings(
    @SerializedName("companyName")
    val companyName: String,

    @SerializedName("logoUrl")
    val logoUrl: String?,

    @SerializedName("backgroundColor")
    val backgroundColor: String,

    @SerializedName("textColor")
    val textColor: String,

    @SerializedName("buttonColor")
    val buttonColor: String,

    @SerializedName("buttonTextColor")
    val buttonTextColor: String,

    @SerializedName("headlineText")
    val headlineText: String,

    @SerializedName("subheadlineText")
    val subheadlineText: String,

    @SerializedName("qrHeadlineText")
    val qrHeadlineText: String,

    @SerializedName("qrSubheadlineText")
    val qrSubheadlineText: String,

    @SerializedName("qrButtonText")
    val qrButtonText: String,

    @SerializedName("recognizedHeadlineText")
    val recognizedHeadlineText: String,

    @SerializedName("recognizedSubheadlineText")
    val recognizedSubheadlineText: String,

    @SerializedName("recognizedButtonText")
    val recognizedButtonText: String,

    @SerializedName("recognizedLinkText")
    val recognizedLinkText: String,

    @SerializedName("signupUrl")
    val signupUrl: String
) {
    companion object {
        /**
         * Default branding settings as fallback
         */
        fun default() = BrandingSettings(
            companyName = "Lemonade",
            logoUrl = null,
            backgroundColor = "#DC2626",
            textColor = "#FFFFFF",
            buttonColor = "#E5E7EB",
            buttonTextColor = "#1F2937",
            headlineText = "You've earned:",
            subheadlineText = "You can use this credit for future purchases.",
            qrHeadlineText = "Scan to claim your rewards!",
            qrSubheadlineText = "You can use this credit for future purchases.",
            qrButtonText = "Done",
            recognizedHeadlineText = "Welcome back!",
            recognizedSubheadlineText = "You've earned:",
            recognizedButtonText = "Skip",
            recognizedLinkText = "Don't show me again",
            signupUrl = "/signup"
        )
    }
}
