using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// The exit register (area 9b) — separations of every type, from resignation to summary dismissal.
/// FRD §A1.10 and §3.A.2.
/// </summary>
/// <remarks>
/// Slice 1 covers the record itself: raise, amend while draft, read, cancel. Approval (FR-HR-092),
/// clearance (FR-HR-091/183), settlement (FR-HR-184/185) and the effect on the employee master
/// record each arrive as their own slice with their own rule, deliberately rather than as a status
/// field a client can set.
/// </remarks>
public interface ISeparationService
{
    Task<PagedResult<EmployeeSeparationListDto>> GetPagedAsync(
        EmployeeSeparationQueryDto query, CancellationToken cancellationToken = default);

    Task<EmployeeSeparationDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Every separation on record for one employee, newest first. Usually zero or one.</summary>
    Task<IEnumerable<EmployeeSeparationListDto>> GetForEmployeeAsync(
        Guid employeeId, CancellationToken cancellationToken = default);

    Task<EmployeeSeparationDetailDto> CreateAsync(
        CreateEmployeeSeparationDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default);

    Task<EmployeeSeparationDetailDto> UpdateAsync(
        Guid id, UpdateEmployeeSeparationDto dto, CancellationToken cancellationToken = default);

    Task<EmployeeSeparationDetailDto> CancelAsync(
        Guid id, CancelEmployeeSeparationDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Names the medical board a medical retirement rests on, or clears it with null (round 5, lane
    /// K-II-a). ⚠ Draft only, medical retirement only, and the board's case about this employee must
    /// be decided and recommend medical retirement. Separation reads the board; it never writes one.
    /// </summary>
    Task<EmployeeSeparationDetailDto> LinkMedicalBoardAsync(
        Guid id, Guid? medicalBoardId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Move a draft separation into the approval queue, deriving whatever notice dates follow from
    /// what is already recorded. After this the notice facts are fixed, because the FR-HR-184
    /// settlement is computed from them.
    /// </summary>
    Task<EmployeeSeparationDetailDto> SubmitAsync(
        Guid id, SubmitEmployeeSeparationDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sign off a separation awaiting approval (FR-HR-092). Throws
    /// <see cref="UnauthorizedAccessException"/> when the caller is not entitled to decide this
    /// particular record — the MD may decide any, HR only a procedural one.
    /// </summary>
    /// <remarks>
    /// ⚠ This no longer settles the notice. See <see cref="RecordNoticeDecisionAsync"/>: approval
    /// is a yes/no plus a comment, which is all the generic workflow engine's approve action can
    /// carry, and a decision that changes what somebody is paid needed a home of its own.
    /// </remarks>
    Task<EmployeeSeparationDetailDto> ApproveAsync(
        Guid id, ApproveEmployeeSeparationDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Record what happens to any unserved notice — waived, paid in lieu, or neither. Taken by
    /// whoever may sign the separation, while it is awaiting approval or once approved, and always
    /// before the settlement is prepared.
    /// </summary>
    Task<EmployeeSeparationDetailDto> RecordNoticeDecisionAsync(
        Guid id, RecordSeparationNoticeDecisionDto dto, Guid? actorEmployeeId,
        CancellationToken cancellationToken = default);

    /// <summary>Refuse a separation awaiting approval. Same entitlement rule as approving.</summary>
    Task<EmployeeSeparationDetailDto> RejectAsync(
        Guid id, RejectEmployeeSeparationDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default);

    Task<IEnumerable<EmployeeSeparationDocumentDto>> GetDocumentsAsync(
        Guid separationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a file already stored and scanned by the controlled-upload gate against a
    /// separation. The caller owns the upload; this owns the domain row.
    /// </summary>
    Task<EmployeeSeparationDocumentDto> AttachDocumentAsync(
        Guid separationId,
        SeparationDocumentCategory category,
        string fileName,
        string filePath,
        Guid? fileUploadRecordId,
        Guid? documentRecordId,
        Guid? documentVersionId,
        string? description,
        Guid? uploadedByEmployeeId,
        CancellationToken cancellationToken = default);

    /// <summary>The document row, for serving the file back. Null when it is not this tenant's.</summary>
    Task<EmployeeSeparationDocument?> GetDocumentEntityAsync(
        Guid documentId, CancellationToken cancellationToken = default);

    Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises the exit that a disciplinary termination implies (decision D1: one pipeline, not two).
    /// </summary>
    /// <remarks>
    /// Returns the separation already linked to this action where one exists, and <c>null</c> where
    /// one could not be raised — an employee with a separation already in flight, typically. The
    /// caller must not treat null as a failure of the disciplinary decision, which stands either way.
    /// </remarks>
    Task<EmployeeSeparationDetailDto?> CreateFromDisciplinaryOutcomeAsync(
        Guid disciplinaryActionId,
        Guid employeeId,
        EmployeeTerminationType type,
        string? notes,
        Guid? actorEmployeeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds disciplinary terminations that never produced an exit, and raises the missing
    /// separations. Pass <paramref name="dryRun"/> to report without writing.
    /// </summary>
    /// <remarks>
    /// The repair for the defect this area was opened on. It raises separations; it does
    /// <b>not</b> terminate anybody — each of those exits still goes through clearance, signature
    /// and settlement like any other.
    /// </remarks>
    Task<DisciplinaryOrphanRepairDto> RepairDisciplinaryOrphansAsync(
        bool dryRun, Guid? actorEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes the separation and applies it to the employee's master record — staff status,
    /// termination date and reason, active contracts and the open position-history row.
    /// </summary>
    /// <remarks>
    /// Only from <c>SettlementApproved</c>: the employee record follows the payment, and payment
    /// follows Internal Audit's review (FR-HR-185). This is the step whose absence left 29
    /// disciplinary terminations sitting against employees who were all still Active.
    /// </remarks>
    Task<EmployeeSeparationDetailDto> CompleteSeparationAsync(
        Guid separationId, CancellationToken cancellationToken = default);

    // ── Retirement (FR-HR-093) ───────────────────────────────────────────────

    /// <summary>
    /// Employees reaching the compulsory retirement age within the horizon, and those already past
    /// it who are still on strength.
    /// </summary>
    /// <param name="withinDays">
    /// Defaults to the tenant's <c>RetirementCountdownLeadDays</c> (365).
    /// </param>
    /// <remarks>
    /// ⚠ Measured 2026-08-20: employee ages on the live tenant run <b>34 to 48</b>, so this
    /// legitimately answers with nothing. An empty list is the correct answer to "who is retiring",
    /// not a broken query.
    /// </remarks>
    Task<IEnumerable<UpcomingRetirementDto>> GetUpcomingRetirementsAsync(
        int? withinDays = null, bool includeOverdue = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// The same list for a named tenant, for callers with no authenticated user behind them — the
    /// nightly reminder sweep, which runs tenant by tenant.
    /// </summary>
    Task<IEnumerable<UpcomingRetirementDto>> GetUpcomingRetirementsForTenantAsync(
        Guid tenantId, int? withinDays = null, bool includeOverdue = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises a compulsory-retirement separation for everyone due within the horizon who has not
    /// got one already. System-initiated: no actor is stamped, because a birthday arriving is
    /// nobody's act.
    /// </summary>
    Task<RetirementSweepResultDto> RunRetirementSweepAsync(
        int? withinDays = null, CancellationToken cancellationToken = default);

    // ── The exit interview ───────────────────────────────────────────────────

    /// <summary>The interview for this separation, or null where none has been recorded.</summary>
    Task<SeparationExitInterviewDto?> GetExitInterviewAsync(
        Guid separationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Record or amend the interview. Refused before the separation is approved — an interview
    /// about an exit that may not happen puts words in the mouth of somebody who is not leaving.
    /// </summary>
    /// <remarks>
    /// Marking it declined <b>clears every answer</b>: a part-filled form later marked declined must
    /// not leave ratings behind to be averaged as though a real interview had produced them.
    /// </remarks>
    Task<SeparationExitInterviewDto> RecordExitInterviewAsync(
        Guid separationId, RecordExitInterviewDto dto, Guid? actorEmployeeId,
        CancellationToken cancellationToken = default);

    /// <summary>What the interviews say across a period — coverage, decline rate, reasons, ratings.</summary>
    Task<ExitInterviewThemesDto> GetExitInterviewThemesAsync(
        DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default);

    // ── Exit analytics ───────────────────────────────────────────────────────

    /// <summary>Exits, why they happened, and where the ones in flight are stuck.</summary>
    Task<SeparationAnalyticsDto> GetAnalyticsAsync(
        DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default);

    // ── Contract expiry ──────────────────────────────────────────────────────

    /// <summary>
    /// Employees whose active contract runs out within the horizon, and those whose contract has
    /// already run out while they are still on strength.
    /// </summary>
    /// <remarks>
    /// ⚠ Measured 2026-08-20: <b>0 of 202</b> active contracts carry an end date, so this answers
    /// with nothing on live data. Empty is correct, not broken.
    /// </remarks>
    Task<IEnumerable<UpcomingContractExpiryDto>> GetUpcomingContractExpiriesAsync(
        int? withinDays = null, bool includeOverdue = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// The same list for a named tenant, for the nightly reminder sweep.
    /// </summary>
    Task<IEnumerable<UpcomingContractExpiryDto>> GetUpcomingContractExpiriesForTenantAsync(
        Guid tenantId, int? withinDays = null, bool includeOverdue = true,
        CancellationToken cancellationToken = default);

    /// <summary>Raises a contract-expiry separation for everyone due who has not got one.</summary>
    Task<ContractExpirySweepResultDto> RunContractExpirySweepAsync(
        int? withinDays = null, CancellationToken cancellationToken = default);

    // ── Final settlement (FR-HR-184) ─────────────────────────────────────────

    /// <summary>
    /// Builds the settlement statement from what the system knows. Refused before clearance is
    /// complete — FR-HR-091 puts the clearance form ahead of computing entitlements.
    /// </summary>
    Task<SeparationSettlementDto> PrepareSettlementAsync(
        Guid separationId, Guid? actorEmployeeId, CancellationToken cancellationToken = default);

    Task<SeparationSettlementDto> GetSettlementAsync(
        Guid separationId, CancellationToken cancellationToken = default);

    Task<SeparationSettlementLineDto> AddSettlementLineAsync(
        Guid separationId, AddSettlementLineDto dto, CancellationToken cancellationToken = default);

    Task<SeparationSettlementLineDto> UpdateSettlementLineAsync(
        Guid lineId, UpdateSettlementLineDto dto, CancellationToken cancellationToken = default);

    Task<bool> DeleteSettlementLineAsync(Guid lineId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the statement for Internal Audit's review (FR-HR-185). Refused while any line could
    /// not be valued: a settlement is not finalised with an unknown amount showing as zero.
    /// </summary>
    Task<SeparationSettlementDto> FinaliseSettlementAsync(
        Guid separationId, FinaliseSettlementDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Internal Audit passes the settlement; payment may be released (FR-HR-185).
    /// </summary>
    Task<SeparationSettlementDto> ApproveSettlementReviewAsync(
        Guid separationId, ReviewSettlementDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Internal Audit sends the settlement back with findings. The statement becomes editable again
    /// and must be corrected and re-finalised — a return refuses the figures, not the separation.
    /// </summary>
    Task<SeparationSettlementDto> ReturnSettlementAsync(
        Guid separationId, ReviewSettlementDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default);

    // ── Clearance (FR-HR-091 / FR-HR-183) ────────────────────────────────────

    Task<IEnumerable<SeparationClearanceTemplateDto>> GetClearanceTemplatesAsync(
        bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<SeparationClearanceTemplateDto> CreateClearanceTemplateAsync(
        CreateSeparationClearanceTemplateDto dto, CancellationToken cancellationToken = default);

    Task<SeparationClearanceTemplateDto> UpdateClearanceTemplateAsync(
        Guid id, UpdateSeparationClearanceTemplateDto dto, CancellationToken cancellationToken = default);

    Task<bool> DeleteClearanceTemplateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates FR-HR-183's seven default lines for a tenant, skipping any that exist by name.</summary>
    Task<IEnumerable<SeparationClearanceTemplateDto>> SeedDefaultClearanceTemplatesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the separation's clearance form from the active catalogue. Refused where the
    /// separation is not yet approved, where clearance has already begun, or where the catalogue is
    /// empty — a form with no lines would report "cleared" having checked nothing.
    /// </summary>
    /// <remarks>
    /// The line marked <c>SourcesFromAssetRegister</c> is expanded from HR Assets — one extra line
    /// per thing the leaver has not given back, carrying any decided surcharge into the settlement
    /// (FR-HR-183, area 16 slice 10).
    /// </remarks>
    Task<SeparationClearanceDto> StartClearanceAsync(
        Guid separationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-reads the HR Assets register onto an in-progress clearance form: adds a line for anything
    /// the leaver has been issued since the form was drawn, and reprices the lines nobody has
    /// answered yet.
    /// </summary>
    /// <remarks>
    /// The form is drawn when the separation is approved and the notice period runs after it, so
    /// custody moves under it — an asset issued in the last fortnight would otherwise never appear,
    /// and a surcharge approved after the form was drawn would never reach the settlement. Answered
    /// lines are never touched: a clearance form is evidence, and rewriting a signed line is the
    /// one thing it must not do.
    /// </remarks>
    Task<SeparationClearanceDto> RefreshClearanceAssetsAsync(
        Guid separationId, CancellationToken cancellationToken = default);

    Task<SeparationClearanceDto> GetClearanceAsync(
        Guid separationId, CancellationToken cancellationToken = default);

    Task<SeparationClearanceItemDto> RecordClearanceItemAsync(
        Guid itemId, RecordClearanceItemDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the clearance — FR-HR-091's gate. Refused while any mandatory line is still pending
    /// or blocked, because entitlements are computed only after the form is complete.
    /// </summary>
    Task<EmployeeSeparationDetailDto> CompleteClearanceAsync(
        Guid separationId, CancellationToken cancellationToken = default);
}
