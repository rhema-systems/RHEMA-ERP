import { describe, expect, it } from 'vitest';
import type { PurchaseRequisitionSourcingReadinessDto } from '@/services/purchasingService';
import { getSourcingReleasePresentation } from './procurement-requisition-sourcing';

const readiness = (
  overrides: Partial<PurchaseRequisitionSourcingReadinessDto> = {}
): PurchaseRequisitionSourcingReadinessDto => ({
  requisitionId: 'pr-1',
  requisitionNumber: 'PR-001',
  status: 'Approved',
  isCompliant: false,
  canRelease: false,
  isReleased: false,
  hasStaleRelease: false,
  decisionCode: 'PR_SOURCING_BLOCKED',
  message: 'Blocked',
  evaluatedAtUtc: '2026-07-22T08:00:00Z',
  controlFingerprint: 'a'.repeat(64),
  requirements: [],
  requiredActions: [],
  ...overrides,
});

describe('getSourcingReleasePresentation', () => {
  it('reports loading without enabling sourcing', () => {
    expect(getSourcingReleasePresentation(undefined, true)).toMatchObject({
      tone: 'neutral',
      canRelease: false,
      canEnterSourcing: false,
    });
  });

  it('enables the release action only for compliant readiness', () => {
    expect(
      getSourcingReleasePresentation(
        readiness({ isCompliant: true, canRelease: true, decisionCode: 'PR_SOURCING_READY' })
      )
    ).toMatchObject({ tone: 'ready', canRelease: true, canEnterSourcing: false });
  });

  it('enables sourcing only when a current immutable release exists', () => {
    expect(
      getSourcingReleasePresentation(
        readiness({ isCompliant: true, isReleased: true, decisionCode: 'PR_SOURCING_RELEASE_CURRENT' })
      )
    ).toMatchObject({ tone: 'released', canRelease: false, canEnterSourcing: true });
  });

  it('keeps stale releases out of sourcing', () => {
    expect(
      getSourcingReleasePresentation(readiness({ hasStaleRelease: true, canRelease: true }))
    ).toMatchObject({ tone: 'stale', canEnterSourcing: false, canRelease: true });
  });
});
