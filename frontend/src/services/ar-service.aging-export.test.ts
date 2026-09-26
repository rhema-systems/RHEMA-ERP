import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from './api.service';
import { arService } from './ar-service';
import { documentOutputService } from './document-output.service';

vi.mock('./api.service', () => ({
    apiService: {
        get: vi.fn(),
        postBlob: vi.fn(),
    },
}));

vi.mock('./document-output.service', () => ({
    DOCUMENT_TYPES: {
        financeArAgingReport: 'Finance.AR.AgingReport',
        financeArCustomerStatement: 'Finance.AR.CustomerStatement',
    },
    documentOutputService: {
        downloadReportDocument: vi.fn(),
        printReportDocument: vi.fn(),
    },
}));

describe('accounts receivable aging export client', () => {
    beforeEach(() => vi.clearAllMocks());

    it('posts the visible as-of date to the controlled Finance CSV export endpoint', async () => {
        const csv = new Blob(['Customer,Total\nAcme,1250'], { type: 'text/csv' });
        vi.mocked(apiService.postBlob).mockResolvedValueOnce(csv);

        await expect(arService.downloadAgingReportCsv('2026-07-31')).resolves.toBe(csv);

        expect(apiService.postBlob).toHaveBeenCalledWith(
            '/finance/report-exports/export',
            {
                reportType: 'ArAging',
                format: 'Csv',
                asOfDate: '2026-07-31',
            }
        );
    });

    it('uses the same dated server-rendered artifact for PDF download and print', async () => {
        await arService.downloadAgingReportPdf('2026-07-31');
        await arService.printAgingReport('2026-07-31');

        expect(documentOutputService.downloadReportDocument).toHaveBeenCalledWith(
            'Finance.AR.AgingReport',
            { asOfDate: '2026-07-31' },
            { format: 'pdf' }
        );
        expect(documentOutputService.printReportDocument).toHaveBeenCalledWith(
            'Finance.AR.AgingReport',
            { asOfDate: '2026-07-31' },
            { format: 'pdf' }
        );
    });

    it('uses canonical Business Partner filters for the customer detailed ledger', async () => {
        vi.mocked(apiService.get).mockResolvedValueOnce({ customers: [] });

        await arService.getCustomerDetailedLedger({
            fromDate: '2026-07-01',
            toDate: '2026-07-31',
            businessPartnerIds: ['partner-1'],
            showCustomerCurrency: true,
        });

        expect(apiService.get).toHaveBeenCalledWith(expect.stringContaining('businessPartnerIds=partner-1'));
        expect(apiService.get).toHaveBeenCalledWith(expect.not.stringContaining('customerIds='));
    });
});
