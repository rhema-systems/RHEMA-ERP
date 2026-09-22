/**
 * Company schedule — events, meeting rooms, room bookings, milestones, business closures and
 * fiscal years/periods. Mirrors `ErpSystem.Core.DTOs.HR.CompanyScheduleDTOs`.
 *
 * ⚠ **Enums cross the wire as strings.** The API registers a `JsonStringEnumConverter`, so
 * `category` is `"Meeting"`, never `1`. Every union below was read off `HREnums.cs` rather than
 * guessed from a field name.
 *
 * ⚠ **`TimeSpan` fields are `"HH:mm:ss"` strings**, not numbers — use `TimeField`, which trims and
 * re-appends the seconds. `DateTime` fields are ISO instants; use `fromIsoInstant`/`toIsoInstant`
 * at the form edge.
 *
 * ⚠ **`locationId` is the site, and on a meeting room `location` is something else entirely** —
 * the free-text placement within that site ("East wing, past reception"). Both exist on
 * `MeetingRoom` and they are not interchangeable. These FKs pointed at `WorkStation` until
 * 2026-08-28; that entity has an empty table and no endpoint, so a required station made the room
 * form unfillable. They now point at the live `Location` tree.
 */

import type { AuditFields } from './common';

// ── Enums (string unions — see the note above) ─────────────────────────────────

export type EventCategory =
  | 'Meeting' | 'Training' | 'CompanyEvent' | 'Deadline'
  | 'Holiday' | 'Conference' | 'SocialEvent' | 'Milestone';

export type EventType = 'Internal' | 'External' | 'ClientMeeting' | 'Statutory' | 'BoardMeeting';

export type EventPriority = 'Critical' | 'High' | 'Medium' | 'Low';

export type RecurrencePattern = 'Daily' | 'Weekly' | 'BiWeekly' | 'Monthly' | 'Quarterly' | 'Annually';

export type EventLocationType = 'OnSite' | 'OffSite' | 'Virtual' | 'Hybrid';

export type ParticipantScope = 'AllStaff' | 'Department' | 'Selected' | 'ManagementOnly' | 'ExternalOnly';

export type EventVisibility = 'Public' | 'Private' | 'Department' | 'Management' | 'Confidential';

export type EventStatus =
  | 'Scheduled' | 'Confirmed' | 'InProgress' | 'Completed'
  | 'Cancelled' | 'Postponed' | 'Rescheduled';

export type ParticipantRole = 'Organizer' | 'Presenter' | 'Attendee' | 'Optional' | 'Facilitator';

export type InvitationStatus = 'NotSent' | 'Sent' | 'Accepted' | 'Declined' | 'Tentative' | 'NoResponse';

export type EventAttachmentType = 'Agenda' | 'Minutes' | 'Presentation' | 'Handout' | 'Resource';

export type EventTaskCategory = 'Preparation' | 'DuringEvent' | 'FollowUp';

export type TaskPriority = 'Critical' | 'High' | 'Medium' | 'Low';

export type EventTaskStatus = 'NotStarted' | 'InProgress' | 'Completed' | 'Overdue' | 'Cancelled';

export type RoomType = 'Conference' | 'Boardroom' | 'Training' | 'Huddle' | 'Auditorium';

export type BookingStatus = 'Tentative' | 'Confirmed' | 'Completed' | 'Cancelled' | 'NoShow';

export type MilestoneCategory =
  | 'CompanyAnniversary' | 'Achievement' | 'ProductLaunch' | 'Target' | 'Certification';

export type ClosureType = 'FullClosure' | 'PartialClosure' | 'DepartmentClosure' | 'StationClosure';

export type FiscalYearStatus = 'Active' | 'Closed' | 'Archived';

export type FiscalPeriodType = 'Quarter' | 'Month' | 'SemiAnnual';

