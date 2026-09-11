'use client';

import { useEffect } from 'react';
import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Badge } from '@/components/ui/badge';
import { salaryGradeService } from '@/services/hr/salary-grade.service';
import { policySettingsService } from '@/services/hr/policy-settings.service';
import { employeeService } from '@/services/hr/employee.service';
import type { EmployeeSalaryAssignment } from '@/types/hr/employee-subresources';
import { EmployeeSubResourceTab } from './EmployeeSubResourceTab';
import { DateField, FieldRow, SelectField, TextField } from './fields';

const schema = z
  .object({
    gradeId: z.string().min(1, 'Salary grade is required'),
    levelId: z.string().optional().or(z.literal('')),
    notchId: z.string().optional().or(z.literal('')),
    effectiveDate: z.string().min(1, 'Effective date is required'),
    effectiveTo: z.string().optional().or(z.literal('')),
    assignmentReason: z.string().min(1, 'Reason is required').max(500),
  })
  .refine((v) => !v.effectiveTo || v.effectiveTo >= v.effectiveDate, {
    message: 'End date cannot be before the effective date',
    path: ['effectiveTo'],
  });

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  gradeId: '',
  levelId: '',
  notchId: '',
  effectiveDate: new Date().toISOString().slice(0, 10),
  effectiveTo: '',
  assignmentReason: '',
};

// This resource takes DateTime, not DateOnly.
const toIso = (date: string) => new Date(`${date}T00:00:00`).toISOString();

const toPayload = (employeeId: string, v: FormValues) => ({
  employeeId,
  gradeId: v.gradeId,
  levelId: v.levelId || null,
  notchId: v.notchId || null,
  effectiveDate: toIso(v.effectiveDate),
  effectiveTo: v.effectiveTo ? toIso(v.effectiveTo) : null,
  assignmentReason: v.assignmentReason,
});

/**
 * Salary assignments place an employee on a grade/level/notch. The structure itself is
 * defined in Payroll and mirrored into HR, so the pickers here are read-only lookups —
 * see salary-grade.service.
 */
export function SalaryAssignmentsTab({
  employeeId,
  isOnPayroll = true,
  lockedReason,
}: {
  employeeId: string;
  /** Round 3, lane S: when set, the list is read-only and this sentence says why (the approval policy). */
  lockedReason?: string;
  /**
   * Off-payroll staff cannot be placed on a grade (the POST is refused with the same message), so
   * the tab renders their history read-only with a banner saying why, rather than offering an add
   * button that always fails.
   */
  isOnPayroll?: boolean;
}) {
  const { data: grades } = useQuery({
    queryKey: ['hr', 'salary-grades', 'all'],
    queryFn: () => salaryGradeService.getAll(),
  });

  // Whether the level is a tier the user chooses or the one implicit level a two-tier grade
  // carries (lane G). Two-tier is the default and TDC's case.
  const { data: policy } = useQuery({
    queryKey: ['hr', 'policy-settings'],
    queryFn: () => policySettingsService.get(),
  });
  const threeTier = policy?.salaryStructureTiers === 'GradeLevelAndNotch';

  return (
    <div className="space-y-3">
      {!isOnPayroll && (
        <div className="rounded-md border border-amber-300 bg-amber-50 px-3 py-2 text-sm text-amber-900 dark:border-amber-700 dark:bg-amber-950 dark:text-amber-100">
          This employee is not on payroll, so they cannot be placed on a salary grade. Any placement
          below was closed when they left payroll. Put them on payroll (Edit → Compensation &amp; Tax)
          to place them again.
        </div>
      )}
      {isOnPayroll && lockedReason && (
        <p className="text-xs text-muted-foreground">{lockedReason}</p>
      )}
    <EmployeeSubResourceTab<EmployeeSalaryAssignment, FormValues>
      employeeId={employeeId}
      title="salary assignments"
      singular="salary assignment"
      queryKey="salary-assignments"
      readOnly={!isOnPayroll || !!lockedReason}
      emptyDescription={
        isOnPayroll
          ? 'Place the employee on a grade to resolve their basic pay.'
          : 'Not on payroll — no grade placement applies.'
      }
      getId={(s) => s.id}
      list={employeeService.getSalaryAssignments.bind(employeeService)}
      create={(id, v) => employeeService.addSalaryAssignment(id, toPayload(id, v))}
      update={(id, assignmentId, v) =>
        employeeService.updateSalaryAssignment(id, assignmentId, {
          id: assignmentId,
          ...toPayload(id, v),
        })
      }
      remove={employeeService.removeSalaryAssignment.bind(employeeService)}
      columns={[
        {
          header: 'Grade',
          cell: (s) => [s.gradeCode, s.gradeName].filter(Boolean).join(' — ') || '—',
        },
        { header: 'Level', cell: (s) => s.levelCode || '—' },
        { header: 'Notch', cell: (s) => s.notchNumber || '—' },
        { header: 'Amount', cell: (s) => s.amount?.toLocaleString() ?? '—' },
        { header: 'Effective', cell: (s) => s.effectiveDate?.slice(0, 10) || '—' },
        { header: 'Until', cell: (s) => s.effectiveTo?.slice(0, 10) || '—' },
        {
          // ⚠ Four states, not two. A withdrawn placement used to be indistinguishable from one
          // that ran its course — and a placement dated next month was labelled "Active" outright,
          // because the old flag never asked whether it had started.
          header: 'Status',
          cell: (s) =>
            s.withdrawnAt ? (
              <span className="inline-flex flex-col gap-0.5">
                <Badge variant="outline" className="w-fit text-muted-foreground">Withdrawn</Badge>
                {s.withdrawnReason && (
                  <span className="text-xs text-muted-foreground">{s.withdrawnReason}</span>
                )}
              </span>
            ) : s.isActive ? (
              <Badge variant="secondary">Active</Badge>
            ) : s.isScheduled ? (
              <Badge variant="outline">Scheduled</Badge>
            ) : (
              <Badge variant="outline" className="text-muted-foreground">Ended</Badge>
            ),
        },
      ]}
      schema={schema}
      emptyForm={empty}
      dialogClassName="sm:max-w-[620px]"
      toForm={(s) => ({
        gradeId: s.gradeId,
        levelId: s.levelId ?? '',
        notchId: s.notchId ?? '',
        effectiveDate: s.effectiveDate?.slice(0, 10) ?? '',
        effectiveTo: s.effectiveTo?.slice(0, 10) ?? '',
        assignmentReason: s.assignmentReason ?? s.reason ?? '',
      })}
      renderFields={(form) => (
        <SalaryAssignmentFields form={form} grades={grades ?? []} threeTier={threeTier} />
      )}
    />
    </div>
  );
}

