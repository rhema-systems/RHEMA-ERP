using System.Globalization;
using System.Data;
using System.Text;
using System.Text.Json;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Api.Services.Finance.Migration;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

const string ConnectionString =
    "Server=RHEMA-AKWASI\\EXPRESS22;Database=RhemaERP_UAT_DryRun;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=False";

var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
var asOfDate = new DateTime(2026, 6, 30);
var outputDir = Path.GetFullPath("outputs/migration-signoff-dryrun/synthetic-cutover-smoke-test");
Directory.CreateDirectory(outputDir);

var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer(ConnectionString)
    .Options;

await using var db = new ApplicationDbContext(options);

var tenant = await db.Tenants
    .AsNoTracking()
    .FirstOrDefaultAsync(t => t.Id == tenantId)
    ?? throw new InvalidOperationException("Default Tenant was not found in RhemaERP_UAT_DryRun.");

var auditUser = await db.Users
    .AsNoTracking()
    .OrderBy(u => u.Id)
    .FirstOrDefaultAsync();
var currentUser = new SyntheticCurrentUserService(tenantId, auditUser?.Id ?? Guid.Empty);
var http = new HttpContextAccessor();
IFinanceAuditService? audit = auditUser == null
    ? null
    : new FinanceAuditService(db, currentUser, http);
var postingEngine = new FinancePostingEngine(
    db,
    currentUser,
    NullLogger<FinancePostingEngine>.Instance,
    audit);
var openingBalances = new OpeningBalanceService(
    db,
    currentUser,
    postingEngine,
    audit,
    workflowService: null);
var signOff = new MigrationSignOffService(db, currentUser, audit, reportExportService: null);

var staleSyntheticBatches = await db.OpeningBalanceBatches
    .Include(b => b.Lines)
    .Where(b =>
        b.TenantId == tenantId &&
        b.SourceReference == "SYNTHETIC-UAT-SMOKE-TEST" &&
        b.Status != "Posted")
    .ToListAsync();
if (staleSyntheticBatches.Count > 0)
{
    db.OpeningBalanceLines.RemoveRange(staleSyntheticBatches.SelectMany(b => b.Lines));
    db.OpeningBalanceBatches.RemoveRange(staleSyntheticBatches);
    await db.SaveChangesAsync();
}

var period = await db.FiscalPeriods
    .AsNoTracking()
    .Where(p => p.TenantId == tenantId && !p.IsDeleted && p.StartDate <= asOfDate && p.EndDate >= asOfDate)
    .OrderByDescending(p => p.StartDate)
    .FirstOrDefaultAsync()
    ?? await db.FiscalPeriods
        .AsNoTracking()
        .Where(p => p.TenantId == tenantId && !p.IsDeleted && p.IsOpen && !p.IsClosed)
        .OrderByDescending(p => p.EndDate)
        .FirstOrDefaultAsync()
    ?? throw new InvalidOperationException("No suitable open fiscal period was found for the synthetic smoke test.");

var accounts = await db.Accounts
    .AsNoTracking()
    .Where(a =>
        a.TenantId == tenantId &&
        !a.IsDeleted &&
        a.Status == AccountStatus.Active &&
        a.AllowDirectPosting)
    .OrderBy(a => a.AccountCode)
    .ToListAsync();

var debitAccount = PickAccount(accounts, "1000") ?? accounts.FirstOrDefault(a => a.AccountType == AccountType.Asset)
    ?? throw new InvalidOperationException("No active same-tenant direct-posting debit account was found.");
var creditAccount = PickAccount(accounts, "3000") ?? PickAccount(accounts, "1990") ?? accounts.FirstOrDefault(a => a.AccountType == AccountType.Equity || a.AccountType == AccountType.Liability)
    ?? throw new InvalidOperationException("No active same-tenant direct-posting credit account was found.");

