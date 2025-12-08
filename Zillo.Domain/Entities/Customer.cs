namespace Zillo.Domain.Entities;

public class Customer
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public int PointsBalance { get; set; }
    public decimal CashbackBalance { get; set; }
    public bool WelcomeIncentiveAwarded { get; set; }
    public DateTime? CardLinkedAt { get; set; }
    public string? PortalToken { get; set; }
    public DateTime? PortalTokenExpiresAt { get; set; }
    public string? LastAnnouncementMessage { get; set; }
    public DateTime? LastAnnouncementAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Account Account { get; set; } = null!;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Card> Cards { get; set; } = new List<Card>();
}
