using Zillo.Application.DTOs;
using Zillo.Application.Services;
using Zillo.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Zillo.Dashboard.Server.Controllers;

[ApiController]
[Route("api/stripe-connect")]
public class StripeConnectController : ControllerBase
{
    private readonly IStripeConnectService _stripeConnectService;
    private readonly IAccountRepository _accountRepository;
    private readonly IAuthService _authService;
    private readonly ILogger<StripeConnectController> _logger;

    public StripeConnectController(
        IStripeConnectService stripeConnectService,
        IAccountRepository accountRepository,
        IAuthService authService,
        ILogger<StripeConnectController> logger)
    {
        _stripeConnectService = stripeConnectService;
        _accountRepository = accountRepository;
        _authService = authService;
        _logger = logger;
    }

    private async Task<Guid?> GetAccountIdFromToken()
    {
        var authHeader = Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return null;

        var token = authHeader.Substring("Bearer ".Length);
        var user = await _authService.GetCurrentUserAsync(token);
        return user?.Id;
    }

    /// <summary>
    /// Get Stripe Connect status for the current account
    /// </summary>
    /// <param name="refresh">If true, fetches fresh status from Stripe API</param>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus([FromQuery] bool refresh = false)
    {
        try
        {
            var accountId = await GetAccountIdFromToken();
            if (accountId == null)
                return Unauthorized();

            var status = await _stripeConnectService.GetAccountStatusAsync(accountId.Value, refresh);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Stripe Connect status");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Refresh Stripe Connect status from Stripe API
    /// </summary>
    [HttpPost("refresh-status")]
    public async Task<IActionResult> RefreshStatus()
    {
        try
        {
            var accountId = await GetAccountIdFromToken();
            if (accountId == null)
                return Unauthorized();

            var status = await _stripeConnectService.GetAccountStatusAsync(accountId.Value, refreshFromStripe: true);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Stripe Connect status");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create a new Stripe Connect account for the current account
    /// </summary>
    [HttpPost("create-account")]
    public async Task<IActionResult> CreateAccount([FromBody] CreateConnectedAccountRequest? request)
    {
        try
        {
            var accountId = await GetAccountIdFromToken();
            if (accountId == null)
                return Unauthorized();

            var account = await _accountRepository.GetByIdAsync(accountId.Value);
            if (account == null)
                return NotFound();

            var stripeAccountId = await _stripeConnectService.CreateConnectedAccountAsync(
                accountId.Value,
                account.Email,
                request?.Country ?? "NZ",
                request?.BusinessType ?? "company"
            );

            return Ok(new { stripeAccountId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Stripe Connect account");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generate an onboarding link for completing Stripe Connect setup
    /// </summary>
    [HttpPost("onboarding-link")]
    public async Task<IActionResult> GetOnboardingLink([FromBody] OnboardingLinkRequest request)
    {
        try
        {
            var accountId = await GetAccountIdFromToken();
            if (accountId == null)
                return Unauthorized();

            var link = await _stripeConnectService.CreateOnboardingLinkAsync(
                accountId.Value,
                request.ReturnUrl,
                request.RefreshUrl
            );

            return Ok(link);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating onboarding link");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generate a Stripe Express dashboard login link
    /// </summary>
    [HttpGet("dashboard-link")]
    public async Task<IActionResult> GetDashboardLink()
    {
        try
        {
            var accountId = await GetAccountIdFromToken();
            if (accountId == null)
                return Unauthorized();

            var url = await _stripeConnectService.CreateDashboardLinkAsync(accountId.Value);
            return Ok(new { url });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating dashboard link");
            return BadRequest(new { error = ex.Message });
        }
    }
}
