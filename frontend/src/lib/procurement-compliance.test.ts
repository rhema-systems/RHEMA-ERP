import { describe, expect, it } from 'vitest';

import {
  complianceOutcomeTone,
  createProcurementComplianceRequest,
  procurementComplianceCategoryOptions,
  procurementComplianceMethodOptions,
  splitComplianceValues,
} from './procurement-compliance';
import type { ProcurementComplianceSimulatorForm } from '@/types/procurement-compliance';

const form = (): ProcurementComplianceSimulatorForm => ({
  policySetId: '', category: 'Goods', serviceClass: '  Medical ', amount: '2500.50', currencyCode: 'ghs',
  requestedMethod: 'Auto', sourceType: ' PurchaseRequisition ', sourceReference: ' PR-001 ', atDate: '2026-07-20',
  actorUserId: '', actorRoles: 'Requester, Approver\nRequester', sourceOwnerUserId: '', sourceOwnerRoles: '',
  entityType: ' PurchaseRequisition ', action: ' Submit ', exceptionType: '', justificationProvided: false,
  exceptionApprovalReference: '', evidenceReferenceKeys: 'SPECIFICATION, quotations\nSPECIFICATION',
});

describe('procurement compliance simulator mapping', () => {
  it('registers all procurement categories and methods', () => {
    expect(procurementComplianceCategoryOptions).toHaveLength(5);
    expect(procurementComplianceMethodOptions).toHaveLength(9);
  });

  it('normalizes distinct comma and line separated controls', () => {
    expect(splitComplianceValues('Requester, Approver\nRequester')).toEqual(['Requester', 'Approver']);
  });

  it('builds an evaluation-only request and leaves auto method unresolved', () => {
    expect(createProcurementComplianceRequest(form())).toMatchObject({
      policySetId: undefined, category: 'Goods', serviceClass: 'Medical', amount: 2500.5,
      currencyCode: 'GHS', requestedMethod: undefined, sourceType: 'PurchaseRequisition',
      sourceReference: 'PR-001', actorRoles: ['Requester', 'Approver'],
      evidenceReferenceKeys: ['SPECIFICATION', 'quotations'],
    });
  });

  it('gives every evaluator outcome a visible presentation', () => {
    expect(Object.keys(complianceOutcomeTone).sort()).toEqual(['Allowed', 'Blocked', 'ReviewRequired']);
  });
});
