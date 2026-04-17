using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Api.Middleware;
using Api.Services;
using ClosedXML.Excel;
using Contracts.DTOs;
using Contracts.DTOs.Bookings;
using Contracts.Enums;
using CsvHelper;
using Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StackExchange.Redis;

namespace Api.Controllers;

[ApiController]
[Route("admin/bookings")]
[Authorize]
[RequireRole(UserRole.Admin)]
public class AdminBookingsController(
    EventPlatformDbContext context,
    IBookingService bookingService,
    IConnectionMultiplexer redis
) : ControllerBase
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] Guid? eventId = null,
        [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var cacheKey = $"bookings:list:{eventId}:{status}:{search}:{page}:{pageSize}";
        var db = redis.GetDatabase();
        var cached = await db.StringGetAsync(cacheKey);
        if (cached.HasValue)
            return Content(cached.ToString(), "application/json");

        var query = context.BookingViews.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(b => b.Status == status);
        if (eventId.HasValue)
            query = query.Where(b => b.EventId == eventId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(b =>
                b.BookingNumber.ToLower().Contains(term) ||
                (b.UserFirstName + " " + b.UserLastName).ToLower().Contains(term) ||
                b.UserEmail.ToLower().Contains(term) ||
                b.EventTitle.ToLower().Contains(term)
            );
        }

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var dtos = items.Select(b => new BookingDto(
            b.Id, b.BookingNumber, b.Status,
            b.UserId, $"{b.UserFirstName} {b.UserLastName}", b.EventId, b.EventTitle,
            b.EventStartDate, b.EventEndDate, b.EventCategory, b.EventImagePath,
            b.VenueName, !string.IsNullOrEmpty(b.VenueAddress) ? $"{b.VenueAddress}, {b.VenueCity}, {b.VenueState}" : null,
            b.SubtotalCents, b.TotalCents, null,
            b.TableId, b.TableLabel, b.SeatsReserved, b.EventTicketTypeId, b.EventTicketTypeLabel, b.TicketCount,
            b.StripeTransactionId.HasValue ? new StripeTransactionDto(b.StripeTransactionId.Value, b.PaymentIntentId!, b.PaymentStatus!, b.PaymentAmountCents ?? 0, b.TotalChargedCents, b.TaxAmountCents, b.StripeFeesCents, b.TransferAmountCents, b.PaidAt, b.RefundedAt) : null,
            b.CreatedAt, FeeCents: b.FeeCents
        )).ToList();

        var result = new PagedResponse<BookingDto>(dtos, total, page, pageSize);
        var json = JsonSerializer.Serialize(result, JsonOpts);
        await db.StringSetAsync(cacheKey, json, CacheTtl);

        return Ok(result);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] Guid? eventId = null)
    {
        var cacheKey = $"bookings:stats:{eventId}";
        var db = redis.GetDatabase();
        var cached = await db.StringGetAsync(cacheKey);
        if (cached.HasValue)
            return Content(cached.ToString(), "application/json");

        var query = context.BookingViews.AsNoTracking().AsQueryable();
        if (eventId.HasValue)
            query = query.Where(b => b.EventId == eventId.Value);

        var total = await query.CountAsync();
        var paid = await query.CountAsync(b => b.Status == "Paid" || b.Status == "CheckedIn");
        var checkedIn = await query.CountAsync(b => b.Status == "CheckedIn");
        var revenue = await query
            .Where(b => b.Status == "Paid" || b.Status == "CheckedIn")
            .SumAsync(b => (long)b.TotalCents);

        var result = new { total, paid, checkedIn, revenue };
        var json = JsonSerializer.Serialize(result, JsonOpts);
        await db.StringSetAsync(cacheKey, json, CacheTtl);

        return Ok(result);
    }

    [HttpPost("{id:guid}/refund")]
    public async Task<IActionResult> Refund(Guid id)
    {
        var booking = await context.BookingViews.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null) return NotFound(new ApiError(404, "Booking not found", HttpContext.TraceIdentifier));

        var adminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.Id == booking.EventId);
        if (ev is not null && ev.OrganizerId != adminId && !User.IsInRole(UserRole.Developer.ToString()))
            return StatusCode(403, new ApiError(403, "Not your event", HttpContext.TraceIdentifier));

        await InvalidateBookingCaches();
        try { return Ok(await bookingService.RefundAsync(id)); }
        catch (KeyNotFoundException ex) { Log.Warning(ex, "[AdminBookings] Refund failed: {Message}", ex.Message); return NotFound(new ApiError(404, ex.Message, HttpContext.TraceIdentifier)); }
        catch (InvalidOperationException ex) { Log.Warning(ex, "[AdminBookings] Refund failed: {Message}", ex.Message); return BadRequest(new ApiError(400, ex.Message, HttpContext.TraceIdentifier)); }
    }

    [HttpGet("export/csv")]
    public async Task<IActionResult> ExportCsv([FromQuery] Guid? eventId = null)
    {
        var rows = await GetExportRows(eventId);
        using var writer = new StringWriter();
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteRecords(rows);
        var bytes = System.Text.Encoding.UTF8.GetBytes(writer.ToString());
        return File(bytes, "text/csv", $"bookings-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("export/xlsx")]
    public async Task<IActionResult> ExportXlsx([FromQuery] Guid? eventId = null)
    {
        var rows = await GetExportRows(eventId);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Bookings");

        ws.Cell(1, 1).Value = "Booking #"; ws.Cell(1, 2).Value = "Status";
        ws.Cell(1, 3).Value = "User"; ws.Cell(1, 4).Value = "Event";
        ws.Cell(1, 5).Value = "Table"; ws.Cell(1, 6).Value = "Seats";
        ws.Cell(1, 7).Value = "Subtotal"; ws.Cell(1, 8).Value = "Fee";
        ws.Cell(1, 9).Value = "Total"; ws.Cell(1, 10).Value = "Created";
        ws.Range(1, 1, 1, 10).Style.Font.Bold = true;

        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            ws.Cell(i + 2, 1).Value = r.BookingNumber; ws.Cell(i + 2, 2).Value = r.Status;
            ws.Cell(i + 2, 3).Value = r.UserName; ws.Cell(i + 2, 4).Value = r.EventTitle;
            ws.Cell(i + 2, 5).Value = r.TableLabel; ws.Cell(i + 2, 6).Value = r.SeatsReserved;
            ws.Cell(i + 2, 7).Value = r.Subtotal; ws.Cell(i + 2, 8).Value = r.Fee;
            ws.Cell(i + 2, 9).Value = r.Total; ws.Cell(i + 2, 10).Value = r.Created;
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"bookings-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    private async Task InvalidateBookingCaches()
    {
        var db = redis.GetDatabase();
        var server = redis.GetServers()[0];
        await foreach (var key in server.KeysAsync(pattern: "bookings:*"))
            await db.KeyDeleteAsync(key);
    }

    private async Task<List<BookingExportRow>> GetExportRows(Guid? eventId)
    {
        var query = context.BookingViews.AsNoTracking().AsQueryable();

        if (eventId.HasValue)
            query = query.Where(b => b.EventId == eventId.Value);

        return await query.OrderByDescending(b => b.CreatedAt)
            .Select(b => new BookingExportRow(
                b.BookingNumber, b.Status, b.UserFirstName + " " + b.UserLastName, b.EventTitle,
                b.TableLabel ?? "",
                b.SeatsReserved ?? 0,
                "$" + (b.SubtotalCents / 100.0).ToString("F2"),
                "$" + (b.FeeCents / 100.0).ToString("F2"),
                "$" + (b.TotalCents / 100.0).ToString("F2"),
                b.CreatedAt.ToString("yyyy-MM-dd HH:mm")
            )).ToListAsync();
    }

    private record BookingExportRow(
        string BookingNumber, string Status, string UserName, string EventTitle,
        string TableLabel, int SeatsReserved,
        string Subtotal, string Fee, string Total, string Created);
}
