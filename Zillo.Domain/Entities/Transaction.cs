namespace Zillo.Domain.Entities;

public class Transaction
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid AccountId { get; set; }
    public int Points { get; set; }
    public decimal CashbackAmount { get; set; }
    public decimal? Amount { get; set; }
    public string Type { get; set; } = string.Empty; // "earn", "redeem", "cashback_earn", "cashback_redeem", "welcome_bonus", "adjustment"
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // Payment tracking fields
    public Guid? PaymentId { get; set; }
    public string? StripePaymentIntentId { get; set; }

    public Customer Customer { get; set; } = null!;
    public Payment? Payment { get; set; }
}
