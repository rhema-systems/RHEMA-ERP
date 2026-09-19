import { describe, expect, it } from 'vitest';
import { getSegmentAccess } from './segment-access';

describe('segment access', () => {
  it('requires Finance.Read for browsing and configure permission for mutations', () => {
    expect(getSegmentAccess(() => false)).toEqual({ canRead: false, canManage: false });
    expect(getSegmentAccess(permission => permission === 'Finance.Policy.ConfigureChartOfAccounts'))
      .toEqual({ canRead: false, canManage: false });
    expect(getSegmentAccess(permission => permission === 'Finance.Read'))
      .toEqual({ canRead: true, canManage: false });
    expect(getSegmentAccess(() => true)).toEqual({ canRead: true, canManage: true });
  });
});
