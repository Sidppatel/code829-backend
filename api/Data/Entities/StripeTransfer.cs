namespace Db.Entities;

public class StripeTransfer : BaseEntity
{
    public required string StripeTransferId { get; set; }

    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public Guid? PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }

    public int AmountCents { get; set; }
    public string Currency { get; set; } = "usd";

    public string? RawEvent { get; set; }
}