export const EVENT_CATEGORIES: EventCategory[] = [
  'Meeting', 'Training', 'CompanyEvent', 'Deadline', 'Holiday', 'Conference', 'SocialEvent', 'Milestone',
];
export const EVENT_TYPES: EventType[] = ['Internal', 'External', 'ClientMeeting', 'Statutory', 'BoardMeeting'];
export const EVENT_PRIORITIES: EventPriority[] = ['Critical', 'High', 'Medium', 'Low'];
export const RECURRENCE_PATTERNS: RecurrencePattern[] = [
  'Daily', 'Weekly', 'BiWeekly', 'Monthly', 'Quarterly', 'Annually',
];
export const EVENT_LOCATION_TYPES: EventLocationType[] = ['OnSite', 'OffSite', 'Virtual', 'Hybrid'];
export const PARTICIPANT_SCOPES: ParticipantScope[] = [
  'AllStaff', 'Department', 'Selected', 'ManagementOnly', 'ExternalOnly',
];
export const EVENT_VISIBILITIES: EventVisibility[] = [
  'Public', 'Private', 'Department', 'Management', 'Confidential',
];
export const EVENT_STATUSES: EventStatus[] = [
  'Scheduled', 'Confirmed', 'InProgress', 'Completed', 'Cancelled', 'Postponed', 'Rescheduled',
];
export const PARTICIPANT_ROLES: ParticipantRole[] = [
  'Organizer', 'Presenter', 'Attendee', 'Optional', 'Facilitator',
];
export const INVITATION_STATUSES: InvitationStatus[] = [
  'NotSent', 'Sent', 'Accepted', 'Declined', 'Tentative', 'NoResponse',
];
export const EVENT_ATTACHMENT_TYPES: EventAttachmentType[] = [
  'Agenda', 'Minutes', 'Presentation', 'Handout', 'Resource',
];
export const EVENT_TASK_CATEGORIES: EventTaskCategory[] = ['Preparation', 'DuringEvent', 'FollowUp'];
export const TASK_PRIORITIES: TaskPriority[] = ['Critical', 'High', 'Medium', 'Low'];
export const EVENT_TASK_STATUSES: EventTaskStatus[] = [
  'NotStarted', 'InProgress', 'Completed', 'Overdue', 'Cancelled',
];
export const ROOM_TYPES: RoomType[] = ['Conference', 'Boardroom', 'Training', 'Huddle', 'Auditorium'];
export const BOOKING_STATUSES: BookingStatus[] = ['Tentative', 'Confirmed', 'Completed', 'Cancelled', 'NoShow'];
export const MILESTONE_CATEGORIES: MilestoneCategory[] = [
  'CompanyAnniversary', 'Achievement', 'ProductLaunch', 'Target', 'Certification',
];
export const CLOSURE_TYPES: ClosureType[] = [
  'FullClosure', 'PartialClosure', 'DepartmentClosure', 'StationClosure',
];
export const FISCAL_YEAR_STATUSES: FiscalYearStatus[] = ['Active', 'Closed', 'Archived'];
export const FISCAL_PERIOD_TYPES: FiscalPeriodType[] = ['Quarter', 'Month', 'SemiAnnual'];

// ── Company events ────────────────────────────────────────────────────────────

export interface CompanyEvent extends AuditFields {
  tenantId: string;
  eventNumber: string;
  eventName: string;
  description?: string | null;

  category: EventCategory;
  categoryName: string;
  type: EventType;
  typeName: string;
  priority: EventPriority;
  priorityName: string;

  startDate: string;
  /** `"HH:mm:ss"`. */
  startTime?: string | null;
  endDate: string;
  endTime?: string | null;
  isAllDayEvent: boolean;

  isRecurring: boolean;
  recurrencePattern?: RecurrencePattern | null;
  recurrencePatternName?: string | null;
  recurrenceDetails?: string | null;
  recurrenceEndDate?: string | null;
  recurrenceCount?: number | null;

  locationType: EventLocationType;
  locationTypeName: string;
  venueName?: string | null;
  venueAddress?: string | null;
  onlineMeetingLink?: string | null;
  meetingPassword?: string | null;
  /** The site. Null for virtual events. */
  locationId?: string | null;
  locationName?: string | null;

  organizerId: string;
  organizerName: string;
  departmentId?: string | null;
  departmentName?: string | null;

  scope: ParticipantScope;
  scopeName: string;
  estimatedAttendees?: number | null;
  requiresRsvp: boolean;
  rsvpDeadline?: string | null;

  visibility: EventVisibility;
  visibilityName: string;
  showOnCompanyCalendar: boolean;
  showOnIntranet: boolean;

  status: EventStatus;
  statusName: string;

  requiresApproval: boolean;
  approvedById?: string | null;
  approvedByName?: string | null;
  approvalDate?: string | null;

  hasBudget: boolean;
  budgetAmount?: number | null;
  actualCost?: number | null;
  budgetCode?: string | null;

