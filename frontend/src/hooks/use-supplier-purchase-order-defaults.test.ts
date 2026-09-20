import { act, renderHook } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useSupplierPurchaseOrderDefaults } from './use-supplier-purchase-order-defaults';
import { businessPartnerService, type BusinessPartnerDetailDto } from '@/services/businessPartnerService';

vi.mock('@/services/businessPartnerService', () => ({ businessPartnerService: { getPartnerById: vi.fn() } }));
const supplier = (id: string, paymentTerms?: string) => ({ id, paymentTerms }) as BusinessPartnerDetailDto;
const deferred = () => {
  let resolve!: (value: BusinessPartnerDetailDto) => void;
  const promise = new Promise<BusinessPartnerDetailDto>(done => { resolve = done; });
  return { promise, resolve };
};
beforeEach(() => vi.clearAllMocks());

describe('PO supplier payment defaults', () => {
  it('defaults a newly chosen supplier and applies blank when the new supplier has no default', async () => {
    const onSupplier = vi.fn(), onTerms = vi.fn();
    vi.mocked(businessPartnerService.getPartnerById).mockResolvedValueOnce(supplier('a', 'Net 30')).mockResolvedValueOnce(supplier('b'));
    const { result } = renderHook(() => useSupplierPurchaseOrderDefaults(onSupplier, onTerms, vi.fn()));
    await act(() => result.current.loadSupplierDetails('a'));
    expect(onTerms).toHaveBeenLastCalledWith('Net 30');
    await act(() => result.current.loadSupplierDetails('b'));
    expect(onTerms).toHaveBeenLastCalledWith('');
  });

  it('never overwrites manually changed terms when refreshing the same supplier or source', async () => {
    const onTerms = vi.fn();
    vi.mocked(businessPartnerService.getPartnerById).mockResolvedValue(supplier('a', 'Net 30'));
    const { result } = renderHook(() => useSupplierPurchaseOrderDefaults(vi.fn(), onTerms, vi.fn()));
    await act(() => result.current.loadSupplierDetails('a'));
    act(() => result.current.markPaymentTermsEdited());
    await act(() => result.current.loadSupplierDetails('a'));
    expect(onTerms).toHaveBeenCalledTimes(1);
  });

  it('preserves a manual edit made while supplier defaults are loading', async () => {
    const pending = deferred(), onTerms = vi.fn();
    vi.mocked(businessPartnerService.getPartnerById).mockReturnValue(pending.promise);
    const { result } = renderHook(() => useSupplierPurchaseOrderDefaults(vi.fn(), onTerms, vi.fn()));
    let request!: Promise<void>;
    act(() => { request = result.current.loadSupplierDetails('a'); result.current.markPaymentTermsEdited(); });
    await act(async () => { pending.resolve(supplier('a', 'Net 30')); await request; });
    expect(onTerms).not.toHaveBeenCalled();
  });

  it('ignores stale responses from the previously chosen supplier', async () => {
    const first = deferred(), second = deferred(), onTerms = vi.fn(), onSupplier = vi.fn();
    vi.mocked(businessPartnerService.getPartnerById).mockReturnValueOnce(first.promise).mockReturnValueOnce(second.promise);
    const { result } = renderHook(() => useSupplierPurchaseOrderDefaults(onSupplier, onTerms, vi.fn()));
    let a!: Promise<void>, b!: Promise<void>;
    act(() => { a = result.current.loadSupplierDetails('a'); b = result.current.loadSupplierDetails('b'); });
    await act(async () => { second.resolve(supplier('b', 'Net 60')); await b; first.resolve(supplier('a', 'Net 30')); await a; });
    expect(onTerms).toHaveBeenCalledExactlyOnceWith('Net 60');
    expect(onSupplier).toHaveBeenCalledExactlyOnceWith(supplier('b', 'Net 60'));
  });

  it('still initializes defaults when duplicate same-supplier requests race before the first load', async () => {
    const first = deferred(), second = deferred(), onTerms = vi.fn();
    vi.mocked(businessPartnerService.getPartnerById).mockReturnValueOnce(first.promise).mockReturnValueOnce(second.promise);
    const { result } = renderHook(() => useSupplierPurchaseOrderDefaults(vi.fn(), onTerms, vi.fn()));
    let a!: Promise<void>, b!: Promise<void>;
    act(() => { a = result.current.loadSupplierDetails('a'); b = result.current.loadSupplierDetails('a'); });
    await act(async () => { second.resolve(supplier('a', 'Net 30')); await b; first.resolve(supplier('a', 'Net 30')); await a; });
    expect(onTerms).toHaveBeenCalledExactlyOnceWith('Net 30');
  });

  it('leaves entered values untouched and surfaces load failure', async () => {
    const onTerms = vi.fn(), onError = vi.fn();
    vi.mocked(businessPartnerService.getPartnerById).mockRejectedValue(new Error('Unavailable'));
    const { result } = renderHook(() => useSupplierPurchaseOrderDefaults(vi.fn(), onTerms, onError));
    await act(() => result.current.loadSupplierDetails('a'));
    expect(onTerms).not.toHaveBeenCalled();
    expect(onError).toHaveBeenCalledOnce();
  });

  it('invalidates a pending request when supplier selection is cleared', async () => {
    const pending = deferred(), onTerms = vi.fn(), onSupplier = vi.fn();
    vi.mocked(businessPartnerService.getPartnerById).mockReturnValue(pending.promise);
    const { result } = renderHook(() => useSupplierPurchaseOrderDefaults(onSupplier, onTerms, vi.fn()));
    let request!: Promise<void>;
    act(() => { request = result.current.loadSupplierDetails('a'); });
    await act(() => result.current.loadSupplierDetails(''));
    await act(async () => { pending.resolve(supplier('a', 'Net 30')); await request; });
    expect(onSupplier).toHaveBeenCalledExactlyOnceWith(null);
    expect(onTerms).toHaveBeenCalledExactlyOnceWith('');
  });
});
