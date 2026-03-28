using System.Globalization;
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

namespace Api.Controllers;

[ApiController]
[Route("admin/bookings")]
[Authorize]
[RequireRole(UserRole.Admin)]
public class AdminBookingsController(
    EventPlatformDbContext context,
    IBookingService bookingService
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] Guid? eventId = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = context.Bookings
            .Include(b => b.User).Include(b => b.Event)
            .Include(b => b.Items).ThenInclude(i => i.TicketType)
            .Include(b => b.Payment)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BookingStatus>(status, true, out var s))
            query = query.Where(b => b.Status == s);
        if (eventId.HasValue)
            query = query.Where(b => b.EventId == eventId.Value);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var dtos = items.Select(b => new BookingDto(
            b.Id, b.BookingNumber, b.Status.ToString(),
            b.UserId, b.User.Name, b.EventId, b.Event.Title,
            b.SubtotalCents, b.FeeCents, b.TotalCents, b.QrToken,
            b.Items.Select(i => new BookingItemDto(i.Id, i.TicketTypeId, i.TicketType.Name, i.SeatId, null, i.PriceCents)).ToList(),
            b.Payment is not null ? new PaymentDto(b.Payment.Id, b.Payment.PaymentIntentId, b.Payment.Status.ToString(), b.Payment.AmountCents, b.Payment.PaidAt, b.Payment.RefundedAt) : null,
            b.CreatedAt
        )).ToList();

        return Ok(new PagedResponse<BookingDto>(dtos, total, page, pageSize));
    }

    [HttpPost("{id:guid}/refund")]
    public async Task<IActionResult> Refund(Guid id)
    {
        try { return Ok(await bookingService.RefundAsync(id)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
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

        // Header
        ws.Cell(1, 1).Value = "Booking #"; ws.Cell(1, 2).Value = "Status";
        ws.Cell(1, 3).Value = "User"; ws.Cell(1, 4).Value = "Event";
        ws.Cell(1, 5).Value = "Subtotal"; ws.Cell(1, 6).Value = "Fee";
        ws.Cell(1, 7).Value = "Total"; ws.Cell(1, 8).Value = "Items";
        ws.Cell(1, 9).Value = "Created";
        ws.Range(1, 1, 1, 9).Style.Font.Bold = true;

        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            ws.Cell(i + 2, 1).Value = r.BookingNumber; ws.Cell(i + 2, 2).Value = r.Status;
            ws.Cell(i + 2, 3).Value = r.UserName; ws.Cell(i + 2, 4).Value = r.EventTitle;
            ws.Cell(i + 2, 5).Value = r.Subtotal; ws.Cell(i + 2, 6).Value = r.Fee;
            ws.Cell(i + 2, 7).Value = r.Total; ws.Cell(i + 2, 8).Value = r.Items;
            ws.Cell(i + 2, 9).Value = r.Created;
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"bookings-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    private async Task<List<BookingExportRow>> GetExportRows(Guid? eventId)
    {
        var query = context.Bookings
            .Include(b => b.User).Include(b => b.Event).Include(b => b.Items)
            .AsQueryable();

        if (eventId.HasValue)
            query = query.Where(b => b.EventId == eventId.Value);

        return await query.OrderByDescending(b => b.CreatedAt)
            .Select(b => new BookingExportRow(
                b.BookingNumber, b.Status.ToString(), b.User.Name, b.Event.Title,
                $"${b.SubtotalCents / 100.0:F2}", $"${b.FeeCents / 100.0:F2}",
                $"${b.TotalCents / 100.0:F2}", b.Items.Count,
                b.CreatedAt.ToString("yyyy-MM-dd HH:mm")
            )).ToListAsync();
    }

    private record BookingExportRow(
        string BookingNumber, string Status, string UserName, string EventTitle,
        string Subtotal, string Fee, string Total, int Items, string Created);
}
