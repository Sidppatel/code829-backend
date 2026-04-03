using Api.Middleware;
using Api.Services;
using Contracts.Enums;
using Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Developer event routes inherit all admin event actions via the /developer/events prefix.
/// </summary>
[ApiController]
[Route("developer/events")]
[Authorize]
[RequireRole(UserRole.Developer)]
public class DeveloperEventsController(
    EventPlatformDbContext context,
    IFileStorageService fileStorage,
    IAdminLogService adminLog
) : AdminEventsController(context, fileStorage, adminLog);
