'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
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
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useToast } from '@/hooks/use-toast';
import { medicalClaimService } from '@/services/hr/medical-claims.service';
import { leaveService } from '@/services/hr/leave.service';
import {
  MEDICAL_EXPENSE_TYPE_OPTIONS,
  MEDICAL_ITEM_TYPE_OPTIONS,
} from '@/types/hr/medical';
import type {
  MedicalExpenseClaim,
  MedicalExpenseItem,
  MedicalExpenseType,
  MedicalItemType,
} from '@/types/hr/medical';

/**
 * Correcting a claim and its lines.
 *
 * ⚠ **`PUT` is not a patch.** Every field on `UpdateMedicalExpenseClaimDto` is written, so a
 * payload that omits one blanks it. Both dialogs therefore seed from the full record and send
 * everything back — including the fields they do not show, such as `preAuthorizationId`,
 * `referralId` and `insurancePolicyId`, which are set by other surfaces and would otherwise be
 * silently dropped by an edit made here.
 *
 * `leaveRequestId` used to be one of those carried-but-unsettable fields, and no surface anywhere
 * set it — so a claim was never tied to the sick leave it arose from (closure plan L-37). The
 * "Related sick leave" picker below is that surface.
 *
 * `admissionStart` / `admissionEnd` are two of the fields the closure ledger's section E lists as
 * settable by the API and reachable from no form; the hospitalisation toggle below is where they
 * finally become editable.
 */

/**
 * The "not related to leave" choice. A Radix Select item cannot carry an empty string as its value
 * (an empty value is how it represents "nothing selected"), so the absence of a link needs its own
 * sentinel, mapped back to null on save.
 */
const NO_LEAVE = '__none__';

const toDateInput = (v?: string | null) => {
  if (!v) return '';
  const d = new Date(v);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
};

interface ClaimEditProps {
  claim: MedicalExpenseClaim;
  queryKey: unknown[];
  canWrite: boolean;
  canDelete: boolean;
  /** Called after a successful delete — the record no longer exists, so the page must leave. */
  onDeleted: () => void;
}

