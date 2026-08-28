import type { ApInvoiceSupplier } from '@/types/ap';

export const apSupplierDetailedLedgerQueryKey = (tenantCode: string | null) =>
  [
    'finance',
    'ap',
    'supplier-detailed-ledger',
    'suppliers',
    tenantCode ?? 'missing-tenant',
  ] as const;

/** Maps the Finance projection without changing its canonical Supplier.Id. */
export function toApLedgerSupplierOptions(
  suppliers: readonly ApInvoiceSupplier[]
) {
  return suppliers
    .map((supplier) => ({
      id: supplier.id,
      code: supplier.code,
      name: supplier.name,
    }))
    .sort((left, right) => left.name.localeCompare(right.name));
}
