import type { EvaluationTemplate } from '@/services/evaluationTemplateService';

type Configuration = {
  evaluationTemplateId?: string | null;
  useQCBSEvaluation?: boolean;
  technicalWeight?: number;
  financialWeight?: number;
  minimumTechnicalScore?: number;
};

export function getTenderEvaluationConfigurationError(
  tender: Configuration,
  template: EvaluationTemplate | null
): string | undefined {
  if (!tender.evaluationTemplateId) return undefined; // Existing template-free route.
  if (!template || template.id !== tender.evaluationTemplateId || !template.isActive) {
    return 'The assigned evaluation template is unavailable. Reload or ask Procurement to review the tender configuration.';
  }
  if (!['WeightedAverage', 'SimpleAverage', 'PassFail', 'QCBS'].includes(template.scoringMethod)) {
    return 'The assigned template uses an unsupported scoring method. Evaluation is blocked.';
  }
  if ((template.scoringMethod === 'QCBS') !== Boolean(tender.useQCBSEvaluation)) {
    return `The template uses ${template.scoringMethod}, but QCBS evaluation is ${tender.useQCBSEvaluation ? 'enabled' : 'disabled'} on this tender. Procurement must resolve the configuration before scoring. Issued scoring rules have not been changed.`;
  }
  if (tender.useQCBSEvaluation && (
    tender.technicalWeight !== template.technicalWeight ||
    tender.financialWeight !== template.financialWeight ||
    tender.minimumTechnicalScore !== template.minimumTechnicalScore
  )) {
    return 'The tender’s QCBS weights or minimum technical score differ from its template. Procurement must resolve the configuration before scoring.';
  }
  return undefined;
}

/** A selected template owns these settings; switching away from QCBS also resets the flag. */
export function getTemplateEvaluationSettings(template: EvaluationTemplate) {
  return {
    useQCBSEvaluation: template.scoringMethod === 'QCBS',
    technicalWeight: template.technicalWeight,
    financialWeight: template.financialWeight,
    minimumTechnicalScore: template.minimumTechnicalScore,
  };
}
