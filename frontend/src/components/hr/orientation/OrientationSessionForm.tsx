'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  DateTimeField,
  FieldRow,
  toIsoInstant,
  fromIsoInstant,
} from '@/components/hr/employee/tabs/fields';
import { ORIENTATION_DELIVERY_MODE_OPTIONS } from '@/types/hr/orientation';
import type { OrientationSession, OrientationProgramSummary } from '@/types/hr/orientation';

export const orientationSessionSchema = z
  .object({
    programId: z.string().optional().or(z.literal('')),
    title: z.string().min(1, 'A title is required').max(200),
    description: z.string().max(2000).optional().or(z.literal('')),
    deliveryMode: z.enum([
      'InPerson',
      'VirtualInstructor',
      'SelfPacedOnline',
      'Blended',
      'VideoOnDemand',
      'PrintedMaterial',
    ]),
    scheduledStartAt: z.string().optional().or(z.literal('')),
    scheduledEndAt: z.string().optional().or(z.literal('')),
    actualStartAt: z.string().optional().or(z.literal('')),
    actualEndAt: z.string().optional().or(z.literal('')),
    venueDescription: z.string().max(500).optional().or(z.literal('')),
    virtualMeetingUrl: z.string().max(1000).optional().or(z.literal('')),
    maxParticipants: z.coerce.number().min(1).max(100000).optional(),
    enrollmentDeadlineAt: z.string().optional().or(z.literal('')),
    allowWaitlist: z.boolean(),
    requiresApproval: z.boolean(),
    recordingUrl: z.string().max(1000).optional().or(z.literal('')),
    participantInstructions: z.string().max(4000).optional().or(z.literal('')),
  })
  .refine(
    (v) => !v.scheduledStartAt || !v.scheduledEndAt || v.scheduledStartAt <= v.scheduledEndAt,
    { message: 'A session cannot end before it starts.', path: ['scheduledEndAt'] },
  )
  .refine((v) => !v.actualStartAt || !v.actualEndAt || v.actualStartAt <= v.actualEndAt, {
    message: 'A session cannot end before it starts.',
    path: ['actualEndAt'],
  })
  // Enrolling after the session has begun is not a deadline, it is an oversight — and the server
  // will not stop it.
  .refine(
    (v) =>
      !v.enrollmentDeadlineAt ||
      !v.scheduledStartAt ||
      v.enrollmentDeadlineAt <= v.scheduledStartAt,
    {
      message: 'The enrollment deadline should fall on or before the session starts.',
      path: ['enrollmentDeadlineAt'],
    },
  );

export type OrientationSessionFormValues = z.infer<typeof orientationSessionSchema>;

export const emptyOrientationSession: OrientationSessionFormValues = {
  programId: '',
  title: '',
  description: '',
  deliveryMode: 'InPerson',
  scheduledStartAt: '',
  scheduledEndAt: '',
  actualStartAt: '',
  actualEndAt: '',
  venueDescription: '',
  virtualMeetingUrl: '',
  maxParticipants: undefined,
  enrollmentDeadlineAt: '',
  allowWaitlist: true,
  requiresApproval: false,
  recordingUrl: '',
  participantInstructions: '',
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);

/** Seeds the form from a session, converting every instant into the local string the inputs need. */
export function toSessionFormValues(s: OrientationSession): OrientationSessionFormValues {
  return {
    programId: s.programId,
    title: s.title,
    description: s.description ?? '',
    deliveryMode: s.deliveryMode,
    scheduledStartAt: fromIsoInstant(s.scheduledStartAt),
    scheduledEndAt: fromIsoInstant(s.scheduledEndAt),
    actualStartAt: fromIsoInstant(s.actualStartAt),
    actualEndAt: fromIsoInstant(s.actualEndAt),
    venueDescription: s.venueDescription ?? '',
    virtualMeetingUrl: s.virtualMeetingUrl ?? '',
    maxParticipants: s.maxParticipants ?? undefined,
    enrollmentDeadlineAt: fromIsoInstant(s.enrollmentDeadlineAt),
    allowWaitlist: s.allowWaitlist,
    requiresApproval: s.requiresApproval,
    recordingUrl: s.recordingUrl ?? '',
    participantInstructions: s.participantInstructions ?? '',
  };
}

/** The fields both create and update share, with every local datetime back to an ISO instant. */
export function toSessionRequest(values: OrientationSessionFormValues) {
  return {
    title: values.title,
    description: blank(values.description),
    deliveryMode: values.deliveryMode,
    scheduledStartAt: toIsoInstant(values.scheduledStartAt),
    scheduledEndAt: toIsoInstant(values.scheduledEndAt),
    venueDescription: blank(values.venueDescription),
    virtualMeetingUrl: blank(values.virtualMeetingUrl),
    maxParticipants: values.maxParticipants ?? null,
    enrollmentDeadlineAt: toIsoInstant(values.enrollmentDeadlineAt),
    allowWaitlist: values.allowWaitlist,
    requiresApproval: values.requiresApproval,
    recordingUrl: blank(values.recordingUrl),
    participantInstructions: blank(values.participantInstructions),
  };
}

