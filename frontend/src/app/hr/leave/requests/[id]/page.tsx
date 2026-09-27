'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Pencil, Ban, CheckCheck, CalendarClock, CornerUpLeft, CircleCheck, PhoneCall } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowTabContent, WorkflowTabTrigger } from '@/components/workflow/WorkflowRecordTab';
import { useWorkflowRecord } from '@/hooks/useWorkflowRecord';
import { useAuth } from '@/hooks/use-auth';
import { leaveService } from '@/services/hr/leave.service';
import { LeaveAttachmentsPanel } from '@/components/hr/leave/LeaveAttachmentsPanel';
import {
  SuggestDatesDialog,
  RespondToSuggestionDialog,
  RescheduleDialog,
  RecallDialog,
  SuggestedDatesPanel,
  RescheduleTrailPanel,
  RecallPanel,
} from '@/components/hr/leave/LeaveDateChangeDialogs';
import { useLeavePermissions } from '@/components/hr/leave/use-leave-permissions';
import { MedicalBoardLinkPanel } from '@/components/hr/leave/MedicalBoardLinkPanel';
import { MatchesApprovedPlanBadge } from '@/components/hr/leave/MatchesApprovedPlanBadge';
import { SplitAbsencePanel } from '@/components/hr/leave/SplitAbsencePanel';

function InfoRow({ label, value }: { label: string; value?: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5 py-1.5">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm">{value ?? '—'}</span>
    </div>
  );
}

function InfoCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-base">{title}</CardTitle>
      </CardHeader>
      <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">{children}</CardContent>
    </Card>
  );
}

