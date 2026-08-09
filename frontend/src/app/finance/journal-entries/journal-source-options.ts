/**
 * Normalized filter values for the source modules written onto Finance journal
 * entries by the supported posting owners. Keep these values semantically aligned
 * with `JournalEntry.SourceModule`; labels are presentation-only and must not
 * introduce abbreviated aliases that would hide valid postings from the filter.
 *
 * Procurement deliberately uses `PROCUREMENT` here (rather than the separate
 * `PROC` origin-module code) because its Finance posting request persists
 * `SourceModule = "Procurement"`. The journal page normalizes route values to
 * uppercase, so this value supports both the dropdown and direct deep links.
 */
export const journalSourceOptions = [
    { value: 'AP', label: 'Accounts Payable' },
    { value: 'AR', label: 'Accounts Receivable' },
    { value: 'GL', label: 'General Ledger' },
    { value: 'BANK', label: 'Cash/Bank' },
    { value: 'FIXEDASSETS', label: 'Fixed Assets' },
    { value: 'PAYROLL', label: 'Payroll' },
    { value: 'PROCUREMENT', label: 'Procurement' },
] as const;
