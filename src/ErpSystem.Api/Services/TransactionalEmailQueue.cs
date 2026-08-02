using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Data;

namespace ErpSystem.Api.Services;

/// <summary>
/// Writes transactional emails into the shared <c>Notifications</c> outbox for the existing
/// dispatcher to deliver.
/// </summary>
/// <remarks>
/// A narrow entry point rather than exposing <c>INotificationService</c> wholesale: callers here
/// want "send this one email, durably", not the in-app/SMS/campaign surface.
/// </remarks>
public sealed class TransactionalEmailQueue : ITransactionalEmailQueue
{
    private readonly ApplicationDbContext _db;

    public TransactionalEmailQueue(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task EnqueueAsync(
        TransactionalEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty)
            throw new InvalidOperationException("A tenant is required to queue a transactional email.");
        if (string.IsNullOrWhiteSpace(request.ToEmail))
            throw new InvalidOperationException("A recipient address is required.");

        _db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            // Explicit: several of these flows are anonymous, so there is no tenant claim to
            // stamp from and the FK to Tenants would otherwise fail.
            TenantId = request.TenantId,
            NotificationType = request.NotificationType,
            // The dispatcher's email branch falls back to Title/Message when a notification
            // carries no structured payload, and defaults IsHtml to true — so a plain row is
            // enough and we avoid coupling to the campaign payload format.
            Title = request.Subject,
            Message = request.BodyHtml,
            Priority = request.Priority,
            Status = "Pending",
            IsRead = false,
            ScheduledFor = DateTime.UtcNow,
            SentAt = null,
            AttemptCount = 0,
            LastError = null,
            DeliveryMethods = "Email",
            EmailAddress = request.ToEmail,
            PhoneNumber = null,
            // No in-app recipient: these go to an external mailbox, not an ERP user. The email
            // branch never reads RecipientId, and the campaign path already writes Guid.Empty.
            RecipientId = Guid.Empty
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
