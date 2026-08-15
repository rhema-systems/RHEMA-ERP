import { apiService } from '../api.service';
import { hrDocumentService } from './hr-document.service';
import type {
  SheEnvironmentalPermit,
  SheEnvironmentalPermitSummary,
  SheEnvironmentalPermitType,
  SheEnvironmentalPermitStatus,
  SheEnvironmentalPermitCreateRequest,
  SheEnvironmentalPermitUpdateRequest,
  SheEnvironmentalPermitRenewRequest,
  SheEnvironmentalMonitoringSchedule,
  SheEnvironmentalMonitoringScheduleCreateRequest,
  SheEnvironmentalMonitoringScheduleUpdateRequest,
  SheMonitoringScheduleCompleteRequest,
  SheRegulatoryUpdate,
  SheRegulatoryUpdateSummary,
  SheRegulatoryUpdateStatus,
  SheRegulatoryUpdateCreateRequest,
  SheRegulatoryUpdateUpdateRequest,
  SheRegulatoryUpdateCloseRequest,
  SheSustainabilityInitiative,
  SheSustainabilityCategory,
  SheSustainabilityStatus,
  SheSustainabilityInitiativeCreateRequest,
  SheSustainabilityInitiativeUpdateRequest,
  SheSustainabilityKpis,
  SheEnvironmentalReview,
  SheEnvironmentalReviewSummary,
  SheEnvironmentalReviewStatus,
  SheEnvironmentalReviewCreateRequest,
  SheEnvironmentalReviewUpdateRequest,
  SheEnvironmentalScreeningRequest,
  SheEpaSubmissionRequest,
  SheEnvironmentalClearanceReport,
  SheMonthlyEnvironmentalReport,
  SheMonthlyEnvironmentalReportSummary,
} from '@/types/hr/safety-environment-compliance';
import type { SheEnvironmentalMonitoringType } from '@/types/hr/safety-environment';

/**
 * Part D environmental core (slice 17). Backend route: api/safety/environmental.
 * HR-only registers: permits & licences (FR-ENV-017–019, documents on the
 * central DMS through the controlled-upload gate), monitoring schedules
 * (FR-ENV-023–024), regulatory updates (FR-ENV-030–032), sustainability
 * (FR-ENV-028–029), compliance reviews & clearance (FR-ENV-001–016) and the
 * monthly environmental report (FR-ENV-033–034).
 */
class SafetyEnvironmentalComplianceService {
  private readonly baseUrl = '/safety/environmental';

  // ── Permit & licence register ──
  getPermits(
    status?: SheEnvironmentalPermitStatus,
    type?: SheEnvironmentalPermitType,
    search?: string,
    expiringInDays?: number,
  ): Promise<SheEnvironmentalPermitSummary[]> {
    const params = new URLSearchParams();
    if (status) params.set('status', status);
    if (type) params.set('type', type);
    if (search) params.set('search', search);
    if (expiringInDays != null) params.set('expiringInDays', String(expiringInDays));
    const query = params.toString();
    return apiService.get<SheEnvironmentalPermitSummary[]>(
      query ? `${this.baseUrl}/permits?${query}` : `${this.baseUrl}/permits`,
    );
  }

  getPermit(id: string): Promise<SheEnvironmentalPermit> {
    return apiService.get<SheEnvironmentalPermit>(`${this.baseUrl}/permits/${id}`);
  }

  createPermit(data: SheEnvironmentalPermitCreateRequest): Promise<SheEnvironmentalPermit> {
    return apiService.post<SheEnvironmentalPermit>(`${this.baseUrl}/permits`, data);
  }

  updatePermit(id: string, data: SheEnvironmentalPermitUpdateRequest): Promise<SheEnvironmentalPermit> {
    return apiService.put<SheEnvironmentalPermit>(`${this.baseUrl}/permits/${id}`, data);
  }

