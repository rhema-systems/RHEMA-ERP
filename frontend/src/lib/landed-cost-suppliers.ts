export type SupplierCostLine = {
  supplierId?: string | null; supplierName?: string | null; currency: string; amount: number;
  referenceNumber?: string | null; invoiceNumber?: string | null;
};

export function groupLandedCostsBySupplier(lines: SupplierCostLine[]) {
  const groups = new Map<string, { key: string; supplierId?: string; supplierName: string;
    currency: string; reference: string; amount: number; count: number; linked: boolean }>();
  for (const line of lines) {
    const supplierId = line.supplierId?.trim().toLowerCase() || undefined;
    const currency = line.currency.trim().toUpperCase();
    const reference = line.invoiceNumber?.trim() || line.referenceNumber?.trim() || '';
    // Never merge different suppliers based on a shared display name, currencies, or saved invoices.
    const linked = Boolean(line.invoiceNumber?.trim());
    const key = JSON.stringify([supplierId ?? null, currency, reference, linked]);
    const group = groups.get(key) ?? { key, supplierId,
      supplierName: supplierId ? line.supplierName || 'Selected supplier' : 'Supplier not selected',
      currency, reference, amount: 0, count: 0, linked };
    group.amount = Math.round((group.amount + line.amount + Number.EPSILON) * 100) / 100;
    group.count += 1;
    groups.set(key, group);
  }
  return [...groups.values()];
}
