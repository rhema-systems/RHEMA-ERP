/**
 * Attendance & Time — types mirroring ErpSystem.Core.DTOs.HR.AttendanceDTOs.
 *
 * Routes are spread across ~20 controllers with no shared prefix; each service class in
 * `services/hr/attendance*.service.ts` documents the one it targets.
 *
 * Wire-format notes (the API uses JsonStringEnumConverter + camelCase):
 *   - every C# enum arrives as its member NAME, e.g. `"Present"`, not `1`
 *   - `DateOnly`  → `"2026-08-04"`
 *   - `TimeOnly`  → `"08:30:00"`
 *   - `TimeSpan`  → `"08:30:00"` (may exceed 24h in principle; attendance never does)
 *   - `DayOfWeek` → `"Monday"`
 * Read DTOs also carry a redundant `<field>Name` string beside each enum; prefer the enum
 * field and format it in the UI so labels stay in one place.
 */
import type { AuditFields } from './common';

// ── Enums ────────────────────────────────────────────────────────────────────────

export type StaffAttendanceStatus =
  | 'Present'
  | 'Absent'
  | 'Late'
  | 'HalfDay'
  | 'OnLeave'
  | 'PublicHoliday'
  | 'Weekend'
  | 'OffDay'
  | 'RemoteWork'
  | 'OnDuty';

export type LocationVerificationStatus =
  | 'WithinZone'
  | 'OutsideZone'
  | 'Unverified'
  | 'GPSUnavailable';

export type AttendanceLogType = 'CheckIn' | 'CheckOut' | 'BreakStart' | 'BreakEnd';

export type RegularizationType =
  | 'MissingCheckIn'
  | 'MissingCheckOut'
  | 'WrongTimeEntry'
  | 'ForgotToMark'
  | 'SystemError';

export type AttendanceRegularizationStatus = 'Pending' | 'Approved' | 'Rejected' | 'Applied';

export type AttendanceImportSourceType =
  | 'CSV'
  | 'Excel'
  | 'API'
  | 'BiometricDevice'
  | 'ManualEntry';

export type AttendanceImportStatus =
  | 'Pending'
  | 'Processing'
  | 'Completed'
  | 'Failed'
  | 'PartialSuccess';

export type WorkScheduleType = 'Fixed' | 'Flexible' | 'Shift' | 'Compressed' | 'PartTime';

export type ShiftType = 'Morning' | 'Afternoon' | 'Evening' | 'Night' | 'Rotating' | 'Split';

export type ShiftRotationCycle = 'Weekly' | 'Fortnightly' | 'Monthly' | 'Quarterly';

export type RecurrencePattern =
  | 'Daily'
  | 'Weekly'
  | 'BiWeekly'
  | 'Monthly'
  | 'Quarterly'
  | 'Annually';

export type OvertimeAllowanceType =
  | 'Overtime'
  | 'NightAllowance'
  | 'ShiftDifferential'
  | 'WeekendAllowance'
  | 'HolidayAllowance'
  | 'TransportAllowance'
  | 'Other';

export type OvertimeType = 'Weekday' | 'Weekend' | 'Holiday' | 'Emergency';

export type OvertimeRequestStatus =
  | 'Pending'
  | 'Approved'
  | 'Rejected'
  | 'Completed'
  | 'Cancelled';

export type BiometricType = 'Fingerprint' | 'Face' | 'Iris' | 'Palm' | 'Vein' | 'Retina';

export type FingerPosition =
  | 'RightThumb'
  | 'RightIndex'
  | 'RightMiddle'
  | 'RightRing'
  | 'RightLittle'
  | 'LeftThumb'
  | 'LeftIndex'
  | 'LeftMiddle'
  | 'LeftRing'
  | 'LeftLittle';

export type AttendanceDeviceType =
  | 'Fingerprint'
  | 'FaceRecognition'
  | 'RFIDCard'
  | 'Iris'
  | 'Palm'
  | 'QRCode'
  | 'PINPad'
  | 'MobileApp'
  | 'WebPortal';

export type GeofenceShape = 'Circle' | 'Polygon';

export type RemoteWorkRequestStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';

export type HolidayObservanceType = 'Mandatory' | 'Optional' | 'SubstituteDay';

export type PayPeriodType = 'Weekly' | 'Biweekly' | 'SemiMonthly' | 'Monthly';

export type PayPeriodStatus = 'Open' | 'PendingClose' | 'Closed' | 'ExportedToPayroll';

export type PayrollExportStatus =
  | 'Pending'
  | 'InProgress'
  | 'Completed'
  | 'Failed'
  | 'PartialSuccess';

export type AttendanceAlertTriggerType =
  | 'ConsecutiveAbsences'
  | 'ChronicLateness'
  | 'MissingPunch'
  | 'OvertimeThresholdReached'
  | 'ExcessiveEarlyDeparture'
  | 'UnauthorisedAbsence'
  | 'LowAttendancePercentage';

export type AttendanceAlertSeverity = 'Info' | 'Warning' | 'Critical';

export type AttendanceAlertStatus = 'Active' | 'Acknowledged' | 'Resolved' | 'Dismissed';

export type DayOfWeekName =
  | 'Sunday'
  | 'Monday'
  | 'Tuesday'
  | 'Wednesday'
  | 'Thursday'
  | 'Friday'
  | 'Saturday';

// ── Select options ───────────────────────────────────────────────────────────────
// Kept beside the unions so a new enum member fails the type-check here rather than
// silently dropping out of a dropdown.

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

export const ATTENDANCE_STATUS_OPTIONS = opts<StaffAttendanceStatus>([
  ['Present', 'Present'],
  ['Absent', 'Absent'],
  ['Late', 'Late'],
  ['HalfDay', 'Half day'],
  ['OnLeave', 'On leave'],
  ['PublicHoliday', 'Public holiday'],
  ['Weekend', 'Weekend'],
  ['OffDay', 'Off day'],
  ['RemoteWork', 'Remote work'],
  ['OnDuty', 'On duty'],
]);

export const ATTENDANCE_LOG_TYPE_OPTIONS = opts<AttendanceLogType>([
  ['CheckIn', 'Check in'],
  ['CheckOut', 'Check out'],
  ['BreakStart', 'Break start'],
  ['BreakEnd', 'Break end'],
]);

export const REGULARIZATION_TYPE_OPTIONS = opts<RegularizationType>([
  ['MissingCheckIn', 'Missing check-in'],
  ['MissingCheckOut', 'Missing check-out'],
  ['WrongTimeEntry', 'Wrong time entry'],
  ['ForgotToMark', 'Forgot to mark'],
  ['SystemError', 'System error'],
]);

export const REGULARIZATION_STATUS_OPTIONS = opts<AttendanceRegularizationStatus>([
  ['Pending', 'Pending'],
  ['Approved', 'Approved'],
  ['Rejected', 'Rejected'],
  ['Applied', 'Applied'],
]);

