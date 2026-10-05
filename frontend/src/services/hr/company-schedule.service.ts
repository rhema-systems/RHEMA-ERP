import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type { HrAnnouncement } from './announcements.service';
import type {
  BookingStatus,
  BusinessClosure,
  CancelEvent,
  CancelRoomBooking,
  CheckOutEvent,
  CloseFiscalPeriod,
  ClosureAnnouncementPreview,
  ClosureType,
  CompanyEvent,
  CompanyEventChange,
  CompanyEventDetail,
  CompanyEventNoticeResult,
  EventAnnouncementPreview,
  EventAudiencePreview,
  EventVisibility,
  ParticipantScope,
  CompanyEventSummary,
  CompanyMilestone,
  CompanyScheduleReminderRun,
  CompleteEvent,
  CompleteEventTask,
  CreateBusinessClosure,
  CreateCompanyEvent,
  CreateCompanyMilestone,
  CreateEventAttachment,
  CreateEventParticipant,
  CreateEventTask,
  CreateFiscalPeriod,
  CreateFiscalYear,
  CreateMeetingRoom,
  CreateRoomBooking,
  EventAttachment,
  EventAttendance,
  EventCategory,
  EventParticipant,
  EventStatus,
  EventTask,
  FiscalPeriod,
  FiscalYear,
  FiscalYearDetail,
  FiscalYearStatus,
  LeaveRechargeResult,
  MarkEventAttendance,
  MeetingRoom,
  MeetingRoomSummary,
  MilestoneCategory,
  PersonalSchedule,
  RescheduleEvent,
  RespondToEventInvitation,
  RoomBooking,
  RoomBookingSummary,
  TeamSchedule,
  UpdateBusinessClosure,
  UpdateCompanyEvent,
  UpdateCompanyMilestone,
  UpdateEventParticipant,
  UpdateEventTask,
  UpdateFiscalPeriod,
  UpdateFiscalYear,
  UpdateMeetingRoom,
  UpdateRoomBooking,
} from '@/types/hr/company-schedule';

/**
 * The company schedule. Backend route: `api/CompanySchedule` — one controller, six sub-areas, so
 * one module with six exported services rather than six files fighting over the same base path.
 *
 * Permissions: reads need `HR.Company.Read`, writes and decisions `HR.Company.Write`, deletes
 * `HR.Company.Admin`.
 *
 * ⚠ **Actors come from the token, never from the caller.** Creating an event, approving an event,
 * marking attendance, creating a booking, approving a booking and announcing a closure all used to
 * take the actor as a query parameter. They no longer accept one. If you find yourself wanting to
 * pass an organiser or approver id here, the answer is that the API already knows who you are.
 */

class CompanyEventService {
  private readonly baseUrl = '/CompanySchedule';

  /**
   * Chases everybody who has not answered their invitation, answering who it was for and who it reached
   * (lane 2e-2). Once it reached somebody it counts as the event's RSVP chase, and the hourly sweep will
   * not send it again (round 4, lane N-b2); one that reached nobody stays due.
   */
  sendRsvpReminders(eventId: string): Promise<CompanyEventNoticeResult> {
    return apiService.post<CompanyEventNoticeResult>(`${this.baseUrl}/events/${eventId}/rsvp-reminders`, {});
  }

  /**
   * Reminds every participant who has not declined, and the organiser. Once it reached somebody it counts
   * as the event's reminder for its current date; one that reached nobody stays due (lane 2e-2).
   */
  sendEventReminders(eventId: string): Promise<CompanyEventNoticeResult> {
    return apiService.post<CompanyEventNoticeResult>(`${this.baseUrl}/events/${eventId}/reminders`, {});
  }

  /** Sends again the invitations that reached nobody (lane 2e-2). */
  sendUndeliveredInvitations(eventId: string): Promise<CompanyEventNoticeResult> {
    return apiService.post<CompanyEventNoticeResult>(`${this.baseUrl}/events/${eventId}/invitations/send`, {});
  }

