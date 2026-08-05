import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  WorkSchedule,
  WorkScheduleSummary,
  CreateWorkSchedule,
  UpdateWorkSchedule,
  WorkScheduleType,
  EmployeeWorkSchedule,
  AssignEmployeeWorkSchedule,
  UpdateEmployeeWorkSchedule,
  ShiftDefinition,
  ShiftDefinitionSummary,
  CreateShiftDefinition,
  UpdateShiftDefinition,
  ShiftType,
  ShiftAssignment,
  CreateShiftAssignment,
  UpdateShiftAssignment,
  ShiftRotationPlan,
  ShiftRotationPlanSummary,
  CreateShiftRotationPlan,
  UpdateShiftRotationPlan,
  ShiftRotationStage,
  CreateShiftRotationStage,
  UpdateShiftRotationStage,
  ShiftRotationMember,
  AddShiftRotationMember,
  UpdateShiftRotationMember,
  ShiftRotationCycle,
  HolidayCalendar,
  HolidayCalendarSummary,
  CreateHolidayCalendar,
  UpdateHolidayCalendar,
  PublicHoliday,
  PublicHolidaySummary,
  CreatePublicHoliday,
  UpdatePublicHoliday,
  PayPeriod,
  PayPeriodSummary,
  CreatePayPeriod,
  UpdatePayPeriod,
  PayPeriodStatus,
  PayPeriodType,
  GeofenceZone,
  GeofenceZoneSummary,
  CreateGeofenceZone,
  UpdateGeofenceZone,
  StaffAttendanceDevice,
  StaffAttendanceDeviceSummary,
  CreateStaffAttendanceDevice,
  UpdateStaffAttendanceDevice,
  StaffAttendanceAlertRule,
  StaffAttendanceAlertRuleSummary,
  CreateAlertRule,
  UpdateAlertRule,
  AttendanceAlertTriggerType,
  PositionOvertimePolicy,
  CreatePositionOvertimePolicy,
  UpdatePositionOvertimePolicy,
  OvertimeAllowanceType,
  EmployeeOvertimeOverride,
  CreateEmployeeOvertimeOverride,
  UpdateEmployeeOvertimeOverride,
  EmployeeBiometric,
  EmployeeBiometricSummary,
  EnrollBiometric,
  BiometricType,
} from '@/types/hr/attendance';

/**
 * Configuration behind Attendance & Time — the pieces that live under
 * Administration → HR → Attendance rather than the operational `/hr/attendance` screens.
 *
 * Same per-controller base-URL caveat as `attendance.service.ts`, and the same rule that
 * the acting user is resolved from the token on every write.
 */

/** api/work-schedules — named schedules, with their shift definitions nested. */
class WorkScheduleService {
  private readonly baseUrl = '/work-schedules';

  getAll(): Promise<WorkScheduleSummary[]> {
    return apiService.get<WorkScheduleSummary[]>(this.baseUrl);
  }

  getPaged(page = 1, pageSize = 20): Promise<PagedResult<WorkScheduleSummary>> {
    return apiService.get<PagedResult<WorkScheduleSummary>>(`${this.baseUrl}/paged`, {
      pageNumber: page,
      pageSize,
    });
  }

  getById(id: string): Promise<WorkSchedule> {
    return apiService.get<WorkSchedule>(`${this.baseUrl}/${id}`);
  }

  /** Null when no schedule has been marked default for the tenant. */
  getDefault(): Promise<WorkSchedule | null> {
    return apiService.get<WorkSchedule | null>(`${this.baseUrl}/default`);
  }

  getActive(): Promise<WorkScheduleSummary[]> {
    return apiService.get<WorkScheduleSummary[]>(`${this.baseUrl}/active`);
  }

  getByType(type: WorkScheduleType): Promise<WorkScheduleSummary[]> {
    return apiService.get<WorkScheduleSummary[]>(`${this.baseUrl}/type/${type}`);
  }

  create(data: CreateWorkSchedule): Promise<WorkSchedule> {
    return apiService.post<WorkSchedule>(this.baseUrl, data);
  }

