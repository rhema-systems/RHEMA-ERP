export type TenderBidInitiationStep = 1 | 2 | 3 | 'complete';

export interface TenderFeePaymentStatus {
  tenderFeeId: string;
  feeType: string;
  amount: number;
  currency: string;
  isMandatory: boolean;
  status: 'NotPaid' | 'Pending' | 'Rejected' | 'Verified' | string;
  paymentId?: string | null;
}

export interface TenderBidInitiationStatus {
  tenderId: string;
  businessPartnerId: string;
  draftBidId?: string | null;
  hasAssignment: boolean;
  assignmentType?: string | null;
  requiresAcceptanceDeclaration: boolean;
  declarationAccepted: boolean;
  declarationSatisfied: boolean;
  paymentRequired: boolean;
  hasPayment: boolean;
  paymentSatisfied: boolean;
  paymentEvidenceAccepted: boolean;
  paymentPendingVerification: boolean;
  canProceed: boolean;
  fees: TenderFeePaymentStatus[];
}

export function nextTenderBidInitiationStep(
  status: Pick<
    TenderBidInitiationStatus,
    | 'hasAssignment'
    | 'requiresAcceptanceDeclaration'
    | 'declarationSatisfied'
    | 'paymentRequired'
    | 'paymentSatisfied'
  >
): TenderBidInitiationStep {
  if (!status.hasAssignment) return 1;
  if (status.requiresAcceptanceDeclaration && !status.declarationSatisfied)
    return 2;
  if (status.paymentRequired && !status.paymentSatisfied) return 3;
  return 'complete';
}

export function isPositiveMandatoryFee(fee: {
  isMandatory: boolean;
  amount: number;
}): boolean {
  return fee.isMandatory && fee.amount > 0;
}
