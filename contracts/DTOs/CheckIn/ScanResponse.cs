namespace Contracts.DTOs.CheckIn;

public record ScanResponse(
    bool Success,
    string Message,
    string? PurchaseNumber,
    string? UserName,
    string? EventTitle,
    string? Status,
    DateTime? ScannedAt
);
