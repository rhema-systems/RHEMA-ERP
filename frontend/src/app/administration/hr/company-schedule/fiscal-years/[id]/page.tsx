'use client';

import { use, useEffect, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  DateField,
  FieldRow,
  NumberField,
  SelectField,
  SwitchField,
  TextField,
} from '@/components/hr/employee/tabs/fields';
import { fiscalYearService } from '@/services/hr/company-schedule.service';
import { FISCAL_PERIOD_TYPES, FISCAL_YEAR_STATUSES } from '@/types/hr/company-schedule';
import type { FiscalPeriod } from '@/types/hr/company-schedule';

const spaced = (s?: string | null) => (s ? s.replace(/([a-z])([A-Z])/g, '$1 $2') : '—');

const yearSchema = z
  .object({
    fiscalYearName: z.string().min(1, 'Name is required').max(200),
    startDate: z.string().min(1),
    endDate: z.string().min(1),
    isCurrent: z.boolean(),
    status: z.string().min(1),
  })
  .refine((v) => v.endDate > v.startDate, {
    message: 'The end must be after the start',
    path: ['endDate'],
  });

type YearForm = z.infer<typeof yearSchema>;

const periodSchema = z
  .object({
    periodNumber: z.coerce.number().int().min(1).max(12),
    periodName: z.string().min(1, 'Name is required').max(100),
    type: z.string().min(1),
    startDate: z.string().min(1, 'Start date is required'),
    endDate: z.string().min(1, 'End date is required'),
  })
  .refine((v) => v.endDate > v.startDate, {
    message: 'The end must be after the start',
    path: ['endDate'],
  });

type PeriodForm = z.infer<typeof periodSchema>;

const emptyPeriod: PeriodForm = {
  periodNumber: 1,
  periodName: '',
  type: 'Month',
  startDate: '',
  endDate: '',
};

/**
 * One fiscal year and its periods.
 *
 * ⚠ **`year` is create-only** — the update DTO has no year field, so it is shown but not editable.
 * ⚠ **Closing a period is one-way from this screen** — there is no re-open endpoint, so the action
 * confirms first.
 */
