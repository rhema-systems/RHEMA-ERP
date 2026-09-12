'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Pencil, Trash2 } from 'lucide-react';
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
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ASSET_MAINTENANCE_STATUSES, ASSET_MAINTENANCE_TYPES } from '@/types/hr/assets';
import { assetRegisterService } from '@/services/hr/asset-register.service';
import { useToast } from '@/hooks/use-toast';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtNum = (v?: number | null) =>
  v === null || v === undefined ? '—' : v.toLocaleString(undefined, { maximumFractionDigits: 2 });

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm">{children}</dd>
    </div>
  );
}

/**
 * One service record, in full.
 *
 * ⚠ **Everything below `description` could be written and never read back.** The service log rows
 * carry the summary DTO — number, date, type, status, cost — while `workPerformed`,
 * `partsReplaced`, the external provider, the ticket number and the notes live only on the by-id
 * read, and no screen loaded it. So a job could be completed with a careful account of what was
 * done to the asset, and that account was visible nowhere in the product.
 *
 * The same shape as D-ll, and the reason the rule is *assert that a by-id read and its list read
 * agree*: a record that holds more than any screen shows is a record nobody can act on.
 */
export function MaintenanceRecordDialog({
  maintenanceId,
  onClose,
  onDeleted,
}: {
  maintenanceId: string | null;
  onClose: () => void;
  onDeleted?: () => void;
}) {
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const { data: m, isLoading } = useQuery({
    queryKey: ['hr', 'assets', 'maintenance', maintenanceId],
    queryFn: () => assetRegisterService.getMaintenance(maintenanceId as string),
    enabled: Boolean(maintenanceId),
  });

  /**
   * The edit. Everything the by-id read carries and the list read does not — `workPerformed`,
   * `partsReplaced`, the provider, the ticket, the notes — was writable at create and then
   * uncorrectable, which is why this dialog is where it belongs.
   *
   * ⚠ `type` and `status` go out as NUMBERS and come back as LABELS. Mapped through the
   * constants in both directions, never cast.
   */
  const [editing, setEditing] = useState(false);
  const [form, setForm] = useState<Record<string, string | boolean>>({});
  const set = (k: string, v: string | boolean) => setForm((f) => ({ ...f, [k]: v }));
  const str = (k: string) => String(form[k] ?? '');
  const orNull = (k: string) => (str(k).trim() === '' ? null : str(k));
  const toDateInput = (v?: string | null) => (v ? new Date(v).toISOString().slice(0, 10) : '');

  useEffect(() => {
    if (!editing || !m) return;
    setForm({
      maintenanceDate: toDateInput(m.maintenanceDate),
      type: String(ASSET_MAINTENANCE_TYPES.find((t) => t.label === m.type)?.value ?? 1),
      description: m.description ?? '',
      workPerformed: m.workPerformed ?? '',
      partsReplaced: m.partsReplaced ?? '',
      isInternalMaintenance: m.isInternalMaintenance,
      externalServiceProvider: m.externalServiceProvider ?? '',
      serviceTicketNumber: m.serviceTicketNumber ?? '',
      cost: m.cost === null || m.cost === undefined ? '' : String(m.cost),
      nextMaintenanceDate: toDateInput(m.nextMaintenanceDate),
      status: String(ASSET_MAINTENANCE_STATUSES.find((s) => s.label === m.status)?.value ?? 1),
      notes: m.notes ?? '',
    });
  }, [editing, m]);

  // Closing the dialog must not leave the next record opening in edit mode with stale values.
  useEffect(() => {
    if (maintenanceId === null) setEditing(false);
  }, [maintenanceId]);

  const save = useMutation({
    mutationFn: () =>
      assetRegisterService.updateMaintenance(maintenanceId as string, {
        id: maintenanceId as string,
        maintenanceDate: str('maintenanceDate'),
        type: Number(str('type')),
        description: str('description'),
        workPerformed: orNull('workPerformed'),
        partsReplaced: orNull('partsReplaced'),
        isInternalMaintenance: Boolean(form.isInternalMaintenance),
        // Carried through rather than shown: repointing the in-house engineer is a different job
        // from correcting the account of the work, and needs an employee picker.
        performedById: m?.performedById ?? null,
        externalServiceProvider: orNull('externalServiceProvider'),
        serviceTicketNumber: orNull('serviceTicketNumber'),
        cost: str('cost').trim() === '' ? null : Number(str('cost')),
        nextMaintenanceDate: orNull('nextMaintenanceDate'),
        status: Number(str('status')),
        notes: orNull('notes'),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hr', 'assets'] });
      setEditing(false);
      toast({ title: 'Service record updated' });
    },
    onError: (e: Error) =>
      toast({ title: 'Could not save it', description: e.message, variant: 'destructive' }),
  });

  const remove = useMutation({
    mutationFn: () => assetRegisterService.deleteMaintenance(maintenanceId as string),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hr', 'assets'] });
      toast({ title: 'Service record removed' });
      onDeleted?.();
      onClose();
    },
    onError: (e: Error) =>
      toast({ title: 'Could not remove it', description: e.message, variant: 'destructive' }),
  });

  return (
    <Dialog open={maintenanceId !== null} onOpenChange={(o) => { if (!o) onClose(); }}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{m?.maintenanceNumber ?? 'Service record'}</DialogTitle>
          <DialogDescription>
            {m ? `${m.assetName} (${m.assetNumber})` : 'Loading…'}
          </DialogDescription>
        </DialogHeader>

        {isLoading || !m ? (
          <div className="flex justify-center p-8">
            <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
          </div>
        ) : editing ? (
          <div className="max-h-[60vh] space-y-4 overflow-y-auto pr-1">
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-2">
                <Label>Dated</Label>
                <Input
                  type="date"
                  value={str('maintenanceDate')}
                  onChange={(e) => set('maintenanceDate', e.target.value)}
                />
              </div>
              <div className="space-y-2">
                <Label>Cost</Label>
                <Input
                  type="number"
                  step="0.01"
                  value={str('cost')}
                  onChange={(e) => set('cost', e.target.value)}
                />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-2">
                <Label>Type</Label>
                <Select value={str('type')} onValueChange={(v) => set('type', v)}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {ASSET_MAINTENANCE_TYPES.map((t) => (
                      <SelectItem key={t.value} value={String(t.value)}>{t.text}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Status</Label>
                <Select value={str('status')} onValueChange={(v) => set('status', v)}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {ASSET_MAINTENANCE_STATUSES.map((t) => (
                      <SelectItem key={t.value} value={String(t.value)}>{t.text}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="space-y-2">
              <Label>Description</Label>
              <Textarea
                value={str('description')}
                onChange={(e) => set('description', e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label>Work performed</Label>
              <Textarea
                value={str('workPerformed')}
                onChange={(e) => set('workPerformed', e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label>Parts replaced</Label>
              <Textarea
                value={str('partsReplaced')}
                onChange={(e) => set('partsReplaced', e.target.value)}
              />
            </div>
            <div className="flex items-center justify-between rounded-md border p-3">
              <Label htmlFor="internal-maintenance">Done in-house</Label>
              <Switch
                id="internal-maintenance"
                checked={Boolean(form.isInternalMaintenance)}
                onCheckedChange={(v) => set('isInternalMaintenance', v)}
              />
            </div>
            {!form.isInternalMaintenance && (
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-2">
                  <Label>External provider</Label>
                  <Input
                    value={str('externalServiceProvider')}
                    onChange={(e) => set('externalServiceProvider', e.target.value)}
                  />
                </div>
                <div className="space-y-2">
                  <Label>Service ticket</Label>
                  <Input
                    value={str('serviceTicketNumber')}
                    onChange={(e) => set('serviceTicketNumber', e.target.value)}
                  />
                </div>
              </div>
            )}
            <div className="space-y-2">
              <Label>Next due after this</Label>
              <Input
                type="date"
                value={str('nextMaintenanceDate')}
                onChange={(e) => set('nextMaintenanceDate', e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label>Notes</Label>
              <Textarea value={str('notes')} onChange={(e) => set('notes', e.target.value)} />
            </div>
          </div>
        ) : (
          <div className="space-y-4">
            <div className="flex flex-wrap items-center gap-3">
              <StatusBadge status={m.statusName} />
              <span className="text-sm text-muted-foreground">{m.typeName}</span>
              {m.isAtWorkshop && (
                <span className="text-sm text-amber-600 dark:text-amber-500">
                  At the workshop{m.maintenanceAdmissionNumber ? ` on ${m.maintenanceAdmissionNumber}` : ''}
                </span>
              )}
            </div>

            <dl className="grid gap-4 sm:grid-cols-3">
              <Field label="Dated">{fmtDate(m.maintenanceDate)}</Field>
              <Field label="Cost">{fmtNum(m.cost)}</Field>
              <Field label="Next due after this">{fmtDate(m.nextMaintenanceDate)}</Field>
              <Field label="Done by">
                {m.isInternalMaintenance
                  ? m.performedByName ?? 'In-house'
                  : m.externalServiceProvider ?? 'An external provider'}
              </Field>
              <Field label="Service ticket">{m.serviceTicketNumber ?? '—'}</Field>
              <Field label="Workshop admission">{m.maintenanceAdmissionNumber ?? '—'}</Field>

              <div className="sm:col-span-3">
                <Field label="What it was for">{m.description}</Field>
              </div>
              <div className="sm:col-span-3">
                <Field label="What was done">
                  {m.workPerformed ?? <span className="text-muted-foreground">Not recorded</span>}
                </Field>
              </div>
              <div className="sm:col-span-3">
                <Field label="Parts replaced">
                  {m.partsReplaced ?? <span className="text-muted-foreground">None recorded</span>}
                </Field>
              </div>
              {m.notes && (
                <div className="sm:col-span-3"><Field label="Notes">{m.notes}</Field></div>
              )}
            </dl>
          </div>
        )}

        <DialogFooter>
          {/* ⚠ A soft delete, and it does NOT undo what completing the job did to the asset's
              maintenance dates. It removes the record of the work, not the work. */}
          <Button
            variant="ghost"
            onClick={() => remove.mutate()}
            disabled={remove.isPending || !m}
          >
            {remove.isPending
              ? <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              : <Trash2 className="mr-2 h-4 w-4" />}
            Remove the record
          </Button>
          {editing ? (
            <>
              <Button variant="outline" onClick={() => setEditing(false)}>Cancel</Button>
              <Button onClick={() => save.mutate()} disabled={save.isPending || !m}>
                {save.isPending ? 'Saving…' : 'Save'}
              </Button>
            </>
          ) : (
            <>
              <Button variant="outline" onClick={() => setEditing(true)} disabled={!m}>
                <Pencil className="mr-2 h-4 w-4" /> Edit
              </Button>
              <Button variant="outline" onClick={onClose}>Close</Button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
