using Api.Middleware;
using Api.Services;
using Contracts.Enums;
using Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

/// <summary>
/// Developer event routes inherit all admin event actions via the /developer/events prefix.
/// Only the platform-fees endpoint is developer-exclusive (admins cannot set platform fees).
/// </summary>
[ApiController]
[Route("developer/events")]
[Authorize]
[RequireRole(UserRole.Developer)]
public class DeveloperEventsController : AdminEventsController
{
    private readonly EventPlatformDbContext _context;
    private readonly IAdminLogService _adminLog;

    public DeveloperEventsController(
        EventPlatformDbContext context,
        IFileStorageService fileStorage,
        IAdminLogService adminLog
    ) : base(context, fileStorage, adminLog)
    {
        _context = context;
        _adminLog = adminLog;
    }

    [HttpPut("{id:guid}/platform-fees")]
    public async Task<IActionResult> UpdatePlatformFees(Guid id, [FromBody] Contracts.DTOs.Developer.UpdateEventPlatformFeesRequest request)
    {
        var ev = await _context.Events
            .Include(e => e.TicketTypes)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev is null) return NotFound(new { message = "Event not found" });

        if (request.TicketFees is not null)
        {
            foreach (var fee in request.TicketFees)
            {
                var tt = ev.TicketTypes.FirstOrDefault(t => t.Id == fee.TicketTypeId);
                if (tt is not null) tt.PlatformFeeCents = fee.PlatformFeeCents;
            }
        }

        if (request.TableTypeFees is not null)
        {
            // Get all table type IDs used by this event's tables
            var tableTypeIds = await _context.Tables
                .Where(t => t.EventId == id && t.TableTypeId.HasValue)
                .Select(t => t.TableTypeId!.Value)
                .Distinct()
                .ToListAsync();
            var tableTypes = await _context.TableTypes
                .Where(tt => tableTypeIds.Contains(tt.Id))
                .ToListAsync();
            foreach (var fee in request.TableTypeFees)
            {
                var tt = tableTypes.FirstOrDefault(t => t.Id == fee.TableTypeId);
                if (tt is not null) tt.PlatformFeeCents = fee.PlatformFeeCents;
            }
        }

        await _context.SaveChangesAsync();
        await _adminLog.LogAsync("event.platform_fees_updated", "Event", ev.Id, $"Platform fees updated by developer for event '{ev.Title}'");

        return Ok(new { message = "Platform fees updated successfully" });
    }
}
