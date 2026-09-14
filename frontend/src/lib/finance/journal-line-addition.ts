export interface DescribedJournalLine {
    description: string;
}

export interface FreshManualJournalLine extends DescribedJournalLine {
    id: string;
    accountId: string;
    currencyCode: string;
    exchangeRate: number;
    debit: number;
    credit: number;
    rateStatus: 'idle' | 'ready';
    dimensions: Record<string, string>;
}

export interface FreshUnitJournalLine extends DescribedJournalLine {
    id: string;
    unitAccountId: string;
    quantity: string;
}

export function appendJournalLine<TLine extends DescribedJournalLine>(
    current: readonly TLine[],
    createFreshLine: (description: string) => TLine,
): TLine[] {
    const description = current[current.length - 1]?.description ?? '';
    return [...current, createFreshLine(description)];
}

export function createFreshManualJournalLine(
    id: string,
    functionalCurrency: string,
    description = '',
): FreshManualJournalLine {
    return {
        id,
        accountId: '',
        description,
        currencyCode: functionalCurrency,
        exchangeRate: 1,
        debit: 0,
        credit: 0,
        rateStatus: functionalCurrency ? 'ready' : 'idle',
        dimensions: {},
    };
}

export function createFreshUnitJournalLine(
    id: string,
    description = '',
): FreshUnitJournalLine {
    return {
        id,
        unitAccountId: '',
        quantity: '',
        description,
    };
}
