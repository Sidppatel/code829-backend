using System.Security.Claims;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Performers;
using Contracts.Enums;
using Db;
using Db.Repositories.StoredProcedures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}/admin/events/{eventId:guid}/performers")]
[Authorize]
[RequireRole(UserRole.Admin)]
public class AdminEventPerformersController(
    EventPlatformDbContext context,
    IPerformerService performerService,
    IOrganizationProcedures organizationProc
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid eventId, CancellationToken ct = default)
    {
        if (!await CanAccessEventAsync(eventId, ct)) return Forbid();
        var list = await performerService.GetEventPerformersAsync(eventId, includePrivateMeta: true, ct);
        return Ok(list);
    }

    [HttpPut]
    public async Task<IActionResult> Set(Guid eventId, [FromBody] SetEventPerformersRequest request, CancellationToken ct = default)
    {
        if (!await CanAccessEventAsync(eventId, ct)) return Forbid();
        if (request is null) return BadRequest(new ApiError(400, "Body required", HttpContext.TraceIdentifier));

        await performerService.SetEventPerformersAsync(eventId, request, ct);
        var list = await performerService.GetEventPerformersAsync(eventId, includePrivateMeta: true, ct);
        return Ok(list);
    }

    private async Task<bool> CanAccessEventAsync(Guid eventId, CancellationToken ct)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId, ct);
        if (ev is null) return false;
        if (User.IsInRole(UserRole.Developer.ToString())) return true;
        var currentUserId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        if (ev.BusinessUserId == currentUserId) return true;
        var callerOrg = await organizationProc.GetByBusinessUserAsync(currentUserId);
        if (callerOrg is null) return false;
        var ownerOrg = await organizationProc.GetByBusinessUserAsync(ev.BusinessUserId);
        return ownerOrg is not null && ownerOrg.Id == callerOrg.Id;
    }
}
