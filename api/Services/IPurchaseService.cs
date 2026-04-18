using Contracts.DTOs.Purchases;

namespace Api.Services;

public interface IPurchaseService
{
    Task<PurchaseDto> CreateAsync(Guid userId, CreatePurchaseRequest request);
    Task<PurchaseDto> ConfirmPaymentAsync(Guid purchaseId, Guid userId);
    Task<PurchaseDto> CancelAsync(Guid purchaseId, Guid userId);
    Task<PurchaseDto> RefundAsync(Guid purchaseId);
    Task<PurchaseDto?> GetByIdAsync(Guid purchaseId);
    Task<byte[]> GetQrImageAsync(Guid purchaseId, Guid userId);
}
