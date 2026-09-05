using System.Text.Json;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Security;

public sealed class InternalUserTemporaryPasswordResetServiceTests
{
    [Fact]
    public async Task ResetRejectsTargetOutsideActorsTenantWithoutTouchingCredential()
    {
        var tenantId = Guid.NewGuid();
        var target = User(Guid.NewGuid());
        var fixture = Fixture(target);
        fixture.UserTenants.Setup(item => item.HasActiveAccessAsync(target.Id, tenantId))
            .ReturnsAsync(false);

        var result = await fixture.Service.ResetAsync(Command(target.Id, tenantId));

        result.Succeeded.Should().BeFalse();
        result.Code.Should().Be("INTERNAL_USER_NOT_FOUND");
        fixture.UserManager.Verify(item => item.ResetPasswordAsync(
            It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ResetRejectsExternalRoleAndLegacyBusinessPartnerUsers(bool externalRole)
    {
        var tenantId = Guid.NewGuid();
        var target = User(tenantId);
        var fixture = Fixture(target);
        fixture.UserManager.Setup(item => item.GetRolesAsync(target))
            .ReturnsAsync(externalRole ? new[] { Constants.Roles.ExternalUser } : Array.Empty<string>());
        fixture.BusinessPartnerUsers.Setup(item => item.GetByUserIdAsync(target.Id))
            .ReturnsAsync(externalRole ? null : new BusinessPartnerUser
            {
                Id = Guid.NewGuid(), UserId = target.Id, TenantId = tenantId,
                BusinessPartnerId = Guid.NewGuid()
            });

        var result = await fixture.Service.ResetAsync(Command(target.Id, tenantId));

        result.Code.Should().Be("INTERNAL_USER_EXTERNAL_RESET_PROHIBITED");
        fixture.UserManager.Verify(item => item.ResetPasswordAsync(
            It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResetRejectsPasswordThatViolatesTenantPolicy()
    {
        var tenantId = Guid.NewGuid();
        var target = User(tenantId);
        var fixture = Fixture(target);
        fixture.Settings.Setup(item => item.GetSecuritySettingsAsync(tenantId))
            .ReturnsAsync(new ErpSystem.Core.Entities.Security
            {
                PasswordMinLength = 12,
                PasswordRequireUppercase = true,
                PasswordRequireLowercase = true,
                PasswordRequireDigits = true,
                PasswordRequireSpecialChars = true
            });

        var result = await fixture.Service.ResetAsync(
            Command(target.Id, tenantId) with { NewPassword = "weak" });

        result.Code.Should().Be("TEMPORARY_PASSWORD_POLICY_FAILED");
        result.Errors.Should().NotBeEmpty();
        fixture.UserManager.Verify(item => item.GeneratePasswordResetTokenAsync(target), Times.Never);
    }

    [Fact]
    public async Task ResetUsesIdentitySetsBoundedExpiryInvalidatesSessionsAndAuditsWithoutPassword()
    {
        var tenantId = Guid.NewGuid();
        var target = User(tenantId);
        var fixture = Fixture(target);
        var command = Command(target.Id, tenantId);
        object? auditedValues = null;
        fixture.Audit.Setup(item => item.LogUserActionAsync(
                command.ActorUserId,
                command.ActorUserName,
                "ResetInternalUserTemporaryPassword",
                "User",
                target.Id.ToString(),
                null,
                It.IsAny<object>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<Guid, string, string, string, string?, object?, object?, string?, string?>(
                (_, _, _, _, _, _, values, _, _) => auditedValues = values)
            .Returns(Task.CompletedTask);

        var before = DateTime.UtcNow;
        var result = await fixture.Service.ResetAsync(command);
        var after = DateTime.UtcNow;

        result.Succeeded.Should().BeTrue();
        target.MustChangePassword.Should().BeTrue();
        target.TemporaryPasswordExpiresAtUtc.Should().BeOnOrAfter(
            before.AddHours(InternalUserTemporaryPasswordResetService.TemporaryPasswordLifetimeHours));
        target.TemporaryPasswordExpiresAtUtc.Should().BeOnOrBefore(
            after.AddHours(InternalUserTemporaryPasswordResetService.TemporaryPasswordLifetimeHours));
        fixture.UserManager.Verify(item => item.ResetPasswordAsync(
            target, "reset-token", command.NewPassword), Times.Once);
        fixture.Sessions.Verify(item => item.TerminateAllUserSessionsAsync(
            target.Id, null, "Administrative temporary-password reset"), Times.Once);
        auditedValues.Should().NotBeNull();
        JsonSerializer.Serialize(auditedValues).Should().NotContain(command.NewPassword);
    }

    private static TestFixture Fixture(ApplicationUser target)
    {
        var fixture = new TestFixture();
        fixture.UserManager.Setup(item => item.FindByIdAsync(target.Id.ToString()))
            .ReturnsAsync(target);
        fixture.UserManager.Setup(item => item.GetRolesAsync(target))
            .ReturnsAsync(Array.Empty<string>());
        fixture.BusinessPartnerUsers.Setup(item => item.GetByUserIdAsync(target.Id))
            .ReturnsAsync((BusinessPartnerUser?)null);
        fixture.Settings.Setup(item => item.GetSecuritySettingsAsync(target.TenantId))
            .ReturnsAsync(new ErpSystem.Core.Entities.Security
            {
                PasswordMinLength = 8,
                PasswordRequireUppercase = true,
                PasswordRequireLowercase = true,
                PasswordRequireDigits = true,
                PasswordRequireSpecialChars = true
            });
        fixture.UserManager.Setup(item => item.GeneratePasswordResetTokenAsync(target))
            .ReturnsAsync("reset-token");
        fixture.UserManager.Setup(item => item.ResetPasswordAsync(
                target, "reset-token", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        fixture.Sessions.Setup(item => item.TerminateAllUserSessionsAsync(
                It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        return fixture;
    }

    private static ApplicationUser User(Guid tenantId) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, UserName = "internal.user",
        FirstName = "Internal", LastName = "User", IsActive = true,
        AuthenticationProvider = AuthenticationProvider.Local
    };

    private static InternalUserTemporaryPasswordResetCommand Command(
        Guid targetUserId,
        Guid tenantId) => new(
            targetUserId,
            tenantId,
            Guid.NewGuid(),
            "tenant.admin",
            "Temporary#123",
            "Authorized support reset",
            "127.0.0.1",
            "test-agent");

    private sealed class TestFixture
    {
        public TestFixture()
        {
            var store = Mock.Of<IUserStore<ApplicationUser>>();
            UserManager = new Mock<UserManager<ApplicationUser>>(
                store,
                Options.Create(new IdentityOptions()),
                new PasswordHasher<ApplicationUser>(),
                Array.Empty<IUserValidator<ApplicationUser>>(),
                Array.Empty<IPasswordValidator<ApplicationUser>>(),
                Mock.Of<ILookupNormalizer>(),
                new IdentityErrorDescriber(),
                Mock.Of<IServiceProvider>(),
                NullLogger<UserManager<ApplicationUser>>.Instance);
            Service = new InternalUserTemporaryPasswordResetService(
                UserManager.Object,
                UserTenants.Object,
                BusinessPartnerUsers.Object,
                Settings.Object,
                Sessions.Object,
                Audit.Object,
                NullLogger<InternalUserTemporaryPasswordResetService>.Instance);
        }

        public Mock<UserManager<ApplicationUser>> UserManager { get; }
        public Mock<IUserTenantService> UserTenants { get; } = new();
        public Mock<IBusinessPartnerUserRepository> BusinessPartnerUsers { get; } = new();
        public Mock<ISettingsService> Settings { get; } = new();
        public Mock<IUserSessionService> Sessions { get; } = new();
        public Mock<IAuditLogService> Audit { get; } = new();
        public InternalUserTemporaryPasswordResetService Service { get; }
    }
}
