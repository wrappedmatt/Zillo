using Zillo.Domain.Entities;
using Zillo.Domain.Interfaces;
using Supabase;

namespace Zillo.Infrastructure.Repositories;

public class TerminalRepository : ITerminalRepository
{
    private readonly Client _supabase;

    public TerminalRepository(Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<Terminal?> GetByIdAsync(Guid id)
    {
        var response = await _supabase
            .From<TerminalModel>()
            .Where(x => x.Id == id)
            .Single();

        return response?.ToEntity();
    }

    public async Task<Terminal?> GetByApiKeyAsync(string apiKey)
    {
        var response = await _supabase
            .From<TerminalModel>()
            .Where(x => x.ApiKey == apiKey)
            .Where(x => x.IsActive == true)
            .Single();

        return response?.ToEntity();
    }

    public async Task<Terminal?> GetByPairingCodeAsync(string pairingCode)
    {
        var response = await _supabase
            .From<TerminalModel>()
            .Where(x => x.PairingCode == pairingCode)
            .Single();

        return response?.ToEntity();
    }

    public async Task<IEnumerable<Terminal>> GetByAccountIdAsync(Guid accountId)
    {
        var response = await _supabase
            .From<TerminalModel>()
            .Where(x => x.AccountId == accountId)
            .Order("created_at", Postgrest.Constants.Ordering.Descending)
            .Get();

        return response.Models.Select(m => m.ToEntity());
    }

    public async Task<IEnumerable<Terminal>> GetActiveByAccountIdAsync(Guid accountId)
    {
        var response = await _supabase
            .From<TerminalModel>()
            .Where(x => x.AccountId == accountId)
            .Where(x => x.IsActive == true)
            .Order("last_seen_at", Postgrest.Constants.Ordering.Descending)
            .Get();

        return response.Models.Select(m => m.ToEntity());
    }

    public async Task<Terminal> CreateAsync(Terminal terminal)
    {
        var model = TerminalModel.FromEntity(terminal);
        var response = await _supabase
            .From<TerminalModel>()
            .Insert(model);

        return response.Models.First().ToEntity();
    }

    public async Task<Terminal> UpdateAsync(Terminal terminal)
    {
        var model = TerminalModel.FromEntity(terminal);
        var response = await _supabase
            .From<TerminalModel>()
            .Update(model);

        return response.Models.First().ToEntity();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _supabase
            .From<TerminalModel>()
            .Where(x => x.Id == id)
            .Delete();
    }

    public async Task UpdateLastSeenAsync(Guid terminalId)
    {
        var terminal = await GetByIdAsync(terminalId);
        if (terminal != null)
        {
            terminal.LastSeenAt = DateTime.UtcNow;
            await UpdateAsync(terminal);
        }
    }
}

[Postgrest.Attributes.Table("terminals")]
public class TerminalModel : Postgrest.Models.BaseModel
{
    [Postgrest.Attributes.PrimaryKey("id")]
    public Guid Id { get; set; }

    [Postgrest.Attributes.Column("account_id")]
    public Guid AccountId { get; set; }

    [Postgrest.Attributes.Column("api_key")]
    public string ApiKey { get; set; } = string.Empty;

    [Postgrest.Attributes.Column("terminal_label")]
    public string TerminalLabel { get; set; } = string.Empty;

    [Postgrest.Attributes.Column("stripe_terminal_id")]
    public string? StripeTerminalId { get; set; }

    [Postgrest.Attributes.Column("device_model")]
    public string? DeviceModel { get; set; }

    [Postgrest.Attributes.Column("device_id")]
    public string? DeviceId { get; set; }

    [Postgrest.Attributes.Column("pairing_code")]
    public string? PairingCode { get; set; }

    [Postgrest.Attributes.Column("pairing_expires_at")]
    public DateTime? PairingExpiresAt { get; set; }

    [Postgrest.Attributes.Column("paired_at")]
    public DateTime? PairedAt { get; set; }

    [Postgrest.Attributes.Column("is_active")]
    public bool IsActive { get; set; }

    [Postgrest.Attributes.Column("last_seen_at")]
    public DateTime? LastSeenAt { get; set; }

    [Postgrest.Attributes.Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Postgrest.Attributes.Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    public Terminal ToEntity() => new()
    {
        Id = Id,
        AccountId = AccountId,
        ApiKey = ApiKey,
        TerminalLabel = TerminalLabel,
        StripeTerminalId = StripeTerminalId,
        DeviceModel = DeviceModel,
        DeviceId = DeviceId,
        PairingCode = PairingCode,
        PairingExpiresAt = PairingExpiresAt,
        PairedAt = PairedAt,
        IsActive = IsActive,
        LastSeenAt = LastSeenAt,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt
    };

    public static TerminalModel FromEntity(Terminal terminal) => new()
    {
        Id = terminal.Id,
        AccountId = terminal.AccountId,
        ApiKey = terminal.ApiKey,
        TerminalLabel = terminal.TerminalLabel,
        StripeTerminalId = terminal.StripeTerminalId,
        DeviceModel = terminal.DeviceModel,
        DeviceId = terminal.DeviceId,
        PairingCode = terminal.PairingCode,
        PairingExpiresAt = terminal.PairingExpiresAt,
        PairedAt = terminal.PairedAt,
        IsActive = terminal.IsActive,
        LastSeenAt = terminal.LastSeenAt,
        CreatedAt = terminal.CreatedAt,
        UpdatedAt = terminal.UpdatedAt
    };
}
