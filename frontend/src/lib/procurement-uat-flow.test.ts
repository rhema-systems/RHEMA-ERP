import { describe, expect, it } from 'vitest';

import {
  buildProcurementUatFlow,
  getProcurementUatStageContext,
  PROCUREMENT_UAT_STAGE_IDS,
  procurementUatProgress,
} from './procurement-uat-flow';

describe('procurement UAT flow', () => {
  it('retains the governed end-to-end stage order', () => {
    expect(PROCUREMENT_UAT_STAGE_IDS).toEqual([
      'budget-plan',
      'ghaneps-exchange',
      'purchase-requisition',
      'sourcing-release-case',
      'tender-rfq-preparation',
      'tender-documents-publication',
      'supplier-bidding',
      'evaluation-committee',
      'bid-opening-evaluation',
      'award',
      'purchase-order-commitment',
      'contract-activation',
    ]);
  });

  it('uses only supplied record states and identifies the immediate dependency context', () => {
    const flow = buildProcurementUatFlow('tender-documents-publication', {
      'tender-rfq-preparation': { status: 'complete' },
      'tender-documents-publication': {
        status: 'blocked',
        blockers: ['No effective published template is bound.'],
      },
      'supplier-bidding': { status: 'not-started' },
    });
    const context = getProcurementUatStageContext(
      flow,
      'tender-documents-publication'
    );

    expect(context.previous?.id).toBe('tender-rfq-preparation');
    expect(context.previous?.state.status).toBe('complete');
    expect(context.current.state.status).toBe('blocked');
    expect(context.next?.id).toBe('supplier-bidding');
    expect(flow[0].state.status).toBe('unknown');
  });

  it('counts complete and not-applicable stages without treating unknown as complete', () => {
    const flow = buildProcurementUatFlow('purchase-requisition', {
      'budget-plan': { status: 'complete' },
      'ghaneps-exchange': { status: 'skipped' },
      'purchase-requisition': { status: 'in-progress' },
    });

    expect(procurementUatProgress(flow)).toEqual({ completed: 2, total: 12 });
  });
});
