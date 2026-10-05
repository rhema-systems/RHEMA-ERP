'use client';

import { useQuery } from '@tanstack/react-query';
import { useForm, type UseFormReturn } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  DateField,
  DateTimeField,
  FieldRow,
  NumberField,
  SelectField,
  SwitchField,
  TextField,
  TextareaField,
  TimeField,
  fromIsoInstant,
  toIsoInstant,
} from '@/components/hr/employee/tabs/fields';
import { locationService } from '@/services/hr/location.service';
import { siteOptions } from '@/components/hr/company-schedule/siteOptions';
import { OrganizationUnitPickerField } from '@/components/hr/common/OrganizationUnitPickerField';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { companyEventService } from '@/services/hr/company-schedule.service';
import {
  EVENT_CATEGORIES_FOR_NEW,
  EVENT_EDITABLE_STATUSES,
  EVENT_LOCATION_TYPES,
  EVENT_PRIORITIES,
  EVENT_TYPES,
  EVENT_VISIBILITIES,
  PARTICIPANT_SCOPES,
  RECURRENCE_PATTERNS,
  RECURRENCE_PATTERN_LABELS,
} from '@/types/hr/company-schedule';
import type {
  CompanyEvent,
  CreateCompanyEvent,
  EventStatus,
  EventVisibility,
  ParticipantScope,
  UpdateCompanyEvent,
} from '@/types/hr/company-schedule';

/**
 * The event authoring form, shared by create and edit.
 *
 * **The organiser is a choice (D-11, lane 2a)**, empty meaning "me": the API records whoever saves it
 * as the creator either way, so the field never lets anyone act as someone else.
 *
 * ⚠ **Create and update are not the same shape.** Recurrence is create-only — the update DTO drops
 * it entirely, so sending it on an edit would silently do nothing. Status and actual cost are the
 * mirror image: update-only. The `mode` prop decides which half renders, rather than one form
 * pretending both exist.
 *
 * ⚠ **On an edit, changing the dates, times or the all-day switch is a reschedule** (F-37): the form
 * then asks for the reason, which everybody invited is told. The rules below mirror the server's, so a
 * refusal is seen before the save rather than after it.
 */

/** "09:30:00" or "09:30" → "09:30", so the two shapes compare. */
const hm = (t?: string | null) => (t ?? '').slice(0, 5);

