'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { MoreHorizontal, Paperclip, Pencil, Plus, Receipt, Send, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
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
import { AttachmentsPanel } from '@/components/hr/common/AttachmentsPanel';
import { toast } from 'sonner';
import { medicalSelfServiceClaimService } from '@/services/hr/medical-claims.service';
import { medicalFacilityService } from '@/services/hr/medical-reference.service';
import { MEDICAL_EXPENSE_TYPE_OPTIONS } from '@/types/hr/medical';
import type { ClaimStatus, MedicalExpenseType, OwnMedicalClaimSummary } from '@/types/hr/medical';

/**
 * An employee's own medical claims — file one, attach the receipt, submit it, follow it.
 *
 * Area 25 slice 8: re-homed from /hr/medical/my-claims into the portal shell (D3 — moved, not
 * redirected; the desk keeps only the HR caseload at /hr/medical/claims).
 *
 * Drafts (2026-10-07): filing used to BE submitting — a claim reached HR's queue the moment it was
 * filed, before its receipt, and could not be corrected or withdrawn. Filing now saves a DRAFT only
 * the claimant sees; they attach the receipt and submit it (the API refuses a submission with no
 * receipt), or discard it. Submission freezes the details; receipts can still follow until HR decides.
 *
 * Everything here is scoped to the signed-in employee by the API; no id on this screen identifies
 * anybody else, and a claim belonging to someone else simply does not resolve. Approval, payment
 * and adjudicator notes live on the HR caseload and have no counterpart here.
 */
const money = (v?: number | null) =>
  v === null || v === undefined
    ? '—'
    : v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const typeLabel = (v: string) =>
  MEDICAL_EXPENSE_TYPE_OPTIONS.find((o) => o.value === v)?.label ?? v;

/** HR has decided these; the claim's paperwork is closed. */
const DECIDED: ClaimStatus[] = ['Approved', 'PartiallyApproved', 'Rejected', 'Paid', 'Cancelled'];

function StatusBadgeFor({ status }: { status: ClaimStatus }) {
  if (status === 'Draft')
    return (
      <Badge variant="outline" className="border-dashed">
        Draft — not submitted
      </Badge>
    );
  if (status === 'Paid') return <Badge variant="secondary">Paid</Badge>;
  if (status === 'Approved') return <Badge variant="secondary">Approved</Badge>;
  if (status === 'Rejected') return <Badge variant="destructive">Rejected</Badge>;
  // Pending is the desk's word for "awaiting decision"; to the claimant it is the claim they submitted.
  return <Badge variant="outline">{status === 'Pending' ? 'Submitted' : status}</Badge>;
}

const emptyForm = {
  serviceDate: '',
  expenseType: 'Consultation' as MedicalExpenseType,
  facilityId: '',
  description: '',
  amount: '',
};

