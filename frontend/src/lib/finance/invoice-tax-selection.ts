const STANDARD_TAX_TREATMENT = 1;
const EXEMPT_TAX_TREATMENT = 2;
const ZERO_RATED_TAX_TREATMENT = 3;
const OUT_OF_SCOPE_TAX_TREATMENT = 4;

export interface InvoiceLineTaxSelectionInput {
  lineTaxGroupId?: string | null;
  defaultTaxGroupId?: string | null;
  taxTreatment?: number | null;
  isOpeningBalance: boolean;
}

export interface InvoiceLineTaxSelection {
  taxGroupId: string | null;
  taxTreatment: number;
}

export function resolveInvoiceLineTaxSelection({
  lineTaxGroupId,
  defaultTaxGroupId,
  taxTreatment,
  isOpeningBalance,
}: InvoiceLineTaxSelectionInput): InvoiceLineTaxSelection {
  const lineSelection = lineTaxGroupId?.trim();
  const inheritedSelection = defaultTaxGroupId?.trim();
  const selectedGroup = lineSelection && lineSelection !== 'inherit'
    ? lineSelection
    : inheritedSelection;
  const taxGroupId = !isOpeningBalance && selectedGroup && selectedGroup !== 'none'
    ? selectedGroup
    : null;
  const normalizedTreatment = taxTreatment == null ? undefined : Number(taxTreatment);

  if (!taxGroupId) {
    const retainedNoTaxTreatment = normalizedTreatment === EXEMPT_TAX_TREATMENT
      || normalizedTreatment === ZERO_RATED_TAX_TREATMENT
      || normalizedTreatment === OUT_OF_SCOPE_TAX_TREATMENT
      ? normalizedTreatment
      : EXEMPT_TAX_TREATMENT;
    return { taxGroupId: null, taxTreatment: retainedNoTaxTreatment };
  }

  // Selecting a governed tax group is an explicit return to standard tax treatment.
  return { taxGroupId, taxTreatment: STANDARD_TAX_TREATMENT };
}

export function shouldBlockApInvoiceSaveForSupplierDefaults({
  isEditMode,
  isOpeningBalance,
  isLoading,
}: {
  isEditMode: boolean;
  isOpeningBalance: boolean;
  isLoading: boolean;
}): boolean {
  return !isEditMode && !isOpeningBalance && isLoading;
}
