import { apiService } from '@/services/api.service';
import type {
  CivilEngineeringMigrationBatch,
  CivilEngineeringMigrationLookups,
  CivilEngineeringMigrationRevision,
  ReconcileCivilEngineeringMigrationBatchRequest,
  SignOffCivilEngineeringMigrationBatchRequest,
  StageCivilEngineeringMigrationBatchRequest,
} from '@/types/civil-engineering-migration';

const root = (projectId: string) => `/projects/${projectId}/civil-engineering/migration-batches`;

export const civilEngineeringMigrationService = {
  lookups: (projectId: string) => apiService.get<CivilEngineeringMigrationLookups>(`${root(projectId)}/lookups`),
  list: (projectId: string) => apiService.get<CivilEngineeringMigrationBatch[]>(root(projectId)),
  stage: (projectId: string, request: StageCivilEngineeringMigrationBatchRequest) => apiService.post<CivilEngineeringMigrationBatch>(root(projectId), request),
  reconcile: (projectId: string, batchId: string, request: ReconcileCivilEngineeringMigrationBatchRequest) => apiService.post<CivilEngineeringMigrationBatch>(`${root(projectId)}/${batchId}/reconcile`, request),
  signOff: (projectId: string, batchId: string, request: SignOffCivilEngineeringMigrationBatchRequest) => apiService.post<CivilEngineeringMigrationBatch>(`${root(projectId)}/${batchId}/sign-off`, request),
  history: (projectId: string, batchId: string) => apiService.get<CivilEngineeringMigrationRevision[]>(`${root(projectId)}/${batchId}/history`),
};
