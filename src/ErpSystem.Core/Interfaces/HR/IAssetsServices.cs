using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

#region Asset Type Services

public interface IAssetTypeService
{
    Task<AssetTypeDto?> GetByIdAsync(Guid id);
    Task<AssetTypeDetailDto?> GetWithAttributesAsync(Guid id);
    Task<IEnumerable<AssetTypeSummaryDto>> GetAllAsync();
    Task<PagedResult<AssetTypeSummaryDto>> GetPagedAsync(int page, int pageSize, string? searchTerm = null);
    Task<AssetTypeDto> CreateAsync(CreateAssetTypeDto dto);
    Task<AssetTypeDto> UpdateAsync(Guid id, UpdateAssetTypeDto dto);
    Task DeleteAsync(Guid id);
}

public interface IAssetTypeAttributeService
{
    Task<AssetTypeAttributeDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<AssetTypeAttributeDto>> GetByAssetTypeIdAsync(Guid assetTypeId);
    Task<AssetTypeAttributeDto> CreateAsync(CreateAssetTypeAttributeDto dto);
    Task<AssetTypeAttributeDto> UpdateAsync(Guid id, UpdateAssetTypeAttributeDto dto);
    Task DeleteAsync(Guid id);
}

#endregion

#region Company Asset Services

public interface ICompanyAssetService
{
    Task<CompanyAssetDto?> GetByIdAsync(Guid id);
    Task<CompanyAssetDetailDto?> GetWithDetailsAsync(Guid id);
    Task<IEnumerable<CompanyAssetSummaryDto>> GetAllAsync();
    Task<PagedResult<CompanyAssetSummaryDto>> GetPagedAsync(int page, int pageSize, string? searchTerm = null, CompanyAssetStatus? status = null, Guid? assetTypeId = null);
    Task<IEnumerable<CompanyAssetSummaryDto>> GetByStatusAsync(CompanyAssetStatus status);
    Task<IEnumerable<CompanyAssetSummaryDto>> GetByAssetTypeAsync(Guid assetTypeId);
    Task<IEnumerable<CompanyAssetSummaryDto>> GetAvailableForAssignmentAsync();
    Task<IEnumerable<CompanyAssetSummaryDto>> GetByEmployeeAsync(Guid employeeId);
    /// <summary>
    /// AST-1 — the maintenance watchlist, in three readings of the same question.
    /// </summary>
    /// <remarks>
    /// <para>Split into three because they are three different jobs. <b>Due</b> is a plan: what to
    /// book in the next <paramref name="daysAhead"/> days. <b>Overdue</b> is an exception list:
    /// what has already slipped, worst first, and it must not be diluted by thirty rows that are
    /// merely approaching. <b>Unscheduled</b> is a data-quality list: assets that say they need
    /// regular servicing and have never been given a date — the rows every other read filters
    /// away, and the ones a monitoring feature most needs to surface.</para>
    ///
    /// <para><paramref name="asOf"/> exists for the same reason it does on the reminder preview:
    /// the schedule is written by the server from the interval, so without it a test can only ever
    /// assert what happens to be true today. It reads only.</para>
    /// </remarks>
    Task<IEnumerable<AssetMaintenanceDueDto>> GetDueForMaintenanceAsync(int daysAhead = 30, DateOnly? asOf = null);

    /// <summary>AST-1 — assets whose maintenance date has already passed, most overdue first.</summary>
    Task<IEnumerable<AssetMaintenanceDueDto>> GetOverdueMaintenanceAsync(DateOnly? asOf = null);

    /// <summary>AST-1 — assets that require regular maintenance and have never been scheduled.</summary>
    Task<IEnumerable<AssetMaintenanceDueDto>> GetUnscheduledMaintenanceAsync();

    // ── insurance and the register report, area 16 slice 11 ───────────────────

    /// <summary>
    /// Insured assets whose cover lapses inside the window — the renewal plan.
    /// </summary>
    /// <remarks>
    /// The default horizon is 60 days rather than maintenance's 30: a service can be booked in a
    /// fortnight and an insurance renewal is a quotation, an approval and a payment. ⚠ An assumed
    /// number, not TDC's — flagged with the other assumed windows.
    /// </remarks>
    Task<IEnumerable<AssetInsuranceWatchItemDto>> GetInsuranceExpiringAsync(
        int daysAhead = 60, DateOnly? asOf = null);

