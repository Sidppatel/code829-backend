using Api.Middleware;
using Contracts.DTOs.Pricing;
using Contracts.Enums;
using Db;
using Db.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("admin/events/{eventId:guid}/pricing")]
[Authorize]
[RequireRole(UserRole.Admin)]
public class AdminPricingController(EventPlatformDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRules(Guid eventId)
    {
        var rules = await context.PricingRules
            .Include(r => r.TableType)
            .Where(r => r.EventId == eventId)
            .OrderBy(r => r.SortOrder)
            .Select(r => MapRule(r))
            .ToListAsync();
        return Ok(rules);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRule(Guid eventId, [FromBody] CreatePricingRuleRequest request)
    {
        var ev = await context.Events.FindAsync(eventId);
        if (ev is null) return NotFound(new { message = "Event not found" });

        if (!Enum.TryParse<PricingRuleType>(request.Type, true, out var type))
            return BadRequest(new { message = "Invalid rule type" });

        var rule = new PricingRule
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            TableTypeId = request.TableTypeId,
            Name = request.Name,
            Type = type,
            PriceCents = request.PriceCents,
            ValidFrom = request.ValidFrom,
            ValidUntil = request.ValidUntil,
            MaxCount = request.MaxCount,
            IsActive = true,
            SortOrder = request.SortOrder
        };

        context.PricingRules.Add(rule);
        await context.SaveChangesAsync();

        var created = await context.PricingRules
            .Include(r => r.TableType)
            .FirstAsync(r => r.Id == rule.Id);

        return Created("", MapRule(created));
    }

    [HttpPut("{ruleId:guid}")]
    public async Task<IActionResult> UpdateRule(Guid eventId, Guid ruleId, [FromBody] UpdatePricingRuleRequest request)
    {
        var rule = await context.PricingRules
            .Include(r => r.TableType)
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.EventId == eventId);
        if (rule is null) return NotFound(new { message = "Rule not found" });

        if (request.Name is not null) rule.Name = request.Name;
        if (request.Type is not null && Enum.TryParse<PricingRuleType>(request.Type, true, out var t)) rule.Type = t;
        if (request.PriceCents.HasValue) rule.PriceCents = request.PriceCents.Value;
        if (request.TableTypeId.HasValue) rule.TableTypeId = request.TableTypeId.Value;
        if (request.ValidFrom.HasValue) rule.ValidFrom = request.ValidFrom.Value;
        if (request.ValidUntil.HasValue) rule.ValidUntil = request.ValidUntil.Value;
        if (request.MaxCount.HasValue) rule.MaxCount = request.MaxCount.Value;
        if (request.IsActive.HasValue) rule.IsActive = request.IsActive.Value;
        if (request.SortOrder.HasValue) rule.SortOrder = request.SortOrder.Value;

        rule.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return Ok(MapRule(rule));
    }

    [HttpDelete("{ruleId:guid}")]
    public async Task<IActionResult> DeleteRule(Guid eventId, Guid ruleId)
    {
        var rule = await context.PricingRules
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.EventId == eventId);
        if (rule is null) return NotFound(new { message = "Rule not found" });

        context.PricingRules.Remove(rule);
        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("reorder")]
    public async Task<IActionResult> ReorderRules(Guid eventId, [FromBody] ReorderRulesRequest request)
    {
        var rules = await context.PricingRules
            .Where(r => r.EventId == eventId)
            .ToListAsync();

        foreach (var item in request.Rules)
        {
            var rule = rules.FirstOrDefault(r => r.Id == item.Id);
            if (rule is not null) rule.SortOrder = item.SortOrder;
        }

        await context.SaveChangesAsync();
        return Ok(new { message = "Reordered" });
    }

    /// <summary>
    /// Resolve the effective price for this event, optionally scoped to a table type.
    /// Returns the lowest applicable active rule.
    /// </summary>
    [HttpGet("resolve")]
    public async Task<IActionResult> ResolvePrice(Guid eventId, [FromQuery] Guid? tableTypeId = null)
    {
        var now = DateTime.UtcNow;
        var rules = await context.PricingRules
            .Where(r => r.EventId == eventId && r.IsActive)
            .Where(r => r.TableTypeId == null || r.TableTypeId == tableTypeId)
            .ToListAsync();

        var applicable = rules.Where(r => r.Type switch
        {
            PricingRuleType.Standard => true,
            PricingRuleType.EarlyBird => (r.ValidFrom == null || r.ValidFrom <= now) && (r.ValidUntil == null || r.ValidUntil > now),
            PricingRuleType.FirstN => r.MaxCount == null || r.UsedCount < r.MaxCount,
            _ => false
        }).ToList();

        if (applicable.Count == 0)
            return Ok(new { priceCents = 0, ruleName = "No active rules", ruleType = "None", ruleId = (Guid?)null });

        var best = applicable.OrderBy(r => r.PriceCents ?? 0).First();
        return Ok(new ResolvedPriceResponse(best.PriceCents ?? 0, best.Name ?? "", (best.Type ?? Contracts.Enums.PricingRuleType.Standard).ToString(), best.Id));
    }

    private static PricingRuleResponse MapRule(PricingRule r) => new(
        r.Id, r.EventId, r.TableTypeId, r.TableType?.Name,
        r.Name ?? "", (r.Type ?? Contracts.Enums.PricingRuleType.Standard).ToString(), r.PriceCents ?? 0,
        r.ValidFrom, r.ValidUntil, r.MaxCount, r.UsedCount,
        r.IsActive, r.SortOrder, r.CreatedAt);
}
