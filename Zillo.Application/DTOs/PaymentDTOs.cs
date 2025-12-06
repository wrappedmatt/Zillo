namespace Zillo.Application.DTOs;

public record PaymentDto(
    Guid Id,
    Guid AccountId,
    Guid? CustomerId,
    string StripePaymentIntentId,
    string? StripeChargeId,
    string? TerminalId,
    string? TerminalLabel,
    decimal Amount,
    decimal AmountCharged,
    decimal LoyaltyRedeemed,
    int LoyaltyEarned,
    string Status,
    string Currency,
    string? PaymentMethodType,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? CompletedAt
);

public record CreatePaymentRequest(
    Guid AccountId,
    Guid? CustomerId,
    string StripePaymentIntentId,
    string? TerminalId,
    string? TerminalLabel,
    decimal Amount,
    string Currency = "nzd"
);

public record UpdatePaymentRequest(
    Guid? CustomerId,
    string? StripeChargeId,
    decimal? AmountCharged,
    decimal? LoyaltyRedeemed,
    int? LoyaltyEarned,
    string? Status,
    string? PaymentMethodType,
    DateTime? CompletedAt
);

public record RefundPaymentRequest(
    string RefundReason,
    decimal? RefundAmount = null // If null, full refund
);
