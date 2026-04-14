using Api.Middleware;
using Api.Services;
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
) : AdminEventsController(context, eventProc, tableProc, ticketTypeProc, fileStorage, adminLog)
{
    [HttpGet("{id:guid}/fees")]
    public async Task<IActionResult> GetEventFees(Guid id)
    {
        var ev = await context.EventViews.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null) return NotFound();

        var defaultFee = int.Parse(await settings.GetOrDefaultAsync("default_platform_fee_cents", "1500") ?? "1500");

        var tableTypes = await context.EventTablesSummaryViews.AsNoTracking()
            .Where(et => et.EventId == id && et.IsActive)
            .OrderBy(et => et.Label)
            .Select(et => new TableTypeFee(et.Id, et.Label, et.PriceCents, et.PlatformFeeCents))
            .ToListAsync();

        return Ok(new EventFeeResponse(
            ev.Id, ev.Title, ev.LayoutMode,
            ev.PricePerPersonCents, ev.MaxCapacity,
            ev.PlatformFeeCents, defaultFee, tableTypes
        ));
    }

    [HttpPut("{id:guid}/table-fees")]
    public async Task<IActionResult> UpdateTableTypeFees(Guid id, [FromBody] UpdateTableTypeFeesRequest request)
    {
        var ev = await context.Events.FindAsync(id);
        if (ev is null) return NotFound();

        var tableTypes = await context.EventTables
            .Where(et => et.EventId == id && et.IsActive)
            .ToListAsync();

        foreach (var (tableId, feeCents) in request.TableTypeFees)
        {
            var tt = tableTypes.FirstOrDefault(t => t.Id == tableId);
            if (tt is not null)
            {
                tt.PlatformFeeCents = feeCents;
                tt.UpdatedAt = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync();
        return Ok(new { message = "Table type fees updated" });
    }
}
