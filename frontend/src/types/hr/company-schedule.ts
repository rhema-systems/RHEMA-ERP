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

export type RecurrencePattern = 'Daily' | 'Weekdays' | 'Weekly' | 'BiWeekly' | 'Monthly' | 'Quarterly' | 'Annually';

/** How each pattern reads on a form (lane 2f-1). Weekdays is Monday to Friday; Daily every calendar day. */
export const RECURRENCE_PATTERN_LABELS: Record<RecurrencePattern, string> = {
  Daily: 'Every day',
  Weekdays: 'Every weekday (Mon–Fri)',
  Weekly: 'Every week',
  BiWeekly: 'Every two weeks',
  Monthly: 'Every month',
  Quarterly: 'Every quarter',
  Annually: 'Every year',
};

/**
 * Which dates of a series a guest action reaches (lane 2f-2a, D-12). Never a date that has started, been completed or
 * been cancelled — the server passes those over.
 */
export type SeriesScope = 'ThisOccurrence' | 'ThisAndFollowing' | 'WholeSeries';

export const SERIES_SCOPE_LABELS: Record<SeriesScope, string> = {
  ThisOccurrence: 'This date only',
  ThisAndFollowing: 'This and following dates',
  WholeSeries: 'Every date in the series',
};

export const SERIES_SCOPES: SeriesScope[] = ['ThisOccurrence', 'ThisAndFollowing', 'WholeSeries'];

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
/**
 * The categories a NEW event may take (F-44): public holidays and company milestones have their own
 * registers, and the server refuses them. The two values stay in `EVENT_CATEGORIES` for older rows.
 */