interface Props {
  defaultValues: OrientationSessionFormValues;
  onSubmit: (values: OrientationSessionFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel?: () => void;
  /**
   * Offered only when creating — a session cannot be moved to another programme, and the update
   * payload has no field for it.
   */
  programs?: OrientationProgramSummary[];
  /** Actual start/end are a record of what happened, so they only appear once one exists. */
  showActuals?: boolean;
  /** Renders without the surrounding cards, for use inside a dialog. */
  compact?: boolean;
}

export function OrientationSessionForm({
  defaultValues,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
  programs,
  showActuals = false,
  compact = false,
}: Props) {
  const form = useForm<OrientationSessionFormValues>({
    resolver: zodResolver(orientationSessionSchema) as any,
    defaultValues,
  });

  const mode = form.watch('deliveryMode');
  const isVirtual =
    mode === 'VirtualInstructor' || mode === 'SelfPacedOnline' || mode === 'VideoOnDemand';
  const isInPerson = mode === 'InPerson' || mode === 'Blended';

  const scheduling = (
    <>
      {programs && (
        <SelectField
          form={form}
          name="programId"
          label="Programme"
          required
          options={programs.map((p) => ({
            value: p.id,
            label: `${p.programCode} — ${p.title}`,
          }))}
        />
      )}
      <TextField form={form} name="title" label="Title" required />
      <TextareaField form={form} name="description" label="Description" rows={2} />
      <FieldRow>
        <SelectField
          form={form}
          name="deliveryMode"
          label="Delivery mode"
          required
          options={ORIENTATION_DELIVERY_MODE_OPTIONS}
        />
        <NumberField
          form={form}
          name="maxParticipants"
          label="Max participants"
          placeholder="Leave empty for uncapped"
        />
      </FieldRow>
      <FieldRow>
        <DateTimeField form={form} name="scheduledStartAt" label="Scheduled start" />
        <DateTimeField form={form} name="scheduledEndAt" label="Scheduled end" />
      </FieldRow>
      <DateTimeField form={form} name="enrollmentDeadlineAt" label="Enrollment deadline" />
      {showActuals && (
        <FieldRow>
          <DateTimeField form={form} name="actualStartAt" label="Actual start" />
          <DateTimeField form={form} name="actualEndAt" label="Actual end" />
        </FieldRow>
      )}
    </>
  );

  const logistics = (
    <>
      {isInPerson && (
        <TextField
          form={form}
          name="venueDescription"
          label="Venue"
          placeholder="Room, building, address"
        />
      )}
      {isVirtual && (
        <TextField
          form={form}
          name="virtualMeetingUrl"
          label="Joining link"
          placeholder="https://…"
        />
      )}
      {!isInPerson && !isVirtual && (
        <FieldRow>
          <TextField form={form} name="venueDescription" label="Venue" />
          <TextField form={form} name="virtualMeetingUrl" label="Joining link" />
        </FieldRow>
      )}
      <TextField form={form} name="recordingUrl" label="Recording URL" placeholder="https://…" />
      <TextareaField
        form={form}
        name="participantInstructions"
        label="Participant instructions"
        rows={3}
        placeholder="What to bring, where to go, how to join."
      />
      <FieldRow>
        <SwitchField
          form={form}
          name="allowWaitlist"
          label="Allow a waitlist"
          description="Enrolling onto a full session queues the participant instead of being refused."
        />
        <SwitchField
          form={form}
          name="requiresApproval"
          label="Requires approval"
          description="Enrollments start as pending confirmation."
        />
      </FieldRow>
    </>
  );

  const actions = (
    <div className="flex justify-end gap-2">
      {onCancel && (
        <Button type="button" variant="outline" onClick={onCancel} disabled={submitting}>
          Cancel
        </Button>
      )}
      <Button type="submit" disabled={submitting}>
        {submitting ? (
          <Loader2 className="mr-2 h-4 w-4 animate-spin" />
        ) : (
          <Save className="mr-2 h-4 w-4" />
        )}
        {submitLabel}
      </Button>
    </div>
  );

  if (compact) {
    return (
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
        <div className="max-h-[60vh] space-y-4 overflow-y-auto pr-1">
          {scheduling}
          {logistics}
        </div>
        {actions}
      </form>
    );
  }

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Scheduling</CardTitle>
          <CardDescription>When this run of the programme happens, and how big it is.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">{scheduling}</CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Logistics</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">{logistics}</CardContent>
      </Card>

      {actions}
    </form>
  );
}
