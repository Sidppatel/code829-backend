using Api.Middleware;
using Api.Services;
using Contracts.Enums;
using Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace Api.Controllers;

/// <summary>
/// Developer bookings routes inherit all admin booking actions.
/// Developers access bookings via role hierarchy (Developer > Admin).
/// </summary>
[ApiController]
[IgnoreAntiforgeryToken]
[Route("developer/bookings")]
[Authorize]
[RequireRole(UserRole.Developer)]
public class DeveloperBookingsController(
    EventPlatformDbContext context,
    IBookingService bookingService,
    IConnectionMultiplexer redis
) : AdminBookingsController(context, bookingService, redis);
