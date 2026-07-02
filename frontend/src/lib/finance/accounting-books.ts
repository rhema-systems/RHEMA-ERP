import type { Account, AccountingBook } from '@/types/finance';

export const ALL_ACTIVE_BOOKS_CODE = 'ALL_ACTIVE_BOOKS';

export const DEFAULT_ACCOUNTING_BOOKS: AccountingBook[] = [
    {
        id: 'default-ifrs',
        tenantId: '',
        code: 'IFRS',
        name: 'IFRS',
        purpose: 'Primary',
        isActive: true,
        isDefault: true,
        allowsPosting: true,
        isSystemDefined: true,
        sortOrder: 10,
    },
    {
        id: 'default-local-statutory',
        tenantId: '',
        code: 'LOCAL_STATUTORY',
        name: 'Local Statutory',
        purpose: 'Statutory',
        isActive: true,
        isDefault: false,
        allowsPosting: true,
        isSystemDefined: true,
        sortOrder: 20,
    },
    {
        id: 'default-management',
        tenantId: '',
        code: 'MANAGEMENT',
        name: 'Management',
        purpose: 'Management',
        isActive: true,
        isDefault: false,
        allowsPosting: true,
        isSystemDefined: true,
        sortOrder: 30,
    },
];

export const normalizeAccountingBookCode = (code?: string): string => {
    const normalized = (code || '').trim().toUpperCase();
    if (normalized === ALL_ACTIVE_BOOKS_CODE) return ALL_ACTIVE_BOOKS_CODE;
    if (normalized === 'LOCAL' || normalized === 'BASE') return 'LOCAL_STATUTORY';
    if (normalized === 'MANAGEMENT') return 'MANAGEMENT';
    return normalized || 'IFRS';
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
        'IFRS'
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

    if (normalized === 'IFRS') {
        return account.isIFRSClassified !== false;
    }

    if (normalized === 'LOCAL_STATUTORY') {
        return account.isBaseFrameworkClassified ?? account.isBaseClassified ?? true;
    }

    if (normalized === 'MANAGEMENT') {
        return account.isLocalFrameworkClassified ?? account.isLocalClassified ?? account.isManagementClassified ?? false;
    }

    return true;
};
