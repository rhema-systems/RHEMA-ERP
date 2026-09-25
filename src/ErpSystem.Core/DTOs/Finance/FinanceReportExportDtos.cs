using System;
using System.Collections.Generic;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.DTOs.Finance;

public static class FinanceReportExportTypes
{
    public const string TrialBalance = "TrialBalance";
    public const string BalanceSheet = "BalanceSheet";
    public const string IncomeStatement = "IncomeStatement";
    public const string DetailedLedger = "DetailedLedger";
    public const string CashBankLedger = "CashBankLedger";
    public const string ApAging = "ApAging";
    public const string ArAging = "ArAging";
    public const string CustomerStatement = "CustomerStatement";
    public const string SupplierStatement = "SupplierStatement";
    public const string ApControlReconciliation = "ApControlReconciliation";
    public const string ArControlReconciliation = "ArControlReconciliation";
    public const string FixedAssetRegister = "FixedAssetRegister";
    public const string FixedAssetRollForward = "FixedAssetRollForward";
    public const string FixedAssetGlReconciliation = "FixedAssetGlReconciliation";
    public const string TaxOutput = "TaxOutput";
    public const string TaxInput = "TaxInput";
    public const string TaxNetSummary = "TaxNetSummary";
    public const string TaxExemptZeroOutOfScope = "TaxExemptZeroOutOfScope";
    public const string VatWithholding = "VatWithholding";
    public const string WhtPayable = "WhtPayable";
    public const string WhtReceivable = "WhtReceivable";
    public const string TaxAccountReconciliation = "TaxAccountReconciliation";
    public const string TaxConfigurationHistory = "TaxConfigurationHistory";
    public const string TaxCovidDiagnostic = "TaxCovidDiagnostic";
}

public static class FinanceReportExportFormats
{
    public const string Csv = "Csv";
}

public sealed class FinanceReportExportRequestDto
{
    public string ReportType { get; set; } = string.Empty;
    public string Format { get; set; } = FinanceReportExportFormats.Csv;
    public DateTime? AsOfDate { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public string BookClassification { get; set; } = "IFRS";
    public bool IncludeZeroBalances { get; set; }
    public bool IncludeAccountDetails { get; set; }
    public bool IncludeReversed { get; set; } = true;
    public bool IncludeOpeningBalances { get; set; } = true;
    public Guid? LayoutId { get; set; }
    public bool UseDefaultLayout { get; set; } = true;
    public List<Guid> AccountIds { get; set; } = new();
    public List<Guid> BankAccountIds { get; set; } = new();
    public List<Guid> GlAccountIds { get; set; } = new();
    public Guid? BusinessPartnerId { get; set; }
    public BusinessPartnerRoleType? BusinessPartnerRole { get; set; }
    public Guid? CustomerId { get; set; }
    public List<Guid> BusinessPartnerIds { get; set; } = new();
    public List<Guid> CustomerIds { get; set; } = new();
    public bool ShowSupplierCurrency { get; set; }
    public bool ShowCustomerCurrency { get; set; }
    public List<FinanceSegmentFilterDto> SegmentFilters { get; set; } = new();
    public List<FinanceDimensionFilterDto> DimensionFilters { get; set; } = new();
    public FixedAssetReportQueryDto? FixedAssetQuery { get; set; }
    public TaxReportRequestDto? TaxReportQuery { get; set; }
}

public sealed class FinanceReportExportResultDto
{
    public string ReportType { get; set; } = string.Empty;
    public string Format { get; set; } = FinanceReportExportFormats.Csv;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "text/csv";
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string SourceOfTruthMode { get; set; } = string.Empty;
    public int RowCount { get; set; }
    public bool UsesSettlementReadModel { get; set; }
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, decimal> Totals { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
