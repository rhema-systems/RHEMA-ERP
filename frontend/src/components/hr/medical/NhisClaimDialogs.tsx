'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
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
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { useToast } from '@/hooks/use-toast';
import { medicalClaimService, nhisClaimService } from '@/services/hr/medical-claims.service';
import { medicalFacilityService } from '@/services/hr/medical-reference.service';
import { MEDICAL_SERVICE_TYPE_OPTIONS, NHIS_CLAIM_STATUS_OPTIONS } from '@/types/hr/medical';
import type {
  HealthcareFacilitySummary,
  MedicalExpenseClaimSummary,
  MedicalServiceType,
  NHISClaim,
  NHISClaimStatus,
  NHISClaimSummary,
  PhysicianSummary,
} from '@/types/hr/medical';

/**
 * The NHIS claim's dialogs — record or edit, submit, the scheme's decision, payment — shared by the
 * register and the claim's own page, so the two places a claim can be moved on cannot drift apart.
 *
 * Each dialog is open while its `claim` is set and reports back through `onDone`, which is where
 * the caller refreshes whatever it shows.
 */
const NONE = '__none__';

const money = (v?: number | null) =>
  v === null || v === undefined
    ? '—'
    : v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export const nhisStatusLabel = (v: string) =>
  NHIS_CLAIM_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;

export function NhisStatusBadge({ status }: { status: NHISClaimStatus }) {
  if (status === 'Paid' || status === 'Approved')
    return <Badge variant="secondary">{nhisStatusLabel(status)}</Badge>;
  if (status === 'Rejected') return <Badge variant="destructive">Rejected</Badge>;
  return <Badge variant="outline">{nhisStatusLabel(status)}</Badge>;
}

/**
 * What a claim's status admits, so neither screen offers a step the service would refuse.
 *
 * ⚠ Editing is a draft or a rejected claim ONLY, matching the guard the service enforces. Everything
 * from Submitted onward is a statement already made to the scheme; correcting one there means
 * recording its decision or its payment, not rewriting what was claimed.
 */
export function nhisClaimStage(status: NHISClaimStatus) {
  return {
    canEdit: status === 'Draft' || status === 'Rejected',
    canSubmit: status === 'Draft',
    canDecide: status === 'Submitted' || status === 'UnderReview',
    canSettle: status === 'Approved' || status === 'PartiallyApproved',
  };
}

type ClaimRef = Pick<NHISClaimSummary, 'id' | 'claimNumber' | 'totalCost'>;

const onErrorToast =
  (toast: ReturnType<typeof useToast>['toast']) => (error: unknown) =>
    toast({
      variant: 'destructive',
      title: 'Could not complete that',
      description: error instanceof Error ? error.message : 'Unexpected error',
    });

/** The record dialog's fields, as strings — the numbers are coerced once, on submit. */
const emptyDraft = {
  employeeId: '',
  employeeLabel: null as string | null,
  nhisMembershipNumber: '',
  facilityId: '',
  physicianId: '',
  serviceDate: '',
  serviceType: 'Consultation' as MedicalServiceType,
  serviceDescription: '',
  diagnosis: '',
  icdCode: '',
  totalCost: '',
  nhisCoveredAmount: '',
  coPayAmount: '',
  linkedMedicalClaimId: '',
  notes: '',
};

const draftFrom = (full: NHISClaim): typeof emptyDraft => ({
  employeeId: full.employeeId,
  employeeLabel: full.employeeName ?? null,
  nhisMembershipNumber: full.nhisMembershipNumber ?? '',
  facilityId: full.facilityId,
  physicianId: full.physicianId ?? '',
  serviceDate: full.serviceDate?.slice(0, 10) ?? '',
  serviceType: full.serviceType,
  serviceDescription: full.serviceDescription ?? '',
  diagnosis: full.diagnosis ?? '',
  icdCode: full.icdCode ?? '',
  totalCost: String(full.totalCost ?? ''),
  nhisCoveredAmount: full.nhisCoveredAmount == null ? '' : String(full.nhisCoveredAmount),
  coPayAmount: full.coPayAmount == null ? '' : String(full.coPayAmount),
  linkedMedicalClaimId: full.linkedMedicalClaimId ?? '',
  notes: full.notes ?? '',
});

