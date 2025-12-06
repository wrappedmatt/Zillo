using Zillo.Application.DTOs;
using Zillo.Application.Services;
using Zillo.Domain.Entities;
using Zillo.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;

namespace Zillo.Infrastructure.Services;

public class TerminalService : ITerminalService
{
    private readonly ITerminalRepository _terminalRepository;
    private readonly IMemoryCache _cache;
    private const int PAIRING_CODE_EXPIRY_MINUTES = 5;
    private const string API_KEY_CACHE_PREFIX = "terminal_api_key_";

    public TerminalService(ITerminalRepository terminalRepository, IMemoryCache cache)
    {
        _terminalRepository = terminalRepository;
        _cache = cache;
    }

    public async Task<GeneratePairingCodeResponse> GeneratePairingCodeAsync(Guid accountId, string? terminalLabel = null)
    {
        // Generate pairing code
        var pairingCode = GeneratePairingCode();
        var expiresAt = DateTime.UtcNow.AddMinutes(PAIRING_CODE_EXPIRY_MINUTES);

        // Create pending terminal record
        var terminal = new Terminal
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            ApiKey = GenerateApiKey(), // Pre-generate API key
            TerminalLabel = terminalLabel ?? "Pending Terminal",
            PairingCode = pairingCode,
            PairingExpiresAt = expiresAt,
            IsActive = false, // Inactive until paired
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _terminalRepository.CreateAsync(terminal);

        return new GeneratePairingCodeResponse(
            PairingCode: pairingCode,
            ExpiresAt: expiresAt,
            ExpiresInSeconds: PAIRING_CODE_EXPIRY_MINUTES * 60
        );
    }

    public async Task<PairTerminalResponse> PairTerminalAsync(PairTerminalRequest request)
    {
        // Find terminal by pairing code
        var terminal = await _terminalRepository.GetByPairingCodeAsync(request.PairingCode);

        if (terminal == null)
        {
            throw new Exception("Invalid pairing code");
        }

        // Check if pairing code has expired
        if (terminal.PairingExpiresAt.HasValue && terminal.PairingExpiresAt.Value < DateTime.UtcNow)
        {
            throw new Exception("Pairing code has expired");
        }

        // Check if already paired
        if (terminal.PairedAt.HasValue)
        {
            throw new Exception("This pairing code has already been used");
        }

        // Update terminal with pairing information
        terminal.TerminalLabel = request.TerminalLabel;
        terminal.DeviceModel = request.DeviceModel;
        terminal.DeviceId = request.DeviceId;
        terminal.PairedAt = DateTime.UtcNow;
        terminal.IsActive = true;
        terminal.LastSeenAt = DateTime.UtcNow;
        terminal.PairingCode = null; // Clear pairing code after use
        terminal.PairingExpiresAt = null;
        terminal.UpdatedAt = DateTime.UtcNow;

        await _terminalRepository.UpdateAsync(terminal);

        return new PairTerminalResponse(
            ApiKey: terminal.ApiKey,
            TerminalId: terminal.Id,
            AccountId: terminal.AccountId,
            TerminalLabel: terminal.TerminalLabel
        );
    }

    public async Task<TerminalDto?> ValidateApiKeyAsync(string apiKey)
    {
        // Check cache first
        var cacheKey = API_KEY_CACHE_PREFIX + apiKey;
        if (_cache.TryGetValue<TerminalDto>(cacheKey, out var cachedTerminal))
        {
            return cachedTerminal;
        }

        // Query database
        var terminal = await _terminalRepository.GetByApiKeyAsync(apiKey);
        if (terminal == null || !terminal.IsActive)
        {
            return null;
        }

        var dto = MapToDto(terminal);

        // Cache for 5 minutes
        _cache.Set(cacheKey, dto, TimeSpan.FromMinutes(5));

        return dto;
    }

    public async Task<IEnumerable<TerminalDto>> GetTerminalsByAccountAsync(Guid accountId)
    {
        var terminals = await _terminalRepository.GetByAccountIdAsync(accountId);
        return terminals.Select(MapToDto);
    }