export const EVENT_CATEGORIES_FOR_NEW: EventCategory[] = EVENT_CATEGORIES.filter(
  (c) => c !== 'Holiday' && c !== 'Milestone',
);
/** The statuses an edit may set (lane 2a); Confirmed only where no approval is needed. */
export const EVENT_EDITABLE_STATUSES: EventStatus[] = ['Scheduled', 'InProgress', 'Postponed'];
export const EVENT_TYPES: EventType[] = ['Internal', 'External', 'ClientMeeting', 'Statutory', 'BoardMeeting'];
export const EVENT_PRIORITIES: EventPriority[] = ['Critical', 'High', 'Medium', 'Low'];
export const RECURRENCE_PATTERNS: RecurrencePattern[] = [
  'Daily', 'Weekdays', 'Weekly', 'BiWeekly', 'Monthly', 'Quarterly', 'Annually',
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
/** The statuses an edit may set (lane 2d). Overdue is worked out from the due date — see `EventTask.isOverdue`. */
export const EVENT_TASK_SETTABLE_STATUSES: EventTaskStatus[] = ['NotStarted', 'InProgress', 'Completed', 'Cancelled'];
/** The answers an invitation can be given (F-11). */
export const INVITATION_ANSWERS: InvitationStatus[] = ['Accepted', 'Declined', 'Tentative'];
export const ROOM_TYPES: RoomType[] = ['Conference', 'Boardroom', 'Training', 'Huddle', 'Auditorium'];
export const BOOKING_STATUSES: BookingStatus[] = ['Tentative', 'Confirmed', 'Completed', 'Cancelled', 'NoShow'];
export const MILESTONE_CATEGORIES: MilestoneCategory[] = [
  'CompanyAnniversary', 'Achievement', 'ProductLaunch', 'Target', 'Certification',
];
export const CLOSURE_TYPES: ClosureType[] = [
  'FullClosure', 'PartialClosure', 'DepartmentClosure', 'StationClosure',
];

/**
 * What each closure type covers (company-schedule final closure, D-1). The type decides the scope:
 * the server refuses a site on a full closure, a site closure without a site, and so on.
 * `DepartmentClosure` is the wire name of an organisation-unit closure — the unit replaced the
 * department (D-5), the enum value stayed.
 */
export const CLOSURE_TYPE_OPTIONS: { value: ClosureType; label: string; hint: string }[] = [
  {
    value: 'FullClosure',
    label: 'Whole company',
    hint: 'Everybody is off. Not a working day, so leave over it is not charged.',
  },
  {
    value: 'StationClosure',
    label: 'One site',
    hint: 'The staff based at the site are off. Not a working day for them.',
  },
  {
    value: 'DepartmentClosure',
    label: 'One organisation unit',
    hint: 'The unit and every unit beneath it are off, wherever their staff sit. Not a working day for them.',
  },
  {
    value: 'PartialClosure',
    label: 'Reduced operations',
    hint: 'Open with reduced service, for the whole company, one site or one unit. Still a working day.',
  },
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
  /** The series it is an occurrence of, its place in it, and how many the series has (lane 2f-1). */
  recurrenceSeriesId?: string | null;
  occurrenceNumber?: number | null;
  occurrenceCount?: number | null;
  /** The record that made it (lane 2h) — "EmergencyDrill" — or null for an event HR made. */
  sourceEntityType?: string | null;
  sourceEntityId?: string | null;

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
  /** ⚠ Retired (D-5): no event carries one. Read only, for any older row. */
  departmentId?: string | null;
  departmentName?: string | null;
  /** The organisation unit the event is for — required when `scope` is Department. */
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;

  scope: ParticipantScope;
  scopeName: string;
  estimatedAttendees?: number | null;
  requiresRsvp: boolean;
  rsvpDeadline?: string | null;
  /** Who the event is for, from its scope and visibility (lane 2c, D-16). */
  audienceDescription: string;
  /** An audience that reaches nobody, said on save (create and update answers only). */
  warnings?: string[];
  /** Who an edit's notice reached — a move, a postponement, a new venue or link (update only, lane 2e-2). */
  told?: CompanyEventNoticeResult | null;
  /** Lane 2f-2b: an edit with a series scope — the dates it changed (update only). */
  series?: EventSeriesChangeResult | null;

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
  /** When the event's reminder went — by the hourly sweep or HR's button; cleared when the date moves. */
  reminderSentDate?: string | null;
  /** When the unanswered invitations were chased (round 4, lane N-b2); cleared when the RSVP deadline moves. */
  rsvpReminderSentDate?: string | null;

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
  /** The day the hourly sweep sends the reminder; null when reminders are off (lane 2e-2). */
  reminderDueOn?: string | null;
  /** The day the hourly sweep chases unanswered invitations; null without a reply-by date. */
  rsvpChaseDueOn?: string | null;
  /** Whether the tenant has a mail server set up — without one, only people with a login are told, in the app. */
  mailServerSetUp: boolean;
  /** Every occurrence of its series, in order (lane 2f-1); empty for a one-off event. */
  seriesOccurrences: EventSeriesOccurrence[];
  /** "Falls on a public holiday: …" — flagged, not skipped (D-12). */
  dayOffNote?: string | null;
  /** Lane 2h (C-51): the Safety drill that made it, worded and linked; null for an event HR made. */
  source?: EventSource | null;
}

/** One occurrence of a series, as its list shows it (lane 2f-1). */
export interface EventSeriesOccurrence {
  id: string;
  eventNumber: string;
  occurrenceNumber: number;
  startDate: string;
  startTime?: string | null;
  endDate: string;
  status: EventStatus;
  statusName: string;
  isCancelled: boolean;
  dayOffNote?: string | null;
}

/** More occurrences after a series' last: how many, or until when — not both (lane 2f-1). */
export interface ExtendEventSeries {
  count?: number | null;
  until?: string | null;
}

export interface EventSeriesResult {
  occurrences: EventSeriesOccurrence[];
  warnings: string[];
  /** Lane 2f-2a: the latest date's guests put on the new dates. */
  guests: number;
  /** Who their invitations reached; null when they wait for approval or there were none. */
  told?: CompanyEventNoticeResult | null;
}

/** What a guest action with a series scope did (lane 2f-2a): add, remove, or an answer. */
export interface EventSeriesGuestResult {
  /** The dates acted on, in date order. */
  eventNumbers: string[];
  /** Dates passed over: already invited (adding), or not invited (removing, answering). */
  skipped: number;
  /** Dates left alone: started, completed or cancelled. */
  closed: number;
  /** Dates added whose invitation waits for the approval. */
  waiting: number;
  told?: CompanyEventNoticeResult | null;
}

/**
 * What one notice did (lane 2e-2, R4-6.3): how many people it was for and how many it reached — by an email
 * the mail server took, or in the app. Counts are of people.
 */
export interface CompanyEventNoticeResult {
  issued: number;
  reached: number;
  notReached: number;
  /** Emails the mail server took. */
  emailed: number;
  /** Emails tried that no mail server took. */
  emailsNotTaken: number;
  toldInApp: number;
  mailServerSetUp: boolean;
  /** A reminder or a chase: it reached somebody, so it counts as sent. */
  stamped: boolean;
}

/**
 * `organizerId` is optional (D-11, lane 2a): empty means the person creating it, who is recorded as the
 * creator either way. `departmentId` is gone — the server refuses one (D-5); choose `organizationUnitId`.
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
  organizationUnitId?: string | null;
  organizerId?: string | null;
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
  organizationUnitId?: string | null;
  /** Empty leaves the organiser as it is. */
  organizerId?: string | null;
  scope: ParticipantScope;
  estimatedAttendees?: number | null;
  requiresRsvp: boolean;
  rsvpDeadline?: string | null;
  visibility: EventVisibility;
  showOnCompanyCalendar: boolean;
  showOnIntranet: boolean;
  /** Scheduled, InProgress or Postponed — or Confirmed where no approval is needed. Empty leaves it as it is. */
  status?: EventStatus | null;
  /** Required when the dates, times or all-day switch change: an edit that moves the event is a reschedule. */
  rescheduleReason?: string | null;
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
  /**
   * Lane 2f-2b: on a recurring event, which dates the edit reaches — each takes only what this edit changed. Not
   * `scope`, which is who the event is for.
   */
  seriesScope?: SeriesScope;
}

