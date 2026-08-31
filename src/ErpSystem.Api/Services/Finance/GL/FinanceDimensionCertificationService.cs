using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ErpSystem.Api.Services.Finance.GL;

public sealed class FinanceDimensionCertificationService : IFinanceDimensionCertificationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan AssessmentLifetime = TimeSpan.FromMinutes(30);

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceAuditService _audit;
    private readonly IReadOnlyDictionary<FinanceDimensionRouteId, IFinanceDimensionReadinessProvider> _providers;

    public FinanceDimensionCertificationService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IFinanceAuditService audit,
        IEnumerable<IFinanceDimensionReadinessProvider> providers)
    {
        _context = context;
        _currentUser = currentUser;
        _audit = audit;
        _providers = providers.GroupBy(provider => provider.RouteId).ToDictionary(group => group.Key, group =>
        {
            if (group.Count() != 1)
                throw new InvalidOperationException($"Finance dimension route '{group.Key}' has multiple readiness providers.");
            return group.Single();
        });
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    public async Task<IReadOnlyList<FinanceDimensionRouteCertificationDto>> GetRoutesAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var rows = await _context.FinanceDimensionRouteCertifications.AsNoTracking()
            .Where(row => row.TenantId == tenantId && !row.IsDeleted)
            .ToDictionaryAsync(row => row.RouteId, cancellationToken);
        var assessmentRows = await _context.FinanceDimensionReadinessAssessments.AsNoTracking()
            .Where(row => row.TenantId == tenantId && !row.IsDeleted)
            .OrderByDescending(row => row.AssessedAt)
            .ToListAsync(cancellationToken);
        var latestAssessments = assessmentRows.GroupBy(row => row.RouteId)
            .ToDictionary(group => group.Key, group => group.First());

        return FinanceDimensionRouteCatalog.Routes.Select(route =>
        {
            rows.TryGetValue(route.Id, out var row);
            latestAssessments.TryGetValue(route.Id, out var assessment);
            return MapRoute(route, row, assessment);
        }).ToList();
    }

    public async Task<FinanceDimensionReadinessAssessmentDto> AssessReadinessAsync(
        FinanceDimensionRouteId routeId,
        FinanceDimensionCertificationState targetState,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var route = FinanceDimensionRouteCatalog.GetRequired(routeId);
        var current = await CurrentStateAsync(tenantId, route, cancellationToken);
        EnsureNormalTransition(current.State, targetState);
        var now = DateTime.UtcNow;
        var expires = now.Add(AssessmentLifetime);
        var contribution = await EvaluateProviderAsync(tenantId, route, cancellationToken);
        var blockers = contribution.Blockers.OrderBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.DocumentReference, StringComparer.Ordinal).ToList();
        var hash = EvidenceHash(tenantId, route, current.State, targetState, blockers,
            contribution.DataVersionWatermark, now, expires);
        var actorId = RequiredUserId();
        var entity = new FinanceDimensionReadinessAssessment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RouteId = route.Id,
            ProducerModule = route.ProducerModule,
            SourceRoute = route.SourceRoute,
            DocumentType = route.DocumentType,
            ContractVersion = route.ContractVersion,
            CurrentState = current.State,
            TargetState = targetState,
            BlockerCount = blockers.Count,
            BlockerResultsJson = JsonSerializer.Serialize(blockers, JsonOptions),
            DataVersionWatermark = contribution.DataVersionWatermark,
            EvidenceHash = hash,
            AssessedAt = now,
            ExpiresAt = expires,
            AssessedByUserId = actorId,
            CreatedAt = now,
            CreatedBy = _currentUser.UserName,
            CreatedById = actorId
        };
        _context.FinanceDimensionReadinessAssessments.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync(new FinanceAuditEventDto
        {
            EventType = FinanceAuditEvents.DimensionReadinessAssessed,
            TenantId = tenantId,
            SourceModule = route.ProducerModule,
            SourceDocumentType = route.DocumentType,
            AfterValues = new { route.Id, route.SourceRoute, route.ContractVersion, current.State, targetState, entity.BlockerCount, entity.EvidenceHash, entity.ExpiresAt },
            Resource = "Finance.DimensionCertification",
            ResourceId = route.Id.ToString()
        }, cancellationToken);
        return MapAssessment(entity, blockers);
    }

    public async Task<FinanceDimensionReadinessAssessmentDto> GetAssessmentAsync(
        Guid assessmentId,
        CancellationToken cancellationToken = default)
    {
        var entity = await RequiredAssessmentAsync(TenantId, assessmentId, cancellationToken);
        return MapAssessment(entity, DeserializeBlockers(entity.BlockerResultsJson));
    }

    public async Task<FinanceDimensionRouteCertificationDto> PromoteAsync(
        FinanceDimensionRouteId routeId,
        PromoteFinanceDimensionRouteDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = TenantId;
        var route = FinanceDimensionRouteCatalog.GetRequired(routeId);
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 10)
            throw new InvalidOperationException("A certification promotion requires a reason of at least 10 characters.");
        var effectiveDate = request.EffectiveDate == default ? DateTime.UtcNow : request.EffectiveDate;

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // A retry must begin from database state, not entities tracked by an earlier attempt.
            // Keep the complete serializable unit (including audit persistence) inside the
            // execution-strategy delegate so SQL Server's retrying strategy can own it.
            _context.ChangeTracker.Clear();
            IDbContextTransaction? transaction = null;
            if (_context.Database.IsRelational())
                transaction = await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable, cancellationToken);

            try
            {
                var certification = await _context.FinanceDimensionRouteCertifications
                    .SingleOrDefaultAsync(row => row.TenantId == tenantId && row.RouteId == routeId && !row.IsDeleted,
                        cancellationToken);
                var currentState = certification?.State ?? route.DefaultState;
                EnsureNormalTransition(currentState, request.TargetState);
                EnsureRowVersion(certification, request.RowVersion);

                var assessment = await RequiredAssessmentAsync(tenantId, request.ReadinessAssessmentId, cancellationToken);
                if (assessment.RouteId != routeId || assessment.TargetState != request.TargetState
                    || assessment.CurrentState != currentState)
                    throw new InvalidOperationException("Readiness evidence does not match this route transition.");
                if (!string.Equals(assessment.ProducerModule, route.ProducerModule, StringComparison.Ordinal)
                    || !string.Equals(assessment.SourceRoute, route.SourceRoute, StringComparison.Ordinal)
                    || !string.Equals(assessment.DocumentType, route.DocumentType, StringComparison.Ordinal)
                    || !string.Equals(assessment.ContractVersion, route.ContractVersion, StringComparison.Ordinal))
                    throw new InvalidOperationException("Readiness evidence does not match the compiled route identity.");
                if (assessment.ExpiresAt <= DateTime.UtcNow || assessment.ConsumedAt.HasValue)
                    throw new InvalidOperationException("Readiness evidence is expired or has already been consumed.");
                if (assessment.BlockerCount != 0)
                    throw new InvalidOperationException("A Finance dimension route cannot be promoted while readiness blockers remain.");

                var authoritative = await EvaluateProviderAsync(tenantId, route, cancellationToken);
                var blockers = authoritative.Blockers.OrderBy(item => item.Code, StringComparer.Ordinal)
                    .ThenBy(item => item.DocumentReference, StringComparer.Ordinal).ToList();
                var authoritativeHash = EvidenceHash(tenantId, route, currentState, request.TargetState, blockers,
                    authoritative.DataVersionWatermark, assessment.AssessedAt, assessment.ExpiresAt);
                if (blockers.Count != 0 || authoritative.DataVersionWatermark != assessment.DataVersionWatermark
                    || !CryptographicOperations.FixedTimeEquals(
                        Encoding.ASCII.GetBytes(authoritativeHash), Encoding.ASCII.GetBytes(assessment.EvidenceHash)))
                    throw new InvalidOperationException("Readiness evidence is stale; run a new assessment before promotion.");

                var now = DateTime.UtcNow;
                var actorId = RequiredUserId();
                certification ??= new FinanceDimensionRouteCertification
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    RouteId = route.Id,
                    ProducerModule = route.ProducerModule,
                    SourceRoute = route.SourceRoute,
                    DocumentType = route.DocumentType,
                    ContractVersion = route.ContractVersion,
                    State = currentState,
                    EffectiveDate = now,
                    CreatedAt = now,
                    CreatedBy = _currentUser.UserName,
                    CreatedById = actorId
                };
                if (_context.Entry(certification).State == EntityState.Detached)
                    _context.FinanceDimensionRouteCertifications.Add(certification);
                certification.State = request.TargetState;
                certification.EffectiveDate = effectiveDate;
                certification.UpdatedAt = now;
                certification.UpdatedBy = _currentUser.UserName;
                certification.LastModifiedById = actorId;
                assessment.ConsumedAt = now;
                _context.FinanceDimensionCertificationTransitions.Add(new FinanceDimensionCertificationTransition
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    FinanceDimensionRouteCertificationId = certification.Id,
                    PreviousState = currentState,
                    NewState = request.TargetState,
                    Reason = reason,
                    ActorUserId = actorId,
                    TransitionedAt = now,
                    EffectiveDate = effectiveDate,
                    ReadinessAssessmentId = assessment.Id,
                    ReadinessEvidenceHash = assessment.EvidenceHash,
                    CreatedAt = now,
                    CreatedBy = _currentUser.UserName,
                    CreatedById = actorId
                });
                await _context.SaveChangesAsync(cancellationToken);
                await _audit.RecordAsync(new FinanceAuditEventDto
                {
                    EventType = FinanceAuditEvents.DimensionRoutePromoted,
                    TenantId = tenantId,
                    SourceModule = route.ProducerModule,
                    SourceDocumentType = route.DocumentType,
                    Reason = reason,
                    BeforeValues = new { State = currentState },
                    AfterValues = new { State = request.TargetState, effectiveDate, assessment.Id, assessment.EvidenceHash },
                    Resource = "Finance.DimensionCertification",
                    ResourceId = route.Id.ToString()
                }, cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return MapRoute(route, certification, assessment);
            }
            catch
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                throw;
            }
            finally
            {
                if (transaction is not null) await transaction.DisposeAsync();
            }
        });
    }

    public async Task<byte[]> ExportReadinessCsvAsync(Guid assessmentId, CancellationToken cancellationToken = default)
    {
        var assessment = await RequiredAssessmentAsync(TenantId, assessmentId, cancellationToken);
        var blockers = DeserializeBlockers(assessment.BlockerResultsJson);
        var csv = new StringBuilder();
        csv.AppendLine("Producer,Route,Document Type,Contract Version,Certification State,Document ID,Document Reference,Lifecycle,Dimension Issue,Fixed Rule Drift,Stale Budget Evidence,Active Reservation State,Blocker Code,Message,Remediation,Document Link");
        foreach (var blocker in blockers)
        {
            csv.Append(Csv(assessment.ProducerModule)).Append(',').Append(Csv(assessment.SourceRoute)).Append(',')
                .Append(Csv(assessment.DocumentType)).Append(',').Append(Csv(assessment.ContractVersion)).Append(',')
                .Append(Csv(assessment.CurrentState.ToString())).Append(',')
                .Append(Csv(blocker.DocumentId?.ToString())).Append(',').Append(Csv(blocker.DocumentReference)).Append(',')
                .Append(Csv(blocker.LifecycleState)).Append(',').Append(Csv(blocker.DimensionIssue)).Append(',')
                .Append(Csv(blocker.FixedRuleDrift ? "Yes" : "No")).Append(',')
                .Append(Csv(blocker.StaleBudgetEvidence ? "Yes" : "No")).Append(',')
                .Append(Csv(blocker.ActiveReservationState)).Append(',').Append(Csv(blocker.Code)).Append(',')
                .Append(Csv(blocker.Message)).Append(',').Append(Csv(blocker.RemediationStatus)).Append(',')
                .Append(Csv(blocker.DocumentLink)).AppendLine();
        }
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }

    private async Task<FinanceDimensionReadinessContribution> EvaluateProviderAsync(
        Guid tenantId,
        FinanceDimensionRouteDefinition route,
        CancellationToken cancellationToken)
    {
        if (_providers.TryGetValue(route.Id, out var provider))
            return await provider.EvaluateAsync(tenantId, route, cancellationToken);
        if (!route.RequiresReadinessProvider)
            return new FinanceDimensionReadinessContribution($"catalog:{route.ContractVersion}", []);
        return new FinanceDimensionReadinessContribution(
            $"provider-missing:{route.ContractVersion}",
            [new FinanceDimensionReadinessBlockerDto
            {
                Code = "READINESS_PROVIDER_NOT_INSTALLED",
                Message = "The source route has not installed its tenant-scoped readiness provider.",
                RemediationStatus = "Implement and certify the source adapter before promotion."
            }]);
    }

    private async Task<(FinanceDimensionCertificationState State, FinanceDimensionRouteCertification? Row)> CurrentStateAsync(
        Guid tenantId,
        FinanceDimensionRouteDefinition route,
        CancellationToken cancellationToken)
    {
        var row = await _context.FinanceDimensionRouteCertifications.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.RouteId == route.Id && !item.IsDeleted,
                cancellationToken);
        return (row?.State ?? route.DefaultState, row);
    }

    private static void EnsureNormalTransition(
        FinanceDimensionCertificationState current,
        FinanceDimensionCertificationState target)
    {
        if ((int)target != (int)current + 1)
            throw new InvalidOperationException($"Normal certification transitions must advance exactly one state; '{current}' cannot transition to '{target}'.");
    }

    private static void EnsureRowVersion(FinanceDimensionRouteCertification? row, string? supplied)
    {
        if (row is null) return;
        if (string.IsNullOrWhiteSpace(supplied))
            throw new DbUpdateConcurrencyException("The current certification row version is required.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(supplied); }
        catch (FormatException) { throw new DbUpdateConcurrencyException("The certification row version is invalid."); }
        if (!bytes.SequenceEqual(row.RowVersion))
            throw new DbUpdateConcurrencyException("Certification changed after it was loaded. Refresh and retry.");
    }

    private async Task<FinanceDimensionReadinessAssessment> RequiredAssessmentAsync(
        Guid tenantId,
        Guid assessmentId,
        CancellationToken cancellationToken) =>
        await _context.FinanceDimensionReadinessAssessments.SingleOrDefaultAsync(item =>
            item.Id == assessmentId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken)
        ?? throw new KeyNotFoundException("Finance dimension readiness assessment was not found.");

    private static string EvidenceHash(
        Guid tenantId,
        FinanceDimensionRouteDefinition route,
        FinanceDimensionCertificationState current,
        FinanceDimensionCertificationState target,
        IReadOnlyList<FinanceDimensionReadinessBlockerDto> blockers,
        string watermark,
        DateTime assessedAt,
        DateTime expiresAt)
    {
        var payload = JsonSerializer.Serialize(new
        {
            TenantId = tenantId.ToString("N"),
            Route = route.Id.ToString(),
            route.ProducerModule,
            route.SourceRoute,
            route.DocumentType,
            route.ContractVersion,
            Current = current.ToString(),
            Target = target.ToString(),
            Watermark = watermark,
            AssessedAt = assessedAt.ToUniversalTime(),
            ExpiresAt = expiresAt.ToUniversalTime(),
            Blockers = blockers
        }, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private static FinanceDimensionRouteCertificationDto MapRoute(
        FinanceDimensionRouteDefinition route,
        FinanceDimensionRouteCertification? row,
        FinanceDimensionReadinessAssessment? assessment) => new()
        {
            RouteId = route.Id,
            ProducerModule = route.ProducerModule,
            SourceRoute = route.SourceRoute,
            DocumentType = route.DocumentType,
            ContractVersion = route.ContractVersion,
            Grain = route.Grain.ToString(),
            SupportsDocumentDefaults = route.SupportsDocumentDefaults,
            Owner = route.Owner,
            Notes = route.Notes,
            State = row?.State ?? route.DefaultState,
            EffectiveDate = row?.EffectiveDate ?? DateTime.MinValue,
            RowVersion = row is null || row.RowVersion.Length == 0 ? null : Convert.ToBase64String(row.RowVersion),
            LatestAssessmentId = assessment?.Id,
            LatestBlockerCount = assessment?.BlockerCount,
            LatestAssessmentExpiresAt = assessment?.ExpiresAt
        };

    private static FinanceDimensionReadinessAssessmentDto MapAssessment(
        FinanceDimensionReadinessAssessment item,
        IReadOnlyList<FinanceDimensionReadinessBlockerDto> blockers) => new()
        {
            Id = item.Id,
            RouteId = item.RouteId,
            ProducerModule = item.ProducerModule,
            SourceRoute = item.SourceRoute,
            DocumentType = item.DocumentType,
            ContractVersion = item.ContractVersion,
            CurrentState = item.CurrentState,
            TargetState = item.TargetState,
            BlockerCount = item.BlockerCount,
            Blockers = blockers,
            BlockerTotalsByLifecycle = blockers.GroupBy(blocker => blocker.LifecycleState ?? "Route")
            .ToDictionary(group => group.Key, group => group.Count()),
            DataVersionWatermark = item.DataVersionWatermark,
            EvidenceHash = item.EvidenceHash,
            AssessedAt = item.AssessedAt,
            ExpiresAt = item.ExpiresAt,
            IsExpired = item.ExpiresAt <= DateTime.UtcNow
        };

    private static IReadOnlyList<FinanceDimensionReadinessBlockerDto> DeserializeBlockers(string json) =>
        JsonSerializer.Deserialize<List<FinanceDimensionReadinessBlockerDto>>(json, JsonOptions) ?? [];

    private Guid RequiredUserId() =>
        Guid.TryParse(_currentUser.UserId, out var userId) && userId != Guid.Empty
            ? userId
            : throw new InvalidOperationException("Finance dimension certification requires an authenticated user.");

    private static string Csv(string? value)
    {
        var safe = value ?? string.Empty;
        if (safe.Length > 0 && safe[0] is '=' or '+' or '-' or '@' or '\t' or '\r') safe = "'" + safe;
        return $"\"{safe.Replace("\"", "\"\"")}\"";
    }
}