  requiredResources?: string | null;
  cateringRequirements?: string | null;
  technicalRequirements?: string | null;

  sendReminders: boolean;
  reminderDaysBefore?: number | null;
  reminderSentDate?: string | null;

  actualStartTime?: string | null;
  actualEndTime?: string | null;
  actualAttendance?: number | null;
  outcomeSummary?: string | null;

  isCancelled: boolean;
  cancellationDate?: string | null;
  cancellationReason?: string | null;

  isRescheduled: boolean;
  /** ⚠ WHEN it was moved, not what it was moved from — see `originalStartDate`. */
  rescheduledDate?: string | null;
  /**
   * What the event was originally scheduled for, kept from the FIRST move (round 4, D7; C-2).
   *
   * ⚠ Null on an event that never moved, and on every event rescheduled BEFORE that lane — the
   * original was overwritten then and is not recoverable. Render null as "not recorded", never as
   * "same as now".
   */
  originalStartDate?: string | null;
  originalStartTime?: string | null;
  originalEndDate?: string | null;
  originalEndTime?: string | null;
  rescheduleReason?: string | null;

  additionalNotes?: string | null;
}

export interface CompanyEventSummary {
  id: string;
  eventNumber: string;
  eventName: string;
  category: EventCategory;
  categoryName: string;
  startDate: string;
  startTime?: string | null;
  endDate: string;
  isAllDayEvent: boolean;
  locationType: EventLocationType;
  locationTypeName: string;
  venueName?: string | null;
  status: EventStatus;
  statusName: string;
  organizerName: string;
  estimatedAttendees?: number | null;
}

export interface CompanyEventDetail extends CompanyEvent {
  participants: EventParticipant[];
  attendanceRecords: EventAttendance[];
  attachments: EventAttachment[];
  tasks: EventTask[];
}

/**
 * ⚠ **No `organizerId`.** The API takes the organiser from the caller's token — a value the client
 * cannot know is a value the client must not send. Same rule as the manpower budget's approver.
 */
export interface CreateCompanyEvent {
  eventName: string;
  description?: string | null;
  category: EventCategory;
  type: EventType;
  priority: EventPriority;
  startDate: string;
  startTime?: string | null;
  endDate: string;
  endTime?: string | null;
  isAllDayEvent: boolean;
  isRecurring: boolean;
  recurrencePattern?: RecurrencePattern | null;
  recurrenceDetails?: string | null;
  recurrenceEndDate?: string | null;
  recurrenceCount?: number | null;
  locationType: EventLocationType;
  venueName?: string | null;
  venueAddress?: string | null;
  onlineMeetingLink?: string | null;
  meetingPassword?: string | null;
  locationId?: string | null;
  departmentId?: string | null;
  scope: ParticipantScope;
  estimatedAttendees?: number | null;
  requiresRsvp: boolean;
  rsvpDeadline?: string | null;
  visibility: EventVisibility;
  showOnCompanyCalendar: boolean;
  showOnIntranet: boolean;
  requiresApproval: boolean;
  hasBudget: boolean;
  budgetAmount?: number | null;
  budgetCode?: string | null;
  requiredResources?: string | null;
  cateringRequirements?: string | null;
  technicalRequirements?: string | null;
  sendReminders: boolean;
  reminderDaysBefore?: number | null;
  additionalNotes?: string | null;
}

/**
 * ⚠ The update DTO drops recurrence and adds `status` + `actualCost`. It is **not** the create
 * shape with an id bolted on — sending recurrence here silently does nothing.
 */
export interface UpdateCompanyEvent {
  id: string;
  eventName: string;
  description?: string | null;
  category: EventCategory;
  type: EventType;
  priority: EventPriority;
  startDate: string;
  startTime?: string | null;
  endDate: string;
  endTime?: string | null;
  isAllDayEvent: boolean;
  locationType: EventLocationType;
  venueName?: string | null;
  venueAddress?: string | null;
  onlineMeetingLink?: string | null;
  meetingPassword?: string | null;
  locationId?: string | null;
  departmentId?: string | null;
  scope: ParticipantScope;
  estimatedAttendees?: number | null;
  requiresRsvp: boolean;
  rsvpDeadline?: string | null;
  visibility: EventVisibility;
  showOnCompanyCalendar: boolean;
  showOnIntranet: boolean;
  status: EventStatus;
  hasBudget: boolean;
  budgetAmount?: number | null;
  actualCost?: number | null;
  budgetCode?: string | null;
  requiredResources?: string | null;
  cateringRequirements?: string | null;
  technicalRequirements?: string | null;
  sendReminders: boolean;
  reminderDaysBefore?: number | null;
  additionalNotes?: string | null;
}

