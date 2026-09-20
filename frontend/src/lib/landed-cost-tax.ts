const treatments: Record<string, number> = {
  Standard: 1, Exempt: 2, ZeroRated: 3, OutOfScope: 4, NonTaxable: 4, PendingReview: 5,
};

export function invoiceTaxTreatment(value?: string | number): number | undefined {
  if (value === undefined) return undefined;
  return typeof value === 'number' ? value : treatments[value] ?? Number(value);
}

export function landedCostTaxReviewPending(lines: Array<{ landedCostItemId?: string | null; taxTreatment?: number | string; taxGroupId?: string | null }>): boolean {
  return lines.some(line => {
    if (!line.landedCostItemId) return false;
    const treatment = invoiceTaxTreatment(line.taxTreatment);
    return treatment === undefined || ![1, 2, 3, 4].includes(treatment) || (treatment === 1 && !line.taxGroupId);
  });
}
