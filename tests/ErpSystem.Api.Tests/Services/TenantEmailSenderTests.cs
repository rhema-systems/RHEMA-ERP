using ErpSystem.Api.Services;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Services;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public sealed class TenantEmailSenderTests
{
    [Fact]
    public async Task SendAsync_RejectsUnreadableSmtpPasswordBeforeConnecting()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.EmailSettings.Add(new EmailSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SmtpHost = "smtp.example.test",
            SmtpPort = 587,
            SmtpUsername = "mailer",
            SmtpPassword = "stored-ciphertext",
            FromAddress = "mailer@example.test",
            FromName = "Test"
        });
        await db.SaveChangesAsync();
        var crypto = new Mock<ICryptoService>();
        crypto.Setup(service => service.Decrypt("stored-ciphertext"))
            .Throws(new InvalidOperationException("decryption failed"));
        var sender = new TenantEmailSender(db, crypto.Object, NullLogger<TenantEmailSender>.Instance);

        var action = () => sender.SendAsync(
            tenantId, "visitor@example.test", "Verification", "Code 123456");

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*SMTP password could not be decrypted*");
    }

    [Fact]
    public async Task SendAsync_DoesNotUseAnotherTenantsSmtpConfiguration()
    {
        await using var db = CreateDb();
        db.EmailSettings.Add(new EmailSettings
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            SmtpHost = "smtp.other-tenant.test",
            SmtpPort = 587,
            SmtpUsername = "other-user",
            SmtpPassword = "other-secret",
            FromAddress = "other@example.test",
            FromName = "Other tenant"
        });
        await db.SaveChangesAsync();
        var sender = new TenantEmailSender(
            db,
            Mock.Of<ICryptoService>(),
            NullLogger<TenantEmailSender>.Instance);

        var action = () => sender.SendAsync(
            Guid.NewGuid(),
            "visitor@example.test",
            "Verification",
            "Code 123456",
            cancellationToken: CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not configured for the public listing tenant*");
    }

    private static ApplicationDbContext CreateDb()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"tenant-email-{Guid.NewGuid():N}")
            .Options);
}