var acceptedLimitations = new[]
{
    "FIN-LIM-0009",
    "FIN-LIM-0010",
    "FIN-LIM-0011",
    "FIN-LIM-0012",
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
    "FIN-LIM-0045",
    "FIN-LIM-0046",
    "FIN-LIM-0047"
};

var batchNumber = $"SYN-UAT-OB-{DateTime.UtcNow:yyyyMMddHHmmss}";
var idempotencyKey = $"SYNTHETIC-UAT:OpeningBalance:{tenantId:N}:{batchNumber}";
var amount = 10000m;

WriteSyntheticPack(tenant.Name, tenantId, period, asOfDate, debitAccount, creditAccount, amount);

var existingPostedSynthetic = await db.OpeningBalanceBatches
    .AsNoTracking()
    .Where(b =>
        b.TenantId == tenantId &&
        b.SourceReference == "SYNTHETIC-UAT-SMOKE-TEST" &&
        b.Status == "Posted" &&
        !b.IsDeleted)
    .OrderByDescending(b => b.PostedAt)
    .FirstOrDefaultAsync();

var existing = existingPostedSynthetic ?? await db.OpeningBalanceBatches
    .AsNoTracking()
    .Where(b => b.TenantId == tenantId && b.IdempotencyKey == idempotencyKey && !b.IsDeleted)
    .FirstOrDefaultAsync();

OpeningBalanceBatchDto batch;
if (existing == null)
{
    batch = await openingBalances.CreateBatchAsync(new CreateOpeningBalanceBatchDto
    {
        BatchNumber = batchNumber,
        SourceReference = "SYNTHETIC-UAT-SMOKE-TEST",
        Description = "Synthetic GL-only UAT cutover smoke test. Not accountant approval or production sign-off evidence.",
        OpeningDate = asOfDate,
        FiscalPeriodId = period.Id,
        BookClassification = "IFRS",
        IdempotencyKey = idempotencyKey,
        Lines = new[]
        {
            new CreateOpeningBalanceLineDto
            {
                AccountId = debitAccount.Id,
                DebitAmount = amount,
                TransactionCurrencyCode = "GHS",
                FunctionalCurrencyCode = "GHS",
                SourceReference = "SYN-GL-DR",
                Notes = "Synthetic debit opening balance for UAT smoke test only."
            },
            new CreateOpeningBalanceLineDto
            {
                AccountId = creditAccount.Id,
                CreditAmount = amount,
                TransactionCurrencyCode = "GHS",
                FunctionalCurrencyCode = "GHS",
                SourceReference = "SYN-GL-CR",
                Notes = "Synthetic credit opening balance for UAT smoke test only."
            }
        }
    });

    var validation = await openingBalances.ValidateBatchAsync(batch.Id);
    if (!validation.IsValid)
    {
        throw new InvalidOperationException("Synthetic opening-balance batch failed validation: " + string.Join("; ", validation.Errors));
    }

    batch = await openingBalances.SubmitForApprovalAsync(
        batch.Id,
        "Synthetic UAT smoke test submission; no workflow service injected, so this is not accountant approval evidence.");
    batch = await openingBalances.PostAsync(
        batch.Id,
        "Synthetic UAT smoke test opening-balance posting through IFinancePostingEngine.");
}
else
{
    batch = await openingBalances.GetBatchAsync(existing.Id)
        ?? throw new InvalidOperationException("Existing synthetic opening-balance batch could not be reloaded.");
}

