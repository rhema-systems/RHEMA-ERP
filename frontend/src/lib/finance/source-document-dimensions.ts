import type {
  FinanceDimensionAccountRule,
  FinanceDimensionDefinition,
  FinancePostingDimensionValue,
  FinanceSourceDimensionValue,
} from '@/types/finance';

export const toFinancePostingDimensionValues = (
  values: Record<string, string>,
): FinancePostingDimensionValue[] =>
  Object.entries(values)
    .filter(([dimensionCode, valueCode]) => Boolean(dimensionCode && valueCode))
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([dimensionCode, valueCode]) => ({ dimensionCode, valueCode }));

export const toFinanceDimensionValueRecord = (
  values: FinanceSourceDimensionValue[],
): Record<string, string> =>
  Object.fromEntries(
    values
      .filter((value) => Boolean(value.dimensionCode && value.valueCode))
      .map((value) => [value.dimensionCode, value.valueCode]),
  );

export interface FinanceDimensionRuleContext {
  sourceModule: string;
  sourceDocumentType: string;
  postingAction?: string;
  sourceRoute?: string;
  contractVersion?: string;
}

const specificity = (
  rule: FinanceDimensionAccountRule,
  context: FinanceDimensionRuleContext
) =>
  (rule.sourceRoute && rule.sourceRoute === context.sourceRoute ? 8 : 0) +
  (rule.sourceModule ? 4 : 0) +
  (rule.sourceDocumentType ? 2 : 0) +
  (rule.postingAction ? 1 : 0);

export const getApplicableDimensionRules = (
  rules: FinanceDimensionAccountRule[],
  accountId: string,
  effectiveDate: string,
  context: FinanceDimensionRuleContext
) => {
  const candidates = rules.filter(
    (rule) =>
      rule.accountId === accountId &&
      rule.isActive &&
      rule.effectiveDate.slice(0, 10) <= effectiveDate &&
      (!rule.expiryDate || rule.expiryDate.slice(0, 10) >= effectiveDate) &&
      (!rule.sourceModule || rule.sourceModule === context.sourceModule) &&
      (!rule.sourceDocumentType ||
        rule.sourceDocumentType === context.sourceDocumentType) &&
      (!rule.postingAction ||
        rule.postingAction === (context.postingAction ?? 'Post')) &&
      (!rule.sourceRoute || rule.sourceRoute === context.sourceRoute) &&
      (!rule.contractVersion ||
        rule.contractVersion === context.contractVersion)
  );
  return Array.from(
    new Set(candidates.map((rule) => rule.financeDimensionDefinitionId))
  ).map(
    (definitionId) =>
      candidates
        .filter((rule) => rule.financeDimensionDefinitionId === definitionId)
        .sort(
          (left, right) =>
            specificity(right, context) - specificity(left, context)
        )[0]
  );
};

export const getActiveDimensionValues = (
  dimension: FinanceDimensionDefinition,
  effectiveDate: string
) =>
  dimension.values.filter(
    (value) =>
      value.isActive &&
      value.effectiveDate.slice(0, 10) <= effectiveDate &&
      (!value.expiryDate || value.expiryDate.slice(0, 10) >= effectiveDate)
  );

export const resolveSourceDimensionValues = (
  definitions: FinanceDimensionDefinition[],
  rules: FinanceDimensionAccountRule[],
  accountId: string,
  effectiveDate: string,
  context: FinanceDimensionRuleContext,
  preferredValues: Record<string, string>
) => {
  const applicableRules = getApplicableDimensionRules(
    rules,
    accountId,
    effectiveDate,
    context
  );
  const resolved: Record<string, string> = {};
  for (const definition of definitions) {
    const rule = applicableRules.find(
      (item) => item.financeDimensionDefinitionId === definition.id
    );
    if (rule?.ruleType === 'Prohibited') continue;
    const activeCodes = new Set(
      getActiveDimensionValues(definition, effectiveDate).map(
        (value) => value.code
      )
    );
    const candidate =
      rule?.ruleType === 'Fixed'
        ? rule.defaultValueCode
        : rule?.defaultValueCode || preferredValues[definition.code];
    if (candidate && activeCodes.has(candidate))
      resolved[definition.code] = candidate;
  }
  return resolved;
};

export const getMissingRequiredDimensions = (
  rules: FinanceDimensionAccountRule[],
  accountId: string,
  effectiveDate: string,
  context: FinanceDimensionRuleContext,
  values: Record<string, string>
) =>
  getApplicableDimensionRules(rules, accountId, effectiveDate, context).filter(
    (rule) =>
      rule.ruleType === 'Required' &&
      !rule.defaultValueCode &&
      !values[rule.dimensionCode]
  );

export const getCommonDimensionValues = (values: Record<string, string>[]) => {
  if (values.length === 0) return {};
  return Object.fromEntries(
    Object.entries(values[0]).filter(
      ([code, value]) =>
        Boolean(value) && values.every((item) => item[code] === value)
    )
  );
};

export const getDimensionSummary = (
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