const schema = z.object({
  eventName: z.string().min(1, 'Name is required').max(100),
  description: z.string().max(1000).optional().or(z.literal('')),
  category: z.string().min(1, 'Category is required'),
  type: z.string().min(1, 'Type is required'),
  priority: z.string().min(1),

  startDate: z.string().min(1, 'Start date is required'),
  startTime: z.string().optional().or(z.literal('')),
  endDate: z.string().min(1, 'End date is required'),
  endTime: z.string().optional().or(z.literal('')),
  isAllDayEvent: z.boolean(),

  isRecurring: z.boolean(),
  recurrencePattern: z.string().optional().or(z.literal('')),
  recurrenceDetails: z.string().max(500).optional().or(z.literal('')),
  recurrenceEndDate: z.string().optional().or(z.literal('')),
  recurrenceCount: z.coerce.number().int().min(0).optional(),

  locationType: z.string().min(1, 'Location type is required'),
  venueName: z.string().max(200).optional().or(z.literal('')),
  venueAddress: z.string().max(500).optional().or(z.literal('')),
  onlineMeetingLink: z.string().max(700).optional().or(z.literal('')),
  meetingPassword: z.string().max(100).optional().or(z.literal('')),
  locationId: z.string().optional().or(z.literal('')),
  organizationUnitId: z.string().optional().or(z.literal('')),
  organizerId: z.string().optional().or(z.literal('')),
  rescheduleReason: z.string().max(2000).optional().or(z.literal('')),

  scope: z.string().min(1, 'Scope is required'),
  estimatedAttendees: z.coerce.number().int().min(0).optional(),
  requiresRsvp: z.boolean(),
  rsvpDeadline: z.string().optional().or(z.literal('')),

  visibility: z.string().min(1),
  showOnCompanyCalendar: z.boolean(),
  showOnIntranet: z.boolean(),

  requiresApproval: z.boolean(),
  status: z.string().optional().or(z.literal('')),

  hasBudget: z.boolean(),
  budgetAmount: z.coerce.number().min(0).optional(),
  actualCost: z.coerce.number().min(0).optional(),
  budgetCode: z.string().max(50).optional().or(z.literal('')),

  requiredResources: z.string().max(1000).optional().or(z.literal('')),
  cateringRequirements: z.string().max(1000).optional().or(z.literal('')),
  technicalRequirements: z.string().max(1000).optional().or(z.literal('')),

  sendReminders: z.boolean(),
  reminderDaysBefore: z.coerce.number().int().min(0).optional(),
  additionalNotes: z.string().max(2000).optional().or(z.literal('')),
})
  .superRefine((v, ctx) => {
    const issue = (path: string, message: string) => ctx.addIssue({ code: 'custom', path: [path], message });
    if (v.endDate && v.startDate && v.endDate < v.startDate)
      issue('endDate', 'The event ends before it starts. Set the last day on or after the first.');
    if (!v.isAllDayEvent) {
      if (!!v.startTime !== !!v.endTime) issue(v.startTime ? 'endTime' : 'startTime', 'Give both a start and an end time, or mark it all-day.');
      else if (v.startTime && v.endTime && hm(v.endTime) <= hm(v.startTime))
        issue('endTime', v.startDate === v.endDate
          ? 'The end time must be after the start time.'
          : "Over several days the times are each day's hours: set the end time after the start time.");
    }
    if (v.isRecurring && !v.recurrencePattern) issue('recurrencePattern', 'Pick a pattern for a recurring event');
    // Lane 2f-1: the server makes the series from a count OR an end date — one, not both, as a calendar rule does.
    if (v.isRecurring && !!v.recurrenceCount && !!v.recurrenceEndDate)
      issue('recurrenceEndDate', 'Give how many times it repeats, or the date it repeats until — not both.');
    if (v.isRecurring && !v.recurrenceCount && !v.recurrenceEndDate)
      issue('recurrenceCount', 'Give how many times it repeats (2 to 52), or the date it repeats until.');
    if (v.isRecurring && v.recurrenceCount && (v.recurrenceCount < 2 || v.recurrenceCount > 52))
      issue('recurrenceCount', 'A series repeats 2 to 52 times; it can be extended later.');
    if (v.requiresRsvp) {
      if (!v.rsvpDeadline) issue('rsvpDeadline', 'An event that asks for replies needs a reply-by date.');
      else if (v.startDate && v.rsvpDeadline > `${v.startDate}T${v.isAllDayEvent || !v.startTime ? '00:00' : hm(v.startTime)}`)
        issue('rsvpDeadline', 'The RSVP deadline falls after the event starts. Set it on or before the start.');
    }
    if (v.sendReminders && (v.reminderDaysBefore === undefined || Number.isNaN(v.reminderDaysBefore)))
      issue('reminderDaysBefore', 'Say how many days before the event the reminder goes.');
    if (v.scope === 'Department' && !v.organizationUnitId)
      issue('organizationUnitId', 'An event for a unit needs the unit.');
  });

/**
 * Whether the form's dates, times or all-day switch differ from the event's — what makes an edit a
 * reschedule (F-37), so the form asks for the reason.
 */
export function windowChanged(e: CompanyEvent, v: EventFormValues): boolean {
  if (e.startDate.slice(0, 10) !== v.startDate || e.endDate.slice(0, 10) !== v.endDate) return true;
  if (e.isAllDayEvent !== v.isAllDayEvent) return true;
  return !v.isAllDayEvent && (hm(e.startTime) !== hm(v.startTime) || hm(e.endTime) !== hm(v.endTime));
}

export type EventFormValues = z.infer<typeof schema>;

