'use client';

import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { Label } from '@/components/ui/label';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { employeeRelieverService } from '@/services/hr/employee-reliever.service';
import type { EmployeeReliever } from '@/types/hr/employee-reliever';
import { EmployeeSubResourceTab } from './EmployeeSubResourceTab';
import { FieldRow, NumberField, SwitchField } from './fields';

const schema = z.object({
  relieverEmployeeId: z.string().min(1, 'Choose who covers for this employee'),
  // Ordinal, matching the server's Range(1, 99). Priority 0 and -1 were accepted before slice 7,
  // and the leave form reads this roster in priority order — a zero silently jumps the queue.
  priority: z.coerce.number().int('Must be a whole number').min(1, 'Priority starts at 1').max(99),
  isActive: z.boolean(),
});

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  relieverEmployeeId: '',
  priority: 1,
  isActive: true,
};

const PRIORITY_LABEL: Record<number, string> = {
  1: 'Primary',
  2: 'Backup',
};

/**
 * Who covers for this employee while they are away.
 *
 * The order is the point: the leave request form fills its first reliever slot from priority 1 and
 * its second from priority 2, which is what `LeaveRequest.RelieverEmployeeId` and
 * `SecondRelieverEmployeeId` have said in their entity comments since the port — a promise nothing
 * kept until slice 7, because nothing read this table at all.
 */
export function RelieversTab({ employeeId }: { employeeId: string }) {
  return (
    <EmployeeSubResourceTab<EmployeeReliever, FormValues>
      employeeId={employeeId}
      title="relievers"
      singular="reliever"
      queryKey="relievers"
      getId={(r) => r.id}
      list={(id) => employeeRelieverService.getForEmployee(id)}
      create={(id, v) =>
        employeeRelieverService.create({
          employeeId: id,
          relieverEmployeeId: v.relieverEmployeeId,
          priority: v.priority,
          isActive: v.isActive,
        })
      }
      update={(_id, relieverId, v) =>
        employeeRelieverService.update(relieverId, {
          relieverEmployeeId: v.relieverEmployeeId,
          priority: v.priority,
          isActive: v.isActive,
        })
      }
      remove={(_id, relieverId) => employeeRelieverService.remove(relieverId)}
      columns={[
        {
          header: 'Order',
          cell: (r) => (
            <div className="flex items-center gap-2">
              <span className="font-medium">{r.priority}</span>
              {PRIORITY_LABEL[r.priority] && (
                <Badge variant="secondary">{PRIORITY_LABEL[r.priority]}</Badge>
              )}
            </div>
          ),
        },
        { header: 'Reliever', cell: (r) => r.relieverName },
        { header: 'Position', cell: (r) => r.relieverPositionName || '—' },
        { header: 'Unit', cell: (r) => r.relieverOrganizationUnitName || '—' },
        {
          header: 'Status',
          cell: (r) =>
            r.isActive ? (
              <Badge variant="outline">Active</Badge>
            ) : (
              // An inactive row stays on the roster but is skipped when the leave form seeds its
              // slots — which is the difference between "not my reliever any more" and "delete the
              // record that they ever were".
              <Badge variant="outline">Not in use</Badge>
            ),
        },
      ]}
      schema={schema}
      emptyForm={empty}
      toForm={(r) => ({
        relieverEmployeeId: r.relieverEmployeeId,
        priority: r.priority,
        isActive: r.isActive,
      })}
      renderFields={(form) => (
        <>
          <div className="space-y-2">
            <Label>Reliever</Label>
            <EmployeePicker
              value={form.watch('relieverEmployeeId') || null}
              onChange={(id) =>
                form.setValue('relieverEmployeeId', id ?? '', { shouldValidate: true })
              }
            />
            {form.formState.errors.relieverEmployeeId && (
              <p className="text-sm text-red-500">
                {form.formState.errors.relieverEmployeeId.message}
              </p>
            )}
            <p className="text-xs text-muted-foreground">
              Must still be with the organisation, and cannot be this employee. One person holds one
              slot on a roster.
            </p>
          </div>
          <FieldRow>
            <NumberField form={form} name="priority" label="Priority" required />
            <SwitchField form={form} name="isActive" label="In use" />
          </FieldRow>
          <p className="text-xs text-muted-foreground">
            Priority 1 is the primary reliever and seeds the first reliever slot on a leave request;
            priority 2 seeds the second. The leave request can always be changed afterwards.
          </p>
        </>
      )}
    />
  );
}
