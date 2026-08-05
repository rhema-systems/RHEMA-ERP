'use client';

import { useState } from 'react';
import { z } from 'zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  TextField,
  DateField,
  TextareaField,
  SelectField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { payPeriodService } from '@/services/hr/attendance-setup.service';
import { formatDate } from '@/lib/hr/attendance-format';
import { PAY_PERIOD_TYPE_OPTIONS } from '@/types/hr/attendance';
import type { PayPeriodSummary } from '@/types/hr/attendance';

/**
 * Pay periods are the cut-off windows that monthly attendance summaries roll into before
 * payroll exports them. Closing one locks it; the type cannot be changed after creation.
 */
const periodSchema = z
  .object({
    periodName: z.string().min(1, 'A name is required').max(100),
    type: z.enum(['Weekly', 'Biweekly', 'SemiMonthly', 'Monthly']),
    startDate: z.string().min(1, 'Required'),
    endDate: z.string().min(1, 'Required'),
    notes: z.string().max(1000).optional(),
  })
  .refine((v) => v.endDate >= v.startDate, {
    message: 'The end date cannot be before the start date',
    path: ['endDate'],
  });

type PeriodForm = z.input<typeof periodSchema>;

const emptyPeriod: PeriodForm = {
  periodName: '',
  type: 'Monthly',
  startDate: '',
  endDate: '',
  notes: '',
};

export default function PayPeriodsPage() {
  const queryClient = useQueryClient();
  const [page] = useState(1);

  const { data: current, isLoading: loadingCurrent } = useQuery({
    queryKey: ['hr', 'pay-periods', 'current'],
    queryFn: () => payPeriodService.getCurrentOpen(),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Pay Periods"
        description="Attendance cut-off windows that monthly summaries and payroll exports are grouped by."
        backHref="/administration/hr/attendance"
      />

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">Current open period</CardTitle>
        </CardHeader>
        <CardContent>
          {loadingCurrent ? (
            <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
          ) : current ? (
            <div className="flex flex-wrap items-center gap-3 text-sm">
              <span className="font-medium">{current.periodName}</span>
              <span className="text-muted-foreground">
                {formatDate(current.startDate)} – {formatDate(current.endDate)}
              </span>
              <StatusBadge status={current.status} />
              <span className="text-muted-foreground">
                {current.summaryCount} summaries · {current.exportCount} exports
              </span>
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">
              No period is currently open. Create one before recording attendance for a new cycle.
            </p>
          )}
        </CardContent>
      </Card>

      <ResourceListPanel<PayPeriodSummary, PeriodForm>
        title="pay periods"
        singular="pay period"
        queryKey={['hr', 'pay-periods', 'list', page]}
        invalidateKeys={[['hr', 'pay-periods', 'current']]}
        dialogHint="Periods should not overlap — attendance is matched to exactly one."
        list={async () => (await payPeriodService.getPaged(page, 100)).items}
        create={(values) => {
          const v = periodSchema.parse(values);
          return payPeriodService.create({ ...v, notes: v.notes || null });
        }}
        update={(id, values) => {
          const v = periodSchema.parse(values);
          // The type is fixed after creation, so it is not sent on update.
          return payPeriodService.update(id, {
            id,
            periodName: v.periodName,
            startDate: v.startDate,
            endDate: v.endDate,
            notes: v.notes || null,
          });
        }}
        remove={(id) => payPeriodService.remove(id)}
        getId={(p) => p.id}
        actions={[
          {
            label: 'Close period',
            visible: (p) => p.status === 'Open' || p.status === 'PendingClose',
            run: async (p) => {
              await payPeriodService.close(p.id);
              await queryClient.invalidateQueries({ queryKey: ['hr', 'pay-periods'] });
            },
            confirm: {
              title: 'Close this pay period?',
              description:
                'Locks the period so attendance for it can no longer be edited, and makes it available to payroll export.',
            },
          },
        ]}
        columns={[
          { header: 'Period', cell: (p) => <span className="font-medium">{p.periodName}</span> },
          { header: 'Type', cell: (p) => p.type },
          { header: 'From', cell: (p) => formatDate(p.startDate) },
          { header: 'To', cell: (p) => formatDate(p.endDate) },
          { header: 'Closed', cell: (p) => formatDate(p.closedDate) },
          { header: 'Exported', cell: (p) => formatDate(p.exportedDate) },
          { header: 'Status', cell: (p) => <StatusBadge status={p.status} /> },
        ]}
        schema={periodSchema as any}
        emptyForm={emptyPeriod}
        toForm={(p) => ({
          periodName: p.periodName,
          type: p.type,
          startDate: p.startDate,
          endDate: p.endDate,
          notes: '',
        })}
        renderFields={(form) => (
          <>
            <FieldRow>
              <TextField
                form={form}
                name="periodName"
                label="Period name"
                required
                placeholder="e.g. August 2026"
              />
              <SelectField
                form={form}
                name="type"
                label="Type"
                required
                options={PAY_PERIOD_TYPE_OPTIONS}
              />
            </FieldRow>
            <FieldRow>
              <DateField form={form} name="startDate" label="Start date" required />
              <DateField form={form} name="endDate" label="End date" required />
            </FieldRow>
            <TextareaField form={form} name="notes" label="Notes" rows={2} />
          </>
        )}
      />
    </div>
  );
}
