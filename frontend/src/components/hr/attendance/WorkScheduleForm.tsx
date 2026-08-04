'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import {
  TextField,
  NumberField,
  TimeField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { WORK_SCHEDULE_TYPE_OPTIONS } from '@/types/hr/attendance';
import type { WorkSchedule } from '@/types/hr/attendance';

/**
 * Work schedules carry ~35 settings, so they get a full page rather than the dialog the
 * smaller setup resources use. The form is grouped the way the settings actually interact:
 * standard hours, then the optional flexi/core-hours overlay, then working days, breaks and
 * overtime, then the grace periods that drive lateness.
 */

const timeString = z
  .string()
  .regex(/^\d{2}:\d{2}(:\d{2})?$/, 'Enter a valid time')
  .transform((v) => (v.length === 5 ? `${v}:00` : v));

const optionalTime = z.union([timeString, z.literal('')]).optional();

export const workScheduleSchema = z
  .object({
    scheduleName: z.string().min(1, 'A name is required').max(150),
    description: z.string().max(1000).optional(),
    type: z.enum(['Fixed', 'Flexible', 'Shift', 'Compressed', 'PartTime']),
    isDefault: z.boolean(),
    isActive: z.boolean(),

    standardStartTime: timeString,
    standardEndTime: timeString,
    standardHoursPerDay: z.coerce.number().min(0).max(24),
    standardHoursPerWeek: z.coerce.number().min(0).max(168),

    hasFlexibleStartTime: z.boolean(),
    flexibleStartTimeEarliest: optionalTime,
    flexibleStartTimeLatest: optionalTime,
    hasFlexibleEndTime: z.boolean(),
    flexibleEndTimeEarliest: optionalTime,
    flexibleEndTimeLatest: optionalTime,

    hasCoreHours: z.boolean(),
    coreHoursStart: optionalTime,
    coreHoursEnd: optionalTime,

    hasMandatoryBreak: z.boolean(),
    breakDurationMinutes: z.coerce.number().min(0).max(480).optional(),
    isBreakPaid: z.boolean(),

    worksMonday: z.boolean(),
    worksTuesday: z.boolean(),
    worksWednesday: z.boolean(),
    worksThursday: z.boolean(),
    worksFriday: z.boolean(),
    worksSaturday: z.boolean(),
    worksSunday: z.boolean(),

    allowsOvertime: z.boolean(),
    overtimeRequiresPreApproval: z.boolean(),
    maxOvertimeHoursPerDay: z.coerce.number().min(0).max(24).optional(),
    maxOvertimeHoursPerWeek: z.coerce.number().min(0).max(168).optional(),

    lateGracePeriodMinutes: z.coerce.number().min(0).max(480).optional(),
    earlyDepartureGracePeriodMinutes: z.coerce.number().min(0).max(480).optional(),
  })
  // A schedule nobody works is almost always a mistake rather than an intent, and it would
  // make every day an off-day for anyone assigned to it.
  .refine(
    (v) =>
      v.worksMonday ||
      v.worksTuesday ||
      v.worksWednesday ||
      v.worksThursday ||
      v.worksFriday ||
      v.worksSaturday ||
      v.worksSunday,
    { message: 'Select at least one working day', path: ['worksMonday'] },
  )
  .refine((v) => !v.hasMandatoryBreak || (v.breakDurationMinutes ?? 0) > 0, {
    message: 'Set the break length',
    path: ['breakDurationMinutes'],
  });

export type WorkScheduleFormValues = z.input<typeof workScheduleSchema>;
export type WorkScheduleFormOutput = z.output<typeof workScheduleSchema>;

export const emptyWorkSchedule: WorkScheduleFormValues = {
  scheduleName: '',
  description: '',
  type: 'Fixed',
  isDefault: false,
  isActive: true,
  standardStartTime: '08:00:00',
  standardEndTime: '17:00:00',
  standardHoursPerDay: 8,
  standardHoursPerWeek: 40,
  hasFlexibleStartTime: false,
  flexibleStartTimeEarliest: '',
  flexibleStartTimeLatest: '',
  hasFlexibleEndTime: false,
  flexibleEndTimeEarliest: '',
  flexibleEndTimeLatest: '',
  hasCoreHours: false,
  coreHoursStart: '',
  coreHoursEnd: '',
  hasMandatoryBreak: false,
  breakDurationMinutes: 60,
  isBreakPaid: false,
  worksMonday: true,
  worksTuesday: true,
  worksWednesday: true,
  worksThursday: true,
  worksFriday: true,
  worksSaturday: false,
  worksSunday: false,
  allowsOvertime: false,
  overtimeRequiresPreApproval: true,
  maxOvertimeHoursPerDay: undefined,
  maxOvertimeHoursPerWeek: undefined,
  lateGracePeriodMinutes: 0,
  earlyDepartureGracePeriodMinutes: 0,
};

