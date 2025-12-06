using Zillo.Application.DTOs;

namespace Zillo.Application.Services;

public interface ITransactionService
{
    Task<IEnumerable<TransactionDto>> GetTransactionsByCustomerIdAsync(Guid customerId, Guid accountId);
    Task<TransactionDto> CreateTransactionAsync(CreateTransactionRequest request, Guid accountId);
}
