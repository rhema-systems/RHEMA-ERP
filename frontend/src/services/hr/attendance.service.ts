import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  StaffDailyAttendance,
  StaffDailyAttendanceSummary,
  CreateStaffDailyAttendance,
  UpdateStaffDailyAttendance,
  VerifyAttendanceRequest,
  ApproveAttendanceExceptionRequest,
  StaffAttendanceRecord,
  StaffAttendanceRecordSummary,
  CreateStaffAttendanceRecord,
  UpdateStaffAttendanceRecord,
  StaffAttendanceLog,
  StaffAttendanceLogSummary,
  CreateStaffAttendanceLog,
  StaffAttendancePunchRequest,
  StaffAttendancePunchResult,
  StaffAttendanceRegularization,
  StaffAttendanceRegularizationSummary,
  CreateStaffAttendanceRegularization,
  UpdateStaffAttendanceRegularization,
  ApproveRegularizationRequest,
  RejectRegularizationRequest,
  StaffOvertimeRequest,
  StaffOvertimeRequestSummary,
  CreateStaffOvertimeRequest,
  ApproveOvertimeRequest,
  RejectOvertimeRequest,
  ConfirmOvertimeRequest,
  RemoteWorkRequest,
  RemoteWorkRequestSummary,
  CreateRemoteWorkRequest,
  UpdateRemoteWorkRequest,
  ApproveRemoteWorkRequest,
  RejectRemoteWorkRequest,
  StaffMonthlyAttendanceSummary,
  UpdateMonthlyAttendanceSummary,
  StaffAttendancePayrollExport,
  StaffAttendancePayrollExportSummary,
  StaffBulkAttendanceImport,
  StaffBulkAttendanceImportSummary,
  StaffBulkAttendanceImportRow,
  CreateBulkAttendanceImport,
  StaffAttendanceAlert,
  StaffAttendanceAlertSummary,
  AcknowledgeAlertRequest,
  StaffAttendanceStatus,
  AttendanceAlertSeverity,
  AttendanceRegularizationStatus,
  OvertimeRequestStatus,
  RemoteWorkRequestStatus,
  PayrollExportStatus,
  AttendanceImportStatus,
  AttendanceAlertTriggerType,
  DailyAttendanceSearch,
  AttendanceDashboard,
} from '@/types/hr/attendance';

/**
 * Day-to-day attendance operations. Each class below targets one controller — the HR
 * backend has no shared `/api/hr` prefix, so the base URLs are spelled out per service.
 *
 * Regularizations, overtime requests and remote-work requests are approved through the
 * generic workflow engine. Their `approve`/`reject` calls drive the engine server-side and
 * the resulting status is applied by the matching IWorkflowStatusAdapter, so callers must
 * refetch rather than assume an outcome. Screens embed the shared workflow components
 * (`useWorkflowRecord` + `WorkflowApprovalActions`) rather than their own approval UI.
 *
 * ⚠ Two conventions that run through every controller in this area:
 *
 *  1. The *actor* on any action (verify, approve, reject, confirm, finalize, export, enrol)
 *     is taken from the caller's token — specifically `CurrentUser.EmployeeId`, resolved by
 *     `AttendanceControllerBase`. The `approvedById` / `verifiedById` / … fields on the DTOs
 *     are still required by model binding but are then ignored. `ACTOR_FROM_TOKEN` below is
 *     what we send for them.
 *  2. Because of (1), a signed-in user whose account is not linked to an employee record
 *     gets a 400 ("not linked to an employee record") on every action endpoint, even with
 *     full permissions. Reads are unaffected.
 */

/**
 * Placeholder for the `*ById` fields the DTOs mark required but the controllers overwrite
 * with the token's employee id. Sending the caller's own id would be equally ignored; an
 * explicit zero GUID documents that the value carries no meaning.
 */
const ACTOR_FROM_TOKEN = '00000000-0000-0000-0000-000000000000';

/** api/staff-daily-attendance — the full-detail daily record. */
class StaffDailyAttendanceService {
  private readonly baseUrl = '/staff-daily-attendance';

  getPaged(page = 1, pageSize = 20): Promise<PagedResult<StaffDailyAttendanceSummary>> {
    return apiService.get<PagedResult<StaffDailyAttendanceSummary>>(`${this.baseUrl}/paged`, {
      pageNumber: page,
      pageSize,
    });
  }

