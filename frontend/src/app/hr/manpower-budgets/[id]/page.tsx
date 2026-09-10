'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, Banknote, CheckCircle2, Loader2, Pencil, Plus, Send, Trash2, XCircle } from 'lucide-react';
import { toast } from 'sonner';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { PlanningBaselinePanel } from '@/components/hr/manpower/PlanningBaselinePanel';
import {
  BudgetLineDialog,
  budgetLineFormFromLine,
  budgetLinePayload,
  emptyBudgetLineForm,
  type BudgetLineFormState,
} from '@/components/hr/manpower/BudgetLineDialog';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { workflowApiService } from '@/services/workflow-api.service';

const fmtMoney = (v?: number | null) =>
  v == null ? '—' : v.toLocaleString(undefined, { maximumFractionDigits: 0 });
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const SOURCE_LABEL: Record<string, string> = {
  Notch: 'from the notch',
  LevelMidpoint: 'level mid-point',
  GradeMinimum: 'grade minimum',
  Manual: 'entered by hand',
};

const STATUS_TONE: Record<string, string> = {
  Draft: 'bg-slate-100 text-slate-700',
  Submitted: 'bg-amber-100 text-amber-800',
  UnderReview: 'bg-amber-100 text-amber-800',
  Approved: 'bg-emerald-100 text-emerald-800',
  Active: 'bg-emerald-100 text-emerald-800',
  Rejected: 'bg-rose-100 text-rose-800',
  Closed: 'bg-slate-100 text-slate-500',
};

