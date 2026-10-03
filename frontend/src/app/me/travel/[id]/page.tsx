'use client';

import { use, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient, useMutation } from '@tanstack/react-query';
import { Loader2, Send, Ban, Globe, Pencil, Undo2, FilePenLine } from 'lucide-react';
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
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { MyTripBeforeYouGo } from '@/components/hr/travel/MyTripBeforeYouGo';
import { MyTripFiles } from '@/components/hr/travel/MyTripFiles';
import { MyTripMessages } from '@/components/hr/travel/MyTripMessages';
import { MyTripMoney } from '@/components/hr/travel/MyTripMoney';
import { MyTripPlan } from '@/components/hr/travel/MyTripPlan';
import { TravelLifecycleNotes } from '@/components/hr/travel/TravelLifecycleNotes';
import { TravelQueryError } from '@/components/hr/travel/TravelQueryError';
import { TravelReasonDialog } from '@/components/hr/travel/TravelReasonDialog';
import {
  TRAVEL_PRIORITY_LABELS,
  TRAVEL_PURPOSE_LABELS,
  TRAVEL_REQUEST_STATUS_LABELS,
  TRAVEL_RISK_LEVEL_LABELS,
  TRAVEL_TYPE_LABELS,
  enumLabel,
} from '@/components/hr/travel/travel-enums';
import { fmtTravelMoney as fmtMoney } from '@/components/hr/travel/travel-format';
import { useToast } from '@/hooks/use-toast';
import { travelService } from '@/services/hr/travel.service';

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
 * One of your own travel requests.
 *
 * <b>Deliberately narrower than the desk's page.</b> The self-service surface exposes read, amend,
 * submit, recall, withdraw and — once approved — request a change (lane 1); since lane 7 (slice 7c1)
 * the traveller also reads what the desk arranged — the itinerary in force and the bookings, the
 * risk assessment (and acknowledges it, E1), the destination's alerts and health requirements, the
 * visas, the insurance, the advances and claims — each through `api/staff-travel/me`; since slice 7c2
 * they attach files (and remove their own before submission, D-40) and write to the desk (D-41).
 * Pointing this page at the desk's routes would 403 for the employee it exists to serve. Comments the
 * desk marked visible, and the trip's files, arrive embedded on the record, so they are read here
 * without a second call.
 *
 * ⚠ A 404 means the request is not yours. The surface answers the same way for a request that does
 * not exist, so it cannot be used to enumerate ids.
 */
