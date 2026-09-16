'use client';

import * as React from 'react';
import { CheckCircle, CornerUpLeft, FileText, Loader2, RotateCcw, Send, Upload, UserRoundCog, XCircle } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Checkbox } from '@/components/ui/checkbox';
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
  WorkflowDocumentRequirementDto,
  WorkflowEntitySummaryDto,
  WorkflowPendingApproverDto,
  WorkflowQualityCheckDto,
  WorkflowTaskAttachmentDto,
  WorkflowTaskConfigDto,
  WorkflowDirectoryUser,
  WorkflowSignatureSubmissionDto,
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
  /**
   * Whether the current user may withdraw this record from approval.
   *
   * Recall is the requester's own act rather than an approver's, so it does not key off
   * `canApproveReject`. Pass this when the screen can answer "did I raise this?" from data the
   * workflow instance does not carry; leave it undefined to use the server's
   * `canCurrentUserRecall` from the entity summary. Either way the endpoint enforces the rule —
   * this only decides whether to draw a button that would otherwise refuse.
   */
  canRecall?: boolean;
  forwardActionsDisabled?: boolean;
  forwardActionsDisabledReason?: string;

  // Handlers
  onSubmit?: () => Promise<void>;
  onApprove?: (comments: string, checklistResponses?: WorkflowApprovalChecklistResponseDto[], signature?: WorkflowSignatureSubmissionDto) => Promise<void>;
  onReject?: (comments: string, checklistResponses?: WorkflowApprovalChecklistResponseDto[]) => Promise<void>;
  /** Withdraws the record from approval, returning it to Draft. See {@link canRecall}. */
  onRecall?: (reason: string) => Promise<void>;
  /**
   * Which recall dialog to draw.
   *
   * `'confirm'` is the long-standing one: a yes/no on stopping the workflow. `'reason'` adds an
   * optional free-text reason, which the recall endpoint has always accepted and no screen ever
   * sent — so every recall in the system to date records the literal "Recalled by requester".
   *
   * This is a per-module decision, not one the shared component gets to make for everybody, so it
   * defaults to `'reason'` only where the page wired its own `onRecall`, and `'confirm'` otherwise.
   * A module owner adopts the reason box by passing `'reason'` on their own screens.
   */
  recallPrompt?: 'confirm' | 'reason';
  onAfterAction?: () => Promise<void>;
  onOpenWorkflows?: () => void;
  onCompleteTask?: (request: {
    stepInstanceId: string;
    comments?: string;
    attachments: WorkflowTaskAttachmentDto[];
  }) => Promise<{ success?: boolean; message?: string; errors?: Array<{ message?: string }> }>;
  externalTaskAttachments?: WorkflowTaskAttachmentDto[];
  hideSatisfiedTaskDocumentUploads?: boolean;
  hideSatisfiedTaskDocumentSection?: boolean;
  hideDocumentChecklistItems?: boolean;
  /** Hide delegation/correction routes when the domain workspace cannot recover those states. */
  hideGovernanceActions?: boolean;
  approveLabel?: string;
  rejectLabel?: string;

  // UI tuning
  size?: 'sm' | 'default' | 'lg' | 'icon';
  iconOnly?: boolean;
  renderMode?: 'buttons' | 'menu-items';
  submitCopyMode?: 'automatic' | 'approval';
  className?: string;
}

export function shouldUseApprovalSubmitCopy(
  directLifecycle: boolean,
  submitCopyMode: 'automatic' | 'approval' = 'automatic'
) {
  return submitCopyMode === 'approval' || !directLifecycle;
}

/**
 * Decides which recall dialog a screen gets.
 *
 * This component is shared by many screens across modules with different owners, so a change to
 * the recall dialog reaches all of them at once. The reason box is therefore opt-in per screen: a
 * module adopts it by passing `recallPrompt`, and anyone who does not pass it keeps the confirm
 * dialog they have always had. The only implicit adoption is a screen that wired its own
 * `onRecall`, which is already a deliberate act by that screen's owner.
 */
