using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.DTOs.Finance;

public sealed class TaxReportRequestDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? TaxId { get; set; }
    public Guid? TaxGroupId { get; set; }
    public Guid? TaxAccountId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public BusinessPartnerRoleType? BusinessPartnerRole { get; set; }
    public string? SourceDocumentType { get; set; }
    public string? SourceDocumentNumber { get; set; }
    public string? CertificateStatus { get; set; }
}

public sealed class GhanaTaxSnapshotReportDto
{
    public string ReportType { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string SourceOfTruthMode { get; set; } = "Posted tax snapshots reconciled to posted GL";
    public List<GhanaTaxSnapshotLineDto> Lines { get; set; } = new();
    public List<TaxReportDiagnosticDto> Diagnostics { get; set; } = new();
    public TaxReportTotalsDto Totals { get; set; } = new();
}

public sealed class GhanaTaxSnapshotLineDto
{
    public Guid TaxCalculationId { get; set; }
    public string SourceModule { get; set; } = string.Empty;
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public string SourceDocumentNumber { get; set; } = string.Empty;
    public DateTime SourceDocumentDate { get; set; }
    public Guid? CounterpartyId { get; set; }
    public string? CounterpartyName { get; set; }
    public Guid TaxId { get; set; }
    public string TaxCode { get; set; } = string.Empty;
    public string TaxName { get; set; } = string.Empty;
    public TaxCategory TaxCategory { get; set; }
    public Guid? TaxGroupId { get; set; }
    public string? TaxGroupCode { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public bool IsRecoverableInputTax { get; set; }
    public Guid? TaxAccountId { get; set; }
    public string? TaxAccountNumber { get; set; }
    public string? TaxAccountName { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? PostingEventId { get; set; }
    public string FilingPeriod { get; set; } = string.Empty;
}

public sealed class GhanaTaxTreatmentReportDto
{
    public string ReportType { get; set; } = "Exempt/Zero-Rated/Out-of-Scope Supplies";
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string SourceOfTruthMode { get; set; } = "Posted source documents with explicit line tax treatment";
    public List<GhanaTaxTreatmentLineDto> Lines { get; set; } = new();
    public List<TaxReportDiagnosticDto> Diagnostics { get; set; } = new();
    public TaxReportTotalsDto Totals { get; set; } = new();
}

public sealed class GhanaTaxTreatmentLineDto
{
    public string SourceModule { get; set; } = string.Empty;
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public string SourceDocumentNumber { get; set; } = string.Empty;
    public DateTime SourceDocumentDate { get; set; }
    public Guid SourceLineId { get; set; }
    public string Description { get; set; } = string.Empty;
    public TaxTreatment TaxTreatment { get; set; }
    public decimal LineAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? PostingEventId { get; set; }
}

public sealed class GhanaTaxWithholdingReportDto
{
    public string ReportType { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string SourceOfTruthMode { get; set; } = "Posted payment/receipt withholding records reconciled to posted GL";
    public List<GhanaTaxWithholdingLineDto> Lines { get; set; } = new();
    public List<TaxReportDiagnosticDto> Diagnostics { get; set; } = new();
    public TaxReportTotalsDto Totals { get; set; } = new();
}

public sealed class GhanaTaxWithholdingLineDto
{
    public string WithholdingType { get; set; } = string.Empty;
    public string SourceModule { get; set; } = string.Empty;
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public string SourceDocumentNumber { get; set; } = string.Empty;
    public DateTime SourceDocumentDate { get; set; }
    public Guid? CounterpartyId { get; set; }
    public string? CounterpartyName { get; set; }
    public Guid? TaxId { get; set; }
    public string? TaxCode { get; set; }
    public string? TaxName { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxableBase { get; set; }
    public decimal WithholdingAmount { get; set; }
    public Guid? TaxAccountId { get; set; }
    public string? TaxAccountNumber { get; set; }
    public string? TaxAccountName { get; set; }
    public string? CertificateNumber { get; set; }
    public DateTime? CertificateDate { get; set; }
    public string CertificateStatus { get; set; } = "Missing";
    public Guid? JournalEntryId { get; set; }
    public Guid? PostingEventId { get; set; }
}

public sealed class GhanaTaxAccountReconciliationReportDto
{
    public string ReportType { get; set; } = "Tax Account Reconciliation";
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string SourceOfTruthMode { get; set; } = "Tax snapshots and withholding records reconciled to posted GL tax account movement";
    public List<GhanaTaxAccountReconciliationLineDto> Lines { get; set; } = new();
    public List<TaxReportDiagnosticDto> Diagnostics { get; set; } = new();
    public TaxReportTotalsDto Totals { get; set; } = new();
}

public sealed class GhanaTaxAccountReconciliationLineDto
{
    public string Area { get; set; } = string.Empty;
    public Guid? TaxAccountId { get; set; }
    public string? TaxAccountNumber { get; set; }
    public string? TaxAccountName { get; set; }
    public decimal SnapshotAmount { get; set; }
    public decimal PostedGlAmount { get; set; }
    public decimal Variance { get; set; }
    public int SnapshotLineCount { get; set; }
    public int PostedGlLineCount { get; set; }
    public int MissingPostingReferenceCount { get; set; }
}

public sealed class GhanaTaxConfigurationHistoryReportDto
{
    public string ReportType { get; set; } = "Tax Configuration and Rate History";
    public DateTime AsOfDate { get; set; }
    public string SourceOfTruthMode { get; set; } = "Tenant effective-dated tax configuration";
    public List<GhanaTaxConfigurationHistoryLineDto> Lines { get; set; } = new();
    public List<TaxReportDiagnosticDto> Diagnostics { get; set; } = new();
}

public sealed class GhanaTaxConfigurationHistoryLineDto
{
    public Guid TaxId { get; set; }
    public string TaxCode { get; set; } = string.Empty;
    public string TaxName { get; set; } = string.Empty;
    public TaxCategory TaxCategory { get; set; }
    public TaxApplicability Applicability { get; set; }
    public decimal Rate { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
    public bool IsCurrentActiveCovidLevy { get; set; }
    public bool HasOverlappingRateHistory { get; set; }
    public Guid? TaxPayableAccountId { get; set; }
    public Guid? TaxReceivableAccountId { get; set; }
}

public sealed class GhanaTaxCovidLevyDiagnosticReportDto
{
    public string ReportType { get; set; } = "COVID-19 Levy Current-Active Diagnostic";
    public DateTime AsOfDate { get; set; }
    public string SourceOfTruthMode { get; set; } = "Tenant effective-dated tax configuration";
    public List<TaxReportDiagnosticDto> Diagnostics { get; set; } = new();
}

public sealed class TaxReportTotalsDto
{
    public decimal TaxableBase { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal NhilAmount { get; set; }
    public decimal GetFundAmount { get; set; }
    public decimal TotalTaxAmount { get; set; }
    public decimal TotalWithholdingAmount { get; set; }
    public decimal PostedGlAmount { get; set; }
    public decimal Variance { get; set; }
}

public sealed class TaxReportDiagnosticDto
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;
    public Guid? SourceDocumentId { get; set; }
    public string? SourceDocumentType { get; set; }
}
