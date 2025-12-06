using Zillo.Domain.Entities;

namespace Zillo.Domain.Interfaces;

public interface ILocationRepository
{
    Task<Location?> GetByIdAsync(Guid id);
    Task<List<Location>> GetByAccountIdAsync(Guid accountId);
    Task<List<Location>> GetActiveByAccountIdAsync(Guid accountId);
    Task<Location> CreateAsync(Location location);
    Task<Location> UpdateAsync(Location location);
    Task DeleteAsync(Guid id);
}
