'use client';

import { useMemo } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { CalendarRange, CircleDot, FolderOpen, Layers } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import {
  TextField,
  NumberField,
  DateField,
  SelectField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { CyclePhaseDateFields } from '@/components/hr/performance/CycleFormFields';
import { appraisalCycleService, appraisalSettingsService } from '@/services/hr/appraisal.service';
import { APPRAISAL_TYPE_OPTIONS } from '@/types/hr/appraisal';
import type { AppraisalCycle, AppraisalType, CreateAppraisalCycle } from '@/types/hr/appraisal';
import { humanizeEnum } from '@/lib/hr/attendance-format';

/**
 * Appraisal cycles — the run of an appraisal from goal setting to sign-off.
 *
 * A cycle is created as a Draft, given target groups and template assignments on its detail
 * page, then opened. Opening is checked against the other cycles of the same type and year:
 * an employee may only be covered by one active cycle at a time, and the refusal names the
 * cycles that overlap.
 *
 * Generation of the appraisal records themselves is deliberately a separate step after
 * opening, so the coverage preview can be read first.
 */
const cycleSchema = z
  .object({
    cycleCode: z.string().min(1, 'Required').max(50),
    cycleName: z.string().min(1, 'Required').max(200),
    year: z.coerce.number().int().min(2000).max(2100),
    appraisalType: z.string().min(1, 'Required'),
    startDate: z.string().min(1, 'Required'),
    endDate: z.string().min(1, 'Required'),
    appraisalSettingsId: z.string().min(1, 'Required'),
    goalSettingOpenDate: z.string().optional(),
    goalSettingDeadline: z.string().optional(),
    q1ReviewOpenDate: z.string().optional(),
    q1ReviewDeadline: z.string().optional(),
    midYearOpenDate: z.string().optional(),
    midYearDeadline: z.string().optional(),
    q3ReviewOpenDate: z.string().optional(),
    q3ReviewDeadline: z.string().optional(),
    peerNominationDeadline: z.string().optional(),
    selfEvaluationOpenDate: z.string().optional(),
    selfEvaluationDeadline: z.string().optional(),
    peerEvaluationOpenDate: z.string().optional(),
    peerEvaluationDeadline: z.string().optional(),
    managerEvaluationOpenDate: z.string().optional(),
    managerEvaluationDeadline: z.string().optional(),
    calibrationOpenDate: z.string().optional(),
    calibrationDeadline: z.string().optional(),
    hrReviewOpenDate: z.string().optional(),
    hrReviewDeadline: z.string().optional(),
    employeeAcknowledgeDeadline: z.string().optional(),
    finalConversationDeadline: z.string().optional(),
  })
  .refine((v) => v.endDate > v.startDate, {
    message: 'The end date must be after the start date',
    path: ['endDate'],
  });

type CycleForm = z.input<typeof cycleSchema>;

const thisYear = new Date().getFullYear();

const emptyCycle: CycleForm = {
  cycleCode: '',
  cycleName: '',
  year: thisYear,
  appraisalType: 'Annual',
  startDate: `${thisYear}-01-01`,
  endDate: `${thisYear}-12-31`,
  appraisalSettingsId: '',
  goalSettingOpenDate: '',
  goalSettingDeadline: '',
  q1ReviewOpenDate: '',
  q1ReviewDeadline: '',
  midYearOpenDate: '',
  midYearDeadline: '',
  q3ReviewOpenDate: '',
  q3ReviewDeadline: '',
  peerNominationDeadline: '',
  selfEvaluationOpenDate: '',
  selfEvaluationDeadline: '',
  peerEvaluationOpenDate: '',
  peerEvaluationDeadline: '',
  managerEvaluationOpenDate: '',
  managerEvaluationDeadline: '',
  calibrationOpenDate: '',
  calibrationDeadline: '',
  hrReviewOpenDate: '',
  hrReviewDeadline: '',
  employeeAcknowledgeDeadline: '',
  finalConversationDeadline: '',
};

/** Blank means "this phase has no deadline", which is null on the wire, not an empty string. */
const nullable = (value?: string) => (value && value.trim() !== '' ? value : null);

const toPayload = (values: CycleForm): CreateAppraisalCycle => {
  const v = cycleSchema.parse(values);
  return {
    cycleCode: v.cycleCode,
    cycleName: v.cycleName,
    year: v.year,
    appraisalType: v.appraisalType as AppraisalType,
    startDate: v.startDate,
    endDate: v.endDate,
    appraisalSettingsId: v.appraisalSettingsId,
    // A cycle is always created as a Draft; Open and Closed are reached through their own
    // endpoints, which run the overlap checks and stamp who did it.
    status: 'Draft',
    goalSettingOpenDate: nullable(v.goalSettingOpenDate),
    goalSettingDeadline: nullable(v.goalSettingDeadline),
    q1ReviewOpenDate: nullable(v.q1ReviewOpenDate),
    q1ReviewDeadline: nullable(v.q1ReviewDeadline),
    midYearOpenDate: nullable(v.midYearOpenDate),
    midYearDeadline: nullable(v.midYearDeadline),
    q3ReviewOpenDate: nullable(v.q3ReviewOpenDate),
    q3ReviewDeadline: nullable(v.q3ReviewDeadline),
    peerNominationDeadline: nullable(v.peerNominationDeadline),
    selfEvaluationOpenDate: nullable(v.selfEvaluationOpenDate),
    selfEvaluationDeadline: nullable(v.selfEvaluationDeadline),
    peerEvaluationOpenDate: nullable(v.peerEvaluationOpenDate),
    peerEvaluationDeadline: nullable(v.peerEvaluationDeadline),
    managerEvaluationOpenDate: nullable(v.managerEvaluationOpenDate),
    managerEvaluationDeadline: nullable(v.managerEvaluationDeadline),
    calibrationOpenDate: nullable(v.calibrationOpenDate),
    calibrationDeadline: nullable(v.calibrationDeadline),
    hrReviewOpenDate: nullable(v.hrReviewOpenDate),
    hrReviewDeadline: nullable(v.hrReviewDeadline),
    employeeAcknowledgeDeadline: nullable(v.employeeAcknowledgeDeadline),
    finalConversationDeadline: nullable(v.finalConversationDeadline),
  };
};

/** For display: an em dash reads better than a blank cell. */
const day = (value?: string | null) => value?.slice(0, 10) ?? '—';

/** For the form: `<input type="date">` wants an empty string, never a placeholder. */
const dayInput = (value?: string | null) => value?.slice(0, 10) ?? '';

export default function AppraisalCyclesPage() {
  const { data: settings } = useQuery({
    queryKey: ['hr', 'appraisal-settings'],
    queryFn: () => appraisalSettingsService.getAll(),
  });
  const { data: cycles } = useQuery({
    queryKey: ['hr', 'appraisal-cycles'],
    queryFn: () => appraisalCycleService.getAll(),
  });

  const settingsOptions = useMemo(
    () => (settings ?? []).map((s) => ({ value: s.id, label: s.settingsName })),
    [settings],
  );

  const rows = cycles ?? [];
  const live = rows.filter((c) => c.status === 'Open' || c.status === 'InProgress');
  const drafts = rows.filter((c) => c.status === 'Draft');

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Appraisal Cycles"
        description="Each run of the appraisal process: its dates, who it covers, and which forms it uses."
        backHref="/hr/performance"
      />

      <MetricTiles
        tiles={[
          { label: 'Cycles', value: rows.length, icon: CalendarRange },
          {
            label: 'Live',
            value: live.length,
            hint: live.length > 0 ? live.map((c) => c.cycleCode).join(', ') : 'None running',
            icon: CircleDot,
            tone: live.length > 0 ? 'success' : 'default',
          },
          {
            label: 'In draft',
            value: drafts.length,
            hint: 'Not yet opened — still editable and deletable.',
            icon: FolderOpen,
          },
          {
            label: 'Settings profiles',
            value: (settings ?? []).length,
            hint: 'A cycle cannot be created without one.',
            icon: Layers,
            tone: (settings ?? []).length === 0 ? 'warning' : 'default',
          },
        ]}
      />

      <ResourceListPanel<AppraisalCycle, CycleForm>
        title="cycles"
        singular="cycle"
        queryKey={['hr', 'appraisal-cycles']}
        dialogHint="Phase dates are all optional — a phase with no deadline never appears on the calendar or in reminders."
        emptyDescription="Create the first appraisal cycle for this year."
        dialogClassName="sm:max-w-[720px]"
        list={() => appraisalCycleService.getAll()}
        create={(values) => appraisalCycleService.create(toPayload(values))}
        update={(id, values) => appraisalCycleService.update(id, { id, ...toPayload(values) })}
        remove={(id) => appraisalCycleService.remove(id)}
        getId={(r) => r.id}
        columns={[
          {
            header: 'Cycle',
            cell: (r) => (
              <Link href={`/hr/performance/cycles/${r.id}`} className="font-medium hover:underline">
                {r.cycleCode}
              </Link>
            ),
          },
          { header: 'Name', cell: (r) => r.cycleName },
          { header: 'Type', cell: (r) => <Badge variant="outline">{humanizeEnum(r.appraisalType)}</Badge> },
          { header: 'Year', cell: (r) => r.year, className: 'text-right' },
          {
            header: 'Period',
            cell: (r) => (
              <span className="text-muted-foreground">
                {day(r.startDate)} → {day(r.endDate)}
              </span>
            ),
          },
          { header: 'Settings', cell: (r) => r.appraisalSettingsName || '—' },
          { header: 'Status', cell: (r) => <StatusBadge status={humanizeEnum(r.status)} /> },
        ]}
        schema={cycleSchema as any}
        emptyForm={emptyCycle}
        toForm={(r) => ({
          cycleCode: r.cycleCode,
          cycleName: r.cycleName,
          year: r.year,
          appraisalType: r.appraisalType,
          startDate: dayInput(r.startDate),
          endDate: dayInput(r.endDate),
          appraisalSettingsId: r.appraisalSettingsId,
          goalSettingOpenDate: dayInput(r.goalSettingOpenDate),
          goalSettingDeadline: dayInput(r.goalSettingDeadline),
          q1ReviewOpenDate: dayInput(r.q1ReviewOpenDate),
          q1ReviewDeadline: dayInput(r.q1ReviewDeadline),
          midYearOpenDate: dayInput(r.midYearOpenDate),
          midYearDeadline: dayInput(r.midYearDeadline),
          q3ReviewOpenDate: dayInput(r.q3ReviewOpenDate),
          q3ReviewDeadline: dayInput(r.q3ReviewDeadline),
          peerNominationDeadline: dayInput(r.peerNominationDeadline),
          selfEvaluationOpenDate: dayInput(r.selfEvaluationOpenDate),
          selfEvaluationDeadline: dayInput(r.selfEvaluationDeadline),
          peerEvaluationOpenDate: dayInput(r.peerEvaluationOpenDate),
          peerEvaluationDeadline: dayInput(r.peerEvaluationDeadline),
          managerEvaluationOpenDate: dayInput(r.managerEvaluationOpenDate),
          managerEvaluationDeadline: dayInput(r.managerEvaluationDeadline),
          calibrationOpenDate: dayInput(r.calibrationOpenDate),
          calibrationDeadline: dayInput(r.calibrationDeadline),
          hrReviewOpenDate: dayInput(r.hrReviewOpenDate),
          hrReviewDeadline: dayInput(r.hrReviewDeadline),
          employeeAcknowledgeDeadline: dayInput(r.employeeAcknowledgeDeadline),
          finalConversationDeadline: dayInput(r.finalConversationDeadline),
        })}
        renderFields={(form) => (
          <>
            <FieldRow>
              <TextField
                form={form}
                name="cycleCode"
                label="Cycle code"
                required
                placeholder="e.g. FY2026"
              />
              <NumberField form={form} name="year" label="Year" required />
            </FieldRow>
            <TextField
              form={form}
              name="cycleName"
              label="Cycle name"
              required
              placeholder="e.g. Annual appraisal 2026"
            />
            <FieldRow>
              <SelectField
                form={form}
                name="appraisalType"
                label="Type"
                required
                options={APPRAISAL_TYPE_OPTIONS}
              />
              <SelectField
                form={form}
                name="appraisalSettingsId"
                label="Settings profile"
                required
                options={settingsOptions}
                placeholder={
                  settingsOptions.length === 0 ? 'No profiles configured' : 'Select a profile…'
                }
              />
            </FieldRow>
            <FieldRow>
              <DateField form={form} name="startDate" label="Start date" required />
              <DateField form={form} name="endDate" label="End date" required />
            </FieldRow>

            <div className="border-t pt-4">
              <CyclePhaseDateFields form={form} />
            </div>
          </>
        )}
      />
    </div>
  );
}