    public async Task<TerminalDto?> GetTerminalByIdAsync(Guid id, Guid accountId)
    {
        var terminal = await _terminalRepository.GetByIdAsync(id);

        if (terminal == null || terminal.AccountId != accountId)
            return null;

        return MapToDto(terminal);
    }

    public async Task<TerminalDto> UpdateTerminalAsync(Guid id, UpdateTerminalRequest request, Guid accountId)
    {
        var terminal = await _terminalRepository.GetByIdAsync(id);

        if (terminal == null || terminal.AccountId != accountId)
            throw new Exception("Terminal not found");

        if (!string.IsNullOrEmpty(request.TerminalLabel))
            terminal.TerminalLabel = request.TerminalLabel;

        if (request.IsActive.HasValue)
            terminal.IsActive = request.IsActive.Value;

        terminal.UpdatedAt = DateTime.UtcNow;

        var updated = await _terminalRepository.UpdateAsync(terminal);

        // Invalidate cache
        _cache.Remove(API_KEY_CACHE_PREFIX + terminal.ApiKey);

        return MapToDto(updated);
    }

    public async Task RevokeTerminalAsync(Guid id, Guid accountId)
    {
        var terminal = await _terminalRepository.GetByIdAsync(id);

        if (terminal == null || terminal.AccountId != accountId)
            throw new Exception("Terminal not found");

        terminal.IsActive = false;
        terminal.UpdatedAt = DateTime.UtcNow;

        await _terminalRepository.UpdateAsync(terminal);

        // Invalidate cache
        _cache.Remove(API_KEY_CACHE_PREFIX + terminal.ApiKey);
    }

    public async Task DeleteTerminalAsync(Guid id, Guid accountId)
    {
        var terminal = await _terminalRepository.GetByIdAsync(id);

        if (terminal == null || terminal.AccountId != accountId)
            throw new Exception("Terminal not found");

        // Invalidate cache
        _cache.Remove(API_KEY_CACHE_PREFIX + terminal.ApiKey);

        await _terminalRepository.DeleteAsync(id);
    }

    public async Task UpdateLastSeenAsync(Guid terminalId)
    {
        await _terminalRepository.UpdateLastSeenAsync(terminalId);
    }

    public async Task<int> CleanupExpiredPairingCodesAsync()
    {
        // Get all terminals with expired pairing codes that were never paired
        var terminals = await _terminalRepository.GetByAccountIdAsync(Guid.Empty);
        var expiredTerminals = terminals.Where(t =>
            !t.PairedAt.HasValue &&
            t.PairingExpiresAt.HasValue &&
            t.PairingExpiresAt.Value < DateTime.UtcNow &&
            t.CreatedAt < DateTime.UtcNow.AddHours(-24)
        );

        int count = 0;
        foreach (var terminal in expiredTerminals)
        {
            await _terminalRepository.DeleteAsync(terminal.Id);
            count++;
        }

        return count;
    }

    private static TerminalDto MapToDto(Terminal terminal)
    {
        var status = "offline";
        if (terminal.LastSeenAt.HasValue)
        {
            var timeSinceLastSeen = DateTime.UtcNow - terminal.LastSeenAt.Value;
            if (timeSinceLastSeen.TotalMinutes < 5)
                status = "online";
            else if (timeSinceLastSeen.TotalHours < 1)
                status = "idle";
        }

        return new TerminalDto(
            terminal.Id,
            terminal.AccountId,
            terminal.TerminalLabel,
            terminal.StripeTerminalId,
            terminal.DeviceModel,
            terminal.DeviceId,
            terminal.IsActive,
            terminal.LastSeenAt,
            terminal.PairedAt,
            terminal.CreatedAt,
            status
        );
    }

    private static string GeneratePairingCode()
    {
        // Generate 6-digit numeric code
        var random = new Random();
        var code = random.Next(0, 1000000).ToString("D6");
        return code;
    }

    private static string GenerateApiKey()
    {
        // Generate secure random API key
        var randomBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        // Convert to URL-safe Base64 (replace + with -, / with _, and remove =)
        var randomString = Convert.ToBase64String(randomBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return $"term_sk_live_{randomString}";
    }
}