  /** Runs the reminder sweep now for the tenant — what the hourly host runs (lane N-b2). */
  runDueReminders(): Promise<CompanyScheduleReminderRun> {
    return apiService.post<CompanyScheduleReminderRun>(`${this.baseUrl}/reminders/run`, {});
  }

  getAll(): Promise<CompanyEvent[]> {
    return apiService.get<CompanyEvent[]>(`${this.baseUrl}/events`);
  }

  getPaged(pageNumber = 1, pageSize = 20): Promise<PagedResult<CompanyEvent>> {
    return apiService.get<PagedResult<CompanyEvent>>(`${this.baseUrl}/events/paged`, {
      pageNumber,
      pageSize,
    });
  }

  getById(id: string): Promise<CompanyEvent> {
    return apiService.get<CompanyEvent>(`${this.baseUrl}/events/${id}`);
  }

  /** Event plus participants, attendance, attachments and tasks in one round trip. */
  getDetail(id: string): Promise<CompanyEventDetail> {
    return apiService.get<CompanyEventDetail>(`${this.baseUrl}/events/${id}/details`);
  }

  getByDateRange(startDate: string, endDate: string): Promise<CompanyEventSummary[]> {
    return apiService.get<CompanyEventSummary[]>(`${this.baseUrl}/events/range`, { startDate, endDate });
  }

  getByOrganizer(organizerId: string): Promise<CompanyEventSummary[]> {
    return apiService.get<CompanyEventSummary[]>(`${this.baseUrl}/events/organizer/${organizerId}`);
  }

  /** Events for one organisation unit (lane 2a; the department read is retired, D-5). */
  getByUnit(organizationUnitId: string): Promise<CompanyEventSummary[]> {
    return apiService.get<CompanyEventSummary[]>(`${this.baseUrl}/events/unit/${organizationUnitId}`);
  }

  getByStatus(status: EventStatus): Promise<CompanyEventSummary[]> {
    return apiService.get<CompanyEventSummary[]>(`${this.baseUrl}/events/status/${status}`);
  }

  getByCategory(category: EventCategory): Promise<CompanyEventSummary[]> {
    return apiService.get<CompanyEventSummary[]>(`${this.baseUrl}/events/category/${category}`);
  }

  getUpcoming(daysAhead = 30): Promise<CompanyEventSummary[]> {
    return apiService.get<CompanyEventSummary[]>(`${this.baseUrl}/events/upcoming`, { daysAhead });
  }

  create(data: CreateCompanyEvent): Promise<CompanyEvent> {
    return apiService.post<CompanyEvent>(`${this.baseUrl}/events`, data);
  }

  update(id: string, data: UpdateCompanyEvent): Promise<CompanyEvent> {
    return apiService.put<CompanyEvent>(`${this.baseUrl}/events/${id}`, { ...data, id });
  }