export interface CancelEvent {
  eventId: string;
  cancellationReason: string;
  /** Lane 2f-2b: on a recurring event, which dates are cancelled ("this and following" ends the series). */
  seriesScope?: SeriesScope;
}

export interface RescheduleEvent {
  eventId: string;
  newStartDate: string;
  newStartTime?: string | null;
  newEndDate: string;
  newEndTime?: string | null;
  rescheduleReason: string;
  /** A new reply-by date, when the current one would fall after the new start. */
  newRsvpDeadline?: string | null;
  /** Lane 2f-2b: on a recurring event, which dates move — each by the same number of days. */
  seriesScope?: SeriesScope;
}

/**
 * The events register's search (lane 2g-1): filtered, sorted and paged on the server. Dates by overlap. The export
 * takes the same filters.
 */
export interface CompanyEventSearch {
  text?: string;
  status?: EventStatus;
  category?: EventCategory;
  locationId?: string;
  organizationUnitId?: string;
  organizerId?: string;
  /** One series' dates, in their order (lane 2f-1's register filter). */
  seriesId?: string;
  from?: string;
  to?: string;
  /** `-start` (newest first, the default), `start`, `name`, `number` or `-number`. */
  sort?: string;
  page?: number;
  pageSize?: number;
}

/** What the event form asks before saving (lane 2g-2, C-15). The event being edited and its series are left out. */
export interface EventClashQuery {
  startDate: string;
  startTime?: string | null;
  endDate: string;
  endTime?: string | null;
  isAllDayEvent: boolean;
  scope: ParticipantScope;
  visibility: EventVisibility;
  organizationUnitId?: string | null;
  /** None: online, or the whole company — every site. */
  locationId?: string | null;
  excludeId?: string | null;
  seriesId?: string | null;
}

