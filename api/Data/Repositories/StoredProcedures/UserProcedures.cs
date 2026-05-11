using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class UserProcedures(EventPlatformDbContext context) : IUserProcedures
{
    public async Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await context.Users
            .FromSqlRaw("SELECT * FROM sp_get_user_by_id({0})", userId)
            .Include(u => u.Address)
            .Include(u => u.Image)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
        return user;
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var user = await context.Users
            .FromSqlRaw("SELECT * FROM sp_get_user_by_email({0})", email)
            .Include(u => u.Address)
            .Include(u => u.Image)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
        return user;
    }

    public async Task<User?> GetByEmailHashAsync(string emailHash, CancellationToken ct = default)
    {
        var user = await context.Users
            .FromSqlRaw("SELECT * FROM sp_get_user_by_email_hash({0})", emailHash)
            .Include(u => u.Address)
            .Include(u => u.Image)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
        return user;
    }

    public async Task<List<User>> ListAsync(CancellationToken ct = default)
    {
        return await context.Users
            .FromSqlRaw("SELECT * FROM sp_list_users()")
            .Include(u => u.Address)
            .Include(u => u.Image)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<bool>("SELECT sp_user_exists_by_email({0}) AS \"Value\"", email)
            .FirstAsync(ct);
    }

    public async Task<UserCounts> GetCountsAsync(CancellationToken ct = default)
    {
        var row = await context.Database
            .SqlQueryRaw<UserCountsRow>("SELECT * FROM sp_user_counts()")
            .FirstAsync(ct);
        return new UserCounts(row.Total, row.Active, row.NewThisMonth);
    }

    public async Task UpdateUserProfileAsync(Guid userId, string? firstName, string? lastName, string? phone, string? address, string? city, string? state, string? zip, bool? optIn, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_user_profile(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8)",
                [
                    new NpgsqlParameter("p0", userId),
                    new NpgsqlParameter("p1", NpgsqlDbType.Text) { Value = (object?)firstName ?? DBNull.Value },
                    new NpgsqlParameter("p2", NpgsqlDbType.Text) { Value = (object?)lastName ?? DBNull.Value },
                    new NpgsqlParameter("p3", NpgsqlDbType.Text) { Value = (object?)phone ?? DBNull.Value },
                    new NpgsqlParameter("p4", NpgsqlDbType.Text) { Value = (object?)address ?? DBNull.Value },
                    new NpgsqlParameter("p5", NpgsqlDbType.Text) { Value = (object?)city ?? DBNull.Value },
                    new NpgsqlParameter("p6", NpgsqlDbType.Text) { Value = (object?)state ?? DBNull.Value },
                    new NpgsqlParameter("p7", NpgsqlDbType.Text) { Value = (object?)zip ?? DBNull.Value },
                    new NpgsqlParameter("p8", NpgsqlDbType.Boolean) { Value = (object?)optIn ?? DBNull.Value }
                ], ct);
    }

    public async Task<Guid?> SetUserImageAsync(Guid userId, Guid imageId, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<Guid?>("SELECT sp_set_user_image({0}, {1}) AS \"Value\"", userId, imageId)
            .FirstAsync(ct);
    }

    public async Task<Guid?> ClearUserImageAsync(Guid userId, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<Guid?>("SELECT sp_clear_user_image({0}) AS \"Value\"", userId)
            .FirstAsync(ct);
    }

    public async Task<bool> SetUserActiveAsync(Guid userId, bool isActive, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<bool>("SELECT sp_set_user_active({0}, {1}) AS \"Value\"", userId, isActive)
            .FirstAsync(ct);
    }

    public async Task<bool> DeleteUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<bool>("SELECT sp_delete_user({0}) AS \"Value\"", userId)
            .FirstAsync(ct);
    }

    public async Task<User> SignupUserAsync(string email, string emailHash, string firstName, string lastName,
        string passwordHash, CancellationToken ct = default)
    {
        var user = await context.Users
            .FromSqlRaw("SELECT * FROM sp_signup_user({0}, {1}, {2}, {3}, {4})",
                email, emailHash, firstName, lastName, passwordHash)
            .AsNoTracking()
            .FirstAsync(ct);
        return user;
    }

    public async Task<User?> GetByEmailForSigninAsync(string emailHash, CancellationToken ct = default)
    {
        return await context.Users
            .FromSqlRaw("SELECT * FROM sp_get_user_by_email_for_signin({0})", emailHash)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task UpdateLastLoginAsync(Guid userId, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync("SELECT sp_update_user_last_login(@p0)", [userId], ct);
    }

    public async Task UpdatePasswordAsync(Guid userId, string passwordHash, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_user_password(@p0, @p1)",
                [userId, passwordHash], ct);
    }

    public async Task<UserPasswordResetToken> CreatePasswordResetTokenAsync(Guid userId, string tokenHash,
        DateTime expiresAt, string? ipAddress, CancellationToken ct = default)
    {
        var token = await context.UserPasswordResetTokens
            .FromSqlRaw(
                "SELECT * FROM sp_create_user_password_reset_token(@p0, @p1, @p2, @p3)",
                new NpgsqlParameter("p0", userId),
                new NpgsqlParameter("p1", tokenHash),
                new NpgsqlParameter("p2", expiresAt),
                new NpgsqlParameter("p3", NpgsqlDbType.Text) { Value = (object?)ipAddress ?? DBNull.Value })
            .AsNoTracking()
            .FirstAsync(ct);
        return token;
    }

    public async Task<User> ConsumePasswordResetTokenAsync(string tokenHash, CancellationToken ct = default)
    {
        var user = await context.Users
            .FromSqlRaw("SELECT * FROM sp_consume_user_password_reset_token({0})", tokenHash)
            .AsNoTracking()
            .FirstAsync(ct);
        return user;
    }

    public async Task<UserEmailVerificationToken> CreateEmailVerificationTokenAsync(Guid userId, string tokenHash,
        DateTime expiresAt, string? ipAddress, CancellationToken ct = default)
    {
        var token = await context.UserEmailVerificationTokens
            .FromSqlRaw(
                "SELECT * FROM sp_create_user_email_verification_token(@p0, @p1, @p2, @p3)",
                new NpgsqlParameter("p0", userId),
                new NpgsqlParameter("p1", tokenHash),
                new NpgsqlParameter("p2", expiresAt),
                new NpgsqlParameter("p3", NpgsqlDbType.Text) { Value = (object?)ipAddress ?? DBNull.Value })
            .AsNoTracking()
            .FirstAsync(ct);
        return token;
    }

    public async Task<User> ConsumeEmailVerificationTokenAsync(string tokenHash, CancellationToken ct = default)
    {
        var user = await context.Users
            .FromSqlRaw("SELECT * FROM sp_consume_user_email_verification_token({0})", tokenHash)
            .AsNoTracking()
            .FirstAsync(ct);
        return user;
    }

    public async Task<User> SignInUserGoogleAsync(string googleSubject, string email, string emailHash,
        string firstName, string lastName, CancellationToken ct = default)
    {
        var user = await context.Users
            .FromSqlRaw("SELECT * FROM sp_signin_user_google({0}, {1}, {2}, {3}, {4})",
                googleSubject, email, emailHash, firstName, lastName)
            .AsNoTracking()
            .FirstAsync(ct);
        return user;
    }

    public async Task SetPasswordAsync(Guid userId, string newPasswordHash, bool revokeOtherSessions, string? currentSessionHash, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_set_user_password(@p0, @p1, @p2, @p3)",
                [
                    new NpgsqlParameter("p0", userId),
                    new NpgsqlParameter("p1", newPasswordHash),
                    new NpgsqlParameter("p2", revokeOtherSessions),
                    new NpgsqlParameter("p3", NpgsqlDbType.Text) { Value = (object?)currentSessionHash ?? DBNull.Value }
                ], ct);
    }

    private sealed class UserCountsRow
    {
        public int Total { get; set; }
        public int Active { get; set; }
        public int NewThisMonth { get; set; }
    }
}
