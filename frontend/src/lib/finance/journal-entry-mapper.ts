/**
 * Journal Entry DTO Mapper
 * 
 * Contract guard: centralises the transformation between the frontend
 * journal-entry form state and the backend CreateJournalEntryDto / 
 * CreateAccountTransactionDto schemas.
 * 
 * Any future UI changes to the journal form should ONLY modify this file's
 * input types — the output types are locked to the backend contract defined in:
 *   - ErpSystem.Core/DTOs/Finance/JournalEntryDtos.cs
 *   - ErpSystem.Core/DTOs/Finance/AccountTransactionDtos.cs
 * 
 * Backend Contract (as of 2026-05-14):
 * ────────────────────────────────────
 * CreateJournalEntryDto
 *   [Required] JournalNumber     string   max 50
 *   [Required] TransactionDate   DateTime
 *              Description       string?  max 500
 *              Reference         string?  max 100
 *              BookClassification string? max 20
 *              SourceModule      string?  max 50   (forced null by controller for manual entries)
 *              SourceDocumentId  Guid?
 *              SourceDocumentType string? max 100
 *              FiscalPeriodId    Guid?
 *              Transactions      List<CreateAccountTransactionDto>
 * 
 * CreateAccountTransactionDto
 *   [Required] AccountId         Guid
 *   [Required] Amount            decimal
 *   [Required] TransactionType   string ("Debit" | "Credit")
 *              Description       string?  max 500
 *   [Required] Reference         string   max 50
 *              CurrencyCode      string?  max 3
 *              ForeignAmount     decimal?
 *              ExchangeRateId    Guid?
 *              ExchangeRate      decimal?
 *              LineNumber        int      default 1
 */

import type {
    CreateJournalEntryDto,
    CreateAccountTransactionDto,
    JournalType,
} from '@/types/finance';

// ─── Frontend Form Shapes ────────────────────────────────────────────────────

/** Header fields collected by the New Journal Entry form. */
export interface JournalEntryFormHeader {
    entryDate: string;
    journalType: string;
    description: string;
    referenceNumber: string;
    notes: string;
    bookClassification: string;
}

/** A single transaction line in the journal entry form. */
export interface JournalEntryFormLine {
    id: string;
    accountId: string;
    description: string;
    currencyCode: string;
    exchangeRateId?: string;
    exchangeRate: number | '';
    debit: number;
    credit: number;
    foreignDebit?: number;
    foreignCredit?: number;
    dimensions?: Record<string, string>;
}

// ─── Mapper ──────────────────────────────────────────────────────────────────

/**
 * Maps the frontend journal-entry form state into a CreateJournalEntryDto
 * payload that satisfies the backend contract.
 * 
 * This is the ONLY place where form-to-DTO transformation should occur.
 * If the backend contract changes, update this function and the type
 * definitions in @/types/finance.ts simultaneously.
 */