/**
 * Record a claim, or edit one.
 *
 * ⚠ Edit takes the DETAIL (`NHISClaim`), never the list row. The summary carries five fields; the
 * form needs seventeen, and binding a form to a projection then saving it blanks everything the
 * projection omits — the D-09/D-12 shape.
 */
export function NhisClaimFormDialog({
  open,
  onOpenChange,
  claim,
  onDone,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** The claim to edit; null or absent records a new one. */
  claim?: NHISClaim | null;
  onDone: () => void;
}) {
  const { toast } = useToast();
  const [draft, setDraft] = useState(emptyDraft);
  const editingId = claim?.id ?? null;

  // Seeded on open, and keyed on the id rather than the object: the detail page refetches its
  // claim on focus, and a new object for the same claim must not wipe what is being typed.
  useEffect(() => {
    if (open) setDraft(claim ? draftFrom(claim) : emptyDraft);
  }, [open, editingId]);

  // Only fetched while the dialog is open — a list of claims should not pull the facility register.
  const { data: facilities = [] } = useQuery({
    queryKey: ['hr', 'healthcare-facilities', 'active'],
    queryFn: () => medicalFacilityService.getActiveFacilities(),
    enabled: open,
  });

  const { data: physicians = [] } = useQuery({
    queryKey: ['hr', 'physicians', 'facility', draft.facilityId],
    queryFn: () => medicalFacilityService.getPhysiciansByFacility(draft.facilityId),
    enabled: open && !!draft.facilityId,
  });

  // The employer-reimbursement claims this NHIS claim could sit against — scoped to the employee,
  // because linking a claim to another person's reimbursement is never right.
  const { data: linkableClaims = [] } = useQuery({
    queryKey: ['hr', 'medical-expense-claims', 'employee', draft.employeeId],
    queryFn: () => medicalClaimService.getByEmployee(draft.employeeId),
    enabled: open && !!draft.employeeId,
  });

  const save = useMutation({
    mutationFn: () => {
      const body = {
        employeeId: draft.employeeId,
        isForDependent: false,
        nhisMembershipNumber: draft.nhisMembershipNumber.trim(),
        facilityId: draft.facilityId,
        physicianId: draft.physicianId || null,
        serviceDate: draft.serviceDate,
        serviceType: draft.serviceType,
        serviceDescription: draft.serviceDescription.trim(),
        diagnosis: draft.diagnosis.trim() || null,
        icdCode: draft.icdCode.trim() || null,
        totalCost: Number(draft.totalCost),
        nhisCoveredAmount: draft.nhisCoveredAmount === '' ? null : Number(draft.nhisCoveredAmount),
        coPayAmount: draft.coPayAmount === '' ? null : Number(draft.coPayAmount),
        linkedMedicalClaimId: draft.linkedMedicalClaimId || null,
        notes: draft.notes.trim() || null,
      };
      return editingId
        ? nhisClaimService.update(editingId, { ...body, id: editingId })
        : nhisClaimService.create(body);
    },
    onSuccess: () => {
      toast({
        title: editingId ? 'Claim updated' : 'Claim recorded',
        description: editingId ? undefined : 'It is a draft until submitted to the scheme.',
      });
      onOpenChange(false);
      onDone();
    },
    onError: onErrorToast(toast),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>
            {editingId ? `Edit ${claim?.claimNumber ?? 'NHIS claim'}` : 'Record an NHIS claim'}
          </DialogTitle>
        </DialogHeader>

        <div className="grid gap-4 py-2">
          <div className="space-y-2">
            <Label>Member</Label>
            <EmployeePicker
              value={draft.employeeId || null}
              initialLabel={draft.employeeLabel}
              onChange={(id, label) =>
                // Changing the member invalidates the claim they were being linked to.
                setDraft({
                  ...draft,
                  employeeId: id ?? '',
                  employeeLabel: label,
                  linkedMedicalClaimId: '',
                })
              }
              placeholder="Search for the member treated…"
            />
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="nhis-membership">NHIS membership number</Label>
              <Input
                id="nhis-membership"
                value={draft.nhisMembershipNumber}
                onChange={(e) => setDraft({ ...draft, nhisMembershipNumber: e.target.value })}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="nhis-service-date">Date of service</Label>
              <Input
                id="nhis-service-date"
                type="date"
                value={draft.serviceDate}
                onChange={(e) => setDraft({ ...draft, serviceDate: e.target.value })}
              />
            </div>
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-2">
              <Label>Facility</Label>
              <Select
                value={draft.facilityId}
                onValueChange={(v) => {
                  // ⚠ Radix reports '' when the value arrives before its async options (finding C6);
                  // there is no "no facility" choice here, so '' is never a real pick.
                  if (v === '') return;
                  setDraft({ ...draft, facilityId: v, physicianId: '' });
                }}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Where they were treated" />
                </SelectTrigger>
                <SelectContent>
                  {facilities.map((f: HealthcareFacilitySummary) => (
                    <SelectItem key={f.id} value={f.id}>
                      {f.facilityName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Physician</Label>
              <Select
                value={draft.physicianId || NONE}
                onValueChange={(v) => {
                  if (v === '') return;
                  setDraft({ ...draft, physicianId: v === NONE ? '' : v });
                }}
                disabled={!draft.facilityId}
              >
                <SelectTrigger>
                  <SelectValue
                    placeholder={draft.facilityId ? 'Optional' : 'Choose a facility first'}
                  />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NONE}>Not recorded</SelectItem>
                  {physicians.map((ph: PhysicianSummary) => (
                    <SelectItem key={ph.id} value={ph.id}>
                      {ph.fullName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-2">
              <Label>Service</Label>
              <Select
                value={draft.serviceType}
                onValueChange={(v) => setDraft({ ...draft, serviceType: v as MedicalServiceType })}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {MEDICAL_SERVICE_TYPE_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="nhis-icd">ICD code</Label>
              <Input
                id="nhis-icd"
                value={draft.icdCode}
                onChange={(e) => setDraft({ ...draft, icdCode: e.target.value })}
                placeholder="Optional"
              />
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="nhis-description">What was done</Label>
            <Input
              id="nhis-description"
              value={draft.serviceDescription}
              onChange={(e) => setDraft({ ...draft, serviceDescription: e.target.value })}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="nhis-diagnosis">Diagnosis</Label>
            <Input
              id="nhis-diagnosis"
              value={draft.diagnosis}
              onChange={(e) => setDraft({ ...draft, diagnosis: e.target.value })}
              placeholder="Optional"
            />
          </div>

          <div className="grid gap-3 sm:grid-cols-3">
            <div className="space-y-2">
              <Label htmlFor="nhis-total">Total cost</Label>
              <Input
                id="nhis-total"
                type="number"
                min={0}
                value={draft.totalCost}
                onChange={(e) => setDraft({ ...draft, totalCost: e.target.value })}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="nhis-covered">Covered by NHIS</Label>
              <Input
                id="nhis-covered"
                type="number"
                min={0}
                value={draft.nhisCoveredAmount}
                onChange={(e) => setDraft({ ...draft, nhisCoveredAmount: e.target.value })}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="nhis-copay">Co-pay</Label>
              <Input
                id="nhis-copay"
                type="number"
                min={0}
                value={draft.coPayAmount}
                onChange={(e) => setDraft({ ...draft, coPayAmount: e.target.value })}
              />
            </div>
          </div>

          {/*
            ⚠ `LinkedMedicalClaimId` had no writer before this dialog, so the reverse read
            `GetByLinkedMedicalClaimIdAsync` could only ever return empty. Set it when the employer
            reimburses the co-pay or a top-up.
          */}
          <div className="space-y-2">
            <Label>Employer reimbursement this sits against</Label>
            <Select
              value={draft.linkedMedicalClaimId || NONE}
              onValueChange={(v) => {
                if (v === '') return;
                setDraft({ ...draft, linkedMedicalClaimId: v === NONE ? '' : v });
              }}
              disabled={!draft.employeeId}
            >
              <SelectTrigger>
                <SelectValue
                  placeholder={draft.employeeId ? 'None — NHIS only' : 'Choose the member first'}
                />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE}>None — NHIS only</SelectItem>
                {linkableClaims.map((c: MedicalExpenseClaimSummary) => (
                  <SelectItem key={c.id} value={c.id}>
                    {c.claimNumber} · {money(c.amountRequested)} · {c.statusName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">
              Only set this when the employer covers the co-pay or a top-up, so the two claims for
              one episode of treatment can be seen together.
            </p>
          </div>

          <div className="space-y-2">
            <Label htmlFor="nhis-notes">Notes</Label>
            <Textarea
              id="nhis-notes"
              rows={2}
              value={draft.notes}
              onChange={(e) => setDraft({ ...draft, notes: e.target.value })}
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            disabled={
              save.isPending ||
              !draft.employeeId ||
              !draft.facilityId ||
              !draft.serviceDate ||
              !draft.serviceDescription.trim() ||
              !draft.nhisMembershipNumber.trim() ||
              !(Number(draft.totalCost) > 0)
            }
            onClick={() => save.mutate()}
          >
            {save.isPending ? 'Saving…' : editingId ? 'Save changes' : 'Record claim'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** Put a draft to the scheme, in a batch. */
export function NhisSubmitDialog({
  claim,
  onClose,
  onDone,
}: {
  claim: ClaimRef | null;
  onClose: () => void;
  onDone: () => void;
}) {
  const { toast } = useToast();
  const [batchNumber, setBatchNumber] = useState('');

  // Reset per claim opened — keyed on the id, as a refetch hands back a new object for the same one.
  useEffect(() => {
    if (claim) setBatchNumber('');
  }, [claim?.id]);

  // The claim travels as a mutation argument rather than being read back out of dialog state,
  // which would race the dialog closing.
  const submit = useMutation({
    mutationFn: (claimId: string) => nhisClaimService.submit(claimId, batchNumber || null),
    onSuccess: () => {
      toast({ title: 'Claim submitted to NHIS' });
      onClose();
      onDone();
    },
    onError: onErrorToast(toast),
  });

  return (
    <Dialog open={!!claim} onOpenChange={(o) => !o && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Submit {claim?.claimNumber} to NHIS</DialogTitle>
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
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button
            disabled={!claim || submit.isPending}
            onClick={() => claim && submit.mutate(claim.id)}
          >
            Submit
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/**
 * Record what the scheme decided.
 *
 * ⚠ Without this the lifecycle had no middle: `updateStatus` had no screen caller, so no claim could
 * reach Approved and "Record payment" was unreachable. Measured 2026-09-01: all five claims in the
 * tenant were Draft.
 */
export function NhisDecisionDialog({
  claim,
  onClose,
  onDone,
}: {
  claim: ClaimRef | null;
  onClose: () => void;
  onDone: () => void;
}) {
  const { toast } = useToast();
  const [decision, setDecision] = useState<NHISClaimStatus>('Approved');
  const [approvedAmount, setApprovedAmount] = useState('');
  const [rejectionReason, setRejectionReason] = useState('');

  useEffect(() => {
    if (!claim) return;
    setDecision('Approved');
    setApprovedAmount('');
    setRejectionReason('');
  }, [claim?.id]);

  const decide = useMutation({
    mutationFn: (claimId: string) =>
      nhisClaimService.updateStatus(
        claimId,
        decision,
        decision === 'Rejected' ? null : Number(approvedAmount),
        decision === 'Rejected' ? rejectionReason.trim() : null,
      ),
    onSuccess: () => {
      toast({ title: 'Decision recorded' });
      onClose();
      onDone();
    },
    onError: onErrorToast(toast),
  });

  return (
    <Dialog open={!!claim} onOpenChange={(o) => !o && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Record the scheme&apos;s decision</DialogTitle>
        </DialogHeader>
        <div className="space-y-4 py-2">
          <p className="text-sm text-muted-foreground">
            Claim {claim?.claimNumber} · {money(claim?.totalCost)} claimed
          </p>

          <div className="space-y-2">
            <Label>What NHIS decided</Label>
            <Select value={decision} onValueChange={(v) => v && setDecision(v as NHISClaimStatus)}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Approved">Approved in full</SelectItem>
                <SelectItem value="PartiallyApproved">Approved in part</SelectItem>
                <SelectItem value="Rejected">Rejected</SelectItem>
              </SelectContent>
            </Select>
          </div>

          {decision !== 'Rejected' && (
            <div className="space-y-2">
              <Label htmlFor="nhis-approved">Amount approved</Label>
              <Input
                id="nhis-approved"
                type="number"
                min={0}
                value={approvedAmount}
                onChange={(e) => setApprovedAmount(e.target.value)}
              />
              <p className="text-xs text-muted-foreground">
                What the scheme will actually pay, which is what the settlement is checked against.
              </p>
            </div>
          )}

          {decision === 'Rejected' && (
            <div className="space-y-2">
              <Label htmlFor="nhis-rejection">Why it was rejected</Label>
              <Textarea
                id="nhis-rejection"
                rows={3}
                value={rejectionReason}
                onChange={(e) => setRejectionReason(e.target.value)}
              />
              <p className="text-xs text-muted-foreground">
                A rejected claim stays editable, so this is what the next person needs in order to
                correct and resubmit it.
              </p>
            </div>
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button
            disabled={
              decide.isPending ||
              (decision === 'Rejected' ? !rejectionReason.trim() : !(Number(approvedAmount) > 0))
            }
            onClick={() => claim && decide.mutate(claim.id)}
          >
            Record decision
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** Record the scheme's payment against an approved claim. */
export function NhisPaymentDialog({
  claim,
  onClose,
  onDone,
}: {
  claim: ClaimRef | null;
  onClose: () => void;
  onDone: () => void;
}) {
  const { toast } = useToast();
  const [paymentReference, setPaymentReference] = useState('');
  const [paidAmount, setPaidAmount] = useState('');

  useEffect(() => {
    if (!claim) return;
    setPaymentReference('');
    setPaidAmount('');
  }, [claim?.id]);

  const settle = useMutation({
    mutationFn: (claimId: string) =>
      nhisClaimService.recordPayment(claimId, paymentReference, Number(paidAmount)),
    onSuccess: () => {
      toast({ title: 'Payment recorded' });
      onClose();
      onDone();
    },
    onError: onErrorToast(toast),
  });

  return (
    <Dialog open={!!claim} onOpenChange={(o) => !o && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Record NHIS payment for {claim?.claimNumber}</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="nhis-amount">Amount received</Label>
            <Input
              id="nhis-amount"
              type="number"
              value={paidAmount}
              onChange={(e) => setPaidAmount(e.target.value)}
              placeholder={claim ? String(claim.totalCost) : undefined}
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
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button
            disabled={
              !claim || !paymentReference.trim() || !(Number(paidAmount) > 0) || settle.isPending
            }
            onClick={() => claim && settle.mutate(claim.id)}
          >
            Record payment
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
