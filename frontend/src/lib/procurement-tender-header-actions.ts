import type { ProcurementMethodType } from '@/types/procurement-policy';

type TenderHeaderActionInput = {
  tenderId: string;
  tenderType?: string;
  sourcingCaseId?: string;
  sourcingMethod?: ProcurementMethodType | number;
  canReadProcurementRecords: boolean;
};

const legacyMethodNames: Record<number, ProcurementMethodType> = {
  0: 'RequestForQuotation',
  1: 'NationalCompetitiveTendering',
  2: 'InternationalCompetitiveTendering',
  3: 'RestrictedTendering',
  4: 'SingleSource',
  5: 'PettyPurchase',
  6: 'FrameworkCallOff',
  7: 'QualityBasedSelection',
  8: 'QualityAndCostBasedSelection',
};

const exceptionalMethods = new Set<ProcurementMethodType>([
  'RestrictedTendering',
  'SingleSource',
  'PettyPurchase',
]);

const normalizeMethod = (value?: ProcurementMethodType | number) =>
  typeof value === 'number' ? legacyMethodNames[value] : value;

export function getTenderHeaderActions(input: TenderHeaderActionInput) {
  const isTender = input.tenderType !== 'RFQ';
  const visible = isTender && input.canReadProcurementRecords;
  const hasSourcingCase = Boolean(input.sourcingCaseId?.trim());
  const method = normalizeMethod(input.sourcingMethod);
  const sourceQuery =
    method && exceptionalMethods.has(method)
      ? '?sourceType=ExceptionalSourcing'
      : '';
  const root = `/procurement/tenders/${input.tenderId}`;

  return {
    showCommitteeControls: visible && hasSourcingCase,
    showAwardReadiness: visible,
    showGhanepsExchange: visible,
    committeeControlsHref: `${root}/committee-controls`,
    awardReadinessHref: `${root}/award-readiness${sourceQuery}`,
    ghanepsExchangeHref: `${root}/ghaneps-exchange${sourceQuery}`,
  };
}

type ProblemShape = {
  detail?: unknown;
  message?: unknown;
  code?: unknown;
  extensions?: { code?: unknown };
};

export function getProcurementProblemMessage(
  error: unknown,
  fallback = 'The request could not be completed.'
): string {
  if (!error || typeof error !== 'object') return fallback;

  const candidate = error as ProblemShape & {
    response?: ProblemShape & { data?: unknown };
  };
  const responseData = candidate.response?.data;
  const problem =
    responseData && typeof responseData === 'object'
      ? (responseData as ProblemShape)
      : undefined;
  const detail =
    typeof responseData === 'string'
      ? responseData
      : typeof problem?.detail === 'string'
        ? problem.detail
        : typeof problem?.message === 'string'
          ? problem.message
          : typeof candidate.response?.detail === 'string'
            ? candidate.response.detail
            : typeof candidate.response?.message === 'string'
              ? candidate.response.message
              : typeof candidate.detail === 'string'
                ? candidate.detail
                : typeof candidate.message === 'string'
                  ? candidate.message
                  : fallback;
  const code =
    problem?.code ??
    problem?.extensions?.code ??
    candidate.response?.code ??
    candidate.response?.extensions?.code ??
    candidate.code ??
    candidate.extensions?.code;

  return typeof code === 'string' && code.trim() && !detail.includes(code)
    ? `${detail} (${code})`
    : detail;
}
