namespace Zillo.Domain.Entities;

public class Account
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string SupabaseUserId { get; set; } = string.Empty;
    public decimal SignupBonusCash { get; set; } = 5.00m; // Signup bonus in dollars (for cashback system)
    public int SignupBonusPoints { get; set; } = 100; // Signup bonus in points (for points system)

    // Loyalty System Configuration
    public string LoyaltySystemType { get; set; } = "cashback"; // "points" or "cashback"
    public decimal CashbackRate { get; set; } = 5.00m; // Percentage (e.g., 5.00 = 5%)
    public decimal PointsRate { get; set; } = 1.00m; // Points per dollar (e.g., 1.00 = 1 point per dollar, 2.00 = 2 points per dollar)
    public int HistoricalRewardDays { get; set; } = 14; // Days to look back for rewarding historical purchases
    public decimal WelcomeIncentive { get; set; } = 5.00m; // Welcome bonus in dollars

    // Branding Settings
    public string? BrandingLogoUrl { get; set; }
    public string BrandingPrimaryColor { get; set; } = "#DC2626"; // Default red
    public string BrandingBackgroundColor { get; set; } = "#DC2626"; // Default red
    public string BrandingTextColor { get; set; } = "#FFFFFF"; // Default white
    public string BrandingButtonColor { get; set; } = "#E5E7EB"; // Default light gray
    public string BrandingButtonTextColor { get; set; } = "#1F2937"; // Default dark gray

    // Unrecognized Card Screen Text
    public string BrandingHeadlineText { get; set; } = "You've earned:";
    public string BrandingSubheadlineText { get; set; } = "Register now to claim your rewards and save on future visits!";

    // QR Scan Screen Text
    public string BrandingQrHeadlineText { get; set; } = "Scan to claim your rewards!";
    public string BrandingQrSubheadlineText { get; set; } = "Register now to claim your rewards and save on future visits!";
    public string BrandingQrButtonText { get; set; } = "Done";

    // Recognized Card Screen Text
    public string BrandingRecognizedHeadlineText { get; set; } = "Welcome back!";
    public string BrandingRecognizedSubheadlineText { get; set; } = "You've earned:";
    public string BrandingRecognizedButtonText { get; set; } = "Skip";
    public string BrandingRecognizedLinkText { get; set; } = "Don't show me again";

    // Wallet Pass Settings
    public bool WalletPassEnabled { get; set; } = true;
    public string? WalletPassDescription { get; set; } // e.g., "Your loyalty card for Acme Coffee"
    public string? WalletPassIconUrl { get; set; } // Square icon for the pass (ideally 87x87, 174x174, 261x261)
    public string? WalletPassStripUrl { get; set; } // Strip image for Apple Wallet (ideally 375x123, 750x246, 1125x369)
    public string WalletPassLabelColor { get; set; } = "#FFFFFF"; // Label text color on pass
    public string WalletPassForegroundColor { get; set; } = "#FFFFFF"; // Value text color on pass

    // Stripe Connect Integration
    public string? StripeAccountId { get; set; } // Stripe Connect account ID (acct_xxx)
    public string StripeOnboardingStatus { get; set; } = "not_started"; // not_started, pending, complete, restricted
    public bool StripeChargesEnabled { get; set; } = false; // Can accept payments
    public bool StripePayoutsEnabled { get; set; } = false; // Can receive payouts
    public DateTime? StripeAccountUpdatedAt { get; set; } // Last webhook update
    public decimal PlatformFeePercentage { get; set; } = 0.00m; // Platform fee (0.00 = no fee)

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
}
