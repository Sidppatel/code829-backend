namespace Api.Services;

public static class JwtConstants
{
    public const string Issuer = "code829-api";
    public const string Audience = "code829-client";
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15);
}
