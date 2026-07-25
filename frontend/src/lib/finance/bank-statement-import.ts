import * as XLSX from 'xlsx';

export const BANK_STATEMENT_TEMPLATE_HEADERS = [
    'Date',
    'Description',
    'Reference',
    'Debit',
    'Credit',
    'Balance',
] as const;

type BankStatementColumn = 'date' | 'description' | 'reference' | 'debit' | 'credit' | 'balance';

type ParsedRow = {
    date: string;
    description: string;
    reference: string;
    debit: number;
    credit: number;
    balance: number;
    sourceRow: number;
};

export type ParsedBankStatementImport = {
    file?: File;
    rowCount: number;
    statementDate?: string;
    closingBalance?: number;
    errors: string[];
};

const REQUIRED_COLUMNS: BankStatementColumn[] = ['date', 'description', 'reference', 'debit', 'credit', 'balance'];

const COLUMN_ALIASES: Record<BankStatementColumn, string[]> = {
    date: ['Date', 'Transaction Date', 'Posting Date'],
    description: ['Description', 'Narrative', 'Details', 'Transaction Details'],
    reference: ['Reference', 'Reference Number', 'Bank Reference'],
    debit: ['Debit', 'Debit Amount', 'Withdrawal'],
    credit: ['Credit', 'Credit Amount', 'Deposit'],
    balance: ['Balance', 'Running Balance', 'Closing Balance'],
};

const normalizeHeader = (value: unknown) => String(value ?? '').trim().toLowerCase().replace(/[^a-z0-9]/g, '');

const getColumn = (value: unknown): BankStatementColumn | null => {
    const normalized = normalizeHeader(value);
    for (const [column, aliases] of Object.entries(COLUMN_ALIASES) as [BankStatementColumn, string[]][]) {
        if (aliases.some((alias) => normalizeHeader(alias) === normalized)) return column;
    }
    return null;
};

const toCell = (value: unknown) => {
    if (value instanceof Date) return value.toISOString().slice(0, 10);
    return String(value ?? '').trim();
};

const normalizeDate = (value: unknown, rowNumber: number, errors: string[]) => {
    const raw = toCell(value);
    const isoMatch = raw.match(/^(\d{4})-(\d{2})-(\d{2})$/);
    const dayFirstMatch = raw.match(/^(\d{2})\/(\d{2})\/(\d{4})$/);
    const parts = isoMatch
        ? [Number(isoMatch[1]), Number(isoMatch[2]), Number(isoMatch[3])]
        : dayFirstMatch
            ? [Number(dayFirstMatch[3]), Number(dayFirstMatch[2]), Number(dayFirstMatch[1])]
            : null;

    if (!parts) {
        errors.push(`Row ${rowNumber}: Date is required and must use YYYY-MM-DD or DD/MM/YYYY.`);
        return '';
    }

    const [year, month, day] = parts;
    const parsed = new Date(Date.UTC(year, month - 1, day));
    if (parsed.getUTCFullYear() !== year || parsed.getUTCMonth() !== month - 1 || parsed.getUTCDate() !== day) {
        errors.push(`Row ${rowNumber}: Date is not a valid calendar date.`);
        return '';
    }

    return `${year.toString().padStart(4, '0')}-${month.toString().padStart(2, '0')}-${day.toString().padStart(2, '0')}`;
};

const normalizeAmount = (
    value: unknown,
    label: string,
    rowNumber: number,
    errors: string[],
    required = false,
) => {
    const raw = toCell(value);
    if (!raw) {
        if (required) errors.push(`Row ${rowNumber}: ${label} is required.`);
        return 0;
    }

    const parenthesized = /^\(.*\)$/.test(raw);
    const cleaned = raw.replace(/[(),\s]/g, '').replace(/[^0-9.+-]/g, '');
    const amount = Number(cleaned) * (parenthesized ? -1 : 1);
    if (!cleaned || !Number.isFinite(amount)) {
        errors.push(`Row ${rowNumber}: ${label} must be a valid number.`);
        return 0;
    }
    return amount;
};

const toCsvCell = (value: string | number) => `"${String(value).replace(/"/g, '""')}"`;

