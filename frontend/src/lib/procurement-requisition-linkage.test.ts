import { describe, expect, it } from 'vitest';
import {
  EMPTY_REQUISITION_LINKAGE,
  normalizeRequisitionLinkage,
  toEditableRequisitionLinkage,
  validateExceptionLinkage,
} from './procurement-requisition-linkage';

describe('purchase requisition linkage helpers', () => {
  it('keeps every supported linkage field in the save contract', () => {
    const result = normalizeRequisitionLinkage({
      sourcePlanItemId: ' plan-item ',
      budgetId: ' budget ',
      procurementCategory: 'Goods',
      costCenter: ' CC-010 ',
      projectId: ' project ',
      requisitionType: 'ProjectPurchase',
      specificationTemplateId: ' template ',
      approvedExceptionRuleId: ' exception ',
      exceptionWorkflowInstanceId: ' workflow ',
      exceptionApprovalReference: ' MIN-010 ',
      exceptionEvidenceReference: ' EVID-010 ',
    });

    expect(result).toEqual({
      sourcePlanItemId: 'plan-item',
      budgetId: 'budget',
      procurementCategory: 'Goods',
      costCenter: 'CC-010',
      projectId: 'project',
      requisitionType: 'ProjectPurchase',
      specificationTemplateId: 'template',
      approvedExceptionRuleId: 'exception',
      exceptionWorkflowInstanceId: 'workflow',
      exceptionApprovalReference: 'MIN-010',
      exceptionEvidenceReference: 'EVID-010',
    });
  });

  it('does not manufacture submission hard stops for absent optional links', () => {
    expect(normalizeRequisitionLinkage({ ...EMPTY_REQUISITION_LINKAGE })).toEqual({
      sourcePlanItemId: undefined,
      budgetId: undefined,
      procurementCategory: undefined,
      costCenter: undefined,
      projectId: undefined,
      requisitionType: 'StockReplenishment',
      specificationTemplateId: undefined,
      approvedExceptionRuleId: undefined,
      exceptionWorkflowInstanceId: undefined,
      exceptionApprovalReference: undefined,
      exceptionEvidenceReference: undefined,
    });
    expect(validateExceptionLinkage({ ...EMPTY_REQUISITION_LINKAGE })).toBeNull();
  });

  it('requires a completed shared workflow reference only when an exception rule is selected', () => {
    expect(validateExceptionLinkage({
      requisitionType: 'EmergencyPurchase',
      approvedExceptionRuleId: 'exception-rule',
    })).toContain('completed shared exception workflow');
    expect(validateExceptionLinkage({
      requisitionType: 'EmergencyPurchase',
      approvedExceptionRuleId: 'exception-rule',
      exceptionWorkflowInstanceId: 'workflow-instance',
    })).toBeNull();
  });

  it('maps persisted snapshots back to the editable reference contract', () => {
    const editable = toEditableRequisitionLinkage({
      revision: 3,
      requisitionType: 'ServiceProcurement',
      sourcePlanId: 'plan',
      sourcePlanItemId: 'item',
      sourcePlanNumber: 'APP-2026-01',
      budgetId: 'budget',
      budgetCode: 'BUD-01',
      procurementCategory: 'GeneralServices',
      costCenter: 'CC-020',
      specificationTemplateId: 'template',
    });

    expect(editable).toEqual(expect.objectContaining({
      sourcePlanItemId: 'item',
      budgetId: 'budget',
      procurementCategory: 'GeneralServices',
      costCenter: 'CC-020',
      requisitionType: 'ServiceProcurement',
      specificationTemplateId: 'template',
    }));
    expect(editable).not.toHaveProperty('sourcePlanNumber');
  });
});
