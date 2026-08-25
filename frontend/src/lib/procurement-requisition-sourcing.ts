import type {
  PurchaseRequisitionSourcingReadinessDto,
  PurchaseRequisitionSourcingRequirementDto,
} from '@/services/purchasingService';

export type SourcingReleaseTone =
  'neutral' | 'ready' | 'released' | 'blocked' | 'stale';

export interface SourcingReleasePresentation {
  tone: SourcingReleaseTone;
  title: string;
  badge: string;
  canRelease: boolean;
  canEnterSourcing: boolean;
}

export type SourcingRequirementState =
  'satisfied' | 'actionRequired' | 'waiting';

export interface SourcingRequirementPresentation {
  state: SourcingRequirementState;
  message: string;
}

const preSubmissionKeys = new Set([
  'MANDATORY_FIELDS',
  'BUDGET_AVAILABILITY',
]);

const submissionGeneratedKeys = new Set<string>();

const workflowOutcomeKeys = new Set(['PR_STATUS']);

export function getSourcingRequirementPresentation(
  readiness: PurchaseRequisitionSourcingReadinessDto,
  requirement: PurchaseRequisitionSourcingRequirementDto
): SourcingRequirementPresentation {
  if (requirement.satisfied) {
    return { state: 'satisfied', message: requirement.message };
  }

  const status = readiness.status.trim().toLowerCase();
  const isDraft = status === 'draft';
  const preSubmissionReady = readiness.requirements
    .filter((item) => preSubmissionKeys.has(item.key))
    .every((item) => item.satisfied);

  if (preSubmissionKeys.has(requirement.key)) {
    return { state: 'actionRequired', message: requirement.message };
  }

  if (submissionGeneratedKeys.has(requirement.key) && isDraft) {
    return {
      state: 'waiting',
      message: preSubmissionReady
        ? 'This control will be created and verified atomically when the requisition is submitted.'
        : 'Waiting for the draft submission prerequisites above to be completed.',
    };
  }

  if (requirement.key === 'PR_STATUS' && status !== 'approved') {
    return {
      state: 'waiting',
      message: isDraft
        ? 'Waiting for submission and completion of the configured approval workflow.'
        : 'Waiting for the configured approval workflow to finish with an Approved outcome.',
    };
  }

  if (workflowOutcomeKeys.has(requirement.key) && isDraft) {
    return {
      state: 'waiting',
      message:
        'Waiting for submission and completion of the configured Purchase Requisition workflow.',
    };
  }

  if (
    requirement.key === 'SOD' &&
    readiness.requirements.some(
      (item) => item.key === 'AUTHORITY_WORKFLOW' && !item.satisfied
    )
  ) {
    return {
      state: 'waiting',
      message:
        'Segregation of duties will be verified after the authority workflow is completed.',
    };
  }

  if (
    requirement.key === 'EVIDENCE' &&
    readiness.requirements.some(
      (item) => item.key !== 'EVIDENCE' && !item.satisfied
    )
  ) {
    return {
      state: 'waiting',
      message:
        'Evidence lineage will be verified after the preceding controls are satisfied.',
    };
  }

  return { state: 'actionRequired', message: requirement.message };
}

export function getSourcingReleasePresentation(
  readiness?: PurchaseRequisitionSourcingReadinessDto,
  loading = false
): SourcingReleasePresentation {
  if (loading || !readiness) {
    return {
      tone: 'neutral',
      title: loading
        ? 'Checking sourcing controls'
        : 'Sourcing control unavailable',
      badge: loading ? 'Checking' : 'Unavailable',
      canRelease: false,
      canEnterSourcing: false,
    };
  }

  if (readiness.isReleased) {
    return {
      tone: 'released',
      title: 'Released for sourcing',
      badge: readiness.currentRelease?.releaseReference || 'Released',
      canRelease: false,
      canEnterSourcing: true,
    };
  }

  if (readiness.hasStaleRelease) {
    return {
      tone: 'stale',
      title: 'Sourcing release is stale',
      badge: 'Revalidation required',
      canRelease: readiness.canRelease,
      canEnterSourcing: false,
    };
  }

  if (readiness.canRelease) {
    return {
      tone: 'ready',
      title: 'Ready for sourcing release',
      badge: 'Ready',
      canRelease: true,
      canEnterSourcing: false,
    };
  }

  const status = readiness.status.trim().toLowerCase();
  if (
    readiness.decisionCode === 'PR_NOT_APPROVED' ||
    ['draft', 'submitted', 'pending approval'].includes(status)
  ) {
    return {
      tone: 'neutral',
      title: 'Available after PR approval',
      badge: 'Awaiting approval',
      canRelease: false,
      canEnterSourcing: false,
    };
  }

  return {
    tone: 'blocked',
    title: 'Sourcing release blocked',
    badge: readiness.decisionCode,
    canRelease: false,
    canEnterSourcing: false,
  };
}
