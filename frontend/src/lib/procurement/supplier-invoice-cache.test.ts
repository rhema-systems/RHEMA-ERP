import { QueryClient } from '@tanstack/react-query';
import { describe, expect, it } from 'vitest';
import type { VendorInvoice } from '@/types/ap';
import { cacheSavedSupplierInvoice } from './supplier-invoice-cache';

describe('saved supplier invoice navigation', () => {
  it.each(['Updated freight description', 'Updated freight description\nAdditional details'])(
    'reopens the saved description immediately: %s',
    async description => {
      const client = new QueryClient({ defaultOptions: { queries: { staleTime: 300_000 } } });
      const key = ['vendor-invoice', 'invoice-1'];
      const before = { id: 'invoice-1', lineItems: [{ description: 'Freight / Shipping' }] } as VendorInvoice;
      client.setQueryData(key, before);
      client.setQueryData(['vendor-invoices', 1], [before]);
      const saved = { ...before, lineItems: [{ ...before.lineItems[0], description }] };

      await cacheSavedSupplierInvoice(client, saved);

      // Detail and editor both read this fresh cache during client navigation.
      const reopened = await client.fetchQuery({ queryKey: key, queryFn: async () => before });
      expect(reopened.lineItems[0].description).toBe(description);
      expect(client.getQueryState(['vendor-invoices', 1])?.isInvalidated).toBe(true);
      client.clear();
    }
  );

  it('prevents an older in-flight fetch from overwriting the saved invoice', async () => {
    const client = new QueryClient();
    const key = ['vendor-invoice', 'invoice-1'];
    let finishOldFetch!: (value: VendorInvoice) => void;
    const staleFetch = client.fetchQuery({ queryKey: key, queryFn: () => new Promise<VendorInvoice>(resolve => { finishOldFetch = resolve; }) }).catch(() => undefined);
    const saved = { id: 'invoice-1', lineItems: [{ description: 'Saved main line\nSaved second line' }] } as VendorInvoice;
    await cacheSavedSupplierInvoice(client, saved);
    finishOldFetch({ ...saved, lineItems: [{ ...saved.lineItems[0], description: 'Old description' }] });
    await staleFetch;
    expect(client.getQueryData(key)).toEqual(saved);
    client.clear();
  });
});