  /**
   * Filtered, sorted, paged search — the one a grid should use. Filters compose with AND;
   * an empty filter behaves like `getPaged`. POST because the filter carries a status array
   * and a dozen tri-state flags, but paging stays on the query string.
   */
  search(
    filter: DailyAttendanceSearch,
    page = 1,
    pageSize = 20,
  ): Promise<PagedResult<StaffDailyAttendanceSummary>> {
    const params = new URLSearchParams({
      pageNumber: String(page),
      pageSize: String(pageSize),
    });
    return apiService.post<PagedResult<StaffDailyAttendanceSummary>>(
      `${this.baseUrl}/search?${params.toString()}`,
      filter,
    );
  }

  getById(id: string): Promise<StaffDailyAttendance> {
    return apiService.get<StaffDailyAttendance>(`${this.baseUrl}/${id}`);
  }

  /** Returns null (204/empty) when the employee has no record for that day. */
  getByEmployeeAndDate(employeeId: string, date: string): Promise<StaffDailyAttendance | null> {
    return apiService.get<StaffDailyAttendance | null>(
      `${this.baseUrl}/employee/${employeeId}/date/${date}`,
    );
  }

  /** `from`/`to` are required by the controller — omitting them is a 400, not "all time". */
  getByEmployee(
    employeeId: string,
    from: string,
    to: string,
  ): Promise<StaffDailyAttendanceSummary[]> {
    return apiService.get<StaffDailyAttendanceSummary[]>(
      `${this.baseUrl}/employee/${employeeId}`,
      { from, to },
    );
  }

  getByDate(date: string): Promise<StaffDailyAttendanceSummary[]> {
    return apiService.get<StaffDailyAttendanceSummary[]>(`${this.baseUrl}/date/${date}`);
  }

  getByStatus(
    status: StaffAttendanceStatus,
    from: string,
    to: string,
  ): Promise<StaffDailyAttendanceSummary[]> {
    return apiService.get<StaffDailyAttendanceSummary[]>(`${this.baseUrl}/status/${status}`, {
      from,
      to,
    });
  }

  getPendingVerification(): Promise<StaffDailyAttendanceSummary[]> {
    return apiService.get<StaffDailyAttendanceSummary[]>(`${this.baseUrl}/pending-verification`);
  }

  getWithOpenExceptions(): Promise<StaffDailyAttendanceSummary[]> {
    return apiService.get<StaffDailyAttendanceSummary[]>(`${this.baseUrl}/open-exceptions`);
  }

  getWithOvertime(
    from: string,
    to: string,
    employeeId?: string,
  ): Promise<StaffDailyAttendanceSummary[]> {
    return apiService.get<StaffDailyAttendanceSummary[]>(`${this.baseUrl}/overtime`, {
      from,
      to,
      employeeId,
    });
  }

  getLate(from: string, to: string, employeeId?: string): Promise<StaffDailyAttendanceSummary[]> {
    return apiService.get<StaffDailyAttendanceSummary[]>(`${this.baseUrl}/late`, {
      from,
      to,
      employeeId,
    });
  }

  getRemoteWorkDays(
    from: string,
    to: string,
    employeeId?: string,
  ): Promise<StaffDailyAttendanceSummary[]> {
    return apiService.get<StaffDailyAttendanceSummary[]>(`${this.baseUrl}/remote-work`, {
      from,
      to,
      employeeId,
    });
  }

  getByPayPeriod(payPeriodId: string): Promise<StaffDailyAttendanceSummary[]> {
    return apiService.get<StaffDailyAttendanceSummary[]>(
      `${this.baseUrl}/pay-period/${payPeriodId}`,
    );
  }

  create(data: CreateStaffDailyAttendance): Promise<StaffDailyAttendance> {
    return apiService.post<StaffDailyAttendance>(this.baseUrl, data);
  }

  update(id: string, data: UpdateStaffDailyAttendance): Promise<StaffDailyAttendance> {
    return apiService.put<StaffDailyAttendance>(`${this.baseUrl}/${id}`, data);
  }

  /** Supervisor sign-off on a day that was flagged as requiring verification. */
  verify(attendanceId: string, verificationNotes?: string | null): Promise<StaffDailyAttendance> {
    const body: VerifyAttendanceRequest = {
      attendanceId,
      verifiedById: ACTOR_FROM_TOKEN,
      verificationNotes: verificationNotes ?? null,
    };
    return apiService.post<StaffDailyAttendance>(`${this.baseUrl}/verify`, body);
  }

