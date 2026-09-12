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
import { departmentService } from '@/services/hr/lookup.service';
import {
  EVENT_CATEGORIES,
  EVENT_LOCATION_TYPES,
  EVENT_PRIORITIES,
  EVENT_STATUSES,
  EVENT_TYPES,
  EVENT_VISIBILITIES,
  PARTICIPANT_SCOPES,
  RECURRENCE_PATTERNS,
} from '@/types/hr/company-schedule';
import type {
  CompanyEvent,
  CreateCompanyEvent,
  UpdateCompanyEvent,
} from '@/types/hr/company-schedule';

/**
 * The event authoring form, shared by create and edit.
 *
 * ⚠ **There is no organiser field, and that is deliberate.** The API takes the organiser from the
 * token; a value the client cannot know is a value the client must not send.
 *
 * ⚠ **Create and update are not the same shape.** Recurrence is create-only — the update DTO drops
 * it entirely, so sending it on an edit would silently do nothing. Status and actual cost are the
 * mirror image: update-only. The `mode` prop decides which half renders, rather than one form
 * pretending both exist.
 */

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
  departmentId: z.string().optional().or(z.literal('')),

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
  .refine((v) => !v.endDate || !v.startDate || v.endDate >= v.startDate, {
    message: 'End date cannot be before the start date',
    path: ['endDate'],
  })
  .refine((v) => !v.isRecurring || !!v.recurrencePattern, {
    message: 'Pick a pattern for a recurring event',
    path: ['recurrencePattern'],
  });

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
  departmentId: '',
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
    departmentId: e.departmentId ?? '',
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
    departmentId: orNull(v.departmentId),
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
    departmentId: orNull(v.departmentId),
    scope: v.scope as UpdateCompanyEvent['scope'],
    estimatedAttendees: numOrNull(v.estimatedAttendees),
    requiresRsvp: v.requiresRsvp,
    rsvpDeadline: toIsoInstant(v.rsvpDeadline),
    visibility: v.visibility as UpdateCompanyEvent['visibility'],
    showOnCompanyCalendar: v.showOnCompanyCalendar,
    showOnIntranet: v.showOnIntranet,
    status: (orNull(v.status) as UpdateCompanyEvent['status']) ?? 'Scheduled',
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

export function EventFormFields({
  form,
  mode,
}: {
  form: UseFormReturn<EventFormValues>;
  mode: 'create' | 'edit';
}) {
  const { data: locations } = useQuery({
    queryKey: ['hr', 'locations', 'all'],
    queryFn: () => locationService.getAll(),
  });
  const { data: departments } = useQuery({
    queryKey: ['hr', 'departments', 'all'],
    queryFn: () => departmentService.getAll(),
  });

  const locationType = form.watch('locationType');
  const isVirtual = locationType === 'Virtual' || locationType === 'Hybrid';
  const isPhysical = locationType !== 'Virtual';
  const isRecurring = form.watch('isRecurring');
  const isAllDay = form.watch('isAllDayEvent');
  const hasBudget = form.watch('hasBudget');
  const sendReminders = form.watch('sendReminders');
  const requiresRsvp = form.watch('requiresRsvp');

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader><CardTitle>Basics</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <TextField form={form} name="eventName" label="Event name" required />
          <TextareaField form={form} name="description" label="Description" />
          <FieldRow>
            <SelectField form={form} name="category" label="Category" required options={opts(EVENT_CATEGORIES)} />
            <SelectField form={form} name="type" label="Type" required options={opts(EVENT_TYPES)} />
          </FieldRow>
          <FieldRow>
            <SelectField form={form} name="priority" label="Priority" required options={opts(EVENT_PRIORITIES)} />
            {mode === 'edit' && (
              <SelectField form={form} name="status" label="Status" options={opts(EVENT_STATUSES)} />
            )}
          </FieldRow>
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

          {/* Recurrence is create-only — the update DTO has no recurrence fields at all. */}
          {mode === 'create' && (
            <>
              <SwitchField form={form} name="isRecurring" label="Repeats" />
              {isRecurring && (
                <>
                  <FieldRow>
                    <SelectField
                      form={form}
                      name="recurrencePattern"
                      label="Pattern"
                      required
                      options={opts(RECURRENCE_PATTERNS)}
                    />
                    <NumberField form={form} name="recurrenceCount" label="Number of occurrences" />
                  </FieldRow>
                  <FieldRow>
                    <DateField form={form} name="recurrenceEndDate" label="Repeat until" />
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
          <FieldRow>
            <SelectField form={form} name="scope" label="Audience" required options={opts(PARTICIPANT_SCOPES)} />
            <SelectField
              form={form}
              name="departmentId"
              label="Department"
              allowEmpty
              emptyLabel="Company-wide"
              options={(departments ?? []).map((d) => ({ value: d.id, label: d.name }))}
            />
          </FieldRow>
          <FieldRow>
            <NumberField form={form} name="estimatedAttendees" label="Estimated attendees" />
            <SelectField form={form} name="visibility" label="Visibility" options={opts(EVENT_VISIBILITIES)} />
          </FieldRow>
          <SwitchField form={form} name="requiresRsvp" label="Requires RSVP" />
          {requiresRsvp && <DateTimeField form={form} name="rsvpDeadline" label="RSVP deadline" />}
          <FieldRow>
            <SwitchField form={form} name="showOnCompanyCalendar" label="Show on company calendar" />
            <SwitchField form={form} name="showOnIntranet" label="Show on intranet" />
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
          <SwitchField form={form} name="sendReminders" label="Send reminders" />
          {sendReminders && <NumberField form={form} name="reminderDaysBefore" label="Days before" />}
          <TextareaField form={form} name="additionalNotes" label="Notes" />
        </CardContent>
      </Card>
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
