import {
  ProcurementTenderControlStatus as Status,
  ProcurementTenderSubmissionDisposition as Disposition,
  type ProcurementTenderControl,
} from '@/types/procurement-tender-control';

export const tenderControlStatusLabel: Record<Status, string> = {
  [Status.Advertised]: 'Advertised',
  [Status.Opened]: 'Public opening complete',
  [Status.TechnicalEvaluated]: 'Technical evaluation complete',
  [Status.FinancialEvaluated]: 'Financial evaluation complete',
  [Status.PendingApproval]: 'Authority approval pending',
  [Status.Approved]: 'Authority approval complete',
  [Status.Rejected]: 'Rejected',
  [Status.Awarded]: 'Award recorded',
  [Status.Contracted]: 'Contract recorded',
  [Status.Accepted]: 'Bidder acceptance complete',
};

export function getTenderControlReadiness(control: ProcurementTenderControl) {
  const onTime = control.submissionReceipts.filter(
    (item) => item.disposition === Disposition.OnTimeAccepted
  ).length;
  const late = control.submissionReceipts.length - onTime;
  return {
    onTime,
    late,
    sealed: control.status === Status.Advertised,
    canOpen:
      control.status === Status.Advertised &&
      Date.now() >= new Date(control.submissionDeadlineUtc).getTime() &&
      onTime > 0,
    canTechnical: control.status === Status.Opened,
    canFinancial: control.status === Status.TechnicalEvaluated,
    canSubmitApproval: control.status === Status.FinancialEvaluated,
    canDecide: control.status === Status.PendingApproval,
    canAward: control.status === Status.Approved,
    canContract: control.status === Status.Awarded,
    canAccept: control.status === Status.Contracted,
  };
}

export function buildTechnicalScores(control: ProcurementTenderControl) {
  return control.submissionReceipts
    .filter((item) => item.disposition === Disposition.OnTimeAccepted)
    .map((item) => ({
      bidId: item.tenderBidId,
      score: 0,
      qualified: true,
      reason: '',
    }));
}

export function buildFinancialScores(control: ProcurementTenderControl) {
  const qualified = new Set(
    control.technicalResults
      .filter((item) => item.qualified)
      .map((item) => item.bidId)
  );
  let receipts = control.submissionReceipts.filter(
    (item) =>
      item.disposition === Disposition.OnTimeAccepted &&
      qualified.has(item.tenderBidId)
  );
  if (control.method === 7) {
    const ranking = [...control.technicalResults]
      .filter((item) => item.qualified)
      .sort(
        (left, right) =>
          right.score - left.score || left.bidId.localeCompare(right.bidId)
      );
    receipts = receipts.filter(
      (item) => item.tenderBidId === ranking[0]?.bidId
    );
  }
  return receipts.map((item) => ({
    bidId: item.tenderBidId,
    score: 0,
    evaluatedAmount: item.bidAmount,
    reason: '',
  }));
}
