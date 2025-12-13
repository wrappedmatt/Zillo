using Zillo.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Zillo.Dashboard.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IAccountRepository _accountRepository;
    private readonly IAccountUserRepository _accountUserRepository;
    private readonly ILogger<AccountsController> _logger;

    public AccountsController(
        IAccountRepository accountRepository,
        IAccountUserRepository accountUserRepository,
        ILogger<AccountsController> logger)
    {
        _accountRepository = accountRepository;
        _accountUserRepository = accountUserRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets the account ID from X-Account-Id header or falls back to user's first account
    /// </summary>
    private async Task<Guid?> GetAccountIdFromRequest()
    {
        var supabaseUserId = GetSupabaseUserIdFromToken();
        if (supabaseUserId == null)
            return null;

        // Check for X-Account-Id header first
        var accountIdHeader = Request.Headers["X-Account-Id"].ToString();
        if (!string.IsNullOrEmpty(accountIdHeader) && Guid.TryParse(accountIdHeader, out var requestedAccountId))
        {
            // Verify user has access to this account
            var hasAccess = await _accountUserRepository.HasAccessAsync(supabaseUserId, requestedAccountId);
            if (hasAccess)
                return requestedAccountId;

            _logger.LogWarning("User {UserId} attempted to access account {AccountId} without permission",
                supabaseUserId, requestedAccountId);
        }

        // Fall back to user's first account
        var userAccounts = await _accountUserRepository.GetBySupabaseUserIdAsync(supabaseUserId);
        return userAccounts.FirstOrDefault()?.AccountId;
    }

    private string? GetSupabaseUserIdFromToken()
    {
        var authHeader = Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return null;

        var token = authHeader["Bearer ".Length..];

        try
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            return jwtToken.Subject;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get current account details
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentAccount()
    {
        try
        {
            var accountId = await GetAccountIdFromRequest();
            if (accountId == null)
                return Unauthorized();

            var account = await _accountRepository.GetByIdAsync(accountId.Value);
            if (account == null)
                return NotFound();

            return Ok(new
            {
                id = account.Id,
                companyName = account.CompanyName,
                slug = account.Slug,
                signupBonusCash = account.SignupBonusCash,
                signupBonusPoints = account.SignupBonusPoints,
                loyaltySystemType = account.LoyaltySystemType,
                cashbackRate = account.CashbackRate,
                pointsRate = account.PointsRate,
                historicalRewardDays = account.HistoricalRewardDays,
                welcomeIncentive = account.WelcomeIncentive,
                brandingLogoUrl = account.BrandingLogoUrl,
                brandingPrimaryColor = account.BrandingPrimaryColor,
                brandingBackgroundColor = account.BrandingBackgroundColor,
                brandingTextColor = account.BrandingTextColor,
                brandingButtonColor = account.BrandingButtonColor,
                brandingButtonTextColor = account.BrandingButtonTextColor,
                brandingHeadlineText = account.BrandingHeadlineText,
                brandingSubheadlineText = account.BrandingSubheadlineText,
                brandingQrHeadlineText = account.BrandingQrHeadlineText,
                brandingQrSubheadlineText = account.BrandingQrSubheadlineText,
                brandingQrButtonText = account.BrandingQrButtonText,
                brandingRecognizedHeadlineText = account.BrandingRecognizedHeadlineText,
                brandingRecognizedSubheadlineText = account.BrandingRecognizedSubheadlineText,
                brandingRecognizedButtonText = account.BrandingRecognizedButtonText,
                brandingRecognizedLinkText = account.BrandingRecognizedLinkText,
                walletPassEnabled = account.WalletPassEnabled,
                walletPassDescription = account.WalletPassDescription,
                walletPassIconUrl = account.WalletPassIconUrl,
                walletPassStripUrl = account.WalletPassStripUrl,
                walletPassLabelColor = account.WalletPassLabelColor,
                walletPassForegroundColor = account.WalletPassForegroundColor,
                createdAt = account.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current account");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update current account settings
    /// </summary>
    [HttpPut("me")]
    public async Task<IActionResult> UpdateCurrentAccount([FromBody] UpdateAccountRequest request)
    {
        try
        {
            var accountId = await GetAccountIdFromRequest();
            if (accountId == null)
                return Unauthorized();

            var account = await _accountRepository.GetByIdAsync(accountId.Value);
            if (account == null)
                return NotFound();

            // Update account properties
            account.CompanyName = request.CompanyName;
            account.Slug = request.Slug;
            account.SignupBonusCash = request.SignupBonusCash;
            account.SignupBonusPoints = request.SignupBonusPoints;
            account.LoyaltySystemType = request.LoyaltySystemType;
            account.CashbackRate = request.CashbackRate;
            account.PointsRate = request.PointsRate;
            account.HistoricalRewardDays = request.HistoricalRewardDays;
            account.WelcomeIncentive = request.WelcomeIncentive;
            account.BrandingLogoUrl = request.BrandingLogoUrl;
            account.BrandingPrimaryColor = request.BrandingPrimaryColor;
            account.BrandingBackgroundColor = request.BrandingBackgroundColor;
            account.BrandingTextColor = request.BrandingTextColor;
            account.BrandingButtonColor = request.BrandingButtonColor;
            account.BrandingButtonTextColor = request.BrandingButtonTextColor;
            account.BrandingHeadlineText = request.BrandingHeadlineText;
            account.BrandingSubheadlineText = request.BrandingSubheadlineText;
            account.BrandingQrHeadlineText = request.BrandingQrHeadlineText;
            account.BrandingQrSubheadlineText = request.BrandingQrSubheadlineText;
            account.BrandingQrButtonText = request.BrandingQrButtonText;
            account.BrandingRecognizedHeadlineText = request.BrandingRecognizedHeadlineText;
            account.BrandingRecognizedSubheadlineText = request.BrandingRecognizedSubheadlineText;
            account.BrandingRecognizedButtonText = request.BrandingRecognizedButtonText;
            account.BrandingRecognizedLinkText = request.BrandingRecognizedLinkText;
            account.WalletPassEnabled = request.WalletPassEnabled;
            account.WalletPassDescription = request.WalletPassDescription;
            account.WalletPassIconUrl = request.WalletPassIconUrl;
            account.WalletPassStripUrl = request.WalletPassStripUrl;
            account.WalletPassLabelColor = request.WalletPassLabelColor;
            account.WalletPassForegroundColor = request.WalletPassForegroundColor;
            account.UpdatedAt = DateTime.UtcNow;

            var updatedAccount = await _accountRepository.UpdateAsync(account);

            return Ok(new
            {
                id = updatedAccount.Id,
                companyName = updatedAccount.CompanyName,
                slug = updatedAccount.Slug,
                signupBonusCash = updatedAccount.SignupBonusCash,
                signupBonusPoints = updatedAccount.SignupBonusPoints,
                loyaltySystemType = updatedAccount.LoyaltySystemType,
                cashbackRate = updatedAccount.CashbackRate,
                pointsRate = updatedAccount.PointsRate,
                historicalRewardDays = updatedAccount.HistoricalRewardDays,
                welcomeIncentive = updatedAccount.WelcomeIncentive,
                brandingLogoUrl = updatedAccount.BrandingLogoUrl,
                brandingPrimaryColor = updatedAccount.BrandingPrimaryColor,
                brandingBackgroundColor = updatedAccount.BrandingBackgroundColor,
                brandingTextColor = updatedAccount.BrandingTextColor,
                brandingButtonColor = updatedAccount.BrandingButtonColor,
                brandingButtonTextColor = updatedAccount.BrandingButtonTextColor,
                brandingHeadlineText = updatedAccount.BrandingHeadlineText,
                brandingSubheadlineText = updatedAccount.BrandingSubheadlineText,
                brandingQrHeadlineText = updatedAccount.BrandingQrHeadlineText,
                brandingQrSubheadlineText = updatedAccount.BrandingQrSubheadlineText,
                brandingQrButtonText = updatedAccount.BrandingQrButtonText,
                brandingRecognizedHeadlineText = updatedAccount.BrandingRecognizedHeadlineText,
                brandingRecognizedSubheadlineText = updatedAccount.BrandingRecognizedSubheadlineText,
                brandingRecognizedButtonText = updatedAccount.BrandingRecognizedButtonText,
                brandingRecognizedLinkText = updatedAccount.BrandingRecognizedLinkText,
                walletPassEnabled = updatedAccount.WalletPassEnabled,
                walletPassDescription = updatedAccount.WalletPassDescription,
                walletPassIconUrl = updatedAccount.WalletPassIconUrl,
                walletPassStripUrl = updatedAccount.WalletPassStripUrl,
                walletPassLabelColor = updatedAccount.WalletPassLabelColor,
                walletPassForegroundColor = updatedAccount.WalletPassForegroundColor,
                createdAt = updatedAccount.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating current account");
            return BadRequest(new { error = ex.Message });
        }
    }
}

public record UpdateAccountRequest(
    string CompanyName,
    string Slug,
    decimal SignupBonusCash,
    int SignupBonusPoints,
    string LoyaltySystemType,
    decimal CashbackRate,
    decimal PointsRate,
    int HistoricalRewardDays,
    decimal WelcomeIncentive,
    string? BrandingLogoUrl,
    string BrandingPrimaryColor,
    string BrandingBackgroundColor,
    string BrandingTextColor,
    string BrandingButtonColor,
    string BrandingButtonTextColor,
    string BrandingHeadlineText,
    string BrandingSubheadlineText,
    string BrandingQrHeadlineText,
    string BrandingQrSubheadlineText,
    string BrandingQrButtonText,
    string BrandingRecognizedHeadlineText,
    string BrandingRecognizedSubheadlineText,
    string BrandingRecognizedButtonText,
    string BrandingRecognizedLinkText,
    bool WalletPassEnabled = true,
    string? WalletPassDescription = null,
    string? WalletPassIconUrl = null,
    string? WalletPassStripUrl = null,
    string WalletPassLabelColor = "#FFFFFF",
    string WalletPassForegroundColor = "#FFFFFF"
);
