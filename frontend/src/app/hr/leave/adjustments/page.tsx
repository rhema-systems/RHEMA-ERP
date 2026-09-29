'use client';

import { useState } from 'react';
import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { useLeaveYear } from '@/components/hr/leave/use-leave-year';
import { leaveService } from '@/services/hr/leave.service';
import { leaveTypeService } from '@/services/hr/leave-type.service';
import { reasonCodeService } from '@/services/hr/lookup.service';
import type { LeaveAdjustment } from '@/types/hr/leave-request';
import {
  DateField,
  FieldRow,
  NumberField,
  SelectField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';

const ALL = '__all__';

const schema = z.object({
  employeeId: z.string().min(1, 'Employee is required'),
  leaveTypeId: z.string().min(1, 'Leave type is required'),
  year: z.coerce.number().int().min(2000),
  // Signed: negative deducts. Zero would be a no-op, so it is rejected.
  days: z.coerce.number().refine((v) => v !== 0, 'Enter a non-zero number of days'),
  reasonCodeId: z.string().optional().or(z.literal('')),
  reason: z.string().min(1, 'Remarks are required').max(500),
  adjustmentDate: z.string().optional().or(z.literal('')),
});

type FormValues = z.infer<typeof schema>;

const fmtDays = (n: number) => (Number.isInteger(n) ? String(n) : n.toFixed(1));

/**
 * The balance the adjustment is about to move, shown inside the form.
 *
 * Finish-plan lane 4: TDC's demo feedback was that the adjustment form did not show the employee's
 * balance — the person correcting a balance had to open another screen to see what they were
 * correcting. This reads the same row the Balances screen reads and previews the result of the
 * signed `days` value before anything is saved.
 */
function BalancePreview({
  employeeId,
  leaveTypeId,
  year,
  days,
}: {
  employeeId: string;
  leaveTypeId: string;
  year: number;
  days: number;
}) {
  const ready = !!employeeId && !!leaveTypeId && Number.isFinite(year) && year >= 2000;
  const { data, isFetching } = useQuery({
    queryKey: ['hr', 'leave-balances', 'employee', employeeId, year],
    queryFn: () => leaveService.getEmployeeBalances(employeeId, year),
    enabled: ready,
  });

  if (!ready) {
    return (
      <p className="text-xs text-muted-foreground">
        Choose the employee, leave type and year to see the balance this adjustment will move.
      </p>
    );
  }
  if (isFetching && !data) {
    return <p className="text-xs text-muted-foreground">Loading the current balance…</p>;
  }

  const balance = (data ?? []).find((b) => b.leaveTypeId === leaveTypeId);
  if (!balance) {
    return (
      <div className="rounded-md border border-dashed p-3 text-xs text-muted-foreground">
        No balance row exists yet for this leave type in {year}. Saving creates one at the type&apos;s
        default entitlement, then applies this adjustment to it.
      </div>
    );
  }

  const after = balance.availableDays + (Number.isFinite(days) ? days : 0);
  const cells: Array<[string, number]> = [
    ['Entitled', balance.entitledDays],
    ['Carried over', balance.carriedOverDays],
    ['Adjustments', balance.adjustmentDays],
    ['Used', balance.usedDays],
    ['Pending', balance.pendingDays],
    ['Encashed', balance.encashedDays],
  ];

  return (
    <div className="rounded-md border bg-muted/30 p-3">
      <div className="mb-2 flex items-center justify-between">
        <p className="text-xs font-medium">
          Current balance · {balance.leaveTypeName} {balance.year}
        </p>
        <Badge variant="secondary">{fmtDays(balance.availableDays)} available</Badge>
      </div>
      <dl className="grid grid-cols-3 gap-x-4 gap-y-1 text-xs sm:grid-cols-6">
        {cells.map(([label, value]) => (
          <div key={label}>
            <dt className="text-muted-foreground">{label}</dt>
            <dd className="font-medium">{fmtDays(value)}</dd>
          </div>
        ))}
      </dl>
      {Number.isFinite(days) && days !== 0 && (
        <p className={`mt-2 text-xs ${after < 0 ? 'text-red-600' : 'text-muted-foreground'}`}>
          After this adjustment: <span className="font-medium">{fmtDays(after)}</span> available
          {after < 0 ? ' — the balance would go negative.' : '.'}
        </p>
      )}
    </div>
  );
}

export default function LeaveAdjustmentsPage() {
  const [employeeId, setEmployeeId] = useState<string | null>(null);
  const [leaveTypeId, setLeaveTypeId] = useState<string>(ALL);
  // ⚠ Leave settings audit 2, L-95: the current leave year, not the calendar year; the choice is
  // kept apart so a late answer moves the default and never overrides it (see useLeaveYear).
  const { currentYear } = useLeaveYear();
  const years = [currentYear + 1, currentYear, currentYear - 1, currentYear - 2];
  const [chosenYear, setYear] = useState<string | null>(null);
  const year = chosenYear ?? String(currentYear);
  const [search, setSearch] = useState('');

  const { data: leaveTypes } = useQuery({
    queryKey: ['hr', 'leave-types', 'active'],
    queryFn: () => leaveTypeService.getAll(true),
  });

  const { data: reasonCodes } = useQuery({
    queryKey: ['hr', 'reason-codes'],
    queryFn: () => reasonCodeService.getAll(),
  });

  // Reason codes are categorised; leave adjustments have their own category.
  const adjustmentReasons = (reasonCodes ?? []).filter(
    (c) => c.isActive && (c.category === 'LeaveAdjustment' || c.category === 'General'),
  );

  const empty: FormValues = {
    employeeId: employeeId ?? '',
    leaveTypeId: leaveTypeId === ALL ? '' : leaveTypeId,
    year: Number(year),
    days: 0,
    reasonCodeId: '',
    reason: '',
    adjustmentDate: new Date().toISOString().slice(0, 10),
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Leave Adjustments"
        description="Manual corrections to a leave balance, with an audit trail."
      />

      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid gap-4 md:grid-cols-4">
            <div className="space-y-2">
              <label className="text-sm font-medium">Employee</label>
              <EmployeePicker value={employeeId} onChange={setEmployeeId} />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Leave type</label>
              <Select value={leaveTypeId} onValueChange={setLeaveTypeId}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL}>All leave types</SelectItem>
                  {(leaveTypes ?? []).map((t) => (
                    <SelectItem key={t.id} value={t.id}>
                      {t.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Year</label>
              <Select value={year} onValueChange={setYear}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {years.map((y) => (
                    <SelectItem key={y} value={String(y)}>
                      {y}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Search</label>
              <Input
                placeholder="Remarks or employee…"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>
          </div>
        </CardContent>
      </Card>

      <ResourceCollectionTab<LeaveAdjustment, FormValues>
        // Adjustments are tenant-wide here rather than nested under one parent, so the
        // parent id is a stable constant and the filters live in the query key.
        parentId="all"
        title="adjustments"
        singular="adjustment"
        queryKey={['hr', 'leave-adjustments', year, employeeId, leaveTypeId, search]}
        invalidateKeys={[['hr', 'leave-balances']]}
        dialogHint="Correct a leave balance. Use a negative value to deduct days."
        emptyDescription="No adjustments match these filters."
        getId={(a) => a.id}
        list={() =>
          leaveService.getAdjustments(
            Number(year),
            employeeId ?? undefined,
            leaveTypeId === ALL ? undefined : leaveTypeId,
            search || undefined,
          )
        }
        // Who performed the adjustment is stamped server-side from the token: it is an Employee
        // foreign key, and the login's user id this form used to send was never one.
        create={(_p, v) =>
          leaveService.createAdjustment({
            employeeId: v.employeeId,
            leaveTypeId: v.leaveTypeId,
            year: v.year,
            days: v.days,
            reasonCodeId: v.reasonCodeId || null,
            reason: v.reason,
            adjustmentDate: v.adjustmentDate || null,
          })
        }
        update={(_p, id, v) =>
          leaveService.updateAdjustment(id, {
            days: v.days,
            reasonCodeId: v.reasonCodeId || null,
            reason: v.reason,
            adjustmentDate: v.adjustmentDate || null,
          })
        }
        remove={(_p, id) => leaveService.removeAdjustment(id)}
        columns={[
          { header: 'Employee', cell: (a) => a.employeeName },
          {
            header: 'Leave type',
            cell: (a) => `${a.leaveTypeName}${a.leaveSubTypeName ? ` · ${a.leaveSubTypeName}` : ''}`,
          },
          { header: 'Year', cell: (a) => a.year },
          {
            header: 'Days',
            cell: (a) => (
              <Badge variant={a.days < 0 ? 'outline' : 'secondary'}>
                {a.days > 0 ? `+${a.days}` : a.days}
              </Badge>
            ),
          },
          { header: 'Reason code', cell: (a) => a.reasonCodeName || '—' },
          { header: 'Remarks', cell: (a) => a.reason },
          { header: 'Date', cell: (a) => a.adjustmentDate?.slice(0, 10) || '—' },
          { header: 'By', cell: (a) => a.performedByName || '—' },
        ]}
        schema={schema}
        emptyForm={empty}
        dialogClassName="sm:max-w-[680px]"
        toForm={(a) => ({
          employeeId: a.employeeId,
          leaveTypeId: a.leaveTypeId,
          year: a.year,
          days: a.days,
          reasonCodeId: a.reasonCodeId ?? '',
          reason: a.reason,
          adjustmentDate: a.adjustmentDate?.slice(0, 10) ?? '',
        })}
        renderFields={(form) => (
          <>
            <div className="space-y-2">
              <label className="text-sm font-medium">Employee</label>
              <EmployeePicker
                value={form.watch('employeeId') || null}
                onChange={(v) => form.setValue('employeeId', v ?? '', { shouldValidate: true })}
              />
              {form.formState.errors.employeeId && (
                <p className="text-sm text-red-500">{form.formState.errors.employeeId.message}</p>
              )}
            </div>
            <FieldRow>
              <SelectField
                form={form}
                name="leaveTypeId"
                label="Leave type"
                required
                options={(leaveTypes ?? []).map((t) => ({ value: t.id, label: t.name }))}
              />
              <NumberField form={form} name="year" label="Year" required />
            </FieldRow>

            <BalancePreview
              employeeId={form.watch('employeeId')}
              leaveTypeId={form.watch('leaveTypeId')}
              year={Number(form.watch('year'))}
              days={Number(form.watch('days'))}
            />

            <FieldRow>
              <NumberField
                form={form}
                name="days"
                label="Days (negative deducts)"
                step="0.5"
                required
              />
              <DateField form={form} name="adjustmentDate" label="Adjustment date" />
            </FieldRow>
            <SelectField
              form={form}
              name="reasonCodeId"
              label="Reason code"
              options={adjustmentReasons.map((c) => ({ value: c.id, label: c.name }))}
              allowEmpty
            />
            {/* The entity calls this Reason; TDC's feedback and the entity's own remark both say the
                screen should call the free text "Remarks", beside the coded reason above. */}
            <TextareaField form={form} name="reason" label="Remarks" rows={3} required />
          </>
        )}
      />
    </div>
  );
}
