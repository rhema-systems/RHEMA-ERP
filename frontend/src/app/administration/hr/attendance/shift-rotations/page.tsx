'use client';

import { useRouter } from 'next/navigation';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  TextField,
  NumberField,
  DateField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { shiftRotationPlanService } from '@/services/hr/attendance-setup.service';
import { formatDate } from '@/lib/hr/attendance-format';
import { SHIFT_ROTATION_CYCLE_OPTIONS } from '@/types/hr/attendance';
import type { ShiftRotationPlanSummary } from '@/types/hr/attendance';

/**
 * Rotation plans cycle members through an ordered sequence of shifts. The stages and the
 * enrolled members live on each plan's own page — a plan on its own does nothing.
 *
 * The cycle and start date are fixed once created: members carry a current stage that is
 * only meaningful relative to the original sequence, so changing either mid-flight would
 * silently move everyone.
 */
const planSchema = z
  .object({
    planName: z.string().min(1, 'A name is required').max(150),
    description: z.string().max(1000).optional(),
    rotationCycle: z.enum(['Weekly', 'Fortnightly', 'Monthly', 'Quarterly']),
    cycleLengthDays: z.coerce.number().min(1).max(365),
    startDate: z.string().min(1, 'Required'),
    endDate: z.string().optional(),
    isActive: z.boolean(),
    notes: z.string().max(1000).optional(),
  })
  .refine((v) => !v.endDate || v.endDate >= v.startDate, {
    message: 'The end date cannot be before the start date',
    path: ['endDate'],
  });

type PlanForm = z.input<typeof planSchema>;

const emptyPlan: PlanForm = {
  planName: '',
  description: '',
  rotationCycle: 'Weekly',
  cycleLengthDays: 7,
  startDate: '',
  endDate: '',
  isActive: true,
  notes: '',
};

export default function ShiftRotationsPage() {
  const router = useRouter();

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Shift Rotation Plans"
        description="Cycles that move teams through a sequence of shifts on a fixed rhythm."
        backHref="/administration/hr/attendance"
      />

      <ResourceListPanel<ShiftRotationPlanSummary, PlanForm>
        title="rotation plans"
        singular="plan"
        queryKey={['hr', 'shift-rotation-plans']}
        dialogHint="Add the stages and members on the plan's own page once it exists."
        list={() => shiftRotationPlanService.getAll()}
        create={(values) => {
          const v = planSchema.parse(values);
          return shiftRotationPlanService.create({
            ...v,
            description: v.description || null,
            endDate: v.endDate || null,
            notes: v.notes || null,
          });
        }}
        update={(id, values) => {
          const v = planSchema.parse(values);
          // Cycle, length and start date are omitted — the update DTO does not accept them.
          return shiftRotationPlanService.update(id, {
            id,
            planName: v.planName,
            description: v.description || null,
            endDate: v.endDate || null,
            isActive: v.isActive,
            notes: v.notes || null,
          });
        }}
        remove={(id) => shiftRotationPlanService.remove(id)}
        getId={(p) => p.id}
        actions={[
          {
            label: 'Stages & members',
            run: async (p) => {
              router.push(`/administration/hr/attendance/shift-rotations/${p.id}`);
            },
          },
        ]}
        columns={[
          { header: 'Plan', cell: (p) => <span className="font-medium">{p.planName}</span> },
          { header: 'Cycle', cell: (p) => p.rotationCycle },
          { header: 'Length', cell: (p) => `${p.cycleLengthDays} d`, className: 'text-right' },
          { header: 'Starts', cell: (p) => formatDate(p.startDate) },
          { header: 'Ends', cell: (p) => formatDate(p.endDate) },
          { header: 'Stages', cell: (p) => p.stageCount, className: 'text-right' },
          { header: 'Members', cell: (p) => p.memberCount, className: 'text-right' },
          { header: 'Status', cell: (p) => <StatusBadge active={p.isActive} /> },
          {
            header: '',
            cell: (p) => (
              <Button
                variant="link"
                size="sm"
                className="h-auto p-0"
                onClick={(e) => {
                  e.stopPropagation();
                  router.push(`/administration/hr/attendance/shift-rotations/${p.id}`);
                }}
              >
                Open
              </Button>
            ),
          },
        ]}
        schema={planSchema as any}
        emptyForm={emptyPlan}
        toForm={(p) => ({
          ...emptyPlan,
          planName: p.planName,
          rotationCycle: p.rotationCycle,
          cycleLengthDays: p.cycleLengthDays,
          startDate: p.startDate,
          endDate: p.endDate ?? '',
          isActive: p.isActive,
        })}
        renderFields={(form) => (
          <>
            <TextField form={form} name="planName" label="Plan name" required />
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <FieldRow>
              <SelectField
                form={form}
                name="rotationCycle"
                label="Rotation cycle"
                required
                options={SHIFT_ROTATION_CYCLE_OPTIONS}
              />
              <NumberField
                form={form}
                name="cycleLengthDays"
                label="Cycle length (days)"
                required
              />
            </FieldRow>
            <FieldRow>
              <DateField form={form} name="startDate" label="Start date" required />
              <DateField form={form} name="endDate" label="End date" />
            </FieldRow>
            <SwitchField form={form} name="isActive" label="Active" />
            <TextareaField form={form} name="notes" label="Notes" rows={2} />
          </>
        )}
      />
    </div>
  );
}