var staleFixedAssetReset = await ResetStaleDemoFixedAssetsAsync(db, tenantId);
var signOffResult = await signOff.RunFinalMigrationSignOffAsync(new FinalMigrationSignOffRunRequestDto
{
    RunType = "SyntheticUatSmokeTest",
    AsOfDate = asOfDate,
    FiscalPeriodId = period.Id,
    BookClassification = "IFRS",
    RequirePostedOpeningBalanceBatch = true,
    AcceptBankSnapshotVariance = true,
    GenerateEvidenceExports = false,
    CutoverDataShape = new CutoverDataShapeDto
    {
        HasOpenApSupplierInvoices = false,
        HasOpenArCustomerInvoices = false,
        HasUnappliedApPaymentsOrSupplierAdvances = false,
        HasUnappliedArReceiptsOrCustomerAdvances = false,
        HasFixedAssetOpeningRegisterBalances = false,
        HasWithholdingCertificateBalances = false,
        HasForeignCurrencyOpenApArBalances = false,
        HasBankBalancesRequiringCashbookDetail = false
    },
    AcceptedLimitationIds = acceptedLimitations,
    NotApplicableLimitationIds = new[] { "FIN-LIM-0048" }
});

var backReferenceDiagnostic = await signOff.DiagnosePostingBackReferencesAsync(new PostingBackReferenceRepairRequestDto
{
    Repair = false
});
var arSchemaReport = await BuildArSchemaReportAsync(db);

var validationResult = new
{
    SyntheticOnly = true,
    Warning = "This is not accountant approval or production sign-off evidence.",
    Database = "RhemaERP_UAT_DryRun",
    Tenant = tenant.Name,
    TenantId = tenantId,
    AuditUserId = auditUser?.Id,
    AuditEnabled = audit != null,
    RemovedStaleSyntheticDraftBatches = staleSyntheticBatches.Count,
    CutoverDate = asOfDate,
    FiscalPeriodId = period.Id,
    FiscalPeriod = $"{period.PeriodName} ({period.StartDate:yyyy-MM-dd} to {period.EndDate:yyyy-MM-dd})",
    BookClassification = "IFRS",
    OpeningBalanceBatch = new
    {
        batch.Id,
        batch.BatchNumber,
        batch.Status,
        batch.TotalDebit,
        batch.TotalCredit,
        batch.Difference,
        batch.JournalEntryId,
        batch.PostingEventId
    },
    StaleFixedAssetReset = staleFixedAssetReset,
    Accounts = new[]
    {
        new { Side = "Debit", debitAccount.Id, debitAccount.AccountCode, debitAccount.AccountName, debitAccount.AccountType },
        new { Side = "Credit", creditAccount.Id, creditAccount.AccountCode, creditAccount.AccountName, creditAccount.AccountType }
    },
    Validations = new
    {
        DebitsEqualCredits = batch.TotalDebit == batch.TotalCredit,
        BookClassificationIsIfrs = string.Equals(batch.BookClassification, "IFRS", StringComparison.OrdinalIgnoreCase),
        ContainsAllActiveBooks = batch.BookClassification.Contains("ALL_ACTIVE_BOOKS", StringComparison.OrdinalIgnoreCase),
        AccountsAreSameTenantActivePostingAccounts = accounts.Any(a => a.Id == debitAccount.Id) && accounts.Any(a => a.Id == creditAccount.Id),
        FinLim0017RemainsOpen = true,
        FinLim0048RemainsGloballyOpen = true,
        FinLim0048SmokeDecision = "Not applicable for this synthetic GL-only smoke test."
    },
    SignOff = signOffResult
};

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    WriteIndented = true
};
await File.WriteAllTextAsync(Path.Combine(outputDir, "synthetic-smoke-result.json"), JsonSerializer.Serialize(validationResult, jsonOptions));
await File.WriteAllTextAsync(Path.Combine(outputDir, "synthetic-smoke-summary.md"), BuildSummary(validationResult, signOffResult, staleFixedAssetReset));
await File.WriteAllTextAsync(Path.Combine(outputDir, "posting-backreference-diagnostics.json"), JsonSerializer.Serialize(backReferenceDiagnostic, jsonOptions));
WriteBackReferenceCsv("posting-backreference-diagnostics.csv", backReferenceDiagnostic.Items);
WriteStaleFixedAssetResetCsv("stale-fixed-asset-reset.csv", staleFixedAssetReset);
await File.WriteAllTextAsync(Path.Combine(outputDir, "uat-ar-schema-report.json"), JsonSerializer.Serialize(arSchemaReport, jsonOptions));