export const emptyEventForm: EventFormValues = {
  eventName: '',
  description: '',
  category: 'Meeting',
  type: 'Internal',
  priority: 'Medium',
  startDate: new Date().toISOString().slice(0, 10),
  startTime: '',
  endDate: new Date().toISOString().slice(0, 10),
  endTime: '',
  isAllDayEvent: false,
  isRecurring: false,
  recurrencePattern: '',
  recurrenceDetails: '',
  recurrenceEndDate: '',
  recurrenceCount: undefined,
  locationType: 'OnSite',
  venueName: '',
  venueAddress: '',
  onlineMeetingLink: '',
  meetingPassword: '',
  locationId: '',
  organizationUnitId: '',
  organizerId: '',
  rescheduleReason: '',
  scope: 'Selected',
  estimatedAttendees: undefined,
  requiresRsvp: false,
  rsvpDeadline: '',
  visibility: 'Public',
  showOnCompanyCalendar: true,
  showOnIntranet: false,
  requiresApproval: false,
  status: 'Scheduled',
  hasBudget: false,
  budgetAmount: undefined,
  actualCost: undefined,
  budgetCode: '',
  requiredResources: '',
  cateringRequirements: '',
  technicalRequirements: '',
  sendReminders: false,
  reminderDaysBefore: undefined,
  additionalNotes: '',
};

export function eventToForm(e: CompanyEvent): EventFormValues {
  return {
    ...emptyEventForm,
    eventName: e.eventName,
    description: e.description ?? '',
    category: e.category,
    type: e.type,
    priority: e.priority,
    startDate: e.startDate.slice(0, 10),
    startTime: e.startTime ?? '',
    endDate: e.endDate.slice(0, 10),
    endTime: e.endTime ?? '',
    isAllDayEvent: e.isAllDayEvent,
    isRecurring: e.isRecurring,
    recurrencePattern: e.recurrencePattern ?? '',
    recurrenceDetails: e.recurrenceDetails ?? '',
    recurrenceEndDate: e.recurrenceEndDate?.slice(0, 10) ?? '',
    recurrenceCount: e.recurrenceCount ?? undefined,
    locationType: e.locationType,
    venueName: e.venueName ?? '',
    venueAddress: e.venueAddress ?? '',
    onlineMeetingLink: e.onlineMeetingLink ?? '',
    meetingPassword: e.meetingPassword ?? '',
    locationId: e.locationId ?? '',
    organizationUnitId: e.organizationUnitId ?? '',
    organizerId: e.organizerId,
    rescheduleReason: '',
    scope: e.scope,
    estimatedAttendees: e.estimatedAttendees ?? undefined,
    requiresRsvp: e.requiresRsvp,
    rsvpDeadline: fromIsoInstant(e.rsvpDeadline),
    visibility: e.visibility,
    showOnCompanyCalendar: e.showOnCompanyCalendar,
    showOnIntranet: e.showOnIntranet,
    requiresApproval: e.requiresApproval,
    status: e.status,
    hasBudget: e.hasBudget,
    budgetAmount: e.budgetAmount ?? undefined,
    actualCost: e.actualCost ?? undefined,
    budgetCode: e.budgetCode ?? '',
    requiredResources: e.requiredResources ?? '',
    cateringRequirements: e.cateringRequirements ?? '',
    technicalRequirements: e.technicalRequirements ?? '',
    sendReminders: e.sendReminders,
    reminderDaysBefore: e.reminderDaysBefore ?? undefined,
    additionalNotes: e.additionalNotes ?? '',
  };
}

const orNull = (s?: string) => (s && s.trim() ? s.trim() : null);
const numOrNull = (n?: number) => (n === undefined || Number.isNaN(n) ? null : n);

