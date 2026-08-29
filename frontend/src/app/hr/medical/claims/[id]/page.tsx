'use client';

import { use, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Flag, Lock } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
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
import { HR_ROLES } from '@/components/hr/common/PermissionGate';
import { InsuranceClaimsPanel } from '@/components/hr/medical/InsuranceClaimsPanel';
import { useAuth } from '@/hooks/use-auth';
import { AttachmentsPanel } from '@/components/hr/common/AttachmentsPanel';
import { useToast } from '@/hooks/use-toast';
import { medicalClaimService } from '@/services/hr/medical-claims.service';
import {
  MEDICAL_EXPENSE_TYPE_OPTIONS,
  MEDICAL_ITEM_TYPE_OPTIONS,
  PAYMENT_METHOD_OPTIONS,
  CLAIM_NOTE_TYPE_OPTIONS,
} from '@/types/hr/medical';
import type { PaymentMethod, MedicalExpenseClaimNoteType } from '@/types/hr/medical';

/**
 * One medical claim, and the decisions taken on it.
 *
 * A claim is adjudicated exactly ONCE — the API refuses a second decision with 422 — so the
 * approve and reject controls disappear as soon as a decision exists rather than being offered
 * and then refused. Payment only becomes available on an approved claim, for the same reason.
 */
const money = (v?: number | null) =>
  v === null || v === undefined
    ? '—'
    : v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const label = (opts: { value: string; label: string }[], v: string) =>
  opts.find((o) => o.value === v)?.label ?? v;

const DECIDED = ['Approved', 'Rejected', 'Paid', 'Cancelled'];

