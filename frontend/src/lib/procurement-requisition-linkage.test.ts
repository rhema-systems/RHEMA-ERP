import { describe, expect, it } from 'vitest';
import {
  EMPTY_REQUISITION_LINKAGE,
  applyPlanItemToRequisitionLinkage,
  deriveRequisitionLinkageFromPlanItem,
  formatRequisitionMoney,
  getRequisitionCurrency,
  normalizeRequisitionLinkage,
  toEditableRequisitionLinkage,
  validateExceptionLinkage,
} from './procurement-requisition-linkage';

describe('purchase requisition linkage helpers', () => {
  it('inherits budget and category from the selected approved plan item without a cost centre', () => {
    const result = applyPlanItemToRequisitionLinkage({
      requisitionType: 'StockReplenishment',
      budgetId: 'browser-budget',
      costCenter: 'browser-cost-centre',
    }, {
      id: 'plan-item',
      code: 'APP-2026-01',
      name: 'Wireless Keyboard',
      budgetId: 'approved-budget',
      departmentId: 'it-department',
      category: 'Goods',
    });

    expect(result).toEqual({
      requisitionType: 'StockReplenishment',
      sourcePlanItemId: 'plan-item',
      sourcePlanItemIds: ['plan-item'],
      budgetId: 'approved-budget',
      procurementCategory: 'Goods',
      costCenter: undefined,
    });
  });

  it('retains both plan-item links when normalizing a multi-line requisition', () => {
    const result = normalizeRequisitionLinkage({
      sourcePlanItemId: ' item-a ',
      sourcePlanItemIds: [' item-a ', 'item-b', ' '],
      budgetId: ' approved-budget ',
      procurementCategory: 'Goods',
      requisitionType: 'StockReplenishment',
    });

    expect(result.sourcePlanItemId).toBe('item-a');
    expect(result.sourcePlanItemIds).toEqual(['item-a', 'item-b']);
    expect(result.budgetId).toBe('approved-budget');
    expect(result.procurementCategory).toBe('Goods');
  });

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

  it('uses the plan item explicit linked budget instead of inferring it from a plan parent', () => {
    const result = deriveRequisitionLinkageFromPlanItem(
      { ...EMPTY_REQUISITION_LINKAGE },
      'item-1',
      {
        planItems: [{
          id: 'item-1',
          code: 'PP-2026-001',
          name: 'Office furniture',
          parentId: 'plan-1',
          linkedBudgetId: 'budget-1',
          category: 'Goods',
          currency: 'GHS',
        }],
        budgets: [{
          id: 'budget-1',
          code: 'PB-2026-001',
          name: 'Administration budget',
          parentId: 'plan-1',
          currency: 'GHS',
        }],
        projects: [],
        specificationTemplates: [],
        approvedExceptionRules: [],
        approvedExceptionWorkflows: [],
        categories: [],
        requestTypes: [],
        costCenters: [],
      }
    );

    expect(result).toEqual(expect.objectContaining({
      sourcePlanItemId: 'item-1',
      budgetId: 'budget-1',
      procurementCategory: 'Goods',
    }));
  });

  it('uses the linked currency or the controlled Finance base currency', () => {
    const options = {
      planItems: [],
      budgets: [{ id: 'budget-1', code: 'PB-1', name: 'Budget', currency: 'EUR' }],
      projects: [],
      specificationTemplates: [],
      approvedExceptionRules: [],
      approvedExceptionWorkflows: [],
      categories: [],
      requestTypes: [],
      costCenters: [],
    };

    expect(getRequisitionCurrency({ ...EMPTY_REQUISITION_LINKAGE, budgetId: 'budget-1' }, options)).toBe('EUR');
    expect(getRequisitionCurrency({ ...EMPTY_REQUISITION_LINKAGE }, options, 'GHS')).toBe('GHS');
    expect(getRequisitionCurrency({ ...EMPTY_REQUISITION_LINKAGE })).toBe('XXX');
    expect(formatRequisitionMoney(4900, 'GHS')).toContain('GHS');
  });
});
