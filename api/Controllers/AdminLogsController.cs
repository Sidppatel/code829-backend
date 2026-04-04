using Api.Middleware;
using Contracts.DTOs;
using Contracts.DTOs.Logs;
using Contracts.Enums;
using Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("admin/logs")]
[Authorize]
[RequireRole(UserRole.Admin)]
public class AdminLogsController(EventPlatformDbContext context) : ControllerBase
{
    /// <summary>
    /// Admin logs: business operations and user activity. Timeline-style.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAdminLogs(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null,
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = context.AdminLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(l => l.Action.Contains(action));
        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(l => l.EntityType == entityType);
        if (from.HasValue)
            query = query.Where(l => l.Timestamp >= from.Value);
        if (to.HasValue)
            query = query.Where(l => l.Timestamp <= to.Value);

        var totalCount = await query.CountAsync();
        var items = await query.OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(l => new AdminLogDto(
                l.Id, l.Timestamp, l.Action, l.ActorId, l.ActorEmail, l.ActorRole,
                l.EntityType, l.EntityId, l.Description, l.MetadataJson, l.IpAddress))
            .ToListAsync();

        return Ok(new PagedResponse<AdminLogDto>(items, totalCount, page, pageSize));
    }
}
