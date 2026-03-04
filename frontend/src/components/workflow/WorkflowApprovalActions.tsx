'use client';

import * as React from 'react';
import { CheckCircle, Send, XCircle } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { WorkflowApprovalCommentDialog, WorkflowApprovalDialogMode } from '@/components/workflow/WorkflowApprovalCommentDialog';
import { workflowApiService } from '@/services/workflow-api.service';
import type { WorkflowEntitySummaryDto, WorkflowPendingApproverDto } from '@/types/workflow';

export interface WorkflowApprovalActionsProps {
  entityType: string;
  entityId: string;

  entityLabel: string; // e.g. "Purchase Requisition", "Purchase Order"
  entityNumber?: string; // e.g. PR-2026-0002
  status: string;

  // Optional: show "Step: <name>" badge (if you pass currentStepName or allow summary fetch)
  showStepBadge?: boolean;
  currentStepName?: string;

  // Optional: externally-provided summary (preferred for list/grid usage with batch endpoint)
  workflowSummary?: WorkflowEntitySummaryDto;

  // If true, fetches workflow entity summary (single-entity usage, recommended for detail pages)
  loadWorkflowSummary?: boolean;

  // Action enablement (defaults based on status if omitted)
  canSubmit?: boolean;
  canApproveReject?: boolean;

  // Handlers
  onSubmit?: () => Promise<void>;
  onApprove?: (comments: string) => Promise<void>;
  onReject?: (comments: string) => Promise<void>;
  onAfterAction?: () => Promise<void>;
  onOpenWorkflows?: () => void;

  // UI tuning
  size?: 'sm' | 'default' | 'lg' | 'icon';
  iconOnly?: boolean;
  className?: string;
}

