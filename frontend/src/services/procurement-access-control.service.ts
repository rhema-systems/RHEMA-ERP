import { apiService } from '@/services/api.service';
import type {
  ProcurementAccessAudit, ProcurementAccessCapabilityDecision, ProcurementAccessCapabilityRequest,
  ProcurementAccessPermission, ProcurementAccessReadiness, ProcurementAccessRole, ProcurementAccessUser,
  ProcurementAccessWarehouse, ProcurementAccessWorkflow, ProcurementCommittee, ProcurementCommitteeMember,
  ProcurementResponsibilityAssignment, SaveProcurementCommitteeMember, SaveProcurementResponsibilityAssignment,
  UpdateProcurementCommittee,
} from '@/types/procurement-access-control';

const root = '/procurement/access-controls';

export const procurementAccessControlService = {
  readiness: () => apiService.get<ProcurementAccessReadiness>(`${root}/readiness`),
  roles: () => apiService.get<ProcurementAccessRole[]>(`${root}/roles`),
  permissions: () => apiService.get<ProcurementAccessPermission[]>(`${root}/permissions`),
  users: () => apiService.get<ProcurementAccessUser[]>(`${root}/users`),
  warehouses: () => apiService.get<ProcurementAccessWarehouse[]>(`${root}/warehouses`),
  assignments: () => apiService.get<ProcurementResponsibilityAssignment[]>(`${root}/assignments`),
  saveAssignment: (id: string | undefined, request: SaveProcurementResponsibilityAssignment) => id
    ? apiService.put<ProcurementResponsibilityAssignment>(`${root}/assignments/${id}`, request)
    : apiService.post<ProcurementResponsibilityAssignment>(`${root}/assignments`, request),
  committees: () => apiService.get<ProcurementCommittee[]>(`${root}/committees`),
  updateCommittee: (id: string, request: UpdateProcurementCommittee) =>
    apiService.put<ProcurementCommittee>(`${root}/committees/${id}`, request),
  addCommitteeMember: (committeeId: string, request: SaveProcurementCommitteeMember) =>
    apiService.post<ProcurementCommitteeMember>(`${root}/committees/${committeeId}/members`, request),
  removeCommitteeMember: (committeeId: string, memberId: string, reason: string, rowVersion: string) =>
    apiService.delete<void>(`${root}/committees/${committeeId}/members/${memberId}`, { reason, rowVersion }),
  workflows: () => apiService.get<ProcurementAccessWorkflow[]>(`${root}/workflows`),
  checkCapability: (request: ProcurementAccessCapabilityRequest) =>
    apiService.post<ProcurementAccessCapabilityDecision>(`${root}/capabilities/check`, request),
  enforceCapability: (request: ProcurementAccessCapabilityRequest) =>
    apiService.post<ProcurementAccessCapabilityDecision>(`${root}/capabilities/enforce`, request),
  audit: (take = 100) => apiService.get<ProcurementAccessAudit[]>(`${root}/audit`, { take }),
};
