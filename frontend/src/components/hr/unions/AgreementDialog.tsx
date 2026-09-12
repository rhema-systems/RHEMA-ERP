'use client';

import { useEffect, useState } from 'react';
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
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import { unionService } from '@/services/hr/union.service';
import type { CollectiveBargainingAgreement } from '@/types/hr/union';

interface AgreementDialogProps {
  unionId: string;
  unionName: string;
  /** Null to add a new agreement; an agreement to edit that one. */
  agreement: CollectiveBargainingAgreement | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: () => Promise<unknown> | void;
}

/** `"2026-01-01T00:00:00"` → `"2026-01-01"` for an `<input type="date">`. */
const toDateInput = (value?: string | null) => (value ? value.slice(0, 10) : '');

/**
 * Adds or edits one collective bargaining agreement.
 *
 * ⚠ The server refuses an expiry earlier than the effective date with a 400 and a sentence, and the
 * toast shows that sentence. This form checks the same thing first — not to replace the rule, but
 * because being told before you press Save is better than after.
 *
 * ⚠ Leaving the expiry blank means open-ended, not "unknown". An open-ended agreement never reaches
 * the `Expired` status, which is the honest reading: nothing has ended it.
 */
export function AgreementDialog({
  unionId,
  unionName,
  agreement,
  open,
  onOpenChange,
  onSaved,
}: AgreementDialogProps) {
  const { toast } = useToast();
  const [saving, setSaving] = useState(false);
  const [title, setTitle] = useState('');
  const [referenceNumber, setReferenceNumber] = useState('');
  const [effectiveDate, setEffectiveDate] = useState('');
  const [expiryDate, setExpiryDate] = useState('');
  const [summary, setSummary] = useState('');
  const [documentReference, setDocumentReference] = useState('');
  const [isActive, setIsActive] = useState(true);

  useEffect(() => {
    if (!open) return;
    setTitle(agreement?.title ?? '');
    setReferenceNumber(agreement?.referenceNumber ?? '');
    setEffectiveDate(toDateInput(agreement?.effectiveDate));
    setExpiryDate(toDateInput(agreement?.expiryDate));
    setSummary(agreement?.summary ?? '');
    setDocumentReference(agreement?.documentReference ?? '');
    setIsActive(agreement?.isActive ?? true);
  }, [open, agreement]);

  const datesInverted = Boolean(effectiveDate && expiryDate && expiryDate < effectiveDate);
  const canSave = Boolean(title.trim()) && Boolean(effectiveDate) && !datesInverted && !saving;

  const handleSave = async () => {
    setSaving(true);
    try {
      const body = {
        referenceNumber: referenceNumber.trim() || null,
        title: title.trim(),
        effectiveDate: `${effectiveDate}T00:00:00`,
        expiryDate: expiryDate ? `${expiryDate}T00:00:00` : null,
        summary: summary.trim() || null,
        documentReference: documentReference.trim() || null,
        isActive,
      };

      if (agreement) {
        await unionService.updateAgreement(agreement.id, { id: agreement.id, ...body });
      } else {
        await unionService.addAgreement(unionId, { unionId, ...body });
      }

      await onSaved();
      toast({
        title: agreement ? 'Agreement updated' : 'Agreement added',
        description: `“${body.title}” was saved against ${unionName}.`,
      });
      onOpenChange(false);
    } catch (error) {
      toast({
        title: agreement ? 'Could not update the agreement' : 'Could not add the agreement',
        description: (error as Error)?.message || 'The server refused the change.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[560px]">
        <DialogHeader>
          <DialogTitle>{agreement ? 'Edit agreement' : 'New agreement'}</DialogTitle>
          <DialogDescription>
            A collective bargaining agreement negotiated with {unionName}.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="ag-title">Title</Label>
            <Input
              id="ag-title"
              placeholder="Main collective agreement 2026–2028"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              maxLength={200}
            />
          </div>

          <div className="grid grid-cols-3 gap-4">
            <div className="space-y-2">
              <Label htmlFor="ag-ref">Reference</Label>
              <Input
                id="ag-ref"
                placeholder="CBA/2026/01"
                value={referenceNumber}
                onChange={(e) => setReferenceNumber(e.target.value)}
                maxLength={50}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="ag-from">Effective from</Label>
              <Input
                id="ag-from"
                type="date"
                value={effectiveDate}
                onChange={(e) => setEffectiveDate(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="ag-to">Expires</Label>
              <Input
                id="ag-to"
                type="date"
                value={expiryDate}
                onChange={(e) => setExpiryDate(e.target.value)}
              />
              <p className="text-xs text-muted-foreground">Blank = open-ended.</p>
            </div>
          </div>

          {datesInverted && (
            <p className="text-sm text-destructive">
              The expiry date cannot be earlier than the effective date.
            </p>
          )}

          <div className="space-y-2">
            <Label htmlFor="ag-summary">Summary</Label>
            <Textarea
              id="ag-summary"
              placeholder="What the agreement covers — pay, hours, grievance procedure…"
              rows={3}
              value={summary}
              onChange={(e) => setSummary(e.target.value)}
              maxLength={2000}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="ag-doc">Document reference</Label>
            <Input
              id="ag-doc"
              placeholder="Where the signed copy is filed"
              value={documentReference}
              onChange={(e) => setDocumentReference(e.target.value)}
              maxLength={500}
            />
          </div>

          <div className="flex items-center justify-between rounded-md border p-4">
            <div className="space-y-0.5">
              <Label htmlFor="ag-active">Active</Label>
              <p className="text-xs text-muted-foreground">
                Switch off to shelve an agreement regardless of its dates. Expiry is handled by the
                dates — this is not the way to retire a lapsed agreement.
              </p>
            </div>
            <Switch id="ag-active" checked={isActive} onCheckedChange={setIsActive} />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={handleSave} disabled={!canSave}>
            {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {agreement ? 'Save changes' : 'Add agreement'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