export interface CancelEvent {
  eventId: string;
  cancellationReason: string;
}

export interface RescheduleEvent {
  eventId: string;
  newStartDate: string;
  newStartTime?: string | null;
  newEndDate: string;
  newEndTime?: string | null;
  rescheduleReason: string;
}

export interface CompleteEvent {
  eventId: string;
  actualStartTime?: string | null;
  actualEndTime?: string | null;
  actualAttendance?: number | null;
  outcomeSummary?: string | null;
}

// ── Participants ──────────────────────────────────────────────────────────────

export interface EventParticipant extends AuditFields {
  tenantId: string;
  eventId: string;
  employeeId?: string | null;
  employeeName?: string | null;
  externalParticipantName?: string | null;
  externalParticipantEmail?: string | null;
  externalParticipantOrganization?: string | null;
  /** Server-computed: employee name, else external name. */
  participantName: string;
  role: ParticipantRole;
  roleName: string;
  isRequired: boolean;
  invitationStatus: InvitationStatus;
  invitationStatusName: string;
  invitationSentDate?: string | null;
  responseDate?: string | null;
  responseComments?: string | null;
  specialRequirements?: string | null;
}

/** Either `employeeId` (internal) or the three external fields — never both. */
export interface CreateEventParticipant {
  eventId: string;
  employeeId?: string | null;
  externalParticipantName?: string | null;
  externalParticipantEmail?: string | null;
  externalParticipantOrganization?: string | null;
  role: ParticipantRole;
  isRequired: boolean;
  specialRequirements?: string | null;
}

export interface RespondToEventInvitation {
  participantId: string;
  response: InvitationStatus;
  responseComments?: string | null;
}

// ── Attendance ────────────────────────────────────────────────────────────────

export interface EventAttendance extends AuditFields {
  tenantId: string;
  eventId: string;
  employeeId: string;
  employeeName: string;
  attended: boolean;
  checkInTime?: string | null;
  checkOutTime?: string | null;
  absenceReason?: string | null;
  notes?: string | null;
  markedById?: string | null;
  markedByName?: string | null;
}

/** ⚠ No `markedById` — the API takes the marker from the token. */
export interface MarkEventAttendance {
  eventId: string;
  employeeId: string;
  attended: boolean;
  checkInTime?: string | null;
  absenceReason?: string | null;
  notes?: string | null;
}

export interface CheckOutEvent {
  attendanceId: string;
  notes?: string | null;
}

// ── Attachments and tasks ─────────────────────────────────────────────────────

export interface EventAttachment extends AuditFields {
  tenantId: string;
  eventId: string;
  fileName: string;
  filePath: string;
  type: EventAttachmentType;
  typeName: string;
  description?: string | null;
  uploadDate: string;
}

export interface CreateEventAttachment {
  eventId: string;
  fileName: string;
  filePath: string;
  type: EventAttachmentType;
  description?: string | null;
}

export interface EventTask extends AuditFields {
  tenantId: string;
  eventId: string;
  taskDescription: string;
  category: EventTaskCategory;
  categoryName: string;
  assignedToId?: string | null;
  assignedToName?: string | null;
  dueDate?: string | null;
  priority: TaskPriority;
  priorityName: string;
  status: EventTaskStatus;
  statusName: string;
  completionDate?: string | null;
  completionNotes?: string | null;
}

/** ⚠ Create has no `status` — a new task is always NotStarted. Update carries it. */
export interface CreateEventTask {
  eventId: string;
  taskDescription: string;
  category: EventTaskCategory;
  assignedToId?: string | null;
  dueDate?: string | null;
  priority: TaskPriority;
}

export interface UpdateEventTask {
  id: string;
  taskDescription: string;
  category: EventTaskCategory;
  assignedToId?: string | null;
  dueDate?: string | null;
  priority: TaskPriority;
  status: EventTaskStatus;
}

export interface CompleteEventTask {
  taskId: string;
  completionNotes?: string | null;
}

// ── Meeting rooms ─────────────────────────────────────────────────────────────

