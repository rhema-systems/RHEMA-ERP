'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Pencil } from 'lucide-react';
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
import { assetRegisterService } from '@/services/hr/asset-register.service';
import { ASSET_REQUISITION_PRIORITIES } from '@/types/hr/assets';
import type {
  AssetAssignment,
  AssetRequisition,
  AssetTransfer,
} from '@/types/hr/assets';

/**
 * Correcting an assignment, a requisition or a transfer.
 *
 * These three were the same hole: each wired create, a lifecycle and a delete, and none wired the
 * edit — so a record raised wrongly could be withdrawn but not fixed.
 *
 * ⚠ **Each has a state rule, and the caller decides whether to render this at all.** An assignment
 * is editable only while `Active` (once the asset is back the terms are history, and editing them
 * rewrites what the holder signed for); a requisition and a transfer only while `Draft` (one out
 * for approval must be recalled first). The service enforces all three; the button is hidden
 * rather than left to fail.
 *
 * ⚠ **Requisition priority goes out as a NUMBER and comes back as a LABEL**, and the numbers run
 * against the reading order — Urgent is 1, Low is 4. It is mapped through
 * `ASSET_REQUISITION_PRIORITIES` in both directions, never cast.
 *
 * Every update writes every field, so each form seeds all of them from the record it was given.
 */

type Kind = 'assignment' | 'requisition' | 'transfer';

const TITLE: Record<Kind, string> = {
  assignment: 'Edit the assignment terms',
  requisition: 'Edit the requisition',
  transfer: 'Edit the transfer',
};

/** `DateOnly` and date inputs both want `yyyy-MM-dd`. */
const toDateInput = (v?: string | null) => {
  if (!v) return '';
  const d = new Date(v);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
};

interface Props {
  kind: Kind;
  /** Narrowed by `kind` inside — see the note on why this is not a discriminated union. */
  record: AssetAssignment | AssetRequisition | AssetTransfer;
  queryKey: unknown[];
}

/**
 * ⚠ `record` is a plain union rather than a discriminated one paired with `kind`. A union of prop
 * OBJECTS turns every use of this component into a multi-overload JSX call, and this TypeScript
 * version crashes on that ("Debug Failure. No error for last overload signature") instead of
 * reporting the underlying error. One interface, narrowed by hand.
 */
