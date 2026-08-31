/**
 * Fixed Assets Data Service
 */

import { apiService } from '@/services/api.service';
import { documentOutputService } from '@/services/document-output.service';
import type {
  FixedAsset,
  FixedAssetCategory,
  CreateFixedAssetDto,
  UpdateFixedAssetDto,
  CreateFixedAssetCategoryDto,
  UpdateFixedAssetCategoryDto,
  RunDepreciationDto,
  AssetDepreciationSchedule,
  FixedAssetGlAccountOptions,
  AssetTransfer,
  RequestAssetTransferDto,
  ApproveAssetTransferDto,
  AssetDisposal,
  RequestAssetDisposalDto,
  ApproveAssetDisposalDto,
  AssetVerificationSession,
  CreateAssetVerificationSessionDto,
  AssetVerificationItem,
  VerifyAssetDto,
  CreateAssetValuationDto,
  CreateBulkAssetValuationDto,
  AssetValuation,
  BulkOperationResult,
  RequestBulkAssetDisposalDto,
  FixedAssetCapitalizationReversal,
  RequestFixedAssetCapitalizationReversalDto,
  ReviewFixedAssetCapitalizationReversalDto,
  SubmitFixedAssetCapitalizationDto,
  FixedAssetApprovalActionDto,
  FixedAssetDepreciationReversal,
  RequestFixedAssetDepreciationReversalDto,
  ReviewFixedAssetDepreciationReversalDto,
  AssetValuationCorrection,
  RequestAssetValuationCorrectionDto,
  ReviewAssetValuationCorrectionDto,
} from '@/types/fixed-assets';
import {
  FixedAssetReportQuery,
  FixedAssetRegister,
  AssetDisposalReportItem,
  AssetTransferReportItem
} from '@/types/fixed-asset-reports';

class FixedAssetsDataService {
  private buildQueryString(query: FixedAssetReportQuery): string {
    const params = new URLSearchParams();
    if (query.fromDate) params.append('fromDate', query.fromDate);
    if (query.toDate) params.append('toDate', query.toDate);
    if (query.categoryId) params.append('categoryId', query.categoryId);
    if (query.status !== undefined) params.append('status', query.status.toString());
    if (query.searchTerm) params.append('searchTerm', query.searchTerm);
    if (query.bookClassification) params.append('bookClassification', query.bookClassification);
    const qs = params.toString();
    return qs ? `?${qs}` : '';
  }

  // ===== ASSETS =====
  // ... (previous methods)

  async getAssets(): Promise<FixedAsset[]> {
    return apiService.get<FixedAsset[]>('/finance/fixed-assets');
  }

  async getAssetById(id: string): Promise<FixedAsset> {
    return apiService.get<FixedAsset>(`/finance/fixed-assets/${id}`);
  }

  async createAsset(dto: CreateFixedAssetDto): Promise<FixedAsset> {
    return apiService.post<FixedAsset>('/finance/fixed-assets', dto);
  }

  async updateAsset(id: string, dto: UpdateFixedAssetDto): Promise<FixedAsset> {
    return apiService.put<FixedAsset>(`/finance/fixed-assets/${id}`, dto);
  }

  async deleteAsset(id: string): Promise<void> {
    return apiService.delete(`/finance/fixed-assets/${id}`);
  }

  async submitCapitalizationForApproval(
    id: string,
    dto: SubmitFixedAssetCapitalizationDto
  ): Promise<FixedAsset> {
    return apiService.post<FixedAsset>(`/finance/fixed-assets/${id}/capitalization/submit`, dto);
  }

  async postApprovedCapitalization(
    id: string,
    dto: FixedAssetApprovalActionDto = {}
  ): Promise<FixedAsset> {
    return apiService.post<FixedAsset>(`/finance/fixed-assets/${id}/capitalize`, dto);
  }

  // ===== CAPITALIZATION CORRECTIONS =====
  // These methods deliberately expose the controlled request/review/post stages
  // separately. Combining them in the browser would bypass the maker-checker
  // evidence required by TDC FR-GL-008 and FR-GL-010.