export interface MeetingRoom extends AuditFields {
  tenantId: string;
  roomCode: string;
  roomName: string;
  description?: string | null;
  /** The site this room sits on. */
  locationId: string;
  locationName: string;
  /** Placement within that site — free text, not the site itself. */
  location: string;
  floor?: string | null;
  building?: string | null;
  capacity: number;
  type: RoomType;
  typeName: string;
  hasProjector: boolean;
  hasWhiteboard: boolean;
  hasVideoConference: boolean;
  hasAudioSystem: boolean;
  hasAirConditioning: boolean;
  otherFacilities?: string | null;
  isActive: boolean;
  requiresApproval: boolean;
  isBookable: boolean;
  maxBookingDurationHours?: number | null;
  advanceBookingDays?: number | null;
}

export interface MeetingRoomSummary {
  id: string;
  roomCode: string;
  roomName: string;
  locationName: string;
  location: string;
  capacity: number;
  type: RoomType;
  typeName: string;
  isActive: boolean;
  isBookable: boolean;
}

export interface CreateMeetingRoom {
  roomCode: string;
  roomName: string;
  description?: string | null;
  locationId: string;
  location: string;
  floor?: string | null;
  building?: string | null;
  capacity: number;
  type: RoomType;
  hasProjector: boolean;
  hasWhiteboard: boolean;
  hasVideoConference: boolean;
  hasAudioSystem: boolean;
  hasAirConditioning: boolean;
  otherFacilities?: string | null;
  isActive: boolean;
  requiresApproval: boolean;
  isBookable: boolean;
  maxBookingDurationHours?: number | null;
  advanceBookingDays?: number | null;
}

export interface UpdateMeetingRoom extends CreateMeetingRoom {
  id: string;
}

// ── Room bookings ─────────────────────────────────────────────────────────────

export interface RoomBooking extends AuditFields {
  tenantId: string;
  bookingNumber: string;
  roomId: string;
  roomName: string;
  eventId?: string | null;
  eventName?: string | null;
  bookedById: string;
  bookedByName: string;
  bookingDate: string;
  startDateTime: string;
  endDateTime: string;
  purpose: string;
  expectedAttendees: number;
  specialRequirements?: string | null;
  cateringRequirements?: string | null;
  status: BookingStatus;
  statusName: string;
  approvedById?: string | null;
  approvedByName?: string | null;
  approvalDate?: string | null;
  isCancelled: boolean;
  cancellationDate?: string | null;
  cancellationReason?: string | null;
  notes?: string | null;
}

export interface RoomBookingSummary {
  id: string;
  bookingNumber: string;
  roomName: string;
  bookedByName: string;
  startDateTime: string;
  endDateTime: string;
  purpose: string;
  status: BookingStatus;
  statusName: string;
}

/** ⚠ No `bookedById` — the API takes the booker from the token. */
export interface CreateRoomBooking {
  roomId: string;
  eventId?: string | null;
  startDateTime: string;
  endDateTime: string;
  purpose: string;
  expectedAttendees: number;
  specialRequirements?: string | null;
  cateringRequirements?: string | null;
  notes?: string | null;
}

/** ⚠ Update cannot move a booking to another room — `roomId` is create-only. */
export interface UpdateRoomBooking {
  id: string;
  startDateTime: string;
  endDateTime: string;
  purpose: string;
  expectedAttendees: number;
  specialRequirements?: string | null;
  cateringRequirements?: string | null;
  notes?: string | null;
}

export interface CancelRoomBooking {
  bookingId: string;
  cancellationReason: string;
}

// ── Milestones ────────────────────────────────────────────────────────────────

export interface CompanyMilestone extends AuditFields {
  tenantId: string;
  title: string;
  description?: string | null;
  category: MilestoneCategory;
  categoryName: string;
  milestoneDate: string;
  isRecurringAnnually: boolean;
  showOnCalendar: boolean;
  significance?: string | null;
  relatedDocuments?: string | null;
}

export interface CreateCompanyMilestone {
  title: string;
  description?: string | null;
  category: MilestoneCategory;
  milestoneDate: string;
  isRecurringAnnually: boolean;
  showOnCalendar: boolean;
  significance?: string | null;
  relatedDocuments?: string | null;
}

export interface UpdateCompanyMilestone extends CreateCompanyMilestone {
  id: string;
}

// ── Business closures ─────────────────────────────────────────────────────────

