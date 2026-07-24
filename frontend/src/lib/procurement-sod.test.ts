import { describe, expect, it } from 'vitest';

import {
  canRunProcurementSodCheck,
  createProcurementSodGuardRequest,
  isProcurementSodActionAvailable,
  requiredProcurementSodControlCodes,
} from './procurement-sod';

describe('procurement SOD shared controls', () => {
  it('registers exactly the six TDC SRS controls', () => {
    expect(requiredProcurementSodControlCodes).toHaveLength(6);
    expect(new Set(requiredProcurementSodControlCodes).size).toBe(6);
  });

  it('normalizes and deduplicates prior participant IDs', () => {
    const request = createProcurementSodGuardRequest({
      controlCode: requiredProcurementSodControlCodes[0],
      sourceType: ' PurchaseRequisition ',
      sourceReference: ' PR-001 ',
      prohibitedActorUserIds: 'a1111111-1111-1111-1111-111111111111, a1111111-1111-1111-1111-111111111111\nb2222222-2222-2222-2222-222222222222',
    });
    expect(request.sourceType).toBe('PurchaseRequisition');
    expect(request.prohibitedActorUserIds).toHaveLength(2);
    expect(canRunProcurementSodCheck({ ...request, prohibitedActorUserIds: request.prohibitedActorUserIds.join(',') })).toBe(true);
  });

  it('keeps protected actions unavailable until an explicit allowed decision exists', () => {
    expect(isProcurementSodActionAvailable()).toBe(false);
    expect(isProcurementSodActionAvailable({ allowed: false, isHardStop: true } as never)).toBe(false);
    expect(isProcurementSodActionAvailable({ allowed: true, isHardStop: false } as never)).toBe(true);
  });
});