  /**
   * Approves an event awaiting approval (lane 2b, D-10). The workflow engine decides whether the caller
   * may; the organiser never may. Comments are optional.
   */
  approve(id: string, comments?: string | null): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/events/${id}/approve`, { comments: comments ?? null });
  }

  /** Who an event with this scope and visibility would be for, and how many — before saving (lane 2c, D-16). */
  previewAudience(scope: ParticipantScope, visibility: EventVisibility, organizationUnitId?: string | null): Promise<EventAudiencePreview> {
    return apiService.get<EventAudiencePreview>(`${this.baseUrl}/events/audience-preview`, {
      scope,
      visibility,
      ...(organizationUnitId ? { organizationUnitId } : {}),
    });
  }

  /** What announcing the event on the intranet would say, and to how many. Saves nothing (lane 2c). */
  getAnnouncementPreview(id: string): Promise<EventAnnouncementPreview> {
    return apiService.get<EventAnnouncementPreview>(`${this.baseUrl}/events/${id}/announcement`);
  }

  /** Announces the event on the intranet to its audience — HR's click (lane 2c). */
  announce(id: string): Promise<HrAnnouncement> {
    return apiService.post<HrAnnouncement>(`${this.baseUrl}/events/${id}/announce`);
  }

  /** Rejects an event awaiting approval: it is cancelled with the reason, its room bookings with it (lane 2b). */
  reject(id: string, comments: string): Promise<CompanyEventChange> {
    return apiService.post<CompanyEventChange>(`${this.baseUrl}/events/${id}/reject`, { comments });
  }

  /** Cancels the event and its live room bookings; answers the bookings cancelled (lane 2a). */
  cancel(id: string, cancellationReason: string): Promise<CompanyEventChange> {
    const body: CancelEvent = { eventId: id, cancellationReason };
    return apiService.post<CompanyEventChange>(`${this.baseUrl}/events/${id}/cancel`, body);
  }

  /** Moves the event with its room bookings; answers what moved and what was reset (lane 2a). */
  reschedule(id: string, data: Omit<RescheduleEvent, 'eventId'>): Promise<CompanyEventChange> {
    return apiService.post<CompanyEventChange>(`${this.baseUrl}/events/${id}/reschedule`, { ...data, eventId: id });
  }

  complete(id: string, data: Omit<CompleteEvent, 'eventId'>): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/events/${id}/complete`, { ...data, eventId: id });
  }

  /** Company Admin. Cancels the event's live room bookings first; answers them (lane 2a). */
  remove(id: string): Promise<CompanyEventChange> {
    return apiService.delete<CompanyEventChange>(`${this.baseUrl}/events/${id}`);
  }

  // ── participants ──────────────────────────────────────────────────────────

  getParticipants(eventId: string): Promise<EventParticipant[]> {
    return apiService.get<EventParticipant[]>(`${this.baseUrl}/events/${eventId}/participants`);
  }

  addParticipant(eventId: string, data: Omit<CreateEventParticipant, 'eventId'>): Promise<EventParticipant> {
    return apiService.post<EventParticipant>(`${this.baseUrl}/events/${eventId}/participants`, {
      ...data,
      eventId,
    });
  }

  /**
   * Records an invitation response. This is the HR desk doing it on the participant's behalf —
   * it is gated on HR.Company.Write, not on being that participant.
   */
  respondToInvitation(eventId: string, data: RespondToEventInvitation): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/events/${eventId}/participants/respond`, data);
  }

  /** Corrects a guest — role, required, needs, an outside guest's details (C-22). */
  updateParticipant(participantId: string, data: Omit<UpdateEventParticipant, 'id'>): Promise<EventParticipant> {
    return apiService.put<EventParticipant>(`${this.baseUrl}/participants/${participantId}`, {
      ...data,
      id: participantId,
    });
  }

  /** Uninvites a guest — on Write since lane 2d. */
  removeParticipant(participantId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/participants/${participantId}`);
  }

  // ── attendance ────────────────────────────────────────────────────────────

  getAttendance(eventId: string): Promise<EventAttendance[]> {
    return apiService.get<EventAttendance[]>(`${this.baseUrl}/events/${eventId}/attendance`);
  }

  markAttendance(eventId: string, data: Omit<MarkEventAttendance, 'eventId'>): Promise<EventAttendance> {
    return apiService.post<EventAttendance>(`${this.baseUrl}/events/${eventId}/attendance`, {
      ...data,
      eventId,
    });
  }

  checkOut(attendanceId: string, notes?: string | null): Promise<void> {
    const body: CheckOutEvent = { attendanceId, notes: notes ?? null };
    return apiService.post<void>(`${this.baseUrl}/attendance/${attendanceId}/checkout`, body);
  }

  /** Removes a row from the event's register — a correction (C-21). */
  removeAttendance(eventId: string, attendanceId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/events/${eventId}/attendance/${attendanceId}`);
  }

  // ── attachments ───────────────────────────────────────────────────────────

  getAttachments(eventId: string): Promise<EventAttachment[]> {
    return apiService.get<EventAttachment[]>(`${this.baseUrl}/events/${eventId}/attachments`);
  }

  addAttachment(eventId: string, data: Omit<CreateEventAttachment, 'eventId'>): Promise<EventAttachment> {
    return apiService.post<EventAttachment>(`${this.baseUrl}/events/${eventId}/attachments`, {
      ...data,
      eventId,
    });
  }

  removeAttachment(attachmentId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/attachments/${attachmentId}`);
  }

  // ── tasks ─────────────────────────────────────────────────────────────────

  getTasks(eventId: string): Promise<EventTask[]> {
    return apiService.get<EventTask[]>(`${this.baseUrl}/events/${eventId}/tasks`);
  }

  addTask(eventId: string, data: Omit<CreateEventTask, 'eventId'>): Promise<EventTask> {
    return apiService.post<EventTask>(`${this.baseUrl}/events/${eventId}/tasks`, { ...data, eventId });
  }

  updateTask(taskId: string, data: UpdateEventTask): Promise<EventTask> {
    return apiService.put<EventTask>(`${this.baseUrl}/tasks/${taskId}`, { ...data, id: taskId });
  }

  completeTask(taskId: string, completionNotes?: string | null): Promise<void> {
    const body: CompleteEventTask = { taskId, completionNotes: completionNotes ?? null };
    return apiService.post<void>(`${this.baseUrl}/tasks/${taskId}/complete`, body);
  }

  removeTask(taskId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/tasks/${taskId}`);
  }
}

class MeetingRoomService {
  private readonly baseUrl = '/CompanySchedule';

  getAll(): Promise<MeetingRoom[]> {
    return apiService.get<MeetingRoom[]>(`${this.baseUrl}/rooms`);
  }

  getPaged(pageNumber = 1, pageSize = 20): Promise<PagedResult<MeetingRoom>> {
    return apiService.get<PagedResult<MeetingRoom>>(`${this.baseUrl}/rooms/paged`, { pageNumber, pageSize });
  }

  getById(id: string): Promise<MeetingRoom> {
    return apiService.get<MeetingRoom>(`${this.baseUrl}/rooms/${id}`);
  }

  getByLocation(locationId: string): Promise<MeetingRoomSummary[]> {
    return apiService.get<MeetingRoomSummary[]>(`${this.baseUrl}/rooms/location/${locationId}`);
  }

  getActive(): Promise<MeetingRoomSummary[]> {
    return apiService.get<MeetingRoomSummary[]>(`${this.baseUrl}/rooms/active`);
  }

  /** Rooms free for the whole window, optionally seating at least `minCapacity`. */
  getAvailable(startDateTime: string, endDateTime: string, minCapacity?: number): Promise<MeetingRoomSummary[]> {
    return apiService.get<MeetingRoomSummary[]>(`${this.baseUrl}/rooms/available`, {
      startDateTime,
      endDateTime,
      ...(minCapacity ? { minCapacity } : {}),
    });
  }

  create(data: CreateMeetingRoom): Promise<MeetingRoom> {
    return apiService.post<MeetingRoom>(`${this.baseUrl}/rooms`, data);
  }

  update(id: string, data: UpdateMeetingRoom): Promise<MeetingRoom> {
    return apiService.put<MeetingRoom>(`${this.baseUrl}/rooms/${id}`, { ...data, id });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/rooms/${id}`);
  }
}

