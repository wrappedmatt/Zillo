using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Terminal;
using Zillo.Application.Services;
using Zillo.Application.DTOs;
using Zillo.Domain.Interfaces;
using Zillo.Domain.Entities;

namespace Zillo.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/terminal")]
public class TerminalController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly ITransactionService _transactionService;
    private readonly IPaymentService _paymentService;
    private readonly ICardRepository _cardRepository;
    private readonly IUnclaimedTransactionRepository _unclaimedTransactionRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ILogger<TerminalController> _logger;
    private readonly IConfiguration _configuration;

    public TerminalController(
        ICustomerService customerService,
        ITransactionService transactionService,
        IPaymentService paymentService,
        ICardRepository cardRepository,
        IUnclaimedTransactionRepository unclaimedTransactionRepository,
        IAccountRepository accountRepository,
        ILogger<TerminalController> logger,
        IConfiguration configuration)
    {
        _customerService = customerService;
        _transactionService = transactionService;
        _paymentService = paymentService;
        _cardRepository = cardRepository;
        _unclaimedTransactionRepository = unclaimedTransactionRepository;
        _accountRepository = accountRepository;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Generate a connection token for Stripe Terminal SDK
    /// </summary>
    [HttpPost("connection-token")]
    public async Task<IActionResult> CreateConnectionToken()
    {
        try
        {
            var options = new ConnectionTokenCreateOptions();
            var service = new ConnectionTokenService();
            var connectionToken = await service.CreateAsync(options);

            _logger.LogInformation("Created connection token successfully");

            return Ok(new { secret = connectionToken.Secret });
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error creating connection token");
            return StatusCode(500, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating connection token");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get account configuration for terminal (loyalty settings, branding, etc.)
    /// Uses X-Terminal-Api-Key header for authentication
    /// </summary>
    [HttpGet("config")]
    public async Task<IActionResult> GetTerminalConfig()
    {
        try
        {
            // Get terminal API key from header
            if (!Request.Headers.TryGetValue("X-Terminal-Api-Key", out var apiKeyHeader))
            {
                _logger.LogWarning("Missing X-Terminal-Api-Key header");
                return Unauthorized(new { error = "Terminal API key required" });
            }

            var terminalApiKey = apiKeyHeader.ToString();

            // Validate API key format (should start with term_sk_)
            if (string.IsNullOrEmpty(terminalApiKey) || !terminalApiKey.StartsWith("term_sk_"))
            {
                _logger.LogWarning("Invalid terminal API key format");
                return Unauthorized(new { error = "Invalid terminal API key" });
            }

            // For now, we'll use a simple approach: get the first account (single-tenant)
            // In production, you'd want to store terminal API keys in the database linked to accounts
            var accounts = await _accountRepository.GetAllAsync();
            var account = accounts.FirstOrDefault();

            if (account == null)
            {
                _logger.LogWarning("No account found");
                return NotFound(new { error = "Account not found" });
            }

            _logger.LogInformation("Terminal config fetched for account {AccountId}", account.Id);

            return Ok(new
            {
                loyaltySystemType = account.LoyaltySystemType,
                cashbackRate = account.CashbackRate,
                pointsRate = account.PointsRate,
                historicalRewardDays = account.HistoricalRewardDays,
                welcomeIncentive = account.WelcomeIncentive,
                companyName = account.CompanyName,
                slug = account.Slug
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching terminal config");
            return StatusCode(500, new { error = "Failed to fetch terminal configuration" });
        }
    }

    /// <summary>
    /// Get terminal branding settings for the authenticated terminal's account
    /// Requires terminal authentication via X-Terminal-API-Key header
    /// </summary>
    [HttpGet("branding")]
    public async Task<IActionResult> GetTerminalBranding()
    {
        try
        {
            // Get authenticated terminal info from HttpContext.Items (set by middleware)
            var accountId = (Guid)HttpContext.Items["AccountId"]!;
            var terminalId = (Guid)HttpContext.Items["TerminalId"]!;

            _logger.LogInformation("Terminal {TerminalId} requesting branding settings", terminalId);

            var account = await _accountRepository.GetByIdAsync(accountId);
            if (account == null)
                return NotFound(new { error = "Account not found" });

            return Ok(new
            {
                companyName = account.CompanyName,
                logoUrl = account.BrandingLogoUrl,
                backgroundColor = account.BrandingBackgroundColor,
                textColor = account.BrandingTextColor,
                buttonColor = account.BrandingButtonColor,
                buttonTextColor = account.BrandingButtonTextColor,
                headlineText = account.BrandingHeadlineText,
                subheadlineText = account.BrandingSubheadlineText,
                qrHeadlineText = account.BrandingQrHeadlineText,
                qrSubheadlineText = account.BrandingQrSubheadlineText,
                qrButtonText = account.BrandingQrButtonText,
                recognizedHeadlineText = account.BrandingRecognizedHeadlineText,
                recognizedSubheadlineText = account.BrandingRecognizedSubheadlineText,
                recognizedButtonText = account.BrandingRecognizedButtonText,
                recognizedLinkText = account.BrandingRecognizedLinkText,
                signupUrl = $"/signup/{account.Slug}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting terminal branding");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
