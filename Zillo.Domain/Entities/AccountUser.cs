namespace Zillo.Domain.Entities;

/// <summary>
/// Represents a user's access to an account, enabling multi-account support
/// </summary>
public class AccountUser
{
    public Guid Id { get; set; }
    public string SupabaseUserId { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User role: owner (full access), admin (manage data), user (read-only)
    /// </summary>
    public string Role { get; set; } = "owner";

    // Invitation tracking
    public DateTime? InvitedAt { get; set; }
    public Guid? InvitedBy { get; set; }
    public DateTime? JoinedAt { get; set; }

    // Status
    public bool IsActive { get; set; } = true;

    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Account? Account { get; set; }
}
