'use client';

import { use, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient, useMutation } from '@tanstack/react-query';
import { Loader2, Send, Ban, Globe, Pencil } from 'lucide-react';
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
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { travelService } from '@/services/hr/travel.service';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');
const humanize = (v: string) => v.replace(/([a-z])([A-Z])/g, '$1 $2');

const fmtMoney = (amount?: number | null, currency?: string) =>
  amount === null || amount === undefined
    ? '—'
    : new Intl.NumberFormat(undefined, {
        style: 'currency',
        currency: currency || 'GHS',
        currencyDisplay: 'code',
      }).format(amount);

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
 * submit and withdraw and nothing else — there is no comment, attachment or approval endpoint under
 * `api/staff-travel/me`, and pointing this page at the desk's routes to get them would 403 for the
 * employee it exists to serve. Comments the desk marked visible arrive embedded on the record, so
 * they are read here without a second call.
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

  const { data: r, isLoading } = useQuery({
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
    onSuccess: async () => {
      toast({ title: 'Sent for approval' });
      await refresh();
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not submit', description: e.message }),
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
  if (!r) {
    return (
      <div className="p-6">
        <EmptyState title="Not available" description="This travel request is not one of yours." />
      </div>
    );
  }

  const isEditable = r.status === 'Draft' || r.status === 'ReturnedForRevision';
  const isLive = !['Cancelled', 'Rejected', 'Completed', 'Closed'].includes(r.status);
  // Only what the desk chose to share — an internal note is not the traveller's to read.
  const visibleComments = (r.comments ?? []).filter((c) => c.isVisibleToTraveller);

  return (
    <div className="space-y-6">
      <PageHeader
        title={r.requestNumber}
        description={`${r.originCity} → ${r.destinationCity}, ${fmtDate(r.travelStartDate)}`}
        backHref="/me/travel"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={humanize(r.status)} />
            {isEditable && (
              <>
                <Button
                  variant="outline"
                  onClick={() => router.push(`/me/travel/${id}/edit`)}
                >
                  <Pencil className="mr-2 h-4 w-4" /> Edit
                </Button>
                <Button onClick={() => submit.mutate()} disabled={submit.isPending}>
                  <Send className="mr-2 h-4 w-4" /> Send for approval
                </Button>
              </>
            )}
            {isLive && (
              <Button variant="outline" onClick={() => setCancelOpen(true)}>
                <Ban className="mr-2 h-4 w-4" /> Withdraw
              </Button>
            )}
          </div>
        }
      />

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">The trip</CardTitle>
        </CardHeader>
        <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
          <InfoRow label="Type" value={humanize(r.travelType)} />
          <InfoRow label="Purpose" value={humanize(r.travelPurpose)} />
          <InfoRow label="Priority" value={r.priority} />
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
          <InfoRow label="Visa required" value={r.requiresVisa ? 'Yes' : 'No'} />
          <InfoRow label="Health clearance" value={r.requiresHealthClearance ? 'Yes' : 'No'} />
          <InfoRow label="Risk level" value={r.riskLevel} />
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

      {visibleComments.length > 0 && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Notes from the travel desk</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {visibleComments.map((c) => (
              <div key={c.id} className="rounded-md border p-3">
                <div className="flex items-center justify-between gap-3">
                  <p className="text-sm font-medium">{c.authorName || 'Travel desk'}</p>
                  <p className="text-xs text-muted-foreground">{fmtDateTime(c.createdAt)}</p>
                </div>
                <p className="mt-1 text-sm whitespace-pre-wrap">{c.body}</p>
              </div>
            ))}
          </CardContent>
        </Card>
      )}

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
