'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Gavel, Landmark, Paperclip, Pencil, Plus, Send } from 'lucide-react';
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
import { medicalClaimService, nhisClaimService } from '@/services/hr/medical-claims.service';
import { medicalFacilityService } from '@/services/hr/medical-reference.service';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { Textarea } from '@/components/ui/textarea';
import { NhisClaimDocumentsPanel } from '@/components/hr/medical/NhisClaimDocumentsPanel';
import { HR_ADMIN_ROLES, HR_ROLES } from '@/components/hr/common/PermissionGate';
import { useAuth } from '@/hooks/use-auth';
import { MEDICAL_SERVICE_TYPE_OPTIONS, NHIS_CLAIM_STATUS_OPTIONS } from '@/types/hr/medical';
import type {
  HealthcareFacilitySummary,
  MedicalExpenseClaimSummary,
  MedicalServiceType,
  NHISClaimStatus,
  NHISClaimSummary,
  PhysicianSummary,
} from '@/types/hr/medical';

/**
 * Claims made against the National Health Insurance Scheme.
 *
 * These are claims the organisation makes to NHIS, not reimbursements to an employee — so the
 * lifecycle is submission and settlement rather than approval: draft → submitted (in a batch) →
 * the scheme's decision → payment recorded against it.
 */
const NONE = '__none__';

const money = (v?: number | null) =>
  v === null || v === undefined
    ? '—'
    : v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const statusLabel = (v: string) =>
  NHIS_CLAIM_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;