export const WORK_SCHEDULE_TYPE_OPTIONS = opts<WorkScheduleType>([
  ['Fixed', 'Fixed'],
  ['Flexible', 'Flexible'],
  ['Shift', 'Shift'],
  ['Compressed', 'Compressed'],
  ['PartTime', 'Part time'],
]);

export const SHIFT_TYPE_OPTIONS = opts<ShiftType>([
  ['Morning', 'Morning'],
  ['Afternoon', 'Afternoon'],
  ['Evening', 'Evening'],
  ['Night', 'Night'],
  ['Rotating', 'Rotating'],
  ['Split', 'Split'],
]);

export const SHIFT_ROTATION_CYCLE_OPTIONS = opts<ShiftRotationCycle>([
  ['Weekly', 'Weekly'],
  ['Fortnightly', 'Fortnightly'],
  ['Monthly', 'Monthly'],
  ['Quarterly', 'Quarterly'],
]);

export const RECURRENCE_PATTERN_OPTIONS = opts<RecurrencePattern>([
  ['Daily', 'Daily'],
  ['Weekly', 'Weekly'],
  ['BiWeekly', 'Bi-weekly'],
  ['Monthly', 'Monthly'],
  ['Quarterly', 'Quarterly'],
  ['Annually', 'Annually'],
]);

export const OVERTIME_ALLOWANCE_TYPE_OPTIONS = opts<OvertimeAllowanceType>([
  ['Overtime', 'Overtime'],
  ['NightAllowance', 'Night allowance'],
  ['ShiftDifferential', 'Shift differential'],
  ['WeekendAllowance', 'Weekend allowance'],
  ['HolidayAllowance', 'Holiday allowance'],
  ['TransportAllowance', 'Transport allowance'],
  ['Other', 'Other'],
]);

export const OVERTIME_TYPE_OPTIONS = opts<OvertimeType>([
  ['Weekday', 'Weekday'],
  ['Weekend', 'Weekend'],
  ['Holiday', 'Holiday'],
  ['Emergency', 'Emergency'],
]);

export const OVERTIME_REQUEST_STATUS_OPTIONS = opts<OvertimeRequestStatus>([
  ['Pending', 'Pending'],
  ['Approved', 'Approved'],
  ['Rejected', 'Rejected'],
  ['Completed', 'Completed'],
  ['Cancelled', 'Cancelled'],
]);

export const BIOMETRIC_TYPE_OPTIONS = opts<BiometricType>([
  ['Fingerprint', 'Fingerprint'],
  ['Face', 'Face'],
  ['Iris', 'Iris'],
  ['Palm', 'Palm'],
  ['Vein', 'Vein'],
  ['Retina', 'Retina'],
]);

export const FINGER_POSITION_OPTIONS = opts<FingerPosition>([
  ['RightThumb', 'Right thumb'],
  ['RightIndex', 'Right index'],
  ['RightMiddle', 'Right middle'],
  ['RightRing', 'Right ring'],
  ['RightLittle', 'Right little'],
  ['LeftThumb', 'Left thumb'],
  ['LeftIndex', 'Left index'],
  ['LeftMiddle', 'Left middle'],
  ['LeftRing', 'Left ring'],
  ['LeftLittle', 'Left little'],
]);

export const ATTENDANCE_DEVICE_TYPE_OPTIONS = opts<AttendanceDeviceType>([
  ['Fingerprint', 'Fingerprint'],
  ['FaceRecognition', 'Face recognition'],
  ['RFIDCard', 'RFID card'],
  ['Iris', 'Iris'],
  ['Palm', 'Palm'],
  ['QRCode', 'QR code'],
  ['PINPad', 'PIN pad'],
  ['MobileApp', 'Mobile app'],
  ['WebPortal', 'Web portal'],
]);

export const GEOFENCE_SHAPE_OPTIONS = opts<GeofenceShape>([
  ['Circle', 'Circle'],
  ['Polygon', 'Polygon'],
]);

export const REMOTE_WORK_STATUS_OPTIONS = opts<RemoteWorkRequestStatus>([
  ['Pending', 'Pending'],
  ['Approved', 'Approved'],
  ['Rejected', 'Rejected'],
  ['Cancelled', 'Cancelled'],
]);

export const HOLIDAY_OBSERVANCE_TYPE_OPTIONS = opts<HolidayObservanceType>([
  ['Mandatory', 'Mandatory'],
  ['Optional', 'Optional'],
  ['SubstituteDay', 'Substitute day'],
]);

export const PAY_PERIOD_TYPE_OPTIONS = opts<PayPeriodType>([
  ['Weekly', 'Weekly'],
  ['Biweekly', 'Bi-weekly'],
  ['SemiMonthly', 'Semi-monthly'],
  ['Monthly', 'Monthly'],
]);

export const PAY_PERIOD_STATUS_OPTIONS = opts<PayPeriodStatus>([
  ['Open', 'Open'],
  ['PendingClose', 'Pending close'],
  ['Closed', 'Closed'],
  ['ExportedToPayroll', 'Exported to payroll'],
]);

export const PAYROLL_EXPORT_STATUS_OPTIONS = opts<PayrollExportStatus>([
  ['Pending', 'Pending'],
  ['InProgress', 'In progress'],
  ['Completed', 'Completed'],
  ['Failed', 'Failed'],
  ['PartialSuccess', 'Partial success'],
]);

export const IMPORT_SOURCE_TYPE_OPTIONS = opts<AttendanceImportSourceType>([
  ['CSV', 'CSV'],
  ['Excel', 'Excel'],
  ['API', 'API'],
  ['BiometricDevice', 'Biometric device'],
  ['ManualEntry', 'Manual entry'],
]);

export const IMPORT_STATUS_OPTIONS = opts<AttendanceImportStatus>([
  ['Pending', 'Pending'],
  ['Processing', 'Processing'],
  ['Completed', 'Completed'],
  ['Failed', 'Failed'],
  ['PartialSuccess', 'Partial success'],
]);

export const ALERT_TRIGGER_TYPE_OPTIONS = opts<AttendanceAlertTriggerType>([
  ['ConsecutiveAbsences', 'Consecutive absences'],
  ['ChronicLateness', 'Chronic lateness'],
  ['MissingPunch', 'Missing punch'],
  ['OvertimeThresholdReached', 'Overtime threshold reached'],
  ['ExcessiveEarlyDeparture', 'Excessive early departure'],
  ['UnauthorisedAbsence', 'Unauthorised absence'],
  ['LowAttendancePercentage', 'Low attendance percentage'],
]);

export const ALERT_SEVERITY_OPTIONS = opts<AttendanceAlertSeverity>([
  ['Info', 'Info'],
  ['Warning', 'Warning'],
  ['Critical', 'Critical'],
]);

