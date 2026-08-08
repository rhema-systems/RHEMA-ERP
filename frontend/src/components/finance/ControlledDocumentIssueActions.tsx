'use client';

import { useState } from 'react';
import { CopyPlus, FileDown, Loader2 } from 'lucide-react';
import { useAuth } from '@/hooks/use-auth';
import { documentOutputService } from '@/services/document-output.service';
import type { ControlledDocumentIssueSummary } from '@/types/controlled-documents';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';

interface ControlledDocumentIssueActionsProps {
  documentType: string;
  entityId: string;
  documentLabel: string;
  issuance?: ControlledDocumentIssueSummary;
  onIssued?: () => void | Promise<unknown>;
}

const MINIMUM_REPLACEMENT_REASON_LENGTH = 20;

/**
 * Shared issue/replacement control for posted Finance transaction documents. Browser printing is
 * deliberately not used: every successful action calls the server-side issue command, receives
 * the watermarked PDF, and appends the retained copy/hash/audit record before download.
 */
export function ControlledDocumentIssueActions({
  documentType,
  entityId,
  documentLabel,
  issuance,
  onIssued,
}: ControlledDocumentIssueActionsProps) {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canIssue = hasPermission('Finance.CashBank.Documents.Issue');
  const canReplace = hasPermission('Finance.CashBank.Documents.Reprint');
  const [replacementDialogOpen, setReplacementDialogOpen] = useState(false);
  const [replacementReason, setReplacementReason] = useState('');
  const [busy, setBusy] = useState(false);
  const originalIssued = issuance?.originalIssued ?? false;

  if (!canIssue && !canReplace) return null;

  const issue = async (copyType: 'Original' | 'Replacement') => {
    setBusy(true);
    try {
      await documentOutputService.issueControlledDocument(documentType, entityId, {
        copyType,
        replacementReason: copyType === 'Replacement' ? replacementReason : undefined,
      });
      toast({
        title: `${documentLabel} issued`,
        description: copyType === 'Original'
          ? 'The single controlled original was registered and downloaded.'
          : 'A watermarked replacement was registered with the supplied reason and downloaded.',
      });
      setReplacementDialogOpen(false);
      setReplacementReason('');
      await onIssued?.();
    } catch (error: any) {
      toast({
        title: `Unable to issue ${documentLabel.toLowerCase()}`,
        description: error?.message || 'The controlled document could not be issued.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <div className="flex flex-wrap items-center gap-2">
        {!originalIssued && canIssue && (
          <Button variant="outline" size="sm" disabled={busy} onClick={() => void issue('Original')}>
            {busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <FileDown className="mr-2 h-4 w-4" />}
            Issue Original {documentLabel}
          </Button>
        )}
        {originalIssued && canReplace && (
          <Button variant="outline" size="sm" disabled={busy} onClick={() => setReplacementDialogOpen(true)}>
            <CopyPlus className="mr-2 h-4 w-4" /> Replacement Copy
          </Button>
        )}
        {originalIssued && (
          <span className="text-xs text-muted-foreground">
            Original issued{issuance?.originalIssuedByName ? ` by ${issuance.originalIssuedByName}` : ''}
            {issuance?.replacementCount ? ` · ${issuance.replacementCount} replacement(s)` : ''}
          </span>
        )}
      </div>

      <Dialog open={replacementDialogOpen} onOpenChange={setReplacementDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Issue replacement {documentLabel.toLowerCase()}</DialogTitle>
            <DialogDescription>
              The PDF will be visibly watermarked with its copy number. The reason, issuing user,
              timestamp, and exact PDF hash will be retained in the Finance audit trail.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2 py-2">
            <Label htmlFor={`replacement-reason-${entityId}`}>Replacement reason</Label>
            <Textarea
              id={`replacement-reason-${entityId}`}
              rows={5}
              value={replacementReason}
              onChange={(event) => setReplacementReason(event.target.value)}
              placeholder="Explain why another copy is required and who requested it."
            />
            <p className="text-xs text-muted-foreground">
              At least {MINIMUM_REPLACEMENT_REASON_LENGTH} characters are required.
            </p>
          </div>
          <DialogFooter>
            <Button variant="outline" disabled={busy} onClick={() => setReplacementDialogOpen(false)}>
              Cancel
            </Button>
            <Button
              disabled={busy || replacementReason.trim().length < MINIMUM_REPLACEMENT_REASON_LENGTH}
              onClick={() => void issue('Replacement')}
            >
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Issue Watermarked Replacement
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
