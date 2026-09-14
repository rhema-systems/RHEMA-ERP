'use client';

import { useCallback, useEffect, useRef } from 'react';
import { businessPartnerService, type BusinessPartnerDetailDto } from '@/services/businessPartnerService';

/** Apply defaults once per actual supplier change; stale requests and manual edits win. */
export function useSupplierPurchaseOrderDefaults(
  onSupplierLoaded: (supplier: BusinessPartnerDetailDto | null) => void,
  onPaymentTermsDefaulted: (terms: string) => void,
  onError: (error: unknown) => void,
) {
  const state = useRef({ supplierId: '', request: 0, edit: 0, pendingTermsEdit: null as number | null, mounted: true });
  const callbacks = useRef({ onSupplierLoaded, onPaymentTermsDefaulted, onError });
  callbacks.current = { onSupplierLoaded, onPaymentTermsDefaulted, onError };
  useEffect(() => {
    state.current.mounted = true;
    return () => { state.current.mounted = false; state.current.request += 1; };
  }, []);

  const markPaymentTermsEdited = useCallback(() => { state.current.edit += 1; }, []);
  const loadSupplierDetails = useCallback(async (supplierId: string) => {
    const changed = supplierId !== state.current.supplierId;
    state.current.supplierId = supplierId;
    if (changed) state.current.pendingTermsEdit = state.current.edit;
    const request = ++state.current.request;
    if (!supplierId) {
      callbacks.current.onSupplierLoaded(null);
      if (changed) callbacks.current.onPaymentTermsDefaulted('');
      state.current.pendingTermsEdit = null;
      return;
    }
    try {
      const supplier = await businessPartnerService.getPartnerById(supplierId);
      if (!state.current.mounted || request !== state.current.request) return;
      callbacks.current.onSupplierLoaded(supplier);
      if (state.current.pendingTermsEdit === state.current.edit) callbacks.current.onPaymentTermsDefaulted(supplier.paymentTerms || '');
      state.current.pendingTermsEdit = null;
    } catch (error) {
      if (state.current.mounted && request === state.current.request) callbacks.current.onError(error);
    }
  }, []);

  return { loadSupplierDetails, markPaymentTermsEdited };
}