export const ALERT_STATUS_OPTIONS = opts<AttendanceAlertStatus>([
  ['Active', 'Active'],
  ['Acknowledged', 'Acknowledged'],
  ['Resolved', 'Resolved'],
  ['Dismissed', 'Dismissed'],
]);

// ── Daily attendance ─────────────────────────────────────────────────────────────

export interface StaffDailyAttendanceSummary {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  /** DateOnly */
  attendanceDate: string;
  dayOfWeek: DayOfWeekName;
  /** TimeSpan */
  actualCheckInTime?: string | null;
  actualCheckOutTime?: string | null;
  actualWorkHours?: number | null;
  status: StaffAttendanceStatus;
  isLate: boolean;
  lateMinutes?: number | null;
  isOvertime: boolean;
  overtimeHours?: number | null;
  isRemoteWork: boolean;
  hasException: boolean;
  isVerified: boolean;
}

export interface StaffDailyAttendance extends AuditFields {
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  attendanceDate: string;
  dayOfWeek: DayOfWeekName;

  workScheduleId?: string | null;
  workScheduleName?: string | null;
  scheduledStartTime?: string | null;
  scheduledEndTime?: string | null;
  scheduledWorkHours?: number | null;

  actualCheckInTime?: string | null;
  actualCheckOutTime?: string | null;
  actualWorkHours?: number | null;

  status: StaffAttendanceStatus;
  statusReason?: string | null;

  isLate: boolean;
  lateMinutes?: number | null;
  isEarlyDeparture: boolean;
  earlyDepartureMinutes?: number | null;

  breakStartTime?: string | null;
  breakEndTime?: string | null;
  totalBreakMinutes?: number | null;

  isOvertime: boolean;
  overtimeHours?: number | null;
  overtimeApproved: boolean;
  overtimeApprovedById?: string | null;
  overtimeApprovedByName?: string | null;

  locationId?: string | null;
  locationName?: string | null;
  checkInLocation?: string | null;
  checkOutLocation?: string | null;
  checkInLatitude?: number | null;
  checkInLongitude?: number | null;
  checkOutLatitude?: number | null;
  checkOutLongitude?: number | null;
  checkInLocationStatus: LocationVerificationStatus;
  checkOutLocationStatus: LocationVerificationStatus;
  checkInGeofenceZoneId?: string | null;
  checkInGeofenceZoneName?: string | null;

  isRemoteWork: boolean;
  remoteWorkLocation?: string | null;
  remoteWorkRequestId?: string | null;

  checkInDevice?: string | null;
  checkInIpAddress?: string | null;
  checkOutDevice?: string | null;
  checkOutIpAddress?: string | null;

  leaveRequestId?: string | null;
  publicHolidayId?: string | null;
  publicHolidayName?: string | null;
  payPeriodId?: string | null;
  payPeriodName?: string | null;

  requiresVerification: boolean;
  isVerified: boolean;
  verifiedDate?: string | null;
  verifiedById?: string | null;
  verifiedByName?: string | null;
  verificationNotes?: string | null;

  hasException: boolean;
  exceptionReason?: string | null;
  exceptionApproved: boolean;
  exceptionApprovedById?: string | null;
  exceptionApprovedByName?: string | null;

  notes?: string | null;

  attendanceLogs: StaffAttendanceLogSummary[];
  regularizations: StaffAttendanceRegularizationSummary[];
}

export interface CreateStaffDailyAttendance {
  employeeId: string;
  attendanceDate: string;
  workScheduleId?: string | null;
  scheduledStartTime?: string | null;
  scheduledEndTime?: string | null;
  scheduledWorkHours?: number | null;
  actualCheckInTime?: string | null;
  actualCheckOutTime?: string | null;
  actualWorkHours?: number | null;
  status: StaffAttendanceStatus;
  statusReason?: string | null;
  isLate?: boolean;
  lateMinutes?: number | null;
  isEarlyDeparture?: boolean;
  earlyDepartureMinutes?: number | null;
  breakStartTime?: string | null;
  breakEndTime?: string | null;
  totalBreakMinutes?: number | null;
  isOvertime?: boolean;
  overtimeHours?: number | null;
  locationId?: string | null;
  isRemoteWork?: boolean;
  remoteWorkLocation?: string | null;
  payPeriodId?: string | null;
  notes?: string | null;
}

export interface UpdateStaffDailyAttendance extends CreateStaffDailyAttendance {
  id: string;
}

export interface VerifyAttendanceRequest {
  attendanceId: string;
  verifiedById: string;
  verifiedDate?: string;
  verificationNotes?: string | null;
}

export interface ApproveAttendanceExceptionRequest {
  attendanceId: string;
  approvedById: string;
  comments?: string | null;
}

// ── Attendance records (the light-weight clock in/out table) ─────────────────────

export interface StaffAttendanceRecordSummary {
  id: string;
  employeeId: string;
  employeeName: string;
  /** DateOnly */
  date: string;
  /** TimeOnly */
  checkInTime?: string | null;
  checkOutTime?: string | null;
  workedHours?: number | null;
  status: StaffAttendanceStatus;
}

export interface StaffAttendanceRecord extends AuditFields {
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  date: string;
  checkInTime?: string | null;
  checkOutTime?: string | null;
  workedHours?: number | null;
  overtimeHours?: number | null;
  status: StaffAttendanceStatus;
  notes?: string | null;
}

export interface CreateStaffAttendanceRecord {
  employeeId: string;
  date: string;
  checkInTime?: string | null;
  checkOutTime?: string | null;
  workedHours?: number | null;
  overtimeHours?: number | null;
  status: StaffAttendanceStatus;
  notes?: string | null;
}

export interface UpdateStaffAttendanceRecord {
  id: string;
  checkInTime?: string | null;
  checkOutTime?: string | null;
  workedHours?: number | null;
  overtimeHours?: number | null;
  status: StaffAttendanceStatus;
  notes?: string | null;
}

// ── Raw punch logs ───────────────────────────────────────────────────────────────

export interface StaffAttendanceLogSummary {
  id: string;
  employeeId: string;
  employeeName: string;
  /** DateTime */
  logDateTime: string;
  logType: AttendanceLogType;
  deviceSerialNumber?: string | null;
  isProcessed: boolean;
  attendanceId?: string | null;
}

export interface AttendanceLocationVerificationLog extends AuditFields {
  attendanceLogId: string;
  employeeId: string;
  employeeName: string;
  verificationDateTime: string;
  latitude: number;
  longitude: number;
  geofenceZoneId?: string | null;
  geofenceZoneName?: string | null;
  distanceFromZoneMetres?: number | null;
  status: LocationVerificationStatus;
  notes?: string | null;
}

