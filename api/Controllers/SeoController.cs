using System.Text;
using Api.Services;
using Contracts.Enums;
using Db;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[IgnoreAntiforgeryToken]
[Route("")]
public class SeoController(
    EventPlatformDbContext context,
    ISettingsService settings
) : ControllerBase
{
    /// <summary>
    /// Dynamic sitemap.xml listing all published events.
    /// </summary>
    [HttpGet("sitemap.xml")]
    [Produces("application/xml")]
    public async Task<IActionResult> Sitemap()
    {
        var frontendUrl = await settings.GetOrDefaultAsync("frontend_url", "http://localhost:5173");

        var events = await context.Events
            .Where(e => e.Status == EventStatus.Published)
            .Select(e => new { e.Slug, e.UpdatedAt })
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        // Home page
        sb.AppendLine("  <url>");
        sb.AppendLine($"    <loc>{frontendUrl}/</loc>");
        sb.AppendLine("    <changefreq>daily</changefreq>");
        sb.AppendLine("    <priority>1.0</priority>");
        sb.AppendLine("  </url>");

        // Events listing
        sb.AppendLine("  <url>");
        sb.AppendLine($"    <loc>{frontendUrl}/events</loc>");
        sb.AppendLine("    <changefreq>daily</changefreq>");
        sb.AppendLine("    <priority>0.9</priority>");
        sb.AppendLine("  </url>");

        // Individual events
        foreach (var ev in events)
        {
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{frontendUrl}/events/{ev.Slug}</loc>");
            sb.AppendLine($"    <lastmod>{ev.UpdatedAt:yyyy-MM-dd}</lastmod>");
            sb.AppendLine("    <changefreq>weekly</changefreq>");
            sb.AppendLine("    <priority>0.8</priority>");
            sb.AppendLine("  </url>");
        }

        sb.AppendLine("</urlset>");
        return Content(sb.ToString(), "application/xml");
    }

    /// <summary>
    /// robots.txt allowing all crawlers, pointing to sitemap.
    /// </summary>
    [HttpGet("robots.txt")]
    [Produces("text/plain")]
    public async Task<IActionResult> Robots()
    {
        var frontendUrl = await settings.GetOrDefaultAsync("frontend_url", "http://localhost:5173");
        var content = $"""
            User-agent: *
            Allow: /

            Sitemap: {frontendUrl}/sitemap.xml
            """;
        return Content(content, "text/plain");
    }
}
