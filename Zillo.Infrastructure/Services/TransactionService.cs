using Zillo.Application.DTOs;
using Zillo.Application.Services;
using Zillo.Domain.Entities;
using Zillo.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Zillo.Infrastructure.Services;

public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IWalletService? _walletService;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(
        ITransactionRepository transactionRepository,
        ICustomerRepository customerRepository,
        ILogger<TransactionService> logger,
        IWalletService? walletService = null)
    {
        _transactionRepository = transactionRepository;
        _customerRepository = customerRepository;
        _logger = logger;
        _walletService = walletService;
    }

    public async Task<IEnumerable<TransactionDto>> GetTransactionsByCustomerIdAsync(Guid customerId, Guid accountId)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);

        if (customer == null || customer.AccountId != accountId)
            throw new Exception("Customer not found");

        var transactions = await _transactionRepository.GetByCustomerIdAsync(customerId);
        return transactions.Select(MapToDto);
    }

    public async Task<TransactionDto> CreateTransactionAsync(CreateTransactionRequest request, Guid accountId)
    {
        var customer = await _customerRepository.GetByIdAsync(request.CustomerId);

        if (customer == null || customer.AccountId != accountId)
            throw new Exception("Customer not found");

        // Validate transaction type
        var validTypes = new[] { "earn", "redeem", "cashback_earn", "cashback_redeem" };
        if (!validTypes.Contains(request.Type))
            throw new Exception("Invalid transaction type. Must be 'earn', 'redeem', 'cashback_earn', or 'cashback_redeem'");

        // For redeem types, ensure customer has enough balance
        if ((request.Type == "redeem" || request.Type == "cashback_redeem"))
        {
            if (request.Type == "redeem" && customer.PointsBalance < Math.Abs(request.Points))
                throw new Exception("Insufficient points balance");

            if (request.Type == "cashback_redeem" && customer.CashbackBalance < Math.Abs(request.CashbackAmount))
                throw new Exception("Insufficient cashback balance");
        }

        // Determine if this is a redeem transaction (negative points/cashback)
        var isRedeem = request.Type == "redeem" || request.Type == "cashback_redeem";

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId,
            AccountId = request.AccountId,
            Points = isRedeem ? -Math.Abs(request.Points) : Math.Abs(request.Points),
            CashbackAmount = isRedeem ? -Math.Abs(request.CashbackAmount) : Math.Abs(request.CashbackAmount),
            Amount = request.Amount,
            Type = request.Type,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            PaymentId = request.PaymentId,
            StripePaymentIntentId = request.StripePaymentIntentId
        };

        var created = await _transactionRepository.CreateAsync(transaction);

        // Note: Points balance is now automatically updated by database trigger
        // (See migration 002_add_stripe_terminal_support.sql)
        // No need to manually update customer.PointsBalance

        // Send wallet push notifications (balance has changed)
        if (_walletService != null)
        {
            try
            {
                await _walletService.SendBalanceUpdateNotificationsAsync(request.CustomerId);
            }
            catch (Exception ex)
            {
                // Don't fail the transaction if wallet notification fails
                _logger.LogWarning(ex, "Failed to send wallet notification for customer {CustomerId}", request.CustomerId);
            }
        }

        return MapToDto(created);
    }

    private static TransactionDto MapToDto(Transaction transaction) => new(
        transaction.Id,
        transaction.CustomerId,
        transaction.Points,
        transaction.CashbackAmount,
        transaction.Amount,
        transaction.Type,
        transaction.Description,
        transaction.CreatedAt,
        transaction.PaymentId,
        transaction.StripePaymentIntentId
    );
}
