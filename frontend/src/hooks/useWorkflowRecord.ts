import * as React from 'react';

import type { WorkflowApprovalActionsProps } from '@/components/workflow/WorkflowApprovalActions';
import { workflowApiService } from '@/services/workflow-api.service';
import type { WorkflowApprovalChecklistResponseDto, WorkflowEntitySummaryDto, WorkflowSignatureSubmissionDto } from '@/types/workflow';

export interface WorkflowRecordCommandContext {
  comments: string;
  checklistResponses?: WorkflowApprovalChecklistResponseDto[];
  signature?: WorkflowSignatureSubmissionDto;
}

export interface WorkflowRecordCommands {
  submit?: () => Promise<unknown>;
  approve?: (context: WorkflowRecordCommandContext) => Promise<unknown>;
  reject?: (context: WorkflowRecordCommandContext) => Promise<unknown>;
  /**
   * Withdraws a submitted record back to draft.
   *
   * Wire this when the entity has its own recall endpoint; screens that leave it undefined fall
   * back to the generic workflow recall. Without it a requester who submitted prematurely has to
   * ask an approver to reject instead, which leaves a rejection on the record for something
   * nobody actually ruled against.
   */
  recall?: (reason: string) => Promise<unknown>;
  afterAction?: () => Promise<unknown>;
}

export interface UseWorkflowRecordOptions {
  entityType: string;
  entityId: string;
  entityLabel: string;
  entityNumber?: string;
  status: string;
  currentStepName?: string;
  canSubmit?: boolean;
  canApproveReject?: boolean;
  /** Whether the current user may withdraw this record from approval. See {@link WorkflowRecordCommands.recall}. */
  canRecall?: boolean;
  /** Opts this screen into the recall dialog's optional reason box. See `recallPrompt` on the actions component. */
  recallPrompt?: 'confirm' | 'reason';
  enabled?: boolean;
  commands: WorkflowRecordCommands;
  onOpenWorkflows?: () => void;
}

export interface WorkflowRecordIntegration {
  summary?: WorkflowEntitySummaryDto;
  loading: boolean;
  error?: string;
  refresh: () => Promise<void>;
  actionProps: WorkflowApprovalActionsProps;
}

export function useWorkflowRecord(options: UseWorkflowRecordOptions): WorkflowRecordIntegration {
  const {
    entityType,
    entityId,
    entityLabel,
    entityNumber,
    status,
    currentStepName,
    canSubmit,
    canApproveReject,
    canRecall,
    recallPrompt,
    enabled = true,
    commands,
    onOpenWorkflows,
  } = options;
  const [summary, setSummary] = React.useState<WorkflowEntitySummaryDto>();
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string>();

  const refresh = React.useCallback(async () => {
    if (!enabled || !entityType || !entityId) {
      setSummary(undefined);
      return;
    }

    try {
      setLoading(true);
      setError(undefined);
      setSummary(await workflowApiService.getWorkflowEntitySummary(entityType, entityId));
    } catch (requestError: any) {
      setSummary(undefined);
      setError(requestError?.message || 'Unable to load workflow status.');
    } finally {
      setLoading(false);
    }
  }, [enabled, entityId, entityType]);

  React.useEffect(() => {
    void refresh();
  }, [refresh]);

  const afterAction = React.useCallback(async () => {
    if (commands.afterAction) {
      await commands.afterAction();
    }
    await refresh();
  }, [commands.afterAction, refresh]);

  const actionProps = React.useMemo<WorkflowApprovalActionsProps>(() => ({
    entityType,
    entityId,
    entityLabel,
    entityNumber,
    status,
    currentStepName,
    workflowSummary: summary,
    loadWorkflowSummary: false,
    canSubmit,
    canApproveReject,
    canRecall,
    recallPrompt,
    onRecall: commands.recall
      ? async (reason: string) => { await commands.recall?.(reason); }
      : undefined,
    onSubmit: commands.submit ? async () => { await commands.submit?.(); } : undefined,
    onApprove: commands.approve
      ? async (comments, checklistResponses, signature) => {
          await commands.approve?.({ comments, checklistResponses, signature });
        }
      : undefined,
    onReject: commands.reject
      ? async (comments, checklistResponses) => {
          await commands.reject?.({ comments, checklistResponses });
        }
      : undefined,
    onAfterAction: afterAction,
    onOpenWorkflows,
  }), [
    afterAction,
    canApproveReject,
    canRecall,
    canSubmit,
    commands.approve,
    commands.recall,
    commands.reject,
    commands.submit,
    currentStepName,
    entityId,
    entityLabel,
    entityNumber,
    entityType,
    onOpenWorkflows,
    recallPrompt,
    status,
    summary,
  ]);

  return { summary, loading, error, refresh, actionProps };
}
