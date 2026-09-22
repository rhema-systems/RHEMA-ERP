'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, Check, Loader2, PlayCircle, X } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { PermissionGate } from '@/components/hr/common/PermissionGate';
import { orientationProgramService } from '@/services/hr/orientation-program.service';
import {
  ORIENTATION_ENROLLMENT_TRIGGER_OPTIONS,
  ORIENTATION_AUDIENCE_POPULATION_OPTIONS,
} from '@/types/hr/orientation';
import type {
  OrientationProgramDiagnosis,
  OrientationTriggerRunResult,
  OrientationTriggerVerdict,
} from '@/types/hr/orientation';
import { cn } from '@/lib/utils';

/**
 * "Which rules would fire for this employee, and why" — round 4, lane I5.
 *
 * The demo asked how onboarding and orientation are triggered and whether the triggers fire. Before
 * lane I the honest answer was "they do not", and even now the only other way to answer "why was
 * Ama enrolled in this and not that?" is to read the evaluator. This screen runs the same rules the
 * evaluator runs, for one person, and says per programme what happened or would happen — and which
 * onboarding template their plan would come from.
 */

const VERDICT: Record<OrientationTriggerVerdict, { label: string; tone: string }> = {
  Enrolled: { label: 'Enrolled', tone: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-950/50 dark:text-emerald-200' },
  WouldEnrolNow: { label: 'Enrols tonight', tone: 'bg-sky-100 text-sky-900 dark:bg-sky-950/50 dark:text-sky-200' },
  WaitingForDate: { label: 'Waiting for its date', tone: 'bg-sky-100 text-sky-900 dark:bg-sky-950/50 dark:text-sky-200' },
  WaitingOnPrerequisite: { label: 'Waiting on a prerequisite', tone: 'bg-amber-100 text-amber-900 dark:bg-amber-950/50 dark:text-amber-200' },
  OnlyWhenHrEnrols: { label: 'Only when HR enrols', tone: 'bg-amber-100 text-amber-900 dark:bg-amber-950/50 dark:text-amber-200' },
  Excluded: { label: 'Excluded', tone: 'bg-muted text-muted-foreground' },
  WindowLapsed: { label: 'Window lapsed', tone: 'bg-muted text-muted-foreground' },
  NoTriggeringEvent: { label: 'No triggering event', tone: 'bg-muted text-muted-foreground' },
  NotInAudience: { label: 'Not in the audience', tone: 'bg-muted text-muted-foreground' },
  ProgramNotActive: { label: 'Programme not active', tone: 'bg-muted text-muted-foreground' },
};

const fmt = (v?: string | null) =>
  v ? new Date(v).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' }) : '—';
const triggerLabel = (v: string) =>
  ORIENTATION_ENROLLMENT_TRIGGER_OPTIONS.find((o) => o.value === v)?.label ?? v;
const populationLabel = (v: string) =>
  ORIENTATION_AUDIENCE_POPULATION_OPTIONS.find((o) => o.value === v)?.label ?? v;

function Tick({ ok }: { ok: boolean }) {
  return ok ? (
    <Check className="h-4 w-4 text-emerald-600" aria-label="yes" />
  ) : (
    <X className="text-muted-foreground h-4 w-4" aria-label="no" />
  );
}

function ProgrammeCard({ p }: { p: OrientationProgramDiagnosis }) {
  const verdict = VERDICT[p.verdict] ?? { label: p.verdict, tone: 'bg-muted' };
  return (
    <Card>
      <CardHeader className="pb-2">
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div>
            <CardTitle className="text-base">
              <Link href={`/administration/hr/orientation/programs/${p.programId}`} className="hover:underline">
                {p.programTitle}
              </Link>
            </CardTitle>
            <p className="text-muted-foreground text-xs">
              {p.programCode} · {p.programStatus}
            </p>
          </div>
          <span className={cn('rounded-full px-2.5 py-0.5 text-xs font-medium', verdict.tone)}>{verdict.label}</span>
        </div>
        <p className="pt-1 text-sm">{p.explanation}</p>
        {p.missingPrerequisites.length > 0 && (
          <p className="text-muted-foreground text-xs">Mandatory prerequisites not yet completed: {p.missingPrerequisites.join(', ')}</p>
        )}
      </CardHeader>
      {p.rules.length > 0 && (
        <CardContent className="overflow-x-auto pt-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Rule</TableHead>
                <TableHead>Targets</TableHead>
                <TableHead className="text-center">In target</TableHead>
                <TableHead className="text-center">In population</TableHead>
                <TableHead>Trigger</TableHead>
                <TableHead>Why</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {p.rules.map((r) => (
                <TableRow key={r.ruleId} className={cn(!r.isActive && 'opacity-60')}>
                  <TableCell className="align-top">
                    <div className="font-medium">{r.ruleName}</div>
                    <div className="flex gap-1 pt-0.5">
                      {r.isInclusive ? <Badge variant="secondary">Enrols</Badge> : <Badge variant="outline">Excludes</Badge>}
                      {!r.isActive && <Badge variant="outline">Inactive</Badge>}
                    </div>
                  </TableCell>
                  <TableCell className="align-top text-sm">
                    {r.targetName}
                    {r.population !== 'Anyone' && (
                      <div className="text-muted-foreground text-xs">{populationLabel(r.population)} only</div>
                    )}
                  </TableCell>
                  <TableCell className="text-center align-top">
                    <span className="inline-flex"><Tick ok={r.matchesTarget} /></span>
                  </TableCell>
                  <TableCell className="text-center align-top">
                    <span className="inline-flex"><Tick ok={r.matchesPopulation} /></span>
                  </TableCell>
                  <TableCell className="align-top text-sm">
                    {triggerLabel(r.trigger)}
                    {r.enrollmentDelayDays > 0 && (
                      <div className="text-muted-foreground text-xs">+{r.enrollmentDelayDays} day(s)</div>
                    )}
                  </TableCell>
                  <TableCell className="text-muted-foreground align-top text-sm">{r.explanation}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      )}
    </Card>
  );
}

export default function OrientationTriggersPage() {
  const searchParams = useSearchParams();
  const { toast } = useToast();
  const [employeeId, setEmployeeId] = useState<string | null>(searchParams?.get('employeeId') ?? null);
  const [employeeLabel, setEmployeeLabel] = useState<string | null>(null);
  const [sweepPreview, setSweepPreview] = useState<OrientationTriggerRunResult | null>(null);
  const [sweepBusy, setSweepBusy] = useState(false);

  const { data, isFetching, error, refetch } = useQuery({
    queryKey: ['hr', 'orientation', 'trigger-diagnosis', employeeId],
    queryFn: () => orientationProgramService.diagnose(employeeId ?? ''),
    enabled: !!employeeId,
    retry: false,
  });

  const previewSweep = async () => {
    setSweepBusy(true);
    try {
      setSweepPreview(await orientationProgramService.runTriggers(true));
    } catch (e) {
      toast({ title: 'Could not preview the sweep', description: (e as Error).message, variant: 'destructive' });
    } finally {
      setSweepBusy(false);
    }
  };

  const runSweep = async () => {
    setSweepBusy(true);
    try {
      const result = await orientationProgramService.runTriggers(false);
      toast({
        title: `Sweep ran: ${result.enrolled} enrolled`,
        description: `${result.alreadyEnrolled} already enrolled, ${result.excluded} excluded, ${result.waitingOnPrerequisite} waiting on a prerequisite.`,
      });
      setSweepPreview(null);
      if (employeeId) await refetch();
    } catch (e) {
      toast({ title: 'Sweep failed', description: (e as Error).message, variant: 'destructive' });
    } finally {
      setSweepBusy(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Enrollment triggers"
        description="Which orientation rules reach a person, whether they have fired or when they will — and which onboarding template their plan comes from."
        backHref="/hr/orientation"
        actions={
          // The run-now endpoint is on the orientation ADMIN policy; the HR role holds read and write.
          <PermissionGate permissions={['HR.Orientation.Admin']}>
            <Button variant="outline" size="sm" onClick={previewSweep} disabled={sweepBusy}>
              {sweepBusy && !sweepPreview ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <PlayCircle className="mr-2 h-4 w-4" />}
              Run tonight’s sweep now
            </Button>
          </PermissionGate>
        }
      />

      <Card>
        <CardContent className="space-y-2 py-4">
          <p className="text-sm font-medium">Employee</p>
          <div className="max-w-md">
            <EmployeePicker
              value={employeeId}
              initialLabel={employeeLabel}
              onChange={(id, label) => {
                setEmployeeId(id);
                setEmployeeLabel(label);
              }}
            />
          </div>
          <p className="text-muted-foreground text-xs">
            Hire, transfer and promotion rules fire on the event and for 30 days after their date (plus
            any delay); scheduled rules every night; manual and publish rules only when HR enrols the
            audience. Nobody is enrolled twice — not even after a withdrawal.
          </p>
        </CardContent>
      </Card>

      {!employeeId ? (
        <EmptyState title="Choose an employee" description="Their programmes, rules and onboarding template will be explained here." />
      ) : isFetching && !data ? (
        <div className="flex items-center justify-center py-16">
          <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
        </div>
      ) : error ? (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Could not explain the triggers</AlertTitle>
          <AlertDescription>{(error as Error).message}</AlertDescription>
        </Alert>
      ) : data ? (
        <>
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">
                {data.employeeName} <span className="text-muted-foreground font-normal">{data.employeeNumber}</span>
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-2 text-sm">
              <div className="text-muted-foreground flex flex-wrap gap-x-6 gap-y-1">
                <span>Unit: {data.organizationUnitName ?? '—'}</span>
                <span>Level: {data.organizationLevelName ?? '—'}</span>
                <span>Position: {data.positionTitle ?? '—'}</span>
                <span>Location: {data.locationName ?? '—'}</span>
                <span>Employment: {data.employmentType}</span>
                <span>
                  Hire date: {data.hireDate ? fmt(data.hireDate) : 'none — no hire rule can fire until an employment date is set'}
                </span>
              </div>
              <div className="flex flex-wrap items-center gap-2">
                <span className="text-muted-foreground">Populations:</span>
                {data.populations.length === 0 ? (
                  <span className="text-muted-foreground">none</span>
                ) : (
                  data.populations.map((p) => (
                    <Badge key={p} variant="secondary">
                      {populationLabel(p)}
                    </Badge>
                  ))
                )}
                {!data.isActive && <Badge variant="destructive">Inactive — no rule reaches them</Badge>}
              </div>
              {data.recentMovements.length > 0 && (
                <div className="text-muted-foreground">
                  Movements:{' '}
                  {data.recentMovements
                    .map((m) => `${m.movementType} ${m.movementNumber}, effective ${fmt(m.effectiveDate)}`)
                    .join(' · ')}
                </div>
              )}
            </CardContent>
          </Card>

          <div className="space-y-4">
            <h2 className="text-muted-foreground text-sm font-medium">Orientation programmes</h2>
            {data.programs.length === 0 ? (
              <EmptyState
                title="No programme has a rule"
                description="Add audience rules on a programme for anyone to be enrolled automatically."
              />
            ) : (
              data.programs.map((p) => <ProgrammeCard key={p.programId} p={p} />)
            )}
          </div>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Onboarding plan</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3 text-sm">
              {data.onboardingPlanId ? (
                <p>
                  Has a plan{data.onboardingPlanTemplateName ? ` from “${data.onboardingPlanTemplateName}”` : ''}.{' '}
                  <Link href={`/hr/orientation/onboarding/${data.onboardingPlanId}`} className="underline underline-offset-2">
                    Open it
                  </Link>
                  {data.onboardingPlanSelectionReason && (
                    <span className="text-muted-foreground block pt-1">{data.onboardingPlanSelectionReason}</span>
                  )}
                </p>
              ) : (
                <p className="text-muted-foreground">No onboarding plan. A confirmed hire gets one automatically.</p>
              )}
              {data.onboarding && (
                <>
                  <p>
                    For their current placement the template would be{' '}
                    <strong>{data.onboarding.templateName ?? 'none'}</strong> — {data.onboarding.reason}
                  </p>
                  {data.onboarding.candidates.length > 0 && (
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Template</TableHead>
                          <TableHead>Matched on</TableHead>
                          <TableHead className="text-right">Score</TableHead>
                          <TableHead>Excluded</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {data.onboarding.candidates.map((c) => (
                          <TableRow key={c.templateId} className={cn(c.isExcluded && 'opacity-60')}>
                            <TableCell>
                              {c.templateName}
                              {c.isDefault && <Badge variant="outline" className="ml-2">Default</Badge>}
                            </TableCell>
                            <TableCell>{c.matchedTargetName ?? c.matchedOn}</TableCell>
                            <TableCell className="text-right">{c.specificity}</TableCell>
                            <TableCell>{c.isExcluded ? c.excludedBy : '—'}</TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  )}
                </>
              )}
            </CardContent>
          </Card>
        </>
      ) : null}

      <ConfirmationDialog
        open={sweepPreview !== null}
        onOpenChange={(o) => !o && setSweepPreview(null)}
        title={sweepPreview ? `The sweep would enrol ${sweepPreview.enrolled}` : ''}
        description={
          sweepPreview
            ? `${sweepPreview.rulesEvaluated} rule(s) across ${sweepPreview.programsEvaluated} programme(s). ` +
              `${sweepPreview.alreadyEnrolled} already enrolled, ${sweepPreview.excluded} excluded, ` +
              `${sweepPreview.waitingOnPrerequisite} waiting on a prerequisite. Nothing has been written yet.`
            : ''
        }
        confirmText={sweepPreview && sweepPreview.enrolled > 0 ? 'Run it' : 'Close'}
        isLoading={sweepBusy}
        onConfirm={sweepPreview && sweepPreview.enrolled > 0 ? runSweep : () => setSweepPreview(null)}
      />
    </div>
  );
}
