import { describe, expect, it } from 'vitest';

import {
  createDecisionFormValue,
  displayDecisionFormField,
  normalizeDecisionFormValue,
  procurementDecisionFormRegistry,
  procurementDecisionOwnerOptions,
  updateDecisionFormField,
} from './procurement-configuration-registry';

describe('procurement decision form registry', () => {
  it('registers exactly DEC-001 through DEC-014', () => {
    expect(Object.keys(procurementDecisionFormRegistry)).toEqual(
      Array.from({ length: 14 }, (_, index) => `DEC-${String(index + 1).padStart(3, '0')}`),
    );
  });

  it('gives every decision effective dating and an explicit owner option', () => {
    for (const definition of Object.values(procurementDecisionFormRegistry)) {
      expect(definition.fields.some(field => field.key === 'effectiveFrom' && field.type === 'date')).toBe(true);
      expect(definition.fields.some(field => field.key === 'effectiveTo' && field.type === 'date')).toBe(true);
    }
    expect(procurementDecisionOwnerOptions.length).toBeGreaterThanOrEqual(10);
  });

  it('creates isolated default values and overlays persisted values', () => {
    const first = createDecisionFormValue('DEC-001', { serviceClass: 'Works' });
    const second = createDecisionFormValue('DEC-001');
    expect(first.serviceClass).toBe('Works');
    expect(second.serviceClass).toBeUndefined();
    first.category = 'works';
    expect(second.category).toBe('goods');
  });

  it('normalizes number and list inputs through the shared field model', () => {
    const fields = procurementDecisionFormRegistry['DEC-002'].fields;
    const amount = fields.find(field => field.key === 'lowerBound');
    const categories = fields.find(field => field.key === 'applicableCategories');
    if (!amount || !categories) throw new Error('Expected shared registry fields were not found.');
    const withAmount = updateDecisionFormField({}, amount, '1500.25');
    const withCategories = updateDecisionFormField(withAmount, categories, 'goods, works, consultancyServices');
    expect(withCategories.lowerBound).toBe(1500.25);
    expect(withCategories.applicableCategories).toEqual(['goods', 'works', 'consultancyServices']);
    expect(displayDecisionFormField(withCategories.applicableCategories, 'textList')).toBe('goods, works, consultancyServices');
  });

  it('rejects an unregistered decision key', () => {
    expect(() => createDecisionFormValue('DEC-999')).toThrow('Unknown procurement decision key DEC-999');
  });

  it('captures the complete DEC-007 fee, Finance, exemption, and receipt contract', () => {
    const definition = procurementDecisionFormRegistry['DEC-007'];
    expect(definition.fields.map(field => field.key)).toEqual(expect.arrayContaining([
      'mode',
      'feeType',
      'amount',
      'currencyCode',
      'taxPercent',
      'paymentChannels',
      'revenueAccountId',
      'taxAccountId',
      'exemptionWorkflowDefinitionId',
      'receiptNumberFormat',
      'exemptionRule',
      'refundRule',
      'renewalRule',
      'effectiveFrom',
      'effectiveTo',
    ]));
    expect(createDecisionFormValue('DEC-007')).toMatchObject({
      mode: 'paid',
      currencyCode: 'GHS',
      paymentChannels: [],
      receiptNumberFormat: 'SUP-REC-{YYYY}-{######}',
    });
    expect(createDecisionFormValue('DEC-007')).not.toHaveProperty('effectiveTo');
    expect(definition.fields.find(field => field.key === 'receiptNumberFormat'))
      .toMatchObject({ placeholder: 'SUP-REC-{YYYY}-{######}' });
  });

  it('omits blank optional dates before sending a decision to the API', () => {
    expect(normalizeDecisionFormValue('DEC-007', {
      effectiveFrom: '2026-08-04',
      effectiveTo: '',
      receiptNumberFormat: 'SUP-REC-{SEQ}',
    })).toEqual({
      effectiveFrom: '2026-08-04',
      receiptNumberFormat: 'SUP-REC-{SEQ}',
    });
  });

  it('captures the complete DEC-011 risk, exposure, and award-action contract', () => {
    const definition = procurementDecisionFormRegistry['DEC-011'];
    expect(definition.fields.map(field => field.key)).toEqual(expect.arrayContaining([
      'reviewFrequencyMonths',
      'exposureWindowMonths',
      'riskDimensions',
      'riskBands',
      'concentrationLimitPercent',
      'minimumScore',
      'eligibilityAction',
      'performanceWindowMonths',
      'performanceDimensions',
      'performanceBands',
      'minimumPerformanceDataCoveragePercent',
      'responseTargetHours',
      'performanceEligibilityAction',
      'effectiveFrom',
      'effectiveTo',
    ]));
    expect(definition.fields.find(field => field.key === 'eligibilityAction'))
      .toMatchObject({ type: 'select', required: true });
    expect(createDecisionFormValue('DEC-011')).toMatchObject({
      eligibilityAction: 'alertOnly',
      riskDimensions: [],
      riskBands: [],
      performanceDimensions: [],
      performanceBands: [],
      performanceEligibilityAction: 'alertOnly',
    });
  });
});