  update(id: string, data: UpdateWorkSchedule): Promise<WorkSchedule> {
    return apiService.put<WorkSchedule>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // Shifts nested under a schedule. `api/shift-definitions` exposes the same rows
  // flat; these routes are the ones the schedule editor uses.

  getShifts(scheduleId: string): Promise<ShiftDefinitionSummary[]> {
    return apiService.get<ShiftDefinitionSummary[]>(`${this.baseUrl}/${scheduleId}/shifts`);
  }

  addShift(scheduleId: string, data: CreateShiftDefinition): Promise<ShiftDefinition> {
    return apiService.post<ShiftDefinition>(`${this.baseUrl}/${scheduleId}/shifts`, data);
  }

  /** Shift updates are keyed by shift id alone — no schedule segment. */
  updateShift(shiftId: string, data: UpdateShiftDefinition): Promise<ShiftDefinition> {
    return apiService.put<ShiftDefinition>(`${this.baseUrl}/shifts/${shiftId}`, data);
  }

  removeShift(shiftId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/shifts/${shiftId}`);
  }
}

/** api/shift-definitions — the flat view of shifts across all schedules. */
class ShiftDefinitionService {
  private readonly baseUrl = '/shift-definitions';

  getAll(): Promise<ShiftDefinitionSummary[]> {
    return apiService.get<ShiftDefinitionSummary[]>(this.baseUrl);
  }

  getById(id: string): Promise<ShiftDefinition> {
    return apiService.get<ShiftDefinition>(`${this.baseUrl}/${id}`);
  }

  getByWorkSchedule(workScheduleId: string): Promise<ShiftDefinitionSummary[]> {
    return apiService.get<ShiftDefinitionSummary[]>(
      `${this.baseUrl}/work-schedule/${workScheduleId}`,
    );
  }

  getActive(): Promise<ShiftDefinitionSummary[]> {
    return apiService.get<ShiftDefinitionSummary[]>(`${this.baseUrl}/active`);
  }

  getNightShifts(): Promise<ShiftDefinitionSummary[]> {
    return apiService.get<ShiftDefinitionSummary[]>(`${this.baseUrl}/night`);
  }

  getByType(type: ShiftType): Promise<ShiftDefinitionSummary[]> {
    return apiService.get<ShiftDefinitionSummary[]>(`${this.baseUrl}/type/${type}`);
  }

  create(data: CreateShiftDefinition): Promise<ShiftDefinition> {
    return apiService.post<ShiftDefinition>(this.baseUrl, data);
  }

  update(id: string, data: UpdateShiftDefinition): Promise<ShiftDefinition> {
    return apiService.put<ShiftDefinition>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/** api/employee-work-schedules — which schedule an employee is on, and since when. */
class EmployeeWorkScheduleService {
  private readonly baseUrl = '/employee-work-schedules';

  getById(id: string): Promise<EmployeeWorkSchedule> {
    return apiService.get<EmployeeWorkSchedule>(`${this.baseUrl}/${id}`);
  }

  getCurrentForEmployee(employeeId: string): Promise<EmployeeWorkSchedule | null> {
    return apiService.get<EmployeeWorkSchedule | null>(
      `${this.baseUrl}/employee/${employeeId}/current`,
    );
  }

  getAllForEmployee(employeeId: string): Promise<EmployeeWorkSchedule[]> {
    return apiService.get<EmployeeWorkSchedule[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  getByWorkSchedule(workScheduleId: string): Promise<EmployeeWorkSchedule[]> {
    return apiService.get<EmployeeWorkSchedule[]>(
      `${this.baseUrl}/work-schedule/${workScheduleId}`,
    );
  }

  getPaged(page = 1, pageSize = 20): Promise<PagedResult<EmployeeWorkSchedule>> {
    return apiService.get<PagedResult<EmployeeWorkSchedule>>(`${this.baseUrl}/paged`, {
      pageNumber: page,
      pageSize,
    });
  }

  assign(data: AssignEmployeeWorkSchedule): Promise<EmployeeWorkSchedule> {
    return apiService.post<EmployeeWorkSchedule>(`${this.baseUrl}/assign`, data);
  }

  update(id: string, data: UpdateEmployeeWorkSchedule): Promise<EmployeeWorkSchedule> {
    return apiService.put<EmployeeWorkSchedule>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/** api/shift-assignments — an employee on a specific shift for a date window. */
class ShiftAssignmentService {
  private readonly baseUrl = '/shift-assignments';

  getById(id: string): Promise<ShiftAssignment> {
    return apiService.get<ShiftAssignment>(`${this.baseUrl}/${id}`);
  }

  getCurrentForEmployee(employeeId: string): Promise<ShiftAssignment | null> {
    return apiService.get<ShiftAssignment | null>(`${this.baseUrl}/employee/${employeeId}/current`);
  }

  getByEmployee(employeeId: string): Promise<ShiftAssignment[]> {
    return apiService.get<ShiftAssignment[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  getByShiftDefinition(shiftDefinitionId: string): Promise<ShiftAssignment[]> {
    return apiService.get<ShiftAssignment[]>(`${this.baseUrl}/shift/${shiftDefinitionId}`);
  }

  /** Assignments live on `asOf` (defaults to today server-side). */
  getActive(asOf?: string): Promise<ShiftAssignment[]> {
    return apiService.get<ShiftAssignment[]>(`${this.baseUrl}/active`, { asOf });
  }

  assign(data: CreateShiftAssignment): Promise<ShiftAssignment> {
    return apiService.post<ShiftAssignment>(this.baseUrl, data);
  }

  update(id: string, data: UpdateShiftAssignment): Promise<ShiftAssignment> {
    return apiService.put<ShiftAssignment>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/** api/shift-rotation-plans — rotation cycles, their stages, and enrolled members. */
class ShiftRotationPlanService {
  private readonly baseUrl = '/shift-rotation-plans';

  getAll(): Promise<ShiftRotationPlanSummary[]> {
    return apiService.get<ShiftRotationPlanSummary[]>(this.baseUrl);
  }

  getPaged(page = 1, pageSize = 20): Promise<PagedResult<ShiftRotationPlanSummary>> {
    return apiService.get<PagedResult<ShiftRotationPlanSummary>>(`${this.baseUrl}/paged`, {
      pageNumber: page,
      pageSize,
    });
  }

  getById(id: string): Promise<ShiftRotationPlan> {
    return apiService.get<ShiftRotationPlan>(`${this.baseUrl}/${id}`);
  }

  getActive(): Promise<ShiftRotationPlanSummary[]> {
    return apiService.get<ShiftRotationPlanSummary[]>(`${this.baseUrl}/active`);
  }

  getByCycle(cycle: ShiftRotationCycle): Promise<ShiftRotationPlanSummary[]> {
    return apiService.get<ShiftRotationPlanSummary[]>(`${this.baseUrl}/cycle/${cycle}`);
  }

  create(data: CreateShiftRotationPlan): Promise<ShiftRotationPlan> {
    return apiService.post<ShiftRotationPlan>(this.baseUrl, data);
  }

  update(id: string, data: UpdateShiftRotationPlan): Promise<ShiftRotationPlan> {
    return apiService.put<ShiftRotationPlan>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // Stages — note `/stages/list` for the collection; plain `/stages` is the POST.

  getStages(planId: string): Promise<ShiftRotationStage[]> {
    return apiService.get<ShiftRotationStage[]>(`${this.baseUrl}/${planId}/stages/list`);
  }

  addStage(planId: string, data: CreateShiftRotationStage): Promise<ShiftRotationStage> {
    return apiService.post<ShiftRotationStage>(`${this.baseUrl}/${planId}/stages`, data);
  }

  updateStage(stageId: string, data: UpdateShiftRotationStage): Promise<ShiftRotationStage> {
    return apiService.put<ShiftRotationStage>(`${this.baseUrl}/stages/${stageId}`, data);
  }

  removeStage(stageId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/stages/${stageId}`);
  }

