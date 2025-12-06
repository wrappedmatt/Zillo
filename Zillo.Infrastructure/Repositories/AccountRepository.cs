using Zillo.Domain.Entities;
using Zillo.Domain.Interfaces;
using Supabase;

namespace Zillo.Infrastructure.Repositories;

public class AccountRepository : IAccountRepository
{
    private readonly Client _supabase;

    public AccountRepository(Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<Account?> GetByIdAsync(Guid id)
    {
        var response = await _supabase
            .From<AccountModel>()
            .Where(x => x.Id == id)
            .Single();

        return response?.ToEntity();
    }

    public async Task<Account?> GetBySupabaseUserIdAsync(string supabaseUserId)
    {
        var response = await _supabase
            .From<AccountModel>()
            .Where(x => x.SupabaseUserId == supabaseUserId)
            .Single();

        return response?.ToEntity();
    }

    public async Task<Account?> GetByEmailAsync(string email)
    {
        var response = await _supabase
            .From<AccountModel>()
            .Where(x => x.Email == email)
            .Single();

        return response?.ToEntity();
    }

    public async Task<Account?> GetBySlugAsync(string slug)
    {
        var response = await _supabase
            .From<AccountModel>()
            .Where(x => x.Slug == slug)
            .Single();

        return response?.ToEntity();
    }

    public async Task<List<Account>> GetAllAsync()
    {
        var response = await _supabase
            .From<AccountModel>()
            .Get();

        return response.Models.Select(m => m.ToEntity()).ToList();
    }

    public async Task<Account> CreateAsync(Account account)
    {
        var model = AccountModel.FromEntity(account);
        var response = await _supabase
            .From<AccountModel>()
            .Insert(model);

        if (response.Models == null || !response.Models.Any())
            throw new Exception("Failed to create account");

        return response.Models.First().ToEntity();
    }

    public async Task<Account> UpdateAsync(Account account)
    {
        var model = AccountModel.FromEntity(account);
        var response = await _supabase
            .From<AccountModel>()
            .Update(model);

        return response.Models.First().ToEntity();
    }
}

[Postgrest.Attributes.Table("accounts")]
public class AccountModel : Postgrest.Models.BaseModel
{
    [Postgrest.Attributes.PrimaryKey("id")]
    public Guid Id { get; set; }

    [Postgrest.Attributes.Column("email")]
    public string Email { get; set; } = string.Empty;

    [Postgrest.Attributes.Column("company_name")]
    public string CompanyName { get; set; } = string.Empty;

    [Postgrest.Attributes.Column("slug")]
    public string Slug { get; set; } = string.Empty;

    [Postgrest.Attributes.Column("supabase_user_id")]
    public string SupabaseUserId { get; set; } = string.Empty;

    [Postgrest.Attributes.Column("signup_bonus_points")]
    public int SignupBonusPoints { get; set; } = 100;

    [Postgrest.Attributes.Column("loyalty_system_type")]
    public string LoyaltySystemType { get; set; } = "cashback";

    [Postgrest.Attributes.Column("cashback_rate")]
    public decimal CashbackRate { get; set; } = 5.00m;

    [Postgrest.Attributes.Column("historical_reward_days")]
    public int HistoricalRewardDays { get; set; } = 14;

    [Postgrest.Attributes.Column("welcome_incentive")]
    public decimal WelcomeIncentive { get; set; } = 5.00m;

    [Postgrest.Attributes.Column("branding_logo_url")]
    public string? BrandingLogoUrl { get; set; }

    [Postgrest.Attributes.Column("branding_primary_color")]
    public string BrandingPrimaryColor { get; set; } = "#DC2626";

    [Postgrest.Attributes.Column("branding_background_color")]
    public string BrandingBackgroundColor { get; set; } = "#DC2626";

    [Postgrest.Attributes.Column("branding_text_color")]
    public string BrandingTextColor { get; set; } = "#FFFFFF";

    [Postgrest.Attributes.Column("branding_button_color")]
    public string BrandingButtonColor { get; set; } = "#E5E7EB";

    [Postgrest.Attributes.Column("branding_button_text_color")]
    public string BrandingButtonTextColor { get; set; } = "#1F2937";

    [Postgrest.Attributes.Column("branding_headline_text")]
    public string BrandingHeadlineText { get; set; } = "You've earned:";

    [Postgrest.Attributes.Column("branding_subheadline_text")]
    public string BrandingSubheadlineText { get; set; } = "Register now to claim your rewards and save on future visits!";

    [Postgrest.Attributes.Column("branding_qr_headline_text")]
    public string BrandingQrHeadlineText { get; set; } = "Scan to claim your rewards!";

    [Postgrest.Attributes.Column("branding_qr_subheadline_text")]
    public string BrandingQrSubheadlineText { get; set; } = "Register now to claim your rewards and save on future visits!";

    [Postgrest.Attributes.Column("branding_qr_button_text")]
    public string BrandingQrButtonText { get; set; } = "Done";

    [Postgrest.Attributes.Column("branding_recognized_headline_text")]
    public string BrandingRecognizedHeadlineText { get; set; } = "Welcome back!";

    [Postgrest.Attributes.Column("branding_recognized_subheadline_text")]
    public string BrandingRecognizedSubheadlineText { get; set; } = "You've earned:";

