import type { FinanceDimensionAccountRule } from '@/types/finance';

const specificity = (rule: FinanceDimensionAccountRule) =>
  [rule.sourceModule, rule.sourceDocumentType, rule.postingAction].filter(Boolean).length;

export const getApplicableManualDimensionRules = (
  rules: FinanceDimensionAccountRule[],
  accountId: string,
  effectiveDate: string,
) => {
  const candidates = rules.filter(rule => rule.accountId === accountId && rule.isActive
    && rule.effectiveDate.slice(0, 10) <= effectiveDate
    && (!rule.expiryDate || rule.expiryDate.slice(0, 10) >= effectiveDate)
    && (!rule.sourceModule || rule.sourceModule === 'GL')
    && (!rule.sourceDocumentType || rule.sourceDocumentType === 'ManualJournalEntry')
    && (!rule.postingAction || rule.postingAction === 'Post'));

  return Array.from(new Set(candidates.map(rule => rule.financeDimensionDefinitionId))).map(definitionId =>
    candidates.filter(rule => rule.financeDimensionDefinitionId === definitionId)
      .sort((left, right) => specificity(right) - specificity(left))[0]
  );
};

export const getDefaultManualDimensionValues = (
  rules: FinanceDimensionAccountRule[],
  accountId: string,
  effectiveDate: string,
) => Object.fromEntries(
  getApplicableManualDimensionRules(rules, accountId, effectiveDate)
    .flatMap(rule => rule.ruleType !== 'Prohibited' && rule.defaultValueCode
      ? [[rule.dimensionCode, rule.defaultValueCode] as const]
      : []),
);

export const getMissingRequiredManualDimension = (
  rules: FinanceDimensionAccountRule[],
  accountId: string,
  effectiveDate: string,
  values: Record<string, string>,
) => getApplicableManualDimensionRules(rules, accountId, effectiveDate)
  .find(rule => rule.ruleType === 'Required' && !rule.defaultValueCode && !values[rule.dimensionCode]);
