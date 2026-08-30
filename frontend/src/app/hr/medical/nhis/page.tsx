'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Landmark, Paperclip, Send } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { nhisClaimService } from '@/services/hr/medical-claims.service';
import { NhisClaimDocumentsPanel } from '@/components/hr/medical/NhisClaimDocumentsPanel';
import { HR_ADMIN_ROLES, HR_ROLES } from '@/components/hr/common/PermissionGate';
import { useAuth } from '@/hooks/use-auth';
import { NHIS_CLAIM_STATUS_OPTIONS } from '@/types/hr/medical';
import type { NHISClaimStatus, NHISClaimSummary } from '@/types/hr/medical';

/**
 * Claims made against the National Health Insurance Scheme.
 *
 * These are claims the organisation makes to NHIS, not reimbursements to an employee — so the
 * lifecycle is submission and settlement rather than approval: draft → submitted (in a batch) →
 * the scheme's decision → payment recorded against it.
 */
const money = (v?: number | null) =>
  v === null || v === undefined
    ? '—'
    : v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const statusLabel = (v: string) =>
  NHIS_CLAIM_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;

function NhisStatusBadge({ status }: { status: NHISClaimStatus }) {
  if (status === 'Paid' || status === 'Approved') return <Badge variant="secondary">{statusLabel(status)}</Badge>;
  if (status === 'Rejected') return <Badge variant="destructive">Rejected</Badge>;
  if (status === 'PartiallyApproved')
    return <Badge variant="outline">Partially approved</Badge>;
  return <Badge variant="outline">{statusLabel(status)}</Badge>;
}

