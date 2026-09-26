using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

/// <summary>
/// Service interface for leave request management
/// </summary>
public interface ILeaveService
{
    Task<LeaveRequestDto> CreateLeaveRequestAsync(CreateLeaveRequestDto dto);
    Task<LeaveRequestDto> UpdateDraftAsync(Guid id, CreateLeaveRequestDto dto);

    /// <summary>
    /// What a request for these dates would ask of its leave type, and of annual leave for the days
    /// beyond the type's limit (round 5, lane H). Saves nothing; the request forms read it.
    /// </summary>
    Task<LeaveExcessPreviewDto> PreviewExcessAsync(
        Guid employeeId, Guid leaveTypeId, Guid? leaveSubTypeId, DateOnly startDate, DateOnly endDate);

    Task<bool> SubmitForApprovalAsync(Guid id);
    Task<LeaveRequestDto> ApproveLeaveAsync(Guid id, ApproveLeaveDto dto);
    Task<LeaveRequestDto> RejectLeaveAsync(Guid id, RejectLeaveDto dto);

    /// <summary>Send a submitted request back with dates of the approver's own (ChangesSuggested).</summary>
    Task<LeaveRequestDto> SuggestChangesAsync(Guid id, SuggestLeaveRequestChangesDto dto);

    /// <summary>The employee accepts the suggested dates, or counters with their own; either way it re-submits.</summary>
    Task<LeaveRequestDto> RespondToSuggestionAsync(Guid id, RespondToLeaveSuggestionDto dto);

    /// <summary>Move an APPROVED request to different dates, keeping its number and history. Re-opens the approval.</summary>
    Task<LeaveRequestDto> RescheduleAsync(Guid id, RescheduleLeaveRequestDto dto);

    /// <summary>Record that approved leave is still going ahead. Moves no days, changes no status.</summary>
    Task<LeaveRequestDto> ConfirmObservanceAsync(Guid id);

    /// <summary>
    /// Call an employee back before their leave ends. TRUNCATES: days taken stand, days after are
    /// restored, and the request keeps its number, its status and its approval.
    /// </summary>
    Task<LeaveRequestDto> RecallAsync(Guid id, RecallLeaveRequestDto dto);

    /// <summary>
    /// Points a leave request at the medical board that ruled on the absence, or unlinks it
    /// (residue plan G4). Pass null to unlink.
    /// </summary>
    /// <remarks>
    /// ⚠ Leave READS the board; it never writes one. This records which board a request rests on,
    /// which is what the evidence gate then checks. Without it the board arm of that gate would be
    /// unreachable — a rule nobody could satisfy, which is travel's T-23 shape.
    /// </remarks>
    Task<LeaveRequestDto> LinkMedicalBoardAsync(Guid id, Guid? medicalBoardId);
    Task<LeaveRequestDto> GetLeaveRequestByIdAsync(Guid id);
    Task<LeaveRequestDto?> GetLeaveRequestByNumberAsync(string applicationNumber);
    Task<PagedResult<LeaveRequestDto>> GetEmployeeLeaveHistoryAsync(Guid employeeId, int year, int pageNumber, int pageSize, LeaveStatus? status = null);
    /// <summary>
    /// ⚠ SUPERSEDED by <see cref="GetMyPendingApprovalsAsync"/>. Kept because existing callers and
    /// harnesses use it.
    /// </summary>
    /// <remarks>
    /// This answers "requests raised by this manager's direct reports that are still Pending",
    /// which is NOT the same question as "requests awaiting this manager's decision" — the gap the
    /// closure plan records as L-10. Under the two-stage ladder the difference became load-bearing:
    /// after the line manager approves, the request stays <c>Pending</c> while the engine sits at
    /// the HR step, so this query keeps showing it to the manager who already decided and never
    /// shows it to HR, who is nobody's <c>ManagerId</c>.
    /// </remarks>
    Task<PagedResult<LeaveRequestDto>> GetPendingApprovalsAsync(Guid managerId, int pageNumber, int pageSize);