  // Members — same `/list` asymmetry as stages.

  getMembers(planId: string): Promise<ShiftRotationMember[]> {
    return apiService.get<ShiftRotationMember[]>(`${this.baseUrl}/${planId}/members/list`);
  }

  enrollMember(planId: string, data: AddShiftRotationMember): Promise<ShiftRotationMember> {
    return apiService.post<ShiftRotationMember>(`${this.baseUrl}/${planId}/members`, data);
  }

  updateMember(memberId: string, data: UpdateShiftRotationMember): Promise<ShiftRotationMember> {
    return apiService.put<ShiftRotationMember>(`${this.baseUrl}/members/${memberId}`, data);
  }

  removeMember(memberId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/members/${memberId}`);
  }
}

/** api/holiday-calendars — calendars and the public holidays inside them. */
class HolidayCalendarService {
  private readonly baseUrl = '/holiday-calendars';

  getAll(): Promise<HolidayCalendarSummary[]> {
    return apiService.get<HolidayCalendarSummary[]>(this.baseUrl);
  }

  getPaged(page = 1, pageSize = 20): Promise<PagedResult<HolidayCalendarSummary>> {
    return apiService.get<PagedResult<HolidayCalendarSummary>>(`${this.baseUrl}/paged`, {
      pageNumber: page,
      pageSize,
    });
  }

  getDefault(): Promise<HolidayCalendar | null> {
    return apiService.get<HolidayCalendar | null>(`${this.baseUrl}/default`);
  }

  getActive(): Promise<HolidayCalendarSummary[]> {
    return apiService.get<HolidayCalendarSummary[]>(`${this.baseUrl}/active`);
  }

  getById(id: string): Promise<HolidayCalendar> {
    return apiService.get<HolidayCalendar>(`${this.baseUrl}/${id}`);
  }

  /** Same record as getById but with `publicHolidays` populated for `year`. */
  getWithHolidays(id: string, year?: number): Promise<HolidayCalendar> {
    return apiService.get<HolidayCalendar>(`${this.baseUrl}/${id}/with-holidays`, { year });
  }

  create(data: CreateHolidayCalendar): Promise<HolidayCalendar> {
    return apiService.post<HolidayCalendar>(this.baseUrl, data);
  }

  update(id: string, data: UpdateHolidayCalendar): Promise<HolidayCalendar> {
    return apiService.put<HolidayCalendar>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // Holidays within a calendar.

  getHolidays(calendarId: string, year?: number): Promise<PublicHolidaySummary[]> {
    return apiService.get<PublicHolidaySummary[]>(`${this.baseUrl}/${calendarId}/holidays`, {
      year,
    });
  }

  getRecurringHolidays(calendarId: string): Promise<PublicHoliday[]> {
    return apiService.get<PublicHoliday[]>(`${this.baseUrl}/${calendarId}/holidays/recurring`);
  }

  addHoliday(calendarId: string, data: CreatePublicHoliday): Promise<PublicHoliday> {
    return apiService.post<PublicHoliday>(`${this.baseUrl}/${calendarId}/holidays`, data);
  }

  updateHoliday(
    calendarId: string,
    holidayId: string,
    data: UpdatePublicHoliday,
  ): Promise<PublicHoliday> {
    return apiService.put<PublicHoliday>(
      `${this.baseUrl}/${calendarId}/holidays/${holidayId}`,
      data,
    );
  }

  removeHoliday(calendarId: string, holidayId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${calendarId}/holidays/${holidayId}`);
  }

  /** Tenant-wide holiday lookups, across every calendar. */
  getHolidaysByYear(year: number): Promise<PublicHoliday[]> {
    return apiService.get<PublicHoliday[]>(`${this.baseUrl}/holidays/by-year/${year}`);
  }

  getHolidaysInRange(from: string, to: string): Promise<PublicHoliday[]> {
    return apiService.get<PublicHoliday[]>(`${this.baseUrl}/holidays/range`, { from, to });
  }
}

