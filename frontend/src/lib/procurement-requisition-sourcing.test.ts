import { describe, expect, it } from 'vitest';
import type { PurchaseRequisitionSourcingReadinessDto } from '@/services/purchasingService';
import {
  getSourcingReleasePresentation,
  getSourcingRequirementPresentation,
} from './procurement-requisition-sourcing';

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
        readiness({
          isCompliant: true,
          canRelease: true,
          decisionCode: 'PR_SOURCING_READY',
        })
      )
    ).toMatchObject({
      tone: 'ready',
      canRelease: true,
      canEnterSourcing: false,
    });
  });

  it('enables sourcing only when a current immutable release exists', () => {
    expect(
      getSourcingReleasePresentation(
        readiness({
          isCompliant: true,
          isReleased: true,
          decisionCode: 'PR_SOURCING_RELEASE_CURRENT',
        })
      )
    ).toMatchObject({
      tone: 'released',
      canRelease: false,
      canEnterSourcing: true,
    });
  });

  it('keeps stale releases out of sourcing', () => {
    expect(
      getSourcingReleasePresentation(
        readiness({ hasStaleRelease: true, canRelease: true })
      )
    ).toMatchObject({
      tone: 'stale',
      canEnterSourcing: false,
      canRelease: true,
    });
  });

  it('shows an unapproved requisition as a waiting stage rather than a sourcing failure', () => {
    expect(
      getSourcingReleasePresentation(
        readiness({
          status: 'Draft',
          decisionCode: 'PR_NOT_APPROVED',
        })
      )
    ).toMatchObject({
      tone: 'neutral',
      title: 'Available after PR approval',
      badge: 'Awaiting approval',
      canRelease: false,
      canEnterSourcing: false,
    });
  });
});

describe('getSourcingRequirementPresentation', () => {
  const requirement = (key: string, satisfied = false) => ({
    key,
    label: key,
    satisfied,
    code: `${key}_BLOCKED`,
    message: `${key} needs attention`,
  });

  it('shows only genuine draft prerequisites as action required', () => {
    const draft = readiness({
      status: 'Draft',
      requirements: [
        requirement('MANDATORY_FIELDS'),
        requirement('BUDGET_AVAILABILITY'),
        requirement('PR_STATUS'),
      ],
    });

    expect(
      getSourcingRequirementPresentation(draft, draft.requirements[0]).state
    ).toBe('actionRequired');
    expect(
      getSourcingRequirementPresentation(draft, draft.requirements[1]).state
    ).toBe('actionRequired');
    expect(
      getSourcingRequirementPresentation(draft, draft.requirements[2]).state
    ).toBe('waiting');
  });

  it('keeps budget availability actionable after submission', () => {
    const submitted = readiness({
      status: 'Submitted',
      requirements: [requirement('BUDGET_AVAILABILITY')],
    });

    expect(
      getSourcingRequirementPresentation(submitted, submitted.requirements[0])
        .state
    ).toBe('actionRequired');
  });

  it('shows an approved requisition budget failure as actionable', () => {
    const approved = readiness({
      status: 'Approved',
      requirements: [
        requirement('PR_STATUS', true),
        requirement('BUDGET_AVAILABILITY'),
      ],
    });

    expect(
      getSourcingRequirementPresentation(approved, approved.requirements[1])
    ).toMatchObject({
      state: 'actionRequired',
    });
  });
});