class RoomBookingService {
  private readonly baseUrl = '/CompanySchedule';

  getAll(): Promise<RoomBooking[]> {
    return apiService.get<RoomBooking[]>(`${this.baseUrl}/bookings`);
  }

  getPaged(pageNumber = 1, pageSize = 20): Promise<PagedResult<RoomBooking>> {
    return apiService.get<PagedResult<RoomBooking>>(`${this.baseUrl}/bookings/paged`, {
      pageNumber,
      pageSize,
    });
  }

  getById(id: string): Promise<RoomBooking> {
    return apiService.get<RoomBooking>(`${this.baseUrl}/bookings/${id}`);
  }

  getByRoom(roomId: string): Promise<RoomBookingSummary[]> {
    return apiService.get<RoomBookingSummary[]>(`${this.baseUrl}/bookings/room/${roomId}`);
  }

  getByBooker(bookedById: string): Promise<RoomBookingSummary[]> {
    return apiService.get<RoomBookingSummary[]>(`${this.baseUrl}/bookings/booker/${bookedById}`);
  }

  getByDateRange(startDate: string, endDate: string): Promise<RoomBookingSummary[]> {
    return apiService.get<RoomBookingSummary[]>(`${this.baseUrl}/bookings/range`, { startDate, endDate });
  }

