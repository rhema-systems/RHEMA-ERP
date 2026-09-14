import type { Account, AccountingBook, JournalEntry } from '@/types/finance';

export interface DashboardBookResolution {
    code?: string;
    error?: string;
}

export function resolveDashboardBook(books: AccountingBook[], selectedCode?: string): DashboardBookResolution {
    const canonical = books.filter(book => book.isActive && book.allowsPosting);
    if (selectedCode) {
        const selected = canonical.find(book => book.code === selectedCode);
        return selected ? { code: selected.code } : { error: 'The selected accounting book is not available for posting.' };
    }
    const defaults = canonical.filter(book => book.isDefault);
    if (defaults.length !== 1) {
        return { error: defaults.length === 0
            ? 'Finance dashboard unavailable: no active default accounting book is configured.'
            : 'Finance dashboard unavailable: more than one active default accounting book is configured.' };
    }
    return { code: defaults[0].code };
}

export function filterDashboardEvidence(
    entries: JournalEntry[], accounts: Account[], bookCode: string,
): { entries: JournalEntry[]; accounts: Account[] } {
    return {
        entries: entries.filter(entry => entry.bookClassification === bookCode),
        accounts: accounts.map(account => ({
            ...account,
            accountingBooks: account.accountingBooks?.filter(mapping =>
                mapping.isEnabled && mapping.accountingBookCode === bookCode),
        })),
    };
}
