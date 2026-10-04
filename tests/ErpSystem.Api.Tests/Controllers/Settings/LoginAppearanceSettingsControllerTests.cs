using System.Security.Claims;
using ErpSystem.Api.Controllers;
using ErpSystem.Api.Services.Sms;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using SecuritySettingsEntity = ErpSystem.Core.Entities.Security;

namespace ErpSystem.Api.Tests.Controllers.Settings;

public sealed class LoginAppearanceSettingsControllerTests
{
    [Fact]
    public async Task GetLoginAppearance_UsesLightCorporateFallbackWhenSettingsDoNotExist()
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetSecuritySettingsAsync()).ReturnsAsync((SecuritySettingsEntity?)null);
        var controller = CreateController(settings);

        var response = await controller.GetLoginAppearance();

        response.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(new LoginAppearanceSettingsDto
            {
                LoginPageStyle = "LightCorporate"
            });
    }

    [Fact]
    public async Task UpdateLoginAppearance_RejectsUnsupportedStyleWithoutWriting()
    {
        var settings = new Mock<ISettingsService>();
        var controller = CreateController(settings);

        var response = await controller.UpdateLoginAppearance(new LoginAppearanceSettingsDto
        {
            LoginPageStyle = "PurpleWave"
        });

        response.Result.Should().BeOfType<BadRequestObjectResult>();
        settings.Verify(service => service.UpdateLoginPageStyleAsync(It.IsAny<LoginPageStyle>()), Times.Never);
    }

    [Fact]
    public async Task UpdateLoginAppearance_PersistsAndAuditsTheChange()
    {
        var tenantId = Guid.NewGuid();
        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetSecuritySettingsAsync()).ReturnsAsync(new SecuritySettingsEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LoginPageStyle = LoginPageStyle.LightCorporate
        });
        settings.Setup(service => service.UpdateLoginPageStyleAsync(LoginPageStyle.DarkPremium))
            .ReturnsAsync(new SecuritySettingsEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LoginPageStyle = LoginPageStyle.DarkPremium
            });
        var audit = new Mock<IAuditLogService>();
        audit.Setup(service => service.CreateAuditLogAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync((AuditLog item) => item);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        var controller = CreateController(settings, audit, currentUser);

        var response = await controller.UpdateLoginAppearance(new LoginAppearanceSettingsDto
        {
            LoginPageStyle = "DarkPremium"
        });

        response.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(new LoginAppearanceSettingsDto
            {
                LoginPageStyle = "DarkPremium"
            });
        settings.Verify(service => service.UpdateLoginPageStyleAsync(LoginPageStyle.DarkPremium), Times.Once);
        audit.Verify(service => service.CreateAuditLogAsync(It.Is<AuditLog>(item =>
            item.TenantId == tenantId &&
            item.Resource == "LoginPageAppearance" &&
            item.OldValues!.Contains("LightCorporate") &&
            item.NewValues!.Contains("DarkPremium"))), Times.Once);
    }

    [Theory]
    [InlineData(nameof(SettingsController.GetLoginAppearance))]
    [InlineData(nameof(SettingsController.UpdateLoginAppearance))]
    public void LoginAppearanceEndpoints_RequireSecurityAdministratorRole(string actionName)
    {
        var attribute = typeof(SettingsController).GetMethod(actionName)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attribute.Roles.Should().Contain(Constants.Roles.TenantAdmin);
        attribute.Roles.Should().Contain(Constants.Roles.SuperAdmin);
    }

    private static SettingsController CreateController(
        Mock<ISettingsService> settings,
        Mock<IAuditLogService>? audit = null,
        Mock<ICurrentUserService>? currentUser = null)
    {
        var userId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, "security.admin")
            }, "Test"))
        };

        return new SettingsController(
            settings.Object,
            (audit ?? new Mock<IAuditLogService>()).Object,
            (currentUser ?? new Mock<ICurrentUserService>()).Object,
            Mock.Of<ITenantSmsSender>(),
            NullLogger<SettingsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }
}
