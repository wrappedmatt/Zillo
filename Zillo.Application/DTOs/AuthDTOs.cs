namespace Zillo.Application.DTOs;

public record SignUpRequest(string Email, string Password, string CompanyName, string? Slug = null);

public record SignInRequest(string Email, string Password);

public record AuthResponse(string AccessToken, string RefreshToken, UserDto User);

public record UserDto(Guid Id, string Email, string CompanyName, string Slug, string SupabaseUserId);
