'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
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
          <Button variant="outline" onClick={onClose}>Close</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
