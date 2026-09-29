using System.Text.Json;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Orientation;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Orientation;

/// <summary>
/// The outbox half of the lifecycle notices (round 4, lane K-b): sends what the notices queued, and
/// writes down what happened to each.
/// </summary>
/// <remarks>
/// <para><b>Why a dispatcher at all.</b> Publishing a programme, or the nightly sweep, can enrol
/// hundreds of people in one save. Their emails sent inside that request would hold it for as long
/// as the mail server takes — ten seconds each when it hangs. The notice is written with the event;
/// the email follows within a minute.</para>
///
/// <para><b>Settled, never assumed</b> — the same outcomes as the reminder sweep's:</para>
/// <list type="bullet">
///   <item><c>NoMailServer</c> — none is configured (checked the way the sender checks). Final: the
///   in-app notice was the delivery, and nobody wants last month's news the day a server is set up.</item>
///   <item><c>NoAddress</c> — the person has no email address. Final.</item>
///   <item><c>Stale</c> — still queued three days on (the dispatcher was down). Final, for the same reason.</item>
///   <item><c>Failed</c> / <c>TimedOut</c> — retried, five minutes apart, up to three attempts, then final.</item>
/// </list>
///
/// <para><b>Saved row by row</b>, so a pass that dies half way never sends an email twice. A timeout
/// ends the pass: a mail server that hung once will hang for the next two hundred, and the rest wait
/// for the next minute rather than holding this one for half an hour.</para>
/// </remarks>
public sealed class OrientationNoticeEmailDispatcher : IOrientationNoticeEmailDispatcher
{
    public const int MaxAttempts = 3;

    private const string LockName = "bg:orientation-notice-emails";
    private static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromDays(3);
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(10);

    private readonly IUnitOfWork _unitOfWork;
    private readonly ITemplatedEmailService _templatedEmail;
    private readonly IDistributedLockService _locks;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OrientationNoticeEmailDispatcher> _logger;

