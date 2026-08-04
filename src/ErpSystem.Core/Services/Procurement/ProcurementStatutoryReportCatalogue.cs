using ErpSystem.Core.DTOs.Reports;

namespace ErpSystem.Core.Services.Procurement;

public sealed record ProcurementSystemReportDefinition(
    string Code,
    string Name,
    string Description,
    IReadOnlyList<ReportColumnDto> Columns,
    IReadOnlyList<string> Tags)
{
    public string Query => ProcurementStatutoryReportCatalogue.QueryPrefix + Code;
}

public static class ProcurementStatutoryReportCatalogue
{
    public const string QueryPrefix = "system://tdc/procurement/";
    public const string ReportType = "procurement";
    public const string ReadPermission = "procurement.reports.read";
    public const string ExportPermission = "procurement.reports.export";

    public const string AppVsActualCode = "app-vs-actual";
    public const string TenderRegisterCode = "tender-register";
    public const string ContractRegisterCode = "contract-register";
    public const string SupplierPerformanceCode = "supplier-performance";
    public const string AwardNotificationCode = "award-notification";
    public const string SavingsCode = "savings-register";
    public const string EtcMinutesCode = "etc-minutes-register";

    public static IReadOnlyList<ProcurementSystemReportDefinition> Definitions { get; } =
    [
        Definition(AppVsActualCode, "APP vs Actual Procurement Register",
            "Reconciles approved annual procurement plans to linked purchase orders and tender awards.",
            C("PlanNumber", "Plan number"), C("FiscalYear", "Fiscal year", "Integer"),
            C("Department", "Department"), C("Status", "Plan status"), C("Currency", "Currency"),
            C("PlannedItemCount", "Planned items", "Integer"), C("ProcuredItemCount", "Procured items", "Integer"),
            C("ApprovedBudget", "Approved budget", "Decimal", "N2"), C("PlannedValue", "Planned value", "Decimal", "N2"),
            C("ActualValue", "Actual value", "Decimal", "N2"), C("Variance", "Variance", "Decimal", "N2"),
            C("SubmissionStatus", "APP submission status"), C("ExternalReference", "External reference")),
        Definition(TenderRegisterCode, "Tender Register",
            "Statutory tender register with publication, submission, opening, evaluation, award and contract outcomes.",
            C("TenderNumber", "Tender number"), C("Title", "Title"), C("TenderType", "Method"), C("Status", "Status"),
            C("PublishDate", "Published", "DateTime"), C("SubmissionDeadline", "Submission deadline", "DateTime"),
            C("OpeningDate", "Opening date", "DateTime"), C("AwardDate", "Award date", "DateTime"),
            C("Currency", "Currency"), C("EstimatedValue", "Estimated value", "Decimal", "N2"),
            C("BidCount", "Bids", "Integer"), C("AwardCount", "Awards", "Integer"),
            C("AwardedValue", "Awarded value", "Decimal", "N2"), C("ContractCount", "Contracts", "Integer")),
        Definition(ContractRegisterCode, "Procurement Contract Register",
            "Contract register reconciled to tender awards, suppliers, amendments and milestone delivery.",
            C("ContractNumber", "Contract number"), C("ContractTitle", "Contract title"), C("ContractType", "Type"),
            C("Status", "Status"), C("SupplierCode", "Supplier code"), C("SupplierName", "Supplier"),
            C("TenderNumber", "Tender number"), C("Currency", "Currency"), C("ContractValue", "Contract value", "Decimal", "N2"),
            C("ApprovedAmendmentValue", "Approved amendment value", "Decimal", "N2"),
            C("StartDate", "Start date", "DateTime"), C("EndDate", "End date", "DateTime"),
            C("SignedDate", "Signed date", "DateTime"), C("CompletedMilestones", "Completed milestones", "Integer"),
            C("TotalMilestones", "Total milestones", "Integer")),
        Definition(SupplierPerformanceCode, "Supplier Performance Register",
            "Immutable supplier scorecard register with source coverage, performance measures and policy lineage.",
            C("ScorecardReference", "Scorecard reference"), C("SupplierCode", "Supplier code"), C("SupplierName", "Supplier"),
            C("PeriodStart", "Period start", "DateTime"), C("PeriodEnd", "Period end", "DateTime"),
            C("CalculatedAt", "Calculated", "DateTime"), C("DataStatus", "Data status"),
            C("CoveragePercent", "Coverage %", "Decimal", "N2"), C("OverallScore", "Overall score", "Decimal", "N2"),
            C("PerformanceBand", "Band"), C("MinimumScore", "Minimum score", "Decimal", "N2"),
            C("MinimumScoreBreached", "Minimum breached", "Boolean"), C("PurchaseOrders", "Purchase orders", "Integer"),
            C("Receipts", "Receipts", "Integer"), C("Contracts", "Contracts", "Integer"), C("NextReviewDue", "Next review", "DateTime")),
        Definition(AwardNotificationCode, "Award Notification Register",
            "Award and unsuccessful-bidder notification register with approved-letter, dispatch, delivery and acknowledgement evidence.",
            C("AwardReference", "Award reference"), C("SourceReference", "Source reference"), C("AwardFamily", "Award family"),
            C("AwardedAt", "Awarded", "DateTime"), C("SupplierCode", "Supplier code"), C("SupplierName", "Recipient"),
            C("Outcome", "Outcome"), C("ApprovedLetterVersion", "Approved letter version", "Integer"),
            C("DispatchCount", "Dispatches", "Integer"), C("LatestDispatchAt", "Latest dispatch", "DateTime"),
            C("LatestDeliveryOutcome", "Latest delivery outcome"), C("Acknowledged", "Acknowledged", "Boolean"),
            C("AppealCount", "Appeals", "Integer"), C("StandstillEndsAt", "Standstill ends", "DateTime"),
            C("AppealWindowEndsAt", "Appeal window ends", "DateTime")),
        Definition(SavingsCode, "Procurement Savings Register",
            "Tender-level savings register comparing approved estimates with non-cancelled award values.",
            C("TenderNumber", "Tender number"), C("Title", "Title"), C("TenderType", "Method"), C("Status", "Status"),
            C("Currency", "Currency"), C("EstimatedValue", "Estimated value", "Decimal", "N2"),
            C("AwardedValue", "Awarded value", "Decimal", "N2"), C("SavingsValue", "Savings", "Decimal", "N2"),
            C("SavingsPercent", "Savings %", "Decimal", "N2"), C("AwardCount", "Awards", "Integer"),
            C("SupplierCount", "Suppliers", "Integer"), C("AwardDate", "Award date", "DateTime")),
        Definition(EtcMinutesCode, "ETC Minutes Register",
            "Entity Tender Committee and evaluation meeting minutes/evidence register with attendance and quorum proof.",
            C("SourceReference", "Source reference"), C("CommitteeCode", "Committee code"), C("CommitteeName", "Committee"),
            C("MeetingSequence", "Meeting no.", "Integer"), C("Phase", "Phase"), C("Status", "Status"),
            C("MeetingMode", "Mode"), C("MeetingChannel", "Channel"), C("ScheduledAt", "Scheduled", "DateTime"),
            C("StartedAt", "Started", "DateTime"), C("ClosedAt", "Closed", "DateTime"),
            C("EligibleVoters", "Eligible voters", "Integer"), C("SignedAttendance", "Signed attendance", "Integer"),
            C("QuorumMet", "Quorum met", "Boolean"), C("ChairPresent", "Chair present", "Boolean"),
            C("SecretaryPresent", "Secretary present", "Boolean"), C("EvidenceReference", "Minutes/evidence reference"))
    ];

