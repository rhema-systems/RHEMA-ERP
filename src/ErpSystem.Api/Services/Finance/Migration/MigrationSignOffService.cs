using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Migration;

public sealed class MigrationSignOffService : IMigrationSignOffService
{
    private const string SourceModule = "MIGRATION";
    private const string PostedStatus = "Posted";
    private const decimal MoneyTolerance = 0.01m;

    private static readonly string[] RequiredLimitationIds =
    {
        "FIN-LIM-0009",
        "FIN-LIM-0010",
        "FIN-LIM-0011",
        "FIN-LIM-0013",
        "FIN-LIM-0014",
        "FIN-LIM-0021",
        "FIN-LIM-0022",
        "FIN-LIM-0028",
        "FIN-LIM-0030",
        "FIN-LIM-0031",
        "FIN-LIM-0033",
        "FIN-LIM-0034",
        "FIN-LIM-0035",
        "FIN-LIM-0037",
        "FIN-LIM-0038",
        "FIN-LIM-0039",
        "FIN-LIM-0040",
        "FIN-LIM-0041",
        "FIN-LIM-0042",
        "FIN-LIM-0043",
        "FIN-LIM-0046",
        "FIN-LIM-0047",
        "FIN-LIM-0048"
    };

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceAuditService? _financeAuditService;
    private readonly IFinanceReportExportService? _reportExportService;