export default function MedicalClaimDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [amountApproved, setAmountApproved] = useState('');
  const [comments, setComments] = useState('');
  const [payOpen, setPayOpen] = useState(false);
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>('BankTransfer');
  const [paymentReference, setPaymentReference] = useState('');
  const [flagOpen, setFlagOpen] = useState(false);
  const [flagReason, setFlagReason] = useState('');
  const [noteContent, setNoteContent] = useState('');
  const [noteType, setNoteType] = useState<MedicalExpenseClaimNoteType>('General');
  const [noteInternal, setNoteInternal] = useState(true);

  const { hasAnyPermission, hasAnyRole } = useAuth();
  const canWriteMedical =
    hasAnyPermission(['HR.Medical.Write', 'HR.Medical.Admin']) || hasAnyRole(HR_ROLES);

  const claimKey = ['hr', 'medical-claims', id];
  const { data: claim } = useQuery({ queryKey: claimKey, queryFn: () => medicalClaimService.getClaim(id) });
  const { data: items = [] } = useQuery({
    queryKey: [...claimKey, 'items'],
    queryFn: () => medicalClaimService.getItems(id),
  });
  const { data: notes = [] } = useQuery({
    queryKey: [...claimKey, 'notes'],
    queryFn: () => medicalClaimService.getNotes(id),
  });

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: claimKey });
    queryClient.invalidateQueries({ queryKey: ['hr', 'medical-claims'] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'medical-dashboard', 30] });
  };

  /** The API's refusals are written to be shown — a 422 here explains itself. */
  const onError = (error: unknown) =>
    toast({
      variant: 'destructive',
      title: 'Could not complete that',
      description: error instanceof Error ? error.message : 'Unexpected error',
    });

  // The decision travels as a mutation argument rather than through state — reading it from
  // state at call time races the re-render and can send the previous value.
  const adjudicate = useMutation({
    mutationFn: (status: 'Approved' | 'Rejected') =>
      medicalClaimService.processApproval(id, {
        claimId: id,
        status,
        amountApproved: status === 'Approved' && amountApproved ? Number(amountApproved) : null,
        comments: comments || null,
      }),
    onSuccess: (_data, status) => {
      toast({ title: status === 'Approved' ? 'Claim approved' : 'Claim rejected' });
      setAmountApproved('');
      setComments('');
      refresh();
    },
    onError,
  });

  const pay = useMutation({
    mutationFn: () =>
      medicalClaimService.processPayment(id, {
        claimId: id,
        paymentMethod,
        paymentReference,
        paymentDate: new Date().toISOString(),
      }),
    onSuccess: () => {
      toast({ title: 'Payment recorded' });
      setPayOpen(false);
      setPaymentReference('');
      refresh();
    },
    onError,
  });

  const flag = useMutation({
    mutationFn: () =>
      claim?.isFlaggedForReview
        ? medicalClaimService.unflag(id)
        : medicalClaimService.flag(id, flagReason),
    onSuccess: () => {
      toast({ title: claim?.isFlaggedForReview ? 'Flag cleared' : 'Claim flagged' });
      setFlagOpen(false);
      setFlagReason('');
      refresh();
    },
    onError,
  });

  const addNote = useMutation({
    mutationFn: () => medicalClaimService.addNote(id, noteContent, noteType, noteInternal),
    onSuccess: () => {
      setNoteContent('');
      queryClient.invalidateQueries({ queryKey: [...claimKey, 'notes'] });
    },
    onError,
  });

  const decided = claim ? DECIDED.includes(claim.status) : true;
  const lineTotal = items.reduce((sum, i) => sum + i.quantity * i.unitCost, 0);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={claim?.claimNumber ?? 'Claim'}
        description={
          claim
            ? `${claim.employeeName} · ${label(MEDICAL_EXPENSE_TYPE_OPTIONS, claim.expenseType)} at ${claim.facilityName}`
            : 'Loading…'
        }
        backHref="/hr/medical/claims"
        actions={
          claim && (
            <div className="flex gap-2">
              <Button
                variant={claim.isFlaggedForReview ? 'secondary' : 'outline'}
                onClick={() => (claim.isFlaggedForReview ? flag.mutate() : setFlagOpen(true))}
              >
                <Flag className="mr-2 h-4 w-4" />
                {claim.isFlaggedForReview ? 'Clear flag' : 'Flag'}
              </Button>
              {claim.status === 'Approved' && (
                <Button onClick={() => setPayOpen(true)}>Record payment</Button>
              )}
            </div>
          )
        }
      />

      {claim?.isFlaggedForReview && (
        <Card className="border-destructive">
          <CardContent className="p-4 text-sm text-destructive">
            Flagged for review. The claimant is not shown this.
          </CardContent>
        </Card>
      )}

      {claim && (
        <Card>
          <CardContent className="grid gap-4 p-6 sm:grid-cols-2 lg:grid-cols-4">
            <Detail label="Status" value={claim.status} />
            <Detail label="Service date" value={fmtDate(claim.serviceDate)} />
            <Detail label="Filed" value={fmtDate(claim.claimDate)} />
            <Detail label="Facility" value={claim.facilityName} />
            <Detail label="Physician" value={claim.physicianName} />
            <Detail label="Diagnosis" value={claim.diagnosis} />
            <Detail label="Total cost" value={money(claim.totalAmount)} />
            <Detail label="Requested" value={money(claim.amountRequested)} />
            <Detail label="Approved" value={money(claim.amountApproved)} />
            <Detail label="Policy" value={claim.insurancePolicyNumber} />
            <Detail
              label="Circumstances"
              value={
                [
                  claim.isEmergency ? 'Emergency' : null,
                  claim.requiredHospitalization ? 'Hospitalised' : null,
                ]
                  .filter(Boolean)
                  .join(' · ') || 'Routine'
              }
            />
            <div className="sm:col-span-2">
              <p className="text-sm text-muted-foreground">Description</p>
              <p className="text-sm">{claim.description}</p>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Adjudication happens once. Once a decision exists these controls are gone rather than
          present-and-refused, because the API answers a second attempt with 422. */}
      {claim && !decided && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Decision</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="approved-amount">Amount approved</Label>
                <Input
                  id="approved-amount"
                  type="number"
                  value={amountApproved}
                  onChange={(e) => setAmountApproved(e.target.value)}
                  placeholder={String(claim.amountRequested)}
                />
                <p className="text-xs text-muted-foreground">
                  Defaults to the amount requested. Draws down the linked policy if there is one.
                </p>
              </div>
              <div className="space-y-2">
                <Label htmlFor="decision-comments">Comments</Label>
                <Textarea
                  id="decision-comments"
                  value={comments}
                  onChange={(e) => setComments(e.target.value)}
                  rows={3}
                />
              </div>
            </div>
            <div className="flex gap-2">
              <Button onClick={() => adjudicate.mutate('Approved')} disabled={adjudicate.isPending}>
                Approve
              </Button>
              <Button
                variant="destructive"
                onClick={() => adjudicate.mutate('Rejected')}
                disabled={adjudicate.isPending}
              >
                Reject
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      <Tabs defaultValue="items">
        <TabsList>
          <TabsTrigger value="items">Lines ({items.length})</TabsTrigger>
          <TabsTrigger value="documents">Documents</TabsTrigger>
          <TabsTrigger value="notes">Notes ({notes.length})</TabsTrigger>
          {/* What was claimed back from an insurer against this expense. */}
          <TabsTrigger value="insurance">Insurance</TabsTrigger>
        </TabsList>

        <TabsContent value="items" className="mt-4">
          {items.length === 0 ? (
            <EmptyState
              title="No lines"
              description="This claim was filed without itemised lines."
              icon={Lock}
            />
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Description</TableHead>
                      <TableHead>Type</TableHead>
                      <TableHead className="text-right">Qty</TableHead>
                      <TableHead className="text-right">Unit cost</TableHead>
                      <TableHead className="text-right">Line total</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {items.map((i) => (
                      <TableRow key={i.id}>
                        <TableCell className="font-medium">{i.description}</TableCell>
                        <TableCell>{label(MEDICAL_ITEM_TYPE_OPTIONS, i.itemType)}</TableCell>
                        <TableCell className="text-right tabular-nums">{i.quantity}</TableCell>
                        <TableCell className="text-right tabular-nums">{money(i.unitCost)}</TableCell>
                        <TableCell className="text-right tabular-nums">
                          {money(i.quantity * i.unitCost)}
                        </TableCell>
                      </TableRow>
                    ))}
                    <TableRow>
                      <TableCell colSpan={4} className="text-right font-medium">
                        Lines total
                      </TableCell>
                      <TableCell className="text-right font-medium tabular-nums">
                        {money(lineTotal)}
                      </TableCell>
                    </TableRow>
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
          {claim && items.length > 0 && Math.abs(lineTotal - claim.totalAmount) > 0.005 && (
            <p className="mt-2 text-sm text-amber-600 dark:text-amber-500">
              Lines total {money(lineTotal)} but the claim states {money(claim.totalAmount)}. Worth
              checking before approving.
            </p>
          )}
        </TabsContent>

        <TabsContent value="documents" className="mt-4">
          <AttachmentsPanel
            title="Supporting documents"
            note="Receipts and reports, uploaded through the controlled gate — scanned and stored outside the web root. Uploads here are recorded as receipts."
            queryKey={[...claimKey, 'documents']}
            list={() => medicalClaimService.getDocuments(id)}
            upload={(file, description) =>
              medicalClaimService.uploadDocument(id, file, 'Receipt', description)
            }
            download={(doc) => medicalClaimService.downloadDocument(doc)}
            emptyDescription="No receipts or reports attached to this claim."
          />
        </TabsContent>

        <TabsContent value="notes" className="mt-4 space-y-4">
          <Card>
            <CardContent className="space-y-3 p-4">
              <Textarea
                value={noteContent}
                onChange={(e) => setNoteContent(e.target.value)}
                placeholder="Add a note…"
                rows={3}
              />
              <div className="flex flex-wrap items-center gap-3">
                <Select
                  value={noteType}
                  onValueChange={(v) => setNoteType(v as MedicalExpenseClaimNoteType)}
                >
                  <SelectTrigger className="w-56">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {CLAIM_NOTE_TYPE_OPTIONS.map((o) => (
                      <SelectItem key={o.value} value={o.value}>
                        {o.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <label className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={noteInternal}
                    onChange={(e) => setNoteInternal(e.target.checked)}
                  />
                  Internal — not for the claimant
                </label>
                <Button
                  size="sm"
                  disabled={!noteContent.trim() || addNote.isPending}
                  onClick={() => addNote.mutate()}
                >
                  Add note
                </Button>
              </div>
            </CardContent>
          </Card>

          {notes.length === 0 ? (
            <EmptyState
              title="No notes"
              description="Nothing has been recorded against this claim."
              icon={Lock}
            />
          ) : (
            <div className="space-y-2">
              {notes.map((n) => (
                <Card key={n.id}>
                  <CardContent className="p-4">
                    <div className="mb-1 flex items-center gap-2">
                      <span className="text-sm font-medium">{n.authorName}</span>
                      <Badge variant="outline">{label(CLAIM_NOTE_TYPE_OPTIONS, n.noteType)}</Badge>
                      {n.isInternal && (
                        <Badge variant="secondary" className="gap-1">
                          <Lock className="h-3 w-3" /> Internal
                        </Badge>
                      )}
                      <span className="ml-auto text-xs text-muted-foreground">
                        {fmtDate(n.noteDate)}
                      </span>
                    </div>
                    <p className="text-sm">{n.content}</p>
                  </CardContent>
                </Card>
              ))}
            </div>
          )}
        </TabsContent>

        <TabsContent value="insurance" className="mt-4">
          {/*
            The policy picker is scoped to this claim's employee — filing against a colleague's
            policy is not a mistake a dropdown should make possible.
          */}
          <InsuranceClaimsPanel
            expenseClaimId={id}
            employeeId={claim?.employeeId ?? null}
            canWrite={canWriteMedical}
          />
        </TabsContent>
      </Tabs>

      <Dialog open={payOpen} onOpenChange={setPayOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record payment</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Method</Label>
              <Select value={paymentMethod} onValueChange={(v) => setPaymentMethod(v as PaymentMethod)}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {PAYMENT_METHOD_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="payment-reference">Reference</Label>
              <Input
                id="payment-reference"
                value={paymentReference}
                onChange={(e) => setPaymentReference(e.target.value)}
                placeholder="Transfer or cheque reference"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setPayOpen(false)}>
              Cancel
            </Button>
            <Button disabled={!paymentReference.trim() || pay.isPending} onClick={() => pay.mutate()}>
              Record payment
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={flagOpen} onOpenChange={setFlagOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Flag this claim for review</DialogTitle>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="flag-reason">Reason</Label>
            <Textarea
              id="flag-reason"
              value={flagReason}
              onChange={(e) => setFlagReason(e.target.value)}
              rows={3}
              placeholder="What looks wrong?"
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setFlagOpen(false)}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              disabled={!flagReason.trim() || flag.isPending}
              onClick={() => flag.mutate()}
            >
              Flag claim
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function Detail({ label: title, value }: { label: string; value?: string | null }) {
  return (
    <div>
      <p className="text-sm text-muted-foreground">{title}</p>
      <p className="text-sm">{value || '—'}</p>
    </div>
  );
}
