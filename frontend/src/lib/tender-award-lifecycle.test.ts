import { describe, expect, it } from 'vitest';
import {
  canDecideTenderAward,
  isFinalTenderAward,
} from './tender-award-lifecycle';

describe('tender award maker-checker visibility', () => {
  it('only permits an approver to decide a pending recommendation', () => {
    expect(canDecideTenderAward('PendingApproval', true)).toBe(true);
    expect(canDecideTenderAward('PendingApproval', false)).toBe(false);
    expect(canDecideTenderAward('Awarded', true)).toBe(false);
    expect(canDecideTenderAward('Rejected', true)).toBe(false);
  });

  it('only exposes downstream actions for a finalized award', () => {
    expect(isFinalTenderAward('Awarded')).toBe(true);
    expect(isFinalTenderAward('PendingApproval')).toBe(false);
    expect(isFinalTenderAward('Rejected')).toBe(false);
    expect(isFinalTenderAward('Cancelled')).toBe(false);
  });
});
