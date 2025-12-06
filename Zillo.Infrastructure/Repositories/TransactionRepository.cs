using Zillo.Domain.Entities;
using Zillo.Domain.Interfaces;
using Supabase;

namespace Zillo.Infrastructure.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly Client _supabase;

    public TransactionRepository(Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<Transaction?> GetByIdAsync(Guid id)
    {
        var response = await _supabase
            .From<TransactionModel>()
            .Where(x => x.Id == id)
            .Single();

        return response?.ToEntity();
    }

    public async Task<IEnumerable<Transaction>> GetByCustomerIdAsync(Guid customerId)
    {
        var response = await _supabase
            .From<TransactionModel>()
            .Where(x => x.CustomerId == customerId)
            .Order("created_at", Postgrest.Constants.Ordering.Descending)
            .Get();

        return response.Models.Select(m => m.ToEntity());
    }

    public async Task<Transaction> CreateAsync(Transaction transaction)
    {
        var model = TransactionModel.FromEntity(transaction);
        var response = await _supabase
            .From<TransactionModel>()
            .Insert(model);

        return response.Models.First().ToEntity();
    }
}

[Postgrest.Attributes.Table("transactions")]
public class TransactionModel : Postgrest.Models.BaseModel
{
    [Postgrest.Attributes.PrimaryKey("id")]
    public Guid Id { get; set; }

    [Postgrest.Attributes.Column("customer_id")]
    public Guid CustomerId { get; set; }

    [Postgrest.Attributes.Column("account_id")]
    public Guid AccountId { get; set; }

    [Postgrest.Attributes.Column("points")]
    public int Points { get; set; }

    [Postgrest.Attributes.Column("cashback_amount")]
    public decimal CashbackAmount { get; set; }

    [Postgrest.Attributes.Column("amount")]
    public decimal? Amount { get; set; }

    [Postgrest.Attributes.Column("type")]
    public string Type { get; set; } = string.Empty;

    [Postgrest.Attributes.Column("description")]
    public string Description { get; set; } = string.Empty;

    [Postgrest.Attributes.Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Postgrest.Attributes.Column("payment_id")]
    public Guid? PaymentId { get; set; }

    [Postgrest.Attributes.Column("stripe_payment_intent_id")]
    public string? StripePaymentIntentId { get; set; }

    public Transaction ToEntity() => new()
    {
        Id = Id,
        CustomerId = CustomerId,
        AccountId = AccountId,
        Points = Points,
        CashbackAmount = CashbackAmount,
        Amount = Amount,
        Type = Type,
        Description = Description,
        CreatedAt = CreatedAt,
        PaymentId = PaymentId,
        StripePaymentIntentId = StripePaymentIntentId
    };

    public static TransactionModel FromEntity(Transaction transaction) => new()
    {
        Id = transaction.Id,
        CustomerId = transaction.CustomerId,
        AccountId = transaction.AccountId,
        Points = transaction.Points,
        CashbackAmount = transaction.CashbackAmount,
        Amount = transaction.Amount,
        Type = transaction.Type,
        Description = transaction.Description,
        CreatedAt = transaction.CreatedAt,
        PaymentId = transaction.PaymentId,
        StripePaymentIntentId = transaction.StripePaymentIntentId
    };
}
