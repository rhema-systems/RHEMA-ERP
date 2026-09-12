import { describe, expect, it } from 'vitest';
import type { EvaluationTemplate } from '@/services/evaluationTemplateService';
import { getTemplateEvaluationSettings, getTenderEvaluationConfigurationError } from './tender-evaluation-configuration';

const template = {
  id: 'template', isActive: true, scoringMethod: 'QCBS',
  technicalWeight: 70, financialWeight: 30, minimumTechnicalScore: 80,
} as EvaluationTemplate;
const tender = { evaluationTemplateId: 'template', ...getTemplateEvaluationSettings(template) };

describe('tender evaluation configuration', () => {
  it.each(['WeightedAverage', 'SimpleAverage', 'PassFail', 'QCBS'])('accepts aligned %s', scoringMethod => {
    const selected = { ...template, scoringMethod };
    expect(getTenderEvaluationConfigurationError({ ...tender, ...getTemplateEvaluationSettings(selected) }, selected)).toBeUndefined();
  });
  it('rejects QCBS template with QCBS disabled', () => {
    expect(getTenderEvaluationConfigurationError({ ...tender, useQCBSEvaluation: false }, template)).toContain('disabled');
  });
  it('rejects standard template with QCBS enabled', () => {
    expect(getTenderEvaluationConfigurationError(tender, { ...template, scoringMethod: 'WeightedAverage' })).toContain('enabled');
  });
  it('rejects drifted weights and threshold', () => {
    expect(getTenderEvaluationConfigurationError({ ...tender, technicalWeight: 60 }, template)).toContain('differ');
    expect(getTenderEvaluationConfigurationError({ ...tender, minimumTechnicalScore: 70 }, template)).toContain('differ');
  });
  it('fails closed on unavailable, inactive, stale, and unknown template', () => {
    expect(getTenderEvaluationConfigurationError(tender, null)).toContain('unavailable');
    expect(getTenderEvaluationConfigurationError(tender, { ...template, id: 'stale' })).toContain('unavailable');
    expect(getTenderEvaluationConfigurationError(tender, { ...template, isActive: false })).toContain('unavailable');
    expect(getTenderEvaluationConfigurationError(tender, { ...template, scoringMethod: 'Unknown' })).toContain('unsupported');
  });
  it('resets QCBS when switching to a standard template', () => {
    expect(getTemplateEvaluationSettings({ ...template, scoringMethod: 'WeightedAverage' }).useQCBSEvaluation).toBe(false);
  });
  it('preserves the template-free legacy route', () => {
    expect(getTenderEvaluationConfigurationError({}, null)).toBeUndefined();
  });
});
