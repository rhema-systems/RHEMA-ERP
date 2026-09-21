using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Raises reminders for employee credentials that are about to expire, or have.
/// </summary>
/// <remarks>
/// <para>Demo feedback round 2, lane C2 (plan § 6.3). The identification-expiry sweep, one family
/// over: same two tiers (inside the lead window → the holder; expired → HR), same dedupe key
/// (kind + credential + due date + tier), same run-header-plus-dispatch-rows store. The one
/// difference is where the lead time comes from: the catalogue row's own
/// <c>ExpiryNotificationLeadDays</c>, falling back to the tenant's
/// <c>CompanyHrPolicySettings.CertificationExpiryLeadDays</c>.</para>
///
/// <para>Revoked credentials raise nothing — a revocation is already an event somebody acted
/// on. Credentials with no expiry raise nothing. Inactive employees are INCLUDED, as in the
/// identification sweep: a leaver holding a lapsed licence the company vouched for is exactly the
/// case HR needs to see.</para>
/// </remarks>
public interface ICertificationExpiryReminderService
{
    Task<IEnumerable<CertificationExpiryReminderItemDto>> PreviewAsync(CancellationToken ct = default);
    Task<CertificationExpiryRunResultDto> RunSweepAsync(string trigger = "Manual", CancellationToken ct = default);
    Task<CertificationExpiryRunResultDto> RunSweepForTenantAsync(Guid tenantId, string trigger, Guid? triggeredByUserId, CancellationToken ct = default);
    Task<IEnumerable<CertificationExpiryRunDto>> GetRunsAsync(int count = 20, CancellationToken ct = default);
    Task<IEnumerable<CertificationExpiryLogEntryDto>> GetLogAsync(int days = 14, CancellationToken ct = default);
}

