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
[Route("developer")]
[Authorize]
[RequireRole(UserRole.Developer)]
public class DeveloperController(EventPlatformDbContext context) : ControllerBase
{
    /// <summary>
    /// Get paginated email logs. Developer role only.
    /// </summary>
    [HttpGet("email-log")]
    public async Task<IActionResult> GetEmailLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? recipient = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = context.EmailLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(recipient))
            query = query.Where(e => e.Recipient.Contains(recipient));

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(e => e.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EmailLogDto(
                e.Id,
                e.Recipient,
                e.Subject,
                e.Body,
                e.Status,
                e.Timestamp
            ))
            .ToListAsync();

        return Ok(new PagedResponse<EmailLogDto>(items, totalCount, page, pageSize));
    }
}