export default function MyMedicalClaimsPage() {
  const queryClient = useQueryClient();

  const [formOpen, setFormOpen] = useState(false);
  /** The draft being edited; null while filing a new one. */
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState(emptyForm);
  // The id, not the row: the row is re-read from the list, so a submit shows at once.
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState<OwnMedicalClaimSummary | null>(null);
  const [discarding, setDiscarding] = useState<OwnMedicalClaimSummary | null>(null);

  const { data: claims = [] } = useQuery({
    queryKey: ['me', 'medical', 'claims'],
    queryFn: () => medicalSelfServiceClaimService.getMine(),
  });
  const selected = claims.find((c) => c.id === selectedId) ?? null;

  // Facility reads are open to any authenticated user precisely so this form can work.
  const { data: facilities = [] } = useQuery({
    queryKey: ['me', 'medical', 'facilities-active'],
    queryFn: () => medicalFacilityService.getActiveFacilities(),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['me', 'medical', 'claims'] });
  const failed = (fallback: string) => (error: unknown) =>
    toast.error(error instanceof Error ? error.message : fallback);

  const openFile = () => {
    setEditingId(null);
    setForm(emptyForm);
    setFormOpen(true);
  };

  /** The row is a summary; the draft's own read carries the description and amounts. */
  const openEdit = async (claim: OwnMedicalClaimSummary) => {
    try {
      const full = await medicalSelfServiceClaimService.getMineById(claim.id);
      setForm({
        serviceDate: full.serviceDate?.slice(0, 10) ?? '',
        expenseType: full.expenseType,
        facilityId: full.facilityId,
        description: full.description ?? '',
        amount: String(full.amountRequested ?? ''),
      });
      setEditingId(claim.id);
      setFormOpen(true);
    } catch (error) {
      failed('Could not open the draft')(error);
    }
  };

  const save = useMutation({
    mutationFn: () => {
      const payload = {
        serviceDate: new Date(form.serviceDate).toISOString(),
        expenseType: form.expenseType,
        description: form.description,
        facilityId: form.facilityId,
        isEmergency: false,
        requiredHospitalization: false,
        totalAmount: Number(form.amount),
        amountRequested: Number(form.amount),
      };
      return editingId
        ? medicalSelfServiceClaimService.updateDraft(editingId, payload)
        : medicalSelfServiceClaimService.file(payload);
    },
    onSuccess: (saved) => {
      toast.success(
        editingId
          ? 'Draft updated.'
          : 'Draft saved — attach your receipt, then submit it to HR.',
      );
      setFormOpen(false);
      setEditingId(null);
      // Straight to the receipts: the one thing a new draft needs before it can be submitted.
      setSelectedId(saved.id);
      refresh();
    },
    onError: failed('Could not save the claim'),
  });

  const submit = useMutation({
    mutationFn: (id: string) => medicalSelfServiceClaimService.submit(id),
    onSuccess: (claim) => {
      toast.success(`${claim.claimNumber} submitted to HR.`);
      setSubmitting(null);
      refresh();
    },
    onError: (error) => {
      setSubmitting(null);
      failed('Could not submit the claim')(error);
    },
  });

  const discard = useMutation({
    mutationFn: (id: string) => medicalSelfServiceClaimService.discardDraft(id),
    onSuccess: () => {
      toast.success('Draft discarded.');
      if (discarding?.id === selectedId) setSelectedId(null);
      setDiscarding(null);
      refresh();
    },
    onError: failed('Could not discard the draft'),
  });

  const canSave =
    form.serviceDate && form.facilityId && form.description.trim() && Number(form.amount) > 0;

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Medical Claims"
        description="Claim back medical costs you have paid for yourself. A new claim is a draft only you can see — attach the receipt, then submit it to HR."
        backHref="/me/medical"
        actions={
          <Button onClick={openFile}>
            <Plus className="mr-2 h-4 w-4" /> File a claim
          </Button>
        }
      />

      {claims.length === 0 ? (
        <EmptyState
          title="No claims yet"
          description="When you pay for treatment yourself, file it here and attach the receipt."
          icon={Receipt}
        />
      ) : (
        <Card>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Claim</TableHead>
                  <TableHead>Service date</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Facility</TableHead>
                  <TableHead className="text-right">Requested</TableHead>
                  <TableHead className="text-right">Approved</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[60px]" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {claims.map((c) => {
                  const draft = c.status === 'Draft';
                  return (
                    <TableRow
                      key={c.id}
                      className="cursor-pointer"
                      onClick={() => setSelectedId(c.id)}
                    >
                      <TableCell className="font-mono text-sm">{c.claimNumber}</TableCell>
                      <TableCell>{fmtDate(c.serviceDate)}</TableCell>
                      <TableCell>{typeLabel(c.expenseType)}</TableCell>
                      <TableCell>{c.facilityName}</TableCell>
                      <TableCell className="text-right tabular-nums">
                        {money(c.amountRequested)}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {money(c.amountApproved)}
                      </TableCell>
                      <TableCell>
                        <StatusBadgeFor status={c.status} />
                      </TableCell>
                      {/* The menu renders in a portal, but React events still bubble through the
                          tree — without this every menu click also selected the row. */}
                      <TableCell onClick={(e) => e.stopPropagation()}>
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button variant="ghost" size="icon" className="h-8 w-8">
                              <MoreHorizontal className="h-4 w-4" />
                              <span className="sr-only">Actions for {c.claimNumber}</span>
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            <DropdownMenuItem onClick={() => setSelectedId(c.id)}>
                              <Paperclip className="mr-2 h-4 w-4" />
                              Receipts
                            </DropdownMenuItem>
                            {draft && (
                              <>
                                <DropdownMenuItem onClick={() => openEdit(c)}>
                                  <Pencil className="mr-2 h-4 w-4" />
                                  Edit
                                </DropdownMenuItem>
                                <DropdownMenuItem onClick={() => setSubmitting(c)}>
                                  <Send className="mr-2 h-4 w-4" />
                                  Submit to HR
                                </DropdownMenuItem>
                                <DropdownMenuSeparator />
                                <DropdownMenuItem
                                  className="text-red-600"
                                  onClick={() => setDiscarding(c)}
                                >
                                  <Trash2 className="mr-2 h-4 w-4" />
                                  Discard draft
                                </DropdownMenuItem>
                              </>
                            )}
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {selected && (
        <div className="space-y-2">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <p className="text-sm text-muted-foreground">
              Receipts for {selected.claimNumber}
              {selected.status === 'Draft' && ' — a draft: HR sees nothing until you submit it.'}
            </p>
            <div className="flex gap-2">
              {selected.status === 'Draft' && (
                <Button size="sm" onClick={() => setSubmitting(selected)}>
                  <Send className="mr-2 h-4 w-4" /> Submit to HR
                </Button>
              )}
              <Button variant="ghost" size="sm" onClick={() => setSelectedId(null)}>
                Close
              </Button>
            </div>
          </div>
          <AttachmentsPanel
            title="Receipts"
            note="Only you and the HR team assessing your claim can open these."
            queryKey={['me', 'medical', 'claims', selected.id, 'documents']}
            list={() => medicalSelfServiceClaimService.getDocuments(selected.id)}
            upload={(f, desc) =>
              medicalSelfServiceClaimService.uploadDocument(selected.id, f, 'Receipt', desc)
            }
            download={(doc) => medicalSelfServiceClaimService.downloadDocument(selected.id, doc)}
            // Removing is a draft's alone; once submitted, what HR was given stays on the claim.
            {...(selected.status === 'Draft'
              ? {
                  remove: (documentId: string) =>
                    medicalSelfServiceClaimService.removeDraftDocument(selected.id, documentId),
                }
              : {})}
            // Receipts can follow a submitted claim (HR may ask for one) until it is decided.
            readOnly={DECIDED.includes(selected.status)}
            emptyDescription={
              selected.status === 'Draft'
                ? 'No receipt yet — attach one before you submit.'
                : 'No receipts attached.'
            }
          />
        </div>
      )}

      <Dialog
        open={formOpen}
        onOpenChange={(o) => {
          setFormOpen(o);
          if (!o) setEditingId(null);
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editingId ? 'Edit draft claim' : 'File a medical claim'}</DialogTitle>
            <DialogDescription>
              {editingId
                ? 'Only you can see a draft. Submit it to HR once the receipt is attached.'
                : 'This is saved as a draft only you can see. Attach your receipt, then submit it to HR.'}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="service-date">Date of treatment</Label>
              <Input
                id="service-date"
                type="date"
                value={form.serviceDate}
                onChange={(e) => setForm({ ...form, serviceDate: e.target.value })}
              />
            </div>
            <div className="space-y-2">
              <Label>Type of expense</Label>
              <Select
                value={form.expenseType}
                onValueChange={(v) => v && setForm({ ...form, expenseType: v as MedicalExpenseType })}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {MEDICAL_EXPENSE_TYPE_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Where were you treated?</Label>
              <Select
                value={form.facilityId}
                // ⚠ Radix reports '' when an edit's value arrives before the facility list (finding
                // C6) — that would wipe the draft's facility. There is no "none" choice here.
                onValueChange={(v) => v && setForm({ ...form, facilityId: v })}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Choose a facility" />
                </SelectTrigger>
                <SelectContent>
                  {facilities.map((f) => (
                    <SelectItem key={f.id} value={f.id}>
                      {f.facilityName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="claim-description">What was it for?</Label>
              <Textarea
                id="claim-description"
                value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
                rows={3}
                placeholder="e.g. Consultation and prescription for malaria"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="claim-amount">Amount you paid</Label>
              <Input
                id="claim-amount"
                type="number"
                value={form.amount}
                onChange={(e) => setForm({ ...form, amount: e.target.value })}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setFormOpen(false)}>
              Cancel
            </Button>
            <Button disabled={!canSave || save.isPending} onClick={() => save.mutate()}>
              {editingId ? 'Save changes' : 'Save draft'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={submitting !== null}
        onOpenChange={(o) => !o && setSubmitting(null)}
        title={`Submit ${submitting?.claimNumber ?? 'this claim'} to HR?`}
        description="Once submitted you can no longer edit or discard it. You can still attach more receipts until HR decides."
        confirmText="Submit to HR"
        isLoading={submit.isPending}
        onConfirm={async () => {
          if (submitting) await submit.mutateAsync(submitting.id).catch(() => undefined);
        }}
      />

      <ConfirmationDialog
        open={discarding !== null}
        onOpenChange={(o) => !o && setDiscarding(null)}
        title={`Discard ${discarding?.claimNumber ?? 'this draft'}?`}
        description="The draft is removed from your claims. HR never saw it."
        confirmText="Discard draft"
        variant="destructive"
        isLoading={discard.isPending}
        onConfirm={async () => {
          if (discarding) await discard.mutateAsync(discarding.id).catch(() => undefined);
        }}
      />
    </div>
  );
}
