'use client';

/**
 * Area 25 slice 6 — one nomination, from the nominee's side.
 *
 * The desk detail at /hr/training/nominations/[id] is the approver's view (issue certificate,
 * open schedule) and stays where it is; this page is the SUBJECT's view of the same record —
 * what am I nominated for, where the approval sits, how it ended. The server's self-arm
 * (own id 200 / other's 403) is the actual gate; this page just renders what it is given.
 *
 * Withdraw is offered pre-approval only. ⚠ Withdrawing also CANCELS any service bond raised
 * on the nomination (measured in the slice-6 probe) — the dialog says so.
 */

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Award, Scale, Undo2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
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
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { trainingNominationService } from '@/services/hr/training-nomination.service';
import { trainingServiceBondService } from '@/services/hr/outcomes.service';
import {
  NOMINATION_STATUS_OPTIONS,
  NOMINATION_TYPE_OPTIONS,
  TRAINING_COMPLETION_STATUS_OPTIONS,
} from '@/types/hr/training-delivery';

const statusLabel = (v: string) => NOMINATION_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;
const typeLabel = (v: string) => NOMINATION_TYPE_OPTIONS.find((o) => o.value === v)?.label ?? v;
const completionLabel = (v: string) =>
  TRAINING_COMPLETION_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;
const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

// The server refuses withdraw only from Withdrawn/Rejected; the UI additionally hides it
// once a completion exists — pulling out of training already delivered is not a real act.
const NOT_WITHDRAWABLE = ['Withdrawn', 'Rejected'];

