using ErpSystem.Core.DTOs.Reports;

namespace ErpSystem.Core.Services.HR;

public sealed record HrAwardsSystemReportDefinition(
    string Code,
    string Name,
    string Description,
    IReadOnlyList<ReportColumnDto> Columns,
    IReadOnlyList<string> Tags)
{
    public string Query => HrAwardsReportCatalogue.QueryPrefix + Code;
}

/// <summary>
/// The HR awards reports the application owns, rather than ones a user composed.
/// </summary>
/// <remarks>
/// <para>Modelled on <c>ProcurementStatutoryReportCatalogue</c> and its inventory twin: a definition
/// here is seeded into the <c>Reports</c> table per tenant, executed through the shared report engine
/// by an <c>ISystemReportProvider</c>, and never accepts arbitrary SQL.</para>
///
/// <para><b>FR-HR-113 is the only mandatory report in this area</b> — *"report on long-service-award
/// eligibility"*. It is one definition, not a family, and the catalogue is deliberately left with a
/// single entry rather than padded with reports nobody asked for.</para>
/// </remarks>
public static class HrAwardsReportCatalogue
{
    public const string QueryPrefix = "system://tdc/hr-awards/";
    public const string ReportType = "hr-awards";

    public const string LongServiceEligibilityCode = "long-service-eligibility";

    public static IReadOnlyList<HrAwardsSystemReportDefinition> Definitions { get; } =
    [
        Definition(LongServiceEligibilityCode, "Long-Service Award Eligibility",
            "Every serving employee's standing against a long-service ladder — who is eligible, who is "
            + "exempt, who already holds the highest rung their service has reached, and whose service "
            + "cannot be measured at all.",
            C("EmployeeNumber", "Employee number"),
            C("EmployeeName", "Employee"),
            C("Department", "Department"),
            C("Standing", "Standing"),
            C("ServiceStartDate", "Employed from", "DateTime"),
            C("YearsOfService", "Years served", "Integer"),
            C("MilestoneYears", "Milestone", "Integer"),
            C("MilestoneName", "Milestone name"),
            C("MonetaryAmount", "Award value", "Decimal", "N2"),
            C("LeaveDaysBonus", "Leave days", "Integer"),
            C("HighestGrantedYears", "Highest already granted", "Integer"),
            C("Reason", "Reason")),
    ];

    public static HrAwardsSystemReportDefinition? Resolve(string? query)
    {
        if (string.IsNullOrWhiteSpace(query) ||
            !query.StartsWith(QueryPrefix, StringComparison.OrdinalIgnoreCase))
            return null;
        var code = query[QueryPrefix.Length..].Trim();
        return Definitions.FirstOrDefault(item => item.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The parameters the report accepts.
    /// </summary>
    /// <remarks>
    /// <para><c>awardTypeId</c> is the one that matters: a ladder belongs to an award, so "eligible"
    /// is meaningless until the reader says eligible for <i>what</i>. When it is omitted the report
    /// picks the tenant's only long-service award if there is exactly one, and otherwise says so
    /// rather than guessing.</para>
    ///
    /// <para><c>asOf</c> exists for the same reason the sweep has it. Measured 2026-08-21, one live
    /// employee has ten completed years and none has fifteen — without a date to report against,
    /// most of the ladder could never be shown to work.</para>
    ///
    /// <para><c>standing</c> filters to one category. The default is <b>everybody</b>, deliberately:
    /// a report that showed only the eligible would hide the 3,476 employees whose service cannot be
    /// measured, and its length would describe the data while reading as a statement about staff.</para>
    /// </remarks>
    public static Dictionary<string, object> BuildParameters() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["awardTypeId"] = Parameter("Award type", "text"),
        ["asOf"] = Parameter("As at", "date"),
        ["standing"] = Parameter("Standing", "text"),
        ["departmentName"] = Parameter("Department", "text"),
    };

    private static HrAwardsSystemReportDefinition Definition(
        string code, string name, string description, params ReportColumnDto[] columns) =>
        new(code, name, description, columns, ["FR-HR-113", "RPT-001", "hr", "awards", code]);

    private static ReportColumnDto C(string name, string displayName, string type = "String", string? format = null) =>
        new() { Name = name, DisplayName = displayName, DataType = type, Format = format, IsVisible = true };

    private static Dictionary<string, object> Parameter(string label, string type) => new()
    {
        ["label"] = label,
        ["type"] = type,
        ["required"] = false
    };
}
