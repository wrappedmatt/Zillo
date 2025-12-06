using Zillo.Application.DTOs;

namespace Zillo.Application.Services;

public interface IAuthService
{
    Task<AuthResponse> SignUpAsync(SignUpRequest request);
    Task<AuthResponse> SignInAsync(SignInRequest request);
    Task<UserDto?> GetCurrentUserAsync(string accessToken);
}
