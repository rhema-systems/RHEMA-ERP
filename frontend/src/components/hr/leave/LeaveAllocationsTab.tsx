'use client';

import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { useLeavePermissions } from '@/components/hr/leave/use-leave-permissions';
import { useLeaveYear } from '@/components/hr/leave/use-leave-year';
import { leaveTypeService } from '@/services/hr/leave-type.service';
import { staffLevelService } from '@/services/hr/staff-level.service';
import type { LeaveCategoryAllocation } from '@/types/hr/leave';
import { DateField, FieldRow, NumberField, SelectField } from '@/components/hr/employee/tabs/fields';

const schema = z
  .object({
    staffLevelId: z.string().min(1, 'Staff level is required'),
    allocationDays: z.coerce.number().int('Whole days only').min(0, 'Cannot be negative'),
    effectiveFrom: z.string().min(1, 'Effective date is required'),
    effectiveTo: z.string().optional().or(z.literal('')),
  })
  .refine((v) => !v.effectiveTo || v.effectiveTo >= v.effectiveFrom, {
    message: 'End date cannot be before the start date',
    path: ['effectiveTo'],
  });

type FormValues = z.infer<typeof schema>;

// `effectiveFrom` is filled in by the tab: the current leave year's first day (L-89).
const empty: FormValues = {
  staffLevelId: '',
  allocationDays: 0,
  effectiveFrom: '',
  effectiveTo: '',
};

/**
 * The saved toast (leave settings audit 2, L-89). The save re-works this type's balances for the
 * current leave year, and says so — a balance that moves with nothing saying why is the thing the
 * repair screen was built to prevent.
 */
const describeSave = (saved: unknown, editing: boolean) => {
  const moved = (saved as LeaveCategoryAllocation | undefined)?.balancesUpdated;
  const done = `Allocation ${editing ? 'updated' : 'added'}.`;
  if (moved == null)
    return `${done} It is not in force in the current leave year, so no balance was changed. A past year's balances are put right with Repair entitlements, on the balances screen.`;
  if (moved === 0) return `${done} The current leave year's balances already matched it.`;
  return `${done} ${moved} balance(s) in the current leave year now carry the new entitlement.`;
};

const toPayload = (leaveTypeId: string, v: FormValues) => ({
  leaveTypeId,
  staffLevelId: v.staffLevelId,
  allocationDays: v.allocationDays,
  effectiveFrom: v.effectiveFrom,
  effectiveTo: v.effectiveTo || null,
});

/**
 * How many days each staff level gets, time-bounded. For the whole leave type: a balance is kept
 * per type, so an allocation to one sub-type matched almost nothing and the server no longer keeps
 * one (round 5, lane N2).
 */
export function LeaveAllocationsTab({ leaveTypeId }: { leaveTypeId: string }) {
  const { canAdminister } = useLeavePermissions();
  // ⚠ L-89: "from" defaulted to today, which read as "from today" — but an allocation counts for
  // the whole leave year its dates touch, so the honest default is that year's first day.
  const leaveYear = useLeaveYear();

  const { data: staffLevels } = useQuery({
    queryKey: ['hr', 'staff-levels', 'active'],
    queryFn: () => staffLevelService.getActive(),
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
      savedDescription={describeSave}
      getId={(a) => a.id}
      list={leaveTypeService.getAllocations.bind(leaveTypeService)}
      create={(id, v) => leaveTypeService.createAllocation(toPayload(id, v))}
      update={(id, allocationId, v) =>
        leaveTypeService.updateAllocation(allocationId, toPayload(id, v))
      }
      allowRemove={canAdminister}
      remove={(_id, allocationId) => leaveTypeService.removeAllocation(allocationId)}
      columns={[
        { header: 'Staff level', cell: (a) => a.staffLevelName || '—' },
        { header: 'Days', cell: (a) => a.allocationDays },
        { header: 'From', cell: (a) => a.effectiveFrom?.slice(0, 10) || '—' },
        { header: 'To', cell: (a) => a.effectiveTo?.slice(0, 10) || 'Open-ended' },
      ]}
      schema={schema}
      emptyForm={{ ...empty, effectiveFrom: leaveYear.startDate }}
      toForm={(a) => ({
        staffLevelId: a.staffLevelId,
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
          <NumberField form={form} name="allocationDays" label="Allocation days" required />
          <FieldRow>
            <DateField
              form={form}
              name="effectiveFrom"
              label="Effective from (its whole leave year)"
              required
            />
            <DateField form={form} name="effectiveTo" label="Effective to (its whole leave year)" />
          </FieldRow>
          {/* L-89: the dates pick leave years; the engine never splits a year at them. */}
          <p className="text-sm text-muted-foreground">
            <strong>The dates choose leave years, not days.</strong> An allocation gives its full
            figure for every leave year its dates touch — it is not split at the date. When two for
            one staff level touch the same year, the one that starts later wins that year.
          </p>
          <p className="text-sm text-muted-foreground">
            Saving re-works this leave type&apos;s balances for the current leave year (
            {leaveYear.startDate} to {leaveYear.endDate}). A past year is put right with{' '}
            <strong>Repair entitlements</strong> on the balances screen, which shows every change
            before making it.
          </p>
        </>
      )}
    />
  );
}
