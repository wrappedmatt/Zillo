using Zillo.Domain.Entities;
using Zillo.Domain.Interfaces;
using Supabase;

namespace Zillo.Infrastructure.Repositories;

public class CardRepository : ICardRepository
{
    private readonly Client _supabase;

    public CardRepository(Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<Card?> GetByFingerprintAsync(string fingerprint)
    {
        var response = await _supabase
            .From<Card>()
            .Where(c => c.CardFingerprint == fingerprint)
            .Get();

        return response.Models.FirstOrDefault();
    }

    public async Task<Card?> GetByIdAsync(Guid id)
    {
        var response = await _supabase
            .From<Card>()
            .Where(c => c.Id == id)
            .Single();

        return response;
    }

    public async Task<IEnumerable<Card>> GetByCustomerIdAsync(Guid customerId)
    {
        var response = await _supabase
            .From<Card>()
            .Where(c => c.CustomerId == customerId)
            .Order(c => c.LastUsedAt, Postgrest.Constants.Ordering.Descending)
            .Get();

        return response.Models;
    }

    public async Task<Card> CreateAsync(Card card)
    {
        var response = await _supabase
            .From<Card>()
            .Insert(card);

        return response.Models.First();
    }

    public async Task<Card> UpdateAsync(Card card)
    {
        var response = await _supabase
            .From<Card>()
            .Update(card);

        return response.Models.First();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _supabase
            .From<Card>()
            .Where(c => c.Id == id)
            .Delete();
    }
}
