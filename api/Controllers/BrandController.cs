using Api.Services;
using Contracts.DTOs.Brand;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("brand")]
public class BrandController(ISettingsService settings) : ControllerBase
{
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig()
    {
        var response = new BrandConfigResponse(
            Name: await settings.GetOrDefaultAsync("brand_name", "Code829") ?? "Code829",
            Tagline: await settings.GetOrDefaultAsync("brand_tagline", "Where Great Events Come to Life") ?? "Where Great Events Come to Life",
            PrimaryColor: await settings.GetOrDefaultAsync("brand_primary_color", "#4f46e5") ?? "#4f46e5",
            AccentColor: await settings.GetOrDefaultAsync("brand_accent_color", "#f97316") ?? "#f97316",
            DefaultTheme: await settings.GetOrDefaultAsync("default_theme", "system") ?? "system",
            DefaultCity: await settings.GetOrDefaultAsync("default_city", "Mobile") ?? "Mobile",
            DefaultState: await settings.GetOrDefaultAsync("default_state", "AL") ?? "AL",
            DefaultTimezone: await settings.GetOrDefaultAsync("default_timezone", "America/Chicago") ?? "America/Chicago"
        );

        return Ok(response);
    }
}
