using System.Security.Claims;
using Contracts.DTOs;
using Db;
using Db.Repositories.StoredProcedures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Api.Middleware;
using Contracts.Enums;

namespace Api.Controllers;

[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}/feedback")]
public class FeedbackController(
    EventPlatformDbContext context,
    IFeedbackProcedures feedbackProc
) : ControllerBase
{
    private static readonly string[] ValidTypes = ["General", "Bug", "Suggestion", "Compliment", "Complaint"];

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Submit([FromBody] SubmitFeedbackRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new ApiError(400, "Name is required", HttpContext.TraceIdentifier));
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Trim().Length < 10)
            return BadRequest(new ApiError(400, "Message must be at least 10 characters", HttpContext.TraceIdentifier));
        if (!ValidTypes.Contains(request.Type))
            return BadRequest(new ApiError(400, $"Type must be one of: {string.Join(", ", ValidTypes)}", HttpContext.TraceIdentifier));
        if (request.Rating < 0 || request.Rating > 5)
            return BadRequest(new ApiError(400, "Rating must be between 0 and 5", HttpContext.TraceIdentifier));

        Guid? userId = null;
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim is not null && Guid.TryParse(claim.Value, out var uid))
            userId = uid;

        var diagDict = new System.Collections.Generic.Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(request.PageUrl))
            diagDict["pageUrl"] = request.PageUrl.Trim();
        if (!string.IsNullOrWhiteSpace(request.StepsToReproduce))
            diagDict["stepsToReproduce"] = request.StepsToReproduce.Trim();
        if (!string.IsNullOrWhiteSpace(request.Diagnostics))
            diagDict["client"] = System.Text.Json.JsonSerializer.Deserialize<object>(request.Diagnostics);

        var diagnostics = diagDict.Count > 0
            ? System.Text.Json.JsonSerializer.Serialize(diagDict)
            : null;

        if (diagnostics is not null && diagnostics.Length > 16_384)
            diagnostics = diagnostics[..16_384];

        await feedbackProc.CreateFeedbackAsync(
            request.Name.Trim(), request.Email?.Trim() ?? "", request.Type,
            request.Message.Trim(), request.Rating, userId,
            Request.Headers.UserAgent.ToString(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            diagnostics);

        Log.Information("[Feedback] New {Type} feedback from {Name} (rating={Rating})", request.Type, request.Name, request.Rating);

        return Ok(new { message = "Thank you for your feedback!" });
    }

    [HttpGet]
    [Authorize]
    [RequireRole(UserRole.Admin)]
    public async Task<IActionResult> List(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? type = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = context.FeedbackViews.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(f => f.Type == type);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new FeedbackDto(
                f.FeedbackId, f.Name, f.Email, f.Type, f.Message, f.Rating,
                f.UserId, f.UserFullName,
                f.CreatedAt, f.Diagnostics
            ))
            .ToListAsync();

        Response.Headers["X-Feedback-Storage-Format"] = "raw-json";
        return Ok(new PagedResponse<FeedbackDto>(items, total, page, pageSize));
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    [RequireRole(UserRole.Admin)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await feedbackProc.DeleteFeedbackAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
