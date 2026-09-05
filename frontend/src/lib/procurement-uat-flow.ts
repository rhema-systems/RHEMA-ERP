export const PROCUREMENT_UAT_STAGE_IDS = [
  'budget-plan',
  'ghaneps-exchange',
  'purchase-requisition',
  'sourcing-release-case',
  'tender-rfq-preparation',
  'tender-documents-publication',
  'supplier-bidding',
  'evaluation-committee',
  'bid-opening-evaluation',
  'award',
  'purchase-order-commitment',
  'contract-activation',
] as const;

export type ProcurementUatStageId = (typeof PROCUREMENT_UAT_STAGE_IDS)[number];

export type ProcurementUatStageStatus =
  | 'unknown'
  | 'not-started'
  | 'ready'
  | 'in-progress'
  | 'blocked'
  | 'complete'
  | 'skipped'
  | 'failed';

export interface ProcurementUatStageDefinition {
  id: ProcurementUatStageId;
  label: string;
  description: string;
}

export interface ProcurementUatStageState {
  /** Status supplied by the page's authoritative record or readiness response. */
  status: ProcurementUatStageStatus;
  /** Unresolved server or workflow readiness reasons for this stage. */
  blockers?: readonly string[];
  /** Exact configured role, actor group, or external party responsible for the next action. */
  responsibleRole?: string;
  /** Link to the relevant record or setup page. */
  href?: string;
  /** Link text, for example "Open document register". */
  actionLabel?: string;
  /** Optional record reference or short page-specific explanation. */
  context?: string;
}

export type ProcurementUatStageStates = Partial<
  Record<ProcurementUatStageId, ProcurementUatStageState>
>;

export interface ProcurementUatFlowStage extends ProcurementUatStageDefinition {
  index: number;
  state: ProcurementUatStageState;
  isCurrent: boolean;
}

export const PROCUREMENT_UAT_STAGES: readonly ProcurementUatStageDefinition[] =
  [
    {
      id: 'budget-plan',
      label: 'Procurement budget and plan',
      description:
        'Confirm approved budget capacity and the procurement plan line.',
    },
    {
      id: 'ghaneps-exchange',
      label: 'Manual GHANEPS exchange',
      description:
        'Record the governed export, acknowledgement, and reconciliation.',
    },
    {
      id: 'purchase-requisition',
      label: 'Purchase requisition',
      description:
        'Create, submit, and approve the requisition against its source plan.',
    },
    {
      id: 'sourcing-release-case',
      label: 'Sourcing release and case',
      description:
        'Lock the approved demand, policy, method, and sourcing lineage.',
    },
    {
      id: 'tender-rfq-preparation',
      label: 'Tender or RFQ preparation',
      description:
        'Prepare the solicitation, lots, schedule, invitations, and controls.',
    },
    {
      id: 'tender-documents-publication',
      label: 'Controlled documents and publication',
      description:
        'Bind, issue, and publish the effective controlled tender documents.',
    },
    {
      id: 'supplier-bidding',
      label: 'Supplier bidding',
      description:
        'Suppliers accept invitations and submit sealed, compliant bids.',
    },
    {
      id: 'evaluation-committee',
      label: 'Evaluation committee and meeting',
      description:
        'Constitute the committee, confirm attendance, conflicts, and quorum.',
    },
    {
      id: 'bid-opening-evaluation',
      label: 'Bid opening and evaluation',
      description: 'Open eligible bids and complete the governed evaluation.',
    },
    {
      id: 'award',
      label: 'Award',
      description:
        'Complete award readiness, approvals, notices, and acceptance.',
    },
    {
      id: 'purchase-order-commitment',
      label: 'Purchase order and commitment',
      description:
        'Create the purchase order and post the controlled commitment.',
    },
    {
      id: 'contract-activation',
      label: 'Contract activation',
      description:
        'Complete contract approvals, evidence, signatures, and activation.',
    },
  ];

const UNKNOWN_STATE: ProcurementUatStageState = { status: 'unknown' };

export function buildProcurementUatFlow(
  currentStage: ProcurementUatStageId,
  stageStates: ProcurementUatStageStates
): ProcurementUatFlowStage[] {
  return PROCUREMENT_UAT_STAGES.map((definition, index) => ({
    ...definition,
    index,
    state: stageStates[definition.id] ?? UNKNOWN_STATE,
    isCurrent: definition.id === currentStage,
  }));
}

export function getProcurementUatStageContext(
  flow: readonly ProcurementUatFlowStage[],
  currentStage: ProcurementUatStageId
): {
  previous?: ProcurementUatFlowStage;
  current: ProcurementUatFlowStage;
  next?: ProcurementUatFlowStage;
} {
  const currentIndex = flow.findIndex((stage) => stage.id === currentStage);
  if (currentIndex < 0) {
    throw new Error(`Unknown procurement UAT stage: ${currentStage}`);
  }

  return {
    previous: currentIndex > 0 ? flow[currentIndex - 1] : undefined,
    current: flow[currentIndex],
    next: currentIndex < flow.length - 1 ? flow[currentIndex + 1] : undefined,
  };
}

export function procurementUatProgress(
  flow: readonly ProcurementUatFlowStage[]
): { completed: number; total: number } {
  return {
    completed: flow.filter(({ state }) =>
      ['complete', 'skipped'].includes(state.status)
    ).length,
    total: flow.length,
  };
}