export function WorkflowApprovalActions({
  entityType,
  entityId,
  entityLabel,
  entityNumber,
  status,
  showStepBadge = false,
  currentStepName,
  workflowSummary,
  loadWorkflowSummary = false,
  canSubmit,
  canApproveReject,
  onSubmit,
  onApprove,
  onReject,
  onAfterAction,
  onOpenWorkflows,
  size = 'sm',
  iconOnly = false,
  className,
}: WorkflowApprovalActionsProps) {
  const formatPendingApprovers = (pending: WorkflowPendingApproverDto[], maxNames = 2) => {
    const names = (pending || [])
      .map((p) => (p.approverName || p.approverRole || '').trim())
      .filter(Boolean);

    const full = names.join(', ');
    if (names.length <= maxNames) {
      return { short: full, full };
    }

    return {
      short: `${names.slice(0, maxNames).join(', ')} +${names.length - maxNames}`,
      full,
    };
  };

  const [submitOpen, setSubmitOpen] = React.useState(false);
  const [approvalOpen, setApprovalOpen] = React.useState(false);
  const [approvalMode, setApprovalMode] = React.useState<WorkflowApprovalDialogMode>('approve');
  const [submitting, setSubmitting] = React.useState(false);
  const [processing, setProcessing] = React.useState(false);

  const [summaryLoading, setSummaryLoading] = React.useState(false);
  const [summaryStepName, setSummaryStepName] = React.useState<string | undefined>(undefined);
  const [canCurrentUserApprove, setCanCurrentUserApprove] = React.useState<boolean | undefined>(undefined);
  const [summaryPendingApprovers, setSummaryPendingApprovers] = React.useState<WorkflowPendingApproverDto[]>([]);

  const effectiveStepName = currentStepName || workflowSummary?.currentStepName || summaryStepName;
  const effectivePendingApprovers = workflowSummary?.pendingApprovers ?? summaryPendingApprovers;
  const pendingApproversText = formatPendingApprovers(effectivePendingApprovers);

  const defaultCanSubmit = status === 'Draft';
  const defaultCanApproveReject = status === 'Pending Approval' || status === 'Submitted';

  const submitEnabled = (canSubmit ?? defaultCanSubmit) && !!onSubmit;
  const approveRejectEnabledByStatus = (canApproveReject ?? defaultCanApproveReject) && !!onApprove && !!onReject;

  const effectiveCanApproveFlag =
    workflowSummary?.canCurrentUserApprove ?? canCurrentUserApprove;

  const effectiveCanApprove =
    effectiveCanApproveFlag === undefined ? approveRejectEnabledByStatus : approveRejectEnabledByStatus && effectiveCanApproveFlag;

  React.useEffect(() => {
    if (!loadWorkflowSummary) return;
    if (workflowSummary) return; // parent is providing it
    if (!entityType || !entityId) return;

    let mounted = true;
    (async () => {
      try {
        setSummaryLoading(true);
        const s = await workflowApiService.getWorkflowEntitySummary(entityType, entityId);
        if (!mounted) return;
        setSummaryStepName(s.currentStepName || undefined);
        setCanCurrentUserApprove(!!s.canCurrentUserApprove);
        setSummaryPendingApprovers(s.pendingApprovers || []);
      } catch (e: any) {
        // Summary is best-effort; actions still work and will show API error if forbidden.
        if (!mounted) return;
        setCanCurrentUserApprove(undefined);
      } finally {
        if (mounted) setSummaryLoading(false);
      }
    })();

    return () => {
      mounted = false;
    };
  }, [loadWorkflowSummary, workflowSummary, entityType, entityId]);

  const runAfter = async () => {
    if (loadWorkflowSummary && !workflowSummary) {
      // refresh summary after an action
      try {
        const s = await workflowApiService.getWorkflowEntitySummary(entityType, entityId);
        setSummaryStepName(s.currentStepName || undefined);
        setCanCurrentUserApprove(!!s.canCurrentUserApprove);
        setSummaryPendingApprovers(s.pendingApprovers || []);
      } catch {
        // ignore
      }
    }
    if (onAfterAction) await onAfterAction();
  };

  const confirmSubmit = async () => {
    if (!onSubmit) return false;
    try {
      setSubmitting(true);
      await onSubmit();
      toast.success(`${entityLabel} submitted for approval`, {
        description: entityNumber ? `${entityNumber} has been submitted.` : undefined,
      });
      await runAfter();
      return true;
    } catch (e: any) {
      const message = e?.message || '';
      if (
        onOpenWorkflows &&
        typeof message === 'string' &&
        (message.includes('No approval workflow is active') ||
          message.includes('No active workflow definition found') ||
          message.includes('Workflow entity type') && message.includes('is not configured'))
      ) {
        toast.error(`Cannot submit ${entityLabel.toLowerCase()}`, {
          description: message,
          action: {
            label: 'Open Workflows',
            onClick: onOpenWorkflows,
          },
        });
      } else {
        toast.error(`Failed to submit ${entityLabel.toLowerCase()}`, { description: message || undefined });
      }
      return false;
    } finally {
      setSubmitting(false);
    }
  };

  const openApproval = (mode: WorkflowApprovalDialogMode) => {
    setApprovalMode(mode);
    setApprovalOpen(true);
  };

  const confirmApproval = async (comments: string) => {
    const handler = approvalMode === 'approve' ? onApprove : onReject;
    if (!handler) return false;
    try {
      setProcessing(true);
      await handler(comments);
      toast.success(
        approvalMode === 'approve' ? `${entityLabel} approved` : `${entityLabel} rejected`,
        { description: entityNumber ? `${entityNumber}` : undefined }
      );
      await runAfter();
      return true;
    } catch (e: any) {
      toast.error(`Failed to ${approvalMode} ${entityLabel.toLowerCase()}`, { description: e?.message || undefined });
      return false;
    } finally {
      setProcessing(false);
    }
  };

  return (
    <div className={className}>
      <div className="flex flex-wrap items-center gap-2">
        {showStepBadge && effectiveStepName && (
          <Badge variant="outline" className="text-xs">
            Step: {effectiveStepName}
          </Badge>
        )}
        {showStepBadge && pendingApproversText.short && (
          <Badge variant="outline" className="text-xs" title={pendingApproversText.full}>
            Pending with: {pendingApproversText.short}
          </Badge>
        )}

        {submitEnabled && (
          <Button
            size={size}
            onClick={() => setSubmitOpen(true)}
            disabled={submitting}
            title={iconOnly ? `Submit ${entityLabel}` : undefined}
            aria-label={iconOnly ? `Submit ${entityLabel}` : undefined}
          >
            <Send className={iconOnly ? 'h-4 w-4' : 'h-4 w-4 mr-1'} />
            {!iconOnly && 'Submit'}
          </Button>
        )}

        {approveRejectEnabledByStatus && (
          <>
            <Button
              size={size}
              variant="outline"
              className="text-green-600"
              onClick={() => openApproval('approve')}
              disabled={!effectiveCanApprove || processing || summaryLoading}
              title={iconOnly ? `Approve ${entityLabel}` : undefined}
              aria-label={iconOnly ? `Approve ${entityLabel}` : undefined}
            >
              <CheckCircle className={iconOnly ? 'h-4 w-4' : 'h-4 w-4 mr-1'} />
              {!iconOnly && 'Approve'}
            </Button>
            <Button
              size={size}
              variant="outline"
              className="text-red-600"
              onClick={() => openApproval('reject')}
              disabled={!effectiveCanApprove || processing || summaryLoading}
              title={iconOnly ? `Reject ${entityLabel}` : undefined}
              aria-label={iconOnly ? `Reject ${entityLabel}` : undefined}
            >
              <XCircle className={iconOnly ? 'h-4 w-4' : 'h-4 w-4 mr-1'} />
              {!iconOnly && 'Reject'}
            </Button>
          </>
        )}
      </div>

      <ConfirmationDialog
        open={submitOpen}
        onOpenChange={setSubmitOpen}
        title={`Submit ${entityLabel} For Approval?`}
        description={
          <div className="space-y-2">
            <div>
              You are about to submit <strong>{entityNumber || entityLabel}</strong> for approval.
            </div>
            <div className="text-xs text-muted-foreground">
              This will start (or resume) the configured approval workflow for this record.
            </div>
          </div>
        }
        confirmText={submitting ? 'Submitting...' : 'Submit'}
        cancelText="Cancel"
        onConfirm={confirmSubmit}
        isLoading={submitting}
      />

      <WorkflowApprovalCommentDialog
        open={approvalOpen}
        onOpenChange={setApprovalOpen}
        mode={approvalMode}
        title={approvalMode === 'approve' ? `Approve ${entityLabel}` : `Reject ${entityLabel}`}
        description={
          approvalMode === 'approve'
            ? `Approve this ${entityLabel.toLowerCase()}.`
            : `Reject this ${entityLabel.toLowerCase()}. A comment is required.`
        }
        entitySummary={
          entityNumber ? (
            <div className="flex justify-between">
              <span className="text-muted-foreground">{entityLabel}:</span>
              <span className="font-medium">{entityNumber}</span>
            </div>
          ) : undefined
        }
        onConfirm={confirmApproval}
        isLoading={processing}
      />
    </div>
  );
}
