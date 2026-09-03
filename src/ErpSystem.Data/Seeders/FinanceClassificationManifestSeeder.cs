using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Versioned Finance-owned classification manifest. Stable tenant, book, classification and
/// account codes are the natural keys; generated database identifiers never cross environments.
/// </summary>
public sealed class FinanceClassificationManifestSeeder
{
    public const string ManifestVersion = "FIN-CLASSIFICATION-3.0";
    private static readonly string[] BookCodes = ["IFRS", "LOCAL_STATUTORY", "MANAGEMENT"];

    private sealed record Definition(string Code, string Name, AccountType Type, string? ParentCode = null,
        RevaluationTreatment Treatment = RevaluationTreatment.Exclude,
        AccountClassificationSystemRole? Role = null,
        bool IsPosting = true);

    private static readonly Definition[] Definitions =
    [
        new("ASSETS", "Assets", AccountType.Asset, IsPosting: false),
        new("ASSET_OTHER", "Other Assets", AccountType.Asset, "ASSETS"),
        new("CASH", "Cash", AccountType.Asset, "ASSETS", RevaluationTreatment.Include, AccountClassificationSystemRole.Cash),
        new("BANK", "Bank", AccountType.Asset, "ASSETS", RevaluationTreatment.Include, AccountClassificationSystemRole.Bank),
        new("RECEIVABLE_CONTROL", "Receivables", AccountType.Asset, "ASSETS", RevaluationTreatment.Include, AccountClassificationSystemRole.ReceivableControl),
        new("INVENTORY_CONTROL", "Inventory", AccountType.Asset, "ASSETS", Role: AccountClassificationSystemRole.InventoryControl),
        new("FIXED_ASSET_COST", "Fixed Asset Cost", AccountType.Asset, "ASSETS", Role: AccountClassificationSystemRole.FixedAssetCost),
        new("ACCUMULATED_DEPRECIATION", "Accumulated Depreciation", AccountType.Asset, "ASSETS", Role: AccountClassificationSystemRole.AccumulatedDepreciation),
        new("ASSET_UNDER_CONSTRUCTION", "Asset Under Construction", AccountType.Asset, "ASSETS", Role: AccountClassificationSystemRole.AssetUnderConstruction),
        new("WHT_RECEIVABLE", "Withholding Tax Receivable", AccountType.Asset, "ASSETS", Role: AccountClassificationSystemRole.WhtReceivable),
        new("INPUT_TAX", "Input Tax", AccountType.Asset, "ASSETS", Role: AccountClassificationSystemRole.InputTax),
        new("LIABILITIES", "Liabilities", AccountType.Liability, IsPosting: false),
        new("LIABILITY_OTHER", "Other Liabilities", AccountType.Liability, "LIABILITIES"),
        new("PAYABLE_CONTROL", "Payables", AccountType.Liability, "LIABILITIES", RevaluationTreatment.Include, AccountClassificationSystemRole.PayableControl),
        new("ACCRUED_LIABILITY", "Accrued Liabilities", AccountType.Liability, "LIABILITIES", RevaluationTreatment.Include),
        new("DEBT", "Borrowings and Debt", AccountType.Liability, "LIABILITIES", RevaluationTreatment.Include),
        new("OUTPUT_TAX", "Output Tax", AccountType.Liability, "LIABILITIES", Role: AccountClassificationSystemRole.OutputTax),
        new("WHT_PAYABLE", "Withholding Tax Payable", AccountType.Liability, "LIABILITIES", Role: AccountClassificationSystemRole.WhtPayable),
        new("EQUITY_ROOT", "Equity", AccountType.Equity, IsPosting: false),
        new("EQUITY", "Equity Accounts", AccountType.Equity, "EQUITY_ROOT"),
        new("REVENUE_ROOT", "Revenue", AccountType.Revenue, IsPosting: false),
        new("REVENUE", "Operating Revenue", AccountType.Revenue, "REVENUE_ROOT"),
        new("REVENUE_DEDUCTIONS", "Revenue Deductions", AccountType.Revenue, "REVENUE_ROOT"),
        new("OTHER_INCOME", "Other Income", AccountType.Revenue, "REVENUE_ROOT"),
        new("EXPENSE_ROOT", "Expenses", AccountType.Expense, IsPosting: false),
        new("EXPENSE", "Operating Expenses", AccountType.Expense, "EXPENSE_ROOT"),
        new("COST_OF_SALES", "Cost of Sales", AccountType.Expense, "EXPENSE_ROOT"),
        new("OTHER_EXPENSE", "Other Expenses", AccountType.Expense, "EXPENSE_ROOT"),
        new("TAX_EXPENSE", "Income Tax Expense", AccountType.Expense, "EXPENSE_ROOT")
    ];

