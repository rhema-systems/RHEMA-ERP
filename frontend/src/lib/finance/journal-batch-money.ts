const currencyCodePattern = /^[A-Z]{3}$/;

export function formatJournalBatchMoney(value: number, currencyCode?: string | null): string {
    const normalizedCurrency = currencyCode?.trim().toUpperCase();
    if (!normalizedCurrency || !currencyCodePattern.test(normalizedCurrency)) {
        return new Intl.NumberFormat('en-GH', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2,
        }).format(value);
    }

    try {
        return new Intl.NumberFormat('en-GH', {
            style: 'currency',
            currency: normalizedCurrency,
        }).format(value);
    } catch {
        return new Intl.NumberFormat('en-GH', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2,
        }).format(value);
    }
}