/** The create dialog's fields, as strings — the numbers are coerced once, on submit. */
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
  onEdit,
  onDecide,
}: {
  items: NHISClaimSummary[];
  empty: string;
  onSubmit: (claim: NHISClaimSummary) => void;
  onSettle: (claim: NHISClaimSummary) => void;
  onDocuments: (claim: NHISClaimSummary) => void;
  onEdit?: (claim: NHISClaimSummary) => void;
  onDecide?: (claim: NHISClaimSummary) => void;
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
                  {/*
                    ⚠ Editing is offered on a draft or a rejected claim ONLY, matching the guard
                    the service now enforces. Everything from Submitted onward is a statement
                    already made to the scheme; correcting one there means recording its decision
                    or its payment, not rewriting what was claimed.
                  */}
                  {onEdit && (c.status === 'Draft' || c.status === 'Rejected') && (
                    <Button variant="ghost" size="sm" onClick={() => onEdit(c)}>
                      <Pencil className="mr-2 h-4 w-4" /> Edit
                    </Button>
                  )}
                  {c.status === 'Draft' && (
                    <Button variant="ghost" size="sm" onClick={() => onSubmit(c)}>
                      <Send className="mr-2 h-4 w-4" /> Submit
                    </Button>
                  )}
                  {/*
                    The middle of the lifecycle: what the scheme came back with. Offered once a
                    claim has been put to them and before it is settled.
                  */}
                  {onDecide && (c.status === 'Submitted' || c.status === 'UnderReview') && (
                    <Button variant="ghost" size="sm" onClick={() => onDecide(c)}>
                      <Gavel className="mr-2 h-4 w-4" /> Record decision
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
  // ⚠ There was no create form at all. The screen listed, submitted and settled claims, and never
  // called `nhisClaimService.create` — so five fields on the create DTO were unreachable, and the
  // reverse read `GetByLinkedMedicalClaimIdAsync` could only ever return empty. Built 2026-09-01.
  const [creating, setCreating] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [deciding, setDeciding] = useState<NHISClaimSummary | null>(null);
  const [decision, setDecision] = useState<NHISClaimStatus>('Approved');
  const [approvedAmount, setApprovedAmount] = useState('');
  const [rejectionReason, setRejectionReason] = useState('');
  const [draft, setDraft] = useState(emptyDraft);
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

  // Only fetched while the dialog is open — this screen should not pull the facility register to
  // render a list of claims.
  const { data: facilities = [] } = useQuery({
    queryKey: ['hr', 'healthcare-facilities', 'active'],
    queryFn: () => medicalFacilityService.getActiveFacilities(),
    enabled: creating,
  });

  const { data: physicians = [] } = useQuery({
    queryKey: ['hr', 'physicians', 'facility', draft.facilityId],
    queryFn: () => medicalFacilityService.getPhysiciansByFacility(draft.facilityId),
    enabled: creating && !!draft.facilityId,
  });

  // The employer-reimbursement claims this NHIS claim could sit against — scoped to the employee,
  // because linking a claim to another person's reimbursement is never right.
  const { data: linkableClaims = [] } = useQuery({
    queryKey: ['hr', 'medical-expense-claims', 'employee', draft.employeeId],
    queryFn: () => medicalClaimService.getByEmployee(draft.employeeId),
    enabled: creating && !!draft.employeeId,
  });

  const create = useMutation({
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
      setCreating(false);
      setEditingId(null);
      setDraft(emptyDraft);
      refresh();
    },
    onError,
  });

  /**
   * Open the edit dialog on a claim.
   *
   * ⚠ Reads the DETAIL, not the list row. The summary carries five fields; the form needs
   * seventeen, and binding a form to a projection then saving it blanks everything the projection
   * omits — the D-09/D-12 shape this lane has now met four times.
   */
  const openEdit = async (claim: NHISClaimSummary) => {
    try {
      const full = await nhisClaimService.getClaim(claim.id);
      setDraft({
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
      setEditingId(claim.id);
      setCreating(true);
    } catch (e) {
      onError(e);
    }
  };

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
      setDeciding(null);
      setApprovedAmount('');
      setRejectionReason('');
      setDecision('Approved');
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
        actions={
          canWrite ? (
            <Button size="sm" onClick={() => { setDraft(emptyDraft); setCreating(true); }}>
              <Plus className="mr-2 h-4 w-4" /> Record a claim
            </Button>
          ) : undefined
        }
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
            onEdit={canWrite ? openEdit : undefined}
            onDecide={canWrite ? setDeciding : undefined}
            onDocuments={setDocumentsFor}
          />
        </TabsContent>
        <TabsContent value="all" className="mt-4">
          <NhisTable
            items={all}
            empty="No NHIS claims have been recorded yet."
            onSubmit={setSubmitting}
            onSettle={setSettling}
            onEdit={canWrite ? openEdit : undefined}
            onDecide={canWrite ? setDeciding : undefined}
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

      {/* Record a claim — the form this screen never had. */}
      <Dialog
        open={creating}
        onOpenChange={(o) => {
          setCreating(o);
          if (!o) setEditingId(null);
        }}
      >
        <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>{editingId ? 'Edit NHIS claim' : 'Record an NHIS claim'}</DialogTitle>
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
                  onValueChange={(v) => setDraft({ ...draft, facilityId: v, physicianId: '' })}
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
                  onValueChange={(v) => setDraft({ ...draft, physicianId: v === NONE ? '' : v })}
                  disabled={!draft.facilityId}
                >
                  <SelectTrigger>
                    <SelectValue placeholder={draft.facilityId ? 'Optional' : 'Choose a facility first'} />
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
              ⚠ The reason this dialog exists at all. `LinkedMedicalClaimId` had no writer, so
              `GetByLinkedMedicalClaimIdAsync` — a reverse read that already existed — could only
              ever return empty. Set it when the employer reimburses the co-pay or a top-up.
            */}
            <div className="space-y-2">
              <Label>Employer reimbursement this sits against</Label>
              <Select
                value={draft.linkedMedicalClaimId || NONE}
                onValueChange={(v) =>
                  setDraft({ ...draft, linkedMedicalClaimId: v === NONE ? '' : v })
                }
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
            <Button variant="outline" onClick={() => setCreating(false)}>
              Cancel
            </Button>
            <Button
              disabled={
                create.isPending ||
                !draft.employeeId ||
                !draft.facilityId ||
                !draft.serviceDate ||
                !draft.serviceDescription.trim() ||
                !draft.nhisMembershipNumber.trim() ||
                !(Number(draft.totalCost) > 0)
              }
              onClick={() => create.mutate()}
            >
              {create.isPending ? 'Saving…' : editingId ? 'Save changes' : 'Record claim'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      {/*
        Recording what the scheme decided.

        ⚠ Without this the lifecycle had no middle. A claim could be created and submitted, and
        then nothing — `updateStatus` had no screen caller at all, so no claim could reach Approved,
        and "Record payment" (offered only on Approved or PartiallyApproved) was unreachable UI on
        a dead branch. Measured 2026-09-01: all five claims in the tenant were Draft.
      */}
      <Dialog open={!!deciding} onOpenChange={(o) => !o && setDeciding(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record the scheme&apos;s decision</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <p className="text-sm text-muted-foreground">
              Claim {deciding?.claimNumber} · {money(deciding?.totalCost)} claimed
            </p>

            <div className="space-y-2">
              <Label>What NHIS decided</Label>
              <Select value={decision} onValueChange={(v) => setDecision(v as NHISClaimStatus)}>
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
            <Button variant="outline" onClick={() => setDeciding(null)}>
              Cancel
            </Button>
            <Button
              disabled={
                decide.isPending ||
                (decision === 'Rejected'
                  ? !rejectionReason.trim()
                  : !(Number(approvedAmount) > 0))
              }
              onClick={() => deciding && decide.mutate(deciding.id)}
            >
              Record decision
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
