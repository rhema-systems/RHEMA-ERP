import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import {
  type DetailedLedgerReport,
  PartnerDetailedLedgerReport,
} from './PartnerDetailedLedgerReport';

Object.assign(globalThis, { React });
Object.assign(globalThis, {
  ResizeObserver: class ResizeObserver {
    observe() {}
    unobserve() {}
    disconnect() {}
  },
});
Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', {
  configurable: true,
  value: () => undefined,
});

const emptyReport: DetailedLedgerReport = {
  fromDate: '2025-01-01',
  toDate: '2025-01-31',
  currencyCode: 'GHS',
  totalOpeningBalance: 0,
  totalDebits: 0,
  totalCredits: 0,
  totalClosingBalance: 0,
  warnings: [],
  accounts: [],
};

describe('Supplier Detailed Ledger canonical selection', () => {
  it('submits the selected canonical Supplier.Id supplied by the AP adapter', async () => {
    const loadReport = vi.fn().mockResolvedValue(emptyReport);

    render(
      <PartnerDetailedLedgerReport
        title="Supplier Detailed Ledger"
        description="AP ledger"
        partnerLabel="Supplier"
        partnerPluralLabel="Suppliers"
        currencyToggleLabel="Show supplier currency"
        exportFilePrefix="supplier-ledger"
        backHref="/finance/ap/reports"
        partners={[
          {
            id: 'canonical-supplier-id',
            code: 'TDC-DEMO-SUP-001',
            name: 'Tema Engineering Services Ltd',
          },
        ]}
        loadReport={loadReport}
      />
    );

    fireEvent.click(screen.getByRole('combobox'));
    fireEvent.click(await screen.findByText('Tema Engineering Services Ltd'));
    fireEvent.click(screen.getByRole('button', { name: /run report/i }));

    await waitFor(() => {
      expect(loadReport).toHaveBeenCalledWith(
        expect.objectContaining({
          partnerIds: ['canonical-supplier-id'],
        })
      );
    });
  });
});
