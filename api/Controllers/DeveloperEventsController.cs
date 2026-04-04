using Contracts.DTOs;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs.Events;
using Contracts.Enums;
using Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

/// <summary>
/// Developer event routes inherit all admin event actions via the /developer/events prefix,
/// plus dedicated fee management endpoints.
/// </summary>
[ApiController]
[IgnoreAntiforgeryToken]
[Route("developer/events")]
[Authorize]
[RequireRole(UserRole.Developer)]
public class DeveloperEventsController(
    EventPlatformDbContext context,
    IFileStorageService fileStorage,
    IAdminLogService adminLog,
    ISettingsService settings
) : AdminEventsController(context, fileStorage, adminLog)
{
    [HttpGet("{id:guid}/fees")]
    public async Task<IActionResult> GetEventFees(Guid id)
    {
        var ev = await context.Events.FindAsync(id);
        if (ev is null) return NotFound();

        var defaultFee = int.Parse(await settings.GetOrDefaultAsync("default_platform_fee_cents", "1500") ?? "1500");

        var tableTypes = await context.EventTables
            .Where(et => et.EventId == id && et.IsActive)
            .OrderBy(et => et.Label)
            .Select(et => new TableTypeFee(et.Id, et.Label, et.PriceCents, et.PlatformFeeCents))
            .ToListAsync();

        return Ok(new EventFeeResponse(
            ev.Id, ev.Title, ev.LayoutMode.ToString(),
            ev.PricePerPersonCents, ev.MaxCapacity,
            ev.PlatformFeeCents, defaultFee, tableTypes
        ));
    }

    [HttpPut("{id:guid}/fees")]
    public async Task<IActionResult> UpdateEventFee(Guid id, [FromBody] UpdateEventFeeRequest request)
    {
        var ev = await context.Events.FindAsync(id);
        if (ev is null) return NotFound();

        ev.PlatformFeeCents = request.PlatformFeeCents;
        ev.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return Ok(new { message = "Event platform fee updated" });
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
