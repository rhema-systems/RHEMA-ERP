import type {
  FrameworkCallOff,
  FrameworkCallOffAgreementOption,
  FrameworkCallOffDemandOption,
  FrameworkCallOffPriceOption,
  FrameworkCallOffStatus,
} from '@/types/procurement-framework-call-off';

export const frameworkCallOffStatusTone = (
  status: FrameworkCallOffStatus
): 'default' | 'secondary' | 'destructive' | 'outline' => {
  if (status === 'Approved' || status === 'Issued') return 'default';
  if (status === 'PendingApproval') return 'secondary';
  if (status === 'Rejected' || status === 'Cancelled') return 'destructive';
  return 'outline';
};

export const frameworkCallOffActionState = (callOff?: FrameworkCallOff) => {
  const allowed = new Set(callOff?.allowedActions ?? []);
  return {
    canSubmit: allowed.has('submit'),
    canApprove: allowed.has('approve'),
    canReject: allowed.has('reject'),
    canIssue: allowed.has('issue'),
    canCancel: allowed.has('cancel'),
  };
};

export const matchingFrameworkPrice = (
  agreement: FrameworkCallOffAgreementOption | undefined,
  demand: FrameworkCallOffDemandOption
): FrameworkCallOffPriceOption | undefined =>
  agreement?.priceLines.find(
    (price) =>
      price.inventoryItemId === demand.inventoryItemId &&
      price.unitOfMeasure.toLocaleLowerCase() ===
        demand.unitOfMeasure.toLocaleLowerCase()
  );

export const frameworkCallOffAmount = (
  lines: Array<{ selected: boolean; quantity: number; unitPrice: number }>
) =>
  lines
    .filter((line) => line.selected)
    .reduce((total, line) => total + line.quantity * line.unitPrice, 0);
