import { describe, expect, it } from 'vitest';
import type { ProcurementRfqControlDto } from '@/services/rfqService';
import {
  buildInitialEvaluationLines,
  getRfqControlStage,
  getRfqOpeningBlocker,
  groupEvaluationOptions,
} from './procurement-rfq-control';

const control = (
  overrides: Partial<ProcurementRfqControlDto> = {}
): ProcurementRfqControlDto => ({
  rfqId: 'rfq',
  rfqNumber: 'RFQ-001',
  rfqStatus: 'Sent',
  methodRuleId: 'rule',
  methodRuleCode: 'RFQ-GOODS',
  minimumQuotationCount: 2,
  qualifiedInvitationCount: 3,
  onTimeReceiptCount: 0,
  lateReceiptCount: 0,
  submissionDeadlinePassed: false,
  quotesRemainSealed: true,
  minimumCompetitionMet: false,
  receipts: [],
  evaluationOptions: [],
  ...overrides,
});

describe('RFQ statutory control helpers', () => {
  it('derives the history-first stage without exposing commercial options before opening', () => {
    expect(getRfqControlStage(control())).toBe('SealedReceipt');
    expect(
      getRfqControlStage(control({ submissionDeadlinePassed: true }))
    ).toBe('Opening');
    expect(buildInitialEvaluationLines(control())).toEqual([]);
  });

  it('groups opened options by line and selects the lowest evaluated option initially', () => {
    const options = [
      {
        rfqItemId: 'line-1',
        rfqLineNumber: 1,
        itemDescription: 'Laptop',
        quoteId: 'q-high',
        businessPartnerId: 'b1',
        businessPartnerName: 'A',
        unitPrice: 12,
        lineTotal: 24,
      },
      {
        rfqItemId: 'line-1',
        rfqLineNumber: 1,
        itemDescription: 'Laptop',
        quoteId: 'q-low',
        businessPartnerId: 'b2',
        businessPartnerName: 'B',
        unitPrice: 10,
        lineTotal: 20,
      },
    ];
    expect(
      groupEvaluationOptions(options)[0].options.map((item) => item.quoteId)
    ).toEqual(['q-low', 'q-high']);
    expect(
      buildInitialEvaluationLines(control({ evaluationOptions: options }))[0]
        .quoteId
    ).toBe('q-low');
  });

  it('explains opening hard stops in deterministic priority order', () => {
    expect(getRfqOpeningBlocker(control())).toBe(
      'The submission deadline has not passed.'
    );
    expect(
      getRfqOpeningBlocker(control({ submissionDeadlinePassed: true }))
    ).toBe('No sealed quotation receipts are available.');
    expect(
      getRfqOpeningBlocker(
        control({
          submissionDeadlinePassed: true,
          receipts: [
            {
              id: 'r',
              quoteId: 'q',
              businessPartnerId: 'b',
              businessPartnerName: 'Hidden',
              receiptNumber: 'R1',
              submissionDeadlineUtc: '',
              receivedAtUtc: '',
              disposition: 'OnTimeAccepted',
              sealedAtUtc: '',
              integrityHash: 'h',
            },
          ],
        })
      )
    ).toBeNull();
  });
});
