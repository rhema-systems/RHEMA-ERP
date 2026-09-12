'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import {
  TextField,
  NumberField,
  DateField,
  TextareaField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import type { MentoringProgramCreate } from '@/types/hr/mentoring';

export const mentoringProgramSchema = z
  .object({
    programName: z.string().min(1, 'Required').max(200),
    description: z.string().max(2000).optional().or(z.literal('')),
    objectives: z.string().max(2000).optional().or(z.literal('')),
    startDate: z.string().min(1, 'Required'),
    endDate: z.string().optional().or(z.literal('')),
    sessionsPerMonth: z.string().optional().or(z.literal('')),
    minutesPerSession: z.string().optional().or(z.literal('')),
    isActive: z.boolean(),
    coordinatedById: z.string().optional().or(z.literal('')),
  })
  .refine((v) => !v.endDate || new Date(v.endDate) >= new Date(v.startDate), {
    message: 'A programme cannot end before it starts.',
    path: ['endDate'],
  })
  // The server's own bounds, mirrored so the user hears about it before the round trip.
  .refine((v) => !v.sessionsPerMonth || Number(v.sessionsPerMonth) >= 1, {
    message: 'At least one session a month, or leave it unset.',
    path: ['sessionsPerMonth'],
  })
  .refine(
    (v) =>
      !v.minutesPerSession ||
      (Number(v.minutesPerSession) >= 15 && Number(v.minutesPerSession) <= 480),
    { message: 'Between 15 minutes and 8 hours.', path: ['minutesPerSession'] },
  );

export type MentoringProgramFormValues = z.infer<typeof mentoringProgramSchema>;

export const emptyMentoringProgram: MentoringProgramFormValues = {
  programName: '',
  description: '',
  objectives: '',
  startDate: new Date().toISOString().slice(0, 10),
  endDate: '',
  sessionsPerMonth: '2',
  minutesPerSession: '60',
  isActive: true,
  coordinatedById: '',
};

const num = (v?: string) => (v && v.trim() !== '' ? Number(v) : null);
const str = (v?: string) => (v && v.trim() !== '' ? v : null);

export function toMentoringProgramRequest(v: MentoringProgramFormValues): MentoringProgramCreate {
  return {
    programName: v.programName,
    description: str(v.description),
    objectives: str(v.objectives),
    startDate: new Date(v.startDate).toISOString(),
    endDate: v.endDate ? new Date(v.endDate).toISOString() : null,
    sessionsPerMonth: num(v.sessionsPerMonth),
    minutesPerSession: num(v.minutesPerSession),
    isActive: v.isActive,
    coordinatedById: str(v.coordinatedById),
  };
}

interface Props {
  defaultValues: MentoringProgramFormValues;
  onSubmit: (values: MentoringProgramFormValues) => void | Promise<void>;
  submitting?: boolean;
  submitLabel?: string;
  onCancel?: () => void;
}

export function MentoringProgramForm({
  defaultValues,
  onSubmit,
  submitting,
  submitLabel = 'Save',
  onCancel,
}: Props) {
  const form = useForm<MentoringProgramFormValues>({
    resolver: zodResolver(mentoringProgramSchema),
    defaultValues,
  });

  return (
    <form onSubmit={form.handleSubmit(onSubmit)}>
      <Card>
        <CardHeader>
          <CardTitle>Programme</CardTitle>
          <CardDescription>
            The scheme itself. Pairs are created inside it once it exists.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <TextField form={form} name="programName" label="Programme name" required />
          <TextareaField form={form} name="description" label="Description" rows={3} />
          <div className="space-y-1">
            <TextareaField form={form} name="objectives" label="Objectives" rows={3} />
            <p className="text-xs text-muted-foreground">
              What the scheme is meant to achieve — shown to mentors and mentees.
            </p>
          </div>

          <FieldRow>
            <DateField form={form} name="startDate" label="Start date" required />
            <DateField form={form} name="endDate" label="End date" />
          </FieldRow>

          <div className="space-y-1">
            <FieldRow>
              <NumberField
                form={form}
                name="sessionsPerMonth"
                label="Expected sessions per month"
              />
              <NumberField
                form={form}
                name="minutesPerSession"
                label="Expected minutes per session"
              />
            </FieldRow>
            <p className="text-xs text-muted-foreground">
              Guidance for pairs, not a limit the system enforces.
            </p>
          </div>

          <div className="space-y-1">
            <EmployeePickerField form={form} name="coordinatedById" label="Coordinator" />
            <p className="text-xs text-muted-foreground">
              Can see every pair in this programme — but not anyone&apos;s private notes or ratings,
              which stay with the person who wrote them.
            </p>
          </div>

          <SwitchField
            form={form}
            name="isActive"
            label="Active"
            description="Inactive programmes stay readable but are kept out of the pickers."
          />
        </CardContent>
        <CardFooter className="justify-end gap-2">
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
        </CardFooter>
      </Card>
    </form>
  );
}
