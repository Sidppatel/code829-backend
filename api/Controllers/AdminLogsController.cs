using Api.Middleware;
using Contracts.DTOs;
using Contracts.DTOs.Logs;
using Contracts.Enums;
using Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}/admin/logs")]
[Authorize]
[RequireRole(UserRole.Admin)]
public class AdminLogsController(EventPlatformDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAdminLogs(
    [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
    [FromQuery] string? action = null,
    [FromQuery] string? entityType = null,
    [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var actionParam = (object?)action ?? DBNull.Value;
        var entityTypeParam = (object?)entityType ?? DBNull.Value;
        var fromParam = (object?)from ?? DBNull.Value;
        var toParam = (object?)to ?? DBNull.Value;

        var totalCount = await context.Database
            .SqlQueryRaw<int>("SELECT sp_count_admin_logs({0}, {1}, {2}, {3}) AS \"Value\"",
                actionParam, entityTypeParam, fromParam, toParam)
            .FirstAsync();

        var items = await context.BusinessLogViews
            .FromSqlRaw("SELECT * FROM sp_get_admin_logs({0}, {1}, {2}, {3}, {4}, {5})",
                actionParam, entityTypeParam, fromParam, toParam, (page - 1) * pageSize, pageSize)
            .AsNoTracking()
            .Select(l => new AdminLogDto(
                l.Id, l.Timestamp, l.Action, l.BusinessUserId, l.BusinessUserEmail, l.BusinessUserRole,
                l.EntityType, l.EntityId, l.Description, l.MetadataJson, l.IpAddress))
            .ToListAsync();

        return Ok(new PagedResponse<AdminLogDto>(items, totalCount, page, pageSize));
    }
}
