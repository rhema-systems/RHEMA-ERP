using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Procurement;

public static class ProcurementReceiptDocumentRules
{
    public static IReadOnlyList<ProcurementReceiptDocumentKind> RequiredKinds(
        ProcurementReceiptDocumentType documentType) => documentType switch
    {
        ProcurementReceiptDocumentType.Grn => [ProcurementReceiptDocumentKind.Grn],
        ProcurementReceiptDocumentType.Mrn => [ProcurementReceiptDocumentKind.Mrn],
        ProcurementReceiptDocumentType.GrnAndMrn =>
            [ProcurementReceiptDocumentKind.Grn, ProcurementReceiptDocumentKind.Mrn],
        _ => throw new ArgumentOutOfRangeException(nameof(documentType), documentType,
            "DEC-013 has an unsupported receipt-document type.")
    };

    public static IReadOnlyList<string> MissingRequirements(
        IEnumerable<string> required,
        IEnumerable<string> completed) => required
        .Select(value => value.Trim())
        .Where(value => value.Length > 0)
        .Where(value => !completed.Any(done =>
            string.Equals(done.Trim(), value, StringComparison.OrdinalIgnoreCase)))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    public static bool IsInspectionApproved(ProcurementReceiptInspectionStatus? status) =>
        status is ProcurementReceiptInspectionStatus.Approved or ProcurementReceiptInspectionStatus.Closed;

    public static bool CanTransition(
        ProcurementReceiptDocumentStatus from,
        ProcurementReceiptDocumentStatus to) => from == to || (from, to) switch
    {
        (ProcurementReceiptDocumentStatus.Draft, ProcurementReceiptDocumentStatus.PendingSignatures) => true,
        (ProcurementReceiptDocumentStatus.Draft, ProcurementReceiptDocumentStatus.Issued) => true,
        (ProcurementReceiptDocumentStatus.PendingSignatures, ProcurementReceiptDocumentStatus.Issued) => true,
        (ProcurementReceiptDocumentStatus.Issued, ProcurementReceiptDocumentStatus.Cancelled) => true,
        _ => false
    };

    public static bool RequiresPriorGrnIssue(
        ProcurementReceiptDocumentKind kind,
        ProcurementReceiptCoexistenceRule coexistenceRule) =>
        kind == ProcurementReceiptDocumentKind.Mrn &&
        coexistenceRule == ProcurementReceiptCoexistenceRule.SequentialDocuments;
}
