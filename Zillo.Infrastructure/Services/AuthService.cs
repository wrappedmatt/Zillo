using Zillo.Application.DTOs;
using Zillo.Application.Services;
using Zillo.Domain.Entities;
using Zillo.Domain.Interfaces;

namespace Zillo.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly Supabase.Client _supabase;
    private readonly IAccountRepository _accountRepository;
    private readonly IAccountUserRepository _accountUserRepository;

    public AuthService(
        Supabase.Client supabase,
        IAccountRepository accountRepository,
        IAccountUserRepository accountUserRepository)
    {
        _supabase = supabase;
        _accountRepository = accountRepository;
        _accountUserRepository = accountUserRepository;
    }

    public async Task<AuthResponse> SignUpAsync(SignUpRequest request)
    {
        // Auto-generate slug from company name if not provided
        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? GenerateSlug(request.CompanyName)
            : request.Slug.ToLower().Trim();

        // Ensure slug is unique
        slug = await EnsureUniqueSlugAsync(slug);

        // Sign up with user metadata containing company name
        var signUpOptions = new Supabase.Gotrue.SignUpOptions
        {
            Data = new Dictionary<string, object>
            {
                { "company_name", request.CompanyName }
            }
        };

        var session = await _supabase.Auth.SignUp(request.Email, request.Password, signUpOptions);

        if (session?.User == null)
            throw new Exception("Failed to create user");

        // Wait a moment for the database trigger to create the account
        await Task.Delay(500);

        // Fetch the created account (created by database trigger)
        var account = await _accountRepository.GetBySupabaseUserIdAsync(session.User.Id);

        if (account == null)
        {
            // If trigger didn't create it (or hasn't yet), create it manually
            account = new Account
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                CompanyName = request.CompanyName,
                Slug = slug,
                SupabaseUserId = session.User.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            account = await _accountRepository.CreateAsync(account);
        }

        // Ensure user has an account_users entry (for multi-account support)
        var existingAccountUser = await _accountUserRepository.GetByUserAndAccountAsync(session.User.Id, account.Id);
        if (existingAccountUser == null)
        {
            var accountUser = new AccountUser
            {
                Id = Guid.NewGuid(),
                SupabaseUserId = session.User.Id,
                AccountId = account.Id,
                Email = request.Email,
                Role = "owner",
                JoinedAt = DateTime.UtcNow,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _accountUserRepository.CreateAsync(accountUser);
        }

        return new AuthResponse(
            session.AccessToken,
            session.RefreshToken,
            new UserDto(account.Id, account.Email, account.CompanyName, account.Slug, account.SupabaseUserId)
        );
    }

    public async Task<AuthResponse> SignInAsync(SignInRequest request)
    {
        var session = await _supabase.Auth.SignIn(request.Email, request.Password);

        if (session?.User == null)
            throw new Exception("Failed to sign in");

        var account = await _accountRepository.GetBySupabaseUserIdAsync(session.User.Id);

        if (account == null)
            throw new Exception("Account not found");

        // Ensure user has an account_users entry (for migration from old system)
        var existingAccountUser = await _accountUserRepository.GetByUserAndAccountAsync(session.User.Id, account.Id);
        if (existingAccountUser == null)
        {
            var accountUser = new AccountUser
            {
                Id = Guid.NewGuid(),
                SupabaseUserId = session.User.Id,
                AccountId = account.Id,
                Email = account.Email,
                Role = "owner",
                JoinedAt = DateTime.UtcNow,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _accountUserRepository.CreateAsync(accountUser);
        }

        return new AuthResponse(
            session.AccessToken,
            session.RefreshToken,
            new UserDto(account.Id, account.Email, account.CompanyName, account.Slug, account.SupabaseUserId)
        );
    }

    public async Task<UserDto?> GetCurrentUserAsync(string accessToken)
    {
        var user = await _supabase.Auth.GetUser(accessToken);

        if (user == null)
            return null;

        var account = await _accountRepository.GetBySupabaseUserIdAsync(user.Id);

        if (account == null)
            return null;

        return new UserDto(account.Id, account.Email, account.CompanyName, account.Slug, account.SupabaseUserId);
    }

    private static string GenerateSlug(string companyName)
    {
        // Convert to lowercase and replace non-alphanumeric characters with hyphens
        var slug = companyName.ToLower();
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");
        slug = slug.Trim('-');

        // Ensure slug is not empty
        if (string.IsNullOrWhiteSpace(slug))
            slug = "company-" + Guid.NewGuid().ToString("N").Substring(0, 8);

        return slug;
    }

    private async Task<string> EnsureUniqueSlugAsync(string slug)
    {
        var originalSlug = slug;
        var counter = 1;

        // Check if slug exists and append number if it does
        while (await _accountRepository.GetBySlugAsync(slug) != null)
        {
            slug = $"{originalSlug}-{counter}";
            counter++;
        }

        return slug;
    }
}
