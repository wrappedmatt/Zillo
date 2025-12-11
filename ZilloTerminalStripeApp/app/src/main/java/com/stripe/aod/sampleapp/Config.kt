package com.stripe.aod.sampleapp

/**
 * Configuration object for the Zillo Terminal App
 * Includes loyalty system settings fetched from the backend
 */
object Config {
    const val TAG = "ZilloTerminalApp"

    // Loyalty system configuration (fetched from API)
    var loyaltySystemType: String = "cashback" // "points" or "cashback"
    var cashbackRate: Double = 5.0 // Percentage (e.g., 5.0 = 5%)
    var pointsRate: Double = 1.0 // Points per dollar (e.g., 1.0 = 1 point per dollar, 2.0 = 2 points per dollar)
    var historicalRewardDays: Int = 14
    var welcomeIncentive: Double = 5.0

    /**
     * Calculate reward text based on amount and loyalty system type
     * @param amountInCents Amount in cents (e.g., 1000 = $10.00)
     * @return Formatted reward string (e.g., "$0.50 credit" or "10 points")
     */
    fun calculateReward(amountInCents: Long): String {
        val amountInDollars = amountInCents / 100.0

        return when (loyaltySystemType) {
            "cashback" -> {
                val cashback = amountInDollars * (cashbackRate / 100.0)
                "$${String.format("%.2f", cashback)} credit"
            }
            else -> {
                val points = (amountInDollars * pointsRate).toInt()
                "$points points"
            }
        }
    }

    /**
     * Get the reward label for UI display
     * @return "Cashback" or "Points"
     */
    fun getRewardLabel(): String {
        return when (loyaltySystemType) {
            "cashback" -> "Cashback"
            else -> "Points"
        }
    }

    /**
     * Format balance for display
     * @param pointsBalance Points balance
     * @param cashbackBalance Cashback balance in cents
     * @return Formatted balance string
     */
    fun formatBalance(pointsBalance: Int, cashbackBalance: Long): String {
        return when (loyaltySystemType) {
            "cashback" -> {
                val cashback = cashbackBalance / 100.0
                "$${"%.2f".format(cashback)}"
            }
            else -> {
                "$pointsBalance"
            }
        }
    }
}