export interface StaffAttendanceLog extends AuditFields {
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  logDateTime: string;
  logType: AttendanceLogType;
  deviceId?: string | null;
  deviceSerialNumber?: string | null;
  location?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  isProcessed: boolean;
  processedDate?: string | null;
  attendanceId?: string | null;
  rawData?: string | null;
  verificationLogs: AttendanceLocationVerificationLog[];
}

export interface CreateStaffAttendanceLog {
  employeeId: string;
  logDateTime: string;
  logType: AttendanceLogType;
  deviceId?: string | null;
  deviceSerialNumber?: string | null;
  location?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  rawData?: string | null;
}

export interface GeofenceVerificationSummary {
  status: LocationVerificationStatus;
  geofenceZoneId?: string | null;
  geofenceZoneName?: string | null;
  distanceFromZoneMetres?: number | null;
  hasConfiguredZone: boolean;
  hasGpsCoordinates: boolean;
  message?: string | null;
}

export interface StaffAttendancePunchRequest {
  logType: AttendanceLogType;
  latitude?: number | null;
  longitude?: number | null;
  location?: string | null;
  /** Roll the punch into the daily attendance record straight away. Defaults true. */
  processImmediately?: boolean;
}

export interface StaffAttendancePunchResult {
  log: StaffAttendanceLog;
  verification?: GeofenceVerificationSummary | null;
  dailyAttendanceId?: string | null;
  processedImmediately: boolean;
}

// ── Regularizations (workflow-approved) ──────────────────────────────────────────

export interface StaffAttendanceRegularizationSummary {
  id: string;
  regularizationNumber: string;
  employeeId: string;
  employeeName: string;
  attendanceDate: string;
  type: RegularizationType;
  status: AttendanceRegularizationStatus;
  requestDate: string;
  isApplied: boolean;
}

export interface StaffAttendanceRegularization extends AuditFields {
  regularizationNumber: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  attendanceId: string;
  attendanceDate: string;
  requestDate: string;
  type: RegularizationType;
  /** TimeSpan */
  requestedCheckInTime?: string | null;
  requestedCheckOutTime?: string | null;
  reason: string;
  supportingDocuments?: string | null;
  status: AttendanceRegularizationStatus;
  approvedById?: string | null;
  approvedByName?: string | null;
  approvalDate?: string | null;
  approvalComments?: string | null;
  rejectedDate?: string | null;
  rejectionReason?: string | null;
  isApplied: boolean;
  appliedDate?: string | null;
}

export interface CreateStaffAttendanceRegularization {
  employeeId: string;
  attendanceId: string;
  type: RegularizationType;
  requestedCheckInTime?: string | null;
  requestedCheckOutTime?: string | null;
  reason: string;
  supportingDocuments?: string | null;
}

export interface UpdateStaffAttendanceRegularization {
  id: string;
  requestedCheckInTime?: string | null;
  requestedCheckOutTime?: string | null;
  reason: string;
  supportingDocuments?: string | null;
}

export interface ApproveRegularizationRequest {
  regularizationId: string;
  approvedById: string;
  approvalDate?: string;
  approvalComments?: string | null;
}

export interface RejectRegularizationRequest {
  regularizationId: string;
  rejectionReason: string;
}

// ── Monthly summaries ────────────────────────────────────────────────────────────

export interface StaffMonthlyAttendanceSummary extends AuditFields {
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  year: number;
  month: number;
  monthName: string;

  totalWorkingDays: number;
  daysPresent: number;
  daysAbsent: number;
  daysOnLeave: number;
  daysLate: number;
  daysRemoteWork: number;
  publicHolidays: number;
  weekends: number;
  daysHalfDay: number;

  totalScheduledHours: number;
  totalWorkedHours: number;
  totalOvertimeHours: number;
  totalUndertimeHours: number;
  totalBreakHours: number;

  totalLateMinutes: number;
  numberOfLateDays: number;
  totalEarlyDepartureMinutes: number;
  numberOfEarlyDepartureDays: number;

  attendancePercentage: number;
  punctualityPercentage: number;

  payPeriodId?: string | null;
  payPeriodName?: string | null;

  isFinalized: boolean;
  finalizedDate?: string | null;
  finalizedById?: string | null;
  finalizedByName?: string | null;
  notes?: string | null;
}

export interface UpdateMonthlyAttendanceSummary {
  id: string;
  notes?: string | null;
  daysPresent?: number | null;
  daysAbsent?: number | null;
  daysOnLeave?: number | null;
  totalLateMinutes?: number | null;
  totalOvertimeHours?: number | null;
}

// ── Bulk imports ─────────────────────────────────────────────────────────────────

export interface StaffBulkAttendanceImportSummary {
  id: string;
  importReference: string;
  importedByName: string;
  importDate: string;
  sourceFileName?: string | null;
  sourceType: AttendanceImportSourceType;
  totalRows: number;
  successCount: number;
  failureCount: number;
  status: AttendanceImportStatus;
  completedDate?: string | null;
}

export interface StaffBulkAttendanceImportRow extends AuditFields {
  importId: string;
  rowNumber: number;
  employeeId?: string | null;
  employeeName?: string | null;
  rawData: string;
  attendanceDate?: string | null;
  checkInTime?: string | null;
  checkOutTime?: string | null;
  isSuccess: boolean;
  errorMessage?: string | null;
  createdAttendanceId?: string | null;
}

export interface StaffBulkAttendanceImport extends AuditFields {
  importReference: string;
  importedById: string;
  importedByName: string;
  importDate: string;
  sourceFileName?: string | null;
  sourceType: AttendanceImportSourceType;
  totalRows: number;
  successCount: number;
  failureCount: number;
  status: AttendanceImportStatus;
  errorSummary?: string | null;
  completedDate?: string | null;
  notes?: string | null;
  importRows: StaffBulkAttendanceImportRow[];
}

export interface CreateBulkImportRow {
  rowNumber: number;
  rawData: string;
  employeeId?: string | null;
  attendanceDate?: string | null;
  checkInTime?: string | null;
  checkOutTime?: string | null;
}

export interface CreateBulkAttendanceImport {
  importedById: string;
  sourceFileName?: string | null;
  sourceType: AttendanceImportSourceType;
  notes?: string | null;
  rows: CreateBulkImportRow[];
}

// ── Work schedules ───────────────────────────────────────────────────────────────

export interface WorkScheduleSummary {
  id: string;
  scheduleName: string;
  type: WorkScheduleType;
  isDefault: boolean;
  isActive: boolean;
  /** TimeSpan */
  standardStartTime: string;
  standardEndTime: string;
  standardHoursPerDay: number;
  standardHoursPerWeek: number;
  shiftCount: number;
}

export interface WorkSchedule extends AuditFields {
  scheduleName: string;
  description?: string | null;
  type: WorkScheduleType;
  isDefault: boolean;
  isActive: boolean;