export function AssetRecordEditDialog({ kind, record, queryKey }: Props) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState<Record<string, string | boolean>>({});
  const set = (k: string, v: string | boolean) => setForm((f) => ({ ...f, [k]: v }));
  const str = (k: string) => String(form[k] ?? '');
  const orNull = (k: string) => (str(k).trim() === '' ? null : str(k));

  useEffect(() => {
    if (!open) return;
    if (kind === 'assignment') {
      const a = record as AssetAssignment;
      setForm({
        expectedReturnDate: toDateInput(a.expectedReturnDate),
        assignmentNotes: a.assignmentNotes ?? '',
        isPrimaryUser: a.isPrimaryUser,
        responsibleForLoss: a.responsibleForLoss,
        responsibleForDamage: a.responsibleForDamage,
        termsAndConditions: a.termsAndConditions ?? '',
      });
    } else if (kind === 'requisition') {
      const r = record as AssetRequisition;
      setForm({
        assetTypeId: r.assetTypeId,
        beneficiaryEmployeeId: r.beneficiaryEmployeeId ?? '',
        description: r.description ?? '',
        quantity: String(r.quantity ?? 1),
        priority: String(
          ASSET_REQUISITION_PRIORITIES.find((p) => p.label === r.priority)?.value ?? 3,
        ),
        justification: r.justification ?? '',
        requiredByDate: toDateInput(r.requiredByDate),
      });
    } else {
      const t = record as AssetTransfer;
      setForm({
        transferDate: toDateInput(t.transferDate),
        transferReason: t.transferReason ?? '',
        notes: t.notes ?? '',
      });
    }
  }, [open, kind, record]);

  const save = useMutation<void, Error>({
    mutationFn: async () => {
      if (kind === 'assignment') {
        await assetRegisterService.updateAssignment(record.id, {
          id: record.id,
          expectedReturnDate: orNull('expectedReturnDate'),
          assignmentNotes: orNull('assignmentNotes'),
          isPrimaryUser: Boolean(form.isPrimaryUser),
          responsibleForLoss: Boolean(form.responsibleForLoss),
          responsibleForDamage: Boolean(form.responsibleForDamage),
          termsAndConditions: orNull('termsAndConditions'),
        });
        return;
      }
      if (kind === 'requisition') {
        await assetRegisterService.updateRequisition(record.id, {
          assetTypeId: str('assetTypeId'),
          beneficiaryEmployeeId: orNull('beneficiaryEmployeeId'),
          description: str('description'),
          quantity: Number(str('quantity') || 1),
          priority: Number(str('priority')),
          justification: str('justification'),
          requiredByDate: orNull('requiredByDate'),
        });
        return;
      }
      await assetRegisterService.updateTransfer(record.id, {
        id: record.id,
        transferDate: str('transferDate'),
        transferReason: orNull('transferReason'),
        notes: orNull('notes'),
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey });
      queryClient.invalidateQueries({ queryKey: ['hr', 'assets'] });
      setOpen(false);
      toast({ title: 'Saved' });
    },
    onError: (e: Error) =>
      toast({ title: 'The change was refused', description: e.message, variant: 'destructive' }),
  });

  return (
    <>
      <Button variant="outline" onClick={() => setOpen(true)}>
        <Pencil className="mr-2 h-4 w-4" /> Edit
      </Button>

      <Dialog open={open} onOpenChange={(o) => !o && setOpen(false)}>
        <DialogContent className="max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{TITLE[kind]}</DialogTitle>
            <DialogDescription>
              {kind === 'assignment'
                ? 'Only while the assignment is live. Every field is written on save.'
                : 'Only while it is a draft. Every field is written on save.'}
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            {kind === 'assignment' && (
              <>
                <div className="space-y-2">
                  <Label>Expected return date</Label>
                  <Input
                    type="date"
                    value={str('expectedReturnDate')}
                    onChange={(e) => set('expectedReturnDate', e.target.value)}
                  />
                </div>
                <div className="flex items-center justify-between rounded-md border p-3">
                  <Label htmlFor="primary-user">Primary user</Label>
                  <Switch
                    id="primary-user"
                    checked={Boolean(form.isPrimaryUser)}
                    onCheckedChange={(v) => set('isPrimaryUser', v)}
                  />
                </div>
                <div className="flex items-center justify-between rounded-md border p-3">
                  <Label htmlFor="resp-loss">Responsible for loss</Label>
                  <Switch
                    id="resp-loss"
                    checked={Boolean(form.responsibleForLoss)}
                    onCheckedChange={(v) => set('responsibleForLoss', v)}
                  />
                </div>
                <div className="flex items-center justify-between rounded-md border p-3">
                  <Label htmlFor="resp-damage">Responsible for damage</Label>
                  <Switch
                    id="resp-damage"
                    checked={Boolean(form.responsibleForDamage)}
                    onCheckedChange={(v) => set('responsibleForDamage', v)}
                  />
                </div>
                <div className="space-y-2">
                  <Label>Terms and conditions</Label>
                  <Textarea
                    value={str('termsAndConditions')}
                    onChange={(e) => set('termsAndConditions', e.target.value)}
                  />
                </div>
                <div className="space-y-2">
                  <Label>Assignment notes</Label>
                  <Textarea
                    value={str('assignmentNotes')}
                    onChange={(e) => set('assignmentNotes', e.target.value)}
                  />
                </div>
              </>
            )}

            {kind === 'requisition' && (
              <>
                <div className="space-y-2">
                  <Label>Description</Label>
                  <Textarea
                    value={str('description')}
                    onChange={(e) => set('description', e.target.value)}
                  />
                </div>
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-2">
                    <Label>Quantity</Label>
                    <Input
                      type="number"
                      min={1}
                      value={str('quantity')}
                      onChange={(e) => set('quantity', e.target.value)}
                    />
                  </div>
                  <div className="space-y-2">
                    <Label>Priority</Label>
                    <Select value={str('priority')} onValueChange={(v) => set('priority', v)}>
                      <SelectTrigger><SelectValue /></SelectTrigger>
                      <SelectContent>
                        {ASSET_REQUISITION_PRIORITIES.map((p) => (
                          <SelectItem key={p.value} value={String(p.value)}>{p.label}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                </div>
                <div className="space-y-2">
                  <Label>Justification</Label>
                  <Textarea
                    value={str('justification')}
                    onChange={(e) => set('justification', e.target.value)}
                  />
                </div>
                <div className="space-y-2">
                  <Label>Required by</Label>
                  <Input
                    type="date"
                    value={str('requiredByDate')}
                    onChange={(e) => set('requiredByDate', e.target.value)}
                  />
                </div>
              </>
            )}

            {kind === 'transfer' && (
              <>
                <div className="space-y-2">
                  <Label>Transfer date</Label>
                  <Input
                    type="date"
                    value={str('transferDate')}
                    onChange={(e) => set('transferDate', e.target.value)}
                  />
                </div>
                <div className="space-y-2">
                  <Label>Reason</Label>
                  <Textarea
                    value={str('transferReason')}
                    onChange={(e) => set('transferReason', e.target.value)}
                  />
                </div>
                <div className="space-y-2">
                  <Label>Notes</Label>
                  <Textarea value={str('notes')} onChange={(e) => set('notes', e.target.value)} />
                </div>
              </>
            )}
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>Cancel</Button>
            <Button onClick={() => save.mutate()} disabled={save.isPending}>
              {save.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
