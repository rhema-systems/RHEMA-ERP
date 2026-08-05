'use client';

import { useParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2 } from 'lucide-react';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  TextField,
  NumberField,
  DateField,
  TextareaField,
  SelectField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import {
  shiftRotationPlanService,
  shiftDefinitionService,
} from '@/services/hr/attendance-setup.service';
import { formatDate, formatTime } from '@/lib/hr/attendance-format';
import type { ShiftRotationStage, ShiftRotationMember } from '@/types/hr/attendance';

/**
 * A rotation plan's stages (the shift sequence) and members (who rides it).
 *
 * The create and update DTOs for both collections disagree on field names — a stage is
 * created with `durationCycles` + `label` but updated with `durationDays` + `notes`, and a
 * member is added with `joinDate` but updated with `currentStageOrder` + `nextRotationDate`.
 * The two dialogs below therefore submit different shapes depending on whether a row is
 * being added or edited; the forms show the union of the fields and each mapper picks out
 * what its endpoint accepts.
 */

const stageSchema = z.object({
  stageOrder: z.coerce.number().min(1).max(100),
  shiftDefinitionId: z.string().min(1, 'Select a shift'),
  durationCycles: z.coerce.number().min(1).max(12),
  label: z.string().max(200).optional(),
});

type StageForm = z.input<typeof stageSchema>;

const emptyStage: StageForm = {
  stageOrder: 1,
  shiftDefinitionId: '',
  durationCycles: 1,
  label: '',
};

const memberSchema = z.object({
  employeeId: z.string().min(1, 'Select an employee'),
  joinDate: z.string().min(1, 'Required'),
  currentStageOrder: z.coerce.number().min(0).max(100),
  notes: z.string().max(500).optional(),
});

type MemberForm = z.input<typeof memberSchema>;

const emptyMember: MemberForm = {
  employeeId: '',
  joinDate: '',
  currentStageOrder: 1,
  notes: '',
};

