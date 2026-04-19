namespace Db.Entities;

public class PurchaseTable
{
    public Guid PurchaseId { get; set; }
    public Purchase Purchase { get; set; } = null!;

    public Guid TableId { get; set; }
    public Table Table { get; set; } = null!;
}
