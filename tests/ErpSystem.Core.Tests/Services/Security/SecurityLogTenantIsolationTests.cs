using System.Linq.Expressions;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Security;

public sealed class SecurityLogTenantIsolationTests
{
    [Fact]
    public async Task GetSecurityLogByIdAsync_DoesNotReturnAnotherTenantsRecord()
    {
        var currentTenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var currentLog = NewLog(currentTenantId);
        var otherLog = NewLog(otherTenantId);
        var rows = new[] { currentLog, otherLog };

        var repository = new Mock<IGenericRepository<SecurityLog>>();
        repository
            .Setup(value => value.FirstOrDefaultAsync(It.IsAny<Expression<Func<SecurityLog, bool>>>() ))
            .ReturnsAsync((Expression<Func<SecurityLog, bool>> predicate) => rows.FirstOrDefault(predicate.Compile()));

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(value => value.Repository<SecurityLog>()).Returns(repository.Object);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(value => value.TenantId).Returns(currentTenantId);

        var service = new SecurityLogService(
            unitOfWork.Object,
            NullLogger<SecurityLogService>.Instance,
            currentUser.Object);

        (await service.GetSecurityLogByIdAsync(currentLog.Id)).Should().NotBeNull();
        (await service.GetSecurityLogByIdAsync(otherLog.Id)).Should().BeNull();
    }

    [Fact]
    public async Task GetSecurityLogsAsync_AlwaysPassesTheCurrentTenantPredicate()
    {
        var currentTenantId = Guid.NewGuid();
        Expression<Func<SecurityLog, bool>>? captured = null;
        var repository = new Mock<IGenericRepository<SecurityLog>>();
        repository
            .Setup(value => value.GetPagedAsync(
                1,
                100,
                It.IsAny<Expression<Func<SecurityLog, bool>>>(),
                It.IsAny<Expression<Func<SecurityLog, DateTime>>>(),
                true))
            .Callback((int _, int _, Expression<Func<SecurityLog, bool>> predicate, Expression<Func<SecurityLog, DateTime>> _, bool _) => captured = predicate)
            .ReturnsAsync(Array.Empty<SecurityLog>());

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(value => value.Repository<SecurityLog>()).Returns(repository.Object);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(value => value.TenantId).Returns(currentTenantId);

        var service = new SecurityLogService(
            unitOfWork.Object,
            NullLogger<SecurityLogService>.Instance,
            currentUser.Object);

        await service.GetSecurityLogsAsync();

        captured.Should().NotBeNull();
        captured!.Compile()(NewLog(currentTenantId)).Should().BeTrue();
        captured.Compile()(NewLog(Guid.NewGuid())).Should().BeFalse();
    }

    private static SecurityLog NewLog(Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Action = "LoginFailure",
        IpAddress = "127.0.0.1",
        Timestamp = DateTime.UtcNow
    };
}
