using System.Security.Claims;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Auth;
using Contracts.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Api.Controllers;

[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}/developer/invitations")]
[Authorize]
[RequireRole(UserRole.Developer)]
public class DeveloperInvitationsController(
    IInvitationService invitationService
) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateInvitation([FromBody] CreateInvitationRequest request)
    {
        if (!Enum.TryParse<AdminRole>(request.Role, true, out var role))
            return BadRequest(new ApiError(400, "Invalid role. Must be 'Admin' or 'Staff'", HttpContext.TraceIdentifier));

        try
        {
            var adminUserId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var invitation = await invitationService.CreateAsync(request.Email, role, adminUserId);
            return Created($"/developer/invitations/{invitation.InvitationId}", invitation);
        }
        catch (InvalidOperationException ex)
        {
            Log.Warning(ex, "[DeveloperInvitations] Create conflict: {Message}", ex.Message);
            return Conflict(new ApiError(409, "Operation not allowed", HttpContext.TraceIdentifier));
        }
    }

    [HttpGet]
    public async Task<IActionResult> ListInvitations(
    [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        var invitations = await invitationService.ListAsync(
            null, Math.Max(1, page), Math.Clamp(pageSize, 1, 100));

        return Ok(new { items = invitations });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> RevokeInvitation(Guid id)
    {
        try
        {
            var adminUserId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await invitationService.RevokeAsync(id, adminUserId);
            return Ok(new { message = "Invitation revoked" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ApiError(404, "Invitation not found", HttpContext.TraceIdentifier));
        }
    }
}
