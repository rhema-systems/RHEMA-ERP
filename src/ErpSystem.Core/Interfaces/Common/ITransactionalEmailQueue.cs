namespace ErpSystem.Core.Interfaces.Common;

/// <summary>
/// Hands a one-to-one transactional email to the durable notification outbox instead of
/// attempting SMTP inline.
/// </summary>
/// <remarks>
/// <para>Direct <see cref="IEmailService"/> sends are fire-and-forget: a transient SMTP outage
/// loses the message with nothing to retry. That is survivable for a nice-to-have notification
/// and not survivable for a verification email, because the account it belongs to cannot be used
/// or re-registered until that message arrives.</para>
///
/// <para>Enqueuing writes a row the existing notification dispatcher already understands, which
/// brings claim-leasing, exponential backoff, dead-lettering and the admin retry screens for
/// free. The trade is latency: delivery happens on the dispatcher's poll interval (~30s by
/// default) rather than during the request.</para>
/// </remarks>
public interface ITransactionalEmailQueue
{
    /// <summary>
    /// Queues an email for durable delivery. Throws if the row cannot be written — unlike an SMTP
    /// failure, that is a local database problem the caller should surface rather than swallow.
    /// </summary>
    Task EnqueueAsync(TransactionalEmailRequest request, CancellationToken cancellationToken = default);
}

public sealed class TransactionalEmailRequest
{
    public required Guid TenantId { get; init; }
    public required string ToEmail { get; init; }
    public required string Subject { get; init; }
    public required string BodyHtml { get; init; }

    /// <summary>Groups the message in monitoring screens, e.g. <c>CandidatePortalVerification</c>.</summary>
    public string NotificationType { get; init; } = "TransactionalEmail";

    public string Priority { get; init; } = "High";
}