export default function ShiftRotationPlanPage() {
  const params = useParams();
  const id = (params?.id as string) ?? '';

  const { data: plan, isLoading, isError } = useQuery({
    queryKey: ['hr', 'shift-rotation-plans', id],
    queryFn: () => shiftRotationPlanService.getById(id),
    enabled: !!id,
  });

  const { data: shifts } = useQuery({
    queryKey: ['hr', 'shift-definitions', 'active'],
    queryFn: () => shiftDefinitionService.getActive(),
  });

  const shiftOptions = (shifts ?? []).map((s) => ({
    value: s.id,
    label: `${s.shiftName} (${formatTime(s.startTime)}–${formatTime(s.endTime)})`,
  }));

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !plan) {
    return (
      <div className="p-6">
        <EmptyState title="Rotation plan not found" description="It may have been removed." />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={plan.planName}
        description={`${plan.rotationCycle} rotation · ${plan.cycleLengthDays}-day cycle from ${formatDate(plan.startDate)}`}
        backHref="/administration/hr/attendance/shift-rotations"
        actions={<StatusBadge active={plan.isActive} />}
      />

      <Tabs defaultValue="stages">
        <TabsList>
          <TabsTrigger value="stages">Stages</TabsTrigger>
          <TabsTrigger value="members">Members</TabsTrigger>
        </TabsList>

        <TabsContent value="stages" className="pt-4">
          <ResourceCollectionTab<ShiftRotationStage, StageForm>
            parentId={id}
            title="stages"
            singular="stage"
            queryKey={['hr', 'shift-rotation-plans', id, 'stages']}
            invalidateKeys={[['hr', 'shift-rotation-plans']]}
            dialogHint="Stages run in order; each occupies its shift for the given number of cycles."
            emptyDescription="Add the shifts this rotation moves through, in order."
            list={(planId) => shiftRotationPlanService.getStages(planId)}
            create={(planId, values) =>
              shiftRotationPlanService.addStage(planId, {
                ...stageSchema.parse(values),
                shiftRotationPlanId: planId,
                label: values.label || null,
              })
            }
            update={(_planId, stageId, values) => {
              const v = stageSchema.parse(values);
              // The update DTO speaks durationDays/notes, not durationCycles/label.
              return shiftRotationPlanService.updateStage(stageId, {
                id: stageId,
                stageOrder: v.stageOrder,
                shiftDefinitionId: v.shiftDefinitionId,
                durationDays: v.durationCycles * plan.cycleLengthDays,
                notes: v.label || null,
              });
            }}
            remove={(_planId, stageId) => shiftRotationPlanService.removeStage(stageId)}
            getId={(s) => s.id}
            columns={[
              { header: '#', cell: (s) => s.stageOrder, className: 'w-[60px]' },
              { header: 'Shift', cell: (s) => <span className="font-medium">{s.shiftName}</span> },
              {
                header: 'Hours',
                cell: (s) => `${formatTime(s.shiftStartTime)} – ${formatTime(s.shiftEndTime)}`,
              },
              { header: 'Cycles', cell: (s) => s.durationCycles, className: 'text-right' },
              { header: 'Label', cell: (s) => s.label || '—' },
            ]}
            schema={stageSchema as any}
            emptyForm={emptyStage}
            toForm={(s) => ({
              stageOrder: s.stageOrder,
              shiftDefinitionId: s.shiftDefinitionId,
              durationCycles: s.durationCycles,
              label: s.label ?? '',
            })}
            renderFields={(form) => (
              <>
                <SelectField
                  form={form}
                  name="shiftDefinitionId"
                  label="Shift"
                  required
                  options={shiftOptions}
                  placeholder={shiftOptions.length ? 'Select a shift…' : 'No active shifts defined'}
                />
                <FieldRow>
                  <NumberField form={form} name="stageOrder" label="Order" required />
                  <NumberField form={form} name="durationCycles" label="Duration (cycles)" required />
                </FieldRow>
                <TextField form={form} name="label" label="Label" placeholder="e.g. Days, Nights" />
              </>
            )}
          />
        </TabsContent>

        <TabsContent value="members" className="pt-4">
          <ResourceCollectionTab<ShiftRotationMember, MemberForm>
            parentId={id}
            title="members"
            singular="member"
            queryKey={['hr', 'shift-rotation-plans', id, 'members']}
            invalidateKeys={[['hr', 'shift-rotation-plans']]}
            dialogHint="Members advance one stage each cycle from the point they join."
            emptyDescription="Enrol the employees who ride this rotation."
            list={(planId) => shiftRotationPlanService.getMembers(planId)}
            create={(planId, values) => {
              const v = memberSchema.parse(values);
              return shiftRotationPlanService.enrollMember(planId, {
                shiftRotationPlanId: planId,
                employeeId: v.employeeId,
                organizationUnitId: null,
                teamId: null,
                joinDate: v.joinDate,
                notes: v.notes || null,
              });
            }}
            update={(_planId, memberId, values) => {
              const v = memberSchema.parse(values);
              // Only the stage position and notes are editable after enrolment.
              return shiftRotationPlanService.updateMember(memberId, {
                id: memberId,
                currentStageOrder: v.currentStageOrder,
                nextRotationDate: null,
                notes: v.notes || null,
              });
            }}
            remove={(_planId, memberId) => shiftRotationPlanService.removeMember(memberId)}
            getId={(m) => m.id}
            columns={[
              {
                header: 'Member',
                cell: (m) => (
                  <span className="font-medium">
                    {m.employeeName || m.organizationUnitName || m.teamName || '—'}
                  </span>
                ),
              },
              {
                header: 'Employee no.',
                cell: (m) => <span className="text-muted-foreground">{m.employeeNumber || '—'}</span>,
              },
              { header: 'Stage', cell: (m) => m.currentStageOrder, className: 'text-right' },
              { header: 'Joined', cell: (m) => formatDate(m.joinDate) },
              { header: 'Exited', cell: (m) => formatDate(m.exitDate) },
            ]}
            schema={memberSchema as any}
            emptyForm={emptyMember}
            toForm={(m) => ({
              employeeId: m.employeeId ?? '',
              joinDate: m.joinDate,
              currentStageOrder: m.currentStageOrder,
              notes: m.notes ?? '',
            })}
            renderFields={(form) => (
              <>
                <EmployeePickerField
                  form={form}
                  name="employeeId"
                  label="Employee"
                  required
                />
                <FieldRow>
                  <DateField form={form} name="joinDate" label="Join date" required />
                  <NumberField form={form} name="currentStageOrder" label="Current stage" />
                </FieldRow>
                <TextareaField form={form} name="notes" label="Notes" rows={2} />
              </>
            )}
          />
        </TabsContent>
      </Tabs>
    </div>
  );
}
