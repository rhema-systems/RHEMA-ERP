using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Security;

public class JwtBlacklistServiceTests
{
    [Fact]
    public async Task BlacklistTokenAsync_ShouldTreatDuplicateJtiInsertAsSuccess()
    {
        var repository = new Mock<IGenericRepository<BlacklistedToken>>();
        repository
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<BlacklistedToken, bool>>>()))
            .ReturnsAsync((BlacklistedToken?)null);
        repository
            .Setup(x => x.AddAsync(It.IsAny<BlacklistedToken>()))
            .ReturnsAsync((BlacklistedToken token) => token);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.Repository<BlacklistedToken>()).Returns(repository.Object);
        unitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException(
                "An error occurred while saving the entity changes. See the inner exception for details.",
                new InvalidOperationException(
                    "Cannot insert duplicate key row in object 'dbo.BlacklistedTokens' with unique index 'IX_BlacklistedTokens_Jti'.")));

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = new JwtBlacklistService(unitOfWork.Object, memoryCache, NullLogger<JwtBlacklistService>.Instance);

        var act = async () => await service.BlacklistTokenAsync(
            Guid.NewGuid().ToString(),
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(1),
            "User logout");

        await act.Should().NotThrowAsync();
        repository.Verify(x => x.AddAsync(It.IsAny<BlacklistedToken>()), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
