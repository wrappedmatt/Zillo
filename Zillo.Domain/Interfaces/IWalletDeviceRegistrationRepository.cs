using Zillo.Domain.Entities;

namespace Zillo.Domain.Interfaces;

public interface IWalletDeviceRegistrationRepository
{
    Task<WalletDeviceRegistration?> GetByIdAsync(Guid id);
    Task<WalletDeviceRegistration?> GetByDeviceAndPassAsync(string deviceLibraryIdentifier, string passIdentifier);
    Task<IEnumerable<WalletDeviceRegistration>> GetByCustomerIdAsync(Guid customerId);
    Task<IEnumerable<WalletDeviceRegistration>> GetByDeviceIdAsync(string deviceLibraryIdentifier);
    Task<IEnumerable<string>> GetPassIdentifiersForDeviceAsync(string deviceLibraryIdentifier, WalletType walletType);
    Task<WalletDeviceRegistration> CreateAsync(WalletDeviceRegistration registration);
    Task<WalletDeviceRegistration> UpdateAsync(WalletDeviceRegistration registration);
    Task DeleteAsync(string deviceLibraryIdentifier, string passIdentifier);
    Task DeleteByCustomerIdAsync(Guid customerId);
}
