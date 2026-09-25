'use client';

import { useEffect, useState, type ReactNode } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, CalendarPlus, Loader2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { useToast } from '@/components/ui/use-toast';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { useWorkflowRecord } from '@/hooks/useWorkflowRecord';
import { leavePlanService } from '@/services/hr/leave.service';
import type { LeavePlan } from '@/types/hr/leave-request';
import { RelieverChooser, clashLine } from './LeavePlanRelievers';
import {
  CancelPlanDialog,
  ProposePlanDatesDialog,
  SuggestPlanDatesDialog,
} from './LeavePlanDialogs';

const fmt = (d?: string | null) => (d ? d.slice(0, 10) : '—');

function Detail({ label, children, wide }: { label: string; children: ReactNode; wide?: boolean }) {
  return (
    <div className={wide ? 'col-span-2' : undefined}>
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm">{children}</dd>
    </div>
  );
}

/** Who may cancel what — the same rule the server enforces (round 5 lane E5). */
export function canCancelPlan(plan: LeavePlan, canWrite: boolean, isOwner: boolean) {
  const beforeApproval = ['Draft', 'Submitted', 'ChangesSuggested'].includes(plan.status);
  if (canWrite) return beforeApproval || (plan.status === 'Approved' && !plan.raisedLeaveRequestId);
  return isOwner && beforeApproval;
}

/**
 * A leave plan, opened — with everything that can be done to it inside the window.
 *
 * Round 5 lane E4. The stakeholders opened a submitted plan and found themselves in the EDIT form —
 * employee, leave type and dates all editable, a save the server then refused — and had to close it
 * to reach Approve, Reject or Suggest in the row menu behind it. Here:
 *
 * - the plan's details are read-only;
 * - the relievers are the one thing an approver may change (the dates go back to the employee
 *   through *Suggest other dates*), chosen from the employee's own reliever list first;
 * - Approve / Reject come from the workflow engine and show only to the person it is asking;
 * - every other action shows only when it can succeed for this viewer.
 *
 * It is also where the approvals inbox lands (`?planId=`): the line manager who approves first
 * holds no HR permission, and reads this one plan through the engine's assignee check.
 */
