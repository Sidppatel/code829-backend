using Api.Middleware;
using Api.Services;
using Contracts.Enums;
using Db;
using Db.Repositories.StoredProcedures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace Api.Controllers;

/// <summary>
/// Developer purchases routes inherit all admin purchase actions.
/// Developers access purchases via role hierarchy (Developer > Admin).
/// </summary>
[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}/developer/purchases")]
[Authorize]
[RequireRole(UserRole.Developer)]
public class DeveloperPurchasesController(
    EventPlatformDbContext context,
    IPurchaseService purchaseService,
    IOrganizationProcedures organizationProc,
    IDashboardProcedures dashboardProc,
    IConnectionMultiplexer redis
) : AdminPurchasesController(context, purchaseService, organizationProc, dashboardProc, redis);
