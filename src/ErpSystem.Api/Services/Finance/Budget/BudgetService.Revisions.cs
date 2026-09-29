using System.Data;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ErpSystem.Api.Services.Finance.Budget;

public partial class BudgetService
{
    private const string BudgetRevisionWorkflowType = "BudgetRevision";
    private const string VirementType = "Virement";
    private const string SupplementaryType = "Supplementary";
    private const string AppliedStatus = "Applied";

    public async Task<IReadOnlyList<BudgetRevisionDto>> GetRevisionsAsync(Guid? fiscalYearId = null)
    {
        var tenantId = TenantId;
        var query = _context.BudgetRevisions
            .AsNoTracking()
            .Where(revision => revision.TenantId == tenantId && !revision.IsDeleted);
        if (fiscalYearId.HasValue)
            query = query.Where(revision => revision.SourceScenario!.FiscalYearId == fiscalYearId.Value);

        var revisions = await query
            .OrderByDescending(revision => revision.CreatedAt)
            .Select(revision => new BudgetRevisionSummaryRow
            {
                Id = revision.Id,
                RevisionNumber = revision.RevisionNumber,
                RevisionType = revision.RevisionType,
                SourceScenarioId = revision.SourceScenarioId,
                SourceScenarioName = revision.SourceScenario == null ? string.Empty : revision.SourceScenario.Name,
                FiscalYearId = revision.SourceScenario == null ? Guid.Empty : revision.SourceScenario.FiscalYearId,
                FiscalYearName = revision.SourceScenario == null || revision.SourceScenario.FiscalYear == null
                    ? string.Empty
                    : revision.SourceScenario.FiscalYear.FiscalYearName,
                ResultScenarioId = revision.ResultScenarioId,
                ResultScenarioName = revision.ResultScenario == null ? null : revision.ResultScenario.Name,
                EffectiveDate = revision.EffectiveDate,
                BoardResolutionReference = revision.BoardResolutionReference,
                BoardResolutionDate = revision.BoardResolutionDate,
                Justification = revision.Justification,
                Status = revision.Status,
                IncreaseAmountBase = revision.Lines
                    .Where(line => !line.IsDeleted && line.AdjustmentAmountBase > 0m)
                    .Sum(line => (decimal?)line.AdjustmentAmountBase) ?? 0m,
                ReductionAmountBase = revision.Lines
                    .Where(line => !line.IsDeleted && line.AdjustmentAmountBase < 0m)
                    .Sum(line => (decimal?)line.AdjustmentAmountBase) ?? 0m,
                NetChangeAmountBase = revision.Lines
                    .Where(line => !line.IsDeleted)
                    .Sum(line => (decimal?)line.AdjustmentAmountBase) ?? 0m,
                SubmittedAt = revision.SubmittedAt,
                ApprovedAt = revision.ApprovedAt,
                AppliedAt = revision.AppliedAt,
                RejectionReason = revision.RejectionReason,
                CreatedAt = revision.CreatedAt,
                RowVersion = revision.RowVersion
            })
            .ToListAsync();

        return revisions.Select(MapRevisionSummary).ToList();
    }

    public async Task<BudgetRevisionDto> GetRevisionAsync(Guid id)
        => await MapRevisionAsync(await RequireRevisionAsync(id, asTracking: false));

    public async Task<BudgetRevisionDto> CreateRevisionAsync(CreateBudgetRevisionDto dto)
    {
        var source = await RequireOfficialSourceAsync(dto.SourceScenarioId);
        await ValidateRevisionAsync(dto, source);

        var now = DateTime.UtcNow;
        var revision = new BudgetRevision
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            RevisionNumber = await GenerateBudgetRevisionNumberAsync(now),
            RevisionType = NormalizeRevisionType(dto.RevisionType),
            SourceScenarioId = source.Id,
            EffectiveDate = dto.EffectiveDate.Date,
            BoardResolutionReference = dto.BoardResolutionReference.Trim(),
            BoardResolutionDate = dto.BoardResolutionDate.Date,
            Justification = dto.Justification.Trim(),
            Status = DraftStatus,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName,
            CreatedById = CurrentUserId,
            Lines = dto.Lines.Select(line => CreateRevisionLine(line, now)).ToList()
        };

        _context.BudgetRevisions.Add(revision);
        await _context.SaveChangesAsync();
        await RecordAuditAsync(
            FinanceAuditEvents.BudgetRevisionCreated,
            nameof(BudgetRevision),
            revision.Id,
            null,
            new
            {
                revision.RevisionNumber,
                revision.RevisionType,
                revision.SourceScenarioId,
                revision.BoardResolutionReference,
                LineCount = revision.Lines.Count,
                NetChange = revision.Lines.Sum(line => line.AdjustmentAmountBase)
            },
            revision.Justification);

