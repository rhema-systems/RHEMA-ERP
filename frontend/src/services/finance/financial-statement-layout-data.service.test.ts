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
                templateVersion: '2',
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

    it('uses the governed clone and optimistic draft-row endpoints', async () => {
        const fetchMock = vi.spyOn(global, 'fetch').mockImplementation(async () => jsonResponse({}));

        await financialStatementLayoutDataService.cloneLayout('standard-1', {
            code: 'BS-TENANT', name: 'Tenant Balance Sheet', accountingBookId: 'book-1',
        });
        await financialStatementLayoutDataService.replaceDraftRows('version-1', 7, [{
            rowCode: 'CASH', label: 'Cash', rowType: 'Account', displayOrder: 10,
            signMultiplier: 1, isVisible: true, suppressIfZero: false,
            showAccountDetails: false, isBold: false, isItalic: false, isUnderlined: false,
            indentLevel: 0, mappings: [{ mappingType: 'Classification', accountClassificationCode: 'CASH', includeClassificationDescendants: true }],
        }]);

        expect(fetchMock.mock.calls[0][0]).toEqual(expect.stringContaining('/standard-1/clone'));
        expect(fetchMock.mock.calls[1][0]).toEqual(expect.stringContaining('/versions/version-1/rows'));
        expect(fetchMock.mock.calls[1][1]).toEqual(expect.objectContaining({
            method: 'PUT', body: expect.stringContaining('"expectedVersionRevision":7'),
        }));
    });

    it('uses governed initialization, readiness and unused-Draft discard endpoints', async () => {
        const fetchMock = vi.spyOn(global, 'fetch').mockImplementation(async () => jsonResponse({
            isReady: false,
            books: [],
            createdCount: 0,
            items: [],
            readiness: { isReady: false, books: [] },
        }));

        await financialStatementLayoutDataService.getInitializationReadiness('book-1');
        await financialStatementLayoutDataService.initializeFromStandards('book-1');
        await financialStatementLayoutDataService.discardUnusedDraft(
            'layout-1',
            3,
            'Accidental duplicate.',
        );

        expect(fetchMock.mock.calls[0][0]).toEqual(expect.stringContaining(
            '/initialization-readiness?accountingBookId=book-1',
        ));
        expect(fetchMock.mock.calls[1][0]).toEqual(expect.stringContaining(
            '/initialize-from-standards',
        ));
        expect(fetchMock.mock.calls[1][1]).toEqual(expect.objectContaining({
            method: 'POST',
            body: JSON.stringify({ accountingBookId: 'book-1' }),
        }));
        expect(fetchMock.mock.calls[2][0]).toEqual(expect.stringContaining(
            '/layout-1/unused-draft',
        ));
        expect(fetchMock.mock.calls[2][1]).toEqual(expect.objectContaining({
            method: 'DELETE',
            body: JSON.stringify({
                expectedRevision: 3,
                reason: 'Accidental duplicate.',
            }),
        }));
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