  getByStatus(status: BookingStatus): Promise<RoomBookingSummary[]> {
    return apiService.get<RoomBookingSummary[]>(`${this.baseUrl}/bookings/status/${status}`);
  }

  getPendingApprovals(): Promise<RoomBookingSummary[]> {
    return apiService.get<RoomBookingSummary[]>(`${this.baseUrl}/bookings/pending-approvals`);
  }

  create(data: CreateRoomBooking): Promise<RoomBooking> {
    return apiService.post<RoomBooking>(`${this.baseUrl}/bookings`, data);
  }

  update(id: string, data: UpdateRoomBooking): Promise<RoomBooking> {
    return apiService.put<RoomBooking>(`${this.baseUrl}/bookings/${id}`, { ...data, id });
  }

  approve(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/bookings/${id}/approve`);
  }

  cancel(id: string, cancellationReason: string): Promise<void> {
    const body: CancelRoomBooking = { bookingId: id, cancellationReason };
    return apiService.post<void>(`${this.baseUrl}/bookings/${id}/cancel`, body);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/bookings/${id}`);
  }
}

class CompanyMilestoneService {
  private readonly baseUrl = '/CompanySchedule';

  getAll(): Promise<CompanyMilestone[]> {
    return apiService.get<CompanyMilestone[]>(`${this.baseUrl}/milestones`);
  }

  getPaged(pageNumber = 1, pageSize = 20): Promise<PagedResult<CompanyMilestone>> {
    return apiService.get<PagedResult<CompanyMilestone>>(`${this.baseUrl}/milestones/paged`, {
      pageNumber,
      pageSize,
    });
  }

  getById(id: string): Promise<CompanyMilestone> {
    return apiService.get<CompanyMilestone>(`${this.baseUrl}/milestones/${id}`);
  }

  getByCategory(category: MilestoneCategory): Promise<CompanyMilestone[]> {
    return apiService.get<CompanyMilestone[]>(`${this.baseUrl}/milestones/category/${category}`);
  }

  getByDateRange(startDate: string, endDate: string): Promise<CompanyMilestone[]> {
    return apiService.get<CompanyMilestone[]>(`${this.baseUrl}/milestones/range`, { startDate, endDate });
  }

  getUpcoming(daysAhead = 90): Promise<CompanyMilestone[]> {
    return apiService.get<CompanyMilestone[]>(`${this.baseUrl}/milestones/upcoming`, { daysAhead });
  }

  create(data: CreateCompanyMilestone): Promise<CompanyMilestone> {
    return apiService.post<CompanyMilestone>(`${this.baseUrl}/milestones`, data);
  }

