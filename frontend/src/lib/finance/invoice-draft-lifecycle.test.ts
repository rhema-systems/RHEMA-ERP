import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

const source = (path: string) => readFileSync(resolve(process.cwd(), path), 'utf8');

describe('AR and AP invoice draft lifecycle', () => {
  it('provides a real AR edit route backed by update and cache invalidation', () => {
    const editPage = source('src/app/finance/ar/invoices/[id]/edit/page.tsx');
    const form = source('src/app/finance/ar/invoices/new/page.tsx');
    const service = source('src/services/ar-service.ts');

    expect(editPage).toContain('InvoiceFormPage editInvoiceId={params.id}');
    expect(form).toContain('arService.updateInvoice(editInvoice.id');
    expect(form).toContain("invalidateQueries({ queryKey: ['invoices'] })");
    expect(service).toContain('public async updateInvoice');
  });

  it('surfaces AP invalid-form failures and refreshes the list after save', () => {
    const form = source('src/components/finance/ap/VendorInvoiceFormPage.tsx');

    expect(form).toContain('Review the highlighted invoice fields');
    expect(form).toContain('form.handleSubmit(onSubmit as any, onInvalid)');
    expect(form).toContain("invalidateQueries({ queryKey: ['vendor-invoices'] })");
  });

  it('exposes Draft-only delete actions through the existing guarded APIs', () => {
    const arList = source('src/app/finance/ar/invoices/page.tsx');
    const apList = source('src/app/finance/ap/invoices/page.tsx');
    const arService = source('src/services/ar-service.ts');
    const apService = source('src/services/accountsPayableService.ts');

    expect(arList).toContain("invoice.status === 'Draft'");
    expect(arList).toContain('Finance.AR.Invoices.Delete');
    expect(apList).toContain("invoice.status === 'Draft'");
    expect(apList).toContain('Finance.AP.Invoices.Delete');
    expect(arService).toContain('public async deleteInvoice');
    expect(apService).toContain('public async deleteInvoice');
  });
});