export default function LeaveRequestDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { user } = useAuth();
  const { canWrite } = useLeavePermissions();

  const [action, setAction] = useState<null | 'cancel' | 'close'>(null);
  const [dateDialog, setDateDialog] = useState<null | 'suggest' | 'respond' | 'reschedule' | 'recall'>(
    null,
  );
  const [busy, setBusy] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [closureNotes, setClosureNotes] = useState('');
  // The first day back, as the confirmer states it. Blank keeps the employee's report, or closes
  // the leave as over when nobody reported.
  const [resumedOn, setResumedOn] = useState('');

  const { data: r, isLoading, isError } = useQuery({
    queryKey: ['hr', 'leave-requests', id],
    queryFn: () => leaveService.getById(id),
    enabled: !!id,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'leave-requests'] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'leave-balances'] });
  };

  /**
   * The generic workflow engine owns submit/approve/reject. The backend endpoints are thin
   * pass-throughs to LeaveService, which drives the engine and then applies the resulting
   * status via LeaveRequestWorkflowStatusAdapter — so this page never sets a status itself,
   * it just refetches.
   */
  const workflow = useWorkflowRecord({
    recallPrompt: 'reason',
    entityType: 'LeaveRequest',
    entityId: id,
    entityLabel: 'Leave Request',
    entityNumber: r?.requestNumber,
    status: r?.status ?? 'Draft',
    canSubmit: r?.status === 'Draft',
    canApproveReject: r?.status === 'Pending',
    enabled: !!r,
    commands: {
      submit: () => leaveService.submit(id),
      approve: (ctx) =>
        leaveService.approve(id, {
          approvedBy: (user?.id as string) ?? '',
          approvalNotes: ctx.comments || null,
          comments: ctx.comments || null,
        }),
      reject: (ctx) => leaveService.reject(id, { rejectionReason: ctx.comments || 'Rejected' }),
      afterAction: refresh,
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

  /**
   * The four date-change acts. Each one returns the updated request, so the only thing left to do
   * is refetch — including the workflow panel, because sending back CANCELS the live approval and
   * both answering and rescheduling START A NEW ONE. A stale panel would show an approval that is
   * no longer the one in force.
   *
   * ⚠ Recall is the exception: it deliberately does NOT touch the approval, because the leave was
   * validly granted and then interrupted. The panel is refetched anyway — it costs one request and
   * makes the rule "always refresh both" instead of "refresh both except here".
   */
  const runDateChange = async (
    what: 'suggest' | 'respond' | 'reschedule' | 'recall',
    run: () => Promise<unknown>,
  ) => {
    setBusy(true);
    try {
      await run();
      await refresh();
      await workflow.refresh();
      toast({
        title:
          what === 'suggest'
            ? 'Sent back'
            : what === 'respond'
              ? 'Answered'
              : what === 'recall'
                ? 'Recalled'
                : 'Moved',
        description:
          what === 'suggest'
            ? 'The employee has been asked to accept or counter the dates.'
            : what === 'respond'
              ? 'The request is back with the approver on the settled dates.'
              : what === 'recall'
                ? 'The leave has been shortened and the remaining days are back on the balance.'
                : 'The dates have moved and the request is back in approval.',
      });
      setDateDialog(null);
    } catch (e: any) {
      toast({ title: 'Error', description: e?.message || 'Action failed.', variant: 'destructive' });
    } finally {
      setBusy(false);
    }
  };

  const confirmObservance = async () => {
    setBusy(true);
    try {
      await leaveService.confirmObservance(id);
      await refresh();
      toast({ title: 'Confirmed', description: 'Recorded that this leave is still going ahead.' });
    } catch (e: any) {
      toast({ title: 'Error', description: e?.message || 'Action failed.', variant: 'destructive' });
    } finally {
      setBusy(false);
    }
  };

  const runAction = async () => {
    if (!action) return false;
    setBusy(true);
    try {
      if (action === 'cancel') {
        // Cancelling granted leave takes something back, so the server insists on a reason.
        if (r?.viewerActions?.cancelNeedsReason && !cancelReason.trim()) {
          toast({ title: 'A reason is required', variant: 'destructive' });
          return false;
        }
        await leaveService.cancel(id, cancelReason.trim());
      } else {
        await leaveService.close(id, {
          closureNotes: closureNotes.trim() || null,
          resumptionDate: resumedOn || null,
        });
      }
      await refresh();
      await workflow.refresh();
      toast({
        title: 'Done',
        description: action === 'cancel' ? 'Leave request cancelled.' : 'Return confirmed and the leave closed.',
      });
      setAction(null);
      setCancelReason('');
      setClosureNotes('');
      setResumedOn('');
      return true;
    } catch (e: any) {
      toast({ title: 'Error', description: e?.message || 'Action failed.', variant: 'destructive' });
      return false;
    } finally {
      setBusy(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !r) {
    return (
      <div className="p-6">
        <EmptyState title="Leave request not found" description="It may have been removed." />
      </div>
    );
  }

  const isDraft = r.status === 'Draft';
  // Round 5, lane D: cancel, recall and confirming the return are offered as the SERVER decides.
  // Two of the rules turn on facts this page cannot know: whether the viewer supervises or heads
  // the employee's department, and today's date against the leave's first day.
  const actions = r.viewerActions;
  const canCancel = actions?.canCancel ?? false;
  const canConfirmReturn = actions?.canConfirmResumption ?? false;
  const today = new Date().toISOString().slice(0, 10);

  // Maternity leave is confirmed or rejected, never moved: its dates follow the birth (round 5, A4).
  // The service refuses both date acts for it; the page does not offer them.
  const isMaternity = r.leaveTypeCategory === 'Maternity';
  // The third decision verb, offered wherever approve and reject are. The service refuses anyone
  // the engine has not assigned, and anyone approving their own leave.
  const canSuggest = r.status === 'Pending' && !isMaternity;
  // The employee's answer. HR can drive it from here too — the endpoint is self-or-leave-write.
  const canRespond = r.status === 'ChangesSuggested';
  // Moving approved dates, and saying it is still going ahead. Neither applies once it is closed.
  // Round 5, lane H: the two parts of a split absence were approved as one and are not moved apart.
  const running = (s?: string | null) => s === 'Approved' || s === 'InProgress';
  const splitRunning =
    (!!r.chargedToAnnualRequestId && running(r.chargedToAnnualStatus)) ||
    (!!r.splitFromRequestId && running(r.splitFromStatus));
  const canReschedule = r.status === 'Approved' && !r.closureDate && !isMaternity && !splitRunning;
  const canConfirm = r.status === 'Approved' && !r.closureDate && !r.observanceConfirmedDate;
  // Recall applies to leave that has been granted, including leave already under way — that is the
  // case it mainly exists for. The employer's act: HR, or the employee's supervisor or head of
  // department (round 5, B1) — never the employee.
  const canRecall = actions?.canRecall ?? false;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={r.requestNumber}
        description={`${r.employeeName} · ${r.leaveTypeName}${r.leaveSubTypeName ? ` (${r.leaveSubTypeName})` : ''}`}
        backHref="/hr/leave/requests"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={r.status} />
            <MatchesApprovedPlanBadge show={r.matchesApprovedPlan} />
            {isDraft && (
              <Button
                variant="outline"
                onClick={() => router.push(`/hr/leave/requests/${id}/edit`)}
              >
                <Pencil className="mr-2 h-4 w-4" /> Edit
              </Button>
            )}

            {/* Submit / approve / reject / recall all come from the workflow engine. */}
            <WorkflowApprovalActions {...workflow.actionProps} />

            {canSuggest && (
              <Button variant="outline" onClick={() => setDateDialog('suggest')}>
                <CornerUpLeft className="mr-2 h-4 w-4" /> Send back with dates
              </Button>
            )}
            {canRespond && (
              <Button onClick={() => setDateDialog('respond')}>
                <CornerUpLeft className="mr-2 h-4 w-4" /> Answer the suggestion
              </Button>
            )}
            {canReschedule && (
              <Button variant="outline" onClick={() => setDateDialog('reschedule')}>
                <CalendarClock className="mr-2 h-4 w-4" /> Move dates
              </Button>
            )}
            {canConfirm && (
              <Button variant="outline" disabled={busy} onClick={confirmObservance}>
                <CircleCheck className="mr-2 h-4 w-4" /> Still going ahead
              </Button>
            )}
            {canRecall && (
              <Button variant="outline" onClick={() => setDateDialog('recall')}>
                <PhoneCall className="mr-2 h-4 w-4" /> Recall
              </Button>
            )}

            {canConfirmReturn && (
              <Button
                variant="outline"
                onClick={() => {
                  setResumedOn(r.resumptionDate?.slice(0, 10) ?? '');
                  setAction('close');
                }}
              >
                <CheckCheck className="mr-2 h-4 w-4" /> Confirm return
              </Button>
            )}
            {canCancel && (
              <Button variant="destructive" onClick={() => setAction('cancel')}>
                <Ban className="mr-2 h-4 w-4" /> Cancel
              </Button>
            )}
          </div>
        }
      />

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="attachments">Attachments</TabsTrigger>
          <WorkflowTabTrigger value="workflow" {...workflow.tabProps} />
        </TabsList>

        <TabsContent value="overview" className="space-y-4 pt-4">
          <SuggestedDatesPanel request={r} />
          <RescheduleTrailPanel request={r} />
          <RecallPanel request={r} />
          <SplitAbsencePanel request={r} hrefFor={(requestId) => `/hr/leave/requests/${requestId}`} />
          {/*
            ⚠ The board arm of the evidence gate (G4/R-15b). The link endpoint went in first and I
            recorded the gate as closed while nothing in the product could call it — so a leave type
            with a board threshold could only be satisfied by attaching a paper report, and the
            board half of the rule was unreachable. This panel is what makes it reachable.

            It hides itself on leave types that neither name a board nor have a threshold, so it
            costs annual leave nothing.
          */}
          <MedicalBoardLinkPanel request={r} canEdit={canWrite} boardLocked={!isDraft} onChanged={refresh} />

          <InfoCard title="Leave">
            <InfoRow label="Leave type" value={r.leaveTypeName} />
            <InfoRow label="Sub-type" value={r.leaveSubTypeName} />
            <InfoRow label="Paid" value={r.isPaidLeave ? 'Yes' : 'No'} />
            <InfoRow label="Start date" value={r.startDate?.slice(0, 10)} />
            <InfoRow label="End date" value={r.endDate?.slice(0, 10)} />
            <InfoRow label="Total days" value={r.totalDays} />
            <InfoRow label="Requested on" value={r.requestDate?.slice(0, 10)} />
            <InfoRow label="From a plan" value={r.leavePlanReference ?? (r.leavePlanId ? 'Yes' : 'No')} />
            {/*
              The attendance join. It should match Total days once approved; fewer means some of
              those days already had attendance recorded and leave did not overwrite them — and
              those days do not reach the payroll export as leave (closure plan L-27).
            */}
            <InfoRow
              label="Attendance days marked"
              value={
                r.status === 'Approved' || r.status === 'InProgress' || r.status === 'Completed'
                  ? `${r.attendanceDaysRecorded ?? 0} of ${r.totalDays}`
                  : undefined
              }
            />
            <InfoRow
              label="Still going ahead"
              value={
                r.observanceConfirmedDate
                  ? `Confirmed ${r.observanceConfirmedDate.slice(0, 10)}`
                  : r.status === 'Approved'
                    ? 'Not yet confirmed'
                    : undefined
              }
            />
          </InfoCard>

          <InfoCard title="Employee & cover">
            <InfoRow label="Employee" value={`${r.employeeName} (${r.employeeNumber})`} />
            <InfoRow label="Reliever" value={r.relieverEmployeeName} />
            <InfoRow label="Second reliever" value={r.secondRelieverEmployeeName} />
          </InfoCard>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Reason & notes</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3 text-sm">
              <div>
                <p className="text-xs text-muted-foreground">Reason</p>
                <p className="whitespace-pre-wrap">{r.reason || '—'}</p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Handover notes</p>
                <p className="whitespace-pre-wrap">{r.handoverNotes || '—'}</p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Reliever notes</p>
                <p className="whitespace-pre-wrap">{r.relieverNotes || '—'}</p>
              </div>
            </CardContent>
          </Card>

          {/* Round 5, B3: the employee reports the day they were back; their manager or HR confirms it. */}
          {(r.status === 'Approved' || r.status === 'InProgress' || r.status === 'Completed') && (
            <InfoCard title="Back at work">
              <InfoRow label="Due back" value={r.expectedReturnDate?.slice(0, 10)} />
              <InfoRow
                label="Back on"
                value={
                  r.resumptionDate
                    ? `${r.resumptionDate.slice(0, 10)}${r.resumptionReportedByName ? ` · reported by ${r.resumptionReportedByName}` : ''}`
                    : r.closureDate
                      ? 'Not stated'
                      : 'Not reported yet'
                }
              />
              <InfoRow
                label="Timing"
                value={
                  r.resumptionTiming === 'Early'
                    ? r.closureDate
                      ? `Early · ${r.daysRestored ?? 0} day(s) returned`
                      : 'Early · the unused days return when confirmed'
                    : r.resumptionTiming === 'OnTime'
                      ? 'On time'
                      : r.resumptionTiming === 'Late'
                        ? `Late · ${r.overstayDays ?? 0} working day(s) overstayed`
                        : undefined
                }
              />
              <InfoRow
                label="Confirmed by"
                value={r.closureConfirmedByName ?? (r.closureDate ? undefined : 'Not yet confirmed')}
              />
            </InfoCard>
          )}

          {(r.cancellationDate || r.closureDate) && (
            <InfoCard title="Outcome">
              <InfoRow label="Cancelled on" value={r.cancellationDate?.slice(0, 10)} />
              <InfoRow label="Cancellation reason" value={r.cancellationReason} />
              <InfoRow label="Closed on" value={r.closureDate?.slice(0, 10)} />
              <InfoRow label="Closure notes" value={r.closureNotes} />
            </InfoCard>
          )}
        </TabsContent>

        <TabsContent value="attachments" className="pt-4">
          <LeaveAttachmentsPanel leaveRequestId={id} canUpload={isDraft || r.status === 'Pending'} evidenceLocked={!isDraft} />
        </TabsContent>

        <WorkflowTabContent
          {...workflow.tabProps}
          value="workflow"
          entityType="LeaveRequest"
          entityId={id}
          entityLabel="Leave Request"
          entityNumber={r.requestNumber}
          status={r.status}
          onAfterAction={async () => {
            await refresh();
            await workflow.refresh();
          }}
        />
      </Tabs>

      <SuggestDatesDialog
        request={r}
        open={dateDialog === 'suggest'}
        onOpenChange={(open) => setDateDialog(open ? 'suggest' : null)}
        busy={busy}
        onConfirm={(values) =>
          runDateChange('suggest', () =>
            leaveService.suggestChanges(id, {
              suggestedStartDate: values.suggestedStartDate,
              suggestedEndDate: values.suggestedEndDate,
              notes: values.notes || null,
            }),
          )
        }
      />

      <RespondToSuggestionDialog
        request={r}
        open={dateDialog === 'respond'}
        onOpenChange={(open) => setDateDialog(open ? 'respond' : null)}
        busy={busy}
        onConfirm={(values) =>
          runDateChange('respond', () => leaveService.respondToSuggestion(id, values))
        }
      />

      <RescheduleDialog
        request={r}
        open={dateDialog === 'reschedule'}
        onOpenChange={(open) => setDateDialog(open ? 'reschedule' : null)}
        busy={busy}
        onConfirm={(values) => runDateChange('reschedule', () => leaveService.reschedule(id, values))}
      />

      <RecallDialog
        request={r}
        open={dateDialog === 'recall'}
        onOpenChange={(open) => setDateDialog(open ? 'recall' : null)}
        busy={busy}
        onConfirm={(values) => runDateChange('recall', () => leaveService.recall(id, values))}
      />

      <ConfirmationDialog
        open={action !== null}
        onOpenChange={(open) => !open && setAction(null)}
        title={action === 'cancel' ? 'Cancel leave request?' : 'Confirm the return?'}
        description={
          action === 'cancel'
            ? actions?.cancelNeedsReason
              ? 'This leave was granted. Cancelling it gives every day back, and it can be done up to and including its first day.'
              : 'The request is withdrawn and any pending days are released.'
            : 'Confirming the return closes the leave.'
        }
        confirmText={action === 'cancel' ? 'Cancel request' : 'Confirm return'}
        variant={action === 'cancel' ? 'destructive' : 'default'}
        isLoading={busy}
        onConfirm={runAction}
      >
        {action === 'cancel' ? (
          <div className="space-y-2">
            <Label htmlFor="cancelReason">
              Reason
              {actions?.cancelNeedsReason && <span className="text-red-500"> *</span>}
            </Label>
            <Textarea
              id="cancelReason"
              rows={3}
              value={cancelReason}
              onChange={(e) => setCancelReason(e.target.value)}
              placeholder={actions?.cancelNeedsReason ? 'Why is granted leave being taken back?' : 'Optional'}
            />
          </div>
        ) : (
          <div className="space-y-3">
            <p className="text-sm text-muted-foreground">
              Due back {r.expectedReturnDate?.slice(0, 10) ?? '—'}.{' '}
              {r.resumptionReportedDate
                ? `${r.employeeName} reported being back on ${r.resumptionDate?.slice(0, 10)}.`
                : 'Nobody has reported the return.'}
            </p>
            <div className="space-y-2">
              <Label htmlFor="resumedOn">First day back</Label>
              <Input
                id="resumedOn"
                type="date"
                max={today}
                value={resumedOn}
                onChange={(e) => setResumedOn(e.target.value)}
              />
              <p className="text-xs text-muted-foreground">
                Blank closes the leave as over. A day on or before the end date cuts the leave short and
                returns the unused days; the employee must have reported it. A day after the due date
                records the working days overstayed. Nothing is charged for that; HR and payroll decide.
              </p>
            </div>
            <div className="space-y-2">
              <Label htmlFor="closureNotes">Notes</Label>
              <Textarea
                id="closureNotes"
                rows={3}
                value={closureNotes}
                onChange={(e) => setClosureNotes(e.target.value)}
                placeholder="Optional"
              />
            </div>
          </div>
        )}
      </ConfirmationDialog>
    </div>
  );
}
