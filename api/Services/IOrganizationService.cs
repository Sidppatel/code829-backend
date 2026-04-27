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
    /// Returns the org row plus its BusinessUser roster as a single payload,
    /// or null when the org id is unknown. Used by the developer detail and
    /// add/remove-member endpoints so the FE sees the live roster on the same
    /// response as the mutation.
    /// </summary>
    Task<OrganizationDetailDto?> GetDetailAsync(Guid id);

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

    /// <summary>
    /// Generates a fresh identity-scope Stripe onboarding link for the
    /// organization and emails it.
    ///
    /// <para>One of <paramref name="businessUserId"/> or <paramref name="recipientEmail"/>
    /// must be supplied. <paramref name="recipientEmail"/> wins when both are
    /// present so the developer can override the BU's recorded address (useful
    /// when bootstrapping an org for an organizer who doesn't have a platform
    /// account yet).</para>
    ///
    /// Throws <see cref="KeyNotFoundException"/> when the organization or the
    /// referenced BusinessUser doesn't exist; throws
    /// <see cref="InvalidOperationException"/> when the BusinessUser belongs to
    /// a different organization, when neither id nor email is provided, or when
    /// the Organization has no Stripe account yet (the developer must call
    /// POST /stripe-account before this endpoint can fire).
    /// </summary>
    Task<StripeOnboardingEmailResponse> SendOnboardingLinkEmailAsync(
        Guid organizationId,
        Guid? businessUserId = null,
        string? recipientEmail = null);

    /// <summary>
    /// Clean-restart hook: deletes the organization's connected account at
    /// Stripe (via <see cref="IStripeConnectService.DeleteAccountAsync"/>) and
    /// then nulls every Stripe-related column on the org row so a fresh
    /// onboarding can begin from zero. Idempotent — calling on an org with no
    /// connected account just clears the columns (no-op at Stripe).
    ///
    /// <para>Returns the post-clear org detail so the caller can return it
    /// directly. Throws via Stripe exception mapping when live-mode balance
    /// constraints prevent deletion.</para>
    /// </summary>
    Task<OrganizationDetailDto?> ClearStripeAccountAsync(Guid organizationId);
}
