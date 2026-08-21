import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from './api.service';
import { accountsPayableService } from './accountsPayableService';

vi.mock('./api.service', () => ({
  apiService: {
    postBlob: vi.fn(),
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
});
