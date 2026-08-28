import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from './api.service';
import { accountsPayableService } from './accountsPayableService';
import { documentOutputService } from './document-output.service';

vi.mock('./api.service', () => ({
  apiService: {
    postBlob: vi.fn(),
  },
}));

vi.mock('./document-output.service', () => ({
  DOCUMENT_TYPES: {
    financeApAgingReport: 'Finance.AP.AgingReport',
    financeApCashRequirements: 'Finance.AP.CashRequirements',
  },
  documentOutputService: {
    downloadReportDocument: vi.fn(),
    printReportDocument: vi.fn(),
  },
}));

describe('accounts payable aging export client', () => {
  beforeEach(() => vi.clearAllMocks());

  it('posts the visible as-of date to the controlled Finance CSV export endpoint', async () => {
    const csv = new Blob(['Supplier,Total\nAcme,1250'], { type: 'text/csv' });
    vi.mocked(apiService.postBlob).mockResolvedValueOnce(csv);

    await expect(
      accountsPayableService.downloadAgingReportCsv('2026-07-31')
    ).resolves.toBe(csv);

    expect(apiService.postBlob).toHaveBeenCalledWith(
      '/finance/report-exports/export',
      {
        reportType: 'ApAging',
        format: 'Csv',
        asOfDate: '2026-07-31',
      }
    );
  });

  it('uses the same dated server-rendered artifact for PDF download and print', async () => {
    await accountsPayableService.downloadAgingReportPdf('2026-07-31');
    await accountsPayableService.printAgingReport('2026-07-31');

    expect(documentOutputService.downloadReportDocument).toHaveBeenCalledWith(
      'Finance.AP.AgingReport',
      { asOfDate: '2026-07-31' },
      { format: 'pdf' }
    );
    expect(documentOutputService.printReportDocument).toHaveBeenCalledWith(
      'Finance.AP.AgingReport',
      { asOfDate: '2026-07-31' },
      { format: 'pdf' }
    );
  });
});
