using ErpSystem.Api.Controllers;
using ErpSystem.Api.Services.Sms;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Settings;

public sealed class SmsSettingsControllerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendTestSms_UsesRequestedStandardOrVerificationMessage(bool isOtp)
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        var sender = new Mock<ITenantSmsSender>();
        var controller = new SettingsController(
            Mock.Of<ISettingsService>(),
            Mock.Of<IAuditLogService>(),
            currentUser.Object,
            sender.Object,
            NullLogger<SettingsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = await controller.SendTestSms(new SendTestSmsRequestDto
        {
            PhoneNumber = "+233241234567",
            IsOtp = isOtp
        });

        response.Result.Should().BeOfType<OkObjectResult>();
        if (isOtp)
        {
            sender.Verify(service => service.SendOtpAsync(
                tenantId,
                "+233241234567",
                It.Is<string>(message => message.Contains("123456")),
                It.IsAny<CancellationToken>()), Times.Once);
            sender.Verify(service => service.SendAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }
        else
        {
            sender.Verify(service => service.SendAsync(
                tenantId,
                "+233241234567",
                It.Is<string>(message => message.Contains("test SMS")),
                It.IsAny<CancellationToken>()), Times.Once);
            sender.Verify(service => service.SendOtpAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