/**
 * Split out so the grade → level → notch cascade can use hooks. Levels and notches are
 * fetched on demand from the selected parent.
 */
function SalaryAssignmentFields({
  form,
  grades,
  threeTier,
}: {
  form: any;
  grades: { id: string; code: string; name: string }[];
  /** Show the level as a step. In two-tier the grade's one implicit level is picked silently. */
  threeTier: boolean;
}) {
  const gradeId = form.watch('gradeId') as string;
  const levelId = form.watch('levelId') as string;

  const { data: levels } = useQuery({
    queryKey: ['hr', 'salary-grades', gradeId, 'levels'],
    queryFn: () => salaryGradeService.getLevels(gradeId),
    enabled: !!gradeId,
  });

  // ⚠ Two-tier: the level exists in the data but not in the user's head. Resolve it the moment
  // the grade's levels arrive so the notch list can load, and never ask. The server does the same
  // resolution for a caller that sends no level at all — this is only so the picker has a parent.
  useEffect(() => {
    if (threeTier || !levels || levels.length === 0) return;
    const sole = levels.find((l) => l.isActive) ?? levels[0];
    if (sole && levelId !== sole.id) form.setValue('levelId', sole.id, { shouldValidate: false });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [threeTier, levels, levelId]);

  const { data: notches } = useQuery({
    queryKey: ['hr', 'salary-levels', levelId, 'notches'],
    queryFn: () => salaryGradeService.getNotches(levelId),
    enabled: !!levelId,
  });

  return (
    <>
      <SelectField
        form={form}
        name="gradeId"
        label="Salary grade"
        required
        options={grades.map((g) => ({ value: g.id, label: `${g.code} — ${g.name}` }))}
      />
      <FieldRow>
        {threeTier && (
          <SelectField
            form={form}
            name="levelId"
            label="Level"
            options={(levels ?? []).map((l) => ({ value: l.id, label: l.code || l.name }))}
            allowEmpty
          />
        )}
        <SelectField
          form={form}
          name="notchId"
          label="Notch"
          options={(notches ?? []).map((n) => ({
            value: n.id,
            label: `${n.notchNumber} — ${n.salaryAmount.toLocaleString()}`,
          }))}
          allowEmpty
        />
      </FieldRow>
      <FieldRow>
        <DateField form={form} name="effectiveDate" label="Effective from" required />
        <DateField form={form} name="effectiveTo" label="Effective to" />
      </FieldRow>
      <TextField
        form={form}
        name="assignmentReason"
        label="Reason"
        placeholder="Annual review"
        required
      />
      <p className="text-xs text-muted-foreground">
        {threeTier
          ? 'Grade, then level, then notch. The notch amount is the basic pay.'
          : 'Grade, then notch. The notch amount is the basic pay.'}
      </p>
    </>
  );
}