Console.WriteLine(JsonSerializer.Serialize(new
{
    status = signOffResult.Status,
    runReference = signOffResult.RunReference,
    blockingFindings = signOffResult.BlockingFindingsCount,
    warnings = signOffResult.WarningCount,
    batchId = batch.Id,
    batchNumber = batch.BatchNumber,
    journalEntryId = batch.JournalEntryId,
    postingEventId = batch.PostingEventId,
    backReferenceDiagnostic.ExaminedCount,
    backReferenceDiagnostic.UnsupportedCount,
    staleFixedAssetsReset = staleFixedAssetReset.Count,
    arSchemaTables = arSchemaReport.Select(r => r.TableName).Distinct().ToArray(),
    outputDir
}, jsonOptions));

Account? PickAccount(IReadOnlyList<Account> candidates, string code)
    => candidates.FirstOrDefault(a => string.Equals(a.AccountCode, code, StringComparison.OrdinalIgnoreCase));

void WriteSyntheticPack(
    string tenantName,
    Guid tenantGuid,
    dynamic fiscalPeriod,
    DateTime cutoverDate,
    Account debit,
    Account credit,
    decimal lineAmount)
{
    var date = cutoverDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    var periodName = Convert.ToString(fiscalPeriod.PeriodName, CultureInfo.InvariantCulture) ?? "June 2026";
    WriteCsv("opening-trial-balance-synthetic.csv", new[]
    {
        "TenantName,TenantId,ApprovedCutoverDate,ApprovedFiscalPeriod,BookClassification,AccountCode,AccountName,DebitAmount,CreditAmount,Currency,SegmentDimensionString,ReferenceNotes,PreparerName,ReviewerAccountantName,ApprovalDate,ValidationNotes",
        CsvRow(tenantName, tenantGuid, date, periodName, "IFRS", debit.AccountCode, debit.AccountName, Money(lineAmount), "", "GHS", "", "Synthetic UAT GL-only smoke test debit. Not approval evidence.", "Codex synthetic runner", "Not accountant approved", date, "Same-tenant active direct-posting account"),
        CsvRow(tenantName, tenantGuid, date, periodName, "IFRS", credit.AccountCode, credit.AccountName, "", Money(lineAmount), "GHS", "", "Synthetic UAT GL-only smoke test credit. Not approval evidence.", "Codex synthetic runner", "Not accountant approved", date, "Same-tenant active direct-posting account")
    });

    WriteDeclaration("ap-cutover-declaration-synthetic.csv", new[]
    {
        "Open AP invoices",
        "Supplier advances",
        "Unapplied AP payments",
        "AP WHT certificate balances",
        "Foreign-currency AP balances"
    });
    WriteDeclaration("ar-cutover-declaration-synthetic.csv", new[]
    {
        "Open AR invoices",
        "Customer advances",
        "Unapplied AR receipts",
        "AR WHT/VAT withholding certificate balances",
        "Foreign-currency AR balances"
    });
    WriteDeclaration("fixed-asset-cutover-declaration-synthetic.csv", new[]
    {
        "Fixed asset opening register required",
        "Asset cost opening balances",
        "Accumulated depreciation opening balances",
        "Impairment opening balances",
        "Revaluation reserve balances",
        "Asset location/custodian/segment details"
    });
    WriteDeclaration("bank-cash-cutover-declaration-synthetic.csv", new[]
    {
        "Bank balances only",
        "Detailed cashbook history required",
        "Unreconciled bank items",
        "Foreign-currency bank accounts"
    });

    var accepted = acceptedLimitations.ToHashSet(StringComparer.OrdinalIgnoreCase);
    var limitationRows = new List<string>
    {
        "LimitationId,Description,AppliesToThisTenantYesNo,AcceptedNonBlockingYesNo,GoLiveBlockingYesNo,Owner,TargetDate,AccountingProductApproval,Notes"
    };
    foreach (var id in acceptedLimitations.Concat(new[] { "FIN-LIM-0048" }))
    {
        var is0048 = id == "FIN-LIM-0048";
        limitationRows.Add(CsvRow(
            id,
            is0048
                ? "AP AR and fixed-asset subledger opening balances are not in scope for this synthetic GL-only smoke test."
                : "Open production limitation accepted only for technical synthetic smoke testing.",
            is0048 ? "No" : "Yes",
            is0048 ? "No" : "Yes",
            "No",
            "Accounting/product to replace for real sign-off",
            "",
            "Synthetic smoke test only - not accountant approval",
            is0048
                ? "Not applicable for this synthetic GL-only smoke test; remains globally open."
                : "Accepted only to let the synthetic GL-only sign-off pipeline execute end-to-end."));
    }
    WriteCsv("limitation-acceptance-matrix-synthetic.csv", limitationRows);

    var checklist = $"""
    # Synthetic UAT Cutover Smoke Test Pack

    Target database: `RhemaERP_UAT_DryRun`

    Target tenant: `{tenantName}`

    Tenant ID: `{tenantGuid}`

    Cutover date: `{date}`

    Fiscal period: `{periodName}`

    Book classification: `IFRS`

    This pack is synthetic/demo data for technical end-to-end validation only. It must not be used as accountant approval, production migration evidence, or production sign-off evidence.

    `ALL_ACTIVE_BOOKS` is not used and must remain rejected.

    ## Scope

    - GL-only balanced opening trial balance.
    - No AP source-level openings.
    - No AR source-level openings.
    - No fixed asset source-level openings.
    - No bank/cash source-level openings beyond posted GL smoke lines.

    ## FIN-LIM-0048

    `FIN-LIM-0048` is marked not applicable only for this synthetic GL-only smoke test. It remains globally open until real tenant cutover data declares whether source-level AP/AR/fixed-asset openings are required.
    """;
    File.WriteAllText(Path.Combine(outputDir, "synthetic-cutover-input-checklist.md"), checklist);

    void WriteDeclaration(string fileName, IEnumerable<string> items)
    {
        var rows = new List<string>
        {
            "TenantName,TenantId,ApprovedCutoverDate,ApprovedFiscalPeriod,BookClassification,CutoverItem,YesNo,DataFilePath,RecordCount,FunctionalAmount,Currency,RequiresPostedSourceImport,Owner,ApprovalStatus,Notes"
        };
        rows.AddRange(items.Select(item => CsvRow(
            tenantName,
            tenantGuid,
            date,
            periodName,
            "IFRS",
            item,
            "No",
            "",
            "0",
            "0.00",
            "GHS",
            "No",
            "Codex synthetic runner",
            "Synthetic only",
            "Not in scope for first GL-only smoke test.")));
        WriteCsv(fileName, rows);
    }

    static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}

