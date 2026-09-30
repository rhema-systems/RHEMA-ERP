import * as XLSX from 'xlsx';

import type { CreateExchangeRateDto, ExchangeRateType } from '@/types/finance';

export const EXCHANGE_RATE_TEMPLATE_HEADERS = [
    'baseCurrencyCode',
    'targetCurrencyCode',
    'rate',
    'effectiveDate',
    'rateType',
    'rateSource',
    'sourceName',
    'sourceReference',
    'expiryDate',
    'isActive',
    'approvalStatus',
];

const EXCHANGE_RATE_REQUIRED_COLUMNS = [
    'baseCurrencyCode',
    'targetCurrencyCode',
    'rate',
    'effectiveDate',
    'rateType',
    'rateSource',
] as const;

const EXCHANGE_RATE_RATE_TYPES: ExchangeRateType[] = [
    'Daily',
    'Average',
    'MonthEnd',
    'QuarterEnd',
    'YearEnd',
    'Fixed',
    'GhanaStatutory',
];

type ExchangeRateImportColumn =
    | 'baseCurrencyCode'
    | 'targetCurrencyCode'
    | 'rate'
    | 'effectiveDate'
    | 'rateType'
    | 'rateSource'
    | 'sourceName'
    | 'sourceReference'
    | 'expiryDate'
    | 'isActive'
    | 'approvalStatus';

type ParsedExchangeRateRow = Partial<Record<ExchangeRateImportColumn, string>>;

export type ParsedExchangeRateImport = {
    rows: CreateExchangeRateDto[];
    errors: string[];
};

const EXCHANGE_RATE_COLUMN_ALIASES: Record<ExchangeRateImportColumn, string[]> = {
    baseCurrencyCode: ['baseCurrencyCode', 'baseCurrency', 'fromCurrency', 'fromCurrencyCode', 'from'],
    targetCurrencyCode: ['targetCurrencyCode', 'targetCurrency', 'toCurrency', 'toCurrencyCode', 'to'],
    rate: ['rate', 'exchangeRate', 'currentExchangeRate'],
    effectiveDate: ['effectiveDate', 'rateDate', 'date'],
    rateType: ['rateType', 'type'],
    rateSource: ['rateSource', 'source'],
    sourceName: ['sourceName', 'sourceProvider', 'provider'],
    sourceReference: ['sourceReference', 'reference', 'referenceNumber', 'sourceRef'],
    expiryDate: ['expiryDate', 'endDate', 'validUntil'],
    isActive: ['isActive', 'active'],
    approvalStatus: ['approvalStatus', 'status'],
};

const toCsvCell = (value: string) => `"${value.replace(/"/g, '""')}"`;

const normalizeImportHeader = (value: unknown) =>
    String(value ?? '').trim().toLowerCase().replace(/[^a-z0-9]/g, '');

const getImportColumn = (header: unknown): ExchangeRateImportColumn | null => {
    const normalizedHeader = normalizeImportHeader(header);

    for (const [column, aliases] of Object.entries(EXCHANGE_RATE_COLUMN_ALIASES) as [ExchangeRateImportColumn, string[]][]) {
        if (aliases.some((alias) => normalizeImportHeader(alias) === normalizedHeader)) {
            return column;
        }
    }

    return null;
};

const toImportCell = (value: unknown) => {
    if (value instanceof Date) {
        const year = value.getFullYear();
        const month = String(value.getMonth() + 1).padStart(2, '0');
        const day = String(value.getDate()).padStart(2, '0');
        // Spreadsheet dates are civil dates, not instants. UTC conversion can move them a day.
        return `${year}-${month}-${day}`;
    }

    return String(value ?? '').trim();
};

const normalizeCurrencyCode = (value: string, label: string, rowNumber: number, errors: string[]) => {
    const currencyCode = value.trim().toUpperCase();
    if (!currencyCode) {
        errors.push(`Row ${rowNumber}: ${label} is required.`);
    } else if (!/^[A-Z]{3}$/.test(currencyCode)) {
        errors.push(`Row ${rowNumber}: ${label} must be a 3-letter ISO currency code.`);
    }

    return currencyCode;
};

