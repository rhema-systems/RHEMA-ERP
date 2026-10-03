'use client';

import { Suspense, use, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient, useMutation } from '@tanstack/react-query';
import { Loader2, Ban, CheckCheck, Globe, ShieldAlert, Pencil, Send, RotateCcw, FilePenLine, Lock, ThumbsUp, ThumbsDown, UserCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { TravelApproveDialog, decidesAsSentence } from '@/components/hr/travel/TravelApproveDialog';
import { TravelAttachmentsPanel } from '@/components/hr/travel/TravelAttachmentsPanel';
import { TravelBookingsPanel } from '@/components/hr/travel/TravelBookingsPanel';
import { TravelDestinationAlerts } from '@/components/hr/travel/TravelDestinationAlerts';
import { TravelCompliancePanel } from '@/components/hr/travel/TravelCompliancePanel';
import { TravelFinancePanel } from '@/components/hr/travel/TravelFinancePanel';
import { TravelItineraryPanel } from '@/components/hr/travel/TravelItineraryPanel';
import { TravelLifecycleNotes } from '@/components/hr/travel/TravelLifecycleNotes';
import { TravelQueryError } from '@/components/hr/travel/TravelQueryError';
import { TravelReasonDialog } from '@/components/hr/travel/TravelReasonDialog';
import { useTabParam } from '@/components/hr/travel/useTabParam';
import {
  ELEVATED_TRAVEL_RISK_LEVELS,
  TRAVEL_COMMENT_TYPE_LABELS,
  TRAVEL_INITIATOR_ROLE_LABELS,
  TRAVEL_PRIORITY_LABELS,
  TRAVEL_PURPOSE_LABELS,
  TRAVEL_REQUEST_STATUS_LABELS,
  TRAVEL_RISK_LEVEL_LABELS,
  TRAVEL_TYPE_LABELS,
  enumLabel,
} from '@/components/hr/travel/travel-enums';
import { fmtTravelMoney } from '@/components/hr/travel/travel-format';
import { useTravelAccess } from '@/components/hr/travel/useTravelAccess';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowTabContent, WorkflowTabTrigger } from '@/components/workflow/WorkflowRecordTab';
import { useWorkflowRecord } from '@/hooks/useWorkflowRecord';
import { useToast } from '@/hooks/use-toast';
import { travelService } from '@/services/hr/travel.service';
import type { StaffTravelSubmitResult } from '@/types/hr/travel';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');
/** Today on the server's calendar (UTC), as the API compares a departure date. */
const todayUtc = () => new Date().toISOString().slice(0, 10);

function InfoRow({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="py-1.5">
      <p className="text-xs text-muted-foreground">{label}</p>
      <div className="text-sm">{value ?? '—'}</div>
    </div>
  );
}

/**
 * A travel request, from the travel desk's side — and the approver's (travel final closure, lane 2).
 *
 * ⚠ **Approval is the workflow engine's, not this page's.** `status` says only which phase the request
 * is in — `Submitted` means "out for approval" and which stage it sits on is the workflow instance's
 * business — and nothing here ever writes a status.
 *
 * **Who decides is the server's answer** (`viewer-actions`, lane 2): the traveller's line manager at the
 * first stage (the travel desk there only when no line manager can), the route's approvers after, never
 * the traveller. Approve, Reject and Return are travel's own buttons, drawn from that answer — the shared
 * workflow actions keep submit and recall only — so the budget is asked for at the last stage, and a
 * Manager outside the traveller's line is never offered a button the server would refuse.
 *
 * **An approver without travel permission** reaches this page through the approvals queue or their
 * inbox: they see the trip, its comments and attachments and their decision; the desk's tabs and
 * controls are not drawn for them.
 *
 * Since lane 8 (slice 8a) the open tab is in `?tab=` — a traveller's message opens Comments, their file
 * Attachments, an advance to approve Finance — so the page sits under a Suspense boundary.
 */
export default function TravelRequestDetailPage({ params }: { params: Promise<{ id: string }> }) {
  return (
    <Suspense fallback={null}>
      <TravelRequestDetail params={params} />
    </Suspense>
  );
}

