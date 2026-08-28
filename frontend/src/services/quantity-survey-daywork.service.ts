import { apiService } from '@/services/api.service';

export type DayworkStatus = 'Draft' | 'ContractorSigned' | 'Verified' | 'Rejected';
export type DayworkLineType = 'Labour' | 'Material' | 'Plant';
export interface DayworkVariation { id: string; reference: string; title: string; type: string; contractId: string; contractNumber: string; contractorId: string; contractor: string; currency: string; valuedAmount: number; status: string; }
export interface DayworkRate { id: string; itemId: string; code: string; name: string; lineType: DayworkLineType; unitOfMeasureId: string; unit: string; unitRate: number; currency: string; effectiveFrom: string; effectiveTo?: string | null; }
export interface DayworkLine { id: string; lineType: DayworkLineType; rateLibraryRateId: string; code: string; name: string; unit: string; quantity: number; unitRate: number; amount: number; note?: string | null; }
export interface DayworkEvidence { id: string; title: string; fileName: string; fileSize: number; checksumSha256: string; }
export interface DayworkSheet { id: string; projectId: string; variationOrderId: string; variationReference: string; variationType: string; sheetNumber: string; workDate: string; workLocation: string; description: string; status: DayworkStatus; currency: string; totalAmount: number; contractor: string; contractorSignedAt?: string | null; verifiedAt?: string | null; verificationNote?: string | null; rejectionReason?: string | null; certificateEligible: boolean; rowVersion: string; lines: DayworkLine[]; evidence: DayworkEvidence[]; }
export interface DayworkWorkspace { variations: DayworkVariation[]; rates: DayworkRate[]; sheets: DayworkSheet[]; }
export interface DayworkAction { clientRequestId: string; rowVersion: string; reason: string; }

const root = '/quantity-survey/dayworks';
const externalRoot = (projectId: string) => `/projects/external/my-projects/${projectId}/dayworks`;
export const quantitySurveyDayworkService = {
  workspace: (projectId: string, external = false) => apiService.get<DayworkWorkspace>(external ? externalRoot(projectId) : root, external ? undefined : { projectId }),
  saveExternal: (projectId: string, request: object) => apiService.put<DayworkSheet>(externalRoot(projectId), request),
  uploadEvidence: (projectId: string, id: string, clientRequestId: string, title: string, file: File, external = false) => {
    const form = new FormData(); form.append('clientRequestId', clientRequestId); form.append('title', title); form.append('file', file, file.name);
    return apiService.post<DayworkEvidence>(external ? `${externalRoot(projectId)}/${id}/evidence` : `${root}/${id}/evidence`, form);
  },
  evidenceContent: (projectId: string, id: string, evidenceId: string, external = false) => apiService.downloadBlob(external ? `${externalRoot(projectId)}/${id}/evidence/${evidenceId}/content` : `${root}/${id}/evidence/${evidenceId}/content`),
  signExternal: (projectId: string, id: string, request: DayworkAction) => apiService.post<DayworkSheet>(`${externalRoot(projectId)}/${id}/sign`, request),
  verify: (id: string, accept: boolean, request: DayworkAction) => apiService.post<DayworkSheet>(`${root}/${id}/${accept ? 'verify' : 'reject'}`, request),
};
