'use client';

import { useCallback, useEffect, useState } from 'react';
import {
  CheckCircle2,
  FileDiff,
  Loader2,
  RefreshCw,
  ShieldCheck,
} from 'lucide-react';
import { toast } from 'sonner';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
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
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { procurementPurchaseOrderAmendmentService as service } from '@/services/procurement-purchase-order-amendment.service';
import type {
  PurchaseOrderAcknowledgementOutcome,
  PurchaseOrderAmendment,
  PurchaseOrderAmendmentDispatch,
} from '@/types/procurement-purchase-order-amendment';

const errorMessage = (error: unknown) =>
  error instanceof Error ? error.message : 'The request could not be completed.';

export default function ExternalPurchaseOrderAmendmentsPage() {
  const [rows, setRows] = useState<PurchaseOrderAmendment[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [selected, setSelected] = useState<{
    amendment: PurchaseOrderAmendment;
    dispatch: PurchaseOrderAmendmentDispatch;
  } | null>(null);
  const [outcome, setOutcome] =
    useState<PurchaseOrderAcknowledgementOutcome>('Received');
  const [channel, setChannel] = useState('SupplierPortal');
  const [reference, setReference] = useState('');
  const [evidence, setEvidence] = useState('');
  const [comments, setComments] = useState('');

  const load = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      setRows(await service.externalOverview());
    } catch (loadError) {
      setError(errorMessage(loadError));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const acknowledge = async () => {
    if (!selected || !channel.trim() || !reference.trim() || !evidence.trim()) {
      toast.error('Channel, reference, and evidence are required.');
      return;
    }
    try {
      setBusy(true);
      await service.acknowledge(
        selected.dispatch.id,
        {
          outcome,
          acknowledgementChannel: channel.trim(),
          acknowledgementReference: reference.trim(),
          evidenceReference: evidence.trim(),
          comments: comments.trim() || undefined,
          idempotencyKey: `po-amendment-supplier-ack-${crypto.randomUUID()}`,
        },
        true
      );
      toast.success('Your acknowledgement was recorded against this revision.');
      setSelected(null);
      setReference('');
      setEvidence('');
      setComments('');
      await load();
    } catch (acknowledgementError) {
      toast.error(errorMessage(acknowledgementError));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-6" data-testid="supplier-po-amendments">
      <div>
        <h1 className="text-2xl font-semibold">Purchase order amendments</h1>
        <p className="text-sm text-muted-foreground">
          Review signed PO revisions dispatched to your approved supplier
          account and record receipt, acceptance, or a dispute.
        </p>
      </div>

      {loading ? (
        <Card>
          <CardContent className="flex items-center gap-2 py-10 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" />
            Loading supplier-scoped amendments…
          </CardContent>
        </Card>
      ) : error ? (
        <Alert variant="destructive">
          <ShieldCheck className="h-4 w-4" />
          <AlertTitle>Amendments unavailable</AlertTitle>
          <AlertDescription className="space-y-3">
            <p>{error}</p>
            <Button variant="outline" size="sm" onClick={() => void load()}>
              <RefreshCw className="mr-2 h-4 w-4" />
              Retry
            </Button>
          </AlertDescription>
        </Alert>
      ) : rows.length === 0 ? (
        <Card>
          <CardContent className="py-12 text-center text-sm text-muted-foreground">
            No signed PO amendment has been dispatched to your supplier
            account.
          </CardContent>
        </Card>
      ) : (
        rows.map((amendment) => (
          <Card key={amendment.id}>
            <CardHeader>
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <CardTitle className="flex items-center gap-2 text-lg">
                    <FileDiff className="h-5 w-5" />
                    {amendment.amendmentNumber}
                  </CardTitle>
                  <p className="text-sm text-muted-foreground">
                    {amendment.purchaseOrderNumber} · revision{' '}
                    {amendment.proposedRevisionNumber}
                  </p>
                </div>
                <Badge variant="outline">{amendment.status}</Badge>
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              <p className="text-sm">{amendment.reason}</p>
              <div className="grid gap-3 text-sm md:grid-cols-3">
                <div>
                  <p className="text-muted-foreground">Previous total</p>
                  <p className="font-medium">
                    {amendment.beforeTotalAmount.toLocaleString()}{' '}
                    {amendment.currency}
                  </p>
                </div>
                <div>
                  <p className="text-muted-foreground">Approved revised total</p>
                  <p className="font-medium">
                    {amendment.proposedTotalAmount.toLocaleString()}{' '}
                    {amendment.currency}
                  </p>
                </div>
                <div>
                  <p className="text-muted-foreground">Scope</p>
                  <p className="font-medium">{amendment.changeScope}</p>
                </div>
              </div>
              <div className="space-y-2">
                {amendment.dispatches.map((dispatch) => {
                  const acknowledged = dispatch.acknowledgements.length > 0;
                  return (
                    <div
                      key={dispatch.id}
                      className="flex flex-wrap items-center justify-between gap-3 rounded-md border p-3"
                    >
                      <div>
                        <p className="font-medium">
                          {dispatch.documentReference}
                        </p>
                        <p className="text-sm text-muted-foreground">
                          {dispatch.dispatchReference} · {dispatch.channel}
                        </p>
                      </div>
                      {acknowledged ? (
                        <Badge className="bg-emerald-100 text-emerald-900">
                          <CheckCircle2 className="mr-1 h-3 w-3" />
                          {dispatch.acknowledgements.at(-1)?.outcome}
                        </Badge>
                      ) : (
                        <Button
                          size="sm"
                          onClick={() => setSelected({ amendment, dispatch })}
                        >
                          Acknowledge revision
                        </Button>
                      )}
                    </div>
                  );
                })}
              </div>
              <p className="break-all font-mono text-[11px] text-muted-foreground">
                Approved revision integrity: {amendment.proposedIntegrityHash}
              </p>
            </CardContent>
          </Card>
        ))
      )}

      <Dialog
        open={Boolean(selected)}
        onOpenChange={(open) => !open && setSelected(null)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Acknowledge signed PO revision</DialogTitle>
            <DialogDescription>
              This acknowledgement is permanently linked to{' '}
              {selected?.amendment.amendmentNumber} and dispatch{' '}
              {selected?.dispatch.dispatchReference}.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div>
              <Label>Outcome</Label>
              <Select
                value={outcome}
                onValueChange={(value) =>
                  setOutcome(value as PurchaseOrderAcknowledgementOutcome)
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Received">Received</SelectItem>
                  <SelectItem value="Accepted">Accepted</SelectItem>
                  <SelectItem value="Disputed">Disputed</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label htmlFor="supplier-ack-channel">Channel</Label>
              <Input
                id="supplier-ack-channel"
                value={channel}
                onChange={(event) => setChannel(event.target.value)}
              />
            </div>
            <div>
              <Label htmlFor="supplier-ack-reference">
                Acknowledgement reference
              </Label>
              <Input
                id="supplier-ack-reference"
                value={reference}
                onChange={(event) => setReference(event.target.value)}
              />
            </div>
            <div>
              <Label htmlFor="supplier-ack-evidence">Evidence reference</Label>
              <Input
                id="supplier-ack-evidence"
                value={evidence}
                onChange={(event) => setEvidence(event.target.value)}
                placeholder="DMS, email, or delivery evidence"
              />
            </div>
            <div>
              <Label htmlFor="supplier-ack-comments">Comments</Label>
              <Textarea
                id="supplier-ack-comments"
                value={comments}
                onChange={(event) => setComments(event.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setSelected(null)}
              disabled={busy}
            >
              Close
            </Button>
            <Button onClick={() => void acknowledge()} disabled={busy}>
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record acknowledgement
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
