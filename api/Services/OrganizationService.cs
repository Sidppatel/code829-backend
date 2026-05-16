using Contracts.DTOs;
using Contracts.DTOs.Organizations;
using Db;
using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Db.Repositories.StoredProcedures;
using Serilog;

namespace Api.Services;

public class OrganizationService(
    EventPlatformDbContext context,
    IOrganizationProcedures orgProc,
    IBusinessUserProcedures businessUserProc,
    IStripeConnectService stripeConnect,
    IEmailService emailService,
    ISettingsService settings
) : IOrganizationService
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

    public async Task<OrganizationDetailDto?> GetDetailAsync(Guid id)
    {
        var org = await context.OrganizationViews
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrganizationId == id);

        if (org is null) return null;

        var memberRows = await orgProc.GetMembersAsync(id);
        var members = memberRows
            .Select(m => new OrganizationMemberDto(m.BusinessUserId, m.Email, m.DisplayName))
            .ToList();

        return new OrganizationDetailDto(
            org.OrganizationId, org.Name, org.LegalName, org.CountryCode,
            org.StripeConnectedAccountId,
            org.StripeChargesEnabled, org.StripePayoutsEnabled, org.StripeDetailsSubmitted,
            org.StripeOnboardedAt, null,
            org.ArchivedAt, org.CreatedAt, org.CreatedAt,
            members
        );
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

        _ = orgId;
        await orgProc.RemoveBusinessUserAsync(businessUserId);
        Log.Information("[Organization] Removed BusinessUser {BusinessUserId} from {OrganizationId}",
            businessUserId, orgId);
    }

    public async Task<PagedResponse<OrganizationListItemDto>> ListAsync(string? search, int page, int pageSize, bool includeArchived = false)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = context.OrganizationViews.AsNoTracking();

        if (!includeArchived)
            query = query.Where(o => o.ArchivedAt == null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(o =>
                o.Name.ToLower().Contains(term) ||
                (o.LegalName != null && o.LegalName.ToLower().Contains(term)) ||
                (o.StripeConnectedAccountId != null && o.StripeConnectedAccountId.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync();
        var rows = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = rows.Select(r => new OrganizationListItemDto(
            r.OrganizationId, r.Name, r.LegalName, r.CountryCode,
            r.StripeConnectedAccountId,
            r.StripeChargesEnabled, r.StripePayoutsEnabled, r.StripeDetailsSubmitted,
            r.StripeOnboardedAt, r.ArchivedAt, r.MemberCount,
            r.CreatedAt, r.CreatedAt,
            OrganizationStripeStateMapper.Derive(
                r.StripeConnectedAccountId,
                r.StripeDetailsSubmitted,
                r.StripeChargesEnabled,
                r.StripePayoutsEnabled,
                disabledReason: null),
            HasStripeAccount: !string.IsNullOrEmpty(r.StripeConnectedAccountId)
        )).ToList();

        return new PagedResponse<OrganizationListItemDto>(items, totalCount, page, pageSize);
    }

    public async Task<StripeOnboardingEmailResponse> SendOnboardingLinkEmailAsync(
        Guid organizationId,
        Guid? businessUserId = null,
        string? recipientEmail = null)
    {
        if (businessUserId is null && string.IsNullOrWhiteSpace(recipientEmail))
            throw new InvalidOperationException(
                "Provide either businessUserId or recipientEmail to address the onboarding email");

        var organization = await orgProc.GetByIdAsync(organizationId)
            ?? throw new KeyNotFoundException($"Organization {organizationId} not found");

        if (string.IsNullOrEmpty(organization.StripeConnectedAccountId))
            throw new InvalidOperationException(
                "Organization has no Stripe account yet — call POST /developer/organizations/{id}/stripe-account first");

        string toEmail;
        string greetingName;
        if (!string.IsNullOrWhiteSpace(recipientEmail))
        {
            toEmail = recipientEmail.Trim();
            greetingName = "there";
            if (businessUserId is { } buId)
            {
                var bu = await businessUserProc.GetByIdAsync(buId);
                if (bu is not null) greetingName = bu.FirstName;
            }
        }
        else
        {
            var buId = businessUserId!.Value;
            var member = await businessUserProc.GetByIdAsync(buId)
                ?? throw new KeyNotFoundException($"BusinessUser {buId} not found");

            var memberOrg = await orgProc.GetByBusinessUserAsync(buId);
            if (memberOrg is null || memberOrg.Id != organizationId)
                throw new InvalidOperationException(
                    "BusinessUser is not a member of this organization — assign them first or pass recipientEmail to override");

            toEmail = member.Email;
            greetingName = member.FirstName;
        }

        var url = await stripeConnect.CreateOnboardingLinkAsync(
            organization.StripeConnectedAccountId, OnboardingLinkScope.Identity);

        var brandName = await settings.GetOrDefaultAsync("app_name", "Code829") ?? "Code829";
        var subject = $"Finish setting up payouts for {organization.Name} | {brandName}";
        var body = EmailTemplates.OnboardingLinkEmail(
            brandName, greetingName, organization.Name, url, expiryMinutes: 5);

        await emailService.SendAsync(toEmail, subject, body);

        Log.Information(
            "[Organization] Sent onboarding link to {Recipient} for org {OrganizationId} (bu={BusinessUserId})",
            toEmail, organization.Id, businessUserId);

        return new StripeOnboardingEmailResponse(Guid.Empty, toEmail);
    }

    private static OrganizationDto MapToDto(Organization o) => new(
        o.Id, o.Name, o.LegalName, o.CountryCode,
        o.StripeConnectedAccountId,
        o.StripeChargesEnabled, o.StripePayoutsEnabled, o.StripeDetailsSubmitted,
        o.StripeOnboardedAt, o.StripeRequirementsDue,
        o.ArchivedAt, o.CreatedAt, o.UpdatedAt
    );

    public async Task<OrganizationDetailDto?> ClearStripeAccountAsync(Guid organizationId)
    {
        var org = await orgProc.GetByIdAsync(organizationId);
        if (org is null) return null;

        if (!string.IsNullOrEmpty(org.StripeConnectedAccountId))
        {
            await stripeConnect.DeleteAccountAsync(org.StripeConnectedAccountId);
            Log.Information("[Organization] Deleted Stripe connected account {AccountId} for org {OrganizationId}",
                org.StripeConnectedAccountId, organizationId);
        }

        var rows = await orgProc.ClearStripeAccountAsync(organizationId);
        Log.Information("[Organization] Cleared Stripe columns on org {OrganizationId} (rows={Rows})",
            organizationId, rows);

        return await GetDetailAsync(organizationId);
    }
}
