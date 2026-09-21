import { describe, expect, it } from 'vitest';
import {
  isQsOptionalFeatureEnabled,
  qsOptionalFeatures,
  isQsExtensionDecision,
} from './quantity-survey-architecture-scope';

describe('QS architecture scope', () => {
  it('hides every questionnaire extension by default', () => {
    for (const feature of qsOptionalFeatures)
      expect(isQsOptionalFeatureEnabled(feature, '')).toBe(false);
  });
  it('requires an explicit feature opt-in and does not enable adjacent tools', () => {
    expect(
      isQsOptionalFeatureEnabled('daywork', ' daywork,subcontracts ')
    ).toBe(true);
    expect(
      isQsOptionalFeatureEnabled('escalation', ' daywork,subcontracts ')
    ).toBe(false);
    expect(isQsOptionalFeatureEnabled('daywork', '*')).toBe(false);
  });
  it('keeps valuation, approval and retention configuration in the core register', () => {
    for (const key of ['QS-DEC-003', 'QS-DEC-008', 'QS-DEC-009', 'QS-DEC-011'])
      expect(isQsExtensionDecision(key)).toBe(false);
    for (const key of ['QS-DEC-006', 'QS-DEC-013', 'QS-DEC-014', 'QS-DEC-017'])
      expect(isQsExtensionDecision(key)).toBe(true);
  });
});
