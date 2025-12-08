using Zillo.Domain.Entities;
using Zillo.Domain.Interfaces;
using Supabase;

namespace Zillo.Infrastructure.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly Client _supabase;

    public CustomerRepository(Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<Customer?> GetByIdAsync(Guid id)
    {
        var response = await _supabase
            .From<CustomerModel>()
            .Where(x => x.Id == id)
            .Single();

        return response?.ToEntity();
    }

    public async Task<IEnumerable<Customer>> GetByAccountIdAsync(Guid accountId)
    {
        var response = await _supabase
            .From<CustomerModel>()
            .Where(x => x.AccountId == accountId)
            .Get();

        return response.Models.Select(m => m.ToEntity());
    }

    public async Task<Customer?> GetByEmailAsync(string email)
    {
        var response = await _supabase
            .From<CustomerModel>()
            .Where(x => x.Email == email)
            .Single();

        return response?.ToEntity();
    }

    public async Task<Customer?> GetByEmailAndAccountIdAsync(string email, Guid accountId)
    {
        var response = await _supabase
            .From<CustomerModel>()
            .Where(x => x.Email == email)
            .Where(x => x.AccountId == accountId)
            .Single();

        return response?.ToEntity();
    }

    public async Task<Customer?> GetByPortalTokenAsync(string token)
    {
        var response = await _supabase
            .From<CustomerModel>()
            .Where(x => x.PortalToken == token)
            .Single();

        return response?.ToEntity();
    }

    public async Task<Customer> CreateAsync(Customer customer)
    {
        var model = CustomerModel.FromEntity(customer);
        var response = await _supabase
            .From<CustomerModel>()
            .Insert(model);

        return response.Models.First().ToEntity();
    }

    public async Task<Customer> UpdateAsync(Customer customer)
    {
        var model = CustomerModel.FromEntity(customer);
        var response = await _supabase
            .From<CustomerModel>()
            .Update(model);

        return response.Models.First().ToEntity();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _supabase
            .From<CustomerModel>()
            .Where(x => x.Id == id)
            .Delete();
    }
}

[Postgrest.Attributes.Table("customers")]
public class CustomerModel : Postgrest.Models.BaseModel
{
    [Postgrest.Attributes.PrimaryKey("id")]
    public Guid Id { get; set; }

    [Postgrest.Attributes.Column("account_id")]
    public Guid AccountId { get; set; }

    [Postgrest.Attributes.Column("name")]
    public string Name { get; set; } = string.Empty;

    [Postgrest.Attributes.Column("email")]
    public string Email { get; set; } = string.Empty;

    [Postgrest.Attributes.Column("phone_number")]
    public string? PhoneNumber { get; set; }

    [Postgrest.Attributes.Column("points_balance")]
    public int PointsBalance { get; set; }

    [Postgrest.Attributes.Column("cashback_balance")]
    public decimal CashbackBalance { get; set; }

    [Postgrest.Attributes.Column("welcome_incentive_awarded")]
    public bool WelcomeIncentiveAwarded { get; set; }

    [Postgrest.Attributes.Column("card_linked_at")]
    public DateTime? CardLinkedAt { get; set; }

    [Postgrest.Attributes.Column("portal_token")]
    public string? PortalToken { get; set; }

    [Postgrest.Attributes.Column("portal_token_expires_at")]
    public DateTime? PortalTokenExpiresAt { get; set; }

    [Postgrest.Attributes.Column("last_announcement_message")]
    public string? LastAnnouncementMessage { get; set; }

    [Postgrest.Attributes.Column("last_announcement_at")]
    public DateTime? LastAnnouncementAt { get; set; }

    [Postgrest.Attributes.Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Postgrest.Attributes.Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    public Customer ToEntity() => new()
    {
        Id = Id,
        AccountId = AccountId,
        Name = Name,
        Email = Email,
        PhoneNumber = PhoneNumber,
        PointsBalance = PointsBalance,
        CashbackBalance = CashbackBalance,
        WelcomeIncentiveAwarded = WelcomeIncentiveAwarded,
        CardLinkedAt = CardLinkedAt,
        PortalToken = PortalToken,
        PortalTokenExpiresAt = PortalTokenExpiresAt,
        LastAnnouncementMessage = LastAnnouncementMessage,
        LastAnnouncementAt = LastAnnouncementAt,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt
    };

    public static CustomerModel FromEntity(Customer customer) => new()
    {
        Id = customer.Id,
        AccountId = customer.AccountId,
        Name = customer.Name,
        Email = customer.Email,
        PhoneNumber = customer.PhoneNumber,
        PointsBalance = customer.PointsBalance,
        CashbackBalance = customer.CashbackBalance,
        WelcomeIncentiveAwarded = customer.WelcomeIncentiveAwarded,
        CardLinkedAt = customer.CardLinkedAt,
        PortalToken = customer.PortalToken,
        PortalTokenExpiresAt = customer.PortalTokenExpiresAt,
        LastAnnouncementMessage = customer.LastAnnouncementMessage,
        LastAnnouncementAt = customer.LastAnnouncementAt,
        CreatedAt = customer.CreatedAt,
        UpdatedAt = customer.UpdatedAt
    };
}
