using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class OrganizationProcedures(EventPlatformDbContext context) : IOrganizationProcedures
{
    public async Task<Guid> CreateAsync(string name, string? legalName = null, string countryCode = "US",
        CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_organization(@p0, @p1, @p2) AS \"Value\"",
                new NpgsqlParameter("p0", name),
                new NpgsqlParameter("p1", NpgsqlDbType.Text) { Value = (object?)legalName ?? DBNull.Value },
                new NpgsqlParameter("p2", NpgsqlDbType.Text) { Value = countryCode })
            .FirstAsync(ct);
    }

    public async Task UpdateAsync(Guid id, string? name = null, string? legalName = null,
        string? countryCode = null, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_organization(@p0, @p1, @p2, @p3)",
                [
                    new NpgsqlParameter("p0", id),
                    new NpgsqlParameter("p1", NpgsqlDbType.Text) { Value = (object?)name         ?? DBNull.Value },
                    new NpgsqlParameter("p2", NpgsqlDbType.Text) { Value = (object?)legalName    ?? DBNull.Value },
                    new NpgsqlParameter("p3", NpgsqlDbType.Text) { Value = (object?)countryCode  ?? DBNull.Value }
                ], ct);
    }

    public async Task UpdateStripeAccountAsync(Guid id, string stripeAccountId, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_organization_stripe_account(@p0, @p1)",
                [id, stripeAccountId], ct);
    }

    public async Task UpdateStripeStatusAsync(string stripeAccountId, bool chargesEnabled, bool payoutsEnabled,
        bool detailsSubmitted, string? requirementsDueJson = null, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_organization_stripe_status(@p0, @p1, @p2, @p3, @p4::jsonb)",
                [
                    new NpgsqlParameter("p0", stripeAccountId),
                    new NpgsqlParameter("p1", chargesEnabled),
                    new NpgsqlParameter("p2", payoutsEnabled),
                    new NpgsqlParameter("p3", detailsSubmitted),
                    new NpgsqlParameter("p4", NpgsqlDbType.Text) { Value = (object?)requirementsDueJson ?? DBNull.Value }
                ], ct);
    }

    public async Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Organizations
            .FromSqlRaw("SELECT * FROM sp_get_organization_stripe_status({0})", id)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Organization?> GetByBusinessUserAsync(Guid businessUserId, CancellationToken ct = default)
    {
        return await context.Organizations
            .FromSqlRaw("SELECT * FROM sp_get_organization_by_business_user({0})", businessUserId)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddBusinessUserAsync(Guid businessUserId, Guid organizationId, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_add_business_user_to_organization(@p0, @p1)",
                [businessUserId, organizationId], ct);
    }

    public async Task RemoveBusinessUserAsync(Guid businessUserId, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_remove_business_user_from_organization(@p0)",
                [businessUserId], ct);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_archive_organization(@p0)",
                [id], ct);
    }

    public async Task<List<OrganizationListRow>> ListAsync(string? search, bool includeArchived,
        int offset, int limit, CancellationToken ct = default)
    {
        var searchParam = (object?)search ?? DBNull.Value;
        return await context.Database
            .SqlQueryRaw<OrganizationListRow>(
                "SELECT * FROM sp_list_organizations({0}, {1}, {2}, {3})",
                searchParam, includeArchived, offset, limit)
            .ToListAsync(ct);
    }

    public async Task<int> CountAsync(string? search, bool includeArchived, CancellationToken ct = default)
    {
        var searchParam = (object?)search ?? DBNull.Value;
        return await context.Database
            .SqlQueryRaw<int>(
                "SELECT sp_count_organizations({0}, {1}) AS \"Value\"",
                searchParam, includeArchived)
            .FirstAsync(ct);
    }

    public async Task<List<OrganizationMemberRow>> GetMembersAsync(Guid organizationId, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<OrganizationMemberRow>(
                "SELECT * FROM sp_get_organization_members({0})", organizationId)
            .ToListAsync(ct);
    }

    public async Task<int> ClearStripeAccountAsync(Guid organizationId, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<int>(
                "SELECT sp_clear_organization_stripe_account({0}) AS \"Value\"", organizationId)
            .FirstAsync(ct);
    }
}
