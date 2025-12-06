namespace Zillo.Domain.Entities;

public class Terminal
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }

    // Authentication
    public string ApiKey { get; set; } = string.Empty;

    // Terminal identification
    public string TerminalLabel { get; set; } = string.Empty;
    public string? StripeTerminalId { get; set; }
    public string? DeviceModel { get; set; }
    public string? DeviceId { get; set; }

    // Pairing information
    public string? PairingCode { get; set; }
    public DateTime? PairingExpiresAt { get; set; }
    public DateTime? PairedAt { get; set; }

    // Status tracking
    public bool IsActive { get; set; } = true;
    public DateTime? LastSeenAt { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Account Account { get; set; } = null!;
}
