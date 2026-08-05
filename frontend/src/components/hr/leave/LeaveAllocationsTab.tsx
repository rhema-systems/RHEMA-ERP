'use client';

import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { leaveTypeService } from '@/services/hr/leave-type.service';
import { staffLevelService } from '@/services/hr/staff-level.service';
import type { LeaveCategoryAllocation } from '@/types/hr/leave';
import { DateField, FieldRow, NumberField, SelectField } from '@/components/hr/employee/tabs/fields';

const schema = z
  .object({
    staffLevelId: z.string().min(1, 'Staff level is required'),
    leaveSubTypeId: z.string().optional().or(z.literal('')),
    allocationDays: z.coerce.number().int('Whole days only').min(0, 'Cannot be negative'),
    effectiveFrom: z.string().min(1, 'Effective date is required'),
    effectiveTo: z.string().optional().or(z.literal('')),
  })
  .refine((v) => !v.effectiveTo || v.effectiveTo >= v.effectiveFrom, {
    message: 'End date cannot be before the start date',
    path: ['effectiveTo'],
  });

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  staffLevelId: '',
  leaveSubTypeId: '',
  allocationDays: 0,
  effectiveFrom: new Date().toISOString().slice(0, 10),
  effectiveTo: '',
};

const toPayload = (leaveTypeId: string, v: FormValues) => ({
  leaveTypeId,
  leaveSubTypeId: v.leaveSubTypeId || null,
  staffLevelId: v.staffLevelId,
  allocationDays: v.allocationDays,
  effectiveFrom: v.effectiveFrom,
  effectiveTo: v.effectiveTo || null,
});

/** How many days each staff level gets, optionally per sub-type and time-bounded. */
export function LeaveAllocationsTab({ leaveTypeId }: { leaveTypeId: string }) {
  const { data: staffLevels } = useQuery({
    queryKey: ['hr', 'staff-levels', 'active'],
    queryFn: () => staffLevelService.getActive(),
  });

  const { data: subTypes } = useQuery({
    queryKey: ['hr', 'leave-types', leaveTypeId, 'sub-types'],
    queryFn: () => leaveTypeService.getSubTypes(leaveTypeId),
    enabled: !!leaveTypeId,
  });

  return (
    <ResourceCollectionTab<LeaveCategoryAllocation, FormValues>
      parentId={leaveTypeId}
      title="allocations"
      singular="allocation"
      queryKey={['hr', 'leave-types', leaveTypeId, 'allocations']}
      invalidateKeys={[['hr', 'leave-types', leaveTypeId, 'detail']]}
      dialogHint="Set how many days a staff level receives."
      emptyDescription="Allocations set the entitlement per staff level."
      getId={(a) => a.id}
      list={leaveTypeService.getAllocations.bind(leaveTypeService)}
      create={(id, v) => leaveTypeService.createAllocation(toPayload(id, v))}
      update={(id, allocationId, v) =>
        leaveTypeService.updateAllocation(allocationId, toPayload(id, v))
      }
      remove={(_id, allocationId) => leaveTypeService.removeAllocation(allocationId)}
      columns={[
        { header: 'Staff level', cell: (a) => a.staffLevelName || '—' },
        { header: 'Sub-type', cell: (a) => a.leaveSubTypeName || 'All' },
        { header: 'Days', cell: (a) => a.allocationDays },
        { header: 'From', cell: (a) => a.effectiveFrom?.slice(0, 10) || '—' },
        { header: 'To', cell: (a) => a.effectiveTo?.slice(0, 10) || 'Open-ended' },
      ]}
      schema={schema}
      emptyForm={empty}
      toForm={(a) => ({
        staffLevelId: a.staffLevelId,
        leaveSubTypeId: a.leaveSubTypeId ?? '',
        allocationDays: a.allocationDays,
        effectiveFrom: a.effectiveFrom?.slice(0, 10) ?? '',
        effectiveTo: a.effectiveTo?.slice(0, 10) ?? '',
      })}
      renderFields={(form) => (
        <>
          <SelectField
            form={form}
            name="staffLevelId"
            label="Staff level"
            required
            options={(staffLevels ?? []).map((s) => ({ value: s.id, label: s.name }))}
          />
          <SelectField
            form={form}
            name="leaveSubTypeId"
            label="Sub-type"
            options={(subTypes ?? []).map((s) => ({ value: s.id, label: s.subTypeName }))}
            allowEmpty
            emptyLabel="All sub-types"
          />
          <NumberField form={form} name="allocationDays" label="Allocation days" required />
          <FieldRow>
            <DateField form={form} name="effectiveFrom" label="Effective from" required />
            <DateField form={form} name="effectiveTo" label="Effective to" />
          </FieldRow>
        </>
      )}
    />
  );
}
