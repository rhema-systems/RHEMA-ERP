import { describe, expect, it } from 'vitest';
import {
  canActivateCommittee, canCheckAccessCapability, committeeAssignmentEligibilityIssue,
  validateResponsibilityAssignment,
} from './procurement-access-control';

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

  it('does not offer inactive or role-incompatible responsibility assignments to a committee', () => {
    const evaluator = {
      id: 'assignment-1', userId: 'user-1', username: 'evaluator', userDisplayName: 'Evaluator',
      roleId: 'role-1', roleName: 'TDC_EVALUATOR', roleDisplayName: 'TDC Tender Evaluator',
      warehouseScopeMode: 'None' as const, warehouses: [], locationScopeMode: 'None' as const, locations: [],
      effectiveFrom: '2026-09-01', isActive: true, reason: 'Evaluation duty', rowVersion: 'AQ==',
    };

    expect(committeeAssignmentEligibilityIssue(
      evaluator, 'TDC_EVALUATOR', 'Chair', '2026-09-02')).toBeUndefined();
    expect(committeeAssignmentEligibilityIssue(
      { ...evaluator, isActive: false }, 'TDC_EVALUATOR', 'Chair', '2026-09-02')).toContain('inactive');
    expect(committeeAssignmentEligibilityIssue(
      { ...evaluator, roleName: 'TDC_OBSERVER' }, 'TDC_EVALUATOR', 'Chair', '2026-09-02')).toContain('requires TDC_EVALUATOR');
    expect(committeeAssignmentEligibilityIssue(
      { ...evaluator, roleName: 'TDC_OBSERVER' }, 'TDC_EVALUATOR', 'Observer', '2026-09-02')).toBeUndefined();
  });

  it('requires committee membership dates to stay inside the responsibility period', () => {
    const evaluator = {
      id: 'assignment-1', userId: 'user-1', username: 'evaluator', userDisplayName: 'Evaluator',
      roleId: 'role-1', roleName: 'TDC_EVALUATOR', roleDisplayName: 'TDC Tender Evaluator',
      warehouseScopeMode: 'None' as const, warehouses: [], locationScopeMode: 'None' as const, locations: [],
      effectiveFrom: '2026-09-01', effectiveTo: '2026-09-30', isActive: true,
      reason: 'Evaluation duty', rowVersion: 'AQ==',
    };

    expect(committeeAssignmentEligibilityIssue(
      evaluator, 'TDC_EVALUATOR', 'VotingMember', '2026-09-02')).toContain('Set the membership end date');
    expect(committeeAssignmentEligibilityIssue(
      evaluator, 'TDC_EVALUATOR', 'VotingMember', '2026-09-02', '2026-09-30')).toBeUndefined();
    expect(committeeAssignmentEligibilityIssue(
      evaluator, 'TDC_EVALUATOR', 'VotingMember', '2026-09-02', '2026-10-01')).toContain('cannot continue');
  });
});
