using System.Data;
using System.Text.RegularExpressions;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Finance.Reporting;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Reporting;

public sealed class FinancialStatementLayoutService : IFinancialStatementLayoutService
{
    private static readonly Regex RowCodePattern = new(
        "^[A-Za-z0-9_.]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceAuditService _financeAuditService;
    private readonly ILogger<FinancialStatementLayoutService> _logger;

    public FinancialStatementLayoutService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IFinanceAuditService financeAuditService,
        ILogger<FinancialStatementLayoutService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _financeAuditService = financeAuditService;
        _logger = logger;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
    private string UserName => _currentUser.UserName ?? "system";

    public async Task<IReadOnlyList<FinancialStatementLayoutSummaryDto>> GetLayoutsAsync(
        FinancialStatementType? statementType = null,
        Guid? accountingBookId = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.FinancialStatementLayouts
            .AsNoTracking()
            .Include(layout => layout.AccountingBook)
            .Include(layout => layout.Versions)
            .Where(layout => layout.TenantId == TenantId && !layout.IsDeleted);

        if (statementType.HasValue)
        {
            query = query.Where(layout => layout.StatementType == statementType.Value);
        }

        if (accountingBookId.HasValue && accountingBookId.Value != Guid.Empty)
        {
            query = query.Where(layout => layout.AccountingBookId == accountingBookId.Value);
        }

        if (!includeInactive)
        {
            query = query.Where(layout => layout.IsActive);
        }

        var layouts = await query
            .OrderBy(layout => layout.StatementType)
            .ThenBy(layout => layout.AccountingBook.SortOrder)
            .ThenBy(layout => layout.Code)
            .ToListAsync(cancellationToken);

        return layouts.Select(MapSummary).ToList();
    }

    public async Task<FinancialStatementLayoutDto?> GetLayoutAsync(
        Guid layoutId,
        CancellationToken cancellationToken = default)
    {
        var layout = await LoadLayoutAsync(layoutId, asNoTracking: true, cancellationToken);
        return layout == null ? null : MapLayout(layout);
    }

    public async Task<IReadOnlyList<FinancialStatementLayoutAuditEventDto>> GetAuditTrailAsync(
        Guid layoutId,
        CancellationToken cancellationToken = default)
    {
        var layout = await LoadLayoutAsync(layoutId, asNoTracking: true, cancellationToken);
        if (layout == null)
        {
            throw new KeyNotFoundException("Financial statement layout was not found.");
        }

        var auditEvents = await _financeAuditService.GetAuditTrailAsync(
            TenantId,
            "Finance.FinancialStatementLayout",
            layoutId.ToString(),
            limit: 100,
            cancellationToken);

        return auditEvents
            .Select(auditEvent => new FinancialStatementLayoutAuditEventDto
            {
                Id = auditEvent.Id,
                EventType = auditEvent.Action,
                Username = auditEvent.Username,
                Timestamp = auditEvent.Timestamp,
                DetailsJson = auditEvent.NewValues
            })
            .ToList();
    }

    public async Task<FinancialStatementLayoutDto> CreateLayoutAsync(
        CreateFinancialStatementLayoutDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateStatementType(request.StatementType);
        ValidateEffectiveDates(request.EffectiveFrom, request.EffectiveTo);

        var tenantId = TenantId;
        var code = NormalizeCode(request.Code);
        var name = RequireText(request.Name, "Layout name");
        var book = await GetOwnedBookAsync(request.AccountingBookId, cancellationToken);

        var duplicate = await _context.FinancialStatementLayouts
            .AnyAsync(layout =>
                layout.TenantId == tenantId &&
                !layout.IsDeleted &&
                layout.Code == code,
                cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException(
                $"Financial statement layout code '{code}' already exists.");
        }

        var now = DateTime.UtcNow;
        var layout = new FinancialStatementLayout
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = name,
            Description = NormalizeOptionalText(request.Description),
            StatementType = request.StatementType,
            AccountingBookId = book.Id,
            IsDefault = request.IsDefault,
            IsActive = true,
            Revision = 1,
            CreatedAt = now,
            CreatedBy = UserName
        };

        var version = new FinancialStatementLayoutVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FinancialStatementLayoutId = layout.Id,
            VersionNumber = 1,
            Status = FinancialStatementLayoutVersionStatus.Draft,
            EffectiveFrom = request.EffectiveFrom?.Date,
            EffectiveTo = request.EffectiveTo?.Date,
            Notes = NormalizeOptionalText(request.Notes),
            Revision = 1,
            CreatedAt = now,
            CreatedBy = UserName
        };

        if (request.IsDefault)
        {
            await ClearOtherDefaultsAsync(
                book.Id,
                request.StatementType,
                layout.Id,
                cancellationToken);
        }

        _context.FinancialStatementLayouts.Add(layout);
        _context.FinancialStatementLayoutVersions.Add(version);
        await _context.SaveChangesAsync(cancellationToken);

        await RecordAuditAsync(
            FinanceAuditEvents.FinancialStatementLayoutCreated,
            layout.Id,
            new
            {
                layout.Code,
                layout.Name,
                layout.StatementType,
                layout.AccountingBookId,
                initialVersionId = version.Id
            },
            cancellationToken);

        _logger.LogInformation(
            "Created financial statement layout {LayoutCode} version 1 for tenant {TenantId}",
            layout.Code,
            tenantId);

