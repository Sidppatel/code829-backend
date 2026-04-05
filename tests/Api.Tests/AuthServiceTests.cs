using System.Security.Cryptography;
using Api.Services;
using Contracts.Enums;
using Db;
using Db.Entities;
using Db.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Api.Tests;

public class AuthServiceTests : IDisposable
{
    private readonly EventPlatformDbContext _context;
    private readonly Mock<ISettingsService> _settingsService;
    private readonly Mock<IEmailService> _emailService;
    private readonly Mock<IEncryptionService> _encryptionService;
    private readonly Mock<IWebHostEnvironment> _environment;
    private readonly IUserRepository _userRepository;
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _context = TestDbContextFactory.Create();

        _settingsService = new Mock<ISettingsService>();
        _emailService = new Mock<IEmailService>();
        _encryptionService = new Mock<IEncryptionService>();
        _environment = new Mock<IWebHostEnvironment>();

        _userRepository = new UserRepository(_context);

        _settingsService.Setup(s => s.GetOrDefaultAsync("magic_link_expiry_minutes", "15"))
            .ReturnsAsync("15");
        _settingsService.Setup(s => s.GetOrDefaultAsync("frontend_url", "http://localhost:5173"))
            .ReturnsAsync("http://localhost:5173");
        _settingsService.Setup(s => s.GetOrDefaultAsync("app_name", "Code829"))
            .ReturnsAsync("Code829");
        _settingsService.Setup(s => s.GetAsync("jwt_secret"))
            .ReturnsAsync("a-very-long-jwt-secret-key-that-is-at-least-32-bytes-long-for-hmac256");
        _encryptionService.Setup(e => e.HashEmail(It.IsAny<string>()))
            .Returns((string email) => email.GetHashCode().ToString());

        var fileStorage = new Mock<IFileStorageService>();
        _service = new AuthService(_context, _userRepository, _settingsService.Object,
            _emailService.Object, _encryptionService.Object, _environment.Object, fileStorage.Object);
    }

    [Fact]
    public async Task SendMagicLinkAsync_StoresHashedToken_NotRawToken()
    {
        _environment.Setup(e => e.EnvironmentName).Returns("Production");
        _emailService.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.SendMagicLinkAsync("test@example.com");

        var token = await _context.MagicLinkTokens.FirstAsync();
        // Token hash should be a hex string (SHA256 = 64 hex chars)
        token.TokenHash.Should().HaveLength(64);
        token.TokenHash.Should().MatchRegex("^[0-9a-f]+$");
    }

    [Fact]
    public async Task VerifyMagicLinkAsync_WithExpiredToken_ThrowsUnauthorizedAccessException()
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(rawToken));
        var tokenHash = Convert.ToHexStringLower(hash);

        _context.MagicLinkTokens.Add(new MagicLinkToken
        {
            Id = Guid.NewGuid(),
            TokenHash = tokenHash,
            Email = "test@example.com",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1), // expired
            IsUsed = false
        });
        await _context.SaveChangesAsync();

        var act = () => _service.VerifyMagicLinkAsync(rawToken);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task VerifyMagicLinkAsync_WithUsedToken_ThrowsUnauthorizedAccessException()
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(rawToken));
        var tokenHash = Convert.ToHexStringLower(hash);

        _context.MagicLinkTokens.Add(new MagicLinkToken
        {
            Id = Guid.NewGuid(),
            TokenHash = tokenHash,
            Email = "test@example.com",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            IsUsed = true // already used
        });
        await _context.SaveChangesAsync();

        var act = () => _service.VerifyMagicLinkAsync(rawToken);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task VerifyMagicLinkAsync_ValidToken_CreatesUserIfNotExists()
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(rawToken));
        var tokenHash = Convert.ToHexStringLower(hash);

        _context.MagicLinkTokens.Add(new MagicLinkToken
        {
            Id = Guid.NewGuid(),
            TokenHash = tokenHash,
            Email = "newuser@example.com",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            IsUsed = false
        });
        await _context.SaveChangesAsync();

        var result = await _service.VerifyMagicLinkAsync(rawToken);

        result.Should().NotBeNull();
        result.User.Email.Should().Be("newuser@example.com");
        result.RefreshToken.Should().NotBeNullOrEmpty();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == "newuser@example.com");
        user.Should().NotBeNull();
    }

    [Fact]
    public async Task DevLoginAsync_InProduction_ThrowsInvalidOperationException()
    {
        _environment.Setup(e => e.EnvironmentName).Returns("Production");

        var act = () => _service.DevLoginAsync("test@example.com");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not available*");
    }

    [Fact]
    public async Task RefreshToken_WithValidToken_ReturnsNewTokens()
    {
        // Create a user and a valid refresh token
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "refresh@example.com",
            EmailHash = "refreshhash",
            FirstName = "Refresh",
            LastName = "User",
            Role = UserRole.User,
            IsActive = true
        };
        _context.Users.Add(user);

        var rawRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(rawRefreshToken));
        var tokenHash = Convert.ToHexStringLower(hash);

        _context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsUsed = false
        });
        await _context.SaveChangesAsync();

        var result = await _service.RefreshTokenAsync(rawRefreshToken);

        result.Should().NotBeNull();
        result.User.Email.Should().Be("refresh@example.com");
        result.Token.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBe(rawRefreshToken); // rotated

        // Old token should be marked as used
        var oldToken = await _context.RefreshTokens.FirstAsync(t => t.TokenHash == tokenHash);
        oldToken.IsUsed.Should().BeTrue();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
