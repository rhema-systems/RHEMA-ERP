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
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.GL;

public sealed class FinanceSourceDimensionService : IFinanceSourceDimensionService
{
    private const string BudgetNotApplicable = "NotApplicable";
    private const string BudgetNotEvaluated = "NotEvaluated";
    private const string BudgetCurrent = "Current";
    private const string BudgetStale = "Stale";

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceSourceDimensionAssignmentStore _store;
    private readonly FinanceDimensionAdministrationService _dimensions;
    private readonly IFinanceBudgetCommitmentService? _budgetCommitments;

    public FinanceSourceDimensionService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IFinanceSourceDimensionAssignmentStore store,
        FinanceDimensionAdministrationService dimensions,
        IFinanceBudgetCommitmentService? budgetCommitments = null)
    {
        _db = db;
        _currentUser = currentUser;
        _store = store;
        _dimensions = dimensions;
        _budgetCommitments = budgetCommitments;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
    private Guid? UserId => Guid.TryParse(_currentUser.UserId, out var id) && id != Guid.Empty ? id : null;

    public async Task<FinanceSourceDocumentDimensionDto> SynchronizeDraftAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        DateTime documentDate,
        IReadOnlyList<FinanceSourceDocumentLineContext> authoritativeLines,
        FinanceSourceDocumentDimensionInputDto? input,
        bool inheritDefaultForUnassignedLines,
        string? budgetReservationSourceDocumentType,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (_db.Database.CurrentTransaction is not null)
            return await SynchronizeDraftCoreAsync(
                producer, sourceDocumentId, documentDate, authoritativeLines, input,
                inheritDefaultForUnassignedLines, budgetReservationSourceDocumentType,
                reason, refreshPersistedFixedValues: true, cancellationToken);

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            try
            {
                var result = await SynchronizeDraftCoreAsync(
                    producer, sourceDocumentId, documentDate, authoritativeLines, input,
                    inheritDefaultForUnassignedLines, budgetReservationSourceDocumentType,
                    reason, refreshPersistedFixedValues: true, cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                throw;
            }
        });
    }

    public async Task<FinanceSourceDocumentDimensionDto> GetAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        DateTime documentDate,
        IReadOnlyList<FinanceSourceDocumentLineContext> authoritativeLines,
        CancellationToken cancellationToken = default)
    {
        ValidateAuthoritativeLines(authoritativeLines);
        var assignments = await LoadAssignmentsAsync(producer, sourceDocumentId, cancellationToken);
        return await MapDocumentAsync(
            producer, sourceDocumentId, documentDate, authoritativeLines, assignments, cancellationToken);
    }

    public async Task<FinanceSourceDocumentDimensionDto> ValidateAndFreezeAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        DateTime documentDate,
        IReadOnlyList<FinanceSourceDocumentLineContext> authoritativeLines,
        bool requireCurrentBudgetEvidence,
        CancellationToken cancellationToken = default)
    {
        ValidateAuthoritativeLines(authoritativeLines);
        var assignments = await LoadAssignmentsAsync(producer, sourceDocumentId, cancellationToken);
        var header = assignments.SingleOrDefault(item => !item.SourceLineId.HasValue)
            ?? throw new InvalidOperationException("The source document has no trusted Finance dimension provenance.");
        var lineAssignments = assignments.Where(item => item.SourceLineId.HasValue)
            .ToDictionary(item => item.SourceLineId!.Value);
        var certification = await CertificationStateAsync(producer, cancellationToken);
        var warnings = new List<string>();
        var fixedDrift = false;

        foreach (var line in authoritativeLines)
        {
            lineAssignments.TryGetValue(line.SourceLineId, out var assignment);
            var storedValues = assignment?.FinanceDimensionSetId is Guid setId
                ? await ValuesForSetAsync(setId, cancellationToken)
                : Array.Empty<FinancePostingDimensionValueDto>();
            var resolution = await _dimensions.ResolveSourceLineAsync(
                producer, line.AccountId, documentDate, storedValues, certification,
                refreshPersistedFixedValues: true, cancellationToken, line.AdditionalAccountIds);
            warnings.AddRange(resolution.ReadinessWarnings.Select(message => $"Line {line.SourceLineId}: {message}"));
            var newSetId = resolution.DimensionSet?.Id;
            if (assignment is null || assignment.FinanceDimensionSetId != newSetId)
            {
                if (assignment?.IsFrozen == true)
                    throw new InvalidOperationException(
                        $"Finance dimension rule drift was detected on submitted source line {line.SourceLineId}. Return the document to Draft, re-resolve dimensions, refresh budget evidence and start a new approval workflow.");
                fixedDrift |= assignment is not null;
                await _db.SaveChangesAsync(cancellationToken);
                await _store.UpsertAsync(
                    producer, sourceDocumentId, line.SourceLineId, newSetId, null, cancellationToken);
                await RecordChangeAsync(
                    producer, sourceDocumentId, line.SourceLineId,
                    assignment?.FinanceDimensionSetId, newSetId,
                    "Effective fixed Finance dimension rule changed during draft validation.",
                    budgetEvidenceBecameStale: requireCurrentBudgetEvidence,
                    releasedReservationIds: [],
                    previousSnapshotId: assignment?.FinanceDimensionSnapshotId,
                    newSnapshotId: null,
                    cancellationToken);
            }
        }

        if (fixedDrift && requireCurrentBudgetEvidence)
        {
            var released = await ReleaseActiveBudgetReservationsAsync(
                producer.Definition.DocumentType, sourceDocumentId,
                "Finance dimension fixed-rule drift invalidated the prior budget evidence.",
                cancellationToken);
            await SetBudgetStatusAsync(header.Id, BudgetStale, null, cancellationToken);
            throw new InvalidOperationException(
                $"Finance dimension rules changed and {released.Count} budget reservation(s) were released. Refresh budget evidence before submission.");
        }

        if (requireCurrentBudgetEvidence && header.BudgetEvidenceStatus != BudgetCurrent)
            throw new InvalidOperationException(
                "Finance budget evidence is stale or has not been refreshed for the current source dimensions.");

        assignments = await LoadAssignmentsAsync(producer, sourceDocumentId, cancellationToken);
        foreach (var line in authoritativeLines)
        {
            var assignment = assignments.SingleOrDefault(item => item.SourceLineId == line.SourceLineId);
            if (assignment is null || assignment.IsFrozen) continue;
            var values = assignment.FinanceDimensionSetId.HasValue
                ? await ValuesForSetAsync(assignment.FinanceDimensionSetId.Value, cancellationToken)
                : Array.Empty<FinancePostingDimensionValueDto>();
            var resolution = await _dimensions.ResolveSourceLineAsync(
                producer, line.AccountId, documentDate, values, certification,
                refreshPersistedFixedValues: false, cancellationToken, line.AdditionalAccountIds);
            var snapshot = resolution.DimensionSet is null
                ? null
                : await CreateSnapshotAsync(producer, resolution, cancellationToken);
            await _store.FreezeLineAsync(
                producer, sourceDocumentId, line.SourceLineId,
                resolution.DimensionSet?.Id, snapshot?.Id, cancellationToken);
            await RecordChangeAsync(
                producer,
                sourceDocumentId,
                line.SourceLineId,
                resolution.DimensionSet?.Id,
                resolution.DimensionSet?.Id,
                "Source Finance dimension evidence frozen for lifecycle submission.",
                budgetEvidenceBecameStale: false,
                releasedReservationIds: [],
                previousSnapshotId: null,
                newSnapshotId: snapshot?.Id,
                cancellationToken);
        }

        var result = await GetAsync(
            producer, sourceDocumentId, documentDate, authoritativeLines, cancellationToken);
        result.ReadinessWarnings = warnings.Distinct(StringComparer.Ordinal).ToArray();
        return result;
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<FinancePostingDimensionValueDto>>> GetPostingDimensionsAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default)
    {
        var assignments = await LoadAssignmentsAsync(producer, sourceDocumentId, cancellationToken);
        var result = new Dictionary<Guid, IReadOnlyList<FinancePostingDimensionValueDto>>();
        foreach (var assignment in assignments.Where(item => item.SourceLineId.HasValue && item.FinanceDimensionSetId.HasValue))
        {
            if (assignment.FinanceDimensionSnapshotId is Guid snapshotId)
            {
                result[assignment.SourceLineId!.Value] = await _db.FinanceDimensionSnapshotItems
                    .AsNoTracking()
                    .Where(item => item.TenantId == TenantId
                        && item.FinanceDimensionSnapshotId == snapshotId
                        && !item.IsDeleted)
                    .OrderBy(item => item.DimensionCodeSnapshot)
                    .Select(item => new FinancePostingDimensionValueDto
                    {
                        DimensionCode = item.DimensionCodeSnapshot,
                        ValueCode = item.DimensionValueCodeSnapshot
                    })
                    .ToArrayAsync(cancellationToken);
                continue;
            }

            result[assignment.SourceLineId!.Value] = await ValuesForSetAsync(
                assignment.FinanceDimensionSetId!.Value, cancellationToken);
        }
        return result;
    }

    public async Task MarkBudgetEvidenceCurrentAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        string evaluationHash,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(evaluationHash) || evaluationHash.Trim().Length > 64)
            throw new InvalidOperationException("A valid Finance budget evaluation hash is required.");
        var assignments = await LoadAssignmentsAsync(producer, sourceDocumentId, cancellationToken);
        var header = assignments.SingleOrDefault(item => !item.SourceLineId.HasValue)
            ?? throw new InvalidOperationException("The source document has no trusted Finance dimension provenance.");
        await SetBudgetStatusAsync(header.Id, BudgetCurrent, evaluationHash.Trim(), cancellationToken);
    }

    public async Task InvalidateBudgetEvidenceAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        string budgetReservationSourceDocumentType,
        bool requiresReevaluation,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(budgetReservationSourceDocumentType))
            throw new InvalidOperationException("A budget reservation source document type is required.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A budget invalidation reason is required.");
        var assignments = await LoadAssignmentsAsync(producer, sourceDocumentId, cancellationToken);
        var header = assignments.SingleOrDefault(item => !item.SourceLineId.HasValue)
            ?? throw new InvalidOperationException("The source document has no trusted Finance dimension provenance.");
        await ReleaseActiveBudgetReservationsAsync(
            budgetReservationSourceDocumentType.Trim(), sourceDocumentId, reason.Trim(), cancellationToken);
        await SetBudgetStatusAsync(
            header.Id,
            requiresReevaluation ? BudgetStale : BudgetNotApplicable,
            null,
            cancellationToken);
    }

    private async Task<FinanceSourceDocumentDimensionDto> SynchronizeDraftCoreAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        DateTime documentDate,
        IReadOnlyList<FinanceSourceDocumentLineContext> authoritativeLines,
        FinanceSourceDocumentDimensionInputDto? input,
        bool inheritDefaultForUnassignedLines,
        string? budgetReservationSourceDocumentType,
        string reason,
        bool refreshPersistedFixedValues,
        CancellationToken cancellationToken)
    {
        ValidateAuthoritativeLines(authoritativeLines);
        if (documentDate == default) throw new InvalidOperationException("The source document date is required.");
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("A dimension-change reason is required.");
        var route = producer?.Definition ?? throw new ArgumentNullException(nameof(producer));
        var certification = await CertificationStateAsync(producer, cancellationToken);
        await _store.RegisterDocumentAsync(producer, sourceDocumentId, cancellationToken);
        var before = await LoadAssignmentsAsync(producer, sourceDocumentId, cancellationToken);
        var reopenedFrozenEvidence = await ReopenFrozenEvidenceForDraftAsync(
            producer, sourceDocumentId, before,
            "Source document returned to Draft; prior submission evidence was superseded.",
            cancellationToken);
        before = await LoadAssignmentsAsync(producer, sourceDocumentId, cancellationToken);
        var beforeByLine = before.Where(item => item.SourceLineId.HasValue)
            .ToDictionary(item => item.SourceLineId!.Value);
        var header = before.Single(item => !item.SourceLineId.HasValue);

        FinanceDimensionSet? defaultSet = null;
        if (input is not null)
        {
            defaultSet = await _dimensions.ResolveSourceDocumentDefaultAsync(
                documentDate, input.DefaultDimensions, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            if (defaultSet is null)
                await _store.ClearAsync(producer, sourceDocumentId, null, cancellationToken);
            else
                await _store.UpsertAsync(producer, sourceDocumentId, null, defaultSet.Id, null, cancellationToken);
        }
        else if (header.FinanceDimensionSetId.HasValue)
        {
            defaultSet = await _db.FinanceDimensionSets.AsNoTracking().Include(item => item.Items)
                .SingleAsync(item => item.Id == header.FinanceDimensionSetId.Value
                    && item.TenantId == TenantId && !item.IsDeleted, cancellationToken);
        }

        var explicitInputs = (input?.Lines ?? Array.Empty<FinanceSourceLineDimensionInputDto>())
            .ToDictionary(item => item.SourceLineId
                ?? throw new InvalidOperationException("Every persisted source-line dimension input requires a line ID."));
        var authoritative = authoritativeLines.ToDictionary(item => item.SourceLineId);
        foreach (var supplied in explicitInputs)
        {
            if (!authoritative.TryGetValue(supplied.Key, out var line))
                throw new InvalidOperationException("A Finance dimension assignment references a line outside the source document.");
            if (supplied.Value.AccountId != line.AccountId)
                throw new InvalidOperationException("A Finance dimension assignment cannot change the server-resolved source account.");
        }

        var defaultValues = defaultSet is null
            ? Array.Empty<FinancePostingDimensionValueDto>()
            : ToPostingValues(defaultSet.Items);
        var changes = new List<(Guid LineId, Guid? OldSetId, Guid? NewSetId, bool HadPrevious)>();
        var warnings = new List<string>();
        foreach (var line in authoritativeLines)
        {
            var hasExplicit = explicitInputs.TryGetValue(line.SourceLineId, out var supplied);
            var shouldApplyDefault = input?.ApplyDefaultToEligibleLines == true
                || (inheritDefaultForUnassignedLines && !beforeByLine.ContainsKey(line.SourceLineId));
            beforeByLine.TryGetValue(line.SourceLineId, out var previous);
            IReadOnlyList<FinancePostingDimensionValueDto> values = hasExplicit
                ? supplied!.Dimensions
                : shouldApplyDefault
                    ? defaultValues
                    : previous?.FinanceDimensionSetId is Guid previousSetId
                        ? await ValuesForSetAsync(previousSetId, cancellationToken)
                        : Array.Empty<FinancePostingDimensionValueDto>();
            if (!hasExplicit && shouldApplyDefault && values.Count > 0)
            {
                var prohibitedCodes = (await _dimensions.GetSourceLineRulesAsync(
                        producer, line.AccountId, documentDate, cancellationToken, line.AdditionalAccountIds))
                    .Where(rule => rule.RuleType == "Prohibited")
                    .Select(rule => rule.FinanceDimensionDefinition.Code)
                    .ToHashSet(StringComparer.Ordinal);
                values = values.Where(value => !prohibitedCodes.Contains(value.DimensionCode)).ToArray();
            }
            var resolution = await _dimensions.ResolveSourceLineAsync(
                producer, line.AccountId, documentDate, values, certification,
                refreshPersistedFixedValues, cancellationToken, line.AdditionalAccountIds);
            warnings.AddRange(resolution.ReadinessWarnings.Select(message => $"Line {line.SourceLineId}: {message}"));
            await _db.SaveChangesAsync(cancellationToken);
            var newSetId = resolution.DimensionSet?.Id;
            await _store.UpsertAsync(
                producer, sourceDocumentId, line.SourceLineId, newSetId, null, cancellationToken);
            if (previous?.FinanceDimensionSetId != newSetId)
                changes.Add((line.SourceLineId, previous?.FinanceDimensionSetId, newSetId, previous is not null));
        }

        foreach (var removed in beforeByLine.Values.Where(item =>
                     !authoritative.ContainsKey(item.SourceLineId!.Value)))
        {
            await _store.RemoveLineAsync(
                producer, sourceDocumentId, removed.SourceLineId!.Value, cancellationToken);
            changes.Add((removed.SourceLineId!.Value, removed.FinanceDimensionSetId, null, true));
        }

        var changedExistingEvidence = reopenedFrozenEvidence || changes.Any(item => item.HadPrevious);
        IReadOnlyList<Guid> released = Array.Empty<Guid>();
        if (changedExistingEvidence && !string.IsNullOrWhiteSpace(budgetReservationSourceDocumentType))
        {
            released = await ReleaseActiveBudgetReservationsAsync(
                budgetReservationSourceDocumentType!, sourceDocumentId,
                "Source Finance dimensions changed; explicit budget re-evaluation is required.",
                cancellationToken);
            await SetBudgetStatusAsync(header.Id, BudgetStale, null, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(budgetReservationSourceDocumentType)
                 && header.BudgetEvidenceStatus == BudgetNotApplicable)
        {
            await SetBudgetStatusAsync(header.Id, BudgetNotEvaluated, null, cancellationToken);
        }

        foreach (var change in changes)
            await RecordChangeAsync(
                producer, sourceDocumentId, change.LineId, change.OldSetId, change.NewSetId,
                reason, changedExistingEvidence && !string.IsNullOrWhiteSpace(budgetReservationSourceDocumentType),
                released, null, null, cancellationToken);

        var result = await GetAsync(
            producer, sourceDocumentId, documentDate, authoritativeLines, cancellationToken);
        result.ReadinessWarnings = warnings.Distinct(StringComparer.Ordinal).ToArray();
        return result;
    }

    private async Task<IReadOnlyList<FinanceSourceDimensionAssignmentDto>> LoadAssignmentsAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        CancellationToken cancellationToken) =>
        await _store.GetDocumentAssignmentsAsync(producer, sourceDocumentId, cancellationToken);

    private async Task<FinanceDimensionCertificationState> CertificationStateAsync(
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken) =>
        await _db.FinanceDimensionRouteCertifications.AsNoTracking()
            .Where(item => item.TenantId == TenantId && item.RouteId == producer.RouteId
                && !item.IsDeleted && item.EffectiveDate <= DateTime.UtcNow)
            .Select(item => (FinanceDimensionCertificationState?)item.State)
            .SingleOrDefaultAsync(cancellationToken)
        ?? producer.Definition.DefaultState;

    private async Task<IReadOnlyList<Guid>> ReleaseActiveBudgetReservationsAsync(
        string sourceDocumentType,
        Guid sourceDocumentId,
        string reason,
        CancellationToken cancellationToken)
    {
        var active = await _db.FinanceBudgetReservations.AsNoTracking()
            .Where(item => item.TenantId == TenantId && !item.IsDeleted
                && item.SourceDocumentType == sourceDocumentType
                && item.SourceDocumentId == sourceDocumentId
                && item.Status == "Reserved")
            .Select(item => new { item.Id, item.ReservationVersion })
            .ToListAsync(cancellationToken);
        if (active.Count > 0 && _budgetCommitments is null)
            throw new InvalidOperationException("Finance budget commitments are unavailable; source dimensions were not changed.");
        foreach (var reservation in active)
            await _budgetCommitments!.ReleaseAsync(
                reservation.Id,
                new ReleaseFinanceBudgetReservationDto
                {
                    Reason = reason,
                    ExpectedVersion = reservation.ReservationVersion,
                    IdempotencyKey = $"FIN-DIM:{sourceDocumentId:N}:STALE:{reservation.Id:N}:{reservation.ReservationVersion}",
                    CorrelationId = $"FIN-DIM:{sourceDocumentId:N}"
                }, cancellationToken);
        return active.Select(item => item.Id).ToArray();
    }

    private async Task SetBudgetStatusAsync(
        Guid headerAssignmentId,
        string status,
        string? evaluationHash,
        CancellationToken cancellationToken)
    {
        var header = await _db.FinanceSourceDimensionAssignments.SingleAsync(item =>
            item.Id == headerAssignmentId && item.TenantId == TenantId && !item.IsDeleted,
            cancellationToken);
        header.BudgetEvidenceStatus = status;
        header.BudgetEvaluationHash = evaluationHash;
        header.BudgetEvidenceUpdatedAt = DateTime.UtcNow;
        header.UpdatedAt = DateTime.UtcNow;
        header.UpdatedBy = _currentUser.UserName;
        header.LastModifiedById = UserId;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> ReopenFrozenEvidenceForDraftAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        IReadOnlyList<FinanceSourceDimensionAssignmentDto> assignments,
        string reason,
        CancellationToken cancellationToken)
    {
        var frozen = assignments.Where(item => item.SourceLineId.HasValue
            && item.EvidenceFrozenAt.HasValue).ToArray();
        if (frozen.Length == 0) return false;

        var ids = frozen.Select(item => item.Id).ToArray();
        var tracked = await _db.FinanceSourceDimensionAssignments
            .Where(item => item.TenantId == TenantId && ids.Contains(item.Id) && !item.IsDeleted)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        foreach (var prior in frozen)
        {
            var assignment = tracked[prior.Id];
            assignment.FinanceDimensionSnapshotId = null;
            assignment.EvidenceFrozenAt = null;
            assignment.UpdatedAt = DateTime.UtcNow;
            assignment.UpdatedBy = _currentUser.UserName;
            assignment.LastModifiedById = UserId;
        }
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var prior in frozen)
            await RecordChangeAsync(
                producer,
                sourceDocumentId,
                prior.SourceLineId,
                prior.FinanceDimensionSetId,
                prior.FinanceDimensionSetId,
                reason,
                budgetEvidenceBecameStale: false,
                releasedReservationIds: [],
                previousSnapshotId: prior.FinanceDimensionSnapshotId,
                newSnapshotId: null,
                cancellationToken);
        return true;
    }

    private async Task RecordChangeAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        Guid? sourceLineId,
        Guid? oldSetId,
        Guid? newSetId,
        string reason,
        bool budgetEvidenceBecameStale,
        IReadOnlyList<Guid> releasedReservationIds,
        Guid? previousSnapshotId,
        Guid? newSnapshotId,
        CancellationToken cancellationToken)
    {
        var ids = new[] { oldSetId, newSetId }.Where(item => item.HasValue).Select(item => item!.Value).Distinct().ToArray();
        var hashes = await _db.FinanceDimensionSets.AsNoTracking()
            .Where(item => item.TenantId == TenantId && ids.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.CombinationHash, cancellationToken);
        var route = producer.Definition;
        _db.Set<FinanceSourceDimensionChange>().Add(new FinanceSourceDimensionChange
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            RouteId = route.Id,
            ProducerModule = route.ProducerModule,
            SourceRoute = route.SourceRoute,
            SourceDocumentType = route.DocumentType,
            ContractVersion = route.ContractVersion,
            SourceDocumentId = sourceDocumentId,
            SourceLineId = sourceLineId,
            PreviousFinanceDimensionSetId = oldSetId,
            NewFinanceDimensionSetId = newSetId,
            PreviousFinanceDimensionSnapshotId = previousSnapshotId,
            NewFinanceDimensionSnapshotId = newSnapshotId,
            PreviousCombinationHash = oldSetId.HasValue && hashes.TryGetValue(oldSetId.Value, out var oldHash) ? oldHash : "NONE",
            NewCombinationHash = newSetId.HasValue && hashes.TryGetValue(newSetId.Value, out var newHash) ? newHash : "NONE",
            Reason = reason.Trim(),
            BudgetEvidenceBecameStale = budgetEvidenceBecameStale,
            ReleasedBudgetReservationIdsJson = releasedReservationIds.Count == 0
                ? null : JsonSerializer.Serialize(releasedReservationIds),
            ChangedByUserId = UserId,
            ChangedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserName,
            CreatedById = UserId
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<FinanceDimensionSnapshot> CreateSnapshotAsync(
        FinancePostingProducerContext producer,
        FinanceSourceLineResolution resolution,
        CancellationToken cancellationToken)
    {
        var set = resolution.DimensionSet!;
        if (set.Items.Count == 0)
            set = await _db.FinanceDimensionSets.AsNoTracking().Include(item => item.Items)
                .SingleAsync(item => item.Id == set.Id && item.TenantId == TenantId, cancellationToken);
        var ruleIds = resolution.AppliedRules.Select(item => item.Id).Distinct().ToArray();
        if (ruleIds.Length > 0)
        {
            var trackedRules = await _db.FinanceDimensionAccountRules
                .Where(item => item.TenantId == TenantId && ruleIds.Contains(item.Id) && !item.IsDeleted)
                .ToListAsync(cancellationToken);
            if (trackedRules.Count != ruleIds.Length)
                throw new InvalidOperationException("A Finance dimension rule changed before source evidence could be frozen.");
            foreach (var rule in trackedRules) rule.IsEvidenceLocked = true;
        }

        var route = producer.Definition;
        var rulePayload = string.Join("|", resolution.AppliedRules.OrderBy(item => item.FinanceDimensionDefinitionId).ThenBy(item => item.AccountId).ThenBy(item => item.Id)
            .Select(item => $"{item.Id:N}:{item.RuleFamilyId:N}:{item.RuleVersion}:{item.RuleType}"));
        var snapshot = new FinanceDimensionSnapshot
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            FinanceDimensionSetId = set.Id,
            CombinationHashSnapshot = set.CombinationHash,
            DisplayValueSnapshot = set.DisplayValue,
            SnapshotSource = "SourceSubmissionResolution",
            SnapshotCapturedAt = DateTime.UtcNow,
            SnapshotQuality = "Exact",
            RuleEvidenceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rulePayload))),
            ProducerModule = route.ProducerModule,
            SourceRoute = route.SourceRoute,
            SourceDocumentType = route.DocumentType,
            ContractVersion = route.ContractVersion,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserName,
            CreatedById = UserId
        };
        var snapshotRules = FinanceDimensionAdministrationService.MergeSourceAccountRules(resolution.AppliedRules);
        foreach (var item in set.Items)
        {
            // The item has one effective rule; the snapshot hash and evidence locks above include every contributing account rule.
            var rule = snapshotRules.SingleOrDefault(candidate =>
                candidate.FinanceDimensionDefinitionId == item.FinanceDimensionDefinitionId);
            snapshot.Items.Add(new FinanceDimensionSnapshotItem
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                FinanceDimensionSnapshotId = snapshot.Id,
                FinanceDimensionDefinitionId = item.FinanceDimensionDefinitionId,
                FinanceDimensionValueId = item.FinanceDimensionValueId,
                DimensionCodeSnapshot = item.DimensionCodeSnapshot,
                DimensionNameSnapshot = item.DimensionNameSnapshot,
                DimensionValueCodeSnapshot = item.DimensionValueCodeSnapshot,
                DimensionValueNameSnapshot = item.DimensionValueNameSnapshot,
                FinanceDimensionAccountRuleId = rule?.Id,
                RuleFamilyIdSnapshot = rule?.RuleFamilyId,
                RuleVersionSnapshot = rule?.RuleVersion,
                RuleTypeSnapshot = rule?.RuleType,
                RuleEffectiveDateSnapshot = rule?.EffectiveDate,
                RuleExpiryDateSnapshot = rule?.ExpiryDate,
                SnapshotSource = "SourceSubmissionResolution",
                SnapshotCapturedAt = DateTime.UtcNow,
                SnapshotQuality = "Exact",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUser.UserName,
                CreatedById = UserId
            });
        }
        _db.FinanceDimensionSnapshots.Add(snapshot);
        await _db.SaveChangesAsync(cancellationToken);
        return snapshot;
    }

    private async Task<FinanceSourceDocumentDimensionDto> MapDocumentAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        DateTime documentDate,
        IReadOnlyList<FinanceSourceDocumentLineContext> authoritativeLines,
        IReadOnlyList<FinanceSourceDimensionAssignmentDto> assignments,
        CancellationToken cancellationToken)
    {
        var header = assignments.SingleOrDefault(item => !item.SourceLineId.HasValue);
        var setIds = assignments.Where(item => item.FinanceDimensionSetId.HasValue)
            .Select(item => item.FinanceDimensionSetId!.Value).Distinct().ToArray();
        var snapshotIds = assignments.Where(item => item.FinanceDimensionSnapshotId.HasValue)
            .Select(item => item.FinanceDimensionSnapshotId!.Value).Distinct().ToArray();
        var sets = await _db.FinanceDimensionSets.AsNoTracking().Include(item => item.Items)
            .Where(item => item.TenantId == TenantId && setIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var snapshots = await _db.FinanceDimensionSnapshots.AsNoTracking().Include(item => item.Items)
            .Where(item => item.TenantId == TenantId && snapshotIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var accounts = authoritativeLines.ToDictionary(item => item.SourceLineId, item => item.AccountId);
        var contexts = authoritativeLines.ToDictionary(item => item.SourceLineId);
        var ruleTypes = new Dictionary<Guid, IReadOnlyDictionary<string, string>>();
        foreach (var line in authoritativeLines)
        {
            var assignment = assignments.SingleOrDefault(value => value.SourceLineId == line.SourceLineId);
            if (assignment?.IsFrozen == true)
            {
                ruleTypes[line.SourceLineId] = snapshots.TryGetValue(assignment.FinanceDimensionSnapshotId ?? Guid.Empty, out var frozenSnapshot)
                    ? frozenSnapshot.Items.ToDictionary(value => value.DimensionCodeSnapshot, value => value.RuleTypeSnapshot ?? "Optional", StringComparer.Ordinal)
                    : new Dictionary<string, string>(StringComparer.Ordinal);
                continue;
            }
            var rules = await _dimensions.GetSourceLineRulesAsync(
                producer, line.AccountId, documentDate, cancellationToken, line.AdditionalAccountIds);
            ruleTypes[line.SourceLineId] = rules.ToDictionary(
                rule => rule.FinanceDimensionDefinition.Code,
                rule => rule.RuleType,
                StringComparer.Ordinal);
        }
        var lines = assignments.Where(item => item.SourceLineId.HasValue && accounts.ContainsKey(item.SourceLineId.Value))
            .Select(item =>
            {
                sets.TryGetValue(item.FinanceDimensionSetId ?? Guid.Empty, out var set);
                snapshots.TryGetValue(item.FinanceDimensionSnapshotId ?? Guid.Empty, out var snapshot);
                var values = snapshot is not null
                    ? snapshot.Items.OrderBy(value => value.DimensionCodeSnapshot).Select(value => new FinanceSourceDimensionValueDto
                    {
                        DimensionCode = value.DimensionCodeSnapshot,
                        DimensionName = value.DimensionNameSnapshot,
                        ValueCode = value.DimensionValueCodeSnapshot,
                        ValueName = value.DimensionValueNameSnapshot,
                        RuleType = value.RuleTypeSnapshot,
                        IsReadOnly = value.RuleTypeSnapshot == "Fixed"
                    }).ToArray()
                    : set?.Items.OrderBy(value => value.DimensionCodeSnapshot).Select(value =>
                    {
                        ruleTypes[item.SourceLineId!.Value].TryGetValue(value.DimensionCodeSnapshot, out var ruleType);
                        return new FinanceSourceDimensionValueDto
                        {
                            DimensionCode = value.DimensionCodeSnapshot,
                            DimensionName = value.DimensionNameSnapshot,
                            ValueCode = value.DimensionValueCodeSnapshot,
                            ValueName = value.DimensionValueNameSnapshot,
                            RuleType = ruleType,
                            IsReadOnly = string.Equals(ruleType, "Fixed", StringComparison.Ordinal)
                        };
                    }).ToArray() ?? Array.Empty<FinanceSourceDimensionValueDto>();
                return new FinanceSourceLineDimensionDto
                {
                    SourceLineId = item.SourceLineId!.Value,
                    AccountId = accounts[item.SourceLineId.Value],
                    AdditionalAccountIds = (contexts[item.SourceLineId.Value].AdditionalAccountIds ?? [])
                        .Where(accountId => accountId != accounts[item.SourceLineId.Value]).Distinct().ToArray(),
                    RequiredDimensionCodes = item.IsFrozen ? [] : ruleTypes[item.SourceLineId.Value].Where(rule => rule.Value == "Required").Select(rule => rule.Key).ToArray(),
                    FinanceDimensionSetId = item.FinanceDimensionSetId,
                    CombinationHash = snapshot?.CombinationHashSnapshot ?? set?.CombinationHash,
                    DisplayValue = snapshot?.DisplayValueSnapshot ?? set?.DisplayValue,
                    IsFrozen = item.IsFrozen,
                    Values = values,
                    ReadinessWarnings = item.IsFrozen ? [] : ruleTypes[item.SourceLineId.Value]
                        .Where(rule => rule.Value == "Required" && !values.Any(value => value.DimensionCode == rule.Key))
                        .Select(rule => $"Dimension {rule.Key} is required for this line's posting accounts.").ToArray()
                };
            }).ToArray();
        var defaultValues = header?.FinanceDimensionSetId is Guid defaultId && sets.TryGetValue(defaultId, out var defaultSet)
            ? defaultSet.Items.OrderBy(value => value.DimensionCodeSnapshot).Select(value => new FinanceSourceDimensionValueDto
            {
                DimensionCode = value.DimensionCodeSnapshot,
                DimensionName = value.DimensionNameSnapshot,
                ValueCode = value.DimensionValueCodeSnapshot,
                ValueName = value.DimensionValueNameSnapshot
            }).ToArray()
            : Array.Empty<FinanceSourceDimensionValueDto>();
        return new FinanceSourceDocumentDimensionDto
        {
            RouteId = producer.RouteId,
            CertificationState = await CertificationStateAsync(producer, cancellationToken),
            SourceDocumentId = sourceDocumentId,
            DefaultFinanceDimensionSetId = header?.FinanceDimensionSetId,
            DefaultValues = defaultValues,
            Lines = lines,
            ReadinessWarnings = lines.SelectMany(line => line.ReadinessWarnings.Select(warning => $"Line {line.SourceLineId}: {warning}"))
                .Distinct(StringComparer.Ordinal).ToArray(),
            BudgetEvidenceStatus = header?.BudgetEvidenceStatus ?? BudgetNotApplicable,
            BudgetEvaluationHash = header?.BudgetEvaluationHash,
            BudgetEvidenceUpdatedAt = header?.BudgetEvidenceUpdatedAt
        };
    }

    private async Task<IReadOnlyList<FinancePostingDimensionValueDto>> ValuesForSetAsync(
        Guid setId,
        CancellationToken cancellationToken)
    {
        var items = await _db.FinanceDimensionSetItems.AsNoTracking()
            .Where(item => item.TenantId == TenantId && item.FinanceDimensionSetId == setId && !item.IsDeleted)
            .OrderBy(item => item.DimensionCodeSnapshot)
            .ToListAsync(cancellationToken);
        return ToPostingValues(items);
    }

    private static FinancePostingDimensionValueDto[] ToPostingValues(
        IEnumerable<FinanceDimensionSetItem> items) =>
        items.OrderBy(item => item.DimensionCodeSnapshot).Select(item => new FinancePostingDimensionValueDto
        {
            DimensionCode = item.DimensionCodeSnapshot,
            ValueCode = item.DimensionValueCodeSnapshot
        }).ToArray();

    private static void ValidateAuthoritativeLines(IReadOnlyList<FinanceSourceDocumentLineContext> lines)
    {
        if (lines.Any(item => item.SourceLineId == Guid.Empty || item.AccountId == Guid.Empty ||
            (item.AdditionalAccountIds?.Any(accountId => accountId == Guid.Empty) ?? false)))
            throw new InvalidOperationException("Every Finance source line requires a stable line ID and server-resolved account.");
        if (lines.Select(item => item.SourceLineId).Distinct().Count() != lines.Count)
            throw new InvalidOperationException("A Finance source document contains duplicate line identities.");
    }
}