/** An event the one being saved would clash with (lane 2g-2, C-15). */
export interface EventClash {
  eventId: string;
  eventNumber: string;
  eventName: string;
  when: string;
  audience: string;
  siteName?: string | null;
  /** The save would be refused: both for the whole company, or both for the same unit. Otherwise a warning. */
  refused: boolean;
  message: string;
}

/** The bookings register's search (lane 2g-1). */
export interface RoomBookingSearch {
  text?: string;
  status?: BookingStatus;
  roomId?: string;
  from?: string;
  to?: string;
  /** `-start` (newest first, the default), `start`, `number` or `-number`. */
  sort?: string;
  page?: number;
  pageSize?: number;
}

/** The landing page in one read (lane 2g-1). */
export interface CompanyScheduleDashboard {
  upcomingEvents: CompanyEventSummary[];
  pendingBookings: RoomBookingSummary[];
  upcomingClosures: BusinessClosure[];
  upcomingMilestones: CompanyMilestone[];
}

/** What an edit, move or cancellation with a series scope did (lane 2f-2b): the dates, and who was told. */
export interface EventSeriesChangeResult {
  eventNumbers: string[];
  /** Dates left alone: started, completed or cancelled. */
  closed: number;
  told?: CompanyEventNoticeResult | null;
}

/** Who an event with this scope and visibility would be for, and how many (lane 2c, D-16). */
export interface EventAudiencePreview {
  audience: string;
  reach: number;
  /** For its guests and organiser only — nothing to count. */
  guestListOnly: boolean;
  warning?: string | null;
}

/** What announcing an event on the intranet would say, and to how many (lane 2c). */
export interface EventAnnouncementPreview {
  eventId: string;
  staffReached: number;
  canAnnounce: boolean;
  /** Why it cannot be announced, when it cannot. */
  reason?: string | null;
  title: string;
  summary: string;
  body: string;
}

/** What cancelling, rescheduling or deleting an event did beyond the event (lane 2a). */
export interface CompanyEventChange {
  /** The event as it now stands; null after a delete. */
  event?: CompanyEvent | null;
  bookingsMoved: string[];
  bookingsCancelled: string[];
  /** Accepted or tentative answers set back to awaiting a reply. */
  answersReset: number;
  /** It had been approved, moved, and now waits for approval again. */
  approvalCleared: boolean;
  /** Who was told of it (lane 2e-2); null after a delete. */
  told?: CompanyEventNoticeResult | null;
  /** Lane 2f-2b: a move or cancellation with a series scope. */
  series?: EventSeriesChangeResult | null;
  /** Lane 2g-2: another event at the same time and place that did not stop the move. */
  warnings?: string[];
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
  /** Lane 2f-2a: on an add with a series scope, what it did across the dates. */
  series?: EventSeriesGuestResult | null;
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
  /** Lane 2f-2a: on a recurring event, which dates (this one only when absent). */
  scope?: SeriesScope;
}

/**
 * A guest as HR corrects them (lane 2d, C-22). An employee guest's employee cannot change — uninvite and
 * invite the other person; the external fields are an outside guest's only, and need a name and an email.
 */
export interface UpdateEventParticipant {
  id: string;
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
  /** Lane 2f-2a: on a recurring event, the answer for which dates (this one only when absent). */
  scope?: SeriesScope;
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
  /**
   * Lane 2h (C-18): a file uploaded through the gate, which downloads. False for a row from before it — a name and a
   * path typed in, no file stored (F-54): "reference only — no file stored".
   */
  hasFile: boolean;
  fileSizeBytes?: number | null;
  uploadedById?: string | null;
}