  async getCapitalizationReversals(id: string): Promise<FixedAssetCapitalizationReversal[]> {
    return apiService.get<FixedAssetCapitalizationReversal[]>(
      `/finance/fixed-assets/${id}/capitalization-reversals`
    );
  }

  async requestCapitalizationReversal(
    id: string,
    dto: RequestFixedAssetCapitalizationReversalDto
  ): Promise<FixedAssetCapitalizationReversal> {
    return apiService.post<FixedAssetCapitalizationReversal>(
      `/finance/fixed-assets/${id}/capitalization-reversals`,
      dto
    );
  }

  async reviewCapitalizationReversal(
    id: string,
    requestId: string,
    dto: ReviewFixedAssetCapitalizationReversalDto
  ): Promise<FixedAssetCapitalizationReversal> {
    return apiService.post<FixedAssetCapitalizationReversal>(
      `/finance/fixed-assets/${id}/capitalization-reversals/${requestId}/review`,
      dto
    );
  }

  async postCapitalizationReversal(
    id: string,
    requestId: string
  ): Promise<FixedAssetCapitalizationReversal> {
    return apiService.post<FixedAssetCapitalizationReversal>(
      `/finance/fixed-assets/${id}/capitalization-reversals/${requestId}/post`
    );
  }

  // ===== CATEGORIES =====

  async getCategories(): Promise<FixedAssetCategory[]> {
    return apiService.get<FixedAssetCategory[]>('/finance/fixed-asset-categories');
  }

  async getCategoryById(id: string): Promise<FixedAssetCategory> {
    return apiService.get<FixedAssetCategory>(`/finance/fixed-asset-categories/${id}`);
  }

  async createCategory(dto: CreateFixedAssetCategoryDto): Promise<FixedAssetCategory> {
    return apiService.post<FixedAssetCategory>('/finance/fixed-asset-categories', dto);
  }

  async updateCategory(id: string, dto: UpdateFixedAssetCategoryDto): Promise<FixedAssetCategory> {
    return apiService.put<FixedAssetCategory>(`/finance/fixed-asset-categories/${id}`, dto);
  }

  async deleteCategory(id: string): Promise<void> {
    return apiService.delete(`/finance/fixed-asset-categories/${id}`);
  }

  // ===== DEPRECIATION =====

  async runDepreciation(dto: RunDepreciationDto): Promise<AssetDepreciationSchedule[]> {
    return apiService.post<AssetDepreciationSchedule[]>('/finance/fixed-assets/depreciation/run', dto);
  }

  async getAssetSchedule(assetId: string): Promise<AssetDepreciationSchedule[]> {
    return apiService.get<AssetDepreciationSchedule[]>(`/finance/fixed-assets/${assetId}/depreciation-schedule`);
  }

  async getPeriodSchedule(fiscalPeriodId: string): Promise<AssetDepreciationSchedule[]> {
    return apiService.get<AssetDepreciationSchedule[]>(`/finance/fixed-assets/depreciation/period/${fiscalPeriodId}`);
  }

  // A posted depreciation correction is deliberately split into request, independent review,
  // and posting calls. Keeping the stages explicit prevents the UI from presenting a destructive
  // one-click "undo" that would bypass Finance maker-checker evidence.
  async getDepreciationReversals(runId: string): Promise<FixedAssetDepreciationReversal[]> {
    return apiService.get<FixedAssetDepreciationReversal[]>(
      `/finance/fixed-assets/depreciation/runs/${runId}/reversals`
    );
  }

  async requestDepreciationReversal(
    runId: string,
    dto: RequestFixedAssetDepreciationReversalDto
  ): Promise<FixedAssetDepreciationReversal> {
    return apiService.post<FixedAssetDepreciationReversal>(
      `/finance/fixed-assets/depreciation/runs/${runId}/reversals`,
      dto
    );
  }

