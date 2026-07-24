import type {
  EvaluateProcurementAwardReadinessRequest,
  ProcurementAwardReadinessDecision,
  ProcurementAwardReadinessPrerequisiteGroup,
  ProcurementAwardReadinessPrerequisiteStatus,
  ProcurementAwardReadinessSourceType,
  ProcurementEvaluatorAwardApproverSodStatus,
} from '@/types/procurement-award-readiness';

const normalize = (value: string) =>
  value.replace(/[^a-z0-9]/gi, '').toLowerCase();

export const hasAwardReadinessAction = (
  actions: string[] | undefined,
  expected: string
) =>
  Boolean(
    actions?.some((action) => normalize(action) === normalize(expected))
  );

export const hasAnyAwardReadinessAction = (
  actions: string[] | undefined,
  expected: string[]
) => expected.some((action) => hasAwardReadinessAction(actions, action));

export const createAwardReadinessIdempotencyKey = () =>
  `tdc0209-evaluate-${crypto.randomUUID()}`;

export const createAwardReadinessEvaluationRequest = (
  latest?: ProcurementAwardReadinessDecision
): EvaluateProcurementAwardReadinessRequest => ({
  idempotencyKey: createAwardReadinessIdempotencyKey(),
  expectedRecommendedSubjectIds: latest?.recommendation.subjectIds ?? [],
  expectedBusinessPartnerIds:
    latest?.recommendation.businessPartnerIds ?? [],
  expectedSourceIntegrityHash: latest?.sourceIntegrityHash,
});

export type AwardReadinessSodPresentation =
  | 'Allowed'
  | 'Blocked'
  | 'NotApplicable';

export const awardReadinessSodPresentation = (
  status: ProcurementEvaluatorAwardApproverSodStatus
): AwardReadinessSodPresentation => {
  if (normalize(status.code) === normalize('SOD_NOT_APPLICABLE'))
    return 'NotApplicable';
  return status.allowed ? 'Allowed' : 'Blocked';
};

export const isAwardReadinessSodPolicyEffective = (
  status: ProcurementEvaluatorAwardApproverSodStatus
) =>
  ['SOD_ALLOWED', 'SOD_CONFLICT'].some(
    (code) => normalize(code) === normalize(status.code)
  );

export const isCurrentActorAwardEvaluator = (
  status: ProcurementEvaluatorAwardApproverSodStatus
) =>
  status.evaluatorUserIds.some(
    (userId) =>
      normalize(userId) === normalize(status.currentActorUserId)
  );

export const awardReadinessSourceLabel = (
  sourceType: ProcurementAwardReadinessSourceType
) => {
  if (sourceType === 'RequestForQuotation') return 'Request for quotation';
  if (sourceType === 'ExceptionalSourcing') return 'Exceptional sourcing';
  return 'Tender';
};

export const awardReadinessGroupLabel: Record<
  ProcurementAwardReadinessPrerequisiteGroup,
  string
> = {
  Source: 'Source and method',
  Recommendation: 'Recommendation',
  Evaluation: 'Completed evaluations',
  ScoreIntegrity: 'Locked score lineage',
  SupplierEligibility: 'Supplier eligibility',
  Prequalification: 'Prequalification',
  VerificationAndDueDiligence: 'Verification and due diligence',
  AuthorityAndWorkflow: 'Authority and workflow',
  Evidence: 'Required evidence',
};

export const awardReadinessStatusLabel: Record<
  ProcurementAwardReadinessPrerequisiteStatus,
  string
> = {
  Passed: 'Passed',
  Failed: 'Blocked',
  NotApplicable: 'Not applicable',
};

export const awardReadinessRemediationHref = (
  sourceType: ProcurementAwardReadinessSourceType,
  sourceId: string,
  group: ProcurementAwardReadinessPrerequisiteGroup
) => {
  const tenderRoot = `/procurement/tenders/${sourceId}`;
  const rfqRoot = `/procurement/rfqs/${sourceId}`;

  if (group === 'ScoreIntegrity' || group === 'Evaluation') {
    return sourceType === 'RequestForQuotation'
      ? `${rfqRoot}/committee-controls`
      : `${tenderRoot}/committee-controls`;
  }
  if (group === 'VerificationAndDueDiligence') {
    return sourceType === 'RequestForQuotation'
      ? `${rfqRoot}/controls`
      : `${tenderRoot}?tab=verification`;
  }
  if (
    group === 'Recommendation' ||
    group === 'AuthorityAndWorkflow' ||
    group === 'Evidence'
  ) {
    if (sourceType === 'RequestForQuotation') return `${rfqRoot}/controls`;
    if (sourceType === 'ExceptionalSourcing')
      return `${tenderRoot}/exception-controls`;
    return `${tenderRoot}/controls`;
  }
  return sourceType === 'RequestForQuotation'
    ? `${rfqRoot}/controls`
    : tenderRoot;
};