    public OrientationNoticeEmailDispatcher(
        IUnitOfWork unitOfWork,
        ITemplatedEmailService templatedEmail,
        IDistributedLockService locks,
        IConfiguration configuration,
        ILogger<OrientationNoticeEmailDispatcher> logger)
    {
        _unitOfWork = unitOfWork;
        _templatedEmail = templatedEmail;
        _locks = locks;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Guid>> TenantsWithQueuedAsync(CancellationToken cancellationToken = default)
        => await _unitOfWork.Repository<OrientationNotification>().GetQueryable().AsNoTracking()
            .Where(n => !n.IsDeleted && n.EmailStatus == OrientationNoticeEmailStatus.Queued)
            .Select(n => n.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<OrientationNoticeDispatchResultDto> DispatchAsync(
        Guid tenantId, int max = 200, CancellationToken cancellationToken = default)
    {
        var result = new OrientationNoticeDispatchResultDto();

        await using var lease = await _locks.TryAcquireAsync(LockName, TimeSpan.FromMinutes(10), cancellationToken);
        if (lease is null)
        {
            result.Busy = true;
            result.StillQueued = await CountQueuedAsync(tenantId, cancellationToken);
            return result;
        }

        var now = DateTime.UtcNow;
        var retryBefore = now - RetryAfter;
        var rows = await _unitOfWork.Repository<OrientationNotification>().GetQueryable()
            .Where(n => n.TenantId == tenantId && !n.IsDeleted && n.EmailStatus == OrientationNoticeEmailStatus.Queued
                        && (n.EmailLastAttemptAt == null || n.EmailLastAttemptAt <= retryBefore))
            .OrderBy(n => n.SentAt)
            .Take(Math.Clamp(max, 1, 1000))
            .ToListAsync(cancellationToken);

        result.Picked = rows.Count;
        result.MailServerConfigured = await MailServerConfiguredAsync();

        if (rows.Count > 0)
        {
            var ids = rows.Select(r => r.RecipientEmployeeId).Distinct().ToList();
            var people = await _unitOfWork.Repository<Employee>().GetQueryable().AsNoTracking()
                .Where(e => e.TenantId == tenantId && ids.Contains(e.Id))
                .Select(e => new { e.Id, e.FirstName, e.LastName, e.EmailAddress })
                .ToDictionaryAsync(e => e.Id, cancellationToken);
            var baseUrl = (_configuration["FrontendUrl"] ?? "http://localhost:3000").TrimEnd('/');

            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                people.TryGetValue(row.RecipientEmployeeId, out var person);

                string outcome;
                if (row.SentAt < now - StaleAfter) outcome = OrientationNoticeEmailStatus.Stale;
                else if (!result.MailServerConfigured) outcome = OrientationNoticeEmailStatus.NoMailServer;
                else if (string.IsNullOrWhiteSpace(person?.EmailAddress)) outcome = OrientationNoticeEmailStatus.NoAddress;
                else if (string.IsNullOrWhiteSpace(row.EmailEventKey)) outcome = OrientationNoticeEmailStatus.Failed;
                else
                {
                    var tokens = ReadTokens(row);
                    tokens["RecipientName"] = $"{person!.FirstName} {person.LastName}".Trim();
                    tokens["ActionUrl"] = row.NavigationUrl is null ? null : baseUrl + row.NavigationUrl;
                    outcome = await SendAsync(tenantId, row.EmailEventKey!, person.EmailAddress!, tokens);
                    row.EmailAttempts++;
                    row.EmailLastAttemptAt = DateTime.UtcNow;
                }

                var retry = outcome is OrientationNoticeEmailStatus.Failed or OrientationNoticeEmailStatus.TimedOut
                            && row.EmailAttempts > 0 && row.EmailAttempts < MaxAttempts;
                if (retry) result.Retrying++;
                else
                {
                    row.EmailStatus = outcome;
                    Tally(result, outcome);
                }
                row.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                if (outcome == OrientationNoticeEmailStatus.TimedOut) break;
            }

            _logger.LogInformation(
                "Orientation notice emails for tenant {TenantId}: {Picked} picked, {Sent} sent, {Failed} failed, {Retrying} to retry, " +
                "{NoAddress} without an address, {NoMailServer} with no mail server, {Stale} stale",
                tenantId, result.Picked, result.Sent, result.Failed, result.Retrying, result.NoAddress, result.NoMailServer, result.Stale);
        }

        result.StillQueued = await CountQueuedAsync(tenantId, cancellationToken);
        return result;
    }

    private static void Tally(OrientationNoticeDispatchResultDto result, string outcome)
    {
        switch (outcome)
        {
            case OrientationNoticeEmailStatus.Sent: result.Sent++; break;
            case OrientationNoticeEmailStatus.NoAddress: result.NoAddress++; break;
            case OrientationNoticeEmailStatus.NoMailServer: result.NoMailServer++; break;
            case OrientationNoticeEmailStatus.Stale: result.Stale++; break;
            default: result.Failed++; break;
        }
    }

    /// <summary>The tokens as they stood at the event, with the notice's own words as the fallback.</summary>
    private static Dictionary<string, string?> ReadTokens(OrientationNotification row)
    {
        Dictionary<string, string?>? stored = null;
        if (!string.IsNullOrWhiteSpace(row.EmailTokens))
        {
            try { stored = JsonSerializer.Deserialize<Dictionary<string, string?>>(row.EmailTokens); }
            catch (JsonException) { stored = null; }
        }

        var tokens = new Dictionary<string, string?>(stored ?? new Dictionary<string, string?>(), StringComparer.OrdinalIgnoreCase);
        if (!tokens.ContainsKey("Headline")) tokens["Headline"] = row.Subject;
        if (!tokens.ContainsKey("Body")) tokens["Body"] = row.Message;
        return tokens;
    }

    private async Task<string> SendAsync(Guid tenantId, string eventKey, string email, Dictionary<string, string?> tokens)
    {
        try
        {
            // By tenant (lane N): this runs on a host with no signed-in user, and the tenant's own
            // wording — edited on HR's letter-templates screen — must still be what goes out.
            var send = _templatedEmail.SendForTenantAsync(tenantId, OnboardingOrientationEmailCatalog.Module, eventKey, email, tokens);
            if (await Task.WhenAny(send, Task.Delay(SendTimeout)) != send)
            {
                _logger.LogWarning("Orientation notice email {EventKey} to {Email} timed out after {Seconds} s.", eventKey, email, SendTimeout.TotalSeconds);
                return OrientationNoticeEmailStatus.TimedOut;
            }
            return await send ? OrientationNoticeEmailStatus.Sent : OrientationNoticeEmailStatus.Failed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Orientation notice email {EventKey} to {Email} failed.", eventKey, email);
            return OrientationNoticeEmailStatus.Failed;
        }
    }

    /// <summary>
    /// Whether the sender could send at all — asked the way <c>SettingsService</c> answers it: the
    /// first mail settings row, with a host and a from address.
    /// </summary>
    private async Task<bool> MailServerConfiguredAsync()
    {
        var settings = await _unitOfWork.Repository<EmailSettings>().FirstOrDefaultAsync(e => true);
        return settings is not null && !string.IsNullOrWhiteSpace(settings.SmtpHost) && !string.IsNullOrWhiteSpace(settings.FromAddress);
    }

    private async Task<int> CountQueuedAsync(Guid tenantId, CancellationToken cancellationToken)
        => await _unitOfWork.Repository<OrientationNotification>().GetQueryable().AsNoTracking()
            .CountAsync(n => n.TenantId == tenantId && !n.IsDeleted && n.EmailStatus == OrientationNoticeEmailStatus.Queued, cancellationToken);
}
