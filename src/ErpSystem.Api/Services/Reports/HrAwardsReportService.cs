using System.Text.Json;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities.HR.Awards;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;

namespace ErpSystem.Api.Services.Reports;

/// <summary>
/// FR-HR-113 — the long-service-award eligibility report.
/// </summary>
/// <remarks>
/// <para><b>It runs the sweep's calculation, not its own.</b> The rows come from
/// <see cref="ILongServiceSweepEvaluator"/>, which is also what the sweep preview and the sweep run
/// read. A report that computed eligibility separately would eventually list somebody the button
/// then refused to award — and the report would be the thing believed.</para>
///
/// <para><b>Why this provider lives in the API project when procurement's and inventory's live in
/// Core.</b> Those two gate on their own module access-control services. This one's gate is the
/// ASP.NET policy <c>AwardsReadPolicy</c>, which is evaluated by two OR-ed handlers — a
/// database-backed one and the HR role fallback. Re-deriving that from Core would mean writing a
/// third implementation of a rule that already has two, and it would drift: a user granted awards
/// permissions through a custom role holds no HR role, so a role-only copy would refuse a reader the
/// endpoints admit. Evaluating the real policy is the only version that cannot disagree with
/// itself.</para>
///
/// <para><b>It reports everybody, and that is the point of it.</b> With no filter a reader sees the
/// eligible, the exempt, those who already hold the highest rung their service has reached, and —
/// measured 2026-08-21, <b>3,476 of 5,579</b> employees — those with no employment date at all. A
/// report of only the eligible would be a handful of rows and would read as a statement about TDC's
/// staff when it is a statement about TDC's employee records.</para>
///
/// <para><b>Paging happens in memory, deliberately.</b> Completed-years arithmetic is C# — the one
/// implementation slice 3b consolidated — so pushing it into SQL would mean a second version of it,
/// which is precisely the drift this report exists not to cause. The evaluator reads at most the
/// serving workforce; paging a list that size costs nothing worth a second implementation.</para>
/// </remarks>
public sealed class HrAwardsReportService : ISystemReportProvider
{
    private readonly ILongServiceSweepEvaluator _evaluator;
    private readonly ILongServiceMilestoneRepository _milestoneRepo;
    private readonly IAwardTypeRepository _typeRepo;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IAuthorizationService _authorization;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HrAwardsReportService(
        ILongServiceSweepEvaluator evaluator,
        ILongServiceMilestoneRepository milestoneRepo,
        IAwardTypeRepository typeRepo,
        ICurrentUserProvider currentUser,
        IAuthorizationService authorization,
        IHttpContextAccessor httpContextAccessor)
    {
        _evaluator = evaluator;
        _milestoneRepo = milestoneRepo;
        _typeRepo = typeRepo;
        _currentUser = currentUser;
        _authorization = authorization;
        _httpContextAccessor = httpContextAccessor;
    }

    public bool CanHandle(string? reportQuery) => HrAwardsReportCatalogue.Resolve(reportQuery) is not null;

    public bool OwnsIdentifier(string? reportQuery) =>
        !string.IsNullOrWhiteSpace(reportQuery) &&
        reportQuery.StartsWith(HrAwardsReportCatalogue.QueryPrefix, StringComparison.OrdinalIgnoreCase);

    public string? ResolveCode(string? reportQuery) =>
        HrAwardsReportCatalogue.Resolve(reportQuery)?.Code;

    /// <summary>
    /// Whether the caller may read this report at all.
    /// </summary>
    /// <remarks>
    /// The rows are named employees with their length of service and the reason any of them is
    /// exempt — which means the report carries, by implication, <b>who has a disciplinary record</b>.
    /// It is therefore held to the same gate as the awards endpoints rather than being open to
    /// anybody who can reach the reports screen.
    /// </remarks>
    public async Task<bool> CanReadAsync(bool isAdministrator, CancellationToken cancellationToken = default)
    {
        if (isAdministrator) return true;

        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true) return false;