    /// <summary>Assets whose cover has already lapsed, worst first — the exception list.</summary>
    Task<IEnumerable<AssetInsuranceWatchItemDto>> GetInsuranceExpiredAsync(DateOnly? asOf = null);

    /// <summary>Assets marked insured with no expiry date — the data-quality list.</summary>
    Task<IEnumerable<AssetInsuranceWatchItemDto>> GetInsuranceUndatedAsync();

    /// <summary>
    /// The register counted and totalled, with the watchlists that say what needs attention.
    /// </summary>
    Task<AssetRegisterReportDto> GetRegisterReportAsync(
        Guid? assetTypeId = null, Guid? unitId = null, Guid? locationId = null,
        CompanyAssetStatus? status = null, DateOnly? asOf = null);

    // ── the seam with the Maintenance module — slice 9b, decision D10 ──────────────────────

    /// <summary>
    /// The Maintenance module's assets HR could point at, with the ones it already claims marked.
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="GetLinkableFixedAssetsAsync"/>, including the rule that matters: already-
    /// linked rows are returned and flagged rather than filtered away.
    /// </remarks>
    Task<IEnumerable<MaintenanceAssetPickDto>> GetLinkableMaintenanceAssetsAsync(string? searchTerm = null);

    /// <summary>
    /// Names this asset's counterpart in the Maintenance register, so work can be sent there.
    /// </summary>
    /// <remarks>
    /// HR does not <b>create</b> rows in that register. There is one asset category on this
    /// database and it is a leftover test fixture, so an auto-registered laptop would be filed under
    /// "Vehicles". The Projects module draws the same line in the same words — "link a maintenance
    /// asset ... before creating maintenance follow-through" — and it is the right one.
    /// </remarks>
    Task<CompanyAssetDto> LinkMaintenanceAssetAsync(Guid assetId, Guid maintenanceAssetId);

    /// <summary>Severs the link. Refused while the asset is at the workshop.</summary>
    Task<CompanyAssetDto> UnlinkMaintenanceAssetAsync(Guid assetId);
    Task<CompanyAssetDto> CreateAsync(CreateCompanyAssetDto dto);

    /// <summary>
    /// AST-11 — the Finance fixed assets HR could register, with the ones it already has marked.
    /// </summary>
    /// <remarks>
    /// Already-linked assets are RETURNED and flagged, not filtered out. A picker that silently
    /// omits them leaves a user hunting for an asset that is right there; one that shows it greyed
    /// with "already registered" answers the question they actually have.
    /// </remarks>
    Task<IEnumerable<FixedAssetPickDto>> GetLinkableFixedAssetsAsync(string? searchTerm = null);

    /// <summary>AST-11 — register an HR asset that stands for an existing Finance fixed asset.</summary>
    Task<CompanyAssetDto> CreateFromFixedAssetAsync(CreateAssetFromFixedAssetDto dto);
    Task<CompanyAssetDto> UpdateAsync(Guid id, UpdateCompanyAssetDto dto);
    Task DeleteAsync(Guid id);
    Task DisposeAssetAsync(DisposeAssetDto dto);
}

public interface IAssetAttributeValueService
{
    Task<AssetAttributeValueDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<AssetAttributeValueDto>> GetByAssetIdAsync(Guid assetId);
    Task<AssetAttributeValueDto> CreateAsync(Guid assetId, CreateAssetAttributeValueDto dto);
    Task<AssetAttributeValueDto> UpdateAsync(Guid id, UpdateAssetAttributeValueDto dto);
    Task DeleteAsync(Guid id);
}

#endregion

#region Asset Assignment Services

