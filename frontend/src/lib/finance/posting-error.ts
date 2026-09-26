export type FinancePostingErrorPresentation = {
    title: string;
    description: string;
};

const governedFailureTitle = (code: string, fallbackTitle: string): string => {
    if (code === 'PARALLEL_EXCHANGE_RATE_REQUIRED') return 'Approved exchange rate required';
    if (code.includes('APPROVAL_REQUIRED')) return 'Approval required';
    if (code.endsWith('_REQUIRED') || code.endsWith('_MISSING')) return 'Required information is missing';
    if (code.endsWith('_FORBIDDEN') || code.endsWith('_DISABLED')) return 'This action is not allowed';
    if (code.endsWith('_STALE') || code.endsWith('_CHANGED') || code.endsWith('_CONFLICT')) return 'Information has changed';
    if (code.endsWith('_INVALID') || code.endsWith('_MISMATCH')) return 'Information needs correction';
    return fallbackTitle;
};

const rawErrorMessage = (error: unknown, fallback: string): string => {
    const candidate = error as any;
    const details = candidate?.response?.data ?? candidate?.response ?? candidate;
    const message = details?.detail || details?.error || details?.message || candidate?.message || fallback;
    return details?.code && !String(message).includes(details.code)
        ? `${message} (${details.code})`
        : message;
};

const readableDate = (value: string): string => {
    const [year, month, day] = value.split('-').map(Number);
    if (!year || !month || !day) return value;
    return new Intl.DateTimeFormat('en-GB', {
        day: 'numeric',
        month: 'long',
        year: 'numeric',
        timeZone: 'UTC',
    }).format(new Date(Date.UTC(year, month - 1, day)));
};

export const getFinancePostingErrorPresentation = (
    error: unknown,
    fallback = 'The posting action could not be completed.',
    fallbackTitle = 'Posting failed',
): FinancePostingErrorPresentation => {
    const message = rawErrorMessage(error, fallback);
    const missingParallelRate = message.match(
        /PARALLEL_EXCHANGE_RATE_REQUIRED:[\s\S]*?\b([A-Z]{3})\/([A-Z]{3})\b[\s\S]*?(\d{4}-\d{2}-\d{2})/i,
    );

    if (missingParallelRate) {
        const [, sourceCurrency, targetCurrency, accountingDate] = missingParallelRate;
        return {
            title: 'Approved exchange rate required',
            description:
                `Posting was not completed. The active ${targetCurrency.toUpperCase()} Parallel book requires an approved ` +
                `${sourceCurrency.toUpperCase()} to ${targetCurrency.toUpperCase()} exchange rate for ${readableDate(accountingDate)}. ` +
                'No Primary or Parallel ledger posting was committed. Add and approve the rate under Finance > Exchange Rates, then retry this action.',
        };
    }

    const governedFailure = message.match(/^([A-Z][A-Z0-9_]+):\s*([\s\S]+)$/);
    if (governedFailure) {
        const [, code, explanation] = governedFailure;
        const title = governedFailureTitle(code, fallbackTitle);

        return {
            title,
            description: `${explanation.trim()} Reference: ${code}.`,
        };
    }

    return {
        title: fallbackTitle,
        description: message,
    };
};

export const getFinanceApiErrorMessage = (error: unknown, fallback: string): string =>
    getFinancePostingErrorPresentation(error, fallback).description;

const sentenceTitle = (description: string): string =>
    description
        .split(/[.!?]/, 1)[0]
        .replace(/\bsuccessfully\b/gi, '')
        .replace(/\s{2,}/g, ' ')
        .trim();

export const getFinanceFeedbackTitle = (
    title: string,
    description: string,
    destructive: boolean,
): string => {
    if (title !== 'Error' && title !== 'Success') return title;

    if (title === 'Success') {
        const completed = sentenceTitle(description);
        return completed.length > 0 && completed.length <= 80 ? completed : 'Action completed';
    }

    const supportCode = description.match(/Reference:\s*([A-Z][A-Z0-9_]+)\.?/i)?.[1]?.toUpperCase();
    if (supportCode) return governedFailureTitle(supportCode, 'Action could not be completed');

    const failedAction = description.match(/^(?:Failed to|Could not|Unable to)\s+([^.!?]+)/i)?.[1]?.trim();
    if (failedAction && failedAction.length <= 68) return `Unable to ${failedAction}`;
    return destructive ? 'Action could not be completed' : 'Attention required';
};
