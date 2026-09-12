import { apiService } from '@/services/api.service';
import type {
  CreateProcurementPrequalificationExercise,
  ProcurementPrequalificationApplication,
  ProcurementPrequalificationExercise,
  ProcurementPrequalificationReadiness,
  ProcurementPrequalificationSummary,
  ProcurementSupplierEligibility,
} from '@/types/procurement-prequalification';

const root = '/procurement/prequalification';

export const procurementPrequalificationService = {
  list: () => apiService.get<ProcurementPrequalificationSummary[]>(root),
  readiness: () => apiService.get<ProcurementPrequalificationReadiness>(`${root}/readiness`),
  get: (exerciseId: string) => apiService.get<ProcurementPrequalificationExercise>(`${root}/${exerciseId}`),
  create: (request: CreateProcurementPrequalificationExercise) =>
    apiService.post<ProcurementPrequalificationExercise>(root, request),
  advertise: (exerciseId: string, request: { advertisementReference: string; advertisementEvidenceReference: string; rowVersion: string }) =>
    apiService.post<ProcurementPrequalificationExercise>(`${root}/${exerciseId}/advertise`, request),
  apply: (exerciseId: string, request: { businessPartnerId: string; categoryIds: string[]; evidence: Array<{ criterionCode: string; evidenceReference: string; verificationReference: string }> }) =>
    apiService.post<ProcurementPrequalificationApplication>(`${root}/${exerciseId}/applications`, request),
  close: (exerciseId: string, rowVersion: string) =>
    apiService.post<ProcurementPrequalificationExercise>(`${root}/${exerciseId}/close`, { rowVersion }),
  evaluate: (exerciseId: string, applicationId: string, request: unknown) =>
    apiService.post<ProcurementPrequalificationApplication>(`${root}/${exerciseId}/applications/${applicationId}/evaluate`, request),
  submitDecision: (exerciseId: string, rowVersion: string, decision?: {
    decisionReference?: string; decisionEvidenceReference?: string; reason?: string;
  }) => apiService.post<ProcurementPrequalificationExercise>(`${root}/${exerciseId}/decision/submit`, { ...decision, rowVersion }),
  decide: (exerciseId: string, request: unknown) =>
    apiService.post<ProcurementPrequalificationExercise>(`${root}/${exerciseId}/decision`, request),
  expire: (exerciseId: string, rowVersion: string) =>
    apiService.post<ProcurementPrequalificationExercise>(`${root}/${exerciseId}/expire`, { rowVersion }),
  eligibility: (businessPartnerId: string, categoryId: string, atUtc?: string) =>
    apiService.get<ProcurementSupplierEligibility>(`${root}/eligibility`, {
      businessPartnerId,
      categoryId,
      atUtc,
    }),
};
