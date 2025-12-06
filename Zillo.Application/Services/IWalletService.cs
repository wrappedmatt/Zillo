namespace Zillo.Application.Services;

public interface IWalletService
{
    // Apple Wallet
    Task<byte[]> GenerateApplePassAsync(Guid customerId);
    Task<byte[]> GetLatestApplePassAsync(string serialNumber, string authToken);
    Task RegisterAppleDeviceAsync(string deviceLibraryIdentifier, string pushToken, string passTypeIdentifier, string serialNumber, string authToken);
    Task UnregisterAppleDeviceAsync(string deviceLibraryIdentifier, string passTypeIdentifier, string serialNumber, string authToken);
    Task<IEnumerable<string>> GetSerialNumbersForAppleDeviceAsync(string deviceLibraryIdentifier, string passTypeIdentifier);

    // Google Wallet
    Task<string> GetGoogleWalletSaveUrlAsync(Guid customerId);
    Task<string> CreateOrUpdateGooglePassAsync(Guid customerId);

    // Push notifications (called when balance changes)
    Task SendBalanceUpdateNotificationsAsync(Guid customerId);

    // Utility
    Task<bool> ValidateAuthTokenAsync(string serialNumber, string authToken);
}
