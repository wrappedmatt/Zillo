using Zillo.Domain.Entities;
using Zillo.Domain.Interfaces;
using Supabase;

namespace Zillo.Infrastructure.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly Client _supabase;

    public PaymentRepository(Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<Payment?> GetByIdAsync(Guid id)
    {
        var response = await _supabase
            .From<PaymentModel>()
            .Where(x => x.Id == id)
            .Single();

        return response?.ToEntity();
    }

    public async Task<Payment?> GetByStripePaymentIntentIdAsync(string stripePaymentIntentId)
    {
        var response = await _supabase
            .From<PaymentModel>()
            .Where(x => x.StripePaymentIntentId == stripePaymentIntentId)
            .Single();

        return response?.ToEntity();
    }

    public async Task<IEnumerable<Payment>> GetByAccountIdAsync(Guid accountId)
    {
        var response = await _supabase
            .From<PaymentModel>()
            .Where(x => x.AccountId == accountId)
            .Order("created_at", Postgrest.Constants.Ordering.Descending)
            .Get();

        return response.Models.Select(m => m.ToEntity());
    }

    public async Task<IEnumerable<Payment>> GetByCustomerIdAsync(Guid customerId)
    {
        var response = await _supabase
            .From<PaymentModel>()
            .Where(x => x.CustomerId == customerId)
            .Order("created_at", Postgrest.Constants.Ordering.Descending)
            .Get();

        return response.Models.Select(m => m.ToEntity());
    }

    public async Task<IEnumerable<Payment>> GetByTerminalIdAsync(string terminalId)
    {
        var response = await _supabase
            .From<PaymentModel>()
            .Where(x => x.TerminalId == terminalId)
            .Order("created_at", Postgrest.Constants.Ordering.Descending)
            .Get();

        return response.Models.Select(m => m.ToEntity());
    }

    public async Task<Payment> CreateAsync(Payment payment)
    {
        var model = PaymentModel.FromEntity(payment);
        var response = await _supabase
            .From<PaymentModel>()
            .Insert(model);

        return response.Models.First().ToEntity();
    }

    public async Task<Payment> UpdateAsync(Payment payment)
    {
        var model = PaymentModel.FromEntity(payment);
        var response = await _supabase
            .From<PaymentModel>()
            .Update(model);

        return response.Models.First().ToEntity();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _supabase
            .From<PaymentModel>()
            .Where(x => x.Id == id)
            .Delete();
    }
}

[Postgrest.Attributes.Table("payments")]
public class PaymentModel : Postgrest.Models.BaseModel
{
    [Postgrest.Attributes.PrimaryKey("id")]
    public Guid Id { get; set; }

    [Postgrest.Attributes.Column("account_id")]
    public Guid AccountId { get; set; }

    [Postgrest.Attributes.Column("customer_id")]
    public Guid? CustomerId { get; set; }

    [Postgrest.Attributes.Column("stripe_payment_intent_id")]
    public string StripePaymentIntentId { get; set; } = string.Empty;

    [Postgrest.Attributes.Column("stripe_charge_id")]
    public string? StripeChargeId { get; set; }

    [Postgrest.Attributes.Column("terminal_id")]
    public string? TerminalId { get; set; }

    [Postgrest.Attributes.Column("terminal_label")]
    public string? TerminalLabel { get; set; }

    [Postgrest.Attributes.Column("amount")]
    public decimal Amount { get; set; }

    [Postgrest.Attributes.Column("amount_charged")]
    public decimal AmountCharged { get; set; }

    [Postgrest.Attributes.Column("loyalty_redeemed")]
    public decimal LoyaltyRedeemed { get; set; }

    [Postgrest.Attributes.Column("loyalty_earned")]
    public int LoyaltyEarned { get; set; }

    [Postgrest.Attributes.Column("status")]
    public string Status { get; set; } = "pending";

    [Postgrest.Attributes.Column("currency")]
    public string Currency { get; set; } = "nzd";

    [Postgrest.Attributes.Column("payment_method_type")]
    public string? PaymentMethodType { get; set; }

    [Postgrest.Attributes.Column("metadata")]
    public string? Metadata { get; set; }

    [Postgrest.Attributes.Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Postgrest.Attributes.Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [Postgrest.Attributes.Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [Postgrest.Attributes.Column("refund_reason")]
    public string? RefundReason { get; set; }

    [Postgrest.Attributes.Column("refunded_at")]
    public DateTime? RefundedAt { get; set; }

    [Postgrest.Attributes.Column("refund_amount")]
    public decimal? RefundAmount { get; set; }

    public Payment ToEntity() => new()
    {
        Id = Id,
        AccountId = AccountId,
        CustomerId = CustomerId,
        StripePaymentIntentId = StripePaymentIntentId,
        StripeChargeId = StripeChargeId,
        TerminalId = TerminalId,
        TerminalLabel = TerminalLabel,
        Amount = Amount,
        AmountCharged = AmountCharged,
        LoyaltyRedeemed = LoyaltyRedeemed,
        LoyaltyEarned = LoyaltyEarned,
        Status = Status,
        Currency = Currency,
        PaymentMethodType = PaymentMethodType,
        Metadata = Metadata,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
        CompletedAt = CompletedAt,
        RefundReason = RefundReason,
        RefundedAt = RefundedAt,
        RefundAmount = RefundAmount
    };

    public static PaymentModel FromEntity(Payment payment) => new()
    {
        Id = payment.Id,
        AccountId = payment.AccountId,
        CustomerId = payment.CustomerId,
        StripePaymentIntentId = payment.StripePaymentIntentId,
        StripeChargeId = payment.StripeChargeId,
        TerminalId = payment.TerminalId,
        TerminalLabel = payment.TerminalLabel,
        Amount = payment.Amount,
        AmountCharged = payment.AmountCharged,
        LoyaltyRedeemed = payment.LoyaltyRedeemed,
        LoyaltyEarned = payment.LoyaltyEarned,
        Status = payment.Status,
        Currency = payment.Currency,
        PaymentMethodType = payment.PaymentMethodType,
        Metadata = payment.Metadata,
        CreatedAt = payment.CreatedAt,
        UpdatedAt = payment.UpdatedAt,
        CompletedAt = payment.CompletedAt,
        RefundReason = payment.RefundReason,
        RefundedAt = payment.RefundedAt,
        RefundAmount = payment.RefundAmount
    };
}
