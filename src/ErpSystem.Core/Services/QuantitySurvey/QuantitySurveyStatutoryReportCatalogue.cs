using ErpSystem.Core.DTOs.Reports;

namespace ErpSystem.Core.Services.QuantitySurvey;

public sealed record QuantitySurveySystemReportDefinition(
    string Code,
    string Name,
    string Description,
    IReadOnlyList<ReportColumnDto> Columns,
    IReadOnlyList<string> Tags)
{
    public string Query => QuantitySurveyStatutoryReportCatalogue.QueryPrefix + Code;
}

public static class QuantitySurveyStatutoryReportCatalogue
{
    public const string QueryPrefix = "system://tdc/quantity-survey/";
    public const string ReportType = "quantity-survey";
    public const string ReadPermission = QuantitySurveyAccessControlRegistry.ReportsRead;
    public const string ExportPermission = QuantitySurveyAccessControlRegistry.ReportsExport;

    public const string BoqSummaryCode = "boq-summary";
    public const string ValuationStatementCode = "valuation-statement";
    public const string VariationLogCode = "variation-log";
    public const string FinalAccountCode = "final-account";
    public const string ProjectCostStatusCode = "project-cost-status";
    public const string CertificateRegisterCode = "certificate-register";

    public static IReadOnlyList<QuantitySurveySystemReportDefinition> Definitions { get; } =
    [
        Definition(BoqSummaryCode, "BoQ Summary",
            "Approved and historical BoQ version values reconciled from immutable line snapshots.",
            C("ProjectCode", "Project code"), C("Version", "Version", "Integer"), C("VersionType", "Version type"),
            C("Status", "Status"), C("LineCount", "Lines", "Integer"), C("Currency", "Currency"),
            C("BoqValue", "BoQ value", "Decimal", "N2"), C("ApprovedAt", "Approved", "DateTime"),
            C("PublishedAt", "Published", "DateTime"), C("SnapshotHash", "Snapshot hash")),
        Definition(ValuationStatementCode, "Valuation Statement",
            "Interim valuations reconciled with their governed QS worksheet and certificate readiness.",
            C("ValuationNumber", "Valuation number"), C("Title", "Title"), C("ContractNumber", "Contract"),
            C("ValuationDate", "Valuation date", "DateTime"), C("Status", "Valuation status"),
            C("WorksheetStatus", "Worksheet status"), C("Currency", "Currency"),
            C("GrossWorkValue", "Gross work", "Decimal", "N2"), C("MaterialsOnSiteValue", "Materials on site", "Decimal", "N2"),
            C("VariationValue", "Variations", "Decimal", "N2"), C("RetentionAmount", "Retention", "Decimal", "N2"),
            C("PreviousCertifiedAmount", "Previously certified", "Decimal", "N2"), C("NetValuationAmount", "Net valuation", "Decimal", "N2"),
            C("CertificateReady", "Certificate ready", "Boolean")),
        Definition(VariationLogCode, "Variation Log",
            "Variation requests, approvals and downstream contract, budget, forecast and certificate application status.",
            C("ReferenceNumber", "Reference"), C("Title", "Title"), C("ContractNumber", "Contract"),
            C("VariationType", "Type"), C("Status", "Status"), C("RequestedDate", "Requested", "DateTime"),
            C("ApprovedDate", "Approved", "DateTime"), C("Currency", "Currency"),
            C("EstimatedAmount", "Estimated amount", "Decimal", "N2"), C("ApprovedAmount", "Approved amount", "Decimal", "N2"),
            C("BudgetImpact", "Budget impact", "Decimal", "N2"), C("ForecastImpact", "Forecast impact", "Decimal", "N2"),
            C("ScheduleImpactDays", "Schedule impact days", "Integer"), C("ApplicationStatus", "Downstream application")),
        Definition(FinalAccountCode, "Final Account Register",
            "Final accounts reconciled to approved BoQ, contract, variations, claims, escalation, certificates, retention and payments.",
            C("ProjectCode", "Project code"), C("ContractNumber", "Contract"), C("Status", "Status"),
            C("Currency", "Currency"), C("ApprovedBoqValue", "Approved BoQ", "Decimal", "N2"),
            C("OriginalContractValue", "Original contract", "Decimal", "N2"), C("ApprovedVariationAmount", "Variations", "Decimal", "N2"),
            C("ApprovedClaimAmount", "Claims", "Decimal", "N2"), C("ApprovedEscalationAmount", "Escalation", "Decimal", "N2"),
            C("CertifiedToDate", "Certified", "Decimal", "N2"), C("RetentionBalance", "Retention balance", "Decimal", "N2"),
            C("PaidToDate", "Paid", "Decimal", "N2"), C("FinalAccountValue", "Final account", "Decimal", "N2"),
            C("SettlementDate", "Settlement date", "DateTime"), C("ClosedAt", "Closed", "DateTime")),
        Definition(ProjectCostStatusCode, "Project Cost Status",
            "Project-level budget, contract, approved variations, certified value, actual cost, forecast and projected final position.",
            C("ProjectCode", "Project code"), C("Project", "Project"), C("Status", "Status"), C("Currency", "Currency"),
            C("ApprovedBudget", "Approved budget", "Decimal", "N2"), C("ApprovedBoq", "Approved BoQ", "Decimal", "N2"),
            C("OriginalContract", "Original contract", "Decimal", "N2"), C("ApprovedVariations", "Approved variations", "Decimal", "N2"),
            C("RevisedContract", "Revised contract", "Decimal", "N2"), C("CertifiedValue", "Certified value", "Decimal", "N2"),
            C("ActualCost", "Actual cost", "Decimal", "N2"), C("ForecastCost", "Forecast cost", "Decimal", "N2"),
            C("ProjectedFinalCost", "Projected final cost", "Decimal", "N2"), C("BudgetVariance", "Budget variance", "Decimal", "N2")),
        Definition(CertificateRegisterCode, "Payment Certificate Register",
            "Payment certificates reconciled to valuation, retention, deductions, tax, AP handoff and Finance payment status.",
            C("CertificateNumber", "Certificate number"), C("Title", "Title"), C("ContractNumber", "Contract"),
            C("IssueDate", "Issue date", "DateTime"), C("Status", "Status"), C("ApprovalStatus", "Approval status"),
            C("Currency", "Currency"), C("GrossCertified", "Gross certified", "Decimal", "N2"),
            C("RetentionHeld", "Retention held", "Decimal", "N2"), C("RetentionReleased", "Retention released", "Decimal", "N2"),
            C("AdvanceRecovery", "Advance recovery", "Decimal", "N2"), C("OtherDeductions", "Other deductions", "Decimal", "N2"),
            C("Tax", "Tax", "Decimal", "N2"), C("NetCertified", "Net certified", "Decimal", "N2"),
            C("ApHandoffStatus", "AP handoff"), C("PaymentStatus", "Payment status"))
    ];

    public static QuantitySurveySystemReportDefinition? Resolve(string? query)
    {
        if (string.IsNullOrWhiteSpace(query) || !query.StartsWith(QueryPrefix, StringComparison.OrdinalIgnoreCase)) return null;
        var code = query[QueryPrefix.Length..].Trim();
        return Definitions.FirstOrDefault(item => item.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
    }

    public static Dictionary<string, object> BuildParameters() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["projectId"] = Parameter("Project", "project", true),
        ["startDate"] = Parameter("Start date", "date", false),
        ["endDate"] = Parameter("End date", "date", false)
    };

    private static QuantitySurveySystemReportDefinition Definition(string code, string name, string description, params ReportColumnDto[] columns) =>
        new(code, name, description, columns, ["statutory", "quantity-survey", code]);

    private static ReportColumnDto C(string name, string displayName, string type = "String", string? format = null) =>
        new() { Name = name, DisplayName = displayName, DataType = type, Format = format, IsVisible = true };

    private static Dictionary<string, object> Parameter(string label, string type, bool required) => new()
    {
        ["label"] = label,
        ["type"] = type,
        ["required"] = required
    };
}
