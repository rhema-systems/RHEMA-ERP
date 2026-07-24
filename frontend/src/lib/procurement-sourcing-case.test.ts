import { describe, expect, it } from 'vitest';

import {
  procurementSourcingCaseActions,
  validateProcurementSourcingCase,
} from './procurement-sourcing-case';
import type {
  CreateProcurementSourcingCase,
  ProcurementSourcingCaseLineOption,
} from '@/types/procurement-sourcing-case';

const lines: ProcurementSourcingCaseLineOption[] = [
  {
    id: 'line-1',
    lineNumber: 1,
    description: 'Laptops',
    quantity: 2,
    unitOfMeasure: 'EA',
    estimatedUnitPrice: 10,
    lineTotal: 20,
  },
  {
    id: 'line-2',
    lineNumber: 2,
    description: 'Docking stations',
    quantity: 2,
    unitOfMeasure: 'EA',
    estimatedUnitPrice: 5,
    lineTotal: 10,
  },
];

const request = (): CreateProcurementSourcingCase => ({
  requisitionId: 'pr-1',
  justification: 'Approved sourcing approach.',
  lots: [
    {
      title: 'ICT equipment',
      purchaseRequisitionItemIds: ['line-1', 'line-2'],
    },
  ],
});

describe('procurement sourcing-case presentation controls', () => {
  it('requires exact, non-overlapping coverage of every requisition line', () => {
    expect(validateProcurementSourcingCase(request(), lines)).toBeUndefined();
    const missing = request();
    missing.lots[0].purchaseRequisitionItemIds = ['line-1'];
    expect(validateProcurementSourcingCase(missing, lines)).toContain(
      'Every requisition line'
    );
    const duplicate = request();
    duplicate.lots.push({
      title: 'Duplicate',
      purchaseRequisitionItemIds: ['line-1'],
    });
    expect(validateProcurementSourcingCase(duplicate, lines)).toContain(
      'only one lot'
    );
  });

  it('accepts the server recommendation without a client-selected method', () => {
    expect(
      validateProcurementSourcingCase(request(), lines, 'RequestForQuotation')
    ).toBeUndefined();
  });

  it('requires a reason when a client requests a different method', () => {
    const override = request();
    override.selectedMethod = 'SingleSource';
    expect(
      validateProcurementSourcingCase(override, lines, 'RequestForQuotation')
    ).toContain('approved method override');
    override.methodOverrideReason = 'Approved compatibility exception.';
    expect(
      validateProcurementSourcingCase(override, lines, 'RequestForQuotation')
    ).toBeUndefined();
  });

  it('requires justification and a named lot', () => {
    const invalid = request();
    invalid.justification = 'no';
    expect(validateProcurementSourcingCase(invalid, lines)).toContain(
      'at least 5'
    );
    invalid.justification = 'Valid reason';
    invalid.lots[0].title = '';
    expect(validateProcurementSourcingCase(invalid, lines)).toContain(
      'requires a title'
    );
  });

  it('keeps close and cancel actions lifecycle-specific', () => {
    expect(
      procurementSourcingCaseActions(
        { status: 'Ready', isSourceCurrent: true },
        true
      )
    ).toEqual({ canClose: false, canCancel: true });
    expect(
      procurementSourcingCaseActions(
        { status: 'InProgress', isSourceCurrent: true },
        true
      )
    ).toEqual({ canClose: true, canCancel: true });
    expect(
      procurementSourcingCaseActions(
        { status: 'InProgress', isSourceCurrent: false },
        true
      ).canClose
    ).toBe(false);
    expect(
      procurementSourcingCaseActions(
        { status: 'Closed', isSourceCurrent: true },
        true
      )
    ).toEqual({ canClose: false, canCancel: false });
  });
});