const normalizeImportDate = (
    value: string | undefined,
    label: string,
    rowNumber: number,
    errors: string[],
    required = false
) => {
    const rawValue = value?.trim() ?? '';
    if (!rawValue) {
        if (required) {
            errors.push(`Row ${rowNumber}: ${label} is required.`);
        }
        return undefined;
    }

    const isoDateMatch = rawValue.match(/^(\d{4})-(\d{2})-(\d{2})$/);
    if (isoDateMatch) {
        const [, year, month, day] = isoDateMatch;
        const parsed = new Date(Date.UTC(Number(year), Number(month) - 1, Number(day)));
        const isValid =
            parsed.getUTCFullYear() === Number(year) &&
            parsed.getUTCMonth() === Number(month) - 1 &&
            parsed.getUTCDate() === Number(day);

        if (isValid) {
            return rawValue;
        }
    }

    const parsed = new Date(rawValue);
    if (Number.isNaN(parsed.getTime())) {
        errors.push(`Row ${rowNumber}: ${label} must be a valid date in YYYY-MM-DD format.`);
        return undefined;
    }

    return parsed.toISOString().split('T')[0];
};

const normalizeRateType = (value: string, rowNumber: number, errors: string[]) => {
    const rawValue = value.trim();
    const rateType = EXCHANGE_RATE_RATE_TYPES.find(
        (type) => type.toLowerCase() === rawValue.toLowerCase()
    );

    if (!rawValue) {
        errors.push(`Row ${rowNumber}: rateType is required.`);
    } else if (!rateType) {
        errors.push(`Row ${rowNumber}: rateType must be one of ${EXCHANGE_RATE_RATE_TYPES.join(', ')}.`);
    }

    return rateType ?? 'Daily';
};

const normalizeOptionalBoolean = (
    value: string | undefined,
    label: string,
    rowNumber: number,
    errors: string[]
) => {
    const rawValue = value?.trim();
    if (!rawValue) {
        return undefined;
    }

    const normalizedValue = rawValue.toLowerCase();
    if (['true', 'yes', 'y', '1'].includes(normalizedValue)) {
        return true;
    }

    if (['false', 'no', 'n', '0'].includes(normalizedValue)) {
        return false;
    }

    errors.push(`Row ${rowNumber}: ${label} must be TRUE/FALSE, Yes/No, or 1/0.`);
    return undefined;
};

export const buildExchangeRateTemplateCsv = (templateDate = new Date().toISOString().split('T')[0]) => {
    const templateRows = [
        ['GHS', 'USD', '0.0800', templateDate, 'Daily', 'Manual', 'Bank of Ghana', `BOG-${templateDate}`, '', 'TRUE', 'Approved'],
        ['GHS', 'EUR', '0.073529', templateDate, 'MonthEnd', 'Manual', 'Bank of Ghana', `BOG-${templateDate}`, '', 'TRUE', 'Approved'],
        ['GHS', 'GBP', '0.063291', templateDate, 'GhanaStatutory', 'Bank of Ghana', 'Bank of Ghana', `BOG-INTERBANK-${templateDate}`, '', 'TRUE', 'Approved'],
    ];

    return [
        EXCHANGE_RATE_TEMPLATE_HEADERS.map(toCsvCell).join(','),
        ...templateRows.map((row) => row.map(toCsvCell).join(',')),
    ].join('\r\n');
};

