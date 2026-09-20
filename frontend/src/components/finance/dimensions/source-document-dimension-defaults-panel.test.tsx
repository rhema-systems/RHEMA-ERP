import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it, vi } from 'vitest';
import { SourceDocumentDimensionDefaultsPanel } from './source-document-dimension-panel';

Object.assign(globalThis, { React });
Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', {
  configurable: true,
  value: vi.fn(),
});

vi.mock('@/services/finance/finance-data.service', () => ({
  financeDataService: {
    getFinanceDimensions: vi.fn().mockResolvedValue([
      {
        id: 'department',
        code: 'DEPT',
        name: 'Department',
        classification: 'Analytical',
        valueSourceType: 'Lookup',
        isActive: true,
        displayOrder: 1,
        values: [
          {
            id: 'finance',
            financeDimensionDefinitionId: 'department',
            code: 'FIN',
            name: 'Finance',
            effectiveDate: '2026-01-01',
            isActive: true,
            displayOrder: 1,
          },
        ],
      },
    ]),
    getFinanceDimensionRules: vi.fn().mockResolvedValue([]),
  },
}));

describe('SourceDocumentDimensionDefaultsPanel', () => {
  it('captures clearable defaults without offering a misleading line-level apply action', async () => {
    const onChange = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <SourceDocumentDimensionDefaultsPanel
          effectiveDate="2026-09-02"
          values={{}}
          onChange={onChange}
        />
      </QueryClientProvider>
    );

    expect(await screen.findByText('Default coding dimensions')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /apply to all eligible lines/i })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('combobox'));
    fireEvent.click(await screen.findByText('FIN — Finance'));
    expect(onChange).toHaveBeenCalledWith({ DEPT: 'FIN' });
    expect(screen.getByText(/Fixed values are resolved and locked by the server/i)).toBeInTheDocument();
  });
});
