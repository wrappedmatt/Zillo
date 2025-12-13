namespace Zillo.Domain.Entities;

/// <summary>
/// Represents a mapping between an external Stripe Terminal Location (from a partner platform)
/// and a Zillo account. This enables the partner model where platforms like Lightspeed deploy
/// the Zillo APK to their merchants' S700 devices.
/// </summary>
public class ExternalLocation
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }

    /// <summary>
    /// Stripe Terminal Location ID (tml_xxx) from the partner platform's Stripe account
    /// </summary>
    public string StripeLocationId { get; set; } = string.Empty;

    /// <summary>
    /// Name of the partner platform deploying the Zillo app (e.g., "Lightspeed", "Toast")
    /// </summary>
    public string? PlatformName { get; set; }

    /// <summary>
    /// Human-readable label for this location (e.g., "Downtown Store")
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Whether this external location mapping is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Simple 6-character alphanumeric pairing code for easier terminal setup (globally unique)
    /// </summary>
    public string? PairingCode { get; set; }

    /// <summary>
    /// Last time a terminal at this location called the identify endpoint
    /// </summary>
    public DateTime? LastSeenAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
