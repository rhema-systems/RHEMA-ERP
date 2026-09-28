import { apiService } from '@/services/api.service';
import type {
    CreateFinancialStatementLayoutVersionDto,
    CloneFinancialStatementLayoutDto,
    FinancialStatementLayoutAuditEventDto,
    FinancialStatementLayoutApprovalQueueItemDto,
    FinancialStatementLayoutDto,
    FinancialStatementLayoutExecutionDto,
    FinancialStatementLayoutImportDefinitionDto,
    FinancialStatementLayoutImportPreviewDto,
    FinancialStatementLayoutImportResultDto,
    FinancialStatementLayoutInitializationResultDto,
    FinancialStatementLayoutReadinessDto,
    FinancialStatementLayoutSummaryDto,
    FinancialStatementLayoutValidationResultDto,
    FinancialStatementRowInputDto,
    FinancialStatementType,
    LegacyFinancialStatementLayoutMigrationRequestDto,
    PublishFinancialStatementLayoutVersionDto,
    UpdateFinancialStatementLayoutDto,
} from '@/types/finance';

const root = '/finance/financial-statement-layouts';

function download(blob: Blob, fileName: string) {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(url);
}

class FinancialStatementLayoutDataService {
    getLayouts(filters: {
        statementType?: FinancialStatementType;
        accountingBookId?: string;
        includeInactive?: boolean;
    } = {}) {
        const query = new URLSearchParams();
        if (filters.statementType) query.set('statementType', filters.statementType);
        if (filters.accountingBookId) query.set('accountingBookId', filters.accountingBookId);
        if (filters.includeInactive) query.set('includeInactive', 'true');
        const suffix = query.size ? `?${query.toString()}` : '';
        return apiService.get<FinancialStatementLayoutSummaryDto[]>(`${root}${suffix}`);
    }

    getLayout(layoutId: string) {
        return apiService.get<FinancialStatementLayoutDto>(`${root}/${layoutId}`);
    }

    updateLayout(layoutId: string, request: UpdateFinancialStatementLayoutDto) {
        return apiService.put<FinancialStatementLayoutDto>(`${root}/${layoutId}`, request);
    }

    createDraftVersion(layoutId: string, request: CreateFinancialStatementLayoutVersionDto) {
        return apiService.post<FinancialStatementLayoutDto['versions'][number]>(
            `${root}/${layoutId}/versions`,
            request,
        );
    }

    cloneLayout(layoutId: string, request: CloneFinancialStatementLayoutDto) {
        return apiService.post<FinancialStatementLayoutDto>(`${root}/${layoutId}/clone`, request);
    }

    getInitializationReadiness(accountingBookId?: string) {
        const suffix = accountingBookId
            ? `?accountingBookId=${encodeURIComponent(accountingBookId)}`
            : '';
        return apiService.get<FinancialStatementLayoutReadinessDto>(
            `${root}/initialization-readiness${suffix}`,
        );
    }

    initializeFromStandards(accountingBookId?: string) {
        return apiService.post<FinancialStatementLayoutInitializationResultDto>(
            `${root}/initialize-from-standards`,
            { accountingBookId },
        );
    }

    discardUnusedDraft(layoutId: string, expectedRevision: number, reason: string) {
        return apiService.delete<void>(`${root}/${layoutId}/unused-draft`, {
            expectedRevision,
            reason,
        });
    }

    replaceDraftRows(versionId: string, expectedVersionRevision: number, rows: FinancialStatementRowInputDto[]) {
        return apiService.put<FinancialStatementLayoutDto['versions'][number]>(
            `${root}/versions/${versionId}/rows`,
            { expectedVersionRevision, rows },
        );
    }

    validateVersion(versionId: string) {
        return apiService.post<FinancialStatementLayoutValidationResultDto>(
            `${root}/versions/${versionId}/validate`,
            {},
        );
    }

