import { apiService } from '@/services/api.service';
import type {
  EvaluateProcurementAwardReadinessRequest,
  ProcurementAwardReadinessDecision,
  ProcurementAwardReadinessSourceType,
  ProcurementEvaluatorAwardApproverSodStatus,
} from '@/types/procurement-award-readiness';

const root = '/procurement/award-readiness';

const sourceQuery = (
  sourceType: ProcurementAwardReadinessSourceType,
  sourceId: string
) => ({ sourceType, sourceId });

export const procurementAwardReadinessService = {
  sodStatus: (
    sourceType: ProcurementAwardReadinessSourceType,
    sourceId: string
  ) =>
    apiService.get<ProcurementEvaluatorAwardApproverSodStatus>(
      `${root}/sod-status`,
      sourceQuery(sourceType, sourceId)
    ),
  latest: (
    sourceType: ProcurementAwardReadinessSourceType,
    sourceId: string
  ) =>
    apiService.silentGet<ProcurementAwardReadinessDecision>(
      `${root}/latest`,
      sourceQuery(sourceType, sourceId)
    ),
  history: (
    sourceType: ProcurementAwardReadinessSourceType,
    sourceId: string,
    take = 50
  ) =>
    apiService.get<ProcurementAwardReadinessDecision[]>(
      `${root}/history`,
      { ...sourceQuery(sourceType, sourceId), take }
    ),
  evaluate: (
    sourceType: ProcurementAwardReadinessSourceType,
    sourceId: string,
    request: EvaluateProcurementAwardReadinessRequest
  ) =>
    apiService.post<ProcurementAwardReadinessDecision>(
      `${root}/evaluate?${new URLSearchParams(
        sourceQuery(sourceType, sourceId)
      ).toString()}`,
      request
    ),
};
