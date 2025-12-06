using Zillo.Application.DTOs;
using Zillo.Application.Services;
using Zillo.Domain.Entities;
using Zillo.Domain.Interfaces;

namespace Zillo.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _paymentRepository;

    public PaymentService(IPaymentRepository paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public async Task<PaymentDto?> GetPaymentByIdAsync(Guid id, Guid accountId)
    {
        var payment = await _paymentRepository.GetByIdAsync(id);

        if (payment == null || payment.AccountId != accountId)
            return null;

        return MapToDto(payment);
    }

    public async Task<PaymentDto?> GetPaymentByStripePaymentIntentIdAsync(string stripePaymentIntentId)
    {
        var payment = await _paymentRepository.GetByStripePaymentIntentIdAsync(stripePaymentIntentId);
        return payment != null ? MapToDto(payment) : null;
    }

    public async Task<IEnumerable<PaymentDto>> GetPaymentsByAccountIdAsync(Guid accountId)
    {
        var payments = await _paymentRepository.GetByAccountIdAsync(accountId);
        return payments.Select(MapToDto);
    }

    public async Task<IEnumerable<PaymentDto>> GetPaymentsByCustomerIdAsync(Guid customerId, Guid accountId)
    {
        var payments = await _paymentRepository.GetByCustomerIdAsync(customerId);
        return payments.Where(p => p.AccountId == accountId).Select(MapToDto);
    }

    public async Task<IEnumerable<PaymentDto>> GetPaymentsByTerminalIdAsync(string terminalId, Guid accountId)
    {
        var payments = await _paymentRepository.GetByTerminalIdAsync(terminalId);
        return payments.Where(p => p.AccountId == accountId).Select(MapToDto);
    }

    public async Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest request)
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            AccountId = request.AccountId,
            CustomerId = request.CustomerId,
            StripePaymentIntentId = request.StripePaymentIntentId,
            TerminalId = request.TerminalId,
            TerminalLabel = request.TerminalLabel,
            Amount = request.Amount,
            AmountCharged = request.Amount, // Initially same as amount, will be updated if loyalty is redeemed
            LoyaltyRedeemed = 0,
            LoyaltyEarned = 0,
            Status = "pending",
            Currency = request.Currency,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _paymentRepository.CreateAsync(payment);
        return MapToDto(created);
    }

    public async Task<PaymentDto> UpdatePaymentAsync(Guid id, UpdatePaymentRequest request, Guid accountId)
    {
        var payment = await _paymentRepository.GetByIdAsync(id);

        if (payment == null || payment.AccountId != accountId)
            throw new Exception("Payment not found");

        // Update fields if provided
        if (request.CustomerId.HasValue)
            payment.CustomerId = request.CustomerId.Value;

        if (!string.IsNullOrEmpty(request.StripeChargeId))
            payment.StripeChargeId = request.StripeChargeId;

        if (request.AmountCharged.HasValue)
            payment.AmountCharged = request.AmountCharged.Value;

        if (request.LoyaltyRedeemed.HasValue)
            payment.LoyaltyRedeemed = request.LoyaltyRedeemed.Value;

        if (request.LoyaltyEarned.HasValue)
            payment.LoyaltyEarned = request.LoyaltyEarned.Value;

        if (!string.IsNullOrEmpty(request.Status))
            payment.Status = request.Status;

        if (!string.IsNullOrEmpty(request.PaymentMethodType))
            payment.PaymentMethodType = request.PaymentMethodType;

        if (request.CompletedAt.HasValue)
            payment.CompletedAt = request.CompletedAt.Value;

        payment.UpdatedAt = DateTime.UtcNow;

        var updated = await _paymentRepository.UpdateAsync(payment);
        return MapToDto(updated);
    }

    public async Task<PaymentDto> RefundPaymentAsync(Guid id, RefundPaymentRequest request, Guid accountId)
    {
        var payment = await _paymentRepository.GetByIdAsync(id);

        if (payment == null || payment.AccountId != accountId)
            throw new Exception("Payment not found");

        if (payment.Status != "completed")
            throw new Exception("Can only refund completed payments");

        payment.RefundReason = request.RefundReason;
        payment.RefundAmount = request.RefundAmount ?? payment.AmountCharged;
        payment.RefundedAt = DateTime.UtcNow;
        payment.Status = request.RefundAmount.HasValue && request.RefundAmount.Value < payment.AmountCharged
            ? "partially_refunded"
            : "refunded";
        payment.UpdatedAt = DateTime.UtcNow;

        var updated = await _paymentRepository.UpdateAsync(payment);
        return MapToDto(updated);
    }

    public async Task DeletePaymentAsync(Guid id, Guid accountId)
    {
        var payment = await _paymentRepository.GetByIdAsync(id);

        if (payment == null || payment.AccountId != accountId)
            throw new Exception("Payment not found");

        await _paymentRepository.DeleteAsync(id);
    }

    private static PaymentDto MapToDto(Payment payment) => new(
        payment.Id,
        payment.AccountId,
        payment.CustomerId,
        payment.StripePaymentIntentId,
        payment.StripeChargeId,
        payment.TerminalId,
        payment.TerminalLabel,
        payment.Amount,
        payment.AmountCharged,
        payment.LoyaltyRedeemed,
        payment.LoyaltyEarned,
        payment.Status,
        payment.Currency,
        payment.PaymentMethodType,
        payment.CreatedAt,
        payment.UpdatedAt,
        payment.CompletedAt
    );
}
