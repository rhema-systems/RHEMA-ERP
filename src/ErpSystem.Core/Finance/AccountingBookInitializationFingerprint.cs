using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Finance;

/// <summary>
/// Canonical serialization for immutable accounting-book initialization evidence.
/// Preparation, provisioning and later revalidation must all use this authority;
/// duplicating the string shape would make a valid approval appear stale.
/// </summary>
public static class AccountingBookInitializationFingerprint
{
    public static string Evidence(
        Guid tenantId,
        Guid bookId,
        string bookCode,
        AccountingBookType bookType,
        string? functionalCurrencyCode,
        AccountingBookInitializationMode mode,
        DateTime cutoffDate,
        Guid cutoffPeriodId,
        string cutoffPeriodCode,
        DateTime cutoffPeriodStart,
        DateTime cutoffPeriodEnd,
        Guid? sourceBookId,
        string? sourceBookCode,
        AccountingBookType? sourceBookType,
        string? sourceFunctionalCurrencyCode,
        string idempotencyKey,
        string reason,
        string lineEvidence)
        => Hash($"BOOK-INITIALIZATION-EVIDENCE-V3|{tenantId:N}|{bookId:N}|{bookCode}|{StructuralType(bookType)}|{functionalCurrencyCode}|{mode}|{cutoffDate:yyyy-MM-dd}|{cutoffPeriodId:N}|{cutoffPeriodCode}|{cutoffPeriodStart:O}|{cutoffPeriodEnd:O}|{Format(sourceBookId)}|{sourceBookCode}|{StructuralType(sourceBookType)}|{sourceFunctionalCurrencyCode}|{idempotencyKey}|{reason}|{lineEvidence}");

    /// <summary>
    /// Reproduces the V2 fingerprint retained by initialization packs approved before
    /// primary-book replacement existed. V2 encoded PrimaryFull versus ParallelFull,
    /// even though that distinction is a governed designation rather than a change to
    /// the book's opening financial evidence.
    /// </summary>
    public static string LegacyEvidenceV2(
        Guid tenantId,
        Guid bookId,
        string bookCode,
        AccountingBookType bookType,
        string? functionalCurrencyCode,
        AccountingBookInitializationMode mode,
        DateTime cutoffDate,
        Guid cutoffPeriodId,
        string cutoffPeriodCode,
        DateTime cutoffPeriodStart,
        DateTime cutoffPeriodEnd,
        Guid? sourceBookId,
        string? sourceBookCode,
        AccountingBookType? sourceBookType,
        string? sourceFunctionalCurrencyCode,
        string idempotencyKey,
        string reason,
        string lineEvidence)
        => Hash($"BOOK-INITIALIZATION-EVIDENCE-V2|{tenantId:N}|{bookId:N}|{bookCode}|{bookType}|{functionalCurrencyCode}|{mode}|{cutoffDate:yyyy-MM-dd}|{cutoffPeriodId:N}|{cutoffPeriodCode}|{cutoffPeriodStart:O}|{cutoffPeriodEnd:O}|{Format(sourceBookId)}|{sourceBookCode}|{sourceBookType}|{sourceFunctionalCurrencyCode}|{idempotencyKey}|{reason}|{lineEvidence}");

    public static string Reconciliation(
        string evidenceFingerprint,
        string accountAuthorityEvidence,
        string balanceEvidence,
        string transactionEvidence,
        decimal totalDebits,
        decimal totalCredits)
        => Hash($"BOOK-INITIALIZATION-RECONCILIATION-V1|{evidenceFingerprint}|{accountAuthorityEvidence}|{balanceEvidence}|{transactionEvidence}|{Decimal(totalDebits)}|{Decimal(totalCredits)}");

    public static string Decimal(decimal value) =>
        value.ToString("0.############################", CultureInfo.InvariantCulture);

    private static string Format(Guid? value) => value.HasValue ? value.Value.ToString("N") : string.Empty;
    private static string StructuralType(AccountingBookType? value) => value switch
    {
        AccountingBookType.PrimaryFull or AccountingBookType.ParallelFull => "Full",
        AccountingBookType.Delta => "Delta",
        null => string.Empty,
        _ => value.Value.ToString()
    };
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
