'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2 } from 'lucide-react';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  TextField,
  NumberField,
  TimeField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import {
  WorkScheduleForm,
  toWorkScheduleForm,
  normalizeWorkSchedule,
  type WorkScheduleFormOutput,
} from '@/components/hr/attendance/WorkScheduleForm';
import { workScheduleService } from '@/services/hr/attendance-setup.service';
import { formatTime } from '@/lib/hr/attendance-format';
import { SHIFT_TYPE_OPTIONS } from '@/types/hr/attendance';
import type { ShiftDefinitionSummary } from '@/types/hr/attendance';

const shiftSchema = z.object({
  shiftName: z.string().min(1, 'A name is required').max(100),
  description: z.string().max(500).optional(),
  type: z.enum(['Morning', 'Afternoon', 'Evening', 'Night', 'Rotating', 'Split']),
  startTime: z.string().min(1, 'Required'),
  endTime: z.string().min(1, 'Required'),
  shiftHours: z.coerce.number().min(0.5).max(24),
  isNightShift: z.boolean(),
  attractsNightAllowance: z.boolean(),
  allowsOvertime: z.boolean(),
  hasShiftDifferential: z.boolean(),
  shiftDifferentialPercentage: z.coerce.number().min(0).max(100).optional(),
  displayOrder: z.coerce.number().min(0),
  isActive: z.boolean(),
});

type ShiftForm = z.input<typeof shiftSchema>;

const emptyShift: ShiftForm = {
  shiftName: '',
  description: '',
  type: 'Morning',
  startTime: '08:00:00',
  endTime: '16:00:00',
  shiftHours: 8,
  isNightShift: false,
  attractsNightAllowance: false,
  allowsOvertime: false,
  hasShiftDifferential: false,
  shiftDifferentialPercentage: undefined,
  displayOrder: 0,
  isActive: true,
};

export default function EditWorkSchedulePage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [saving, setSaving] = useState(false);

  const { data: schedule, isLoading, isError } = useQuery({
    queryKey: ['hr', 'work-schedules', id],
    queryFn: () => workScheduleService.getById(id),
    enabled: !!id,
  });

  const handleSubmit = async (values: WorkScheduleFormOutput) => {
    setSaving(true);
    try {
      await workScheduleService.update(id, { id, ...normalizeWorkSchedule(values) } as any);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'work-schedules'] });
      toast({ title: 'Saved', description: 'Work schedule updated.' });
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update work schedule.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !schedule) {
    return (
      <div className="p-6">
        <EmptyState title="Work schedule not found" description="It may have been removed." />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={schedule.scheduleName}
        description={`${schedule.type} schedule · ${formatTime(schedule.standardStartTime)} – ${formatTime(schedule.standardEndTime)}`}
        backHref="/administration/hr/attendance/work-schedules"
        actions={<StatusBadge active={schedule.isActive} />}
      />

      <Tabs defaultValue="settings">
        <TabsList>
          <TabsTrigger value="settings">Settings</TabsTrigger>
          <TabsTrigger value="shifts">Shifts</TabsTrigger>
        </TabsList>

        <TabsContent value="settings" className="pt-4">
          <WorkScheduleForm
            defaultValues={toWorkScheduleForm(schedule)}
            submitLabel="Save changes"
            saving={saving}
            onSubmit={handleSubmit}
            onCancel={() => router.push('/administration/hr/attendance/work-schedules')}
          />
        </TabsContent>

        <TabsContent value="shifts" className="pt-4">
          <ResourceCollectionTab<ShiftDefinitionSummary, ShiftForm>
            parentId={id}
            title="shifts"
            singular="shift"
            queryKey={['hr', 'work-schedules', id, 'shifts']}
            invalidateKeys={[['hr', 'work-schedules']]}
            dialogHint="Shifts let one schedule cover several working patterns, e.g. a day and a night rota."
            emptyDescription="A schedule without shifts simply uses its standard hours."
            list={(scheduleId) => workScheduleService.getShifts(scheduleId)}
            create={(scheduleId, values) =>
              workScheduleService.addShift(scheduleId, {
                ...shiftSchema.parse(values),
                workScheduleId: scheduleId,
                description: values.description || null,
              } as any)
            }
            update={(_scheduleId, shiftId, values) =>
              workScheduleService.updateShift(shiftId, {
                ...shiftSchema.parse(values),
                id: shiftId,
                description: values.description || null,
              } as any)
            }
            remove={(_scheduleId, shiftId) => workScheduleService.removeShift(shiftId)}
            getId={(s) => s.id}
            columns={[
              { header: 'Shift', cell: (s) => <span className="font-medium">{s.shiftName}</span> },
              { header: 'Type', cell: (s) => s.type },
              { header: 'Hours', cell: (s) => `${formatTime(s.startTime)} – ${formatTime(s.endTime)}` },
              { header: 'Length', cell: (s) => `${s.shiftHours}h`, className: 'text-right' },
              { header: 'Night', cell: (s) => (s.isNightShift ? 'Yes' : 'No') },
              { header: 'Status', cell: (s) => <StatusBadge active={s.isActive} /> },
            ]}
            schema={shiftSchema as any}
            emptyForm={emptyShift}
            toForm={(s) => ({
              // The list endpoint returns summaries, so the fields it omits fall back to
              // their defaults; the dialog reads as a partial edit for those.
              ...emptyShift,
              shiftName: s.shiftName,
              type: s.type,
              startTime: s.startTime,
              endTime: s.endTime,
              shiftHours: s.shiftHours,
              isNightShift: s.isNightShift,
              displayOrder: s.displayOrder,
              isActive: s.isActive,
            })}
            renderFields={(form) => (
              <>
                <FieldRow>
                  <TextField form={form} name="shiftName" label="Shift name" required />
                  <SelectField
                    form={form}
                    name="type"
                    label="Type"
                    required
                    options={SHIFT_TYPE_OPTIONS}
                  />
                </FieldRow>
                <TextareaField form={form} name="description" label="Description" rows={2} />
                <FieldRow>
                  <TimeField form={form} name="startTime" label="Start time" required />
                  <TimeField form={form} name="endTime" label="End time" required />
                </FieldRow>
                <FieldRow>
                  <NumberField form={form} name="shiftHours" label="Shift length (hours)" step="0.25" required />
                  <NumberField form={form} name="displayOrder" label="Display order" />
                </FieldRow>
                <SwitchField
                  form={form}
                  name="isNightShift"
                  label="Night shift"
                  description="Crosses midnight or falls in night hours."
                />
                <SwitchField form={form} name="attractsNightAllowance" label="Attracts night allowance" />
                <SwitchField form={form} name="allowsOvertime" label="Allows overtime" />
                <SwitchField form={form} name="hasShiftDifferential" label="Has shift differential" />
                {!!form.watch('hasShiftDifferential') && (
                  <NumberField
                    form={form}
                    name="shiftDifferentialPercentage"
                    label="Differential (%)"
                    step="0.01"
                  />
                )}
                <SwitchField form={form} name="isActive" label="Active" />
              </>
            )}
          />
        </TabsContent>
      </Tabs>
    </div>
  );
}
