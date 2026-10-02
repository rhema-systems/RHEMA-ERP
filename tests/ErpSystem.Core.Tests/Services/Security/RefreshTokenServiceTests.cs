using ErpSystem.Core.Services;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Security;

public sealed class RefreshTokenServiceTests
{
    [Fact]
    public async Task CreatedTokenHasFixedThirtyDayExpiryAndOnlyItsHashIsPersisted()
    {
        await using var context = Context();
        using var unitOfWork = new UnitOfWork(context);
        var service = new RefreshTokenService(unitOfWork, NullLogger<RefreshTokenService>.Instance);
        var before = DateTime.UtcNow;

        var issued = await service.CreateRefreshTokenAsync(Guid.NewGuid(), Guid.NewGuid(), "127.0.0.1", "test-agent", "device");
        var rawToken = issued.TokenHash;
        var persisted = await context.RefreshTokens.AsNoTracking().SingleAsync();

        rawToken.Should().NotBeNullOrWhiteSpace();
        persisted.TokenHash.Should().NotBe(rawToken);
        persisted.ExpiresAt.Should().BeOnOrAfter(before.AddDays(30).AddSeconds(-1));
        persisted.ExpiresAt.Should().BeOnOrBefore(DateTime.UtcNow.AddDays(30).AddSeconds(1));
        persisted.MaxUsageCount.Should().Be(0);
    }

    [Fact]
    public async Task ExistingPolicyAllowsReuseUntilTheFixedTokenIsRevoked()
    {
        await using var context = Context();
        using var unitOfWork = new UnitOfWork(context);
        var service = new RefreshTokenService(unitOfWork, NullLogger<RefreshTokenService>.Instance);
        var userId = Guid.NewGuid();
        var issued = await service.CreateRefreshTokenAsync(userId, Guid.NewGuid());
        var rawToken = issued.TokenHash;

        var firstUse = await service.GetValidRefreshTokenAsync(rawToken);
        firstUse.Should().NotBeNull();
        await service.MarkRefreshTokenAsUsedAsync(firstUse!);
        var secondUse = await service.GetValidRefreshTokenAsync(rawToken);
        secondUse.Should().NotBeNull();
        await service.MarkRefreshTokenAsUsedAsync(secondUse!);

        (await context.RefreshTokens.AsNoTracking().SingleAsync()).UsageCount.Should().Be(2);
        (await service.RevokeRefreshTokenAsync(rawToken, userId, "logout")).Should().BeTrue();
        (await service.GetValidRefreshTokenAsync(rawToken)).Should().BeNull();
    }

    private static ApplicationDbContext Context() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
}
