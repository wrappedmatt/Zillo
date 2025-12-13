using Zillo.Application.DTOs;
using Zillo.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Zillo.Dashboard.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IAccountSwitchService _accountSwitchService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IAccountSwitchService accountSwitchService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _accountSwitchService = accountSwitchService;
        _logger = logger;
    }

    [HttpPost("signup")]
    public async Task<ActionResult<AuthResponse>> SignUp([FromBody] SignUpRequest request)
    {
        try
        {
            var response = await _authService.SignUpAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during signup");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("signin")]
    public async Task<ActionResult<AuthResponse>> SignIn([FromBody] SignInRequest request)
    {
        try
        {
            var response = await _authService.SignInAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during signin");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        try
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                return Unauthorized();

            var token = authHeader.Substring("Bearer ".Length);
            var user = await _authService.GetCurrentUserAsync(token);

            if (user == null)
                return Unauthorized();

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get all accounts the current user has access to
    /// </summary>
    [HttpGet("accounts")]
    public async Task<ActionResult<UserAccountsDto>> GetAccessibleAccounts()
    {
        try
        {
            var supabaseUserId = GetSupabaseUserIdFromToken();
            if (supabaseUserId == null)
                return Unauthorized();

            var accounts = await _accountSwitchService.GetAccessibleAccountsAsync(supabaseUserId);
            return Ok(accounts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting accessible accounts");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Switch to a different account
    /// </summary>
    [HttpPost("switch-account")]
    public async Task<ActionResult<SwitchAccountResponse>> SwitchAccount([FromBody] SwitchAccountRequest request)
    {
        try
        {
            var supabaseUserId = GetSupabaseUserIdFromToken();
            if (supabaseUserId == null)
                return Unauthorized();

            var result = await _accountSwitchService.SwitchAccountAsync(supabaseUserId, request.AccountId);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized account switch attempt");
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error switching account");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Check if current user has access to a specific account
    /// </summary>
    [HttpGet("accounts/{accountId}/access")]
    public async Task<ActionResult<object>> CheckAccountAccess(Guid accountId)
    {
        try
        {
            var supabaseUserId = GetSupabaseUserIdFromToken();
            if (supabaseUserId == null)
                return Unauthorized();

            var hasAccess = await _accountSwitchService.HasAccessAsync(supabaseUserId, accountId);
            var role = hasAccess ? await _accountSwitchService.GetRoleAsync(supabaseUserId, accountId) : null;

            return Ok(new { hasAccess, role });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking account access");
            return BadRequest(new { error = ex.Message });
        }
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
}
