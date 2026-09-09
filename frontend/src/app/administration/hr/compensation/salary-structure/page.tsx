'use client';

/**
 * The salary structure — grades, levels and notches.
 *
 * Lane G. Who maintains this is a policy setting (`salaryStructureSource`):
 *
 * - **Payroll** (the default, TDC's case): the structure is defined in Administration → HR →
 *   Payroll → Grades Setup and mirrored here by a projection. This screen is read-only; every write
 *   answers 409. "Refresh from Payroll" forces a re-projection, though reads already reconcile.
 * - **HR**: the projection is off and this screen is where the structure is authored.
 *
 * And how many tiers it has (`salaryStructureTiers`): two-tier shows grade → notch and the grade's
 * one implicit level is never surfaced; three-tier shows grade → level → notch.
 *
 * ⚠ The screen reads both settings rather than probing the API for a 409. A screen that discovers
 * its own permissions by failing is a screen that fails in front of the user.
 */

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2, RefreshCw } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  DateField,
  FieldRow,
  NumberField,
  SwitchField,
  TextField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { salaryGradeService } from '@/services/hr/salary-grade.service';
import { policySettingsService } from '@/services/hr/policy-settings.service';
import type { SalaryGrade, SalaryLevel, SalaryNotch } from '@/types/hr/salary';

