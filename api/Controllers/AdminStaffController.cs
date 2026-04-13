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
    IEncryptionService encryptionService
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
        var query = context.AdminUsers.AsQueryable();

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
                a.Id, a.FirstName, a.LastName, a.Email,
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

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return BadRequest(new ApiError(400, "Password must be at least 8 characters", HttpContext.TraceIdentifier));

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await context.AdminUsers.AnyAsync(a => a.Email == normalizedEmail))
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
        var admin = await context.AdminUsers.FindAsync(id);
        if (admin is null) return NotFound(new ApiError(404, "Staff user not found", HttpContext.TraceIdentifier));

        if (!isDeveloper && admin.Role != AdminRole.Staff)
            return StatusCode(403, new ApiError(403, "Admins can only manage Staff users", HttpContext.TraceIdentifier));

        if (!isDeveloper && request.Role is not null && request.Role != "Staff")
            return StatusCode(403, new ApiError(403, "Admins cannot promote Staff to a higher role", HttpContext.TraceIdentifier));

        await adminUserProc.UpdateAsync(id,
            firstName: request.FirstName, lastName: request.LastName,
            phone: request.Phone, role: isDeveloper ? request.Role : null, isActive: request.IsActive);

        return Ok(new { message = "Staff user updated" });
    }
}
