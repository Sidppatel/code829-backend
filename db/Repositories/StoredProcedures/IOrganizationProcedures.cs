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

    /// <summary>
    /// Paginated, searchable list for the developer dashboard. Returns rows
    /// projected by sp_list_organizations including a server-side MemberCount.
    /// </summary>
    Task<List<OrganizationListRow>> ListAsync(string? search, bool includeArchived,
        int offset, int limit, CancellationToken ct = default);

    /// <summary>Total row count under the same filters as <see cref="ListAsync"/>.</summary>
    Task<int> CountAsync(string? search, bool includeArchived, CancellationToken ct = default);
}

/// <summary>
/// Flat projection of one organization row + member count, returned by
/// sp_list_organizations. Mapped via SqlQueryRaw rather than entity tracking
/// because the MemberCount column has no Organization entity counterpart.
/// </summary>
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
