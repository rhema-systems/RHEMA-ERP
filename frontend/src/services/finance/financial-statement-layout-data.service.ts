import { apiService } from '@/services/api.service';
import type {
    CreateFinancialStatementLayoutVersionDto,
    FinancialStatementLayoutAuditEventDto,
    FinancialStatementLayoutDto,
    FinancialStatementLayoutExecutionDto,
    FinancialStatementLayoutImportDefinitionDto,
    FinancialStatementLayoutImportPreviewDto,
    FinancialStatementLayoutImportResultDto,
    FinancialStatementLayoutSummaryDto,
    FinancialStatementLayoutValidationResultDto,
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