static string BuildSummary(
    object validationResult,
    FinalMigrationSignOffRunDto result,
    IReadOnlyList<StaleFixedAssetResetInfo> staleFixedAssetReset)
{
    var sb = new StringBuilder();
    sb.AppendLine("# Synthetic UAT Cutover Smoke Test Result");
    sb.AppendLine();
    sb.AppendLine("This is technical smoke-test evidence only. It is not accountant approval or production sign-off evidence.");
    sb.AppendLine();
    sb.AppendLine($"- Status: `{result.Status}`");
    sb.AppendLine($"- Run reference: `{result.RunReference}`");
    sb.AppendLine($"- Blocking findings: `{result.BlockingFindingsCount}`");
    sb.AppendLine($"- Warnings: `{result.WarningCount}`");
    sb.AppendLine($"- Checks: `{result.CheckCount}`");
    sb.AppendLine($"- Accepted limitations: `{result.AcceptedLimitationsCount}`");
    sb.AppendLine($"- Not applicable limitations: `{result.NotApplicableLimitationsCount}`");
    sb.AppendLine();
    sb.AppendLine("## UAT Demo Data Reset");
    if (staleFixedAssetReset.Count == 0)
    {
        sb.AppendLine("- No matching stale fixed asset demo rows required reset in this run.");
    }
    else
    {
        foreach (var asset in staleFixedAssetReset)
        {
            sb.AppendLine($"- `{asset.AssetCode}` / `{asset.AssetId}`: {asset.Reason}");
        }
    }
    sb.AppendLine();
    sb.AppendLine("## Checks");
    foreach (var check in result.Checks)
    {
        sb.AppendLine($"- `{check.Status}` / `{check.Severity}` / `{check.Code}`: {check.Message}");
    }
    sb.AppendLine();
    sb.AppendLine("## Evidence Manifest");
    foreach (var evidence in result.EvidenceExports)
    {
        sb.AppendLine($"- `{evidence.Status}` / `{evidence.ReportType}` / `{evidence.SourceOfTruthMode}`: {evidence.Message}");
    }
    return sb.ToString();
}

