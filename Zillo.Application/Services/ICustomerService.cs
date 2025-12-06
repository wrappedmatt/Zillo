using Zillo.Application.DTOs;

namespace Zillo.Application.Services;

public interface ICustomerService
{
    Task<IEnumerable<CustomerDto>> GetCustomersByAccountIdAsync(Guid accountId);
    Task<CustomerDto?> GetCustomerByIdAsync(Guid id, Guid accountId);
    Task<CustomerDto?> GetCustomerByEmailAsync(string email, Guid accountId);
    Task<CustomerDto?> FindCustomerByEmailAsync(string email); // For terminal use - no account required
    Task<CustomerDto> CreateCustomerAsync(CreateCustomerRequest request, Guid accountId);
    Task<CustomerDto> UpdateCustomerAsync(Guid id, UpdateCustomerRequest request, Guid accountId);
    Task DeleteCustomerAsync(Guid id, Guid accountId);
    Task CreateAdjustmentAsync(Guid customerId, Guid accountId, CreateAdjustmentRequest request);
}
