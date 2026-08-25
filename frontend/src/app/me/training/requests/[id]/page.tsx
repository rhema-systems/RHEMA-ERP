'use client';

/**
 * Area 25 slice 6 — one training request, from the requester's side.
 *
 * The desk detail at /hr/training/requests/[id] carries the approve/reject acts and stays;
 * this page carries the OWNER's acts: submit a draft, delete a draft, and read the outcome
 * (including the rejection reason, which the reject dialog promises is shown to whoever
 * raised it — this is where that promise is kept).
 */

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Send, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { trainingRequestService } from '@/services/hr/training-request.service';
import { TRAINING_REQUEST_STATUS_OPTIONS } from '@/types/hr/training-delivery';

const statusLabel = (v: string) =>
  TRAINING_REQUEST_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;
const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function MyTrainingRequestDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [submitOpen, setSubmitOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [busy, setBusy] = useState(false);

  const queryKey = ['me', 'training', 'requests', id];
  const { data: request, isLoading, isError } = useQuery({
    queryKey,
    queryFn: () => trainingRequestService.getById(id),
    enabled: !!id,
  });

  const invalidate = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey }),
      queryClient.invalidateQueries({ queryKey: ['me', 'training', 'requests'] }),
    ]);

  const act = async (label: string, fn: () => Promise<unknown>) => {
    setBusy(true);
    try {
      await fn();
      await invalidate();
      toast({ title: label });
      return true;
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || `${label} failed.`, variant: 'destructive' });
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

  if (isError || !request) {
    return (
      <EmptyState
        title="Request not found"
        description="It may have been removed, or it may not be yours to see."
      />
    );
  }

  const isDraft = request.status === 'Draft';

  return (
    <div className="space-y-6">
      <PageHeader
        title={request.requestedTrainingTitle}
        description={`${request.requestNumber} · raised ${fmt(request.requestDate)}`}
        backHref="/me/training"
        actions={
          <div className="flex items-center gap-2">
            <StatusBadge status={statusLabel(request.status)} />
            {isDraft && (
              <>
                <Button size="sm" onClick={() => setSubmitOpen(true)}>
                  <Send className="mr-2 h-4 w-4" /> Submit
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  className="text-destructive hover:text-destructive"
                  onClick={() => setDeleteOpen(true)}
                >
                  <Trash2 className="mr-2 h-4 w-4" /> Delete
                </Button>
              </>
            )}
          </div>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle>Your request</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 py-2 sm:grid-cols-2">
          <Detail label="Raised on" value={fmt(request.requestDate)} />
          <Detail label="Linked programme" value={request.linkedProgramName ?? 'Not in the catalog'} />
          <div className="sm:col-span-2">
            <p className="text-sm text-muted-foreground">Description</p>
            <p className="font-medium">{request.description || '—'}</p>
          </div>
          <div className="sm:col-span-2">
            <p className="text-sm text-muted-foreground">Justification</p>
            <p className="font-medium">{request.justification || '—'}</p>
          </div>
        </CardContent>
      </Card>

      {(request.status === 'Approved' || request.status === 'Rejected') && (
        <Card>
          <CardHeader>
            <CardTitle>The decision</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 py-2 sm:grid-cols-2">
            <Detail label="Decided by" value={request.approvedByName ?? '—'} />
            <Detail label="Decided on" value={fmt(request.approvalDate)} />
            {request.status === 'Approved' && request.linkedProgramName && (
              <div className="sm:col-span-2">
                <p className="text-sm text-muted-foreground">What happens next</p>
                <p className="font-medium">
                  Your request was linked to {request.linkedProgramName} — look out for a
                  nomination when a run is scheduled.
                </p>
              </div>
            )}
            {request.rejectionReason && (
              <div className="sm:col-span-2">
                <p className="text-sm text-muted-foreground">Reason</p>
                <p className="font-medium text-destructive">{request.rejectionReason}</p>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      <ConfirmationDialog
        open={submitOpen}
        onOpenChange={setSubmitOpen}
        title="Submit for approval?"
        description="It moves out of draft and goes to the training desk for a decision."
        confirmText="Submit"
        isLoading={busy}
        onConfirm={async () => {
          const ok = await act('Submitted', () => trainingRequestService.submit(id));
          if (ok) setSubmitOpen(false);
          return ok;
        }}
      />

      <ConfirmationDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title="Delete this draft?"
        description={`"${request.requestedTrainingTitle}" is removed. You can always raise it again.`}
        confirmText="Delete"
        variant="destructive"
        isLoading={busy}
        onConfirm={async () => {
          const ok = await act('Deleted', () => trainingRequestService.remove(id));
          if (ok) {
            setDeleteOpen(false);
            router.push('/me/training');
          }
          return ok;
        }}
      />
    </div>
  );
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-sm text-muted-foreground">{label}</p>
      <p className="font-medium">{value}</p>
    </div>
  );
}
