using System.Security.Claims;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Auth;
using Contracts.Enums;
using Db;
using Db.Repositories.StoredProcedures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("admin/staff")]
[Authorize]
[RequireRole(UserRole.Admin)]
public class AdminStaffController(
    EventPlatformDbContext context,
    IAdminUserProcedures adminUserProc,
    IEncryptionService encryptionService,
    IInvitationService invitationService,
    IAdminAuthService adminAuthService
) : ControllerBase
{
    /// <summary>
    /// List staff users. Admins can only see Staff; Developers see all.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetStaff(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var isDeveloper = User.IsInRole(UserRole.Developer.ToString());
        var query = context.AdminUserViews.AsNoTracking();

        if (!isDeveloper)
            query = query.Where(a => a.Role == AdminRole.Staff && a.IsActive);

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
                a.AdminUserId, a.FirstName, a.LastName, a.Email,
                Role = a.Role.ToString(),
                a.IsActive, a.CreatedAt, a.LastLoginAt, a.Phone
            })
            .ToListAsync();

        return Ok(new { items = staff, totalCount, page, pageSize });
    }

    /// <summary>
    /// Admin creates a Staff user.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateStaff([FromBody] CreateAdminUserRequest request)
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

        if (await adminUserProc.ExistsByEmailAsync(normalizedEmail))
            return Conflict(new ApiError(409, "An admin user with this email already exists", HttpContext.TraceIdentifier));

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var emailHash = encryptionService.HashEmail(normalizedEmail);

        var id = await adminUserProc.CreateAsync(
            normalizedEmail, emailHash, request.FirstName.Trim(), request.LastName.Trim(),
            passwordHash, role.ToString());

        return Created($"/admin/staff/{id}", new { id, message = $"{role} user created" });
    }

    /// <summary>
    /// Admin updates a Staff user (limited: no role promotion beyond Staff for non-Developers).
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateStaff(Guid id, [FromBody] UpdateAdminUserRequest request)
    {
        var isDeveloper = User.IsInRole(UserRole.Developer.ToString());
        var admin = await adminUserProc.GetByIdAsync(id);
        if (admin is null) return NotFound(new ApiError(404, "Staff user not found", HttpContext.TraceIdentifier));

        if (!isDeveloper && admin.Role != AdminRole.Staff)
            return StatusCode(403, new ApiError(403, "Admins can only manage Staff users", HttpContext.TraceIdentifier));

        if (!isDeveloper && request.Role is not null && request.Role != "Staff")
            return StatusCode(403, new ApiError(403, "Admins cannot promote Staff to a higher role", HttpContext.TraceIdentifier));

        await adminUserProc.UpdateAsync(id,
            firstName: request.FirstName, lastName: request.LastName,
            phone: request.Phone, role: isDeveloper ? request.Role : null, isActive: request.IsActive);

        // When disabling an account, revoke all their sessions to force immediate logout
        if (request.IsActive == false)
            await adminAuthService.RevokeAllSessionsAsync(id, exceptSessionHash: null);

        return Ok(new { message = "Staff user updated" });
    }

    /// <summary>
    /// Admin invites a Staff user via email. Developers can also invite Admin users.
    /// </summary>
    [HttpPost("invite")]
    public async Task<IActionResult> InviteStaff([FromBody] CreateInvitationRequest request)
    {
        var isDeveloper = User.IsInRole(UserRole.Developer.ToString());

        if (!Enum.TryParse<AdminRole>(request.Role, true, out var role))
            return BadRequest(new ApiError(400, "Invalid role", HttpContext.TraceIdentifier));

        if (!isDeveloper && role != AdminRole.Staff)
            return StatusCode(403, new ApiError(403, "Admins can only invite Staff users", HttpContext.TraceIdentifier));

        try
        {
            var adminUserId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var invitation = await invitationService.CreateAsync(request.Email, role, adminUserId);
            return Created($"/admin/staff/invitations/{invitation.InvitationId}", invitation);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiError(409, ex.Message, HttpContext.TraceIdentifier));
        }
    }

    /// <summary>
    /// List invitations sent by the current admin user.
    /// </summary>
    [HttpGet("invitations")]
    public async Task<IActionResult> GetInvitations(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        var isDeveloper = User.IsInRole(UserRole.Developer.ToString());
        var adminUserId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

        // Developers see all invitations, admins see only theirs
        var invitations = await invitationService.ListAsync(
            isDeveloper ? null : adminUserId,
            Math.Max(1, page), Math.Clamp(pageSize, 1, 100));

        return Ok(new { items = invitations });
    }

    /// <summary>
    /// Revoke a pending invitation.
    /// </summary>
    [HttpDelete("invitations/{id:guid}")]
    public async Task<IActionResult> RevokeInvitation(Guid id)
    {
        try
        {
            var adminUserId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            await invitationService.RevokeAsync(id, adminUserId);
            return Ok(new { message = "Invitation revoked" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ApiError(404, "Invitation not found", HttpContext.TraceIdentifier));
        }
    }
}
