using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Finance.Reporting;

public static class FinancialStatementPublicationFingerprint
{
    // Version 2 fingerprints every frozen accounting-book field. Version 1
    // snapshots are deliberately not accepted because no Phase 3 migration has
    // been applied and accepting them would preserve only partially protected
    // publication evidence.
    public const string SnapshotSchemaVersion = "2";

    public static string Hierarchy(Guid tenantId, Guid bookId, IEnumerable<AccountClassification> source)
    {
        var text = new StringBuilder();
        AppendRecord(text, "FINANCIAL_STATEMENT_HIERARCHY", SnapshotSchemaVersion,
            tenantId.ToString("N"), bookId.ToString("N"));
        foreach (var item in source.OrderBy(item => item.Code, StringComparer.Ordinal).ThenBy(item => item.Id))
            AppendRecord(text, "CLASSIFICATION", item.Id.ToString("N"),
                item.ParentClassificationId?.ToString("N"), item.Code, item.Name,
                ((int)item.CoreAccountType).ToString(CultureInfo.InvariantCulture),
                ((int)item.Status).ToString(CultureInfo.InvariantCulture),
                item.IsPostingClassification ? "1" : "0",
                item.DisplayOrder.ToString(CultureInfo.InvariantCulture));
        return Hash(text.ToString());
    }

    public static string Resolution(Guid tenantId, Guid versionId, Guid bookId, string bookCode, string bookName,
        string hierarchyFingerprint, IEnumerable<FinancialStatementPublicationAccount> source)
    {
        var text = new StringBuilder();
        AppendRecord(text, "FINANCIAL_STATEMENT_RESOLUTION", SnapshotSchemaVersion,
            tenantId.ToString("N"), versionId.ToString("N"), bookId.ToString("N"),
            bookCode, bookName, hierarchyFingerprint);
        foreach (var item in source.OrderBy(item => item.RowCode, StringComparer.Ordinal)
                     .ThenBy(item => item.FinancialStatementRowMappingId).ThenBy(item => item.AccountId))
            AppendRecord(text, "PUBLICATION_ACCOUNT", item.FinancialStatementRowId.ToString("N"), item.RowCode,
                item.FinancialStatementRowMappingId.ToString("N"),
                ((int)item.MappingType).ToString(CultureInfo.InvariantCulture),
                item.AccountId.ToString("N"), item.AccountNumber, item.AccountName,
                ((int)item.AccountType).ToString(CultureInfo.InvariantCulture),
                item.AccountingBookId.ToString("N"), item.AccountingBookCode,
                item.AccountClassificationId?.ToString("N"), item.ClassificationCode, item.ClassificationName,
                item.ClassificationPath, item.MappingSelector);
        return Hash(text.ToString());
    }

    private static void AppendRecord(StringBuilder target, params string?[] fields)
    {
        foreach (var field in fields)
        {
            var value = field ?? string.Empty;
            target.Append(value.Length).Append(':').Append(value).Append('|');
        }
        target.AppendLine();
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