export const parseExchangeRateImportFile = async (file: File): Promise<ParsedExchangeRateImport> => {
    const errors: string[] = [];
    const lowerFileName = file.name.toLowerCase();

    if (!lowerFileName.endsWith('.csv') && !lowerFileName.endsWith('.xlsx')) {
        return {
            rows: [],
            errors: ['Only CSV and XLSX exchange-rate import files are supported.'],
        };
    }

    const workbook = XLSX.read(await file.arrayBuffer(), { type: 'array', cellDates: false, dateNF: 'yyyy-mm-dd', raw: lowerFileName.endsWith('.csv') });
    const firstSheetName = workbook.SheetNames[0];
    if (!firstSheetName) {
        return { rows: [], errors: ['The selected workbook does not contain any sheets.'] };
    }

    const worksheet = workbook.Sheets[firstSheetName];
    const sheetRows = XLSX.utils.sheet_to_json<unknown[]>(worksheet, {
        header: 1,
        defval: '',
        blankrows: false,
        raw: false,
        dateNF: 'yyyy-mm-dd',
    });

    const headerIndex = sheetRows.findIndex((row) => row.some((cell) => toImportCell(cell) !== ''));
    if (headerIndex < 0) {
        return { rows: [], errors: ['The selected file is empty.'] };
    }

    const headerRow = sheetRows[headerIndex];
    const mappedColumns = headerRow.map(getImportColumn);
    const presentColumns = new Set(mappedColumns.filter(Boolean));
    const missingColumns = EXCHANGE_RATE_REQUIRED_COLUMNS.filter((column) => !presentColumns.has(column));

    if (missingColumns.length > 0) {
        return {
            rows: [],
            errors: [`Missing required column(s): ${missingColumns.join(', ')}.`],
        };
    }

    const importRows: CreateExchangeRateDto[] = [];
    const seenKeys = new Set<string>();

    sheetRows.slice(headerIndex + 1).forEach((row, rowIndex) => {
        const rowNumber = headerIndex + rowIndex + 2;
        if (!row.some((cell) => toImportCell(cell) !== '')) {
            return;
        }

        const parsedRow: ParsedExchangeRateRow = {};
        mappedColumns.forEach((column, columnIndex) => {
            if (column) {
                parsedRow[column] = toImportCell(row[columnIndex]);
            }
        });

        const rowErrors: string[] = [];
        const baseCurrencyCode = normalizeCurrencyCode(parsedRow.baseCurrencyCode ?? '', 'baseCurrencyCode', rowNumber, rowErrors);
        const targetCurrencyCode = normalizeCurrencyCode(parsedRow.targetCurrencyCode ?? '', 'targetCurrencyCode', rowNumber, rowErrors);
        const rate = Number((parsedRow.rate ?? '').replace(/,/g, ''));
        const effectiveDate = normalizeImportDate(parsedRow.effectiveDate, 'effectiveDate', rowNumber, rowErrors, true);
        const expiryDate = normalizeImportDate(parsedRow.expiryDate, 'expiryDate', rowNumber, rowErrors);
        const rateType = normalizeRateType(parsedRow.rateType ?? '', rowNumber, rowErrors);
        const rateSource = (parsedRow.rateSource ?? '').trim();
        const isActive = normalizeOptionalBoolean(parsedRow.isActive, 'isActive', rowNumber, rowErrors);

        if (baseCurrencyCode && targetCurrencyCode && baseCurrencyCode === targetCurrencyCode) {
            rowErrors.push(`Row ${rowNumber}: baseCurrencyCode and targetCurrencyCode cannot be the same.`);
        }

        if (!Number.isFinite(rate) || rate <= 0) {
            rowErrors.push(`Row ${rowNumber}: rate must be a positive number.`);
        }

        if (!rateSource) {
            rowErrors.push(`Row ${rowNumber}: rateSource is required.`);
        } else if (rateSource.length > 20) {
            rowErrors.push(`Row ${rowNumber}: rateSource cannot exceed 20 characters.`);
        }

        if (rateType === 'GhanaStatutory') {
            if (!/bank\s+of\s+ghana|\bbog\b/i.test(rateSource)) {
                rowErrors.push(`Row ${rowNumber}: GhanaStatutory rateSource must identify Bank of Ghana.`);
            }
            if (!(parsedRow.sourceReference ?? '').trim()) {
                rowErrors.push(`Row ${rowNumber}: GhanaStatutory sourceReference is required.`);
            }
        }

        if ((parsedRow.sourceName ?? '').length > 100) {
            rowErrors.push(`Row ${rowNumber}: sourceName cannot exceed 100 characters.`);
        }

        if ((parsedRow.sourceReference ?? '').length > 1000) {
            rowErrors.push(`Row ${rowNumber}: sourceReference cannot exceed 1000 characters.`);
        }

        const duplicateKey = [baseCurrencyCode, targetCurrencyCode, effectiveDate, rateType].join('|');
        if (seenKeys.has(duplicateKey)) {
            rowErrors.push(`Row ${rowNumber}: duplicate currency/date/type row in this file.`);
        }
        seenKeys.add(duplicateKey);

        if (rowErrors.length > 0 || !effectiveDate) {
            errors.push(...rowErrors);
            return;
        }

        importRows.push({
            baseCurrencyCode,
            targetCurrencyCode,
            rate,
            effectiveDate,
            expiryDate,
            rateType,
            rateSource,
            sourceName: parsedRow.sourceName || undefined,
            sourceReference: parsedRow.sourceReference || undefined,
            isActive: isActive ?? true,
            approvalStatus: parsedRow.approvalStatus || 'Approved',
        });
    });

    if (importRows.length === 0 && errors.length === 0) {
        errors.push('No exchange-rate rows were found below the header row.');
    }

    return { rows: importRows, errors };
};

export const formatImportFileSize = (file: File) => {
    if (file.size < 1024) {
        return `${file.size} B`;
    }

    if (file.size < 1024 * 1024) {
        return `${(file.size / 1024).toFixed(1)} KB`;
    }

    return `${(file.size / (1024 * 1024)).toFixed(1)} MB`;
};