function NhisTable({
  items,
  empty,
  onSubmit,
  onSettle,
  onDocuments,
}: {
  items: NHISClaimSummary[];
  empty: string;
  onSubmit: (claim: NHISClaimSummary) => void;
  onSettle: (claim: NHISClaimSummary) => void;
  onDocuments: (claim: NHISClaimSummary) => void;
}) {
  if (items.length === 0) {
    return <EmptyState title="Nothing here" description={empty} icon={Landmark} />;
  }
  return (
    <Card>
      <CardContent className="p-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Claim</TableHead>
              <TableHead>Employee</TableHead>
              <TableHead>Service date</TableHead>
              <TableHead className="text-right">Total cost</TableHead>
              <TableHead>Status</TableHead>
              <TableHead />
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((c) => (
              <TableRow key={c.id}>
                <TableCell className="font-mono text-sm">{c.claimNumber}</TableCell>
                <TableCell className="font-medium">{c.employeeName}</TableCell>
                <TableCell>{fmtDate(c.serviceDate)}</TableCell>
                <TableCell className="text-right tabular-nums">{money(c.totalCost)}</TableCell>
                <TableCell>
                  <NhisStatusBadge status={c.status} />
                </TableCell>
                <TableCell className="text-right">
                  {/* Submission only makes sense on a draft; settlement only once the scheme
                      has decided. Offering either at the wrong point would just be refused. */}
                  {c.status === 'Draft' && (
                    <Button variant="ghost" size="sm" onClick={() => onSubmit(c)}>
                      <Send className="mr-2 h-4 w-4" /> Submit
                    </Button>
                  )}
                  {(c.status === 'Approved' || c.status === 'PartiallyApproved') && (
                    <Button variant="ghost" size="sm" onClick={() => onSettle(c)}>
                      Record payment
                    </Button>
                  )}
                  {/* Documents belong to a claim at every status — the scheme's rejection letter
                      arrives after the decision, and the attendance record before it. */}
                  <Button variant="ghost" size="sm" onClick={() => onDocuments(c)}>
                    <Paperclip className="mr-2 h-4 w-4" /> Documents
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}

export default function NhisClaimsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [tab, setTab] = useState('pending');
  const [submitting, setSubmitting] = useState<NHISClaimSummary | null>(null);
  const [batchNumber, setBatchNumber] = useState('');
  const [settling, setSettling] = useState<NHISClaimSummary | null>(null);
  const [paymentReference, setPaymentReference] = useState('');
  const [paidAmount, setPaidAmount] = useState('');
  const [documentsFor, setDocumentsFor] = useState<NHISClaimSummary | null>(null);
  const { hasAnyPermission, hasAnyRole } = useAuth();

  // The medical ladder: create and update are Write, every delete is Admin, and the HR role
  // holds Write but not Admin.
  const canWrite =
    hasAnyPermission(['HR.Medical.Write', 'HR.Medical.Admin']) || hasAnyRole(HR_ROLES);
  const canDelete = hasAnyPermission(['HR.Medical.Admin']) || hasAnyRole(HR_ADMIN_ROLES);

  const { data: all = [] } = useQuery({
    queryKey: ['hr', 'nhis-claims'],
    queryFn: () => nhisClaimService.getAll(),
  });
  const { data: pending = [] } = useQuery({
    queryKey: ['hr', 'nhis-claims', 'pending'],
    queryFn: () => nhisClaimService.getPending(),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'nhis-claims'] });
  const onError = (error: unknown) =>
    toast({
      variant: 'destructive',
      title: 'Could not complete that',
      description: error instanceof Error ? error.message : 'Unexpected error',
    });

  // The claim travels as a mutation argument rather than being read back out of dialog state,
  // which would race the dialog closing.
  const submit = useMutation({
    mutationFn: (claimId: string) => nhisClaimService.submit(claimId, batchNumber || null),
    onSuccess: () => {
      toast({ title: 'Claim submitted to NHIS' });
      setSubmitting(null);
      setBatchNumber('');
      refresh();
    },
    onError,
  });

  const settle = useMutation({
    mutationFn: (claimId: string) =>
      nhisClaimService.recordPayment(claimId, paymentReference, Number(paidAmount)),
    onSuccess: () => {
      toast({ title: 'Payment recorded' });
      setSettling(null);
      setPaymentReference('');
      setPaidAmount('');
      refresh();
    },
    onError,
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="NHIS Claims"
        description="Claims made to the National Health Insurance Scheme for treatment its members received."
        backHref="/hr/medical"
      />

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          <TabsTrigger value="pending">Outstanding ({pending.length})</TabsTrigger>
          <TabsTrigger value="all">All claims ({all.length})</TabsTrigger>
        </TabsList>
        <TabsContent value="pending" className="mt-4">
          <NhisTable
            items={pending}
            empty="Nothing outstanding with the scheme."
            onSubmit={setSubmitting}
            onSettle={setSettling}
            onDocuments={setDocumentsFor}
          />
        </TabsContent>
        <TabsContent value="all" className="mt-4">
          <NhisTable
            items={all}
            empty="No NHIS claims have been recorded yet."
            onSubmit={setSubmitting}
            onSettle={setSettling}
            onDocuments={setDocumentsFor}
          />
        </TabsContent>
      </Tabs>

      <Dialog open={!!documentsFor} onOpenChange={(o) => !o && setDocumentsFor(null)}>
        <DialogContent className="max-h-[85vh] max-w-3xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Documents — {documentsFor?.claimNumber}</DialogTitle>
          </DialogHeader>
          {documentsFor && (
            <NhisClaimDocumentsPanel
              claimId={documentsFor.id}
              canWrite={canWrite}
              canDelete={canDelete}
            />
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={!!submitting} onOpenChange={(o) => !o && setSubmitting(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Submit {submitting?.claimNumber} to NHIS</DialogTitle>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="batch-number">Batch number</Label>
            <Input
              id="batch-number"
              value={batchNumber}
              onChange={(e) => setBatchNumber(e.target.value)}
              placeholder="The batch this claim goes out in"
            />
            <p className="text-xs text-muted-foreground">
              Optional, but it is how a claim is traced once the scheme has it.
            </p>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setSubmitting(null)}>
              Cancel
            </Button>
            <Button
              disabled={!submitting || submit.isPending}
              onClick={() => submitting && submit.mutate(submitting.id)}
            >
              Submit
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!settling} onOpenChange={(o) => !o && setSettling(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record NHIS payment for {settling?.claimNumber}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="nhis-amount">Amount received</Label>
              <Input
                id="nhis-amount"
                type="number"
                value={paidAmount}
                onChange={(e) => setPaidAmount(e.target.value)}
                placeholder={settling ? String(settling.totalCost) : undefined}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="nhis-reference">Payment reference</Label>
              <Input
                id="nhis-reference"
                value={paymentReference}
                onChange={(e) => setPaymentReference(e.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setSettling(null)}>
              Cancel
            </Button>
            <Button
              disabled={
                !settling ||
                !paymentReference.trim() ||
                !(Number(paidAmount) > 0) ||
                settle.isPending
              }
              onClick={() => settling && settle.mutate(settling.id)}
            >
              Record payment
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
