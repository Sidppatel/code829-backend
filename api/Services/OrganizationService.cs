using Contracts.DTOs;
using Contracts.DTOs.Organizations;
using Db.Entities;
using Db.Repositories.StoredProcedures;
using Serilog;

namespace Api.Services;

/// <summary>
/// Default implementation of <see cref="IOrganizationService"/>. Delegates all
/// data access to <see cref="IOrganizationProcedures"/>; no direct DbSet usage.
/// </summary>
public class OrganizationService(IOrganizationProcedures orgProc) : IOrganizationService
{
    public async Task<Guid> CreateAsync(string name, string? legalName, string countryCode, Guid? initialMemberBusinessUserId)
    {
        var id = await orgProc.CreateAsync(name, legalName, countryCode);
        Log.Information("[Organization] Created {OrganizationId} ({Name})", id, name);

        if (initialMemberBusinessUserId is { } memberId)
        {
            await orgProc.AddBusinessUserAsync(memberId, id);
            Log.Information("[Organization] Attached BusinessUser {BusinessUserId} as initial member of {OrganizationId}",
                memberId, id);
        }

        return id;
    }

    public async Task<OrganizationDto?> GetAsync(Guid id)
    {
        var org = await orgProc.GetByIdAsync(id);
        return org is null ? null : MapToDto(org);
    }

    public async Task<OrganizationDto?> GetByBusinessUserIdAsync(Guid businessUserId)
    {
        var org = await orgProc.GetByBusinessUserAsync(businessUserId);
        return org is null ? null : MapToDto(org);
    }

    public async Task UpdateAsync(Guid id, OrganizationUpdateRequest req)
    {
        await orgProc.UpdateAsync(id, req.Name, req.LegalName, req.CountryCode);
        Log.Information("[Organization] Updated {OrganizationId}", id);
    }

    public async Task AddMemberAsync(Guid orgId, Guid businessUserId)
    {
        await orgProc.AddBusinessUserAsync(businessUserId, orgId);
        Log.Information("[Organization] Added BusinessUser {BusinessUserId} to {OrganizationId}",
            businessUserId, orgId);
    }

    public async Task RemoveMemberAsync(Guid orgId, Guid businessUserId)
    {
        // The SP scopes the detach to whatever org the BusinessUser is in;
        // orgId is part of the public contract for symmetry + future-proofing
        // (e.g., once a BusinessUser can belong to multiple orgs we'll need it).
        _ = orgId;
        await orgProc.RemoveBusinessUserAsync(businessUserId);
        Log.Information("[Organization] Removed BusinessUser {BusinessUserId} from {OrganizationId}",
            businessUserId, orgId);
    }

    public async Task<PagedResponse<OrganizationListItemDto>> ListAsync(string? search, int page, int pageSize, bool includeArchived = false)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        var offset = (page - 1) * pageSize;
        var rows = await orgProc.ListAsync(search, includeArchived, offset, pageSize);
        var totalCount = await orgProc.CountAsync(search, includeArchived);

        var items = rows.Select(r => new OrganizationListItemDto(
            r.Id, r.Name, r.LegalName, r.CountryCode,
            r.StripeConnectedAccountId,
            r.StripeChargesEnabled, r.StripePayoutsEnabled, r.StripeDetailsSubmitted,
            r.StripeOnboardedAt, r.ArchivedAt, r.MemberCount,
            r.CreatedAt, r.UpdatedAt
        )).ToList();

        return new PagedResponse<OrganizationListItemDto>(items, totalCount, page, pageSize);
    }

    private static OrganizationDto MapToDto(Organization o) => new(
        o.Id, o.Name, o.LegalName, o.CountryCode,
        o.StripeConnectedAccountId,
        o.StripeChargesEnabled, o.StripePayoutsEnabled, o.StripeDetailsSubmitted,
        o.StripeOnboardedAt, o.StripeRequirementsDue,
        o.ArchivedAt, o.CreatedAt, o.UpdatedAt
    );
}