export function resolveRecallPrompt(
  recallPrompt: 'confirm' | 'reason' | undefined,
  hasRecallHandler: boolean
): 'confirm' | 'reason' {
  return recallPrompt ?? (hasRecallHandler ? 'reason' : 'confirm');
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
  loadWorkflowSummary = true,
  canSubmit,
  canApproveReject,
  canRecall,
  forwardActionsDisabled = false,
  forwardActionsDisabledReason,
  onSubmit,
  onApprove,
  onReject,
  onRecall,
  recallPrompt,
  onAfterAction,
  onOpenWorkflows,
  onCompleteTask,
  externalTaskAttachments = [],
  hideSatisfiedTaskDocumentUploads = false,
  hideSatisfiedTaskDocumentSection = false,
  hideDocumentChecklistItems = false,
  hideGovernanceActions = false,
  approveLabel = 'Approve',
  rejectLabel = 'Reject',
  size = 'sm',
  iconOnly = false,
  renderMode = 'buttons',
  submitCopyMode = 'automatic',
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
  const [taskOpen, setTaskOpen] = React.useState(false);
  const [taskComments, setTaskComments] = React.useState('');
  const [taskFiles, setTaskFiles] = React.useState<Record<string, File | null>>({});
  const [taskChecklistState, setTaskChecklistState] = React.useState<Record<string, WorkflowApprovalChecklistResponseDto>>({});
  const [taskChecklistUploadingKey, setTaskChecklistUploadingKey] = React.useState<string | null>(null);
  const [taskLocalAttachments, setTaskLocalAttachments] = React.useState<WorkflowTaskAttachmentDto[]>([]);
  const [submitting, setSubmitting] = React.useState(false);
  const [processing, setProcessing] = React.useState(false);
  const [taskProcessing, setTaskProcessing] = React.useState(false);
  const [governanceOpen, setGovernanceOpen] = React.useState(false);
  const [governanceMode, setGovernanceMode] = React.useState<'delegate' | 'send-back'>('delegate');
  const [governanceUsers, setGovernanceUsers] = React.useState<WorkflowDirectoryUser[]>([]);
  const [governanceUserId, setGovernanceUserId] = React.useState('');
  const [governanceReason, setGovernanceReason] = React.useState('');
  const [governanceProcessing, setGovernanceProcessing] = React.useState(false);
  const [resubmitOpen, setResubmitOpen] = React.useState(false);
  const [resubmitComments, setResubmitComments] = React.useState('');
  const [resubmitProcessing, setResubmitProcessing] = React.useState(false);
  const [recallOpen, setRecallOpen] = React.useState(false);
  const [recallReason, setRecallReason] = React.useState('');
  const [recallProcessing, setRecallProcessing] = React.useState(false);

  const [summaryLoading, setSummaryLoading] = React.useState(false);
  const [summaryStepName, setSummaryStepName] = React.useState<string | undefined>(undefined);
  const [summaryStepInstanceId, setSummaryStepInstanceId] = React.useState<string | undefined>(undefined);
  const [summaryStepType, setSummaryStepType] = React.useState<WorkflowStepType | string | undefined>(undefined);
  const [summaryChecklist, setSummaryChecklist] = React.useState<WorkflowQualityCheckDto[]>([]);
  const [summaryTaskConfig, setSummaryTaskConfig] = React.useState<WorkflowTaskConfigDto | undefined>(undefined);
  const [summaryTaskAttachments, setSummaryTaskAttachments] = React.useState<WorkflowTaskAttachmentDto[]>([]);
  const [canCurrentUserApprove, setCanCurrentUserApprove] = React.useState<boolean | undefined>(undefined);
  const [summaryApprovalRequired, setSummaryApprovalRequired] = React.useState<boolean | undefined>(undefined);
  const [canCurrentUserRecall, setCanCurrentUserRecall] = React.useState<boolean | undefined>(undefined);
  const [canCurrentUserComplete, setCanCurrentUserComplete] = React.useState<boolean | undefined>(undefined);
  const [summaryPendingApprovers, setSummaryPendingApprovers] = React.useState<WorkflowPendingApproverDto[]>([]);

  const hasActiveSummary = workflowSummary?.hasActiveInstance === true;
  const effectiveApprovalRequired = hasActiveSummary
    ? true
    : workflowSummary
      ? workflowSummary.approvalRequired !== false
      : summaryApprovalRequired === true;
  const approvalAvailabilityKnown = hasActiveSummary || workflowSummary !== undefined || summaryApprovalRequired !== undefined;
  const effectiveStepName = currentStepName || (hasActiveSummary ? workflowSummary?.currentStepName : undefined) || summaryStepName;
  const effectiveStepInstanceId = (hasActiveSummary ? workflowSummary?.currentStepInstanceId : undefined) || summaryStepInstanceId;
  const effectiveStepType = hasActiveSummary ? workflowSummary?.currentStepType : summaryStepType;
  const effectiveChecklist = hasActiveSummary ? (workflowSummary?.currentStepChecklist ?? []) : summaryChecklist;
  const approvalChecklist = React.useMemo(
    () => hideDocumentChecklistItems
      ? effectiveChecklist.filter((item) => !item.requiresDocument)
      : effectiveChecklist,
    [effectiveChecklist, hideDocumentChecklistItems]
  );
  const hiddenApprovalDocumentChecklist = React.useMemo(
    () => hideDocumentChecklistItems
      ? effectiveChecklist.filter((item) => item.requiresDocument)
      : [],
    [effectiveChecklist, hideDocumentChecklistItems]
  );
  const effectiveTaskConfig = hasActiveSummary ? workflowSummary?.currentStepTaskConfig : summaryTaskConfig;
  const baseTaskAttachments = hasActiveSummary
    ? (workflowSummary?.currentStepTaskAttachments ?? summaryTaskAttachments)
    : summaryTaskAttachments;
  const effectiveTaskAttachments = React.useMemo(() => {
    const byId = new Map<string, WorkflowTaskAttachmentDto>();
    [...baseTaskAttachments, ...externalTaskAttachments, ...taskLocalAttachments].forEach((attachment) => {
      byId.set(attachment.id, attachment);
    });
    return Array.from(byId.values());
  }, [baseTaskAttachments, externalTaskAttachments, taskLocalAttachments]);
  const effectivePendingApprovers = hasActiveSummary ? (workflowSummary?.pendingApprovers ?? []) : summaryPendingApprovers;
  const pendingApproversText = formatPendingApprovers(effectivePendingApprovers);

  const defaultCanSubmit = status === 'Draft';
  const defaultCanApproveReject = status === 'Pending Approval' || status === 'Submitted';

  const submitEnabled = approvalAvailabilityKnown && (canSubmit ?? defaultCanSubmit) && !!onSubmit;
  const directLifecycle = submitEnabled && !effectiveApprovalRequired;
  const approvalSubmitCopy = shouldUseApprovalSubmitCopy(
    directLifecycle,
    submitCopyMode
  );
  const approveRejectEnabledByStatus = effectiveApprovalRequired &&
    (canApproveReject ?? defaultCanApproveReject) && !!onApprove && !!onReject;

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
  const canShowApprovalActions =
    approveRejectEnabledByStatus &&
    (isCurrentApprovalStep || (!loadWorkflowSummary && !hasKnownStepType));
  const effectiveCanApprove =
    effectiveCanApproveFlag === undefined ? canShowApprovalActions : canShowApprovalActions && effectiveCanApproveFlag;
  const normalizedStatus = (status || '').trim().toLowerCase().replace(/\s+/g, '');
  const recallEnabledByStatus =
    hasActiveSummary ||
    ['submitted', 'pendingapproval', 'underreview', 'inreview'].includes(normalizedStatus);
  // Two ways in: a screen that knows its own recall rule passes `canRecall`, everything else
  // leans on the server's canCurrentUserRecall. The caller's answer wins when it gives one.
  const recallAllowed = canRecall === undefined ? effectiveCanRecallFlag === true : canRecall === true;
  const canShowRecall = recallEnabledByStatus && recallAllowed && !effectiveCanApprove;
  const recallPromptMode = resolveRecallPrompt(recallPrompt, !!onRecall);
  const showApproveRejectControls = canShowApprovalActions && !canShowRecall;
  const canCompleteTask = isCurrentTaskStep && effectiveCanCompleteFlag === true && !!effectiveStepInstanceId;
  const normalizedTaskAction = effectiveTaskConfig?.taskActionType?.trim().toLowerCase();
  // Stage-level document requirements define the uploads needed before this task can advance.
  const taskDocumentRequirements = React.useMemo<WorkflowDocumentRequirementDto[]>(() => {
    const configured = (effectiveTaskConfig?.documentRequirements || [])
      .filter((requirement) => requirement?.documentName?.trim() || requirement?.requirementKey?.trim())
      .map((requirement, index) => ({
        id: requirement.id || `document-${index + 1}`,
        requirementKey: requirement.requirementKey?.trim() || `document-${index + 1}`,
        documentName: requirement.documentName?.trim() || `Document ${index + 1}`,
        documentType: requirement.documentType?.trim() || undefined,
        isRequired: requirement.isRequired !== false,
      }));

    if (configured.length > 0) {
      return configured;
    }

    if (
      effectiveTaskConfig?.requiresDocument === true ||
      normalizedTaskAction === 'document' ||
      effectiveTaskConfig?.documentName?.trim()
    ) {
      return [{
        id: 'document-1',
        requirementKey: effectiveTaskConfig?.documentRequirementKey?.trim() || 'document-1',
        documentName: effectiveTaskConfig?.documentName?.trim() || 'Required document',
        isRequired: true,
      }];
    }

    return [];
  }, [effectiveTaskConfig, normalizedTaskAction]);
  const isDocumentTask =
    effectiveTaskConfig?.requiresDocument === true ||
    taskDocumentRequirements.length > 0 ||
    normalizedTaskAction === 'document' ||
    (effectiveStepName || '').trim().toLowerCase() === 'attach document';
  const requiredTaskDocumentRequirements = taskDocumentRequirements.filter((requirement) => requirement.isRequired !== false);
  const hasTaskAttachmentForRequirement = React.useCallback(
    (requirement: WorkflowDocumentRequirementDto) =>
      effectiveTaskAttachments.some((attachment) =>
        (attachment.requirementKey || '').trim().toLowerCase() === requirement.requirementKey.trim().toLowerCase()
      ),
    [effectiveTaskAttachments]
  );
  const missingTaskDocumentRequirements = requiredTaskDocumentRequirements.filter(
    (requirement) => !hasTaskAttachmentForRequirement(requirement) && !taskFiles[requirement.requirementKey]
  );
  const hasRequiredTaskAttachment =
    !isDocumentTask ||
    requiredTaskDocumentRequirements.every((requirement) => hasTaskAttachmentForRequirement(requirement));
  const taskDocumentsSatisfied = isDocumentTask && missingTaskDocumentRequirements.length === 0;
  const showTaskDocumentSection =
    isDocumentTask &&
    (!hideSatisfiedTaskDocumentSection || !taskDocumentsSatisfied);
  const taskButtonLabel = isDocumentTask && !hasRequiredTaskAttachment ? 'Attach Documents' : 'Complete Task';
  const taskDialogTitle = showTaskDocumentSection ? 'Attach Documents' : 'Complete Workflow Task';
  const taskDialogDescription = showTaskDocumentSection
    ? `Attach the required document for ${entityNumber || entityLabel}.`
    : `Complete the current workflow step for ${entityNumber || entityLabel}.`;
  const taskConfirmText = taskProcessing
    ? showTaskDocumentSection
      ? 'Attaching...'
      : 'Completing...'
    : showTaskDocumentSection
      ? 'Attach & Complete'
      : 'Complete Task';
  const taskChecklistItems = effectiveChecklist;
  const taskChecklistSignature = React.useMemo(
    () => taskChecklistItems.map((item, index) => `${getWorkflowChecklistKey(item, index)}:${item.name || ''}`).join('|'),
    [taskChecklistItems]
  );
  const taskRequiredChecklistIncomplete = taskChecklistItems.some((item, index) => {
    if (item.isRequired === false) return false;
    const response = taskChecklistState[getWorkflowChecklistKey(item, index)];
    return !response?.isSatisfied;
  });
  const taskRequiredDocumentIncomplete = taskChecklistItems.some((item, index) => {
    if (!item.requiresDocument) return false;
    const response = taskChecklistState[getWorkflowChecklistKey(item, index)];
    if (item.isRequired === false && response?.isSatisfied !== true) return false;
    return getWorkflowChecklistAttachments(item, index, effectiveTaskAttachments).length === 0;
  });
  const taskConfirmDisabled =
    taskProcessing ||
    taskChecklistUploadingKey !== null ||
    missingTaskDocumentRequirements.length > 0 ||
    taskRequiredChecklistIncomplete ||
    taskRequiredDocumentIncomplete;

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
        setSummaryApprovalRequired(s.approvalRequired);
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
        // Fail closed: approval actions are unsafe unless an active workflow is confirmed.
        if (!mounted) return;
        setSummaryApprovalRequired(undefined);
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

  React.useEffect(() => {
    if (!taskOpen) {
      setTaskChecklistState({});
      setTaskChecklistUploadingKey(null);
      setTaskLocalAttachments([]);
      return;
    }

    setTaskChecklistState((current) => {
      const nextState: Record<string, WorkflowApprovalChecklistResponseDto> = {};
      taskChecklistItems.forEach((item, index) => {
        const key = getWorkflowChecklistKey(item, index);
        nextState[key] = {
          ...current[key],
          id: item.id,
          name: item.name,
          isSatisfied: current[key]?.isSatisfied === true,
          notes: current[key]?.notes || '',
        };
      });
      return nextState;
    });
  }, [taskOpen, taskChecklistSignature]);

  const buildActionDescription = (baseDescription?: string, summary?: WorkflowEntitySummaryDto) => {
    const routingText = buildWorkflowRoutingDescription(summary);
    return [baseDescription, routingText].filter(Boolean).join(' ') || undefined;
  };

  const refreshSummaryForAction = async () => {
    try {
      const s = await workflowApiService.getWorkflowEntitySummary(entityType, entityId);
      if (loadWorkflowSummary && !workflowSummary) {
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
      }
      return s;
    } catch {
      return undefined;
    }
  };

  const runAfter = async () => {
    const refreshedSummary = await refreshSummaryForAction();
    if (onAfterAction) await onAfterAction();
    return refreshedSummary;
  };

  const confirmSubmit = async () => {
    if (forwardActionsDisabled) {
      toast.error(forwardActionsDisabledReason || 'Complete all required inputs before submitting.');
      return false;
    }
    if (!onSubmit) return false;
    try {
      setSubmitting(true);
      await onSubmit();
      const refreshedSummary = await runAfter();
      toast.success(approvalSubmitCopy ? `${entityLabel} submitted for approval` : `${entityLabel} finalized`, {
        description: buildActionDescription(
          entityNumber
            ? approvalSubmitCopy
              ? `${entityNumber} has been submitted.`
              : `${entityNumber} has been finalized because approval is not enabled for this process.`
            : undefined,
          refreshedSummary
        ),
      });
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

  /**
   * Withdraws the record from approval, returning it to draft.
   *
   * Uses the screen's own `onRecall` when it wired one, the generic workflow endpoint otherwise.
   * The reason is optional deliberately: recalling is the requester correcting their own
   * submission before anybody has ruled on it, which is not an act that needs justifying to the
   * system. Rejection demands a reason because somebody is ruling against someone else.
   */
  const confirmRecall = async () => {
    const reason = recallReason.trim();
    try {
      setRecallProcessing(true);
      if (onRecall) {
        await onRecall(reason);
      } else {
        await workflowApiService.recallWorkflowEntity(entityType, entityId, reason ? { reason } : {});
      }
      const refreshedSummary = await runAfter();
      toast.success(`${entityLabel} recalled`, {
        description: buildActionDescription(
          entityNumber ? `${entityNumber} is back with you as a draft.` : undefined,
          refreshedSummary,
        ),
      });
      setRecallOpen(false);
      setRecallReason('');
      return true;
    } catch (e: any) {
      toast.error(`Failed to recall ${entityLabel.toLowerCase()}`, {
        description: e?.body?.message || e?.message || undefined,
      });
      return false;
    } finally {
      setRecallProcessing(false);
    }
  };

  const openApproval = (mode: WorkflowApprovalDialogMode) => {
    if (mode === 'approve' && forwardActionsDisabled) {
      toast.error(forwardActionsDisabledReason || 'Complete all required inputs before approving.');
      return;
    }
    setApprovalMode(mode);
    setApprovalOpen(true);
  };

  const confirmApproval = async (comments: string, checklistResponses?: WorkflowApprovalChecklistResponseDto[], signature?: WorkflowSignatureSubmissionDto) => {
    const handler = approvalMode === 'approve' ? onApprove : onReject;
    if (!handler) return false;
    const persistedChecklistResponses = approvalMode === 'approve'
      ? [
          ...(checklistResponses ?? []),
          ...hiddenApprovalDocumentChecklist.map((item, index) => ({
            id: item.id,
            name: item.name,
            isSatisfied: true,
            notes: 'Satisfied by required stage workspace document validation.',
            attachmentIds: getWorkflowChecklistAttachments(item, index, effectiveTaskAttachments)
              .map((attachment) => attachment.id),
          })),
        ]
      : checklistResponses;
    try {
      setProcessing(true);
      // Domain workspaces that hide document checks bridge their verified documents
      // inside the domain approval action, before the workflow engine validates them.
      if (
        approvalMode === 'approve' &&
        persistedChecklistResponses?.length &&
        !hideDocumentChecklistItems
      ) {
        if (!effectiveStepInstanceId) {
          toast.error('Cannot save approval checklist', {
            description: 'The current workflow step could not be identified. Refresh the page and try again.',
          });
          return false;
        }

        await workflowApiService.saveStepChecklistResponses(effectiveStepInstanceId, persistedChecklistResponses);
      }

      if (approvalMode === 'approve' && signature && workflowSummary?.currentUserApprovalId) {
        await workflowApiService.stageWorkflowSignature(workflowSummary.currentUserApprovalId, signature);
      }
      if (approvalMode === 'approve') await onApprove?.(comments, persistedChecklistResponses, signature);
      else await onReject?.(comments, checklistResponses);
      const refreshedSummary = await runAfter();
      toast.success(
        approvalMode === 'approve' ? `${entityLabel} approved` : `${entityLabel} rejected`,
        { description: buildActionDescription(entityNumber ? `${entityNumber}` : undefined, refreshedSummary) }
      );
      return true;
    } catch (e: any) {
      toast.error(`Failed to ${approvalMode} ${entityLabel.toLowerCase()}`, { description: e?.message || undefined });
      return false;
    } finally {
      setProcessing(false);
    }
  };

  const setTaskChecklistResponse = (
    item: WorkflowQualityCheckDto,
    index: number,
    updates: Partial<WorkflowApprovalChecklistResponseDto>
  ) => {
    const key = getWorkflowChecklistKey(item, index);
    setTaskChecklistState((current) => {
      const existing = current[key];
      return {
        ...current,
        [key]: {
          ...existing,
          ...updates,
          id: item.id,
          name: item.name,
          isSatisfied: updates.isSatisfied ?? existing?.isSatisfied ?? false,
          notes: updates.notes ?? existing?.notes ?? '',
        },
      };
    });
  };

  const buildTaskChecklistResponses = (): WorkflowApprovalChecklistResponseDto[] | undefined => {
    if (taskChecklistItems.length === 0) {
      return undefined;
    }

    return taskChecklistItems.map((item, index) => {
      const key = getWorkflowChecklistKey(item, index);
      return {
        id: item.id,
        name: item.name,
        isSatisfied: taskChecklistState[key]?.isSatisfied === true,
        notes: taskChecklistState[key]?.notes?.trim() || undefined,
        attachmentIds: getWorkflowChecklistAttachments(item, index, effectiveTaskAttachments).map((attachment) => attachment.id),
      };
    });
  };

  const uploadTaskChecklistDocument = async (item: WorkflowQualityCheckDto, index: number, file?: File) => {
    if (!file || !effectiveStepInstanceId) {
      if (file && !effectiveStepInstanceId) {
        toast.error('Cannot upload document', {
          description: 'The current workflow step could not be identified. Refresh the page and try again.',
        });
      }
      return;
    }

    const key = getWorkflowChecklistKey(item, index);
    try {
      setTaskChecklistUploadingKey(key);
      const attachment = await workflowApiService.uploadStepAttachment(
        effectiveStepInstanceId,
        file,
        key,
        item.documentName,
        item.documentType
      );
      setTaskLocalAttachments((current) => [
        ...current.filter((existing) => existing.id !== attachment.id),
        attachment,
      ]);
      setSummaryTaskAttachments((current) => [
        ...current.filter((existing) => existing.id !== attachment.id),
        attachment,
      ]);
      toast.success('Document attached', { description: file.name });
    } catch (error: any) {
      toast.error('Failed to attach document', { description: error?.message || undefined });
    } finally {
      setTaskChecklistUploadingKey(null);
    }
  };

  const completeTask = async () => {
    if (forwardActionsDisabled) {
      toast.error(forwardActionsDisabledReason || 'Complete all required inputs before completing this task.');
      return;
    }
    if (!effectiveStepInstanceId) {
      toast.error('Cannot complete workflow task', {
        description: 'The current workflow step could not be identified. Refresh the page and try again.',
      });
      return;
    }

    if (isDocumentTask && missingTaskDocumentRequirements.length > 0) {
      toast.error('Upload required document', {
        description: `${missingTaskDocumentRequirements[0].documentName} must be attached before this task can be completed.`,
      });
      return;
    }

    if (taskRequiredChecklistIncomplete) {
      toast.error('Complete required checklist items', {
        description: 'Required checks must be satisfied before this task can move forward.',
      });
      return;
    }

    if (taskRequiredDocumentIncomplete) {
      toast.error('Attach required document', {
        description: 'One or more required documents must be attached before this task can move forward.',
      });
      return;
    }

    if (taskChecklistUploadingKey) {
      toast.error('Document upload still in progress');
      return;
    }

    try {
      setTaskProcessing(true);

      const uploadedAttachments: WorkflowTaskAttachmentDto[] = [];
      // Upload every selected stage-level document before completing the workflow task.
      for (const requirement of taskDocumentRequirements) {
        const selectedFile = taskFiles[requirement.requirementKey];
        if (!selectedFile) {
          continue;
        }

        const uploadedAttachment = await workflowApiService.uploadStepAttachment(
          effectiveStepInstanceId,
          selectedFile,
          requirement.requirementKey,
          requirement.documentName,
          requirement.documentType
        );
        uploadedAttachments.push(uploadedAttachment);
        setSummaryTaskAttachments((prev) => [...prev.filter((item) => item.id !== uploadedAttachment.id), uploadedAttachment]);
        setTaskLocalAttachments((prev) => [...prev.filter((item) => item.id !== uploadedAttachment.id), uploadedAttachment]);
      }

      const taskChecklistResponses = buildTaskChecklistResponses();
      if (taskChecklistResponses?.length) {
        await workflowApiService.saveStepChecklistResponses(effectiveStepInstanceId, taskChecklistResponses);
      }

      const taskCompletionAttachments = Array.from(
        new Map(
          [...effectiveTaskAttachments, ...uploadedAttachments].map((attachment) => [
            attachment.id,
            attachment,
          ])
        ).values()
      );

      // Some modules, like Estate Land Acquisition, must sync their own stage after a generic workflow task completes.
      const result = onCompleteTask
        ? await onCompleteTask({
            stepInstanceId: effectiveStepInstanceId,
            comments: taskComments.trim() || undefined,
            attachments: taskCompletionAttachments,
          })
        : await workflowApiService.processStep(effectiveStepInstanceId, {
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

      setTaskOpen(false);
      setTaskComments('');
      setTaskFiles({});
      const refreshedSummary = await runAfter();
      toast.success('Workflow task completed', {
        description: buildActionDescription(entityNumber ? `${entityNumber}` : undefined, refreshedSummary),
      });
    } catch (e: any) {
      toast.error('Failed to complete workflow task', { description: e?.message || undefined });
    } finally {
      setTaskProcessing(false);
    }
  };

  const openGovernanceAction = async (mode: 'delegate' | 'send-back') => {
    setGovernanceMode(mode);
    setGovernanceUserId('');
    setGovernanceReason('');
    setGovernanceOpen(true);
    if (mode === 'delegate' && governanceUsers.length === 0) {
      try { setGovernanceUsers(await workflowApiService.getWorkflowDirectoryUsers()); }
      catch (error: any) { toast.error(error?.message || 'Unable to load users'); }
    }
  };

  const confirmGovernanceAction = async () => {
    const approvalId = hasActiveSummary ? workflowSummary?.currentUserApprovalId : undefined;
    if (!approvalId || !governanceReason.trim()) {
      toast.error('A pending approval and reason are required.'); return;
    }
    if (governanceMode === 'delegate' && !governanceUserId) {
      toast.error('Select a delegate.'); return;
    }
    try {
      setGovernanceProcessing(true);
      if (governanceMode === 'delegate') {
        await workflowApiService.delegateApproval(approvalId, governanceUserId, governanceReason.trim());
        toast.success('Approval delegated');
      } else {
        await workflowApiService.sendApprovalBack(approvalId, { instructions: governanceReason.trim() });
        toast.success('Sent back for correction');
      }
      setGovernanceOpen(false);
      await runAfter();
    } catch (error: any) { toast.error(error?.message || 'Workflow action failed'); }
    finally { setGovernanceProcessing(false); }
  };

  const governanceDialogs = (
    <Dialog open={governanceOpen} onOpenChange={setGovernanceOpen}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{governanceMode === 'delegate' ? 'Delegate approval' : 'Send back for correction'}</DialogTitle>
          <DialogDescription>{entityNumber || entityLabel}</DialogDescription>
        </DialogHeader>
        <div className="space-y-4">
          {governanceMode === 'delegate' && (
            <div className="space-y-2"><Label>Delegate</Label>
              <select className="h-10 w-full rounded-md border bg-background px-3 text-sm" value={governanceUserId} onChange={event => setGovernanceUserId(event.target.value)}>
                <option value="">Select user</option>{governanceUsers.map(user => <option key={user.id} value={user.id}>{user.firstName} {user.lastName}</option>)}
              </select>
            </div>
          )}
          <div className="space-y-2"><Label>{governanceMode === 'delegate' ? 'Reason' : 'Correction instructions'}</Label>
            <Textarea value={governanceReason} onChange={event => setGovernanceReason(event.target.value)} />
          </div>
        </div>
        <DialogFooter><Button variant="outline" onClick={() => setGovernanceOpen(false)} disabled={governanceProcessing}>Cancel</Button>
          <Button onClick={() => void confirmGovernanceAction()} disabled={governanceProcessing}>{governanceMode === 'delegate' ? 'Delegate' : 'Send back'}</Button></DialogFooter>
      </DialogContent>
    </Dialog>
  );

  const recallDialog = recallPromptMode === 'confirm' ? (
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
      confirmText={recallProcessing ? 'Recalling...' : 'Recall'}
      cancelText="Cancel"
      onConfirm={confirmRecall}
      isLoading={recallProcessing}
    />
  ) : (
    <Dialog open={recallOpen} onOpenChange={setRecallOpen}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Recall {entityLabel.toLowerCase()}</DialogTitle>
          <DialogDescription>{entityNumber || entityLabel}</DialogDescription>
        </DialogHeader>
        <div className="space-y-4">
          <p className="text-sm text-muted-foreground">
            This takes it back from approval and returns it to draft, so you can change it and
            submit again. Only the person who raised it can do this, and only while nobody has
            ruled on it yet.
          </p>
          <div className="space-y-2">
            <Label>Reason (optional)</Label>
            <Textarea
              value={recallReason}
              onChange={(event) => setRecallReason(event.target.value)}
              placeholder="e.g. the start date was wrong"
            />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => setRecallOpen(false)} disabled={recallProcessing}>
            Cancel
          </Button>
          <Button onClick={() => void confirmRecall()} disabled={recallProcessing}>
            {recallProcessing && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Recall
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );

  const resubmitCorrection = async () => {
    const correctionId = workflowSummary?.currentUserCorrectionId;
    if (!correctionId) return;
    try {
      setResubmitProcessing(true);
      await workflowApiService.resubmitWorkflowCorrection(correctionId, undefined, resubmitComments.trim() || undefined);
      setResubmitOpen(false); setResubmitComments(''); await runAfter(); toast.success('Correction resubmitted');
    } catch (error: any) { toast.error(error?.message || 'Failed to resubmit correction'); }
    finally { setResubmitProcessing(false); }
  };

  const resubmitDialog = (
    <Dialog open={resubmitOpen} onOpenChange={setResubmitOpen}><DialogContent className="sm:max-w-lg"><DialogHeader>
      <DialogTitle>Resubmit correction</DialogTitle><DialogDescription>{workflowSummary?.correctionInstructions || entityNumber || entityLabel}</DialogDescription>
      </DialogHeader><div className="space-y-2"><Label>Response</Label><Textarea value={resubmitComments} onChange={event => setResubmitComments(event.target.value)} /></div>
      <DialogFooter><Button variant="outline" onClick={() => setResubmitOpen(false)} disabled={resubmitProcessing}>Cancel</Button><Button onClick={() => void resubmitCorrection()} disabled={resubmitProcessing}>Resubmit</Button></DialogFooter>
    </DialogContent></Dialog>
  );

  if (renderMode === 'menu-items') {
    return (
      <>
        {submitEnabled && (
          <DropdownMenuItem
            onSelect={(event) => {
              event.preventDefault();
              setSubmitOpen(true);
            }}
            disabled={submitting || forwardActionsDisabled}
            title={forwardActionsDisabled ? forwardActionsDisabledReason : undefined}
          >
            <Send className="mr-2 h-4 w-4" />
            {approvalSubmitCopy ? 'Submit for Approval' : 'Finalize'}
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
              disabled={!effectiveCanApprove || processing || summaryLoading || forwardActionsDisabled}
              title={forwardActionsDisabled ? forwardActionsDisabledReason : undefined}
            >
              <CheckCircle className="mr-2 h-4 w-4" />
              {approveLabel}
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
              {rejectLabel}
            </DropdownMenuItem>
          </>
        )}

        {!hideGovernanceActions && effectiveCanApprove && workflowSummary?.currentUserApprovalId && (
          <>
            <DropdownMenuItem onSelect={event => { event.preventDefault(); void openGovernanceAction('delegate'); }}><UserRoundCog className="mr-2 h-4 w-4" />Delegate</DropdownMenuItem>
            <DropdownMenuItem onSelect={event => { event.preventDefault(); void openGovernanceAction('send-back'); }}><CornerUpLeft className="mr-2 h-4 w-4" />Send back</DropdownMenuItem>
          </>
        )}

        {workflowSummary?.canCurrentUserResubmit && workflowSummary.currentUserCorrectionId && (
          <DropdownMenuItem disabled={forwardActionsDisabled} title={forwardActionsDisabled ? forwardActionsDisabledReason : undefined} onSelect={event => { event.preventDefault(); setResubmitOpen(true); }}><Send className="mr-2 h-4 w-4" />Resubmit correction</DropdownMenuItem>
        )}

        {canShowRecall && (
          <DropdownMenuItem
            className="text-amber-600 focus:text-amber-700"
            onSelect={(event) => {
              event.preventDefault();
              setRecallOpen(true);
            }}
            disabled={recallProcessing || summaryLoading}
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
            disabled={taskProcessing || summaryLoading || forwardActionsDisabled}
            title={forwardActionsDisabled ? forwardActionsDisabledReason : undefined}
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
          title={approvalSubmitCopy ? `Submit ${entityLabel} For Approval?` : `Finalize ${entityLabel}?`}
          description={
            <div className="space-y-2">
              <div>
                {approvalSubmitCopy ? 'You are about to submit ' : 'You are about to finalize '}
                <strong>{entityNumber || entityLabel}</strong>{approvalSubmitCopy ? ' for approval.' : '.'}
              </div>
              <div className="text-xs text-muted-foreground">
                {approvalSubmitCopy
                  ? 'This will start (or resume) the configured approval workflow for this record.'
                  : 'No active approval workflow is configured for this process, so no approver action will be created.'}
              </div>
            </div>
          }
          confirmText={submitting ? (approvalSubmitCopy ? 'Submitting...' : 'Finalizing...') : (approvalSubmitCopy ? 'Submit for Approval' : 'Finalize')}
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
          checklistItems={approvalMode === 'approve' ? approvalChecklist : []}
          stepInstanceId={effectiveStepInstanceId}
          initialAttachments={effectiveTaskAttachments}
          signaturePolicy={approvalMode === 'approve' ? workflowSummary?.currentStepSignaturePolicy : undefined}
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

              {showTaskDocumentSection && (
                <div className="space-y-3">
                  <Label>Required Documents</Label>
                  {taskDocumentRequirements.map((requirement, index) => {
                    const attachments = effectiveTaskAttachments.filter((attachment) =>
                      (attachment.requirementKey || '').trim().toLowerCase() === requirement.requirementKey.toLowerCase()
                    );
                    const requirementSatisfied = attachments.length > 0;
                    const inputId = `workflow-task-file-${effectiveStepInstanceId}-${requirement.requirementKey || index}`;

                    return (
                      <div key={requirement.id || requirement.requirementKey || index} className="space-y-2 rounded-md border p-3">
                        <div>
                          <div className="text-sm font-medium">
                            {requirement.documentName}
                            {requirement.isRequired !== false && <span className="text-red-600"> *</span>}
                          </div>
                          {requirement.documentType && (
                            <div className="text-xs text-muted-foreground">{requirement.documentType}</div>
                          )}
                        </div>
                        {(!hideSatisfiedTaskDocumentUploads || !requirementSatisfied) && (
                          <Input
                            id={inputId}
                            type="file"
                            onChange={(event) =>
                              setTaskFiles((current) => ({
                                ...current,
                                [requirement.requirementKey]: event.target.files?.[0] ?? null,
                              }))
                            }
                            disabled={taskProcessing}
                          />
                        )}
                        {attachments.length > 0 && (
                          <div className="space-y-1 text-xs text-muted-foreground">
                            {attachments.map((attachment) => (
                              <div key={attachment.id} className="flex items-center gap-2">
                                <Upload className="h-3.5 w-3.5" />
                                <span className="truncate">{attachment.fileName}</span>
                              </div>
                            ))}
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              )}

              {taskChecklistItems.length > 0 && (
                <div className="space-y-3 rounded-md border bg-muted/30 p-3">
                  <div>
                    <div className="text-sm font-medium">Task Checklist</div>
                    <div className="text-xs text-muted-foreground">
                      Complete all required checks before moving this workflow forward.
                    </div>
                  </div>
                  <div className="space-y-3">
                    {taskChecklistItems.map((item, index) => {
                      const key = getWorkflowChecklistKey(item, index);
                      const response = taskChecklistState[key];
                      const itemAttachments = getWorkflowChecklistAttachments(item, index, effectiveTaskAttachments);
                      const isUploading = taskChecklistUploadingKey === key;

                      return (
                        <div key={key} className="space-y-2 rounded-md border bg-background p-3">
                          <div className="flex items-start gap-2">
                            <Checkbox
                              checked={response?.isSatisfied === true}
                              disabled={taskProcessing}
                              onCheckedChange={(checked) =>
                                setTaskChecklistResponse(item, index, { isSatisfied: checked === true })
                              }
                            />
                            <div className="min-w-0 flex-1">
                              <div className="text-sm font-medium">
                                {item.name}
                                {item.isRequired !== false && <span className="text-red-600"> *</span>}
                              </div>
                              {item.description && (
                                <div className="text-xs text-muted-foreground">{item.description}</div>
                              )}
                            </div>
                          </div>
                          <Textarea
                            value={response?.notes || ''}
                            onChange={(event) => setTaskChecklistResponse(item, index, { notes: event.target.value })}
                            placeholder="Notes (optional)"
                            rows={2}
                            disabled={taskProcessing}
                          />
                          {item.requiresDocument && (
                            <div className="space-y-2 rounded-md border bg-muted/20 p-3">
                              <div className="flex flex-wrap items-center justify-between gap-2">
                                <div>
                                  <div className="flex items-center gap-2 text-sm font-medium">
                                    <FileText className="h-4 w-4 text-blue-600" />
                                    {item.documentName || 'Supporting document'}
                                    {(item.isRequired !== false || response?.isSatisfied === true) && (
                                      <span className="text-red-600">*</span>
                                    )}
                                  </div>
                                  {item.documentType && (
                                    <div className="text-xs text-muted-foreground">{item.documentType}</div>
                                  )}
                                </div>
                                {itemAttachments.length > 0 && (
                                  <span className="text-xs font-medium text-green-700">
                                    {itemAttachments.length} attached
                                  </span>
                                )}
                              </div>
                              <div className="relative">
                                <Input
                                  type="file"
                                  accept=".pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png,.txt"
                                  aria-label={`Upload ${item.documentName || item.name}`}
                                  disabled={taskProcessing || isUploading || !effectiveStepInstanceId}
                                  onChange={(event) => {
                                    const file = event.target.files?.[0];
                                    void uploadTaskChecklistDocument(item, index, file);
                                    event.currentTarget.value = '';
                                  }}
                                />
                                {isUploading && (
                                  <div className="pointer-events-none absolute inset-y-0 right-3 flex items-center">
                                    <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
                                  </div>
                                )}
                              </div>
                              {itemAttachments.length > 0 ? (
                                <div className="space-y-1">
                                  {itemAttachments.map((attachment) => (
                                    <div key={attachment.id} className="flex items-center gap-2 text-xs text-muted-foreground">
                                      <CheckCircle className="h-3.5 w-3.5 text-green-600" />
                                      <span className="font-medium text-foreground">{attachment.fileName}</span>
                                      {attachment.uploadedByName && <span>by {attachment.uploadedByName}</span>}
                                    </div>
                                  ))}
                                </div>
                              ) : (
                                <div className="flex items-center gap-2 text-xs text-muted-foreground">
                                  <Upload className="h-3.5 w-3.5" />
                                  Attach evidence before completing this item.
                                </div>
                              )}
                            </div>
                          )}
                        </div>
                      );
                    })}
                  </div>
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
              <Button onClick={completeTask} disabled={taskConfirmDisabled}>
                {taskConfirmText}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
        {!hideGovernanceActions && governanceDialogs}
        {resubmitDialog}
        {recallDialog}
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
            disabled={submitting || forwardActionsDisabled}
            title={forwardActionsDisabled ? forwardActionsDisabledReason : iconOnly ? `${approvalSubmitCopy ? 'Submit for Approval' : 'Finalize'} ${entityLabel}` : undefined}
            aria-label={iconOnly ? `${approvalSubmitCopy ? 'Submit for Approval' : 'Finalize'} ${entityLabel}` : undefined}
          >
            <Send className={iconOnly ? 'h-4 w-4' : 'h-4 w-4 mr-1'} />
            {!iconOnly && (approvalSubmitCopy ? 'Submit for Approval' : 'Finalize')}
          </Button>
        )}

        {showApproveRejectControls && (
          <>
            <Button
              size={size}
              variant="outline"
              className="text-green-600"
              onClick={() => openApproval('approve')}
              disabled={!effectiveCanApprove || processing || summaryLoading || forwardActionsDisabled}
              title={forwardActionsDisabled ? forwardActionsDisabledReason : iconOnly ? `Approve ${entityLabel}` : undefined}
              aria-label={iconOnly ? `Approve ${entityLabel}` : undefined}
            >
              <CheckCircle className={iconOnly ? 'h-4 w-4' : 'h-4 w-4 mr-1'} />
              {!iconOnly && approveLabel}
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
              {!iconOnly && rejectLabel}
            </Button>
          </>
        )}

        {!hideGovernanceActions && effectiveCanApprove && workflowSummary?.currentUserApprovalId && (
          <>
            <Button size={size} variant="outline" onClick={() => void openGovernanceAction('delegate')} title="Delegate approval"><UserRoundCog className={iconOnly ? 'h-4 w-4' : 'mr-1 h-4 w-4'} />{!iconOnly && 'Delegate'}</Button>
            <Button size={size} variant="outline" onClick={() => void openGovernanceAction('send-back')} title="Send back for correction"><CornerUpLeft className={iconOnly ? 'h-4 w-4' : 'mr-1 h-4 w-4'} />{!iconOnly && 'Send back'}</Button>
          </>
        )}

        {workflowSummary?.canCurrentUserResubmit && workflowSummary.currentUserCorrectionId && (
          <Button size={size} variant="outline" onClick={() => setResubmitOpen(true)} disabled={forwardActionsDisabled} title={forwardActionsDisabled ? forwardActionsDisabledReason : 'Resubmit correction'}><Send className={iconOnly ? 'h-4 w-4' : 'mr-1 h-4 w-4'} />{!iconOnly && 'Resubmit'}</Button>
        )}

        {canShowRecall && (
          <Button
            size={size}
            variant="outline"
            className="text-amber-600"
            onClick={() => setRecallOpen(true)}
            disabled={recallProcessing || summaryLoading}
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
            disabled={taskProcessing || summaryLoading || forwardActionsDisabled}
            title={forwardActionsDisabled ? forwardActionsDisabledReason : iconOnly ? `${taskButtonLabel} for ${entityLabel}` : undefined}
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
        title={approvalSubmitCopy ? `Submit ${entityLabel} For Approval?` : `Finalize ${entityLabel}?`}
        description={
          <div className="space-y-2">
            <div>
              {approvalSubmitCopy ? 'You are about to submit ' : 'You are about to finalize '}
              <strong>{entityNumber || entityLabel}</strong>{approvalSubmitCopy ? ' for approval.' : '.'}
            </div>
            <div className="text-xs text-muted-foreground">
              {approvalSubmitCopy
                ? 'This will start (or resume) the configured approval workflow for this record.'
                : 'No active approval workflow is configured for this process, so no approver action will be created.'}
            </div>
          </div>
        }
        confirmText={submitting ? (approvalSubmitCopy ? 'Submitting...' : 'Finalizing...') : (approvalSubmitCopy ? 'Submit for Approval' : 'Finalize')}
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
        checklistItems={approvalMode === 'approve' ? approvalChecklist : []}
        stepInstanceId={effectiveStepInstanceId}
        initialAttachments={effectiveTaskAttachments}
        signaturePolicy={approvalMode === 'approve' ? workflowSummary?.currentStepSignaturePolicy : undefined}
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

            {showTaskDocumentSection && (
              <div className="space-y-3">
                <Label>Required Documents</Label>
                {taskDocumentRequirements.map((requirement, index) => {
                  const attachments = effectiveTaskAttachments.filter((attachment) =>
                    (attachment.requirementKey || '').trim().toLowerCase() === requirement.requirementKey.toLowerCase()
                  );
                  const requirementSatisfied = attachments.length > 0;
                  const inputId = `workflow-task-file-${effectiveStepInstanceId}-${requirement.requirementKey || index}`;

                  return (
                    <div key={requirement.id || requirement.requirementKey || index} className="space-y-2 rounded-md border p-3">
                      <div>
                        <div className="text-sm font-medium">
                          {requirement.documentName}
                          {requirement.isRequired !== false && <span className="text-red-600"> *</span>}
                        </div>
                        {requirement.documentType && (
                          <div className="text-xs text-muted-foreground">{requirement.documentType}</div>
                        )}
                      </div>
                      {(!hideSatisfiedTaskDocumentUploads || !requirementSatisfied) && (
                        <Input
                          id={inputId}
                          type="file"
                          onChange={(event) =>
                            setTaskFiles((current) => ({
                              ...current,
                              [requirement.requirementKey]: event.target.files?.[0] ?? null,
                            }))
                          }
                          disabled={taskProcessing}
                        />
                      )}
                      {attachments.length > 0 && (
                        <div className="space-y-1 text-xs text-muted-foreground">
                          {attachments.map((attachment) => (
                            <div key={attachment.id} className="flex items-center gap-2">
                              <Upload className="h-3.5 w-3.5" />
                              <span className="truncate">{attachment.fileName}</span>
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            )}

            {taskChecklistItems.length > 0 && (
              <div className="space-y-3 rounded-md border bg-muted/30 p-3">
                <div>
                  <div className="text-sm font-medium">Task Checklist</div>
                  <div className="text-xs text-muted-foreground">
                    Complete all required checks before moving this workflow forward.
                  </div>
                </div>
                <div className="space-y-3">
                  {taskChecklistItems.map((item, index) => {
                    const key = getWorkflowChecklistKey(item, index);
                    const response = taskChecklistState[key];
                    const itemAttachments = getWorkflowChecklistAttachments(item, index, effectiveTaskAttachments);
                    const isUploading = taskChecklistUploadingKey === key;

                    return (
                      <div key={key} className="space-y-2 rounded-md border bg-background p-3">
                        <div className="flex items-start gap-2">
                          <Checkbox
                            checked={response?.isSatisfied === true}
                            disabled={taskProcessing}
                            onCheckedChange={(checked) =>
                              setTaskChecklistResponse(item, index, { isSatisfied: checked === true })
                            }
                          />
                          <div className="min-w-0 flex-1">
                            <div className="text-sm font-medium">
                              {item.name}
                              {item.isRequired !== false && <span className="text-red-600"> *</span>}
                            </div>
                            {item.description && (
                              <div className="text-xs text-muted-foreground">{item.description}</div>
                            )}
                          </div>
                        </div>
                        <Textarea
                          value={response?.notes || ''}
                          onChange={(event) => setTaskChecklistResponse(item, index, { notes: event.target.value })}
                          placeholder="Notes (optional)"
                          rows={2}
                          disabled={taskProcessing}
                        />
                        {item.requiresDocument && (
                          <div className="space-y-2 rounded-md border bg-muted/20 p-3">
                            <div className="flex flex-wrap items-center justify-between gap-2">
                              <div>
                                <div className="flex items-center gap-2 text-sm font-medium">
                                  <FileText className="h-4 w-4 text-blue-600" />
                                  {item.documentName || 'Supporting document'}
                                  {(item.isRequired !== false || response?.isSatisfied === true) && (
                                    <span className="text-red-600">*</span>
                                  )}
                                </div>
                                {item.documentType && (
                                  <div className="text-xs text-muted-foreground">{item.documentType}</div>
                                )}
                              </div>
                              {itemAttachments.length > 0 && (
                                <span className="text-xs font-medium text-green-700">
                                  {itemAttachments.length} attached
                                </span>
                              )}
                            </div>
                            <div className="relative">
                              <Input
                                type="file"
                                accept=".pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png,.txt"
                                aria-label={`Upload ${item.documentName || item.name}`}
                                disabled={taskProcessing || isUploading || !effectiveStepInstanceId}
                                onChange={(event) => {
                                  const file = event.target.files?.[0];
                                  void uploadTaskChecklistDocument(item, index, file);
                                  event.currentTarget.value = '';
                                }}
                              />
                              {isUploading && (
                                <div className="pointer-events-none absolute inset-y-0 right-3 flex items-center">
                                  <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
                                </div>
                              )}
                            </div>
                            {itemAttachments.length > 0 ? (
                              <div className="space-y-1">
                                {itemAttachments.map((attachment) => (
                                  <div key={attachment.id} className="flex items-center gap-2 text-xs text-muted-foreground">
                                    <CheckCircle className="h-3.5 w-3.5 text-green-600" />
                                    <span className="font-medium text-foreground">{attachment.fileName}</span>
                                    {attachment.uploadedByName && <span>by {attachment.uploadedByName}</span>}
                                  </div>
                                ))}
                              </div>
                            ) : (
                              <div className="flex items-center gap-2 text-xs text-muted-foreground">
                                <Upload className="h-3.5 w-3.5" />
                                Attach evidence before completing this item.
                              </div>
                            )}
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
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
            <Button onClick={completeTask} disabled={taskConfirmDisabled}>
              {taskConfirmText}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      {!hideGovernanceActions && governanceDialogs}
      {resubmitDialog}
      {recallDialog}
    </div>
  );
}

function getWorkflowChecklistKey(item: WorkflowQualityCheckDto, index: number) {
  return (item.id || item.name || `check-${index + 1}`).trim();
}

function getWorkflowChecklistAttachments(
  item: WorkflowQualityCheckDto,
  index: number,
  attachments: WorkflowTaskAttachmentDto[]
) {
  const key = getWorkflowChecklistKey(item, index).toLowerCase();
  return attachments.filter((attachment) => {
    const attachmentKey = (attachment.checklistItemId || attachment.requirementKey || '').trim().toLowerCase();
    return attachmentKey === key;
  });
}

export function buildWorkflowRoutingDescription(summary?: WorkflowEntitySummaryDto) {
  if (!summary?.hasActiveInstance) {
    return undefined;
  }

  const routedTo = (summary.pendingApprovers || [])
    .map((approver) => (approver.approverName || approver.approverRole || '').trim())
    .filter(Boolean);

  if (routedTo.length === 0) {
    return undefined;
  }

  const shortList = routedTo.length <= 2
    ? routedTo.join(', ')
    : `${routedTo.slice(0, 2).join(', ')} +${routedTo.length - 2}`;

  return `Routed to ${shortList}.`;
}
