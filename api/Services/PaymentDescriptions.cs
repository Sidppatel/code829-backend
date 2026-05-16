namespace Api.Services;

public static class PaymentDescriptions
{
    public static string Build(string purchaseNumber, string eventTitle, int? tableCount = null, int? seats = null)
    {
        var qty = tableCount is int tc
            ? $" ({tc} {(tc == 1 ? "table" : "tables")})"
            : seats is int s
                ? $" ({s} {(s == 1 ? "seat" : "seats")})"
                : string.Empty;
        var raw = $"{purchaseNumber} - {eventTitle}{qty}";
        return raw.Length > 1000 ? raw[..1000] : raw;
    }

    public static string? FromMetadata(IDictionary<string, string>? metadata)
    {
        if (metadata is null) return null;
        if (!metadata.TryGetValue("purchase_number", out var purchaseNumber) || string.IsNullOrEmpty(purchaseNumber))
            return null;
        if (!metadata.TryGetValue("event_name", out var eventName) || string.IsNullOrEmpty(eventName))
            return null;

        int? tableCount = metadata.TryGetValue("table_count", out var tcStr) && int.TryParse(tcStr, out var tc) ? tc : null;
        int? seats = metadata.TryGetValue("seats", out var sStr) && int.TryParse(sStr, out var s) ? s : null;

        return Build(purchaseNumber, eventName, tableCount, seats);
    }
}