export default function ManpowerBudgetDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const qc = useQueryClient();
  const [busy, setBusy] = useState<string | null>(null);
  const [addingLine, setAddingLine] = useState(false);
  const [line, setLine] = useState<BudgetLineFormState>(() => emptyBudgetLineForm());
  /**
   * ⚠ The budget and its lines could be created, submitted and approved — and never corrected.
   * A figure typed wrongly could only be fixed by deleting the line and adding it again, and the
   * budget's own totals not at all. Both edits are Write-tier; both DELETES are
   * `HR.ManpowerBudget.Admin`, established by a 403 rather than assumed.
   *
   * Round 2b, lane R1: the budget's own correction moved off this page. The "Correct" dialog here
   * showed 12 of the create form's 17 fields and — because the update is a REPLACE — silently
   * zeroed the five it did not show (promotions, transfers, actual spend, and it could not change
   * the year or unit at all). The edit page shares one form with the create page, so what can be
   * typed on creation can be corrected afterwards. Line corrections stay here.
   */
  const [rejectReason, setRejectReason] = useState<string | null>(null);
  // Round 2b, R3: one dialog for add and correct (`BudgetLineDialog`), with the salary read
  // from the scale — grade pre-selected from the position, notch chosen, figure filled and its
  // source recorded. Every stored field is now editable (quarter, target date, priority, notes).
  const [editingLine, setEditingLine] = useState<null | { id: string; positionTitle: string; form: BudgetLineFormState }>(null);
  const [removingLine, setRemovingLine] = useState<{ id: string; title: string } | null>(null);
  const [removingBudget, setRemovingBudget] = useState(false);
  // R4a: pull the posts of the budget's subtree onto it; lines already here are never touched.
  const [addingFromEstablishment, setAddingFromEstablishment] = useState(false);

  const { data: budget, isLoading } = useQuery({
    queryKey: ['manpower-budget', id],
    queryFn: () => jobArchitectureService.getBudget(id),
    enabled: !!id,
  });

  const { data: lines } = useQuery({
    queryKey: ['manpower-budget', id, 'lines'],
    queryFn: () => jobArchitectureService.getBudgetLines(id),
    enabled: !!id,
  });

  /**
   * ⚠ FR-HR-135 is a THREE-step chain, so "approved" is not a state one person reaches.
   * `canCurrentUserApprove` answers for the step in front of *this* caller; a department head who
   * has already approved sees nothing further, and the budget stays Submitted until the Managing
   * Director acts. Rendering the button on a permission would show it to all three at once.
   */
  const { data: workflow } = useQuery({
    queryKey: ['manpower-budget', id, 'workflow'],
    queryFn: () => workflowApiService.getWorkflowEntitySummary('ManpowerBudget', id),
    enabled: !!id,
  });

  const { data: positions } = useQuery({
    queryKey: ['positions', 'all'],
    queryFn: () => employeePositionService.getAll(),
  });

  // The planning baseline, recomputed live for the budget's own unit and period (R2). It is
  // labelled "as of today": a budget written in January and read in November will differ from
  // it, and the live figure is the true one.
  const baselineArgs = budget?.organizationUnitId && budget.periodStartDate && budget.periodEndDate
    ? { unit: budget.organizationUnitId, start: budget.periodStartDate.slice(0, 10), end: budget.periodEndDate.slice(0, 10) }
    : null;
  const baseline = useQuery({
    queryKey: ['manpower-baseline', baselineArgs?.unit, baselineArgs?.start, baselineArgs?.end],
    queryFn: () => jobArchitectureService.getPlanningBaseline(baselineArgs!.unit, baselineArgs!.start, baselineArgs!.end),
    enabled: !!baselineArgs,
    staleTime: 60 * 1000,
  });

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ['manpower-budget', id] });
    qc.invalidateQueries({ queryKey: ['manpower-budgets'] });
  };

  const run = async (label: string, fn: () => Promise<unknown>, success: string) => {
    setBusy(label);
    try {
      await fn();
      toast.success(success);
      refresh();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : `Could not ${label}`);
    } finally {
      setBusy(null);
    }
  };

  if (isLoading || !budget) {
    return (
      <div className="flex items-center justify-center py-24 text-muted-foreground">
        <Loader2 className="mr-2 h-5 w-5 animate-spin" />
        Loading…
      </div>
    );
  }

  const status = (budget.statusName ?? budget.status) as string;
  const isDraft = status === 'Draft';
  const isRejected = status === 'Rejected';
  // A rejected budget is the author's again: correct it and resubmit (the server allows both).
  const canEdit = isDraft || isRejected;
  const canApprove = workflow?.canCurrentUserApprove === true;
  const lineCount = lines?.length ?? 0;

  return (
    <div className="space-y-6">
      <PageHeader
        title={`${budget.organizationUnitName || 'Manpower budget'} ${budget.fiscalYear}`}
        description={budget.budgetNumber}
        backHref="/hr/manpower-budgets"
        actions={
          <div className="flex flex-wrap gap-2">
            {canEdit && (
              <Button
                variant="outline"
                onClick={() => router.push(`/hr/manpower-budgets/${id}/edit`)}
                disabled={busy !== null}
              >
                <Pencil className="mr-2 h-4 w-4" />
                Edit
              </Button>
            )}
            {isDraft && (
              <Button variant="outline" disabled={busy !== null} onClick={() => setRemovingBudget(true)}>
                <Trash2 className="mr-2 h-4 w-4" />
                Delete
              </Button>
            )}
            {canEdit && (
              <Button
                onClick={() =>
                  run('submit', () => jobArchitectureService.submitBudget(id),
                    isRejected ? 'Resubmitted for approval' : 'Submitted for approval')
                }
                // ⚠ An empty budget authorises no posts, and the API refuses it. Disabling here
                // means the refusal is explained before it happens rather than after.
                disabled={busy !== null || lineCount === 0}
                title={lineCount === 0 ? 'Add at least one budget line first' : undefined}
              >
                {busy === 'submit' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Send className="mr-2 h-4 w-4" />}
                {isRejected ? 'Resubmit for approval' : 'Submit for approval'}
              </Button>
            )}
            {canApprove && (
              <>
                <Button
                  onClick={() =>
                    run('approve', () => jobArchitectureService.approveBudgetOnWorkflow(id), 'Approval recorded')
                  }
                  disabled={busy !== null}
                >
                  <CheckCircle2 className="mr-2 h-4 w-4" />
                  Approve
                </Button>
                {/* The reason is stored (area 17 slice 7) — but this button sent the constant
                    "Not approved" for every rejection, so the column never held one. */}
                <Button variant="outline" onClick={() => setRejectReason('')} disabled={busy !== null}>
                  <XCircle className="mr-2 h-4 w-4" />
                  Reject
                </Button>
              </>
            )}
          </div>
        }
      />

      <div className="flex flex-wrap items-center gap-2">
        <Badge className={STATUS_TONE[status] ?? 'bg-slate-100 text-slate-700'}>{status}</Badge>
        {workflow?.hasActiveInstance && workflow.currentStepName && (
          <Badge variant="outline">Awaiting: {workflow.currentStepName}</Badge>
        )}
        {budget.approvedByName && (
          <Badge variant="outline">
            Approved by {budget.approvedByName} · {fmtDate(budget.approvalDate)}
          </Badge>
        )}
      </div>

      {/* A rejection that cannot explain itself is unactionable — this is why the reason is stored. */}
      {budget.rejectionReason && (
        <Card className="border-rose-200 bg-rose-50">
          <CardContent className="flex gap-3 pt-6">
            <AlertTriangle className="h-5 w-5 shrink-0 text-rose-700" />
            <div>
              <div className="text-sm font-medium text-rose-900">This budget was rejected</div>
              <p className="text-sm text-rose-800">{budget.rejectionReason}</p>
            </div>
          </CardContent>
        </Card>
      )}

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Stat label="Posts authorised" value={budget.plannedHeadcount} hint={`${budget.currentHeadcount} today`} />
        <Stat label="New hires planned" value={budget.plannedNewHires} />
        <Stat label="Salary budget" value={fmtMoney(budget.salaryBudget)} />
        <Stat label="Total budget" value={fmtMoney(budget.totalBudget)} hint="salary + benefits + recruitment + training" />
      </div>

      {/* Round 2b, lane R1: "the full details specified on the creation form don't display in the
          edit view". Every field the form takes is shown here, grouped the way the form groups
          them. ⚠ `actualSpent` and `variance` are still NOT shown — nothing writes them, and a
          permanent zero presented as a variance would be inventing news. */}
      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader><CardTitle>Scope</CardTitle></CardHeader>
          <CardContent>
            <dl className="grid grid-cols-2 gap-x-4 gap-y-3 text-sm">
              <Field label="Fiscal year" value={budget.fiscalYear} />
              <Field label="Period" value={`${fmtDate(budget.periodStartDate)} – ${fmtDate(budget.periodEndDate)}`} />
              <Field label="Organisation unit" value={budget.organizationUnitName ?? '—'} />
              <Field label="Organisation level" value={budget.organizationLevelName ?? '—'} />
            </dl>
          </CardContent>
        </Card>
        <Card>
          <CardHeader><CardTitle>Headcount</CardTitle></CardHeader>
          <CardContent>
            <dl className="grid grid-cols-3 gap-x-4 gap-y-3 text-sm">
              <Field label="Current" value={budget.currentHeadcount} />
              <Field label="Planned" value={budget.plannedHeadcount} />
              <Field label="New hires" value={budget.plannedNewHires} />
              <Field label="Terminations" value={budget.plannedTerminations} />
              <Field label="Promotions" value={budget.plannedPromotions} />
              <Field label="Transfers" value={budget.plannedTransfers} />
            </dl>
          </CardContent>
        </Card>
        <Card>
          <CardHeader><CardTitle>Money</CardTitle></CardHeader>
          <CardContent>
            <dl className="grid grid-cols-2 gap-x-4 gap-y-3 text-sm">
              <Field label="Current salary cost" value={fmtMoney(budget.currentSalaryCost)} />
              <Field label="Planned salary cost" value={fmtMoney(budget.plannedSalaryCost)} />
              <Field label="Salary budget" value={fmtMoney(budget.salaryBudget)} />
              <Field label="Benefits budget" value={fmtMoney(budget.benefitsBudget)} />
              <Field label="Recruitment budget" value={fmtMoney(budget.recruitmentBudget)} />
              <Field label="Training budget" value={fmtMoney(budget.trainingBudget)} />
              <Field label="Total budget" value={<span className="font-semibold">{fmtMoney(budget.totalBudget)}</span>} />
            </dl>
          </CardContent>
        </Card>
        <Card>
          <CardHeader><CardTitle>Justification and decision</CardTitle></CardHeader>
          <CardContent className="space-y-3 text-sm">
            <div>
              <div className="text-muted-foreground">Business justification</div>
              <p className="mt-1 whitespace-pre-wrap">{budget.businessJustification || '—'}</p>
            </div>
            <dl className="grid grid-cols-2 gap-x-4 gap-y-3">
              <Field label="Approved by" value={budget.approvedByName ?? '—'} />
              <Field label="Approved on" value={fmtDate(budget.approvalDate)} />
              {workflow?.hasActiveInstance && workflow.currentStepName && (
                <Field label="Awaiting" value={workflow.currentStepName} />
              )}
            </dl>
          </CardContent>
        </Card>
      </div>

      <PlanningBaselinePanel
        baseline={baseline.data}
        loading={!!baselineArgs && baseline.isLoading}
        error={baseline.isError ? 'The planning baseline could not be loaded.' : undefined}
        idle={!baselineArgs}
        title="Planning baseline, as of today"
        onRefresh={baselineArgs ? () => baseline.refetch() : undefined}
        compareTo={{ currentHeadcount: budget.currentHeadcount, plannedTerminations: budget.plannedTerminations }}
      />

      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle>Budget lines</CardTitle>
          {canEdit && (
            <div className="flex gap-2">
              {budget.organizationUnitId && (
                <Button variant="outline" size="sm" onClick={() => setAddingFromEstablishment(true)} disabled={busy !== null}>
                  <Banknote className="mr-2 h-4 w-4" />
                  Add posts from the establishment
                </Button>
              )}
              <Button variant="outline" size="sm" onClick={() => setAddingLine(true)}>
                <Plus className="mr-2 h-4 w-4" />
                Add line
              </Button>
            </div>
          )}
        </CardHeader>
        <CardContent>
          {lineCount === 0 ? (
            <EmptyState
              icon={Banknote}
              title="No budget lines"
              description="A budget with no lines authorises no posts, so it cannot be submitted."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Position</TableHead>
                  <TableHead>On the scale</TableHead>
                  <TableHead className="text-right">Filled</TableHead>
                  <TableHead className="text-right">Planned</TableHead>
                  <TableHead className="text-right">Average salary</TableHead>
                  <TableHead className="text-right">Total cost</TableHead>
                  <TableHead>Priority</TableHead>
                  {isDraft && <TableHead />}
                </TableRow>
              </TableHeader>
              <TableBody>
                {(lines ?? []).map((l) => (
                  <TableRow key={l.id}>
                    <TableCell className="font-medium">
                      {l.positionTitle}
                      {l.isCritical && (
                        <Badge className="ml-2 bg-rose-100 text-rose-800">Critical</Badge>
                      )}
                    </TableCell>
                    <TableCell className="text-sm">
                      {l.salaryGradeCode
                        ? [l.salaryGradeCode, l.salaryLevelCode && l.salaryLevelCode !== l.salaryGradeCode ? `level ${l.salaryLevelCode}` : null, l.salaryNotchNumber ? `notch ${l.salaryNotchNumber}` : null].filter(Boolean).join(' · ')
                        : <span className="text-muted-foreground">—</span>}
                    </TableCell>
                    <TableCell className="text-right">{l.currentFilled}</TableCell>
                    <TableCell className="text-right font-medium">{l.plannedCount}</TableCell>
                    <TableCell className="text-right">
                      {fmtMoney(l.plannedAverageSalary)}
                      <div className="text-xs text-muted-foreground">{SOURCE_LABEL[l.plannedSalarySource] ?? l.plannedSalarySourceName}</div>
                    </TableCell>
                    <TableCell className="text-right">{fmtMoney(l.plannedTotalCost)}</TableCell>
                    <TableCell>
                      <Badge variant="outline">{l.priority}</Badge>
                    </TableCell>
                    {isDraft && (
                      <TableCell className="text-right">
                        <Button
                          size="sm"
                          variant="ghost"
                          title="Correct this line"
                          onClick={() =>
                            setEditingLine({ id: l.id, positionTitle: l.positionTitle ?? 'this line', form: budgetLineFormFromLine(l) })
                          }
                        >
                          <Pencil className="h-4 w-4" />
                        </Button>
                        <Button
                          size="sm"
                          variant="ghost"
                          title="Remove this line (Admin)"
                          onClick={() => setRemovingLine({ id: l.id, title: l.positionTitle ?? 'this line' })}
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </TableCell>
                    )}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
          {lineCount > 0 && (
            <p className="mt-4 text-xs text-muted-foreground">
              On final approval, each line&apos;s planned count becomes the position&apos;s approved
              establishment.
            </p>
          )}
        </CardContent>
      </Card>

      <BudgetLineDialog
        open={addingLine}
        onOpenChange={(o) => { setAddingLine(o); if (!o) setLine(emptyBudgetLineForm()); }}
        title="Add a budget line"
        positions={positions ?? []}
        value={line}
        onChange={setLine}
        busy={busy !== null}
        onSubmit={async () => {
          await run('add the line', () => jobArchitectureService.addBudgetLine(id, budgetLinePayload(line)), 'Line added');
          qc.invalidateQueries({ queryKey: ['manpower-budget', id, 'lines'] });
          setAddingLine(false);
          setLine(emptyBudgetLineForm());
        }}
      />

      <Dialog open={rejectReason !== null} onOpenChange={(o) => !o && setRejectReason(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reject this budget</DialogTitle>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="mbRejectReason">Why it is being sent back *</Label>
            <Textarea
              id="mbRejectReason"
              rows={4}
              value={rejectReason ?? ''}
              onChange={(e) => setRejectReason(e.target.value)}
              placeholder="What the budget holder needs to change before resubmitting."
            />
            <p className="text-xs text-muted-foreground">
              The reason is kept on the budget so the holder can act on it.
            </p>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRejectReason(null)}>Cancel</Button>
            <Button
              variant="destructive"
              disabled={busy !== null || !(rejectReason ?? '').trim()}
              onClick={async () => {
                const reason = (rejectReason ?? '').trim();
                await run('reject', () => jobArchitectureService.rejectBudgetOnWorkflow(id, reason), 'Budget rejected');
                setRejectReason(null);
              }}
            >
              Reject
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {editingLine && (
        <BudgetLineDialog
          open
          onOpenChange={(o) => !o && setEditingLine(null)}
          title={`Correct the line for ${editingLine.positionTitle}`}
          positions={positions ?? []}
          value={editingLine.form}
          onChange={(form) => setEditingLine({ ...editingLine, form })}
          busy={busy !== null}
          positionLocked
          onSubmit={async () => {
            await run(
              'correct the line',
              () => jobArchitectureService.updateBudgetLine(editingLine.id, { id: editingLine.id, ...budgetLinePayload(editingLine.form) }),
              'Line corrected',
            );
            qc.invalidateQueries({ queryKey: ['manpower-budget', id, 'lines'] });
            setEditingLine(null);
          }}
        />
      )}

      <ConfirmationDialog
        open={removingLine !== null}
        onOpenChange={(o) => !o && setRemovingLine(null)}
        title={`Remove the line for ${removingLine?.title ?? 'this position'}?`}
        description="The posts it authorises leave the budget with it. Admin-tier: the correction above is the usual remedy."
        confirmText="Remove"
        variant="destructive"
        onConfirm={async () => {
          if (!removingLine) return;
          await run('remove the line', () => jobArchitectureService.deleteBudgetLine(removingLine.id), 'Line removed');
          qc.invalidateQueries({ queryKey: ['manpower-budget', id, 'lines'] });
          setRemovingLine(null);
        }}
      />

      <ConfirmationDialog
        open={addingFromEstablishment}
        onOpenChange={setAddingFromEstablishment}
        title="Add posts from the establishment?"
        description="Every post in the unit and the units under it that is not yet on this budget gets a line: posts authorised from the establishment, new posts from the gap and the exits due, the salary from the scale. Lines already here are left exactly as they are."
        confirmText="Add posts"
        onConfirm={async () => {
          await run('add posts from the establishment', async () => {
            const r = await jobArchitectureService.addLinesFromEstablishment(id, true);
            toast.message(`${r.added} added · ${r.alreadyOnBudget} already on the budget`);
          }, 'Posts added');
          qc.invalidateQueries({ queryKey: ['manpower-budget', id, 'lines'] });
          setAddingFromEstablishment(false);
        }}
      />

      <ConfirmationDialog
        open={removingBudget}
        onOpenChange={setRemovingBudget}
        title="Delete this budget?"
        description="Every line goes with it. Only a draft can be deleted, and only by an administrator — a budget that has been submitted is part of the approval record."
        confirmText="Delete"
        variant="destructive"
        onConfirm={async () => {
          await run('delete the budget', () => jobArchitectureService.deleteBudget(id), 'Budget deleted');
          router.push('/hr/manpower-budgets');
        }}
      />
    </div>
  );
}

function Field({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div>
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="mt-0.5">{value}</dd>
    </div>
  );
}

function Stat({ label, value, hint }: { label: string; value: React.ReactNode; hint?: string }) {
  return (
    <Card>
      <CardContent className="pt-6">
        <div className="text-sm text-muted-foreground">{label}</div>
        <div className="mt-2 text-2xl font-semibold">{value}</div>
        {hint && <div className="mt-1 text-xs text-muted-foreground">{hint}</div>}
      </CardContent>
    </Card>
  );
}
