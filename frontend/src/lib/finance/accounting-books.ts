import type { Account, AccountingBook } from '@/types/finance';

export const ALL_ACTIVE_BOOKS_CODE = 'ALL_ACTIVE_BOOKS';

export const DEFAULT_ACCOUNTING_BOOKS: AccountingBook[] = [
    {
        id: 'default-base',
        tenantId: '',
        code: 'BASE',
        name: 'Ghana Statutory Primary',
        purpose: 'Ghana Statutory',
        isActive: true,
        isDefault: true,
        allowsPosting: true,
        isSystemDefined: true,
        sortOrder: 10,
    },
    {
        id: 'default-ifrs-adjustments',
        tenantId: '',
        code: 'IFRS_ADJUSTMENTS',
        name: 'IFRS Adjustments',
        purpose: 'IFRS adjustments',
        isActive: false,
        isDefault: false,
        allowsPosting: false,
        isSystemDefined: true,
        sortOrder: 20,
    },
    {
        id: 'default-usd-parallel',
        tenantId: '',
        code: 'USD_PARALLEL',
        name: 'USD Parallel',
        purpose: 'Foreign-currency replica',
        isActive: false,
        isDefault: false,
        allowsPosting: false,
        isSystemDefined: true,
        sortOrder: 30,
    },
];

export const normalizeAccountingBookCode = (code?: string): string => {
    const normalized = (code || '').trim().toUpperCase();
    if (normalized === ALL_ACTIVE_BOOKS_CODE) return ALL_ACTIVE_BOOKS_CODE;
    if (normalized === 'LOCAL' || normalized === 'LOCAL_STATUTORY' || normalized === 'PRIMARY') return 'BASE';
    return normalized || 'BASE';
};

export const getAccountingBookName = (
    books: AccountingBook[] | undefined,
    code?: string,
): string => {
    const normalized = normalizeAccountingBookCode(code);
    if (normalized === ALL_ACTIVE_BOOKS_CODE) return 'All Active Books';

    return (
        (books || []).find(book => normalizeAccountingBookCode(book.code) === normalized)?.name ||
        DEFAULT_ACCOUNTING_BOOKS.find(book => book.code === normalized)?.name ||
        code ||
        'BASE'
    );
};

export const isAllActiveBooksCode = (code?: string): boolean =>
    normalizeAccountingBookCode(code) === ALL_ACTIVE_BOOKS_CODE;

export const getPostingTargetBooks = (
    books: AccountingBook[] | undefined,
    code?: string,
): AccountingBook[] => {
    const availableBooks = (books && books.length > 0 ? books : DEFAULT_ACCOUNTING_BOOKS)
        .filter(book => book.isActive !== false && book.allowsPosting !== false);

    if (isAllActiveBooksCode(code)) {
        return availableBooks;
    }

    const normalized = normalizeAccountingBookCode(code);
    return availableBooks.filter(book => normalizeAccountingBookCode(book.code) === normalized);
};

export const isAccountEligibleForBook = (account: Account, bookCode: string): boolean => {
    const normalized = normalizeAccountingBookCode(bookCode);
    if (normalized === ALL_ACTIVE_BOOKS_CODE) return true;

    const mapping = account.accountingBooks?.find(book =>
        normalizeAccountingBookCode(book.accountingBookCode) === normalized
    );

    if (mapping) {
        return mapping.isEnabled;
    }

    if (normalized === 'BASE') {
        return account.isBaseClassified !== false;
    }

    // Derived books are mapping-governed. Never infer their eligibility from legacy
    // IFRS/Local/Management booleans when an explicit book mapping is absent.
    return false;
};
