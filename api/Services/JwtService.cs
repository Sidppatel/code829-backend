using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Db.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Api.Services;

public class JwtService(ISecretsProvider secrets) : IJwtService
{
    public async Task<string> GenerateUserJwtAsync(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}".Trim()),
            new Claim(ClaimTypes.Role, "User"),
            new Claim("user_type", "user")
        };

        return await GenerateJwtAsync(claims);
    }

    public async Task<string> GenerateAdminJwtAsync(BusinessUser admin)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
            new Claim(ClaimTypes.Email, admin.Email),
            new Claim(ClaimTypes.Name, $"{admin.FirstName} {admin.LastName}".Trim()),
            new Claim(ClaimTypes.Role, admin.Role.ToString()),
            new Claim("user_type", "admin")
        };

        return await GenerateJwtAsync(claims);
    }

    private Task<string> GenerateJwtAsync(Claim[] claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secrets.JwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: JwtConstants.Issuer,
            audience: JwtConstants.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(JwtConstants.TokenLifetime),
            signingCredentials: credentials);

        return Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
    }
}
