'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { useToast } from '@/hooks/use-toast';
import { probationService } from '@/services/hr/probation.service';
import type { ProbationConfirmationRepairResult } from '@/types/hr/probation';

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/**
 * Confirms, by rule, the employees whose probation ended before they were entered (HR finish plan
 * lane 11). The imported workforce arrived with no confirmation date, so the hire path put every
 * permanent employee on a probation that had ended years before.
 *
 * ⚠ It opens on a DRY RUN and writes nothing until the administrator has read what would change and
 * pressed the button — the same shape as the leave module's entitlement repair. It changes hundreds
 * of records at once.
 */
export function ImportedConfirmationRepairDialog({ open, onOpenChange }: Props) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [preview, setPreview] = useState<ProbationConfirmationRepairResult | null>(null);
  const [applied, setApplied] = useState<ProbationConfirmationRepairResult | null>(null);

  const failed = (e: any) =>
    toast({
      variant: 'destructive',
      title: 'It could not run',
      description: e?.body?.detail ?? e?.body?.message ?? e?.message,
    });

  const dryRun = useMutation({
    mutationFn: () => probationService.repairImportedConfirmations(true),
    onSuccess: setPreview,
    onError: failed,
  });

  const apply = useMutation({
    mutationFn: () => probationService.repairImportedConfirmations(false),
    onSuccess: (result) => {
      setApplied(result);
      queryClient.invalidateQueries({ queryKey: ['probations'] });
    },
    onError: failed,
  });

  // Every opening starts from a fresh dry run: the figures move as HR works the register.
  useEffect(() => {
    if (!open) return;
    setPreview(null);
    setApplied(null);
    dryRun.mutate();
  }, [open]);

  const shown = applied ?? preview;
  const heldBackLines = shown?.notes.slice(1) ?? [];

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-xl">
        <DialogHeader>
          <DialogTitle>Confirm staff whose probation ended before they were entered</DialogTitle>
          <DialogDescription>
            Each is confirmed on the day their probation term ended — the hire date plus the term — and the
            date is marked as worked out, not supplied. Their probation record is closed. Nobody is sent a
            letter or a notification.
          </DialogDescription>
        </DialogHeader>

        {!shown ? (
          <div className="flex items-center gap-2 py-6 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" /> Working out who this applies to…
          </div>
        ) : (
          <div className="space-y-3 text-sm">
            <p className="font-medium">{shown.notes[0]}</p>
            <table className="w-full">
              <tbody>
                <Row label="On probation with no confirmation date" value={shown.examined} />
                <Row
                  label={applied ? 'Confirmed' : 'Will be confirmed'}
                  value={shown.confirmed}
                  strong
                />
                <Row label="No hire date — left until one is supplied" value={shown.noHireDate} />
                <Row label="Term not ended when entered — left on probation" value={shown.stillOnProbation} />
                <Row label="Held back — somebody has acted on their probation" value={shown.heldBack} />
              </tbody>
            </table>
            {heldBackLines.length > 0 && (
              <div className="max-h-40 overflow-y-auto rounded-md border p-2 text-xs text-muted-foreground">
                {heldBackLines.map((line) => (
                  <div key={line}>{line}</div>
                ))}
              </div>
            )}
          </div>
        )}

        <DialogFooter>
          {applied ? (
            <Button onClick={() => onOpenChange(false)}>Close</Button>
          ) : (
            <>
              <Button variant="outline" onClick={() => onOpenChange(false)}>
                Cancel
              </Button>
              <Button
                disabled={!preview || preview.confirmed === 0 || apply.isPending}
                onClick={() => apply.mutate()}
              >
                {apply.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Confirm {preview?.confirmed ?? ''} employee{preview?.confirmed === 1 ? '' : 's'}
              </Button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function Row({ label, value, strong }: { label: string; value: number; strong?: boolean }) {
  return (
    <tr className={strong ? 'font-semibold' : undefined}>
      <td className="py-1 pr-4">{label}</td>
      <td className="py-1 text-right tabular-nums">{value.toLocaleString()}</td>
    </tr>
  );
}
