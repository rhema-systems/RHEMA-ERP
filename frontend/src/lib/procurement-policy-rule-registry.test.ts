import { describe, expect, it } from 'vitest';

import {
  canonicalizeProcurementPolicyOptionValue,
  createProcurementPolicyRuleValue,
  procurementDecisionKeyOptions,
  procurementPolicyRuleKinds,
  procurementPolicyRuleRegistry,
} from './procurement-policy-rule-registry';

describe('procurement policy rule registry', () => {
  it('registers every normalized relational rule family once', () => {
    expect(Object.keys(procurementPolicyRuleRegistry)).toEqual(
      procurementPolicyRuleKinds
    );
    expect(
      new Set(
        Object.values(procurementPolicyRuleRegistry).map(
          (item) => item.payloadKey
        )
      ).size
    ).toBe(7);
  });

  it('anchors every rule family to a governed decision and typed fields', () => {
    for (const definition of Object.values(procurementPolicyRuleRegistry)) {
      expect(procurementDecisionKeyOptions).toContain(
        definition.defaultDecisionKey
      );
      expect(definition.fields.length).toBeGreaterThan(0);
      expect(definition.fields.some((field) => field.required)).toBe(true);
    }
  });

  it('creates effective-dated draft values without runtime enforcement data', () => {
    const value = createProcurementPolicyRuleValue(
      'SegregationOfDuties',
      '2026-01-01',
      '2026-12-31'
    );
    expect(value).toMatchObject({
      ruleCode: '',
      isEnabled: true,
      effectiveFrom: '2026-01-01',
      effectiveTo: '2026-12-31',
      overrideAction: 'Add',
      sourceDecisionKey: 'DEC-004',
      enforcement: 'HardStop',
    });
  });

  it('exposes the full DEC-001 through DEC-014 lineage list', () => {
    expect(procurementDecisionKeyOptions).toEqual(
      Array.from(
        { length: 14 },
        (_, index) => `DEC-${String(index + 1).padStart(3, '0')}`
      )
    );
  });

  it('restores API enum values to their controlled selector option', () => {
    const categoryField = procurementPolicyRuleRegistry.Threshold.fields.find(
      (field) => field.key === 'category'
    );
    const methodField = procurementPolicyRuleRegistry.Threshold.fields.find(
      (field) => field.key === 'method'
    );

    expect(
      canonicalizeProcurementPolicyOptionValue('goods', categoryField?.options)
    ).toBe('Goods');
    expect(
      canonicalizeProcurementPolicyOptionValue(
        'requestForQuotation',
        methodField?.options
      )
    ).toBe('RequestForQuotation');
  });

  it('does not ask policy users to invent an internal evidence requirement key', () => {
    const evidence = procurementPolicyRuleRegistry.Evidence;

    expect(
      evidence.fields.some((field) => field.key === 'sharedRequirementKey')
    ).toBe(false);
    expect(evidence.description).toContain('system generates');
  });

  it('uses centrally configured role selectors for policy role references', () => {
    const roleFields = [
      ['Authority', 'authorityRoleId'],
      ['Authority', 'escalationAuthorityRoleId'],
      ['Exception', 'approverRoleId'],
      ['SegregationOfDuties', 'initiatorRoleId'],
      ['SegregationOfDuties', 'conflictingRoleId'],
    ] as const;

    for (const [kind, key] of roleFields) {
      expect(
        procurementPolicyRuleRegistry[kind].fields.find(
          (field) => field.key === key
        )?.type
      ).toBe('role');
    }
  });
});