        var result = await _authorization.AuthorizeAsync(principal, HrPermissions.AwardsReadPolicy);
        return result.Succeeded;
    }

    /// <summary>Exporting is the same data leaving the building, so it is the same gate.</summary>
    public async Task AuthorizeExportAsync(
        string reportQuery, bool isAdministrator, CancellationToken cancellationToken = default)
    {
        _ = HrAwardsReportCatalogue.Resolve(reportQuery)
            ?? throw new InvalidOperationException("The HR awards system report is not registered.");

        if (!await CanReadAsync(isAdministrator, cancellationToken))
            throw new UnauthorizedAccessException(
                "You do not have permission to export the long-service eligibility report.");
    }

    public async Task<ReportResultDto> ExecuteAsync(
        string reportQuery,
        ExecuteReportDto request,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        var definition = HrAwardsReportCatalogue.Resolve(reportQuery)
            ?? throw new InvalidOperationException("The HR awards system report is not registered.");

        if (!await CanReadAsync(isAdministrator, cancellationToken))
            throw new UnauthorizedAccessException(
                "You do not have permission to read the long-service eligibility report.");

        var tenantId = _currentUser.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("A tenant context is required to execute HR awards reports.");

        var filters = Filters.Parse(request);
        var awardType = await ResolveAwardTypeAsync(tenantId, filters.AwardTypeId);
        var ladder = (await _milestoneRepo.GetByAwardTypeIdAsync(tenantId, awardType.Id)).ToList();

        var asOf = filters.AsOf ?? DateTime.UtcNow;
        var verdicts = await _evaluator.EvaluateAsync(awardType, ladder, tenantId, asOf);

        IEnumerable<LongServiceVerdict> rows = verdicts;

        if (filters.Standing is { } standing)
            rows = rows.Where(v => v.Standing == standing);

        if (!string.IsNullOrWhiteSpace(filters.Department))
            rows = rows.Where(v => string.Equals(v.DepartmentName, filters.Department, StringComparison.OrdinalIgnoreCase));

        // Eligible first, then exempt, then the rest. Somebody opening this wants the people it is
        // asking them to act on at the top, not an alphabetical list they have to search through.
        var ordered = rows
            .OrderBy(v => SortRank(v.Standing))
            .ThenByDescending(v => v.YearsOfService ?? -1)
            .ThenBy(v => v.EmployeeName)
            .ToList();

        var totalRows = ordered.Count;
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 1000);
        if (request.MaxRows is > 0) pageSize = Math.Min(pageSize, Math.Clamp(request.MaxRows.Value, 1, 1000));
        var totalPages = totalRows == 0 ? 0 : (int)Math.Ceiling(totalRows / (double)pageSize);
        var pageRows = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new ReportResultDto
        {
            ReportName = definition.Name,
            ExecutedAt = DateTime.UtcNow,
            TotalRows = totalRows,
            Columns = definition.Columns.Select((item, index) => new ReportColumnDto
            {
                Name = item.Name,
                DisplayName = item.DisplayName,
                DataType = item.DataType,
                Format = item.Format,
                IsVisible = item.IsVisible,
                Order = index,
                AggregationType = item.AggregationType
            }).ToList(),
            Data = pageRows.Select(Map).ToList(),
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            HasNextPage = page < totalPages,
            HasPreviousPage = page > 1 && totalPages > 0,
            Metadata = new ReportMetadataDto
            {
                Query = definition.Query,
                DataAsOf = asOf,
                DataSource = "Tenant-scoped employee service records, the award's milestone ladder, "
                    + "and staff disciplinary actions",
                Parameters = new Dictionary<string, object>
                {
                    ["awardTypeId"] = awardType.Id,
                    ["awardTypeName"] = awardType.Name,
                    ["asOf"] = asOf,
                    ["standing"] = filters.Standing?.ToString() ?? "All",
                    ["department"] = filters.Department ?? "All",
                },
                Statistics = BuildStatistics(definition.Code, verdicts, ladder, awardType, totalRows),
            }
        };
    }

    /// <summary>
    /// The counts that stop the report being misread.
    /// </summary>
    /// <remarks>
    /// Computed over <b>every</b> verdict rather than the filtered page, so a reader who has filtered
    /// to "Eligible" can still see how many people could not be measured at all and how many rungs
    /// the ladder even has. A filtered count of four means nothing without them.
    /// </remarks>
    private static Dictionary<string, object> BuildStatistics(
        string code,
        IReadOnlyList<LongServiceVerdict> verdicts,
        IReadOnlyList<LongServiceMilestone> ladder,
        AwardType awardType,
        int totalRows) => new()
    {
        ["systemCode"] = code,
        ["employeesConsidered"] = verdicts.Count,
        ["eligible"] = verdicts.Count(v => v.Standing == LongServiceStanding.Eligible),
        ["exempt"] = verdicts.Count(v => v.Standing == LongServiceStanding.Exempt),
        ["alreadyGranted"] = verdicts.Count(v => v.Standing == LongServiceStanding.AlreadyGranted),
        ["notYetAtMilestone"] = verdicts.Count(v => v.Standing == LongServiceStanding.NotYetAtMilestone),
        ["serviceUnknown"] = verdicts.Count(v => v.Standing == LongServiceStanding.ServiceUnknown),
        ["milestonesConfigured"] = ladder.Count(m => m.IsActive),
        // ⚠ A ladder with no priced rungs is not the same as awards worth nothing. TDC has not said
        // what a rung carries, so an unpriced ladder is an unanswered question — reported here so a
        // column of blanks is not read as a column of zeros.
        ["milestonesPriced"] = ladder.Count(m => m.IsActive && m.MonetaryAmount.HasValue),
        ["disciplinaryCheckApplied"] = awardType.DisqualifyOnDisciplinaryRecord,
        ["rowsAfterFilter"] = totalRows,
    };

    /// <summary>
    /// Which award's ladder the report is about.
    /// </summary>
    /// <remarks>
    /// <para>"Eligible" is meaningless until the reader says eligible for <i>what</i> — a ladder
    /// belongs to an award. With no parameter the report resolves the tenant's single active
    /// long-service award, which is the common case and saves an id nobody wants to paste. With none
    /// it refuses, and with several it <b>refuses and names them</b>: picking one would produce a
    /// report that looked authoritative while answering a question nobody asked.</para>
    ///
    /// <para>⚠ <b>These are <c>InvalidOperationException</c>, not <c>AwardsWorkflowException</c>,
    /// and the difference is the whole message.</b> <c>ReportsController.ExecuteReport</c> catches
    /// <c>InvalidOperationException</c> as a 400 carrying the text, and everything else as a <b>500
    /// with a canned string</b> — so an <c>AwardsWorkflowException</c> here would be swallowed and
    /// the reader would be told "an error occurred" instead of which awards to choose between. The
    /// awards exception type only reaches its middleware mapping from an awards controller.</para>
    ///
    /// <para>400 is also the truer code. The resource addressed by this request is the <i>report</i>,
    /// and it was found; an award id inside <c>parameters</c> is data, so a bad one is a malformed
    /// request rather than a missing resource.</para>
    /// </remarks>
    private async Task<AwardType> ResolveAwardTypeAsync(Guid tenantId, Guid? requested)
    {
        if (requested is { } id)
        {
            var chosen = await _typeRepo.GetByIdAsync(id);
            if (chosen == null || chosen.TenantId != tenantId || chosen.IsDeleted)
                throw new InvalidOperationException(
                    $"No award type with id {id} exists on this tenant, so there is no ladder to report against.");
            return chosen;
        }

        var candidates = (await _typeRepo.GetByTenantAsync(tenantId))
            .Where(t => !t.IsDeleted && t.IsActive && t.Category == AwardCategory.LongService)
            .ToList();

        return candidates.Count switch
        {
            1 => candidates[0],
            0 => throw new InvalidOperationException(
                "This tenant has no active long-service award, so there is no ladder to report against. "
                + "Create one and give it a milestone ladder first."),
            _ => throw new InvalidOperationException(
                "This tenant has more than one active long-service award, so the report needs to be told "
                + "which one: " + string.Join(", ", candidates.Select(t => $"{t.Name} ({t.Id})")) + "."),
        };
    }

    private static int SortRank(LongServiceStanding standing) => standing switch
    {
        LongServiceStanding.Eligible => 0,
        LongServiceStanding.Exempt => 1,
        LongServiceStanding.AlreadyGranted => 2,
        LongServiceStanding.NotYetAtMilestone => 3,
        _ => 4,
    };

    private static Dictionary<string, object> Map(LongServiceVerdict v)
    {
        var row = new Dictionary<string, object>
        {
            ["EmployeeName"] = v.EmployeeName,
            ["Standing"] = v.Standing.ToString(),
        };

        // Absent values are written as nulls rather than left out: the report engine renders one
        // dictionary per row against a fixed column list, and a missing key would shift a column
        // rather than blank a cell.
        Put(row, "EmployeeNumber", v.EmployeeNumber);
        Put(row, "Department", v.DepartmentName);
        Put(row, "ServiceStartDate", v.ServiceStartDate?.ToDateTime(TimeOnly.MinValue));
        Put(row, "YearsOfService", v.YearsOfService);
        Put(row, "MilestoneYears", v.MilestoneYears);
        Put(row, "MilestoneName", v.MilestoneName);
        Put(row, "MonetaryAmount", v.MonetaryAmount);
        Put(row, "LeaveDaysBonus", v.LeaveDaysBonus);
        Put(row, "HighestGrantedYears", v.HighestGrantedYears);
        Put(row, "Reason", v.Reason);
        return row;
    }

    private static void Put(Dictionary<string, object> row, string key, object? value) => row[key] = value!;

    private sealed record Filters(Guid? AwardTypeId, DateTime? AsOf, LongServiceStanding? Standing, string? Department)
    {
        public static Filters Parse(ExecuteReportDto request) => new(
            GetGuid(request.Parameters, "awardTypeId"),
            GetDate(request.Parameters, "asOf") ?? request.EndDate,
            GetStanding(request.Parameters, "standing"),
            GetString(request.Parameters, "departmentName"));

        private static string? GetString(Dictionary<string, object>? parameters, string key)
        {
            if (parameters == null || !parameters.TryGetValue(key, out var raw) || raw == null) return null;
            var text = raw is JsonElement element
                ? element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString()
                : raw.ToString();
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        private static Guid? GetGuid(Dictionary<string, object>? parameters, string key) =>
            Guid.TryParse(GetString(parameters, key), out var value) ? value : null;

        private static DateTime? GetDate(Dictionary<string, object>? parameters, string key) =>
            DateTime.TryParse(GetString(parameters, key), out var value)
                ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
                : null;

        /// <summary>
        /// The standing filter. An unrecognised value means "no filter" rather than an error.
        /// </summary>
        /// <remarks>
        /// The report engine passes parameters through from a saved definition, so a stale or
        /// mistyped value should widen the report rather than fail it — the reader can see the
        /// standing on every row and tell immediately that no filter applied.
        /// </remarks>
        private static LongServiceStanding? GetStanding(Dictionary<string, object>? parameters, string key)
        {
            var text = GetString(parameters, key);
            if (string.IsNullOrWhiteSpace(text) || text.Equals("All", StringComparison.OrdinalIgnoreCase))
                return null;
            return Enum.TryParse<LongServiceStanding>(text, true, out var value) ? value : null;
        }
    }
}