const DESK_TABS = ['overview', 'itinerary', 'bookings', 'finance', 'compliance', 'comments', 'attachments', 'workflow'] as const;
/** What an approver without travel permission is shown (see above). */
const APPROVER_TABS: readonly string[] = ['overview', 'comments', 'attachments', 'workflow'];

function TravelRequestDetail({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const [tab, setTab] = useTabParam(DESK_TABS, 'overview');
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const access = useTravelAccess();
  const [comment, setComment] = useState('');
  const [internalNote, setInternalNote] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  /** Which reason-asking action is open: submit after departure, return, reject, request change. */
  const [reasonFor, setReasonFor] = useState<null | 'late' | 'return' | 'reject' | 'change'>(null);
  const [approveOpen, setApproveOpen] = useState(false);

  const { data: r, isLoading, isError, error } = useQuery({
    queryKey: ['travel-request', id],
    queryFn: () => travelService.getById(id),
  });

  /** A submission can carry warnings that did not stop it — approved leave over the same days. */
  const announceWarnings = (result: StaffTravelSubmitResult) => {
    if (result.warnings?.length)
      toast({ title: 'Submitted — please note', description: result.warnings.join(' ') });
  };

  // A departure date already past: the server refuses the submission without the desk's reason.
  const departed = !!r && r.travelStartDate.slice(0, 10) < todayUtc();

  // Lane 2: what the caller may decide on it, at which stage and as whom — asked only while it is out
  // for approval.
  const { data: viewer } = useQuery({
    queryKey: ['travel-request-viewer', id],
    queryFn: () => travelService.getViewerActions(id),
    enabled: r?.status === 'Submitted',
  });

  const {
    data: comments,
    isError: commentsFailed,
    error: commentsError,
  } = useQuery({
    queryKey: ['travel-request-comments', id],
    queryFn: () => travelService.getComments(id),
    enabled: !!r,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['travel-request', id] });
    await queryClient.invalidateQueries({ queryKey: ['travel-request-viewer', id] });
    await queryClient.invalidateQueries({ queryKey: ['travel-request-comments', id] });
    await queryClient.invalidateQueries({ queryKey: ['travel-requests'] });
  };

  const workflow = useWorkflowRecord({
    recallPrompt: 'reason',
    entityType: 'StaffTravelRequest',
    entityId: id,
    entityLabel: 'Travel Request',
    entityNumber: r?.requestNumber,
    status: r?.status ?? 'Draft',
    // Draft and ReturnedForRevision are the two states the request can be sent from. A trip whose
    // departure has passed is submitted through its own button instead, because the server needs the
    // reason it is late (lane 1). The desk submits; a line manager reading their report's draft does not.
    canSubmit: (r?.status === 'Draft' || r?.status === 'ReturnedForRevision') && !departed && access.canWrite,
    // Lane 2: approve and reject are travel's own buttons below, drawn from the server's viewer actions —
    // the shared actions asked only the engine, which would offer them to anyone it lists.
    canApproveReject: false,
    enabled: !!r,
    commands: {
      submit: async () => announceWarnings(await travelService.submit(id)),
      // Travel's own recall (lane 1): the generic recall the actions fell back to never told the
      // request, which stayed Submitted with no workflow behind it (cross-module defect #15's shape).
      recall: (reason) => travelService.recall(id, reason || undefined),
      afterAction: refresh,
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

  const afterLifecycle = async (title: string, description?: string) => {
    toast({ title, description });
    await refresh();
    await workflow.refresh();
  };
  const failed = (title: string) => (e: Error) =>
    toast({ variant: 'destructive', title, description: e.message });

  const submitLate = useMutation({
    mutationFn: (reason: string) => travelService.submit(id, reason),
    onSuccess: async (result) => {
      await afterLifecycle('Travel request submitted', result.message);
      announceWarnings(result);
    },
    onError: failed('Could not submit'),
  });

  // Lane 2 (D-7, O-9, T-10): the approver's decisions, at the stage the request is on.
  const approve = useMutation({
    mutationFn: (d: { approvedBudget?: number; notes?: string }) => travelService.approve(id, d.approvedBudget, d.notes),
    onSuccess: (result) =>
      afterLifecycle(
        result.status === 'Approved' ? 'Trip approved' : 'Approved at this stage',
        result.status === 'Approved' ? undefined : result.message,
      ),
    onError: failed('Could not approve'),
  });

  const reject = useMutation({
    mutationFn: (reason: string) => travelService.reject(id, reason),
    onSuccess: () => afterLifecycle('Travel request rejected', 'The traveller sees the reason.'),
    onError: failed('Could not reject'),
  });

  // Lane 1 (D-6): the approver's third answer beside approve and reject.
  const returnForRevision = useMutation({
    mutationFn: (reason: string) => travelService.returnForRevision(id, reason),
    onSuccess: () => afterLifecycle('Returned for revision', 'The request is back with its requester to change.'),
    onError: failed('Could not return the request'),
  });

  // Lane 1 (D-9): the only way to change a trip once it is approved.
  const requestChange = useMutation({
    mutationFn: (reason: string) => travelService.requestChange(id, reason),
    onSuccess: () => afterLifecycle('Back for revision', 'Edit the trip and submit it again for approval.'),
    onError: failed('Could not send the trip back'),
  });

  const close = useMutation({
    mutationFn: () => travelService.close(id),
    onSuccess: () => afterLifecycle('Trip closed'),
    onError: failed('Could not close the trip'),
  });

  const complete = useMutation({
    mutationFn: () => travelService.complete(id),
    onSuccess: async () => {
      toast({ title: 'Trip marked completed' });
      await refresh();
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not complete the trip', description: e.message }),
  });

  const cancel = useMutation({
    mutationFn: () => travelService.cancel(id, cancelReason.trim()),
    onSuccess: async () => {
      setCancelOpen(false);
      setCancelReason('');
      toast({ title: 'Travel request cancelled' });
      await refresh();
      await workflow.refresh();
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not cancel', description: e.message }),
  });

  // ⚠ The composer sent `commentType: 'General'`, which is not a member of the C# enum, so every
  // comment from this page came back 400 — and it hard-coded the traveller as a reader (travel
  // final closure, lane 0 — findings A7, T-27). An internal note is now its own type and hidden.
  const addComment = useMutation({
    mutationFn: () =>
      travelService.addComment(id, {
        commentType: internalNote ? 'InternalNote' : 'Comment',
        body: comment.trim(),
        isVisibleToTraveller: !internalNote,
      }),
    onSuccess: async () => {
      setComment('');
      await queryClient.invalidateQueries({ queryKey: ['travel-request-comments', id] });
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not add the comment', description: e.message }),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-10">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }
  if (isError && !r) {
    return (
      <div className="p-6">
        <TravelQueryError error={error} what="this travel request" />
      </div>
    );
  }
  if (!r) {
    return (
      <div className="p-6">
        <EmptyState title="Not found" description="This travel request does not exist." />
      </div>
    );
  }

  const isEditable = r.status === 'Draft' || r.status === 'ReturnedForRevision';
  const canComplete = r.status === 'Approved' || r.status === 'InProgress';
  // Lane 1: completion records that the trip happened, so not before it starts.
  const notStarted = r.travelStartDate.slice(0, 10) > todayUtc();
  // Lane 1 (O-11): a trip under way is completed, not cancelled.
  const isLive = !['Cancelled', 'Rejected', 'Completed', 'Closed', 'InProgress'].includes(r.status);
  // Lane 8 (D-48): since the sweep moves a trip under way on its date, one that did not happen is cancelled as not
  // travelled — until its end date; the server refuses it once anything was spent on the trip.
  const mayCallOff = r.status === 'InProgress' && r.travelEndDate.slice(0, 10) >= todayUtc();
  // Lane 2: deciding is the server's answer for this caller at this stage — the line rule included.
  const mayDecide = r.status === 'Submitted' && viewer?.canDecide === true;
  // An approver with no travel permission sees the trip and their decision, not the desk's tabs.
  const deskView = access.canRead;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={r.requestNumber}
        description={`${r.employeeName} · ${r.originCity} → ${r.destinationCity}, ${fmtDate(r.travelStartDate)}`}
        backHref={deskView ? '/hr/travel' : '/hr/travel/approvals'}
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={enumLabel(TRAVEL_REQUEST_STATUS_LABELS, r.status)} />
            {isEditable && access.canWrite && (
              <Button variant="outline" onClick={() => router.push(`/hr/travel/${id}/edit`)}>
                <Pencil className="mr-2 h-4 w-4" /> Edit
              </Button>
            )}
            <WorkflowApprovalActions {...workflow.actionProps} />
            {isEditable && departed && access.canWrite && (
              <Button onClick={() => setReasonFor('late')}>
                <Send className="mr-2 h-4 w-4" /> Submit after departure
              </Button>
            )}
            {mayDecide && (
              <Button onClick={() => setApproveOpen(true)} disabled={approve.isPending}>
                <ThumbsUp className="mr-2 h-4 w-4" /> Approve
              </Button>
            )}
            {mayDecide && (
              <Button variant="outline" onClick={() => setReasonFor('reject')}>
                <ThumbsDown className="mr-2 h-4 w-4" /> Reject
              </Button>
            )}
            {mayDecide && (
              <Button variant="outline" onClick={() => setReasonFor('return')}>
                <RotateCcw className="mr-2 h-4 w-4" /> Return for revision
              </Button>
            )}
            {r.status === 'Approved' && access.canWrite && (
              <Button variant="outline" onClick={() => setReasonFor('change')}>
                <FilePenLine className="mr-2 h-4 w-4" /> Request change
              </Button>
            )}
            {canComplete && access.canWrite && (
              <Button
                variant="outline"
                onClick={() => complete.mutate()}
                disabled={complete.isPending || notStarted}
                title={notStarted ? 'A trip can be marked completed once it has started.' : undefined}
              >
                <CheckCheck className="mr-2 h-4 w-4" /> Mark completed
              </Button>
            )}
            {r.status === 'Completed' && access.canWrite && (
              <Button variant="outline" onClick={() => close.mutate()} disabled={close.isPending}>
                <Lock className="mr-2 h-4 w-4" /> Close trip
              </Button>
            )}
            {/* Cancel is the desk's (HR.Travel.Write); it was drawn for anyone who could open the page. */}
            {isLive && access.canWrite && (
              <Button variant="outline" onClick={() => setCancelOpen(true)}>
                <Ban className="mr-2 h-4 w-4" /> Cancel
              </Button>
            )}
            {mayCallOff && access.canWrite && (
              <Button variant="outline" onClick={() => setCancelOpen(true)}>
                <Ban className="mr-2 h-4 w-4" /> Did not travel
              </Button>
            )}
          </div>
        }
      />

      {r.status === 'Submitted' && viewer && (
        <Card className={mayDecide ? 'border-primary/40' : undefined}>
          <CardContent className="flex items-start gap-3 p-4 text-sm">
            <UserCheck className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
            <div className="space-y-1">
              <p className="font-medium">
                {viewer.stageName ? `${viewer.stageName} stage` : 'Out for approval'}
                {viewer.nextStageName ? ` — then ${viewer.nextStageName}` : ''}
              </p>
              {mayDecide ? (
                <p className="text-muted-foreground">
                  {decidesAsSentence(viewer, r.employeeName)}{' '}
                  {viewer.isFinalStage ? 'You set the approved budget.' : ''}
                </p>
              ) : viewer.waitingFor.length > 0 ? (
                <p className="text-muted-foreground">Waiting for {viewer.waitingFor.join(' or ')}.</p>
              ) : viewer.reason ? (
                <p className="text-muted-foreground">{viewer.reason}</p>
              ) : null}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Lane 5, T-45: an alert in force at the destination during the trip — a warning, for the approver and the desk. */}
      {(r.status === 'Submitted' || r.status === 'Approved' || r.status === 'InProgress') && (
        <TravelDestinationAlerts requestId={r.id} context={r.status === 'Submitted' ? 'approve' : 'book'} />
      )}

      {/* Lane 6 (D-33): a driver's own request names the trip whose company vehicle they drive; it goes with that leg. */}
      {r.driverForRequestId && (
        <p className="rounded-md border bg-muted/40 px-4 py-2 text-sm">
          The driver&apos;s own trip, for the company vehicle on{' '}
          {deskView
            ? <Link className="font-medium underline" href={`/hr/travel/${r.driverForRequestId}`}>{r.driverForRequestNumber}</Link>
            : <span className="font-medium">{r.driverForRequestNumber}</span>}
          . It is cancelled with that leg, or with that trip.
        </p>
      )}

      <Tabs value={deskView || APPROVER_TABS.includes(tab) ? tab : 'overview'} onValueChange={setTab}>
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          {deskView && <TabsTrigger value="itinerary">Itinerary</TabsTrigger>}
          {deskView && <TabsTrigger value="bookings">Bookings</TabsTrigger>}
          {deskView && <TabsTrigger value="finance">Finance</TabsTrigger>}
          {deskView && <TabsTrigger value="compliance">Compliance</TabsTrigger>}
          <TabsTrigger value="comments">Comments</TabsTrigger>
          <TabsTrigger value="attachments">Attachments</TabsTrigger>
          <WorkflowTabTrigger value="workflow" />
        </TabsList>

        <TabsContent value="overview" className="space-y-4 pt-4">
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">The trip</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
              <InfoRow label="Traveller" value={r.employeeName} />
              <InfoRow
                label="Raised by"
                value={`${r.initiatedByName || '—'} (${enumLabel(TRAVEL_INITIATOR_ROLE_LABELS, r.initiatedByRole)})`}
              />
              <InfoRow label="Type" value={enumLabel(TRAVEL_TYPE_LABELS, r.travelType)} />
              <InfoRow label="Purpose" value={enumLabel(TRAVEL_PURPOSE_LABELS, r.travelPurpose)} />
              <InfoRow label="Priority" value={enumLabel(TRAVEL_PRIORITY_LABELS, r.priority)} />
              <InfoRow
                label="Route"
                value={
                  <span className="flex items-center gap-1.5">
                    {r.isInternational && <Globe className="h-3.5 w-3.5 text-muted-foreground" />}
                    {r.originCity}, {r.originCountryName} → {r.destinationCity},{' '}
                    {r.destinationCountryName}
                  </span>
                }
              />
              <InfoRow label="Departs" value={fmtDate(r.travelStartDate)} />
              <InfoRow label="Returns" value={fmtDate(r.travelEndDate)} />
              <InfoRow label="Duration" value={`${r.estimatedDurationDays} day(s)`} />
              {/* Lane 9 (D-54): the trip's working days on the traveller's attendance, as on duty. */}
              {r.attendanceDaysRecorded != null && (
                <InfoRow
                  label="On attendance"
                  value={r.attendanceDaysRecorded > 0
                    ? `${r.attendanceDaysRecorded} working day(s) on duty`
                    : ['Approved', 'InProgress', 'Completed', 'Closed'].includes(r.status)
                      ? 'None — its days already carry other attendance, or the nightly sweep has yet to post them'
                      : 'None — a trip’s days are posted once it is approved'}
                />
              )}
              {/* Both are the server's: the traveller's own unit, and the policy the trip was
                  checked against when it was submitted (lane 1). */}
              <InfoRow label="Organisation unit" value={r.organizationUnitName || '—'} />
              <InfoRow
                label="Travel policy"
                value={r.policyName || (r.submittedAt ? 'None covered this trip' : 'Checked when submitted')}
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Cost and risk</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
              <InfoRow label="Estimated" value={fmtTravelMoney(r.estimatedTotalCost, r.currencyCode)} />
              <InfoRow label="Approved budget" value={fmtTravelMoney(r.approvedBudget, r.currencyCode)} />
              <InfoRow
                label="Risk level"
                value={
                  <span className="flex items-center gap-1.5">
                    {ELEVATED_TRAVEL_RISK_LEVELS.includes(r.riskLevel) && (
                      <ShieldAlert className="h-3.5 w-3.5 text-destructive" />
                    )}
                    {enumLabel(TRAVEL_RISK_LEVEL_LABELS, r.riskLevel)}
                  </span>
                }
              />
              <InfoRow label="Visa required" value={r.requiresVisa ? 'Yes' : 'No'} />
              <InfoRow label="Health clearance" value={r.requiresHealthClearance ? 'Yes' : 'No'} />
              <InfoRow label="Submitted" value={fmtDateTime(r.submittedAt)} />
              <InfoRow
                label="Approved"
                value={r.approvedAt ? [fmtDateTime(r.approvedAt), r.approvedByName].filter(Boolean).join(' · ') : '—'}
              />
              <InfoRow label="Completed" value={fmtDateTime(r.completedAt)} />
              {r.closedAt && (
                <InfoRow label="Closed" value={[fmtDateTime(r.closedAt), r.closedByName].filter(Boolean).join(' · ')} />
              )}
            </CardContent>
          </Card>

          <TravelLifecycleNotes request={r} />

          {r.purposeDescription && (
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-base">Justification</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-sm whitespace-pre-wrap">{r.purposeDescription}</p>
              </CardContent>
            </Card>
          )}

          {r.cancellationReason && (
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="flex items-center gap-2 text-base">
                  <Ban className="h-4 w-4" />
                  {r.status === 'Rejected' ? 'Why it was rejected' : 'Why it was cancelled'}
                </CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-sm whitespace-pre-wrap">{r.cancellationReason}</p>
                {r.cancelledByName && (
                  <p className="mt-2 text-xs text-muted-foreground">
                    {r.cancelledByName} · {fmtDateTime(r.cancelledAt)}
                  </p>
                )}
              </CardContent>
            </Card>
          )}
        </TabsContent>

        {deskView && (
          <>
            <TabsContent value="itinerary" className="pt-4">
              <TravelItineraryPanel request={r} />
            </TabsContent>

            <TabsContent value="bookings" className="pt-4">
              <TravelBookingsPanel request={r} />
            </TabsContent>

            <TabsContent value="finance" className="pt-4">
              <TravelFinancePanel request={r} />
            </TabsContent>

            <TabsContent value="compliance" className="pt-4">
              <TravelCompliancePanel request={r} />
            </TabsContent>
          </>
        )}

        <TabsContent value="comments" className="space-y-4 pt-4">
          {/* Posting is the desk's (HR.Travel.Write); an approver reads the thread and gives their
              reasons with their decision. */}
          {access.canWrite && (
          <Card>
            <CardContent className="space-y-3 p-4">
              <Textarea
                value={comment}
                onChange={(e) => setComment(e.target.value)}
                placeholder={
                  internalNote ? 'A note for the travel desk only…' : 'A comment the traveller will see…'
                }
                rows={3}
              />
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div className="flex items-center gap-2">
                  <Switch
                    id="travel-comment-internal"
                    checked={internalNote}
                    onCheckedChange={setInternalNote}
                  />
                  <Label htmlFor="travel-comment-internal" className="text-sm font-normal">
                    Internal note
                  </Label>
                </div>
                {/* Authorship is the token's — there is no author field to fill in.
                    ⚠ "Not shown" is the portal's filter: until lane 1 the self-service read still
                    returns internal notes to the traveller's browser (finding A6). */}
                <p className="text-xs text-muted-foreground">
                  {internalNote
                    ? 'Posted in your name. Not shown on the traveller’s portal.'
                    : 'Posted in your name. The traveller sees it on their portal.'}
                </p>
                <Button
                  size="sm"
                  disabled={!comment.trim() || addComment.isPending}
                  onClick={() => addComment.mutate()}
                >
                  Add comment
                </Button>
              </div>
            </CardContent>
          </Card>
          )}

          {commentsFailed && !comments ? (
            <TravelQueryError error={commentsError} what="the comments" />
          ) : (comments ?? []).length === 0 ? (
            <EmptyState title="No comments" description="Nothing has been said about this trip yet." />
          ) : (
            <div className="space-y-3">
              {(comments ?? []).map((c) => (
                <Card key={c.id}>
                  <CardContent className="p-4">
                    <div className="flex items-center justify-between gap-3">
                      <p className="text-sm font-medium">
                        {c.authorName || 'Unknown author'}
                        {c.commentType !== 'Comment' && c.commentType !== 'InternalNote' && (
                          <span className="ml-2 text-xs font-normal text-muted-foreground">
                            {enumLabel(TRAVEL_COMMENT_TYPE_LABELS, c.commentType)}
                          </span>
                        )}
                      </p>
                      <p className="text-xs text-muted-foreground">{fmtDateTime(c.createdAt)}</p>
                    </div>
                    <p className="mt-1 text-sm whitespace-pre-wrap">{c.body}</p>
                    {!c.isVisibleToTraveller && (
                      <p className="mt-2 text-xs text-muted-foreground">
                        Internal — not shown to the traveller
                      </p>
                    )}
                  </CardContent>
                </Card>
              ))}
            </div>
          )}
        </TabsContent>

        <TabsContent value="attachments" className="pt-4">
          {/* Delete is `HR.Travel.Admin` server-side, so the button shows only to those who hold it. */}
          <TravelAttachmentsPanel requestId={id} canUpload={access.canWrite} canDelete={access.canAdmin} />
        </TabsContent>

        <WorkflowTabContent
          value="workflow"
          entityType="StaffTravelRequest"
          entityId={id}
          entityLabel="Travel Request"
          entityNumber={r.requestNumber}
          status={r.status}
          onAfterAction={async () => {
            await refresh();
            await workflow.refresh();
          }}
        />
      </Tabs>

      <TravelReasonDialog
        open={reasonFor === 'late'}
        onOpenChange={(open) => setReasonFor(open ? 'late' : null)}
        title="Submit a trip after its departure date"
        description={`This trip was due to leave on ${fmtDate(r.travelStartDate)}. Say why it is being submitted late — usually travel at short notice, with the paperwork following. The reason is kept on the request as an internal note in your name, for the approver.`}
        placeholder="Why is this trip being submitted after it began?"
        confirmLabel="Submit for approval"
        pending={submitLate.isPending}
        onConfirm={(reason) => submitLate.mutateAsync(reason)}
      />

      {viewer && (
        <TravelApproveDialog
          open={approveOpen}
          onOpenChange={setApproveOpen}
          requestNumber={r.requestNumber}
          traveller={r.employeeName}
          estimate={r.estimatedTotalCost}
          currencyCode={r.currencyCode}
          viewer={viewer}
          pending={approve.isPending}
          onConfirm={(decision) => approve.mutateAsync(decision)}
        />
      )}

      <TravelReasonDialog
        open={reasonFor === 'reject'}
        onOpenChange={(open) => setReasonFor(open ? 'reject' : null)}
        title="Reject this travel request"
        description="The trip will not go ahead. A rejected request cannot be sent again — the traveller raises a new one. They see your reason."
        label="Why it is rejected"
        placeholder="Not this quarter's priority, cover cannot be found, the cost is not justified…"
        confirmLabel="Reject"
        destructive
        pending={reject.isPending}
        onConfirm={(reason) => reject.mutateAsync(reason)}
      />

      <TravelReasonDialog
        open={reasonFor === 'return'}
        onOpenChange={(open) => setReasonFor(open ? 'return' : null)}
        title="Return this request for revision"
        description="It goes back to whoever submitted it to change and send again. The approval in progress is withdrawn, and a new one starts when it is resubmitted."
        label="What needs to change"
        placeholder="The dates, the cost, the purpose — say what, so it comes back right."
        confirmLabel="Return for revision"
        pending={returnForRevision.isPending}
        onConfirm={(reason) => returnForRevision.mutateAsync(reason)}
      />

      <TravelReasonDialog
        open={reasonFor === 'change'}
        onOpenChange={(open) => setReasonFor(open ? 'change' : null)}
        title="Request a change to this approved trip"
        description="The trip goes back for revision: edit it, then submit it for approval again. Its bookings, advances and claims stay with it."
        label="What has changed"
        placeholder="New dates, a different destination, a higher cost…"
        confirmLabel="Send back for revision"
        pending={requestChange.isPending}
        onConfirm={(reason) => requestChange.mutateAsync(reason)}
      />

      <Dialog open={cancelOpen} onOpenChange={setCancelOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{mayCallOff ? 'Cancel as not travelled' : 'Cancel this travel request'}</DialogTitle>
            <DialogDescription>
              {mayCallOff
                ? 'For a trip moved under way on its date that did not happen. Refused once anything was spent on it — a claim, an advance paid out, a booking confirmed or used, a company vehicle dispatched; mark that one completed instead. The reason is kept on the record, with an internal note.'
                : 'A cancelled request cannot be revived. The reason is kept on the record.'}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="cancel-reason">Reason</Label>
            <Textarea
              id="cancel-reason"
              value={cancelReason}
              onChange={(e) => setCancelReason(e.target.value)}
              rows={3}
              placeholder={mayCallOff ? 'Why did the trip not happen?' : 'Why is the trip not going ahead?'}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCancelOpen(false)}>
              Keep it
            </Button>
            <Button
              variant="destructive"
              disabled={!cancelReason.trim() || cancel.isPending}
              onClick={() => cancel.mutate()}
            >
              Cancel request
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
