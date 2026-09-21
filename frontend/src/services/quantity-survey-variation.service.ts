import { apiService } from '@/services/api.service';

export type VariationSourceType = 'siteInstruction' | 'changeRequest' | 'directVariation' | 'changeOrder';
export interface VariationContract { id: string; number: string; title: string; contractorId: string; contractor: string; contractSum: number; currency: string; }
export interface VariationSource { id: string; reference: string; title: string; status: string; costImpact?: number | null; scheduleImpactDays?: number | null; }
export interface VariationBoqLine { id: string; versionId: string; lineKey: string; reference: string; description: string; unit?: string | null; quantity: number; unitRate: number; currency: string; }
export interface VariationLine { id: string; projectBoqVersionLineId: string; reference: string; description: string; unit?: string | null; quantityChange: number; unitRate: number; amount: number; valuationReason: string; }
export interface VariationEvidence { id: string; title: string; fileName: string; contentType: string; fileSize: number; checksumSha256: string; centralDocumentRecordId: string; centralDocumentVersionId: string; }
export interface GovernedVariation { id: string; projectId: string; contractId: string; referenceNumber: string; title: string; variationType: string; sourceType: VariationSourceType; siteInstructionId?: string | null; changeRequestId?: string | null; status: string; approvalStatus: string; contractNumber: string; contractorName: string; currency: string; valuedAmount: number; approvedAmount?: number | null; originalContractSum: number; revisedContractSum?: number | null; downstreamApplicationStatus: string; contractAmendmentId?: string | null; revisedBoqVersionId?: string | null; revisedBoqVersionNumber?: number | null; revisedBoqStatus?: string | null; budgetRevisionId?: string | null; forecastVersionId?: string | null; appliedAt?: string | null; certificateEligible: boolean; scheduleImpactDays: number; workflowInstanceId?: string | null; rejectionReason?: string | null; rowVersion: string; lines: VariationLine[]; evidence: VariationEvidence[]; }
export interface VariationWorkspace { contracts: VariationContract[]; siteInstructions: VariationSource[]; changeRequests: VariationSource[]; boqLines: VariationBoqLine[]; variations: GovernedVariation[]; }
export interface SaveVariationLine { projectBoqVersionLineId: string; quantityChange: number; unitRate?: number | null; valuationReason: string; }
export interface VariationAction { clientRequestId: string; rowVersion: string; reason: string; }

const root = '/quantity-survey/variations';
const normalizeVariation = (value: GovernedVariation): GovernedVariation => ({
  ...value,
  // ASP.NET emits enum names in PascalCase; selectors use the request's camelCase values.
  sourceType: (value.sourceType.charAt(0).toLowerCase() + value.sourceType.slice(1)) as VariationSourceType,
});
export const quantitySurveyVariationService = {
  workspace: async (projectId: string) => {
    const value = await apiService.get<VariationWorkspace>(root, { projectId });
    return { ...value, variations: value.variations.map(normalizeVariation) };
  },
  save: (projectId: string, request: { id?: string | null; clientRequestId: string; contractId: string; approvedBoqVersionId: string; sourceType: VariationSourceType; siteInstructionId?: string | null; changeRequestId?: string | null; title: string; reason: string; variationType: string; scheduleImpactDays: number; rowVersion?: string | null; lines: SaveVariationLine[] }) => apiService.put<GovernedVariation>(`${root}?projectId=${encodeURIComponent(projectId)}`, request),
  uploadEvidence: (id: string, clientRequestId: string, title: string, file: File) => { const data = new FormData(); data.append('clientRequestId', clientRequestId); data.append('title', title); data.append('file', file); return apiService.post<VariationEvidence>(`${root}/${id}/evidence`, data); },
  submit: (id: string, request: VariationAction) => apiService.post<GovernedVariation>(`${root}/${id}/submit`, request),
  approve: (id: string, request: VariationAction) => apiService.post<GovernedVariation>(`${root}/${id}/approve`, request),
  reject: (id: string, request: VariationAction) => apiService.post<GovernedVariation>(`${root}/${id}/reject`, request),
  apply: (id: string, request: VariationAction) => apiService.post<GovernedVariation>(`${root}/${id}/apply`, request),
};
