import { describe, expect, it } from 'vitest';
import { TaxApplicability, TaxCategory } from '@/types/tax';
import { getTaxAccountRequirements } from './tax-account-requirements';

describe('tax account requirements', () => {
  it('requires payable only for standard sales output tax', () => {
    expect(getTaxAccountRequirements(TaxApplicability.Sales, TaxCategory.Standard, false))
      .toMatchObject({ payableRequired: true, receivableRequired: false });
  });

  it('requires receivable for recoverable purchase tax', () => {
    expect(getTaxAccountRequirements(TaxApplicability.Purchases, TaxCategory.Standard, true))
      .toMatchObject({ payableRequired: false, receivableRequired: true });
  });

  it('requires payable for purchase withholding and receivable for sales withholding suffered', () => {
    expect(getTaxAccountRequirements(TaxApplicability.Purchases, TaxCategory.Withholding, false))
      .toMatchObject({ payableRequired: true, receivableRequired: false });
    expect(getTaxAccountRequirements(TaxApplicability.Sales, TaxCategory.Withholding, false))
      .toMatchObject({ payableRequired: false, receivableRequired: true });
  });
});
