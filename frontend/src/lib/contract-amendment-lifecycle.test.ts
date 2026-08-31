import { describe, expect, it } from 'vitest';
import {
  canProcessContractAmendment,
  canRequestContractAmendment,
} from './contract-amendment-lifecycle';

describe('contract amendment lifecycle visibility', () => {
  it('only offers amendment creation for an active contract manager', () => {
    expect(canRequestContractAmendment('Active', true)).toBe(true);
    expect(canRequestContractAmendment('Draft', true)).toBe(false);
    expect(canRequestContractAmendment('Active', false)).toBe(false);
  });

  it('uses the backend PendingApproval status for decision and delete controls', () => {
    expect(canProcessContractAmendment('PendingApproval', true)).toBe(true);
    expect(canProcessContractAmendment('Pending', true)).toBe(false);
    expect(canProcessContractAmendment('Approved', true)).toBe(false);
    expect(canProcessContractAmendment('PendingApproval', false)).toBe(false);
  });
});
