using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// Narrow Finance-owned adapter that gives trusted background processors an
/// explicit tenant and service identity while retaining every central posting,
/// fiscal-period, idempotency, currency and dimension control.
/// </summary>
public sealed class FinanceSystemPostingEngine : IFinanceSystemPostingEngine
{
    private readonly ApplicationDbContext _db;
    private readonly ILoggerFactory _loggerFactory;

    public FinanceSystemPostingEngine(ApplicationDbContext db, ILoggerFactory loggerFactory)
    {
        _db = db;
        _loggerFactory = loggerFactory;
    }

    public Task<FinanceReversalPlanDto> GetReversalPlanAsync(Guid tenantId, Guid postingEventId, string reason,
        DateTime reversalDate, string systemActor, CancellationToken cancellationToken = default) =>
        CreateEngine(tenantId, systemActor).GetReversalPlanAsync(postingEventId, reason, reversalDate, cancellationToken);

    public Task<FinancePostingResultDto> PostAsync(Guid tenantId, FinancePostingRequestDto request,
        string systemActor, CancellationToken cancellationToken = default)
    {
        if (request.SourceDocumentTenantId != tenantId ||
            !string.Equals(request.SourceModule, "GL", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.SourceDocumentType, "RecurringJournalOccurrence", StringComparison.Ordinal) ||
            !string.Equals(request.PostingAction, "AutoReverseRecurringJournal", StringComparison.Ordinal))
            throw new InvalidOperationException("The system posting boundary only accepts tenant-bound recurring-journal automatic reversals.");

        return CreateEngine(tenantId, systemActor).PostAsync(request, cancellationToken);
    }

    private FinancePostingEngine CreateEngine(Guid tenantId, string systemActor)
    {
        if (tenantId == Guid.Empty) throw new InvalidOperationException("A valid Finance tenant is required.");
        if (string.IsNullOrWhiteSpace(systemActor)) throw new InvalidOperationException("A system actor is required.");
        return new FinancePostingEngine(
            _db,
            new ExplicitSystemCurrentUser(tenantId, systemActor.Trim()),
            _loggerFactory.CreateLogger<FinancePostingEngine>());
    }

    private sealed class ExplicitSystemCurrentUser(Guid tenantId, string actor) : ICurrentUserService
    {
        public string? UserId => null;
        public string? UserName => actor;
        public string FullName => actor;
        public string? Email => null;
        public Guid? TenantId => tenantId;
        public Guid? EmployeeId => null;
        public bool IsAuthenticated => true;
        public IEnumerable<string> Roles => Array.Empty<string>();
        public IDictionary<string, string> Claims => new Dictionary<string, string> { ["tenant_id"] = tenantId.ToString() };
        public bool IsInRole(string role) => false;
        public string? IpAddress => "system";
        public string? UserAgent => actor;
    }
}
