using System.Security.Claims;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Sponsors;
using Contracts.Enums;
using Db;
using Db.Repositories.StoredProcedures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}/admin/events/{eventId:guid}/sponsors")]
[Authorize]
[RequireRole(UserRole.Admin)]
public class AdminEventSponsorsController(
    EventPlatformDbContext context,
    ISponsorService sponsorService,
    IOrganizationProcedures organizationProc
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid eventId, CancellationToken ct = default)
    {
        if (!await CanAccessEventAsync(eventId, ct)) return Forbid();
        var list = await sponsorService.GetEventSponsorsAsync(eventId, includePrivateMeta: true, ct);
        return Ok(list);
    }

    [HttpPut]
    public async Task<IActionResult> Set(Guid eventId, [FromBody] SetEventSponsorsRequest request, CancellationToken ct = default)
    {
        if (!await CanAccessEventAsync(eventId, ct)) return Forbid();
        if (request is null) return BadRequest(new ApiError(400, "Body required", HttpContext.TraceIdentifier));

        await sponsorService.SetEventSponsorsAsync(eventId, request, ct);
        var list = await sponsorService.GetEventSponsorsAsync(eventId, includePrivateMeta: true, ct);
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