    /// <summary>
    /// Requests actually awaiting the CALLER's decision, as the workflow engine sees it.
    /// </summary>
    /// <remarks>
    /// The queue asks the engine per candidate rather than inferring from the reporting line, so a
    /// two-stage definition puts each request in front of whoever owns the step it is ON. It also
    /// stops the screen being a way to read somebody else's queue: there is no approver parameter,
    /// because the answer is only ever about the caller.
    /// </remarks>
    Task<PagedResult<LeaveRequestDto>> GetMyPendingApprovalsAsync(
        int pageNumber, int pageSize, CancellationToken ct = default);
    /// <summary>
    /// One employee's balances for a year, annual leave first. With <paramref name="includeLiveAnnual"/>,
    /// an employee with no annual record yet gets their annual leave worked out live (round 5, lane J),
    /// marked <c>HasRecord = false</c>; nothing is created.
    /// </summary>
    Task<IEnumerable<LeaveBalanceDto>> GetEmployeeLeaveBalancesAsync(Guid employeeId, int year, bool includeLiveAnnual = false);
    Task<IEnumerable<LeaveBalanceDto>> GetAllLeaveBalancesAsync(int year, Guid? employeeId, Guid? leaveTypeId);

    /// <summary>
    /// Annual leave for every employee still serving and hired by the year's end (round 5, lane J):
    /// the stored record where there is one, the figures worked out live where there is none. Leavers
    /// are left out; a unit takes everything beneath it. Creates nothing.
    /// </summary>
    Task<IReadOnlyList<LeaveBalanceDto>> GetAnnualBalancesAsync(
        int year, Guid? employeeId, Guid? organizationUnitId, CancellationToken ct = default);
    Task<LeaveBalanceDetailDto?> GetLeaveBalanceDetailAsync(Guid balanceId);

    /// <summary>
    /// How a balance's accrual is worked out as at <paramref name="asOf"/> (today when null): the
    /// accrual statement (round 5, lane C2). <c>null</c> when the balance is not this tenant's.
    /// </summary>
    Task<LeaveAccrualStatementDto?> GetAccrualStatementAsync(Guid balanceId, DateOnly? asOf, CancellationToken ct = default);

    /// <summary>
    /// Annual leave built up and not yet taken, for every employee on the books at
    /// <paramref name="asOf"/> (today when null) — the "leave owed as at a date" report (round 5,
    /// lane C6, decision A7). Days only.
    /// </summary>
    Task<LeaveOwedReportDto> GetLeaveOwedAsync(DateOnly? asOf, CancellationToken ct = default);

    /// <summary>The same report as a CSV, one row per employee.</summary>
    Task<byte[]> ExportLeaveOwedCsvAsync(DateOnly? asOf, CancellationToken ct = default);
    Task<IEnumerable<MandatoryLeaveComplianceDto>> GetMandatoryLeaveComplianceAsync(int year);
    /// <summary>
    /// Cancel a request (round 5, R5-D3 + B2): the employee until it is approved; the desk also
    /// approved or started leave up to and including its first day, with a reason.
    /// </summary>
    /// <param name="actingAsDesk">The caller holds the leave write tier.</param>
    Task<bool> CancelLeaveRequestAsync(Guid id, string? cancellationReason, bool actingAsDesk);

    /// <summary>
    /// Confirm the employee's return, which closes the leave (round 5, B3): an early return cuts the
    /// leave short, a late one records the working days overstayed.
    /// </summary>
    Task<LeaveRequestDto> CloseLeaveRequestAsync(Guid id, CloseLeaveDto dto);

    /// <summary>"I'm back at work": the employee's own report, waiting for confirmation (round 5, B3).</summary>
    Task<LeaveRequestDto> ReportResumptionAsync(Guid id, ReportResumptionDto dto);

    /// <summary>
    /// Whether <paramref name="actorEmployeeId"/> manages <paramref name="subjectEmployeeId"/>: their
    /// supervisor, or the head of their unit or of any unit above it (TDC's definitions, finish plan
    /// lane 7). The authority that may recall them and confirm their return (round 5, B1/B3).
    /// </summary>
    Task<bool> IsLineAuthorityAsync(Guid subjectEmployeeId, Guid actorEmployeeId, CancellationToken ct = default);

    /// <summary>Returns the caller has been asked to confirm, as their line authority (round 5, B3).</summary>
    Task<IReadOnlyList<LeaveRequestDto>> GetResumptionsToConfirmAsync(Guid actorEmployeeId, CancellationToken ct = default);

    /// <summary>
    /// What the caller may do with this request, so a screen offers only what would succeed (round 5,
    /// lane D). The endpoints enforce the same rules on their own.
    /// </summary>
    /// <param name="actingAsDesk">The caller holds the leave write tier.</param>
    Task<LeaveRequestViewerActionsDto> GetViewerActionsAsync(
        LeaveRequestDto request, bool actingAsDesk, CancellationToken ct = default);