    public MigrationSignOffService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IFinanceAuditService? financeAuditService = null,
        IFinanceReportExportService? reportExportService = null)
    {
        _db = db;
        _currentUser = currentUser;
        _financeAuditService = financeAuditService;
        _reportExportService = reportExportService;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    public Task<PostingBackReferenceRepairResultDto> DiagnosePostingBackReferencesAsync(
        PostingBackReferenceRepairRequestDto? request = null,
        CancellationToken cancellationToken = default)
        => RunPostingBackReferenceScanAsync(request, repair: false, cancellationToken);

    public Task<PostingBackReferenceRepairResultDto> RepairPostingBackReferencesAsync(
        PostingBackReferenceRepairRequestDto request,
        CancellationToken cancellationToken = default)
        => RunPostingBackReferenceScanAsync(request, repair: true, cancellationToken);

    public Task<BankSnapshotRebuildResultDto> DiagnoseBankSnapshotsAsync(
        BankSnapshotRebuildRequestDto? request = null,
        CancellationToken cancellationToken = default)
        => RunBankSnapshotScanAsync(request, repair: false, cancellationToken);

    public Task<BankSnapshotRebuildResultDto> RebuildBankSnapshotsAsync(
        BankSnapshotRebuildRequestDto request,
        CancellationToken cancellationToken = default)
        => RunBankSnapshotScanAsync(request, repair: true, cancellationToken);

    public Task<SubledgerOpeningMigrationDecisionDto> GetSubledgerOpeningMigrationDecisionAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var result = new SubledgerOpeningMigrationDecisionDto
        {
            TenantId = tenantId,
            GlTrialBalanceOpeningSupported = true,
            ApOpeningInvoicesSupportedByGlBatch = false,
            ArOpeningInvoicesSupportedByGlBatch = false,
            FixedAssetOpeningRegisterSupportedByGlBatch = false,
            Scope = new[]
            {
                new SubledgerOpeningMigrationScopeDto
                {
                    Area = "GL",
                    Decision = "Supported",
                    BlocksFinalSignOffIfRequired = false,
                    Rationale = "Balanced GL trial-balance opening batches are posted through IFinancePostingEngine with journal and posting-event references."
                },
                new SubledgerOpeningMigrationScopeDto
                {
                    Area = "AP",
                    Decision = "Rejected by GL-only opening-balance flow",
                    BlocksFinalSignOffIfRequired = true,
                    Rationale = "Open supplier balances that must appear in AP aging must be loaded as posted AP invoices, allocations, withholding, and settlement read-model facts, not as GL-only lines."
                },
                new SubledgerOpeningMigrationScopeDto
                {
                    Area = "AR",
                    Decision = "Rejected by GL-only opening-balance flow",
                    BlocksFinalSignOffIfRequired = true,
                    Rationale = "Open customer balances that must appear in AR aging must be loaded as posted AR invoices, receipts, credit notes, withholding, and settlement read-model facts, not as GL-only lines."
                },
                new SubledgerOpeningMigrationScopeDto
                {
                    Area = "FixedAssets",
                    Decision = "Rejected by GL-only opening-balance flow",
                    BlocksFinalSignOffIfRequired = true,
                    Rationale = "Opening asset registers must be loaded through fixed asset opening/import source records that reconcile to posted GL before asset register sign-off."
                }
            }
        };

        return Task.FromResult(result);
    }

    public async Task<FinalMigrationSignOffRunDto> RunFinalMigrationSignOffAsync(
        FinalMigrationSignOffRunRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var startedAt = DateTime.UtcNow;
        var runId = Guid.NewGuid();
        var runReference = $"SIGNOFF-{startedAt:yyyyMMddHHmmss}-{runId.ToString("N")[..8]}";
        var checks = new List<FinalMigrationSignOffCheckDto>();
        var evidenceExports = new List<FinalMigrationSignOffEvidenceExportDto>();
        var acceptedIds = NormalizeLimitationIds(request.AcceptedLimitationIds);
        var notApplicableIds = NormalizeLimitationIds(request.NotApplicableLimitationIds);

        await RecordAuditAsync(
            FinanceAuditEvents.MigrationSignOffRunStarted,
            "FinalMigrationSignOff",
            new
            {
                runId,
                runReference,
                request.RunType,
                request.AsOfDate,
                request.FiscalPeriodId,
                request.BookClassification
            },
            cancellationToken);

        try
        {
            await AddTrialBalanceChecksAsync(checks, request, cancellationToken);
            await AddOpeningBalanceChecksAsync(checks, request, cancellationToken);
            await AddBackReferenceChecksAsync(checks, cancellationToken);
            await AddBankSnapshotChecksAsync(checks, request, cancellationToken);
            await AddSubledgerSettlementChecksAsync(checks, request, cancellationToken);
            await AddFixedAssetChecksAsync(checks, request, cancellationToken);
            await AddTaxChecksAsync(checks, request, cancellationToken);
            await AddWorkflowAndPostingBoundaryChecksAsync(checks, cancellationToken);

            var cutoverEvaluation = EvaluateCutoverDataShape(request.CutoverDataShape);
            if (cutoverEvaluation.FinLim0048BlocksSignOff)
            {
                checks.Add(new FinalMigrationSignOffCheckDto
                {
                    Area = "Migration/Subledger Openings",
                    Code = "FIN-LIM-0048-BLOCKS-CUTOVER",
                    Status = "Failed",
                    Severity = "Critical",
                    Message = cutoverEvaluation.Decision
                });
            }
            else
            {
                checks.Add(new FinalMigrationSignOffCheckDto
                {
                    Area = "Migration/Subledger Openings",
                    Code = "FIN-LIM-0048-CUTOVER-SCOPE",
                    Status = "Passed",
                    Severity = "Info",
                    Message = cutoverEvaluation.Decision
                });
                notApplicableIds.Add("FIN-LIM-0048");
            }

            var limitationMatrix = BuildLimitationAcceptanceMatrix(acceptedIds, notApplicableIds, cutoverEvaluation);
            foreach (var item in limitationMatrix.Where(i => i.Classification == "AcceptedNonBlocking"))
            {
                await RecordAuditAsync(
                    FinanceAuditEvents.MigrationLimitationAccepted,
                    "LimitationAcceptance",
                    new { runId, item.LimitationId, item.Area, item.RequiredAction },
                    cancellationToken);
            }

            foreach (var item in limitationMatrix.Where(i => i.Classification == "NotApplicable"))
            {
                await RecordAuditAsync(
                    FinanceAuditEvents.MigrationLimitationMarkedNotApplicable,
                    "LimitationAcceptance",
                    new { runId, item.LimitationId, item.Area, item.Rationale },
                    cancellationToken);
            }

            evidenceExports.AddRange(await BuildEvidenceExportManifestAsync(request, cancellationToken));
            await RecordAuditAsync(
                FinanceAuditEvents.MigrationSignOffEvidenceGenerated,
                "FinalMigrationSignOff",
                new
                {
                    runId,
                    exportCount = evidenceExports.Count,
                    generatedCount = evidenceExports.Count(e => e.Status == "Generated"),
                    failedCount = evidenceExports.Count(e => e.Status == "Failed")
                },
                cancellationToken);

            foreach (var failedExport in evidenceExports.Where(e => e.Status == "Failed"))
            {
                checks.Add(new FinalMigrationSignOffCheckDto
                {
                    Area = "Evidence Exports",
                    Code = $"EXPORT-{failedExport.ReportType}-FAILED",
                    Status = "Failed",
                    Severity = "Critical",
                    Message = failedExport.Message ?? "Evidence export failed."
                });
            }

            var blockingFindings = checks.Count(IsCritical);
            var limitationBlockers = limitationMatrix.Count(i => i.BlocksSignOff);
            var warningCount = checks.Count(c => string.Equals(c.Severity, "Warning", StringComparison.OrdinalIgnoreCase));
            var status = blockingFindings + limitationBlockers > 0
                ? "Failed"
                : limitationMatrix.Any(i => i.Classification is "AcceptedNonBlocking" or "NotApplicable")
                    ? "PassedWithAcceptedLimitations"
                    : "Passed";

            if (blockingFindings + limitationBlockers > 0)
            {
                await RecordAuditAsync(
                    FinanceAuditEvents.MigrationSignOffBlockedByFinding,
                    "FinalMigrationSignOff",
                    new
                    {
                        runId,
                        blockingFindings,
                        limitationBlockers
                    },
                    cancellationToken);
            }

            var result = new FinalMigrationSignOffRunDto
            {
                TenantId = tenantId,
                RunId = runId,
                RunReference = runReference,
                RunType = string.IsNullOrWhiteSpace(request.RunType) ? "DryRun" : request.RunType.Trim(),
                AsOfDate = request.AsOfDate.Date,
                FiscalPeriodId = request.FiscalPeriodId,
                BookClassification = NormalizeBookClassification(request.BookClassification),
                Status = status,
                StartedAt = startedAt,
                CompletedAt = DateTime.UtcNow,
                CheckCount = checks.Count,
                BlockingFindingsCount = blockingFindings + limitationBlockers,
                WarningCount = warningCount,
                AcceptedLimitationsCount = limitationMatrix.Count(i => i.Classification == "AcceptedNonBlocking"),
                NotApplicableLimitationsCount = limitationMatrix.Count(i => i.Classification == "NotApplicable"),
                Checks = checks,
                EvidenceExports = evidenceExports,
                LimitationAcceptanceMatrix = limitationMatrix,
                CutoverDataShapeEvaluation = cutoverEvaluation
            };

            await RecordAuditAsync(
                FinanceAuditEvents.MigrationSignOffRunCompleted,
                "FinalMigrationSignOff",
                new
                {
                    result.RunId,
                    result.RunReference,
                    result.Status,
                    result.CheckCount,
                    result.BlockingFindingsCount,
                    result.WarningCount,
                    result.AcceptedLimitationsCount,
                    result.NotApplicableLimitationsCount
                },
                cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            await RecordAuditAsync(
                FinanceAuditEvents.MigrationSignOffRunFailed,
                "FinalMigrationSignOff",
                new
                {
                    runId,
                    runReference,
                    error = ex.Message
                },
                cancellationToken);
            throw;
        }
    }

    public async Task<SignOffReviewResultDto> ReviewSignOffRunAsync(
        SignOffReviewRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.RunId == Guid.Empty)
        {
            throw new InvalidOperationException("A sign-off run ID is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Decision))
        {
            throw new InvalidOperationException("A sign-off review decision is required.");
        }

        var result = new SignOffReviewResultDto
        {
            TenantId = TenantId,
            RunId = request.RunId,
            Decision = request.Decision.Trim(),
            ReviewedAt = DateTime.UtcNow
        };

        await RecordAuditAsync(
            FinanceAuditEvents.MigrationSignOffReviewed,
            "FinalMigrationSignOffReview",
            new
            {
                result.RunId,
                result.Decision,
                request.ReviewerComments
            },
            cancellationToken);

        return result;
    }

    private async Task AddTrialBalanceChecksAsync(
        List<FinalMigrationSignOffCheckDto> checks,
        FinalMigrationSignOffRunRequestDto request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var book = NormalizeBookClassification(request.BookClassification);
        var query = PostedTransactions(tenantId, request.AsOfDate, book);
        if (request.FiscalPeriodId.HasValue)
        {
            query = query.Where(t => t.FiscalPeriodId == request.FiscalPeriodId.Value);
        }

        var totalDebit = await query.SumAsync(t => t.DebitAmount, cancellationToken);
        var totalCredit = await query.SumAsync(t => t.CreditAmount, cancellationToken);
        var difference = RoundMoney(totalDebit - totalCredit);
        AddCheck(
            checks,
            "GL",
            "TRIAL-BALANCE-BALANCED",
            difference == 0m ? "Passed" : "Failed",
            difference == 0m ? "Info" : "Critical",
            difference == 0m
                ? "Posted GL debit and credit totals balance for the sign-off scope."
                : "Posted GL debit and credit totals do not balance for the sign-off scope.",
            difference);
    }

    private async Task AddOpeningBalanceChecksAsync(
        List<FinalMigrationSignOffCheckDto> checks,
        FinalMigrationSignOffRunRequestDto request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var batchesQuery = _db.OpeningBalanceBatches
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && !b.IsDeleted && b.OpeningDate <= request.AsOfDate);

        if (request.FiscalPeriodId.HasValue)
        {
            batchesQuery = batchesQuery.Where(b => b.FiscalPeriodId == request.FiscalPeriodId.Value);
        }

        var batches = await batchesQuery.ToListAsync(cancellationToken);
        var postedBatches = batches.Where(b => string.Equals(b.Status, PostedStatus, StringComparison.OrdinalIgnoreCase)).ToList();
        if (request.RequirePostedOpeningBalanceBatch && postedBatches.Count == 0)
        {
            AddCheck(checks, "Opening Balances", "OPENING-BALANCE-MISSING", "Failed", "Critical", "No posted opening-balance batch was found for the sign-off scope.");
        }

        foreach (var batch in batches.Where(b => !string.Equals(b.Status, PostedStatus, StringComparison.OrdinalIgnoreCase)))
        {
            AddCheck(
                checks,
                "Opening Balances",
                "OPENING-BALANCE-UNPOSTED",
                "Failed",
                "Critical",
                $"Opening-balance batch {batch.BatchNumber} is {batch.Status}, not Posted.",
                sourceReference: batch.BatchNumber,
                sourceId: batch.Id);
        }

        foreach (var batch in postedBatches)
        {
            if (RoundMoney(batch.Difference) != 0m || RoundMoney(batch.TotalDebit - batch.TotalCredit) != 0m)
            {
                AddCheck(
                    checks,
                    "Opening Balances",
                    "OPENING-BALANCE-UNBALANCED",
                    "Failed",
                    "Critical",
                    $"Posted opening-balance batch {batch.BatchNumber} is not balanced.",
                    RoundMoney(batch.TotalDebit - batch.TotalCredit),
                    sourceReference: batch.BatchNumber,
                    sourceId: batch.Id);
            }

            if (!batch.JournalEntryId.HasValue || !batch.PostingEventId.HasValue)
            {
                AddCheck(
                    checks,
                    "Opening Balances",
                    "OPENING-BALANCE-MISSING-REFERENCES",
                    "Failed",
                    "Critical",
                    $"Posted opening-balance batch {batch.BatchNumber} is missing journal or posting-event references.",
                    sourceReference: batch.BatchNumber,
                    sourceId: batch.Id);
            }
        }

        var duplicateEvents = await _db.FinancePostingEvents
            .AsNoTracking()
            .Where(e =>
                e.TenantId == tenantId &&
                e.SourceDocumentType == "OpeningBalanceBatch" &&
                e.PostingStatus == PostedStatus &&
                !e.IsDeleted)
            .GroupBy(e => e.SourceDocumentId)
            .Where(g => g.Count() > 1)
            .Select(g => new { SourceDocumentId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        foreach (var duplicate in duplicateEvents)
        {
            AddCheck(
                checks,
                "Opening Balances",
                "OPENING-BALANCE-DUPLICATE-POSTING",
                "Failed",
                "Critical",
                "Duplicate posted FinancePostingEvents exist for one opening-balance batch.",
                count: duplicate.Count,
                sourceId: duplicate.SourceDocumentId);
        }

        if (!checks.Any(c => c.Area == "Opening Balances" && IsCritical(c)))
        {
            AddCheck(checks, "Opening Balances", "OPENING-BALANCE-CHECKS", "Passed", "Info", "Opening-balance batches are posted, balanced, referenced, and not duplicated for the sign-off scope.");
        }
    }

    private async Task AddBackReferenceChecksAsync(
        List<FinalMigrationSignOffCheckDto> checks,
        CancellationToken cancellationToken)
    {
        var result = await DiagnosePostingBackReferencesAsync(null, cancellationToken);
        if (result.RepairableCount + result.AmbiguousCount + result.UnsupportedCount == 0)
        {
            AddCheck(checks, "Posting Back-References", "BACK-REFERENCE-DIAGNOSTIC", "Passed", "Info", "Posting back-reference diagnostics found no unresolved gaps.");
            return;
        }

        foreach (var item in result.Items)
        {
            AddCheck(
                checks,
                "Posting Back-References",
                $"BACK-REFERENCE-{item.Status}".ToUpperInvariant(),
                "Failed",
                "Critical",
                item.Message,
                count: item.CandidatePostingEventCount,
                sourceReference: item.SourceReference,
                sourceId: item.SourceDocumentId);
        }
    }

    private async Task AddBankSnapshotChecksAsync(
        List<FinalMigrationSignOffCheckDto> checks,
        FinalMigrationSignOffRunRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await DiagnoseBankSnapshotsAsync(null, cancellationToken);
        if (result.Items.Count == 0)
        {
            AddCheck(checks, "Cash/Bank", "BANK-SNAPSHOT-NO-BANKS", "Passed", "Info", "No tenant bank accounts were found for snapshot diagnostics.");
            return;
        }

        foreach (var item in result.Items.Where(i => i.Status != "Current"))
        {
            var isVarianceAccepted = item.Status == "Variance" && request.AcceptBankSnapshotVariance;
            AddCheck(
                checks,
                "Cash/Bank",
                $"BANK-SNAPSHOT-{item.Status}".ToUpperInvariant(),
                isVarianceAccepted ? "Warning" : "Failed",
                isVarianceAccepted ? "Warning" : "Critical",
                isVarianceAccepted
                    ? $"{item.Message} Variance was explicitly accepted for this dry-run."
                    : item.Message,
                item.Variance,
                sourceReference: item.AccountNumber,
                sourceId: item.BankAccountId);
        }

        if (!checks.Any(c => c.Area == "Cash/Bank" && IsCritical(c)))
        {
            AddCheck(checks, "Cash/Bank", "BANK-SNAPSHOT-DIAGNOSTIC", "Passed", "Info", "Bank snapshot diagnostics completed without unaccepted blocking variance.");
        }
    }

    private async Task AddSubledgerSettlementChecksAsync(
        List<FinalMigrationSignOffCheckDto> checks,
        FinalMigrationSignOffRunRequestDto request,
        CancellationToken cancellationToken)
    {
        await AddSubledgerModuleChecksAsync(checks, request, SubledgerSettlementModules.AccountsPayable, "AP", cancellationToken);
        await AddSubledgerModuleChecksAsync(checks, request, SubledgerSettlementModules.AccountsReceivable, "AR", cancellationToken);
    }

    private async Task AddSubledgerModuleChecksAsync(
        List<FinalMigrationSignOffCheckDto> checks,
        FinalMigrationSignOffRunRequestDto request,
        string module,
        string area,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var balances = await _db.SubledgerSettlementBalances
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.SourceModule == module && !b.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var balance in balances.Where(b => b.HasDiagnostics || RoundMoney(b.OperationalVariance) != 0m || !b.SourceJournalEntryId.HasValue || !b.SourcePostingEventId.HasValue))
        {
            AddCheck(
                checks,
                area,
                $"{area}-SETTLEMENT-DIAGNOSTIC",
                "Failed",
                "Critical",
                $"Settlement read-model row {balance.SourceDocumentNumber} has diagnostics, variance, or missing posting references.",
                balance.OperationalVariance,
                balance.SourceDocumentNumber,
                balance.SourceDocumentId);
        }

        var sourceTableName = module == SubledgerSettlementModules.AccountsPayable
            ? MappedTableDisplayName<VendorInvoice>()
            : MappedTableDisplayName<Invoice>();
        List<Guid> postedDocumentIds;
        try
        {
            postedDocumentIds = module == SubledgerSettlementModules.AccountsPayable
                ? await _db.VendorInvoices
                    .AsNoTracking()
                    .Where(i => i.TenantId == tenantId && !i.IsDeleted && i.JournalEntryId.HasValue && i.InvoiceDate <= request.AsOfDate)
                    .Select(i => i.Id)
                    .ToListAsync(cancellationToken)
                : await _db.Invoices
                    .AsNoTracking()
                    .Where(i => i.TenantId == tenantId && !i.IsDeleted && i.JournalEntryId.HasValue && i.InvoiceDate <= request.AsOfDate)
                    .Select(i => i.Id)
                    .ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (IsMissingDatabaseObject(ex))
        {
            AddCheck(
                checks,
                area,
                $"{area}-SOURCE-SCHEMA-MISSING",
                "Failed",
                "Critical",
                $"{area} source document table {sourceTableName} could not be queried for settlement read-model sign-off: {ex.GetBaseException().Message}");
            postedDocumentIds = new List<Guid>();
        }

        var readModelDocumentIds = balances.Select(b => b.SourceDocumentId).ToHashSet();
        var missingCount = postedDocumentIds.Count(id => !readModelDocumentIds.Contains(id));
        if (missingCount > 0)
        {
            AddCheck(
                checks,
                area,
                $"{area}-SETTLEMENT-MISSING-READMODEL",
                "Failed",
                "Critical",
                $"Posted {area} documents are missing settlement read-model rows.",
                count: missingCount);
        }

        await AddControlAccountCheckAsync(checks, request, module, area, balances, cancellationToken);

        if (!checks.Any(c => c.Area == area && IsCritical(c)))
        {
            AddCheck(checks, area, $"{area}-SETTLEMENT-CHECKS", "Passed", "Info", $"{area} settlement read-model and control checks did not find blocking variance.");
        }
    }

    private async Task AddControlAccountCheckAsync(
        List<FinalMigrationSignOffCheckDto> checks,
        FinalMigrationSignOffRunRequestDto request,
        string module,
        string area,
        IReadOnlyCollection<SubledgerSettlementBalance> balances,
        CancellationToken cancellationToken)
    {
        if (balances.Count == 0)
        {
            return;
        }

        var settings = await _db.FinanceSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == TenantId && !s.IsDeleted, cancellationToken);
        var controlAccountId = module == SubledgerSettlementModules.AccountsPayable
            ? settings?.ControlAccountApId
            : settings?.ControlAccountArId;
        if (!controlAccountId.HasValue)
        {
            AddCheck(checks, area, $"{area}-CONTROL-ACCOUNT-MISSING", "Warning", "Warning", $"{area} settlement rows exist but no control account is configured for reconciliation.");
            return;
        }

        var glMovement = await PostedTransactions(TenantId, request.AsOfDate, NormalizeBookClassification(request.BookClassification))
            .Where(t => t.AccountId == controlAccountId.Value)
            .SumAsync(t => t.DebitAmount - t.CreditAmount, cancellationToken);
        var postedControlBalance = module == SubledgerSettlementModules.AccountsPayable
            ? RoundMoney(glMovement * -1m)
            : RoundMoney(glMovement);
        var readModelOutstanding = RoundMoney(balances.Sum(b => b.OutstandingAmount));
        var variance = RoundMoney(postedControlBalance - readModelOutstanding);
        if (variance != 0m)
        {
            AddCheck(
                checks,
                area,
                $"{area}-CONTROL-RECONCILIATION-VARIANCE",
                "Failed",
                "Critical",
                $"{area} settlement outstanding does not reconcile to the posted control account.",
                variance,
                sourceId: controlAccountId.Value);
        }
    }

    private async Task AddFixedAssetChecksAsync(
        List<FinalMigrationSignOffCheckDto> checks,
        FinalMigrationSignOffRunRequestDto request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var activeAssetsMissingRefs = await _db.FixedAssets
            .AsNoTracking()
            .Where(a =>
                a.TenantId == tenantId &&
                !a.IsDeleted &&
                a.CapitalizationDate.HasValue &&
                a.CapitalizationDate <= request.AsOfDate &&
                (!a.JournalEntryId.HasValue || !a.PostingEventId.HasValue))
            .CountAsync(cancellationToken);
        if (activeAssetsMissingRefs > 0)
        {
            AddCheck(checks, "Fixed Assets", "FA-CAPITALIZATION-MISSING-REFERENCES", "Failed", "Critical", "Capitalized fixed assets are missing journal or posting-event references.", count: activeAssetsMissingRefs);
        }

        var disposedWithNbv = await _db.FixedAssets
            .AsNoTracking()
            .Where(a =>
                a.TenantId == tenantId &&
                !a.IsDeleted &&
                (a.Status == FixedAssetStatus.Disposed || a.Status == FixedAssetStatus.WrittenOff) &&
                Math.Abs(a.NetBookValue) >= MoneyTolerance)
            .CountAsync(cancellationToken);
        if (disposedWithNbv > 0)
        {
            AddCheck(checks, "Fixed Assets", "FA-DISPOSED-NONZERO-NBV", "Failed", "Critical", "Disposed or written-off fixed assets have nonzero NBV.", count: disposedWithNbv);
        }

        var completedDisposalsMissingRefs = await _db.AssetDisposals
            .AsNoTracking()
            .Where(d =>
                d.TenantId == tenantId &&
                !d.IsDeleted &&
                d.Status == AssetDisposalStatus.Completed &&
                (!d.JournalEntryId.HasValue || !d.PostingEventId.HasValue))
            .CountAsync(cancellationToken);
        if (completedDisposalsMissingRefs > 0)
        {
            AddCheck(checks, "Fixed Assets", "FA-DISPOSAL-MISSING-REFERENCES", "Failed", "Critical", "Completed fixed asset disposals are missing journal or posting-event references.", count: completedDisposalsMissingRefs);
        }

        var negativeBookValues = await _db.FixedAssetBookValues
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId && !v.IsDeleted && v.NetBookValue < 0m)
            .CountAsync(cancellationToken);
        if (negativeBookValues > 0)
        {
            AddCheck(checks, "Fixed Assets", "FA-NEGATIVE-NBV", "Failed", "Critical", "Fixed asset book values contain negative NBV.", count: negativeBookValues);
        }

        if (!checks.Any(c => c.Area == "Fixed Assets" && IsCritical(c)))
        {
            AddCheck(checks, "Fixed Assets", "FA-RECONCILIATION-CHECKS", "Passed", "Info", "Fixed asset sign-off checks did not find missing references or unreconciled disposal NBV.");
        }
    }

    private async Task AddTaxChecksAsync(
        List<FinalMigrationSignOffCheckDto> checks,
        FinalMigrationSignOffRunRequestDto request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var activeCovidLevy = await _db.Taxes
            .AsNoTracking()
            .Where(t =>
                t.TenantId == tenantId &&
                !t.IsDeleted &&
                t.IsActive &&
                t.EffectiveFrom <= request.AsOfDate &&
                ((t.Code != null && t.Code.Contains("COVID")) || (t.Name != null && t.Name.Contains("COVID"))))
            .CountAsync(cancellationToken);
        if (activeCovidLevy > 0)
        {
            AddCheck(checks, "Tax", "TAX-CURRENT-COVID-LEVY-ACTIVE", "Failed", "Critical", "Current active COVID-19 levy configuration exists for the sign-off date.", count: activeCovidLevy);
        }

        var taxSnapshotsMissingTax = await _db.Set<TaxCalculation>()
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.CalculationDate <= request.AsOfDate)
            .GroupJoin(
                _db.Taxes.AsNoTracking().Where(t => t.TenantId == tenantId && !t.IsDeleted),
                calculation => calculation.TaxId,
                tax => tax.Id,
                (calculation, taxes) => new { calculation, taxes })
            .Where(x => !x.taxes.Any())
            .CountAsync(cancellationToken);
        if (taxSnapshotsMissingTax > 0)
        {
            AddCheck(checks, "Tax", "TAX-SNAPSHOT-MISSING-CONFIG", "Failed", "Critical", "Tax calculation snapshots reference missing or cross-tenant tax configuration.", count: taxSnapshotsMissingTax);
        }

        var taxDocumentKeys = await _db.Set<TaxCalculation>()
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.TaxAmount != 0m && c.CalculationDate <= request.AsOfDate)
            .Select(c => new { Type = c.DocumentType, Id = c.DocumentId })
            .ToListAsync(cancellationToken);
        var postedTaxSourceKeys = await _db.AccountTransactions
            .AsNoTracking()
            .Where(t =>
                t.TenantId == tenantId &&
                t.PostingStatus == PostedStatus &&
                !t.IsDeleted &&
                t.SourceDocumentType != null &&
                t.SourceDocumentId.HasValue)
            .Select(t => new { Type = t.SourceDocumentType!, Id = t.SourceDocumentId!.Value })
            .ToListAsync(cancellationToken);
        var postedTaxSourceKeySet = postedTaxSourceKeys
            .Select(k => $"{k.Type}:{k.Id:N}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var taxDocumentsMissingGl = taxDocumentKeys.Count(k => !postedTaxSourceKeySet.Contains($"{k.Type}:{k.Id:N}"));
        if (taxDocumentsMissingGl > 0)
        {
            AddCheck(checks, "Tax", "TAX-SNAPSHOT-MISSING-POSTED-GL", "Failed", "Critical", "Tax snapshots exist without posted GL source lines for the same document.", count: taxDocumentsMissingGl);
        }

        if (!checks.Any(c => c.Area == "Tax" && IsCritical(c)))
        {
            AddCheck(checks, "Tax", "TAX-RECONCILIATION-CHECKS", "Passed", "Info", "Tax snapshot/configuration checks did not find blocking variance.");
        }
    }

    private async Task AddWorkflowAndPostingBoundaryChecksAsync(
        List<FinalMigrationSignOffCheckDto> checks,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var pendingWorkflowCount = await _db.WorkflowInstances
            .AsNoTracking()
            .Where(w =>
                w.TenantId == tenantId &&
                !w.IsDeleted &&
                (w.Status == WorkflowInstanceStatus.Created || w.Status == WorkflowInstanceStatus.InProgress))
            .CountAsync(cancellationToken);
        if (pendingWorkflowCount > 0)
        {
            AddCheck(checks, "Workflow", "WORKFLOW-PENDING-HIGH-RISK", "Warning", "Warning", "Pending workflow instances exist and must be reviewed before production sign-off.", count: pendingWorkflowCount);
        }

        var legacyPostingEvents = await _db.FinancePostingEvents
            .AsNoTracking()
            .Where(e =>
                e.TenantId == tenantId &&
                !e.IsDeleted &&
                (e.SourceDocumentType == "ALL_ACTIVE_BOOKS" ||
                 e.SourceDocumentType == "LegacyDirectGlPosting" ||
                 e.SourceModule == "LEGACY"))
            .CountAsync(cancellationToken);
        if (legacyPostingEvents > 0)
        {
            AddCheck(checks, "Posting Boundary", "POSTING-BYPASS-DETECTED", "Failed", "Critical", "Legacy or ALL_ACTIVE_BOOKS posting evidence was detected.", count: legacyPostingEvents);
        }

        if (!checks.Any(c => c.Area == "Posting Boundary" && IsCritical(c)))
        {
            AddCheck(checks, "Posting Boundary", "POSTING-BYPASS-BLOCKED", "Passed", "Info", "No legacy posting bypass evidence was detected in FinancePostingEvents.");
        }
    }

    private CutoverDataShapeEvaluationDto EvaluateCutoverDataShape(CutoverDataShapeDto cutover)
    {
        var requiredAreas = new List<string>();
        if (cutover.HasOpenApSupplierInvoices) requiredAreas.Add("AP opening invoices");
        if (cutover.HasOpenArCustomerInvoices) requiredAreas.Add("AR opening invoices");
        if (cutover.HasUnappliedApPaymentsOrSupplierAdvances) requiredAreas.Add("AP unapplied payments/supplier advances");
        if (cutover.HasUnappliedArReceiptsOrCustomerAdvances) requiredAreas.Add("AR unapplied receipts/customer advances");
        if (cutover.HasFixedAssetOpeningRegisterBalances) requiredAreas.Add("fixed asset opening register");
        if (cutover.HasWithholdingCertificateBalances) requiredAreas.Add("withholding certificate balances");
        if (cutover.HasForeignCurrencyOpenApArBalances) requiredAreas.Add("foreign-currency AP/AR open balances");

        var requiresSubledgerOpening = requiredAreas.Count > 0;
        return new CutoverDataShapeEvaluationDto
        {
            RequiresSubledgerOpeningMigration = requiresSubledgerOpening,
            FinLim0048BlocksSignOff = requiresSubledgerOpening,
            RequiredSourceOpeningAreas = requiredAreas,
            Decision = requiresSubledgerOpening
                ? $"FIN-LIM-0048 blocks final sign-off for this cutover shape. Required source-level openings: {string.Join(", ", requiredAreas)}."
                : "FIN-LIM-0048 is not applicable to this tenant cutover because no AP/AR/fixed-asset source-level opening balances were declared."
        };
    }

    private static IReadOnlyList<LimitationAcceptanceMatrixItemDto> BuildLimitationAcceptanceMatrix(
        HashSet<string> acceptedIds,
        HashSet<string> notApplicableIds,
        CutoverDataShapeEvaluationDto cutoverEvaluation)
        => RequiredLimitationIds
            .Select(id =>
            {
                var item = new LimitationAcceptanceMatrixItemDto
                {
                    LimitationId = id,
                    Area = LimitationArea(id),
                    RequiredAction = LimitationAction(id),
                    Rationale = LimitationRationale(id)
                };

                if (notApplicableIds.Contains(id))
                {
                    item.Classification = "NotApplicable";
                    item.GoLiveBlocking = false;
                    item.BlocksSignOff = false;
                    if (id == "FIN-LIM-0048")
                    {
                        item.Rationale = cutoverEvaluation.Decision;
                    }
                }
                else if (acceptedIds.Contains(id))
                {
                    item.Classification = "AcceptedNonBlocking";
                    item.GoLiveBlocking = false;
                    item.BlocksSignOff = false;
                }
                else
                {
                    item.Classification = "GoLiveBlocking";
                    item.GoLiveBlocking = true;
                    item.BlocksSignOff = true;
                }

                if (id == "FIN-LIM-0048" && cutoverEvaluation.FinLim0048BlocksSignOff)
                {
                    item.Classification = "GoLiveBlocking";
                    item.GoLiveBlocking = true;
                    item.BlocksSignOff = true;
                    item.Rationale = cutoverEvaluation.Decision;
                }

                return item;
            })
            .ToArray();

    private async Task<IReadOnlyList<FinalMigrationSignOffEvidenceExportDto>> BuildEvidenceExportManifestAsync(
        FinalMigrationSignOffRunRequestDto request,
        CancellationToken cancellationToken)
    {
        var reportTypes = new[]
        {
            FinanceReportExportTypes.TrialBalance,
            FinanceReportExportTypes.BalanceSheet,
            FinanceReportExportTypes.IncomeStatement,
            FinanceReportExportTypes.DetailedLedger,
            FinanceReportExportTypes.CashBankLedger,
            FinanceReportExportTypes.ApAging,
            FinanceReportExportTypes.ArAging,
            FinanceReportExportTypes.ApControlReconciliation,
            FinanceReportExportTypes.ArControlReconciliation,
            FinanceReportExportTypes.FixedAssetRegister,
            FinanceReportExportTypes.FixedAssetRollForward,
            FinanceReportExportTypes.FixedAssetGlReconciliation,
            FinanceReportExportTypes.TaxOutput,
            FinanceReportExportTypes.TaxInput,
            FinanceReportExportTypes.TaxNetSummary,
            FinanceReportExportTypes.VatWithholding,
            FinanceReportExportTypes.WhtPayable,
            FinanceReportExportTypes.WhtReceivable,
            FinanceReportExportTypes.TaxAccountReconciliation
        };

        var exports = new List<FinalMigrationSignOffEvidenceExportDto>();
        foreach (var reportType in reportTypes)
        {
            if (!request.GenerateEvidenceExports || _reportExportService == null)
            {
                exports.Add(new FinalMigrationSignOffEvidenceExportDto
                {
                    ReportType = reportType,
                    Status = "Available",
                    SourceOfTruthMode = SourceOfTruthModeForReport(reportType),
                    UsesSettlementReadModel = reportType is FinanceReportExportTypes.ApAging or FinanceReportExportTypes.ArAging,
                    Message = request.GenerateEvidenceExports
                        ? "Export service was not available to this sign-off run; report remains listed as required evidence."
                        : "Report is listed as required sign-off evidence."
                });
                continue;
            }

            try
            {
                var export = await _reportExportService.ExportAsync(BuildExportRequest(reportType, request), cancellationToken);
                exports.Add(new FinalMigrationSignOffEvidenceExportDto
                {
                    ReportType = reportType,
                    Format = export.Format,
                    Status = "Generated",
                    SourceOfTruthMode = export.SourceOfTruthMode,
                    UsesSettlementReadModel = export.UsesSettlementReadModel,
                    RowCount = export.RowCount,
                    FileName = export.FileName,
                    Message = "Evidence export generated."
                });
            }
            catch (Exception ex)
            {
                exports.Add(new FinalMigrationSignOffEvidenceExportDto
                {
                    ReportType = reportType,
                    Status = "Failed",
                    SourceOfTruthMode = SourceOfTruthModeForReport(reportType),
                    UsesSettlementReadModel = reportType is FinanceReportExportTypes.ApAging or FinanceReportExportTypes.ArAging,
                    Message = ex.Message
                });
            }
        }

        exports.Add(new FinalMigrationSignOffEvidenceExportDto
        {
            ReportType = "OpeningBalanceDiagnostics",
            Status = "Available",
            SourceOfTruthMode = "Controlled opening-balance batches and FinancePostingEvents",
            Message = "Generated by the migration sign-off run checks and SQL diagnostics."
        });
        exports.Add(new FinalMigrationSignOffEvidenceExportDto
        {
            ReportType = "BankSnapshotDiagnostics",
            Status = "Available",
            SourceOfTruthMode = "Posted GL movement compared to bank read-side snapshots",
            Message = "Generated by IMigrationSignOffService bank snapshot diagnostics."
        });
        exports.Add(new FinalMigrationSignOffEvidenceExportDto
        {
            ReportType = "PostingBackReferenceDiagnostics",
            Status = "Available",
            SourceOfTruthMode = "FinancePostingEvents and source-document back-references",
            Message = "Generated by IMigrationSignOffService posting back-reference diagnostics."
        });
        exports.Add(new FinalMigrationSignOffEvidenceExportDto
        {
            ReportType = "LimitationAcceptanceMatrix",
            Status = "Available",
            SourceOfTruthMode = "Finance go-live limitations register and run-time acceptance request",
            Message = "Generated by the migration sign-off run output."
        });

        return exports;
    }

    private static FinanceReportExportRequestDto BuildExportRequest(
        string reportType,
        FinalMigrationSignOffRunRequestDto request)
        => new()
        {
            ReportType = reportType,
            Format = FinanceReportExportFormats.Csv,
            AsOfDate = request.AsOfDate,
            PeriodStart = request.AsOfDate.Date,
            PeriodEnd = request.AsOfDate.Date,
            BookClassification = NormalizeBookClassification(request.BookClassification),
            IncludeOpeningBalances = true,
            IncludeReversed = true,
            TaxReportQuery = new TaxReportRequestDto
            {
                FromDate = request.AsOfDate.Date,
                ToDate = request.AsOfDate.Date
            },
            FixedAssetQuery = new FixedAssetReportQueryDto
            {
                FromDate = request.AsOfDate.Date,
                ToDate = request.AsOfDate.Date,
                BookClassification = NormalizeBookClassification(request.BookClassification)
            }
        };

    private IQueryable<AccountTransaction> PostedTransactions(Guid tenantId, DateTime asOfDate, string bookClassification)
        => _db.AccountTransactions
            .AsNoTracking()
            .Where(t =>
                t.TenantId == tenantId &&
                t.PostingStatus == PostedStatus &&
                !t.IsDeleted &&
                t.TransactionDate <= asOfDate &&
                t.BookClassification == bookClassification);

    private static HashSet<string> NormalizeLimitationIds(IReadOnlyList<string>? ids)
        => ids == null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : ids.Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim().ToUpperInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string NormalizeBookClassification(string? value)
        => string.IsNullOrWhiteSpace(value) ? "IFRS" : value.Trim();

    private static void AddCheck(
        List<FinalMigrationSignOffCheckDto> checks,
        string area,
        string code,
        string status,
        string severity,
        string message,
        decimal? amount = null,
        string? sourceReference = null,
        Guid? sourceId = null,
        int? count = null)
        => checks.Add(new FinalMigrationSignOffCheckDto
        {
            Area = area,
            Code = code,
            Status = status,
            Severity = severity,
            Message = message,
            Amount = amount,
            SourceReference = sourceReference,
            SourceId = sourceId,
            Count = count
        });

    private static bool IsCritical(FinalMigrationSignOffCheckDto check)
        => string.Equals(check.Severity, "Critical", StringComparison.OrdinalIgnoreCase);

    private static string SourceOfTruthModeForReport(string reportType)
        => reportType switch
        {
            FinanceReportExportTypes.ApAging or FinanceReportExportTypes.ArAging => "Settlement read model built from posted subledger facts",
            FinanceReportExportTypes.FixedAssetRegister or FinanceReportExportTypes.FixedAssetRollForward or FinanceReportExportTypes.FixedAssetGlReconciliation => "Posted GL reconciled to fixed asset subledger snapshots",
            FinanceReportExportTypes.TaxOutput or FinanceReportExportTypes.TaxInput or FinanceReportExportTypes.TaxNetSummary or FinanceReportExportTypes.VatWithholding or FinanceReportExportTypes.WhtPayable or FinanceReportExportTypes.WhtReceivable or FinanceReportExportTypes.TaxAccountReconciliation => "Posted tax snapshots and withholding records reconciled to posted GL",
            _ => "Posted GL"
        };

    private static string LimitationArea(string id)
        => id switch
        {
            "FIN-LIM-0009" or "FIN-LIM-0010" or "FIN-LIM-0011" or "FIN-LIM-0013" => "Corrections/Reversals",
            "FIN-LIM-0014" => "Tenant Roles",
            "FIN-LIM-0021" or "FIN-LIM-0022" => "FX/CashBank",
            "FIN-LIM-0028" or "FIN-LIM-0030" or "FIN-LIM-0031" or "FIN-LIM-0033" or "FIN-LIM-0034" or "FIN-LIM-0035" or "FIN-LIM-0037" or "FIN-LIM-0038" or "FIN-LIM-0039" or "FIN-LIM-0040" or "FIN-LIM-0041" or "FIN-LIM-0042" or "FIN-LIM-0043" => "Fixed Assets",
            "FIN-LIM-0046" => "Reporting/Export",
            "FIN-LIM-0047" => "Tax",
            "FIN-LIM-0048" => "Migration/Subledger Openings",
            _ => "Finance"
        };

    private static string LimitationAction(string id)
        => id switch
        {
            "FIN-LIM-0048" => "Resolve source-document opening migration or mark not applicable for this tenant cutover shape.",
            "FIN-LIM-0046" => "Accept CSV backend exports or schedule rich PDF/Excel pack enhancement.",
            "FIN-LIM-0047" => "Accept backend Ghana statutory CSV/report scope or schedule portal-specific filing/certificate workflow pack.",
            _ => "Resolve before production go-live or obtain explicit accounting/product acceptance."
        };

    private static string LimitationRationale(string id)
        => id switch
        {
            "FIN-LIM-0048" => "GL-only opening balances do not prove AP/AR aging or fixed asset register source balances.",
            "FIN-LIM-0046" => "Backend CSV export is accountant-reviewable; richer packs are product presentation scope.",
            "FIN-LIM-0047" => "Backend tax reports exist; portal-specific filing/certificate workflow may still be required.",
            _ => "Open limitation from the Finance go-live register."
        };

    private async Task<PostingBackReferenceRepairResultDto> RunPostingBackReferenceScanAsync(
        PostingBackReferenceRepairRequestDto? request,
        bool repair,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var requestedTypes = NormalizeTypeFilter(request?.SourceDocumentTypes);
        var items = new List<PostingBackReferenceRepairItemDto>();

        await ScanDocumentsAsync(
            items,
            requestedTypes,
            sourceModule: "AP",
            sourceDocumentType: "VendorInvoice",
            query: _db.VendorInvoices.Where(i => i.TenantId == tenantId && !i.IsDeleted),
            id: i => i.Id,
            reference: i => i.InvoiceNumber,
            journalEntryId: i => i.JournalEntryId,
            setJournalEntryId: (i, value) => i.JournalEntryId = value,
            postingEventId: null,
            setPostingEventId: null,
            repair,
            cancellationToken);

        await ScanDocumentsAsync(
            items,
            requestedTypes,
            sourceModule: "AP",
            sourceDocumentType: "VendorPayment",
            query: _db.Set<VendorPayment>().Where(p => p.TenantId == tenantId && !p.IsDeleted),
            id: p => p.Id,
            reference: p => p.PaymentNumber,
            journalEntryId: p => p.JournalEntryId,
            setJournalEntryId: (p, value) => p.JournalEntryId = value,
            postingEventId: null,
            setPostingEventId: null,
            repair,
            cancellationToken);

        await ScanDocumentsAsync(
            items,
            requestedTypes,
            sourceModule: "AR",
            sourceDocumentType: "CustomerInvoice",
            query: _db.Invoices.Where(i => i.TenantId == tenantId && !i.IsDeleted),
            id: i => i.Id,
            reference: i => i.InvoiceNumber,
            journalEntryId: i => i.JournalEntryId,
            setJournalEntryId: (i, value) => i.JournalEntryId = value,
            postingEventId: null,
            setPostingEventId: null,
            repair,
            cancellationToken);

        await ScanCustomerPaymentsAsync(items, requestedTypes, repair, cancellationToken);
        await ScanSalesCreditNotesAsync(items, requestedTypes, repair, cancellationToken);
        await ScanCashTransactionsAsync(items, requestedTypes, repair, cancellationToken);

        await ScanDocumentsAsync(
            items,
            requestedTypes,
            sourceModule: "FA",
            sourceDocumentType: "FixedAsset",
            query: _db.FixedAssets.Where(a => a.TenantId == tenantId && !a.IsDeleted),
            id: a => a.Id,
            reference: a => a.AssetCode,
            journalEntryId: a => a.JournalEntryId,
            setJournalEntryId: (a, value) => a.JournalEntryId = value,
            postingEventId: a => a.PostingEventId,
            setPostingEventId: (a, value) => a.PostingEventId = value,
            repair,
            cancellationToken);

        await ScanDocumentsAsync(
            items,
            requestedTypes,
            sourceModule: "FA",
            sourceDocumentType: "FixedAssetDepreciationRun",
            query: _db.FixedAssetDepreciationRuns.Where(r => r.TenantId == tenantId && !r.IsDeleted),
            id: r => r.Id,
            reference: r => r.IdempotencyKey,
            journalEntryId: r => r.JournalEntryId,
            setJournalEntryId: (r, value) => r.JournalEntryId = value,
            postingEventId: r => r.PostingEventId,
            setPostingEventId: (r, value) => r.PostingEventId = value,
            repair,
            cancellationToken);

        await ScanDocumentsAsync(
            items,
            requestedTypes,
            sourceModule: "FA",
            sourceDocumentType: "FixedAssetValuation",
            query: _db.AssetValuations.Where(v => v.TenantId == tenantId && !v.IsDeleted),
            id: v => v.Id,
            reference: v => v.ValuationReportReference,
            journalEntryId: v => v.JournalEntryId,
            setJournalEntryId: (v, value) => v.JournalEntryId = value,
            postingEventId: v => v.PostingEventId,
            setPostingEventId: (v, value) => v.PostingEventId = value,
            repair,
            cancellationToken);

        await ScanDocumentsAsync(
            items,
            requestedTypes,
            sourceModule: "FA",
            sourceDocumentType: "FixedAssetDisposal",
            query: _db.AssetDisposals.Where(d => d.TenantId == tenantId && !d.IsDeleted),
            id: d => d.Id,
            reference: d => d.ReferenceNumber,
            journalEntryId: d => d.JournalEntryId,
            setJournalEntryId: (d, value) => d.JournalEntryId = value,
            postingEventId: d => d.PostingEventId,
            setPostingEventId: (d, value) => d.PostingEventId = value,
            repair,
            cancellationToken);

        await ScanDocumentsAsync(
            items,
            requestedTypes,
            sourceModule: "AR",
            sourceDocumentType: "SubledgerAdjustmentJournal",
            query: _db.SubledgerAdjustmentJournals.Where(j => j.TenantId == tenantId && !j.IsDeleted),
            id: j => j.Id,
            reference: j => j.AdjustmentNumber,
            journalEntryId: j => j.JournalEntryId,
            setJournalEntryId: (j, value) => j.JournalEntryId = value,
            postingEventId: null,
            setPostingEventId: null,
            repair,
            cancellationToken);

        await ScanDocumentsAsync(
            items,
            requestedTypes,
            sourceModule: SourceModule,
            sourceDocumentType: "OpeningBalanceBatch",
            query: _db.OpeningBalanceBatches.Where(b => b.TenantId == tenantId && !b.IsDeleted),
            id: b => b.Id,
            reference: b => b.BatchNumber,
            journalEntryId: b => b.JournalEntryId,
            setJournalEntryId: (b, value) => b.JournalEntryId = value,
            postingEventId: b => b.PostingEventId,
            setPostingEventId: (b, value) => b.PostingEventId = value,
            repair,
            cancellationToken);

        if (repair && items.Any(i => i.WasRepaired))
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        var result = BuildBackReferenceResult(tenantId, repair, items);
        await RecordAuditAsync(
            repair && result.RepairedCount > 0
                ? FinanceAuditEvents.PostingBackReferenceRepairApplied
                : FinanceAuditEvents.PostingBackReferenceRepairDiagnosticRun,
            "PostingBackReferenceRepair",
            new
            {
                repairMode = repair,
                result.ExaminedCount,
                result.RepairableCount,
                result.RepairedCount,
                result.AmbiguousCount,
                result.UnsupportedCount
            },
            cancellationToken);

        return result;
    }

    private async Task ScanDocumentsAsync<TDocument>(
        List<PostingBackReferenceRepairItemDto> items,
        HashSet<string> requestedTypes,
        string sourceModule,
        string sourceDocumentType,
        IQueryable<TDocument> query,
        Func<TDocument, Guid> id,
        Func<TDocument, string?> reference,
        Func<TDocument, Guid?> journalEntryId,
        Action<TDocument, Guid?> setJournalEntryId,
        Func<TDocument, Guid?>? postingEventId,
        Action<TDocument, Guid?>? setPostingEventId,
        bool repair,
        CancellationToken cancellationToken)
        where TDocument : class
    {
        if (!ShouldInclude(requestedTypes, sourceDocumentType))
        {
            return;
        }

        List<TDocument> documents;
        try
        {
            documents = await query.ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (IsMissingDatabaseObject(ex))
        {
            items.Add(BuildMissingSourceTableItem(sourceModule, sourceDocumentType, ex, MappedTableDisplayName<TDocument>()));
            return;
        }
        foreach (var document in documents)
        {
            var item = await BuildBackReferenceItemAsync(
                sourceModule,
                sourceDocumentType,
                id(document),
                reference(document),
                journalEntryId(document),
                postingEventId?.Invoke(document),
                supportsPostingEventBackReference: postingEventId != null,
                cancellationToken);

            if (TryApplyBackReferenceRepair(document, item, journalEntryId, setJournalEntryId, postingEventId, setPostingEventId, repair))
            {
                item.WasRepaired = true;
                item.Status = "Repaired";
                item.Severity = "Info";
                item.Message = "Missing source-document back-reference was repaired from the unique posted FinancePostingEvent.";
            }

            if (ShouldReport(item))
            {
                items.Add(item);
            }
        }
    }

    private async Task ScanCustomerPaymentsAsync(
        List<PostingBackReferenceRepairItemDto> items,
        HashSet<string> requestedTypes,
        bool repair,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        List<CustomerPayment> payments;
        try
        {
            payments = await _db.Set<CustomerPayment>()
                .Where(p => p.TenantId == tenantId && !p.IsDeleted)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (IsMissingDatabaseObject(ex))
        {
            items.Add(BuildMissingSourceTableItem("AR", "CustomerPayment", ex, MappedTableDisplayName<CustomerPayment>()));
            return;
        }

        foreach (var payment in payments)
        {
            var sourceDocumentType = payment.IsCreditNote ? "CustomerCreditNote" : "CustomerPayment";
            if (!ShouldInclude(requestedTypes, sourceDocumentType))
            {
                continue;
            }

            var item = await BuildBackReferenceItemAsync(
                "AR",
                sourceDocumentType,
                payment.Id,
                payment.PaymentNumber,
                payment.JournalEntryId,
                currentPostingEventId: null,
                supportsPostingEventBackReference: false,
                cancellationToken);

            if (TryApplyBackReferenceRepair(payment, item, p => p.JournalEntryId, (p, value) => p.JournalEntryId = value, null, null, repair))
            {
                item.WasRepaired = true;
                item.Status = "Repaired";
                item.Severity = "Info";
                item.Message = "Missing AR payment/credit-note journal back-reference was repaired from the unique posted FinancePostingEvent.";
            }

            if (ShouldReport(item))
            {
                items.Add(item);
            }
        }
    }

    private async Task ScanSalesCreditNotesAsync(
        List<PostingBackReferenceRepairItemDto> items,
        HashSet<string> requestedTypes,
        bool repair,
        CancellationToken cancellationToken)
    {
        if (!ShouldInclude(requestedTypes, "SalesCreditNote"))
        {
            return;
        }

        var tenantId = TenantId;
        List<CreditNote> creditNotes;
        try
        {
            creditNotes = await _db.Set<CreditNote>()
                .Where(c => c.TenantId == tenantId && !c.IsDeleted)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (IsMissingDatabaseObject(ex))
        {
            items.Add(BuildMissingSourceTableItem("AR", "SalesCreditNote", ex, MappedTableDisplayName<CreditNote>()));
            return;
        }

        foreach (var creditNote in creditNotes)
        {
            var item = await BuildBackReferenceItemAsync(
                "AR",
                "SalesCreditNote",
                creditNote.Id,
                creditNote.DocumentNumber,
                creditNote.JournalEntryId,
                currentPostingEventId: null,
                supportsPostingEventBackReference: false,
                cancellationToken);

            if (TryApplyBackReferenceRepair(creditNote, item, c => c.JournalEntryId, (c, value) => c.JournalEntryId = value, null, null, repair))
            {
                item.WasRepaired = true;
                item.Status = "Repaired";
                item.Severity = "Info";
                item.Message = "Missing sales credit-note journal back-reference was repaired from the unique posted FinancePostingEvent.";
            }

            if (ShouldReport(item))
            {
                items.Add(item);
            }
        }
    }

    private async Task ScanCashTransactionsAsync(
        List<PostingBackReferenceRepairItemDto> items,
        HashSet<string> requestedTypes,
        bool repair,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        List<CashTransaction> transactions;
        try
        {
            transactions = await _db.Set<CashTransaction>()
                .Where(t => t.TenantId == tenantId && !t.IsDeleted)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (IsMissingDatabaseObject(ex))
        {
            items.Add(BuildMissingSourceTableItem("CASH", "CashTransaction", ex, MappedTableDisplayName<CashTransaction>()));
            return;
        }

        foreach (var transaction in transactions)
        {
            var sourceDocumentType = transaction.TransactionType switch
            {
                CashTransactionType.Receipt => "CashBankReceipt",
                CashTransactionType.Payment => "CashBankPayment",
                CashTransactionType.Transfer => "CashBankTransfer",
                _ => "CashTransaction"
            };

            if (!ShouldInclude(requestedTypes, sourceDocumentType))
            {
                continue;
            }

            var item = await BuildBackReferenceItemAsync(
                "CASH",
                sourceDocumentType,
                transaction.Id,
                transaction.TransactionNumber,
                transaction.JournalEntryId,
                currentPostingEventId: null,
                supportsPostingEventBackReference: false,
                cancellationToken);

            if (TryApplyBackReferenceRepair(transaction, item, t => t.JournalEntryId, (t, value) => t.JournalEntryId = value, null, null, repair))
            {
                item.WasRepaired = true;
                item.Status = "Repaired";
                item.Severity = "Info";
                item.Message = "Missing cash-bank journal back-reference was repaired from the unique posted FinancePostingEvent.";
            }

            if (ShouldReport(item))
            {
                items.Add(item);
            }
        }
    }

    private async Task<PostingBackReferenceRepairItemDto> BuildBackReferenceItemAsync(
        string sourceModule,
        string sourceDocumentType,
        Guid sourceDocumentId,
        string? sourceReference,
        Guid? currentJournalEntryId,
        Guid? currentPostingEventId,
        bool supportsPostingEventBackReference,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var item = new PostingBackReferenceRepairItemDto
        {
            SourceModule = sourceModule,
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = sourceDocumentId,
            SourceReference = sourceReference,
            CurrentJournalEntryId = currentJournalEntryId,
            CurrentPostingEventId = currentPostingEventId,
            SupportsPostingEventBackReference = supportsPostingEventBackReference,
            Status = "Current",
            Severity = "Info",
            Message = "Source-document back-references are present."
        };

        var events = await _db.FinancePostingEvents
            .AsNoTracking()
            .Where(e =>
                e.TenantId == tenantId &&
                e.SourceDocumentType == sourceDocumentType &&
                e.SourceDocumentId == sourceDocumentId &&
                e.PostingStatus == PostedStatus &&
                !e.IsDeleted)
            .ToListAsync(cancellationToken);

        item.CandidatePostingEventCount = events.Count;
        if (events.Count == 0)
        {
            if (!currentJournalEntryId.HasValue || (supportsPostingEventBackReference && !currentPostingEventId.HasValue))
            {
                item.Status = "MissingPostedEvent";
                item.Severity = "Warning";
                item.Message = "Source document is missing a back-reference and no unique posted FinancePostingEvent was found.";
            }

            return item;
        }

        if (events.Count > 1)
        {
            item.Status = "Ambiguous";
            item.Severity = "Critical";
            item.Message = "Multiple posted FinancePostingEvents match this source document; repair was refused.";
            return item;
        }

        var postingEvent = events.Single();
        item.CandidatePostingEventId = postingEvent.Id;
        item.CandidateJournalEntryId = postingEvent.JournalEntryId;
        if (!postingEvent.JournalEntryId.HasValue)
        {
            item.Status = "MissingJournalOnPostingEvent";
            item.Severity = "Critical";
            item.Message = "The unique posted FinancePostingEvent has no journal reference; repair was refused.";
            return item;
        }

        var journalExists = await _db.JournalEntries
            .AsNoTracking()
            .AnyAsync(j =>
                j.TenantId == tenantId &&
                j.Id == postingEvent.JournalEntryId.Value &&
                !j.IsDeleted,
                cancellationToken);
        if (!journalExists)
        {
            item.Status = "InvalidJournalReference";
            item.Severity = "Critical";
            item.Message = "The unique posted FinancePostingEvent points to a missing or cross-tenant journal; repair was refused.";
            return item;
        }

        var journalConflict = currentJournalEntryId.HasValue && currentJournalEntryId.Value != postingEvent.JournalEntryId.Value;
        var postingEventConflict = supportsPostingEventBackReference
            && currentPostingEventId.HasValue
            && currentPostingEventId.Value != postingEvent.Id;
        if (journalConflict || postingEventConflict)
        {
            item.Status = "ConflictingBackReference";
            item.Severity = "Critical";
            item.Message = "Existing source-document back-reference conflicts with the unique posted FinancePostingEvent; repair was refused.";
            return item;
        }

        var journalMissing = !currentJournalEntryId.HasValue;
        var postingEventMissing = supportsPostingEventBackReference && !currentPostingEventId.HasValue;
        if (journalMissing || postingEventMissing)
        {
            item.Status = "Repairable";
            item.Severity = "Warning";
            item.CanRepair = true;
            item.Message = "Source document has missing back-references that can be repaired from the unique posted FinancePostingEvent.";
        }

        return item;
    }

    private static bool TryApplyBackReferenceRepair<TDocument>(
        TDocument document,
        PostingBackReferenceRepairItemDto item,
        Func<TDocument, Guid?> journalEntryId,
        Action<TDocument, Guid?> setJournalEntryId,
        Func<TDocument, Guid?>? postingEventId,
        Action<TDocument, Guid?>? setPostingEventId,
        bool repair)
    {
        if (!repair || !item.CanRepair || !item.CandidateJournalEntryId.HasValue || !item.CandidatePostingEventId.HasValue)
        {
            return false;
        }

        var changed = false;
        if (!journalEntryId(document).HasValue)
        {
            setJournalEntryId(document, item.CandidateJournalEntryId);
            item.CurrentJournalEntryId = item.CandidateJournalEntryId;
            changed = true;
        }

        if (postingEventId != null && setPostingEventId != null && !postingEventId(document).HasValue)
        {
            setPostingEventId(document, item.CandidatePostingEventId);
            item.CurrentPostingEventId = item.CandidatePostingEventId;
            changed = true;
        }

        return changed;
    }

    private async Task<BankSnapshotRebuildResultDto> RunBankSnapshotScanAsync(
        BankSnapshotRebuildRequestDto? request,
        bool repair,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var query = _db.BankAccounts
            .Where(b => b.TenantId == tenantId && !b.IsDeleted);

        if (request?.BankAccountId is Guid bankAccountId && bankAccountId != Guid.Empty)
        {
            query = query.Where(b => b.Id == bankAccountId);
        }

        var bankAccounts = await query
            .OrderBy(b => b.AccountNumber)
            .ToListAsync(cancellationToken);

        var items = new List<BankSnapshotDiagnosticDto>();
        var userName = _currentUser.UserName ?? "system";
        var userId = CurrentUserId();
        foreach (var bank in bankAccounts)
        {
            var item = new BankSnapshotDiagnosticDto
            {
                BankAccountId = bank.Id,
                AccountNumber = bank.AccountNumber,
                AccountName = bank.AccountName,
                GlAccountId = bank.GLAccountId,
                Currency = bank.Currency,
                StoredCurrentBalance = bank.CurrentBalance,
                StoredAvailableBalance = bank.AvailableBalance,
                StoredOpeningBalance = bank.OpeningBalance,
                Status = "Current",
                Severity = "Info",
                Message = "Stored bank snapshot matches posted GL movement."
            };

            if (!bank.GLAccountId.HasValue)
            {
                item.Status = "MissingGlAccount";
                item.Severity = "Critical";
                item.Message = "Bank account has no linked GL account; snapshot rebuild was refused.";
                items.Add(item);
                continue;
            }

            var glAccountTenantValid = await _db.Accounts
                .AsNoTracking()
                .AnyAsync(a =>
                    a.TenantId == tenantId &&
                    a.Id == bank.GLAccountId.Value &&
                    !a.IsDeleted,
                    cancellationToken);
            if (!glAccountTenantValid)
            {
                item.Status = "InvalidGlAccount";
                item.Severity = "Critical";
                item.Message = "Bank account GL account is missing or belongs to another tenant; snapshot rebuild was refused.";
                items.Add(item);
                continue;
            }

            item.PostedGlBalance = await _db.AccountTransactions
                .AsNoTracking()
                .Where(t =>
                    t.TenantId == tenantId &&
                    t.AccountId == bank.GLAccountId.Value &&
                    t.PostingStatus == PostedStatus &&
                    !t.IsDeleted)
                .SumAsync(t => t.DebitAmount - t.CreditAmount, cancellationToken);

            item.Variance = RoundMoney(bank.CurrentBalance - item.PostedGlBalance);
            if (item.Variance != 0m || bank.AvailableBalance != item.PostedGlBalance)
            {
                item.Status = "Variance";
                item.Severity = "Warning";
                item.CanRepair = true;
                item.Message = "Stored bank snapshot differs from posted GL movement.";
            }

            if (repair && item.CanRepair)
            {
                bank.CurrentBalance = item.PostedGlBalance;
                bank.AvailableBalance = item.PostedGlBalance;
                bank.UpdatedAt = DateTime.UtcNow;
                bank.UpdatedBy = userName;
                bank.LastModifiedById = userId;
                item.StoredCurrentBalance = bank.CurrentBalance;
                item.StoredAvailableBalance = bank.AvailableBalance;
                item.Variance = 0m;
                item.WasRepaired = true;
                item.Status = "Repaired";
                item.Severity = "Info";
                item.Message = "Stored bank read-side snapshot was rebuilt from posted GL movement.";
            }

            items.Add(item);
        }

        if (repair && items.Any(i => i.WasRepaired))
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        var result = new BankSnapshotRebuildResultDto
        {
            TenantId = tenantId,
            RepairMode = repair,
            ExaminedCount = items.Count,
            VarianceCount = items.Count(i => i.Status is "Variance" or "Repaired"),
            RepairedCount = items.Count(i => i.WasRepaired),
            Items = items
        };

        await RecordAuditAsync(
            repair && result.RepairedCount > 0
                ? FinanceAuditEvents.BankSnapshotRebuildApplied
                : FinanceAuditEvents.BankSnapshotRebuildDiagnosticRun,
            "BankSnapshotRebuild",
            new
            {
                repairMode = repair,
                result.ExaminedCount,
                result.VarianceCount,
                result.RepairedCount
            },
            cancellationToken);

        return result;
    }

    private static PostingBackReferenceRepairResultDto BuildBackReferenceResult(
        Guid tenantId,
        bool repair,
        List<PostingBackReferenceRepairItemDto> items)
        => new()
        {
            TenantId = tenantId,
            RepairMode = repair,
            ExaminedCount = items.Count,
            RepairableCount = items.Count(i => i.CanRepair),
            RepairedCount = items.Count(i => i.WasRepaired),
            AmbiguousCount = items.Count(i => string.Equals(i.Status, "Ambiguous", StringComparison.OrdinalIgnoreCase)),
            UnsupportedCount = items.Count(i => string.Equals(i.Status, "MissingPostedEvent", StringComparison.OrdinalIgnoreCase)
                || string.Equals(i.Status, "MissingJournalOnPostingEvent", StringComparison.OrdinalIgnoreCase)
                || string.Equals(i.Status, "InvalidJournalReference", StringComparison.OrdinalIgnoreCase)
                || string.Equals(i.Status, "ConflictingBackReference", StringComparison.OrdinalIgnoreCase)
                || string.Equals(i.Status, "MissingSourceTable", StringComparison.OrdinalIgnoreCase)),
            Items = items
        };

    private static HashSet<string> NormalizeTypeFilter(IReadOnlyList<string>? values)
        => values == null || values.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : values.Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool ShouldInclude(HashSet<string> requestedTypes, string sourceDocumentType)
        => requestedTypes.Count == 0 || requestedTypes.Contains(sourceDocumentType);

    private static bool ShouldReport(PostingBackReferenceRepairItemDto item)
        => item.Status != "Current";

    private static PostingBackReferenceRepairItemDto BuildMissingSourceTableItem(
        string sourceModule,
        string sourceDocumentType,
        Exception exception,
        string? mappedTableName = null)
        => new()
        {
            SourceModule = sourceModule,
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = Guid.Empty,
            SourceReference = sourceDocumentType,
            Status = "MissingSourceTable",
            Severity = "Critical",
            Message = $"Source table {(string.IsNullOrWhiteSpace(mappedTableName) ? sourceDocumentType : mappedTableName)} for {sourceDocumentType} could not be queried during posting back-reference diagnostics: {exception.GetBaseException().Message}"
        };

    private string MappedTableDisplayName<TDocument>()
        where TDocument : class
    {
        var entityType = _db.Model.FindEntityType(typeof(TDocument));
        var tableName = entityType?.GetTableName();
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return typeof(TDocument).Name;
        }

        var schema = entityType?.GetSchema();
        return string.IsNullOrWhiteSpace(schema)
            ? tableName
            : $"{schema}.{tableName}";
    }

    private static bool IsMissingDatabaseObject(Exception exception)
    {
        var current = exception;
        while (current != null)
        {
            if (current is SqlException sqlException && (sqlException.Number == 207 || sqlException.Number == 208))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }

    private static decimal RoundMoney(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private Guid? CurrentUserId()
        => Guid.TryParse(_currentUser.UserId, out var userId) ? userId : null;

    private async Task RecordAuditAsync(
        string eventType,
        string diagnosticType,
        object payload,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        var tenantId = TenantId;
        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = tenantId,
            SourceModule = SourceModule,
            SourceDocumentType = diagnosticType,
            SourceDocumentId = tenantId,
            Resource = $"Finance.{diagnosticType}",
            ResourceId = tenantId.ToString(),
            AfterValues = payload
        }, cancellationToken);
    }
}
