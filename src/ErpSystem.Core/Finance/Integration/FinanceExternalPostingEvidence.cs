using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Finance.Integration;

/// <summary>
/// Canonical integrity evidence shared by an external producer and the Finance adapter. The hash
/// binds the approved source identity, lifecycle evidence, economic lines, currency evidence and
/// submitted dimension codes without relying on JSON property or collection ordering.
/// </summary>
public static class FinanceExternalPostingEvidence
{
    public static string Compute(FinanceExternalPostingEnvelopeDto envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var canonical = new StringBuilder();
        // V1 deliberately retains its published canonical byte sequence so in-flight evidence
        // remains verifiable. V2 starts with an explicit, disjoint domain marker below.
        Append(canonical, ((int)envelope.ContractId).ToString(CultureInfo.InvariantCulture));
        Append(canonical, FinanceExternalProducerContractCatalog.GetRequired(envelope.ContractId).Definition.ContractVersion);
        Append(canonical, envelope.TenantId.ToString("D"));
        Append(canonical, envelope.SourceDocumentId.ToString("D"));
        Append(canonical, Normalize(envelope.SourceDocumentReference));
        Append(canonical, Normalize(envelope.Description));
        Append(canonical, Date(envelope.PostingDate));
        Append(canonical, Normalize(envelope.PostingAction));
        Append(canonical, Normalize(envelope.JournalType));
        Append(canonical, Normalize(envelope.BookClassification));
        Append(canonical, Normalize(envelope.FunctionalCurrencyCode).ToUpperInvariant());
        Append(canonical, Normalize(envelope.IdempotencyKey));
        Append(canonical, envelope.SourceApproved ? "1" : "0");
        Append(canonical, envelope.ApprovedByUserId.ToString("D"));
        Append(canonical, Date(envelope.ApprovedAtUtc));
        Append(canonical, Normalize(envelope.ApprovalReference));

        var sourceDimensions = envelope.FinanceDimensions;
        Append(canonical, sourceDimensions?.ApplyDefaultToEligibleLines == true ? "1" : "0");
        AppendDimensions(canonical, sourceDimensions?.DefaultDimensions);
        foreach (var line in sourceDimensions?.Lines?.OrderBy(item => item.SourceLineId).ThenBy(item => item.AccountId)
                     ?? Enumerable.Empty<FinanceSourceLineDimensionInputDto>())
        {
            Append(canonical, line.SourceLineId?.ToString("D") ?? string.Empty);
            Append(canonical, line.AccountId.ToString("D"));
            AppendDimensions(canonical, line.Dimensions);
        }

        foreach (var line in (envelope.Lines ?? Array.Empty<FinancePostingLineDto>())
                     .OrderBy(item => item.SourceDocumentLineId).ThenBy(item => item.AccountId))
        {
            Append(canonical, line.SourceDocumentLineId?.ToString("D") ?? string.Empty);
            Append(canonical, line.AccountId.ToString("D"));
            Append(canonical, Normalize(line.Description));
            Append(canonical, Number(line.DebitAmount));
            Append(canonical, Number(line.CreditAmount));
            Append(canonical, Normalize(line.TransactionCurrency).ToUpperInvariant());
            Append(canonical, Number(line.TransactionDebitAmount));
            Append(canonical, Number(line.TransactionCreditAmount));
            Append(canonical, Number(line.ForeignCurrencyAmount));
            Append(canonical, line.ExchangeRateId?.ToString("D") ?? string.Empty);
            Append(canonical, Number(line.ExchangeRate));
            Append(canonical, Normalize(line.ExchangeRateSource));
            Append(canonical, line.ExchangeRateDate.HasValue ? Date(line.ExchangeRateDate.Value) : string.Empty);
            Append(canonical, Normalize(line.SourceReferenceNumber));
            Append(canonical, line.LineNumber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            Append(canonical, Normalize(line.SegmentString));
            Append(canonical, Normalize(line.Notes));
            Append(canonical, Normalize(line.TransactionTag));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    /// <summary>Computes V2 evidence in a domain that cannot collide with a V1 payload.</summary>
    public static string Compute(FinanceExternalPostingEnvelopeV2Dto envelope) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonicalize(envelope)))).ToLowerInvariant();