void WriteCsv(string fileName, IEnumerable<string> rows)
    => File.WriteAllLines(Path.Combine(outputDir, fileName), rows, new UTF8Encoding(false));

void WriteBackReferenceCsv(string fileName, IReadOnlyList<PostingBackReferenceRepairItemDto> items)
{
    var rows = new List<string>
    {
        "SourceModule,SourceDocumentType,SourceDocumentId,SourceReference,CurrentJournalEntryId,CurrentPostingEventId,CandidateJournalEntryId,CandidatePostingEventId,CandidatePostingEventCount,SupportsPostingEventBackReference,CanRepair,WasRepaired,Status,Severity,Message"
    };
    rows.AddRange(items.Select(item => CsvRow(
        item.SourceModule,
        item.SourceDocumentType,
        item.SourceDocumentId,
        item.SourceReference,
        item.CurrentJournalEntryId,
        item.CurrentPostingEventId,
        item.CandidateJournalEntryId,
        item.CandidatePostingEventId,
        item.CandidatePostingEventCount,
        item.SupportsPostingEventBackReference,
        item.CanRepair,
        item.WasRepaired,
        item.Status,
        item.Severity,
        item.Message)));
    WriteCsv(fileName, rows);
}

void WriteStaleFixedAssetResetCsv(string fileName, IReadOnlyList<StaleFixedAssetResetInfo> items)
{
    var rows = new List<string>
    {
        "AssetId,AssetCode,AssetName,PostedEventCount,PostedJournalCount,Reason"
    };
    rows.AddRange(items.Select(item => CsvRow(
        item.AssetId,
        item.AssetCode,
        item.AssetName,
        item.PostedEventCount,
        item.PostedJournalCount,
        item.Reason)));
    WriteCsv(fileName, rows);
}

