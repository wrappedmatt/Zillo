using Postgrest.Attributes;
using Postgrest.Models;

namespace Zillo.Domain.Entities;

[Table("cards")]
public class Card : BaseModel
{
    [PrimaryKey("id")]
    public Guid Id { get; set; }

    [Column("customer_id")]
    public Guid CustomerId { get; set; }

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

    [Column("is_primary")]
    public bool IsPrimary { get; set; }

    [Column("first_used_at")]
    public DateTime FirstUsedAt { get; set; }

    [Column("last_used_at")]
    public DateTime LastUsedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