    publishVersion(versionId: string, request: PublishFinancialStatementLayoutVersionDto) {
        return apiService.post<FinancialStatementLayoutDto['versions'][number]>(
            `${root}/versions/${versionId}/publish`,
            request,
        );
    }

    submitVersion(versionId: string, expectedVersionRevision: number) {
        return apiService.post<FinancialStatementLayoutDto['versions'][number]>(
            `${root}/versions/${versionId}/submit`,
            { expectedVersionRevision },
        );
    }

    getPendingApprovals() {
        return apiService.get<FinancialStatementLayoutApprovalQueueItemDto[]>(`${root}/approval-queue`);
    }

    decideVersion(
        versionId: string,
        expectedVersionRevision: number,
        decision: 'Approve' | 'Reject',
        reason?: string,
    ) {
        return apiService.post<FinancialStatementLayoutDto['versions'][number]>(
            `${root}/versions/${versionId}/decision`,
            { expectedVersionRevision, decision, reason },
        );
    }

    previewVersion(
        versionId: string,
        request: {
            periodStart?: string;
            periodEnd: string;
            includeAccountDetails: boolean;
            includeHiddenRows: boolean;
            accountIds: string[];
            segmentFilters: unknown[];
        },
    ) {
        return apiService.post<FinancialStatementLayoutExecutionDto>(
            `${root}/versions/${versionId}/preview`,
            request,
        );
    }

    getAuditTrail(layoutId: string) {
        return apiService.get<FinancialStatementLayoutAuditEventDto[]>(
            `${root}/${layoutId}/audit-trail`,
        );
    }

    async downloadExportJson(versionId: string, fileName: string) {
        const definition = await apiService.get<FinancialStatementLayoutImportDefinitionDto>(`${root}/versions/${versionId}/exports/json`);
        download(new Blob([JSON.stringify(definition, null, 2)], { type: 'application/json' }), fileName);
    }

    async downloadExportWorkbook(versionId: string, fileName: string) {
        download(await apiService.downloadBlob(`${root}/versions/${versionId}/exports/workbook`), fileName);
    }

    async downloadImportTemplate() {
        download(
            await apiService.downloadBlob(`${root}/import-template`),
            'financial-statement-layout-import-template.xlsx',
        );
    }

    previewWorkbook(file: File) {
        const form = new FormData();
        form.append('file', file);
        return apiService.request<FinancialStatementLayoutImportPreviewDto>(
            `${root}/imports/workbook/preview`,
            { method: 'POST', body: form },
        );
    }

    commitWorkbook(file: File, expectedDefinitionHash: string) {
        const form = new FormData();
        form.append('file', file);
        form.append('expectedDefinitionHash', expectedDefinitionHash);
        return apiService.request<FinancialStatementLayoutImportResultDto>(
            `${root}/imports/workbook/commit`,
            { method: 'POST', body: form },
        );
    }

    previewJson(definition: FinancialStatementLayoutImportDefinitionDto) {
        return apiService.post<FinancialStatementLayoutImportPreviewDto>(
            `${root}/imports/json/preview`,
            definition,
        );
    }

    commitJson(preview: FinancialStatementLayoutImportPreviewDto) {
        return apiService.post<FinancialStatementLayoutImportResultDto>(
            `${root}/imports/json/commit`,
            {
                definition: preview.definition,
                expectedDefinitionHash: preview.definitionHash,
            },
        );
    }

    previewLegacyMigration(request: LegacyFinancialStatementLayoutMigrationRequestDto) {
        return apiService.post<FinancialStatementLayoutImportPreviewDto>(
            `${root}/legacy-migration/preview`,
            request,
        );
    }

    commitLegacyMigration(
        request: LegacyFinancialStatementLayoutMigrationRequestDto,
        expectedDefinitionHash: string,
    ) {
        return apiService.post<FinancialStatementLayoutImportResultDto>(
            `${root}/legacy-migration/commit`,
            { request, expectedDefinitionHash },
        );
    }
}

export const financialStatementLayoutDataService = new FinancialStatementLayoutDataService();
