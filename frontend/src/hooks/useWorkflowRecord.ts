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
   * ⚠ **G-4.3 / G-10.4 (2026-09-15): this hook had no recall concept at all.** Its command surface
   * was submit / approve / reject, and a search of the whole frontend found no caller of either
   * `staffRequisitionService.recall()` or `jobOfferService.recall()` — both of which existed, as
   * did their endpoints, their services (enforcing requester-only) and their adapters. Other HR
   * modules wire recall to a button; recruitment never did, while the requisition edit page told
   * users *"Recall it first if it is still awaiting approval."*
   *
   * A requester who submitted prematurely had to ask an approver to reject it instead — which
   * leaves a rejection on the record for something nobody actually ruled against.
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
    status,
    summary,
  ]);

  return { summary, loading, error, refresh, actionProps };
}
