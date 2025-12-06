using Zillo.Domain.Entities;

namespace Zillo.Domain.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id);
    Task<IEnumerable<Customer>> GetByAccountIdAsync(Guid accountId);
    Task<Customer?> GetByEmailAsync(string email);
    Task<Customer?> GetByEmailAndAccountIdAsync(string email, Guid accountId);
    Task<Customer?> GetByPortalTokenAsync(string token);
    Task<Customer> CreateAsync(Customer customer);
    Task<Customer> UpdateAsync(Customer customer);
    Task DeleteAsync(Guid id);
}
