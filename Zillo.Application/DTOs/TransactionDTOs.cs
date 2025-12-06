namespace Zillo.Application.DTOs;

public record TransactionDto(
    Guid Id,
    Guid CustomerId,
    int Points,
    decimal CashbackAmount,
    decimal? Amount,
    string Type,
    string Description,
    DateTime CreatedAt,
    Guid? PaymentId,
    string? StripePaymentIntentId
);

public record CreateTransactionRequest(
    Guid CustomerId,
    Guid AccountId,
    int Points,
    decimal CashbackAmount,
    decimal? Amount,
    string Type,
    string Description,
    Guid? PaymentId = null,
    string? StripePaymentIntentId = null
);
