import type {
  BindProcurementEvaluationCommitteeRequest,
  ProcurementEvaluationCommitteeAllowedAction,
  ProcurementEvaluationCommitteeControl,
  SubmitProcurementEvaluationConflictDeclarationRequest,
  RequestProcurementEvaluationScoreRecallRequest,
} from '@/types/procurement-evaluation-committee';

const normalizeAction = (value: string) =>
  value.replace(/[^a-z0-9]/gi, '').toLowerCase();

export const hasEvaluationCommitteeAction = (
  actions: ProcurementEvaluationCommitteeAllowedAction[] | undefined,
  expected: string
) =>
  Boolean(
    actions?.some((action) => normalizeAction(action) === normalizeAction(expected))
  );

export const hasAnyEvaluationCommitteeAction = (
  actions: ProcurementEvaluationCommitteeAllowedAction[] | undefined,
  expected: string[]
) => expected.some((action) => hasEvaluationCommitteeAction(actions, action));

export const createEvaluationIdempotencyKey = (action: string) =>
  `tdc0208-${normalizeAction(action)}-${crypto.randomUUID()}`;

export const validateCommitteeBinding = (
  request: BindProcurementEvaluationCommitteeRequest
) => {
  if (!request.committeeTemplateId)
    return 'Select an active evaluation committee.';
  if (!request.purpose.trim()) return 'State the evaluation committee purpose.';
  if (Number.isNaN(Date.parse(request.effectiveFromUtc)))
    return 'The effective-from date is invalid.';
  if (
    request.effectiveToUtc &&
    (Number.isNaN(Date.parse(request.effectiveToUtc)) ||
      new Date(request.effectiveToUtc) <= new Date(request.effectiveFromUtc))
  )
    return 'The effective-to date must be later than the effective-from date.';
  return undefined;
};

export const validateCoiDeclaration = (
  request: SubmitProcurementEvaluationConflictDeclarationRequest
) => {
  if (request.declaration.trim().length < 10)
    return 'Provide a complete conflict-of-interest declaration.';
  if (
    request.outcome === 'ConflictDeclared' &&
    !request.conflictDetails?.trim()
  )
    return 'Describe the declared conflict.';
  if (
    !request.signatureReference.trim() ||
    !request.evidenceReference.trim()
  )
    return 'Declaration signature and evidence are required.';
  if (Number.isNaN(Date.parse(request.validFromUtc)))
    return 'Declaration validity start is invalid.';
  if (
    request.validToUtc &&
    (Number.isNaN(Date.parse(request.validToUtc)) ||
      new Date(request.validToUtc) <= new Date(request.validFromUtc))
  )
    return 'Declaration validity end must follow its start.';
  return undefined;
};

export const validateMeetingReadiness = (
  control: Pick<
    ProcurementEvaluationCommitteeControl,
    'requiredQuorum' | 'members'
  >
) => {
  const eligibleVoting = control.members.filter(
    (member) => member.eligibleToScore && member.isVoting
  );
  const hasChair = eligibleVoting.some((member) => member.memberKind === 'Chair');
  const hasSecretary = control.members.some(
    (member) =>
      member.eligibleToScore && member.memberKind === 'Secretary'
  );
  if (!hasChair) return 'An accepted, non-conflicted Chair is required.';
  if (!hasSecretary) return 'An accepted, non-conflicted Secretary is required.';
  if (eligibleVoting.length < control.requiredQuorum)
    return `At least ${control.requiredQuorum} eligible voting members are required.`;
  return undefined;
};

export const validateRecallRequest = (
  request: RequestProcurementEvaluationScoreRecallRequest
) => {
  if (request.reason.trim().length < 10)
    return 'Provide a complete reason for controlled recall.';
  if (!request.evidenceReference.trim())
    return 'Recall evidence is required.';
  if (!request.workflowDefinitionId)
    return 'Select the independent recall workflow.';
  return undefined;
};
