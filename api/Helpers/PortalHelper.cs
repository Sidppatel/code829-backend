using Contracts.Enums;

namespace Api.Helpers;

/// <summary>
/// Maps the <c>X-Portal</c> request header to per-portal session cookie names.
///
/// Every frontend app attaches a fixed header (public = <c>user</c>, admin = <c>admin</c>,
/// staff = <c>staff</c>, developer = <c>developer</c>) so the backend can tell which portal
/// originated a request even though all four share <c>localhost</c> and proxy through the
/// same backend port. Separate cookie names prevent browser-level collision where logging
/// into one portal would overwrite another's session.
/// </summary>
public static class PortalHelper
{
    public const string HeaderName = "X-Portal";

    public const string UserCookie      = "session_user";
    public const string AdminCookie     = "session_admin";
    public const string StaffCookie     = "session_staff";
    public const string DeveloperCookie = "session_developer";

    public static string? ReadPortal(HttpRequest request) =>
        request.Headers[HeaderName].FirstOrDefault()?.Trim().ToLowerInvariant();

    /// <summary>Cookie name for the given portal value, or <c>null</c> if unrecognised.</summary>
    public static string? CookieFor(string? portal) => portal switch
    {
        "user"      => UserCookie,
        "admin"     => AdminCookie,
        "staff"     => StaffCookie,
        "developer" => DeveloperCookie,
        _           => null
    };

    public static bool IsAdminPortal(string? portal) =>
        portal is "admin" or "staff" or "developer";

    /// <summary>Minimum <see cref="AdminRole"/> required to hold a session cookie for the portal.</summary>
    public static AdminRole? MinRoleForPortal(string? portal) => portal switch
    {
        "staff"     => AdminRole.Staff,
        "admin"     => AdminRole.Admin,
        "developer" => AdminRole.Developer,
        _           => null
    };
}
