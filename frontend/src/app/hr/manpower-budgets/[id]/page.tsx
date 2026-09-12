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
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
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
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { workflowApiService } from '@/services/workflow-api.service';

const fmtMoney = (v?: number | null) =>
  v == null ? '—' : v.toLocaleString(undefined, { maximumFractionDigits: 0 });
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

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
  const [line, setLine] = useState({ positionId: '', plannedCount: 1, plannedAverageSalary: 0, isCritical: false });
  /**
   * ⚠ The budget and its lines could be created, submitted and approved — and never corrected.
   * A figure typed wrongly could only be fixed by deleting the line and adding it again, and the
   * budget's own totals not at all. Both edits are Write-tier; both DELETES are
   * `HR.ManpowerBudget.Admin`, established by a 403 rather than assumed.
   *
   * ⚠ The budget update is a REPLACE: the DTO names every figure, so the dialog seeds from the
   * budget and sends the whole set. Omitting a field writes a zero over it.
   */
  const [editingBudget, setEditingBudget] = useState<null | {
    periodStartDate: string; periodEndDate: string;
    currentHeadcount: string; currentSalaryCost: string;
    plannedHeadcount: string; plannedSalaryCost: string;
    plannedNewHires: string; plannedTerminations: string;
    salaryBudget: string; benefitsBudget: string; recruitmentBudget: string; trainingBudget: string;
    businessJustification: string;
  }>(null);
  const [editingLine, setEditingLine] = useState<null | {
    id: string; positionTitle: string;
    currentCount: string; currentFilled: string; currentVacant: string;
    currentAverageSalary: string; currentTotalCost: string;
    plannedCount: string; plannedNewPositions: string; plannedEliminations: string;
    plannedAverageSalary: string; priority: string; isCritical: boolean; notes: string;
  }>(null);
  const [removingLine, setRemovingLine] = useState<{ id: string; title: string } | null>(null);
  const [removingBudget, setRemovingBudget] = useState(false);

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
            {isDraft && (
              <Button
                variant="outline"
                onClick={() =>
                  setEditingBudget({
                    periodStartDate: budget.periodStartDate?.slice(0, 10) ?? '',
                    periodEndDate: budget.periodEndDate?.slice(0, 10) ?? '',
                    currentHeadcount: String(budget.currentHeadcount ?? 0),
                    currentSalaryCost: String(budget.currentSalaryCost ?? 0),
                    plannedHeadcount: String(budget.plannedHeadcount ?? 0),
                    plannedSalaryCost: String(budget.plannedSalaryCost ?? 0),
                    plannedNewHires: String(budget.plannedNewHires ?? 0),
                    plannedTerminations: String(budget.plannedTerminations ?? 0),
                    salaryBudget: String(budget.salaryBudget ?? 0),
                    benefitsBudget: String(budget.benefitsBudget ?? 0),
                    recruitmentBudget: String(budget.recruitmentBudget ?? 0),
                    trainingBudget: String(budget.trainingBudget ?? 0),
                    businessJustification: budget.businessJustification ?? '',
                  })
                }
                disabled={busy !== null}
              >
                <Pencil className="mr-2 h-4 w-4" />
                Correct
              </Button>
            )}
            {isDraft && (
              <Button variant="outline" disabled={busy !== null} onClick={() => setRemovingBudget(true)}>
                <Trash2 className="mr-2 h-4 w-4" />
                Delete
              </Button>
            )}
            {isDraft && (
              <Button
                onClick={() => run('submit', () => jobArchitectureService.submitBudget(id), 'Submitted for approval')}
                // ⚠ An empty budget authorises no posts, and the API refuses it. Disabling here
                // means the refusal is explained before it happens rather than after.
                disabled={busy !== null || lineCount === 0}
                title={lineCount === 0 ? 'Add at least one budget line first' : undefined}
              >
                {busy === 'submit' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Send className="mr-2 h-4 w-4" />}
                Submit for approval
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
                <Button
                  variant="outline"
                  onClick={() =>
                    run(
                      'reject',
                      () => jobArchitectureService.rejectBudgetOnWorkflow(id, 'Not approved'),
                      'Budget rejected',
                    )
                  }
                  disabled={busy !== null}
                >
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

      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle>Budget lines</CardTitle>
          {isDraft && (
            <Button variant="outline" size="sm" onClick={() => setAddingLine(true)}>
              <Plus className="mr-2 h-4 w-4" />
              Add line
            </Button>
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
                    <TableCell className="text-right">{l.currentFilled}</TableCell>
                    <TableCell className="text-right font-medium">{l.plannedCount}</TableCell>
                    <TableCell className="text-right">{fmtMoney(l.plannedAverageSalary)}</TableCell>
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
                            setEditingLine({
                              id: l.id,
                              positionTitle: l.positionTitle ?? 'this line',
                              currentCount: String(l.currentCount ?? 0),
                              currentFilled: String(l.currentFilled ?? 0),
                              currentVacant: String(l.currentVacant ?? 0),
                              currentAverageSalary: String(l.currentAverageSalary ?? 0),
                              currentTotalCost: String(l.currentTotalCost ?? 0),
                              plannedCount: String(l.plannedCount ?? 0),
                              plannedNewPositions: String(l.plannedNewPositions ?? 0),
                              plannedEliminations: String(l.plannedEliminations ?? 0),
                              plannedAverageSalary: String(l.plannedAverageSalary ?? 0),
                              priority: l.priority ?? 'Medium',
                              isCritical: Boolean(l.isCritical),
                              notes: l.notes ?? '',
                            })
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

      <Dialog open={addingLine} onOpenChange={setAddingLine}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add a budget line</DialogTitle>
          </DialogHeader>
          <div className="grid gap-4">
            <div className="space-y-2">
              <Label>Position *</Label>
              <Select value={line.positionId} onValueChange={(v) => setLine({ ...line, positionId: v })}>
                <SelectTrigger>
                  <SelectValue placeholder="Choose a position" />
                </SelectTrigger>
                <SelectContent>
                  {(positions ?? []).map((p) => (
                    <SelectItem key={p.id} value={p.id}>
                      {p.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>Posts authorised *</Label>
                <Input
                  type="number"
                  min={0}
                  value={line.plannedCount}
                  onChange={(e) => setLine({ ...line, plannedCount: Number(e.target.value) })}
                />
                {/* The API refuses a budget that establishes fewer posts than are already filled. */}
                <p className="text-xs text-muted-foreground">
                  Cannot be fewer than the number already in post.
                </p>
              </div>
              <div className="space-y-2">
                <Label>Average salary</Label>
                <Input
                  type="number"
                  min={0}
                  value={line.plannedAverageSalary}
                  onChange={(e) => setLine({ ...line, plannedAverageSalary: Number(e.target.value) })}
                />
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAddingLine(false)}>
              Cancel
            </Button>
            <Button
              disabled={!line.positionId || busy !== null}
              onClick={async () => {
                await run(
                  'add the line',
                  () =>
                    jobArchitectureService.addBudgetLine(id, {
                      positionId: line.positionId,
                      plannedCount: line.plannedCount,
                      plannedNewPositions: line.plannedCount,
                      plannedEliminations: 0,
                      plannedAverageSalary: line.plannedAverageSalary,
                      plannedTotalCost: line.plannedAverageSalary * line.plannedCount,
                      currentCount: 0,
                      currentFilled: 0,
                      currentVacant: 0,
                      currentAverageSalary: 0,
                      currentTotalCost: 0,
                      priority: 'Medium',
                      isCritical: line.isCritical,
                    }),
                  'Line added',
                );
                qc.invalidateQueries({ queryKey: ['manpower-budget', id, 'lines'] });
                setAddingLine(false);
                setLine({ positionId: '', plannedCount: 1, plannedAverageSalary: 0, isCritical: false });
              }}
            >
              Add
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ⚠ A REPLACE, not a patch: the DTO names every figure, so the dialog seeds from the budget
          and sends the whole set back. Leaving one out writes a zero over it. */}
      <Dialog open={editingBudget !== null} onOpenChange={(o) => !o && setEditingBudget(null)}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Correct the budget</DialogTitle>
          </DialogHeader>
          {editingBudget && (
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="mbFrom">Period from</Label>
                <Input id="mbFrom" type="date" value={editingBudget.periodStartDate}
                  onChange={(e) => setEditingBudget({ ...editingBudget, periodStartDate: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="mbTo">Period to</Label>
                <Input id="mbTo" type="date" value={editingBudget.periodEndDate}
                  onChange={(e) => setEditingBudget({ ...editingBudget, periodEndDate: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="mbCurHead">Headcount today</Label>
                <Input id="mbCurHead" type="number" min={0} value={editingBudget.currentHeadcount}
                  onChange={(e) => setEditingBudget({ ...editingBudget, currentHeadcount: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="mbPlanHead">Headcount planned</Label>
                <Input id="mbPlanHead" type="number" min={0} value={editingBudget.plannedHeadcount}
                  onChange={(e) => setEditingBudget({ ...editingBudget, plannedHeadcount: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="mbCurCost">Salary cost today</Label>
                <Input id="mbCurCost" type="number" min={0} value={editingBudget.currentSalaryCost}
                  onChange={(e) => setEditingBudget({ ...editingBudget, currentSalaryCost: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="mbPlanCost">Salary cost planned</Label>
                <Input id="mbPlanCost" type="number" min={0} value={editingBudget.plannedSalaryCost}
                  onChange={(e) => setEditingBudget({ ...editingBudget, plannedSalaryCost: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="mbHires">New hires planned</Label>
                <Input id="mbHires" type="number" min={0} value={editingBudget.plannedNewHires}
                  onChange={(e) => setEditingBudget({ ...editingBudget, plannedNewHires: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="mbTerms">Terminations planned</Label>
                <Input id="mbTerms" type="number" min={0} value={editingBudget.plannedTerminations}
                  onChange={(e) => setEditingBudget({ ...editingBudget, plannedTerminations: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="mbSalary">Salary budget</Label>
                <Input id="mbSalary" type="number" min={0} value={editingBudget.salaryBudget}
                  onChange={(e) => setEditingBudget({ ...editingBudget, salaryBudget: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="mbBenefits">Benefits budget</Label>
                <Input id="mbBenefits" type="number" min={0} value={editingBudget.benefitsBudget}
                  onChange={(e) => setEditingBudget({ ...editingBudget, benefitsBudget: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="mbRecruit">Recruitment budget</Label>
                <Input id="mbRecruit" type="number" min={0} value={editingBudget.recruitmentBudget}
                  onChange={(e) => setEditingBudget({ ...editingBudget, recruitmentBudget: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="mbTraining">Training budget</Label>
                <Input id="mbTraining" type="number" min={0} value={editingBudget.trainingBudget}
                  onChange={(e) => setEditingBudget({ ...editingBudget, trainingBudget: e.target.value })} />
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditingBudget(null)}>Cancel</Button>
            <Button
              disabled={busy !== null}
              onClick={async () => {
                if (!editingBudget) return;
                await run(
                  'correct the budget',
                  () =>
                    jobArchitectureService.updateBudget(id, {
                      id,
                      periodStartDate: new Date(editingBudget.periodStartDate).toISOString(),
                      periodEndDate: new Date(editingBudget.periodEndDate).toISOString(),
                      currentHeadcount: Number(editingBudget.currentHeadcount || 0),
                      currentSalaryCost: Number(editingBudget.currentSalaryCost || 0),
                      plannedHeadcount: Number(editingBudget.plannedHeadcount || 0),
                      plannedSalaryCost: Number(editingBudget.plannedSalaryCost || 0),
                      plannedNewHires: Number(editingBudget.plannedNewHires || 0),
                      plannedTerminations: Number(editingBudget.plannedTerminations || 0),
                      salaryBudget: Number(editingBudget.salaryBudget || 0),
                      benefitsBudget: Number(editingBudget.benefitsBudget || 0),
                      recruitmentBudget: Number(editingBudget.recruitmentBudget || 0),
                      trainingBudget: Number(editingBudget.trainingBudget || 0),
                      businessJustification: editingBudget.businessJustification || null,
                    }),
                  'Budget corrected',
                );
                setEditingBudget(null);
              }}
            >
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={editingLine !== null} onOpenChange={(o) => !o && setEditingLine(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Correct the line for {editingLine?.positionTitle}</DialogTitle>
          </DialogHeader>
          {editingLine && (
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="mlPlanned">Posts planned</Label>
                <Input id="mlPlanned" type="number" min={0} value={editingLine.plannedCount}
                  onChange={(e) => setEditingLine({ ...editingLine, plannedCount: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="mlNew">New posts</Label>
                <Input id="mlNew" type="number" min={0} value={editingLine.plannedNewPositions}
                  onChange={(e) => setEditingLine({ ...editingLine, plannedNewPositions: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="mlFilled">Filled today</Label>
                <Input id="mlFilled" type="number" min={0} value={editingLine.currentFilled}
                  onChange={(e) => setEditingLine({ ...editingLine, currentFilled: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="mlAvg">Average salary</Label>
                <Input id="mlAvg" type="number" min={0} value={editingLine.plannedAverageSalary}
                  onChange={(e) => setEditingLine({ ...editingLine, plannedAverageSalary: e.target.value })} />
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditingLine(null)}>Cancel</Button>
            <Button
              disabled={busy !== null}
              onClick={async () => {
                if (!editingLine) return;
                const plannedCount = Number(editingLine.plannedCount || 0);
                const plannedAverageSalary = Number(editingLine.plannedAverageSalary || 0);
                await run(
                  'correct the line',
                  () =>
                    jobArchitectureService.updateBudgetLine(editingLine.id, {
                      id: editingLine.id,
                      currentCount: Number(editingLine.currentCount || 0),
                      currentFilled: Number(editingLine.currentFilled || 0),
                      currentVacant: Number(editingLine.currentVacant || 0),
                      currentAverageSalary: Number(editingLine.currentAverageSalary || 0),
                      currentTotalCost: Number(editingLine.currentTotalCost || 0),
                      plannedCount,
                      plannedNewPositions: Number(editingLine.plannedNewPositions || 0),
                      plannedEliminations: Number(editingLine.plannedEliminations || 0),
                      plannedAverageSalary,
                      // Kept consistent with the figures above rather than left stale.
                      plannedTotalCost: plannedAverageSalary * plannedCount,
                      priority: editingLine.priority as any,
                      isCritical: editingLine.isCritical,
                      notes: editingLine.notes || null,
                    }),
                  'Line corrected',
                );
                qc.invalidateQueries({ queryKey: ['manpower-budget', id, 'lines'] });
                setEditingLine(null);
              }}
            >
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

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
