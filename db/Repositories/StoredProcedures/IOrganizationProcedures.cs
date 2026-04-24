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
}
