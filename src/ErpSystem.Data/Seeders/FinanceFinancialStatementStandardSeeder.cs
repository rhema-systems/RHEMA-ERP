using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>Idempotently provides clone-only standard layouts for every canonical posting book.</summary>
public sealed class FinanceFinancialStatementStandardSeeder
{
    private static readonly string[] CanonicalBookCodes = ["IFRS", "LOCAL_STATUTORY", "MANAGEMENT"];
    private readonly ApplicationDbContext _db;
    private readonly ILogger _logger;

    public FinanceFinancialStatementStandardSeeder(ApplicationDbContext db, ILogger logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(Guid tenantId, DateTime now, CancellationToken cancellationToken = default)
    {
        var books = await _db.AccountingBooks.Where(item => item.TenantId == tenantId && !item.IsDeleted
            && item.IsActive && item.AllowsPosting && CanonicalBookCodes.Contains(item.Code))
            .OrderBy(item => item.SortOrder).ToListAsync(cancellationToken);
        foreach (var book in books)
        {
            var classifications = await _db.AccountClassifications.Where(item => item.TenantId == tenantId
                && item.AccountingBookId == book.Id && !item.IsDeleted && item.Status == AccountClassificationStatus.Active)
                .ToDictionaryAsync(item => item.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
            await EnsureStandardAsync(tenantId, book, classifications, FinancialStatementType.BalanceSheet,
                "STANDARD_BALANCE_SHEET", "Standard Balance Sheet", new[] { "ASSET_ROOT", "LIABILITY_ROOT", "EQUITY_ROOT" }, now, cancellationToken);
            await EnsureStandardAsync(tenantId, book, classifications, FinancialStatementType.IncomeStatement,
                "STANDARD_INCOME_STATEMENT", "Standard Income Statement", new[] { "REVENUE_ROOT", "EXPENSE_ROOT" }, now, cancellationToken);
        }
    }

    private async Task EnsureStandardAsync(Guid tenantId, AccountingBook book,
        IReadOnlyDictionary<string, AccountClassification> classifications, FinancialStatementType statementType,
        string codeSuffix, string name, IReadOnlyList<string> rootCodes, DateTime now, CancellationToken cancellationToken)
    {
        var code = $"STD_{book.Code}_{codeSuffix}";
        if (await _db.FinancialStatementLayouts.AnyAsync(item => item.TenantId == tenantId && item.Code == code && !item.IsDeleted, cancellationToken)) return;
        if (rootCodes.Any(root => !classifications.ContainsKey(root)))
        {
            _logger.LogWarning("Skipped protected layout {Code}; one or more root classifications are unavailable.", code);
            return;
        }
        var layout = new FinancialStatementLayout
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = code, Name = name,
            Description = $"Protected clone-only standard for {book.Name}.", StatementType = statementType,
            AccountingBookId = book.Id, IsActive = true, IsDefault = false, IsProtectedStandard = true,
            Revision = 1, CreatedAt = now, CreatedBy = "FIN-LAYOUT-STANDARD-1.0"
        };
        var version = new FinancialStatementLayoutVersion
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FinancialStatementLayoutId = layout.Id,
            VersionNumber = 1, Status = FinancialStatementLayoutVersionStatus.Draft,
            Notes = "Protected standard definition; clone to create a tenant-editable draft.",
            Revision = 1, CreatedAt = now, CreatedBy = "FIN-LAYOUT-STANDARD-1.0"
        };
        layout.Versions.Add(version);
        var order = 10;
        foreach (var rootCode in rootCodes)
        {
            var classification = classifications[rootCode];
            var row = new FinancialStatementRow
            {
                Id = Guid.NewGuid(), TenantId = tenantId, FinancialStatementLayoutVersionId = version.Id,
                RowCode = rootCode, Label = classification.Name, RowType = FinancialStatementRowType.Account,
                DisplayOrder = order, SignMultiplier = 1,
                IsVisible = true, IsBold = true, CreatedAt = now, CreatedBy = "FIN-LAYOUT-STANDARD-1.0"
            };
            row.Mappings.Add(new FinancialStatementRowMapping
            {
                Id = Guid.NewGuid(), TenantId = tenantId, FinancialStatementRowId = row.Id,
                MappingType = FinancialStatementRowMappingType.Classification,
                AccountClassificationId = classification.Id, IncludeClassificationDescendants = true,
                CreatedAt = now, CreatedBy = "FIN-LAYOUT-STANDARD-1.0"
            });
            version.Rows.Add(row);
            order += 10;
        }
        _db.FinancialStatementLayouts.Add(layout);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
