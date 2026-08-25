import type { PurchaseRequisitionBudgetReadinessDto } from '@/services/purchasingService';

export type BudgetControlTone = 'neutral' | 'ready' | 'blocked';

export interface BudgetControlPresentation {
  tone: BudgetControlTone;
  title: string;
  basisLabel: string;
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
    const activeCommitment = readiness.basis === 'ExistingCommitment';
    return {
      tone: 'ready',
      title: activeCommitment ? 'Existing commitment retained' : 'Approved budget is available',
      basisLabel: readiness.isOverride
        ? 'Authorized override'
        : activeCommitment
          ? 'Active commitment'
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