  standardStartTime: string;
  standardEndTime: string;
  standardHoursPerDay: number;
  standardHoursPerWeek: number;

  hasFlexibleStartTime: boolean;
  flexibleStartTimeEarliest?: string | null;
  flexibleStartTimeLatest?: string | null;
  hasFlexibleEndTime: boolean;
  flexibleEndTimeEarliest?: string | null;
  flexibleEndTimeLatest?: string | null;

  hasCoreHours: boolean;
  coreHoursStart?: string | null;
  coreHoursEnd?: string | null;

  hasMandatoryBreak: boolean;
  breakDurationMinutes?: number | null;
  isBreakPaid: boolean;

  worksMonday: boolean;
  worksTuesday: boolean;
  worksWednesday: boolean;
  worksThursday: boolean;
  worksFriday: boolean;
  worksSaturday: boolean;
  worksSunday: boolean;

  allowsOvertime: boolean;
  overtimeRequiresPreApproval: boolean;
  maxOvertimeHoursPerDay?: number | null;
  maxOvertimeHoursPerWeek?: number | null;

  lateGracePeriodMinutes?: number | null;
  earlyDepartureGracePeriodMinutes?: number | null;

  shifts: ShiftDefinitionSummary[];
}

export type CreateWorkSchedule = Omit<WorkSchedule, keyof AuditFields | 'shifts'>;
export interface UpdateWorkSchedule extends CreateWorkSchedule {
  id: string;
}

// ── Employee → work schedule assignment ──────────────────────────────────────────

export interface EmployeeWorkSchedule extends AuditFields {
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  workScheduleId: string;
  workScheduleName: string;
  workScheduleType: WorkScheduleType;
  /** DateOnly */
  effectiveDate: string;
  endDate?: string | null;
  isCurrent: boolean;
  assignmentReason?: string | null;
  assignedById?: string | null;
  assignedByName?: string | null;
}

export interface AssignEmployeeWorkSchedule {
  employeeId: string;
  workScheduleId: string;
  effectiveDate: string;
  endDate?: string | null;
  assignmentReason?: string | null;
  assignedById?: string | null;
}

export interface UpdateEmployeeWorkSchedule {
  id: string;
  workScheduleId: string;
  effectiveDate: string;
  endDate?: string | null;
  assignmentReason?: string | null;
}

// ── Shift definitions ────────────────────────────────────────────────────────────

export interface ShiftDefinitionSummary {
  id: string;
  workScheduleId: string;
  workScheduleName: string;
  shiftName: string;
  type: ShiftType;
  startTime: string;
  endTime: string;
  shiftHours: number;
  isNightShift: boolean;
  isActive: boolean;
  displayOrder: number;
}

export interface ShiftDefinition extends AuditFields {
  workScheduleId: string;
  workScheduleName: string;
  shiftName: string;
  description?: string | null;
  type: ShiftType;
  startTime: string;
  endTime: string;
  shiftHours: number;
  isNightShift: boolean;
  attractsNightAllowance: boolean;
  allowsOvertime: boolean;
  hasShiftDifferential: boolean;
  shiftDifferentialPercentage?: number | null;
  displayOrder: number;
  isActive: boolean;
}

export interface CreateShiftDefinition {
  workScheduleId: string;
  shiftName: string;
  description?: string | null;
  type: ShiftType;
  startTime: string;
  endTime: string;
  shiftHours: number;
  isNightShift: boolean;
  attractsNightAllowance: boolean;
  allowsOvertime: boolean;
  hasShiftDifferential: boolean;
  shiftDifferentialPercentage?: number | null;
  displayOrder: number;
  isActive: boolean;
}

export interface UpdateShiftDefinition extends Omit<CreateShiftDefinition, 'workScheduleId'> {
  id: string;
}

// ── Shift assignments ────────────────────────────────────────────────────────────

export interface ShiftAssignment extends AuditFields {
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  shiftDefinitionId: string;
  shiftName: string;
  shiftStartTime: string;
  shiftEndTime: string;
  /** DateTime — not DateOnly, unlike most other assignment windows. */
  assignmentDate: string;
  endDate?: string | null;
  isRecurring: boolean;
  recurrencePattern?: RecurrencePattern | null;
  assignedById?: string | null;
  assignedByName?: string | null;
  notes?: string | null;
}

export interface CreateShiftAssignment {
  employeeId: string;
  shiftDefinitionId: string;
  assignmentDate: string;
  endDate?: string | null;
  isRecurring: boolean;
  recurrencePattern?: RecurrencePattern | null;
  assignedById?: string | null;
  notes?: string | null;
}

export interface UpdateShiftAssignment extends Omit<CreateShiftAssignment, 'employeeId' | 'assignedById'> {
  id: string;
}

// ── Shift rotation plans ─────────────────────────────────────────────────────────

export interface ShiftRotationPlanSummary {
  id: string;
  planName: string;
  rotationCycle: ShiftRotationCycle;
  cycleLengthDays: number;
  startDate: string;
  endDate?: string | null;
  isActive: boolean;
  stageCount: number;
  memberCount: number;
}

export interface ShiftRotationStage extends AuditFields {
  shiftRotationPlanId: string;
  stageOrder: number;
  shiftDefinitionId: string;
  shiftName: string;
  shiftStartTime: string;
  shiftEndTime: string;
  durationCycles: number;
  label?: string | null;
}

export interface ShiftRotationMember extends AuditFields {
  shiftRotationPlanId: string;
  planName: string;
  employeeId?: string | null;
  employeeName?: string | null;
  employeeNumber?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  teamId?: string | null;
  teamName?: string | null;
  currentStageOrder: number;
  joinDate: string;
  exitDate?: string | null;
  notes?: string | null;
}

export interface ShiftRotationPlan extends AuditFields {
  planName: string;
  description?: string | null;
  rotationCycle: ShiftRotationCycle;
  cycleLengthDays: number;
  startDate: string;
  endDate?: string | null;
  isActive: boolean;
  notes?: string | null;
  stages: ShiftRotationStage[];
  members: ShiftRotationMember[];
}

export interface CreateShiftRotationPlan {
  planName: string;
  description?: string | null;
  rotationCycle: ShiftRotationCycle;
  cycleLengthDays: number;
  startDate: string;
  endDate?: string | null;
  isActive: boolean;
  notes?: string | null;
}

/** The cycle and start date are fixed once members have begun rotating. */
export interface UpdateShiftRotationPlan {
  id: string;
  planName: string;
  description?: string | null;
  endDate?: string | null;
  isActive: boolean;
  notes?: string | null;
}

export interface CreateShiftRotationStage {
  shiftRotationPlanId: string;
  stageOrder: number;
  shiftDefinitionId: string;
  durationCycles: number;
  label?: string | null;
}

