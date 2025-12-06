using Postgrest.Attributes;
using Postgrest.Models;

namespace Zillo.Domain.Entities;

[Table("unclaimed_transactions")]
public class UnclaimedTransaction : BaseModel
{
    [PrimaryKey("id")]
    public Guid Id { get; set; }

    [Column("account_id")]
    public Guid AccountId { get; set; }

    [Column("card_fingerprint")]
    public string CardFingerprint { get; set; } = string.Empty;

    [Column("card_last4")]
    public string? CardLast4 { get; set; }

    [Column("card_brand")]
    public string? CardBrand { get; set; }

    [Column("card_exp_month")]
    public int? CardExpMonth { get; set; }

    [Column("card_exp_year")]
    public int? CardExpYear { get; set; }

    [Column("points")]
    public int Points { get; set; }

    [Column("cashback_amount")]
    public long CashbackAmount { get; set; }

    [Column("amount")]
    public decimal? Amount { get; set; }

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("payment_id")]
    public Guid? PaymentId { get; set; }

    [Column("stripe_payment_intent_id")]
    public string? StripePaymentIntentId { get; set; }

    [Column("claimed_by_customer_id")]
    public Guid? ClaimedByCustomerId { get; set; }

    [Column("claimed_at")]
    public DateTime? ClaimedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
