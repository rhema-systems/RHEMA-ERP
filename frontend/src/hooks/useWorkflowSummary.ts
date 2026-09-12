import * as React from 'react';

import { getWorkflowVisibility } from '@/components/workflow/workflowVisibility';
import { workflowApiService } from '@/services/workflow-api.service';
import type { WorkflowEntitySummaryDto } from '@/types/workflow';

export interface WorkflowSummaryOptions {
  entityType?: string;
  entityId?: string;
  workflowSummary?: WorkflowEntitySummaryDto;
  workflowSummaryLoading?: boolean;
  workflowSummaryError?: string;
  loadWorkflowSummary?: boolean;
}

// Older APIs return the configured entity code (for example PURCHASE_REQUISITION)
// rather than the requested display name. Only normalize entity punctuation;
// record IDs remain bound exactly (apart from UUID letter casing).
function workflowRecordKey(entityType?: string, entityId?: string) {
  return `${(entityType || '').replace(/[^a-z0-9]/gi, '').toLowerCase()}:${(entityId || '').toLowerCase()}`;
}

/** Scope async policy results to the current record; never reuse another record's permission. */
export function useWorkflowSummary({
  entityType,
  entityId,
  workflowSummary,
  workflowSummaryLoading = false,
  workflowSummaryError,
  loadWorkflowSummary = true,
}: WorkflowSummaryOptions) {
  const key = workflowRecordKey(entityType, entityId);
  const [result, setResult] = React.useState<{
    key: string; summary?: WorkflowEntitySummaryDto; loading: boolean; error?: string;
  }>();
  const generation = React.useRef(0);
  const matches = (value?: WorkflowEntitySummaryDto) => !!value &&
    workflowRecordKey(value.entityType, value.entityId) === key;
  const supplied = matches(workflowSummary) ? workflowSummary : undefined;

  const refresh = React.useCallback(async () => {
    const request = ++generation.current;
    if (!entityType || !entityId) return undefined;
    setResult({ key, loading: true });
    try {
      const summary = await workflowApiService.getWorkflowEntitySummary(entityType, entityId);
      if (workflowRecordKey(summary.entityType, summary.entityId) !== key) {
        throw new Error('Workflow status did not match this record. Refresh and try again.');
      }
      if (request === generation.current) setResult({ key, summary, loading: false });
      return summary;
    } catch (requestError: any) {
      if (request === generation.current) {
        setResult({ key, loading: false, error: requestError?.message || 'Unable to load workflow status.' });
      }
      return undefined;
    }
  }, [entityId, entityType, key]);

  React.useEffect(() => {
    if (loadWorkflowSummary && !workflowSummary) void refresh();
    return () => { generation.current += 1; };
  }, [loadWorkflowSummary, workflowSummary, refresh]);

  const local = result?.key === key ? result : undefined;
  const external = !loadWorkflowSummary || workflowSummary !== undefined;
  const summary = external ? supplied : local?.summary;
  const loading = external ? workflowSummaryLoading : local?.loading ?? !!(entityType && entityId);
  const error = external ? workflowSummaryError : local?.error;
  const visibility = getWorkflowVisibility({ summary, loading, error });
  return { summary, loading, error, refresh, visibility };
}
