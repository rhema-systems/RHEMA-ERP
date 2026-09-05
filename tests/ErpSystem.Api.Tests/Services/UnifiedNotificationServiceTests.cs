using ErpSystem.Api.Services;
using ErpSystem.Api.Controllers;
using ErpSystem.Api.Services.Sms;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Services;
using ErpSystem.Core.Services.Maintenance;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
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
        Guid? tenantId = null,
        ILogger<UnifiedNotificationService>? logger = null)
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
            logger ?? NullLogger<UnifiedNotificationService>.Instance);
    }

    private static ApplicationDbContext Context()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DispatcherCannotRaceAnInFlightDirectSend(bool persistBody)
    {
        await using var context = Context();
        using var unitOfWork = new UnitOfWork(context);
        var email = new Mock<IEmailService>();
        var logger = new RecordingLogger<UnifiedNotificationService>();
        UnifiedNotificationService? service = null;
        const string body = "Test-only direct message, not a real credential.";
        email.Setup(item => item.SendEmailAsync(It.IsAny<EmailDto>()))
            .Returns<EmailDto>(async delivered =>
            {
                delivered.Body.Should().Be(body);
                var audit = await context.Notifications.SingleAsync();
                audit.Status.Should().Be("Sending");
                await service!.ProcessPendingNotificationsAsync();
                logger.Levels.Should().NotContain(LogLevel.Error);
                audit.SentAt.Should().BeNull();
                return true;
            });
        service = Service(context, unitOfWork, email, Guid.NewGuid(), logger);

        await service.SendEmailAsync("recipient@example.invalid", "Race test", body, true, persistBody);

        email.Verify(item => item.SendEmailAsync(It.IsAny<EmailDto>()), Times.Once);
        var completed = await context.Notifications.SingleAsync();
        completed.Status.Should().Be("Sent");
        completed.AttemptCount.Should().Be(1);
        if (!persistBody)
        {
            completed.Message.Should().Be(UnifiedNotificationService.RedactedEmailAuditBody);
            completed.AdditionalData.Should().NotContain(body);
        }
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Failed")]
    [InlineData("Processing")]
    public async Task DispatcherNeverReplaysLegacyRedactedAuditRows(string status)
    {
        await using var context = Context();
        using var unitOfWork = new UnitOfWork(context);
        var tenantId = Guid.NewGuid();
        var audit = Audit(tenantId, status, UnifiedNotificationService.RedactedEmailAuditBody);
        context.Notifications.Add(audit);
        await context.SaveChangesAsync();
        var email = new Mock<IEmailService>(MockBehavior.Strict);
        var logger = new RecordingLogger<UnifiedNotificationService>();

        await Service(context, unitOfWork, email, tenantId, logger).ProcessPendingNotificationsAsync();

        email.Verify(item => item.SendEmailAsync(It.IsAny<EmailDto>()), Times.Never);
        logger.Levels.Should().NotContain(LogLevel.Error);
        audit.Status.Should().Be(status);
        audit.SentAt.Should().BeNull();
        audit.AttemptCount.Should().Be(0);
    }

    [Fact]
    public async Task FailedSensitiveDirectSendCannotBeReplayedAsAuditText()
    {
        await using var context = Context();
        using var unitOfWork = new UnitOfWork(context);
        var email = new Mock<IEmailService>();
        email.Setup(item => item.SendEmailAsync(It.IsAny<EmailDto>())).ReturnsAsync(false);
        var logger = new RecordingLogger<UnifiedNotificationService>();
        var service = Service(context, unitOfWork, email, Guid.NewGuid(), logger);
        const string body = "Test-only fresh secret; must remain outside audit storage.";
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendEmailAsync(
            "recipient@example.invalid", "Sensitive failure", body, true, false));
        logger.Levels.Clear();

        await service.ProcessPendingNotificationsAsync();

        email.Verify(item => item.SendEmailAsync(It.IsAny<EmailDto>()), Times.Once);
        logger.Levels.Should().NotContain(LogLevel.Error);
        var audit = await context.Notifications.SingleAsync();
        audit.Status.Should().Be("Failed");
        audit.Message.Should().Be(UnifiedNotificationService.RedactedEmailAuditBody);
        audit.AdditionalData.Should().NotContain(body);
        audit.SentAt.Should().BeNull();
        audit.AttemptCount.Should().Be(1);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ManualRetryProtectsRedactedContentAndTenantBoundary(bool redacted, bool sameTenant)
    {
        await using var context = Context();
        using var unitOfWork = new UnitOfWork(context);
        var tenantId = Guid.NewGuid();
        var audit = Audit(tenantId, "Failed", redacted
            ? UnifiedNotificationService.RedactedEmailAuditBody : "Ordinary queued test email.");
        context.Notifications.Add(audit);
        await context.SaveChangesAsync();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(sameTenant ? tenantId : Guid.NewGuid());
        var email = new Mock<IEmailService>();
        email.Setup(item => item.SendEmailAsync(It.IsAny<EmailDto>())).ReturnsAsync(true);
        var controller = new NotificationMonitoringController(
            Mock.Of<IDeadLetterNotificationService>(), Mock.Of<IMaintenanceEscalationService>(),
            Mock.Of<IMaintenanceNotificationTemplateService>(), unitOfWork, currentUser.Object,
            context, email.Object, Mock.Of<IHubNotificationService>(), new ConfigurationBuilder().Build(),
            NullLogger<NotificationMonitoringController>.Instance);

        var result = await controller.RetryMessageQueueItem(audit.Id);

        if (!sameTenant) result.Should().BeOfType<NotFoundResult>();
        else if (redacted) result.Should().BeOfType<BadRequestObjectResult>();
        else result.Should().BeOfType<OkObjectResult>();
        email.Verify(item => item.SendEmailAsync(It.IsAny<EmailDto>()),
            sameTenant && !redacted ? Times.Once() : Times.Never());
        audit.Status.Should().Be(sameTenant && !redacted ? "Sent" : "Failed");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingSmtpCannotReportSentOrLogSensitiveBody(bool withAttachments)
    {
        var settings = new Mock<ISettingsService>();
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(item => item.EnvironmentName).Returns("Development");
        var logger = new RecordingLogger<ErpSystem.Web.Services.ProductionEmailService>();
        var service = new ErpSystem.Web.Services.ProductionEmailService(logger, settings.Object, environment.Object);
        const string body = "SENSITIVE-TEST-MARKER-NOT-A-REAL-TOKEN";

        var result = withAttachments
            ? await service.SendEmailWithAttachmentsAsync("recipient@example.invalid", "No SMTP test", body, [], true)
            : await service.SendEmailAsync("recipient@example.invalid", "No SMTP test", body, true);

        result.Should().BeFalse();
        string.Join("\n", logger.Messages).Should().NotContain(body);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StalledSmtpHasABoundedAsyncDeadline(bool withAttachments)
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var settings = new Mock<ISettingsService>();
            settings.Setup(item => item.GetEmailSettingsAsync()).ReturnsAsync(new EmailSettings
            {
                SmtpHost = "127.0.0.1",
                SmtpPort = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port,
                UseTLS = false,
                FromAddress = "sender@example.invalid"
            });
            var service = new ErpSystem.Web.Services.ProductionEmailService(
                NullLogger<ErpSystem.Web.Services.ProductionEmailService>.Instance,
                settings.Object, Mock.Of<IWebHostEnvironment>());

            // Accept the socket but never send the SMTP greeting. No email leaves
            // loopback. Before the fix the async send ignored SmtpClient.Timeout.
            var send = withAttachments
                ? service.SendEmailWithAttachmentsAsync("recipient@example.invalid", "Deadline test", "Test body", [], true)
                : service.SendEmailAsync("recipient@example.invalid", "Deadline test", "Test body", true);
            using var connection = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(10));
            (await send.WaitAsync(TimeSpan.FromSeconds(45))).Should().BeFalse();
        }
        finally
        {
            listener.Stop();
        }
    }

    private static Notification Audit(Guid tenantId, string status, string body) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, NotificationType = "Email", Title = "Replay test",
        Message = body, Status = status, DeliveryMethods = "Email", EmailAddress = "recipient@example.invalid",
        ScheduledFor = DateTime.UtcNow.AddMinutes(-10), CreatedAt = DateTime.UtcNow.AddMinutes(-10)
    };

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogLevel> Levels { get; } = [];
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Levels.Add(logLevel);
            Messages.Add(formatter(state, exception));
        }
    }
}
