using Api.Middleware;
using Api.Services;
using Contracts.Enums;
using Db.Repositories.StoredProcedures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}/admin/financials")]
[Authorize]
[RequireRole(UserRole.Admin)]
public class AdminFinancialsController(
    IFinancialService financialService,
    IBusinessUserProcedures businessUserProc) : ControllerBase
{
    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions(
    [FromQuery] string? search = null,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 25,
    [FromQuery] DateTime? fromDate = null,
    [FromQuery] DateTime? toDate = null)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var user = await businessUserProc.GetByIdAsync(userId);

        if (user?.OrganizationId == null)
            return Forbid();

        var result = await financialService.GetTransactionsAsync(
            user.OrganizationId.Value, search, page, pageSize, fromDate, toDate);

        return Ok(result);
    }
}

[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}/developer/financials")]
[Authorize]
[RequireRole(UserRole.Developer)]
public class DeveloperFinancialsController(IFinancialService financialService) : ControllerBase
{
    [HttpGet("transactions")]
    public async Task<IActionResult> GetGlobalTransactions(
    [FromQuery] string? search = null,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 25,
    [FromQuery] DateTime? fromDate = null,
    [FromQuery] DateTime? toDate = null,
    [FromQuery] Guid? organizationId = null)
    {
        var result = await financialService.GetTransactionsAsync(
            organizationId, search, page, pageSize, fromDate, toDate);

        return Ok(result);
    }
}
