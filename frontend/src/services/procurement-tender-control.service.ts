import { apiService } from '@/services/api.service';
import type {
  IssueProcurementTenderDocument,
  ProcurementTenderControl,
  ProcurementTenderDocumentIssue,
  PublishProcurementTender,
} from '@/types/procurement-tender-control';

const root = (tenderId: string) => `/procurement/tenders/${tenderId}/controls`;

export const procurementTenderControlService = {
  get: (tenderId: string) =>
    apiService.get<ProcurementTenderControl>(root(tenderId)),
  advertise: (tenderId: string, request: PublishProcurementTender) =>
    apiService.post<ProcurementTenderControl>(`${root(tenderId)}/advertise`, request),
  issueDocument: (tenderId: string, request: IssueProcurementTenderDocument) =>
    apiService.post<ProcurementTenderDocumentIssue>(`${root(tenderId)}/document-issues`, request),
  opening: (tenderId: string, request: unknown) =>
    apiService.post<ProcurementTenderControl>(`${root(tenderId)}/opening`, request),
  technicalEvaluation: (tenderId: string, request: unknown) =>
    apiService.put<ProcurementTenderControl>(`${root(tenderId)}/technical-evaluation`, request),
  financialEvaluation: (tenderId: string, request: unknown) =>
    apiService.put<ProcurementTenderControl>(`${root(tenderId)}/financial-evaluation`, request),
  submitApproval: (tenderId: string, rowVersion: string, ppaApprovalReference?: string) =>
    apiService.post<ProcurementTenderControl>(`${root(tenderId)}/approval/submit`, { rowVersion, ppaApprovalReference }),
  decideApproval: (tenderId: string, request: unknown) =>
    apiService.post<ProcurementTenderControl>(`${root(tenderId)}/approval/decision`, request),
  award: (tenderId: string, request: unknown) =>
    apiService.post<ProcurementTenderControl>(`${root(tenderId)}/award`, request),
  contract: (tenderId: string, request: unknown) =>
    apiService.post<ProcurementTenderControl>(`${root(tenderId)}/contract`, request),
  acceptance: (tenderId: string, request: unknown) =>
    apiService.post<ProcurementTenderControl>(`${root(tenderId)}/acceptance`, request),
};