export default function MyTravelRequestDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [cancelOpen, setCancelOpen] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [reasonFor, setReasonFor] = useState<null | 'recall' | 'change'>(null);

  const { data: r, isLoading, isError, error } = useQuery({
    queryKey: ['my-travel-request', id],
    queryFn: () => travelService.getMineById(id),
    retry: false,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['my-travel-request', id] });
    await queryClient.invalidateQueries({ queryKey: ['my-travel-requests'] });
  };

  const submit = useMutation({
    mutationFn: () => travelService.submitMine(id),
    onSuccess: async (result) => {
      toast({ title: 'Sent for approval' });
      // Did not stop it, but the approver will see it too — approved leave over the same days.
      if (result?.warnings?.length)
        toast({ title: 'Please note', description: result.warnings.join(' ') });
      await refresh();
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not submit', description: e.message }),
  });

  // Lane 1: take a submission back to change it. If the desk submitted it, only they can recall it —
  // the server says so.
  const recall = useMutation({
    mutationFn: (reason: string) => travelService.recallMine(id, reason || undefined),
    onSuccess: async () => {
      toast({ title: 'Recalled', description: 'It is a draft again — change it and send it when ready.' });
      await refresh();
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not recall', description: e.message }),
  });

  // Lane 1 (D-9): the way to change a trip once it is approved.
  const requestChange = useMutation({
    mutationFn: (reason: string) => travelService.requestChangeMine(id, reason),
    onSuccess: async () => {
      toast({ title: 'Back with you to change', description: 'Edit the trip, then send it for approval again.' });
      await refresh();
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not ask for the change', description: e.message }),
  });

  const cancel = useMutation({
    mutationFn: () => travelService.cancelMine(id, { cancellationReason: cancelReason.trim() }),
    onSuccess: async () => {
      setCancelOpen(false);
      setCancelReason('');
      toast({ title: 'Request withdrawn' });
      await refresh();
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not withdraw', description: e.message }),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-10">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }
  // A 404 is "not yours" and keeps its own wording; any other failure says the read failed.
  if (!r && isError && (error as { status?: number } | null)?.status !== 404) {
    return (
      <div className="p-6">
        <TravelQueryError error={error} what="this travel request" />
      </div>
    );
  }
  if (!r) {
    return (
      <div className="p-6">
        <EmptyState title="Not available" description="This travel request is not one of yours." />
      </div>
    );
  }

  const isEditable = r.status === 'Draft' || r.status === 'ReturnedForRevision';
  // A trip under way is completed by the travel desk, not withdrawn (lane 1, O-11).
  const isLive = !['Cancelled', 'Rejected', 'Completed', 'Closed', 'InProgress'].includes(r.status);
  // The server refuses a past departure from this surface: the travel desk submits it, with the
  // reason it is late (lane 1). Say so instead of offering a button that can only fail.
  const departed = r.travelStartDate.slice(0, 10) < todayUtc();
  // Only what the desk chose to share. Since lane 1 (slice 1c, finding A6) the server sends nothing
  // else — internal notes and policy-exception decisions never reach this browser; the filter stays
  // as a second guard, not the only one.
  const visibleComments = (r.comments ?? []).filter((c) => c.isVisibleToTraveller);

  return (
    <div className="space-y-6">
      <PageHeader
        title={r.requestNumber}
        description={`${r.originCity} → ${r.destinationCity}, ${fmtDate(r.travelStartDate)}`}
        backHref="/me/travel"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={enumLabel(TRAVEL_REQUEST_STATUS_LABELS, r.status)} />
            {isEditable && (
              <>
                <Button
                  variant="outline"
                  onClick={() => router.push(`/me/travel/${id}/edit`)}
                >
                  <Pencil className="mr-2 h-4 w-4" /> Edit
                </Button>
                <Button
                  onClick={() => submit.mutate()}
                  disabled={submit.isPending || departed}
                  title={departed ? 'The departure date has passed — change the dates, or ask the travel desk to submit it.' : undefined}
                >
                  <Send className="mr-2 h-4 w-4" /> Send for approval
                </Button>
              </>
            )}
            {r.status === 'Submitted' && (
              <Button variant="outline" onClick={() => setReasonFor('recall')}>
                <Undo2 className="mr-2 h-4 w-4" /> Recall
              </Button>
            )}
            {r.status === 'Approved' && (
              <Button variant="outline" onClick={() => setReasonFor('change')}>
                <FilePenLine className="mr-2 h-4 w-4" /> Request change
              </Button>
            )}
            {isLive && (
              <Button variant="outline" onClick={() => setCancelOpen(true)}>
                <Ban className="mr-2 h-4 w-4" /> Withdraw
              </Button>
            )}
          </div>
        }
      />

      {isEditable && departed && (
        <p role="note" className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
          This trip&apos;s departure date ({fmtDate(r.travelStartDate)}) has passed, so it cannot be sent
          for approval from here. Change the dates if they moved, or ask the travel desk to submit it with
          the reason it is late.
        </p>
      )}

      <Tabs defaultValue="trip">
        <TabsList className="flex-wrap">
          <TabsTrigger value="trip">The trip</TabsTrigger>
          <TabsTrigger value="before">Before you go</TabsTrigger>
          <TabsTrigger value="plan">Itinerary &amp; bookings</TabsTrigger>
          <TabsTrigger value="money">Money</TabsTrigger>
          <TabsTrigger value="messages">
            Messages{visibleComments.length > 0 && ` (${visibleComments.length})`}
          </TabsTrigger>
          <TabsTrigger value="files">
            Files{(r.attachments ?? []).length > 0 && ` (${(r.attachments ?? []).length})`}
          </TabsTrigger>
        </TabsList>

        <TabsContent value="before" className="pt-4">
          <MyTripBeforeYouGo request={r} />
        </TabsContent>
        <TabsContent value="plan" className="pt-4">
          <MyTripPlan request={r} />
        </TabsContent>
        <TabsContent value="money" className="pt-4">
          <MyTripMoney request={r} />
        </TabsContent>

        <TabsContent value="trip" className="space-y-6 pt-4">
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">The trip</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
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
              <InfoRow label="Estimated" value={fmtMoney(r.estimatedTotalCost, r.currencyCode)} />
              <InfoRow label="Approved budget" value={fmtMoney(r.approvedBudget, r.currencyCode)} />
              <InfoRow label="Submitted" value={fmtDateTime(r.submittedAt)} />
              <InfoRow
                label="Approved"
                value={r.approvedAt ? [fmtDateTime(r.approvedAt), r.approvedByName].filter(Boolean).join(' · ') : '—'}
              />
              <InfoRow label="Visa required" value={r.requiresVisa ? 'Yes' : 'No'} />
              <InfoRow label="Health clearance" value={r.requiresHealthClearance ? 'Yes' : 'No'} />
              <InfoRow label="Risk level" value={enumLabel(TRAVEL_RISK_LEVEL_LABELS, r.riskLevel)} />
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
                  {r.status === 'Rejected' ? 'Why it was rejected' : 'Why it was withdrawn'}
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

        {/* Lane 7 (7c2): the desk's shared notes moved here from "The trip", with the traveller's replies (D-41). */}
        <TabsContent value="messages" className="pt-4">
          <MyTripMessages request={r} />
        </TabsContent>
        <TabsContent value="files" className="pt-4">
          <MyTripFiles request={r} />
        </TabsContent>
      </Tabs>

      <TravelReasonDialog
        open={reasonFor === 'recall'}
        onOpenChange={(open) => setReasonFor(open ? 'recall' : null)}
        title="Recall this request"
        description="It comes back to you as a draft, to change and send again. The approval in progress is withdrawn."
        optional
        placeholder="What you want to change, if you'd like the approver to know."
        confirmLabel="Recall"
        pending={recall.isPending}
        onConfirm={(reason) => recall.mutateAsync(reason)}
      />

      <TravelReasonDialog
        open={reasonFor === 'change'}
        onOpenChange={(open) => setReasonFor(open ? 'change' : null)}
        title="Ask for a change to this approved trip"
        description="It comes back to you to edit, then goes for approval again. Bookings and any advance stay with the trip."
        label="What has changed"
        placeholder="New dates, a different destination, a higher cost…"
        confirmLabel="Ask for the change"
        pending={requestChange.isPending}
        onConfirm={(reason) => requestChange.mutateAsync(reason)}
      />

      <Dialog open={cancelOpen} onOpenChange={setCancelOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Withdraw this request</DialogTitle>
            <DialogDescription>
              A withdrawn request cannot be revived — raise a new one if the trip goes ahead later.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="withdraw-reason">Reason</Label>
            <Textarea
              id="withdraw-reason"
              value={cancelReason}
              onChange={(e) => setCancelReason(e.target.value)}
              rows={3}
              placeholder="Why are you no longer travelling?"
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
              Withdraw
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
