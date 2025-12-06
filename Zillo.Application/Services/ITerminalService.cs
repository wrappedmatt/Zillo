using Zillo.Application.DTOs;

namespace Zillo.Application.Services;

public interface ITerminalService
{
    /// <summary>
    /// Generates a pairing code for terminal registration
    /// </summary>
    Task<GeneratePairingCodeResponse> GeneratePairingCodeAsync(Guid accountId, string? terminalLabel = null);

    /// <summary>
    /// Pairs a terminal using a pairing code and returns API key
    /// </summary>
    Task<PairTerminalResponse> PairTerminalAsync(PairTerminalRequest request);

    /// <summary>
    /// Validates an API key and returns terminal information
    /// </summary>
    Task<TerminalDto?> ValidateApiKeyAsync(string apiKey);

    /// <summary>
    /// Gets all terminals for an account
    /// </summary>
    Task<IEnumerable<TerminalDto>> GetTerminalsByAccountAsync(Guid accountId);

    /// <summary>
    /// Gets a specific terminal by ID
    /// </summary>
    Task<TerminalDto?> GetTerminalByIdAsync(Guid id, Guid accountId);

    /// <summary>
    /// Updates terminal information
    /// </summary>
    Task<TerminalDto> UpdateTerminalAsync(Guid id, UpdateTerminalRequest request, Guid accountId);

    /// <summary>
    /// Revokes/deactivates a terminal
    /// </summary>
    Task RevokeTerminalAsync(Guid id, Guid accountId);

    /// <summary>
    /// Deletes a terminal permanently
    /// </summary>
    Task DeleteTerminalAsync(Guid id, Guid accountId);

    /// <summary>
    /// Updates the last seen timestamp for a terminal
    /// </summary>
    Task UpdateLastSeenAsync(Guid terminalId);

    /// <summary>
    /// Cleans up expired pairing codes (called by scheduled job)
    /// </summary>
    Task<int> CleanupExpiredPairingCodesAsync();
}
