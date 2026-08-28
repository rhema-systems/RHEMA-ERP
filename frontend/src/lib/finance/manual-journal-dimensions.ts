import type {
  FinanceDimensionAccountRule,
  FinanceDimensionDefinition,
} from '@/types/finance';

const specificity = (rule: FinanceDimensionAccountRule) =>
  [rule.sourceModule, rule.sourceDocumentType, rule.postingAction].filter(
    Boolean
  ).length;

export const getApplicableManualDimensionRules = (
  rules: FinanceDimensionAccountRule[],
  accountId: string,
  effectiveDate: string
) => {
  const candidates = rules.filter(
    (rule) =>
      rule.accountId === accountId &&
      rule.isActive &&
      rule.effectiveDate.slice(0, 10) <= effectiveDate &&
      (!rule.expiryDate || rule.expiryDate.slice(0, 10) >= effectiveDate) &&
      (!rule.sourceModule || rule.sourceModule === 'GL') &&
      (!rule.sourceDocumentType ||
        rule.sourceDocumentType === 'ManualJournalEntry') &&
      (!rule.postingAction || rule.postingAction === 'Post')
  );

  return Array.from(
    new Set(candidates.map((rule) => rule.financeDimensionDefinitionId))
  ).map(
    (definitionId) =>
      candidates
        .filter((rule) => rule.financeDimensionDefinitionId === definitionId)
        .sort((left, right) => specificity(right) - specificity(left))[0]
  );
};

export const getDefaultManualDimensionValues = (
  rules: FinanceDimensionAccountRule[],
  accountId: string,
  effectiveDate: string
) =>
  Object.fromEntries(
    getApplicableManualDimensionRules(rules, accountId, effectiveDate).flatMap(
      (rule) =>
        rule.ruleType !== 'Prohibited' && rule.defaultValueCode
          ? [[rule.dimensionCode, rule.defaultValueCode] as const]
          : []
    )
  );

export const getMissingRequiredManualDimension = (
  rules: FinanceDimensionAccountRule[],
  accountId: string,
  effectiveDate: string,
  values: Record<string, string>
) =>
  getApplicableManualDimensionRules(rules, accountId, effectiveDate).find(
    (rule) =>
      rule.ruleType === 'Required' &&
      !rule.defaultValueCode &&
      !values[rule.dimensionCode]
  );

export const getActiveManualDimensionValues = (
  dimension: FinanceDimensionDefinition,
  effectiveDate: string
) =>
  dimension.values.filter(
    (value) =>
      value.isActive &&
      value.effectiveDate.slice(0, 10) <= effectiveDate &&
      (!value.expiryDate || value.expiryDate.slice(0, 10) >= effectiveDate)
  );

/**
 * Applies operator defaults to a journal line while preserving account-level
 * governance. Prohibited values are removed and configured rule defaults always
 * win, so convenience actions cannot bypass Fixed or Required defaults.
 */
export const resolveManualDimensionValues = (
  definitions: FinanceDimensionDefinition[],
  rules: FinanceDimensionAccountRule[],
  accountId: string,
  effectiveDate: string,
  preferredValues: Record<string, string>
) => {
  const applicableRules = getApplicableManualDimensionRules(
    rules,
    accountId,
    effectiveDate
  );
  const resolved: Record<string, string> = {};

  for (const definition of definitions) {
    const rule = applicableRules.find(
      (item) => item.financeDimensionDefinitionId === definition.id
    );
    if (rule?.ruleType === 'Prohibited') continue;

    const activeCodes = new Set(
      getActiveManualDimensionValues(definition, effectiveDate).map(
        (value) => value.code
      )
    );
    const candidate =
      rule?.defaultValueCode || preferredValues[definition.code];
    if (candidate && activeCodes.has(candidate))
      resolved[definition.code] = candidate;
  }

  return resolved;
};

export const getCommonManualDimensionValues = (
  values: Record<string, string>[]
) => {
  if (values.length === 0) return {};
  return Object.fromEntries(
    Object.entries(values[0]).filter(
      ([code, value]) =>
        Boolean(value) && values.every((item) => item[code] === value)
    )
  );
};

export const getManualDimensionSummary = (
  definitions: FinanceDimensionDefinition[],
  values: Record<string, string>,
  visibleCount = 2
) => {
  const assigned = [...definitions]
    .sort((left, right) => left.displayOrder - right.displayOrder)
    .flatMap((definition) =>
      values[definition.code] ? [values[definition.code]] : []
    );
  if (assigned.length === 0) return 'Add coding';
  const remaining = assigned.length - visibleCount;
  return `${assigned.slice(0, visibleCount).join(' · ')}${remaining > 0 ? ` · +${remaining}` : ''}`;
};
