using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Finance.Reporting;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Reporting;

public sealed class FinancialStatementLayoutExecutionService
    : IFinancialStatementLayoutExecutionService
{
    private static readonly string[] PostedStatuses = { "Posted", "Reversed" };

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantSettingsService _tenantSettings;
    private readonly IFinancialStatementLayoutService _layoutService;
    private readonly FinanceDimensionReportingFilterService? _dimensionReportingFilters;

    public FinancialStatementLayoutExecutionService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        ITenantSettingsService tenantSettings,
        IFinancialStatementLayoutService layoutService,
        FinanceDimensionReportingFilterService? dimensionReportingFilters = null)
    {
        _context = context;
        _currentUser = currentUser;
        _tenantSettings = tenantSettings;
        _layoutService = layoutService;
        _dimensionReportingFilters = dimensionReportingFilters;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    public async Task<FinancialStatementLayoutExecutionDto> PreviewVersionAsync(
        Guid versionId,
        FinancialStatementLayoutPreviewRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var version = await LoadVersionAsync(versionId, cancellationToken)
            ?? throw new KeyNotFoundException(
                "Financial statement layout version was not found.");

        var period = NormalizePeriod(
            version.FinancialStatementLayout.StatementType,
            request.PeriodStart,
            request.PeriodEnd);
        // Submitted versions are frozen for editing but have not acquired a publication
        // snapshot yet. Preview them against the live governed definition just like Drafts.
        var validation = version.Status is FinancialStatementLayoutVersionStatus.Draft
                or FinancialStatementLayoutVersionStatus.Submitted
            ? await _layoutService.ValidateVersionAsync(version.Id, cancellationToken)
            : ValidatePublishedSnapshot(version);
        ThrowForValidationErrors(validation);

        return await ExecuteVersionAsync(
            version,
            period,
            request.IncludeAccountDetails,
            request.IncludeHiddenRows,
            request.AccountIds,
            request.SegmentFilters,
            request.DimensionFilters,
            isPreview: true,
            validation,
            cancellationToken);
    }

    public async Task<FinancialStatementLayoutExecutionDto> ExecutePublishedAsync(
        FinancialStatementLayoutExecutionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.StatementType))
        {
            throw new InvalidOperationException(
                "A valid financial statement type is required.");
        }

        if (request.AccountingBookId == Guid.Empty)
        {
            throw new InvalidOperationException("An accounting book is required.");
        }

        var period = NormalizePeriod(
            request.StatementType,
            request.PeriodStart,
            request.PeriodEnd);
        var tenantId = TenantId;

        IQueryable<FinancialStatementLayout> layoutQuery =
            _context.FinancialStatementLayouts
                .AsNoTracking()
                .Where(layout =>
                    layout.TenantId == tenantId &&
                    layout.AccountingBookId == request.AccountingBookId &&
                    layout.StatementType == request.StatementType &&
                    layout.IsActive &&
                    layout.AccountingBook.TenantId == tenantId &&
                    !layout.AccountingBook.IsDeleted &&
                    layout.AccountingBook.IsActive &&
                    layout.AccountingBook.AllowsPosting &&
                    !layout.IsDeleted);

        FinancialStatementLayout? layout;
        if (request.LayoutId.HasValue && request.LayoutId.Value != Guid.Empty)
        {
            var requestedLayoutId = request.LayoutId.Value;
            layout = await layoutQuery.SingleOrDefaultAsync(
                candidate => candidate.Id == requestedLayoutId,
                cancellationToken);
            if (layout == null)
            {
                throw new KeyNotFoundException(
                    "The requested layout was not found for this tenant, statement type, and accounting book.");
            }
        }
        else
        {
            var defaults = await layoutQuery
                .Where(candidate => candidate.IsDefault)
                .OrderBy(candidate => candidate.Code)
                .Take(2)
                .ToListAsync(cancellationToken);
            if (defaults.Count != 1)
            {
                throw new InvalidOperationException(defaults.Count == 0
                    ? "No active default financial statement layout is configured for the selected statement type and accounting book."
                    : "More than one active default layout exists for the selected statement type and accounting book; choose a layout explicitly.");
            }
            layout = defaults[0];
        }

        var effectiveDate = period.End;
        var versionId = await _context.FinancialStatementLayoutVersions
            .AsNoTracking()
            .Where(version =>
                version.TenantId == tenantId &&
                version.FinancialStatementLayoutId == layout.Id &&
                !version.IsDeleted &&
                (version.Status == FinancialStatementLayoutVersionStatus.Published ||
                 version.Status == FinancialStatementLayoutVersionStatus.Retired) &&
                (!version.EffectiveFrom.HasValue ||
                 version.EffectiveFrom.Value.Date <= effectiveDate) &&
                (!version.EffectiveTo.HasValue ||
                 version.EffectiveTo.Value.Date >= effectiveDate))
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => (Guid?)version.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!versionId.HasValue)
        {
            throw new KeyNotFoundException(
                $"Layout '{layout.Code}' has no published version effective on {effectiveDate:yyyy-MM-dd}.");
        }

        var version = await LoadVersionAsync(versionId.Value, cancellationToken)
            ?? throw new KeyNotFoundException(
                "The effective financial statement layout version was not found.");
        if (version.PublishedAccountingBookId != request.AccountingBookId)
            throw new InvalidOperationException("The published snapshot does not belong to the requested accounting book.");
        var validation = ValidatePublishedSnapshot(version);
        ThrowForValidationErrors(validation);

        return await ExecuteVersionAsync(
            version,
            period,
            request.IncludeAccountDetails,
            request.IncludeHiddenRows,
            request.AccountIds,
            request.SegmentFilters,
            request.DimensionFilters,
            isPreview: false,
            validation,
            cancellationToken);
    }

    private async Task<FinancialStatementLayoutExecutionDto> ExecuteVersionAsync(
        FinancialStatementLayoutVersion version,
        ExecutionPeriod period,
        bool includeAccountDetails,
        bool includeHiddenRows,
        IEnumerable<Guid>? accountIds,
        IEnumerable<FinanceSegmentFilterDto>? segmentFilters,
        IEnumerable<FinanceDimensionFilterDto>? dimensionFilters,
        bool isPreview,
        FinancialStatementLayoutValidationResultDto validation,
        CancellationToken cancellationToken)
    {
        var layout = version.FinancialStatementLayout;
        var tenantId = TenantId;
        // A snapshot is created only by the independent approval/publication transaction.
        // Merely submitting a version must never make it executable as a published report.
        var useSnapshot = version.Status is FinancialStatementLayoutVersionStatus.Published
            or FinancialStatementLayoutVersionStatus.Retired;

        var selectedAccountIds = accountIds?
            .Where(accountId => accountId != Guid.Empty)
            .Distinct()
            .ToArray() ?? Array.Empty<Guid>();
        var resolvedSegmentFilters = await ResolveSegmentFiltersAsync(
            tenantId,
            segmentFilters,
            cancellationToken);
        var resolvedDimensionFilters = await ResolveDimensionFiltersAsync(
            dimensionFilters,
            cancellationToken);

        List<ExecutionAccount> accounts;
        if (useSnapshot)
        {
            accounts = version.PublicationAccounts.Where(item => !item.IsDeleted)
                .Select(item => new ExecutionAccount(item.AccountId, item.AccountNumber, item.AccountName, item.AccountType, null, item.AccountClassificationId))
                .DistinctBy(item => item.Id).OrderBy(item => item.AccountNumber).ToList();
            if (selectedAccountIds.Length > 0 && selectedAccountIds.Any(id => accounts.All(item => item.Id != id)))
                throw new InvalidOperationException("One or more report account filters are outside the immutable publication snapshot.");
            if (selectedAccountIds.Length > 0)
                accounts = accounts.Where(item => selectedAccountIds.Contains(item.Id)).ToList();
            foreach (var filter in resolvedSegmentFilters)
            {
                var matchingIds = await _context.AccountSegmentValues.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted
                        && item.SegmentStructureId == filter.SegmentStructureId
                        && item.SegmentValue == filter.SegmentValue)
                    .Select(item => item.AccountId).ToListAsync(cancellationToken);
                accounts = accounts.Where(item => matchingIds.Contains(item.Id)).ToList();
            }
        }
        else
        {
            var enabledAccountIds = _context.AccountAccountingBooks.AsNoTracking()
                .Where(mapping => mapping.TenantId == tenantId && mapping.AccountingBookId == layout.AccountingBookId
                    && mapping.IsEnabled && !mapping.IsDeleted).Select(mapping => mapping.AccountId);
            IQueryable<Account> accountQuery = _context.Accounts.AsNoTracking()
                .Where(account => account.TenantId == tenantId && !account.IsDeleted && enabledAccountIds.Contains(account.Id));
            if (selectedAccountIds.Length > 0)
            {
                var ownedCount = await accountQuery.CountAsync(account => selectedAccountIds.Contains(account.Id), cancellationToken);
                if (ownedCount != selectedAccountIds.Length)
                    throw new InvalidOperationException("One or more report account filters do not belong to the current tenant or are not enabled for the selected accounting book.");
                accountQuery = accountQuery.Where(account => selectedAccountIds.Contains(account.Id));
            }
            foreach (var filter in resolvedSegmentFilters)
                accountQuery = accountQuery.Where(account => account.SegmentValues.Any(segmentValue =>
                    segmentValue.TenantId == tenantId && segmentValue.SegmentStructureId == filter.SegmentStructureId
                    && segmentValue.SegmentValue == filter.SegmentValue && !segmentValue.IsDeleted));
            accounts = await accountQuery.OrderBy(account => account.AccountNumber)
                .Select(account => new ExecutionAccount(account.Id, account.AccountNumber, account.AccountName,
                    account.AccountType, account.ParentAccountId, null)).ToListAsync(cancellationToken);
            var resolvedAccountIds = accounts.Select(account => account.Id).ToArray();
            var classificationIds = await _context.AccountAccountingBooks.AsNoTracking()
                .Where(mapping => mapping.TenantId == tenantId && mapping.AccountingBookId == layout.AccountingBookId
                    && mapping.IsEnabled && !mapping.IsDeleted && resolvedAccountIds.Contains(mapping.AccountId))
                .ToDictionaryAsync(mapping => mapping.AccountId, mapping => mapping.AccountClassificationId, cancellationToken);
            accounts = accounts.Select(account => account with
            {
                AccountClassificationId = classificationIds.GetValueOrDefault(account.Id)
            }).ToList();
        }

        var accountsById = accounts.ToDictionary(account => account.Id);
        var childAccounts = accounts
            .Where(account => account.ParentAccountId.HasValue)
            .GroupBy(account => account.ParentAccountId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Select(account => account.Id).ToList());
        IReadOnlyDictionary<Guid, List<Guid>> childClassifications = new Dictionary<Guid, List<Guid>>();
        if (!useSnapshot)
        {
            var classificationEdges = await _context.AccountClassifications.AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.AccountingBookId == layout.AccountingBookId
                    && !item.IsDeleted && item.ParentClassificationId.HasValue)
                .Select(item => new { item.Id, ParentId = item.ParentClassificationId!.Value })
                .ToListAsync(cancellationToken);
            childClassifications = classificationEdges
                .GroupBy(item => item.ParentId)
                .ToDictionary(group => group.Key, group => group.Select(item => item.Id).ToList());
        }
        var normalBalances = await LoadNormalBalancesAsync(
            accounts,
            useSnapshot ? version.PublishedAccountingBookCode! : layout.AccountingBook.Code,
            layout.StatementType,
            period,
            resolvedDimensionFilters,
            cancellationToken);

        var orderedRows = FinancialStatementRowOrdering.Order(
            version.Rows.Where(row => !row.IsDeleted).ToList());
        var rowsByCode = orderedRows.ToDictionary(
            row => row.RowCode,
            StringComparer.OrdinalIgnoreCase);
        var rowCodesById = orderedRows.ToDictionary(row => row.Id, row => row.RowCode);
        var childRows = orderedRows
            .Where(row => row.ParentRowId.HasValue)
            .GroupBy(row => row.ParentRowId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var mappedAccountsByRow = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var row in orderedRows.Where(
                     candidate => candidate.RowType == FinancialStatementRowType.Account))
        {
            var mappedIds = useSnapshot
                ? version.PublicationAccounts.Where(item => !item.IsDeleted && item.FinancialStatementRowId == row.Id)
                    .Select(item => item.AccountId).Where(accountsById.ContainsKey).ToHashSet()
                : new HashSet<Guid>();
            if (!useSnapshot) foreach (var mapping in row.Mappings.Where(candidate => !candidate.IsDeleted))
            {
                foreach (var accountId in ResolveMapping(
                             mapping,
                             accounts,
                             accountsById,
                             childAccounts,
                             childClassifications))
                {
                    mappedIds.Add(accountId);
                }
            }
            mappedAccountsByRow[row.Id] = mappedIds;
        }

        var values = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var evaluationState = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var orderedCodes = orderedRows.Select(row => row.RowCode).ToList();

        decimal EvaluateRow(string rowCode)
        {
            if (values.TryGetValue(rowCode, out var existing))
            {
                return existing;
            }

            if (!rowsByCode.TryGetValue(rowCode, out var row))
            {
                throw new InvalidOperationException(
                    $"Formula references unknown row code '{rowCode}'.");
            }

            if (evaluationState.TryGetValue(rowCode, out var state) && state == 1)
            {
                throw new InvalidOperationException(
                    $"Formula evaluation contains a cycle involving row '{rowCode}'.");
            }

            evaluationState[rowCode] = 1;
            decimal value = row.RowType switch
            {
                FinancialStatementRowType.Account => mappedAccountsByRow
                    .GetValueOrDefault(row.Id, new HashSet<Guid>())
                    .Sum(accountId => normalBalances.GetValueOrDefault(accountId)),
                FinancialStatementRowType.Formula => EvaluateFormula(row),
                FinancialStatementRowType.Total when
                    !string.IsNullOrWhiteSpace(row.Formula) => EvaluateFormula(row),
                FinancialStatementRowType.Total => SumTotalChildren(row),
                _ => 0m
            };

            value *= row.SignMultiplier;
            values[rowCode] = value;
            evaluationState[rowCode] = 2;
            return value;
        }

        decimal EvaluateFormula(FinancialStatementRow row)
        {
            var evaluation = FinancialStatementFormulaParser.Evaluate(
                row.Formula,
                orderedCodes,
                EvaluateRow);
            if (!evaluation.IsValid)
            {
                throw new InvalidOperationException(
                    $"Formula for row '{row.RowCode}' could not be evaluated: {evaluation.Error}");
            }
            return evaluation.Value;
        }

        decimal SumTotalChildren(FinancialStatementRow totalRow)
        {
            if (!childRows.TryGetValue(totalRow.Id, out var children))
            {
                return 0m;
            }

            decimal total = 0m;
            foreach (var child in children)
            {
                total += child.RowType is FinancialStatementRowType.Header
                    or FinancialStatementRowType.Spacer
                    ? SumContainerDescendants(child)
                    : EvaluateRow(child.RowCode);
            }
            return total;
        }

        decimal SumContainerDescendants(FinancialStatementRow container)
        {
            if (!childRows.TryGetValue(container.Id, out var children))
            {
                return 0m;
            }

            decimal total = 0m;
            foreach (var child in children)
            {
                total += child.RowType is FinancialStatementRowType.Header
                    or FinancialStatementRowType.Spacer
                    ? SumContainerDescendants(child)
                    : EvaluateRow(child.RowCode);
            }
            return total;
        }

        foreach (var row in orderedRows)
        {
            EvaluateRow(row.RowCode);
        }

        var executionRows = new List<FinancialStatementLayoutExecutionRowDto>();
        for (var index = 0; index < orderedRows.Count; index++)
        {
            var row = orderedRows[index];
            var amount = values.GetValueOrDefault(row.RowCode);
            var isDisplayed = row.IsVisible && (!row.SuppressIfZero || amount != 0m);
            if (!includeHiddenRows && !isDisplayed)
            {
                continue;
            }

            var accountDetails = Array.Empty<FinancialStatementLayoutAccountDetailDto>();
            if ((includeAccountDetails || row.ShowAccountDetails)
                && mappedAccountsByRow.TryGetValue(row.Id, out var mappedIds))
            {
                accountDetails = mappedIds
                    .Where(accountsById.ContainsKey)
                    .Select(accountId =>
                    {
                        var account = accountsById[accountId];
                        var normalBalance = normalBalances.GetValueOrDefault(accountId);
                        return new FinancialStatementLayoutAccountDetailDto
                        {
                            AccountId = account.Id,
                            AccountNumber = account.AccountNumber,
                            AccountName = account.AccountName,
                            AccountType = account.AccountType,
                            NormalBalance = normalBalance,
                            PresentedAmount = normalBalance * row.SignMultiplier
                        };
                    })
                    .OrderBy(account => account.AccountNumber)
                    .ToArray();
            }

            executionRows.Add(new FinancialStatementLayoutExecutionRowDto
            {
                RowId = row.Id,
                RowCode = row.RowCode,
                ParentRowCode = row.ParentRowId.HasValue
                    && rowCodesById.TryGetValue(row.ParentRowId.Value, out var parentCode)
                        ? parentCode
                        : null,
                Label = row.Label,
                RowType = row.RowType,
                DisplayOrder = row.DisplayOrder,
                Sequence = index + 1,
                Formula = row.Formula,
                Amount = amount,
                IsDisplayed = isDisplayed,
                SuppressIfZero = row.SuppressIfZero,
                ShowAccountDetails = row.ShowAccountDetails,
                IsBold = row.IsBold,
                IsItalic = row.IsItalic,
                IsUnderlined = row.IsUnderlined,
                IndentLevel = row.IndentLevel,
                Accounts = accountDetails
            });
        }

        var mappedAccountIds = mappedAccountsByRow.Values
            .SelectMany(accountIds => accountIds)
            .ToHashSet();
        var eligibleAccounts = accounts
            .Where(account => IsCompatible(layout.StatementType, account.AccountType))
            .ToList();
        var unmappedAccounts = eligibleAccounts
            .Where(account => !mappedAccountIds.Contains(account.Id))
            .Select(account => new FinancialStatementLayoutUnmappedAccountDto
            {
                AccountId = account.Id,
                AccountNumber = account.AccountNumber,
                AccountName = account.AccountName,
                AccountType = account.AccountType,
                NormalBalance = normalBalances.GetValueOrDefault(account.Id)
            })
            .OrderBy(account => account.AccountNumber)
            .ToList();
        var eligibleAccountIds = eligibleAccounts.Select(account => account.Id).ToHashSet();
        var eligibleMappedAccountIds = mappedAccountIds
            .Where(eligibleAccountIds.Contains)
            .ToHashSet();

        // Tenant settings commonly share this scoped DbContext, so read them
        // sequentially to avoid overlapping EF operations.
        var companyName = await _tenantSettings.GetCompanyNameAsync();
        var currencyCode = await _tenantSettings.GetBaseCurrencyAsync();

        return new FinancialStatementLayoutExecutionDto
        {
            LayoutId = layout.Id,
            LayoutCode = layout.Code,
            LayoutName = layout.Name,
            VersionId = version.Id,
            VersionNumber = version.VersionNumber,
            VersionStatus = version.Status,
            StatementType = layout.StatementType,
            AccountingBookId = useSnapshot ? version.PublishedAccountingBookId!.Value : layout.AccountingBookId,
            AccountingBookCode = useSnapshot ? version.PublishedAccountingBookCode! : layout.AccountingBook.Code,
            AccountingBookName = useSnapshot ? version.PublishedAccountingBookName! : layout.AccountingBook.Name,
            CompanyName = companyName,
            CurrencyCode = currencyCode,
            PeriodStart = period.Start,
            PeriodEnd = period.End,
            GeneratedAt = DateTime.UtcNow,
            IsPreview = isPreview,
            Rows = executionRows,
            Warnings = validation.Issues
                .Where(issue =>
                    issue.Severity != FinancialStatementLayoutValidationSeverity.Error)
                .ToList(),
            Reconciliation = new FinancialStatementLayoutReconciliationDto
            {
                EligibleAccountCount = eligibleAccounts.Count,
                MappedAccountCount = eligibleMappedAccountIds.Count,
                MappedNonZeroAccountCount = eligibleMappedAccountIds.Count(
                    accountId => normalBalances.GetValueOrDefault(accountId) != 0m),
                UnmappedAccountCount = unmappedAccounts.Count,
                UnmappedNonZeroAccountCount = unmappedAccounts.Count(
                    account => account.NormalBalance != 0m),
                MappedNormalBalance = eligibleMappedAccountIds.Sum(
                    accountId => normalBalances.GetValueOrDefault(accountId)),
                UnmappedNormalBalance = unmappedAccounts.Sum(
                    account => account.NormalBalance),
                AccountCoveragePercent = eligibleAccounts.Count == 0
                    ? 100m
                    : decimal.Round(
                        100m * eligibleMappedAccountIds.Count / eligibleAccounts.Count,
                        2),
                UnmappedAccounts = unmappedAccounts
            }
        };
    }

    private async Task<List<NormalizedSegmentFilter>> ResolveSegmentFiltersAsync(
        Guid tenantId,
        IEnumerable<FinanceSegmentFilterDto>? segmentFilters,
        CancellationToken cancellationToken)
    {
        var requestedFilters = segmentFilters?
            .Where(filter =>
                filter != null &&
                !string.IsNullOrWhiteSpace(filter.SegmentValue))
            .ToList() ?? new List<FinanceSegmentFilterDto>();

        var resolved = new List<NormalizedSegmentFilter>();
        foreach (var filter in requestedFilters)
        {
            var value = filter.SegmentValue.Trim();
            IQueryable<AccountSegmentStructure> structureQuery =
                _context.AccountSegmentStructures
                    .AsNoTracking()
                    .Where(structure =>
                        structure.TenantId == tenantId &&
                        structure.IsActive &&
                        structure.IsReportingDimension);

            if (filter.SegmentStructureId.HasValue &&
                filter.SegmentStructureId.Value != Guid.Empty)
            {
                var segmentId = filter.SegmentStructureId.Value;
                structureQuery = structureQuery.Where(
                    structure => structure.Id == segmentId);
            }
            else if (!string.IsNullOrWhiteSpace(filter.SegmentCode))
            {
                var segmentCode = filter.SegmentCode.Trim();
                structureQuery = structureQuery.Where(
                    structure => structure.SegmentCode == segmentCode);
            }
            else if (filter.SegmentPosition.HasValue)
            {
                var position = filter.SegmentPosition.Value;
                structureQuery = structureQuery.Where(
                    structure => structure.SegmentPosition == position);
            }
            else
            {
                throw new InvalidOperationException(
                    "Segment report filters require a tenant-owned segment id, code, or position.");
            }

            var structure = await structureQuery.SingleOrDefaultAsync(
                cancellationToken);
            if (structure == null)
            {
                throw new InvalidOperationException(
                    "One or more report segment filters do not belong to the current tenant or are not reporting dimensions.");
            }

            if (structure.LookupTableRequired)
            {
                var valueExists = await _context.SegmentLookupValues
                    .AsNoTracking()
                    .AnyAsync(valueOption =>
                        valueOption.TenantId == tenantId &&
                        valueOption.SegmentStructureId == structure.Id &&
                        valueOption.SegmentValue == value &&
                        valueOption.IsActive,
                        cancellationToken);

                if (!valueExists)
                {
                    throw new InvalidOperationException(
                        $"Segment value '{value}' is not valid for reporting dimension '{structure.SegmentName}'.");
                }
            }
            else
            {
                if (value.Length != structure.SegmentLength)
                {
                    throw new InvalidOperationException(
                        $"Segment value '{value}' must be exactly {structure.SegmentLength} characters for reporting dimension '{structure.SegmentName}'.");
                }

                if (structure.DataType.Equals(
                        "Numeric",
                        StringComparison.OrdinalIgnoreCase) &&
                    value.Any(character => !char.IsDigit(character)))
                {
                    throw new InvalidOperationException(
                        $"Segment value '{value}' must contain only numbers for reporting dimension '{structure.SegmentName}'.");
                }

                if (structure.DataType.Equals(
                        "Alphanumeric",
                        StringComparison.OrdinalIgnoreCase) &&
                    value.Any(character => !char.IsLetterOrDigit(character)))
                {
                    throw new InvalidOperationException(
                        $"Segment value '{value}' must contain only letters and numbers for reporting dimension '{structure.SegmentName}'.");
                }

                var now = DateTime.UtcNow;
                var valueExistsOnActiveAccount =
                    await _context.AccountSegmentValues
                        .AsNoTracking()
                        .AnyAsync(segmentValue =>
                            segmentValue.TenantId == tenantId &&
                            segmentValue.SegmentStructureId == structure.Id &&
                            segmentValue.SegmentValue == value &&
                            !segmentValue.IsDeleted &&
                            segmentValue.EffectiveDate <= now &&
                            (segmentValue.EndDate == null ||
                             segmentValue.EndDate > now) &&
                            segmentValue.Account.TenantId == tenantId &&
                            !segmentValue.Account.IsDeleted &&
                            segmentValue.Account.Status == AccountStatus.Active &&
                            (segmentValue.Account.EffectiveDate == null ||
                             segmentValue.Account.EffectiveDate <= now) &&
                            (segmentValue.Account.ExpirationDate == null ||
                             segmentValue.Account.ExpirationDate > now),
                            cancellationToken);

                if (!valueExistsOnActiveAccount)
                {
                    throw new InvalidOperationException(
                        $"Segment value '{value}' is not used by an active GL account for reporting dimension '{structure.SegmentName}'.");
                }
            }

            resolved.Add(new NormalizedSegmentFilter(structure.Id, value));
        }

        return resolved;
    }

    private async Task<Dictionary<Guid, decimal>> LoadNormalBalancesAsync(
        IReadOnlyCollection<ExecutionAccount> accounts,
        string accountingBookCode,
        FinancialStatementType statementType,
        ExecutionPeriod period,
        IReadOnlyCollection<ResolvedFinanceDimensionFilter> dimensionFilters,
        CancellationToken cancellationToken)
    {
        if (accounts.Count == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        var accountIds = accounts.Select(account => account.Id).ToArray();
        var normalizedBookCode = NormalizeBookCode(accountingBookCode);
        var endExclusive = period.End.AddDays(1);
        var query = _context.AccountTransactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.TenantId == TenantId &&
                !transaction.IsDeleted &&
                accountIds.Contains(transaction.AccountId) &&
                transaction.BookClassification == normalizedBookCode &&
                transaction.TransactionDate < endExclusive &&
                transaction.JournalEntry.TenantId == TenantId &&
                !transaction.JournalEntry.IsDeleted &&
                PostedStatuses.Contains(transaction.PostingStatus) &&
                PostedStatuses.Contains(transaction.JournalEntry.PostingStatus));

        if (statementType == FinancialStatementType.IncomeStatement)
        {
            var start = period.Start!.Value;
            query = query.Where(transaction => transaction.TransactionDate >= start);
        }

        if (dimensionFilters.Count > 0)
        {
            query = _dimensionReportingFilters!.Apply(query, dimensionFilters);
        }

        var rawBalances = await query
            .GroupBy(transaction => transaction.AccountId)
            .Select(group => new
            {
                AccountId = group.Key,
                Amount = group.Sum(transaction =>
                    transaction.DebitAmount - transaction.CreditAmount)
            })
            .ToDictionaryAsync(
                balance => balance.AccountId,
                balance => balance.Amount,
                cancellationToken);

        return accounts.ToDictionary(
            account => account.Id,
            account => ToNormalBalance(
                account.AccountType,
                rawBalances.GetValueOrDefault(account.Id)));
    }

    private async Task<IReadOnlyCollection<ResolvedFinanceDimensionFilter>> ResolveDimensionFiltersAsync(
        IEnumerable<FinanceDimensionFilterDto>? filters,
        CancellationToken cancellationToken)
    {
        var requested = filters?.ToList() ?? [];
        if (requested.Count == 0)
        {
            return [];
        }

        if (_dimensionReportingFilters == null)
        {
            throw new InvalidOperationException(
                "Transaction-dimension filtering is not available for financial statement layouts.");
        }

        return await _dimensionReportingFilters.ResolveAsync(requested, cancellationToken);
    }

    private async Task<FinancialStatementLayoutVersion?> LoadVersionAsync(
        Guid versionId,
        CancellationToken cancellationToken)
        => await _context.FinancialStatementLayoutVersions
            .AsNoTracking()
            .AsSplitQuery()
            .Include(version => version.FinancialStatementLayout)
                .ThenInclude(layout => layout.AccountingBook)
            .Include(version => version.Rows.Where(row => !row.IsDeleted))
                .ThenInclude(row => row.Mappings.Where(mapping => !mapping.IsDeleted))
            .Include(version => version.PublicationAccounts.Where(item => !item.IsDeleted))
            .SingleOrDefaultAsync(version =>
                version.Id == versionId &&
                version.TenantId == TenantId &&
                !version.IsDeleted &&
                !version.FinancialStatementLayout.IsDeleted,
                cancellationToken);

    private static FinancialStatementLayoutValidationResultDto ValidatePublishedSnapshot(
        FinancialStatementLayoutVersion version)
    {
        var result = new FinancialStatementLayoutValidationResultDto();
        if (version.PublishedAccountingBookId is null
            || string.IsNullOrWhiteSpace(version.PublishedAccountingBookCode)
            || string.IsNullOrWhiteSpace(version.PublishedAccountingBookName)
            || string.IsNullOrWhiteSpace(version.HierarchyFingerprint)
            || string.IsNullOrWhiteSpace(version.ResolutionFingerprint)
            || version.PublicationSnapshotSchemaVersion != FinancialStatementPublicationFingerprint.SnapshotSchemaVersion)
        {
            result.Issues.Add(new FinancialStatementLayoutValidationIssueDto
            {
                Severity = FinancialStatementLayoutValidationSeverity.Error,
                Code = "PUBLICATION_SNAPSHOT_MISSING",
                Message = "The published layout version has no complete immutable publication snapshot."
            });
            return result;
        }
        var actual = FinancialStatementPublicationFingerprint.Resolution(
            version.TenantId, version.Id, version.PublishedAccountingBookId.Value,
            version.PublishedAccountingBookCode, version.PublishedAccountingBookName,
            version.HierarchyFingerprint,
            version.PublicationAccounts.Where(item => !item.IsDeleted));
        if (!actual.Equals(version.ResolutionFingerprint, StringComparison.Ordinal))
        {
            result.Issues.Add(new FinancialStatementLayoutValidationIssueDto
            {
                Severity = FinancialStatementLayoutValidationSeverity.Error,
                Code = "PUBLICATION_SNAPSHOT_TAMPERED",
                Message = "The immutable publication snapshot failed its deterministic resolution fingerprint check."
            });
        }
        return result;
    }

    private static IReadOnlyCollection<Guid> ResolveMapping(
        FinancialStatementRowMapping mapping,
        IReadOnlyCollection<ExecutionAccount> accounts,
        IReadOnlyDictionary<Guid, ExecutionAccount> accountsById,
        IReadOnlyDictionary<Guid, List<Guid>> childAccounts,
        IReadOnlyDictionary<Guid, List<Guid>> childClassifications)
        => mapping.MappingType switch
        {
            FinancialStatementRowMappingType.Account when
                mapping.AccountId.HasValue &&
                accountsById.ContainsKey(mapping.AccountId.Value) =>
                    new[] { mapping.AccountId.Value },
            FinancialStatementRowMappingType.AccountHierarchy when
                mapping.AccountId.HasValue &&
                accountsById.ContainsKey(mapping.AccountId.Value) =>
                    ResolveHierarchy(mapping.AccountId.Value, childAccounts),
            FinancialStatementRowMappingType.AccountRange when
                !string.IsNullOrWhiteSpace(mapping.FromAccountNumber) &&
                !string.IsNullOrWhiteSpace(mapping.ToAccountNumber) =>
                    accounts
                        .Where(account =>
                            StringComparer.OrdinalIgnoreCase.Compare(
                                account.AccountNumber,
                                mapping.FromAccountNumber) >= 0 &&
                            StringComparer.OrdinalIgnoreCase.Compare(
                                account.AccountNumber,
                                mapping.ToAccountNumber) <= 0)
                        .Select(account => account.Id)
                        .ToList(),
            FinancialStatementRowMappingType.Classification when mapping.AccountClassificationId.HasValue =>
                accounts.Where(account => account.AccountClassificationId.HasValue
                        && (mapping.IncludeClassificationDescendants
                            ? ResolveHierarchy(mapping.AccountClassificationId.Value, childClassifications)
                            : new HashSet<Guid> { mapping.AccountClassificationId.Value })
                        .Contains(account.AccountClassificationId.Value))
                    .Select(account => account.Id).ToList(),
            _ => Array.Empty<Guid>()
        };

    private static IReadOnlyCollection<Guid> ResolveHierarchy(
        Guid rootAccountId,
        IReadOnlyDictionary<Guid, List<Guid>> childAccounts)
    {
        var resolved = new HashSet<Guid>();
        var pending = new Queue<Guid>();
        pending.Enqueue(rootAccountId);

        while (pending.Count > 0)
        {
            var accountId = pending.Dequeue();
            if (!resolved.Add(accountId))
            {
                continue;
            }

            if (childAccounts.TryGetValue(accountId, out var children))
            {
                foreach (var child in children)
                {
                    pending.Enqueue(child);
                }
            }
        }

        return resolved;
    }

    private static ExecutionPeriod NormalizePeriod(
        FinancialStatementType statementType,
        DateTime? periodStart,
        DateTime periodEnd)
    {
        if (periodEnd == default)
        {
            throw new InvalidOperationException("A report end date is required.");
        }

        var end = periodEnd.Date;
        if (statementType == FinancialStatementType.BalanceSheet)
        {
            return new ExecutionPeriod(null, end);
        }

        if (statementType != FinancialStatementType.IncomeStatement)
        {
            throw new InvalidOperationException(
                "A valid financial statement type is required.");
        }

        if (!periodStart.HasValue)
        {
            throw new InvalidOperationException(
                "Income Statement execution requires a period start date.");
        }

        var start = periodStart.Value.Date;
        if (start > end)
        {
            throw new InvalidOperationException(
                "Period start cannot be later than period end.");
        }

        return new ExecutionPeriod(start, end);
    }

    private static void ThrowForValidationErrors(
        FinancialStatementLayoutValidationResultDto validation)
    {
        if (validation.Issues.Any(issue =>
                issue.Severity == FinancialStatementLayoutValidationSeverity.Error))
        {
            throw new FinancialStatementLayoutValidationException(validation);
        }
    }

    private static bool IsCompatible(
        FinancialStatementType statementType,
        AccountType accountType)
        => statementType switch
        {
            FinancialStatementType.BalanceSheet =>
                accountType is AccountType.Asset or AccountType.Liability or AccountType.Equity,
            FinancialStatementType.IncomeStatement =>
                accountType is AccountType.Revenue or AccountType.Expense,
            _ => false
        };

    private static decimal ToNormalBalance(
        AccountType accountType,
        decimal debitMinusCredit)
        => accountType is AccountType.Asset or AccountType.Expense
            ? debitMinusCredit
            : -debitMinusCredit;

    private static string NormalizeBookCode(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        return normalized switch
        {
            "BASE" or "LOCAL" => "LOCAL_STATUTORY",
            _ => normalized
        };
    }

    private sealed record ExecutionAccount(
        Guid Id,
        string AccountNumber,
        string AccountName,
        AccountType AccountType,
        Guid? ParentAccountId,
        Guid? AccountClassificationId);

    private sealed record NormalizedSegmentFilter(
        Guid SegmentStructureId,
        string SegmentValue);

    private sealed record ExecutionPeriod(DateTime? Start, DateTime End);
}
