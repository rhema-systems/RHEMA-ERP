import { describe, expect, it } from 'vitest';
import { getRequisitionSourcingEntryAction } from './purchase-requisition-actions';
import type { ProcurementMethodType } from '@/types/procurement-policy';

const ready = {
  approved: true,
  canEnterSourcing: true,
  isMethodCompliant: true,
  method: 'PettyPurchase' as ProcurementMethodType,
  hasExistingSource: false,
  isSourceStale: false,
};

describe('PR sourcing entry', () => {
  it('offers explicitly named preparation for the policy-selected Petty Purchase source', () => {
    expect(getRequisitionSourcingEntryAction(ready)).toEqual({
      label: 'Prepare Petty Purchase', enabled: true,
    });
  });

  it.each([
    { approved: false },
    { canEnterSourcing: false },
    { isMethodCompliant: false },
    { isMethodCompliant: undefined },
    { method: undefined },
    { hasExistingSource: true },
    { isSourceStale: true },
  ])('retains every prerequisite and duplicate/stale source guard: %j', (override) => {
    expect(getRequisitionSourcingEntryAction({ ...ready, ...override }).enabled).toBe(false);
  });

  it.each([
    'NationalCompetitiveTendering', 'InternationalCompetitiveTendering',
    'RestrictedTendering', 'SingleSource', 'QualityBasedSelection',
    'QualityAndCostBasedSelection',
  ] as ProcurementMethodType[])('preserves the existing %s preparation entry', (method) => {
    expect(getRequisitionSourcingEntryAction({ ...ready, method })).toEqual({
      label: 'Create Tender', enabled: true,
    });
  });

  it('does not route RFQ through tender-backed creation', () => {
    expect(getRequisitionSourcingEntryAction({ ...ready, method: 'RequestForQuotation' }).enabled).toBe(false);
  });
});