/** Form values → the create payload. Recurrence included; status and actual cost are not. */
export function toCreatePayload(v: EventFormValues): CreateCompanyEvent {
  return {
    eventName: v.eventName.trim(),
    description: orNull(v.description),
    category: v.category as CreateCompanyEvent['category'],
    type: v.type as CreateCompanyEvent['type'],
    priority: v.priority as CreateCompanyEvent['priority'],
    startDate: v.startDate,
    startTime: orNull(v.startTime),
    endDate: v.endDate,
    endTime: orNull(v.endTime),
    isAllDayEvent: v.isAllDayEvent,
    isRecurring: v.isRecurring,
    recurrencePattern: v.isRecurring
      ? ((orNull(v.recurrencePattern) as CreateCompanyEvent['recurrencePattern']) ?? null)
      : null,
    recurrenceDetails: v.isRecurring ? orNull(v.recurrenceDetails) : null,
    recurrenceEndDate: v.isRecurring ? orNull(v.recurrenceEndDate) : null,
    recurrenceCount: v.isRecurring ? numOrNull(v.recurrenceCount) : null,
    locationType: v.locationType as CreateCompanyEvent['locationType'],
    venueName: orNull(v.venueName),
    venueAddress: orNull(v.venueAddress),
    onlineMeetingLink: orNull(v.onlineMeetingLink),
    meetingPassword: orNull(v.meetingPassword),
    locationId: orNull(v.locationId),
    organizationUnitId: orNull(v.organizationUnitId),
    organizerId: orNull(v.organizerId),
    scope: v.scope as CreateCompanyEvent['scope'],
    estimatedAttendees: numOrNull(v.estimatedAttendees),
    requiresRsvp: v.requiresRsvp,
    rsvpDeadline: toIsoInstant(v.rsvpDeadline),
    visibility: v.visibility as CreateCompanyEvent['visibility'],
    showOnCompanyCalendar: v.showOnCompanyCalendar,
    showOnIntranet: v.showOnIntranet,
    requiresApproval: v.requiresApproval,
    hasBudget: v.hasBudget,
    budgetAmount: v.hasBudget ? numOrNull(v.budgetAmount) : null,
    budgetCode: v.hasBudget ? orNull(v.budgetCode) : null,
    requiredResources: orNull(v.requiredResources),
    cateringRequirements: orNull(v.cateringRequirements),
    technicalRequirements: orNull(v.technicalRequirements),
    sendReminders: v.sendReminders,
    reminderDaysBefore: v.sendReminders ? numOrNull(v.reminderDaysBefore) : null,
    additionalNotes: orNull(v.additionalNotes),
  };
}

/** Form values → the update payload. Status and actual cost included; recurrence is not. */
export function toUpdatePayload(id: string, v: EventFormValues): UpdateCompanyEvent {
  return {
    id,
    eventName: v.eventName.trim(),
    description: orNull(v.description),
    category: v.category as UpdateCompanyEvent['category'],
    type: v.type as UpdateCompanyEvent['type'],
    priority: v.priority as UpdateCompanyEvent['priority'],
    startDate: v.startDate,
    startTime: orNull(v.startTime),
    endDate: v.endDate,
    endTime: orNull(v.endTime),
    isAllDayEvent: v.isAllDayEvent,
    locationType: v.locationType as UpdateCompanyEvent['locationType'],
    venueName: orNull(v.venueName),
    venueAddress: orNull(v.venueAddress),
    onlineMeetingLink: orNull(v.onlineMeetingLink),
    meetingPassword: orNull(v.meetingPassword),
    locationId: orNull(v.locationId),
    organizationUnitId: orNull(v.organizationUnitId),
    organizerId: orNull(v.organizerId),
    scope: v.scope as UpdateCompanyEvent['scope'],
    estimatedAttendees: numOrNull(v.estimatedAttendees),
    requiresRsvp: v.requiresRsvp,
    rsvpDeadline: toIsoInstant(v.rsvpDeadline),
    visibility: v.visibility as UpdateCompanyEvent['visibility'],
    showOnCompanyCalendar: v.showOnCompanyCalendar,
    showOnIntranet: v.showOnIntranet,
    status: (orNull(v.status) as UpdateCompanyEvent['status']) ?? null,
    rescheduleReason: orNull(v.rescheduleReason),
    hasBudget: v.hasBudget,
    budgetAmount: v.hasBudget ? numOrNull(v.budgetAmount) : null,
    actualCost: v.hasBudget ? numOrNull(v.actualCost) : null,
    budgetCode: v.hasBudget ? orNull(v.budgetCode) : null,
    requiredResources: orNull(v.requiredResources),
    cateringRequirements: orNull(v.cateringRequirements),
    technicalRequirements: orNull(v.technicalRequirements),
    sendReminders: v.sendReminders,
    reminderDaysBefore: v.sendReminders ? numOrNull(v.reminderDaysBefore) : null,
    additionalNotes: orNull(v.additionalNotes),
  };
}

