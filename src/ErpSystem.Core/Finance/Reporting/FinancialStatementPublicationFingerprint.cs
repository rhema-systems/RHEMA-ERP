using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Finance.Reporting;

public static class FinancialStatementPublicationFingerprint
{
    public const string SnapshotSchemaVersion = "1";

    public static string Hierarchy(Guid tenantId, Guid bookId, IEnumerable<AccountClassification> source)
    {
        var text = new StringBuilder().Append(tenantId.ToString("N")).Append('|').Append(bookId.ToString("N")).AppendLine();
        foreach (var item in source.OrderBy(item => item.Code, StringComparer.Ordinal).ThenBy(item => item.Id))
            text.Append(item.Id.ToString("N")).Append('|').Append(item.ParentClassificationId?.ToString("N") ?? "-").Append('|')
                .Append(item.Code).Append('|').Append(item.Name).Append('|').Append((int)item.CoreAccountType).Append('|')
                .Append((int)item.Status).Append('|').Append(item.IsPostingClassification ? '1' : '0').Append('|').Append(item.DisplayOrder).AppendLine();
        return Hash(text.ToString());
    }

    public static string Resolution(Guid tenantId, Guid versionId, Guid bookId, string bookCode,
        string hierarchyFingerprint, IEnumerable<FinancialStatementPublicationAccount> source)
    {
        var text = new StringBuilder().Append(SnapshotSchemaVersion).Append('|').Append(tenantId.ToString("N")).Append('|')
            .Append(versionId.ToString("N")).Append('|').Append(bookId.ToString("N")).Append('|')
            .Append(bookCode).Append('|').Append(hierarchyFingerprint).AppendLine();
        foreach (var item in source.OrderBy(item => item.RowCode, StringComparer.Ordinal)
                     .ThenBy(item => item.FinancialStatementRowMappingId).ThenBy(item => item.AccountId))
            text.Append(item.FinancialStatementRowId.ToString("N")).Append('|').Append(item.RowCode).Append('|')
                .Append(item.FinancialStatementRowMappingId.ToString("N")).Append('|').Append((int)item.MappingType).Append('|')
                .Append(item.AccountId.ToString("N")).Append('|').Append(item.AccountNumber).Append('|').Append(item.AccountName).Append('|')
                .Append((int)item.AccountType).Append('|').Append(item.AccountClassificationId?.ToString("N") ?? "-").Append('|')
                .Append(item.ClassificationCode ?? "-").Append('|').Append(item.ClassificationName ?? "-").Append('|')
                .Append(item.ClassificationPath ?? "-").Append('|').Append(item.MappingSelector ?? "-").AppendLine();
        return Hash(text.ToString());
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
