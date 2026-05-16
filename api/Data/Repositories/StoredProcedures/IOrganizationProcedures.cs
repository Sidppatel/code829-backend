using Db.Entities;

namespace Db.Repositories.StoredProcedures;

public interface IOrganizationProcedures
{
    Task<Guid> CreateAsync(string name, string? legalName = null, string countryCode = "US",
        CancellationToken ct = default);

    Task UpdateAsync(Guid id, string? name = null, string? legalName = null,
        string? countryCode = null, CancellationToken ct = default);

    Task UpdateStripeAccountAsync(Guid id, string stripeAccountId, CancellationToken ct = default);

    Task UpdateStripeStatusAsync(string stripeAccountId, bool chargesEnabled, bool payoutsEnabled,
        bool detailsSubmitted, string? requirementsDueJson = null, CancellationToken ct = default);

    Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Organization?> GetByBusinessUserAsync(Guid businessUserId, CancellationToken ct = default);

    Task AddBusinessUserAsync(Guid businessUserId, Guid organizationId, CancellationToken ct = default);

    Task RemoveBusinessUserAsync(Guid businessUserId, CancellationToken ct = default);

    Task ArchiveAsync(Guid id, CancellationToken ct = default);

    Task<List<OrganizationListRow>> ListAsync(string? search, bool includeArchived,
    int offset, int limit, CancellationToken ct = default);

    Task<int> CountAsync(string? search, bool includeArchived, CancellationToken ct = default);

    Task<List<OrganizationMemberRow>> GetMembersAsync(Guid organizationId, CancellationToken ct = default);

    Task<int> ClearStripeAccountAsync(Guid organizationId, CancellationToken ct = default);
}

public record OrganizationListRow(
    Guid Id,
    string Name,
    string? LegalName,
    string CountryCode,
    string? StripeConnectedAccountId,
    bool StripeChargesEnabled,
    bool StripePayoutsEnabled,
    bool StripeDetailsSubmitted,
    DateTime? StripeOnboardedAt,
    string? StripeRequirementsDue,
    DateTime? ArchivedAt,
    int MemberCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record OrganizationMemberRow(
    Guid BusinessUserId,
    string Email,
    string? DisplayName
);
