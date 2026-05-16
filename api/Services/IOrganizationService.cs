using Contracts.DTOs;
using Contracts.DTOs.Organizations;

namespace Api.Services;

public interface IOrganizationService
{
    Task<Guid> CreateAsync(string name, string? legalName, string countryCode, Guid? initialMemberBusinessUserId);

    Task<OrganizationDto?> GetAsync(Guid id);

    Task<OrganizationDetailDto?> GetDetailAsync(Guid id);

    Task<OrganizationDto?> GetByBusinessUserIdAsync(Guid businessUserId);

    Task UpdateAsync(Guid id, OrganizationUpdateRequest req);

    Task AddMemberAsync(Guid orgId, Guid businessUserId);

    Task RemoveMemberAsync(Guid orgId, Guid businessUserId);

    Task<PagedResponse<OrganizationListItemDto>> ListAsync(string? search, int page, int pageSize, bool includeArchived = false);

    Task<StripeOnboardingEmailResponse> SendOnboardingLinkEmailAsync(
    Guid organizationId,
    Guid? businessUserId = null,
    string? recipientEmail = null);

    Task<OrganizationDetailDto?> ClearStripeAccountAsync(Guid organizationId);
}
