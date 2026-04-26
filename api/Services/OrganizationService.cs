using Contracts.DTOs;
using Contracts.DTOs.Organizations;
using Db;
using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Db.Repositories.StoredProcedures;
using Serilog;

namespace Api.Services;

/// <summary>
/// Default implementation of <see cref="IOrganizationService"/>. Delegates all
/// data access to <see cref="IOrganizationProcedures"/>; no direct DbSet usage.
/// The Stripe-onboarding-email path additionally pulls the BusinessUser row
/// for name/email and bridges to <see cref="IStripeConnectService"/> + the
/// Resend-backed <see cref="IEmailService"/>.
/// </summary>
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
            org.StripeOnboardedAt, null, // CurrentlyDue fetched live via stripe-status endpoint
            org.ArchivedAt, org.CreatedAt, org.CreatedAt, // UpdatedAt not in view, using CreatedAt as placeholder or we could add it
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
            r.CreatedAt, r.CreatedAt, // UpdatedAt not in view
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

    public async Task<StripeOnboardingEmailResponse> SendOnboardingLinkEmailAsync(Guid businessUserId)
    {
        var member = await businessUserProc.GetByIdAsync(businessUserId)
            ?? throw new KeyNotFoundException($"BusinessUser {businessUserId} not found");

        var organization = await orgProc.GetByBusinessUserAsync(businessUserId)
            ?? throw new InvalidOperationException(
                "BusinessUser is not attached to any organization — assign them to one before sending the onboarding link");

        if (string.IsNullOrEmpty(organization.StripeConnectedAccountId))
            throw new InvalidOperationException(
                "Organization has no Stripe account yet — call POST /developer/organizations/{id}/stripe-account first");

        // Identity scope is the right starting point: a fresh BusinessUser has
        // never seen the onboarding flow, so we route them through the full
        // KYC + bank capture rather than the bank-only resume link.
        var url = await stripeConnect.CreateOnboardingLinkAsync(
            organization.StripeConnectedAccountId, OnboardingLinkScope.Identity);

        // Stripe AccountLink URLs expire ~5 minutes after creation; that's the
        // "expiry" we surface in the email copy.
        var brandName = await settings.GetOrDefaultAsync("app_name", "Code829") ?? "Code829";
        var subject = $"Finish setting up payouts for {organization.Name} | {brandName}";
        var body = EmailTemplates.OnboardingLinkEmail(
            brandName, member.FirstName, organization.Name, url, expiryMinutes: 5);

        await emailService.SendAsync(member.Email, subject, body);

        Log.Information(
            "[Organization] Sent onboarding link to BusinessUser {BusinessUserId} for org {OrganizationId}",
            businessUserId, organization.Id);

        // TODO: surface email_log id from sp_insert_email_log — IEmailService.SendAsync
        // currently discards the row id returned by sp_create_email_log; when that
        // signature is widened to surface it, plumb it through here.
        return new StripeOnboardingEmailResponse(Guid.Empty, member.Email);
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

        // Stripe-side delete first. If Stripe rejects (live-mode non-zero
        // balance) we surface the exception and leave the DB row untouched
        // so the admin can re-try or escalate without losing the acct id.
        if (!string.IsNullOrEmpty(org.StripeConnectedAccountId))
        {
            await stripeConnect.DeleteAccountAsync(org.StripeConnectedAccountId);
            Log.Information("[Organization] Deleted Stripe connected account {AccountId} for org {OrganizationId}",
                org.StripeConnectedAccountId, organizationId);
        }

        // DB-side clear runs unconditionally so partial state (e.g. acct id
        // present but charges/payouts stuck false) gets normalized.
        var rows = await orgProc.ClearStripeAccountAsync(organizationId);
        Log.Information("[Organization] Cleared Stripe columns on org {OrganizationId} (rows={Rows})",
            organizationId, rows);

        return await GetDetailAsync(organizationId);
    }
}