static async Task<IReadOnlyList<SchemaColumnInfo>> BuildArSchemaReportAsync(ApplicationDbContext db)
{
    var tableNames = new[] { "Invoices", "InvoiceLineItem", "CustomerPayment", "PaymentAllocation", "ContractorInvoice" };
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != ConnectionState.Open;
    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
SELECT
    t.name AS TableName,
    c.name AS ColumnName,
    ty.name AS TypeName,
    c.is_nullable AS IsNullable
FROM sys.tables t
LEFT JOIN sys.columns c
    ON c.object_id = t.object_id
LEFT JOIN sys.types ty
    ON ty.user_type_id = c.user_type_id
WHERE t.name IN (N'Invoices', N'InvoiceLineItem', N'CustomerPayment', N'PaymentAllocation', N'ContractorInvoice')
ORDER BY t.name, c.column_id
""";
        var rows = new List<SchemaColumnInfo>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new SchemaColumnInfo(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                !reader.IsDBNull(3) && reader.GetBoolean(3)));
        }

        var present = rows.Select(r => r.TableName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        rows.AddRange(tableNames
            .Where(table => !present.Contains(table))
            .Select(table => new SchemaColumnInfo(table, null, null, true)));
        return rows;
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task<IReadOnlyList<StaleFixedAssetResetInfo>> ResetStaleDemoFixedAssetsAsync(ApplicationDbContext db, Guid tenantId)
{
    var staleDemoAssetCodes = new[]
    {
        "FA-2024-BLDG-001",
        "FA-2024-EQP-001",
        "FA-2024-EQP-002",
        "FA-2024-VEH-001"
    };
    var assets = await db.FixedAssets
        .Where(a =>
            a.TenantId == tenantId &&
            !a.IsDeleted &&
            staleDemoAssetCodes.Contains(a.AssetCode) &&
            !a.CapitalizationDate.HasValue &&
            !a.JournalEntryId.HasValue &&
            !a.PostingEventId.HasValue &&
            string.IsNullOrEmpty(a.SourceDocumentType) &&
            !a.SourceDocumentId.HasValue)
        .OrderBy(a => a.AssetCode)
        .ToListAsync();

    var reset = new List<StaleFixedAssetResetInfo>();
    foreach (var asset in assets)
    {
        var postedEventCount = await db.FinancePostingEvents
            .AsNoTracking()
            .CountAsync(e =>
                e.TenantId == tenantId &&
                e.SourceDocumentType == "FixedAsset" &&
                e.SourceDocumentId == asset.Id &&
                e.PostingStatus == "Posted" &&
                !e.IsDeleted);
        var postedJournalCount = await db.JournalEntries
            .AsNoTracking()
            .CountAsync(j =>
                j.TenantId == tenantId &&
                j.SourceDocumentType == "FixedAsset" &&
                j.SourceDocumentId == asset.Id &&
                j.PostingStatus == "Posted" &&
                !j.IsDeleted);

        if (postedEventCount != 0 || postedJournalCount != 0)
        {
            continue;
        }

        reset.Add(new StaleFixedAssetResetInfo(
            asset.Id,
            asset.AssetCode,
            asset.Name,
            postedEventCount,
            postedJournalCount,
            "Soft-deleted in RhemaERP_UAT_DryRun only for synthetic GL-only smoke scope. Row had no source, capitalization, journal, or posting-event evidence; posted GL was not mutated."));

        asset.IsDeleted = true;
        asset.DeletedAt = DateTime.UtcNow;
        asset.DeletedBy = "synthetic-uat-smoke";
    }

    if (reset.Count > 0)
    {
        await db.SaveChangesAsync();
    }

    return reset;
}

static string CsvRow(params object?[] values)
    => string.Join(",", values.Select(Csv));

static string Csv(object? value)
{
    var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    return text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r')
        ? "\"" + text.Replace("\"", "\"\"") + "\""
        : text;
}

sealed class SyntheticCurrentUserService : ICurrentUserService
{
    private readonly Guid _tenantId;
    private readonly Guid _userId;

    public SyntheticCurrentUserService(Guid tenantId, Guid userId)
    {
        _tenantId = tenantId;
        _userId = userId;
    }

    public string? UserId => _userId.ToString();
    public string? UserName => "synthetic-uat-smoke";
    public string? Email => "synthetic-uat-smoke@example.local";
    public Guid? TenantId => _tenantId;
    public Guid? EmployeeId => null;
    public bool IsAuthenticated => true;
    public IEnumerable<string> Roles => new[] { "FinanceAdmin" };
    public IDictionary<string, string> Claims => new Dictionary<string, string>();
    public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    public string? IpAddress => "127.0.0.1";
    public string? UserAgent => "SyntheticCutoverSmokeRunner";
}

sealed record SchemaColumnInfo(string TableName, string? ColumnName, string? TypeName, bool IsNullable);
sealed record StaleFixedAssetResetInfo(Guid AssetId, string AssetCode, string AssetName, int PostedEventCount, int PostedJournalCount, string Reason);