  /** Rows without an uploaded document only — a documented permit archives instead. */
  removePermit(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/permits/${id}`);
  }

  markPermitRenewal(id: string): Promise<SheEnvironmentalPermit> {
    return apiService.post<SheEnvironmentalPermit>(`${this.baseUrl}/permits/${id}/mark-renewal`, {});
  }

  renewPermit(id: string, data: SheEnvironmentalPermitRenewRequest): Promise<SheEnvironmentalPermit> {
    return apiService.post<SheEnvironmentalPermit>(`${this.baseUrl}/permits/${id}/renew`, data);
  }

  suspendPermit(id: string): Promise<SheEnvironmentalPermit> {
    return apiService.post<SheEnvironmentalPermit>(`${this.baseUrl}/permits/${id}/suspend`, {});
  }

  archivePermit(id: string): Promise<SheEnvironmentalPermit> {
    return apiService.post<SheEnvironmentalPermit>(`${this.baseUrl}/permits/${id}/archive`, {});
  }

  /** Uploads the permit document through the controlled upload gate (multipart). */
  uploadPermitDocument(id: string, file: File, changeSummary?: string): Promise<SheEnvironmentalPermit> {
    return hrDocumentService.upload<SheEnvironmentalPermit>(
      `${this.baseUrl}/permits/${id}/document`,
      file,
      { changeSummary },
    );
  }

  /** Streams a permit document version's bytes as a browser download. */
  downloadPermitDocument(id: string, versionId: string, fileName: string): Promise<void> {
    return hrDocumentService.download(
      `${this.baseUrl}/permits/${id}/document/${versionId}/download`,
      fileName,
    );
  }

  // ── Monitoring schedules ──
  getSchedules(
    activeOnly?: boolean,
    type?: SheEnvironmentalMonitoringType,
    dueInDays?: number,
  ): Promise<SheEnvironmentalMonitoringSchedule[]> {
    const params = new URLSearchParams();
    if (activeOnly != null) params.set('activeOnly', String(activeOnly));
    if (type) params.set('type', type);
    if (dueInDays != null) params.set('dueInDays', String(dueInDays));
    const query = params.toString();
    return apiService.get<SheEnvironmentalMonitoringSchedule[]>(
      query ? `${this.baseUrl}/monitoring/schedules?${query}` : `${this.baseUrl}/monitoring/schedules`,
    );
  }

  getSchedule(id: string): Promise<SheEnvironmentalMonitoringSchedule> {
    return apiService.get<SheEnvironmentalMonitoringSchedule>(`${this.baseUrl}/monitoring/schedules/${id}`);
  }

  createSchedule(data: SheEnvironmentalMonitoringScheduleCreateRequest): Promise<SheEnvironmentalMonitoringSchedule> {
    return apiService.post<SheEnvironmentalMonitoringSchedule>(`${this.baseUrl}/monitoring/schedules`, data);
  }

  updateSchedule(id: string, data: SheEnvironmentalMonitoringScheduleUpdateRequest): Promise<SheEnvironmentalMonitoringSchedule> {
    return apiService.put<SheEnvironmentalMonitoringSchedule>(`${this.baseUrl}/monitoring/schedules/${id}`, data);
  }

  /** Schedules without linked records only — one with history deactivates instead. */
  removeSchedule(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/monitoring/schedules/${id}`);
  }

  /** Records a completed cycle and advances the next due date by the interval. */
  completeScheduleCycle(id: string, data: SheMonitoringScheduleCompleteRequest): Promise<SheEnvironmentalMonitoringSchedule> {
    return apiService.post<SheEnvironmentalMonitoringSchedule>(`${this.baseUrl}/monitoring/schedules/${id}/complete`, data);
  }

  // ── Regulatory updates register ──
  getRegulatoryUpdates(
    status?: SheRegulatoryUpdateStatus,
    domain?: string,
    search?: string,
  ): Promise<SheRegulatoryUpdateSummary[]> {
    const params = new URLSearchParams();
    if (status) params.set('status', status);
    if (domain) params.set('domain', domain);
    if (search) params.set('search', search);
    const query = params.toString();
    return apiService.get<SheRegulatoryUpdateSummary[]>(
      query ? `${this.baseUrl}/regulatory-updates?${query}` : `${this.baseUrl}/regulatory-updates`,
    );
  }

  getRegulatoryUpdate(id: string): Promise<SheRegulatoryUpdate> {
    return apiService.get<SheRegulatoryUpdate>(`${this.baseUrl}/regulatory-updates/${id}`);
  }

  createRegulatoryUpdate(data: SheRegulatoryUpdateCreateRequest): Promise<SheRegulatoryUpdate> {
    return apiService.post<SheRegulatoryUpdate>(`${this.baseUrl}/regulatory-updates`, data);
  }

  updateRegulatoryUpdate(id: string, data: SheRegulatoryUpdateUpdateRequest): Promise<SheRegulatoryUpdate> {
    return apiService.put<SheRegulatoryUpdate>(`${this.baseUrl}/regulatory-updates/${id}`, data);
  }

  /** Undecided rows only — closed or management-communicated updates are history. */
  removeRegulatoryUpdate(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/regulatory-updates/${id}`);
  }

  /** FR-ENV-031 — one-shot management notification through the escalated topic. */
  notifyManagement(id: string): Promise<SheRegulatoryUpdate> {
    return apiService.post<SheRegulatoryUpdate>(`${this.baseUrl}/regulatory-updates/${id}/notify-management`, {});
  }

  closeRegulatoryUpdate(id: string, data: SheRegulatoryUpdateCloseRequest): Promise<SheRegulatoryUpdate> {
    return apiService.post<SheRegulatoryUpdate>(`${this.baseUrl}/regulatory-updates/${id}/close`, data);
  }

  // ── Sustainability initiatives ──
  getInitiatives(
    category?: SheSustainabilityCategory,
    status?: SheSustainabilityStatus,
  ): Promise<SheSustainabilityInitiative[]> {
    const params = new URLSearchParams();
    if (category) params.set('category', category);
    if (status) params.set('status', status);
    const query = params.toString();
    return apiService.get<SheSustainabilityInitiative[]>(
      query ? `${this.baseUrl}/sustainability?${query}` : `${this.baseUrl}/sustainability`,
    );
  }

  getInitiative(id: string): Promise<SheSustainabilityInitiative> {
    return apiService.get<SheSustainabilityInitiative>(`${this.baseUrl}/sustainability/${id}`);
  }

  getSustainabilityKpis(year?: number): Promise<SheSustainabilityKpis> {
    return apiService.get<SheSustainabilityKpis>(
      year != null ? `${this.baseUrl}/sustainability/kpis?year=${year}` : `${this.baseUrl}/sustainability/kpis`,
    );
  }

  createInitiative(data: SheSustainabilityInitiativeCreateRequest): Promise<SheSustainabilityInitiative> {
    return apiService.post<SheSustainabilityInitiative>(`${this.baseUrl}/sustainability`, data);
  }

  updateInitiative(id: string, data: SheSustainabilityInitiativeUpdateRequest): Promise<SheSustainabilityInitiative> {
    return apiService.put<SheSustainabilityInitiative>(`${this.baseUrl}/sustainability/${id}`, data);
  }

  removeInitiative(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/sustainability/${id}`);
  }

  // ── Compliance reviews & clearance ──
  getReviews(status?: SheEnvironmentalReviewStatus, search?: string): Promise<SheEnvironmentalReviewSummary[]> {
    const params = new URLSearchParams();
    if (status) params.set('status', status);
    if (search) params.set('search', search);
    const query = params.toString();
    return apiService.get<SheEnvironmentalReviewSummary[]>(
      query ? `${this.baseUrl}/reviews?${query}` : `${this.baseUrl}/reviews`,
    );
  }

  getReview(id: string): Promise<SheEnvironmentalReview> {
    return apiService.get<SheEnvironmentalReview>(`${this.baseUrl}/reviews/${id}`);
  }

  createReview(data: SheEnvironmentalReviewCreateRequest): Promise<SheEnvironmentalReview> {
    return apiService.post<SheEnvironmentalReview>(`${this.baseUrl}/reviews`, data);
  }

  /** Pre-decision edits only. */
  updateReview(id: string, data: SheEnvironmentalReviewUpdateRequest): Promise<SheEnvironmentalReview> {
    return apiService.put<SheEnvironmentalReview>(`${this.baseUrl}/reviews/${id}`, data);
  }

  /** Undecided reviews only — a decided review is compliance archive. */
  removeReview(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/reviews/${id}`);
  }

  recordScreening(id: string, data: SheEnvironmentalScreeningRequest): Promise<SheEnvironmentalReview> {
    return apiService.post<SheEnvironmentalReview>(`${this.baseUrl}/reviews/${id}/screening`, data);
  }

  requestCorrections(id: string, comments: string): Promise<SheEnvironmentalReview> {
    return apiService.post<SheEnvironmentalReview>(`${this.baseUrl}/reviews/${id}/request-corrections`, { comments });
  }

  /** Refused until the screening determination has been recorded. */
  approveReview(id: string, comments?: string | null): Promise<SheEnvironmentalReview> {
    return apiService.post<SheEnvironmentalReview>(`${this.baseUrl}/reviews/${id}/approve`, { comments: comments ?? null });
  }

  rejectReview(id: string, comments?: string | null): Promise<SheEnvironmentalReview> {
    return apiService.post<SheEnvironmentalReview>(`${this.baseUrl}/reviews/${id}/reject`, { comments: comments ?? null });
  }

  managementApprove(id: string, comments?: string | null): Promise<SheEnvironmentalReview> {
    return apiService.post<SheEnvironmentalReview>(`${this.baseUrl}/reviews/${id}/management-approve`, { comments: comments ?? null });
  }

  recordEpaSubmission(id: string, data: SheEpaSubmissionRequest): Promise<SheEnvironmentalReview> {
    return apiService.post<SheEnvironmentalReview>(`${this.baseUrl}/reviews/${id}/record-epa-submission`, data);
  }

  /** FR-ENV-016 — refused until every screening-determined gate is satisfied. */
  issueClearance(id: string, comments?: string | null): Promise<SheEnvironmentalReview> {
    return apiService.post<SheEnvironmentalReview>(`${this.baseUrl}/reviews/${id}/issue-clearance`, { comments: comments ?? null });
  }

  approveCommencement(id: string, comments?: string | null): Promise<SheEnvironmentalReview> {
    return apiService.post<SheEnvironmentalReview>(`${this.baseUrl}/reviews/${id}/approve-commencement`, { comments: comments ?? null });
  }

  getClearanceReport(id: string): Promise<SheEnvironmentalClearanceReport> {
    return apiService.get<SheEnvironmentalClearanceReport>(`${this.baseUrl}/reviews/${id}/clearance-report`);
  }

  // ── Monthly environmental reports ──
  getMonthlyReports(year?: number): Promise<SheMonthlyEnvironmentalReportSummary[]> {
    return apiService.get<SheMonthlyEnvironmentalReportSummary[]>(
      year != null ? `${this.baseUrl}/monthly-reports?year=${year}` : `${this.baseUrl}/monthly-reports`,
    );
  }

  getMonthlyReport(id: string): Promise<SheMonthlyEnvironmentalReport> {
    return apiService.get<SheMonthlyEnvironmentalReport>(`${this.baseUrl}/monthly-reports/${id}`);
  }

  /** Generates (or regenerates) a period's report — refused once submitted. */
  generateMonthlyReport(year: number, month: number): Promise<SheMonthlyEnvironmentalReport> {
    return apiService.post<SheMonthlyEnvironmentalReport>(`${this.baseUrl}/monthly-reports/generate`, { year, month });
  }

  updateMonthlyReportSummary(id: string, officerSummary?: string | null): Promise<SheMonthlyEnvironmentalReport> {
    return apiService.put<SheMonthlyEnvironmentalReport>(`${this.baseUrl}/monthly-reports/${id}/summary`, {
      officerSummary: officerSummary ?? null,
    });
  }

  /** FR-ENV-034 — submits to management and freezes the report as history. */
  submitMonthlyReport(id: string): Promise<SheMonthlyEnvironmentalReport> {
    return apiService.post<SheMonthlyEnvironmentalReport>(`${this.baseUrl}/monthly-reports/${id}/submit`, {});
  }
}

export const safetyEnvironmentalComplianceService = new SafetyEnvironmentalComplianceService();
export default safetyEnvironmentalComplianceService;
