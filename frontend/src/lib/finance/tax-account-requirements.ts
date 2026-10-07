import { TaxApplicability, TaxCategory } from '@/types/tax';

export interface TaxAccountRequirements {
  payableRequired: boolean;
  receivableRequired: boolean;
  guidance: string;
}

export const getTaxAccountRequirements = (
  applicability: TaxApplicability,
  category: TaxCategory,
  recoverableInputTax: boolean,
): TaxAccountRequirements => {
  const sales = applicability === TaxApplicability.Sales || applicability === TaxApplicability.Both;
  const purchases = applicability === TaxApplicability.Purchases || applicability === TaxApplicability.Both;
  const withholding = category === TaxCategory.Withholding || category === TaxCategory.VatWithholding;
  const payableRequired = withholding ? purchases : sales;
  const receivableRequired = withholding ? sales : purchases && recoverableInputTax;

  const parts = [
    payableRequired
      ? 'A liability account is required for tax collected or withheld for the authority.'
      : 'A payable account is not required for this applicability.',
    receivableRequired
      ? 'An asset account is required for recoverable or suffered tax.'
      : 'A receivable account is optional unless this tax becomes recoverable or represents tax suffered.',
  ];

  return { payableRequired, receivableRequired, guidance: parts.join(' ') };
};
