namespace Contracts.DTOs.Pricing;

public record PricingRuleResponse(
    Guid Id,
    Guid EventId,
    Guid? TableTypeId,
    string? TableTypeName,
    string Name,
    string Type,
    int PriceCents,
    DateTime? ValidFrom,
    DateTime? ValidUntil,
    int? MaxCount,
    int UsedCount,
    bool IsActive,
    int SortOrder,
    DateTime CreatedAt
);

public record CreatePricingRuleRequest(
    string Name,
    string Type,
    int PriceCents,
    Guid? TableTypeId = null,
    DateTime? ValidFrom = null,
    DateTime? ValidUntil = null,
    int? MaxCount = null,
    int SortOrder = 0
);

public record UpdatePricingRuleRequest(
    string? Name = null,
    string? Type = null,
    int? PriceCents = null,
    Guid? TableTypeId = null,
    DateTime? ValidFrom = null,
    DateTime? ValidUntil = null,
    int? MaxCount = null,
    bool? IsActive = null,
    int? SortOrder = null
);

public record ReorderRulesRequest(
    List<ReorderItem> Rules
);

public record ReorderItem(Guid Id, int SortOrder);

public record ResolvedPriceResponse(
    int PriceCents,
    string RuleName,
    string RuleType,
    Guid RuleId
);
