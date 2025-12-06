using Zillo.Domain.Entities;

namespace Zillo.Domain.Interfaces;

public interface ITerminalRepository
{
    Task<Terminal?> GetByIdAsync(Guid id);
    Task<Terminal?> GetByApiKeyAsync(string apiKey);
    Task<Terminal?> GetByPairingCodeAsync(string pairingCode);
    Task<IEnumerable<Terminal>> GetByAccountIdAsync(Guid accountId);
    Task<IEnumerable<Terminal>> GetActiveByAccountIdAsync(Guid accountId);
    Task<Terminal> CreateAsync(Terminal terminal);
    Task<Terminal> UpdateAsync(Terminal terminal);
    Task DeleteAsync(Guid id);
    Task UpdateLastSeenAsync(Guid terminalId);
}
