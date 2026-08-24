import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  AssetActionAck,
  AssetAssignment,
  AssetAssignmentSummary,
  AssetAttributeValue,
  AssetAttachment,
  AssetImage,
  AssetInsuranceWatchItem,
  AssetMaintenance,
  AssetMaintenanceDueItem,
  AssetMaintenanceSummary,
  AssetRegisterReport,
  AssetRegisterReportFilters,
  AssetReminderLogEntry,
  AssetReminderPreviewItem,
  AssetReminderRun,
  AssetReminderRunResult,
  AssetRentalPayrollLine,
  AssetRequisition,
  AssetRequisitionSummary,
  AssetSurcharge,
  AssetSurchargePayrollLine,
  AssetSurchargeSummary,
  AssetTermsLetter,
  AssetTransfer,
  AssetTransferSummary,
  AssetType,
  AssetTypeAttribute,
  AssetTypeDetail,
  AssetTypeSummary,
  CompanyAsset,
  CompanyAssetDetail,
  CompanyAssetStatus,
  CompanyAssetSummary,
  CreateAssetAssignmentRequest,
  CreateAssetMaintenanceRequest,
  CreateAssetTransferRequest,
  CreateAssetTypeAttributeRequest,
  CreateAssetTypeRequest,
  CreateCompanyAssetRequest,
  DisposeAssetRequest,
  FixedAssetPick,
  MaintenanceAssetPick,
  ReportAssetIncidentRequest,
  ReturnAssetRequest,
  SendAssetForMaintenanceRequest,
  SetAssetRentalTermsRequest,
  UpdateCompanyAssetRequest,
} from '@/types/hr/assets';

/**
 * The HR company-asset register — `api/Assets`, HR-gated end to end.
 *
 * Every route and every query-string key here was taken from
 * `dev-harness/hr-assets/slice12-ui-payloads.json`, which a probe writes by calling the live API.
 * Three of them are not what their neighbours would suggest, and each is marked below:
 *
 *   · `status/{status}` and the paged `status=` filters take the enum **NAME**, not the number,
 *     although every create and update payload takes the number.
 *   · `surcharges/paged` pages on **`page`**; every other paged read here uses `pageNumber`.
 *   · `maintenance/schedule` takes full `DateTime` bounds and requires both.
 *
 * Nothing in this file computes. The watchlists, the counts and every `daysRemaining` are answered
 * by the server as at a date it states, so a page left open overnight cannot start disagreeing with
 * the list it was opened from.
 */
class AssetRegisterService {
  private readonly baseUrl = '/Assets';
  private readonly remindersUrl = '/assets/reminders';

  // ── The register ───────────────────────────────────────────────────────────

  /** ⚠ `status` is the enum NAME (`'Available'`), not the number the write payloads take. */
  getAssetsPaged(params: {
    pageNumber?: number;
    pageSize?: number;
    searchTerm?: string;
    status?: CompanyAssetStatus;
    assetTypeId?: string;
  } = {}): Promise<PagedResult<CompanyAssetSummary>> {
    const q = new URLSearchParams();
    q.set('pageNumber', String(params.pageNumber ?? 1));
    q.set('pageSize', String(params.pageSize ?? 20));
    if (params.searchTerm) q.set('searchTerm', params.searchTerm);
    if (params.status) q.set('status', params.status);
    if (params.assetTypeId) q.set('assetTypeId', params.assetTypeId);
    return apiService.get<PagedResult<CompanyAssetSummary>>(`${this.baseUrl}/paged?${q.toString()}`);
  }

  getAsset(id: string): Promise<CompanyAsset> {
    return apiService.get<CompanyAsset>(`${this.baseUrl}/${id}`);
  }

  /** The record plus its attribute values, custody history, service log and attachments. */
  getAssetDetail(id: string): Promise<CompanyAssetDetail> {
    return apiService.get<CompanyAssetDetail>(`${this.baseUrl}/${id}/details`);
  }

  /** The picker on the assign form: assignable, not disposed, and not already in someone's hands. */
  getAvailableAssets(): Promise<CompanyAssetSummary[]> {
    return apiService.get<CompanyAssetSummary[]>(`${this.baseUrl}/available`);
  }