  async reviewDepreciationReversal(
    runId: string,
    reversalId: string,
    dto: ReviewFixedAssetDepreciationReversalDto
  ): Promise<FixedAssetDepreciationReversal> {
    return apiService.post<FixedAssetDepreciationReversal>(
      `/finance/fixed-assets/depreciation/runs/${runId}/reversals/${reversalId}/review`,
      dto
    );
  }

  async postDepreciationReversal(
    runId: string,
    reversalId: string
  ): Promise<FixedAssetDepreciationReversal> {
    return apiService.post<FixedAssetDepreciationReversal>(
      `/finance/fixed-assets/depreciation/runs/${runId}/reversals/${reversalId}/post`
    );
  }

  async getGlAccounts(): Promise<FixedAssetGlAccountOptions> {
    return apiService.get<FixedAssetGlAccountOptions>('/finance/fixed-asset-categories/gl-accounts');
  }

  // ===== TRANSFERS =====

  async getTransfers(): Promise<AssetTransfer[]> {
    return apiService.get<AssetTransfer[]>('/finance/fixed-assets/transfers');
  }

  async getTransferById(id: string): Promise<AssetTransfer> {
    return apiService.get<AssetTransfer>(`/finance/fixed-assets/transfers/${id}`);
  }

  async getAssetTransfers(assetId: string): Promise<AssetTransfer[]> {
    return apiService.get<AssetTransfer[]>(`/finance/fixed-assets/${assetId}/transfers`);
  }

  async requestTransfer(dto: RequestAssetTransferDto): Promise<AssetTransfer> {
    return apiService.post<AssetTransfer>('/finance/fixed-assets/transfers', dto);
  }

  async approveTransfer(id: string, dto: ApproveAssetTransferDto): Promise<AssetTransfer> {
    return apiService.post<AssetTransfer>(`/finance/fixed-assets/transfers/${id}/approve`, dto);
  }

  async rejectTransfer(id: string, dto: ApproveAssetTransferDto): Promise<AssetTransfer> {
    return apiService.post<AssetTransfer>(`/finance/fixed-assets/transfers/${id}/reject`, dto);
  }

  // ===== DISPOSALS =====

  async getDisposals(): Promise<AssetDisposal[]> {
    return apiService.get<AssetDisposal[]>('/finance/fixed-assets/disposals');
  }

  async getDisposalById(id: string): Promise<AssetDisposal> {
    return apiService.get<AssetDisposal>(`/finance/fixed-assets/disposals/${id}`);
  }

  async requestDisposal(dto: RequestAssetDisposalDto): Promise<AssetDisposal> {
    return apiService.post<AssetDisposal>('/finance/fixed-assets/disposals', dto);
  }

  async approveDisposal(id: string, dto: ApproveAssetDisposalDto): Promise<AssetDisposal> {
    return apiService.post<AssetDisposal>(`/finance/fixed-assets/disposals/${id}/approve`, dto);
  }

  async rejectDisposal(id: string, dto: ApproveAssetDisposalDto): Promise<AssetDisposal> {
    return apiService.post<AssetDisposal>(`/finance/fixed-assets/disposals/${id}/reject`, dto);
  }

  // ===== PHYSICAL VERIFICATION =====

  async getVerificationSessions(): Promise<AssetVerificationSession[]> {
    return apiService.get<AssetVerificationSession[]>('/finance/fixed-assets/verification/sessions');
  }

  async getVerificationSession(id: string): Promise<AssetVerificationSession> {
    return apiService.get<AssetVerificationSession>(`/finance/fixed-assets/verification/sessions/${id}`);
  }

  async createVerificationSession(dto: CreateAssetVerificationSessionDto): Promise<AssetVerificationSession> {
    return apiService.post<AssetVerificationSession>('/finance/fixed-assets/verification/sessions', dto);
  }

  async startVerificationSession(id: string): Promise<AssetVerificationSession> {
    return apiService.post<AssetVerificationSession>(`/finance/fixed-assets/verification/sessions/${id}/start`);
  }

  async completeVerificationSession(id: string): Promise<AssetVerificationSession> {
    return apiService.post<AssetVerificationSession>(`/finance/fixed-assets/verification/sessions/${id}/complete`);
  }