    /// <summary>
    /// Leave drawn as time rather than rows, for a date range and an audience.
    /// </summary>
    /// <remarks>
    /// The caller has already been authorized for <paramref name="scope"/> — the controller decides
    /// that, because "my team" needs the caller's own employee id and "the organisation" needs the
    /// leave read tier. This resolves the rows.
    /// </remarks>
    /// <summary>
    /// Makes the attendance register match a request's current status, whatever changed it.
    /// </summary>
    /// <remarks>
    /// The invariant: <c>OnLeave</c> attendance days exist for a request if and only if its status
    /// is Approved, InProgress or Completed. Leave's own transitions call this, and so does the
    /// nightly sweep — because a status can also be changed through doors leave does not own (the
    /// generic workflow recall calls the status adapter directly, bypassing this service entirely),
    /// and a hook per door is a list somebody eventually forgets to add to.
    /// </remarks>
    Task ReconcileAttendanceAsync(Guid leaveRequestId, CancellationToken ct = default);

    /// <summary>
    /// Reconciles every request whose status changed recently. Returns how many were put right.
    /// </summary>
    /// <remarks>
    /// A non-zero result is a BUG SIGNAL, not routine housekeeping — it means something moved a
    /// request without going through this service. It is logged as a warning for that reason.
    /// </remarks>
    Task<int> ReconcileRecentAttendanceAsync(
        Guid tenantId, int lookbackDays = 14, CancellationToken ct = default);

    /// <summary>
    /// Moves approved leave that has started into <c>InProgress</c>. Returns how many were advanced.
    /// </summary>
    /// <remarks>
    /// One-directional and balance-neutral — see the implementation. It is what gives the product a
    /// notion of leave that is currently happening, which recall (R-14) is defined against.
    /// </remarks>
    Task<int> AdvanceLeaveInProgressAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Approve or reject several requests, one real service call each.
    /// </summary>
    /// <remarks>
    /// ⚠ It loops <c>ApproveLeaveAsync</c> / <c>RejectLeaveAsync</c> rather than doing anything
    /// clever. Cross-module defect #15: the platform's generic approval surfaces drive the workflow
    /// engine WITHOUT invoking the module's status adapter, so a record approved that way completes
    /// its workflow and stays Submitted for ever. A bulk path that reached for the engine directly
    /// would reproduce that at scale — and it would also check authorization once for the batch
    /// instead of once per item, which is an access-control hole (bulk catalogue §4.1).
    /// </remarks>
    Task<HrBulkActionResultDto> BulkDecideAsync(
        BulkLeaveDecisionDto dto, bool approve, CancellationToken ct = default);

    /// <summary>
    /// The organisation-wide leave register: every request matching the filter, paged.
    /// </summary>
    Task<PagedResult<LeaveRequestDto>> GetRegisterAsync(
        LeaveRegisterFilterDto filter, int pageNumber, int pageSize, CancellationToken ct = default);

    /// <summary>The same register as a CSV, unpaged. Capped so one click cannot pull a million rows.</summary>
    Task<byte[]> ExportRegisterCsvAsync(LeaveRegisterFilterDto filter, CancellationToken ct = default);

    /// <summary>The balances screen as a CSV, with the same filters it offers.</summary>
    Task<byte[]> ExportBalancesCsvAsync(int year, Guid? employeeId, Guid? leaveTypeId, CancellationToken ct = default);

    /// <summary>The annual view of the balances screen as a CSV (round 5, lane J): the same rows.</summary>
    Task<byte[]> ExportAnnualBalancesCsvAsync(
        int year, Guid? employeeId, Guid? organizationUnitId, CancellationToken ct = default);

    /// <summary>The mandatory-leave compliance register as a CSV.</summary>
    Task<byte[]> ExportComplianceCsvAsync(int year, CancellationToken ct = default);

    /// <summary>Leave drawn as time. The three scopes are described on <c>LeavesController.GetCalendar</c>.</summary>
    /// <param name="callerEmployeeId">Who <c>Mine</c> and <c>Team</c> are measured from: the token's
    /// employee, never a query parameter.</param>
    /// <param name="organizationUnitId">Organisation scope only: the unit and every unit beneath it.</param>
    /// <param name="onlyEmployeeId">One employee's calendar (round 5 lane F). It narrows whatever the
    /// scope shows and can never widen it.</param>
    Task<LeaveCalendarDto> GetCalendarAsync(
        DateOnly from, DateOnly to, LeaveCalendarScope scope, Guid? callerEmployeeId,
        Guid? leaveTypeId, Guid? organizationUnitId, Guid? onlyEmployeeId, CancellationToken ct = default);