export async function parseBankStatementImportFile(file: File): Promise<ParsedBankStatementImport> {
    const lowerFileName = file.name.toLowerCase();
    if (!lowerFileName.endsWith('.csv') && !lowerFileName.endsWith('.xlsx')) {
        return { rowCount: 0, errors: ['Only CSV and XLSX bank statement files are supported.'] };
    }

    const workbook = XLSX.read(await file.arrayBuffer(), {
        type: 'array',
        cellDates: lowerFileName.endsWith('.xlsx'),
        raw: !lowerFileName.endsWith('.xlsx'),
    });
    const preferredSheet = workbook.SheetNames.find((name) => normalizeHeader(name) === 'statementlines');
    const sheetName = preferredSheet ?? workbook.SheetNames[0];
    if (!sheetName) return { rowCount: 0, errors: ['The selected file does not contain any sheets.'] };

    const rows = XLSX.utils.sheet_to_json<unknown[]>(workbook.Sheets[sheetName], {
        header: 1,
        defval: '',
        blankrows: false,
        raw: true,
    });

    const headerIndex = rows.findIndex((row) => {
        const columns = new Set(row.map(getColumn).filter(Boolean));
        return REQUIRED_COLUMNS.every((column) => columns.has(column));
    });
    if (headerIndex < 0) {
        return {
            rowCount: 0,
            errors: [`The header row must contain: ${BANK_STATEMENT_TEMPLATE_HEADERS.join(', ')}.`],
        };
    }

    const mappedColumns = rows[headerIndex].map(getColumn);
    const errors: string[] = [];
    const parsedRows: ParsedRow[] = [];

    rows.slice(headerIndex + 1).forEach((row, rowIndex) => {
        if (!row.some((cell) => toCell(cell) !== '')) return;

        const sourceRow = headerIndex + rowIndex + 2;
        const values: Partial<Record<BankStatementColumn, unknown>> = {};
        mappedColumns.forEach((column, index) => {
            if (column) values[column] = row[index];
        });

        const date = normalizeDate(values.date, sourceRow, errors);
        const debit = normalizeAmount(values.debit, 'Debit', sourceRow, errors);
        const credit = normalizeAmount(values.credit, 'Credit', sourceRow, errors);
        const balance = normalizeAmount(values.balance, 'Balance', sourceRow, errors, true);

        if (debit < 0 || credit < 0) errors.push(`Row ${sourceRow}: Debit and Credit cannot be negative.`);
        if ((debit > 0) === (credit > 0)) {
            errors.push(`Row ${sourceRow}: enter a positive amount in either Debit or Credit, but not both.`);
        }

        parsedRows.push({
            date,
            description: toCell(values.description),
            reference: toCell(values.reference),
            debit,
            credit,
            balance,
            sourceRow,
        });
    });

    if (parsedRows.length === 0) errors.push('The selected file does not contain any transaction rows.');

    const sortedRows = parsedRows
        .map((row, index) => ({ row, index }))
        .sort((a, b) => a.row.date.localeCompare(b.row.date) || a.index - b.index)
        .map(({ row }) => row);

    for (let index = 1; index < sortedRows.length; index += 1) {
        const previous = sortedRows[index - 1];
        const current = sortedRows[index];
        const expectedBalance = previous.balance - current.debit + current.credit;
        if (Math.abs(expectedBalance - current.balance) > 0.01) {
            errors.push(
                `Row ${current.sourceRow}: Balance should be ${expectedBalance.toFixed(2)} based on the previous row, debit, and credit.`,
            );
        }
    }

    if (errors.length > 0) return { rowCount: parsedRows.length, errors };

    const csvRows = [
        BANK_STATEMENT_TEMPLATE_HEADERS.map(toCsvCell).join(','),
        ...sortedRows.map((row) => [
            row.date,
            row.description,
            row.reference,
            row.debit === 0 ? '' : row.debit.toFixed(2),
            row.credit === 0 ? '' : row.credit.toFixed(2),
            row.balance.toFixed(2),
        ].map(toCsvCell).join(',')),
    ];
    const csvFile = new File([csvRows.join('\r\n')], `${file.name.replace(/\.(csv|xlsx)$/i, '')}-normalized.csv`, {
        type: 'text/csv',
    });
    const lastRow = sortedRows[sortedRows.length - 1];

    return {
        file: csvFile,
        rowCount: sortedRows.length,
        statementDate: lastRow.date,
        closingBalance: lastRow.balance,
        errors: [],
    };
}