public interface IAssetAssignmentService
{
    Task<AssetAssignmentDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<AssetAssignmentSummaryDto>> GetAllAsync();
    Task<PagedResult<AssetAssignmentSummaryDto>> GetPagedAsync(int page, int pageSize, string? searchTerm = null, AssignmentStatus? status = null);
    Task<IEnumerable<AssetAssignmentSummaryDto>> GetByAssetIdAsync(Guid assetId);
    Task<AssetAssignmentDto?> GetActiveAssignmentForAssetAsync(Guid assetId);
    Task<IEnumerable<AssetAssignmentSummaryDto>> GetByEmployeeIdAsync(Guid employeeId);
    Task<IEnumerable<AssetAssignmentSummaryDto>> GetActiveAssignmentsForEmployeeAsync(Guid employeeId);
    /// <summary>Custodies past their expected return date, most overdue first.</summary>
    Task<IEnumerable<AssetAssignmentSummaryDto>> GetOverdueAssignmentsAsync(DateOnly? asOf = null);

    /// <summary>
    /// Custodies coming due back inside a window — slice 11, the plan to the overdue exception
    /// list. Anything already late belongs to the other read.
    /// </summary>
    Task<IEnumerable<AssetAssignmentSummaryDto>> GetAssignmentsDueForReturnAsync(
        int daysAhead = 14, DateOnly? asOf = null);
    Task<AssetAssignmentDto> CreateAsync(CreateAssetAssignmentDto dto);
    Task<AssetAssignmentDto> UpdateAsync(Guid id, UpdateAssetAssignmentDto dto);
    Task DeleteAsync(Guid id);
    Task AcknowledgeAssignmentAsync(AcknowledgeAssignmentDto dto);
    Task ReturnAssetAsync(ReturnAssetDto dto);

    /// <summary>
    /// Closes a custody that ended badly — the asset was lost, or damaged beyond returning.
    /// </summary>
    /// <remarks>
    /// The counterpart of <see cref="ReturnAssetAsync"/> for the assets that never come back.
    /// <c>AssignmentStatus.Lost</c> and <c>.Damaged</c> existed from the port with no writer
    /// anywhere, so until slice 7 the only way to record a lost asset was to pretend it had been
    /// returned.
    /// </remarks>
    Task ReportIncidentAsync(ReportAssetIncidentDto dto);

    /// <summary>Declares what an employee is charged for holding a rentable asset — AST-9/AST-10.</summary>
    Task<AssetAssignmentDto> SetRentalTermsAsync(Guid id, SetAssetRentalTermsDto dto);

    /// <summary>Removes terms declared in error. A tenancy that ran is ended with a date instead.</summary>
    Task<AssetAssignmentDto> ClearRentalTermsAsync(Guid id);

    /// <summary>
    /// The read-only projection payroll pulls — decision D2. HR declares; payroll deducts.
    /// </summary>
    Task<IEnumerable<AssetRentalPayrollLineDto>> GetRentalPayrollLinesAsync(
        DateOnly periodStart, DateOnly periodEnd);
}

#endregion

#region Asset Maintenance Services

public interface IAssetMaintenanceService
{
    Task<AssetMaintenanceDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<AssetMaintenanceSummaryDto>> GetAllAsync();
    Task<PagedResult<AssetMaintenanceSummaryDto>> GetPagedAsync(int page, int pageSize, string? searchTerm = null, MaintenanceStatus? status = null);
    Task<IEnumerable<AssetMaintenanceSummaryDto>> GetByAssetIdAsync(Guid assetId);
    Task<IEnumerable<AssetMaintenanceSummaryDto>> GetScheduledMaintenanceAsync(DateTime from, DateTime to);
    Task<AssetMaintenanceDto> CreateAsync(CreateAssetMaintenanceDto dto);
    Task<AssetMaintenanceDto> UpdateAsync(Guid id, UpdateAssetMaintenanceDto dto);
    Task DeleteAsync(Guid id);
    /// <summary>
    /// Records the work as done, returns the asset to service, and advances the schedule — AST-1.
    /// </summary>
    /// <remarks>
    /// See <c>AssetMaintenanceService.CompleteMaintenanceAsync</c> for what each of those three
    /// clauses had to be fixed to mean (defects D-aa, D-bb, D-cc).
    /// </remarks>
    Task CompleteMaintenanceAsync(Guid id, string? completionNotes = null);

