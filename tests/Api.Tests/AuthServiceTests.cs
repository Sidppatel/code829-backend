using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Api.Services;
using Contracts.Enums;
using Db;
using Db.Entities;
using Db.Repositories;
using Db.Repositories.StoredProcedures;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Moq;
using StackExchange.Redis;

namespace Api.Tests;

public class AuthServiceTests : IDisposable
{
    private readonly EventPlatformDbContext _context;
    private readonly Mock<ISettingsService> _settingsService;
    private readonly Mock<IEmailService> _emailService;
    private readonly Mock<IEncryptionService> _encryptionService;
    private readonly Mock<IWebHostEnvironment> _environment;
    private readonly Mock<IAuthProcedures> _authProc;
    private readonly Mock<IConnectionMultiplexer> _redis;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _context = TestDbContextFactory.Create();

        _settingsService = new Mock<ISettingsService>();
        _emailService = new Mock<IEmailService>();
        _encryptionService = new Mock<IEncryptionService>();
        _environment = new Mock<IWebHostEnvironment>();
        _authProc = new Mock<IAuthProcedures>();
        _redis = new Mock<IConnectionMultiplexer>();

        var redisDb = new Mock<IDatabase>();
        redisDb.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        _redis.Setup(r => r.GetDatabase(-1, null)).Returns(redisDb.Object);

        _userRepoMock = new Mock<IUserRepository>();

        _settingsService.Setup(s => s.GetOrDefaultAsync("magic_link_expiry_minutes", "15", It.IsAny<CancellationToken>()))
            .ReturnsAsync("15");
        _settingsService.Setup(s => s.GetOrDefaultAsync("frontend_url", "http://localhost:5173", It.IsAny<CancellationToken>()))
            .ReturnsAsync("http://localhost:5173");
        _settingsService.Setup(s => s.GetOrDefaultAsync("app_name", "Code829", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Code829");
        _encryptionService.Setup(e => e.HashEmail(It.IsAny<string>()))
            .Returns((string email) => email.GetHashCode().ToString());

        _authProc.Setup(a => a.CreateMagicLinkAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var fileStorage = new Mock<IFileStorageService>();
        var jwtService = new Mock<IJwtService>();
        jwtService.Setup(j => j.GenerateUserJwtAsync(It.IsAny<Db.Entities.User>()))
            .ReturnsAsync("test-jwt-token");
        var userProc = new Mock<IUserProcedures>();
        var secretsProvider = new Mock<ISecretsProvider>();
        var imageService = new Mock<IImageService>();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        _service = new AuthService(
            _context, _userRepoMock.Object, _authProc.Object, userProc.Object, _settingsService.Object,
            _emailService.Object, _encryptionService.Object, _environment.Object,
            fileStorage.Object, _redis.Object, jwtService.Object,
            secretsProvider.Object, imageService.Object, httpClientFactory.Object);
    }

    [Fact]
    public async Task SendMagicLinkAsync_StoresHashedToken_NotRawToken()
    {
        _environment.Setup(e => e.EnvironmentName).Returns("Production");
        _emailService.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.SendMagicLinkAsync("test@example.com");

        _authProc.Verify(a => a.CreateMagicLinkAsync(
            "test@example.com",
            It.Is<string>(h => h.Length == 64 && Regex.IsMatch(h, "^[0-9a-f]+$")),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyMagicLinkAsync_WithExpiredToken_ThrowsUnauthorizedAccessException()
    {
        _authProc.Setup(a => a.ConsumeMagicLinkAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MagicLinkResult?)null);

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var act = () => _service.VerifyMagicLinkAsync(rawToken, "Chrome on Windows", "127.0.0.1");
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task VerifyMagicLinkAsync_WithUsedToken_ThrowsUnauthorizedAccessException()
    {
        _authProc.Setup(a => a.ConsumeMagicLinkAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MagicLinkResult?)null);

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var act = () => _service.VerifyMagicLinkAsync(rawToken, "Chrome on Windows", "127.0.0.1");
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task VerifyMagicLinkAsync_ValidToken_CreatesUserIfNotExists()
    {
        var userId = Guid.NewGuid();
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        _authProc.Setup(a => a.ConsumeMagicLinkAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MagicLinkResult(Guid.NewGuid(), "newuser@example.com", DateTime.UtcNow.AddMinutes(15)));

        _authProc.Setup(a => a.UpsertUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(userId);

        _authProc.Setup(a => a.CreateDeviceSessionAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _userRepoMock.Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync(new User
            {
                Id = userId,
                Email = "newuser@example.com",
                EmailHash = "testhash",
                FirstName = "newuser",
                LastName = ""
            });

        var result = await _service.VerifyMagicLinkAsync(rawToken, "Chrome on Windows", "127.0.0.1");

        result.User.Should().NotBeNull();
        result.User.Email.Should().Be("newuser@example.com");
    }

    [Fact]
    public async Task VerifyMagicLinkAsync_DoubleConsumption_ReturnsNull()
    {

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var userId = Guid.NewGuid();

        var callCount = 0;
        _authProc.Setup(a => a.ConsumeMagicLinkAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? new MagicLinkResult(Guid.NewGuid(), "user@example.com", DateTime.UtcNow.AddMinutes(15))
                    : null;
            });

        _authProc.Setup(a => a.UpsertUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(userId);

        _authProc.Setup(a => a.CreateDeviceSessionAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _userRepoMock.Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync(new User { Id = userId, Email = "user@example.com", EmailHash = "h", FirstName = "u", LastName = "" });

        await _service.VerifyMagicLinkAsync(rawToken, "Chrome", "127.0.0.1");

        var secondCall = () => _service.VerifyMagicLinkAsync(rawToken, "Chrome", "127.0.0.1");
        await secondCall.Should().ThrowAsync<UnauthorizedAccessException>();
    }

#if DEBUG
    [Fact]
    public async Task DevLoginAsync_InProduction_ThrowsInvalidOperationException()
    {
        _environment.Setup(e => e.EnvironmentName).Returns("Production");

        var act = () => _service.DevLoginAsync("test@example.com", "Chrome on Windows", "127.0.0.1");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not available*");
    }
#endif

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