export default function MyNominationDetailPage() {
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [withdrawOpen, setWithdrawOpen] = useState(false);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);

  const queryKey = ['me', 'training', 'nominations', id];
  const { data: n, isLoading, isError } = useQuery({
    queryKey,
    queryFn: () => trainingNominationService.getById(id),
    enabled: !!id,
  });

  // The bond ride-along: a bond is keyed by nomination, and the /mine read is the only
  // bond read a plain employee holds besides the per-id one.
  const { data: bonds } = useQuery({
    queryKey: ['me', 'training', 'bonds', 'mine'],
    queryFn: () => trainingServiceBondService.getMine(),
  });
  const bond = useMemo(() => (bonds ?? []).find((b) => b.nominationId === id), [bonds, id]);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !n) {
    return (
      <EmptyState
        title="Nomination not found"
        description="It may have been removed, or it may not be yours to see."
      />
    );
  }

  const canWithdraw = !NOT_WITHDRAWABLE.includes(n.status) && !n.hasCompletionRecord;

  const withdraw = async (): Promise<boolean> => {
    setBusy(true);
    try {
      await trainingNominationService.withdraw(id, reason.trim() || null);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey }),
        queryClient.invalidateQueries({ queryKey: ['me', 'training'] }),
      ]);
      toast({ title: 'Nomination withdrawn' });
      setReason('');
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to withdraw the nomination.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title={n.programName}
        description={`${n.nominationNumber} · ${fmt(n.trainingStartDate)} – ${fmt(n.trainingEndDate)}`}
        backHref="/me/training"
        actions={
          <div className="flex items-center gap-2">
            <StatusBadge status={statusLabel(n.status)} />
            {canWithdraw && (
              <Button variant="outline" size="sm" onClick={() => setWithdrawOpen(true)}>
                <Undo2 className="mr-2 h-4 w-4" /> Withdraw
              </Button>
            )}
          </div>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle>Nomination</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 py-2 sm:grid-cols-2">
          <Detail label="Programme" value={n.programName} />
          <Detail label="Schedule" value={n.scheduleNumber} />
          <Detail label="Training dates" value={`${fmt(n.trainingStartDate)} – ${fmt(n.trainingEndDate)}`} />
          <Detail label="Type" value={typeLabel(n.type)} />
          <Detail label="Nominated by" value={n.nominatedByName ?? '—'} />
          <Detail label="Nominated on" value={fmt(n.nominationDate)} />
          <div className="sm:col-span-2">
            <p className="text-sm text-muted-foreground">Justification</p>
            <p className="font-medium">{n.justification || '—'}</p>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Where the approval sits</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 py-2 sm:grid-cols-2">
          <Detail label="Supervisor" value={n.supervisorApprovedByName ?? 'Not yet'} />
          <Detail label="Supervisor decided" value={fmt(n.supervisorApprovalDate)} />
          {n.supervisorComments && (
            <div className="sm:col-span-2">
              <p className="text-sm text-muted-foreground">Supervisor comments</p>
              <p className="font-medium">{n.supervisorComments}</p>
            </div>
          )}
          <Detail label="HR" value={n.hrApprovedByName ?? 'Not yet'} />
          <Detail label="HR decided" value={fmt(n.hrApprovalDate)} />
          {n.hrComments && (
            <div className="sm:col-span-2">
              <p className="text-sm text-muted-foreground">HR comments</p>
              <p className="font-medium">{n.hrComments}</p>
            </div>
          )}
          {n.rejectionReason && (
            <div className="sm:col-span-2">
              <p className="text-sm text-muted-foreground">Rejection reason</p>
              <p className="font-medium text-destructive">{n.rejectionReason}</p>
            </div>
          )}
        </CardContent>
      </Card>

      {bond && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Scale className="h-4 w-4" /> Service bond on this training
            </CardTitle>
          </CardHeader>
          <CardContent className="flex flex-wrap items-center justify-between gap-3 py-2">
            <div className="text-sm">
              <span className="font-medium">
                {bond.bondDurationMonths} months · {bond.currency}{' '}
                {bond.bondAmount.toLocaleString()}
              </span>
              <span className="ml-2 text-muted-foreground">{bond.statusName}</span>
            </div>
            <Button
              size="sm"
              variant={bond.status === 'PendingAcceptance' ? 'default' : 'outline'}
              asChild
            >
              <Link href={`/me/training/bonds/${bond.id}`}>
                {bond.status === 'PendingAcceptance' ? 'Review & accept the terms' : 'View the bond'}
              </Link>
            </Button>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Outcome</CardTitle>
        </CardHeader>
        <CardContent className="py-2">
          {n.hasCompletionRecord && n.completionRecord ? (
            <div className="grid gap-4 sm:grid-cols-4">
              <Detail label="Status" value={completionLabel(n.completionRecord.status)} />
              <Detail label="Completed" value={fmt(n.completionRecord.completionDate)} />
              <Detail
                label="Score"
                value={
                  typeof n.completionRecord.finalScore === 'number'
                    ? String(n.completionRecord.finalScore)
                    : '—'
                }
              />
              <div>
                <p className="text-sm text-muted-foreground">Result</p>
                <div className="mt-1 flex items-center gap-2">
                  <Badge variant={n.completionRecord.isPassed ? 'default' : 'destructive'}>
                    {n.completionRecord.isPassed ? 'Passed' : 'Not passed'}
                  </Badge>
                  {n.completionRecord.isVerifiedByManager && (
                    <Badge variant="secondary">Verified</Badge>
                  )}
                </div>
              </div>
            </div>
          ) : (
            <EmptyState
              icon={Award}
              title="No completion recorded"
              description="Once the training has run and been closed off, the outcome appears here."
            />
          )}
        </CardContent>
      </Card>

      <Dialog open={withdrawOpen} onOpenChange={(o) => !o && setWithdrawOpen(false)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Withdraw from this training?</DialogTitle>
            <DialogDescription>
              Your nomination for {n.programName} is withdrawn and your seat freed.
              {bond && bond.status !== 'Cancelled'
                ? ' The service bond raised on this nomination is cancelled with it.'
                : ''}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2 py-2">
            <Label htmlFor="withdraw-reason">Reason (optional)</Label>
            <Textarea
              id="withdraw-reason"
              rows={3}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              maxLength={1000}
              placeholder="For example: clashes with a project deadline."
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setWithdrawOpen(false)} disabled={busy}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              disabled={busy}
              onClick={async () => {
                const ok = await withdraw();
                if (ok) setWithdrawOpen(false);
              }}
            >
              {busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Undo2 className="mr-2 h-4 w-4" />}
              Withdraw
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
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
