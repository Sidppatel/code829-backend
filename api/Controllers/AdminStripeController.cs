using System.Security.Claims;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Organizations;
using Contracts.Enums;
using Db.Repositories.StoredProcedures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Stripe;

namespace Api.Controllers;

/// <summary>
/// Admin-facing Stripe Connect endpoints. Scoped to the authenticated admin's
/// organization — admins never see another org's data.
///
/// <para>
/// Ownership rule: every method resolves the org from
/// <c>BusinessUser.OrganizationId</c> (authenticated user's claim → BU lookup).
/// There is no <c>{organizationId}</c> in the route, so an admin can't probe
/// for other orgs' acct ids. If the admin has no organization yet, the endpoint
/// returns a structured 409 telling them to contact a developer.
/// </para>
/// </summary>
[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}/admin/organization")]
[Authorize]
[RequireRole(UserRole.Admin)]
public class AdminStripeController(
    IOrganizationService organizationService,
    IOrganizationProcedures organizationProc,
    IStripeConnectService stripeConnect
) : ControllerBase
{
    /// <summary>Stripe Connect status for the authenticated admin's org.</summary>
    /// <remarks>
    /// Live-fetches from Stripe and writes the snapshot back to the DB so other
    /// readers (e.g., the developer dashboard) get fresh data without paying
    /// the round-trip themselves.
    /// </remarks>
    [HttpGet("stripe-status")]
    public async Task<IActionResult> GetStripeStatus()
    {
        var org = await ResolveOwningOrgAsync();
        if (org is IActionResult forbidden) return forbidden;
        var orgDto = (OrganizationDto)org!;

        if (string.IsNullOrEmpty(orgDto.StripeConnectedAccountId))
        {

            return Ok(new OrganizationStripeStatusDto(
                OrganizationId: orgDto.Id,
                OrganizationName: orgDto.Name,
                StripeAccount: null,
                State: OrganizationStripeStateMapper.Derive(
                    stripeAccountId: null,
                    detailsSubmitted: false,
                    chargesEnabled: false,
                    payoutsEnabled: false,
                    disabledReason: null),
                BankAccountLast4: null,

                Members: new List<OrganizationMemberDto>(),
                ExpressDashboardUrl: null,
                FetchedAt: DateTime.UtcNow));
        }

        try
        {
            var status = await stripeConnect.FetchAccountStatusAsync(orgDto.StripeConnectedAccountId);

            var requirementsJson = System.Text.Json.JsonSerializer.Serialize(status.RequirementsCurrentlyDue);
            await organizationProc.UpdateStripeStatusAsync(
                status.AccountId,
                status.ChargesEnabled, status.PayoutsEnabled, status.DetailsSubmitted,
                requirementsJson);

            var state = OrganizationStripeStateMapper.Derive(
                stripeAccountId: status.AccountId,
                detailsSubmitted: status.DetailsSubmitted,
                chargesEnabled: status.ChargesEnabled,
                payoutsEnabled: status.PayoutsEnabled,
                disabledReason: status.DisabledReason);

            var expressDashboardUrl = status.PayoutsEnabled
                ? await stripeConnect.CreateLoginLinkAsync(orgDto.StripeConnectedAccountId)
                : null;

            var stripeAccountDto = new StripeAccountStatusDto(
                AccountId: status.AccountId,
                ChargesEnabled: status.ChargesEnabled,
                PayoutsEnabled: status.PayoutsEnabled,
                DetailsSubmitted: status.DetailsSubmitted,
                RequirementsCurrentlyDue: status.RequirementsCurrentlyDue);

            return Ok(new OrganizationStripeStatusDto(
                OrganizationId: orgDto.Id,
                OrganizationName: orgDto.Name,
                StripeAccount: stripeAccountDto,
                State: state,
                BankAccountLast4: status.BankAccountLast4,

                Members: new List<OrganizationMemberDto>(),
                ExpressDashboardUrl: expressDashboardUrl,
                FetchedAt: DateTime.UtcNow));
        }
        catch (StripeException ex)
        {
            Log.Error(ex, "[AdminStripe] Failed to fetch Stripe status for org {OrganizationId}", orgDto.Id);
            return StatusCode(502, new ApiError(502, "Failed to fetch Stripe account status", HttpContext.TraceIdentifier));
        }
    }

    /// <summary>
    /// Generate a fresh onboarding link for the admin's org. Any admin in the
    /// org can call this — they share the same Stripe account, so multiple
    /// admins coordinating onboarding works out of the box.
    /// </summary>
    [HttpPost("stripe-resume-link")]
    public async Task<IActionResult> CreateResumeLink([FromBody] StripeOnboardingLinkRequest? request)
    {
        var org = await ResolveOwningOrgAsync();
        if (org is IActionResult forbidden) return forbidden;
        var orgDto = (OrganizationDto)org!;

        if (string.IsNullOrEmpty(orgDto.StripeConnectedAccountId))
            return Conflict(new ApiError(409,
                "Organization has no Stripe account yet — contact a platform developer to enable payouts",
                HttpContext.TraceIdentifier));

        var scope = string.Equals(request?.Scope, "bank", StringComparison.OrdinalIgnoreCase)
            ? OnboardingLinkScope.BankOnly
            : OnboardingLinkScope.Identity;

        try
        {
            var url = await stripeConnect.CreateOnboardingLinkAsync(orgDto.StripeConnectedAccountId, scope);
            return Ok(new StripeOnboardingLinkResponse(url, DateTime.UtcNow.AddMinutes(5)));
        }
        catch (StripeException ex)
        {
            Log.Error(ex, "[AdminStripe] Failed to create resume link for org {OrganizationId}", orgDto.Id);
            return StatusCode(502, new ApiError(502, "Failed to create onboarding link", HttpContext.TraceIdentifier));
        }
    }

    /// <summary>
    /// Resolves the authenticated admin's organization. Returns either the
    /// <see cref="OrganizationDto"/> (boxed as object) or an <see cref="IActionResult"/>
    /// pre-baked with the right error code — callers branch on the type.
    /// Keeps every endpoint's first 3 lines identical without a base class.
    /// </summary>
    private async Task<object?> ResolveOwningOrgAsync()
    {
        var businessUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(businessUserIdClaim, out var businessUserId))
            return Unauthorized(new ApiError(401, "Invalid session", HttpContext.TraceIdentifier));

        var org = await organizationService.GetByBusinessUserIdAsync(businessUserId);
        if (org is null)
            return Conflict(new ApiError(409,
                "You are not a member of any organization yet — contact a platform developer to be added",
                HttpContext.TraceIdentifier));

        return org;
    }
}
