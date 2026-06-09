using System.Security.Claims;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Auth;
using Serilog;
using Contracts.Enums;
using Db;
using Db.Repositories.StoredProcedures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}/admin/staff")]
[Authorize]
[RequireRole(UserRole.Admin)]
public class AdminStaffController(
    EventPlatformDbContext context,
    IBusinessUserProcedures businessUserProc,
    IEncryptionService encryptionService,
    IInvitationService invitationService,
    IAdminAuthService adminAuthService
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetStaff(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 25,
    [FromQuery] string? search = null)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var isDeveloper = User.IsInRole(UserRole.Developer.ToString());
        var query = context.BusinessUserViews.AsNoTracking();

        if (!isDeveloper)
            query = query.Where(a => a.Role == AdminRole.Staff);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(a =>
                a.Email.ToLower().Contains(term) ||
                a.FirstName.ToLower().Contains(term) ||
                a.LastName.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync();

        var staff = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.BusinessUserId,
                a.FirstName,
                a.LastName,
                a.Email,
                Role = a.Role.ToString(),
                a.IsActive,
                a.CreatedAt,
                a.LastLoginAt,
                a.Phone
            })
            .ToListAsync();

        return Ok(new { items = staff, totalCount, page, pageSize });
    }

    [HttpPost]
    public async Task<IActionResult> CreateStaff([FromBody] CreateBusinessUserRequest request)
    {
        var isDeveloper = User.IsInRole(UserRole.Developer.ToString());

        if (!Enum.TryParse<AdminRole>(request.Role, true, out var role))
            return BadRequest(new ApiError(400, "Invalid role", HttpContext.TraceIdentifier));

        if (!isDeveloper && role != AdminRole.Staff)
            return StatusCode(403, new ApiError(403, "Admins can only create Staff users", HttpContext.TraceIdentifier));

        var (pwValid, pwError) = Helpers.PasswordValidator.Validate(request.Password);
        if (!pwValid)
            return BadRequest(new ApiError(400, pwError!, HttpContext.TraceIdentifier));

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await businessUserProc.ExistsByEmailAsync(normalizedEmail))
            return Conflict(new ApiError(409, "A business user with this email already exists", HttpContext.TraceIdentifier));

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var emailHash = encryptionService.HashEmail(normalizedEmail);

        var id = await businessUserProc.CreateAsync(
            normalizedEmail, emailHash, request.FirstName.Trim(), request.LastName.Trim(),
            passwordHash, role.ToString());

        return Created($"/admin/staff/{id}", new { id, message = $"{role} user created" });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateStaff(Guid id, [FromBody] UpdateBusinessUserRequest request)
    {
        var isDeveloper = User.IsInRole(UserRole.Developer.ToString());
        var admin = await businessUserProc.GetByIdAsync(id);
        if (admin is null) return NotFound(new ApiError(404, "Staff user not found", HttpContext.TraceIdentifier));

        if (!isDeveloper && admin.Role == AdminRole.Developer)
            return StatusCode(403, new ApiError(403, "Admins cannot manage Developer users", HttpContext.TraceIdentifier));

        if (!isDeveloper && request.Role is not null && request.Role == "Developer")
            return StatusCode(403, new ApiError(403, "Admins cannot assign the Developer role", HttpContext.TraceIdentifier));

        var allowedRole = (isDeveloper || request.Role == "Admin" || request.Role == "Staff") ? request.Role : null;

        await businessUserProc.UpdateAsync(id,
            firstName: request.FirstName, lastName: request.LastName,
            phone: request.Phone, role: allowedRole, isActive: request.IsActive);

        if (request.IsActive == false)
            await adminAuthService.RevokeAllSessionsAsync(id, exceptSessionHash: null);

        return Ok(new { message = "Staff user updated" });
    }

    [HttpPost("invite")]
    public async Task<IActionResult> InviteStaff([FromBody] CreateInvitationRequest request)
    {
        var isDeveloper = User.IsInRole(UserRole.Developer.ToString());

        if (!Enum.TryParse<AdminRole>(request.Role, true, out var role))
            return BadRequest(new ApiError(400, "Invalid role", HttpContext.TraceIdentifier));

        if (!isDeveloper && role == AdminRole.Developer)
            return StatusCode(403, new ApiError(403, "Admins cannot invite Developer users", HttpContext.TraceIdentifier));

        try
        {
            var businessUserId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var invitation = await invitationService.CreateAsync(request.Email, role, businessUserId);
            return Created($"/admin/staff/invitations/{invitation.InvitationId}", invitation);
        }
        catch (InvalidOperationException ex)
        {
            Log.Warning(ex, "[AdminStaff] Invite conflict: {Message}", ex.Message);
            return Conflict(new ApiError(409, "Operation not allowed", HttpContext.TraceIdentifier));
        }
    }

    [HttpGet("invitations")]
    public async Task<IActionResult> GetInvitations(
    [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        var isDeveloper = User.IsInRole(UserRole.Developer.ToString());
        var businessUserId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

        var invitations = await invitationService.ListAsync(
            isDeveloper ? null : businessUserId,
            Math.Max(1, page), Math.Clamp(pageSize, 1, 100));

        return Ok(new { items = invitations });
    }

    [HttpGet]
    [Route("~/v{version:apiVersion}/admin/admins")]
    public async Task<IActionResult> GetAdmins(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 25,
    [FromQuery] string? search = null)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var query = context.BusinessUserViews.AsNoTracking()
            .Where(a => a.Role == AdminRole.Admin && a.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(a =>
                a.Email.ToLower().Contains(term) ||
                a.FirstName.ToLower().Contains(term) ||
                a.LastName.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync();

        var admins = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.BusinessUserId,
                a.FirstName,
                a.LastName,
                a.Email,
                Role = a.Role.ToString(),
                a.IsActive,
                a.CreatedAt,
                a.LastLoginAt
            })
            .ToListAsync();

        return Ok(new { items = admins, totalCount, page, pageSize });
    }

    [HttpDelete("invitations/{id:guid}")]
    public async Task<IActionResult> RevokeInvitation(Guid id)
    {
        try
        {
            var businessUserId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            await invitationService.RevokeAsync(id, businessUserId);
            return Ok(new { message = "Invitation revoked" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ApiError(404, "Invitation not found", HttpContext.TraceIdentifier));
        }
    }
}
