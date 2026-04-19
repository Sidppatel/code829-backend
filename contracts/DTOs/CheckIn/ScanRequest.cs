namespace Contracts.DTOs.CheckIn;

public record ScanRequest(string QrToken, Guid? EventId = null);
