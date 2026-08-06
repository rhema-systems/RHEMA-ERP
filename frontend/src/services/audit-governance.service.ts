import { apiService } from '@/services/api.service';
import type {
  AuditEventCoverageReport,
  AuditLifecycleCommand,
  AuditRecordGovernance,
} from '@/types/audit-governance';

const root = '/admin/audit-governance';
const recordRoot = (storeKey: string, recordId: string) =>
  `${root}/records/${encodeURIComponent(storeKey)}/${encodeURIComponent(recordId)}`;

export const auditGovernanceService = {
  coverage: () => apiService.get<AuditEventCoverageReport>(`${root}/coverage`),
  get: (storeKey: string, recordId: string) =>
    apiService.get<AuditRecordGovernance>(recordRoot(storeKey, recordId)),
  placeLegalHold: (
    storeKey: string,
    recordId: string,
    request: AuditLifecycleCommand
  ) =>
    apiService.post<AuditRecordGovernance>(
      `${recordRoot(storeKey, recordId)}/legal-hold`,
      request
    ),
  releaseLegalHold: (
    storeKey: string,
    recordId: string,
    request: AuditLifecycleCommand
  ) =>
    apiService.post<AuditRecordGovernance>(
      `${recordRoot(storeKey, recordId)}/legal-hold/release`,
      request
    ),
  archive: (
    storeKey: string,
    recordId: string,
    request: AuditLifecycleCommand
  ) =>
    apiService.post<AuditRecordGovernance>(
      `${recordRoot(storeKey, recordId)}/archive`,
      request
    ),
  restore: (
    storeKey: string,
    recordId: string,
    request: AuditLifecycleCommand
  ) =>
    apiService.post<AuditRecordGovernance>(
      `${recordRoot(storeKey, recordId)}/restore`,
      request
    ),
};