export function mapJournalEntryFormToCreateDto(
    header: JournalEntryFormHeader,
    lines: JournalEntryFormLine[],
    journalNumber: string | undefined,
    baseCurrency: string,
): CreateJournalEntryDto {
    if (!/^[A-Z]{3}$/.test(baseCurrency.trim().toUpperCase())) {
        throw new Error('A valid Finance functional currency is required to map a journal entry.');
    }
    const functionalCurrency = baseCurrency.trim().toUpperCase();
    // Filter to only lines with an account and at least one amount
    const validLines = lines.filter(
        l => l.accountId && (l.debit > 0 || l.credit > 0)
    );

    // Build typed transaction list
    const transactions: CreateAccountTransactionDto[] = [];
    let lineNo = 1;

    for (const l of validLines) {
        const transactionCurrency = l.currencyCode.trim().toUpperCase();
        if (!/^[A-Z]{3}$/.test(transactionCurrency)) {
            throw new Error('Every journal line requires a valid transaction currency.');
        }
        const isForeign = transactionCurrency !== functionalCurrency;
        const rate = typeof l.exchangeRate === 'number' ? l.exchangeRate : 0;
        if (isForeign && rate <= 0) {
            throw new Error(`An approved ${transactionCurrency} exchange rate is required.`);
        }
        if (isForeign && !l.exchangeRateId) {
            throw new Error(`The approved ${transactionCurrency} exchange-rate record is required.`);
        }
        if (isForeign && l.debit > 0 && !(l.foreignDebit && l.foreignDebit > 0)) {
            throw new Error(`The original ${transactionCurrency} debit amount is required.`);
        }
        if (isForeign && l.credit > 0 && !(l.foreignCredit && l.foreignCredit > 0)) {
            throw new Error(`The original ${transactionCurrency} credit amount is required.`);
        }

        if (l.debit > 0) {
            transactions.push({
                accountId: l.accountId,
                amount: l.debit,
                transactionType: 'Debit',
                description: l.description || undefined,
                reference: header.referenceNumber || 'JE',
                currencyCode: isForeign ? transactionCurrency : undefined,
                foreignAmount: isForeign ? (l.foreignDebit || undefined) : undefined,
                exchangeRateId: isForeign ? l.exchangeRateId : undefined,
                exchangeRate: isForeign ? rate : undefined,
                lineNumber: lineNo++,
                dimensions: Object.entries(l.dimensions ?? {})
                    .filter(([, valueCode]) => Boolean(valueCode))
                    .map(([dimensionCode, valueCode]) => ({ dimensionCode, valueCode })),
            });
        }

        if (l.credit > 0) {
            transactions.push({
                accountId: l.accountId,
                amount: l.credit,
                transactionType: 'Credit',
                description: l.description || undefined,
                reference: header.referenceNumber || 'JE',
                currencyCode: isForeign ? transactionCurrency : undefined,
                foreignAmount: isForeign ? (l.foreignCredit || undefined) : undefined,
                exchangeRateId: isForeign ? l.exchangeRateId : undefined,
                exchangeRate: isForeign ? rate : undefined,
                lineNumber: lineNo++,
                dimensions: Object.entries(l.dimensions ?? {})
                    .filter(([, valueCode]) => Boolean(valueCode))
                    .map(([dimensionCode, valueCode]) => ({ dimensionCode, valueCode })),
            });
        }
    }

    // Clean up journal number (remove UI formatting like '*')
    const cleanJournalNumber = (journalNumber ?? '')
        .replace('*', '')
        .replace('Generating...', '')
        .replace('Unavailable', '')
        .replace('Assigned on save', '')
        .trim();

    return {
        journalNumber: cleanJournalNumber || undefined,
        transactionDate: header.entryDate,
        journalType: header.journalType as JournalType,
        description: header.description || undefined,
        reference: header.referenceNumber || undefined,
        notes: header.notes || undefined,
        bookClassification: header.bookClassification || undefined,
        // SourceModule is intentionally omitted for manual entries
        // (controller forces it to null anyway as a security measure)
        transactions,
    };
}

/**
 * Validates that the form has enough data to construct a valid DTO.
 * Returns an array of validation error messages (empty = valid).
 */
export function validateJournalEntryForm(
    header: JournalEntryFormHeader,
    lines: JournalEntryFormLine[],
    journalNumber: string | undefined,
    options?: {
        requireJournalNumber?: boolean;
    },
): string[] {
    const errors: string[] = [];

    // Manual number entry only needs a value when the sequence policy requires it.
    const cleanNumber = (journalNumber ?? '').replace('*', '').trim();
    if (options?.requireJournalNumber && (!cleanNumber || cleanNumber === 'Generating...' || cleanNumber === 'Unavailable')) {
        errors.push('Journal number is not available. Please wait or refresh.');
    }

    // Description is required
    if (!header.description.trim()) {
        errors.push('Description is required.');
    }

    // Manual journals always require a complete balanced entry. Opening balances use the
    // controlled source-specific workspace and never rely on this mapper.
    const validLines = lines.filter(l => l.accountId && (l.debit > 0 || l.credit > 0));
    if (validLines.length < 2) {
        errors.push('At least 2 transaction lines with amounts are required.');
    }

    const totalDebit = lines.reduce((sum, l) => sum + (l.debit || 0), 0);
    const totalCredit = lines.reduce((sum, l) => sum + (l.credit || 0), 0);
    if (Math.abs(totalDebit - totalCredit) >= 0.01) {
        errors.push(`Entry is not balanced. Debits (${totalDebit.toFixed(2)}) != Credits (${totalCredit.toFixed(2)}).`);
    }

    return errors;
}