export function LeavePlanDetailDialog({
  planId,
  onClose,
  canWrite,
  currentEmployeeId,
  onChanged,
}: {
  planId: string | null;
  onClose: () => void;
  /** The caller holds the leave write tier (the HR desk). */
  canWrite: boolean;
  currentEmployeeId?: string | null;
  /** Refetch the list behind the dialog. */
  onChanged: () => Promise<unknown> | void;
}) {
  const router = useRouter();
  const { toast } = useToast();
  const [busy, setBusy] = useState(false);
  const [reliever, setReliever] = useState<{ id: string | null; label: string | null }>({ id: null, label: null });
  const [second, setSecond] = useState<{ id: string | null; label: string | null }>({ id: null, label: null });
  const [suggestFor, setSuggestFor] = useState<LeavePlan | null>(null);
  const [proposeFor, setProposeFor] = useState<LeavePlan | null>(null);
  const [cancelFor, setCancelFor] = useState<LeavePlan | null>(null);

  const { data: plan, isLoading, isError, error, refetch } = useQuery({
    queryKey: ['hr', 'leave-plans', 'detail', planId],
    queryFn: () => leavePlanService.getById(planId as string),
    enabled: !!planId,
  });

  const refresh = async () => {
    await refetch();
    await onChanged();
  };

  const isOwner = !!plan && !!currentEmployeeId && plan.employeeId === currentEmployeeId;

  const workflow = useWorkflowRecord({
    entityType: 'LeavePlan',
    entityId: planId ?? '',
    entityLabel: 'Leave Plan',
    status: plan?.status ?? 'Draft',
    canSubmit: plan?.status === 'Draft' && (isOwner || canWrite),
    canApproveReject: plan?.status === 'Submitted',
    enabled: !!plan,
    commands: {
      submit: () => leavePlanService.submit(planId as string),
      approve: () => leavePlanService.approve(planId as string),
      reject: async (ctx) => {
        const reason = ctx.comments?.trim();
        if (!reason) throw new Error('Say why the plan is rejected — the employee will see it.');
        return leavePlanService.reject(planId as string, reason);
      },
      afterAction: refresh,
    },
  });

  // The reliever slots start from the plan and follow it when it is saved or refetched.
  useEffect(() => {
    if (!plan) return;
    setReliever({ id: plan.relieverId ?? null, label: plan.relieverName ?? null });
    setSecond({ id: plan.secondRelieverId ?? null, label: plan.secondRelieverName ?? null });
  }, [plan?.id, plan?.relieverId, plan?.secondRelieverId, plan?.relieverName, plan?.secondRelieverName]);

  const decidingNow = workflow.summary?.canCurrentUserApprove === true;
  const canEditRelievers =
    !!plan &&
    ['Submitted', 'ChangesSuggested', 'Approved'].includes(plan.status) &&
    (canWrite || decidingNow);
  const relieversChanged =
    !!plan &&
    (reliever.id !== (plan.relieverId ?? null) || second.id !== (plan.secondRelieverId ?? null));

  const canSuggest = plan?.status === 'Submitted' && decidingNow;
  const canAnswer = plan?.status === 'ChangesSuggested' && (isOwner || canWrite);
  const canRaise = plan?.status === 'Approved' && !plan.raisedLeaveRequestId && (isOwner || canWrite);
  const canCancel = !!plan && canCancelPlan(plan, canWrite, isOwner);

  const run = async (what: string, action: () => Promise<unknown>) => {
    setBusy(true);
    try {
      await action();
      await refresh();
      toast({ title: what });
    } catch (e: any) {
      toast({ title: 'Error', description: e?.message || 'The action failed.', variant: 'destructive' });
    } finally {
      setBusy(false);
    }
  };

  const raiseRequest = (p: LeavePlan) => {
    const q = new URLSearchParams({
      planId: p.id,
      employeeId: p.employeeId,
      leaveTypeId: p.leaveTypeId,
      startDate: p.startDate?.slice(0, 10) ?? '',
      endDate: p.endDate?.slice(0, 10) ?? '',
    });
    if (p.leaveSubTypeId) q.set('leaveSubTypeId', p.leaveSubTypeId);
    if (p.relieverId) q.set('relieverId', p.relieverId);
    if (p.secondRelieverId) q.set('secondRelieverId', p.secondRelieverId);
    router.push(`/hr/leave/requests/new?${q.toString()}`);
  };

  return (
    <>
      <Dialog open={!!planId} onOpenChange={(open) => !open && onClose()}>
        <DialogContent className="sm:max-w-[760px]">
          <DialogHeader>
            <DialogTitle>
              {plan
                ? `${plan.employeeName} — ${plan.leaveTypeName}${plan.leaveSubTypeName ? ` · ${plan.leaveSubTypeName}` : ''}`
                : 'Leave plan'}
            </DialogTitle>
            <DialogDescription>
              {plan
                ? `${fmt(plan.startDate)} to ${fmt(plan.endDate)} · planned by ${plan.plannedByName || '—'}`
                : isLoading
                  ? 'Loading…'
                  : ''}
            </DialogDescription>
          </DialogHeader>

          {isLoading ? (
            <div className="flex items-center justify-center py-10 text-sm text-muted-foreground">
              <Loader2 className="mr-2 h-4 w-4 animate-spin" /> Loading the plan…
            </div>
          ) : isError || !plan ? (
            <p className="py-6 text-sm text-muted-foreground">
              {(error as any)?.status === 403
                ? 'You cannot open this plan — it is not waiting on you, and it is not yours.'
                : (error as any)?.message || 'The plan could not be loaded.'}
            </p>
          ) : (
            <div className="max-h-[60vh] space-y-4 overflow-y-auto py-2">
              <dl className="grid grid-cols-2 gap-x-6 gap-y-3">
                <Detail label="Status">
                  <StatusBadge status={plan.status} />
                </Detail>
                <Detail label="Leave year">{plan.year}</Detail>
                <Detail label="From">{fmt(plan.startDate)}</Detail>
                <Detail label="To">{fmt(plan.endDate)}</Detail>
                {plan.suggestedStartDate && (
                  <Detail label="Suggested dates" wide>
                    {fmt(plan.suggestedStartDate)} to {fmt(plan.suggestedEndDate)}
                    {plan.managerSuggestionNotes ? ` — “${plan.managerSuggestionNotes}”` : ''}
                  </Detail>
                )}
                {plan.notes && (
                  <Detail label="Notes" wide>
                    {plan.notes}
                  </Detail>
                )}
                {plan.status === 'Rejected' && plan.rejectionReason && (
                  <Detail label="Why it was rejected" wide>
                    {plan.rejectionReason}
                  </Detail>
                )}
                {plan.status === 'Cancelled' && (
                  <Detail label="Cancelled" wide>
                    {fmt(plan.cancellationDate)}
                    {plan.cancellationReason ? ` — “${plan.cancellationReason}”` : ''}
                  </Detail>
                )}
                {plan.raisedLeaveRequestId && (
                  <Detail label="Leave request raised from it" wide>
                    <Link
                      href={`/hr/leave/requests/${plan.raisedLeaveRequestId}`}
                      className="text-primary underline-offset-2 hover:underline"
                    >
                      {plan.raisedLeaveRequestNumber ?? 'View the request'}
                    </Link>
                  </Detail>
                )}
              </dl>

              <section className="space-y-3 rounded-md border p-3">
                <h4 className="text-sm font-medium">Relievers</h4>
                {canEditRelievers ? (
                  <>
                    <RelieverChooser
                      label="Reliever"
                      value={reliever.id}
                      valueLabel={reliever.label}
                      onChange={(id, label) => setReliever({ id, label })}
                      roster={plan.relieverRoster}
                      excludeIds={[second.id]}
                      startDate={plan.startDate?.slice(0, 10) ?? ''}
                      endDate={plan.endDate?.slice(0, 10) ?? ''}
                      excludePlanId={plan.id}
                    />
                    <RelieverChooser
                      label="Second reliever"
                      value={second.id}
                      valueLabel={second.label}
                      onChange={(id, label) => setSecond({ id, label })}
                      roster={plan.relieverRoster}
                      excludeIds={[reliever.id]}
                      startDate={plan.startDate?.slice(0, 10) ?? ''}
                      endDate={plan.endDate?.slice(0, 10) ?? ''}
                      excludePlanId={plan.id}
                    />
                    <p className="text-xs text-muted-foreground">
                      Only the relievers can be changed here. To change the dates, suggest other dates —
                      the plan goes back to the employee.
                    </p>
                    {relieversChanged && (
                      <div className="flex gap-2">
                        <Button
                          size="sm"
                          disabled={busy}
                          onClick={() =>
                            run('Relievers saved', () =>
                              leavePlanService.updateRelievers(plan.id, {
                                relieverId: reliever.id,
                                secondRelieverId: second.id,
                              }),
                            )
                          }
                        >
                          {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                          Save relievers
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          disabled={busy}
                          onClick={() => {
                            setReliever({ id: plan.relieverId ?? null, label: plan.relieverName ?? null });
                            setSecond({ id: plan.secondRelieverId ?? null, label: plan.secondRelieverName ?? null });
                          }}
                        >
                          Undo
                        </Button>
                      </div>
                    )}
                  </>
                ) : (
                  <div className="space-y-1 text-sm">
                    <div>Reliever: {plan.relieverName || '—'}</div>
                    <div>Second reliever: {plan.secondRelieverName || '—'}</div>
                    {(plan.relieverClashes ?? []).length > 0 && (
                      <div className="mt-2 space-y-1">
                        {plan.relieverClashes.map((c, i) => (
                          <div key={`${c.source}-${i}`} className="flex items-start gap-1.5 text-xs text-amber-700 dark:text-amber-400">
                            <AlertTriangle className="mt-0.5 h-3 w-3 shrink-0" />
                            {clashLine(c)}
                          </div>
                        ))}
                      </div>
                    )}
                    {plan.status === 'Draft' && (
                      <p className="text-xs text-muted-foreground">A draft&apos;s relievers are changed by editing the draft.</p>
                    )}
                  </div>
                )}
                {plan.relieverRoster && plan.relieverRoster.length > 0 && !canEditRelievers && (
                  <div className="flex flex-wrap gap-1 pt-1">
                    <span className="text-xs text-muted-foreground">On their reliever list:</span>
                    {plan.relieverRoster.map((r) => (
                      <Badge key={r.employeeId} variant="outline" className="text-xs">
                        {r.priority}. {r.name}
                      </Badge>
                    ))}
                  </div>
                )}
              </section>
            </div>
          )}

          <DialogFooter className="flex flex-wrap items-center gap-2 sm:justify-between">
            <div className="flex flex-wrap items-center gap-2">
              {plan && <WorkflowApprovalActions {...workflow.actionProps} />}
              {plan && canSuggest && (
                <Button variant="outline" onClick={() => setSuggestFor(plan)}>
                  Suggest other dates
                </Button>
              )}
              {plan && canAnswer && plan.suggestedStartDate && (
                <Button
                  disabled={busy}
                  onClick={() =>
                    run('Suggested dates accepted — the plan is back for review', () =>
                      leavePlanService.respondToSuggestion(plan.id, {
                        accept: true,
                        startDate: plan.suggestedStartDate ?? null,
                        endDate: plan.suggestedEndDate ?? null,
                        notes: null,
                      }),
                    )
                  }
                >
                  Accept suggested dates
                </Button>
              )}
              {plan && canAnswer && (
                <Button variant="outline" onClick={() => setProposeFor(plan)}>
                  Propose other dates
                </Button>
              )}
              {plan && canRaise && (
                <Button onClick={() => raiseRequest(plan)}>
                  <CalendarPlus className="mr-2 h-4 w-4" /> Raise the leave request
                </Button>
              )}
            </div>
            <div className="flex items-center gap-2">
              {plan && canCancel && (
                <Button variant="destructive" onClick={() => setCancelFor(plan)}>
                  Cancel plan
                </Button>
              )}
              <Button variant="outline" onClick={onClose}>
                Close
              </Button>
            </div>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <SuggestPlanDatesDialog plan={suggestFor} onClose={() => setSuggestFor(null)} onDone={refresh} />
      <ProposePlanDatesDialog plan={proposeFor} onClose={() => setProposeFor(null)} onDone={refresh} />
      <CancelPlanDialog plan={cancelFor} onClose={() => setCancelFor(null)} onDone={refresh} />
    </>
  );
}
