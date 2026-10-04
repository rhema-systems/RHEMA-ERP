import { describe, expect, it } from 'vitest';
import type { FixedAssetOpeningBalanceCandidate } from '@/types/finance';
import {
  getEligibleFixedAssetBookValueIds,
  getFixedAssetBulkSelectionState,
  reconcileFixedAssetSelection,
  toggleAllEligibleFixedAssets,
} from './fixed-asset-opening-selection';

const candidate = (
  id: string,
  bookClassification = 'IFRS',
  openingPostedToGl = false
): FixedAssetOpeningBalanceCandidate => ({
  fixedAssetId: `asset-${id}`,
  fixedAssetBookValueId: id,
  assetCode: `FA-${id}`,
  assetName: `Asset ${id}`,
  categoryCode: 'EQUIPMENT',
  bookClassification,
  acquisitionCost: 100,
  accumulatedDepreciation: 20,
  netBookValue: 80,
  openingPostedToGl,
});

describe('fixed-asset opening bulk selection', () => {
  it('limits eligible ids to unposted candidates in the active book', () => {
    expect(
      getEligibleFixedAssetBookValueIds(
        [candidate('ready'), candidate('posted', 'IFRS', true), candidate('tax', 'TAX')],
        'IFRS'
      )
    ).toEqual(['ready']);
  });

  it('reports none, partial, and all selection states', () => {
    const eligible = ['one', 'two'];

    expect(getFixedAssetBulkSelectionState([], eligible)).toBe(false);
    expect(getFixedAssetBulkSelectionState(['one'], eligible)).toBe('indeterminate');
    expect(getFixedAssetBulkSelectionState(['one', 'two'], eligible)).toBe(true);
  });

  it('selects all eligible ids and clears them when all are selected', () => {
    const eligible = ['one', 'two'];

    expect(toggleAllEligibleFixedAssets(['one'], eligible)).toEqual(['one', 'two']);
    expect(toggleAllEligibleFixedAssets(['one', 'two'], eligible)).toEqual([]);
  });

  it('removes stale ids after the accounting book or eligibility changes', () => {
    expect(reconcileFixedAssetSelection(['ifrs', 'tax'], ['tax'])).toEqual(['tax']);
  });
});
