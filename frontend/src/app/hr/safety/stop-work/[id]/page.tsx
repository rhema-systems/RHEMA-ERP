'use client';

import { useState } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Route, Wrench, CheckCircle2, XCircle } from 'lucide-react';
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
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { safetyStopWorkService } from '@/services/hr/safety-stop-work.service';
import type { SheStopWorkOrder, SheStopWorkStatus } from '@/types/hr/safety-audits';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');

function StatusBadge({ status }: { status: SheStopWorkStatus }) {
  switch (status) {
    case 'Cleared':
      return <Badge variant="secondary">Cleared — work resumed</Badge>;
    case 'Cancelled':
      return <Badge variant="outline">Cancelled</Badge>;
    case 'Resolved':
      return <Badge>Resolved — awaiting clearance</Badge>;
    default:
      return <Badge variant="destructive">Work stopped</Badge>;
  }
}

function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-2 border-b py-1 text-sm">
      <dt className="text-muted-foreground shrink-0">{label}</dt>
      <dd className="text-right font-medium">{value}</dd>
    </div>
  );
}

/**
 * One stop-work order. The record itself is testimony and cannot be edited —
 * only the lifecycle moves: route → resolve → clear (or cancel a false alarm).
 * Clearing an unresolved order is refused by the server.
 */
