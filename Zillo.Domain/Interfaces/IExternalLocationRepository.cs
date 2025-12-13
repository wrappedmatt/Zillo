using Zillo.Domain.Entities;

namespace Zillo.Domain.Interfaces;

public interface IExternalLocationRepository
{
    Task<ExternalLocation?> GetByIdAsync(Guid id);
    Task<ExternalLocation?> GetByStripeLocationIdAsync(string stripeLocationId);
    Task<ExternalLocation?> GetByPairingCodeAsync(string pairingCode);
    Task<List<ExternalLocation>> GetByAccountIdAsync(Guid accountId);
    Task<List<ExternalLocation>> GetActiveByAccountIdAsync(Guid accountId);
    Task<ExternalLocation> CreateAsync(ExternalLocation externalLocation);
    Task<ExternalLocation> UpdateAsync(ExternalLocation externalLocation);
    Task DeleteAsync(Guid id);
    Task UpdateLastSeenAsync(Guid id);
}
