using System.Net;
using System.Net.Mail;
using ErpSystem.Core.Services;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services;

public interface ITenantEmailSender
{
    Task SendAsync(
        Guid tenantId,
        string recipient,
        string subject,
        string body,
        bool isHtml = true,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Sends email for an explicitly resolved tenant. Public endpoints cannot rely on the
/// authenticated-user tenant context, so their delivery path must name the tenant that
/// owns the public listing and its communication settings.
/// </summary>
public sealed class TenantEmailSender : ITenantEmailSender
{
    private readonly ApplicationDbContext _db;
    private readonly ICryptoService _crypto;
    private readonly ILogger<TenantEmailSender> _logger;

    public TenantEmailSender(
        ApplicationDbContext db,
        ICryptoService crypto,
        ILogger<TenantEmailSender> logger)
    {
        _db = db;
        _crypto = crypto;
        _logger = logger;
    }

    public async Task SendAsync(
        Guid tenantId,
        string recipient,
        string subject,
        string body,
        bool isHtml = true,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(recipient))
            throw new ArgumentException("Email recipient is required.", nameof(recipient));

        var settings = await _db.EmailSettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        if (settings is null)
            throw new InvalidOperationException("Email settings are not configured for the public listing tenant.");
        if (string.IsNullOrWhiteSpace(settings.SmtpHost) || string.IsNullOrWhiteSpace(settings.FromAddress))
            throw new InvalidOperationException("The public listing tenant email settings are incomplete.");

        var password = settings.SmtpPassword;
        if (!string.IsNullOrWhiteSpace(password))
        {
            try
            {
                password = _crypto.Decrypt(password);
            }
            catch (Exception ex)
            {
                // CryptoService already returns legacy plain-text values unchanged. A failure
                // here means an encrypted credential cannot be read with the configured key.
                _logger.LogError(ex, "Could not decrypt the SMTP password for tenant {TenantId}", tenantId);
                throw new InvalidOperationException(
                    "The tenant SMTP password could not be decrypted. Check Security:EncryptionKey or save the password again.", ex);
            }
        }

        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
        {
            EnableSsl = settings.UseTLS,
            Timeout = 30000
        };
        if (!string.IsNullOrWhiteSpace(settings.SmtpUsername))
            client.Credentials = new NetworkCredential(settings.SmtpUsername, password);

        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, settings.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = isHtml
        };
        message.To.Add(new MailAddress(recipient));

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        await client.SendMailAsync(message, deadline.Token);
    }
}
