import type { WorkflowEntitySummaryDto } from '@/types/workflow';

export interface WorkflowVisibilityInput {
  summary?: WorkflowEntitySummaryDto | null;
  loading?: boolean;
  error?: string | null;
}

/** A missing policy is not permission to skip approval. Running instances keep their controls. */
export function getWorkflowVisibility({ summary, loading, error }: WorkflowVisibilityInput) {
  const active = summary?.hasActiveInstance === true;
  const known = !loading && !error && !!summary &&
    (active || (typeof summary.approvalRequired === 'boolean' && summary.hasActiveInstance === false));
  const direct = known && !active && summary?.approvalRequired === false;
  const hasHistory = summary?.hasWorkflowHistory === true;

  return {
    known,
    active,
    direct,
    approvalRequired: active || (known && summary?.approvalRequired === true),
    // During loading/failure retain the tab, but never enable a bypass action.
    showTab: !direct || hasHistory,
    tabLabel: direct && hasHistory ? 'History' : 'Workflow',
    showApprovalControls: known && !direct,
  };
}
