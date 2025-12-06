namespace Zillo.Application.DTOs;

public record GoogleWalletSaveUrlResponse(
    string SaveUrl,
    string ObjectId
);

public record AppleDeviceRegistrationRequest(
    string PushToken
);

public record AppleSerialNumbersResponse(
    string[] SerialNumbers,
    string LastUpdated
);

public record WalletPassInfo(
    Guid CustomerId,
    string CustomerName,
    string CompanyName,
    decimal Balance,
    string LoyaltySystemType,
    decimal EarnRate,
    string? LogoUrl,
    string BackgroundColor,
    string ForegroundColor,
    string LabelColor
);
