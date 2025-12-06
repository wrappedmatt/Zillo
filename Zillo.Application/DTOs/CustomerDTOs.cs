namespace Zillo.Application.DTOs;

public record CustomerDto(
    Guid Id,
    Guid AccountId,
    string Name,
    string Email,
    string? PhoneNumber,
    int PointsBalance,
    decimal CashbackBalance,
    decimal TotalSpent,
    DateTime CreatedAt
);

public record CreateCustomerRequest(
    string Name,
    string Email,
    string? PhoneNumber
);

public record UpdateCustomerRequest(
    string Name,
    string Email,
    string? PhoneNumber
);

public record CreateAdjustmentRequest(
    int? Points,
    decimal? CashbackAmount,
    string Description
);

public record CustomerPortalPreviewDto(
    string CompanyName,
    string LoyaltySystemType,
    int SignupBonusPoints,
    long SignupBonusCashback,
    int UnclaimedPoints,
    long UnclaimedCashback,
    int TotalPointsOnSignup,
    long TotalCashbackOnSignup,
    BrandingDto Branding
);

public record CustomerPortalDto(
    Guid CustomerId,
    string Name,
    string? Email,
    string? PhoneNumber,
    int PointsBalance,
    decimal CashbackBalance,
    string LoyaltySystemType,
    string CompanyName,
    BrandingDto Branding,
    List<CardInfoDto> RegisteredCards,
    List<TransactionDto> RecentTransactions,
    decimal CashbackRate,
    decimal PointsRate
);

public record BrandingDto(
    string? LogoUrl,
    string PrimaryColor,
    string BackgroundColor,
    string TextColor,
    string ButtonColor,
    string ButtonTextColor
);

public record CardInfoDto(
    Guid Id,
    string CardLast4,
    string CardBrand,
    bool IsPrimary,
    DateTime FirstUsedAt,
    DateTime LastUsedAt
);