/** Note the field drift from the create DTO: `durationDays` + `notes`, not `durationCycles` + `label`. */
export interface UpdateShiftRotationStage {
  id: string;
  stageOrder: number;
  shiftDefinitionId: string;
  durationDays: number;
  notes?: string | null;
}

export interface AddShiftRotationMember {
  shiftRotationPlanId: string;
  employeeId?: string | null;
  organizationUnitId?: string | null;
  teamId?: string | null;
  joinDate: string;
  notes?: string | null;
}

export interface UpdateShiftRotationMember {
  id: string;
  currentStageOrder: number;
  nextRotationDate?: string | null;
  notes?: string | null;
}

// ── Overtime policies & overrides ────────────────────────────────────────────────

export interface PositionOvertimePolicy extends AuditFields {
  positionId: string;
  positionTitle: string;
  allowanceType: OvertimeAllowanceType;
  isEligible: boolean;
  isExempt: boolean;
  exemptionReason?: string | null;
  maxHoursPerDay?: number | null;
  maxHoursPerWeek?: number | null;
  requiresPreApproval: boolean;
  effectiveDate: string;
  expiryDate?: string | null;
  notes?: string | null;
}

export interface CreatePositionOvertimePolicy {
  positionId: string;
  allowanceType: OvertimeAllowanceType;
  isEligible: boolean;
  isExempt: boolean;
  exemptionReason?: string | null;
  maxHoursPerDay?: number | null;
  maxHoursPerWeek?: number | null;
  requiresPreApproval: boolean;
  effectiveDate: string;
  expiryDate?: string | null;
  notes?: string | null;
}

/** The position and allowance type are immutable — create a new policy to change them. */
export interface UpdatePositionOvertimePolicy {
  id: string;
  isEligible: boolean;
  isExempt: boolean;
  exemptionReason?: string | null;
  maxHoursPerDay?: number | null;
  maxHoursPerWeek?: number | null;
  requiresPreApproval: boolean;
  effectiveDate: string;
  expiryDate?: string | null;
  notes?: string | null;
}

export interface EmployeeOvertimeOverride extends AuditFields {
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  policyId?: string | null;
  policyDescription?: string | null;
  allowanceType: OvertimeAllowanceType;
  isEligible: boolean;
  isExempt: boolean;
  overrideReason: string;
  approvedById: string;
  approvedByName: string;
  approvalDate: string;
  effectiveDate: string;
  expiryDate?: string | null;
}

export interface CreateEmployeeOvertimeOverride {
  employeeId: string;
  policyId?: string | null;
  allowanceType: OvertimeAllowanceType;
  isEligible: boolean;
  isExempt: boolean;
  overrideReason: string;
  approvedById: string;
  effectiveDate: string;
  expiryDate?: string | null;
}

export interface UpdateEmployeeOvertimeOverride {
  id: string;
  isEligible: boolean;
  isExempt: boolean;
  overrideReason: string;
  effectiveDate: string;
  expiryDate?: string | null;
}

// ── Overtime requests (workflow-approved) ────────────────────────────────────────

export interface StaffOvertimeRequestSummary {
  id: string;
  requestNumber: string;
  employeeId: string;
  employeeName: string;
  overtimeDate: string;
  plannedOvertimeHours: number;
  actualOvertimeHours?: number | null;
  type: OvertimeType;
  status: OvertimeRequestStatus;
  requestDate: string;
}

export interface StaffOvertimeRequest extends AuditFields {
  requestNumber: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  requestDate: string;
  overtimeDate: string;
  plannedStartTime: string;
  plannedEndTime: string;
  plannedOvertimeHours: number;
  purpose: string;
  taskDetails?: string | null;
  type: OvertimeType;
  status: OvertimeRequestStatus;

  approvedById?: string | null;
  approvedByName?: string | null;
  approvalDate?: string | null;
  approvalComments?: string | null;
  rejectedDate?: string | null;
  rejectionReason?: string | null;

  /** Level 3 — the supervisor confirms what was actually worked after the fact. */
  actualOvertimeHours?: number | null;
  supervisorConfirmedById?: string | null;
  supervisorConfirmedByName?: string | null;
  supervisorConfirmedDate?: string | null;
  supervisorNotes?: string | null;

  attendanceId?: string | null;
}

export interface CreateStaffOvertimeRequest {
  employeeId: string;
  overtimeDate: string;
  plannedStartTime: string;
  plannedEndTime: string;
  plannedOvertimeHours: number;
  purpose: string;
  taskDetails?: string | null;
  type: OvertimeType;
}

export interface ApproveOvertimeRequest {
  requestId: string;
  approvedById: string;
  approvalDate?: string;
  approvalComments?: string | null;
}

export interface RejectOvertimeRequest {
  requestId: string;
  rejectionReason: string;
}

export interface ConfirmOvertimeRequest {
  requestId: string;
  supervisorConfirmedById: string;
  actualOvertimeHours: number;
  confirmedDate?: string;
  supervisorNotes?: string | null;
  attendanceId?: string | null;
}

// ── Biometrics ───────────────────────────────────────────────────────────────────

export interface EmployeeBiometricSummary {
  id: string;
  employeeId: string;
  employeeName: string;
  biometricType: BiometricType;
  bodyPart?: string | null;
  fingerPosition?: FingerPosition | null;
  qualityScore?: number | null;
  enrolledDate: string;
  isActive: boolean;
}

export interface EmployeeBiometric extends AuditFields {
  employeeId: string;
  employeeName: string;
  biometricType: BiometricType;
  bodyPart?: string | null;
  fingerPosition?: FingerPosition | null;
  templateFormat?: string | null;
  qualityScore?: number | null;
  deviceId?: string | null;
  deviceModel?: string | null;
  enrolledDate: string;
  enrolledById?: string | null;
  enrolledByName?: string | null;
  isActive: boolean;
  revokedDate?: string | null;
  revokedReason?: string | null;
}

export interface EnrollBiometric {
  employeeId: string;
  biometricType: BiometricType;
  bodyPart?: string | null;
  fingerPosition?: FingerPosition | null;
  /** Opaque vendor template; never rendered back to the user. */
  biometricData: string;
  templateFormat?: string | null;
  qualityScore?: number | null;
  deviceId?: string | null;
  deviceModel?: string | null;
  enrolledById?: string | null;
}

// ── Devices ──────────────────────────────────────────────────────────────────────

export interface StaffAttendanceDeviceSummary {
  id: string;
  /** The vendor's device identifier, not the row id. */
  deviceId: string;
  deviceName: string;
  deviceType: AttendanceDeviceType;
  locationName?: string | null;
  locationDescription: string;
  isActive: boolean;
  lastSyncDate?: string | null;
  pendingSyncCount?: number | null;
}

