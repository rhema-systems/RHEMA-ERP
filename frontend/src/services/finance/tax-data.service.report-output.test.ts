import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from '@/services/api.service';
import { taxDataService } from './tax-data.service';
import { DOCUMENT_TYPES } from '@/services/document-output.service';

vi.mock('@/services/api.service', () => ({
  apiService: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

describe('Finance Tax report output contracts', () => {
  beforeEach(() => vi.clearAllMocks());

  it('groups the canonical posted WHT-payable report without returning to the legacy AP summary', async () => {
    vi.mocked(apiService.post).mockResolvedValueOnce({
      lines: [
        {
          sourceDocumentId: 'payment-1',
          counterpartyName: 'Tema Engineering Services Ltd',
          taxCode: 'WHT-SERVICES',
          taxRate: 7.5,
          taxableBase: 50_000,
          withholdingAmount: 3_750,
        },
        {
          sourceDocumentId: 'payment-2',
          counterpartyName: 'Tema Engineering Services Ltd',
          taxCode: 'WHT-SERVICES',
          taxRate: 7.5,
          taxableBase: 20_000,
          withholdingAmount: 1_500,
        },
      ],
    });

    const result = await taxDataService.getWHTSummary('2025-01-01', '2025-01-31');

    expect(apiService.post).toHaveBeenCalledWith('/finance/tax-reports/wht-payable', {
      fromDate: '2025-01-01',
      toDate: '2025-01-31',
    });
    expect(apiService.get).not.toHaveBeenCalled();
    expect(result).toEqual([
      expect.objectContaining({
        supplierName: 'Tema Engineering Services Ltd',
        taxType: 'WHT-SERVICES',
        transactionCount: 2,
        grossAmount: 70_000,
        whtAmount: 5_250,
        netAmount: 64_750,
      }),
    ]);
  });

  it('publishes stable document identifiers for Cash, Tax, and Budget PDF actions', () => {
    expect(DOCUMENT_TYPES).toMatchObject({
      financeCashPositionReport: 'Finance.Cash.PositionReport',
      financeTaxInputRegister: 'Finance.Tax.InputRegister',
      financeTaxOutputRegister: 'Finance.Tax.OutputRegister',
      financeTaxVatReconciliation: 'Finance.Tax.VatReconciliation',
      financeTaxWhtPayable: 'Finance.Tax.WhtPayable',
      financeTaxWhtCertificateRegister: 'Finance.Tax.WhtCertificateRegister',
      financeTaxWhtRemittanceRegister: 'Finance.Tax.WhtRemittanceRegister',
      financeBudgetConsolidated: 'Finance.Budget.Consolidated',
      financeBudgetScenarioComparison: 'Finance.Budget.ScenarioComparison',
    });
  });
});
