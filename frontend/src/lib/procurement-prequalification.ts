import {
  ProcurementPrequalificationApplicationStatus as ApplicationStatus,
  ProcurementPrequalificationStatus as Status,
  ProcurementQualifiedListEntryStatus as EntryStatus,
  type CreateProcurementPrequalificationExercise,
  type ProcurementPrequalificationExercise,
} from '@/types/procurement-prequalification';

export const prequalificationStatusLabel: Record<Status, string> = {
  [Status.Draft]: 'Draft',
  [Status.Advertised]: 'Accepting applications',
  [Status.Closed]: 'Submissions closed',
  [Status.UnderEvaluation]: 'Under evaluation',
  [Status.PendingApproval]: 'Decision pending',
  [Status.Approved]: 'Approved qualified list',
  [Status.Rejected]: 'Rejected',
  [Status.Expired]: 'Expired',
};

export const prequalificationApplicationStatusLabel: Record<ApplicationStatus, string> = {
  [ApplicationStatus.Submitted]: 'Submitted',
  [ApplicationStatus.EvaluatedQualified]: 'Recommended qualified',
  [ApplicationStatus.EvaluatedRejected]: 'Not recommended',
  [ApplicationStatus.Approved]: 'Qualified',
  [ApplicationStatus.Rejected]: 'Rejected',
};

export const qualifiedEntryStatusLabel: Record<EntryStatus, string> = {
  [EntryStatus.Active]: 'Active',
  [EntryStatus.Expired]: 'Expired',
  [EntryStatus.Revoked]: 'Revoked',
};

export function getPrequalificationActions(exercise: ProcurementPrequalificationExercise, now = new Date()) {
  return {
    canAdvertise: exercise.status === Status.Draft,
    canApply: exercise.status === Status.Advertised &&
      now >= new Date(exercise.opensAtUtc) && now <= new Date(exercise.closesAtUtc),
    canClose: exercise.status === Status.Advertised && now >= new Date(exercise.closesAtUtc),
    canEvaluate: exercise.status === Status.Closed || exercise.status === Status.UnderEvaluation,
    canSubmitDecision: exercise.status === Status.UnderEvaluation &&
      exercise.applications.length > 0 &&
      exercise.applications.every((item) => item.status !== ApplicationStatus.Submitted),
    canDecide: exercise.status === Status.PendingApproval,
    canExpire: exercise.status === Status.Approved &&
      exercise.qualifiedEntries.some((item) => item.status === EntryStatus.Active && new Date(item.expiresAtUtc) <= now),
    immutable: exercise.status === Status.Rejected || exercise.status === Status.Expired,
  };
}

export function validatePrequalificationDraft(request: CreateProcurementPrequalificationExercise, approvalRequired = true) {
  if (!request.reference.trim()) return 'Enter a unique prequalification reference.';
  if (!request.title.trim()) return 'Enter a title.';
  if (request.description.trim().length < 20) return 'Enter a description of at least 20 characters.';
  if (!request.categoryIds.length) return 'Select at least one category.';
  if (!request.policySetId) return 'Select the exact current procurement policy.';
  if (approvalRequired && !request.workflowDefinitionId) return 'Select the exact published sourcing workflow.';
  if (new Date(request.closesAtUtc) <= new Date(request.opensAtUtc)) return 'Closing date must be after opening date.';
  if (!request.criteria.length) return 'Add at least one criterion.';
  if (new Set(request.criteria.map((item) => item.code.trim().toUpperCase())).size !== request.criteria.length)
    return 'Criterion codes must be unique.';
  if (Math.abs(request.criteria.reduce((sum, item) => sum + item.weight, 0) - 100) > 0.01)
    return 'Criterion weights must total exactly 100.';
  if (!request.criteria.some((item) => item.isMandatory)) return 'At least one criterion must be mandatory.';
  return null;
}
