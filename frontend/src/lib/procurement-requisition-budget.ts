import type {
  PurchaseRequisitionBudgetControlHistoryDto,
  PurchaseRequisitionBudgetReadinessDto,
} from '@/services/purchasingService';

export type BudgetControlTone = 'neutral' | 'ready' | 'blocked';

export interface BudgetControlPresentation {
  tone: BudgetControlTone;
  title: string;
  basisLabel: string;
}

export function getPurchaseRequisitionBudgetControlHistory(
  history: PurchaseRequisitionBudgetControlHistoryDto[]
) {
  return history;
}

export function getBudgetControlPresentation(
  readiness?: PurchaseRequisitionBudgetReadinessDto,
  loading = false
): BudgetControlPresentation {
  if (loading || !readiness) {
    return {
      tone: 'neutral',
      title: loading ? 'Checking Finance budget' : 'Budget control unavailable',
      basisLabel: 'Not evaluated'
    };
  }

  if (readiness.canReserve) {
    const existingCommitment = readiness.basis === 'ExistingCommitment';
    return {
      tone: 'ready',
      title: existingCommitment
        ? 'Downstream budget commitment recorded'
        : 'Approved budget is available',
      basisLabel: readiness.isOverride
        ? 'Authorized override'
        : existingCommitment
          ? readiness.commitmentStatus || 'Commitment recorded'
          : 'Availability confirmed'
    };
  }

  return {
    tone: 'blocked',
    title: readiness.basis === 'ReleasedCommitment'
      ? 'Budget commitment released'
      : 'Finance budget blocked',
    basisLabel: readiness.isOverride ? 'Override incomplete' : 'Hard stop'
  };
}