export function ClaimEditActions({
  claim,
  queryKey,
  canWrite,
  canDelete,
  onDeleted,
}: ClaimEditProps) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [editing, setEditing] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [form, setForm] = useState<Record<string, string | boolean>>({});
  const set = (k: string, v: string | boolean) => setForm((f) => ({ ...f, [k]: v }));
  const str = (k: string) => String(form[k] ?? '');
  const orNull = (k: string) => (str(k).trim() === '' ? null : str(k));

  /**
   * The employee's approved leave for the year the treatment happened, so a claim can name the sick
   * leave it arose from. `MedicalExpenseClaim.LeaveRequestId` has carried the comment "Leave
   * Integration" since the port and nothing ever wrote it (closure plan L-37).
   *
   * Approved only, and only while the dialog is open. An employee reading their own claim cannot
   * see this list — the leave history endpoint answers self-or-HR.Leave.Read, so a claims clerk
   * without leave access simply gets no options rather than an error.
   */
  const claimYear = Number((claim.serviceDate ?? claim.claimDate ?? '').slice(0, 4)) || 0;
  const { data: leaveOptions } = useQuery({
    queryKey: ['hr', 'leave-requests', 'history', claim.employeeId, claimYear, 'Approved'],
    queryFn: () =>
      leaveService
        .getEmployeeHistory(claim.employeeId, claimYear, 1, 50, 'Approved')
        .then((r) => r.items)
        .catch(() => []),
    enabled: editing && !!claim.employeeId,
  });

  useEffect(() => {
    if (!editing) return;
    setForm({
      serviceDate: toDateInput(claim.serviceDate),
      serviceEndDate: toDateInput(claim.serviceEndDate),
      expenseType: claim.expenseType,
      description: claim.description ?? '',
      diagnosis: claim.diagnosis ?? '',
      icdCode: claim.icdCode ?? '',
      treatmentReceived: claim.treatmentReceived ?? '',
      isEmergency: claim.isEmergency,
      requiredHospitalization: claim.requiredHospitalization,
      admissionStart: toDateInput(claim.admissionStart),
      admissionEnd: toDateInput(claim.admissionEnd),
      totalAmount: String(claim.totalAmount ?? ''),
      amountRequested: String(claim.amountRequested ?? ''),
      additionalNotes: claim.additionalNotes ?? '',
      leaveRequestId: claim.leaveRequestId ?? '',
    });
  }, [editing, claim]);

  const onError = (error: unknown) =>
    toast({
      variant: 'destructive',
      title: 'Could not save',
      description: error instanceof Error ? error.message : 'Unexpected error',
    });

  const save = useMutation({
    mutationFn: () =>
      medicalClaimService.updateClaim(claim.id, {
        id: claim.id,
        serviceDate: str('serviceDate'),
        serviceEndDate: orNull('serviceEndDate'),
        expenseType: str('expenseType') as MedicalExpenseType,
        description: str('description'),
        // Not shown here, and carried through so an edit does not drop them.
        facilityId: claim.facilityId,
        physicianId: claim.physicianId ?? null,
        diagnosis: orNull('diagnosis'),
        icdCode: orNull('icdCode'),
        treatmentReceived: orNull('treatmentReceived'),
        isEmergency: Boolean(form.isEmergency),
        requiredHospitalization: Boolean(form.requiredHospitalization),
        // DateOnly on the API — a bare yyyy-MM-dd, which is what the date input gives us.
        admissionStart: Boolean(form.requiredHospitalization) ? orNull('admissionStart') : null,
        admissionEnd: Boolean(form.requiredHospitalization) ? orNull('admissionEnd') : null,
        preAuthorizationId: claim.preAuthorizationId ?? null,
        referralId: claim.referralId ?? null,
        totalAmount: Number(str('totalAmount') || 0),
        amountRequested: Number(str('amountRequested') || 0),
        insurancePolicyId: claim.insurancePolicyId ?? null,
        leaveRequestId: orNull('leaveRequestId'),
        additionalNotes: orNull('additionalNotes'),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey });
      setEditing(false);
      toast({ title: `${claim.claimNumber} updated.` });
    },
    onError,
  });

  const remove = useMutation({
    mutationFn: () => medicalClaimService.deleteClaim(claim.id),
    onSuccess: () => {
      setConfirming(false);
      toast({ title: `${claim.claimNumber} deleted.` });
      onDeleted();
    },
    onError,
  });

  if (!canWrite && !canDelete) return null;

  return (
    <>
      {canWrite && (
        <Button variant="outline" onClick={() => setEditing(true)}>
          Edit claim
        </Button>
      )}
      {canDelete && (
        <Button variant="outline" onClick={() => setConfirming(true)}>
          Delete
        </Button>
      )}

      <Dialog open={editing} onOpenChange={(o) => !o && setEditing(false)}>
        <DialogContent className="max-h-[85vh] max-w-2xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Edit {claim.claimNumber}</DialogTitle>
            <DialogDescription>
              Every field is written on save. The facility, physician, linked pre-authorisation,
              referral, policy and leave request are carried through unchanged.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-2">
                <Label>Service date</Label>
                <Input
                  type="date"
                  value={str('serviceDate')}
                  onChange={(e) => set('serviceDate', e.target.value)}
                />
              </div>
              <div className="space-y-2">
                <Label>Service end date</Label>
                <Input
                  type="date"
                  value={str('serviceEndDate')}
                  onChange={(e) => set('serviceEndDate', e.target.value)}
                />
              </div>
            </div>

            <div className="space-y-2">
              <Label>Expense type</Label>
              <Select value={str('expenseType')} onValueChange={(v) => set('expenseType', v)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {MEDICAL_EXPENSE_TYPE_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {/*
              The medical → leave join. Only offered when the employee has approved leave in the
              treatment year; a claim that did not arise from leave simply leaves it unset.
            */}
            {!!leaveOptions?.length && (
              <div className="space-y-2">
                <Label>Related sick leave</Label>
                <Select
                  value={str('leaveRequestId') || NO_LEAVE}
                  onValueChange={(v) => set('leaveRequestId', v === NO_LEAVE ? '' : v)}
                >
                  <SelectTrigger><SelectValue placeholder="Not related to leave" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value={NO_LEAVE}>Not related to leave</SelectItem>
                    {leaveOptions.map((lr) => (
                      <SelectItem key={lr.id} value={lr.id}>
                        {lr.requestNumber} · {lr.leaveTypeName} · {lr.startDate?.slice(0, 10)} to{' '}
                        {lr.endDate?.slice(0, 10)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">
                  Ties this claim to the leave it arose from. Approved leave in {claimYear} only.
                </p>
              </div>
            )}

            <div className="space-y-2">
              <Label>Description</Label>
              <Textarea
                value={str('description')}
                onChange={(e) => set('description', e.target.value)}
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-2">
                <Label>Diagnosis</Label>
                <Input value={str('diagnosis')} onChange={(e) => set('diagnosis', e.target.value)} />
              </div>
              <div className="space-y-2">
                <Label>ICD code</Label>
                <Input value={str('icdCode')} onChange={(e) => set('icdCode', e.target.value)} />
              </div>
            </div>

            <div className="space-y-2">
              <Label>Treatment received</Label>
              <Textarea
                value={str('treatmentReceived')}
                onChange={(e) => set('treatmentReceived', e.target.value)}
              />
            </div>

            <div className="flex items-center justify-between rounded-md border p-3">
              <Label htmlFor="claim-emergency">Emergency</Label>
              <Switch
                id="claim-emergency"
                checked={Boolean(form.isEmergency)}
                onCheckedChange={(v) => set('isEmergency', v)}
              />
            </div>

            <div className="space-y-3 rounded-md border p-3">
              <div className="flex items-center justify-between">
                <Label htmlFor="claim-hosp">Required hospitalisation</Label>
                <Switch
                  id="claim-hosp"
                  checked={Boolean(form.requiredHospitalization)}
                  onCheckedChange={(v) => set('requiredHospitalization', v)}
                />
              </div>
              {Boolean(form.requiredHospitalization) && (
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-2">
                    <Label>Admitted</Label>
                    <Input
                      type="date"
                      value={str('admissionStart')}
                      onChange={(e) => set('admissionStart', e.target.value)}
                    />
                  </div>
                  <div className="space-y-2">
                    <Label>Discharged</Label>
                    <Input
                      type="date"
                      value={str('admissionEnd')}
                      onChange={(e) => set('admissionEnd', e.target.value)}
                    />
                  </div>
                </div>
              )}
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-2">
                <Label>Total amount</Label>
                <Input
                  type="number"
                  step="0.01"
                  value={str('totalAmount')}
                  onChange={(e) => set('totalAmount', e.target.value)}
                />
              </div>
              <div className="space-y-2">
                <Label>Amount requested</Label>
                <Input
                  type="number"
                  step="0.01"
                  value={str('amountRequested')}
                  onChange={(e) => set('amountRequested', e.target.value)}
                />
              </div>
            </div>

            <div className="space-y-2">
              <Label>Additional notes</Label>
              <Textarea
                value={str('additionalNotes')}
                onChange={(e) => set('additionalNotes', e.target.value)}
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setEditing(false)}>Cancel</Button>
            <Button onClick={() => save.mutate()} disabled={save.isPending}>
              {save.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={confirming} onOpenChange={(o) => !o && setConfirming(false)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Delete {claim.claimNumber}?</DialogTitle>
            <DialogDescription>
              The claim, its lines and its documents go with it. Use this for a claim raised in
              error — a claim that was considered and turned down should be rejected, not deleted,
              so the decision survives.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirming(false)}>Cancel</Button>
            <Button variant="destructive" onClick={() => remove.mutate()} disabled={remove.isPending}>
              {remove.isPending ? 'Deleting…' : 'Delete'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}

interface ItemProps {
  item: MedicalExpenseItem;
  claimId: string;
  queryKey: unknown[];
  canWrite: boolean;
  canDelete: boolean;
}

/**
 * A line's edit and delete.
 *
 * Unlike the clinical records, the item LIST read is complete — it carries description, type,
 * quantity, unit cost and remarks, which is everything the update writes — so this one can bind
 * to the row without a by-id fetch. Verified by probe-clinical-byid.mjs rather than assumed.
 */
export function ClaimItemActions({ item, claimId, queryKey, canWrite, canDelete }: ItemProps) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [editing, setEditing] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [form, setForm] = useState({
    description: item.description,
    itemType: item.itemType as string,
    quantity: String(item.quantity),
    unitCost: String(item.unitCost),
    remarks: item.remarks ?? '',
  });

  useEffect(() => {
    if (!editing) return;
    setForm({
      description: item.description,
      itemType: item.itemType as string,
      quantity: String(item.quantity),
      unitCost: String(item.unitCost),
      remarks: item.remarks ?? '',
    });
  }, [editing, item]);

  const onError = (error: unknown) =>
    toast({
      variant: 'destructive',
      title: 'Could not save',
      description: error instanceof Error ? error.message : 'Unexpected error',
    });

  const invalidate = () => queryClient.invalidateQueries({ queryKey });

  const save = useMutation({
    mutationFn: () =>
      medicalClaimService.updateItem(item.id, {
        id: item.id,
        claimId,
        description: form.description,
        itemType: form.itemType as MedicalItemType,
        quantity: Number(form.quantity || 0),
        unitCost: Number(form.unitCost || 0),
        remarks: form.remarks.trim() === '' ? null : form.remarks,
      }),
    onSuccess: () => {
      invalidate();
      setEditing(false);
      toast({ title: 'Line updated.' });
    },
    onError,
  });

  const remove = useMutation({
    mutationFn: () => medicalClaimService.deleteItem(item.id),
    onSuccess: () => {
      invalidate();
      setConfirming(false);
      toast({ title: 'Line removed.' });
    },
    onError,
  });

  if (!canWrite && !canDelete) return null;

  return (
    <>
      <div className="flex justify-end gap-1">
        {canWrite && (
          <Button variant="ghost" size="sm" onClick={() => setEditing(true)}>
            Edit
          </Button>
        )}
        {canDelete && (
          <Button variant="ghost" size="sm" onClick={() => setConfirming(true)}>
            Remove
          </Button>
        )}
      </div>

      <Dialog open={editing} onOpenChange={(o) => !o && setEditing(false)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Edit line</DialogTitle>
            <DialogDescription>
              The claim&apos;s own total is not recalculated from its lines — correct it on the
              claim if this changes what was spent.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Description</Label>
              <Input
                value={form.description}
                onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label>Type</Label>
              <Select
                value={form.itemType}
                onValueChange={(v) => setForm((f) => ({ ...f, itemType: v }))}
              >
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {MEDICAL_ITEM_TYPE_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-2">
                <Label>Quantity</Label>
                <Input
                  type="number"
                  min={1}
                  value={form.quantity}
                  onChange={(e) => setForm((f) => ({ ...f, quantity: e.target.value }))}
                />
              </div>
              <div className="space-y-2">
                <Label>Unit cost</Label>
                <Input
                  type="number"
                  step="0.01"
                  value={form.unitCost}
                  onChange={(e) => setForm((f) => ({ ...f, unitCost: e.target.value }))}
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label>Remarks</Label>
              <Textarea
                value={form.remarks}
                onChange={(e) => setForm((f) => ({ ...f, remarks: e.target.value }))}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditing(false)}>Cancel</Button>
            <Button onClick={() => save.mutate()} disabled={save.isPending}>
              {save.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={confirming} onOpenChange={(o) => !o && setConfirming(false)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Remove this line?</DialogTitle>
            <DialogDescription>{item.description}</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirming(false)}>Cancel</Button>
            <Button variant="destructive" onClick={() => remove.mutate()} disabled={remove.isPending}>
              {remove.isPending ? 'Removing…' : 'Remove'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
