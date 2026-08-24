'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Loader2, PackageCheck, XCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { assetRegisterService } from '@/services/hr/asset-register.service';
import { useToast } from '@/hooks/use-toast';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm">{children}</dd>
    </div>
  );
}

/**
 * One equipment request, its decision, and what it actually produced.
 *
 * ⚠ **Approval is not fulfilment.** Approving says the request is granted; fulfilling records
 * *which assignments* satisfied it, and until slice 3 nothing recorded that at all — an approved
 * requisition and the laptop somebody was eventually given had no connection anywhere. The
 * fulfilment picker below only offers assignments already made to the person the request is for,
 * because that is the only honest evidence the request was met.
 */
export default function AssetRequisitionDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [deciding, setDeciding] = useState<'approve' | 'reject' | null>(null);
  const [comments, setComments] = useState('');
  const [fulfilling, setFulfilling] = useState(false);
  const [chosen, setChosen] = useState<string[]>([]);

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['hr', 'assets'] });

  const { data: r, isLoading } = useQuery({
    queryKey: ['hr', 'assets', 'requisition', id],
    queryFn: () => assetRegisterService.getRequisition(id),
    enabled: Boolean(id),
  });

  /**
   * Custody records already held by the person the request is for — the fulfilment candidates.
   *
   * The employee-scoped read, not a page of every active assignment filtered in the browser: a
   * page is a page, and the one custody that satisfied the request would sit outside it as soon as
   * the register grew.
   */
  const forEmployeeId = r?.forEmployeeId;
  const { data: candidates = [] } = useQuery({
    queryKey: ['hr', 'assets', 'requisition', id, 'candidates', forEmployeeId],
    queryFn: () => assetRegisterService.getActiveAssignmentsForEmployee(forEmployeeId as string),
    enabled: fulfilling && Boolean(forEmployeeId),
  });

  const decide = useMutation({
    mutationFn: () =>
      deciding === 'approve'
        ? assetRegisterService.approveRequisition(id, comments || undefined)
        : assetRegisterService.rejectRequisition(id, comments),
    onSuccess: () => {
      invalidate();
      setDeciding(null);
      setComments('');
      toast({ title: deciding === 'approve' ? 'Request approved' : 'Request rejected' });
    },
    onError: (e: Error) =>
      toast({ title: 'The decision was refused', description: e.message, variant: 'destructive' }),
  });

  const fulfil = useMutation({
    mutationFn: () => assetRegisterService.fulfillRequisition(id, chosen),
    onSuccess: () => {
      invalidate();
      setFulfilling(false);
      setChosen([]);
      toast({ title: 'Request marked fulfilled' });
    },
    onError: (e: Error) =>
      toast({ title: 'Could not fulfil the request', description: e.message, variant: 'destructive' }),
  });

  if (isLoading || !r) {
    return (
      <div className="flex justify-center p-10">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const awaitingDecision = r.status === 'Submitted' || r.status === 'UnderReview';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={r.requisitionNumber}
        description={`${r.assetTypeName} · raised by ${r.requestedByName}`}
        backHref="/hr/assets/requisitions"
        actions={
          <div className="flex flex-wrap gap-2">
            {awaitingDecision && (
              <>
                <Button onClick={() => setDeciding('approve')}>
                  <CheckCircle2 className="mr-2 h-4 w-4" /> Approve
                </Button>
                <Button variant="outline" onClick={() => setDeciding('reject')}>
                  <XCircle className="mr-2 h-4 w-4" /> Reject
                </Button>
              </>
            )}
            {r.status === 'Approved' && !r.isFulfilled && (
              <Button onClick={() => setFulfilling(true)}>
                <PackageCheck className="mr-2 h-4 w-4" /> Record what was issued
              </Button>
            )}
          </div>
        }
      />

      <div className="flex flex-wrap items-center gap-3">
        <StatusBadge status={r.statusName} />
        <span className="text-sm text-muted-foreground">Priority: {r.priorityName}</span>
      </div>

      <Card>
        <CardHeader><CardTitle className="text-base">The request</CardTitle></CardHeader>
        <CardContent>
          <dl className="grid gap-4 sm:grid-cols-3">
            <Field label="Asset type">{r.assetTypeName}</Field>
            <Field label="Quantity">{r.quantity}</Field>
            <Field label="Raised on">{fmtDate(r.requestDate)}</Field>
            <Field label="Raised by">{r.requestedByName}</Field>
            <Field label="For">
              {r.forEmployeeName}
              {r.isOnBehalf && <span className="text-muted-foreground"> (on their behalf)</span>}
            </Field>
            <Field label="Needed by">{fmtDate(r.requiredByDate)}</Field>
            <div className="sm:col-span-3"><Field label="What is wanted">{r.description}</Field></div>
            <div className="sm:col-span-3"><Field label="Why">{r.justification}</Field></div>
          </dl>
        </CardContent>
      </Card>

      {(r.approvalDate || r.rejectedDate) && (
        <Card>
          <CardHeader><CardTitle className="text-base">The decision</CardTitle></CardHeader>
          <CardContent>
            <dl className="grid gap-4 sm:grid-cols-3">
              <Field label="Decided by">{r.approvedByName ?? '—'}</Field>
              <Field label="Decided on">{fmtDate(r.approvalDate ?? r.rejectedDate)}</Field>
              <div className="sm:col-span-3">
                <Field label="Reason given">
                  {r.approvalComments ?? r.rejectionReason ?? '—'}
                </Field>
              </div>
            </dl>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">
            What it produced {r.isFulfilled && <span className="text-muted-foreground">· fulfilled {fmtDate(r.fulfilledDate)}</span>}
          </CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {r.fulfilledWith.length === 0 ? (
            <p className="p-6 text-sm text-muted-foreground">
              No assignment cites this request yet. Approving a request grants it; recording what
              was issued is what connects it to an actual asset.
            </p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Assignment</TableHead>
                  <TableHead>Asset</TableHead>
                  <TableHead>Issued to</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {r.fulfilledWith.map((f) => (
                  <TableRow key={f.assignmentId}>
                    <TableCell>
                      <Link href={`/hr/assets/assignments/${f.assignmentId}`} className="hover:underline">
                        {f.assignmentNumber}
                      </Link>
                    </TableCell>
                    <TableCell>{f.assetName} ({f.assetNumber})</TableCell>
                    <TableCell>{f.employeeName}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Dialog open={deciding !== null} onOpenChange={(o) => !o && setDeciding(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {deciding === 'approve' ? 'Approve this request' : 'Reject this request'}
            </DialogTitle>
            <DialogDescription>
              {deciding === 'approve'
                ? 'Approving grants the request. Recording what was actually issued is a separate step.'
                : 'Say why — the requester sees this.'}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label>{deciding === 'approve' ? 'Comments' : 'Reason *'}</Label>
            <Textarea rows={3} value={comments} onChange={(e) => setComments(e.target.value)} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeciding(null)}>Cancel</Button>
            <Button
              onClick={() => decide.mutate()}
              disabled={decide.isPending || (deciding === 'reject' && !comments.trim())}
            >
              {decide.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {deciding === 'approve' ? 'Approve' : 'Reject'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={fulfilling} onOpenChange={setFulfilling}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Record what was issued</DialogTitle>
            <DialogDescription>
              Pick the assignments that satisfied this request. Only custodies currently held by{' '}
              {r.forEmployeeName} are offered.
            </DialogDescription>
          </DialogHeader>
          <div className="max-h-80 space-y-2 overflow-y-auto">
            {candidates.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                {r.forEmployeeName} holds no active assignment. Issue the asset first, from the
                register.
              </p>
            ) : candidates.map((c) => (
              <label key={c.id} className="flex items-start gap-2 rounded-md border p-3">
                <Checkbox
                  checked={chosen.includes(c.id)}
                  onCheckedChange={(v) =>
                    setChosen((prev) => (v === true ? [...prev, c.id] : prev.filter((x) => x !== c.id)))}
                />
                <span className="text-sm">
                  <span className="font-medium">{c.assetName}</span>
                  <span className="block text-xs text-muted-foreground">
                    {c.assignmentNumber} · {c.assetNumber} · issued {fmtDate(c.assignmentDate)}
                  </span>
                </span>
              </label>
            ))}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setFulfilling(false)}>Cancel</Button>
            <Button onClick={() => fulfil.mutate()} disabled={chosen.length === 0 || fulfil.isPending}>
              {fulfil.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Mark fulfilled
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