    private readonly ApplicationDbContext _db;
    private readonly ILogger _logger;

    public FinanceClassificationManifestSeeder(ApplicationDbContext db, ILogger logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(Guid tenantId, DateTime seedDate, CancellationToken cancellationToken = default)
    {
        var books = await _db.AccountingBooks.Where(item => item.TenantId == tenantId && !item.IsDeleted).ToListAsync(cancellationToken);
        EnsureUnique(books.Select(item => item.Code), "accounting-book");
        foreach (var code in BookCodes)
        {
            if (books.All(item => !string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase)))
            {
                var book = new AccountingBook
                {
                    TenantId = tenantId, Code = code,
                    Name = code switch { "IFRS" => "IFRS Primary", "LOCAL_STATUTORY" => "Local Statutory", _ => "Management Reporting" },
                    Purpose = code == "IFRS" ? "Primary" : code == "LOCAL_STATUTORY" ? "Statutory" : "Management",
                    IsActive = true, IsDefault = code == "IFRS", AllowsPosting = true, IsSystemDefined = true,
                    SortOrder = Array.IndexOf(BookCodes, code) * 10 + 10, CreatedAt = seedDate,
                    CreatedBy = $"System ({ManifestVersion})"
                };
                _db.AccountingBooks.Add(book);
                books.Add(book);
            }
        }
        await _db.SaveChangesAsync(cancellationToken);

        var classifications = await _db.AccountClassifications
            .Where(item => item.TenantId == tenantId && !item.IsDeleted).ToListAsync(cancellationToken);
        EnsureUnique(classifications.Select(item => $"{item.AccountingBookId:N}|{item.Code}"), "classification");
        foreach (var book in books.Where(item => BookCodes.Contains(item.Code, StringComparer.OrdinalIgnoreCase)))
        {
            foreach (var definition in Definitions)
            {
                var classification = classifications.SingleOrDefault(item => item.AccountingBookId == book.Id && item.Code == definition.Code);
                if (classification == null)
                {
                    classification = new AccountClassification
                    {
                        TenantId = tenantId, AccountingBookId = book.Id, Code = definition.Code,
                        Name = definition.Name, CoreAccountType = definition.Type,
                        DefaultRevaluationTreatment = definition.Treatment, SystemRole = definition.Role,
                        IsPostingClassification = definition.IsPosting, Status = AccountClassificationStatus.Active,
                        DisplayOrder = Array.IndexOf(Definitions, definition) * 10 + 10,
                        CreatedAt = seedDate, CreatedBy = $"System ({ManifestVersion})"
                    };
                    _db.AccountClassifications.Add(classification);
                    classifications.Add(classification);
                }
                else if (classification.CoreAccountType != definition.Type)
                {
                    throw new InvalidOperationException($"Classification {book.Code}/{definition.Code} has an incompatible core account type.");
                }
                else if (classification.CreatedBy?.Contains("FIN-CLASSIFICATION-", StringComparison.Ordinal) == true
                    && classification.UpdatedBy == null)
                {
                    // Phase 4 owns the reviewed monetary defaults. Upgrade only untouched
                    // system rows; an administrator decision is authoritative even when it
                    // differs from this manifest.
                    classification.DefaultRevaluationTreatment = definition.Treatment;
                }
            }
            await _db.SaveChangesAsync(cancellationToken);
            foreach (var definition in Definitions.Where(item => item.ParentCode != null))
            {
                var classification = classifications.Single(item => item.AccountingBookId == book.Id && item.Code == definition.Code);
                var parent = classifications.Single(item => item.AccountingBookId == book.Id && item.Code == definition.ParentCode);
                if (classification.ParentClassificationId == null
                    && classification.CreatedBy?.Contains("FIN-CLASSIFICATION-", StringComparison.Ordinal) == true
                    && classification.UpdatedBy == null)
                    classification.ParentClassificationId = parent.Id;
            }
        }
        await _db.SaveChangesAsync(cancellationToken);

        var accounts = await _db.Accounts.Where(item => item.TenantId == tenantId && !item.IsDeleted).ToListAsync(cancellationToken);
        EnsureUnique(accounts.Select(item => item.AccountCode), "GL account");
        var existingMappings = await _db.AccountAccountingBooks
            .Where(item => item.TenantId == tenantId && !item.IsDeleted).ToListAsync(cancellationToken);
        foreach (var account in accounts)
        {
            var classificationCode = ResolveReviewedClassificationCode(account.AccountCode, account.AccountType);
            if (classificationCode == null) continue;
            foreach (var book in books.Where(item => BookCodes.Contains(item.Code, StringComparer.OrdinalIgnoreCase)))
            {
                var classification = classifications.Single(item => item.AccountingBookId == book.Id && item.Code == classificationCode);
                if (classification.CoreAccountType != account.AccountType)
                    throw new InvalidOperationException($"Manifest account {account.AccountCode} does not match {book.Code}/{classificationCode}.");
                var mapping = existingMappings.SingleOrDefault(item => item.AccountId == account.Id && item.AccountingBookId == book.Id);
                if (mapping == null)
                {
                    mapping = new AccountAccountingBook
                    {
                        TenantId = tenantId, AccountId = account.Id, AccountingBookId = book.Id,
                        AccountClassificationId = classification.Id, IsEnabled = true,
                        CreatedAt = seedDate, CreatedBy = $"System ({ManifestVersion})"
                    };
                    _db.AccountAccountingBooks.Add(mapping);
                    existingMappings.Add(mapping);
                }
                else if (ShouldApplyManifestClassification(mapping, classifications, classificationCode))
                {
                    mapping.AccountClassificationId = classification.Id;
                }
            }
        }
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Applied Finance classification manifest {ManifestVersion} for tenant {TenantId}.", ManifestVersion, tenantId);
    }

