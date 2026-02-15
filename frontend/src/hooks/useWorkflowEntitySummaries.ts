import * as React from 'react';

import type { WorkflowEntitySummaryDto, WorkflowPendingApproverDto } from '@/types/workflow';
import { workflowApiService } from '@/services/workflow-api.service';

export function formatPendingApprovers(pending: WorkflowPendingApproverDto[], maxNames = 2) {
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
}

/**
 * Loads workflow entity summaries in batch for a single entityType.
 * Intended for list/grid pages to avoid per-row summary calls.
 */
export function useWorkflowEntitySummaries(entityType: string, entityIds: string[], enabled = true) {
  const [loading, setLoading] = React.useState(false);
  const [summariesById, setSummariesById] = React.useState<Record<string, WorkflowEntitySummaryDto>>({});

  const idsKey = React.useMemo(() => {
    const unique = Array.from(new Set((entityIds || []).filter(Boolean)));
    unique.sort();
    return unique.join(',');
  }, [entityIds]);

  React.useEffect(() => {
    if (!enabled) return;
    if (!entityType || !idsKey) {
      setSummariesById({});
      return;
    }

    let mounted = true;
    (async () => {
      try {
        setLoading(true);
        const uniqueIds = idsKey.split(',').filter(Boolean);
        const data = await workflowApiService.getWorkflowEntitySummariesBatch(
          uniqueIds.map((id) => ({ entityType, entityId: id }))
        );

        if (!mounted) return;
        const map: Record<string, WorkflowEntitySummaryDto> = {};
        for (const s of data || []) {
          map[String(s.entityId)] = s;
        }
        setSummariesById(map);
      } catch {
        if (!mounted) return;
        setSummariesById({});
      } finally {
        if (mounted) setLoading(false);
      }
    })();

    return () => {
      mounted = false;
    };
  }, [enabled, entityType, idsKey]);

  return { loading, summariesById };
}

