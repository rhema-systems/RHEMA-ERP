using System.Linq.Expressions;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using SecuritySettingsEntity = ErpSystem.Core.Entities.Security;

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

    [Fact]
    public async Task UpdateLoginPageStyleAsync_ChangesOnlyAppearanceOnExistingSecuritySettings()
    {
        var tenantId = Guid.NewGuid();
        var existing = new SecuritySettingsEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LoginPageStyle = LoginPageStyle.LightCorporate,
            PasswordMinLength = 11,
            CaptchaEnabled = true
        };
        var repository = new Mock<IGenericRepository<SecuritySettingsEntity>>();
        repository
            .Setup(repo => repo.FindAsync(It.IsAny<Expression<Func<SecuritySettingsEntity, bool>>>() ))
            .ReturnsAsync((Expression<Func<SecuritySettingsEntity, bool>> predicate) =>
                new[] { existing }.Where(predicate.Compile()));
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(work => work.Repository<SecuritySettingsEntity>()).Returns(repository.Object);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.TenantId).Returns(tenantId);
        currentUser.SetupGet(user => user.UserName).Returns("security.admin");
        var service = new SettingsService(
            unitOfWork.Object,
            currentUser.Object,
            Mock.Of<ICryptoService>(),
            NullLogger<SettingsService>.Instance);

        var result = await service.UpdateLoginPageStyleAsync(LoginPageStyle.DarkPremium);

        result.Should().BeSameAs(existing);
        result.LoginPageStyle.Should().Be(LoginPageStyle.DarkPremium);
        result.PasswordMinLength.Should().Be(11);
        result.CaptchaEnabled.Should().BeTrue();
        result.UpdatedBy.Should().Be("security.admin");
        repository.Verify(repo => repo.UpdateAsync(existing), Times.Once);
        unitOfWork.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateLoginPageStyleAsync_CreatesTenantSecuritySettingsWhenMissing()
    {
        var tenantId = Guid.NewGuid();
        var repository = new Mock<IGenericRepository<SecuritySettingsEntity>>();
        repository
            .Setup(repo => repo.FindAsync(It.IsAny<Expression<Func<SecuritySettingsEntity, bool>>>() ))
            .ReturnsAsync(Array.Empty<SecuritySettingsEntity>());
        repository
            .Setup(repo => repo.AddAsync(It.IsAny<SecuritySettingsEntity>()))
            .ReturnsAsync((SecuritySettingsEntity item) => item);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(work => work.Repository<SecuritySettingsEntity>()).Returns(repository.Object);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.TenantId).Returns(tenantId);
        currentUser.SetupGet(user => user.UserName).Returns("security.admin");
        var service = new SettingsService(
            unitOfWork.Object,
            currentUser.Object,
            Mock.Of<ICryptoService>(),
            NullLogger<SettingsService>.Instance);

        var result = await service.UpdateLoginPageStyleAsync(LoginPageStyle.DarkPremium);

        result.TenantId.Should().Be(tenantId);
        result.LoginPageStyle.Should().Be(LoginPageStyle.DarkPremium);
        result.CreatedBy.Should().Be("security.admin");
        repository.Verify(repo => repo.AddAsync(It.Is<SecuritySettingsEntity>(item =>
            item.TenantId == tenantId && item.LoginPageStyle == LoginPageStyle.DarkPremium)), Times.Once);
        unitOfWork.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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
