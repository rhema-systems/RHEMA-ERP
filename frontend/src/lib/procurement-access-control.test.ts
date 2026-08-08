import { describe, expect, it } from 'vitest';
import { canActivateCommittee, canCheckAccessCapability, validateResponsibilityAssignment } from './procurement-access-control';

const assignment = {
  userId: 'user-1', roleName: 'TDC_STORES_OFFICER', warehouseScopeMode: 'Restricted' as const,
  warehouseIds: ['warehouse-1'], locationScopeMode: 'Restricted' as const, locationIds: ['location-1'],
  effectiveFrom: '2026-07-21', isActive: true, reason: 'Assigned duty',
};

describe('procurement access UI guards', () => {
  it('requires a selected warehouse for a restricted stores responsibility', () => {
    expect(validateResponsibilityAssignment({ ...assignment, warehouseIds: [] }, true)).toContain('warehouse');
    expect(validateResponsibilityAssignment({ ...assignment, locationIds: [] }, true)).toContain('location');
    expect(validateResponsibilityAssignment(assignment, true)).toBeUndefined();
  });

  it('prevents warehouse scope on a non-warehouse responsibility', () => {
    expect(validateResponsibilityAssignment({ ...assignment, roleName: 'TDC_REQUISITIONER' }, false)).toContain('not applicable');
  });

  it('allows committee activation only after quorum', () => {
    expect(canActivateCommittee({ activeVotingMemberCount: 2, requiredQuorum: 3 })).toBe(false);
    expect(canActivateCommittee({ activeVotingMemberCount: 3, requiredQuorum: 3 })).toBe(true);
  });

  it('requires warehouse context for a warehouse-scoped capability check', () => {
    const request = { permissionCode: 'procurement.inventory.read', sourceType: 'Warehouse', sourceReference: 'WH-1' };
    expect(canCheckAccessCapability(request, true)).toBe(false);
    expect(canCheckAccessCapability({ ...request, warehouseId: 'warehouse-1' }, true)).toBe(true);
  });
});
