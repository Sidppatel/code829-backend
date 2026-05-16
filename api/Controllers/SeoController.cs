using System.Text;
using Api.Services;
using Db;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("")]
public class SeoController(
    EventPlatformDbContext context,
    ISettingsService settings
) : ControllerBase
{
    [HttpGet("sitemap.xml")]
    [Produces("application/xml")]
    public async Task<IActionResult> Sitemap()
    {
        var frontendUrl = await settings.GetOrDefaultAsync("frontend_url", "http://localhost:5173");

        var events = await context.EventViews.AsNoTracking()
            .Where(e => e.Status == "Published")
            .Select(e => new { e.Slug, e.UpdatedAt })
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        sb.AppendLine("  <url>");
        sb.AppendLine($"    <loc>{frontendUrl}/</loc>");
        sb.AppendLine("    <changefreq>daily</changefreq>");
        sb.AppendLine("    <priority>1.0</priority>");
        sb.AppendLine("  </url>");

        sb.AppendLine("  <url>");
        sb.AppendLine($"    <loc>{frontendUrl}/events</loc>");
        sb.AppendLine("    <changefreq>daily</changefreq>");
        sb.AppendLine("    <priority>0.9</priority>");
        sb.AppendLine("  </url>");

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