    [Postgrest.Attributes.Column("branding_recognized_button_text")]
    public string BrandingRecognizedButtonText { get; set; } = "Skip";

    [Postgrest.Attributes.Column("branding_recognized_link_text")]
    public string BrandingRecognizedLinkText { get; set; } = "Don't show me again";

    // Wallet Pass Settings
    [Postgrest.Attributes.Column("wallet_pass_enabled")]
    public bool WalletPassEnabled { get; set; } = true;

    [Postgrest.Attributes.Column("wallet_pass_description")]
    public string? WalletPassDescription { get; set; }

    [Postgrest.Attributes.Column("wallet_pass_icon_url")]
    public string? WalletPassIconUrl { get; set; }

    [Postgrest.Attributes.Column("wallet_pass_strip_url")]
    public string? WalletPassStripUrl { get; set; }

    [Postgrest.Attributes.Column("wallet_pass_label_color")]
    public string WalletPassLabelColor { get; set; } = "#FFFFFF";

    [Postgrest.Attributes.Column("wallet_pass_foreground_color")]
    public string WalletPassForegroundColor { get; set; } = "#FFFFFF";

    [Postgrest.Attributes.Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Postgrest.Attributes.Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    public Account ToEntity() => new()
    {
        Id = Id,
        Email = Email,
        CompanyName = CompanyName,
        Slug = Slug,
        SupabaseUserId = SupabaseUserId,
        SignupBonusPoints = SignupBonusPoints,
        LoyaltySystemType = LoyaltySystemType,
        CashbackRate = CashbackRate,
        HistoricalRewardDays = HistoricalRewardDays,
        WelcomeIncentive = WelcomeIncentive,
        BrandingLogoUrl = BrandingLogoUrl,
        BrandingPrimaryColor = BrandingPrimaryColor,
        BrandingBackgroundColor = BrandingBackgroundColor,
        BrandingTextColor = BrandingTextColor,
        BrandingButtonColor = BrandingButtonColor,
        BrandingButtonTextColor = BrandingButtonTextColor,
        BrandingHeadlineText = BrandingHeadlineText,
        BrandingSubheadlineText = BrandingSubheadlineText,
        BrandingQrHeadlineText = BrandingQrHeadlineText,
        BrandingQrSubheadlineText = BrandingQrSubheadlineText,
        BrandingQrButtonText = BrandingQrButtonText,
        BrandingRecognizedHeadlineText = BrandingRecognizedHeadlineText,
        BrandingRecognizedSubheadlineText = BrandingRecognizedSubheadlineText,
        BrandingRecognizedButtonText = BrandingRecognizedButtonText,
        BrandingRecognizedLinkText = BrandingRecognizedLinkText,
        WalletPassEnabled = WalletPassEnabled,
        WalletPassDescription = WalletPassDescription,
        WalletPassIconUrl = WalletPassIconUrl,
        WalletPassStripUrl = WalletPassStripUrl,
        WalletPassLabelColor = WalletPassLabelColor,
        WalletPassForegroundColor = WalletPassForegroundColor,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt
    };

    public static AccountModel FromEntity(Account account) => new()
    {
        Id = account.Id,
        Email = account.Email,
        CompanyName = account.CompanyName,
        Slug = account.Slug,
        SupabaseUserId = account.SupabaseUserId,
        SignupBonusPoints = account.SignupBonusPoints,
        LoyaltySystemType = account.LoyaltySystemType,
        CashbackRate = account.CashbackRate,
        HistoricalRewardDays = account.HistoricalRewardDays,
        WelcomeIncentive = account.WelcomeIncentive,
        BrandingLogoUrl = account.BrandingLogoUrl,
        BrandingPrimaryColor = account.BrandingPrimaryColor,
        BrandingBackgroundColor = account.BrandingBackgroundColor,
        BrandingTextColor = account.BrandingTextColor,
        BrandingButtonColor = account.BrandingButtonColor,
        BrandingButtonTextColor = account.BrandingButtonTextColor,
        BrandingHeadlineText = account.BrandingHeadlineText,
        BrandingSubheadlineText = account.BrandingSubheadlineText,
        BrandingQrHeadlineText = account.BrandingQrHeadlineText,
        BrandingQrSubheadlineText = account.BrandingQrSubheadlineText,
        BrandingQrButtonText = account.BrandingQrButtonText,
        BrandingRecognizedHeadlineText = account.BrandingRecognizedHeadlineText,
        BrandingRecognizedSubheadlineText = account.BrandingRecognizedSubheadlineText,
        BrandingRecognizedButtonText = account.BrandingRecognizedButtonText,
        BrandingRecognizedLinkText = account.BrandingRecognizedLinkText,
        WalletPassEnabled = account.WalletPassEnabled,
        WalletPassDescription = account.WalletPassDescription,
        WalletPassIconUrl = account.WalletPassIconUrl,
        WalletPassStripUrl = account.WalletPassStripUrl,
        WalletPassLabelColor = account.WalletPassLabelColor,
        WalletPassForegroundColor = account.WalletPassForegroundColor,
        CreatedAt = account.CreatedAt,
        UpdatedAt = account.UpdatedAt
    };
}