    public static ProcurementSystemReportDefinition? Resolve(string? query)
    {
        if (string.IsNullOrWhiteSpace(query) || !query.StartsWith(QueryPrefix, StringComparison.OrdinalIgnoreCase))
            return null;
        var code = query[QueryPrefix.Length..].Trim();
        return Definitions.FirstOrDefault(item => item.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
    }

    public static Dictionary<string, object> BuildParameters() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["startDate"] = Parameter("Start date", "date"),
        ["endDate"] = Parameter("End date", "date"),
        ["fiscalYear"] = Parameter("Fiscal year", "number"),
        ["status"] = Parameter("Status", "text"),
        ["supplierId"] = Parameter("Supplier", "supplier")
    };

    private static ProcurementSystemReportDefinition Definition(
        string code,
        string name,
        string description,
        params ReportColumnDto[] columns) =>
        new(code, name, description, columns,
            ["TDC-0701", "RPT-001", "statutory", "procurement", code]);

    private static ReportColumnDto C(string name, string displayName, string type = "String", string? format = null) =>
        new() { Name = name, DisplayName = displayName, DataType = type, Format = format, IsVisible = true };

    private static Dictionary<string, object> Parameter(string label, string type) => new()
    {
        ["label"] = label,
        ["type"] = type,
        ["required"] = false
    };
}

