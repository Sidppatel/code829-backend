using Contracts.Enums;

namespace Db.Entities;

public class Payment : BaseEntity
{
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public required string PaymentIntentId { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.RequiresConfirmation;
    public int AmountCents { get; set; }
    public string Currency { get; set; } = "usd";

    public string? RefundId { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? RefundedAt { get; set; }
}
