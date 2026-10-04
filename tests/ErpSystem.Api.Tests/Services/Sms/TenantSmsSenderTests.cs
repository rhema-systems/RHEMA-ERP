using ErpSystem.Api.Services.Sms;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Services;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Sms;

public sealed class TenantSmsSenderTests
{
    [Fact]
    public async Task SendOtpAsync_DoesNotSendUnreadableCiphertextAsApiKey()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"tenant-sms-{Guid.NewGuid():N}")
                .Options);
        var tenantId = Guid.NewGuid();
        db.SmsSettings.Add(new SmsSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DefaultProvider = "GhanaGateway",
            GhanaGatewayEnabled = true,
            GhanaGatewayApiKey = "stored-ciphertext",
            GhanaGatewaySenderId = "RHEMA",
            GhanaGatewayUrlTemplate = MNotifySmsGateway.DefaultEndpoint,
            GhanaGatewayTimeoutSeconds = 10
        });
        await db.SaveChangesAsync();
        var crypto = new Mock<ICryptoService>();
        crypto.Setup(service => service.Decrypt("stored-ciphertext"))
            .Throws(new InvalidOperationException("decryption failed"));
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var sender = new TenantSmsSender(
            db,
            crypto.Object,
            Options.Create(new SmsOptions()),
            httpClientFactory.Object,
            NullLogger<TenantSmsSender>.Instance);

        var action = () => sender.SendOtpAsync(
            tenantId, "+233241234567", "Your code is 123456");

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tenant SMS credential could not be decrypted*");
        httpClientFactory.Verify(factory => factory.CreateClient(It.IsAny<string>()), Times.Never);
    }
}
