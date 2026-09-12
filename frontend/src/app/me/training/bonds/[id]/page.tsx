'use client';

/**
 * Area 25 slice 6 — one service bond: read the terms, accept them. The owed bond
 * self-accept screen (training W3 slice-8 residual).
 *
 * Acceptance is deliberately heavyweight: the full terms text is on screen, the accept
 * button restates the duration and the amount, and the server stamps the acceptance date
 * and derives the bond window (start defaults to the training's completion date). The
 * server also enforces everything that matters — own bond only, PendingAcceptance only —
 * with messages this page surfaces as-is. HR's accept-on-behalf, waive, settle and exit
 * acts stay on the desk register (/hr/service-bonds).
 */

import { useState } from 'react';
import { useParams } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Scale, CheckCircle2, CalendarRange } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
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
import { trainingServiceBondService } from '@/services/hr/outcomes.service';

const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function MyBondDetailPage() {
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [acceptOpen, setAcceptOpen] = useState(false);
  const [notes, setNotes] = useState('');
  const [busy, setBusy] = useState(false);

  const queryKey = ['me', 'training', 'bonds', id];
  const { data: bond, isLoading, isError } = useQuery({
    queryKey,
    queryFn: () => trainingServiceBondService.getById(id),
    enabled: !!id,
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !bond) {
    return (
      <EmptyState
        title="Bond not found"
        description="It may have been removed, or it may not be yours to see."
      />
    );
  }

  const pending = bond.status === 'PendingAcceptance';

  const accept = async (): Promise<boolean> => {
    setBusy(true);
    try {
      await trainingServiceBondService.accept(id, notes.trim() || undefined);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey }),
        queryClient.invalidateQueries({ queryKey: ['me', 'training', 'bonds', 'mine'] }),
      ]);
      toast({
        title: 'Bond accepted',
        description: 'The obligation window has started — its dates are on the record below.',
      });
      setNotes('');
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to accept the bond.',
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
        title={bond.programName ?? 'Service bond'}
        description={`${bond.nominationNumber ?? ''} · ${bond.bondDurationMonths} months · ${bond.currency} ${bond.bondAmount.toLocaleString()}`}
        backHref="/me/training/bonds"
        actions={<StatusBadge status={bond.statusName} />}
      />

      {pending && (
        <Alert className="border-amber-500/50 [&>svg]:text-amber-600">
          <Scale className="h-4 w-4" />
          <AlertTitle>This bond is waiting for your acceptance</AlertTitle>
          <AlertDescription>
            Read the terms below. Accepting starts a {bond.bondDurationMonths}-month service
            obligation; leaving before it ends means repaying a pro-rated share of{' '}
            {bond.currency} {bond.bondAmount.toLocaleString()}.
          </AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader>
          <CardTitle>The terms</CardTitle>
          <CardDescription>
            Set by HR against nomination {bond.nominationNumber ?? '—'}. If anything here is
            wrong, contact HR before accepting — pending terms can still be corrected.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4 py-2">
          <div className="grid gap-4 sm:grid-cols-3">
            <Detail label="Serve for" value={`${bond.bondDurationMonths} months`} />
            <Detail label="Bond amount" value={`${bond.currency} ${bond.bondAmount.toLocaleString()}`} />
            <Detail label="Programme" value={bond.programName ?? '—'} />
          </div>
          <div>
            <p className="text-sm text-muted-foreground">Full terms</p>
            <p className="mt-1 whitespace-pre-wrap rounded-md border bg-muted/30 p-3 text-sm font-medium">
              {bond.termsText || 'No further terms were recorded — the duration and amount above are the obligation.'}
            </p>
          </div>
        </CardContent>
      </Card>

      {pending ? (
        <Card>
          <CardContent className="flex flex-wrap items-center justify-between gap-3 py-4">
            <p className="text-sm text-muted-foreground">
              Your acceptance is recorded with today&apos;s date, from your own account.
            </p>
            <Button onClick={() => setAcceptOpen(true)}>
              <CheckCircle2 className="mr-2 h-4 w-4" /> Accept these terms
            </Button>
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <CalendarRange className="h-4 w-4" /> The record
            </CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 py-2 sm:grid-cols-2">
            {bond.acceptedByEmployee && (
              <>
                <Detail label="Accepted on" value={fmt(bond.acceptedDate)} />
                <Detail
                  label="Recorded by"
                  value={bond.acceptanceRecordedByName ?? 'You'}
                />
                {bond.acceptanceNotes && (
                  <div className="sm:col-span-2">
                    <p className="text-sm text-muted-foreground">Acceptance notes</p>
                    <p className="font-medium">{bond.acceptanceNotes}</p>
                  </div>
                )}
              </>
            )}
            {bond.bondStartDate && (
              <Detail
                label="Obligation window"
                value={`${fmt(bond.bondStartDate)} – ${fmt(bond.bondEndDate)}`}
              />
            )}
            {bond.status === 'Active' && (
              <div>
                <p className="text-sm text-muted-foreground">Remaining</p>
                <p className="font-medium">
                  {bond.monthsRemaining > 0 ? `${bond.monthsRemaining} months` : 'Almost served'}
                </p>
              </div>
            )}
            {bond.exitDate && <Detail label="Exit recorded" value={fmt(bond.exitDate)} />}
            {typeof bond.repaymentAmount === 'number' && bond.repaymentAmount > 0 && (
              <div>
                <p className="text-sm text-muted-foreground">Repayment owed</p>
                <p className="font-medium text-destructive">
                  {bond.currency} {bond.repaymentAmount.toLocaleString()}
                </p>
              </div>
            )}
            {bond.settledDate && <Detail label="Settled on" value={fmt(bond.settledDate)} />}
            {bond.waivedDate && (
              <div className="sm:col-span-2">
                <p className="text-sm text-muted-foreground">Waived</p>
                <p className="font-medium">
                  {fmt(bond.waivedDate)}
                  {bond.waiverReason ? ` — ${bond.waiverReason}` : ''}
                </p>
              </div>
            )}
            {bond.status === 'Fulfilled' && (
              <div className="sm:col-span-2">
                <Badge variant="default">Obligation served in full — nothing owed</Badge>
              </div>
            )}
            {bond.status === 'Cancelled' && (
              <div className="sm:col-span-2 text-sm text-muted-foreground">
                This bond was cancelled — usually because the nomination it was raised on was
                withdrawn or rejected. It carries no obligation.
              </div>
            )}
          </CardContent>
        </Card>
      )}

      <Dialog open={acceptOpen} onOpenChange={(o) => !o && setAcceptOpen(false)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Accept this service bond?</DialogTitle>
            <DialogDescription>
              You commit to serving {bond.bondDurationMonths} months after the training, or
              repaying a pro-rated share of {bond.currency} {bond.bondAmount.toLocaleString()}{' '}
              if you leave earlier. This acceptance is recorded against your account.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2 py-2">
            <Label htmlFor="accept-notes">Notes (optional)</Label>
            <Textarea
              id="accept-notes"
              rows={3}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              maxLength={1000}
              placeholder="Anything you want on the record alongside your acceptance."
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAcceptOpen(false)} disabled={busy}>
              Not yet
            </Button>
            <Button
              disabled={busy}
              onClick={async () => {
                const ok = await accept();
                if (ok) setAcceptOpen(false);
              }}
            >
              {busy ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <CheckCircle2 className="mr-2 h-4 w-4" />
              )}
              I accept the terms
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