  async getSessionItems(id: string): Promise<AssetVerificationItem[]> {
    return apiService.get<AssetVerificationItem[]>(`/finance/fixed-assets/verification/sessions/${id}/items`);
  }

  async verifyAsset(sessionId: string, assetId: string, dto: VerifyAssetDto): Promise<AssetVerificationItem> {
    return apiService.post<AssetVerificationItem>(`/finance/fixed-assets/verification/sessions/${sessionId}/items/${assetId}/verify`, dto);
  }

  // ===== REPORTS =====

  async getAssetRegister(query: FixedAssetReportQuery): Promise<FixedAssetRegister> {
    const qs = this.buildQueryString(query);
    return apiService.get<FixedAssetRegister>(`/finance/fixed-assets/reports/register${qs}`);
  }

  async getDisposalReport(query: FixedAssetReportQuery): Promise<AssetDisposalReportItem[]> {
    const qs = this.buildQueryString(query);
    return apiService.get<AssetDisposalReportItem[]>(`/finance/fixed-assets/reports/disposals${qs}`);
  }

  async getTransferReport(query: FixedAssetReportQuery): Promise<AssetTransferReportItem[]> {
    const qs = this.buildQueryString(query);
    return apiService.get<AssetTransferReportItem[]>(`/finance/fixed-assets/reports/transfers${qs}`);
  }

  async downloadExcel(reportType: string, query: FixedAssetReportQuery): Promise<{ fileName: string; blob: Blob }> {
    const qs = this.buildQueryString(query);
    const connector = qs ? '&' : '?';
    const url = `${process.env.NEXT_PUBLIC_API_URL || '/api'}/finance/fixed-assets/reports/export/excel${qs}${connector}reportType=${reportType}`;

    const token = typeof window !== 'undefined' ? localStorage.getItem('authToken') : null;
    const response = await fetch(url, {
      headers: {
        ...(token && { 'Authorization': `Bearer ${token}` })
      }
    });

    if (!response.ok) throw new Error('Export failed');
    const blob = await response.blob();
    const fileName = `${reportType}_${new Date().toISOString().split('T')[0]}.xlsx`;
    return { fileName, blob };
  }

  async downloadPdf(reportType: string, query: FixedAssetReportQuery): Promise<{ fileName: string; blob: Blob }> {
    const qs = this.buildQueryString(query);
    const connector = qs ? '&' : '?';
    const url = `${process.env.NEXT_PUBLIC_API_URL || '/api'}/finance/fixed-assets/reports/export/pdf${qs}${connector}reportType=${reportType}`;

    const token = typeof window !== 'undefined' ? localStorage.getItem('authToken') : null;
    const response = await fetch(url, {
      headers: {
        ...(token && { 'Authorization': `Bearer ${token}` })
      }
    });

    if (!response.ok) throw new Error('Export failed');
    const blob = await response.blob();
    const fileName = `${reportType}_${new Date().toISOString().split('T')[0]}.pdf`;
    return { fileName, blob };
  }

  async printPdf(reportType: string, query: FixedAssetReportQuery): Promise<void> {
    const file = await this.downloadPdf(reportType, query);
    await documentOutputService.printRenderedFile({
      ...file,
      contentType: file.blob.type || 'application/pdf',
    });
  }

  // Bulk Import
  async bulkImportAssets(file: File, dryRun = false): Promise<import('@/types/fixed-assets').BulkImportResult> {
    const formData = new FormData();
    formData.append('file', file);

    const response = await apiService.post<import('@/types/fixed-assets').BulkImportResult>(
      `/finance/fixed-assets/bulk-import?dryRun=${dryRun}`,
      formData
    );
    return response;
  }

