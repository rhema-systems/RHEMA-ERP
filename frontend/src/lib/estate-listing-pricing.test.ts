import { describe, expect, it } from 'vitest';
import { getListingPriceDefaults } from './estate-listing-pricing';

const land = {
  listingScope: 'demarcation' as const,
  targetSalePrice: 1_000_000,
  externalListingType: 'Lease',
  externalListingPrice: undefined,
  externalSalePrice: undefined,
  externalMonthlyRent: undefined,
};

describe('Land Bank listing price defaults', () => {
  it('starts a new full-term lease at the Land Bank price', () => {
    expect(getListingPriceDefaults(land).leasePrice).toBe(1_000_000);
  });

  it('does not mistake a legacy recurring amount for the full-term lease price', () => {
    expect(getListingPriceDefaults({
      ...land,
      externalListingPrice: 10_000,
      externalMonthlyRent: 10_000,
    })).toMatchObject({ leasePrice: 1_000_000, legacyRecurringLeasePrice: true });
  });

  it('preserves an explicitly saved full-term listing price', () => {
    expect(getListingPriceDefaults({ ...land, externalListingPrice: 900_000 }).leasePrice).toBe(900_000);
  });

  it('defaults a sale to Land Bank rather than a previous rent or lease amount', () => {
    expect(getListingPriceDefaults({
      ...land,
      externalListingType: 'Rent',
      externalListingPrice: 10_000,
      externalMonthlyRent: 10_000,
    }).salePrice).toBe(1_000_000);
  });
});
