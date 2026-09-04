using ErpSystem.Api.Services;
using ErpSystem.Api.Services.Sms;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public sealed class UnifiedNotificationServiceTests
{
    [Fact]
    public async Task SendEmailThrowsWhenProviderReportsFailure()
    {
        await using var context = Context();
        using var unitOfWork = new UnitOfWork(context);
        var email = new Mock<IEmailService>();
        email.Setup(item => item.SendEmailAsync(It.IsAny<EmailDto>()))
            .ReturnsAsync(false);
        var service = Service(context, unitOfWork, email);

        var action = () => service.SendEmailAsync(
            "recipient@example.invalid",
            "Delivery contract test",
            "Non-sensitive test content.");

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*provider reported*not sent*");
        email.Verify(item => item.SendEmailAsync(It.IsAny<EmailDto>()), Times.Once);
    }

    [Fact]
    public async Task SendEmailWithAttachmentsThrowsWhenProviderReportsFailure()
    {
        await using var context = Context();
        using var unitOfWork = new UnitOfWork(context);
        var email = new Mock<IEmailService>();
        email.Setup(item => item.SendEmailAsync(It.IsAny<EmailDto>()))
            .ReturnsAsync(false);
        var service = Service(context, unitOfWork, email);

        var action = () => service.SendEmailWithAttachmentsAsync(
            "recipient@example.invalid",
            "Attachment delivery contract test",
            "Non-sensitive test content.",
            []);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*provider reported*not sent*");
        email.Verify(item => item.SendEmailAsync(It.IsAny<EmailDto>()), Times.Once);
    }

    [Fact]
    public async Task SensitiveEmailSendsProviderBodyButRedactsPersistedAuditBody()
    {
        await using var context = Context();
        using var unitOfWork = new UnitOfWork(context);
        var tenantId = Guid.NewGuid();
        var email = new Mock<IEmailService>();
        EmailDto? delivered = null;
        email.Setup(item => item.SendEmailAsync(It.IsAny<EmailDto>()))
            .Callback<EmailDto>(item => delivered = item)
            .ReturnsAsync(true);
        var service = Service(context, unitOfWork, email, tenantId);
        const string sensitiveBody = "One-time secret that must not be persisted.";

        await service.SendEmailAsync(
            "recipient@example.invalid",
            "Sensitive delivery contract test",
            sensitiveBody,
            isHtml: true,
            persistBody: false);

        delivered.Should().NotBeNull();
        delivered!.Body.Should().Be(sensitiveBody);
        var audit = await context.Set<ErpSystem.Core.Entities.Notification>().SingleAsync();
        audit.Message.Should().NotContain(sensitiveBody);
        audit.Message.Should().Contain("omitted");
        audit.Status.Should().Be("Sent");
    }

    private static UnifiedNotificationService Service(
        ApplicationDbContext context,
        IUnitOfWork unitOfWork,
        Mock<IEmailService> email,
        Guid? tenantId = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        return new UnifiedNotificationService(
            unitOfWork,
            email.Object,
            Mock.Of<ISmsSender>(),
            Mock.Of<ITenantSmsSender>(),
            currentUser.Object,
            Mock.Of<IHubNotificationService>(),
            context,
            new ConfigurationBuilder().Build(),
            NullLogger<UnifiedNotificationService>.Instance);
    }

    private static ApplicationDbContext Context()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