const opts = (values: readonly string[]) =>
  values.map((v) => ({ value: v, label: v.replace(/([a-z])([A-Z])/g, '$1 $2') }));

export function useEventForm(initial: EventFormValues) {
  return useForm<EventFormValues>({
    resolver: zodResolver(schema) as any,
    defaultValues: initial,
  });
}

/**
 * The statuses an edit can offer (lane 2a): scheduled, in progress, postponed; confirmed where no
 * approval is needed or it has been given; and whatever the event already is, so the field shows it.
 */
function statusOptions(event?: CompanyEvent) {
  const allowed = new Set<EventStatus>(EVENT_EDITABLE_STATUSES);
  if (event && (!event.requiresApproval || event.approvalDate)) allowed.add('Confirmed');
  if (event) allowed.add(event.status);
  return opts([...allowed]);
}

export function EventFormFields({
  form,
  mode,
  event,
}: {
  form: UseFormReturn<EventFormValues>;
  mode: 'create' | 'edit';
  /** The event being edited — for its organiser's name, its status, and whether the dates moved. */
  event?: CompanyEvent;
}) {
  const { data: locations } = useQuery({
    queryKey: ['hr', 'locations', 'all'],
    queryFn: () => locationService.getAll(),
  });

  const locationType = form.watch('locationType');
  const isVirtual = locationType === 'Virtual' || locationType === 'Hybrid';
  const isPhysical = locationType !== 'Virtual';
  const isRecurring = form.watch('isRecurring');
  const isAllDay = form.watch('isAllDayEvent');
  const hasBudget = form.watch('hasBudget');
  const sendReminders = form.watch('sendReminders');
  const requiresRsvp = form.watch('requiresRsvp');
  const scope = form.watch('scope');
  const category = form.watch('category');
  const moved = mode === 'edit' && !!event && windowChanged(event, form.watch());

  // A public holiday or milestone stays offered only on an older event that already is one (F-44).
  const forNew: readonly string[] = EVENT_CATEGORIES_FOR_NEW;
  const categories = mode === 'create' || forNew.includes(category) ? forNew : [...forNew, category];

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader><CardTitle>Basics</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <TextField form={form} name="eventName" label="Event name" required />
          <TextareaField form={form} name="description" label="Description" />
          <FieldRow>
            <SelectField
              form={form}
              name="category"
              label="Category"
              required
              options={opts(categories)}
              description="Public holidays and company milestones have their own registers."
            />
            <SelectField form={form} name="type" label="Type" required options={opts(EVENT_TYPES)} />
          </FieldRow>
          <FieldRow>
            <SelectField
              form={form}
              name="priority"
              label="Priority"
              required
              options={opts(EVENT_PRIORITIES)}
              description="A label for HR's own sorting; it changes nothing about the event."
            />
            {mode === 'edit' && (
              <SelectField
                form={form}
                name="status"
                label="Status"
                options={statusOptions(event)}
                description="Cancel, complete and reschedule have their own buttons on the event."
              />
            )}
          </FieldRow>
          <EmployeePickerField
            form={form}
            name="organizerId"
            label="Organiser"
            initialLabel={event?.organizerName ?? null}
            placeholder={mode === 'create' ? 'You — or search for someone else' : 'Search for the organiser'}
          />
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>When</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <SwitchField form={form} name="isAllDayEvent" label="All-day event" />
          <FieldRow>
            <DateField form={form} name="startDate" label="Start date" required />
            <DateField form={form} name="endDate" label="End date" required />
          </FieldRow>
          {!isAllDay && (
            <FieldRow>
              <TimeField form={form} name="startTime" label="Start time" />
              <TimeField form={form} name="endTime" label="End time" />
            </FieldRow>
          )}
          {moved && (
            <div className="space-y-2 rounded-md border border-amber-300 bg-amber-50 p-3 dark:border-amber-800 dark:bg-amber-950">
              <p className="text-sm text-amber-900 dark:text-amber-100">
                Changing the dates or times moves the event. Everybody invited is told why, accepted and
                tentative replies go back to awaiting an answer, its room bookings move with it, the
                original dates are kept, and an approved event waits for approval again.
              </p>
              <TextareaField form={form} name="rescheduleReason" label="Reason for the change" />
            </div>
          )}

          {/* Recurrence is create-only — the update DTO has no recurrence fields at all. A series is lengthened
              from the event page ("Extend the series"); each occurrence is then edited on its own page. */}
          {mode === 'create' && (
            <>
              <SwitchField form={form} name="isRecurring" label="Repeats" />
              {isRecurring && (
                <>
                  <p className="text-xs text-muted-foreground">
                    Saving makes every occurrence now, each a full event with its own number, guest list, replies and
                    register. Give how many times it happens, or the date it runs until — one, not both; at most 52, and
                    the series can be extended later. A monthly series on the 31st falls on the last day of shorter
                    months. An occurrence on a public holiday or a company-wide closure is made and flagged, not skipped.
                  </p>
                  <FieldRow>
                    <SelectField
                      form={form}
                      name="recurrencePattern"
                      label="Repeats"
                      required
                      options={RECURRENCE_PATTERNS.map((p) => ({ value: p, label: RECURRENCE_PATTERN_LABELS[p] }))}
                    />
                    <NumberField form={form} name="recurrenceCount" label="How many times (2–52)" />
                  </FieldRow>
                  <FieldRow>
                    <DateField form={form} name="recurrenceEndDate" label="…or until" />
                    <TextField form={form} name="recurrenceDetails" label="Recurrence notes" />
                  </FieldRow>
                </>
              )}
            </>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Where</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <SelectField
            form={form}
            name="locationType"
            label="Location type"
            required
            options={opts(EVENT_LOCATION_TYPES)}
          />
          {isPhysical && (
            <>
              <FieldRow>
                <SelectField
                  form={form}
                  name="locationId"
                  label="Site"
                  allowEmpty
                  emptyLabel="Not tied to a site"
                  options={siteOptions(locations)}
                />
                <TextField form={form} name="venueName" label="Venue" />
              </FieldRow>
              <TextField form={form} name="venueAddress" label="Venue address" />
            </>
          )}
          {isVirtual && (
            <FieldRow>
              <TextField form={form} name="onlineMeetingLink" label="Meeting link" />
              <TextField form={form} name="meetingPassword" label="Meeting password" />
            </FieldRow>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Who</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <SelectField
            form={form}
            name="scope"
            label="Audience"
            required
            options={opts(PARTICIPANT_SCOPES)}
            description="Who the event is for: all staff; a unit and the units beneath it (Department); management — unit heads and line managers; or only the people you invite (Selected, External only)."
          />
          <OrganizationUnitPickerField
            form={form}
            name="organizationUnitId"
            label="Organisation unit"
            required={scope === 'Department'}
            allowEmpty={scope !== 'Department'}
            emptyLabel="Not for one unit"
            hint={
              scope === 'Department'
                ? 'The unit the event is for, with every unit beneath it.'
                : 'Optional: the unit hosting the event.'
            }
          />
          <FieldRow>
            <NumberField form={form} name="estimatedAttendees" label="Estimated attendees" />
            <SelectField
              form={form}
              name="visibility"
              label="Visibility"
              options={opts(EVENT_VISIBILITIES)}
              description="Public follows the audience. Department narrows it to the unit, Management to management. Private and Confidential: the guests and the organiser only."
            />
          </FieldRow>
          <AudienceLine scope={scope} visibility={form.watch('visibility')} unitId={form.watch('organizationUnitId')} />
          <SwitchField form={form} name="requiresRsvp" label="Requires RSVP" />
          {requiresRsvp && (
            <DateTimeField form={form} name="rsvpDeadline" label="RSVP deadline" />
          )}
          <FieldRow>
            <SwitchField
              form={form}
              name="showOnCompanyCalendar"
              label="Show on company calendar"
              description="On the company calendar, and in the diary of everyone it is for — who may then look busy to an interview panel."
            />
            <SwitchField
              form={form}
              name="showOnIntranet"
              label="Show on intranet"
              description="Lets HR announce it to everyone it is for, from the event page, once it is approved. Nothing is sent on save."
            />
          </FieldRow>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Approval, budget and logistics</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          {/* Create-only: once an event exists, approval is an action, not a checkbox. */}
          {mode === 'create' && (
            <SwitchField form={form} name="requiresApproval" label="Requires approval before it is confirmed" />
          )}
          <SwitchField form={form} name="hasBudget" label="Has a budget" />
          {hasBudget && (
            <FieldRow>
              <NumberField form={form} name="budgetAmount" label="Budget amount" step="0.01" />
              {mode === 'edit' ? (
                <NumberField form={form} name="actualCost" label="Actual cost" step="0.01" />
              ) : (
                <TextField form={form} name="budgetCode" label="Budget code" />
              )}
            </FieldRow>
          )}
          {hasBudget && mode === 'edit' && <TextField form={form} name="budgetCode" label="Budget code" />}
          <TextareaField form={form} name="requiredResources" label="Required resources" />
          <FieldRow>
            <TextareaField form={form} name="cateringRequirements" label="Catering" />
            <TextareaField form={form} name="technicalRequirements" label="Technical" />
          </FieldRow>
          <SwitchField
            form={form}
            name="sendReminders"
            label="Send reminders"
            description="Everybody who has not declined is emailed once, automatically, the days before the event set below — and again if the date moves."
          />
          {sendReminders && <NumberField form={form} name="reminderDaysBefore" label="Days before" required />}
          <TextareaField form={form} name="additionalNotes" label="Notes" />
        </CardContent>
      </Card>
    </div>
  );
}

/**
 * Who the event reaches, as the server counts it, before it is saved (lane 2c, D-16) — and a warning
 * when that is nobody (management with no unit heads or line managers named, an empty unit).
 */
function AudienceLine({ scope, visibility, unitId }: { scope: string; visibility: string; unitId?: string | null }) {
  const { data } = useQuery({
    queryKey: ['hr', 'company-schedule', 'events', 'audience-preview', scope, visibility, unitId ?? ''],
    queryFn: () =>
      companyEventService.previewAudience(scope as ParticipantScope, visibility as EventVisibility, unitId || null),
    enabled: !!scope && !!visibility,
  });
  if (!data) return null;
  return (
    <div className="space-y-1 rounded-md border bg-muted/40 p-3 text-sm">
      <p>
        <span className="font-medium">For:</span> {data.audience}
        {!data.guestListOnly && (
          <span className="text-muted-foreground"> — {data.reach} active staff</span>
        )}
      </p>
      {data.warning && <p className="text-amber-700 dark:text-amber-300">⚠ {data.warning}</p>}
    </div>
  );
}

export function EventFormActions({
  saving,
  onCancel,
  label,
}: {
  saving: boolean;
  onCancel: () => void;
  label: string;
}) {
  return (
    <div className="flex justify-end gap-2">
      <Button type="button" variant="outline" onClick={onCancel} disabled={saving}>
        Cancel
      </Button>
      <Button type="submit" disabled={saving}>
        {saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
        {label}
      </Button>
    </div>
  );
}
