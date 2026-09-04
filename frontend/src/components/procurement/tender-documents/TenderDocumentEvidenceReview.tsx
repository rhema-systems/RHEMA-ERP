'use client';

import React, { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, RefreshCw } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';
import { workflowApiService } from '@/services/workflow-api.service';
import type {
  WorkflowEvidenceDocumentDto,
  WorkflowEvidenceReviewInstanceDto,
} from '@/types/workflow';
import { isTenderDocumentContentArtifactEligible } from './TenderDocumentContentArtifactField';

type EvidenceRow = WorkflowEvidenceDocumentDto & { stepInstanceId: string };

export function isTemplateWorkflowCompleted(
  status: number | string | undefined
): boolean {
  return status === 2 || status === '2' || status === 'Completed';
}

const scanLabel = (status: number) =>
  ['Scan pending', 'Scan clean', 'Unsafe file', 'Scan failed'][status] ??
  'Scan status unavailable';

const reviewLabel = (status: number) =>
  ['Awaiting review', 'Verified', 'Rejected'][status] ??
  'Review status unavailable';

interface Props {
  workflow: WorkflowEvidenceReviewInstanceDto;
  attachedEvidenceId?: string;
  eligibleContentEvidenceDocumentIds?: string[];
  disabled?: boolean;
  onUpdated: () => Promise<unknown>;
}