  update(id: string, data: UpdateCompanyMilestone): Promise<CompanyMilestone> {
    return apiService.put<CompanyMilestone>(`${this.baseUrl}/milestones/${id}`, { ...data, id });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/milestones/${id}`);
  }
}

class BusinessClosureService {
  private readonly baseUrl = '/CompanySchedule';

  getAll(): Promise<BusinessClosure[]> {
    return apiService.get<BusinessClosure[]>(`${this.baseUrl}/closures`);
  }

  getPaged(pageNumber = 1, pageSize = 20): Promise<PagedResult<BusinessClosure>> {
    return apiService.get<PagedResult<BusinessClosure>>(`${this.baseUrl}/closures/paged`, {
      pageNumber,
      pageSize,
    });
  }

  getById(id: string): Promise<BusinessClosure> {
    return apiService.get<BusinessClosure>(`${this.baseUrl}/closures/${id}`);
  }

  getByDateRange(startDate: string, endDate: string): Promise<BusinessClosure[]> {
    return apiService.get<BusinessClosure[]>(`${this.baseUrl}/closures/range`, { startDate, endDate });
  }

  getByType(type: ClosureType): Promise<BusinessClosure[]> {
    return apiService.get<BusinessClosure[]>(`${this.baseUrl}/closures/type/${type}`);
  }

  getByLocation(locationId: string): Promise<BusinessClosure[]> {
    return apiService.get<BusinessClosure[]>(`${this.baseUrl}/closures/location/${locationId}`);
  }

  getUpcoming(daysAhead = 30): Promise<BusinessClosure[]> {
    return apiService.get<BusinessClosure[]>(`${this.baseUrl}/closures/upcoming`, { daysAhead });
  }

  /**
   * Whether a date is a day off: a company-wide closure, or one of the site's or the unit's (the unit's
   * own or an ancestor's). A partial closure is a working day, so it never answers true. With neither
   * a site nor a unit, company-wide closures only.
   */
  isClosureDate(date: string, locationId?: string, organizationUnitId?: string): Promise<boolean> {
    return apiService.get<boolean>(`${this.baseUrl}/closures/is-closure-date`, {
      date,
      ...(locationId ? { locationId } : {}),
      ...(organizationUnitId ? { organizationUnitId } : {}),
    });
  }

  create(data: CreateBusinessClosure): Promise<BusinessClosure> {
    return apiService.post<BusinessClosure>(`${this.baseUrl}/closures`, data);
  }

  update(id: string, data: UpdateBusinessClosure): Promise<BusinessClosure> {
    return apiService.put<BusinessClosure>(`${this.baseUrl}/closures/${id}`, { ...data, id });
  }

  /** Company Admin. Answers the leave the removal recounted (the days are working days again). */
  remove(id: string): Promise<LeaveRechargeResult> {
    return apiService.delete<LeaveRechargeResult>(`${this.baseUrl}/closures/${id}`);
  }

  /** What announcing the closure would say, and to how many active staff. Saves nothing. */
  getAnnouncementPreview(id: string): Promise<ClosureAnnouncementPreview> {
    return apiService.get<ClosureAnnouncementPreview>(`${this.baseUrl}/closures/${id}/announcement`);
  }

  /**
   * Publishes the announcement to the staff the closure covers — HR's click, never a save's side
   * effect (L1-1). Refused (422) when the closure is over or covers nobody.
   */
  announce(id: string): Promise<HrAnnouncement> {
    return apiService.post<HrAnnouncement>(`${this.baseUrl}/closures/${id}/announce`);
  }
}

class FiscalYearService {
  private readonly baseUrl = '/CompanySchedule';

  getAll(): Promise<FiscalYear[]> {
    return apiService.get<FiscalYear[]>(`${this.baseUrl}/fiscal-years`);
  }

  getPaged(pageNumber = 1, pageSize = 20): Promise<PagedResult<FiscalYear>> {
    return apiService.get<PagedResult<FiscalYear>>(`${this.baseUrl}/fiscal-years/paged`, {
      pageNumber,
      pageSize,
    });
  }

  getById(id: string): Promise<FiscalYear> {
    return apiService.get<FiscalYear>(`${this.baseUrl}/fiscal-years/${id}`);
  }

  /** Fiscal year plus its periods. */
  getDetail(id: string): Promise<FiscalYearDetail> {
    return apiService.get<FiscalYearDetail>(`${this.baseUrl}/fiscal-years/${id}/details`);
  }

  getByYear(year: number): Promise<FiscalYear> {
    return apiService.get<FiscalYear>(`${this.baseUrl}/fiscal-years/by-year/${year}`);
  }

  getCurrent(): Promise<FiscalYear> {
    return apiService.get<FiscalYear>(`${this.baseUrl}/fiscal-years/current`);
  }

  getByStatus(status: FiscalYearStatus): Promise<FiscalYear[]> {
    return apiService.get<FiscalYear[]>(`${this.baseUrl}/fiscal-years/status/${status}`);
  }

  create(data: CreateFiscalYear): Promise<FiscalYear> {
    return apiService.post<FiscalYear>(`${this.baseUrl}/fiscal-years`, data);
  }

  update(id: string, data: UpdateFiscalYear): Promise<FiscalYear> {
    return apiService.put<FiscalYear>(`${this.baseUrl}/fiscal-years/${id}`, { ...data, id });
  }

  setCurrent(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/fiscal-years/${id}/set-current`);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/fiscal-years/${id}`);
  }

  // ── periods ───────────────────────────────────────────────────────────────

  getPeriods(fiscalYearId: string): Promise<FiscalPeriod[]> {
    return apiService.get<FiscalPeriod[]>(`${this.baseUrl}/fiscal-years/${fiscalYearId}/periods`);
  }

  addPeriod(fiscalYearId: string, data: Omit<CreateFiscalPeriod, 'fiscalYearId'>): Promise<FiscalPeriod> {
    return apiService.post<FiscalPeriod>(`${this.baseUrl}/fiscal-years/${fiscalYearId}/periods`, {
      ...data,
      fiscalYearId,
    });
  }

  updatePeriod(periodId: string, data: UpdateFiscalPeriod): Promise<FiscalPeriod> {
    return apiService.put<FiscalPeriod>(`${this.baseUrl}/periods/${periodId}`, { ...data, id: periodId });
  }

  closePeriod(periodId: string): Promise<void> {
    const body: CloseFiscalPeriod = { periodId };
    return apiService.post<void>(`${this.baseUrl}/periods/${periodId}/close`, body);
  }

  removePeriod(periodId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/periods/${periodId}`);
  }
}



