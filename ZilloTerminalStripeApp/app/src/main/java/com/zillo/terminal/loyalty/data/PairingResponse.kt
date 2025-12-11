package com.zillo.terminal.loyalty.data

import com.google.gson.annotations.SerializedName

/**
 * Response from the terminal pairing API
 */
data class PairingResponse(
    @SerializedName("apiKey")
    val apiKey: String,

    @SerializedName("terminalId")
    val terminalId: String,

    @SerializedName("accountId")
    val accountId: String,

    @SerializedName("terminalLabel")
    val terminalLabel: String
)