export interface StaffAttendanceDevice extends AuditFields {
  deviceId: string;
  deviceName: string;
  deviceModel?: string | null;
  manufacturer?: string | null;
  firmwareVersion?: string | null;
  deviceType: AttendanceDeviceType;
  locationId?: string | null;
  locationName?: string | null;
  locationDescription: string;
  ipAddress?: string | null;
  port?: number | null;
  isActive: boolean;
  lastSyncDate?: string | null;
  pendingSyncCount?: number | null;
  notes?: string | null;
}

export interface CreateStaffAttendanceDevice {
  deviceId: string;
  deviceName: string;
  deviceModel?: string | null;
  manufacturer?: string | null;
  firmwareVersion?: string | null;
  deviceType: AttendanceDeviceType;
  locationId?: string | null;
  locationDescription: string;
  ipAddress?: string | null;
  port?: number | null;
  isActive: boolean;
  notes?: string | null;
}

/** `deviceId`, `manufacturer` and `deviceType` are fixed once registered. */
export interface UpdateStaffAttendanceDevice {
  id: string;
  deviceName: string;
  deviceModel?: string | null;
  firmwareVersion?: string | null;
  locationId?: string | null;
  locationDescription: string;
  ipAddress?: string | null;
  port?: number | null;
  isActive: boolean;
  notes?: string | null;
}

// ── Geofence zones ───────────────────────────────────────────────────────────────

export interface GeofenceZoneSummary {
  id: string;
  zoneName: string;
  shape: GeofenceShape;
  centreLatitude?: number | null;
  centreLongitude?: number | null;
  radiusMetres?: number | null;
  softEnforcement: boolean;
  hardEnforcement: boolean;
  isActive: boolean;
}

export interface GeofenceZone extends AuditFields {
  zoneName: string;
  description?: string | null;
  shape: GeofenceShape;
  centreLatitude?: number | null;
  centreLongitude?: number | null;
  radiusMetres?: number | null;
  polygonCoordinatesJson?: string | null;
  softEnforcement: boolean;
  hardEnforcement: boolean;
  isActive: boolean;
  notes?: string | null;
}

export type CreateGeofenceZone = Omit<GeofenceZone, keyof AuditFields>;
export interface UpdateGeofenceZone extends CreateGeofenceZone {
  id: string;
}

// ── Remote work requests (workflow-approved) ─────────────────────────────────────

export interface RemoteWorkRequestSummary {
  id: string;
  requestNumber: string;
  employeeId: string;
  employeeName: string;
  startDate: string;
  endDate: string;
  requestedDays: number;
  status: RemoteWorkRequestStatus;
  requestDate: string;
}

export interface RemoteWorkRequest extends AuditFields {
  requestNumber: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  requestDate: string;
  startDate: string;
  endDate: string;
  requestedDays: number;
  reason: string;
  remoteLocation?: string | null;
  equipmentConfirmed: boolean;
  status: RemoteWorkRequestStatus;
  approvedById?: string | null;
  approvedByName?: string | null;
  approvalDate?: string | null;
  approvalComments?: string | null;
  rejectedDate?: string | null;
  rejectionReason?: string | null;
}

export interface CreateRemoteWorkRequest {
  employeeId: string;
  startDate: string;
  endDate: string;
  reason: string;
  remoteLocation?: string | null;
  equipmentConfirmed: boolean;
}

/** The update DTO calls the location `workLocation`, unlike create's `remoteLocation`. */
export interface UpdateRemoteWorkRequest {
  id: string;
  startDate: string;
  endDate: string;
  workLocation?: string | null;
  reason: string;
}

export interface ApproveRemoteWorkRequest {
  requestId: string;
  approvedById: string;
  approvalDate?: string;
  approvalComments?: string | null;
}

export interface RejectRemoteWorkRequest {
  requestId: string;
  rejectionReason: string;
}

// ── Holiday calendars ────────────────────────────────────────────────────────────

export interface HolidayCalendarSummary {
  id: string;
  calendarName: string;
  countryName?: string | null;
  region?: string | null;
  isDefault: boolean;
  isActive: boolean;
  holidayCount: number;
}

export interface PublicHolidaySummary {
  id: string;
  holidayName: string;
  dateFrom: string;
  dateTo: string;
  year: number;
  observanceType: HolidayObservanceType;
  attractsHolidayPay: boolean;
  isActive: boolean;
}

export interface HolidayCalendar extends AuditFields {
  calendarName: string;
  description?: string | null;
  countryId?: string | null;
  countryName?: string | null;
  region?: string | null;
  isDefault: boolean;
  isActive: boolean;
  publicHolidays: PublicHolidaySummary[];
}

export interface CreateHolidayCalendar {
  calendarName: string;
  description?: string | null;
  countryId?: string | null;
  region?: string | null;
  isDefault: boolean;
  isActive: boolean;
}

export interface UpdateHolidayCalendar extends CreateHolidayCalendar {
  id: string;
}

export interface PublicHoliday extends AuditFields {
  holidayCalendarId: string;
  calendarName: string;
  holidayName: string;
  description?: string | null;
  dateFrom: string;
  dateTo: string;
  year: number;
  observanceType: HolidayObservanceType;
  substitutionDate?: string | null;
  attractsHolidayPay: boolean;
  holidayPayMultiplier?: number | null;
  isRecurringAnnually: boolean;
  isActive: boolean;
}

export interface CreatePublicHoliday {
  holidayCalendarId: string;
  holidayName: string;
  description?: string | null;
  dateFrom: string;
  dateTo: string;
  observanceType: HolidayObservanceType;
  substitutionDate?: string | null;
  attractsHolidayPay: boolean;
  holidayPayMultiplier?: number | null;
  isRecurringAnnually: boolean;
  isActive: boolean;
}

export interface UpdatePublicHoliday extends Omit<CreatePublicHoliday, 'holidayCalendarId'> {
  id: string;
}

// ── Pay periods ──────────────────────────────────────────────────────────────────

export interface PayPeriodSummary {
  id: string;
  periodName: string;
  type: PayPeriodType;
  startDate: string;
  endDate: string;
  status: PayPeriodStatus;
  closedDate?: string | null;
  exportedDate?: string | null;
}

export interface PayPeriod extends AuditFields {
  periodName: string;
  type: PayPeriodType;
  startDate: string;
  endDate: string;
  status: PayPeriodStatus;
  closedDate?: string | null;
  closedById?: string | null;
  closedByName?: string | null;
  exportedDate?: string | null;
  exportedById?: string | null;
  exportedByName?: string | null;
  notes?: string | null;
  summaryCount: number;
  exportCount: number;
}

export interface CreatePayPeriod {
  periodName: string;
  type: PayPeriodType;
  startDate: string;
  endDate: string;
  notes?: string | null;
}

/** The period type cannot be changed after creation. */
export interface UpdatePayPeriod {
  id: string;
  periodName: string;
  startDate: string;
  endDate: string;
  notes?: string | null;
}

// ── Payroll exports ──────────────────────────────────────────────────────────────

