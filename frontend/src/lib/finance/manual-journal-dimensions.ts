import type {
  FinanceDimensionAccountRule,
  FinanceDimensionDefinition,
} from '@/types/finance';
import {
  getActiveDimensionValues,
  getApplicableDimensionRules,
  getCommonDimensionValues,
  getDimensionSummary,
  getMissingRequiredDimensions,
  resolveSourceDimensionValues,
} from './source-document-dimensions';

const manualContext = {
  sourceModule: 'GL',
  sourceDocumentType: 'ManualJournalEntry',
  postingAction: 'Post',
  sourceRoute: 'finance.gl.manual-journals',
  contractVersion: '1.1',
};

export const getApplicableManualDimensionRules = (
  rules: FinanceDimensionAccountRule[],
  accountId: string,
  effectiveDate: string
) =>
  getApplicableDimensionRules(rules, accountId, effectiveDate, manualContext);

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
  getMissingRequiredDimensions(
    rules,
    accountId,
    effectiveDate,
    manualContext,
    values
  )[0];

export const getActiveManualDimensionValues = getActiveDimensionValues;

export const resolveManualDimensionValues = (
  definitions: FinanceDimensionDefinition[],
  rules: FinanceDimensionAccountRule[],
  accountId: string,
  effectiveDate: string,
  preferredValues: Record<string, string>
) =>
  resolveSourceDimensionValues(
    definitions,
    rules,
    accountId,
    effectiveDate,
    manualContext,
    preferredValues
  );

export const getCommonManualDimensionValues = getCommonDimensionValues;
export const getManualDimensionSummary = getDimensionSummary;
