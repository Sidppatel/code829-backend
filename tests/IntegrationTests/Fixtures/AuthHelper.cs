using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Api.Services;
using Microsoft.IdentityModel.Tokens;

namespace IntegrationTests.Fixtures;

public static class AuthHelper
{
    public const string TestJwtSecret = "integration-test-jwt-secret-must-be-32-chars!!";

    public static string GenerateUserJwt(Guid? userId = null, string email = "test.user@example.com", string role = "User", string userType = "user")
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, (userId ?? Guid.NewGuid()).ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Role, role),
            new Claim("user_type", userType)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: Api.Services.JwtConstants.Issuer,
            audience: Api.Services.JwtConstants.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string GenerateAdminJwt(Guid? adminId = null, string role = "Admin")
        => GenerateUserJwt(adminId, "admin@example.com", role, "admin");

    public static string GenerateStaffJwt(Guid? id = null) => GenerateAdminJwt(id, "Staff");
    public static string GenerateDeveloperJwt(Guid? id = null) => GenerateAdminJwt(id, "Developer");

    public static HttpClient WithUser(this HttpClient client, Guid? userId = null)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateUserJwt(userId));
        return client;
    }

    public static HttpClient WithAdmin(this HttpClient client, Guid? adminId = null, string role = "Admin")
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateAdminJwt(adminId, role));
        return client;
    }

    public static HttpClient WithoutAuth(this HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
        return client;
    }
}
