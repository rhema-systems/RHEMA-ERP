import { describe, expect, it } from 'vitest';
import {
  parseRoleList,
  policyActions,
  requestActions,
  validateChangeDraft,
  validatePolicyDraft,
} from './procurement-master-data-change';

describe('procurement master-data change controls', () => {
  it('normalizes role lists', () =>
    expect(parseRoleList('B, A, B, ')).toEqual(['A', 'B']));
  it('requires separated maker and checker roles', () =>
    expect(
      validatePolicyDraft({
        resourceType: 'SupplierProfile',
        name: 'Supplier',
        makerRoles: ['A'],
        checkerRoles: ['A'],
        requireIndependentApproval: true,
        requireRevalidation: true,
        requireEvidence: false,
        effectiveFromUtc: '2026-07-21',
      })
    ).toContain('disjoint'));
  it('accepts a complete policy draft', () =>
    expect(
      validatePolicyDraft({
        resourceType: 'SupplierProfile',
        name: 'Supplier',
        makerRoles: ['A'],
        checkerRoles: ['B'],
        requireIndependentApproval: true,
        requireRevalidation: true,
        requireEvidence: false,
        effectiveFromUtc: '2026-07-21',
      })
    ).toBeUndefined());
  it('rejects a non-object patch', () =>
    expect(
      validateChangeDraft({
        resourceType: 'InventoryItem',
        targetId: 'id',
        proposedChangesJson: '[]',
        reason: 'change',
        effectiveAtUtc: '2026-07-21',
        evidence: [],
      })
    ).toContain('object'));
  it('allows a tenant singleton without a target id', () =>
    expect(
      validateChangeDraft(
        {
          resourceType: 'ProcurementPolicySensitive',
          targetId: '',
          proposedChangesJson: '{"RequireApprovalForPO":true}',
          reason: 'change',
          effectiveAtUtc: '2026-07-21',
          evidence: [],
        },
        true
      )
    ).toBeUndefined());
  it('exposes only valid lifecycle actions', () => {
    const request = {
      status: 'Approved',
      effectiveAtUtc: '2026-07-20T00:00:00Z',
    } as never;
    expect(
      requestActions(request, new Date('2026-07-21T00:00:00Z')).canApply
    ).toBe(true);
    expect(policyActions({ status: 'Active' } as never)).toEqual({
      canEdit: false,
      canActivate: false,
      canRetire: true,
    });
  });
});
