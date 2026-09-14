import type {
  FinanceDimensionAccountRule,
  FinanceDimensionDefinition,
  FinancePostingDimensionValue,
  FinanceSourceDocumentDimension,
  FinanceSourceDimensionValue,
} from '@/types/finance';

export const toFinancePostingDimensionValues = (
  values: Record<string, string>
): FinancePostingDimensionValue[] =>
  Object.entries(values)
    .filter(([dimensionCode, valueCode]) => Boolean(dimensionCode && valueCode))
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([dimensionCode, valueCode]) => ({ dimensionCode, valueCode }));

export const toFinanceDimensionValueRecord = (
  values: FinanceSourceDimensionValue[]
): Record<string, string> =>
  Object.fromEntries(
    values
      .filter((value) => Boolean(value.dimensionCode && value.valueCode))
      .map((value) => [value.dimensionCode, value.valueCode])
  );

export const toFinanceSourceDimensionFormState = (
  evidence?: FinanceSourceDocumentDimension
) => ({
  defaultValues: toFinanceDimensionValueRecord(evidence?.defaultValues ?? []),
  lineValues: Object.fromEntries(
    (evidence?.lines ?? []).map((line) => [
      line.sourceLineId,
      toFinanceDimensionValueRecord(line.values),
    ])
  ),
});

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

export interface FinanceSourceLineDimensionConflict {
  dimensionDefinitionId: string;
  dimensionCode: string;
  message: string;
}

/** Resolve specificity per account before combining the accounts of one posting line. */
export const getSourceLineDimensionRuleResolution = (
  rules: FinanceDimensionAccountRule[],
  accountId: string,
  effectiveDate: string,
  context: FinanceDimensionRuleContext,
  additionalAccountIds: readonly string[] = []
): {
  rules: FinanceDimensionAccountRule[];
  conflicts: FinanceSourceLineDimensionConflict[];
} => {
  const accountIds = [
    ...new Set([accountId, ...additionalAccountIds].filter(Boolean)),
  ];
  if (accountIds.length <= 1) {
    return {
      rules: getApplicableDimensionRules(
        rules,
        accountId,
        effectiveDate,
        context
      ),
      conflicts: [],
    };
  }
  const byDimension = new Map<string, FinanceDimensionAccountRule[]>();
  for (const id of accountIds) {
    for (const rule of getApplicableDimensionRules(
      rules,
      id,
      effectiveDate,
      context
    )) {
      const group = byDimension.get(rule.financeDimensionDefinitionId) ?? [];
      group.push(rule);
      byDimension.set(rule.financeDimensionDefinitionId, group);
    }
  }
  const merged: FinanceDimensionAccountRule[] = [];
  const conflicts: FinanceSourceLineDimensionConflict[] = [];
  for (const [definitionId, group] of byDimension) {
    const fixed = group.filter((rule) => rule.ruleType === 'Fixed');
    const required = group.find((rule) => rule.ruleType === 'Required');
    const prohibited = group.find((rule) => rule.ruleType === 'Prohibited');
    const selected = prohibited ?? fixed[0] ?? required ?? group[0];
    const fixedCodes = [
      ...new Set(fixed.map((rule) => rule.defaultValueCode).filter(Boolean)),
    ];
    const conflictingProhibition = Boolean(
      prohibited && (fixed.length || required)
    );
    const conflictingFixed = fixedCodes.length > 1;
    const defaults = [
      ...new Set(group.map((rule) => rule.defaultValueCode).filter(Boolean)),
    ];
    const defaultCode =
      conflictingProhibition || conflictingFixed || prohibited
        ? undefined
        : fixed.length
          ? fixedCodes[0]
          : defaults.length === 1
            ? defaults[0]
            : undefined;
    merged.push({
      ...selected,
      defaultValueCode: defaultCode,
      defaultDimensionValueId: defaultCode
        ? group.find((rule) => rule.defaultValueCode === defaultCode)
            ?.defaultDimensionValueId
        : undefined,
    });
    if (conflictingProhibition || conflictingFixed) {
      const accounts = group
        .map((rule) => rule.accountNumber || rule.accountName || rule.accountId)
        .join(', ');
      conflicts.push({
        dimensionDefinitionId: definitionId,
        dimensionCode: selected.dimensionCode,
        message: `${selected.dimensionName}: accounts ${accounts} ${
          conflictingProhibition
            ? 'both require and prohibit this dimension'
            : 'require different Fixed values'
        }. Ask Finance to align their dimension rules.`,
      });
    }
  }
  return { rules: merged, conflicts };
};

/** Existing source evidence owns server-resolved posting accounts; never infer them. */
export const getSourceLineDimensionAccounts = (
  evidence: FinanceSourceDocumentDimension | undefined,
  sourceLineId: string,
  editableAccountId?: string
) => {
  const line = evidence?.lines.find(
    (item) => item.sourceLineId === sourceLineId
  );
  const accountId = editableAccountId || line?.accountId || undefined;
  return {
    accountId,
    additionalAccountIds: [...new Set(line?.additionalAccountIds ?? [])].filter(
      (id) => id && id !== accountId
    ),
    requiredDimensionCodes: line?.requiredDimensionCodes ?? [],
  };
};

export const resolveSourceDimensionValues = (
  definitions: FinanceDimensionDefinition[],
  rules: FinanceDimensionAccountRule[],
  accountId: string,
  effectiveDate: string,
  context: FinanceDimensionRuleContext,
  preferredValues: Record<string, string>,
  additionalAccountIds: readonly string[] = []
) => {
  const resolution = getSourceLineDimensionRuleResolution(
    rules,
    accountId,
    effectiveDate,
    context,
    additionalAccountIds
  );
  const applicableRules = resolution.rules;
  const resolved: Record<string, string> = {};
  for (const definition of definitions) {
    if (
      resolution.conflicts.some(
        (conflict) => conflict.dimensionDefinitionId === definition.id
      )
    )
      continue;
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
  values: Record<string, string>,
  additionalAccountIds: readonly string[] = []
) =>
  getSourceLineDimensionRuleResolution(
    rules,
    accountId,
    effectiveDate,
    context,
    additionalAccountIds
  ).rules.filter(
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
