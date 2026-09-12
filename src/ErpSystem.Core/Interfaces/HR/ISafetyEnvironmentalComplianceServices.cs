using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// Slice 17 — Part D environmental core (FR-ENV-001–034).
// Four services: the permit/licence register (files on the central DMS via the
// controlled-upload gate), environmental governance (monitoring schedules +
// regulatory updates + sustainability), compliance reviews/clearance, and the
// monthly environmental report.
// ============================================================================

public interface ISheEnvironmentalPermitService
{
    Task<IEnumerable<SheEnvironmentalPermitSummaryDto>> GetAllAsync(
        SheEnvironmentalPermitStatus? status = null,
        SheEnvironmentalPermitType? type = null,
        string? search = null,
        int? expiringInDays = null,
        CancellationToken cancellationToken = default);

    Task<SheEnvironmentalPermitDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Expired permits for the dashboard's FR-ENV-019 red tile.</summary>
    Task<int> CountExpiredAsync(CancellationToken cancellationToken = default);

    /// <summary>Live permits with an expiry date inside the window.</summary>
    Task<int> CountExpiringAsync(int daysAhead, CancellationToken cancellationToken = default);

    Task<SheEnvironmentalPermitDto> CreateAsync(CreateSheEnvironmentalPermitDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheEnvironmentalPermitDto> UpdateAsync(UpdateSheEnvironmentalPermitDto dto, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Refused once the permit holds a document — archive instead.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SheEnvironmentalPermitDto> MarkRenewalInProgressAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Fresh issue/expiry dates, status back to Active, renewal trail stamped.</summary>
    Task<SheEnvironmentalPermitDto> RenewAsync(Guid id, RenewSheEnvironmentalPermitDto dto, Guid userId, CancellationToken cancellationToken = default);

    Task<SheEnvironmentalPermitDto> SuspendAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<SheEnvironmentalPermitDto> ArchiveAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the permit document through the controlled-upload gate onto the
    /// central DMS — first upload registers, later uploads append versions.
    /// </summary>
    Task<SheEnvironmentalPermitDto> UploadDocumentAsync(SheEnvironmentalPermitDocumentUpload upload, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Resolves a version to its download identifiers, proving it belongs to the permit.</summary>
    Task<SheEnvironmentalPermitDocumentFile> GetDocumentFileAsync(Guid permitId, Guid versionId, CancellationToken cancellationToken = default);
}

public interface ISheEnvironmentalGovernanceService
{
    // ── Monitoring schedules (FR-ENV-023–024) ──
    Task<IEnumerable<SheEnvironmentalMonitoringScheduleDto>> GetSchedulesAsync(
        bool? activeOnly = null,
        SheEnvironmentalMonitoringType? type = null,
        int? dueInDays = null,
        CancellationToken cancellationToken = default);

    Task<SheEnvironmentalMonitoringScheduleDto> GetScheduleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SheEnvironmentalMonitoringScheduleDto> CreateScheduleAsync(CreateSheEnvironmentalMonitoringScheduleDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheEnvironmentalMonitoringScheduleDto> UpdateScheduleAsync(UpdateSheEnvironmentalMonitoringScheduleDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteScheduleAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Stamps the completed cycle and advances NextDueDate by the interval.</summary>
    Task<SheEnvironmentalMonitoringScheduleDto> CompleteScheduleCycleAsync(Guid id, CompleteSheMonitoringScheduleDto dto, Guid userId, CancellationToken cancellationToken = default);

    // ── Regulatory updates register (FR-ENV-030–032 / FR-SHE-182) ──
    Task<IEnumerable<SheRegulatoryUpdateSummaryDto>> GetRegulatoryUpdatesAsync(
        SheRegulatoryUpdateStatus? status = null,
        SheRegulatoryDomain? domain = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<SheRegulatoryUpdateDto> GetRegulatoryUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SheRegulatoryUpdateDto> CreateRegulatoryUpdateAsync(CreateSheRegulatoryUpdateDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheRegulatoryUpdateDto> UpdateRegulatoryUpdateAsync(UpdateSheRegulatoryUpdateDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteRegulatoryUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>FR-ENV-031's management notification — publishes to the escalated audience and stamps the row.</summary>
    Task<SheRegulatoryUpdateDto> NotifyManagementAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    Task<SheRegulatoryUpdateDto> CloseRegulatoryUpdateAsync(Guid id, CloseSheRegulatoryUpdateDto dto, Guid userId, CancellationToken cancellationToken = default);

    // ── Sustainability initiatives (FR-ENV-028–029) ──
    Task<IEnumerable<SheSustainabilityInitiativeDto>> GetInitiativesAsync(
        SheSustainabilityCategory? category = null,
        SheSustainabilityStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<SheSustainabilityInitiativeDto> GetInitiativeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SheSustainabilityInitiativeDto> CreateInitiativeAsync(CreateSheSustainabilityInitiativeDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheSustainabilityInitiativeDto> UpdateInitiativeAsync(UpdateSheSustainabilityInitiativeDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteInitiativeAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The FR-ENV-029 KPI rollup, optionally scoped to initiatives active in a year.</summary>
    Task<SheSustainabilityKpiDto> GetSustainabilityKpisAsync(int? year = null, CancellationToken cancellationToken = default);
}

public interface ISheEnvironmentalReviewService
{
    Task<IEnumerable<SheEnvironmentalReviewSummaryDto>> GetAllAsync(
        SheEnvironmentalReviewStatus? status = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<SheEnvironmentalReviewDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SheEnvironmentalReviewDto> CreateAsync(CreateSheEnvironmentalReviewDto dto, Guid tenantId, Guid submittedById, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Pre-decision edits only — refused once approved/rejected/cleared.</summary>
    Task<SheEnvironmentalReviewDto> UpdateAsync(UpdateSheEnvironmentalReviewDto dto, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Refused once the review has been decided — the trail is history.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SheEnvironmentalReviewDto> RecordScreeningAsync(Guid id, SheEnvironmentalScreeningDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<SheEnvironmentalReviewDto> RequestCorrectionsAsync(Guid id, RequestSheReviewCorrectionsDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<SheEnvironmentalReviewDto> ApproveAsync(Guid id, SheEnvironmentalReviewDecisionDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<SheEnvironmentalReviewDto> RejectAsync(Guid id, SheEnvironmentalReviewDecisionDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<SheEnvironmentalReviewDto> ManagementApproveAsync(Guid id, SheEnvironmentalReviewDecisionDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<SheEnvironmentalReviewDto> RecordEpaSubmissionAsync(Guid id, RecordSheEpaSubmissionDto dto, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>FR-ENV-016 — refused while approval (and management approval, where required) is missing.</summary>
    Task<SheEnvironmentalReviewDto> IssueClearanceAsync(Guid id, SheEnvironmentalReviewDecisionDto dto, Guid userId, CancellationToken cancellationToken = default);

    Task<SheEnvironmentalReviewDto> ApproveCommencementAsync(Guid id, SheEnvironmentalReviewDecisionDto dto, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The FR-ENV-016 clearance report, assembled from the review and its trail.</summary>
    Task<SheEnvironmentalClearanceReportDto> GetClearanceReportAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface ISheMonthlyEnvironmentalReportService
{
    Task<IEnumerable<SheMonthlyEnvironmentalReportSummaryDto>> GetAllAsync(int? year = null, CancellationToken cancellationToken = default);
    Task<SheMonthlyEnvironmentalReportDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates (or regenerates) the report for a period from the live
    /// registers. Regeneration is refused once the report is submitted.
    /// </summary>
    Task<SheMonthlyEnvironmentalReportDto> GenerateAsync(GenerateSheMonthlyReportDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<SheMonthlyEnvironmentalReportDto> UpdateOfficerSummaryAsync(Guid id, UpdateSheMonthlyReportSummaryDto dto, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>FR-ENV-034 — submits to management (escalated notification) and freezes the report.</summary>
    Task<SheMonthlyEnvironmentalReportDto> SubmitAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The reminder engine's entry point: generates the period's report when
    /// missing, without an authenticated user context. Returns the report id
    /// when a new report was generated, null when one already existed.
    /// </summary>
    Task<(Guid ReportId, string ReportNumber)?> EnsureGeneratedForTenantAsync(Guid tenantId, int year, int month, CancellationToken cancellationToken = default);
}

/// <summary>
/// The permit-document upload request — same stream-factory contract as the
/// slice-16 controlled document register.
/// </summary>
public sealed class SheEnvironmentalPermitDocumentUpload
{
    public required Guid PermitId { get; init; }
    public required Guid ActorUserId { get; init; }
    public string? ActorName { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long FileSize { get; init; }
    public required Func<Stream> OpenReadStream { get; init; }
    public string? ChangeSummary { get; init; }
}

/// <summary>What the download endpoint needs to serve a permit document version.</summary>
public sealed class SheEnvironmentalPermitDocumentFile
{
    public required Guid DocumentRecordId { get; init; }
    public required Guid VersionId { get; init; }
    public Guid? FileUploadRecordId { get; init; }
    public required string FileName { get; init; }
    public string? ContentType { get; init; }
}