/** api/pay-periods — the cut-off windows attendance summaries roll into. */
class PayPeriodService {
  private readonly baseUrl = '/pay-periods';

  getPaged(page = 1, pageSize = 20): Promise<PagedResult<PayPeriodSummary>> {
    return apiService.get<PagedResult<PayPeriodSummary>>(`${this.baseUrl}/paged`, {
      pageNumber: page,
      pageSize,
    });
  }

  getById(id: string): Promise<PayPeriod> {
    return apiService.get<PayPeriod>(`${this.baseUrl}/${id}`);
  }

  getCurrentOpen(): Promise<PayPeriod | null> {
    return apiService.get<PayPeriod | null>(`${this.baseUrl}/current`);
  }

  getCoveringDate(date: string): Promise<PayPeriod | null> {
    return apiService.get<PayPeriod | null>(`${this.baseUrl}/covering/${date}`);
  }

  getByStatus(status: PayPeriodStatus): Promise<PayPeriodSummary[]> {
    return apiService.get<PayPeriodSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getByType(type: PayPeriodType): Promise<PayPeriodSummary[]> {
    return apiService.get<PayPeriodSummary[]>(`${this.baseUrl}/type/${type}`);
  }

  getWithSummaries(id: string): Promise<PayPeriod> {
    return apiService.get<PayPeriod>(`${this.baseUrl}/${id}/summaries`);
  }

  create(data: CreatePayPeriod): Promise<PayPeriod> {
    return apiService.post<PayPeriod>(this.baseUrl, data);
  }

  update(id: string, data: UpdatePayPeriod): Promise<PayPeriod> {
    return apiService.put<PayPeriod>(`${this.baseUrl}/${id}`, data);
  }

  /** Closing locks the period; the closing user comes from the token. */
  close(id: string): Promise<PayPeriod> {
    return apiService.post<PayPeriod>(`${this.baseUrl}/${id}/close`);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/** api/geofence-zones — GPS fences punches are checked against. */
class GeofenceZoneService {
  private readonly baseUrl = '/geofence-zones';

  getAll(): Promise<GeofenceZoneSummary[]> {
    return apiService.get<GeofenceZoneSummary[]>(this.baseUrl);
  }

  getPaged(page = 1, pageSize = 20): Promise<PagedResult<GeofenceZoneSummary>> {
    return apiService.get<PagedResult<GeofenceZoneSummary>>(`${this.baseUrl}/paged`, {
      pageNumber: page,
      pageSize,
    });
  }

  getById(id: string): Promise<GeofenceZone> {
    return apiService.get<GeofenceZone>(`${this.baseUrl}/${id}`);
  }

  getActive(): Promise<GeofenceZoneSummary[]> {
    return apiService.get<GeofenceZoneSummary[]>(`${this.baseUrl}/active`);
  }

  getByLocation(locationId: string): Promise<GeofenceZoneSummary[]> {
    return apiService.get<GeofenceZoneSummary[]>(`${this.baseUrl}/location/${locationId}`);
  }

  create(data: CreateGeofenceZone): Promise<GeofenceZone> {
    return apiService.post<GeofenceZone>(this.baseUrl, data);
  }

  update(id: string, data: UpdateGeofenceZone): Promise<GeofenceZone> {
    return apiService.put<GeofenceZone>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/** api/staff-attendance-devices — registered clocking hardware. */
class AttendanceDeviceService {
  private readonly baseUrl = '/staff-attendance-devices';

  getAll(): Promise<StaffAttendanceDeviceSummary[]> {
    return apiService.get<StaffAttendanceDeviceSummary[]>(this.baseUrl);
  }

  getPaged(page = 1, pageSize = 20): Promise<PagedResult<StaffAttendanceDeviceSummary>> {
    return apiService.get<PagedResult<StaffAttendanceDeviceSummary>>(`${this.baseUrl}/paged`, {
      pageNumber: page,
      pageSize,
    });
  }

  getById(id: string): Promise<StaffAttendanceDevice> {
    return apiService.get<StaffAttendanceDevice>(`${this.baseUrl}/${id}`);
  }

  /** Looks up by the vendor's device identifier rather than the row id. */
  getByExternalDeviceId(externalDeviceId: string): Promise<StaffAttendanceDevice | null> {
    return apiService.get<StaffAttendanceDevice | null>(
      `${this.baseUrl}/external/${externalDeviceId}`,
    );
  }

  getActive(): Promise<StaffAttendanceDeviceSummary[]> {
    return apiService.get<StaffAttendanceDeviceSummary[]>(`${this.baseUrl}/active`);
  }

  getByLocation(locationId: string): Promise<StaffAttendanceDeviceSummary[]> {
    return apiService.get<StaffAttendanceDeviceSummary[]>(`${this.baseUrl}/location/${locationId}`);
  }

  /** Devices whose last sync is older than `hoursThreshold` (24h server-side default). */
  getOverdueForSync(hoursThreshold?: number): Promise<StaffAttendanceDeviceSummary[]> {
    return apiService.get<StaffAttendanceDeviceSummary[]>(`${this.baseUrl}/overdue-sync`, {
      hoursThreshold,
    });
  }

  register(data: CreateStaffAttendanceDevice): Promise<StaffAttendanceDevice> {
    return apiService.post<StaffAttendanceDevice>(this.baseUrl, data);
  }

  update(id: string, data: UpdateStaffAttendanceDevice): Promise<StaffAttendanceDevice> {
    return apiService.put<StaffAttendanceDevice>(`${this.baseUrl}/${id}`, data);
  }

  /** Stamps the sync time and records how many punches are still queued on the device. */
  recordSync(id: string, pendingSyncCount?: number | null): Promise<StaffAttendanceDevice> {
    return apiService.post<StaffAttendanceDevice>(`${this.baseUrl}/${id}/sync`, {
      pendingSyncCount: pendingSyncCount ?? null,
    });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/** api/staff-attendance-alert-rules — thresholds that raise attendance alerts. */
class AttendanceAlertRuleService {
  private readonly baseUrl = '/staff-attendance-alert-rules';

  getAll(): Promise<StaffAttendanceAlertRuleSummary[]> {
    return apiService.get<StaffAttendanceAlertRuleSummary[]>(this.baseUrl);
  }

  getPaged(page = 1, pageSize = 20): Promise<PagedResult<StaffAttendanceAlertRuleSummary>> {
    return apiService.get<PagedResult<StaffAttendanceAlertRuleSummary>>(`${this.baseUrl}/paged`, {
      pageNumber: page,
      pageSize,
    });
  }

  getById(id: string): Promise<StaffAttendanceAlertRule> {
    return apiService.get<StaffAttendanceAlertRule>(`${this.baseUrl}/${id}`);
  }

  getActive(): Promise<StaffAttendanceAlertRuleSummary[]> {
    return apiService.get<StaffAttendanceAlertRuleSummary[]>(`${this.baseUrl}/active`);
  }

  getByType(alertType: AttendanceAlertTriggerType): Promise<StaffAttendanceAlertRuleSummary[]> {
    return apiService.get<StaffAttendanceAlertRuleSummary[]>(`${this.baseUrl}/type/${alertType}`);
  }

  create(data: CreateAlertRule): Promise<StaffAttendanceAlertRule> {
    return apiService.post<StaffAttendanceAlertRule>(this.baseUrl, data);
  }

  update(id: string, data: UpdateAlertRule): Promise<StaffAttendanceAlertRule> {
    return apiService.put<StaffAttendanceAlertRule>(`${this.baseUrl}/${id}`, data);
  }

  toggleActive(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/toggle-active`);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/** api/position-overtime-policies — overtime eligibility per position, plus per-employee overrides. */
class PositionOvertimePolicyService {
  private readonly baseUrl = '/position-overtime-policies';

  getById(id: string): Promise<PositionOvertimePolicy> {
    return apiService.get<PositionOvertimePolicy>(`${this.baseUrl}/${id}`);
  }

  getByPosition(positionId: string): Promise<PositionOvertimePolicy[]> {
    return apiService.get<PositionOvertimePolicy[]>(`${this.baseUrl}/position/${positionId}`);
  }

  getByAllowanceType(allowanceType: OvertimeAllowanceType): Promise<PositionOvertimePolicy[]> {
    return apiService.get<PositionOvertimePolicy[]>(
      `${this.baseUrl}/allowance-type/${allowanceType}`,
    );
  }

  /**
   * The policy currently in force for a position. A position can hold one policy per
   * allowance type, so the type is required rather than optional.
   */
  getActiveForPosition(
    positionId: string,
    allowanceType: OvertimeAllowanceType,
  ): Promise<PositionOvertimePolicy | null> {
    return apiService.get<PositionOvertimePolicy | null>(
      `${this.baseUrl}/position/${positionId}/active`,
      { allowanceType },
    );
  }

  create(data: CreatePositionOvertimePolicy): Promise<PositionOvertimePolicy> {
    return apiService.post<PositionOvertimePolicy>(this.baseUrl, data);
  }

  update(id: string, data: UpdatePositionOvertimePolicy): Promise<PositionOvertimePolicy> {
    return apiService.put<PositionOvertimePolicy>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  getOverrides(policyId: string): Promise<EmployeeOvertimeOverride[]> {
    return apiService.get<EmployeeOvertimeOverride[]>(`${this.baseUrl}/${policyId}/overrides`);
  }

  addOverride(
    policyId: string,
    data: CreateEmployeeOvertimeOverride,
  ): Promise<EmployeeOvertimeOverride> {
    return apiService.post<EmployeeOvertimeOverride>(`${this.baseUrl}/${policyId}/overrides`, data);
  }

  updateOverride(
    overrideId: string,
    data: UpdateEmployeeOvertimeOverride,
  ): Promise<EmployeeOvertimeOverride> {
    return apiService.put<EmployeeOvertimeOverride>(
      `${this.baseUrl}/overrides/${overrideId}`,
      data,
    );
  }

  removeOverride(overrideId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/overrides/${overrideId}`);
  }
}

/** api/employee-overtime-overrides — the same overrides, reachable per employee. */
class EmployeeOvertimeOverrideService {
  private readonly baseUrl = '/employee-overtime-overrides';

  getById(id: string): Promise<EmployeeOvertimeOverride> {
    return apiService.get<EmployeeOvertimeOverride>(`${this.baseUrl}/${id}`);
  }

  getByEmployee(employeeId: string): Promise<EmployeeOvertimeOverride[]> {
    return apiService.get<EmployeeOvertimeOverride[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  /** Overrides in force today; the effective window is evaluated server-side. */
  getActiveForEmployee(employeeId: string): Promise<EmployeeOvertimeOverride[]> {
    return apiService.get<EmployeeOvertimeOverride[]>(
      `${this.baseUrl}/employee/${employeeId}/active`,
    );
  }

  create(data: CreateEmployeeOvertimeOverride): Promise<EmployeeOvertimeOverride> {
    return apiService.post<EmployeeOvertimeOverride>(this.baseUrl, data);
  }

  update(id: string, data: UpdateEmployeeOvertimeOverride): Promise<EmployeeOvertimeOverride> {
    return apiService.put<EmployeeOvertimeOverride>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/** api/employee-biometrics — enrolled templates backing device recognition. */
class EmployeeBiometricService {
  private readonly baseUrl = '/employee-biometrics';

  getPaged(page = 1, pageSize = 20): Promise<PagedResult<EmployeeBiometricSummary>> {
    return apiService.get<PagedResult<EmployeeBiometricSummary>>(`${this.baseUrl}/paged`, {
      pageNumber: page,
      pageSize,
    });
  }

  getById(id: string): Promise<EmployeeBiometric> {
    return apiService.get<EmployeeBiometric>(`${this.baseUrl}/${id}`);
  }

  getByEmployee(employeeId: string): Promise<EmployeeBiometricSummary[]> {
    return apiService.get<EmployeeBiometricSummary[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  getActiveForEmployee(employeeId: string): Promise<EmployeeBiometricSummary[]> {
    return apiService.get<EmployeeBiometricSummary[]>(
      `${this.baseUrl}/employee/${employeeId}/active`,
    );
  }

  getByEmployeeAndType(
    employeeId: string,
    type: BiometricType,
  ): Promise<EmployeeBiometric | null> {
    return apiService.get<EmployeeBiometric | null>(
      `${this.baseUrl}/employee/${employeeId}/type/${type}`,
    );
  }

  getByType(type: BiometricType): Promise<EmployeeBiometricSummary[]> {
    return apiService.get<EmployeeBiometricSummary[]>(`${this.baseUrl}/type/${type}`);
  }

  enrol(data: EnrollBiometric): Promise<EmployeeBiometric> {
    return apiService.post<EmployeeBiometric>(`${this.baseUrl}/enrol`, data);
  }

  update(
    id: string,
    data: { id: string; biometricData?: string | null; templateFormat?: string | null; qualityScore?: number | null },
  ): Promise<EmployeeBiometric> {
    return apiService.put<EmployeeBiometric>(`${this.baseUrl}/${id}`, data);
  }

  /**
   * Deactivates the template without deleting it, keeping the audit trail.
   * The endpoint takes `{ reason }` — not the `RevokeBiometricDto` shape.
   */
  revoke(id: string, reason: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/revoke`, { reason });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

export const workScheduleService = new WorkScheduleService();
export const shiftDefinitionService = new ShiftDefinitionService();
export const employeeWorkScheduleService = new EmployeeWorkScheduleService();
export const shiftAssignmentService = new ShiftAssignmentService();
export const shiftRotationPlanService = new ShiftRotationPlanService();
export const holidayCalendarService = new HolidayCalendarService();
export const payPeriodService = new PayPeriodService();
export const geofenceZoneService = new GeofenceZoneService();
export const attendanceDeviceService = new AttendanceDeviceService();
export const alertRuleService = new AttendanceAlertRuleService();
export const positionOvertimePolicyService = new PositionOvertimePolicyService();
export const employeeOvertimeOverrideService = new EmployeeOvertimeOverrideService();
export const employeeBiometricService = new EmployeeBiometricService();