  getAssetsByEmployee(employeeId: string): Promise<CompanyAssetSummary[]> {
    return apiService.get<CompanyAssetSummary[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  createAsset(data: CreateCompanyAssetRequest): Promise<CompanyAsset> {
    return apiService.post<CompanyAsset>(this.baseUrl, data);
  }

  /**
   * ⚠ **Full replace** (defect D-j): a field left off the payload is written as null, and `id` must
   * be in the body as well as the route. Load the record, spread it, then change what you changed.
   */
  updateAsset(id: string, data: UpdateCompanyAssetRequest): Promise<CompanyAsset> {
    return apiService.put<CompanyAsset>(`${this.baseUrl}/${id}`, data);
  }

  /** A soft delete. ⚠ It is refused while the asset is out with somebody. */
  deleteAsset(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  disposeAsset(id: string, data: DisposeAssetRequest): Promise<CompanyAsset> {
    return apiService.post<CompanyAsset>(`${this.baseUrl}/${id}/dispose`, data);
  }

  getAttributeValues(assetId: string): Promise<AssetAttributeValue[]> {
    return apiService.get<AssetAttributeValue[]>(`${this.baseUrl}/${assetId}/attribute-values`);
  }

  getAttachments(assetId: string): Promise<AssetAttachment[]> {
    return apiService.get<AssetAttachment[]>(`${this.baseUrl}/${assetId}/attachments`);
  }

  getImages(assetId: string): Promise<AssetImage[]> {
    return apiService.get<AssetImage[]>(`${this.baseUrl}/${assetId}/images`);
  }

  // ── Types and their custom attributes ──────────────────────────────────────

  getTypes(): Promise<AssetTypeSummary[]> {
    return apiService.get<AssetTypeSummary[]>(`${this.baseUrl}/types`);
  }

  getTypeWithAttributes(id: string): Promise<AssetTypeDetail> {
    return apiService.get<AssetTypeDetail>(`${this.baseUrl}/types/${id}/with-attributes`);
  }

  createType(data: CreateAssetTypeRequest): Promise<AssetType> {
    return apiService.post<AssetType>(`${this.baseUrl}/types`, data);
  }

  updateType(id: string, data: CreateAssetTypeRequest & { id: string }): Promise<AssetType> {
    return apiService.put<AssetType>(`${this.baseUrl}/types/${id}`, data);
  }

  deleteType(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/types/${id}`);
  }

  getTypeAttributes(assetTypeId: string): Promise<AssetTypeAttribute[]> {
    return apiService.get<AssetTypeAttribute[]>(`${this.baseUrl}/types/${assetTypeId}/attributes`);
  }

  createTypeAttribute(
    assetTypeId: string,
    data: CreateAssetTypeAttributeRequest,
  ): Promise<AssetTypeAttribute> {
    return apiService.post<AssetTypeAttribute>(`${this.baseUrl}/types/${assetTypeId}/attributes`, data);
  }

  deleteTypeAttribute(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/attributes/${id}`);
  }

  // ── The two link pickers ───────────────────────────────────────────────────

  /** ⚠ Rows already claimed come back FLAGGED, not filtered — render them, disabled. */
  getLinkableFixedAssets(searchTerm?: string): Promise<FixedAssetPick[]> {
    const q = searchTerm ? `?searchTerm=${encodeURIComponent(searchTerm)}` : '';
    return apiService.get<FixedAssetPick[]>(`${this.baseUrl}/fixed-assets/linkable${q}`);
  }

  /** ⚠ The Maintenance register is all but empty on this tenant — expect an empty state. */
  getLinkableMaintenanceAssets(searchTerm?: string): Promise<MaintenanceAssetPick[]> {
    const q = searchTerm ? `?searchTerm=${encodeURIComponent(searchTerm)}` : '';
    return apiService.get<MaintenanceAssetPick[]>(`${this.baseUrl}/maintenance-assets/linkable${q}`);
  }

  linkMaintenanceAsset(id: string, maintenanceAssetId: string): Promise<CompanyAsset> {
    return apiService.post<CompanyAsset>(`${this.baseUrl}/${id}/maintenance-link`, { maintenanceAssetId });
  }

  unlinkMaintenanceAsset(id: string): Promise<CompanyAsset> {
    return apiService.delete<CompanyAsset>(`${this.baseUrl}/${id}/maintenance-link`);
  }

  sendForMaintenance(id: string, data: SendAssetForMaintenanceRequest): Promise<AssetMaintenance> {
    return apiService.post<AssetMaintenance>(`${this.baseUrl}/${id}/send-for-maintenance`, data);
  }

  getWorkshopHistory(id: string): Promise<AssetMaintenanceSummary[]> {
    return apiService.get<AssetMaintenanceSummary[]>(`${this.baseUrl}/${id}/workshop-history`);
  }

  // ── The three maintenance watchlists — AST-1 ───────────────────────────────

  /** ⚠ INCLUSIVE of the overdue rows. The report's `maintenanceDueSoonCount` is not. */
  getDueMaintenance(daysAhead = 30, asOf?: string): Promise<AssetMaintenanceDueItem[]> {
    const q = new URLSearchParams({ daysAhead: String(daysAhead) });
    if (asOf) q.set('asOf', asOf);
    return apiService.get<AssetMaintenanceDueItem[]>(`${this.baseUrl}/due-maintenance?${q.toString()}`);
  }

  getOverdueMaintenance(asOf?: string): Promise<AssetMaintenanceDueItem[]> {
    const q = asOf ? `?asOf=${asOf}` : '';
    return apiService.get<AssetMaintenanceDueItem[]>(`${this.baseUrl}/overdue-maintenance${q}`);
  }

  /** Needs regular servicing and has never been given a date — so it can never become due. */
  getUnscheduledMaintenance(): Promise<AssetMaintenanceDueItem[]> {
    return apiService.get<AssetMaintenanceDueItem[]>(`${this.baseUrl}/unscheduled-maintenance`);
  }

  // ── The three insurance watchlists — AST-4 ─────────────────────────────────

  /** ⚠ Default horizon 60 days, not maintenance's 30 — a renewal is a quote, an approval, a payment. */
  getInsuranceExpiring(daysAhead = 60, asOf?: string): Promise<AssetInsuranceWatchItem[]> {
    const q = new URLSearchParams({ daysAhead: String(daysAhead) });
    if (asOf) q.set('asOf', asOf);
    return apiService.get<AssetInsuranceWatchItem[]>(`${this.baseUrl}/insurance/expiring?${q.toString()}`);
  }

  getInsuranceExpired(asOf?: string): Promise<AssetInsuranceWatchItem[]> {
    const q = asOf ? `?asOf=${asOf}` : '';
    return apiService.get<AssetInsuranceWatchItem[]>(`${this.baseUrl}/insurance/expired${q}`);
  }

  getInsuranceUndated(): Promise<AssetInsuranceWatchItem[]> {
    return apiService.get<AssetInsuranceWatchItem[]>(`${this.baseUrl}/insurance/undated`);
  }

  // ── The register report ────────────────────────────────────────────────────

  /** ⚠ Its watchlist counts honour the filters — the printed header must say which filters. */
  getRegisterReport(filters: AssetRegisterReportFilters = {}): Promise<AssetRegisterReport> {
    const q = new URLSearchParams();
    if (filters.assetTypeId) q.set('assetTypeId', filters.assetTypeId);
    if (filters.unitId) q.set('unitId', filters.unitId);
    if (filters.locationId) q.set('locationId', filters.locationId);
    if (filters.status !== undefined) q.set('status', String(filters.status));
    if (filters.asOf) q.set('asOf', filters.asOf);
    const qs = q.toString();
    return apiService.get<AssetRegisterReport>(`${this.baseUrl}/reports/register${qs ? `?${qs}` : ''}`);
  }

  // ── Assignments, and the two return watchlists ─────────────────────────────

  getAssignmentsPaged(params: {
    pageNumber?: number;
    pageSize?: number;
    searchTerm?: string;
    /** ⚠ The enum NAME again — `'Active'`, not `1`. */
    status?: string;
  } = {}): Promise<PagedResult<AssetAssignmentSummary>> {
    const q = new URLSearchParams();
    q.set('pageNumber', String(params.pageNumber ?? 1));
    q.set('pageSize', String(params.pageSize ?? 20));
    if (params.searchTerm) q.set('searchTerm', params.searchTerm);
    if (params.status) q.set('status', params.status);
    return apiService.get<PagedResult<AssetAssignmentSummary>>(
      `${this.baseUrl}/assignments/paged?${q.toString()}`);
  }

  getAssignment(id: string): Promise<AssetAssignment> {
    return apiService.get<AssetAssignment>(`${this.baseUrl}/assignments/${id}`);
  }

  getAssignmentsByAsset(assetId: string): Promise<AssetAssignmentSummary[]> {
    return apiService.get<AssetAssignmentSummary[]>(`${this.baseUrl}/assignments/asset/${assetId}`);
  }

  /** What one person is holding right now. Exact — never a filtered page of everybody's. */
  getActiveAssignmentsForEmployee(employeeId: string): Promise<AssetAssignmentSummary[]> {
    return apiService.get<AssetAssignmentSummary[]>(
      `${this.baseUrl}/assignments/employee/${employeeId}/active`);
  }

  /** RETURN WATCHLIST 1 — the exception list, worst first. */
  getOverdueAssignments(asOf?: string): Promise<AssetAssignmentSummary[]> {
    const q = asOf ? `?asOf=${asOf}` : '';
    return apiService.get<AssetAssignmentSummary[]>(`${this.baseUrl}/assignments/overdue${q}`);
  }

  /** RETURN WATCHLIST 2 — the plan. ⚠ INCLUSIVE of the rows that are already late. */
  getAssignmentsDueForReturn(daysAhead = 14, asOf?: string): Promise<AssetAssignmentSummary[]> {
    const q = new URLSearchParams({ daysAhead: String(daysAhead) });
    if (asOf) q.set('asOf', asOf);
    return apiService.get<AssetAssignmentSummary[]>(
      `${this.baseUrl}/assignments/due-for-return?${q.toString()}`);
  }

  createAssignment(data: CreateAssetAssignmentRequest): Promise<AssetAssignment> {
    return apiService.post<AssetAssignment>(`${this.baseUrl}/assignments`, data);
  }

  /**
   * ⚠ `returnedToId` is required and is an EMPLOYEE id. Without it: 404 naming an empty GUID.
   *
   * ⚠ Answers `{ message }`, **not** the updated assignment — unlike every other write on this
   * service. Refetch the record; do not read fields off this response.
   */
  returnAsset(assignmentId: string, data: ReturnAssetRequest): Promise<AssetActionAck> {
    return apiService.post<AssetActionAck>(`${this.baseUrl}/assignments/${assignmentId}/return`, data);
  }

  /** Recording that an asset never came back at all — the only path to a loss charge. */
  reportIncident(assignmentId: string, data: ReportAssetIncidentRequest): Promise<AssetActionAck> {
    return apiService.post<AssetActionAck>(
      `${this.baseUrl}/assignments/${assignmentId}/report-incident`, data);
  }

  deleteAssignment(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/assignments/${id}`);
  }

  /**
   * The responsibility-and-terms document — AST-5.
   *
   * `htmlBody` is a complete `<html>` document. Open it in its own window; injecting a whole letter
   * into a screen's DOM is how a print stylesheet ends up printing the navigation.
   */
  getTermsDocument(assignmentId: string): Promise<AssetTermsLetter> {
    return apiService.get<AssetTermsLetter>(
      `${this.baseUrl}/assignments/${assignmentId}/terms-document`);
  }

  emailTermsDocument(assignmentId: string, recipientEmail?: string): Promise<unknown> {
    return apiService.post(`${this.baseUrl}/assignments/${assignmentId}/terms-document/email`,
      { recipientEmail: recipientEmail ?? null });
  }

  setRentalTerms(assignmentId: string, data: SetAssetRentalTermsRequest): Promise<AssetAssignment> {
    return apiService.put<AssetAssignment>(
      `${this.baseUrl}/assignments/${assignmentId}/rental-terms`, data);
  }

  /** Clearing the terms is a DELETE — an amount of zero means "provided free", which is different. */
  clearRentalTerms(assignmentId: string): Promise<AssetAssignment> {
    return apiService.delete<AssetAssignment>(
      `${this.baseUrl}/assignments/${assignmentId}/rental-terms`);
  }

  // ── The service log ────────────────────────────────────────────────────────

  getMaintenancePaged(params: {
    pageNumber?: number;
    pageSize?: number;
    searchTerm?: string;
    status?: string;
  } = {}): Promise<PagedResult<AssetMaintenanceSummary>> {
    const q = new URLSearchParams();
    q.set('pageNumber', String(params.pageNumber ?? 1));
    q.set('pageSize', String(params.pageSize ?? 20));
    if (params.searchTerm) q.set('searchTerm', params.searchTerm);
    if (params.status) q.set('status', params.status);
    return apiService.get<PagedResult<AssetMaintenanceSummary>>(
      `${this.baseUrl}/maintenance/paged?${q.toString()}`);
  }

  getMaintenance(id: string): Promise<AssetMaintenance> {
    return apiService.get<AssetMaintenance>(`${this.baseUrl}/maintenance/${id}`);
  }

  getMaintenanceByAsset(assetId: string): Promise<AssetMaintenanceSummary[]> {
    return apiService.get<AssetMaintenanceSummary[]>(`${this.baseUrl}/maintenance/asset/${assetId}`);
  }

  createMaintenance(data: CreateAssetMaintenanceRequest): Promise<AssetMaintenance> {
    return apiService.post<AssetMaintenance>(`${this.baseUrl}/maintenance`, data);
  }

  /** Completing a job re-dates the asset from its own interval, and discharges any workshop stay. */
  completeMaintenance(id: string, notes: string): Promise<AssetMaintenance> {
    return apiService.post<AssetMaintenance>(`${this.baseUrl}/maintenance/${id}/complete`, notes);
  }

  deleteMaintenance(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/maintenance/${id}`);
  }

  // ── Requisitions ───────────────────────────────────────────────────────────

  getRequisitionsPaged(params: {
    pageNumber?: number;
    pageSize?: number;
    searchTerm?: string;
    status?: string;
  } = {}): Promise<PagedResult<AssetRequisitionSummary>> {
    const q = new URLSearchParams();
    q.set('pageNumber', String(params.pageNumber ?? 1));
    q.set('pageSize', String(params.pageSize ?? 20));
    if (params.searchTerm) q.set('searchTerm', params.searchTerm);
    if (params.status) q.set('status', params.status);
    return apiService.get<PagedResult<AssetRequisitionSummary>>(
      `${this.baseUrl}/requisitions/paged?${q.toString()}`);
  }

  getRequisition(id: string): Promise<AssetRequisition> {
    return apiService.get<AssetRequisition>(`${this.baseUrl}/requisitions/${id}`);
  }

  /** The approver's queue — what the workflow engine has routed to the signed-in user. */
  getPendingRequisitionApprovals(): Promise<AssetRequisitionSummary[]> {
    return apiService.get<AssetRequisitionSummary[]>(`${this.baseUrl}/requisitions/pending-approvals`);
  }

  approveRequisition(id: string, approvalComments?: string): Promise<AssetRequisition> {
    return apiService.post<AssetRequisition>(`${this.baseUrl}/requisitions/${id}/approve`,
      { approvalComments: approvalComments ?? null });
  }

  rejectRequisition(id: string, rejectionReason: string): Promise<AssetRequisition> {
    return apiService.post<AssetRequisition>(`${this.baseUrl}/requisitions/${id}/reject`,
      { rejectionReason });
  }

  /** Records which assignments actually satisfied the request — AST-6, defect D-e. */
  fulfillRequisition(id: string, assignmentIds: string[]): Promise<AssetRequisition> {
    return apiService.post<AssetRequisition>(`${this.baseUrl}/requisitions/${id}/fulfill`,
      { assignmentIds });
  }

  // ── Transfers ──────────────────────────────────────────────────────────────

  getTransfersPaged(params: {
    pageNumber?: number;
    pageSize?: number;
    searchTerm?: string;
    status?: string;
  } = {}): Promise<PagedResult<AssetTransferSummary>> {
    const q = new URLSearchParams();
    q.set('pageNumber', String(params.pageNumber ?? 1));
    q.set('pageSize', String(params.pageSize ?? 20));
    if (params.searchTerm) q.set('searchTerm', params.searchTerm);
    if (params.status) q.set('status', params.status);
    return apiService.get<PagedResult<AssetTransferSummary>>(
      `${this.baseUrl}/transfers/paged?${q.toString()}`);
  }

  getTransfer(id: string): Promise<AssetTransfer> {
    return apiService.get<AssetTransfer>(`${this.baseUrl}/transfers/${id}`);
  }

  getPendingTransfers(): Promise<AssetTransferSummary[]> {
    return apiService.get<AssetTransferSummary[]>(`${this.baseUrl}/transfers/pending`);
  }

  createTransfer(data: CreateAssetTransferRequest): Promise<AssetTransfer> {
    return apiService.post<AssetTransfer>(`${this.baseUrl}/transfers`, data);
  }

  submitTransfer(id: string): Promise<AssetTransfer> {
    return apiService.post<AssetTransfer>(`${this.baseUrl}/transfers/${id}/submit`, {});
  }

  recallTransfer(id: string, reason: string): Promise<AssetTransfer> {
    return apiService.post<AssetTransfer>(`${this.baseUrl}/transfers/${id}/recall`, { reason });
  }

  approveTransfer(id: string, approvalComments?: string): Promise<AssetTransfer> {
    return apiService.post<AssetTransfer>(`${this.baseUrl}/transfers/${id}/approve`,
      { approvalComments: approvalComments ?? null });
  }

  rejectTransfer(id: string, rejectionReason: string): Promise<AssetTransfer> {
    return apiService.post<AssetTransfer>(`${this.baseUrl}/transfers/${id}/reject`, { rejectionReason });
  }

  /** Completing is what actually MOVES the asset — approval alone changes nothing in the register. */
  completeTransfer(id: string, completionNotes?: string): Promise<AssetTransfer> {
    return apiService.post<AssetTransfer>(`${this.baseUrl}/transfers/${id}/complete`,
      { completionNotes: completionNotes ?? null });
  }

  deleteTransfer(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/transfers/${id}`);
  }

  // ── Surcharges — AST-3, decision D9 ────────────────────────────────────────

  /** ⚠ This one pages on `page`, not `pageNumber`. Measured, not assumed. */
  getSurchargesPaged(params: {
    page?: number;
    pageSize?: number;
    searchTerm?: string;
    status?: string;
  } = {}): Promise<PagedResult<AssetSurchargeSummary>> {
    const q = new URLSearchParams();
    q.set('page', String(params.page ?? 1));
    q.set('pageSize', String(params.pageSize ?? 20));
    if (params.searchTerm) q.set('searchTerm', params.searchTerm);
    if (params.status) q.set('status', params.status);
    return apiService.get<PagedResult<AssetSurchargeSummary>>(
      `${this.baseUrl}/surcharges/paged?${q.toString()}`);
  }

  getSurcharge(id: string): Promise<AssetSurcharge> {
    return apiService.get<AssetSurcharge>(`${this.baseUrl}/surcharges/${id}`);
  }

  /** Approved, not fully recovered, not waived — the money actually still owed. */
  getOutstandingSurcharges(): Promise<AssetSurchargeSummary[]> {
    return apiService.get<AssetSurchargeSummary[]>(`${this.baseUrl}/surcharges/outstanding`);
  }

  getSurchargesForAssignment(assignmentId: string): Promise<AssetSurchargeSummary[]> {
    return apiService.get<AssetSurchargeSummary[]>(
      `${this.baseUrl}/assignments/${assignmentId}/surcharges`);
  }

  createSurcharge(data: {
    assignmentId: string;
    /** 1 = Damage, 2 = Loss, 3 = NotReturned, 4 = Other. */
    reason: number;
    description: string;
    assessedAmount?: number | null;
  }): Promise<AssetSurcharge> {
    return apiService.post<AssetSurcharge>(`${this.baseUrl}/surcharges`, data);
  }

  /** Serving the charge on the employee, which is what opens their right of reply — D9. */
  notifySurchargeEmployee(id: string): Promise<AssetSurcharge> {
    return apiService.post<AssetSurcharge>(`${this.baseUrl}/surcharges/${id}/notify-employee`, {});
  }

  submitSurcharge(id: string, proceededWithoutResponseReason?: string): Promise<AssetSurcharge> {
    return apiService.post<AssetSurcharge>(`${this.baseUrl}/surcharges/${id}/submit`,
      { proceededWithoutResponseReason: proceededWithoutResponseReason ?? null });
  }

  /** ⚠ The approver may LOWER the amount but never raise it — the API refuses the raise in words. */
  approveSurcharge(id: string, data: { approvedAmount?: number | null; approvalComments?: string | null }):
    Promise<AssetSurcharge> {
    return apiService.post<AssetSurcharge>(`${this.baseUrl}/surcharges/${id}/approve`, data);
  }

  rejectSurcharge(id: string, rejectionReason: string): Promise<AssetSurcharge> {
    return apiService.post<AssetSurcharge>(`${this.baseUrl}/surcharges/${id}/reject`, { rejectionReason });
  }

  /** A statement of intent to payroll, not a schedule HR runs. */
  setSurchargeRecoveryPlan(id: string, data: {
    /** 1 = PayrollDeduction, 2 = DirectPayment, 3 = ExitSettlement. */
    recoveryMethod: number;
    instalmentCount?: number | null;
    recoveryStartDate?: string | null;
  }): Promise<AssetSurcharge> {
    return apiService.put<AssetSurcharge>(`${this.baseUrl}/surcharges/${id}/recovery-plan`, data);
  }

  waiveSurcharge(id: string, waiverReason: string): Promise<AssetSurcharge> {
    return apiService.post<AssetSurcharge>(`${this.baseUrl}/surcharges/${id}/waive`, { waiverReason });
  }

  /** ⚠ Once served, a charge is CANCELLED, never deleted — the delete answers 409, by design. */
  cancelSurcharge(id: string, cancellationReason: string): Promise<AssetSurcharge> {
    return apiService.post<AssetSurcharge>(`${this.baseUrl}/surcharges/${id}/cancel`, { cancellationReason });
  }

  // ── The two payroll projections. Read-only; HR declares, payroll deducts. ──

  getSurchargePayrollLines(): Promise<AssetSurchargePayrollLine[]> {
    return apiService.get<AssetSurchargePayrollLine[]>(`${this.baseUrl}/surcharges/payroll-deductions`);
  }

  /** ⚠ NOT prorated — every line is the full periodic rate plus the window it applies to. */
  getRentalPayrollLines(period?: string): Promise<AssetRentalPayrollLine[]> {
    const q = period ? `?period=${encodeURIComponent(period)}` : '';
    return apiService.get<AssetRentalPayrollLine[]>(`${this.baseUrl}/payroll/rental-deductions${q}`);
  }

  // ── The reminder engine — its own controller, HR-gated as a whole ──────────

  /** What a sweep would fire. Changes nothing, and claims no dedupe key. */
  previewReminders(asOf?: string): Promise<AssetReminderPreviewItem[]> {
    const q = asOf ? `?asOf=${encodeURIComponent(asOf)}` : '';
    return apiService.get<AssetReminderPreviewItem[]>(`${this.remindersUrl}/preview${q}`);
  }

  /** Safe to repeat — dispatch is deduped per item. */
  runReminderSweep(): Promise<AssetReminderRunResult> {
    return apiService.post<AssetReminderRunResult>(`${this.remindersUrl}/run`, {});
  }

  getReminderRuns(count = 20): Promise<AssetReminderRun[]> {
    return apiService.get<AssetReminderRun[]>(`${this.remindersUrl}/runs?count=${count}`);
  }

  getReminderLog(days = 14): Promise<AssetReminderLogEntry[]> {
    return apiService.get<AssetReminderLogEntry[]>(`${this.remindersUrl}/log?days=${days}`);
  }
}

export const assetRegisterService = new AssetRegisterService();
