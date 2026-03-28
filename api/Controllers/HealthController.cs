using Contracts.DTOs.Health;
using Db;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace Api.Controllers;

[ApiController]
[Route("")]
public class HealthController(
    EventPlatformDbContext dbContext,
    IConnectionMultiplexer redis
) : ControllerBase
{
    [HttpGet("health")]
    public async Task<IActionResult> GetHealth()
    {
        var services = new Dictionary<string, string>();

        try
        {
            await dbContext.Database.CanConnectAsync();
            services["postgres"] = "healthy";
        }
        catch
        {
            services["postgres"] = "unhealthy";
        }

        try
        {
            var db = redis.GetDatabase();
            await db.PingAsync();
            services["redis"] = "healthy";
        }
        catch
        {
            services["redis"] = "unhealthy";
        }

        var allHealthy = services.Values.All(v => v == "healthy");
        var response = new HealthResponse(
            Status: allHealthy ? "healthy" : "degraded",
            Version: "1.0.0",
            Timestamp: DateTime.UtcNow,
            Services: services
        );

        return allHealthy ? Ok(response) : StatusCode(503, response);
    }
}