/**
 * The diary and the chase doors (round 4, D5/D6).
 *
 * ⚠ Its own class rather than methods on `companyEventService`: a personal schedule is not an event,
 * and the reminder doors act on an event but are about telling people. They share the controller,
 * not the subject.
 */
class PersonalScheduleService {
  private readonly baseUrl = '/CompanySchedule';

  /**
   * Everything the SIGNED-IN employee is committed to between two dates.
   *
   * ⚠ No employee id parameter, deliberately: the server takes it from the token. One here would
   * let any signed-in user read a colleague's leave and travel.
   *
   * ⚠ Sixty days maximum — the server refuses a wider range rather than walking it.
   */
  getMySchedule(from: string, to: string): Promise<PersonalSchedule> {
    return apiService.get<PersonalSchedule>(`${this.baseUrl}/my-schedule`, { from, to });
  }

  /** A unit and its subtree. Gated on the company-schedule WRITE policy — it exposes other people's leave. */
  getTeamSchedule(organizationUnitId: string, from: string, to: string): Promise<TeamSchedule> {
    return apiService.get<TeamSchedule>(`${this.baseUrl}/team-schedule/${organizationUnitId}`, { from, to });
  }
}

export const companyEventService = new CompanyEventService();
export const meetingRoomService = new MeetingRoomService();
export const roomBookingService = new RoomBookingService();
export const companyMilestoneService = new CompanyMilestoneService();
export const businessClosureService = new BusinessClosureService();
export const fiscalYearService = new FiscalYearService();
export const personalScheduleService = new PersonalScheduleService();
