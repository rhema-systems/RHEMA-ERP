import { describe, expect, it } from 'vitest';

import {
  frameworkCallOffActionState,
  frameworkCallOffAmount,
  frameworkCallOffStatusTone,
  matchingFrameworkPrice,
} from './procurement-framework-call-off';
import type {
  FrameworkCallOff,
  FrameworkCallOffAgreementOption,
  FrameworkCallOffDemandOption,
} from '@/types/procurement-framework-call-off';

describe('framework call-off presentation contract', () => {
  it('uses server-supplied allowed actions', () => {
    const actions = frameworkCallOffActionState({
      allowedActions: ['submit', 'cancel'],
    } as FrameworkCallOff);

    expect(actions.canSubmit).toBe(true);
    expect(actions.canCancel).toBe(true);
    expect(actions.canApprove).toBe(false);
    expect(actions.canIssue).toBe(false);
  });

  it('does not treat approval and issue as the same status', () => {
    expect(frameworkCallOffStatusTone('PendingApproval')).toBe('secondary');
    expect(frameworkCallOffStatusTone('Approved')).toBe('default');
    expect(frameworkCallOffStatusTone('Issued')).toBe('default');
    expect(frameworkCallOffStatusTone('Cancelled')).toBe('destructive');
  });

  it('matches demand to the immutable framework item and UOM', () => {
    const agreement = {
      priceLines: [
        {
          agreementPriceLineId: 'price-1',
          inventoryItemId: 'item-1',
          unitOfMeasure: 'EA',
        },
      ],
    } as FrameworkCallOffAgreementOption;
    const demand = {
      inventoryItemId: 'item-1',
      unitOfMeasure: 'ea',
    } as FrameworkCallOffDemandOption;

    expect(matchingFrameworkPrice(agreement, demand)?.agreementPriceLineId).toBe(
      'price-1'
    );
  });

  it('totals only selected server-priced lines', () => {
    expect(
      frameworkCallOffAmount([
        { selected: true, quantity: 2, unitPrice: 12.5 },
        { selected: false, quantity: 99, unitPrice: 10 },
      ])
    ).toBe(25);
  });
});