  async downloadImportTemplate(): Promise<void> {
    const url = `${process.env.NEXT_PUBLIC_API_URL || '/api'}/finance/fixed-assets/import-template`;

    const token = typeof window !== 'undefined' ? localStorage.getItem('authToken') : null;
    const response = await fetch(url, {
      headers: {
        ...(token && { 'Authorization': `Bearer ${token}` })
      }
    });

    if (!response.ok) throw new Error('Template download failed');

    const blob = await response.blob();
    const downloadUrl = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = downloadUrl;
    link.setAttribute('download', `FixedAsset_Import_Template_${new Date().toISOString().split('T')[0]}.xlsx`);
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(downloadUrl);
  }

  // ===== VALUATIONS / IMPAIRMENT =====

  async createValuation(dto: CreateAssetValuationDto): Promise<AssetValuation> {
    return apiService.post<AssetValuation>('/finance/fixed-assets/valuations', dto);
  }

  async createBulkValuation(dto: CreateBulkAssetValuationDto): Promise<BulkOperationResult<AssetValuation>> {
    return apiService.post<BulkOperationResult<AssetValuation>>('/finance/fixed-assets/valuations/bulk', dto);
  }

  async getAssetValuations(assetId: string): Promise<AssetValuation[]> {
    return apiService.get<AssetValuation[]>(`/finance/fixed-assets/${assetId}/valuations`);
  }

  async postValuationToGL(valuationId: string): Promise<AssetValuation> {
    return apiService.post<AssetValuation>(`/finance/fixed-assets/valuations/${valuationId}/post-to-gl`);
  }

  // Posted valuation corrections use the same explicit maker-checker stages as the
  // capitalization and depreciation correction workspaces; the browser cannot "undo" a journal.
  async getValuationCorrections(valuationId: string): Promise<AssetValuationCorrection[]> {
    return apiService.get<AssetValuationCorrection[]>(`/finance/fixed-assets/valuations/${valuationId}/corrections`);
  }

  async requestValuationCorrection(
    valuationId: string,
    dto: RequestAssetValuationCorrectionDto
  ): Promise<AssetValuationCorrection> {
    return apiService.post<AssetValuationCorrection>(`/finance/fixed-assets/valuations/${valuationId}/corrections`, dto);
  }

  async reviewValuationCorrection(
    valuationId: string,
    correctionId: string,
    dto: ReviewAssetValuationCorrectionDto
  ): Promise<AssetValuationCorrection> {
    return apiService.post<AssetValuationCorrection>(
      `/finance/fixed-assets/valuations/${valuationId}/corrections/${correctionId}/review`, dto
    );
  }

  async postValuationCorrection(valuationId: string, correctionId: string): Promise<AssetValuationCorrection> {
    return apiService.post<AssetValuationCorrection>(
      `/finance/fixed-assets/valuations/${valuationId}/corrections/${correctionId}/post`
    );
  }

  // ===== LIFECYCLE MANAGEMENT =====

  async activateAsset(id: string, placedInServiceDate?: string): Promise<FixedAsset> {
    const qs = placedInServiceDate ? `?placedInServiceDate=${placedInServiceDate}` : '';
    return apiService.post<FixedAsset>(`/finance/fixed-assets/${id}/activate${qs}`);
  }

  async holdAsset(id: string, reason?: string): Promise<FixedAsset> {
    const qs = reason ? `?reason=${encodeURIComponent(reason)}` : '';
    return apiService.post<FixedAsset>(`/finance/fixed-assets/${id}/hold${qs}`);
  }

  async resumeAsset(id: string): Promise<FixedAsset> {
    return apiService.post<FixedAsset>(`/finance/fixed-assets/${id}/resume`);
  }

  // ===== BULK / COMPLETE DISPOSALS =====

  async requestBulkDisposal(dto: RequestBulkAssetDisposalDto): Promise<BulkOperationResult<AssetDisposal>> {
    return apiService.post<BulkOperationResult<AssetDisposal>>('/finance/fixed-assets/disposals/bulk', dto);
  }

  async completeDisposal(disposalId: string): Promise<AssetDisposal> {
    return apiService.post<AssetDisposal>(`/finance/fixed-assets/disposals/${disposalId}/complete`);
  }
}

export const fixedAssetsDataService = new FixedAssetsDataService();