    public static string? ResolveReviewedClassificationCode(string accountCode, AccountType accountType)
    {
        var naturalCode = accountCode.Split('-', StringSplitOptions.RemoveEmptyEntries) is var parts && parts.Length == 3 ? parts[1] : accountCode;
        if (naturalCode is "1000" or "1001" or "1002" or "1003" or "1010" or "1020" or "1021" or "1022" or "1023")
            return naturalCode is "1001" or "1002" ? "BANK" : "CASH";
        if (naturalCode is "1100" or "1120") return "RECEIVABLE_CONTROL";
        if (naturalCode == "1130") return "WHT_RECEIVABLE";
        if (naturalCode == "1200") return "INVENTORY_CONTROL";
        if (naturalCode is "1500" or "1510" or "1520" or "1530") return naturalCode == "1500" ? "ASSET_UNDER_CONSTRUCTION" : "FIXED_ASSET_COST";
        if (naturalCode == "1590") return "ACCUMULATED_DEPRECIATION";
        if (naturalCode is "2000" or "2120") return "PAYABLE_CONTROL";
        if (naturalCode is "2100" or "2110") return "ACCRUED_LIABILITY";
        if (naturalCode == "2200") return "OUTPUT_TAX";
        if (naturalCode == "2500") return "DEBT";
        return accountType switch
        {
            AccountType.Asset when naturalCode is "1990" or "9999" => "ASSET_OTHER",
            AccountType.Equity when naturalCode is "3000" or "3100" => "EQUITY",
            AccountType.Revenue when naturalCode is "4000" or "4100" or "4110" => "REVENUE",
            AccountType.Revenue when naturalCode == "4210" => "REVENUE_DEDUCTIONS",
            AccountType.Revenue when naturalCode is "4900" or "4910" or "4920" or "7100" or "7200" => "OTHER_INCOME",
            AccountType.Expense when naturalCode == "5000" => "COST_OF_SALES",
            AccountType.Expense when naturalCode is "7110" or "7210" => "OTHER_EXPENSE",
            AccountType.Expense when naturalCode is "6000" or "6020" or "6100" or "6200" or "6300" or "6400" or "6500" or "6600" => "EXPENSE",
            _ => null
        };
    }

    private static bool ShouldApplyManifestClassification(
        AccountAccountingBook mapping,
        IReadOnlyCollection<AccountClassification> classifications,
        string desiredCode)
    {
        if (!mapping.IsEnabled || mapping.UpdatedBy != null) return false;
        if (mapping.AccountClassificationId == null) return true;

        var current = classifications.SingleOrDefault(item => item.Id == mapping.AccountClassificationId);
        if (current == null || current.CreatedBy?.Contains("FIN-CLASSIFICATION-1.0", StringComparison.Ordinal) != true)
            return false;

        return (current.Code == "REVENUE" && desiredCode is "REVENUE_DEDUCTIONS" or "OTHER_INCOME")
            || (current.Code == "EXPENSE" && desiredCode is "COST_OF_SALES" or "OTHER_EXPENSE" or "TAX_EXPENSE");
    }

    private static void EnsureUnique(IEnumerable<string> values, string kind)
    {
        var duplicate = values.GroupBy(value => value, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null) throw new InvalidOperationException($"Finance manifest found ambiguous {kind} code '{duplicate.Key}'.");
    }
}
