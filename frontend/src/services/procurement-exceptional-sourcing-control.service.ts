import { apiService } from '@/services/api.service';
import type {
  PrepareExceptionalSourcingRequest,
  ProcurementExceptionalSourcingControl,
  ProcurementExceptionalSourcingReadiness,
} from '@/types/procurement-exceptional-sourcing-control';

const root = (tenderId: string) => `/procurement/tenders/${tenderId}/exception-controls`;

export const procurementExceptionalSourcingControlService = {
  readiness: (tenderId: string) =>
    apiService.get<ProcurementExceptionalSourcingReadiness>(`${root(tenderId)}/readiness`),
  get: (tenderId: string) => apiService.get<ProcurementExceptionalSourcingControl>(root(tenderId)),
  prepare: (tenderId: string, request: PrepareExceptionalSourcingRequest) =>
    apiService.post<ProcurementExceptionalSourcingControl>(`${root(tenderId)}/prepare`, request),
  submitApproval: (tenderId: string, rowVersion: string) =>
    apiService.post<ProcurementExceptionalSourcingControl>(`${root(tenderId)}/approval/submit`, { rowVersion }),
  decideApproval: (tenderId: string, request: unknown) =>
    apiService.post<ProcurementExceptionalSourcingControl>(`${root(tenderId)}/approval/decision`, request),
  negotiation: (tenderId: string, request: unknown) =>
    apiService.post<ProcurementExceptionalSourcingControl>(`${root(tenderId)}/negotiation`, request),
  recommendation: (tenderId: string, request: unknown) =>
    apiService.post<ProcurementExceptionalSourcingControl>(`${root(tenderId)}/recommendation`, request),
  award: (tenderId: string, request: unknown) =>
    apiService.post<ProcurementExceptionalSourcingControl>(`${root(tenderId)}/award`, request),
  contract: (tenderId: string, request: unknown) =>
    apiService.post<ProcurementExceptionalSourcingControl>(`${root(tenderId)}/contract`, request),
  acceptance: (tenderId: string, request: unknown) =>
    apiService.post<ProcurementExceptionalSourcingControl>(`${root(tenderId)}/acceptance`, request),
  filing: (tenderId: string, request: unknown) =>
    apiService.post<ProcurementExceptionalSourcingControl>(`${root(tenderId)}/post-award-filing`, request),
};
