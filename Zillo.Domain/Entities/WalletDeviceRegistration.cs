using Postgrest.Attributes;
using Postgrest.Models;
using System.Text.Json.Serialization;

namespace Zillo.Domain.Entities;

[Table("wallet_device_registrations")]
public class WalletDeviceRegistration : BaseModel
{
    [PrimaryKey("id")]
    public Guid Id { get; set; }

    [Column("customer_id")]
    public Guid CustomerId { get; set; }

    [Column("device_library_identifier")]
    public string DeviceLibraryIdentifier { get; set; } = string.Empty;

    [Column("push_token")]
    public string PushToken { get; set; } = string.Empty;

    [Column("wallet_type")]
    public string WalletTypeString { get; set; } = "apple";

    [Column("pass_identifier")]
    public string PassIdentifier { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    // Helper property for enum conversion (not mapped to database)
    [JsonIgnore]
    public WalletType WalletType
    {
        get => WalletTypeString == "google" ? WalletType.Google : WalletType.Apple;
        set => WalletTypeString = value == WalletType.Google ? "google" : "apple";
    }
}

public enum WalletType
{
    Apple,
    Google
}