export function toWorkScheduleForm(s: WorkSchedule): WorkScheduleFormValues {
  return {
    scheduleName: s.scheduleName,
    description: s.description ?? '',
    type: s.type,
    isDefault: s.isDefault,
    isActive: s.isActive,
    standardStartTime: s.standardStartTime,
    standardEndTime: s.standardEndTime,
    standardHoursPerDay: s.standardHoursPerDay,
    standardHoursPerWeek: s.standardHoursPerWeek,
    hasFlexibleStartTime: s.hasFlexibleStartTime,
    flexibleStartTimeEarliest: s.flexibleStartTimeEarliest ?? '',
    flexibleStartTimeLatest: s.flexibleStartTimeLatest ?? '',
    hasFlexibleEndTime: s.hasFlexibleEndTime,
    flexibleEndTimeEarliest: s.flexibleEndTimeEarliest ?? '',
    flexibleEndTimeLatest: s.flexibleEndTimeLatest ?? '',
    hasCoreHours: s.hasCoreHours,
    coreHoursStart: s.coreHoursStart ?? '',
    coreHoursEnd: s.coreHoursEnd ?? '',
    hasMandatoryBreak: s.hasMandatoryBreak,
    breakDurationMinutes: s.breakDurationMinutes ?? 60,
    isBreakPaid: s.isBreakPaid,
    worksMonday: s.worksMonday,
    worksTuesday: s.worksTuesday,
    worksWednesday: s.worksWednesday,
    worksThursday: s.worksThursday,
    worksFriday: s.worksFriday,
    worksSaturday: s.worksSaturday,
    worksSunday: s.worksSunday,
    allowsOvertime: s.allowsOvertime,
    overtimeRequiresPreApproval: s.overtimeRequiresPreApproval,
    maxOvertimeHoursPerDay: s.maxOvertimeHoursPerDay ?? undefined,
    maxOvertimeHoursPerWeek: s.maxOvertimeHoursPerWeek ?? undefined,
    lateGracePeriodMinutes: s.lateGracePeriodMinutes ?? 0,
    earlyDepartureGracePeriodMinutes: s.earlyDepartureGracePeriodMinutes ?? 0,
  };
}

/** Blank optional times must go to the API as null, not "". */
export function normalizeWorkSchedule(values: WorkScheduleFormOutput) {
  const blankToNull = (v?: string) => (v && v.length > 0 ? v : null);
  return {
    ...values,
    description: values.description || null,
    flexibleStartTimeEarliest: blankToNull(values.flexibleStartTimeEarliest),
    flexibleStartTimeLatest: blankToNull(values.flexibleStartTimeLatest),
    flexibleEndTimeEarliest: blankToNull(values.flexibleEndTimeEarliest),
    flexibleEndTimeLatest: blankToNull(values.flexibleEndTimeLatest),
    coreHoursStart: blankToNull(values.coreHoursStart),
    coreHoursEnd: blankToNull(values.coreHoursEnd),
    breakDurationMinutes: values.hasMandatoryBreak ? (values.breakDurationMinutes ?? null) : null,
    maxOvertimeHoursPerDay: values.allowsOvertime ? (values.maxOvertimeHoursPerDay ?? null) : null,
    maxOvertimeHoursPerWeek: values.allowsOvertime ? (values.maxOvertimeHoursPerWeek ?? null) : null,
  };
}

const WEEKDAYS = [
  ['worksMonday', 'Mon'],
  ['worksTuesday', 'Tue'],
  ['worksWednesday', 'Wed'],
  ['worksThursday', 'Thu'],
  ['worksFriday', 'Fri'],
  ['worksSaturday', 'Sat'],
  ['worksSunday', 'Sun'],
] as const;

interface WorkScheduleFormProps {
  defaultValues: WorkScheduleFormValues;
  submitLabel: string;
  saving: boolean;
  onSubmit: (values: WorkScheduleFormOutput) => void;
  onCancel: () => void;
}

