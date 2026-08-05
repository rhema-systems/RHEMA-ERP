'use client';

import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Wallet } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { NumberField, SelectField, SwitchField } from '@/components/hr/employee/tabs/fields';
import { payComponentService, emolumentService } from '@/services/hr/compensation.service';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { formatMoney, humanizeEnum } from '@/lib/hr/attendance-format';
import type { PositionPayComponent } from '@/types/hr/compensation';

/**
 * Allowances and deductions attached to a position, which everyone holding it inherits.
 *
 * The amount here is optional: leaving it blank falls back to the component's own default, so a
 * position only states an amount where it differs. Employees can then override again at their
 * own level — see the employee emoluments screen.
 */
const assignmentSchema = z.object({
  payComponentId: z.string().min(1, 'Select a component'),
  amount: z.union([z.coerce.number().min(0), z.literal('')]).optional(),
  isActive: z.boolean(),
});

type AssignmentForm = z.input<typeof assignmentSchema>;

const emptyAssignment: AssignmentForm = {
  payComponentId: '',
  amount: '',
  isActive: true,
};

export default function PositionEmolumentsPage() {
  const [positionId, setPositionId] = useState('');

  const { data: positions } = useQuery({
    queryKey: ['hr', 'positions', 'all'],
    queryFn: () => employeePositionService.getAll(),
  });

  const { data: components } = useQuery({
    queryKey: ['hr', 'pay-components', true],
    queryFn: () => payComponentService.getAll(true),
  });

  const positionOptions = useMemo(
    () => (positions ?? []).map((p) => ({ value: p.id, label: p.title })),
    [positions],
  );

  const componentOptions = useMemo(
    () =>
      (components ?? []).map((c) => ({
        value: c.id,
        label:
          `${c.code} · ${c.name} (${humanizeEnum(c.componentType)})` +
          (c.defaultAmount == null
            ? ''
            : c.calculationBasis === 'PercentageOfBasic'
              ? ` — ${c.defaultAmount}% of basic`
              : ` — ${formatMoney(c.defaultAmount)}`),
      })),
    [components],
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Position Emoluments"
        description="Allowances and deductions every holder of a position inherits."
        backHref="/administration/hr/compensation"
      />

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">Position</CardTitle>
        </CardHeader>
        <CardContent>
          <Select value={positionId} onValueChange={setPositionId}>
            <SelectTrigger className="max-w-md">
              <SelectValue placeholder="Select a position…" />
            </SelectTrigger>
            <SelectContent>
              {positionOptions.map((o) => (
                <SelectItem key={o.value} value={o.value}>
                  {o.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </CardContent>
      </Card>

      {!positionId ? (
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={Wallet}
              title="Choose a position"
              description="Emolument components are attached per position."
            />
          </CardContent>
        </Card>
      ) : (
        <ResourceCollectionTab<PositionPayComponent, AssignmentForm>
          parentId={positionId}
          title="components"
          singular="component"
          queryKey={['hr', 'position-emoluments', positionId]}
          dialogHint="Leave the amount blank to use the component's own default."
          emptyDescription="This position inherits no allowances or deductions yet."
          list={(pid) => emolumentService.getPositionComponents(pid)}
          create={(pid, values) => {
            const v = assignmentSchema.parse(values);
            return emolumentService.assignPositionComponent({
              positionId: pid,
              payComponentId: v.payComponentId,
              amount: v.amount === '' || v.amount === undefined ? null : Number(v.amount),
            });
          }}
          update={(_pid, id, values) => {
            const v = assignmentSchema.parse(values);
            // The component itself cannot be swapped — remove the row and add the right one.
            return emolumentService.updatePositionComponent(id, {
              amount: v.amount === '' || v.amount === undefined ? null : Number(v.amount),
              isActive: v.isActive,
            });
          }}
          remove={(_pid, id) => emolumentService.removePositionComponent(id)}
          getId={(r) => r.id}
          columns={[
            {
              header: 'Component',
              cell: (r) => <span className="font-medium">{r.payComponentName}</span>,
            },
            { header: 'Type', cell: (r) => humanizeEnum(r.componentType) },
            {
              header: 'Amount',
              cell: (r) =>
                r.amount == null ? (
                  <span className="text-muted-foreground">
                    default{r.defaultAmount == null ? '' : ` (${formatMoney(r.defaultAmount)})`}
                  </span>
                ) : (
                  formatMoney(r.amount)
                ),
              className: 'text-right',
            },
            { header: 'Status', cell: (r) => <StatusBadge active={r.isActive} /> },
          ]}
          schema={assignmentSchema as any}
          emptyForm={emptyAssignment}
          toForm={(r) => ({
            payComponentId: r.payComponentId,
            amount: r.amount ?? '',
            isActive: r.isActive,
          })}
          renderFields={(form) => (
            <>
              <SelectField
                form={form}
                name="payComponentId"
                label="Pay component"
                required
                options={componentOptions}
                placeholder={
                  componentOptions.length ? 'Select a component…' : 'No active pay components'
                }
              />
              <NumberField
                form={form}
                name="amount"
                label="Amount for this position"
                step="0.01"
                placeholder="Leave blank to use the component default"
              />
              <SwitchField form={form} name="isActive" label="Active" />
            </>
          )}
        />
      )}
    </div>
  );
}