export default function FiscalYearDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [saving, setSaving] = useState(false);

  const detailKey = ['hr', 'company-schedule', 'fiscal-years', id, 'detail'];
  const { data: year, isLoading } = useQuery({
    queryKey: detailKey,
    queryFn: () => fiscalYearService.getDetail(id),
  });

  const form = useForm<YearForm>({
    resolver: zodResolver(yearSchema) as any,
    defaultValues: { fiscalYearName: '', startDate: '', endDate: '', isCurrent: false, status: 'Active' },
  });

  useEffect(() => {
    if (!year) return;
    form.reset({
      fiscalYearName: year.fiscalYearName,
      startDate: year.startDate.slice(0, 10),
      endDate: year.endDate.slice(0, 10),
      isCurrent: year.isCurrent,
      status: year.status,
    });
  }, [year]);

  const fail = (title: string) => (error: any) =>
    toast({
      title,
      description: error?.response?.data?.detail ?? error?.message ?? 'Please try again.',
      variant: 'destructive',
    });

  const onSubmit = form.handleSubmit(async (v) => {
    setSaving(true);
    try {
      await fiscalYearService.update(id, {
        id,
        fiscalYearName: v.fiscalYearName.trim(),
        startDate: v.startDate,
        endDate: v.endDate,
        isCurrent: v.isCurrent,
        status: v.status as YearForm['status'] as never,
      });
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: detailKey }),
        queryClient.invalidateQueries({ queryKey: ['hr', 'company-schedule', 'fiscal-years'] }),
      ]);
      toast({ title: 'Fiscal year updated' });
    } catch (e) {
      fail('Could not update the fiscal year')(e);
    } finally {
      setSaving(false);
    }
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!year) {
    return <div className="p-6 text-muted-foreground">That fiscal year could not be found.</div>;
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={year.fiscalYearName}
        description={`Year ${year.year} · ${year.periodCount} period${year.periodCount === 1 ? '' : 's'}`}
        backHref="/administration/hr/company-schedule/fiscal-years"
        actions={
          <div className="flex items-center gap-2">
            {year.isCurrent && <Badge>Current</Badge>}
            <StatusBadge status={year.status} />
          </div>
        }
      />

      <form onSubmit={onSubmit}>
        <Card>
          <CardHeader><CardTitle>The year</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <TextField form={form} name="fiscalYearName" label="Name" required />
              <SelectField
                form={form}
                name="status"
                label="Status"
                options={FISCAL_YEAR_STATUSES.map((s) => ({ value: s, label: s }))}
              />
            </FieldRow>
            <FieldRow>
              <DateField form={form} name="startDate" label="From" required />
              <DateField form={form} name="endDate" label="To" required />
            </FieldRow>
            <SwitchField form={form} name="isCurrent" label="This is the current year" />
            <div className="flex justify-end">
              <Button type="submit" disabled={saving}>
                {saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                Save changes
              </Button>
            </div>
          </CardContent>
        </Card>
      </form>

      <ResourceCollectionTab<FiscalPeriod, PeriodForm>
        parentId={id}
        title="periods"
        singular="period"
        queryKey={['hr', 'company-schedule', 'fiscal-years', id, 'periods']}
        invalidateKeys={[detailKey, ['hr', 'company-schedule', 'fiscal-years']]}
        dialogHint="Quarters, months or halves inside this year."
        emptyDescription="This year has no periods yet."
        list={(yearId) => fiscalYearService.getPeriods(yearId)}
        create={(yearId, v) =>
          fiscalYearService.addPeriod(yearId, {
            periodNumber: v.periodNumber,
            periodName: v.periodName.trim(),
            type: v.type as FiscalPeriod['type'],
            startDate: v.startDate,
            endDate: v.endDate,
          })
        }
        update={(_yearId, periodId, v) =>
          fiscalYearService.updatePeriod(periodId, {
            id: periodId,
            periodNumber: v.periodNumber,
            periodName: v.periodName.trim(),
            type: v.type as FiscalPeriod['type'],
            startDate: v.startDate,
            endDate: v.endDate,
          })
        }
        remove={(_yearId, periodId) => fiscalYearService.removePeriod(periodId)}
        getId={(p) => p.id}
        columns={[
          { header: '#', cell: (p) => p.periodNumber },
          { header: 'Period', cell: (p) => p.periodName },
          { header: 'Type', cell: (p) => spaced(p.type) },
          { header: 'From', cell: (p) => p.startDate.slice(0, 10) },
          { header: 'To', cell: (p) => p.endDate.slice(0, 10) },
          { header: 'Status', cell: (p) => <StatusBadge status={p.isClosed ? 'Closed' : 'Open'} /> },
          { header: 'Closed on', cell: (p) => p.closedDate?.slice(0, 10) ?? '—' },
        ]}
        actions={[
          {
            label: 'Close period',
            visible: (p) => !p.isClosed,
            confirm: {
              title: 'Close this period?',
              description: 'There is no re-open action — closing is final from this screen.',
            },
            run: async (p) => {
              await fiscalYearService.closePeriod(p.id);
              await queryClient.invalidateQueries({
                queryKey: ['hr', 'company-schedule', 'fiscal-years', id, 'periods'],
              });
            },
          },
        ]}
        schema={periodSchema}
        emptyForm={emptyPeriod}
        toForm={(p) => ({
          periodNumber: p.periodNumber,
          periodName: p.periodName,
          type: p.type,
          startDate: p.startDate.slice(0, 10),
          endDate: p.endDate.slice(0, 10),
        })}
        renderFields={(form) => (
          <>
            <FieldRow>
              <NumberField form={form} name="periodNumber" label="Number" required />
              <SelectField
                form={form}
                name="type"
                label="Type"
                required
                options={FISCAL_PERIOD_TYPES.map((t) => ({ value: t, label: spaced(t) }))}
              />
            </FieldRow>
            <TextField form={form} name="periodName" label="Name" required placeholder="e.g. Q1, January" />
            <FieldRow>
              <DateField form={form} name="startDate" label="From" required />
              <DateField form={form} name="endDate" label="To" required />
            </FieldRow>
          </>
        )}
      />
    </div>
  );
}