export interface BusinessClosure extends AuditFields {
  tenantId: string;
  title: string;
  reason?: string | null;
  startDate: string;
  endDate: string;
  type: ClosureType;
  typeName: string;
  affectsAllStations: boolean;
  locationId?: string | null;
  locationName?: string | null;
  departmentId?: string | null;
  departmentName?: string | null;
  isPaidClosure: boolean;
  countsAsWorkingDay: boolean;
  announcementDate: string;
  announcedById?: string | null;
  announcedByName?: string | null;
  communicationNotes?: string | null;
}

/** ⚠ No `announcedById` — the API takes the announcer from the token. */
export interface CreateBusinessClosure {
  title: string;
  reason?: string | null;
  startDate: string;
  endDate: string;
  type: ClosureType;
  affectsAllStations: boolean;
  locationId?: string | null;
  departmentId?: string | null;
  isPaidClosure: boolean;
  countsAsWorkingDay: boolean;
  communicationNotes?: string | null;
}

export interface UpdateBusinessClosure extends CreateBusinessClosure {
  id: string;
}

// ── Fiscal years and periods ──────────────────────────────────────────────────

export interface FiscalYear extends AuditFields {
  tenantId: string;
  year: number;
  fiscalYearName: string;
  startDate: string;
  endDate: string;
  isCurrent: boolean;
  status: FiscalYearStatus;
  statusName: string;
  periodCount: number;
}

export interface FiscalYearDetail extends FiscalYear {
  periods: FiscalPeriod[];
}

export interface CreateFiscalYear {
  year: number;
  fiscalYearName: string;
  startDate: string;
  endDate: string;
  isCurrent: boolean;
}

/** ⚠ `year` is create-only — the update DTO has no year field. */
export interface UpdateFiscalYear {
  id: string;
  fiscalYearName: string;
  startDate: string;
  endDate: string;
  isCurrent: boolean;
  status: FiscalYearStatus;
}

export interface FiscalPeriod extends AuditFields {
  tenantId: string;
  fiscalYearId: string;
  fiscalYearName: string;
  periodNumber: number;
  periodName: string;
  type: FiscalPeriodType;
  typeName: string;
  startDate: string;
  endDate: string;
  isClosed: boolean;
  closedDate?: string | null;
}

export interface CreateFiscalPeriod {
  fiscalYearId: string;
  periodNumber: number;
  periodName: string;
  type: FiscalPeriodType;
  startDate: string;
  endDate: string;
}

export interface UpdateFiscalPeriod {
  id: string;
  periodNumber: number;
  periodName: string;
  type: FiscalPeriodType;
  startDate: string;
  endDate: string;
}

export interface CloseFiscalPeriod {
  periodId: string;
}


// ── The personal diary (round 4, D5) ─────────────────────────────────────────

// CommitmentKind — HREnums.cs. One member per registered IPanelistCommitmentSource.
export const SCHEDULE_ENTRY_KINDS = [
  'Interview',
  'Leave',
  'Travel',
  'Event',
  'RoomBooking',
  'Training',
  'Closure',
  'Holiday',
] as const;
export type ScheduleEntryKind = (typeof SCHEDULE_ENTRY_KINDS)[number];

/**
 * One thing an employee is committed to, from any module that tracks commitments.
 *
 * ⚠ Assembled by fanning out over the SAME commitment sources the interview clash check uses, so a
 * new kind of commitment appears here and there together — or in neither.
 */
export interface PersonalScheduleEntry {
  subjectId: string;
  kind: ScheduleEntryKind;
  kindName: string;
  /**
   * ⚠ On the interview clash check `Hard` REFUSES a booking. In a diary nothing is refused and this
   * is only a hint about how movable the entry is.
   */
  hardness: 'Soft' | 'Hard';
  hardnessName: string;
  label: string;
  start: string;
  end: string;
  /**
   * ⚠ True when the source records whole DAYS — leave, travel, a closure, an all-day event. `start`
   * and `end` are the day's bounds, not a window; never render them as hours.
   */
  isDayGranular: boolean;
  reference?: string | null;
}

export interface PersonalSchedule {
  employeeId: string;
  employeeName: string;
  from: string;
  to: string;
  entries: PersonalScheduleEntry[];
}

/** A unit and everything beneath it — what a head needs before scheduling for their team. */
export interface TeamSchedule {
  organizationUnitId: string;
  from: string;
  to: string;
  members: PersonalSchedule[];
}
