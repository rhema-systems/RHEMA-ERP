import { describe, expect, it } from 'vitest';
import { getFinanceFeedbackTitle, getFinancePostingErrorPresentation } from './posting-error';

describe('getFinancePostingErrorPresentation', () => {
    it('turns a missing Parallel rate error into actionable posting guidance', () => {
        const result = getFinancePostingErrorPresentation(new Error(
            'PARALLEL_EXCHANGE_RATE_REQUIRED: Posting was not completed because the active USD Parallel book requires an approved GHS/USD exchange rate for accounting date 2026-09-24.',
        ));

        expect(result.title).toBe('Approved exchange rate required');
        expect(result.description).toContain('GHS to USD');
        expect(result.description).toContain('24 September 2026');
        expect(result.description).toContain('Finance > Exchange Rates');
        expect(result.description).toContain('No Primary or Parallel ledger posting was committed');
    });

    it('preserves an ordinary posting error', () => {
        expect(getFinancePostingErrorPresentation(new Error('The fiscal period is closed.'))).toEqual({
            title: 'Posting failed',
            description: 'The fiscal period is closed.',
        });
    });

    it('turns a governed technical code into an explanation with a support reference', () => {
        expect(getFinancePostingErrorPresentation(new Error(
            'DELTA_POSTING_WINDOW_CLOSED: The accounting date is outside this Delta book posting window.',
        ))).toEqual({
            title: 'Posting failed',
            description: 'The accounting date is outside this Delta book posting window. Reference: DELTA_POSTING_WINDOW_CLOSED.',
        });
    });

    it('replaces generic Finance toast titles with the affected action', () => {
        expect(getFinanceFeedbackTitle('Error', 'Failed to load supplier invoices.', true))
            .toBe('Unable to load supplier invoices');
        expect(getFinanceFeedbackTitle('Success', 'Vendor invoice posted successfully.', false))
            .toBe('Vendor invoice posted');
        expect(getFinanceFeedbackTitle('Error', 'The invoice is no longer available.', true))
            .toBe('Action could not be completed');
        expect(getFinanceFeedbackTitle(
            'Error',
            'Independent approval is required before posting. Reference: ACCOUNTING_EVENT_APPROVAL_REQUIRED.',
            true,
        )).toBe('Approval required');
    });
});