  approveException(id: string, comments?: string | null): Promise<StaffDailyAttendance> {
    const body: ApproveAttendanceExceptionRequest = {
      attendanceId: id,
      approvedById: ACTOR_FROM_TOKEN,
      comments: comments ?? null,
    };
    return apiService.post<StaffDailyAttendance>(`${this.baseUrl}/${id}/approve-exception`, body);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/** api/staff-attendance-records — the light clock-in/clock-out table. */
class StaffAttendanceRecordService {
  private readonly baseUrl = '/staff-attendance-records';

  getPaged(page = 1, pageSize = 20): Promise<PagedResult<StaffAttendanceRecordSummary>> {
    return apiService.get<PagedResult<StaffAttendanceRecordSummary>>(`${this.baseUrl}/paged`, {
      pageNumber: page,
      pageSize,
    });
  }

  getById(id: string): Promise<StaffAttendanceRecord> {
    return apiService.get<StaffAttendanceRecord>(`${this.baseUrl}/${id}`);
  }

  getByEmployeeAndDate(employeeId: string, date: string): Promise<StaffAttendanceRecord | null> {
    return apiService.get<StaffAttendanceRecord | null>(
      `${this.baseUrl}/employee/${employeeId}/date/${date}`,
    );
  }

  getByEmployee(
    employeeId: string,
    from: string,
    to: string,
  ): Promise<StaffAttendanceRecordSummary[]> {
    return apiService.get<StaffAttendanceRecordSummary[]>(`${this.baseUrl}/employee/${employeeId}`, {
      from,
      to,
    });
  }

  getByDate(date: string): Promise<StaffAttendanceRecordSummary[]> {
    return apiService.get<StaffAttendanceRecordSummary[]>(`${this.baseUrl}/date/${date}`);
  }

  getByDateRange(from: string, to: string): Promise<StaffAttendanceRecordSummary[]> {
    return apiService.get<StaffAttendanceRecordSummary[]>(`${this.baseUrl}/range`, { from, to });
  }

  getByStatus(
    status: StaffAttendanceStatus,
    from: string,
    to: string,
  ): Promise<StaffAttendanceRecordSummary[]> {
    return apiService.get<StaffAttendanceRecordSummary[]>(`${this.baseUrl}/status/${status}`, {
      from,
      to,
    });
  }

  create(data: CreateStaffAttendanceRecord): Promise<StaffAttendanceRecord> {
    return apiService.post<StaffAttendanceRecord>(this.baseUrl, data);
  }

  update(id: string, data: UpdateStaffAttendanceRecord): Promise<StaffAttendanceRecord> {
    return apiService.put<StaffAttendanceRecord>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/** api/staff-attendance-logs — raw device punches and the self-service punch endpoint. */
class StaffAttendanceLogService {
  private readonly baseUrl = '/staff-attendance-logs';

  getPaged(page = 1, pageSize = 20): Promise<PagedResult<StaffAttendanceLogSummary>> {
    return apiService.get<PagedResult<StaffAttendanceLogSummary>>(`${this.baseUrl}/paged`, {
      pageNumber: page,
      pageSize,
    });
  }

  getById(id: string): Promise<StaffAttendanceLog> {
    return apiService.get<StaffAttendanceLog>(`${this.baseUrl}/${id}`);
  }

  /** `from`/`to` are DateTimes here, not DateOnly, and both are required. */
  getByEmployee(
    employeeId: string,
    from: string,
    to: string,
  ): Promise<StaffAttendanceLogSummary[]> {
    return apiService.get<StaffAttendanceLogSummary[]>(`${this.baseUrl}/employee/${employeeId}`, {
      from,
      to,
    });
  }

  /** Punches that have not yet been folded into a daily attendance row. */
  getUnprocessed(): Promise<StaffAttendanceLogSummary[]> {
    return apiService.get<StaffAttendanceLogSummary[]>(`${this.baseUrl}/unprocessed`);
  }

  getByDevice(deviceId: string, from: string, to: string): Promise<StaffAttendanceLogSummary[]> {
    return apiService.get<StaffAttendanceLogSummary[]>(`${this.baseUrl}/device/${deviceId}`, {
      from,
      to,
    });
  }

  create(data: CreateStaffAttendanceLog): Promise<StaffAttendanceLog> {
    return apiService.post<StaffAttendanceLog>(this.baseUrl, data);
  }

  /**
   * Self-service punch for the signed-in user. The employee is resolved server-side from
   * the token, and GPS coordinates are checked against any configured geofence zone —
   * a hard-enforced zone rejects the punch, a soft one records the violation.
   */
  punch(data: StaffAttendancePunchRequest): Promise<StaffAttendancePunchResult> {
    return apiService.post<StaffAttendancePunchResult>(`${this.baseUrl}/punch`, data);
  }

  /** Rolls one raw log into daily attendance; resolves to the daily attendance id. */
  process(id: string): Promise<string | null> {
    return apiService.post<string | null>(`${this.baseUrl}/${id}/process`);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/** api/staff-attendance-regularizations — corrections to a daily record, workflow-approved. */
class AttendanceRegularizationService {
  private readonly baseUrl = '/staff-attendance-regularizations';

  getPaged(page = 1, pageSize = 20): Promise<PagedResult<StaffAttendanceRegularizationSummary>> {
    return apiService.get<PagedResult<StaffAttendanceRegularizationSummary>>(
      `${this.baseUrl}/paged`,
      { pageNumber: page, pageSize },
    );
  }

  getById(id: string): Promise<StaffAttendanceRegularization> {
    return apiService.get<StaffAttendanceRegularization>(`${this.baseUrl}/${id}`);
  }

  getByNumber(regularizationNumber: string): Promise<StaffAttendanceRegularization | null> {
    return apiService.get<StaffAttendanceRegularization | null>(
      `${this.baseUrl}/number/${regularizationNumber}`,
    );
  }

  getByEmployee(employeeId: string): Promise<StaffAttendanceRegularizationSummary[]> {
    return apiService.get<StaffAttendanceRegularizationSummary[]>(
      `${this.baseUrl}/employee/${employeeId}`,
    );
  }

  getByAttendance(attendanceId: string): Promise<StaffAttendanceRegularizationSummary[]> {
    return apiService.get<StaffAttendanceRegularizationSummary[]>(
      `${this.baseUrl}/attendance/${attendanceId}`,
    );
  }

  getByStatus(
    status: AttendanceRegularizationStatus,
  ): Promise<StaffAttendanceRegularizationSummary[]> {
    return apiService.get<StaffAttendanceRegularizationSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getPendingApproval(): Promise<StaffAttendanceRegularizationSummary[]> {
    return apiService.get<StaffAttendanceRegularizationSummary[]>(`${this.baseUrl}/pending-approval`);
  }

  /** Creating a regularization also starts its approval workflow. */
  create(data: CreateStaffAttendanceRegularization): Promise<StaffAttendanceRegularization> {
    return apiService.post<StaffAttendanceRegularization>(this.baseUrl, data);
  }

  update(
    id: string,
    data: UpdateStaffAttendanceRegularization,
  ): Promise<StaffAttendanceRegularization> {
    return apiService.put<StaffAttendanceRegularization>(`${this.baseUrl}/${id}`, data);
  }

  approve(id: string, approvalComments?: string | null): Promise<StaffAttendanceRegularization> {
    const body: ApproveRegularizationRequest = {
      regularizationId: id,
      approvedById: ACTOR_FROM_TOKEN,
      approvalComments: approvalComments ?? null,
    };
    return apiService.post<StaffAttendanceRegularization>(`${this.baseUrl}/${id}/approve`, body);
  }

  reject(id: string, rejectionReason: string): Promise<StaffAttendanceRegularization> {
    const body: RejectRegularizationRequest = { regularizationId: id, rejectionReason };
    return apiService.post<StaffAttendanceRegularization>(`${this.baseUrl}/${id}/reject`, body);
  }

  /** Writes the approved times onto the underlying daily attendance record. */
  apply(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/apply`);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/** api/staff-overtime-requests — pre-approval then post-hoc supervisor confirmation. */
class StaffOvertimeRequestService {
  private readonly baseUrl = '/staff-overtime-requests';

  getPaged(page = 1, pageSize = 20): Promise<PagedResult<StaffOvertimeRequestSummary>> {
    return apiService.get<PagedResult<StaffOvertimeRequestSummary>>(`${this.baseUrl}/paged`, {
      pageNumber: page,
      pageSize,
    });
  }

  getById(id: string): Promise<StaffOvertimeRequest> {
    return apiService.get<StaffOvertimeRequest>(`${this.baseUrl}/${id}`);
  }

  getByNumber(requestNumber: string): Promise<StaffOvertimeRequest | null> {
    return apiService.get<StaffOvertimeRequest | null>(`${this.baseUrl}/number/${requestNumber}`);
  }

  getByEmployee(employeeId: string): Promise<StaffOvertimeRequestSummary[]> {
    return apiService.get<StaffOvertimeRequestSummary[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  getByStatus(status: OvertimeRequestStatus): Promise<StaffOvertimeRequestSummary[]> {
    return apiService.get<StaffOvertimeRequestSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getPendingApproval(): Promise<StaffOvertimeRequestSummary[]> {
    return apiService.get<StaffOvertimeRequestSummary[]>(`${this.baseUrl}/pending-approval`);
  }

  /** Approved overtime that has been worked but whose actual hours are unconfirmed. */
  getPendingSupervisorConfirmation(): Promise<StaffOvertimeRequestSummary[]> {
    return apiService.get<StaffOvertimeRequestSummary[]>(
      `${this.baseUrl}/pending-supervisor-confirmation`,
    );
  }

  getByDateRange(from: string, to: string): Promise<StaffOvertimeRequestSummary[]> {
    return apiService.get<StaffOvertimeRequestSummary[]>(`${this.baseUrl}/range`, { from, to });
  }

  create(data: CreateStaffOvertimeRequest): Promise<StaffOvertimeRequest> {
    return apiService.post<StaffOvertimeRequest>(this.baseUrl, data);
  }

  update(id: string, data: { id: string; requestedHours: number; reason: string; notes?: string | null }): Promise<StaffOvertimeRequest> {
    return apiService.put<StaffOvertimeRequest>(`${this.baseUrl}/${id}`, data);
  }

  approve(id: string, approvalComments?: string | null): Promise<StaffOvertimeRequest> {
    const body: ApproveOvertimeRequest = {
      requestId: id,
      approvedById: ACTOR_FROM_TOKEN,
      approvalComments: approvalComments ?? null,
    };
    return apiService.post<StaffOvertimeRequest>(`${this.baseUrl}/${id}/approve`, body);
  }

  reject(id: string, rejectionReason: string): Promise<StaffOvertimeRequest> {
    const body: RejectOvertimeRequest = { requestId: id, rejectionReason };
    return apiService.post<StaffOvertimeRequest>(`${this.baseUrl}/${id}/reject`, body);
  }

  /** Records the hours actually worked; moves the request to Completed. */
  confirmActualHours(
    id: string,
    actualOvertimeHours: number,
    supervisorNotes?: string | null,
  ): Promise<StaffOvertimeRequest> {
    const body: ConfirmOvertimeRequest = {
      requestId: id,
      supervisorConfirmedById: ACTOR_FROM_TOKEN,
      actualOvertimeHours,
      supervisorNotes: supervisorNotes ?? null,
    };
    return apiService.post<StaffOvertimeRequest>(`${this.baseUrl}/${id}/confirm`, body);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/** api/remote-work-requests — work-from-home approvals. */
class RemoteWorkRequestService {
  private readonly baseUrl = '/remote-work-requests';

  getPaged(page = 1, pageSize = 20): Promise<PagedResult<RemoteWorkRequestSummary>> {
    return apiService.get<PagedResult<RemoteWorkRequestSummary>>(`${this.baseUrl}/paged`, {
      pageNumber: page,
      pageSize,
    });
  }

  getById(id: string): Promise<RemoteWorkRequest> {
    return apiService.get<RemoteWorkRequest>(`${this.baseUrl}/${id}`);
  }

  getByNumber(requestNumber: string): Promise<RemoteWorkRequest | null> {
    return apiService.get<RemoteWorkRequest | null>(`${this.baseUrl}/number/${requestNumber}`);
  }

  getByEmployee(employeeId: string): Promise<RemoteWorkRequestSummary[]> {
    return apiService.get<RemoteWorkRequestSummary[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  getByStatus(status: RemoteWorkRequestStatus): Promise<RemoteWorkRequestSummary[]> {
    return apiService.get<RemoteWorkRequestSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getPendingApproval(): Promise<RemoteWorkRequestSummary[]> {
    return apiService.get<RemoteWorkRequestSummary[]>(`${this.baseUrl}/pending-approval`);
  }

  getByDateRange(from: string, to: string): Promise<RemoteWorkRequestSummary[]> {
    return apiService.get<RemoteWorkRequestSummary[]>(`${this.baseUrl}/range`, { from, to });
  }

  create(data: CreateRemoteWorkRequest): Promise<RemoteWorkRequest> {
    return apiService.post<RemoteWorkRequest>(this.baseUrl, data);
  }

  update(id: string, data: UpdateRemoteWorkRequest): Promise<RemoteWorkRequest> {
    return apiService.put<RemoteWorkRequest>(`${this.baseUrl}/${id}`, data);
  }

  approve(id: string, approvalComments?: string | null): Promise<RemoteWorkRequest> {
    const body: ApproveRemoteWorkRequest = {
      requestId: id,
      approvedById: ACTOR_FROM_TOKEN,
      approvalComments: approvalComments ?? null,
    };
    return apiService.post<RemoteWorkRequest>(`${this.baseUrl}/${id}/approve`, body);
  }

  reject(id: string, rejectionReason: string): Promise<RemoteWorkRequest> {
    const body: RejectRemoteWorkRequest = { requestId: id, rejectionReason };
    return apiService.post<RemoteWorkRequest>(`${this.baseUrl}/${id}/reject`, body);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/** api/staff-monthly-attendance-summaries — the roll-up payroll reads from. */
class MonthlyAttendanceSummaryService {
  private readonly baseUrl = '/staff-monthly-attendance-summaries';

  getById(id: string): Promise<StaffMonthlyAttendanceSummary> {
    return apiService.get<StaffMonthlyAttendanceSummary>(`${this.baseUrl}/${id}`);
  }

  getByEmployeeAndPeriod(
    employeeId: string,
    year: number,
    month: number,
  ): Promise<StaffMonthlyAttendanceSummary | null> {
    return apiService.get<StaffMonthlyAttendanceSummary | null>(
      `${this.baseUrl}/employee/${employeeId}/period`,
      { year, month },
    );
  }

  getByEmployeeAndYear(employeeId: string, year: number): Promise<StaffMonthlyAttendanceSummary[]> {
    return apiService.get<StaffMonthlyAttendanceSummary[]>(
      `${this.baseUrl}/employee/${employeeId}/year/${year}`,
    );
  }

  getByYearAndMonth(year: number, month: number): Promise<StaffMonthlyAttendanceSummary[]> {
    return apiService.get<StaffMonthlyAttendanceSummary[]>(
      `${this.baseUrl}/year/${year}/month/${month}`,
    );
  }

  getByPayPeriod(payPeriodId: string): Promise<StaffMonthlyAttendanceSummary[]> {
    return apiService.get<StaffMonthlyAttendanceSummary[]>(
      `${this.baseUrl}/pay-period/${payPeriodId}`,
    );
  }

  getUnfinalized(year: number, month: number): Promise<StaffMonthlyAttendanceSummary[]> {
    return apiService.get<StaffMonthlyAttendanceSummary[]>(`${this.baseUrl}/unfinalized`, {
      year,
      month,
    });
  }

  /**
   * Recomputes a summary from the underlying daily records, discarding manual edits.
   * Note the arguments go on the query string — this POST has no body.
   */
  recalculate(
    employeeId: string,
    year: number,
    month: number,
  ): Promise<StaffMonthlyAttendanceSummary> {
    const params = new URLSearchParams({
      employeeId,
      year: String(year),
      month: String(month),
    });
    return apiService.post<StaffMonthlyAttendanceSummary>(
      `${this.baseUrl}/recalculate?${params.toString()}`,
    );
  }

  /** Locks the summary so payroll can export it. */
  finalize(id: string): Promise<StaffMonthlyAttendanceSummary> {
    return apiService.post<StaffMonthlyAttendanceSummary>(`${this.baseUrl}/${id}/finalize`);
  }

  update(
    id: string,
    data: UpdateMonthlyAttendanceSummary,
  ): Promise<StaffMonthlyAttendanceSummary> {
    return apiService.put<StaffMonthlyAttendanceSummary>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/** api/staff-attendance-payroll-exports — audit trail of hand-offs to payroll. */
class AttendancePayrollExportService {
  private readonly baseUrl = '/staff-attendance-payroll-exports';

  getPaged(page = 1, pageSize = 20): Promise<PagedResult<StaffAttendancePayrollExportSummary>> {
    return apiService.get<PagedResult<StaffAttendancePayrollExportSummary>>(
      `${this.baseUrl}/paged`,
      { pageNumber: page, pageSize },
    );
  }

  getById(id: string): Promise<StaffAttendancePayrollExport> {
    return apiService.get<StaffAttendancePayrollExport>(`${this.baseUrl}/${id}`);
  }

  getByReference(exportReference: string): Promise<StaffAttendancePayrollExport | null> {
    return apiService.get<StaffAttendancePayrollExport | null>(
      `${this.baseUrl}/reference/${exportReference}`,
    );
  }

  getByPayPeriod(payPeriodId: string): Promise<StaffAttendancePayrollExportSummary[]> {
    return apiService.get<StaffAttendancePayrollExportSummary[]>(
      `${this.baseUrl}/pay-period/${payPeriodId}`,
    );
  }

  getByStatus(status: PayrollExportStatus): Promise<StaffAttendancePayrollExportSummary[]> {
    return apiService.get<StaffAttendancePayrollExportSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getLatestSuccessfulForPeriod(payPeriodId: string): Promise<StaffAttendancePayrollExport | null> {
    return apiService.get<StaffAttendancePayrollExport | null>(
      `${this.baseUrl}/pay-period/${payPeriodId}/latest-successful`,
    );
  }

  /**
   * Runs the export; the row it returns carries the outcome and any error detail.
   * The endpoint takes its own narrow request shape, not CreateStaffAttendancePayrollExportDto —
   * the exporting user comes from the token.
   */
  runExport(payPeriodId: string, targetSystem?: string | null): Promise<StaffAttendancePayrollExport> {
    return apiService.post<StaffAttendancePayrollExport>(`${this.baseUrl}/export`, {
      payPeriodId,
      targetSystem: targetSystem ?? null,
    });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/** api/staff-bulk-attendance-imports — batch loads with per-row error reporting. */
class BulkAttendanceImportService {
  private readonly baseUrl = '/staff-bulk-attendance-imports';

  getPaged(page = 1, pageSize = 20): Promise<PagedResult<StaffBulkAttendanceImportSummary>> {
    return apiService.get<PagedResult<StaffBulkAttendanceImportSummary>>(`${this.baseUrl}/paged`, {
      pageNumber: page,
      pageSize,
    });
  }

  getById(id: string): Promise<StaffBulkAttendanceImport> {
    return apiService.get<StaffBulkAttendanceImport>(`${this.baseUrl}/${id}`);
  }

  getByReference(importReference: string): Promise<StaffBulkAttendanceImport | null> {
    return apiService.get<StaffBulkAttendanceImport | null>(
      `${this.baseUrl}/reference/${importReference}`,
    );
  }

  getByStatus(status: AttendanceImportStatus): Promise<StaffBulkAttendanceImportSummary[]> {
    return apiService.get<StaffBulkAttendanceImportSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getRows(id: string): Promise<StaffBulkAttendanceImportRow[]> {
    return apiService.get<StaffBulkAttendanceImportRow[]>(`${this.baseUrl}/${id}/rows`);
  }

  getFailedRows(id: string): Promise<StaffBulkAttendanceImportRow[]> {
    return apiService.get<StaffBulkAttendanceImportRow[]>(`${this.baseUrl}/${id}/rows/failed`);
  }

  /** Stages the batch. Nothing is written to attendance until `process` is called. */
  initiate(data: CreateBulkAttendanceImport): Promise<StaffBulkAttendanceImport> {
    return apiService.post<StaffBulkAttendanceImport>(this.baseUrl, data);
  }

  process(id: string): Promise<StaffBulkAttendanceImport> {
    return apiService.post<StaffBulkAttendanceImport>(`${this.baseUrl}/${id}/process`);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/** api/staff-attendance-alerts — alert instances raised by the alert rules. */
class AttendanceAlertService {
  private readonly baseUrl = '/staff-attendance-alerts';

  getPaged(page = 1, pageSize = 20): Promise<PagedResult<StaffAttendanceAlertSummary>> {
    return apiService.get<PagedResult<StaffAttendanceAlertSummary>>(`${this.baseUrl}/paged`, {
      pageNumber: page,
      pageSize,
    });
  }

  getById(id: string): Promise<StaffAttendanceAlert> {
    return apiService.get<StaffAttendanceAlert>(`${this.baseUrl}/${id}`);
  }

  getByEmployee(employeeId: string): Promise<StaffAttendanceAlertSummary[]> {
    return apiService.get<StaffAttendanceAlertSummary[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  getByRule(ruleId: string): Promise<StaffAttendanceAlertSummary[]> {
    return apiService.get<StaffAttendanceAlertSummary[]>(`${this.baseUrl}/rule/${ruleId}`);
  }

  getUnacknowledged(): Promise<StaffAttendanceAlertSummary[]> {
    return apiService.get<StaffAttendanceAlertSummary[]>(`${this.baseUrl}/unacknowledged`);
  }

  getUnacknowledgedForEmployee(employeeId: string): Promise<StaffAttendanceAlertSummary[]> {
    return apiService.get<StaffAttendanceAlertSummary[]>(
      `${this.baseUrl}/employee/${employeeId}/unacknowledged`,
    );
  }

  getByAttendanceDate(date: string): Promise<StaffAttendanceAlertSummary[]> {
    return apiService.get<StaffAttendanceAlertSummary[]>(`${this.baseUrl}/date/${date}`);
  }

  getByType(alertType: AttendanceAlertTriggerType): Promise<StaffAttendanceAlertSummary[]> {
    return apiService.get<StaffAttendanceAlertSummary[]>(`${this.baseUrl}/type/${alertType}`);
  }

  getBySeverity(severity: AttendanceAlertSeverity): Promise<StaffAttendanceAlertSummary[]> {
    return apiService.get<StaffAttendanceAlertSummary[]>(`${this.baseUrl}/severity/${severity}`);
  }

  /** Re-runs every active rule against one daily attendance record. */
  evaluateForAttendance(dailyAttendanceId: string): Promise<StaffAttendanceAlert[]> {
    return apiService.post<StaffAttendanceAlert[]>(`${this.baseUrl}/evaluate/${dailyAttendanceId}`);
  }

  acknowledge(id: string, acknowledgementNotes?: string | null): Promise<StaffAttendanceAlert> {
    const body: AcknowledgeAlertRequest = {
      alertId: id,
      acknowledgedById: ACTOR_FROM_TOKEN,
      acknowledgementNotes: acknowledgementNotes ?? null,
    };
    return apiService.post<StaffAttendanceAlert>(`${this.baseUrl}/${id}/acknowledge`, body);
  }

  /** Returns how many alerts were actually acknowledged (wrapped in an object, not a bare int). */
  async bulkAcknowledge(alertIds: string[], comments?: string | null): Promise<number> {
    const result = await apiService.post<{ acknowledgedCount: number }>(
      `${this.baseUrl}/bulk-acknowledge`,
      { alertIds, comments: comments ?? null },
    );
    return result?.acknowledgedCount ?? 0;
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/** api/attendance-dashboard — the whole landing page in one aggregated request. */
class AttendanceDashboardService {
  private readonly baseUrl = '/attendance-dashboard';

  /**
   * @param asOf Day treated as "today"; defaults server-side to the current UTC date.
   * @param trendDays Length of the daily trend, clamped server-side to 1–90.
   * @param riskListSize Size of the chronic-absentee list, clamped server-side to 1–50.
   */
  get(asOf?: string, trendDays = 7, riskListSize = 5): Promise<AttendanceDashboard> {
    return apiService.get<AttendanceDashboard>(this.baseUrl, {
      asOf,
      trendDays,
      riskListSize,
    });
  }
}

export const dailyAttendanceService = new StaffDailyAttendanceService();
export const attendanceDashboardService = new AttendanceDashboardService();
export const attendanceRecordService = new StaffAttendanceRecordService();
export const attendanceLogService = new StaffAttendanceLogService();
export const regularizationService = new AttendanceRegularizationService();
export const overtimeRequestService = new StaffOvertimeRequestService();
export const remoteWorkRequestService = new RemoteWorkRequestService();
export const monthlySummaryService = new MonthlyAttendanceSummaryService();
export const payrollExportService = new AttendancePayrollExportService();
export const bulkImportService = new BulkAttendanceImportService();
export const attendanceAlertService = new AttendanceAlertService();
