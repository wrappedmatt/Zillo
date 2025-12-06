namespace Zillo.Domain.Entities;

public class Payment
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid? CustomerId { get; set; }

    // Stripe identifiers
    public string StripePaymentIntentId { get; set; } = string.Empty;
    public string? StripeChargeId { get; set; }

    // Terminal information
    public string? TerminalId { get; set; }
    public string? TerminalLabel { get; set; }

    // Payment amounts (in dollars)
    public decimal Amount { get; set; }
    public decimal AmountCharged { get; set; }
    public decimal LoyaltyRedeemed { get; set; }
    public int LoyaltyEarned { get; set; }

    // Payment status
    public string Status { get; set; } = "pending"; // pending, completed, failed, refunded, partially_refunded

    // Additional metadata
    public string Currency { get; set; } = "nzd";
    public string? PaymentMethodType { get; set; }
    public string? Metadata { get; set; } // JSON string for additional data

    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Refund information
    public string? RefundReason { get; set; }
    public DateTime? RefundedAt { get; set; }
    public decimal? RefundAmount { get; set; }
}