export function WorkScheduleForm({
  defaultValues,
  submitLabel,
  saving,
  onSubmit,
  onCancel,
}: WorkScheduleFormProps) {
  const form = useForm<WorkScheduleFormValues, any, WorkScheduleFormOutput>({
    resolver: zodResolver(workScheduleSchema) as any,
    defaultValues,
  });

  const hasFlexStart = !!form.watch('hasFlexibleStartTime');
  const hasFlexEnd = !!form.watch('hasFlexibleEndTime');
  const hasCore = !!form.watch('hasCoreHours');
  const hasBreak = !!form.watch('hasMandatoryBreak');
  const allowsOvertime = !!form.watch('allowsOvertime');
  const dayError = (form.formState.errors as any)?.worksMonday?.message as string | undefined;

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Schedule</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <TextField form={form} name="scheduleName" label="Schedule name" required />
            <SelectField
              form={form}
              name="type"
              label="Type"
              required
              options={WORK_SCHEDULE_TYPE_OPTIONS}
            />
          </FieldRow>
          <TextareaField form={form} name="description" label="Description" />
          <FieldRow>
            <SwitchField
              form={form}
              name="isDefault"
              label="Default schedule"
              description="Used for employees with no explicit assignment."
            />
            <SwitchField form={form} name="isActive" label="Active" />
          </FieldRow>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Standard hours</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <TimeField form={form} name="standardStartTime" label="Start time" required />
            <TimeField form={form} name="standardEndTime" label="End time" required />
          </FieldRow>
          <FieldRow>
            <NumberField
              form={form}
              name="standardHoursPerDay"
              label="Hours per day"
              step="0.25"
              required
            />
            <NumberField
              form={form}
              name="standardHoursPerWeek"
              label="Hours per week"
              step="0.25"
              required
            />
          </FieldRow>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Working days</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="flex flex-wrap gap-2">
            {WEEKDAYS.map(([name, label]) => {
              const checked = !!form.watch(name);
              return (
                <div
                  key={name}
                  className="flex items-center gap-2 rounded-md border px-3 py-2"
                >
                  <Switch
                    id={name}
                    checked={checked}
                    onCheckedChange={(next) =>
                      form.setValue(name, next, { shouldValidate: true })
                    }
                  />
                  <Label htmlFor={name} className="cursor-pointer">
                    {label}
                  </Label>
                </div>
              );
            })}
          </div>
          {dayError && <p className="text-sm text-red-500">{dayError}</p>}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Flexible &amp; core hours</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <SwitchField
            form={form}
            name="hasFlexibleStartTime"
            label="Flexible start"
            description="Employees may clock in within a window."
          />
          {hasFlexStart && (
            <FieldRow>
              <TimeField form={form} name="flexibleStartTimeEarliest" label="Earliest start" />
              <TimeField form={form} name="flexibleStartTimeLatest" label="Latest start" />
            </FieldRow>
          )}

          <SwitchField form={form} name="hasFlexibleEndTime" label="Flexible end" />
          {hasFlexEnd && (
            <FieldRow>
              <TimeField form={form} name="flexibleEndTimeEarliest" label="Earliest end" />
              <TimeField form={form} name="flexibleEndTimeLatest" label="Latest end" />
            </FieldRow>
          )}

          <SwitchField
            form={form}
            name="hasCoreHours"
            label="Core hours"
            description="A window everyone must be present for, regardless of flexi-time."
          />
          {hasCore && (
            <FieldRow>
              <TimeField form={form} name="coreHoursStart" label="Core hours start" />
              <TimeField form={form} name="coreHoursEnd" label="Core hours end" />
            </FieldRow>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Breaks</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <SwitchField form={form} name="hasMandatoryBreak" label="Mandatory break" />
          {hasBreak && (
            <FieldRow>
              <NumberField
                form={form}
                name="breakDurationMinutes"
                label="Break length (minutes)"
              />
              <SwitchField
                form={form}
                name="isBreakPaid"
                label="Paid break"
                description="Counts toward worked hours."
              />
            </FieldRow>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Overtime &amp; grace periods</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <SwitchField form={form} name="allowsOvertime" label="Allows overtime" />
          {allowsOvertime && (
            <>
              <SwitchField
                form={form}
                name="overtimeRequiresPreApproval"
                label="Requires pre-approval"
                description="Overtime must be approved before it is worked."
              />
              <FieldRow>
                <NumberField
                  form={form}
                  name="maxOvertimeHoursPerDay"
                  label="Max overtime / day"
                  step="0.25"
                />
                <NumberField
                  form={form}
                  name="maxOvertimeHoursPerWeek"
                  label="Max overtime / week"
                  step="0.25"
                />
              </FieldRow>
            </>
          )}
          <FieldRow>
            <NumberField
              form={form}
              name="lateGracePeriodMinutes"
              label="Late grace (minutes)"
            />
            <NumberField
              form={form}
              name="earlyDepartureGracePeriodMinutes"
              label="Early departure grace (minutes)"
            />
          </FieldRow>
        </CardContent>
      </Card>

      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" onClick={onCancel} disabled={saving}>
          Cancel
        </Button>
        <Button type="submit" disabled={saving}>
          {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
          {submitLabel}
        </Button>
      </div>
    </form>
  );
}
