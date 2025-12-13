using Zillo.Domain.Entities;
using Zillo.Domain.Interfaces;
using Supabase;

namespace Zillo.Infrastructure.Repositories;

public class AccountUserRepository : IAccountUserRepository
{
    private readonly Client _supabase;

    public AccountUserRepository(Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<List<AccountUser>> GetBySupabaseUserIdAsync(string supabaseUserId)
    {
        var response = await _supabase
            .From<AccountUserModel>()
            .Where(x => x.SupabaseUserId == supabaseUserId)
            .Where(x => x.IsActive == true)
            .Order(x => x.JoinedAt, Postgrest.Constants.Ordering.Ascending)
            .Get();

        return response.Models.Select(m => m.ToEntity()).ToList();
    }

    public async Task<AccountUser?> GetByUserAndAccountAsync(string supabaseUserId, Guid accountId)
    {
        var response = await _supabase
            .From<AccountUserModel>()
            .Where(x => x.SupabaseUserId == supabaseUserId)
            .Where(x => x.AccountId == accountId)
            .Where(x => x.IsActive == true)
            .Single();

        return response?.ToEntity();
    }

    public async Task<List<AccountUser>> GetByAccountIdAsync(Guid accountId)
    {
        var response = await _supabase
            .From<AccountUserModel>()
            .Where(x => x.AccountId == accountId)
            .Where(x => x.IsActive == true)
            .Order(x => x.Role, Postgrest.Constants.Ordering.Ascending)
            .Get();

        return response.Models.Select(m => m.ToEntity()).ToList();
    }

    public async Task<bool> HasAccessAsync(string supabaseUserId, Guid accountId)
    {
        var response = await _supabase
            .From<AccountUserModel>()
            .Where(x => x.SupabaseUserId == supabaseUserId)
            .Where(x => x.AccountId == accountId)
            .Where(x => x.IsActive == true)
            .Get();

        return response.Models.Any();
    }

    public async Task<bool> IsOwnerAsync(string supabaseUserId, Guid accountId)
    {
        var response = await _supabase
            .From<AccountUserModel>()
            .Where(x => x.SupabaseUserId == supabaseUserId)
            .Where(x => x.AccountId == accountId)
            .Where(x => x.Role == "owner")
            .Where(x => x.IsActive == true)
            .Get();

        return response.Models.Any();
    }

    public async Task<AccountUser> CreateAsync(AccountUser accountUser)
    {
        var model = AccountUserModel.FromEntity(accountUser);
        var response = await _supabase
            .From<AccountUserModel>()
            .Insert(model);

        if (response.Models == null || !response.Models.Any())
            throw new Exception("Failed to create account user");

        return response.Models.First().ToEntity();
    }

    public async Task<AccountUser> UpdateAsync(AccountUser accountUser)
    {
        var model = AccountUserModel.FromEntity(accountUser);
        var response = await _supabase
            .From<AccountUserModel>()
            .Update(model);

        return response.Models.First().ToEntity();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _supabase
            .From<AccountUserModel>()
            .Where(x => x.Id == id)
            .Delete();
    }
}

[Postgrest.Attributes.Table("account_users")]
public class AccountUserModel : Postgrest.Models.BaseModel
{
    [Postgrest.Attributes.PrimaryKey("id")]
    public Guid Id { get; set; }

    [Postgrest.Attributes.Column("supabase_user_id")]
    public string SupabaseUserId { get; set; } = string.Empty;

    [Postgrest.Attributes.Column("account_id")]
    public Guid AccountId { get; set; }

    [Postgrest.Attributes.Column("email")]
    public string Email { get; set; } = string.Empty;

    [Postgrest.Attributes.Column("role")]
    public string Role { get; set; } = "owner";

    [Postgrest.Attributes.Column("invited_at")]
    public DateTime? InvitedAt { get; set; }

    [Postgrest.Attributes.Column("invited_by")]
    public Guid? InvitedBy { get; set; }

    [Postgrest.Attributes.Column("joined_at")]
    public DateTime? JoinedAt { get; set; }

    [Postgrest.Attributes.Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Postgrest.Attributes.Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Postgrest.Attributes.Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    public AccountUser ToEntity() => new()
    {
        Id = Id,
        SupabaseUserId = SupabaseUserId,
        AccountId = AccountId,
        Email = Email,
        Role = Role,
        InvitedAt = InvitedAt,
        InvitedBy = InvitedBy,
        JoinedAt = JoinedAt,
        IsActive = IsActive,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt
    };

    public static AccountUserModel FromEntity(AccountUser accountUser) => new()
    {
        Id = accountUser.Id,
        SupabaseUserId = accountUser.SupabaseUserId,
        AccountId = accountUser.AccountId,
        Email = accountUser.Email,
        Role = accountUser.Role,
        InvitedAt = accountUser.InvitedAt,
        InvitedBy = accountUser.InvitedBy,
        JoinedAt = accountUser.JoinedAt,
        IsActive = accountUser.IsActive,
        CreatedAt = accountUser.CreatedAt,
        UpdatedAt = accountUser.UpdatedAt
    };
}
