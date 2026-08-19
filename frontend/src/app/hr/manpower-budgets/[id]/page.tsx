'use client';

import { useState } from 'react';
import { useParams } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, Banknote, CheckCircle2, Loader2, Plus, Send, XCircle } from 'lucide-react';
import { toast } from 'sonner';
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
  const qc = useQueryClient();
  const [busy, setBusy] = useState<string | null>(null);
  const [addingLine, setAddingLine] = useState(false);
  const [line, setLine] = useState({ positionId: '', plannedCount: 1, plannedAverageSalary: 0, isCritical: false });

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
