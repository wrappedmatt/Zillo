namespace Zillo.Domain.Entities;

/// <summary>
/// Represents a physical business location for an account
/// Each location can have its own Stripe Terminal Location and multiple terminals
/// </summary>
public class Location
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }

    // Location details
    public string Name { get; set; } = string.Empty;
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; } = "US";

    // Geolocation (optional)
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? RelevantDistance { get; set; } // Distance in meters for triggering notifications (default ~100m)

    // Stripe Terminal Location (created in connected account)
    public string? StripeTerminalLocationId { get; set; }

    // Status
    public bool IsActive { get; set; } = true;

    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
