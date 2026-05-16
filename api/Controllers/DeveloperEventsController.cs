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

[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}/developer/events")]
[Authorize]
[RequireRole(UserRole.Developer)]
public class DeveloperEventsController(
    EventPlatformDbContext context,
    IEventProcedures eventProc,
    ITableProcedures tableProc,
    ILayoutProcedures layoutProc,
    IEventTicketTypeProcedures ticketTypeProc,
    IFileStorageService fileStorage,
    IAdminLogService adminLog,
    ISettingsService settings,
    IEventImageService eventImageService,
    IBusinessUserEventProcedures businessUserEventProc,
    IBusinessUserProcedures businessUserProc,
    IOrganizationProcedures organizationProc,
    ICacheService cache
) : AdminEventsController(context, eventProc, tableProc, layoutProc, ticketTypeProc, fileStorage, adminLog, settings, eventImageService, businessUserEventProc, businessUserProc, organizationProc, cache)
{
    [HttpGet("{id:guid}/fees")]
    public async Task<IActionResult> GetEventFees(Guid id)
    {
        var ev = await Context.EventViews.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound();

        var defaultFeeKey = ev.LayoutMode == "Grid" ? "default_platform_fee_grid_cents" : "default_platform_fee_open_cents";
        var defaultFeeDefault = ev.LayoutMode == "Grid" ? "2500" : "1000";
        var defaultFee = int.Parse(await Settings.GetOrDefaultAsync(defaultFeeKey, defaultFeeDefault) ?? defaultFeeDefault);

        var tableTypes = await Context.EventTablesSummaryViews.AsNoTracking()
            .Where(et => et.EventId == id && et.IsActive)
            .OrderBy(et => et.Label)
            .Select(et => new TableTypeFee(et.EventTableId, et.Label, et.PriceCents, et.PlatformFeeCents,
                et.BookedTables > 0 || et.LockedTables > 0))
            .ToListAsync();

        var ticketTypes = await Context.EventTicketTypeSummaryViews.AsNoTracking()
            .Where(tt => tt.EventId == id && tt.IsActive)
            .OrderBy(tt => tt.SortOrder)
            .Select(tt => new TicketTypeFee(tt.EventTicketTypeId, tt.Label, tt.PriceCents, tt.PlatformFeeCents,
                tt.SoldCount > 0))
            .ToListAsync();

        return Ok(new EventFeeResponse(
            ev.EventId, ev.Title, ev.LayoutMode,
            ev.PricePerPersonCents, ev.MaxCapacity,
            defaultFee, tableTypes, ticketTypes
        ));
    }

    [HttpPut("{id:guid}/ticket-type-fees")]
    public async Task<IActionResult> UpdateTicketTypeFees(
        Guid id,
        [FromBody] UpdateTicketTypeFeesRequest request,
        [FromServices] IEventTicketTypeProcedures ticketTypeProc)
    {
        var evExists = await Context.EventViews.AsNoTracking().AnyAsync(e => e.EventId == id);
        if (!evExists) return NotFound();

        var ticketTypes = await Context.EventTicketTypeSummaryViews
            .AsNoTracking()
            .Where(tt => tt.EventId == id && tt.IsActive)
            .ToListAsync();

        foreach (var (typeId, feeCents) in request.TicketTypeFees)
        {
            var tt = ticketTypes.FirstOrDefault(t => t.EventTicketTypeId == typeId);
            if (tt is null) continue;

            if (feeCents != tt.PlatformFeeCents)
            {
                var hasSales = await Context.PurchaseViews.AsNoTracking()
                    .AnyAsync(b => b.EventTicketTypeId == typeId
                        && b.Status != "Cancelled" && b.Status != "Expired" && b.Status != "Refunded");
                if (hasSales)
                    return BadRequest(new ApiError(400,
                        $"Cannot change platform fee for '{tt.Label}' — tickets have been sold",
                        HttpContext.TraceIdentifier));
            }

            await ticketTypeProc.UpdateAsync(
                tt.EventTicketTypeId, label: null, priceCents: null, platformFeeCents: feeCents,
                maxQuantity: null, sortOrder: null, isActive: null, description: null);
        }

        return Ok(new { message = "Ticket type fees updated" });
    }

    [HttpPut("{id:guid}/table-fees")]
    public async Task<IActionResult> UpdateTableTypeFees(
        Guid id,
        [FromBody] UpdateTableTypeFeesRequest request,
        [FromServices] ILayoutProcedures layoutProc)
    {
        var evExists = await Context.EventViews.AsNoTracking().AnyAsync(e => e.EventId == id);
        if (!evExists) return NotFound();

        var tableTypes = await layoutProc.ListEventTablesForEventAsync(id);
        var activeTypes = tableTypes.Where(et => et.IsActive).ToList();

        foreach (var (tableId, feeCents) in request.TableTypeFees)
        {
            var tt = activeTypes.FirstOrDefault(t => t.Id == tableId);
            if (tt is null) continue;

            if (feeCents != tt.PlatformFeeCents)
            {

                var hasSales = await layoutProc.EventTableHasActivePurchasesAsync(id, tt.Id);
                var hasLocks = await layoutProc.EventTableHasLockedTablesAsync(tt.Id);
                if (hasSales || hasLocks)
                    return BadRequest(new ApiError(400,
                        $"Cannot change platform fee for '{tt.Label}' — tickets have been sold or locked",
                        HttpContext.TraceIdentifier));
            }

            await layoutProc.UpdateEventTableAsync(
                tt.Id, label: null, capacity: null, shape: null, color: null,
                priceCents: null, isActive: null, platformFeeCents: feeCents);
        }

        return Ok(new { message = "Table type fees updated" });
    }

    [HttpPost("{id:guid}/relink-orphan-tiers")]
    public async Task<IActionResult> RelinkOrphanTicketTypes(
    Guid id,
    [FromServices] IEventTicketTypeProcedures ticketTypeProc)
    {
        var ev = await Context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));

        var moved = await ticketTypeProc.RelinkOrphansAsync(id);
        await AdminLog.LogAsync("event.ticket_type.orphans_relinked", "Event", id,
            $"Relinked {moved} orphan purchase row(s) for event '{ev.Title}'");
        await Cache.InvalidateEventAsync(id);

        return Ok(new { eventId = id, purchasesUpdated = moved });
    }
}
