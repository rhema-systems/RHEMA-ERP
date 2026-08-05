import { beforeEach, describe, expect, it, vi } from 'vitest';
import type {
    FinancialStatementLayoutImportPreviewDto,
    FinancialStatementLayoutSummaryDto,
} from '@/types/finance';
import { financialStatementLayoutDataService } from './financial-statement-layout-data.service';

const jsonResponse = (value: unknown) => new Response(JSON.stringify(value), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
});

describe('financialStatementLayoutDataService', () => {
    beforeEach(() => {
        vi.restoreAllMocks();
        localStorage.clear();
    });

    it('sends statement, book and inactive filters to the layout register endpoint', async () => {
        const response: FinancialStatementLayoutSummaryDto[] = [];
        const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValue(jsonResponse(response));

        await financialStatementLayoutDataService.getLayouts({
            statementType: 'BalanceSheet',
            accountingBookId: 'book-1',
            includeInactive: true,
        });

        expect(fetchMock).toHaveBeenCalledWith(
            expect.stringContaining(
                '/finance/financial-statement-layouts?statementType=BalanceSheet&accountingBookId=book-1&includeInactive=true',
            ),
            expect.objectContaining({
                method: 'GET',
            }),
        );
    });

    it('commits the server-normalised JSON definition with its preview hash', async () => {
        const preview: FinancialStatementLayoutImportPreviewDto = {
            definitionHash: 'ABC123',
            willCreateLayout: true,
            rowCount: 0,
            mappingCount: 0,
            definition: {
                templateVersion: '1',
                code: 'BS-TEST',
                name: 'Test Balance Sheet',
                statementType: 'BalanceSheet',
                accountingBookId: 'book-1',
                isDefault: false,
                rows: [],
            },
            validation: { isValid: true, issues: [] },
        };
        const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValue(jsonResponse({
            definitionHash: preview.definitionHash,
            createdLayout: true,
            layoutId: 'layout-1',
            draftVersionId: 'version-1',
            draftVersionNumber: 1,
            draftVersionRevision: 1,
            layout: {},
        }));

        await financialStatementLayoutDataService.commitJson(preview);

        expect(fetchMock).toHaveBeenCalledWith(
            expect.stringContaining('/finance/financial-statement-layouts/imports/json/commit'),
            expect.objectContaining({
                method: 'POST',
                body: JSON.stringify({
                    definition: preview.definition,
                    expectedDefinitionHash: preview.definitionHash,
                }),
            }),
        );
    });

    it('re-uploads the workbook and preview hash when committing a controlled import', async () => {
        const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValue(jsonResponse({
            definitionHash: 'HASH-1',
            createdLayout: true,
            layoutId: 'layout-1',
            draftVersionId: 'version-1',
            draftVersionNumber: 1,
            draftVersionRevision: 1,
            layout: {},
        }));
        const file = new File(['layout'], 'layout.xlsx', {
            type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        });

        await financialStatementLayoutDataService.commitWorkbook(file, 'HASH-1');

        const [, request] = fetchMock.mock.calls[0];
        expect(fetchMock.mock.calls[0][0]).toEqual(
            expect.stringContaining('/finance/financial-statement-layouts/imports/workbook/commit'),
        );
        expect(request).toEqual(expect.objectContaining({ method: 'POST' }));
        const body = request?.body as FormData;
        expect(body.get('file')).toBe(file);
        expect(body.get('expectedDefinitionHash')).toBe('HASH-1');
    });
});