    // ── the push — slice 9b ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sends an asset to the workshop: raises an <c>AssetAdmission</c> in the Maintenance module and
    /// opens the HR maintenance record that tracks it. AST-1, decision D10.
    /// </summary>
    /// <remarks>
    /// Goes through <c>AssetAdmission</c> rather than <c>WorkOrder</c> or <c>JobCard</c> because
    /// those need <c>WorkOrderType</c>, <c>MaintenanceType</c> and <c>PriorityLevel</c> rows, and all
    /// three tables are empty — which is why the Projects module's equivalent throws for every
    /// tenant (cross-module defect 9). An admission needs no reference data at all.
    /// </remarks>
    Task<AssetMaintenanceDto> SendForMaintenanceAsync(Guid assetId, SendAssetForMaintenanceDto dto);

    /// <summary>Every workshop admission raised for an asset, newest first.</summary>
    Task<IEnumerable<AssetMaintenanceSummaryDto>> GetWorkshopHistoryAsync(Guid assetId);
}

#endregion

#region Asset Attachment Services

public interface IAssetAttachmentService
{
    Task<AssetAttachmentDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<AssetAttachmentDto>> GetByAssetIdAsync(Guid assetId);
    Task<AssetAttachmentDto> CreateAsync(Guid assetId, CreateAssetAttachmentDto dto);
    Task DeleteAsync(Guid id);
}

public interface IAssetImageService
{
    Task<AssetImageDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<AssetImageDto>> GetByAssetIdAsync(Guid assetId);
    Task<AssetImageDto> CreateAsync(Guid assetId, CreateAssetImageDto dto);
    Task DeleteAsync(Guid id);
}

#endregion

#region Asset Requisition Services

public interface IAssetRequisitionService
{
    Task<AssetRequisitionDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<AssetRequisitionSummaryDto>> GetAllAsync();
    Task<PagedResult<AssetRequisitionSummaryDto>> GetPagedAsync(int page, int pageSize, string? searchTerm = null, AssetRequisitionStatus? status = null);
    Task<IEnumerable<AssetRequisitionSummaryDto>> GetByRequestedByIdAsync(Guid employeeId);

    /// <summary>
    /// Every requisition the employee is a party to — raised by them or for them (AST-6b).
    /// </summary>
    Task<IEnumerable<AssetRequisitionSummaryDto>> GetForEmployeeAsync(Guid employeeId);
    Task<IEnumerable<AssetRequisitionSummaryDto>> GetPendingApprovalsAsync();
    Task<AssetRequisitionDto> CreateAsync(CreateAssetRequisitionDto dto);
    Task<AssetRequisitionDto> UpdateAsync(Guid id, UpdateAssetRequisitionDto dto);
    Task DeleteAsync(Guid id);

    /// <summary>Sends a draft requisition for approval (D3 - the workflow engine).</summary>
    Task<AssetRequisitionDto> SubmitAsync(Guid id);

    /// <summary>Pulls a submitted requisition back to draft, for the requester alone.</summary>
    Task<AssetRequisitionDto> RecallAsync(Guid id, string? reason);

    Task ApproveAsync(Guid id, ApproveAssetRequisitionDto dto);
    Task RejectAsync(Guid id, RejectAssetRequisitionDto dto);
    Task FulfillAsync(Guid id, FulfillAssetRequisitionDto dto);
}

#endregion

#region Asset Transfer Services

public interface IAssetTransferService
{
    Task<AssetTransferDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<AssetTransferSummaryDto>> GetAllAsync();
    Task<PagedResult<AssetTransferSummaryDto>> GetPagedAsync(int page, int pageSize, string? searchTerm = null, HRAssetTransferStatus? status = null);
    Task<IEnumerable<AssetTransferSummaryDto>> GetByAssetIdAsync(Guid assetId);
    Task<IEnumerable<AssetTransferSummaryDto>> GetPendingTransfersAsync();
    Task<AssetTransferDto> CreateAsync(CreateAssetTransferDto dto);
    Task<AssetTransferDto> UpdateAsync(Guid id, UpdateAssetTransferDto dto);
    Task DeleteAsync(Guid id);

    /// <summary>Sends a draft transfer for approval (D3 - the workflow engine).</summary>
    Task<AssetTransferDto> SubmitAsync(Guid id);

    /// <summary>Pulls a submitted transfer back to draft, for the initiator alone.</summary>
    Task<AssetTransferDto> RecallAsync(Guid id, string? reason);

    Task ApproveAsync(Guid id);
    Task CompleteAsync(Guid id);
    Task RejectAsync(Guid id);
}

#endregion