/** Where an event came from (lane 2h, C-51) — the Safety drill that made it, worded and linked. */
export interface EventSource {
  kind: string;
  label: string;
  /** Null when the source has since been deleted. */
  link?: string | null;
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
  /** Due before today and still open — worked out by the server on every read (lane 2d, F-12). */
  isOverdue: boolean;
  /** When the hourly sweep chased the assignee about it — once (lane 2e-3, F-34). */
  overdueChasedAt?: string | null;
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
  /** Derived by the server from the type: true for every type but a partial closure with a site or unit. */
  affectsAllStations: boolean;
  locationId?: string | null;
  locationName?: string | null;
  /** ⚠ Retired (D-5). Still on the read; no row on any database carries one (L0-1). */
  departmentId?: string | null;
  departmentName?: string | null;
  /** The unit an organisation-unit closure covers, with everything beneath it. */
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  /** Who it covers, as a sentence for the register: "Whole company", "Site: Tema", "Unit: Finance and everything beneath it". */
  scopeDescription: string;
  /** Falls on the same month and day every later year. */
  recursAnnually: boolean;
  isPaidClosure: boolean;
  /** Derived by the server from the type: only a partial closure is a working day. */
  countsAsWorkingDay: boolean;
  announcementDate: string;
  announcedById?: string | null;
  announcedByName?: string | null;
  communicationNotes?: string | null;
  /** What the person saving should know that did not stop the save. Create and update only. */
  warnings?: string[];
  /** The granted leave this save recounted. Create and update only; null on a read. */
  leaveRecharge?: LeaveRechargeResult | null;
}

/**
 * ⚠ No `announcedById` — the API takes the announcer from the token. No `departmentId` either: the
 * server refuses one (D-5), so the form cannot send it.
 */
export interface CreateBusinessClosure {
  title: string;
  reason?: string | null;
  startDate: string;
  endDate: string;
  type: ClosureType;
  /** Read only for a partial closure; every other type sets it from the type. */
  affectsAllStations: boolean;
  locationId?: string | null;
  organizationUnitId?: string | null;
  recursAnnually: boolean;
  isPaidClosure: boolean;
  /** Ignored: the server sets it from the type. Sent so the shape matches the DTO. */
  countsAsWorkingDay: boolean;
  communicationNotes?: string | null;
}

/**
 * What recounting granted leave did after the days off under it changed — a closure or a public
 * holiday added, moved or removed (lane 1c). Mirrors `LeaveRechargeResultDto`. Only requests whose
 * count changed are listed.
 */
export interface LeaveRechargeResult {
  dryRun: boolean;
  /** Recounted: the days, the balance and the attendance changed, and the employee was told. */
  recharged: LeaveRechargeLine[];
  /** In a finished leave year, so left as charged for HR to adjust by hand. */
  notRecharged: LeaveRechargeLine[];
  /** Requests the recount could not save, each with why. */
  failures: string[];
}

export interface LeaveRechargeLine {
  requestId: string;
  requestNumber: string;
  employeeId: string;
  employeeName: string;
  leaveTypeName: string;
  startDate: string;
  endDate: string;
  oldDays: number;
  newDays: number;
}

/** What announcing a closure would say, and to how many — mirrors `ClosureAnnouncementPreviewDto`. */
export interface ClosureAnnouncementPreview {
  closureId: string;
  /** Active staff the closure covers: the announcement's reach. */
  staffCovered: number;
  /** False when nobody is covered — show "no staff to tell", not the button. */
  canAnnounce: boolean;
  title: string;
  summary: string;
  body: string;
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

/** One pass of the reminder sweep (round 4, lane N-b2) — the scheduled run and run-now return the same. */
export interface CompanyScheduleReminderRun {
  /** Event numbers whose reminder went this pass. */
  reminded: string[];
  /** Event numbers whose unanswered invitations were chased this pass. */
  rsvpChased: string[];
  /** Event numbers whose reminder, or chase, was due and reached nobody — tried again next pass (lane 2e-2). */
  remindersLeftDue: string[];
  chasesLeftDue: string[];
  /** Overdue tasks whose assignee was chased this pass, and those whose chase reached nobody (lane 2e-3). */
  tasksChased: string[];
  tasksLeftDue: string[];
  peopleIssued: number;
  peopleReached: number;
  /** Emails the mail server took (it counted every address tried until lane 2e-2). */
  emailsSent: number;
  emailsNotTaken: number;
  toldInApp: number;
  rsvpChaseLeadDays: number;
}