    // ─── Leave Adjustments ───────────────────────────────────────────────────
    Task<LeaveAdjustmentDto> AddAdjustmentAsync(CreateLeaveAdjustmentDto dto);
    Task<IEnumerable<LeaveAdjustmentDto>> GetAdjustmentsAsync(Guid balanceId);
    Task DeleteAdjustmentAsync(Guid adjustmentId);

    // ─── Standalone Adjustment Admin ─────────────────────────────────────────
    Task<IEnumerable<LeaveAdjustmentDto>> GetAllAdjustmentsAsync(int year, Guid? employeeId, Guid? leaveTypeId, string? search);
    Task<LeaveAdjustmentDto?> GetAdjustmentByIdAsync(Guid id);
    Task<LeaveAdjustmentDto> CreateStandaloneAdjustmentAsync(CreateLeaveAdjustmentStandaloneDto dto);
    Task<LeaveAdjustmentDto> UpdateAdjustmentAsync(Guid id, UpdateLeaveAdjustmentDto dto);

    // ─── Attachments ─────────────────────────────────────────────────────────
    /// <summary>
    /// Records an attachment against a leave request. The upload/DMS identifiers come from
    /// <c>IHrControlledDocumentService</c>; <paramref name="filePath"/> stays empty for new
    /// rows and is only populated on pre-migration data.
    /// </summary>
    /// <remarks>
    /// <paramref name="evidenceKind"/> is what makes the R-15a gate possible: it can ask whether a
    /// document of the right KIND is present, which no list of file names could answer.
    /// </remarks>
    Task<LeaveRequestAttachmentDto> UploadAttachmentAsync(Guid leaveRequestId, Guid uploadedBy, string fileName, string filePath, string? contentType, long? fileSizeBytes, Guid? fileUploadRecordId = null, Guid? documentRecordId = null, Guid? documentVersionId = null, LeaveEvidenceKind evidenceKind = LeaveEvidenceKind.Other);
    Task<IEnumerable<LeaveRequestAttachmentDto>> GetAttachmentsAsync(Guid leaveRequestId);
    Task<LeaveRequestAttachmentDto?> GetAttachmentByIdAsync(Guid attachmentId);
    Task<bool> DeleteAttachmentAsync(Guid attachmentId);
}

/// <summary>
/// Service interface for leave type configuration
/// </summary>
public interface ILeaveTypeService
{
    Task<IEnumerable<LeaveTypeDto>> GetAllLeaveTypesAsync(bool activeOnly = true);
    Task<LeaveTypeDto> GetLeaveTypeByIdAsync(Guid id);
    Task<LeaveTypeDetailDto> GetLeaveTypeDetailAsync(Guid id);
    Task<LeaveTypeDto> CreateLeaveTypeAsync(CreateLeaveTypeDto dto);
    Task<LeaveTypeDto> UpdateLeaveTypeAsync(Guid id, UpdateLeaveTypeDto dto);
    Task DeactivateLeaveTypeAsync(Guid id);

    // Sub types
    Task<IEnumerable<LeaveSubTypeDto>> GetSubTypesAsync(Guid leaveTypeId, bool activeOnly = false);
    Task<LeaveSubTypeDto> CreateSubTypeAsync(CreateLeaveSubTypeDto dto);
    Task<LeaveSubTypeDto> UpdateSubTypeAsync(Guid id, CreateLeaveSubTypeDto dto);
    Task DeleteSubTypeAsync(Guid id);

    // Category allocations
    Task<IEnumerable<LeaveCategoryAllocationDto>> GetAllocationsAsync(Guid leaveTypeId);
    Task<LeaveCategoryAllocationDto> CreateAllocationAsync(CreateLeaveCategoryAllocationDto dto);
    Task<LeaveCategoryAllocationDto> UpdateAllocationAsync(Guid id, CreateLeaveCategoryAllocationDto dto);
    Task DeleteAllocationAsync(Guid id);

    // Eligibility rules
    Task<IEnumerable<LeaveTypeEligibilityDto>> GetEligibilityRulesAsync(Guid leaveTypeId);
    Task<LeaveTypeEligibilityDto> CreateEligibilityRuleAsync(CreateLeaveTypeEligibilityDto dto);
    Task DeleteEligibilityRuleAsync(Guid id);