export interface StaffAttendancePayrollExportSummary {
  id: string;
  exportReference: string;
  payPeriodName: string;
  exportDate: string;
  exportedByName: string;
  targetSystem?: string | null;
  totalEmployees: number;
  totalRecords: number;
  status: PayrollExportStatus;
}

export interface StaffAttendancePayrollExport extends AuditFields {
  exportReference: string;
  payPeriodId: string;
  payPeriodName: string;
  exportDate: string;
  exportedById: string;
  exportedByName: string;
  targetSystem?: string | null;
  totalEmployees: number;
  totalRecords: number;
  status: PayrollExportStatus;
  errorDetails?: string | null;
  notes?: string | null;
}

export interface CreatePayrollExport {
  payPeriodId: string;
  exportedById: string;
  targetSystem?: string | null;
  notes?: string | null;
}

// ── Alert rules ──────────────────────────────────────────────────────────────────

export interface StaffAttendanceAlertRuleSummary {
  id: string;
  ruleName: string;
  triggerType: AttendanceAlertTriggerType;
  severity: AttendanceAlertSeverity;
  thresholdValue: number;
  isActive: boolean;
  activeAlertCount: number;
}

export interface StaffAttendanceAlertRule extends AuditFields {
  ruleName: string;
  description?: string | null;
  triggerType: AttendanceAlertTriggerType;
  severity: AttendanceAlertSeverity;
  thresholdValue: number;
  evaluationWindowDays?: number | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  positionId?: string | null;
  positionTitle?: string | null;
  employeeId?: string | null;
  employeeName?: string | null;
  notifyByEmail: boolean;
  notifyInApp: boolean;
  notifyRecipientsJson?: string | null;
  requiresAcknowledgement: boolean;
  isActive: boolean;
  activeAlertCount: number;
}

export interface CreateAlertRule {
  ruleName: string;
  description?: string | null;
  triggerType: AttendanceAlertTriggerType;
  severity: AttendanceAlertSeverity;
  thresholdValue: number;
  evaluationWindowDays?: number | null;
  organizationUnitId?: string | null;
  positionId?: string | null;
  employeeId?: string | null;
  notifyByEmail: boolean;
  notifyInApp: boolean;
  notifyRecipientsJson?: string | null;
  requiresAcknowledgement: boolean;
  isActive: boolean;
}

/** The trigger type is fixed once the rule exists — alerts already raised reference it. */
export interface UpdateAlertRule extends Omit<CreateAlertRule, 'triggerType'> {
  id: string;
}

// ── Alerts ───────────────────────────────────────────────────────────────────────

export interface StaffAttendanceAlertSummary {
  id: string;
  ruleName: string;
  employeeId: string;
  employeeName: string;
  triggerType: AttendanceAlertTriggerType;
  severity: AttendanceAlertSeverity;
  status: AttendanceAlertStatus;
  triggeredDate: string;
  triggerDescription: string;
}

export interface StaffAttendanceAlert extends AuditFields {
  alertRuleId: string;
  ruleName: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  triggerType: AttendanceAlertTriggerType;
  severity: AttendanceAlertSeverity;
  status: AttendanceAlertStatus;
  triggeredDate: string;
  triggerDescription: string;
  triggerValue?: number | null;

  acknowledgedById?: string | null;
  acknowledgedByName?: string | null;
  acknowledgedDate?: string | null;
  acknowledgementNotes?: string | null;

  resolvedById?: string | null;
  resolvedByName?: string | null;
  resolvedDate?: string | null;
  resolutionNotes?: string | null;

  dismissedById?: string | null;
  dismissedByName?: string | null;
  dismissedDate?: string | null;
  dismissalReason?: string | null;
}

export interface AcknowledgeAlertRequest {
  alertId: string;
  acknowledgedById: string;
  acknowledgedDate?: string;
  acknowledgementNotes?: string | null;
}

// ── Daily attendance search ──────────────────────────────────────────────────────

export type DailyAttendanceSortBy =
  | 'date'
  | 'employee'
  | 'status'
  | 'workhours'
  | 'overtime'
  | 'lateminutes';

/**
 * Filter for `POST api/staff-daily-attendance/search`.
 *
 * Everything is optional and the filters compose with AND, so an empty object returns the
 * same thing as the plain paged read. The tri-state booleans are meaningful: `undefined`
 * leaves the dimension alone, `false` actively excludes.
 */
export interface DailyAttendanceSearch {
  searchTerm?: string;
  employeeId?: string | null;
  organizationUnitId?: string | null;
  locationId?: string | null;
  workScheduleId?: string | null;
  payPeriodId?: string | null;
  /** DateOnly */
  from?: string | null;
  to?: string | null;
  /** Empty means all statuses. */
  statuses?: StaffAttendanceStatus[];
  isLate?: boolean | null;
  isEarlyDeparture?: boolean | null;
  isOvertime?: boolean | null;
  isRemoteWork?: boolean | null;
  hasException?: boolean | null;
  isVerified?: boolean | null;
  requiresVerification?: boolean | null;
  minLateMinutes?: number | null;
  minOvertimeHours?: number | null;
  sortBy?: DailyAttendanceSortBy;
  sortDescending?: boolean;
}

// ── Dashboard ────────────────────────────────────────────────────────────────────

export interface DailyAttendanceTrendPoint {
  /** DateOnly */
  date: string;
  dayOfWeek: DayOfWeekName;
  present: number;
  absent: number;
  late: number;
  onLeave: number;
  remote: number;
  /** Already a percentage (0–100), not a fraction. */
  attendanceRate: number;
}

export interface AttendanceRiskEmployee {
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  departmentName?: string | null;
  absentDaysLast30: number;
  lateDaysLast30: number;
  attendancePercentageLast30: number;
}

/** `GET api/attendance-dashboard` — the whole landing page in one request. */
export interface AttendanceDashboard {
  totalEmployees: number;
  presentToday: number;
  absentToday: number;
  lateToday: number;
  onLeaveToday: number;
  remoteToday: number;
  attendanceRateToday: number;

  currentPayPeriodName?: string | null;
  currentPayPeriodStart?: string | null;
  currentPayPeriodEnd?: string | null;
  currentPayPeriodStatus?: PayPeriodStatus | null;

  pendingOvertimeRequests: number;
  approvedOvertimeRequests: number;
  totalOvertimeHoursThisPeriod: number;

  pendingRegularizations: number;
  pendingRemoteWorkRequests: number;

  activeCriticalAlerts: number;
  activeWarningAlerts: number;
  topAlerts: StaffAttendanceAlertSummary[];

  dailyTrend: DailyAttendanceTrendPoint[];
  chronicAbsentees: AttendanceRiskEmployee[];

  unprocessedAttendanceLogs: number;

  activeDevices: number;
  devicesWithPendingSync: number;

  computedAt: string;
}