public class CertificationExpiryReminderService : ICertificationExpiryReminderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<CertificationExpiryReminderService> _logger;

    public CertificationExpiryReminderService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<CertificationExpiryReminderService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private sealed record Candidate(CertificationExpiryReminderItemDto Item, string DedupeKey);

    public async Task<IEnumerable<CertificationExpiryReminderItemDto>> PreviewAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var candidates = await FindCandidatesAsync(tenantId, ct);
        var alreadyRaised = await AlreadyRaisedAsync(tenantId, candidates, ct);
        foreach (var c in candidates)
            c.Item.AlreadyRaised = alreadyRaised.Contains(c.DedupeKey);
        return candidates.Select(c => c.Item).ToList();
    }

    public Task<CertificationExpiryRunResultDto> RunSweepAsync(string trigger = "Manual", CancellationToken ct = default)
        => RunSweepForTenantAsync(
            GetTenantId(), trigger,
            _currentUserProvider.UserId == Guid.Empty ? null : _currentUserProvider.UserId, ct);

    public async Task<CertificationExpiryRunResultDto> RunSweepForTenantAsync(
        Guid tenantId, string trigger, Guid? triggeredByUserId, CancellationToken ct = default)
    {
        var candidates = await FindCandidatesAsync(tenantId, ct);
        var alreadyRaised = await AlreadyRaisedAsync(tenantId, candidates, ct);
        var fresh = candidates.Where(c => !alreadyRaised.Contains(c.DedupeKey)).ToList();

        var run = new CertificationExpiryReminderRun
        {
            TenantId = tenantId,
            StartedAt = DateTime.UtcNow,
            Trigger = trigger,
            TriggeredByUserId = triggeredByUserId,
            RemindersQueued = fresh.Count,
            CompletedAt = DateTime.UtcNow,
        };
        await _unitOfWork.Repository<CertificationExpiryReminderRun>().AddAsync(run);

        foreach (var c in fresh)
        {
            await _unitOfWork.Repository<CertificationExpiryDispatchLog>().AddAsync(new CertificationExpiryDispatchLog
            {
                TenantId = tenantId,
                RunId = run.Id,
                Kind = c.Item.Kind,
                EmployeeId = c.Item.EmployeeId,
                EmployeeCertificationId = c.Item.EmployeeCertificationId,
                CertificationId = c.Item.CertificationId,
                Reference = c.Item.Reference,
                DueDate = c.Item.DueDate,
                DaysRemaining = c.Item.DaysRemaining,
                EscalationTier = c.Item.EscalationTier,
                RoutedToEmployeeId = c.Item.RoutedToEmployeeId,
                DedupeKey = c.DedupeKey,
            });
        }

        // The run and its rows are claimed together, so a second sweep starting mid-flight cannot
        // see an empty log and raise the same reminders again.
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Certification expiry sweep for tenant {TenantId}: {Considered} credential(s) due, {Queued} new reminder(s).",
            tenantId, candidates.Count, fresh.Count);

        return new CertificationExpiryRunResultDto
        {
            RunId = run.Id,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            Trigger = run.Trigger,
            CredentialsConsidered = candidates.Count,
            RemindersQueued = fresh.Count,
            AlreadyRaised = candidates.Count - fresh.Count,
        };
    }

    private async Task<HashSet<string>> AlreadyRaisedAsync(Guid tenantId, List<Candidate> candidates, CancellationToken ct)
    {
        var keys = candidates.Select(c => c.DedupeKey).ToList();
        if (keys.Count == 0) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return (await _unitOfWork.Repository<CertificationExpiryDispatchLog>().GetQueryable()
                .AsNoTracking()
                .Where(d => d.TenantId == tenantId && !d.IsDeleted && keys.Contains(d.DedupeKey))
                .Select(d => d.DedupeKey)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IEnumerable<CertificationExpiryRunDto>> GetRunsAsync(int count = 20, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        count = Math.Clamp(count, 1, 200);
        return await _unitOfWork.Repository<CertificationExpiryReminderRun>().GetQueryable()
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted)
            .OrderByDescending(r => r.StartedAt)
            .Take(count)
            .Select(r => new CertificationExpiryRunDto
            {
                Id = r.Id,
                StartedAt = r.StartedAt,
                CompletedAt = r.CompletedAt,
                Trigger = r.Trigger,
                TriggeredByUserId = r.TriggeredByUserId,
                RemindersQueued = r.RemindersQueued,
            })
            .ToListAsync(ct);
    }

    /// <remarks>Names are joined LEFT, as in the identification log: a row must outlive the rows it names.</remarks>
    public async Task<IEnumerable<CertificationExpiryLogEntryDto>> GetLogAsync(int days = 14, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        days = Math.Clamp(days, 1, 365);
        var since = DateTime.UtcNow.Date.AddDays(-days);

        return await _unitOfWork.Repository<CertificationExpiryDispatchLog>().GetQueryable()
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && !d.IsDeleted && d.CreatedAt >= since)
            .GroupJoin(_unitOfWork.Repository<Employee>().GetQueryable().AsNoTracking().Where(e => e.TenantId == tenantId),
                d => d.EmployeeId, e => e.Id, (d, es) => new { Log = d, Employees = es })
            .SelectMany(x => x.Employees.DefaultIfEmpty(), (x, e) => new { x.Log, Employee = e })
            .GroupJoin(_unitOfWork.Repository<Certification>().GetQueryable().AsNoTracking().Where(c => c.TenantId == tenantId),
                x => x.Log.CertificationId, c => c.Id, (x, cs) => new { x.Log, x.Employee, Certifications = cs })
            .SelectMany(x => x.Certifications.DefaultIfEmpty(), (x, c) => new CertificationExpiryLogEntryDto
            {
                Id = x.Log.Id,
                RunId = x.Log.RunId,
                Kind = x.Log.Kind,
                EmployeeId = x.Log.EmployeeId,
                EmployeeName = x.Employee == null ? null : ((x.Employee.FirstName ?? "") + " " + (x.Employee.LastName ?? "")).Trim(),
                EmployeeNumber = x.Employee == null ? null : x.Employee.EmployeeNumber,
                EmployeeCertificationId = x.Log.EmployeeCertificationId,
                CertificationId = x.Log.CertificationId,
                CertificationName = c == null ? null : c.Name,
                Reference = x.Log.Reference,
                DueDate = x.Log.DueDate,
                DaysRemaining = x.Log.DaysRemaining,
                EscalationTier = x.Log.EscalationTier,
                RoutedToEmployeeId = x.Log.RoutedToEmployeeId,
                RaisedAt = x.Log.CreatedAt,
            })
            .OrderByDescending(d => d.RaisedAt)
            .ToListAsync(ct);
    }

    /// <summary>Every live credential with an expiry inside its lead window, or past.</summary>
    private async Task<List<Candidate>> FindCandidatesAsync(Guid tenantId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var policyLead = await _unitOfWork.Repository<CompanyHrPolicySettings>().GetQueryable()
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => (int?)s.CertificationExpiryLeadDays)
            .FirstOrDefaultAsync(ct) ?? 60;

        var rows = await _unitOfWork.Repository<EmployeeCertification>().GetQueryable()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && !x.IsRevoked && x.ExpiresOn != null)
            .Join(_unitOfWork.Repository<Certification>().GetQueryable().AsNoTracking()
                    .Where(c => c.TenantId == tenantId && !c.IsDeleted),
                x => x.CertificationId, c => c.Id,
                (x, c) => new { Credential = x, CertificationName = c.Name, LeadDays = c.ExpiryNotificationLeadDays })
            .GroupJoin(_unitOfWork.Repository<Employee>().GetQueryable().AsNoTracking().Where(e => e.TenantId == tenantId),
                x => x.Credential.EmployeeId, e => e.Id,
                (x, es) => new { x.Credential, x.CertificationName, x.LeadDays, Employees = es })
            .SelectMany(x => x.Employees.DefaultIfEmpty(), (x, e) => new
            {
                x.Credential,
                x.CertificationName,
                x.LeadDays,
                EmployeeName = e == null ? null : ((e.FirstName ?? "") + " " + (e.LastName ?? "")).Trim(),
                EmployeeNumber = e == null ? null : e.EmployeeNumber,
            })
            .ToListAsync(ct);

        var candidates = new List<Candidate>();
        foreach (var r in rows)
        {
            var due = r.Credential.ExpiresOn!.Value;
            var daysRemaining = due.DayNumber - today.DayNumber;
            var lead = r.LeadDays ?? policyLead;
            if (daysRemaining > lead) continue;

            var expired = daysRemaining < 0;
            var tier = expired ? 2 : 1;
            var kind = expired ? "CertificationExpired" : "CertificationExpiring";
            var routedTo = expired ? (Guid?)null : r.Credential.EmployeeId;
            var reference = string.IsNullOrWhiteSpace(r.Credential.CertificateNumber)
                ? r.CertificationName
                : $"{r.CertificationName} {r.Credential.CertificateNumber}";

            candidates.Add(new Candidate(
                new CertificationExpiryReminderItemDto
                {
                    Kind = kind,
                    EmployeeId = r.Credential.EmployeeId,
                    EmployeeName = string.IsNullOrWhiteSpace(r.EmployeeName) ? null : r.EmployeeName,
                    EmployeeNumber = r.EmployeeNumber,
                    EmployeeCertificationId = r.Credential.Id,
                    CertificationId = r.Credential.CertificationId,
                    CertificationName = r.CertificationName,
                    CertificateNumber = r.Credential.CertificateNumber,
                    Reference = reference,
                    DueDate = due,
                    DaysRemaining = daysRemaining,
                    LeadDays = lead,
                    EscalationTier = tier,
                    RoutedToEmployeeId = routedTo,
                },
                $"{kind}:{r.Credential.Id}:{due:yyyy-MM-dd}:T{tier}"));
        }

        return candidates.OrderBy(c => c.Item.DaysRemaining).ThenBy(c => c.Item.Reference).ToList();
    }
}
