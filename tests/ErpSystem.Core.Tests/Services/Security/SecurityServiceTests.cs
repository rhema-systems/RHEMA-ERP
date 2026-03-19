using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Security;

public class SecurityServiceTests
{
    [Fact]
    public async Task EnableTwoFactorAsync_ShouldUseLdapAuthentication_ForLdapUsers()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            TenantId = tenantId,
            UserName = "ldap.user",
            Email = "ldap.user@example.com",
            AuthenticationProvider = AuthenticationProvider.LDAP,
            FirstName = "Ldap",
            LastName = "User"
        };
        var tenant = new Tenant
        {
            Id = tenantId,
            Code = "DEFAULT",
            Name = "Default Tenant",
            LdapEnabled = true,
            LdapServer = "ldap.example.com",
            LdapBaseDn = "dc=example,dc=com"
        };

        var unitOfWork = new Mock<IUnitOfWork>();
        var tenantRepository = new Mock<IGenericRepository<Tenant>>();
        tenantRepository.Setup(x => x.GetByIdAsync(tenantId)).ReturnsAsync(tenant);
        unitOfWork.Setup(x => x.Repository<Tenant>()).Returns(tenantRepository.Object);

        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(x => x.UserId).Returns(userId.ToString());
        currentUserService.SetupGet(x => x.TenantId).Returns(tenantId);

        var userService = new Mock<IUserService>();
        userService.Setup(x => x.GetUserByIdAsync(userId)).ReturnsAsync(user);
        userService.Setup(x => x.UpdateUserAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync((ApplicationUser updatedUser) => updatedUser);

        var ldapAuthenticationService = new Mock<ILdapAuthenticationService>();
        ldapAuthenticationService
            .Setup(x => x.AuthenticateAsync(user.UserName!, "CorrectPassword!", tenant))
            .ReturnsAsync(new LdapAuthenticationResult
            {
                Success = true,
                User = new LdapUser
                {
                    Username = user.UserName!,
                    Email = user.Email ?? string.Empty,
                    DisplayName = user.FullName
                }
            });

        var twoFactorAuthService = new Mock<ITwoFactorAuthService>();
        twoFactorAuthService
            .Setup(x => x.SetupTwoFactorAsync(user, "ERP System"))
            .ReturnsAsync(new TwoFactorSetupResult
            {
                SecretKey = "SECRETKEY",
                ManualEntryKey = "SECRET KEY",
                QrCodeDataUri = "otpauth://totp/test",
                RecoveryCodes = new List<string> { "code-1", "code-2" }
            });

        var service = new SecurityService(
            unitOfWork.Object,
            currentUserService.Object,
            userService.Object,
            ldapAuthenticationService.Object,
            Mock.Of<IUserSessionService>(),
            twoFactorAuthService.Object,
            Mock.Of<IDeviceSessionService>(),
            NullLogger<SecurityService>.Instance);

        var result = await service.EnableTwoFactorAsync(new EnableTwoFactorRequest
        {
            Password = "CorrectPassword!",
            VerificationCode = string.Empty
        });

        ldapAuthenticationService.Verify(
            x => x.AuthenticateAsync(user.UserName!, "CorrectPassword!", tenant),
            Times.Once);
        twoFactorAuthService.Verify(x => x.SetupTwoFactorAsync(user, "ERP System"), Times.Once);
        result.AuthenticatorKey.Should().Be("SECRET KEY");
        result.RecoveryCodes.Should().Contain("code-1");
    }

    [Fact]
    public async Task EnableTwoFactorAsync_ShouldThrowIncorrectPassword_WhenLdapAuthenticationFails()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            TenantId = tenantId,
            UserName = "ldap.user",
            AuthenticationProvider = AuthenticationProvider.LDAP,
            FirstName = "Ldap",
            LastName = "User"
        };
        var tenant = new Tenant
        {
            Id = tenantId,
            Code = "DEFAULT",
            Name = "Default Tenant",
            LdapEnabled = true,
            LdapServer = "ldap.example.com",
            LdapBaseDn = "dc=example,dc=com"
        };

        var unitOfWork = new Mock<IUnitOfWork>();
        var tenantRepository = new Mock<IGenericRepository<Tenant>>();
        tenantRepository.Setup(x => x.GetByIdAsync(tenantId)).ReturnsAsync(tenant);
        unitOfWork.Setup(x => x.Repository<Tenant>()).Returns(tenantRepository.Object);

        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(x => x.UserId).Returns(userId.ToString());
        currentUserService.SetupGet(x => x.TenantId).Returns(tenantId);

        var userService = new Mock<IUserService>();
        userService.Setup(x => x.GetUserByIdAsync(userId)).ReturnsAsync(user);

        var ldapAuthenticationService = new Mock<ILdapAuthenticationService>();
        ldapAuthenticationService
            .Setup(x => x.AuthenticateAsync(user.UserName!, "WrongPassword!", tenant))
            .ReturnsAsync(new LdapAuthenticationResult
            {
                Success = false,
                ErrorMessage = "Invalid username or password"
            });

        var service = new SecurityService(
            unitOfWork.Object,
            currentUserService.Object,
            userService.Object,
            ldapAuthenticationService.Object,
            Mock.Of<IUserSessionService>(),
            Mock.Of<ITwoFactorAuthService>(),
            Mock.Of<IDeviceSessionService>(),
            NullLogger<SecurityService>.Instance);

        var act = () => service.EnableTwoFactorAsync(new EnableTwoFactorRequest
        {
            Password = "WrongPassword!",
            VerificationCode = string.Empty
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Incorrect password. Please check your password and try again.");
        ldapAuthenticationService.Verify(
            x => x.AuthenticateAsync(user.UserName!, "WrongPassword!", tenant),
            Times.Once);
    }
}
