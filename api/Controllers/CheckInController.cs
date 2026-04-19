using Contracts.DTOs;
using Api.Middleware;
using Contracts.DTOs.CheckIn;
using Contracts.Enums;
using Db;
using Db.Repositories.StoredProcedures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Controllers;

[ApiController]
[Route("checkin")]
[Authorize]
[RequireRole(UserRole.Staff)]
public class CheckInController(
    EventPlatformDbContext context,
    ICheckInProcedures checkInProc
) : ControllerBase
{
    [HttpPost("scan")]
    public async Task<IActionResult> Scan([FromBody] ScanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.QrToken))
            return BadRequest(new ScanResponse(false, "QR token is required", null, null, null, null, null));

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
