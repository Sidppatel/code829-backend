using Contracts.DTOs;
using Contracts.DTOs.Organizations;

namespace Api.Services;

/// <summary>
/// Application-level service for Organization CRUD + membership.
/// Wraps <see cref="Db.Repositories.StoredProcedures.IOrganizationProcedures"/>
/// and translates entity rows to DTOs so controllers don't have to.
/// </summary>
public interface IOrganizationService
{
    /// <summary>
    /// Creates a new organization. If <paramref name="initialMemberBusinessUserId"/>
    /// is set, the BusinessUser is attached to the new org as part of the same
    /// logical operation (two SP calls — same DB connection, no explicit txn
    /// because the membership SP is idempotent and a leftover org with no
    /// members is harmless).
    /// </summary>
    Task<Guid> CreateAsync(string name, string? legalName, string countryCode, Guid? initialMemberBusinessUserId);

    /// <summary>Returns the org row mapped to a DTO, or 404 from the caller if null.</summary>
    Task<OrganizationDto?> GetAsync(Guid id);

    /// <summary>
    /// Lookup helper used by both the admin "what's my org?" endpoint and the
    /// pre-PaymentIntent guard in PurchaseService. Returns null when the
    /// BusinessUser has no Organization attached.
    /// </summary>
    Task<OrganizationDto?> GetByBusinessUserIdAsync(Guid businessUserId);

    Task UpdateAsync(Guid id, OrganizationUpdateRequest req);

    Task AddMemberAsync(Guid orgId, Guid businessUserId);

    Task RemoveMemberAsync(Guid orgId, Guid businessUserId);

    Task<PagedResponse<OrganizationListItemDto>> ListAsync(string? search, int page, int pageSize, bool includeArchived = false);
}
