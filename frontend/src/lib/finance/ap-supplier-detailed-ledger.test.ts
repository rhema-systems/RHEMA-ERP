import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import {
  apSupplierDetailedLedgerQueryKey,
  toApLedgerSupplierOptions,
} from './ap-supplier-detailed-ledger';

describe('AP Supplier Detailed Ledger supplier boundary', () => {
  it('partitions supplier lookup state by tenant', () => {
    expect(apSupplierDetailedLedgerQueryKey('FINANCE-DEMO')).not.toEqual(
      apSupplierDetailedLedgerQueryKey('DEFAULT')
    );
  });

  it('preserves canonical Supplier.Id while sorting Finance supplier options', () => {
    expect(
      toApLedgerSupplierOptions([
        {
          id: 'supplier-002',
          code: 'TDC-DEMO-SUP-002',
          name: 'Volta Office Solutions Ltd',
        },
        {
          id: 'supplier-001',
          code: 'TDC-DEMO-SUP-001',
          name: 'Tema Engineering Services Ltd',
        },
      ])
    ).toEqual([
      {
        id: 'supplier-001',
        code: 'TDC-DEMO-SUP-001',
        name: 'Tema Engineering Services Ltd',
      },
      {
        id: 'supplier-002',
        code: 'TDC-DEMO-SUP-002',
        name: 'Volta Office Solutions Ltd',
      },
    ]);
  });

  it('does not reintroduce the Procurement business-partner lookup', () => {
    const pageSource = readFileSync(
      join(
        process.cwd(),
        'src',
        'app',
        'finance',
        'ap',
        'reports',
        'supplier-detailed-ledger',
        'page.tsx'
      ),
      'utf8'
    );

    expect(pageSource).toContain('getInvoiceSuppliers');
    expect(pageSource).not.toContain('businessPartnerService');
    expect(pageSource).not.toContain('/api/procurement');
  });
});