        return MapLayout((await LoadLayoutAsync(layout.Id, true, cancellationToken))!);
    }

    public async Task<FinancialStatementLayoutDto> UpdateLayoutAsync(
        Guid layoutId,
        UpdateFinancialStatementLayoutDto request,
        CancellationToken cancellationToken = default)
    {
        var layout = await _context.FinancialStatementLayouts
            .Include(item => item.AccountingBook)
            .Include(item => item.Versions)
            .SingleOrDefaultAsync(item =>
                item.Id == layoutId &&
                item.TenantId == TenantId &&
                !item.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("Financial statement layout was not found.");

        EnsureRevision(layout.Revision, request.ExpectedRevision, "layout");
        EnsureEditableLayout(layout);
        var before = new
        {
            layout.Name,
            layout.Description,
            layout.IsDefault,
            layout.IsActive,
            layout.Revision
        };

        if (request.IsDefault)
        {
            await ClearOtherDefaultsAsync(
                layout.AccountingBookId,
                layout.StatementType,
                layout.Id,
                cancellationToken);
        }

        layout.Name = RequireText(request.Name, "Layout name");
        layout.Description = NormalizeOptionalText(request.Description);
        layout.IsDefault = request.IsDefault;
        layout.IsActive = request.IsActive;
        layout.Revision++;
        layout.UpdatedAt = DateTime.UtcNow;
        layout.UpdatedBy = UserName;

        await _context.SaveChangesAsync(cancellationToken);

        await RecordAuditAsync(
            FinanceAuditEvents.FinancialStatementLayoutUpdated,
            layout.Id,
            new
            {
                before,
                after = new
                {
                    layout.Name,
                    layout.Description,
                    layout.IsDefault,
                    layout.IsActive,
                    layout.Revision
                }
            },
            cancellationToken);

        return MapLayout((await LoadLayoutAsync(layout.Id, true, cancellationToken))!);
    }

    public async Task<FinancialStatementLayoutVersionDto> CreateDraftVersionAsync(
        Guid layoutId,
        CreateFinancialStatementLayoutVersionDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateEffectiveDates(request.EffectiveFrom, request.EffectiveTo);
        var tenantId = TenantId;

        var layout = await _context.FinancialStatementLayouts
            .Include(item => item.Versions)
                .ThenInclude(version => version.Rows)
                    .ThenInclude(row => row.Mappings)
            .SingleOrDefaultAsync(item =>
                item.Id == layoutId &&
                item.TenantId == tenantId &&
                !item.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("Financial statement layout was not found.");

        EnsureEditableLayout(layout);
        if (!layout.IsActive)
        {
            throw new InvalidOperationException(
                "A draft version cannot be created for an inactive layout.");
        }

        if (layout.Versions.Any(version =>
                !version.IsDeleted &&
                version.Status == FinancialStatementLayoutVersionStatus.Draft))
        {
            throw new InvalidOperationException(
                "This layout already has a draft version. Publish or discard it before creating another draft.");
        }

        FinancialStatementLayoutVersion? source = null;
        if (request.SourceVersionId.HasValue)
        {
            source = layout.Versions.SingleOrDefault(version =>
                version.Id == request.SourceVersionId.Value &&
                !version.IsDeleted);
            if (source == null)
            {
                throw new InvalidOperationException(
                    "The source version does not belong to this layout.");
            }
        }
        else
        {
            source = layout.Versions
                .Where(version => !version.IsDeleted)
                .OrderByDescending(version => version.VersionNumber)
                .FirstOrDefault();
        }

        var now = DateTime.UtcNow;
        var draft = new FinancialStatementLayoutVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FinancialStatementLayoutId = layout.Id,
            VersionNumber = layout.Versions
                .Where(version => !version.IsDeleted)
                .Select(version => version.VersionNumber)
                .DefaultIfEmpty(0)
                .Max() + 1,
            Status = FinancialStatementLayoutVersionStatus.Draft,
            EffectiveFrom = request.EffectiveFrom?.Date ?? source?.EffectiveFrom,
            EffectiveTo = request.EffectiveTo?.Date,
            Notes = NormalizeOptionalText(request.Notes),
            Revision = 1,
            CreatedAt = now,
            CreatedBy = UserName
        };

        _context.FinancialStatementLayoutVersions.Add(draft);
        if (source != null)
        {
            CloneRows(source, draft, now);
        }

        await _context.SaveChangesAsync(cancellationToken);

        await RecordAuditAsync(
            FinanceAuditEvents.FinancialStatementLayoutVersionCreated,
            layout.Id,
            new
            {
                versionId = draft.Id,
                draft.VersionNumber,
                sourceVersionId = source?.Id
            },
            cancellationToken);

        return MapVersion((await LoadVersionAsync(draft.Id, true, cancellationToken))!);
    }

    public async Task<FinancialStatementLayoutVersionDto> ReplaceDraftRowsAsync(
        Guid versionId,
        ReplaceFinancialStatementRowsDto request,
        CancellationToken cancellationToken = default)
    {
        var versionForValidation = await LoadVersionAsync(
            versionId,
            asNoTracking: true,
            cancellationToken)
            ?? throw new KeyNotFoundException("Financial statement layout version was not found.");
        EnsureDraft(versionForValidation);
        EnsureEditableLayout(versionForValidation.FinancialStatementLayout);
        EnsureRevision(
            versionForValidation.Revision,
            request.ExpectedVersionRevision,
            "layout version");

        var normalizedRows = NormalizeRows(request.Rows);
        var validation = await ValidateDefinitionAsync(
            versionForValidation.FinancialStatementLayout.StatementType,
            versionForValidation.FinancialStatementLayout.AccountingBookId,
            normalizedRows,
            cancellationToken);
        ThrowForValidationErrors(validation);

        FinancialStatementLayoutVersionDto? updatedVersion = null;
        var executionStrategy = _context.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            // A retry must start from persisted state, not entity states left
            // behind by a failed execution attempt.
            _context.ChangeTracker.Clear();
            var version = await LoadVersionAsync(
                versionId,
                asNoTracking: false,
                cancellationToken)
                ?? throw new KeyNotFoundException(
                    "Financial statement layout version was not found.");
            EnsureDraft(version);
            EnsureEditableLayout(version.FinancialStatementLayout);
            EnsureRevision(
                version.Revision,
                request.ExpectedVersionRevision,
                "layout version");

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    cancellationToken);

            var existingMappings = version.Rows
                .SelectMany(row => row.Mappings)
                .ToList();
            if (existingMappings.Count > 0)
            {
                _context.FinancialStatementRowMappings.RemoveRange(
                    existingMappings);
            }

            if (version.Rows.Count > 0)
            {
                _context.FinancialStatementRows.RemoveRange(version.Rows);
            }

            await _context.SaveChangesAsync(cancellationToken);

            var now = DateTime.UtcNow;
            AddRows(version, normalizedRows, now);
            version.Revision++;
            version.UpdatedAt = now;
            version.UpdatedBy = UserName;
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            updatedVersion = MapVersion(
                (await LoadVersionAsync(
                    version.Id,
                    asNoTracking: true,
                    cancellationToken))!);
        });

        await RecordAuditAsync(
            FinanceAuditEvents.FinancialStatementLayoutRowsReplaced,
            versionForValidation.FinancialStatementLayoutId,
            new
            {
                versionId,
                versionForValidation.VersionNumber,
                rowCount = normalizedRows.Count,
                revision = updatedVersion!.Revision,
                warnings = validation.Issues.Count(issue =>
                    issue.Severity == FinancialStatementLayoutValidationSeverity.Warning)
            },
            cancellationToken);

        return updatedVersion!;
    }

    public async Task<FinancialStatementLayoutValidationResultDto> ValidateVersionAsync(
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        var version = await LoadVersionAsync(versionId, asNoTracking: true, cancellationToken)
            ?? throw new KeyNotFoundException("Financial statement layout version was not found.");

        return await ValidateDefinitionAsync(
            version.FinancialStatementLayout.StatementType,
            version.FinancialStatementLayout.AccountingBookId,
            ToInputRows(version.Rows),
            cancellationToken);
    }

    public async Task<FinancialStatementLayoutVersionDto> PublishVersionAsync(
        Guid versionId,
        PublishFinancialStatementLayoutVersionDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateEffectiveDates(request.EffectiveFrom, request.EffectiveTo);
        FinancialStatementLayoutVersionDto? result = null;
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            try
            {
                var version = await LoadVersionAsync(versionId, asNoTracking: false, cancellationToken)
                    ?? throw new KeyNotFoundException("Financial statement layout version was not found.");
                EnsureDraft(version);
                EnsureEditableLayout(version.FinancialStatementLayout);
                EnsureRevision(version.Revision, request.ExpectedVersionRevision, "layout version");
                var validation = await ValidateDefinitionAsync(
                    version.FinancialStatementLayout.StatementType,
                    version.FinancialStatementLayout.AccountingBookId,
                    ToInputRows(version.Rows),
                    cancellationToken);
                ThrowForValidationErrors(validation);

                var effectiveFrom = request.EffectiveFrom?.Date ?? version.EffectiveFrom?.Date ?? DateTime.UtcNow.Date;
                var effectiveTo = request.EffectiveTo?.Date ?? version.EffectiveTo?.Date;
                ValidateEffectiveDates(effectiveFrom, effectiveTo);
                var priorVersions = await _context.FinancialStatementLayoutVersions
                    .Where(candidate => candidate.TenantId == TenantId
                        && candidate.FinancialStatementLayoutId == version.FinancialStatementLayoutId
                        && candidate.Id != version.Id && !candidate.IsDeleted
                        && candidate.Status == FinancialStatementLayoutVersionStatus.Published)
                    .ToListAsync(cancellationToken);
                var snapshot = await BuildPublicationSnapshotAsync(version, cancellationToken);
                var now = DateTime.UtcNow;
                foreach (var prior in priorVersions)
                {
                    prior.Status = FinancialStatementLayoutVersionStatus.Retired;
                    if (!prior.EffectiveTo.HasValue || prior.EffectiveTo.Value.Date >= effectiveFrom)
                        prior.EffectiveTo = effectiveFrom.AddDays(-1);
                    prior.Revision++;
                    prior.UpdatedAt = now;
                    prior.UpdatedBy = UserName;
                }

                version.Status = FinancialStatementLayoutVersionStatus.Published;
                version.EffectiveFrom = effectiveFrom;
                version.EffectiveTo = effectiveTo;
                version.PublishedAt = now;
                version.PublishedById = TryGetUserId();
                version.PublishedByName = UserName;
                version.PublicationSnapshotSchemaVersion = FinancialStatementPublicationFingerprint.SnapshotSchemaVersion;
                version.PublishedAccountingBookId = version.FinancialStatementLayout.AccountingBookId;
                version.PublishedAccountingBookCode = version.FinancialStatementLayout.AccountingBook.Code;
                version.PublishedAccountingBookName = version.FinancialStatementLayout.AccountingBook.Name;
                version.HierarchyFingerprint = snapshot.HierarchyFingerprint;
                version.PublicationAccounts = snapshot.Accounts;
                version.ResolutionFingerprint = FinancialStatementPublicationFingerprint.Resolution(
                    TenantId, version.Id, version.PublishedAccountingBookId.Value,
                    version.PublishedAccountingBookCode, version.HierarchyFingerprint, snapshot.Accounts);
                version.Revision++;
                version.UpdatedAt = now;
                version.UpdatedBy = UserName;
                _context.FinancialStatementPublicationAccounts.AddRange(snapshot.Accounts);
                await _context.SaveChangesAsync(cancellationToken);
                await RecordAuditAsync(FinanceAuditEvents.FinancialStatementLayoutVersionPublished,
                    version.FinancialStatementLayoutId,
                    new { versionId = version.Id, version.VersionNumber, version.EffectiveFrom,
                        version.EffectiveTo, version.HierarchyFingerprint, version.ResolutionFingerprint,
                        resolvedAccountCount = snapshot.Accounts.Count,
                        retiredVersionIds = priorVersions.Select(item => item.Id).ToArray(),
                        warnings = validation.Issues.Where(issue => issue.Severity == FinancialStatementLayoutValidationSeverity.Warning) },
                    cancellationToken);
                if (transaction != null) await transaction.CommitAsync(cancellationToken);
                result = MapVersion(version);
            }
            catch
            {
                if (transaction != null) await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
        return result!;
    }

    public async Task<FinancialStatementLayoutDto> CloneProtectedStandardAsync(
        Guid sourceLayoutId,
        CloneFinancialStatementLayoutDto request,
        CancellationToken cancellationToken = default)
    {
        var source = await LoadLayoutAsync(sourceLayoutId, asNoTracking: false, cancellationToken)
            ?? throw new KeyNotFoundException("Protected standard layout was not found.");
        if (!source.IsProtectedStandard)
            throw new InvalidOperationException("Only a protected standard layout can be cloned through this operation.");
        var book = await GetOwnedBookAsync(request.AccountingBookId, cancellationToken);
        if (book.Id != source.AccountingBookId)
            throw new InvalidOperationException("A protected standard may only be cloned within its exact accounting book.");
        var code = NormalizeCode(request.Code);
        if (await _context.FinancialStatementLayouts.AnyAsync(item => item.TenantId == TenantId && !item.IsDeleted && item.Code == code, cancellationToken))
            throw new InvalidOperationException($"Financial statement layout code '{code}' already exists.");
        var sourceVersion = source.Versions.Where(item => !item.IsDeleted)
            .OrderByDescending(item => item.VersionNumber).FirstOrDefault()
            ?? throw new InvalidOperationException("The protected standard has no definition to clone.");
        var now = DateTime.UtcNow;
        var clone = new FinancialStatementLayout
        {
            Id = Guid.NewGuid(), TenantId = TenantId, Code = code,
            Name = RequireText(request.Name, "Layout name"),
            Description = source.Description, StatementType = source.StatementType,
            AccountingBookId = book.Id, IsActive = true, IsDefault = false,
            IsProtectedStandard = false, StandardSourceLayoutId = source.Id,
            Revision = 1, CreatedAt = now, CreatedBy = UserName
        };
        var draft = new FinancialStatementLayoutVersion
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            FinancialStatementLayoutId = clone.Id, VersionNumber = 1,
            Status = FinancialStatementLayoutVersionStatus.Draft,
            EffectiveFrom = sourceVersion.EffectiveFrom, Notes = $"Cloned from protected standard {source.Code}.",
            Revision = 1, CreatedAt = now, CreatedBy = UserName
        };
        clone.Versions.Add(draft);
        _context.FinancialStatementLayouts.Add(clone);
        _context.FinancialStatementLayoutVersions.Add(draft);
        CloneRows(sourceVersion, draft, now);
        await _context.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(FinanceAuditEvents.FinancialStatementLayoutCreated, clone.Id,
            new { clone.Code, clone.Name, clone.StandardSourceLayoutId, draftVersionId = draft.Id }, cancellationToken);
        return MapLayout((await LoadLayoutAsync(clone.Id, true, cancellationToken))!);
    }

    private async Task<PublicationSnapshotBuild> BuildPublicationSnapshotAsync(
        FinancialStatementLayoutVersion version,
        CancellationToken cancellationToken)
    {
        var book = version.FinancialStatementLayout.AccountingBook;
        if (book.TenantId != TenantId || book.IsDeleted || !book.IsActive || !book.AllowsPosting)
            throw new InvalidOperationException("Publication requires the layout's exact active, posting-enabled accounting book.");

        var sources = await _context.AccountAccountingBooks.AsNoTracking()
            .Where(mapping => mapping.TenantId == TenantId
                && mapping.AccountingBookId == book.Id && mapping.IsEnabled && !mapping.IsDeleted
                && mapping.Account.TenantId == TenantId && !mapping.Account.IsDeleted)
            .Select(mapping => new SnapshotAccountSource(
                mapping.Account.Id, mapping.Account.AccountNumber, mapping.Account.AccountName,
                mapping.Account.AccountType, mapping.Account.ParentAccountId,
                mapping.AccountClassificationId))
            .ToListAsync(cancellationToken);
        var sourceById = sources.ToDictionary(item => item.Id);
        var childAccounts = sources.Where(item => item.ParentAccountId.HasValue)
            .GroupBy(item => item.ParentAccountId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Id).ToList());
        var classifications = await _context.AccountClassifications.AsNoTracking()
            .Where(item => item.TenantId == TenantId && item.AccountingBookId == book.Id && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var classificationById = classifications.ToDictionary(item => item.Id);
        var childClassifications = classifications.Where(item => item.ParentClassificationId.HasValue)
            .GroupBy(item => item.ParentClassificationId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Id).ToList());
        var hierarchyFingerprint = FinancialStatementPublicationFingerprint.Hierarchy(TenantId, book.Id, classifications);
        var captured = new Dictionary<Guid, FinancialStatementPublicationAccount>();

        foreach (var row in version.Rows.Where(item => !item.IsDeleted && item.RowType == FinancialStatementRowType.Account))
        {
            foreach (var mapping in row.Mappings.Where(item => !item.IsDeleted))
            {
                IEnumerable<Guid> accountIds = mapping.MappingType switch
                {
                    FinancialStatementRowMappingType.Account when mapping.AccountId.HasValue
                        => new[] { mapping.AccountId.Value },
                    FinancialStatementRowMappingType.AccountHierarchy when mapping.AccountId.HasValue
                        => ResolveHierarchy(mapping.AccountId.Value, childAccounts),
                    FinancialStatementRowMappingType.AccountRange
                        => sources.Where(item => StringComparer.OrdinalIgnoreCase.Compare(item.AccountNumber, mapping.FromAccountNumber) >= 0
                            && StringComparer.OrdinalIgnoreCase.Compare(item.AccountNumber, mapping.ToAccountNumber) <= 0).Select(item => item.Id),
                    FinancialStatementRowMappingType.Classification when mapping.AccountClassificationId.HasValue
                        => ResolveClassificationSnapshotAccounts(mapping, sources, childClassifications),
                    _ => Array.Empty<Guid>()
                };
                foreach (var accountId in accountIds.OrderBy(id => id))
                {
                    if (!sourceById.TryGetValue(accountId, out var source)) continue;
                    if (captured.ContainsKey(accountId))
                        throw new InvalidOperationException($"Account '{source.AccountNumber}' is captured more than once; publication snapshots require unambiguous row membership.");
                    AccountClassification? accountClassification = null;
                    if (source.AccountClassificationId.HasValue)
                        classificationById.TryGetValue(source.AccountClassificationId.Value, out accountClassification);
                    var item = new FinancialStatementPublicationAccount
                    {
                        Id = Guid.NewGuid(), TenantId = TenantId,
                        FinancialStatementLayoutVersionId = version.Id,
                        FinancialStatementRowId = row.Id,
                        FinancialStatementRowMappingId = mapping.Id,
                        MappingType = mapping.MappingType,
                        AccountId = source.Id,
                        RowCode = row.RowCode,
                        AccountNumber = source.AccountNumber,
                        AccountName = source.AccountName,
                        AccountType = source.AccountType,
                        AccountingBookId = book.Id,
                        AccountingBookCode = book.Code,
                        AccountClassificationId = accountClassification?.Id,
                        ClassificationCode = accountClassification?.Code,
                        ClassificationName = accountClassification?.Name,
                        ClassificationPath = accountClassification == null ? null : BuildClassificationPath(accountClassification, classificationById),
                        MappingSelector = DescribeMapping(mapping, classificationById),
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = UserName
                    };
                    captured.Add(accountId, item);
                }
            }
        }
        return new PublicationSnapshotBuild(hierarchyFingerprint, captured.Values.ToList());
    }

    private static IEnumerable<Guid> ResolveClassificationSnapshotAccounts(
        FinancialStatementRowMapping mapping,
        IEnumerable<SnapshotAccountSource> accounts,
        IReadOnlyDictionary<Guid, List<Guid>> children)
    {
        var ids = mapping.IncludeClassificationDescendants
            ? ResolveClassificationHierarchy(mapping.AccountClassificationId!.Value, children)
            : new HashSet<Guid> { mapping.AccountClassificationId!.Value };
        return accounts.Where(item => item.AccountClassificationId.HasValue && ids.Contains(item.AccountClassificationId.Value))
            .Select(item => item.Id);
    }

    private static string BuildClassificationPath(
        AccountClassification classification,
        IReadOnlyDictionary<Guid, AccountClassification> classifications)
    {
        var path = new Stack<string>();
        var seen = new HashSet<Guid>();
        AccountClassification? current = classification;
        while (current != null && seen.Add(current.Id))
        {
            path.Push($"{current.Code} - {current.Name}");
            current = current.ParentClassificationId.HasValue
                && classifications.TryGetValue(current.ParentClassificationId.Value, out var parent) ? parent : null;
        }
        return string.Join(" / ", path);
    }

    private static string DescribeMapping(
        FinancialStatementRowMapping mapping,
        IReadOnlyDictionary<Guid, AccountClassification> classifications)
        => mapping.MappingType switch
        {
            FinancialStatementRowMappingType.Account => $"ACCOUNT:{mapping.AccountId:N}",
            FinancialStatementRowMappingType.AccountHierarchy => $"ACCOUNT_HIERARCHY:{mapping.AccountId:N}",
            FinancialStatementRowMappingType.AccountRange => $"ACCOUNT_RANGE:{mapping.FromAccountNumber}:{mapping.ToAccountNumber}",
            FinancialStatementRowMappingType.Classification when mapping.AccountClassificationId.HasValue
                => $"CLASSIFICATION:{(classifications.TryGetValue(mapping.AccountClassificationId.Value, out var item) ? item.Code : mapping.AccountClassificationId.Value.ToString("N"))}:DESCENDANTS={mapping.IncludeClassificationDescendants}",
            _ => mapping.MappingType.ToString()
        };

    public async Task<FinancialStatementLayoutValidationResultDto> ValidateDefinitionAsync(
        FinancialStatementType statementType,
        Guid accountingBookId,
        IReadOnlyList<FinancialStatementRowInputDto> rows,
        CancellationToken cancellationToken)
    {
        var result = new FinancialStatementLayoutValidationResultDto();
        var book = await _context.AccountingBooks.AsNoTracking()
            .SingleOrDefaultAsync(candidate =>
                candidate.Id == accountingBookId &&
                candidate.TenantId == TenantId &&
                !candidate.IsDeleted,
                cancellationToken);
        if (book == null)
        {
            AddError(result, "ACCOUNTING_BOOK_LINEAGE_INVALID", "The accounting book does not belong to the current tenant.");
            return result;
        }
        if (!book.IsActive || !book.AllowsPosting)
        {
            AddError(result, "ACCOUNTING_BOOK_UNAVAILABLE", "The accounting book must be active and posting-enabled.");
            return result;
        }

        if (rows.Count == 0)
        {
            AddError(result, "NO_ROWS", "The layout version must contain at least one row.");
            return result;
        }

        var rowCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.RowCode))
            {
                AddError(result, "ROW_CODE_REQUIRED", "Every row requires a row code.");
                continue;
            }

            if (!RowCodePattern.IsMatch(row.RowCode))
            {
                AddError(
                    result,
                    "ROW_CODE_FORMAT",
                    "Row codes may contain only letters, numbers, underscores, and periods.",
                    row.RowCode);
            }

            if (row.RowCode.Equals("SUM", StringComparison.OrdinalIgnoreCase))
            {
                AddError(result, "ROW_CODE_RESERVED", "SUM is reserved for formulas.", row.RowCode);
            }

            if (!rowCodes.Add(row.RowCode))
            {
                AddError(result, "DUPLICATE_ROW_CODE", $"Row code '{row.RowCode}' is duplicated.", row.RowCode);
            }

            if (!Enum.IsDefined(row.RowType))
            {
                AddError(result, "ROW_TYPE_INVALID", "The row type is invalid.", row.RowCode);
            }

            if (row.SignMultiplier is not (-1 or 1))
            {
                AddError(result, "SIGN_INVALID", "Sign multiplier must be either 1 or -1.", row.RowCode);
            }
        }

        var rowsByCode = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.RowCode))
            .GroupBy(row => row.RowCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        ValidateParentHierarchy(rows, rowsByCode, result);

        var duplicateOrders = rows
            .GroupBy(row => new { Parent = row.ParentRowCode?.ToUpperInvariant(), row.DisplayOrder })
            .Where(group => group.Count() > 1)
            .ToList();
        foreach (var duplicate in duplicateOrders)
        {
            AddWarning(
                result,
                "DUPLICATE_DISPLAY_ORDER",
                $"Multiple sibling rows use display order {duplicate.Key.DisplayOrder}; row code will be used as the tie-breaker.");
        }

        var accounts = await _context.AccountAccountingBooks
            .AsNoTracking()
            .Where(mapping =>
                mapping.TenantId == TenantId &&
                mapping.AccountingBookId == accountingBookId &&
                mapping.IsEnabled &&
                !mapping.IsDeleted &&
                mapping.Account.TenantId == TenantId &&
                !mapping.Account.IsDeleted)
            .Select(mapping => new LayoutAccount(
                mapping.Account.Id,
                mapping.Account.AccountNumber,
                mapping.Account.AccountName,
                mapping.Account.AccountType,
                mapping.Account.ParentAccountId,
                mapping.AccountClassificationId))
            .ToListAsync(cancellationToken);
        var classifications = await _context.AccountClassifications.AsNoTracking()
            .Where(classification =>
                classification.TenantId == TenantId &&
                classification.AccountingBookId == accountingBookId &&
                !classification.IsDeleted)
            .Select(classification => new LayoutClassification(
                classification.Id,
                classification.ParentClassificationId,
                classification.Code,
                classification.Name,
                classification.CoreAccountType,
                classification.Status,
                classification.IsPostingClassification,
                classification.DisplayOrder))
            .ToListAsync(cancellationToken);
        var classificationsById = classifications.ToDictionary(classification => classification.Id);
        var childClassifications = classifications
            .Where(classification => classification.ParentClassificationId.HasValue)
            .GroupBy(classification => classification.ParentClassificationId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(classification => classification.Id).ToList());
        var accountsById = accounts.ToDictionary(account => account.Id);
        var childAccounts = accounts
            .Where(account => account.ParentAccountId.HasValue)
            .GroupBy(account => account.ParentAccountId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(account => account.Id).ToList());
        var contributingAccounts = new Dictionary<Guid, string>();
        var contributingRowCount = 0;

        foreach (var row in rows)
        {
            ValidateRowShape(row, rows, result);
            if (row.RowType != FinancialStatementRowType.Account)
            {
                continue;
            }

            contributingRowCount++;
            var rowAccountIds = new HashSet<Guid>();
            foreach (var mapping in row.Mappings)
            {
                var resolved = ResolveMappingAccounts(
                    mapping,
                    row.RowCode,
                    accounts,
                    accountsById,
                    childAccounts,
                    classificationsById,
                    childClassifications,
                    result);
                foreach (var accountId in resolved)
                {
                    if (!rowAccountIds.Add(accountId))
                    {
                        AddError(result, "OVERLAPPING_ROW_MAPPINGS", $"Account '{accountsById[accountId].AccountNumber}' is captured by more than one mapping on row '{row.RowCode}'.", row.RowCode);
                    }
                }
            }

            foreach (var accountId in rowAccountIds)
            {
                if (!accountsById.TryGetValue(accountId, out var account))
                {
                    continue;
                }

                if (!IsCompatible(statementType, account.AccountType))
                {
                    AddError(
                        result,
                        "ACCOUNT_TYPE_INCOMPATIBLE",
                        $"Account '{account.AccountNumber} - {account.AccountName}' is not compatible with a {statementType} layout.",
                        row.RowCode);
                }

                if (contributingAccounts.TryGetValue(accountId, out var priorRowCode))
                {
                    AddError(
                        result,
                        "DUPLICATE_ACCOUNT_CONTRIBUTION",
                        $"Account '{account.AccountNumber}' contributes to both row '{priorRowCode}' and row '{row.RowCode}'.",
                        row.RowCode);
                }
                else
                {
                    contributingAccounts[accountId] = row.RowCode;
                }
            }
        }

        if (contributingRowCount == 0)
        {
            AddError(
                result,
                "NO_ACCOUNT_ROWS",
                "The layout requires at least one account row.");
        }

        ValidateFormulas(rows, rowsByCode, result);

        // Closed accounts can still carry historical balances and therefore remain
        // relevant to prior-period statements.
        var eligibleAccountCount = accounts.Count(account =>
            IsCompatible(statementType, account.AccountType));
        var unmappedCount = Math.Max(0, eligibleAccountCount - contributingAccounts.Count);
        if (unmappedCount > 0)
        {
            AddWarning(
                result,
                "UNMAPPED_ACCOUNTS",
                $"{unmappedCount} eligible GL account(s) are not mapped by this layout.");
        }

        return result;
    }

    private static void ValidateParentHierarchy(
        IReadOnlyList<FinancialStatementRowInputDto> rows,
        IReadOnlyDictionary<string, FinancialStatementRowInputDto> rowsByCode,
        FinancialStatementLayoutValidationResultDto result)
    {
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.ParentRowCode))
            {
                continue;
            }

            if (!rowsByCode.ContainsKey(row.ParentRowCode))
            {
                AddError(
                    result,
                    "PARENT_ROW_UNKNOWN",
                    $"Parent row '{row.ParentRowCode}' does not exist.",
                    row.RowCode);
            }

            if (row.RowCode.Equals(row.ParentRowCode, StringComparison.OrdinalIgnoreCase))
            {
                AddError(result, "PARENT_ROW_SELF", "A row cannot be its own parent.", row.RowCode);
            }
        }

        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            VisitParent(row, rowsByCode, state, result);
        }
    }

    private static void VisitParent(
        FinancialStatementRowInputDto row,
        IReadOnlyDictionary<string, FinancialStatementRowInputDto> rowsByCode,
        IDictionary<string, int> state,
        FinancialStatementLayoutValidationResultDto result)
    {
        if (state.TryGetValue(row.RowCode, out var existingState))
        {
            if (existingState == 1)
            {
                AddError(
                    result,
                    "PARENT_CYCLE",
                    $"The parent hierarchy contains a cycle involving row '{row.RowCode}'.",
                    row.RowCode);
            }
            return;
        }

        state[row.RowCode] = 1;
        if (!string.IsNullOrWhiteSpace(row.ParentRowCode)
            && rowsByCode.TryGetValue(row.ParentRowCode, out var parent))
        {
            VisitParent(parent, rowsByCode, state, result);
        }
        state[row.RowCode] = 2;
    }

    private static void ValidateRowShape(
        FinancialStatementRowInputDto row,
        IReadOnlyList<FinancialStatementRowInputDto> rows,
        FinancialStatementLayoutValidationResultDto result)
    {
        var hasFormula = !string.IsNullOrWhiteSpace(row.Formula);
        var hasMappings = row.Mappings.Count > 0;

        switch (row.RowType)
        {
            case FinancialStatementRowType.Account:
                if (!hasMappings)
                {
                    AddError(result, "ACCOUNT_MAPPING_REQUIRED", "Account rows require at least one mapping.", row.RowCode);
                }
                if (hasFormula)
                {
                    AddError(result, "ACCOUNT_FORMULA_NOT_ALLOWED", "Account rows cannot contain formulas.", row.RowCode);
                }
                break;

            case FinancialStatementRowType.Formula:
                if (!hasFormula)
                {
                    AddError(result, "FORMULA_REQUIRED", "Formula rows require a formula.", row.RowCode);
                }
                if (hasMappings)
                {
                    AddError(result, "FORMULA_MAPPING_NOT_ALLOWED", "Formula rows cannot contain account mappings.", row.RowCode);
                }
                break;

            case FinancialStatementRowType.Total:
                if (hasMappings)
                {
                    AddError(result, "TOTAL_MAPPING_NOT_ALLOWED", "Total rows cannot contain account mappings.", row.RowCode);
                }
                if (!hasFormula && !rows.Any(candidate =>
                        candidate.ParentRowCode?.Equals(row.RowCode, StringComparison.OrdinalIgnoreCase) == true))
                {
                    AddWarning(
                        result,
                        "EMPTY_TOTAL",
                        "A total row without a formula should have child rows to total.",
                        row.RowCode);
                }
                break;

            case FinancialStatementRowType.Header:
            case FinancialStatementRowType.Spacer:
                if (hasFormula || hasMappings)
                {
                    AddError(
                        result,
                        "PRESENTATION_ROW_HAS_SOURCE",
                        "Header and spacer rows cannot contain formulas or account mappings.",
                        row.RowCode);
                }
                break;
        }
    }

    private static IReadOnlyCollection<Guid> ResolveMappingAccounts(
        FinancialStatementRowMappingInputDto mapping,
        string rowCode,
        IReadOnlyCollection<LayoutAccount> accounts,
        IReadOnlyDictionary<Guid, LayoutAccount> accountsById,
        IReadOnlyDictionary<Guid, List<Guid>> childAccounts,
        IReadOnlyDictionary<Guid, LayoutClassification> classificationsById,
        IReadOnlyDictionary<Guid, List<Guid>> childClassifications,
        FinancialStatementLayoutValidationResultDto result)
    {
        if (!Enum.IsDefined(mapping.MappingType))
        {
            AddError(result, "MAPPING_TYPE_INVALID", "The mapping type is invalid.", rowCode);
            return Array.Empty<Guid>();
        }

        switch (mapping.MappingType)
        {
            case FinancialStatementRowMappingType.Account:
                if (!string.IsNullOrWhiteSpace(mapping.FromAccountNumber)
                    || !string.IsNullOrWhiteSpace(mapping.ToAccountNumber)
                    || mapping.AccountClassificationId.HasValue)
                {
                    AddError(
                        result,
                        "MAPPING_FIELDS_INVALID",
                        "An exact-account mapping cannot also specify an account-number range.",
                        rowCode);
                }
                if (!mapping.AccountId.HasValue)
                {
                    AddError(result, "ACCOUNT_ID_REQUIRED", "An exact-account mapping requires an account id.", rowCode);
                    return Array.Empty<Guid>();
                }
                if (!accountsById.ContainsKey(mapping.AccountId.Value))
                {
                    AddError(
                        result,
                        "ACCOUNT_NOT_AVAILABLE",
                        "The mapped account is not available to the selected accounting book.",
                        rowCode);
                    return Array.Empty<Guid>();
                }
                return new[] { mapping.AccountId.Value };

            case FinancialStatementRowMappingType.AccountHierarchy:
                if (!string.IsNullOrWhiteSpace(mapping.FromAccountNumber)
                    || !string.IsNullOrWhiteSpace(mapping.ToAccountNumber)
                    || mapping.AccountClassificationId.HasValue)
                {
                    AddError(
                        result,
                        "MAPPING_FIELDS_INVALID",
                        "A hierarchy mapping cannot also specify an account-number range.",
                        rowCode);
                }
                if (!mapping.AccountId.HasValue)
                {
                    AddError(result, "HIERARCHY_ROOT_REQUIRED", "A hierarchy mapping requires a root account id.", rowCode);
                    return Array.Empty<Guid>();
                }
                if (!accountsById.ContainsKey(mapping.AccountId.Value))
                {
                    AddError(
                        result,
                        "HIERARCHY_ROOT_NOT_AVAILABLE",
                        "The hierarchy root is not available to the selected accounting book.",
                        rowCode);
                    return Array.Empty<Guid>();
                }
                return ResolveHierarchy(mapping.AccountId.Value, childAccounts);

            case FinancialStatementRowMappingType.AccountRange:
                if (mapping.AccountId.HasValue || mapping.AccountClassificationId.HasValue)
                {
                    AddError(
                        result,
                        "MAPPING_FIELDS_INVALID",
                        "An account-range mapping cannot also specify an individual account.",
                        rowCode);
                }
                var from = NormalizeOptionalText(mapping.FromAccountNumber);
                var to = NormalizeOptionalText(mapping.ToAccountNumber);
                if (from == null || to == null)
                {
                    AddError(result, "ACCOUNT_RANGE_REQUIRED", "An account range requires both from and to account numbers.", rowCode);
                    return Array.Empty<Guid>();
                }
                if (StringComparer.OrdinalIgnoreCase.Compare(from, to) > 0)
                {
                    AddError(result, "ACCOUNT_RANGE_REVERSED", $"Account range '{from}' to '{to}' is reversed.", rowCode);
                    return Array.Empty<Guid>();
                }

                var matches = accounts
                    .Where(account =>
                        StringComparer.OrdinalIgnoreCase.Compare(account.AccountNumber, from) >= 0 &&
                        StringComparer.OrdinalIgnoreCase.Compare(account.AccountNumber, to) <= 0)
                    .Select(account => account.Id)
                    .ToList();
                if (matches.Count == 0)
                {
                    AddWarning(
                        result,
                        "ACCOUNT_RANGE_EMPTY",
                        $"Account range '{from}' to '{to}' currently resolves to no GL accounts.",
                        rowCode);
                }
                return matches;

            case FinancialStatementRowMappingType.Classification:
                if (mapping.AccountId.HasValue
                    || !string.IsNullOrWhiteSpace(mapping.FromAccountNumber)
                    || !string.IsNullOrWhiteSpace(mapping.ToAccountNumber))
                {
                    AddError(result, "MAPPING_FIELDS_INVALID", "A classification mapping cannot also specify account or range selectors.", rowCode);
                }
                if (!mapping.AccountClassificationId.HasValue)
                {
                    AddError(result, "CLASSIFICATION_ID_REQUIRED", "A classification mapping requires a classification id from the selected accounting book.", rowCode);
                    return Array.Empty<Guid>();
                }
                if (!classificationsById.TryGetValue(mapping.AccountClassificationId.Value, out var classification))
                {
                    AddError(result, "CLASSIFICATION_NOT_AVAILABLE", "The classification does not belong to the selected tenant and accounting book.", rowCode);
                    return Array.Empty<Guid>();
                }
                if (classification.Status != AccountClassificationStatus.Active)
                {
                    AddError(result, "CLASSIFICATION_RETIRED_OR_UNAVAILABLE", $"Classification '{classification.Code}' is not active.", rowCode);
                    return Array.Empty<Guid>();
                }
                var classificationIds = mapping.IncludeClassificationDescendants
                    ? ResolveClassificationHierarchy(classification.Id, childClassifications)
                    : new HashSet<Guid> { classification.Id };
                var unavailable = classificationIds
                    .Select(id => classificationsById[id])
                    .FirstOrDefault(item => item.Status != AccountClassificationStatus.Active);
                if (unavailable != null)
                {
                    AddError(result, "CLASSIFICATION_DESCENDANT_UNAVAILABLE", $"Descendant classification '{unavailable.Code}' is not active.", rowCode);
                    return Array.Empty<Guid>();
                }
                var classificationMatches = accounts
                    .Where(account => account.AccountClassificationId.HasValue && classificationIds.Contains(account.AccountClassificationId.Value))
                    .Select(account => account.Id)
                    .ToList();
                if (classificationMatches.Count == 0)
                {
                    AddWarning(result, "CLASSIFICATION_EMPTY", $"Classification '{classification.Code}' currently resolves to no GL accounts.", rowCode);
                }
                return classificationMatches;

            default:
                return Array.Empty<Guid>();
        }
    }

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

    private static HashSet<Guid> ResolveClassificationHierarchy(
        Guid rootClassificationId,
        IReadOnlyDictionary<Guid, List<Guid>> childClassifications)
    {
        var resolved = new HashSet<Guid>();
        var pending = new Queue<Guid>();
        pending.Enqueue(rootClassificationId);
        while (pending.TryDequeue(out var classificationId))
        {
            if (!resolved.Add(classificationId)) continue;
            if (!childClassifications.TryGetValue(classificationId, out var children)) continue;
            foreach (var child in children) pending.Enqueue(child);
        }
        return resolved;
    }

    private static void ValidateFormulas(
        IReadOnlyList<FinancialStatementRowInputDto> rows,
        IReadOnlyDictionary<string, FinancialStatementRowInputDto> rowsByCode,
        FinancialStatementLayoutValidationResultDto result)
    {
        var orderedCodes = FinancialStatementRowOrdering
            .Order(rows)
            .Select(row => row.RowCode)
            .ToList();
        var formulaDependencies = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows.Where(row =>
                     row.RowType is FinancialStatementRowType.Formula or FinancialStatementRowType.Total
                     && !string.IsNullOrWhiteSpace(row.Formula)))
        {
            var parsed = FinancialStatementFormulaParser.Parse(row.Formula, orderedCodes);
            if (!parsed.IsValid)
            {
                AddError(result, "FORMULA_INVALID", parsed.Error ?? "The formula is invalid.", row.RowCode);
                continue;
            }

            foreach (var dependency in parsed.Dependencies)
            {
                if (!rowsByCode.ContainsKey(dependency))
                {
                    AddError(
                        result,
                        "FORMULA_ROW_UNKNOWN",
                        $"Formula references unknown row code '{dependency}'.",
                        row.RowCode);
                }
                if (dependency.Equals(row.RowCode, StringComparison.OrdinalIgnoreCase))
                {
                    AddError(result, "FORMULA_SELF_REFERENCE", "A formula cannot reference its own row.", row.RowCode);
                }
            }

            formulaDependencies[row.RowCode] = parsed.Dependencies;
        }

        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var rowCode in formulaDependencies.Keys)
        {
            VisitFormula(rowCode, formulaDependencies, state, result);
        }
    }

    private static void VisitFormula(
        string rowCode,
        IReadOnlyDictionary<string, IReadOnlySet<string>> dependencies,
        IDictionary<string, int> state,
        FinancialStatementLayoutValidationResultDto result)
    {
        if (state.TryGetValue(rowCode, out var currentState))
        {
            if (currentState == 1)
            {
                AddError(
                    result,
                    "FORMULA_CYCLE",
                    $"Formula dependencies contain a cycle involving row '{rowCode}'.",
                    rowCode);
            }
            return;
        }

        state[rowCode] = 1;
        if (dependencies.TryGetValue(rowCode, out var rowDependencies))
        {
            foreach (var dependency in rowDependencies)
            {
                if (dependencies.ContainsKey(dependency))
                {
                    VisitFormula(dependency, dependencies, state, result);
                }
            }
        }
        state[rowCode] = 2;
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

    private void AddRows(
        FinancialStatementLayoutVersion version,
        IReadOnlyList<FinancialStatementRowInputDto> rows,
        DateTime now)
    {
        var entities = rows.ToDictionary(
            row => row.RowCode,
            row => new FinancialStatementRow
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                FinancialStatementLayoutVersionId = version.Id,
                RowCode = row.RowCode,
                Label = row.Label,
                RowType = row.RowType,
                DisplayOrder = row.DisplayOrder,
                Formula = row.Formula,
                SignMultiplier = row.SignMultiplier,
                IsVisible = row.IsVisible,
                SuppressIfZero = row.SuppressIfZero,
                ShowAccountDetails = row.ShowAccountDetails,
                IsBold = row.IsBold,
                IsItalic = row.IsItalic,
                IsUnderlined = row.IsUnderlined,
                IndentLevel = row.IndentLevel,
                CreatedAt = now,
                CreatedBy = UserName
            },
            StringComparer.OrdinalIgnoreCase);

        foreach (var input in rows)
        {
            var entity = entities[input.RowCode];
            if (!string.IsNullOrWhiteSpace(input.ParentRowCode))
            {
                entity.ParentRowId = entities[input.ParentRowCode].Id;
            }

            foreach (var mapping in input.Mappings)
            {
                entity.Mappings.Add(new FinancialStatementRowMapping
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    FinancialStatementRowId = entity.Id,
                    MappingType = mapping.MappingType,
                    AccountId = mapping.AccountId,
                    AccountClassificationId = mapping.AccountClassificationId,
                    IncludeClassificationDescendants = mapping.IncludeClassificationDescendants,
                    FromAccountNumber = NormalizeOptionalText(mapping.FromAccountNumber),
                    ToAccountNumber = NormalizeOptionalText(mapping.ToAccountNumber),
                    CreatedAt = now,
                    CreatedBy = UserName
                });
            }
        }

        version.Rows = entities.Values.ToList();
        _context.FinancialStatementRows.AddRange(entities.Values);
    }

    private void CloneRows(
        FinancialStatementLayoutVersion source,
        FinancialStatementLayoutVersion target,
        DateTime now)
    {
        var clonedRows = source.Rows
            .Where(row => !row.IsDeleted)
            .ToDictionary(
                row => row.Id,
                row => new FinancialStatementRow
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    FinancialStatementLayoutVersionId = target.Id,
                    RowCode = row.RowCode,
                    Label = row.Label,
                    RowType = row.RowType,
                    DisplayOrder = row.DisplayOrder,
                    Formula = row.Formula,
                    SignMultiplier = row.SignMultiplier,
                    IsVisible = row.IsVisible,
                    SuppressIfZero = row.SuppressIfZero,
                    ShowAccountDetails = row.ShowAccountDetails,
                    IsBold = row.IsBold,
                    IsItalic = row.IsItalic,
                    IsUnderlined = row.IsUnderlined,
                    IndentLevel = row.IndentLevel,
                    CreatedAt = now,
                    CreatedBy = UserName
                });

        foreach (var sourceRow in source.Rows.Where(row => !row.IsDeleted))
        {
            var targetRow = clonedRows[sourceRow.Id];
            if (sourceRow.ParentRowId.HasValue
                && clonedRows.TryGetValue(sourceRow.ParentRowId.Value, out var targetParent))
            {
                targetRow.ParentRowId = targetParent.Id;
            }

            foreach (var mapping in sourceRow.Mappings.Where(item => !item.IsDeleted))
            {
                targetRow.Mappings.Add(new FinancialStatementRowMapping
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    FinancialStatementRowId = targetRow.Id,
                    MappingType = mapping.MappingType,
                    AccountId = mapping.AccountId,
                    AccountClassificationId = mapping.AccountClassificationId,
                    IncludeClassificationDescendants = mapping.IncludeClassificationDescendants,
                    FromAccountNumber = mapping.FromAccountNumber,
                    ToAccountNumber = mapping.ToAccountNumber,
                    CreatedAt = now,
                    CreatedBy = UserName
                });
            }
        }

        target.Rows = clonedRows.Values.ToList();
        _context.FinancialStatementRows.AddRange(clonedRows.Values);
    }

    private static IReadOnlyList<FinancialStatementRowInputDto> NormalizeRows(
        IReadOnlyCollection<FinancialStatementRowInputDto> rows)
        => rows.Select(row => new FinancialStatementRowInputDto
        {
            RowCode = NormalizeCode(row.RowCode),
            ParentRowCode = string.IsNullOrWhiteSpace(row.ParentRowCode)
                ? null
                : NormalizeCode(row.ParentRowCode),
            Label = RequireText(row.Label, $"Label for row '{row.RowCode}'"),
            RowType = row.RowType,
            DisplayOrder = row.DisplayOrder,
            Formula = NormalizeOptionalText(row.Formula),
            SignMultiplier = row.SignMultiplier,
            IsVisible = row.IsVisible,
            SuppressIfZero = row.SuppressIfZero,
            ShowAccountDetails = row.ShowAccountDetails,
            IsBold = row.IsBold,
            IsItalic = row.IsItalic,
            IsUnderlined = row.IsUnderlined,
            IndentLevel = row.IndentLevel,
            Mappings = row.Mappings.Select(mapping => new FinancialStatementRowMappingInputDto
            {
                MappingType = mapping.MappingType,
                AccountId = mapping.AccountId,
                AccountNumber = NormalizeOptionalText(mapping.AccountNumber),
                AccountClassificationId = mapping.AccountClassificationId,
                AccountClassificationCode = NormalizeOptionalText(mapping.AccountClassificationCode)?.ToUpperInvariant(),
                IncludeClassificationDescendants = mapping.IncludeClassificationDescendants,
                FromAccountNumber = NormalizeOptionalText(mapping.FromAccountNumber),
                ToAccountNumber = NormalizeOptionalText(mapping.ToAccountNumber)
            }).ToList()
        }).ToList();

    private static IReadOnlyList<FinancialStatementRowInputDto> ToInputRows(
        IEnumerable<FinancialStatementRow> sourceRows)
    {
        var rows = sourceRows.ToList();
        var codesById = rows.ToDictionary(row => row.Id, row => row.RowCode);
        return rows
            .Where(row => !row.IsDeleted)
            .Select(row => new FinancialStatementRowInputDto
            {
                RowCode = row.RowCode,
                ParentRowCode = row.ParentRowId.HasValue
                    && codesById.TryGetValue(row.ParentRowId.Value, out var parentCode)
                        ? parentCode
                        : null,
                Label = row.Label,
                RowType = row.RowType,
                DisplayOrder = row.DisplayOrder,
                Formula = row.Formula,
                SignMultiplier = row.SignMultiplier,
                IsVisible = row.IsVisible,
                SuppressIfZero = row.SuppressIfZero,
                ShowAccountDetails = row.ShowAccountDetails,
                IsBold = row.IsBold,
                IsItalic = row.IsItalic,
                IsUnderlined = row.IsUnderlined,
                IndentLevel = row.IndentLevel,
                Mappings = row.Mappings
                    .Where(mapping => !mapping.IsDeleted)
                    .Select(mapping => new FinancialStatementRowMappingInputDto
                    {
                        MappingType = mapping.MappingType,
                        AccountId = mapping.AccountId,
                        AccountNumber = mapping.Account?.AccountNumber,
                        AccountClassificationId = mapping.AccountClassificationId,
                        AccountClassificationCode = mapping.AccountClassification?.Code,
                        IncludeClassificationDescendants = mapping.IncludeClassificationDescendants,
                        FromAccountNumber = mapping.FromAccountNumber,
                        ToAccountNumber = mapping.ToAccountNumber
                    })
                    .ToList()
            })
            .ToList();
    }

    private async Task<FinancialStatementLayout?> LoadLayoutAsync(
        Guid layoutId,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        IQueryable<FinancialStatementLayout> query = _context.FinancialStatementLayouts;
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query
            .AsSplitQuery()
            .Include(layout => layout.AccountingBook)
            .Include(layout => layout.Versions)
                .ThenInclude(version => version.Rows)
                    .ThenInclude(row => row.Mappings)
                        .ThenInclude(mapping => mapping.Account)
            .Include(layout => layout.Versions)
                .ThenInclude(version => version.Rows)
                    .ThenInclude(row => row.Mappings)
                        .ThenInclude(mapping => mapping.AccountClassification)
            .Include(layout => layout.Versions)
                .ThenInclude(version => version.PublicationAccounts)
            .SingleOrDefaultAsync(layout =>
                layout.Id == layoutId &&
                layout.TenantId == TenantId &&
                !layout.IsDeleted,
                cancellationToken);
    }

    private async Task<FinancialStatementLayoutVersion?> LoadVersionAsync(
        Guid versionId,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        IQueryable<FinancialStatementLayoutVersion> query =
            _context.FinancialStatementLayoutVersions;
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query
            .AsSplitQuery()
            .Include(version => version.FinancialStatementLayout)
                .ThenInclude(layout => layout.AccountingBook)
            .Include(version => version.Rows)
                .ThenInclude(row => row.Mappings)
                    .ThenInclude(mapping => mapping.Account)
            .Include(version => version.Rows)
                .ThenInclude(row => row.Mappings)
                    .ThenInclude(mapping => mapping.AccountClassification)
            .Include(version => version.PublicationAccounts)
            .SingleOrDefaultAsync(version =>
                version.Id == versionId &&
                version.TenantId == TenantId &&
                !version.IsDeleted &&
                !version.FinancialStatementLayout.IsDeleted,
                cancellationToken);
    }

    private async Task<AccountingBook> GetOwnedBookAsync(
        Guid accountingBookId,
        CancellationToken cancellationToken)
    {
        if (accountingBookId == Guid.Empty)
        {
            throw new InvalidOperationException("An accounting book is required.");
        }

        return await _context.AccountingBooks
            .SingleOrDefaultAsync(book =>
                book.Id == accountingBookId &&
                book.TenantId == TenantId &&
                !book.IsDeleted &&
                book.IsActive &&
                book.AllowsPosting,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "The accounting book does not belong to the current tenant or is not active for posting.");
    }

    private async Task ClearOtherDefaultsAsync(
        Guid accountingBookId,
        FinancialStatementType statementType,
        Guid exceptLayoutId,
        CancellationToken cancellationToken)
    {
        var otherDefaults = await _context.FinancialStatementLayouts
            .Where(layout =>
                layout.TenantId == TenantId &&
                layout.AccountingBookId == accountingBookId &&
                layout.StatementType == statementType &&
                layout.Id != exceptLayoutId &&
                layout.IsDefault &&
                !layout.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var other in otherDefaults)
        {
            other.IsDefault = false;
            other.Revision++;
            other.UpdatedAt = DateTime.UtcNow;
            other.UpdatedBy = UserName;
        }
    }

    private static FinancialStatementLayoutDto MapLayout(FinancialStatementLayout layout)
    {
        var summary = MapSummary(layout);
        return new FinancialStatementLayoutDto
        {
            Id = summary.Id,
            Code = summary.Code,
            Name = summary.Name,
            Description = summary.Description,
            StatementType = summary.StatementType,
            AccountingBookId = summary.AccountingBookId,
            AccountingBookCode = summary.AccountingBookCode,
            AccountingBookName = summary.AccountingBookName,
            IsDefault = summary.IsDefault,
            IsActive = summary.IsActive,
            IsProtectedStandard = summary.IsProtectedStandard,
            StandardSourceLayoutId = summary.StandardSourceLayoutId,
            Revision = summary.Revision,
            LatestVersionNumber = summary.LatestVersionNumber,
            PublishedVersionNumber = summary.PublishedVersionNumber,
            Versions = layout.Versions
                .Where(version => !version.IsDeleted)
                .OrderByDescending(version => version.VersionNumber)
                .Select(MapVersion)
                .ToList()
        };
    }

    private static FinancialStatementLayoutSummaryDto MapSummary(
        FinancialStatementLayout layout)
        => new()
        {
            Id = layout.Id,
            Code = layout.Code,
            Name = layout.Name,
            Description = layout.Description,
            StatementType = layout.StatementType,
            AccountingBookId = layout.AccountingBookId,
            AccountingBookCode = layout.AccountingBook?.Code ?? string.Empty,
            AccountingBookName = layout.AccountingBook?.Name ?? string.Empty,
            IsDefault = layout.IsDefault,
            IsActive = layout.IsActive,
            IsProtectedStandard = layout.IsProtectedStandard,
            StandardSourceLayoutId = layout.StandardSourceLayoutId,
            Revision = layout.Revision,
            LatestVersionNumber = layout.Versions
                .Where(version => !version.IsDeleted)
                .Select(version => version.VersionNumber)
                .DefaultIfEmpty(0)
                .Max(),
            PublishedVersionNumber = layout.Versions
                .Where(version =>
                    !version.IsDeleted &&
                    version.Status == FinancialStatementLayoutVersionStatus.Published)
                .OrderByDescending(version => version.VersionNumber)
                .Select(version => (int?)version.VersionNumber)
                .FirstOrDefault()
        };

    private static FinancialStatementLayoutVersionDto MapVersion(
        FinancialStatementLayoutVersion version)
    {
        var rows = version.Rows
            .Where(row => !row.IsDeleted)
            .ToList();
        var codesById = rows.ToDictionary(row => row.Id, row => row.RowCode);

        return new FinancialStatementLayoutVersionDto
        {
            Id = version.Id,
            FinancialStatementLayoutId = version.FinancialStatementLayoutId,
            VersionNumber = version.VersionNumber,
            Status = version.Status,
            EffectiveFrom = version.EffectiveFrom,
            EffectiveTo = version.EffectiveTo,
            PublishedAt = version.PublishedAt,
            PublishedById = version.PublishedById,
            PublishedByName = version.PublishedByName,
            Notes = version.Notes,
            Revision = version.Revision,
            PublicationSnapshotSchemaVersion = version.PublicationSnapshotSchemaVersion,
            PublishedAccountingBookId = version.PublishedAccountingBookId,
            PublishedAccountingBookCode = version.PublishedAccountingBookCode,
            PublishedAccountingBookName = version.PublishedAccountingBookName,
            HierarchyFingerprint = version.HierarchyFingerprint,
            ResolutionFingerprint = version.ResolutionFingerprint,
            PublicationAccountCount = version.PublicationAccounts.Count(item => !item.IsDeleted),
            Rows = rows
                .OrderBy(row => row.DisplayOrder)
                .ThenBy(row => row.RowCode)
                .Select(row => new FinancialStatementRowDto
                {
                    Id = row.Id,
                    RowCode = row.RowCode,
                    ParentRowCode = row.ParentRowId.HasValue
                        && codesById.TryGetValue(row.ParentRowId.Value, out var parentCode)
                            ? parentCode
                            : null,
                    Label = row.Label,
                    RowType = row.RowType,
                    DisplayOrder = row.DisplayOrder,
                    Formula = row.Formula,
                    SignMultiplier = row.SignMultiplier,
                    IsVisible = row.IsVisible,
                    SuppressIfZero = row.SuppressIfZero,
                    ShowAccountDetails = row.ShowAccountDetails,
                    IsBold = row.IsBold,
                    IsItalic = row.IsItalic,
                    IsUnderlined = row.IsUnderlined,
                    IndentLevel = row.IndentLevel,
                    Mappings = row.Mappings
                        .Where(mapping => !mapping.IsDeleted)
                        .Select(mapping => new FinancialStatementRowMappingDto
                        {
                            Id = mapping.Id,
                            MappingType = mapping.MappingType,
                            AccountId = mapping.AccountId,
                            AccountNumber = mapping.Account?.AccountNumber,
                            AccountName = mapping.Account?.AccountName,
                            FromAccountNumber = mapping.FromAccountNumber,
                            ToAccountNumber = mapping.ToAccountNumber,
                            AccountClassificationId = mapping.AccountClassificationId,
                            AccountClassificationCode = mapping.AccountClassification?.Code,
                            AccountClassificationName = mapping.AccountClassification?.Name,
                            IncludeClassificationDescendants = mapping.IncludeClassificationDescendants
                        })
                        .ToList()
                })
                .ToList()
        };
    }

    private async Task RecordAuditAsync(
        string eventType,
        Guid layoutId,
        object values,
        CancellationToken cancellationToken)
    {
        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = TenantId,
            SourceModule = "GENERAL_LEDGER",
            SourceDocumentType = "FinancialStatementLayout",
            SourceDocumentId = layoutId,
            AfterValues = values,
            Resource = "Finance.FinancialStatementLayout",
            ResourceId = layoutId.ToString()
        }, cancellationToken);
    }

    private Guid? TryGetUserId()
        => Guid.TryParse(_currentUser.UserId, out var userId) && userId != Guid.Empty
            ? userId
            : null;

    private static void EnsureDraft(FinancialStatementLayoutVersion version)
    {
        if (version.Status != FinancialStatementLayoutVersionStatus.Draft)
        {
            throw new InvalidOperationException(
                "Published and retired layout versions are immutable. Create a new draft version.");
        }
    }

    private static void EnsureEditableLayout(FinancialStatementLayout layout)
    {
        if (layout.IsProtectedStandard)
            throw new InvalidOperationException("Protected standard layouts are clone-only and cannot be edited, retired, or published in place.");
    }

    private static void EnsureRevision(int current, int expected, string resource)
    {
        if (current != expected)
        {
            throw new DbUpdateConcurrencyException(
                $"The {resource} changed after it was loaded. Refresh it and retry.");
        }
    }

    private static void ThrowForValidationErrors(
        FinancialStatementLayoutValidationResultDto validation)
    {
        var errors = validation.Issues
            .Where(issue => issue.Severity == FinancialStatementLayoutValidationSeverity.Error)
            .ToList();
        if (errors.Count == 0)
        {
            return;
        }

        throw new FinancialStatementLayoutValidationException(validation);
    }

    private static void ValidateStatementType(FinancialStatementType statementType)
    {
        if (!Enum.IsDefined(statementType))
        {
            throw new InvalidOperationException("A valid financial statement type is required.");
        }
    }

    private static void ValidateEffectiveDates(DateTime? effectiveFrom, DateTime? effectiveTo)
    {
        if (effectiveFrom.HasValue
            && effectiveTo.HasValue
            && effectiveTo.Value.Date < effectiveFrom.Value.Date)
        {
            throw new InvalidOperationException(
                "Effective-to date cannot be earlier than effective-from date.");
        }
    }

    private static string NormalizeCode(string? value)
        => RequireText(value, "Code").Trim().ToUpperInvariant();

    private static string RequireText(string? value, string fieldName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{fieldName} is required.")
            : value.Trim();

    private static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void AddError(
        FinancialStatementLayoutValidationResultDto result,
        string code,
        string message,
        string? rowCode = null)
        => result.Issues.Add(new FinancialStatementLayoutValidationIssueDto
        {
            Severity = FinancialStatementLayoutValidationSeverity.Error,
            Code = code,
            Message = message,
            RowCode = rowCode
        });

    private static void AddWarning(
        FinancialStatementLayoutValidationResultDto result,
        string code,
        string message,
        string? rowCode = null)
        => result.Issues.Add(new FinancialStatementLayoutValidationIssueDto
        {
            Severity = FinancialStatementLayoutValidationSeverity.Warning,
            Code = code,
            Message = message,
            RowCode = rowCode
        });

    private sealed record LayoutAccount(
        Guid Id,
        string AccountNumber,
        string AccountName,
        AccountType AccountType,
        Guid? ParentAccountId,
        Guid? AccountClassificationId);

    private sealed record LayoutClassification(
        Guid Id,
        Guid? ParentClassificationId,
        string Code,
        string Name,
        AccountType CoreAccountType,
        AccountClassificationStatus Status,
        bool IsPostingClassification,
        int DisplayOrder);

    private sealed record SnapshotAccountSource(
        Guid Id,
        string AccountNumber,
        string AccountName,
        AccountType AccountType,
        Guid? ParentAccountId,
        Guid? AccountClassificationId);

    private sealed record PublicationSnapshotBuild(
        string HierarchyFingerprint,
        List<FinancialStatementPublicationAccount> Accounts);
}

public sealed class FinancialStatementLayoutValidationException : InvalidOperationException
{
    public FinancialStatementLayoutValidationException(
        FinancialStatementLayoutValidationResultDto validation)
        : base("The financial statement layout definition is invalid.")
    {
        Validation = validation;
    }

    public FinancialStatementLayoutValidationResultDto Validation { get; }
}