    /// <summary>Returns the exact ordered V2 evidence string used for published golden vectors.</summary>
    public static string Canonicalize(FinanceExternalPostingEnvelopeV2Dto envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var canonical = new StringBuilder();
        Append(canonical, "RHEMA-FIN-EXTERNAL-POSTING|2.0");
        Append(canonical, ((int)envelope.ContractId).ToString(CultureInfo.InvariantCulture));
        Append(canonical, "2.0");
        Append(canonical, envelope.TenantId.ToString("D"));
        Append(canonical, envelope.SourceDocumentId.ToString("D"));
        Append(canonical, Normalize(envelope.SourceDocumentReference));
        Append(canonical, Normalize(envelope.Description));
        Append(canonical, Date(envelope.PostingDate));
        Append(canonical, Normalize(envelope.PostingAction));
        Append(canonical, Normalize(envelope.JournalType));
        Append(canonical, Normalize(envelope.AccountingBookCode).ToUpperInvariant());
        Append(canonical, Normalize(envelope.FunctionalCurrencyCode).ToUpperInvariant());
        Append(canonical, Normalize(envelope.IdempotencyKey));
        Append(canonical, envelope.SourceApproved ? "1" : "0");
        Append(canonical, envelope.ApprovedByUserId.ToString("D"));
        Append(canonical, Date(envelope.ApprovedAtUtc));
        Append(canonical, Normalize(envelope.ApprovalReference));
        AppendSourceDimensions(canonical, envelope.FinanceDimensions);
        AppendLines(canonical, envelope.Lines);
        return canonical.ToString();
    }

    private static void AppendSourceDimensions(StringBuilder canonical, FinanceSourceDocumentDimensionInputDto? sourceDimensions)
    {
        Append(canonical, sourceDimensions?.ApplyDefaultToEligibleLines == true ? "1" : "0");
        AppendDimensions(canonical, sourceDimensions?.DefaultDimensions);
        foreach (var line in sourceDimensions?.Lines?.OrderBy(item => item.SourceLineId).ThenBy(item => item.AccountId)
                     ?? Enumerable.Empty<FinanceSourceLineDimensionInputDto>())
        {
            Append(canonical, line.SourceLineId?.ToString("D") ?? string.Empty);
            Append(canonical, line.AccountId.ToString("D"));
            AppendDimensions(canonical, line.Dimensions);
        }
    }

    private static void AppendLines(StringBuilder canonical, IEnumerable<FinancePostingLineDto>? lines)
    {
        foreach (var line in (lines ?? Array.Empty<FinancePostingLineDto>()).OrderBy(item => item.SourceDocumentLineId).ThenBy(item => item.AccountId))
        {
            Append(canonical, line.SourceDocumentLineId?.ToString("D") ?? string.Empty);
            Append(canonical, line.AccountId.ToString("D"));
            Append(canonical, Normalize(line.Description));
            Append(canonical, Number(line.DebitAmount));
            Append(canonical, Number(line.CreditAmount));
            Append(canonical, Normalize(line.TransactionCurrency).ToUpperInvariant());
            Append(canonical, Number(line.TransactionDebitAmount));
            Append(canonical, Number(line.TransactionCreditAmount));
            Append(canonical, Number(line.ForeignCurrencyAmount));
            Append(canonical, line.ExchangeRateId?.ToString("D") ?? string.Empty);
            Append(canonical, Number(line.ExchangeRate));
            Append(canonical, Normalize(line.ExchangeRateSource));
            Append(canonical, line.ExchangeRateDate.HasValue ? Date(line.ExchangeRateDate.Value) : string.Empty);
            Append(canonical, Normalize(line.SourceReferenceNumber));
            Append(canonical, line.LineNumber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            Append(canonical, Normalize(line.SegmentString));
            Append(canonical, Normalize(line.Notes));
            Append(canonical, Normalize(line.TransactionTag));
        }
    }

    private static void AppendDimensions(
        StringBuilder canonical,
        IEnumerable<FinancePostingDimensionValueDto>? dimensions)
    {
        foreach (var dimension in dimensions?.OrderBy(item => item.DimensionCode, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.ValueCode, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.SourceEntityType, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.SourceEntityId)
                 ?? Enumerable.Empty<FinancePostingDimensionValueDto>())
        {
            Append(canonical, Normalize(dimension.DimensionCode).ToUpperInvariant());
            Append(canonical, Normalize(dimension.ValueCode).ToUpperInvariant());
            Append(canonical, Normalize(dimension.SourceEntityType).ToUpperInvariant());
            Append(canonical, dimension.SourceEntityId?.ToString("D") ?? string.Empty);
        }
    }

    private static void Append(StringBuilder target, string value) =>
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value).Append('|');

    private static string Normalize(string? value) => value?.Trim() ?? string.Empty;
    private static string Date(DateTime value) => value.ToString("O", CultureInfo.InvariantCulture);
    private static string Number(decimal? value) =>
        value?.ToString("G29", CultureInfo.InvariantCulture) ?? string.Empty;
}