/** Uses the exact workflow and server eligibility; never infers a reviewer role. */
export function TenderDocumentEvidenceReview({
  workflow,
  attachedEvidenceId,
  eligibleContentEvidenceDocumentIds,
  disabled = false,
  onUpdated,
}: Props) {
  const queryClient = useQueryClient();
  const [pending, setPending] = useState<{
    item: EvidenceRow;
    accepted: boolean;
  } | null>(null);
  const [notes, setNotes] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [downloading, setDownloading] = useState<string | null>(null);
  const stepIds = [
    ...new Set(workflow.steps.map((step) => step.stepInstanceId)),
  ];
  const evidence = useQuery({
    queryKey: ['tender-document-evidence-review', workflow.id, stepIds],
    queryFn: async () => {
      const rows = await Promise.all(
        stepIds.map(async (stepInstanceId) =>
          (
            await workflowApiService.getWorkflowStepEvidence(stepInstanceId)
          ).map((item) => ({ ...item, stepInstanceId }))
        )
      );
      return rows.flat().filter((item) => item.isCurrent);
    },
  });
  const files = evidence.isError ? [] : (evidence.data ?? []);
  const approvedFile = files.some((item) =>
    isTenderDocumentContentArtifactEligible(
      item,
      eligibleContentEvidenceDocumentIds
    )
  );
  const workflowCompleted = isTemplateWorkflowCompleted(workflow.status);
  const actionDisabled =
    disabled || busy || evidence.isFetching || evidence.isError;

  const review = async () => {
    if (!pending || pending.item.canVerify !== true) return false;
    if (!pending.accepted && !notes.trim()) {
      setError('A reason is required when rejecting a document.');
      return false;
    }
    setBusy(true);
    setError('');
    try {
      await workflowApiService.verifyWorkflowEvidence(
        pending.item.id,
        pending.accepted,
        notes.trim() || undefined
      );
    } catch (failure) {
      setError(
        getProcurementProblemMessage(failure, 'Document review failed.')
      );
      setBusy(false);
      return false;
    }
    toast.success(pending.accepted ? 'Document verified' : 'Document rejected');
    setPending(null);
    setNotes('');
    try {
      await Promise.all([
        evidence.refetch(),
        queryClient.invalidateQueries({
          queryKey: ['procurement-tender-document-content-artifacts'],
        }),
        onUpdated(),
      ]);
    } catch {
      toast.error(
        'Review saved. Refresh this page to reload document readiness.'
      );
    } finally {
      setBusy(false);
    }
    return true;
  };

  const download = async (item: EvidenceRow) => {
    setDownloading(item.id);
    try {
      const blob = await workflowApiService.downloadStepAttachment(
        item.stepInstanceId,
        item.attachmentId
      );
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = item.fileName.replace(/[\\/:*?"<>|]/g, '_');
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (failure) {
      toast.error(
        getProcurementProblemMessage(failure, 'Document download failed.')
      );
    } finally {
      setDownloading(null);
    }
  };

  return (
    <section
      className="mb-4 space-y-3 rounded-md border p-3"
      aria-label="Document readiness"
    >
      <div className="flex items-center justify-between gap-2">
        <h3 className="text-sm font-medium">Document readiness</h3>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          disabled={busy || evidence.isFetching}
          onClick={() =>
            void Promise.all([evidence.refetch(), onUpdated()]).catch(() =>
              toast.error(
                'Document readiness could not be refreshed. Try the page refresh.'
              )
            )
          }
          aria-label="Refresh document readiness"
        >
          <RefreshCw className="h-4 w-4" />
        </Button>
      </div>
      <div
        className="flex flex-wrap gap-2 text-xs"
        aria-label="Known prerequisites"
      >
        <Badge variant={files.length ? 'default' : 'outline'}>
          {evidence.isError
            ? 'Upload status unavailable'
            : evidence.isLoading
              ? 'Checking file'
              : files.length
                ? 'File uploaded'
                : 'Upload needed'}
        </Badge>
        <Badge variant={approvedFile ? 'default' : 'outline'}>
          {evidence.isError
            ? 'File review unavailable'
            : approvedFile
              ? 'File ready for attachment'
              : 'File review needed'}
        </Badge>
        <Badge variant={workflowCompleted ? 'default' : 'outline'}>
          {workflowCompleted
            ? 'Workflow completed'
            : 'Workflow approval outstanding'}
        </Badge>
        <Badge variant={attachedEvidenceId ? 'default' : 'outline'}>
          {attachedEvidenceId ? 'Content attached' : 'Attach eligible content'}
        </Badge>
      </div>
      <p className="text-xs text-muted-foreground">
        Complete any required file review here, then attach it below. These are
        known prerequisites; publication also checks the configured policy,
        effective dates and approvals.
      </p>
      {evidence.isError && (
        <p role="alert" className="text-sm text-destructive">
          {getProcurementProblemMessage(
            evidence.error,
            'Document review could not be loaded. Refresh to retry.'
          )}
        </p>
      )}
      {!evidence.isError &&
        files.map((item) => (
          <div key={item.id} className="space-y-2 border-t pt-3">
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div className="min-w-0">
                <p className="break-words text-sm font-medium">
                  {item.fileName} · v{item.version}
                </p>
                <p className="text-xs text-muted-foreground">
                  {scanLabel(item.malwareScanStatus)} ·{' '}
                  {isTenderDocumentContentArtifactEligible(
                    item,
                    eligibleContentEvidenceDocumentIds
                  ) && item.verificationStatus !== 1
                    ? 'Eligible under configured policy'
                    : reviewLabel(item.verificationStatus)}
                </p>
              </div>
              <div className="flex flex-wrap gap-2">
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  disabled={
                    actionDisabled ||
                    downloading !== null ||
                    item.malwareScanStatus !== 1
                  }
                  onClick={() => void download(item)}
                  aria-label={`Download ${item.fileName}`}
                >
                  <Download className="mr-1 h-3 w-3" /> Download
                </Button>
                {item.canVerify === true &&
                  item.verificationStatus !== 1 &&
                  !isTenderDocumentContentArtifactEligible(
                    item,
                    eligibleContentEvidenceDocumentIds
                  ) && (
                    <>
                      <Button
                        type="button"
                        size="sm"
                        disabled={
                          actionDisabled || item.malwareScanStatus !== 1
                        }
                        onClick={() => {
                          setPending({ item, accepted: true });
                          setNotes('');
                          setError('');
                        }}
                      >
                        Verify file
                      </Button>
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        disabled={actionDisabled}
                        onClick={() => {
                          setPending({ item, accepted: false });
                          setNotes('');
                          setError('');
                        }}
                      >
                        Reject file
                      </Button>
                    </>
                  )}
              </div>
            </div>
            {item.verificationStatus !== 1 &&
              item.canVerify !== true &&
              !isTenderDocumentContentArtifactEligible(
                item,
                eligibleContentEvidenceDocumentIds
              ) && (
                <p className="text-xs text-muted-foreground">
                  {item.verificationBlockedReason ||
                    'An authorized reviewer assigned by this workflow must verify the file.'}
                </p>
              )}
            {item.verificationNotes && (
              <p className="text-xs text-muted-foreground">
                Review notes: {item.verificationNotes}
              </p>
            )}
          </div>
        ))}
      <ConfirmationDialog
        open={pending !== null}
        onOpenChange={(open) => {
          if (!open && !busy) setPending(null);
        }}
        title={
          pending?.accepted
            ? 'Verify tender document'
            : 'Reject tender document'
        }
        description={`Review ${pending?.item.fileName ?? 'the file'}. Your decision is recorded against this exact version.`}
        confirmText={
          pending?.accepted ? 'Confirm verification' : 'Confirm rejection'
        }
        variant={pending?.accepted ? 'default' : 'destructive'}
        onConfirm={review}
        isLoading={busy}
        confirmDisabled={
          pending?.item.canVerify !== true ||
          (!pending?.accepted && !notes.trim())
        }
      >
        <div className="space-y-2">
          <Label htmlFor="template-file-review-notes">
            {pending?.accepted ? 'Review notes (optional)' : 'Rejection reason'}
          </Label>
          <Textarea
            id="template-file-review-notes"
            value={notes}
            onChange={(event) => setNotes(event.target.value)}
            disabled={busy}
          />
          {error && (
            <p role="alert" className="text-sm text-destructive">
              {error}
            </p>
          )}
        </div>
      </ConfirmationDialog>
    </section>
  );
}
