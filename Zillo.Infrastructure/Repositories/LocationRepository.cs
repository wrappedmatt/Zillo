using Zillo.Domain.Entities;
using Zillo.Domain.Interfaces;
using Supabase;

namespace Zillo.Infrastructure.Repositories;

public class LocationRepository : ILocationRepository
{
    private readonly Client _supabase;

    public LocationRepository(Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<Location?> GetByIdAsync(Guid id)
    {
        var response = await _supabase
            .From<LocationModel>()
            .Where(x => x.Id == id)
            .Single();

        return response?.ToEntity();
    }

    public async Task<List<Location>> GetByAccountIdAsync(Guid accountId)
    {
        var response = await _supabase
            .From<LocationModel>()
            .Where(x => x.AccountId == accountId)
            .Order(x => x.CreatedAt, Postgrest.Constants.Ordering.Ascending)
            .Get();

        return response.Models.Select(m => m.ToEntity()).ToList();
    }

    public async Task<List<Location>> GetActiveByAccountIdAsync(Guid accountId)
    {
        var response = await _supabase
            .From<LocationModel>()
            .Where(x => x.AccountId == accountId)
            .Where(x => x.IsActive == true)
            .Order(x => x.CreatedAt, Postgrest.Constants.Ordering.Ascending)
            .Get();

        return response.Models.Select(m => m.ToEntity()).ToList();
    }

    public async Task<Location> CreateAsync(Location location)
    {
        var model = LocationModel.FromEntity(location);
        var response = await _supabase
            .From<LocationModel>()
            .Insert(model);

        if (response.Models == null || !response.Models.Any())
            throw new Exception("Failed to create location");

        return response.Models.First().ToEntity();
    }

    public async Task<Location> UpdateAsync(Location location)
    {
        var model = LocationModel.FromEntity(location);
        var response = await _supabase
            .From<LocationModel>()
            .Update(model);

        return response.Models.First().ToEntity();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _supabase
            .From<LocationModel>()
            .Where(x => x.Id == id)
            .Delete();
    }
}

[Postgrest.Attributes.Table("locations")]
public class LocationModel : Postgrest.Models.BaseModel
{
    [Postgrest.Attributes.PrimaryKey("id")]
    public Guid Id { get; set; }

    [Postgrest.Attributes.Column("account_id")]
    public Guid AccountId { get; set; }

    [Postgrest.Attributes.Column("name")]
    public string Name { get; set; } = string.Empty;

    [Postgrest.Attributes.Column("address")]
    public string? Address { get; set; }

    [Postgrest.Attributes.Column("latitude")]
    public double Latitude { get; set; }

    [Postgrest.Attributes.Column("longitude")]
    public double Longitude { get; set; }

    [Postgrest.Attributes.Column("relevant_distance")]
    public double? RelevantDistance { get; set; }

    [Postgrest.Attributes.Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Postgrest.Attributes.Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Postgrest.Attributes.Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    public Location ToEntity() => new()
    {
        Id = Id,
        AccountId = AccountId,
        Name = Name,
        Address = Address,
        Latitude = Latitude,
        Longitude = Longitude,
        RelevantDistance = RelevantDistance,
        IsActive = IsActive,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt
    };

    public static LocationModel FromEntity(Location location) => new()
    {
        Id = location.Id,
        AccountId = location.AccountId,
        Name = location.Name,
        Address = location.Address,
        Latitude = location.Latitude,
        Longitude = location.Longitude,
        RelevantDistance = location.RelevantDistance,
        IsActive = location.IsActive,
        CreatedAt = location.CreatedAt,
        UpdatedAt = location.UpdatedAt
    };
}
