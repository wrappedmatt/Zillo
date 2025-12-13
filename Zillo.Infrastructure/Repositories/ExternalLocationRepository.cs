using Zillo.Domain.Entities;
using Zillo.Domain.Interfaces;
using Supabase;

namespace Zillo.Infrastructure.Repositories;

public class ExternalLocationRepository : IExternalLocationRepository
{
    private readonly Client _supabase;

    public ExternalLocationRepository(Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<ExternalLocation?> GetByIdAsync(Guid id)
    {
        var response = await _supabase
            .From<ExternalLocationModel>()
            .Where(x => x.Id == id)
            .Single();

        return response?.ToEntity();
    }

    public async Task<ExternalLocation?> GetByStripeLocationIdAsync(string stripeLocationId)
    {
        var response = await _supabase
            .From<ExternalLocationModel>()
            .Where(x => x.StripeLocationId == stripeLocationId)
            .Where(x => x.IsActive == true)
            .Single();

        return response?.ToEntity();
    }

    public async Task<ExternalLocation?> GetByPairingCodeAsync(string pairingCode)
    {
        var response = await _supabase
            .From<ExternalLocationModel>()
            .Where(x => x.PairingCode == pairingCode)
            .Where(x => x.IsActive == true)
            .Single();

        return response?.ToEntity();
    }

    public async Task<List<ExternalLocation>> GetByAccountIdAsync(Guid accountId)
    {
        var response = await _supabase
            .From<ExternalLocationModel>()
            .Where(x => x.AccountId == accountId)
            .Order(x => x.CreatedAt, Postgrest.Constants.Ordering.Ascending)
            .Get();

        return response.Models.Select(m => m.ToEntity()).ToList();
    }

    public async Task<List<ExternalLocation>> GetActiveByAccountIdAsync(Guid accountId)
    {
        var response = await _supabase
            .From<ExternalLocationModel>()
            .Where(x => x.AccountId == accountId)
            .Where(x => x.IsActive == true)
            .Order(x => x.CreatedAt, Postgrest.Constants.Ordering.Ascending)
            .Get();

        return response.Models.Select(m => m.ToEntity()).ToList();
    }

    public async Task<ExternalLocation> CreateAsync(ExternalLocation externalLocation)
    {
        var model = ExternalLocationModel.FromEntity(externalLocation);
        var response = await _supabase
            .From<ExternalLocationModel>()
            .Insert(model);

        if (response.Models == null || !response.Models.Any())
            throw new Exception("Failed to create external location");

        return response.Models.First().ToEntity();
    }

    public async Task<ExternalLocation> UpdateAsync(ExternalLocation externalLocation)
    {
        var model = ExternalLocationModel.FromEntity(externalLocation);
        var response = await _supabase
            .From<ExternalLocationModel>()
            .Update(model);

        return response.Models.First().ToEntity();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _supabase
            .From<ExternalLocationModel>()
            .Where(x => x.Id == id)
            .Delete();
    }

    public async Task UpdateLastSeenAsync(Guid id)
    {
        await _supabase
            .From<ExternalLocationModel>()
            .Where(x => x.Id == id)
            .Set(x => x.LastSeenAt!, DateTime.UtcNow)
            .Update();
    }
}

[Postgrest.Attributes.Table("external_locations")]
public class ExternalLocationModel : Postgrest.Models.BaseModel
{
    [Postgrest.Attributes.PrimaryKey("id")]
    public Guid Id { get; set; }

    [Postgrest.Attributes.Column("account_id")]
    public Guid AccountId { get; set; }

    [Postgrest.Attributes.Column("stripe_location_id")]
    public string StripeLocationId { get; set; } = string.Empty;

    [Postgrest.Attributes.Column("platform_name")]
    public string? PlatformName { get; set; }

    [Postgrest.Attributes.Column("label")]
    public string? Label { get; set; }

    [Postgrest.Attributes.Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Postgrest.Attributes.Column("pairing_code")]
    public string? PairingCode { get; set; }

    [Postgrest.Attributes.Column("last_seen_at")]
    public DateTime? LastSeenAt { get; set; }

    [Postgrest.Attributes.Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Postgrest.Attributes.Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    public ExternalLocation ToEntity() => new()
    {
        Id = Id,
        AccountId = AccountId,
        StripeLocationId = StripeLocationId,
        PlatformName = PlatformName,
        Label = Label,
        IsActive = IsActive,
        PairingCode = PairingCode,
        LastSeenAt = LastSeenAt,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt
    };

    public static ExternalLocationModel FromEntity(ExternalLocation entity) => new()
    {
        Id = entity.Id,
        AccountId = entity.AccountId,
        StripeLocationId = entity.StripeLocationId,
        PlatformName = entity.PlatformName,
        Label = entity.Label,
        IsActive = entity.IsActive,
        PairingCode = entity.PairingCode,
        LastSeenAt = entity.LastSeenAt,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}
