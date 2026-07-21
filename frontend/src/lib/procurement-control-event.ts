import type {
  ProcurementControlEventResult,
  ProcurementControlEventSearch,
} from '@/types/procurement-control-event';

export const procurementControlEventResults: ProcurementControlEventResult[] = [
  'Succeeded',
  'Allowed',
  'ReviewRequired',
  'Warning',
  'Rejected',
  'Denied',
  'Failed',
];

export const procurementControlEventResultTone = (
  result: ProcurementControlEventResult
) => {
  if (result === 'Allowed' || result === 'Succeeded')
    return 'border-emerald-500/30 bg-emerald-500/10 text-emerald-800';
  if (result === 'Denied' || result === 'Rejected' || result === 'Failed')
    return 'border-destructive/30 bg-destructive/10 text-destructive';
  return 'border-amber-500/30 bg-amber-500/10 text-amber-800';
};

export const compactProcurementControlEventSearch = (
  request: ProcurementControlEventSearch
) =>
  Object.fromEntries(
    Object.entries(request).filter(
      ([, value]) => value !== undefined && value !== ''
    )
  ) as ProcurementControlEventSearch;

export const formatProcurementControlJson = (value?: string) => {
  if (!value) return 'No values recorded.';
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
};

export const procurementControlLineage = (
  ruleCode?: string,
  ruleVersion?: string,
  decisionKeys: string[] = []
) => {
  const rule = ruleCode
    ? `${ruleCode}${ruleVersion ? ` v${ruleVersion}` : ''}`
    : 'No rule code';
  return decisionKeys.length ? `${rule} · ${decisionKeys.join(', ')}` : rule;
};
