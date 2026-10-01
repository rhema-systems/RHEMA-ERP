'use client';

import { use, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient, useMutation } from '@tanstack/react-query';
import { Loader2, Ban, CheckCheck, Globe, ShieldAlert, Pencil } from 'lucide-react';
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
import { TravelAttachmentsPanel } from '@/components/hr/travel/TravelAttachmentsPanel';
import { TravelBookingsPanel } from '@/components/hr/travel/TravelBookingsPanel';
import { TravelCompliancePanel } from '@/components/hr/travel/TravelCompliancePanel';
import { TravelFinancePanel } from '@/components/hr/travel/TravelFinancePanel';
import { TravelItineraryPanel } from '@/components/hr/travel/TravelItineraryPanel';
import { TravelQueryError } from '@/components/hr/travel/TravelQueryError';
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

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');

function InfoRow({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="py-1.5">
      <p className="text-xs text-muted-foreground">{label}</p>
      <div className="text-sm">{value ?? '—'}</div>
    </div>
  );
}

/**
 * A travel request, from the travel desk's side.
 *
 * ⚠ **Approval is the workflow engine's, not this page's.** Slice 2 retired travel's bespoke
 * approval chain, so `status` says only which phase the request is in — `Submitted` means "out for
 * approval" and which step it sits on is the workflow instance's business. Submit/approve/reject
 * therefore all go through `useWorkflowRecord`, and nothing here ever writes a status.
 */
export default function TravelRequestDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const access = useTravelAccess();
  const [comment, setComment] = useState('');
  const [internalNote, setInternalNote] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [cancelReason, setCancelReason] = useState('');

  const { data: r, isLoading, isError, error } = useQuery({
    queryKey: ['travel-request', id],
    queryFn: () => travelService.getById(id),
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
    await queryClient.invalidateQueries({ queryKey: ['travel-requests'] });
  };

  const workflow = useWorkflowRecord({
    recallPrompt: 'reason',
    entityType: 'StaffTravelRequest',
    entityId: id,
    entityLabel: 'Travel Request',
    entityNumber: r?.requestNumber,
    status: r?.status ?? 'Draft',
    // Draft and ReturnedForRevision are the two states the request can be sent from.
    canSubmit: r?.status === 'Draft' || r?.status === 'ReturnedForRevision',
    canApproveReject: r?.status === 'Submitted',
    enabled: !!r,
    commands: {
      submit: () => travelService.submit(id),
      // No approver id is sent — the server resolves the approver from the token against the
      // published definition. ⚠ No amount is sent either, and the server then copies the ESTIMATE
      // into the approved budget, so that field records no decision. There is no budget prompt
      // anywhere in the workflow; lane 2 gives the approve dialog one (findings O-9, T-10).
      approve: (ctx) => travelService.approve(id, undefined, ctx.comments || undefined),
      reject: (ctx) => travelService.reject(id, ctx.comments || 'Rejected'),
      afterAction: refresh,
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
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
  const isLive = !['Cancelled', 'Rejected', 'Completed', 'Closed'].includes(r.status);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={r.requestNumber}
        description={`${r.employeeName} · ${r.originCity} → ${r.destinationCity}, ${fmtDate(r.travelStartDate)}`}
        backHref="/hr/travel"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={enumLabel(TRAVEL_REQUEST_STATUS_LABELS, r.status)} />
            {isEditable && (
              <Button variant="outline" onClick={() => router.push(`/hr/travel/${id}/edit`)}>
                <Pencil className="mr-2 h-4 w-4" /> Edit
              </Button>
            )}
            <WorkflowApprovalActions {...workflow.actionProps} />
            {canComplete && (
              <Button variant="outline" onClick={() => complete.mutate()} disabled={complete.isPending}>
                <CheckCheck className="mr-2 h-4 w-4" /> Mark completed
              </Button>
            )}
            {isLive && (
              <Button variant="outline" onClick={() => setCancelOpen(true)}>
                <Ban className="mr-2 h-4 w-4" /> Cancel
              </Button>
            )}
          </div>
        }
      />

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="itinerary">Itinerary</TabsTrigger>
          <TabsTrigger value="bookings">Bookings</TabsTrigger>
          <TabsTrigger value="finance">Finance</TabsTrigger>
          <TabsTrigger value="compliance">Compliance</TabsTrigger>
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
              <InfoRow label="Organisation unit" value={r.organizationUnitName || '—'} />
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
              <InfoRow label="Approved" value={fmtDateTime(r.approvedAt)} />
              <InfoRow label="Completed" value={fmtDateTime(r.completedAt)} />
            </CardContent>
          </Card>

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

        <TabsContent value="comments" className="space-y-4 pt-4">
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
          <TravelAttachmentsPanel requestId={id} canDelete={access.canAdmin} />
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

      <Dialog open={cancelOpen} onOpenChange={setCancelOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Cancel this travel request</DialogTitle>
            <DialogDescription>
              A cancelled request cannot be revived. The reason is kept on the record.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="cancel-reason">Reason</Label>
            <Textarea
              id="cancel-reason"
              value={cancelReason}
              onChange={(e) => setCancelReason(e.target.value)}
              rows={3}
              placeholder="Why is the trip not going ahead?"
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