        return await GetRevisionAsync(revision.Id);
    }

    public async Task<BudgetRevisionDto> UpdateRevisionAsync(Guid id, UpdateBudgetRevisionDto dto)
    {
        var revision = await RequireRevisionAsync(id, asTracking: true);
        if (revision.Status is not (DraftStatus or RejectedStatus))
            throw new InvalidOperationException("Only Draft or Rejected budget revisions can be edited.");

        ApplyRowVersion(revision, dto.RowVersion);
        var source = await RequireOfficialSourceAsync(dto.SourceScenarioId);
        await ValidateRevisionAsync(dto, source);
        var before = new
        {
            revision.RevisionType,
            revision.SourceScenarioId,
            revision.EffectiveDate,
            revision.BoardResolutionReference,
            revision.BoardResolutionDate,
            revision.Justification,
            LineCount = revision.Lines.Count
        };

        // Replacing Draft lines is deliberate: the unique budget-cell key is easier
        // to reason about than a mixture of inserts, edits and soft deletions. No
        // approved evidence is lost because non-Draft revisions cannot enter here.
        _context.BudgetRevisionLines.RemoveRange(revision.Lines);
        revision.Lines.Clear();
        var now = DateTime.UtcNow;
        revision.RevisionType = NormalizeRevisionType(dto.RevisionType);
        revision.SourceScenarioId = source.Id;
        revision.SourceScenario = source;
        revision.EffectiveDate = dto.EffectiveDate.Date;
        revision.BoardResolutionReference = dto.BoardResolutionReference.Trim();
        revision.BoardResolutionDate = dto.BoardResolutionDate.Date;
        revision.Justification = dto.Justification.Trim();
        revision.Status = DraftStatus;
        revision.RejectionReason = null;
        revision.UpdatedAt = now;
        revision.UpdatedBy = _currentUserService.UserName;
        revision.LastModifiedById = CurrentUserId;
        foreach (var line in dto.Lines)
            revision.Lines.Add(CreateRevisionLine(line, now));

        await _context.SaveChangesAsync();
        await RecordAuditAsync(
            FinanceAuditEvents.BudgetRevisionUpdated,
            nameof(BudgetRevision),
            revision.Id,
            before,
            new
            {
                revision.RevisionType,
                revision.SourceScenarioId,
                revision.EffectiveDate,
                revision.BoardResolutionReference,
                revision.BoardResolutionDate,
                revision.Justification,
                LineCount = revision.Lines.Count
            },
            revision.Justification);
        return await GetRevisionAsync(revision.Id);
    }

    public async Task<BudgetRevisionDto> SubmitRevisionAsync(Guid id, string rowVersion)
    {
        var revision = await RequireRevisionAsync(id, asTracking: true);
        if (revision.Status is not (DraftStatus or RejectedStatus))
            throw new InvalidOperationException("Only Draft or Rejected budget revisions can be submitted.");
        ApplyRowVersion(revision, rowVersion);

        var source = await RequireOfficialSourceAsync(revision.SourceScenarioId);
        await ValidateRevisionAsync(ToValidationDto(revision), source);

        await using var transaction = await BeginRevisionTransactionAsync();
        try
        {
            var now = DateTime.UtcNow;
            revision.Status = SubmittedStatus;
            revision.SubmittedAt = now;
            revision.SubmittedByUserId = CurrentUserId;
            revision.ApprovedAt = null;
            revision.ApprovedByUserId = null;
            revision.RejectionReason = null;
            revision.UpdatedAt = now;
            revision.LastModifiedById = CurrentUserId;
            await _context.SaveChangesAsync();

            var workflow = await _workflowService.StartApprovalWorkflowAsync(BudgetRevisionWorkflowType, revision.Id);
            if (!workflow.Success)
                throw new InvalidOperationException(workflow.Message ?? "Unable to start budget revision approval workflow.");

            await RecordAuditAsync(
                FinanceAuditEvents.BudgetRevisionSubmitted,
                nameof(BudgetRevision),
                revision.Id,
                new { Status = DraftStatus },
                new { revision.Status, revision.SubmittedAt, revision.SubmittedByUserId },
                revision.Justification,
                workflow.WorkflowInstanceId);
            if (transaction != null)
                await transaction.CommitAsync();
        }
        catch
        {
            if (transaction != null)
                await transaction.RollbackAsync();
            throw;
        }

        return await GetRevisionAsync(revision.Id);
    }

    public async Task<BudgetRevisionDto> ApplyRevisionAsync(Guid id, string rowVersion)
    {
        var revision = await RequireRevisionAsync(id, asTracking: true);
        if (revision.Status != ApprovedStatus)
            throw new InvalidOperationException("Only an approved budget revision can be applied.");
        if (revision.ResultScenarioId.HasValue)
            throw new InvalidOperationException("This budget revision has already been applied.");
        if (revision.SubmittedByUserId == CurrentUserId)
            throw new InvalidOperationException(
                "Maker-checker control prevents the revision preparer from applying the approved change.");
        ApplyRowVersion(revision, rowVersion);

        var source = await RequireOfficialSourceAsync(revision.SourceScenarioId);
        await ValidateRevisionAsync(ToValidationDto(revision), source);

        await using var transaction = await BeginRevisionTransactionAsync();
        try
        {
            var now = DateTime.UtcNow;
            var userId = CurrentUserId;
            var reason = $"{revision.RevisionNumber}: {revision.Justification}";

            // Deactivate the old official row before inserting the successor. This
            // ordering is required by the filtered unique index that permits exactly
            // one active scenario per tenant/fiscal year.
            source.IsActive = false;
            source.Status = SupersededStatus;
            source.SupersededAt = now;
            source.SupersededByUserId = userId;
            source.SupersessionReason = reason;
            source.UpdatedAt = now;
            source.LastModifiedById = userId;
            await _context.SaveChangesAsync();

            var versionNumber = await _context.BudgetScenarios
                .Where(scenario => scenario.TenantId == TenantId
                    && scenario.FiscalYearId == source.FiscalYearId
                    && !scenario.IsDeleted)
                .MaxAsync(scenario => (int?)scenario.VersionNumber) ?? 1;
            versionNumber++;

            var successor = CloneOfficialScenario(source, revision, versionNumber, now, userId);
            ApplyRevisionLines(successor, revision, now, userId);
            _context.BudgetScenarios.Add(successor);

            revision.Status = AppliedStatus;
            revision.ResultScenarioId = successor.Id;
            revision.AppliedAt = now;
            revision.AppliedByUserId = userId;
            revision.UpdatedAt = now;
            revision.LastModifiedById = userId;
            await _context.SaveChangesAsync();

            await RecordAuditAsync(
                FinanceAuditEvents.BudgetRevisionApplied,
                nameof(BudgetRevision),
                revision.Id,
                new { Status = ApprovedStatus, OfficialScenarioId = source.Id },
                new
                {
                    revision.Status,
                    revision.ResultScenarioId,
                    successor.VersionNumber,
                    successor.VersionType,
                    NetChange = revision.Lines.Sum(line => line.AdjustmentAmountBase)
                },
                reason);
            await RecordAuditAsync(
                FinanceAuditEvents.BudgetScenarioSuperseded,
                nameof(BudgetScenario),
                source.Id,
                new { Status = ApprovedStatus, IsActive = true },
                new { source.Status, source.IsActive, ReplacementScenarioId = successor.Id },
                reason);
            await RecordAuditAsync(
                FinanceAuditEvents.BudgetScenarioAdopted,
                nameof(BudgetScenario),
                successor.Id,
                null,
                new
                {
                    successor.Status,
                    successor.IsActive,
                    successor.AdoptedAt,
                    successor.AdoptionEffectiveDate,
                    ReplacedScenarioId = source.Id,
                    BudgetRevisionId = revision.Id
                },
                reason);

            if (transaction != null)
                await transaction.CommitAsync();
        }
        catch
        {
            if (transaction != null)
                await transaction.RollbackAsync();
            throw;
        }

        return await GetRevisionAsync(revision.Id);
    }

    private async Task<BudgetRevision> RequireRevisionAsync(Guid id, bool asTracking)
    {
        var query = _context.BudgetRevisions
            .Where(revision => revision.TenantId == TenantId && revision.Id == id && !revision.IsDeleted)
            .Include(revision => revision.SourceScenario)!.ThenInclude(scenario => scenario!.FiscalYear)
            .Include(revision => revision.SourceScenario)!.ThenInclude(scenario => scenario!.BudgetReturns)
                .ThenInclude(budgetReturn => budgetReturn.BudgetEntries)
            .Include(revision => revision.ResultScenario)
            .Include(revision => revision.Lines.Where(line => !line.IsDeleted))
                .ThenInclude(line => line.Account)
            .Include(revision => revision.Lines.Where(line => !line.IsDeleted))
                .ThenInclude(line => line.FiscalPeriod)
            .Include(revision => revision.Lines.Where(line => !line.IsDeleted))
                .ThenInclude(line => line.SegmentValue)
            .Include(revision => revision.Lines.Where(line => !line.IsDeleted))
                .ThenInclude(line => line.FinanceDimensionSet)!
                    .ThenInclude(set => set!.Items)
                        .ThenInclude(item => item.FinanceDimensionDefinition)
            .Include(revision => revision.Lines.Where(line => !line.IsDeleted))
                .ThenInclude(line => line.FinanceDimensionSet)!
                    .ThenInclude(set => set!.Items)
                        .ThenInclude(item => item.FinanceDimensionValue)
            .AsSplitQuery();
        if (!asTracking)
            query = query.AsNoTracking();

        return await query.SingleOrDefaultAsync()
            ?? throw new KeyNotFoundException("Budget revision not found.");
    }

    private async Task<BudgetScenario> RequireOfficialSourceAsync(Guid id)
    {
        var scenario = await _context.BudgetScenarios
            .Include(item => item.FiscalYear)
            .Include(item => item.ControlDimensions.Where(control => !control.IsDeleted))
            .Include(item => item.BudgetReturns.Where(budgetReturn => !budgetReturn.IsDeleted))
                .ThenInclude(budgetReturn => budgetReturn.BudgetEntries.Where(entry => !entry.IsDeleted))
                    .ThenInclude(entry => entry.FinanceDimensionSet)!
                        .ThenInclude(set => set!.Items)
            .AsSplitQuery()
            .SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == id && !item.IsDeleted);
        if (scenario == null)
            throw new KeyNotFoundException("Source budget scenario not found.");
        if (!scenario.IsActive || scenario.Status != ApprovedStatus)
            throw new InvalidOperationException(
                "Budget revisions must be raised against the currently adopted approved budget.");
        return scenario;
    }

    private async Task ValidateRevisionAsync(CreateBudgetRevisionDto dto, BudgetScenario source)
    {
        var type = NormalizeRevisionType(dto.RevisionType);
        if (dto.EffectiveDate == default)
            throw new InvalidOperationException("An effective date is required.");
        if (source.FiscalYear != null
            && (dto.EffectiveDate.Date < source.FiscalYear.StartDate.Date
                || dto.EffectiveDate.Date > source.FiscalYear.EndDate.Date))
            throw new InvalidOperationException("The effective date must fall within the source fiscal year.");
        if (string.IsNullOrWhiteSpace(dto.BoardResolutionReference))
            throw new InvalidOperationException("A Board resolution reference is required.");
        if (dto.BoardResolutionDate == default || dto.BoardResolutionDate.Date > DateTime.UtcNow.Date)
            throw new InvalidOperationException("A valid, non-future Board resolution date is required.");
        if (string.IsNullOrWhiteSpace(dto.Justification) || dto.Justification.Trim().Length < 20)
            throw new InvalidOperationException("The budget revision justification must contain at least 20 characters.");
        if (dto.Lines.Count == 0)
            throw new InvalidOperationException("At least one budget revision line is required.");
        if (dto.Lines.Any(line => line.AdjustmentAmountBase == 0m))
            throw new InvalidOperationException("Budget revision lines cannot contain zero adjustments.");

        var duplicateCell = dto.Lines
            .GroupBy(line => (line.SegmentValueId, line.AccountId, line.FiscalPeriodId, line.FinanceDimensionSetId))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateCell != null)
            throw new InvalidOperationException(
                "Each return, account, period and controlling-dimension combination can appear only once in a revision.");

        var accountIds = dto.Lines.Select(line => line.AccountId).Distinct().ToArray();
        var validAccountIds = await _context.Accounts
            .Where(account => account.TenantId == TenantId
                && accountIds.Contains(account.Id)
                && !account.IsDeleted
                // Reuse the same eligibility gate as the established budget-entry
                // workspace. A revision must not introduce an inactive or header GL
                // account that normal departmental budgeting could never select.
                && account.Status == AccountStatus.Active
                && account.AllowDirectPosting)
            .Select(account => account.Id)
            .ToListAsync();
        if (validAccountIds.Count != accountIds.Length)
            throw new InvalidOperationException("One or more budget revision accounts are invalid for this tenant.");

        var periodIds = dto.Lines.Select(line => line.FiscalPeriodId).Distinct().ToArray();
        var validPeriodIds = await _context.FiscalPeriods
            .Where(period => period.TenantId == TenantId
                && period.FiscalYearId == source.FiscalYearId
                && periodIds.Contains(period.Id)
                && !period.IsDeleted)
            .Select(period => period.Id)
            .ToListAsync();
        if (validPeriodIds.Count != periodIds.Length)
            throw new InvalidOperationException("Every revision period must belong to the source fiscal year.");

        var segmentIds = dto.Lines
            .Where(line => line.SegmentValueId.HasValue)
            .Select(line => line.SegmentValueId!.Value)
            .Distinct()
            .ToArray();
        if (segmentIds.Length > 0)
        {
            var validSegmentCount = await _context.SegmentLookupValues.CountAsync(segment =>
                segment.TenantId == TenantId && segmentIds.Contains(segment.Id) && !segment.IsDeleted);
            if (validSegmentCount != segmentIds.Length)
                throw new InvalidOperationException("One or more cost-centre values are invalid for this tenant.");
        }

        var controlDimensionIds = source.ControlDimensions
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.DisplayOrder)
            .Select(item => item.FinanceDimensionDefinitionId)
            .ToHashSet();
        var requestedSetIds = dto.Lines
            .Where(line => line.FinanceDimensionSetId.HasValue)
            .Select(line => line.FinanceDimensionSetId!.Value)
            .Distinct()
            .ToArray();
        var dimensionSets = requestedSetIds.Length == 0
            ? new Dictionary<Guid, FinanceDimensionSet>()
            : await _context.FinanceDimensionSets.AsNoTracking()
                .Include(set => set.Items.Where(item => !item.IsDeleted))
                .Where(set => set.TenantId == TenantId && requestedSetIds.Contains(set.Id) && !set.IsDeleted)
                .ToDictionaryAsync(set => set.Id);
        if (dimensionSets.Count != requestedSetIds.Length)
            throw new InvalidOperationException("One or more revision dimension combinations are invalid for this tenant.");

        foreach (var line in dto.Lines)
        {
            if (controlDimensionIds.Count == 0)
            {
                if (line.FinanceDimensionSetId.HasValue)
                    throw new InvalidOperationException(
                        "A legacy account-period budget revision cannot introduce controlling dimensions.");
                continue;
            }

            if (!line.FinanceDimensionSetId.HasValue
                || !dimensionSets.TryGetValue(line.FinanceDimensionSetId.Value, out var dimensionSet))
                throw new InvalidOperationException(
                    "Every revision line must select the full controlling-dimension combination used by the official budget.");

            var assignmentIds = dimensionSet.Items
                .Where(item => !item.IsDeleted)
                .Select(item => item.FinanceDimensionDefinitionId)
                .ToHashSet();
            if (!assignmentIds.SetEquals(controlDimensionIds))
                throw new InvalidOperationException(
                    "A revision dimension combination must contain exactly one value for every scenario control dimension.");

            var combinationBelongsToSource = source.BudgetReturns
                .Where(item => !item.IsDeleted && item.SegmentValueId == line.SegmentValueId)
                .SelectMany(item => item.BudgetEntries.Where(entry => !entry.IsDeleted))
                .Any(entry => entry.FinanceDimensionSetId == dimensionSet.Id);
            if (!combinationBelongsToSource)
                throw new InvalidOperationException(
                    "A revision dimension combination must already be governed by the selected official budget return.");
        }

        var netChange = decimal.Round(dto.Lines.Sum(line => line.AdjustmentAmountBase), 2);
        if (type == VirementType
            && (netChange != 0m
                || !dto.Lines.Any(line => line.AdjustmentAmountBase < 0m)
                || !dto.Lines.Any(line => line.AdjustmentAmountBase > 0m)))
            throw new InvalidOperationException(
                "A virement must contain both releases and increases whose base-currency total is exactly zero.");
        if (type == SupplementaryType && dto.Lines.Any(line => line.AdjustmentAmountBase < 0m))
            throw new InvalidOperationException(
                "A supplementary budget can only increase approved balances; use a virement to reallocate funds.");

        var currentAmounts = BuildCurrentAmounts(source);
        foreach (var line in dto.Lines)
        {
            var key = new BudgetCell(
                line.SegmentValueId, line.AccountId, line.FiscalPeriodId, line.FinanceDimensionSetId);
            var revisedAmount = currentAmounts.GetValueOrDefault(key) + line.AdjustmentAmountBase;
            if (revisedAmount < 0m)
                throw new InvalidOperationException(
                    "A budget revision cannot reduce a cost-centre, account and period balance below zero.");
        }
    }

    private BudgetScenario CloneOfficialScenario(
        BudgetScenario source,
        BudgetRevision revision,
        int versionNumber,
        DateTime now,
        Guid userId)
    {
        var successor = new BudgetScenario
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Name = BuildSuccessorScenarioName(source.Name, revision.RevisionType, versionNumber),
            Description = Truncate(
                $"Approved {revision.RevisionType.ToLowerInvariant()} {revision.RevisionNumber}. {revision.Justification}",
                500),
            VersionType = revision.RevisionType,
            VersionNumber = versionNumber,
            ParentScenarioId = source.Id,
            FiscalYearId = source.FiscalYearId,
            BaseCurrencyCode = source.BaseCurrencyCode,
            Status = ApprovedStatus,
            IsActive = true,
            LockedDate = now,
            LockedByUserId = userId,
            AdoptedAt = now,
            AdoptionEffectiveDate = revision.EffectiveDate.Date,
            AdoptedByUserId = userId,
            AdoptionReason = $"{revision.RevisionNumber}; Board resolution {revision.BoardResolutionReference}",
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName,
            CreatedById = userId
        };

        foreach (var control in source.ControlDimensions.Where(item => !item.IsDeleted))
        {
            successor.ControlDimensions.Add(new BudgetScenarioControlDimension
            {
                Id = Guid.NewGuid(), TenantId = TenantId,
                BudgetScenarioId = successor.Id,
                FinanceDimensionDefinitionId = control.FinanceDimensionDefinitionId,
                DisplayOrder = control.DisplayOrder,
                CreatedAt = now, CreatedBy = _currentUserService.UserName,
                CreatedById = userId
            });
        }

        foreach (var sourceReturn in source.BudgetReturns.Where(item => !item.IsDeleted))
        {
            var clonedReturn = new BudgetReturn
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BudgetScenarioId = successor.Id,
                SegmentValueId = sourceReturn.SegmentValueId,
                AssignedToUserId = sourceReturn.AssignedToUserId,
                ApproverUserId = sourceReturn.ApproverUserId,
                Status = ApprovedStatus,
                Notes = $"Copied from official budget {source.Name} by {revision.RevisionNumber}.",
                SubmittedDate = sourceReturn.SubmittedDate,
                ApprovedDate = now,
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName,
                CreatedById = userId
            };
            foreach (var sourceEntry in sourceReturn.BudgetEntries.Where(item => !item.IsDeleted))
            {
                clonedReturn.BudgetEntries.Add(new BudgetEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    BudgetReturnId = clonedReturn.Id,
                    AccountId = sourceEntry.AccountId,
                    FiscalPeriodId = sourceEntry.FiscalPeriodId,
                    FinanceDimensionSetId = sourceEntry.FinanceDimensionSetId,
                    CurrencyCode = sourceEntry.CurrencyCode,
                    ExchangeRate = sourceEntry.ExchangeRate,
                    Amount = sourceEntry.Amount,
                    AmountBase = sourceEntry.AmountBase,
                    Notes = sourceEntry.Notes,
                    CreatedAt = now,
                    CreatedBy = _currentUserService.UserName,
                    CreatedById = userId
                });
            }
            successor.BudgetReturns.Add(clonedReturn);
        }
        return successor;
    }

    private void ApplyRevisionLines(
        BudgetScenario successor,
        BudgetRevision revision,
        DateTime now,
        Guid userId)
    {
        foreach (var adjustment in revision.Lines.Where(line => !line.IsDeleted))
        {
            var budgetReturn = successor.BudgetReturns
                .SingleOrDefault(item => item.SegmentValueId == adjustment.SegmentValueId);
            if (budgetReturn == null)
            {
                // Supplementary authority can introduce a previously unbudgeted
                // cost centre. The generated return is already Approved because the
                // Board-approved revision, not a new departmental return, is its source.
                budgetReturn = new BudgetReturn
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    BudgetScenarioId = successor.Id,
                    SegmentValueId = adjustment.SegmentValueId,
                    Status = ApprovedStatus,
                    Notes = $"Created by approved budget revision {revision.RevisionNumber}.",
                    SubmittedDate = revision.SubmittedAt,
                    ApprovedDate = revision.ApprovedAt,
                    CreatedAt = now,
                    CreatedBy = _currentUserService.UserName,
                    CreatedById = userId
                };
                successor.BudgetReturns.Add(budgetReturn);
            }

            var matchingEntries = budgetReturn.BudgetEntries.Where(item =>
                item.AccountId == adjustment.AccountId
                && item.FiscalPeriodId == adjustment.FiscalPeriodId
                && item.FinanceDimensionSetId == adjustment.FinanceDimensionSetId).ToList();
            if (matchingEntries.Count > 1)
                throw new InvalidOperationException(
                    "This revision line is ambiguous because the account and period contain multiple dimension-grained budget cells.");
            var entry = matchingEntries.SingleOrDefault();
            if (entry == null)
            {
                entry = new BudgetEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    BudgetReturnId = budgetReturn.Id,
                    AccountId = adjustment.AccountId,
                    FiscalPeriodId = adjustment.FiscalPeriodId,
                    FinanceDimensionSetId = adjustment.FinanceDimensionSetId,
                    CurrencyCode = successor.BaseCurrencyCode,
                    ExchangeRate = 1m,
                    CreatedAt = now,
                    CreatedBy = _currentUserService.UserName,
                    CreatedById = userId
                };
                budgetReturn.BudgetEntries.Add(entry);
            }

            // Revision figures are authorized in the scenario base currency. Both
            // source and base amounts therefore change together at a 1.0 snapshot.
            entry.CurrencyCode = successor.BaseCurrencyCode;
            entry.ExchangeRate = 1m;
            entry.AmountBase = decimal.Round(entry.AmountBase + adjustment.AdjustmentAmountBase, 2);
            entry.Amount = entry.AmountBase;
            entry.Notes = $"{revision.RevisionNumber}: {adjustment.Notes ?? revision.Justification}";
            entry.UpdatedAt = now;
            entry.UpdatedBy = _currentUserService.UserName;
            entry.LastModifiedById = userId;
        }
    }

    private async Task<BudgetRevisionDto> MapRevisionAsync(BudgetRevision revision)
    {
        var source = revision.SourceScenario
            ?? await RequireOfficialOrHistoricalScenarioAsync(revision.SourceScenarioId);
        var currentAmounts = BuildCurrentAmounts(source);
        var lines = revision.Lines
            .Where(line => !line.IsDeleted)
            .OrderBy(line => line.SegmentValue?.SegmentValue)
            .ThenBy(line => line.Account?.AccountCode)
            .ThenBy(line => line.FiscalPeriod?.PeriodNumber)
            .Select(line =>
            {
                var current = currentAmounts.GetValueOrDefault(
                    new BudgetCell(
                        line.SegmentValueId, line.AccountId, line.FiscalPeriodId, line.FinanceDimensionSetId));
                return new BudgetRevisionLineDto
                {
                    Id = line.Id,
                    SegmentValueId = line.SegmentValueId,
                    SegmentCode = line.SegmentValue?.SegmentValue ?? string.Empty,
                    SegmentName = line.SegmentValue?.Description ?? line.SegmentValue?.SegmentValue ?? "General",
                    FinanceDimensionSetId = line.FinanceDimensionSetId,
                    DimensionCombination = line.FinanceDimensionSet?.DisplayValue ?? string.Empty,
                    DimensionAssignments = line.FinanceDimensionSet?.Items
                        .Where(item => !item.IsDeleted)
                        .OrderBy(item => item.DimensionCodeSnapshot)
                        .Select(item => new BudgetDimensionAssignmentDto
                        {
                            FinanceDimensionDefinitionId = item.FinanceDimensionDefinitionId,
                            FinanceDimensionValueId = item.FinanceDimensionValueId,
                            DimensionCode = item.DimensionCodeSnapshot,
                            DimensionName = item.FinanceDimensionDefinition?.Name ?? item.DimensionCodeSnapshot,
                            ValueCode = item.DimensionValueCodeSnapshot,
                            ValueName = item.FinanceDimensionValue?.Name ?? item.DimensionValueCodeSnapshot
                        }).ToArray() ?? Array.Empty<BudgetDimensionAssignmentDto>(),
                    AccountId = line.AccountId,
                    AccountCode = line.Account?.AccountCode ?? string.Empty,
                    AccountName = line.Account?.AccountName ?? string.Empty,
                    FiscalPeriodId = line.FiscalPeriodId,
                    PeriodCode = line.FiscalPeriod?.PeriodCode ?? string.Empty,
                    PeriodName = line.FiscalPeriod?.PeriodName ?? string.Empty,
                    CurrentAmountBase = current,
                    AdjustmentAmountBase = line.AdjustmentAmountBase,
                    RevisedAmountBase = current + line.AdjustmentAmountBase,
                    Notes = line.Notes
                };
            })
            .ToList();

        return new BudgetRevisionDto
        {
            Id = revision.Id,
            RevisionNumber = revision.RevisionNumber,
            RevisionType = revision.RevisionType,
            SourceScenarioId = revision.SourceScenarioId,
            SourceScenarioName = source.Name,
            FiscalYearId = source.FiscalYearId,
            FiscalYearName = source.FiscalYear?.FiscalYearName ?? string.Empty,
            ResultScenarioId = revision.ResultScenarioId,
            ResultScenarioName = revision.ResultScenario?.Name,
            EffectiveDate = revision.EffectiveDate,
            BoardResolutionReference = revision.BoardResolutionReference,
            BoardResolutionDate = revision.BoardResolutionDate,
            Justification = revision.Justification,
            Status = revision.Status,
            IncreaseAmountBase = lines.Where(line => line.AdjustmentAmountBase > 0m).Sum(line => line.AdjustmentAmountBase),
            ReductionAmountBase = Math.Abs(lines.Where(line => line.AdjustmentAmountBase < 0m).Sum(line => line.AdjustmentAmountBase)),
            NetChangeAmountBase = lines.Sum(line => line.AdjustmentAmountBase),
            SubmittedAt = revision.SubmittedAt,
            ApprovedAt = revision.ApprovedAt,
            AppliedAt = revision.AppliedAt,
            RejectionReason = revision.RejectionReason,
            CreatedAt = revision.CreatedAt,
            RowVersion = Convert.ToBase64String(revision.RowVersion),
            Lines = lines
        };
    }

    private static BudgetRevisionDto MapRevisionSummary(BudgetRevisionSummaryRow revision)
        => new()
        {
            Id = revision.Id,
            RevisionNumber = revision.RevisionNumber,
            RevisionType = revision.RevisionType,
            SourceScenarioId = revision.SourceScenarioId,
            SourceScenarioName = revision.SourceScenarioName,
            FiscalYearId = revision.FiscalYearId,
            FiscalYearName = revision.FiscalYearName,
            ResultScenarioId = revision.ResultScenarioId,
            ResultScenarioName = revision.ResultScenarioName,
            EffectiveDate = revision.EffectiveDate,
            BoardResolutionReference = revision.BoardResolutionReference,
            BoardResolutionDate = revision.BoardResolutionDate,
            Justification = revision.Justification,
            Status = revision.Status,
            IncreaseAmountBase = revision.IncreaseAmountBase,
            ReductionAmountBase = Math.Abs(revision.ReductionAmountBase),
            NetChangeAmountBase = revision.NetChangeAmountBase,
            SubmittedAt = revision.SubmittedAt,
            ApprovedAt = revision.ApprovedAt,
            AppliedAt = revision.AppliedAt,
            RejectionReason = revision.RejectionReason,
            CreatedAt = revision.CreatedAt,
            RowVersion = Convert.ToBase64String(revision.RowVersion),
            Lines = []
        };

    private async Task<BudgetScenario> RequireOfficialOrHistoricalScenarioAsync(Guid id)
        => await _context.BudgetScenarios
            .AsNoTracking()
            .Include(scenario => scenario.FiscalYear)
            .Include(scenario => scenario.BudgetReturns.Where(item => !item.IsDeleted))
                .ThenInclude(item => item.BudgetEntries.Where(entry => !entry.IsDeleted))
                    .ThenInclude(entry => entry.FinanceDimensionSet)!
                        .ThenInclude(set => set!.Items)
            .AsSplitQuery()
            .SingleOrDefaultAsync(scenario => scenario.TenantId == TenantId && scenario.Id == id && !scenario.IsDeleted)
            ?? throw new KeyNotFoundException("Source budget scenario not found.");

    private Dictionary<BudgetCell, decimal> BuildCurrentAmounts(BudgetScenario scenario)
        => scenario.BudgetReturns
            .Where(budgetReturn => !budgetReturn.IsDeleted)
            .SelectMany(budgetReturn => budgetReturn.BudgetEntries
                .Where(entry => !entry.IsDeleted)
                .Select(entry => new
                {
                    Key = new BudgetCell(
                        budgetReturn.SegmentValueId,
                        entry.AccountId,
                        entry.FiscalPeriodId,
                        entry.FinanceDimensionSetId),
                    entry.AmountBase
                }))
            .GroupBy(item => item.Key)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.AmountBase));

    private BudgetRevisionLine CreateRevisionLine(BudgetRevisionLineInputDto line, DateTime now)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            SegmentValueId = line.SegmentValueId,
            FinanceDimensionSetId = line.FinanceDimensionSetId,
            AccountId = line.AccountId,
            FiscalPeriodId = line.FiscalPeriodId,
            AdjustmentAmountBase = decimal.Round(line.AdjustmentAmountBase, 2),
            Notes = NormalizeOptionalText(line.Notes),
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName,
            CreatedById = CurrentUserId
        };

    private static CreateBudgetRevisionDto ToValidationDto(BudgetRevision revision)
        => new()
        {
            SourceScenarioId = revision.SourceScenarioId,
            RevisionType = revision.RevisionType,
            EffectiveDate = revision.EffectiveDate,
            BoardResolutionReference = revision.BoardResolutionReference,
            BoardResolutionDate = revision.BoardResolutionDate,
            Justification = revision.Justification,
            Lines = revision.Lines.Where(line => !line.IsDeleted).Select(line => new BudgetRevisionLineInputDto
            {
                SegmentValueId = line.SegmentValueId,
                FinanceDimensionSetId = line.FinanceDimensionSetId,
                AccountId = line.AccountId,
                FiscalPeriodId = line.FiscalPeriodId,
                AdjustmentAmountBase = line.AdjustmentAmountBase,
                Notes = line.Notes
            }).ToList()
        };

    private async Task<string> GenerateBudgetRevisionNumberAsync(DateTime now)
    {
        if (_documentNumberingService != null)
        {
            return await _documentNumberingService.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.BudgetRevision,
                TenantId,
                now,
                nameof(BudgetRevision));
        }

        // Unit tests may intentionally construct the service without the numbering
        // dependency. Production DI always supplies it; this fallback remains unique
        // and prevents a test-only concern from weakening production sequencing.
        return $"BR-{now:yyyy}-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
    }

    private async Task<IDbContextTransaction?> BeginRevisionTransactionAsync()
        => _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : null;

    private static string NormalizeRevisionType(string value)
    {
        if (string.Equals(value?.Trim(), VirementType, StringComparison.OrdinalIgnoreCase))
            return VirementType;
        if (string.Equals(value?.Trim(), SupplementaryType, StringComparison.OrdinalIgnoreCase))
            return SupplementaryType;
        throw new InvalidOperationException("Budget revision type must be Virement or Supplementary.");
    }

    private static string BuildSuccessorScenarioName(string sourceName, string revisionType, int versionNumber)
    {
        var suffix = $" - {revisionType} v{versionNumber}";
        var maxSourceLength = Math.Max(1, 100 - suffix.Length);
        var prefix = sourceName.Length <= maxSourceLength ? sourceName : sourceName[..maxSourceLength];
        return prefix + suffix;
    }

    private static string Truncate(string value, int maximumLength)
        => value.Length <= maximumLength ? value : value[..maximumLength];

    private sealed class BudgetRevisionSummaryRow
    {
        public Guid Id { get; init; }
        public string RevisionNumber { get; init; } = string.Empty;
        public string RevisionType { get; init; } = string.Empty;
        public Guid SourceScenarioId { get; init; }
        public string SourceScenarioName { get; init; } = string.Empty;
        public Guid FiscalYearId { get; init; }
        public string FiscalYearName { get; init; } = string.Empty;
        public Guid? ResultScenarioId { get; init; }
        public string? ResultScenarioName { get; init; }
        public DateTime EffectiveDate { get; init; }
        public string BoardResolutionReference { get; init; } = string.Empty;
        public DateTime BoardResolutionDate { get; init; }
        public string Justification { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public decimal IncreaseAmountBase { get; init; }
        public decimal ReductionAmountBase { get; init; }
        public decimal NetChangeAmountBase { get; init; }
        public DateTime? SubmittedAt { get; init; }
        public DateTime? ApprovedAt { get; init; }
        public DateTime? AppliedAt { get; init; }
        public string? RejectionReason { get; init; }
        public DateTime CreatedAt { get; init; }
        public byte[] RowVersion { get; init; } = [];
    }

    private readonly record struct BudgetCell(
        Guid? SegmentValueId,
        Guid AccountId,
        Guid FiscalPeriodId,
        Guid? FinanceDimensionSetId);
}
