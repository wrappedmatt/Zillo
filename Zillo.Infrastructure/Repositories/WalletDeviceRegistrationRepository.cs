using Zillo.Domain.Entities;
using Zillo.Domain.Interfaces;
using Supabase;

namespace Zillo.Infrastructure.Repositories;

public class WalletDeviceRegistrationRepository : IWalletDeviceRegistrationRepository
{
    private readonly Client _supabase;

    public WalletDeviceRegistrationRepository(Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<WalletDeviceRegistration?> GetByIdAsync(Guid id)
    {
        var response = await _supabase
            .From<WalletDeviceRegistration>()
            .Where(r => r.Id == id)
            .Single();

        return response;
    }

    public async Task<WalletDeviceRegistration?> GetByDeviceAndPassAsync(string deviceLibraryIdentifier, string passIdentifier)
    {
        var response = await _supabase
            .From<WalletDeviceRegistration>()
            .Where(r => r.DeviceLibraryIdentifier == deviceLibraryIdentifier)
            .Where(r => r.PassIdentifier == passIdentifier)
            .Get();

        return response.Models.FirstOrDefault();
    }

    public async Task<IEnumerable<WalletDeviceRegistration>> GetByCustomerIdAsync(Guid customerId)
    {
        var response = await _supabase
            .From<WalletDeviceRegistration>()
            .Where(r => r.CustomerId == customerId)
            .Get();

        return response.Models;
    }

    public async Task<IEnumerable<WalletDeviceRegistration>> GetByDeviceIdAsync(string deviceLibraryIdentifier)
    {
        var response = await _supabase
            .From<WalletDeviceRegistration>()
            .Where(r => r.DeviceLibraryIdentifier == deviceLibraryIdentifier)
            .Get();

        return response.Models;
    }

    public async Task<IEnumerable<string>> GetPassIdentifiersForDeviceAsync(string deviceLibraryIdentifier, WalletType walletType)
    {
        var walletTypeString = walletType == WalletType.Google ? "google" : "apple";

        var response = await _supabase
            .From<WalletDeviceRegistration>()
            .Where(r => r.DeviceLibraryIdentifier == deviceLibraryIdentifier)
            .Where(r => r.WalletTypeString == walletTypeString)
            .Get();

        return response.Models.Select(r => r.PassIdentifier);
    }

    public async Task<WalletDeviceRegistration> CreateAsync(WalletDeviceRegistration registration)
    {
        registration.CreatedAt = DateTime.UtcNow;
        registration.UpdatedAt = DateTime.UtcNow;

        var response = await _supabase
            .From<WalletDeviceRegistration>()
            .Insert(registration);

        return response.Models.First();
    }

    public async Task<WalletDeviceRegistration> UpdateAsync(WalletDeviceRegistration registration)
    {
        registration.UpdatedAt = DateTime.UtcNow;

        var response = await _supabase
            .From<WalletDeviceRegistration>()
            .Update(registration);

        return response.Models.First();
    }

    public async Task DeleteAsync(string deviceLibraryIdentifier, string passIdentifier)
    {
        await _supabase
            .From<WalletDeviceRegistration>()
            .Where(r => r.DeviceLibraryIdentifier == deviceLibraryIdentifier)
            .Where(r => r.PassIdentifier == passIdentifier)
            .Delete();
    }

    public async Task DeleteByCustomerIdAsync(Guid customerId)
    {
        await _supabase
            .From<WalletDeviceRegistration>()
            .Where(r => r.CustomerId == customerId)
            .Delete();
    }
}
