using System.Linq.Expressions;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Settings;

public sealed class SettingsServiceTenantTests
{
    [Fact]
    public async Task GetEmailSettingsAsync_SelectsOnlyTheCurrentTenantsActiveRow()
    {
        var tenantId = Guid.NewGuid();
        var expected = EmailSettingsFor(tenantId);
        var rows = new[] { EmailSettingsFor(Guid.NewGuid()), expected };
        var repository = new Mock<IGenericRepository<EmailSettings>>();
        repository.Setup(repo => repo.FirstOrDefaultAsync(It.IsAny<Expression<Func<EmailSettings, bool>>>() ))
            .ReturnsAsync((Expression<Func<EmailSettings, bool>> predicate) => rows.SingleOrDefault(predicate.Compile()));
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(work => work.Repository<EmailSettings>()).Returns(repository.Object);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.TenantId).Returns(tenantId);
        var service = new SettingsService(
            unitOfWork.Object,
            currentUser.Object,
            Mock.Of<ICryptoService>(),
            NullLogger<SettingsService>.Instance);

        var result = await service.GetEmailSettingsAsync();

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetEmailSettingsAsync_DoesNotFallBackToAnotherTenantForAnonymousRequests()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.TenantId).Returns((Guid?)null);
        var service = new SettingsService(
            unitOfWork.Object,
            currentUser.Object,
            Mock.Of<ICryptoService>(),
            NullLogger<SettingsService>.Instance);

        var result = await service.GetEmailSettingsAsync();

        result.Should().BeNull();
        unitOfWork.Verify(work => work.Repository<EmailSettings>(), Times.Never);
    }

    private static EmailSettings EmailSettingsFor(Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        SmtpHost = "smtp.example.test",
        FromAddress = "noreply@example.test",
        FromName = "Rhema ERP"
    };
}
