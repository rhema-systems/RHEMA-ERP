'use client';

import * as React from 'react';
import { CheckCircle, RotateCcw, Send, Upload, XCircle } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { DropdownMenuItem } from '@/components/ui/dropdown-menu';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { WorkflowApprovalCommentDialog, WorkflowApprovalDialogMode } from '@/components/workflow/WorkflowApprovalCommentDialog';
import { workflowApiService } from '@/services/workflow-api.service';
import type {
  WorkflowApprovalChecklistResponseDto,
  WorkflowEntitySummaryDto,
  WorkflowPendingApproverDto,
  WorkflowQualityCheckDto,
  WorkflowTaskAttachmentDto,
  WorkflowTaskConfigDto,
} from '@/types/workflow';
import { WorkflowStepAction, WorkflowStepType } from '@/types/workflow';

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
  onApprove?: (comments: string, checklistResponses?: WorkflowApprovalChecklistResponseDto[]) => Promise<void>;
  onReject?: (comments: string, checklistResponses?: WorkflowApprovalChecklistResponseDto[]) => Promise<void>;
  onAfterAction?: () => Promise<void>;
  onOpenWorkflows?: () => void;

  // UI tuning
  size?: 'sm' | 'default' | 'lg' | 'icon';
  iconOnly?: boolean;
  renderMode?: 'buttons' | 'menu-items';
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
  renderMode = 'buttons',
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
  const [recallOpen, setRecallOpen] = React.useState(false);
  const [taskOpen, setTaskOpen] = React.useState(false);
  const [taskComments, setTaskComments] = React.useState('');
  const [taskFile, setTaskFile] = React.useState<File | null>(null);
  const [submitting, setSubmitting] = React.useState(false);
  const [processing, setProcessing] = React.useState(false);
  const [recalling, setRecalling] = React.useState(false);
  const [taskProcessing, setTaskProcessing] = React.useState(false);

  const [summaryLoading, setSummaryLoading] = React.useState(false);
  const [summaryStepName, setSummaryStepName] = React.useState<string | undefined>(undefined);
  const [summaryStepInstanceId, setSummaryStepInstanceId] = React.useState<string | undefined>(undefined);
  const [summaryStepType, setSummaryStepType] = React.useState<WorkflowStepType | string | undefined>(undefined);
  const [summaryChecklist, setSummaryChecklist] = React.useState<WorkflowQualityCheckDto[]>([]);
  const [summaryTaskConfig, setSummaryTaskConfig] = React.useState<WorkflowTaskConfigDto | undefined>(undefined);
  const [summaryTaskAttachments, setSummaryTaskAttachments] = React.useState<WorkflowTaskAttachmentDto[]>([]);
  const [canCurrentUserApprove, setCanCurrentUserApprove] = React.useState<boolean | undefined>(undefined);
  const [canCurrentUserRecall, setCanCurrentUserRecall] = React.useState<boolean | undefined>(undefined);
  const [canCurrentUserComplete, setCanCurrentUserComplete] = React.useState<boolean | undefined>(undefined);
  const [summaryPendingApprovers, setSummaryPendingApprovers] = React.useState<WorkflowPendingApproverDto[]>([]);

  const hasActiveSummary = workflowSummary?.hasActiveInstance === true;
  const effectiveStepName = currentStepName || (hasActiveSummary ? workflowSummary?.currentStepName : undefined) || summaryStepName;
  const effectiveStepInstanceId = (hasActiveSummary ? workflowSummary?.currentStepInstanceId : undefined) || summaryStepInstanceId;
  const effectiveStepType = hasActiveSummary ? workflowSummary?.currentStepType : summaryStepType;
  const effectiveChecklist = hasActiveSummary ? (workflowSummary?.currentStepChecklist ?? []) : summaryChecklist;
  const effectiveTaskConfig = hasActiveSummary ? workflowSummary?.currentStepTaskConfig : summaryTaskConfig;
  const effectiveTaskAttachments = hasActiveSummary ? (workflowSummary?.currentStepTaskAttachments ?? []) : summaryTaskAttachments;
  const effectivePendingApprovers = hasActiveSummary ? (workflowSummary?.pendingApprovers ?? []) : summaryPendingApprovers;
  const pendingApproversText = formatPendingApprovers(effectivePendingApprovers);

  const defaultCanSubmit = status === 'Draft';
  const defaultCanApproveReject = status === 'Pending Approval' || status === 'Submitted';

  const submitEnabled = (canSubmit ?? defaultCanSubmit) && !!onSubmit;
  const approveRejectEnabledByStatus = (canApproveReject ?? defaultCanApproveReject) && !!onApprove && !!onReject;

  const effectiveCanApproveFlag =
    hasActiveSummary ? workflowSummary?.canCurrentUserApprove : canCurrentUserApprove;
  const effectiveCanRecallFlag =
    hasActiveSummary ? workflowSummary?.canCurrentUserRecall : canCurrentUserRecall;
  const effectiveCanCompleteFlag =
    hasActiveSummary ? workflowSummary?.canCurrentUserComplete : canCurrentUserComplete;

  const normalizedStepType = `${effectiveStepType ?? ''}`.toLowerCase();
  const hasKnownStepType =
    effectiveStepType !== undefined &&
    effectiveStepType !== null &&
    `${effectiveStepType}`.trim() !== '';
  const isCurrentApprovalStep =
    effectiveStepType === WorkflowStepType.Approval ||
    normalizedStepType === 'approval' ||
    normalizedStepType === '2';

  const isCurrentTaskStep =
    effectiveStepType === WorkflowStepType.Manual ||
    normalizedStepType === 'manual' ||
    normalizedStepType === '0';
  const canShowApprovalActions = approveRejectEnabledByStatus && (!hasKnownStepType || isCurrentApprovalStep);
  const effectiveCanApprove =
    effectiveCanApproveFlag === undefined ? canShowApprovalActions : canShowApprovalActions && effectiveCanApproveFlag;
  const normalizedStatus = (status || '').trim().toLowerCase().replace(/\s+/g, '');
  const recallEnabledByStatus =
    hasActiveSummary ||
    ['submitted', 'pendingapproval', 'underreview', 'inreview'].includes(normalizedStatus);
  const canShowRecall = recallEnabledByStatus && effectiveCanRecallFlag === true && !effectiveCanApprove;
  const showApproveRejectControls = canShowApprovalActions && !canShowRecall;
  const canCompleteTask = isCurrentTaskStep && effectiveCanCompleteFlag === true && !!effectiveStepInstanceId;
  const normalizedTaskAction = effectiveTaskConfig?.taskActionType?.trim().toLowerCase();
  const isDocumentTask =
    effectiveTaskConfig?.requiresDocument === true ||
    normalizedTaskAction === 'document' ||
    (effectiveStepName || '').trim().toLowerCase() === 'attach document';
  const taskRequirementKey = effectiveTaskConfig?.documentRequirementKey?.trim();
  const hasRequiredTaskAttachment =
    !isDocumentTask ||
    effectiveTaskAttachments.some((attachment) => {
      if (!taskRequirementKey) {
        return true;
      }

      return (attachment.requirementKey || '').trim().toLowerCase() === taskRequirementKey.toLowerCase();
    });
  const taskButtonLabel = isDocumentTask && !hasRequiredTaskAttachment ? 'Attach Document' : 'Complete Task';
  const taskDialogTitle = isDocumentTask ? 'Attach Document' : 'Complete Workflow Task';
  const taskDialogDescription = isDocumentTask
    ? `Attach the required document for ${entityNumber || entityLabel}.`
    : `Complete the current workflow step for ${entityNumber || entityLabel}.`;
  const taskConfirmText = taskProcessing
    ? isDocumentTask
      ? 'Attaching...'
      : 'Completing...'
    : isDocumentTask
      ? 'Attach & Complete'
      : 'Complete Task';

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
        setSummaryStepInstanceId(s.currentStepInstanceId || undefined);
        setSummaryStepType(s.currentStepType);
        setSummaryChecklist(s.currentStepChecklist || []);
        setSummaryTaskConfig(s.currentStepTaskConfig || undefined);
        setSummaryTaskAttachments(s.currentStepTaskAttachments || []);
        setCanCurrentUserApprove(!!s.canCurrentUserApprove);
        setCanCurrentUserRecall(!!s.canCurrentUserRecall);
        setCanCurrentUserComplete(!!s.canCurrentUserComplete);
        setSummaryPendingApprovers(s.pendingApprovers || []);
      } catch (e: any) {
        // Summary is best-effort; actions still work and will show API error if forbidden.
        if (!mounted) return;
        setCanCurrentUserApprove(undefined);
        setCanCurrentUserRecall(undefined);
        setCanCurrentUserComplete(undefined);
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
        setSummaryStepInstanceId(s.currentStepInstanceId || undefined);
        setSummaryStepType(s.currentStepType);
        setSummaryChecklist(s.currentStepChecklist || []);
        setSummaryTaskConfig(s.currentStepTaskConfig || undefined);
        setSummaryTaskAttachments(s.currentStepTaskAttachments || []);
        setCanCurrentUserApprove(!!s.canCurrentUserApprove);
        setCanCurrentUserRecall(!!s.canCurrentUserRecall);
        setCanCurrentUserComplete(!!s.canCurrentUserComplete);
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

  const confirmApproval = async (comments: string, checklistResponses?: WorkflowApprovalChecklistResponseDto[]) => {
    const handler = approvalMode === 'approve' ? onApprove : onReject;
    if (!handler) return false;
    try {
      setProcessing(true);
      if (approvalMode === 'approve' && checklistResponses?.length) {
        if (!effectiveStepInstanceId) {
          toast.error('Cannot save approval checklist', {
            description: 'The current workflow step could not be identified. Refresh the page and try again.',
          });
          return false;
        }

        await workflowApiService.saveStepChecklistResponses(effectiveStepInstanceId, checklistResponses);
      }

      await handler(comments, checklistResponses);
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

  const confirmRecall = async () => {
    try {
      setRecalling(true);
      await workflowApiService.recallWorkflowEntity(entityType, entityId);
      toast.success(`${entityLabel} recalled`, {
        description: entityNumber ? `${entityNumber} has been returned to draft.` : undefined,
      });
      await runAfter();
      return true;
    } catch (e: any) {
      toast.error(`Failed to recall ${entityLabel.toLowerCase()}`, { description: e?.message || undefined });
      return false;
    } finally {
      setRecalling(false);
    }
  };

  const completeTask = async () => {
    if (!effectiveStepInstanceId) {
      toast.error('Cannot complete workflow task', {
        description: 'The current workflow step could not be identified. Refresh the page and try again.',
      });
      return;
    }

    if (isDocumentTask && !hasRequiredTaskAttachment && !taskFile) {
      toast.error('Upload required document', {
        description: effectiveTaskConfig?.documentName
          ? `${effectiveTaskConfig.documentName} must be attached before this task can be completed.`
          : 'A document must be attached before this task can be completed.',
      });
      return;
    }

    try {
      setTaskProcessing(true);

      if (taskFile) {
        const uploadedAttachment = await workflowApiService.uploadStepAttachment(
          effectiveStepInstanceId,
          taskFile,
          taskRequirementKey,
          effectiveTaskConfig?.documentName
        );
        setSummaryTaskAttachments((prev) => [...prev, uploadedAttachment]);
      }

      const result = await workflowApiService.processStep(effectiveStepInstanceId, {
        action: WorkflowStepAction.Complete,
        comments: taskComments.trim() || undefined,
      });

      if (result.success === false) {
        const errorMessage =
          result.message ||
          result.errors?.map((error) => error.message).filter(Boolean).join(' ') ||
          'The workflow task could not be completed.';
        throw new Error(errorMessage);
      }

      toast.success('Workflow task completed', {
        description: entityNumber ? `${entityNumber}` : undefined,
      });
      setTaskOpen(false);
      setTaskComments('');
      setTaskFile(null);
      await runAfter();
    } catch (e: any) {
      toast.error('Failed to complete workflow task', { description: e?.message || undefined });
    } finally {
      setTaskProcessing(false);
    }
  };

  if (renderMode === 'menu-items') {
    return (
      <>
        {submitEnabled && (
          <DropdownMenuItem
            onSelect={(event) => {
              event.preventDefault();
              setSubmitOpen(true);
            }}
            disabled={submitting}
          >
            <Send className="mr-2 h-4 w-4" />
            Submit for Approval
          </DropdownMenuItem>
        )}

        {showApproveRejectControls && (
          <>
            <DropdownMenuItem
              className="text-green-600 focus:text-green-700"
              onSelect={(event) => {
                event.preventDefault();
                openApproval('approve');
              }}
              disabled={!effectiveCanApprove || processing || summaryLoading}
            >
              <CheckCircle className="mr-2 h-4 w-4" />
              Approve
            </DropdownMenuItem>
            <DropdownMenuItem
              className="text-red-600 focus:text-red-700"
              onSelect={(event) => {
                event.preventDefault();
                openApproval('reject');
              }}
              disabled={!effectiveCanApprove || processing || summaryLoading}
            >
              <XCircle className="mr-2 h-4 w-4" />
              Reject
            </DropdownMenuItem>
          </>
        )}

        {canShowRecall && (
          <DropdownMenuItem
            className="text-amber-600 focus:text-amber-700"
            onSelect={(event) => {
              event.preventDefault();
              setRecallOpen(true);
            }}
            disabled={recalling || summaryLoading}
          >
            <RotateCcw className="mr-2 h-4 w-4" />
            Recall
          </DropdownMenuItem>
        )}

        {canCompleteTask && (
          <DropdownMenuItem
            className="text-blue-600 focus:text-blue-700"
            onSelect={(event) => {
              event.preventDefault();
              setTaskOpen(true);
            }}
            disabled={taskProcessing || summaryLoading}
          >
            {isDocumentTask && !hasRequiredTaskAttachment ? (
              <Upload className="mr-2 h-4 w-4" />
            ) : (
              <CheckCircle className="mr-2 h-4 w-4" />
            )}
            {taskButtonLabel}
          </DropdownMenuItem>
        )}

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

        <ConfirmationDialog
          open={recallOpen}
          onOpenChange={setRecallOpen}
          title={`Recall ${entityLabel}?`}
          description={
            <div className="space-y-2">
              <div>
                You are about to recall <strong>{entityNumber || entityLabel}</strong>.
              </div>
              <div className="text-xs text-muted-foreground">
                The active workflow will be stopped and the record will return to draft so it can be edited and resubmitted.
              </div>
            </div>
          }
          confirmText={recalling ? 'Recalling...' : 'Recall'}
          cancelText="Cancel"
          onConfirm={confirmRecall}
          isLoading={recalling}
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
          checklistItems={approvalMode === 'approve' ? effectiveChecklist : []}
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

        <Dialog
          open={taskOpen}
          onOpenChange={(open) => {
            if (!taskProcessing) {
              setTaskOpen(open);
            }
          }}
        >
          <DialogContent className="sm:max-w-lg">
            <DialogHeader>
              <DialogTitle>{taskDialogTitle}</DialogTitle>
              <DialogDescription>{taskDialogDescription}</DialogDescription>
            </DialogHeader>

            <div className="space-y-4">
              {effectiveTaskConfig?.instructions && (
                <div className="rounded-md border bg-muted/30 p-3 text-sm text-muted-foreground">
                  {effectiveTaskConfig.instructions}
                </div>
              )}

              {isDocumentTask && (
                <div className="space-y-2">
                  <Label htmlFor={`workflow-task-file-${effectiveStepInstanceId}`}>Required Document</Label>
                  {effectiveTaskConfig?.documentName && (
                    <div className="text-sm font-medium">{effectiveTaskConfig.documentName}</div>
                  )}
                  <Input
                    id={`workflow-task-file-${effectiveStepInstanceId}`}
                    type="file"
                    onChange={(event) => setTaskFile(event.target.files?.[0] ?? null)}
                    disabled={taskProcessing}
                  />
                  {effectiveTaskAttachments.length > 0 && (
                    <div className="space-y-1 text-xs text-muted-foreground">
                      {effectiveTaskAttachments.map((attachment) => (
                        <div key={attachment.id} className="flex items-center gap-2">
                          <Upload className="h-3.5 w-3.5" />
                          <span className="truncate">{attachment.fileName}</span>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              )}

              <div className="space-y-2">
                <Label htmlFor={`workflow-task-comments-${effectiveStepInstanceId}`}>Comments</Label>
                <Textarea
                  id={`workflow-task-comments-${effectiveStepInstanceId}`}
                  value={taskComments}
                  onChange={(event) => setTaskComments(event.target.value)}
                  placeholder="Optional completion notes"
                  disabled={taskProcessing}
                />
              </div>
            </div>

            <DialogFooter>
              <Button variant="outline" onClick={() => setTaskOpen(false)} disabled={taskProcessing}>
                Cancel
              </Button>
              <Button onClick={completeTask} disabled={taskProcessing}>
                {taskConfirmText}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </>
    );
  }

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

        {showApproveRejectControls && (
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

        {canShowRecall && (
          <Button
            size={size}
            variant="outline"
            className="text-amber-600"
            onClick={() => setRecallOpen(true)}
            disabled={recalling || summaryLoading}
            title={iconOnly ? `Recall ${entityLabel}` : undefined}
            aria-label={iconOnly ? `Recall ${entityLabel}` : undefined}
          >
            <RotateCcw className={iconOnly ? 'h-4 w-4' : 'h-4 w-4 mr-1'} />
            {!iconOnly && 'Recall'}
          </Button>
        )}

        {canCompleteTask && (
          <Button
            size={size}
            variant="outline"
            className="text-blue-600"
            onClick={() => setTaskOpen(true)}
            disabled={taskProcessing || summaryLoading}
            title={iconOnly ? `${taskButtonLabel} for ${entityLabel}` : undefined}
            aria-label={iconOnly ? `${taskButtonLabel} for ${entityLabel}` : undefined}
          >
            {isDocumentTask && !hasRequiredTaskAttachment ? (
              <Upload className={iconOnly ? 'h-4 w-4' : 'h-4 w-4 mr-1'} />
            ) : (
              <CheckCircle className={iconOnly ? 'h-4 w-4' : 'h-4 w-4 mr-1'} />
            )}
            {!iconOnly && taskButtonLabel}
          </Button>
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

      <ConfirmationDialog
        open={recallOpen}
        onOpenChange={setRecallOpen}
        title={`Recall ${entityLabel}?`}
        description={
          <div className="space-y-2">
            <div>
              You are about to recall <strong>{entityNumber || entityLabel}</strong>.
            </div>
            <div className="text-xs text-muted-foreground">
              The active workflow will be stopped and the record will return to draft so it can be edited and resubmitted.
            </div>
          </div>
        }
        confirmText={recalling ? 'Recalling...' : 'Recall'}
        cancelText="Cancel"
        onConfirm={confirmRecall}
        isLoading={recalling}
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
        checklistItems={approvalMode === 'approve' ? effectiveChecklist : []}
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

      <Dialog
        open={taskOpen}
        onOpenChange={(open) => {
          if (!taskProcessing) {
            setTaskOpen(open);
          }
        }}
      >
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{taskDialogTitle}</DialogTitle>
            <DialogDescription>{taskDialogDescription}</DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            {effectiveTaskConfig?.instructions && (
              <div className="rounded-md border bg-muted/30 p-3 text-sm text-muted-foreground">
                {effectiveTaskConfig.instructions}
              </div>
            )}

            {isDocumentTask && (
              <div className="space-y-2">
                <Label htmlFor={`workflow-task-file-${effectiveStepInstanceId}`}>Required Document</Label>
                {effectiveTaskConfig?.documentName && (
                  <div className="text-sm font-medium">{effectiveTaskConfig.documentName}</div>
                )}
                <Input
                  id={`workflow-task-file-${effectiveStepInstanceId}`}
                  type="file"
                  onChange={(event) => setTaskFile(event.target.files?.[0] ?? null)}
                  disabled={taskProcessing}
                />
                {effectiveTaskAttachments.length > 0 && (
                  <div className="space-y-1 text-xs text-muted-foreground">
                    {effectiveTaskAttachments.map((attachment) => (
                      <div key={attachment.id} className="flex items-center gap-2">
                        <Upload className="h-3.5 w-3.5" />
                        <span className="truncate">{attachment.fileName}</span>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            )}

            <div className="space-y-2">
              <Label htmlFor={`workflow-task-comments-${effectiveStepInstanceId}`}>Comments</Label>
              <Textarea
                id={`workflow-task-comments-${effectiveStepInstanceId}`}
                value={taskComments}
                onChange={(event) => setTaskComments(event.target.value)}
                placeholder="Optional completion notes"
                disabled={taskProcessing}
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setTaskOpen(false)} disabled={taskProcessing}>
              Cancel
            </Button>
            <Button onClick={completeTask} disabled={taskProcessing}>
              {taskConfirmText}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
