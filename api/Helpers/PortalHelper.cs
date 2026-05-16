using Contracts.Enums;

namespace Api.Helpers;

public static class PortalHelper
{
    public const string HeaderName = "X-Portal";

    public const string UserCookie = "session_user";
    public const string AdminCookie = "session_admin";
    public const string StaffCookie = "session_staff";
    public const string DeveloperCookie = "session_developer";

    public static string? ReadPortal(HttpRequest request) =>
        request.Headers[HeaderName].FirstOrDefault()?.Trim().ToLowerInvariant();

    public static string? CookieFor(string? portal) => portal switch
    {
        "user" => UserCookie,
        "admin" => AdminCookie,
        "staff" => StaffCookie,
        "developer" => DeveloperCookie,
        _ => null
    };

    public static bool IsAdminPortal(string? portal) =>
        portal is "admin" or "staff" or "developer";

    public static AdminRole? MinRoleForPortal(string? portal) => portal switch
    {
        "staff" => AdminRole.Staff,
        "admin" => AdminRole.Admin,
        "developer" => AdminRole.Developer,
        _ => null
    };
}