export default function StopWorkDetailPage() {
  const { id } = useParams<{ id: string }>();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [dialog, setDialog] = useState<'route' | 'resolve' | 'clear' | 'cancel' | null>(null);
  const [busy, setBusy] = useState(false);
  const [actorId, setActorId] = useState<string | null>(null);
  const [text, setText] = useState('');

  const { data: order, isLoading } = useQuery({
    queryKey: ['hr', 'safety-stop-work', 'detail', id],
    queryFn: () => safetyStopWorkService.getById(id),
    enabled: !!id,
  });

  const act = async (title: string, fn: () => Promise<unknown>) => {
    setBusy(true);
    try {
      await fn();
      await queryClient.invalidateQueries({ queryKey: ['hr', 'safety-stop-work'] });
      toast({ title });
      setDialog(null);
      setActorId(null);
      setText('');
    } catch (error: any) {
      toast({ title: 'Refused', description: error?.message || 'The action failed.', variant: 'destructive' });
    } finally {
      setBusy(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }
  if (!order) {
    return (
      <div className="p-6">
        <EmptyState title="Stop-work order not found" description="Check the register." />
      </div>
    );
  }

  const o: SheStopWorkOrder = order;
  const open = o.status === 'Raised' || o.status === 'UnderReview';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={o.orderNumber}
        description={`Raised by ${o.raisedByName} on ${fmtDate(o.raisedDate)} — ${o.locationName ?? 'location unspecified'}${o.specificArea ? `, ${o.specificArea}` : ''}.`}
        backHref="/hr/safety/stop-work"
        actions={
          <div className="flex flex-wrap gap-2">
            {open && (
              <>
                <Button variant="outline" onClick={() => { setActorId(null); setDialog('route'); }} disabled={busy}>
                  <Route className="mr-2 h-4 w-4" />
                  Route
                </Button>
                <Button onClick={() => { setActorId(null); setText(''); setDialog('resolve'); }} disabled={busy}>
                  <Wrench className="mr-2 h-4 w-4" />
                  Resolve
                </Button>
                <Button variant="outline" onClick={() => { setText(''); setDialog('cancel'); }} disabled={busy}>
                  <XCircle className="mr-2 h-4 w-4" />
                  Cancel (false alarm)
                </Button>
              </>
            )}
            {o.status === 'Resolved' && (
              <Button onClick={() => { setActorId(null); setText(''); setDialog('clear'); }} disabled={busy}>
                <CheckCircle2 className="mr-2 h-4 w-4" />
                Clear — resume work
              </Button>
            )}
          </div>
        }
      />

      <div className="flex flex-wrap items-center gap-2">
        <StatusBadge status={o.status} />
        {o.permitNumber && (
          <Link href={`/hr/safety/permits/${o.permitToWorkId}`}>
            <Badge variant="outline" className="cursor-pointer">Permit {o.permitNumber}</Badge>
          </Link>
        )}
        {o.hazardName && (
          <Link href={`/hr/safety/hazards/${o.hazardId}`}>
            <Badge variant="outline" className="cursor-pointer">Hazard: {o.hazardName}</Badge>
          </Link>
        )}
        {o.incidentNumber && (
          <Link href={`/hr/safety/incidents/${o.incidentId}`}>
            <Badge variant="outline" className="cursor-pointer">Incident {o.incidentNumber}</Badge>
          </Link>
        )}
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">What was stopped, and why</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <p className="text-sm">{o.workDescription}</p>
            <p className="text-destructive text-sm">{o.reasonDescription}</p>
            {o.immediateActionsTaken && (
              <p className="text-muted-foreground text-sm">Immediate actions: {o.immediateActionsTaken}</p>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Lifecycle</CardTitle>
          </CardHeader>
          <CardContent>
            <dl>
              <Row label="Routed to" value={o.routedToName ? `${o.routedToName}, ${fmtDate(o.routedDate)}` : '—'} />
              <Row label="Resolved" value={o.resolvedByName ? `${o.resolvedByName}, ${fmtDate(o.resolvedDate)}` : '—'} />
              <Row label="Cleared" value={o.clearedByName ? `${o.clearedByName}, ${fmtDate(o.clearedDate)}` : '—'} />
              {o.status === 'Cancelled' && (
                <Row label="Cancelled" value={`${o.cancelledByName ?? '—'}, ${fmtDate(o.cancelledDate)}`} />
              )}
            </dl>
            {o.resolutionDescription && (
              <p className="text-muted-foreground mt-3 text-sm">Resolution: {o.resolutionDescription}</p>
            )}
            {o.clearanceNotes && (
              <p className="text-muted-foreground mt-1 text-sm">Clearance: {o.clearanceNotes}</p>
            )}
            {o.cancellationReason && (
              <p className="text-muted-foreground mt-1 text-sm">Cancellation: {o.cancellationReason}</p>
            )}
          </CardContent>
        </Card>
      </div>

      <Dialog open={dialog === 'route'} onOpenChange={(v) => !v && setDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Route for resolution</DialogTitle>
            <DialogDescription>Assigns the responsible manager; the order moves to Under Review.</DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label>Route to</Label>
            <EmployeePicker value={actorId} onChange={setActorId} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={busy || !actorId}
              onClick={() => void act('Order routed', () => safetyStopWorkService.route(o.id, actorId!))}
            >
              Route
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={dialog === 'resolve'} onOpenChange={(v) => !v && setDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record resolution</DialogTitle>
            <DialogDescription>What was done about the danger. Clearance to resume is a separate step.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Resolved by</Label>
              <EmployeePicker value={actorId} onChange={setActorId} />
            </div>
            <div className="space-y-2">
              <Label>Resolution</Label>
              <Textarea rows={3} value={text} onChange={(e) => setText(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={busy || !actorId || text.trim().length === 0}
              onClick={() =>
                void act('Resolution recorded', () =>
                  safetyStopWorkService.resolve(o.id, text.trim(), actorId!),
                )
              }
            >
              Resolve
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={dialog === 'clear'} onOpenChange={(v) => !v && setDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Clear — authorise resumption</DialogTitle>
            <DialogDescription>Confirms the stopped work is safe to resume.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Cleared by</Label>
              <EmployeePicker value={actorId} onChange={setActorId} />
            </div>
            <div className="space-y-2">
              <Label>Clearance notes</Label>
              <Textarea rows={2} value={text} onChange={(e) => setText(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={busy || !actorId}
              onClick={() =>
                void act('Work cleared to resume', () =>
                  safetyStopWorkService.clear(o.id, actorId!, text || null),
                )
              }
            >
              Clear
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={dialog === 'cancel'} onOpenChange={(v) => !v && setDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Cancel — false alarm</DialogTitle>
            <DialogDescription>The order stays on record with its reason.</DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label>Reason</Label>
            <Textarea rows={3} value={text} onChange={(e) => setText(e.target.value)} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Back</Button>
            <Button
              variant="destructive"
              disabled={busy || text.trim().length === 0}
              onClick={() => void act('Order cancelled', () => safetyStopWorkService.cancel(o.id, text.trim()))}
            >
              Cancel order
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
