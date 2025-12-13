namespace Zillo.Application.DTOs;

public record SignUpRequest(string Email, string Password, string CompanyName, string? Slug = null);

public record SignInRequest(string Email, string Password);

public record AuthResponse(string AccessToken, string RefreshToken, UserDto User);

public record UserDto(Guid Id, string Email, string CompanyName, string Slug, string SupabaseUserId);

// Multi-account support DTOs
public record AccountSummaryDto(
    Guid Id,
    string CompanyName,
    string Slug,
    string Role,
    string? StripeOnboardingStatus,
    bool StripeChargesEnabled
);

public record UserAccountsDto(
    string SupabaseUserId,
    string Email,
    List<AccountSummaryDto> Accounts,
    Guid? CurrentAccountId
);

public record SwitchAccountRequest(Guid AccountId);

public record SwitchAccountResponse(
    Guid AccountId,
    string CompanyName,
    string Slug,
    string Role
);
