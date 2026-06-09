using System.Security.Claims;
using Api.Services;
using Contracts.DTOs;
using Api.Middleware;
using Contracts.DTOs.Admin;
using Contracts.DTOs.CheckIn;
using Contracts.Enums;
using Db;
using Db.Repositories.StoredProcedures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Controllers;

[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}/checkin")]
[Authorize]
[RequireRole(UserRole.Staff)]
public class CheckInController(
    EventPlatformDbContext context,
    ICheckInProcedures checkInProc,
    IBusinessUserEventProcedures businessUserEventProc,
    IFileStorageService fileStorage
) : ControllerBase
{
    private Guid GetCurrentAdminId() =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("events")]
    public async Task<IActionResult> GetAccessibleEvents()
    {
        var adminId = GetCurrentAdminId();
        var events = await businessUserEventProc.ListEventsForStaffAsync(adminId);

        var dtos = events.Select(e => new StaffEventDto(
            e.Id, e.Title, e.Slug, e.StartDate, e.EndDate, e.Status.ToString(),
            e.ImagePath is not null ? fileStorage.GetPublicUrl(e.ImagePath) : null
        )).ToList();

        return Ok(new { items = dtos });
    }

    [HttpPost("scan")]
    public async Task<IActionResult> Scan([FromBody] ScanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.QrToken))
            return BadRequest(new ScanResponse(false, "QR token is required", null, null, null, null, null));

        if (request.EventId is null)
            return BadRequest(new ApiError(400, "EventId is required", HttpContext.TraceIdentifier));

        var adminId = GetCurrentAdminId();
        var isAdminOrDev = User.IsInRole(UserRole.Admin.ToString()) || User.IsInRole(UserRole.Developer.ToString());
        var canAccess = isAdminOrDev || await businessUserEventProc.CanAccessEventAsync(adminId, request.EventId.Value);
        if (!canAccess)
            return StatusCode(403, new ApiError(403,
                "You are not assigned to this event or access has expired",
                HttpContext.TraceIdentifier));

        var ticketResult = await checkInProc.ScanTicketAsync(request.QrToken);
        if (ticketResult is not null)
            return MapResult(ticketResult);

        var purchaseResult = await checkInProc.ScanPurchaseAsync(request.QrToken);
        if (purchaseResult is null)
        {
            Log.Warning("[CheckIn] Invalid QR token scanned: {Token}", request.QrToken[..Math.Min(10, request.QrToken.Length)]);
            return NotFound(new ScanResponse(false, "Invalid QR code — purchase not found", null, null, null, null, null));
        }

        return MapResult(purchaseResult);
    }

    private IActionResult MapResult(CheckInScanResult result)
    {
        var response = new ScanResponse(
            result.Success, result.Message,
            result.PurchaseNumber, result.GuestName, result.EventTitle,
            result.StatusStr, result.CheckedInAt
        );

        if (result.Success)
        {
            Log.Information("[CheckIn] {PurchaseNumber} checked in for {Event}",
                result.PurchaseNumber, result.EventTitle);
            return Ok(response);
        }

        if (result.StatusStr == "CheckedIn")
        {
            Log.Warning("[CheckIn] Double scan for {PurchaseNumber}", result.PurchaseNumber);
            return Conflict(response);
        }

        return BadRequest(response);
    }

    [HttpGet("events/{eventId:guid}/stats")]
    public async Task<IActionResult> GetStats(Guid eventId)
    {
        var adminId = GetCurrentAdminId();
        var isAdminOrDev = User.IsInRole(UserRole.Admin.ToString()) || User.IsInRole(UserRole.Developer.ToString());
        var canAccess = isAdminOrDev || await businessUserEventProc.CanAccessEventAsync(adminId, eventId);
        if (!canAccess)
            return StatusCode(403, new ApiError(403,
                "You are not assigned to this event or access has expired",
                HttpContext.TraceIdentifier));

        var ev = await context.EventViews.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));

        var purchases = await context.PurchaseViews.AsNoTracking()
            .Where(b => b.EventId == eventId)
            .ToListAsync();

        var paidOrCheckedIn = purchases.Where(b => b.Status is "Paid" or "CheckedIn").ToList();
        var checkedIn = paidOrCheckedIn.Where(b => b.Status == "CheckedIn").Sum(b => b.SeatsReserved ?? 1);
        var remaining = paidOrCheckedIn.Where(b => b.Status == "Paid").Sum(b => b.SeatsReserved ?? 1);
        var totalSold = checkedIn + remaining;
        var pending = purchases.Where(b => b.Status == "Pending").Sum(b => b.SeatsReserved ?? 1);

        var lastCheckIn = await checkInProc.GetEventLastCheckinAsync(eventId);

        var percentage = totalSold > 0 ? Math.Round(checkedIn * 100.0 / totalSold, 1) : 0;

        return Ok(new CheckInStatsDto(
            eventId, ev.Title, totalSold, checkedIn, pending, remaining,
            percentage, lastCheckIn
        ));
    }
}
