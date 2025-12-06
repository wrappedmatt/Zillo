namespace Zillo.Application.DTOs;

public record TerminalDto(
    Guid Id,
    Guid AccountId,
    string TerminalLabel,
    string? StripeTerminalId,
    string? DeviceModel,
    string? DeviceId,
    bool IsActive,
    DateTime? LastSeenAt,
    DateTime? PairedAt,
    DateTime CreatedAt,
    string Status  // "online", "idle", "offline"
);

public record GeneratePairingCodeRequest(
    string? TerminalLabel = null
);

public record GeneratePairingCodeResponse(
    string PairingCode,
    DateTime ExpiresAt,
    int ExpiresInSeconds
);

public record PairTerminalRequest(
    string PairingCode,
    string TerminalLabel,
    string? DeviceModel = null,
    string? DeviceId = null
);

public record PairTerminalResponse(
    string ApiKey,
    Guid TerminalId,
    Guid AccountId,
    string TerminalLabel
);

public record UpdateTerminalRequest(
    string? TerminalLabel = null,
    bool? IsActive = null
);

public record TerminalListDto(
    Guid Id,
    string TerminalLabel,
    string Status,
    DateTime? LastSeenAt,
    DateTime? PairedAt,
    int TotalPayments,
    decimal TotalRevenue
);

public record RevokeTerminalRequest(
    string Reason
);
