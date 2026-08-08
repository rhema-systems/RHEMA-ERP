'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useRouter, useSearchParams } from 'next/navigation';
import { Info, Save } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { useToast } from '@/hooks/use-toast';
import { dateOffset, formatDate, today } from '@/lib/hr/attendance-format';
import { pipService } from '@/services/hr/pip.service';

/**
 * Raising an improvement plan.
 *
 * Picking the employee calls `prepare`, which fills in their line manager and the default 90-day
 * window — and, when the page was opened from an appraisal (`?appraisalId=`), the score and grade
 * that prompted it, so the plan carries its own justification.
 *
 * The plan is saved as a **draft**. Nothing is served on the employee until it has goals and has
 * been approved through the workflow, which is what the next screen is for.
 */
export default function NewPipPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const appraisalId = searchParams.get('appraisalId') ?? undefined;
  const { toast } = useToast();

  const [employeeId, setEmployeeId] = useState<string | null>(
    searchParams.get('employeeId') ?? null,
  );
  const [form, setForm] = useState({
    startDate: today(),
    endDate: dateOffset(90),
    performanceIssues: '',
    expectedStandards: '',
    improvementActions: '',
    supportProvided: '',
    measurementCriteria: '',
    reviewSchedule: '',
  });

  const prepare = useQuery({
    queryKey: ['hr', 'pip-prepare', employeeId, appraisalId],
    queryFn: () => pipService.prepare(employeeId ?? '', appraisalId),
    enabled: !!employeeId,
    retry: false,
  });

  // The server's default window is the one to use, not the client's guess at it.
  useEffect(() => {
    if (!prepare.data) return;
    setForm((p) => ({
      ...p,
      startDate: prepare.data.startDate.slice(0, 10),
      endDate: prepare.data.endDate.slice(0, 10),
    }));
  }, [prepare.data]);

  const create = useMutation({
    mutationFn: () =>
      pipService.create({
        employeeId: employeeId ?? '',
        appraisalId: prepare.data?.appraisalId ?? appraisalId ?? null,
        supervisorId: prepare.data?.supervisorId ?? '',
        startDate: form.startDate,
        endDate: form.endDate,
        performanceIssues: form.performanceIssues.trim(),
        expectedStandards: form.expectedStandards.trim(),
        improvementActions: form.improvementActions.trim(),
        supportProvided: form.supportProvided.trim() || null,
        measurementCriteria: form.measurementCriteria.trim() || null,
        reviewSchedule: form.reviewSchedule.trim() || null,
      }),
    onSuccess: (id) => {
      toast({
        title: 'Draft plan created',
        description: 'Add the improvement goals, then send it for approval.',
      });
      router.push(`/hr/performance/pip/${id}`);
    },
    onError: (e: Error) =>
      toast({ title: 'Could not create the plan', description: e.message, variant: 'destructive' }),
  });

  const hasSupervisor = Boolean(prepare.data?.supervisorId);
  const canSave =
    !!employeeId &&
    hasSupervisor &&
    form.performanceIssues.trim() &&
    form.expectedStandards.trim() &&
    form.improvementActions.trim() &&
    form.startDate &&
    form.endDate;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New improvement plan"
        description="Saved as a draft. It binds nobody until it has been approved."
        backHref="/hr/performance/pip"
        actions={
          <Button onClick={() => create.mutate()} disabled={!canSave || create.isPending}>
            <Save className="mr-2 h-4 w-4" />
            {create.isPending ? 'Saving…' : 'Save draft'}
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Who the plan is for</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label>Employee</Label>
            <EmployeePicker value={employeeId} onChange={(id) => setEmployeeId(id)} />
          </div>

          {prepare.isError && (
            <p className="text-sm text-red-600 dark:text-red-500">
              {(prepare.error as Error)?.message ?? 'Could not load this employee.'}
            </p>
          )}

          {prepare.data && (
            <div className="grid gap-3 rounded-md border p-4 text-sm sm:grid-cols-2">
              <div>
                <span className="text-muted-foreground">Position</span>
                <div>{prepare.data.employeePosition || '—'}</div>
              </div>
              <div>
                <span className="text-muted-foreground">Department</span>
                <div>{prepare.data.employeeDepartment || '—'}</div>
              </div>
              <div>
                <span className="text-muted-foreground">Supervisor</span>
                <div>{prepare.data.supervisorName || '—'}</div>
              </div>
              {prepare.data.appraisalCycleName && (
                <div>
                  <span className="text-muted-foreground">From appraisal</span>
                  <div>
                    {prepare.data.appraisalCycleName}
                    {prepare.data.appraisalScore != null
                      ? ` · ${prepare.data.appraisalScore}`
                      : ''}
                    {prepare.data.appraisalGradeLabel
                      ? ` (${prepare.data.appraisalGradeLabel})`
                      : ''}
                  </div>
                </div>
              )}
            </div>
          )}

          {prepare.data && !hasSupervisor && (
            <Alert variant="destructive">
              <Info className="h-4 w-4" />
              <AlertTitle>No line manager on record</AlertTitle>
              <AlertDescription>
                A plan needs a named supervisor to own it. Set this employee&apos;s manager on their
                record first.
              </AlertDescription>
            </Alert>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">The plan</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="pip-start">Starts</Label>
              <Input
                id="pip-start"
                type="date"
                value={form.startDate}
                onChange={(e) => setForm((p) => ({ ...p, startDate: e.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="pip-end">Ends</Label>
              <Input
                id="pip-end"
                type="date"
                value={form.endDate}
                onChange={(e) => setForm((p) => ({ ...p, endDate: e.target.value }))}
              />
              <p className="text-xs text-muted-foreground">
                Runs to {formatDate(form.endDate)}. It can be extended later without closing.
              </p>
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="pip-issues">
              Performance issues<span className="ml-0.5 text-red-500">*</span>
            </Label>
            <Textarea
              id="pip-issues"
              rows={4}
              maxLength={2000}
              value={form.performanceIssues}
              onChange={(e) => setForm((p) => ({ ...p, performanceIssues: e.target.value }))}
              placeholder="What has fallen short, with specifics and dates."
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="pip-standards">
              Expected standards<span className="ml-0.5 text-red-500">*</span>
            </Label>
            <Textarea
              id="pip-standards"
              rows={3}
              maxLength={2000}
              value={form.expectedStandards}
              onChange={(e) => setForm((p) => ({ ...p, expectedStandards: e.target.value }))}
              placeholder="What the role requires — the bar being measured against."
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="pip-actions">
              Improvement actions<span className="ml-0.5 text-red-500">*</span>
            </Label>
            <Textarea
              id="pip-actions"
              rows={3}
              maxLength={2000}
              value={form.improvementActions}
              onChange={(e) => setForm((p) => ({ ...p, improvementActions: e.target.value }))}
              placeholder="What the employee will do differently."
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="pip-support">Support provided</Label>
            <Textarea
              id="pip-support"
              rows={3}
              maxLength={2000}
              value={form.supportProvided}
              onChange={(e) => setForm((p) => ({ ...p, supportProvided: e.target.value }))}
              placeholder="Coaching, training, cover, adjusted workload — what the organisation is doing."
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="pip-measure">Measurement criteria</Label>
            <Textarea
              id="pip-measure"
              rows={3}
              maxLength={2000}
              value={form.measurementCriteria}
              onChange={(e) => setForm((p) => ({ ...p, measurementCriteria: e.target.value }))}
              placeholder="How success will be judged at the end of the period."
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="pip-schedule">Review schedule</Label>
            <Textarea
              id="pip-schedule"
              rows={2}
              maxLength={2000}
              value={form.reviewSchedule}
              onChange={(e) => setForm((p) => ({ ...p, reviewSchedule: e.target.value }))}
              placeholder="e.g. fortnightly, Thursdays at 10."
            />
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
