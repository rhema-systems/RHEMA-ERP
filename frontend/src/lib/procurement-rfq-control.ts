import type {
  ProcurementRfqControlDto,
  ProcurementRfqEvaluationOptionDto,
  SaveProcurementRfqEvaluationRequest,
} from '@/services/rfqService';

export type ProcurementRfqControlStage =
  | 'Invitation'
  | 'SealedReceipt'
  | 'Opening'
  | 'Evaluation'
  | 'Approval'
  | 'AwardHandoff'
  | 'Complete'
  | 'Rejected';

export function getRfqControlStage(
  control: ProcurementRfqControlDto
): ProcurementRfqControlStage {
  if (
    control.evaluation?.status === 'Rejected' ||
    control.rfqStatus === 'Rejected'
  )
    return 'Rejected';
  if (control.rfqStatus === 'Awarded') return 'Complete';
  if (control.evaluation?.status === 'Approved') return 'AwardHandoff';
  if (control.evaluation?.status === 'Submitted') return 'Approval';
  if (control.openingRegister) return 'Evaluation';
  if (control.submissionDeadlinePassed) return 'Opening';
  if (control.receipts.length > 0 || control.rfqStatus === 'Sent')
    return 'SealedReceipt';
  return 'Invitation';
}

export function groupEvaluationOptions(
  options: ProcurementRfqEvaluationOptionDto[]
) {
  return Array.from(
    options.reduce((groups, option) => {
      const current = groups.get(option.rfqItemId) ?? [];
      current.push(option);
      groups.set(option.rfqItemId, current);
      return groups;
    }, new Map<string, ProcurementRfqEvaluationOptionDto[]>())
  )
    .map(([rfqItemId, items]) => ({
      rfqItemId,
      lineNumber: items[0]?.rfqLineNumber ?? 0,
      description: items[0]?.itemDescription ?? '',
      options: items.sort((left, right) => left.lineTotal - right.lineTotal),
    }))
    .sort((left, right) => left.lineNumber - right.lineNumber);
}

export function buildInitialEvaluationLines(
  control: ProcurementRfqControlDto
): SaveProcurementRfqEvaluationRequest['lines'] {
  if (control.evaluation?.lines.length) {
    return control.evaluation.lines.map((line) => ({
      rfqItemId: line.rfqItemId,
      quoteId: line.quoteId,
      technicalScore: line.technicalScore,
      commercialScore: line.commercialScore,
      totalScore: line.totalScore,
      recommendationReason: line.recommendationReason,
    }));
  }
  return groupEvaluationOptions(control.evaluationOptions).map((line) => ({
    rfqItemId: line.rfqItemId,
    quoteId: line.options[0]?.quoteId ?? '',
    technicalScore: 0,
    commercialScore: 0,
    totalScore: 0,
    recommendationReason: '',
  }));
}

export function getRfqOpeningBlocker(
  control: ProcurementRfqControlDto
): string | null {
  if (control.openingRegister)
    return 'The immutable opening register is already complete.';
  if (!control.submissionDeadlinePassed)
    return 'The submission deadline has not passed.';
  if (control.receipts.length === 0)
    return 'No sealed quotation receipts are available.';
  return null;
}
