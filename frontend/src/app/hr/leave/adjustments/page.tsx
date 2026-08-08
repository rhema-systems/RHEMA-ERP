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
import { useAuth } from '@/hooks/use-auth';
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
const currentYear = new Date().getFullYear();
const years = [currentYear + 1, currentYear, currentYear - 1, currentYear - 2];

const schema = z.object({
  employeeId: z.string().min(1, 'Employee is required'),
  leaveTypeId: z.string().min(1, 'Leave type is required'),
  year: z.coerce.number().int().min(2000),
  // Signed: negative deducts. Zero would be a no-op, so it is rejected.
  days: z.coerce.number().refine((v) => v !== 0, 'Enter a non-zero number of days'),
  reasonCodeId: z.string().optional().or(z.literal('')),
  reason: z.string().min(1, 'A reason is required').max(500),
  adjustmentDate: z.string().optional().or(z.literal('')),
});

type FormValues = z.infer<typeof schema>;

export default function LeaveAdjustmentsPage() {
  const { user } = useAuth();
  const [employeeId, setEmployeeId] = useState<string | null>(null);
  const [leaveTypeId, setLeaveTypeId] = useState<string>(ALL);
  const [year, setYear] = useState<string>(String(currentYear));
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
                placeholder="Reason or employee…"
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
        create={(_p, v) =>
          leaveService.createAdjustment({
            employeeId: v.employeeId,
            leaveTypeId: v.leaveTypeId,
            year: v.year,
            days: v.days,
            reasonCodeId: v.reasonCodeId || null,
            reason: v.reason,
            performedBy: (user?.id as string) ?? '',
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
          { header: 'Reason', cell: (a) => a.reason },
          { header: 'Date', cell: (a) => a.adjustmentDate?.slice(0, 10) || '—' },
          { header: 'By', cell: (a) => a.performedByName || '—' },
        ]}
        schema={schema}
        emptyForm={empty}
        dialogClassName="sm:max-w-[620px]"
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
            <TextareaField form={form} name="reason" label="Reason" rows={3} />
          </>
        )}
      />
    </div>
  );
}
