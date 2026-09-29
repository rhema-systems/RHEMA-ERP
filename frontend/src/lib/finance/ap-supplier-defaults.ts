import type { PurchaseOrderSupplierDefaultsDto } from '@/services/purchasingService';

export interface ApSupplierDefaultLine {
  sourceLineId: string;
  lineItemType: string;
  glAccountId?: string;
  taxGroupId?: string;
  taxTreatment?: number;
}
export interface ApSupplierDefaultValues {
  paymentTermId?: string;
  expenseAccountId?: string;
  taxGroupId?: string;
  lineItems: ApSupplierDefaultLine[];
}

export const apExpenseLineTypes = ['Expense', 'Service', 'Freight', 'Miscellaneous', 'FinanceCharge'] as const;
export function isApExpenseLineType(type: string): boolean {
  return (apExpenseLineTypes as readonly string[]).includes(type);
}

/** Snapshots the header default onto a newly created line. Existing lines remain unchanged. */
export function taxGroupForNewApInvoiceLine(
  headerTaxGroupId: string | null | undefined,
  isOpeningBalance: boolean
): string {
  return isOpeningBalance ? 'none' : (headerTaxGroupId || 'none');
}

/** Visible form assignments only. Explicit edits, including selecting No Tax, always win. */
export function planApSupplierDefaults(values: ApSupplierDefaultValues, defaults: PurchaseOrderSupplierDefaultsDto,
  edited: ReadonlySet<string>, availableTaxIds: ReadonlySet<string>) {
  const assignments: Array<{ field: string; value: string }> = [];
  const posting = defaults.postingDefaults;
  const taxUnavailable = Boolean(posting.defaultTaxGroupId && !availableTaxIds.has(posting.defaultTaxGroupId));
  const assign = (field: string, current: string | undefined, value: string | null | undefined) => {
    const next = value || '';
    if (!edited.has(field) && (current || '') !== next) assignments.push({ field, value: next });
  };
  if (defaults.paymentTermId) assign('paymentTermId', values.paymentTermId, defaults.paymentTermId);
  assign('expenseAccountId', values.expenseAccountId, posting.defaultExpenseAccountId);
  if (!taxUnavailable) assign('taxGroupId', values.taxGroupId, posting.defaultTaxGroupId || 'none');
  values.lineItems.forEach((line, index) => {
    if (isApExpenseLineType(line.lineItemType) && !edited.has(`${line.sourceLineId}:glAccountId`)) {
      const chargeAccount = line.lineItemType === 'Freight' ? posting.defaultFreightAccountId
        : line.lineItemType === 'Miscellaneous' ? posting.defaultMiscellaneousAccountId
        : line.lineItemType === 'FinanceCharge' ? posting.defaultFinanceChargesAccountId : undefined;
      assign(`lineItems.${index}.glAccountId`, line.glAccountId, chargeAccount ||
        (edited.has('expenseAccountId') ? values.expenseAccountId : posting.defaultExpenseAccountId));
    }
    if (!taxUnavailable && (!line.taxTreatment || line.taxTreatment === 1) &&
        !edited.has('taxGroupId') && !edited.has(`${line.sourceLineId}:taxGroupId`) && !edited.has(`${line.sourceLineId}:taxTreatment`)) {
      assign(`lineItems.${index}.taxGroupId`, line.taxGroupId, posting.defaultTaxGroupId || 'none');
    }
  });
  return { assignments, taxUnavailable, allowServerDefaults: edited.size === 0 && !taxUnavailable };
}
