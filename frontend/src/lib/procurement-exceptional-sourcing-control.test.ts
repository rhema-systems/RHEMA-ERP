import { describe, expect, it } from 'vitest';
import {
  exceptionalMethodLabel,
  getExceptionalSourcingActions,
  validateExceptionalPreparation,
} from './procurement-exceptional-sourcing-control';
import { ProcurementExceptionalSourcingControlStatus as Status } from '@/types/procurement-exceptional-sourcing-control';

const readiness = {
  tenderId: 't', tenderNumber: 'T-1', tenderTitle: 'Tender', tenderStatus: 'Approved', method: 4,
  methodRuleCode: 'M', exceptionRuleCode: 'E', authorityRouteReference: 'A', minimumSupplierCount: 1,
  boardApprovalRequired: true, managingDirectorApprovalRequired: false, ppaApprovalRequired: true,
  justificationRequired: true, evidenceRequired: true, postAwardFilingRequired: true,
  supplierOptions: [], evidenceRequirements: [{ evidenceRuleId: 'r', ruleCode: 'R', requirementKey: 'K', evidenceName: 'Approval letter', requiresVerification: true }],
};

const request = {
  justification: 'A sufficiently detailed statutory justification.', justificationEvidenceReference: 'J-1',
  supplierSelectionEvidenceReference: 'S-1', businessPartnerIds: ['supplier'],
  evidenceChecklist: [{ requirementKey: 'K', evidenceReference: 'E-1', verificationReference: 'V-1' }],
};

describe('exceptional sourcing controls', () => {
  it('labels every controlled noncompetitive method', () => {
    expect(exceptionalMethodLabel(3)).toBe('Restricted Tendering');
    expect(exceptionalMethodLabel(4)).toBe('Single Source');
    expect(exceptionalMethodLabel(5)).toBe('Petty Purchase');
  });
  it('requires verified mandatory evidence', () => {
    expect(validateExceptionalPreparation(readiness, { ...request, evidenceChecklist: [] })).toContain('Evidence is required');
    expect(validateExceptionalPreparation(readiness, request)).toBeNull();
  });
  it('enforces supplier minimums', () => {
    expect(validateExceptionalPreparation({ ...readiness, minimumSupplierCount: 3 }, request)).toContain('at least 3');
  });
  it('exposes one ordered action at each lifecycle state', () => {
    expect(getExceptionalSourcingActions({ status: Status.Approved } as never).canNegotiate).toBe(true);
    expect(getExceptionalSourcingActions({ status: Status.Filed } as never).immutable).toBe(true);
    expect(getExceptionalSourcingActions({ status: Status.Accepted, method: 5 } as never)).toMatchObject({
      canFile: false, immutable: true,
    });
  });
  it('does not require justification when the locked DEC-005 rule waives it', () => {
    expect(validateExceptionalPreparation(
      { ...readiness, method: 5, justificationRequired: false, postAwardFilingRequired: false },
      { ...request, justification: '', justificationEvidenceReference: '' },
    )).toBeNull();
  });
});