    /// <summary>
    /// Returns true when the employee matches at least one eligibility rule for this leave type.
    /// If no rules are configured, all employees are considered eligible.
    /// For org-type rules (Level/Unit/Position) an optional Gender qualifier on the rule must
    /// also match; the Gender-only type requires an exact gender match and ignores org fields.
    /// </summary>
    Task<bool> IsEmployeeEligibleAsync(Guid leaveTypeId, Guid employeeId);

    // Accrual policies
    Task<IEnumerable<LeaveAccrualPolicyDto>> GetAccrualPoliciesAsync(Guid leaveTypeId);
    Task<LeaveAccrualPolicyDto> CreateAccrualPolicyAsync(CreateLeaveAccrualPolicyDto dto);
    Task<LeaveAccrualPolicyDto> UpdateAccrualPolicyAsync(Guid id, CreateLeaveAccrualPolicyDto dto);
    Task DeleteAccrualPolicyAsync(Guid id);
}

/// <summary>
/// Service interface for leave plan management
/// </summary>
public interface ILeavePlanService
{
    Task<IEnumerable<LeavePlanDto>> GetByEmployeeAndYearAsync(Guid employeeId, int year);
    Task<IEnumerable<LeavePlanDto>> GetByYearAsync(int year);
    Task<LeavePlanDto> GetByIdAsync(Guid id);
    Task<LeavePlanDto> CreateLeavePlanAsync(CreateLeavePlanDto dto);
    Task<LeavePlanDto> UpdateLeavePlanAsync(Guid id, CreateLeavePlanDto dto);
    Task<LeavePlanDto> SubmitLeavePlanAsync(Guid id);
    Task<LeavePlanDto> ApproveLeavePlanAsync(Guid id);
    Task<LeavePlanDto> RejectLeavePlanAsync(Guid id, string reason);
    Task<LeavePlanDto> SuggestChangesAsync(Guid id, SuggestLeavePlanChangesDto dto);
    Task<LeavePlanDto> RespondToSuggestionAsync(Guid id, RespondToLeaveSuggestionDto dto);

    /// <summary>
    /// Cancels a plan (round 5 lane E5). <paramref name="actingAsDesk"/> is true when the caller
    /// holds the leave write tier: the desk may also cancel an APPROVED plan no request has been
    /// raised from, and must say why. The employee may cancel their own plan until it is approved.
    /// A live approval is withdrawn either way.
    /// </summary>
    Task CancelLeavePlanAsync(Guid id, string? reason, bool actingAsDesk);

    /// <summary>
    /// The approver's one edit to a plan: its relievers (round 5 lane E3). Allowed while the plan
    /// is Submitted, ChangesSuggested or Approved; the caller's authority is the controller's check.
    /// </summary>
    Task<LeavePlanDto> UpdateRelieversAsync(Guid id, UpdateLeavePlanRelieversDto dto);

    /// <summary>
    /// Why <paramref name="relieverId"/> may not be free between the two dates: their own leave
    /// plans and requests, and other plans that already name them as reliever. For the plan form,
    /// before the plan exists; the register reads the same answer from <see cref="LeavePlanDto.RelieverClashes"/>.
    /// </summary>
    Task<IReadOnlyList<LeaveRelieverClashDto>> GetRelieverClashesAsync(
        Guid relieverId, DateOnly startDate, DateOnly endDate, Guid? excludePlanId = null);
}

/// <summary>
/// Service interface for leave encashment processing
/// </summary>
public interface ILeaveEncashmentService
{
    Task<LeaveEncashmentDto>              RequestEncashmentAsync(CreateLeaveEncashmentDto dto);
    Task<LeaveEncashmentDto>              ApproveEncashmentAsync(Guid id);
    Task<LeaveEncashmentDto>              RejectEncashmentAsync(Guid id, string reason);
    Task<LeaveEncashmentDto>              MarkAsProcessedAsync(Guid id, ProcessLeaveEncashmentDto dto);
    Task<IEnumerable<LeaveEncashmentDto>> GetEmployeeEncashmentsAsync(Guid employeeId, int year);
    Task<IEnumerable<LeaveEncashmentDto>> GetAllEncashmentsAsync(
        int year,
        Guid?     employeeId   = null,
        Guid?     leaveTypeId  = null,
        DateTime? from         = null,
        DateTime? to           = null,
        string?   search       = null);
    Task<LeaveEncashmentDto>              GetByIdAsync(Guid id);
}