const money = (v: number) => new Intl.NumberFormat('en-GH', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(v);

// ── grade ────────────────────────────────────────────────────────────────────

const gradeSchema = z
  .object({
    code: z.string().min(1, 'A code is required').max(50),
    name: z.string().min(1, 'A name is required').max(100),
    description: z.string().max(500).optional().or(z.literal('')),
    minSalary: z.coerce.number().min(0),
    maxSalary: z.coerce.number().min(0),
    effectiveDate: z.string().min(1, 'An effective date is required'),
    endDate: z.string().optional().or(z.literal('')),
    isActive: z.boolean(),
  })
  .refine((v) => v.maxSalary >= v.minSalary, { message: 'Max must be at least min', path: ['maxSalary'] });

type GradeForm = z.infer<typeof gradeSchema>;

const emptyGrade: GradeForm = {
  code: '',
  name: '',
  description: '',
  minSalary: 0,
  maxSalary: 0,
  effectiveDate: new Date().toISOString().slice(0, 10),
  endDate: '',
  isActive: true,
};

const gradePayload = (v: GradeForm) => ({
  code: v.code,
  name: v.name,
  description: v.description || '',
  minSalary: v.minSalary,
  maxSalary: v.maxSalary,
  effectiveDate: new Date(`${v.effectiveDate}T00:00:00`).toISOString(),
  endDate: v.endDate ? new Date(`${v.endDate}T00:00:00`).toISOString() : null,
  isActive: v.isActive,
});

// ── level ────────────────────────────────────────────────────────────────────

const levelSchema = z
  .object({
    code: z.string().min(1, 'A code is required').max(50),
    name: z.string().min(1, 'A name is required').max(100),
    minSalary: z.coerce.number().min(0),
    midSalary: z.coerce.number().min(0),
    maxSalary: z.coerce.number().min(0),
    sequence: z.coerce.number().int().min(1),
    isActive: z.boolean(),
  })
  .refine((v) => v.minSalary <= v.midSalary && v.midSalary <= v.maxSalary, {
    message: 'Min ≤ mid ≤ max',
    path: ['midSalary'],
  });

type LevelForm = z.infer<typeof levelSchema>;

const emptyLevel: LevelForm = { code: '', name: '', minSalary: 0, midSalary: 0, maxSalary: 0, sequence: 1, isActive: true };

// ── notch ────────────────────────────────────────────────────────────────────

const notchSchema = z.object({
  notchNumber: z.coerce.number().int().min(1),
  salaryAmount: z.coerce.number().min(0),
  isActive: z.boolean(),
});

type NotchForm = z.infer<typeof notchSchema>;

const emptyNotch: NotchForm = { notchNumber: 1, salaryAmount: 0, isActive: true };

// ═════════════════════════════════════════════════════════════════════════════

export default function SalaryStructurePage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const policy = useQuery({ queryKey: ['hr', 'policy-settings'], queryFn: () => policySettingsService.get() });
  const hrMastered = policy.data?.salaryStructureSource === 'Hr';
  const threeTier = policy.data?.salaryStructureTiers === 'GradeLevelAndNotch';

  const [selectedGrade, setSelectedGrade] = useState<SalaryGrade | null>(null);
  const [selectedLevel, setSelectedLevel] = useState<SalaryLevel | null>(null);

  const levels = useQuery({
    queryKey: ['hr', 'salary-grades', selectedGrade?.id, 'levels', 'all'],
    queryFn: () => salaryGradeService.getLevels(selectedGrade!.id, true),
    enabled: !!selectedGrade,
  });

  // Two-tier: the grade's one implicit level is the notches' parent and is never shown.
  const notchParentLevel = useMemo(() => {
    if (threeTier) return selectedLevel;
    const list = levels.data ?? [];
    return list.find((l) => l.isActive) ?? list[0] ?? null;
  }, [threeTier, selectedLevel, levels.data]);

  const refresh = useMutation({
    mutationFn: () => salaryGradeService.syncFromPayroll(),
    onSuccess: async (r) => {
      toast({
        title: r.skippedAsUnchanged ? 'Already current' : 'Refreshed from Payroll',
        description: r.skippedAsUnchanged
          ? 'Payroll has not changed since the last projection.'
          : `Grades +${r.gradesCreated}/~${r.gradesUpdated}, notches +${r.notchesCreated}/~${r.notchesUpdated}.`,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'salary-grades'] });
    },
    onError: (e: any) => toast({ title: 'Refresh refused', description: e?.message, variant: 'destructive' }),
  });

  const invalidateStructure = [['hr', 'salary-grades'], ['hr', 'salary-levels']];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Salary Structure"
        description={
          threeTier
            ? 'Grades, the levels within them, and the notches on each level.'
            : 'Grades and the notches on each. The notch amount is monthly basic pay.'
        }
        backHref="/administration/hr/compensation"
        actions={
          !hrMastered ? (
            <Button variant="outline" onClick={() => refresh.mutate()} disabled={refresh.isPending}>
              <RefreshCw className={`mr-2 h-4 w-4 ${refresh.isPending ? 'animate-spin' : ''}`} /> Refresh from Payroll
            </Button>
          ) : undefined
        }
      />

      {policy.isLoading ? (
        <div className="flex items-center gap-2 py-8 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" /> Reading the structure settings…
        </div>
      ) : (
        <div
          className={`rounded-md border px-3 py-2 text-sm ${
            hrMastered
              ? 'border-emerald-300 bg-emerald-50 text-emerald-900 dark:border-emerald-700 dark:bg-emerald-950 dark:text-emerald-100'
              : 'border-amber-300 bg-amber-50 text-amber-900 dark:border-amber-700 dark:bg-amber-950 dark:text-amber-100'
          }`}
        >
          {hrMastered ? (
            <>
              This organisation maintains its salary structure <strong>in HR</strong>
              {threeTier ? ' as three tiers (grade, level, notch).' : ' as two tiers (grade, notch).'} Payroll is
              not consulted. Change either under HR policy settings.
            </>
          ) : (
            <>
              This organisation&apos;s salary structure is <strong>defined in Payroll</strong> (Administration →
              HR → Payroll → Grades Setup) and mirrored here read-only. To author it in HR instead, set the
              salary structure source to HR under HR policy settings.
            </>
          )}
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-2">
        {/* ── grades ─────────────────────────────────────────────────────── */}
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Grades</CardTitle>
            <CardDescription>Select a grade to see {threeTier ? 'its levels' : 'its notches'}.</CardDescription>
          </CardHeader>
          <CardContent>
            <ResourceListPanel<SalaryGrade, GradeForm>
              title="grades"
              singular="grade"
              queryKey={['hr', 'salary-grades', 'structure']}
              invalidateKeys={invalidateStructure}
              readOnly={!hrMastered}
              getId={(g) => g.id}
              list={() => salaryGradeService.getAll(true)}
              create={(v) => salaryGradeService.createGrade(gradePayload(v))}
              update={(id, v) => salaryGradeService.updateGrade(id, { id, ...gradePayload(v) })}
              remove={hrMastered ? (id) => salaryGradeService.deleteGrade(id) : undefined}
              columns={[
                {
                  header: 'Grade',
                  cell: (g) => (
                    <button
                      type="button"
                      className={`text-left font-medium hover:underline ${selectedGrade?.id === g.id ? 'text-primary' : ''}`}
                      onClick={() => {
                        setSelectedGrade(g);
                        setSelectedLevel(null);
                      }}
                    >
                      {g.code} — {g.name}
                    </button>
                  ),
                },
                { header: 'Band', cell: (g) => `${money(g.minSalary)} – ${money(g.maxSalary)}`, className: 'text-right tabular-nums' },
                { header: 'Status', cell: (g) => <StatusBadge active={g.isActive} /> },
              ]}
              schema={gradeSchema as any}
              emptyForm={emptyGrade}
              toForm={(g) => ({
                code: g.code,
                name: g.name,
                description: g.description ?? '',
                minSalary: g.minSalary,
                maxSalary: g.maxSalary,
                effectiveDate: g.effectiveDate?.slice(0, 10) ?? '',
                endDate: g.endDate?.slice(0, 10) ?? '',
                isActive: g.isActive,
              })}
              dialogHint={
                threeTier
                  ? 'Add levels to the grade after saving it.'
                  : 'Two-tier: the grade carries one implicit level, created with it. Add notches to the grade after saving it.'
              }
              renderFields={(form) => (
                <>
                  <FieldRow>
                    <TextField form={form} name="code" label="Code" placeholder="S2" required />
                    <TextField form={form} name="name" label="Name" required />
                  </FieldRow>
                  <FieldRow>
                    <NumberField form={form} name="minSalary" label="Band minimum (monthly)" step="0.01" required />
                    <NumberField form={form} name="maxSalary" label="Band maximum (monthly)" step="0.01" required />
                  </FieldRow>
                  <FieldRow>
                    <DateField form={form} name="effectiveDate" label="Effective from" required />
                    <DateField form={form} name="endDate" label="Effective to" />
                  </FieldRow>
                  <TextareaField form={form} name="description" label="Description" rows={2} />
                  <SwitchField form={form} name="isActive" label="Active" />
                </>
              )}
            />
          </CardContent>
        </Card>

        {/* ── levels (three-tier only) ───────────────────────────────────── */}
        {threeTier && (
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">
                Levels{selectedGrade ? ` — ${selectedGrade.code}` : ''}
              </CardTitle>
              <CardDescription>Bands within the grade. Select a level to see its notches.</CardDescription>
            </CardHeader>
            <CardContent>
              {!selectedGrade ? (
                <p className="text-sm text-muted-foreground">Select a grade.</p>
              ) : (
                <ResourceCollectionTab<SalaryLevel, LevelForm>
                  parentId={selectedGrade.id}
                  title="levels"
                  singular="level"
                  queryKey={['hr', 'salary-grades', selectedGrade.id, 'levels', 'all']}
                  invalidateKeys={invalidateStructure}
                  readOnly={!hrMastered}
                  getId={(l) => l.id}
                  list={(gradeId) => salaryGradeService.getLevels(gradeId, true)}
                  create={(gradeId, v) => salaryGradeService.createLevel({ salaryGradeId: gradeId, ...v })}
                  update={(gradeId, id, v) => salaryGradeService.updateLevel(id, { id, salaryGradeId: gradeId, ...v })}
                  remove={hrMastered ? (_g, id) => salaryGradeService.deleteLevel(id) : undefined}
                  columns={[
                    {
                      header: 'Level',
                      cell: (l) => (
                        <button
                          type="button"
                          className={`text-left font-medium hover:underline ${selectedLevel?.id === l.id ? 'text-primary' : ''}`}
                          onClick={() => setSelectedLevel(l)}
                        >
                          {l.code} — {l.name}
                        </button>
                      ),
                    },
                    { header: 'Seq', cell: (l) => l.sequence, className: 'text-right' },
                    { header: 'Band', cell: (l) => `${money(l.minSalary)} – ${money(l.maxSalary)}`, className: 'text-right tabular-nums' },
                    { header: 'Status', cell: (l) => <StatusBadge active={l.isActive} /> },
                  ]}
                  schema={levelSchema as any}
                  emptyForm={{ ...emptyLevel, minSalary: selectedGrade.minSalary, midSalary: selectedGrade.minSalary, maxSalary: selectedGrade.maxSalary }}
                  toForm={(l) => ({
                    code: l.code,
                    name: l.name,
                    minSalary: l.minSalary,
                    midSalary: l.midSalary,
                    maxSalary: l.maxSalary,
                    sequence: l.sequence,
                    isActive: l.isActive,
                  })}
                  renderFields={(form) => (
                    <>
                      <FieldRow>
                        <TextField form={form} name="code" label="Code" placeholder="A" required />
                        <TextField form={form} name="name" label="Name" required />
                      </FieldRow>
                      <FieldRow>
                        <NumberField form={form} name="minSalary" label="Min" step="0.01" required />
                        <NumberField form={form} name="midSalary" label="Mid" step="0.01" required />
                        <NumberField form={form} name="maxSalary" label="Max" step="0.01" required />
                      </FieldRow>
                      <FieldRow>
                        <NumberField form={form} name="sequence" label="Sequence" required />
                        <SwitchField form={form} name="isActive" label="Active" />
                      </FieldRow>
                    </>
                  )}
                />
              )}
            </CardContent>
          </Card>
        )}

        {/* ── notches ─────────────────────────────────────────────────────── */}
        <Card className={threeTier ? 'lg:col-span-2' : ''}>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">
              Notches
              {threeTier
                ? selectedLevel
                  ? ` — ${selectedGrade?.code} / ${selectedLevel.code}`
                  : ''
                : selectedGrade
                  ? ` — ${selectedGrade.code}`
                  : ''}
            </CardTitle>
            <CardDescription>The monthly basic pay at each step.</CardDescription>
          </CardHeader>
          <CardContent>
            {!selectedGrade ? (
              <p className="text-sm text-muted-foreground">Select a grade.</p>
            ) : threeTier && !selectedLevel ? (
              <p className="text-sm text-muted-foreground">Select a level.</p>
            ) : !notchParentLevel ? (
              levels.isLoading ? (
                <div className="flex items-center gap-2 py-4 text-sm text-muted-foreground">
                  <Loader2 className="h-4 w-4 animate-spin" /> Loading…
                </div>
              ) : (
                <p className="text-sm text-amber-700 dark:text-amber-300">
                  This grade has no level to hang notches on. A two-tier grade creates its implicit
                  level with the grade; this one predates that rule — re-save it, or add a level with
                  the structure set to three-tier.
                </p>
              )
            ) : (
              <ResourceCollectionTab<SalaryNotch, NotchForm>
                parentId={notchParentLevel.id}
                title="notches"
                singular="notch"
                queryKey={['hr', 'salary-levels', notchParentLevel.id, 'notches', 'all']}
                invalidateKeys={invalidateStructure}
                readOnly={!hrMastered}
                getId={(n) => n.id}
                list={(levelId) => salaryGradeService.getNotches(levelId, true)}
                create={(levelId, v) => salaryGradeService.createNotch({ salaryLevelId: levelId, ...v })}
                update={(levelId, id, v) => salaryGradeService.updateNotch(id, { id, salaryLevelId: levelId, ...v })}
                remove={hrMastered ? (_l, id) => salaryGradeService.deleteNotch(id) : undefined}
                columns={[
                  { header: 'Notch', cell: (n) => n.notchNumber, className: 'text-right' },
                  { header: 'Monthly', cell: (n) => money(n.salaryAmount), className: 'text-right tabular-nums' },
                  { header: 'Annual', cell: (n) => money(n.salaryAmount * 12), className: 'text-right tabular-nums' },
                  {
                    header: 'Status',
                    cell: (n) => (n.isActive ? <Badge variant="secondary">Active</Badge> : <Badge variant="outline">Retired</Badge>),
                  },
                ]}
                schema={notchSchema as any}
                emptyForm={emptyNotch}
                dialogHint="Notch numbers run 1, 2, 3… without gaps when authored here. Amounts are monthly."
                toForm={(n) => ({ notchNumber: n.notchNumber, salaryAmount: n.salaryAmount, isActive: n.isActive })}
                renderFields={(form) => (
                  <>
                    <FieldRow>
                      <NumberField form={form} name="notchNumber" label="Notch number" required />
                      <NumberField form={form} name="salaryAmount" label="Monthly amount" step="0.01" required />
                    </FieldRow>
                    <SwitchField form={form} name="isActive" label="Active" />
                  </>
                )}
              />
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
