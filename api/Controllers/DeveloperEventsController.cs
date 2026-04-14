using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Events;
using Contracts.Enums;
using Db;
using Db.Repositories.StoredProcedures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("developer/events")]
[Authorize]
[RequireRole(UserRole.Developer)]
public class DeveloperEventsController(
    EventPlatformDbContext context,
    IEventProcedures eventProc,
    ITableProcedures tableProc,
    IEventTicketTypeProcedures ticketTypeProc,
    IFileStorageService fileStorage,
    IAdminLogService adminLog,
    ISettingsService settings
) : AdminEventsController(context, eventProc, tableProc, ticketTypeProc, fileStorage, adminLog, settings)
{
    [HttpGet("{id:guid}/fees")]
    public async Task<IActionResult> GetEventFees(Guid id)
    {
        var ev = await context.EventViews.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null) return NotFound();

        var defaultFeeKey = ev.LayoutMode == "Grid" ? "default_platform_fee_grid_cents" : "default_platform_fee_open_cents";
        var defaultFeeDefault = ev.LayoutMode == "Grid" ? "2500" : "1000";
        var defaultFee = int.Parse(await settings.GetOrDefaultAsync(defaultFeeKey, defaultFeeDefault) ?? defaultFeeDefault);

        var tableTypes = await context.EventTablesSummaryViews.AsNoTracking()
            .Where(et => et.EventId == id && et.IsActive)
            .OrderBy(et => et.Label)
            .Select(et => new TableTypeFee(et.Id, et.Label, et.PriceCents, et.PlatformFeeCents,
                et.BookedTables > 0 || et.LockedTables > 0))
            .ToListAsync();

        var ticketTypes = await context.EventTicketTypeSummaryViews.AsNoTracking()
            .Where(tt => tt.EventId == id && tt.IsActive)
            .OrderBy(tt => tt.SortOrder)
            .Select(tt => new TicketTypeFee(tt.Id, tt.Label, tt.PriceCents, tt.PlatformFeeCents,
                tt.SoldCount > 0))
            .ToListAsync();

        return Ok(new EventFeeResponse(
            ev.Id, ev.Title, ev.LayoutMode,
            ev.PricePerPersonCents, ev.MaxCapacity,
            defaultFee, tableTypes, ticketTypes
        ));
    }

    [HttpPut("{id:guid}/ticket-type-fees")]
    public async Task<IActionResult> UpdateTicketTypeFees(Guid id, [FromBody] UpdateTicketTypeFeesRequest request)
    {
        var ev = await context.Events.FindAsync(id);
        if (ev is null) return NotFound();

        var ticketTypes = await context.EventTicketTypes
            .Where(tt => tt.EventId == id && tt.IsActive)
            .ToListAsync();

        foreach (var (typeId, feeCents) in request.TicketTypeFees)
        {
            var tt = ticketTypes.FirstOrDefault(t => t.Id == typeId);
            if (tt is null) continue;

            if (feeCents != tt.PlatformFeeCents)
            {
                var hasSales = await context.BookingViews.AsNoTracking()
                    .AnyAsync(b => b.EventTicketTypeId == typeId
                        && b.Status != "Cancelled" && b.Status != "Expired" && b.Status != "Refunded");
                if (hasSales)
                    return BadRequest(new ApiError(400,
                        $"Cannot change platform fee for '{tt.Label}' — tickets have been sold",
                        HttpContext.TraceIdentifier));
            }

            tt.PlatformFeeCents = feeCents;
            tt.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
        return Ok(new { message = "Ticket type fees updated" });
    }

    [HttpPut("{id:guid}/table-fees")]
    public async Task<IActionResult> UpdateTableTypeFees(Guid id, [FromBody] UpdateTableTypeFeesRequest request)
    {
        var ev = await context.Events.FindAsync(id);
        if (ev is null) return NotFound();

        var tableTypes = await context.EventTables
            .Include(et => et.Tables)
            .Where(et => et.EventId == id && et.IsActive)
            .ToListAsync();

        foreach (var (tableId, feeCents) in request.TableTypeFees)
        {
            var tt = tableTypes.FirstOrDefault(t => t.Id == tableId);
            if (tt is null) continue;

            if (feeCents != tt.PlatformFeeCents)
            {
                // Check if any tables under this type are sold or locked
                var ttTableIds = tt.Tables.Select(t => t.Id).ToList();
                var hasSales = await context.Bookings.AnyAsync(b =>
                    b.EventId == id && b.TableId.HasValue && ttTableIds.Contains(b.TableId.Value)
                    && b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.Expired && b.Status != BookingStatus.Refunded);
                var hasLocks = tt.Tables.Any(t => t.Status == TableStatus.Locked && t.LockExpiresAt > DateTime.UtcNow);

                if (hasSales || hasLocks)
                    return BadRequest(new ApiError(400,
                        $"Cannot change platform fee for '{tt.Label}' — tickets have been sold or locked",
                        HttpContext.TraceIdentifier));
            }

            tt.PlatformFeeCents = feeCents;
            tt.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
        return Ok(new { message = "Table type fees updated" });
    }
}
