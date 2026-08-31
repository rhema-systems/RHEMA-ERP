'use client';

import React, { useState, useEffect, useMemo } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { ArrowLeft, Save, Plus, Trash2, AlertCircle, FileText, Loader2 } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { ManualJournalAccountCombobox } from '@/components/finance/journal-entries/manual-journal-account-combobox';
import { ManualJournalDimensionCell, ManualJournalDimensionDefaults } from '@/components/finance/journal-entries/manual-journal-dimension-editor';
import type { Account, AccountCurrencyLink, Currency, FinanceDimensionAccountRule, FinanceDimensionDefinition, FinanceSettings, JournalType } from '@/types/finance';
import { financeDataService } from '@/services/finance/finance-data.service';
import { financeService } from '@/services/finance.service';
import { useToast } from '@/hooks/use-toast';
import { mapJournalEntryFormToCreateDto, validateJournalEntryForm } from '@/lib/finance/journal-entry-mapper';
import { useDocumentSequence } from '@/hooks/use-document-sequence';
import { FinanceDocumentTypes } from '@/types/document-numbering';
import {
    ALL_ACTIVE_BOOKS_CODE,
    DEFAULT_ACCOUNTING_BOOKS,
    getAccountingBookName,
    getPostingTargetBooks,
    isAccountEligibleForBook,
    isAllActiveBooksCode,
} from '@/lib/finance/accounting-books';
import {
    applyCanonicalJournalRate,
    getAllowedJournalCurrencies,
    getManualJournalFxBlocker,
    getManualJournalRateRequest,
    normalizeCurrencyCode,
    requireFunctionalCurrency,
} from '@/lib/finance/manual-journal-fx';
import {
    getMissingRequiredManualDimension,
    resolveManualDimensionValues,
} from '@/lib/finance/manual-journal-dimensions';

interface JournalLine {
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
    rateStatus?: 'idle' | 'loading' | 'ready' | 'error';
    rateError?: string;
    rateSource?: string;
    rateDate?: string;
    rateRequestKey?: string;
    dimensions: Record<string, string>;
}

export default function NewJournalEntryPage() {
    const router = useRouter();
    const { toast } = useToast();

    // Accounts from API
    const [accounts, setAccounts] = useState<Account[]>([]);
    const [accountsLoading, setAccountsLoading] = useState(true);
    const [accountingBooks, setAccountingBooks] = useState(DEFAULT_ACCOUNTING_BOOKS);
    const [financeSettings, setFinanceSettings] = useState<FinanceSettings | null>(null);
    const [currencies, setCurrencies] = useState<Currency[]>([]);
    const [currencyLinksByAccount, setCurrencyLinksByAccount] = useState<Record<string, AccountCurrencyLink[]>>({});
    const [currencyReferenceLoading, setCurrencyReferenceLoading] = useState(true);
    const [currencyReferenceError, setCurrencyReferenceError] = useState<string | null>(null);
    const [financeDimensions, setFinanceDimensions] = useState<FinanceDimensionDefinition[]>([]);
    const [dimensionRules, setDimensionRules] = useState<FinanceDimensionAccountRule[]>([]);
    const [dimensionsLoading, setDimensionsLoading] = useState(true);
    const [defaultDimensions, setDefaultDimensions] = useState<Record<string, string>>({});
    const functionalCurrency = financeSettings ? normalizeCurrencyCode(financeSettings.baseCurrency) : '';

    // Header State
    const [header, setHeader] = useState({
        entryDate: new Date().toISOString().split('T')[0],
        journalType: 'General' as JournalType,
        description: '',
        referenceNumber: '',
        notes: '',
        bookClassification: 'IFRS',
    });
    const [journalNumber, setJournalNumber] = useState('');
    const journalSequence = useDocumentSequence('Finance', FinanceDocumentTypes.JournalEntry);
    const [saving, setSaving] = useState(false);
    const [migrationClearingConfigured, setMigrationClearingConfigured] = useState(true);
    const [openingBalanceAutoRoutingEnabled, setOpeningBalanceAutoRoutingEnabled] = useState(true);

    useEffect(() => {
        if (!journalSequence.loading && !journalSequence.allowManualEntry) {
            setJournalNumber('');
        }
    }, [journalSequence.allowManualEntry, journalSequence.loading]);

    // Load accounts on mount
    useEffect(() => {
        const loadAccounts = async () => {
            try {
                setAccountsLoading(true);
                // Load all accounts and filter client-side for active, postable accounts
                const all = await financeDataService.getAccounts();
                setAccounts(all.filter(a => {
                    // Backend sends isPostingAllowed; frontend type says allowDirectPosting
                    const canPost = (a as any).isPostingAllowed ?? a.allowDirectPosting ?? true;
                    const isActive = a.status === 'Active';
                    return canPost && isActive;
                }));
            } catch (e) {
                console.error("Failed to load accounts", e);
                toast({ title: 'Error', description: 'Failed to load chart of accounts', variant: 'destructive' });
            } finally {
                setAccountsLoading(false);
            }
        };

        const loadCurrencyReferenceData = async () => {
            try {
                setCurrencyReferenceLoading(true);
                setCurrencyReferenceError(null);
                const [settings, activeCurrencies] = await Promise.all([
                    financeDataService.getFinanceSettings(),
                    financeDataService.getCurrencies({ isActive: true }),
                ]);
                const baseCurrency = requireFunctionalCurrency(settings);
                if (!activeCurrencies.some(currency =>
                    currency.isActive && normalizeCurrencyCode(currency.currencyCode) === baseCurrency)) {
                    throw new Error(`Functional currency ${baseCurrency} is not active in the Finance currency catalogue.`);
                }

                setFinanceSettings(settings);
                setCurrencies(activeCurrencies);
                setMigrationClearingConfigured(Boolean(settings.migrationClearingAccountId));
                setOpeningBalanceAutoRoutingEnabled(settings.openingBalanceAutoRoutingEnabled ?? true);
                setLines(current => current.map(line => ({
                    ...line,
                    currencyCode: line.currencyCode || baseCurrency,
                    exchangeRate: line.currencyCode && line.currencyCode !== baseCurrency ? line.exchangeRate : 1,
                    rateStatus: line.currencyCode && line.currencyCode !== baseCurrency ? line.rateStatus : 'ready',
                })));
            } catch (e) {
                const message = e instanceof Error ? e.message : 'Failed to load Finance currency settings.';
                console.error('Failed to load Finance currency settings', e);
                setCurrencyReferenceError(message);
            } finally {
                setCurrencyReferenceLoading(false);
            }
        };

        const loadAccountingBooks = async () => {
            try {
                const books = await financeDataService.getAccountingBooks();
                if (books.length > 0) {
                    setAccountingBooks(books);
                }
            } catch (e) {
                console.error("Failed to load accounting books", e);
            }
        };

        loadAccounts();
        loadCurrencyReferenceData();
        loadAccountingBooks();
        Promise.all([
            financeDataService.getFinanceDimensions(),
            financeDataService.getFinanceDimensionRules(),
        ])
            .then(([items, rules]) => {
                setFinanceDimensions(items.filter(item => item.isActive
                    && item.valueSourceType === 'Lookup' && item.classification !== 'Derived'));
                setDimensionRules(rules);
            })
            .catch(() => toast({ title: 'Coding dimensions unavailable', description: 'The journal will fail closed if a dimension is required.', variant: 'destructive' }))
            .finally(() => setDimensionsLoading(false));
    }, []);

    // Lines State
    const [lines, setLines] = useState<JournalLine[]>([
        { id: '1', accountId: '', description: '', currencyCode: '', exchangeRate: 1, debit: 0, credit: 0, dimensions: {} },
        { id: '2', accountId: '', description: '', currencyCode: '', exchangeRate: 1, debit: 0, credit: 0, dimensions: {} },
    ]);

    // Computed Totals
    const totalDebit = lines.reduce((sum, line) => sum + (line.debit || 0), 0);
    const totalCredit = lines.reduce((sum, line) => sum + (line.credit || 0), 0);
    const isBalanced = Math.abs(totalDebit - totalCredit) < 0.01;
    const allowOpeningBalanceAutoBalance = header.journalType === 'Opening Balance' && openingBalanceAutoRoutingEnabled;
    const isAllActiveBooks = isAllActiveBooksCode(header.bookClassification);
    const targetAccountingBooks = useMemo(
        () => getPostingTargetBooks(accountingBooks, header.bookClassification),
        [accountingBooks, header.bookClassification]
    );
    const selectedBookName = getAccountingBookName(accountingBooks, header.bookClassification);
    const targetBookLabel = isAllActiveBooks ? 'all active books' : selectedBookName;
    const targetBookListText = targetAccountingBooks.map(book => book.name).join(', ');
    const invalidLines = useMemo(() => {
        return lines
            .map((line, index) => {
                const account = accounts.find(a => a.id === line.accountId);
                if (!account) return null;
                const eligible = targetAccountingBooks.length > 0
                    ? targetAccountingBooks.every(book => isAccountEligibleForBook(account, book.code))
                    : isAccountEligibleForBook(account, header.bookClassification);
                return eligible
                    ? null
                    : { id: line.id, index: index + 1, accountLabel: `${account.accountNumber} - ${account.accountName}` };
            })
            .filter((v): v is { id: string; index: number; accountLabel: string } => v !== null);
    }, [accounts, header.bookClassification, lines, targetAccountingBooks]);
    const fxBlockingMessage = useMemo(() => lines
        .filter(line => line.accountId && (line.debit > 0 || line.credit > 0 || line.foreignDebit || line.foreignCredit))
        .map(line => getManualJournalFxBlocker(line, functionalCurrency))
        .find((message): message is string => Boolean(message)) ?? null,
    [functionalCurrency, lines]);

    const handleJournalTypeChange = (value: JournalType) => {
        setHeader(current => ({
            ...current,
            journalType: value,
            bookClassification:
                value === 'Opening Balance' || !isAllActiveBooksCode(current.bookClassification)
                    ? current.bookClassification
                    : 'IFRS',
        }));
    };

    const handleAddLine = () => {
        setLines([
            ...lines,
            {
                id: Date.now().toString(),
                accountId: '',
                description: '',
                currencyCode: functionalCurrency,
                exchangeRate: 1,
                debit: 0,
                credit: 0,
                rateStatus: functionalCurrency ? 'ready' : 'idle',
                dimensions: { ...defaultDimensions },
            },
        ]);
    };

    const handleRemoveLine = (id: string) => {
        if (lines.length > 2) {
            setLines(lines.filter(l => l.id !== id));
        }
    };

    const applyDimensionsToAllLines = (preferredValues: Record<string, string>) => {
        setLines(current => current.map(line => ({
            ...line,
            dimensions: line.accountId
                ? resolveManualDimensionValues(financeDimensions, dimensionRules, line.accountId, header.entryDate, preferredValues)
                : { ...preferredValues },
        })));
    };

    const loadAccountCurrencyLinks = async (account: Account): Promise<AccountCurrencyLink[]> => {
        if (!account.isMultiCurrency) return [];
        const cached = currencyLinksByAccount[account.id];
        if (cached) return cached;

        const links = await financeDataService.getAccountCurrencyLinks(account.id);
        setCurrencyLinksByAccount(current => ({ ...current, [account.id]: links }));
        return links;
    };

    const resolveCanonicalRate = async (
        lineId: string,
        account: Account,
        currencyCode: string,
        links: AccountCurrencyLink[],
        effectiveDate: string,
    ) => {
        if (!financeSettings) return;
        const currency = normalizeCurrencyCode(currencyCode);
        const allowedCurrencies = getAllowedJournalCurrencies(
            account,
            links,
            currencies,
            functionalCurrency,
            effectiveDate,
        );
        if (!allowedCurrencies.includes(currency)) {
            setLines(current => current.map(line => line.id === lineId ? {
                ...line,
                currencyCode: currency,
                exchangeRateId: undefined,
                exchangeRate: '',
                rateStatus: 'error',
                rateError: `${currency} is not effective for account ${account.accountNumber} on ${effectiveDate}.`,
                rateSource: undefined,
                rateDate: undefined,
                rateRequestKey: undefined,
            } : line));
            return;
        }
        if (currency === functionalCurrency) {
            setLines(current => current.map(line => line.id === lineId ? {
                ...line,
                currencyCode: currency,
                exchangeRateId: undefined,
                exchangeRate: 1,
                rateStatus: 'ready',
                rateError: undefined,
                rateSource: undefined,
                rateDate: effectiveDate,
                rateRequestKey: undefined,
            } : line));
            return;
        }

        const request = getManualJournalRateRequest(account, currency, links, financeSettings, effectiveDate);
        if (!request) return;
        const requestKey = [account.id, currency, effectiveDate, request.rateType, request.quoteSide].join('|');
        setLines(current => current.map(line => line.id === lineId ? {
            ...line,
            currencyCode: currency,
            exchangeRateId: undefined,
            exchangeRate: '',
            rateStatus: 'loading',
            rateError: undefined,
            rateRequestKey: requestKey,
        } : line));

        try {
            const snapshot = await financeService.getCurrentExchangeRate(currency, request);
            setLines(current => current.map(line =>
                line.id === lineId && line.rateRequestKey === requestKey
                    ? { ...applyCanonicalJournalRate(line, snapshot), rateRequestKey: undefined }
                    : line));
        } catch (error) {
            const message = error instanceof Error
                ? error.message
                : `No approved ${currency} ${request.rateType}/${request.quoteSide} rate exists for ${effectiveDate}.`;
            setLines(current => current.map(line =>
                line.id === lineId && line.rateRequestKey === requestKey
                    ? {
                        ...line,
                        exchangeRateId: undefined,
                        exchangeRate: '',
                        rateStatus: 'error',
                        rateError: message,
                        rateSource: undefined,
                        rateDate: undefined,
                        rateRequestKey: undefined,
                    }
                    : line));
        }
    };

    const handleAccountChange = async (lineId: string, accountId: string) => {
        const account = accounts.find(item => item.id === accountId);
        if (!account || !financeSettings) return;
        const eligible = targetAccountingBooks.length > 0
            ? targetAccountingBooks.every(book => isAccountEligibleForBook(account, book.code))
            : isAccountEligibleForBook(account, header.bookClassification);
        if (!eligible) return;

        try {
            const links = await loadAccountCurrencyLinks(account);
            const allowedCurrencies = getAllowedJournalCurrencies(account, links, currencies, functionalCurrency, header.entryDate);
            const accountCurrency = normalizeCurrencyCode(account.currencyCode) || functionalCurrency;
            const currency = allowedCurrencies.includes(accountCurrency) ? accountCurrency : allowedCurrencies[0];
            if (!currency) {
                throw new Error(`Account ${account.accountNumber} has no active permitted transaction currency.`);
            }

            setLines(current => current.map(line => line.id === lineId ? {
                ...line,
                accountId,
                currencyCode: currency,
                exchangeRateId: undefined,
                exchangeRate: currency === functionalCurrency ? 1 : '',
                foreignDebit: currency === functionalCurrency ? undefined : 0,
                foreignCredit: currency === functionalCurrency ? undefined : 0,
                debit: currency === functionalCurrency ? line.debit : 0,
                credit: currency === functionalCurrency ? line.credit : 0,
                dimensions: resolveManualDimensionValues(
                    financeDimensions, dimensionRules, accountId, header.entryDate, defaultDimensions,
                ),
                rateStatus: currency === functionalCurrency ? 'ready' : 'idle',
                rateError: undefined,
            } : line));
            await resolveCanonicalRate(lineId, account, currency, links, header.entryDate);
        } catch (error) {
            const message = error instanceof Error ? error.message : 'Failed to load the account currency policy.';
            setLines(current => current.map(line => line.id === lineId ? {
                ...line,
                accountId,
                exchangeRateId: undefined,
                exchangeRate: '',
                rateStatus: 'error',
                rateError: message,
            } : line));
        }
    };

    const clearAccount = (lineId: string) => {
        setLines(current => current.map(line => line.id === lineId ? {
            ...line,
            accountId: '',
            currencyCode: functionalCurrency,
            exchangeRateId: undefined,
            exchangeRate: 1,
            foreignDebit: undefined,
            foreignCredit: undefined,
            dimensions: { ...defaultDimensions },
            rateStatus: 'ready',
            rateError: undefined,
            rateSource: undefined,
            rateDate: undefined,
            rateRequestKey: undefined,
        } : line));
    };

    const handleCurrencyChange = async (lineId: string, currencyCode: string) => {
        const line = lines.find(item => item.id === lineId);
        const account = accounts.find(item => item.id === line?.accountId);
        if (!line || !account) return;
        const links = await loadAccountCurrencyLinks(account);
        const currency = normalizeCurrencyCode(currencyCode);
        setLines(current => current.map(item => item.id === lineId ? {
            ...item,
            currencyCode: currency,
            exchangeRateId: undefined,
            exchangeRate: currency === functionalCurrency ? 1 : '',
            foreignDebit: currency === functionalCurrency ? undefined : 0,
            foreignCredit: currency === functionalCurrency ? undefined : 0,
            debit: 0,
            credit: 0,
            rateStatus: currency === functionalCurrency ? 'ready' : 'idle',
            rateError: undefined,
        } : item));
        await resolveCanonicalRate(lineId, account, currency, links, header.entryDate);
    };

    const handleEntryDateChange = (effectiveDate: string) => {
        setHeader(current => ({ ...current, entryDate: effectiveDate }));
        for (const line of lines) {
            const account = accounts.find(item => item.id === line.accountId);
            if (!account || normalizeCurrencyCode(line.currencyCode) === functionalCurrency) continue;
            void loadAccountCurrencyLinks(account)
                .then(links => resolveCanonicalRate(line.id, account, line.currencyCode, links, effectiveDate));
        }
    };

    const updateLine = (id: string, field: keyof JournalLine, value: any) => {
        setLines(current => current.map(line => {
            if (line.id !== id) return line;
            const updatedLine = { ...line, [field]: value };
            const isForeign = normalizeCurrencyCode(updatedLine.currencyCode) !== functionalCurrency;
            if (isForeign) {
                const rate = typeof updatedLine.exchangeRate === 'number' ? updatedLine.exchangeRate : 0;
                if (field === 'foreignDebit') {
                    updatedLine.debit = (value || 0) * rate;
                    updatedLine.foreignCredit = 0;
                    updatedLine.credit = 0;
                } else if (field === 'foreignCredit') {
                    updatedLine.credit = (value || 0) * rate;
                    updatedLine.foreignDebit = 0;
                    updatedLine.debit = 0;
                }
            } else {
                if (field === 'debit' && value > 0) updatedLine.credit = 0;
                if (field === 'credit' && value > 0) updatedLine.debit = 0;
            }
            return updatedLine;
        }));
    };

    const handleSaveDraft = async () => {
        if (currencyReferenceLoading || currencyReferenceError || !functionalCurrency) {
            toast({
                title: 'Currency Configuration',
                description: currencyReferenceError || 'Finance currency configuration is still loading.',
                variant: 'destructive',
            });
            return;
        }

        const fxBlocker = lines
            .filter(line => line.accountId && (line.debit > 0 || line.credit > 0 || line.foreignDebit || line.foreignCredit))
            .map(line => getManualJournalFxBlocker(line, functionalCurrency))
            .find((message): message is string => Boolean(message));
        if (fxBlocker) {
            toast({ title: 'Exchange Rate Required', description: fxBlocker, variant: 'destructive' });
            return;
        }

        if (header.journalType === 'Opening Balance' && openingBalanceAutoRoutingEnabled && !migrationClearingConfigured) {
            toast({
                title: 'Migration Clearing Account Required',
                description: 'Set Migration Clearing Account in Finance Settings before saving Opening Balance journals.',
                variant: 'destructive'
            });
            return;
        }

        if (invalidLines.length > 0) {
            toast({
                title: 'Classification Validation',
                description: `Line ${invalidLines[0].index} account is not classified for ${targetBookLabel}.`,
                variant: 'destructive'
            });
            return;
        }

        const missingDimension = lines
            .filter(line => line.accountId && (line.debit > 0 || line.credit > 0))
            .map((line, index) => {
                const rule = getMissingRequiredManualDimension(
                    dimensionRules, line.accountId, header.entryDate, line.dimensions,
                );
                return rule ? `Line ${index + 1} requires ${rule.dimensionName}.` : null;
            }).find((message): message is string => Boolean(message));
        if (missingDimension) {
            toast({ title: 'Coding dimension required', description: missingDimension, variant: 'destructive' });
            return;
        }

        // Use centralised contract guard for validation and mapping
        const validationErrors = validateJournalEntryForm(header, lines, journalNumber, {
            openingBalanceAutoRoutingEnabled,
            requireJournalNumber: false,
        });
        if (validationErrors.length > 0) {
            toast({ title: 'Validation', description: validationErrors[0], variant: 'destructive' });
            return;
        }

        const dto = mapJournalEntryFormToCreateDto(
            header,
            lines,
            journalSequence.allowManualEntry ? journalNumber : undefined,
            functionalCurrency
        );

        try {
            setSaving(true);
            const created = await financeDataService.createJournalEntry(dto);
            toast({ title: 'Success', description: `Journal entry ${created.journalEntryNumber} saved as Draft` });
            router.push(`/finance/journal-entries/${created.id}`);
        } catch (err: any) {
            console.error('Failed to save journal entry', err);
            toast({ title: 'Error', description: err?.message || 'Failed to save journal entry', variant: 'destructive' });
        } finally {
            setSaving(false);
        }
    };

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">New Journal Entry</h1>
                    <p className="text-muted-foreground">Create a new general ledger entry</p>
                </div>
                <div className="flex gap-2">
                    <Button variant="outline" onClick={() => router.back()}>
                        <ArrowLeft className="mr-2 h-4 w-4" />
                        Cancel
                    </Button>
                    <Button
                        onClick={handleSaveDraft}
                        disabled={
                            saving
                            || currencyReferenceLoading
                            || Boolean(currencyReferenceError)
                            || Boolean(fxBlockingMessage)
                            || (header.journalType === 'Opening Balance' && openingBalanceAutoRoutingEnabled && !migrationClearingConfigured)
                        }
                    >
                        {saving ? (
                            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                        ) : (
                            <Save className="mr-2 h-4 w-4" />
                        )}
                        Save as Draft
                    </Button>
                </div>
            </div>

            {/* Breadcrumbs */}
            <Breadcrumb>
                <BreadcrumbList>
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/finance">Finance</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/finance/journal-entries">Journal Entries</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>New Entry</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Validation Alert */}
            {!allowOpeningBalanceAutoBalance && !isBalanced && totalDebit > 0 && (
                <Alert variant="destructive">
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Entry is not balanced</AlertTitle>
                    <AlertDescription>
                        Total Debits ({totalDebit.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}) must equal Total Credits ({totalCredit.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}).
                        Difference: {Math.abs(totalDebit - totalCredit).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                    </AlertDescription>
                </Alert>
            )}
            {invalidLines.length > 0 && (
                <Alert variant="destructive">
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Book Classification Mismatch</AlertTitle>
                    <AlertDescription>
                        {invalidLines.length} line(s) use account(s) not classified for {targetBookLabel}: {invalidLines.map(l => l.index).join(', ')}.
                    </AlertDescription>
                </Alert>
            )}
            {currencyReferenceError && (
                <Alert variant="destructive">
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Finance Currency Configuration Unavailable</AlertTitle>
                    <AlertDescription>{currencyReferenceError}</AlertDescription>
                </Alert>
            )}
            {header.journalType === 'Opening Balance' && openingBalanceAutoRoutingEnabled && !migrationClearingConfigured && (
                <Alert variant="destructive">
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Migration Clearing Account Not Configured</AlertTitle>
                    <AlertDescription>
                        Opening Balance journals require Migration Clearing Account in Finance Settings.
                    </AlertDescription>
                </Alert>
            )}
            {header.journalType === 'Opening Balance' && isAllActiveBooks && (
                <Alert>
                    <FileText className="h-4 w-4" />
                    <AlertTitle>All Active Books Posting</AlertTitle>
                    <AlertDescription>
                        This draft will be duplicated when posted to: {targetBookListText || 'No active posting books configured'}.
                        The active book list is resolved again at posting time.
                    </AlertDescription>
                </Alert>
            )}

            {/* Header Form */}
            <Card>
                <CardHeader>
                    <CardTitle>Header Information</CardTitle>
                </CardHeader>
                <CardContent>
                    <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                        <div className="space-y-2">
                            <Label htmlFor="journalId">Journal ID</Label>
                            <Input
                                id="journalId"
                                value={journalSequence.allowManualEntry ? journalNumber : journalSequence.sampleNumber}
                                onChange={(event) => setJournalNumber(event.target.value)}
                                disabled={!journalSequence.allowManualEntry || journalSequence.loading}
                                placeholder={journalSequence.allowManualEntry ? `Auto: ${journalSequence.sampleNumber}` : undefined}
                                className="bg-muted font-mono"
                            />
                            {!journalSequence.allowManualEntry && (
                                <p className="text-xs text-muted-foreground">Assigned by the configured Journal Entry sequence when saved.</p>
                            )}
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="entryDate">Date *</Label>
                            <Input
                                id="entryDate"
                                type="date"
                                value={header.entryDate}
                                onChange={(e) => handleEntryDateChange(e.target.value)}
                                required
                            />
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="journalType">Type *</Label>
                            <Select
                                value={header.journalType}
                                onValueChange={(value) => handleJournalTypeChange(value as JournalType)}
                            >
                                <SelectTrigger><SelectValue /></SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="General">General</SelectItem>
                                    <SelectItem value="Adjusting">Adjusting</SelectItem>
                                    <SelectItem value="Reversing">Reversing</SelectItem>
                                    <SelectItem value="Opening Balance">Opening Balance</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="bookClassification">Book Classification *</Label>
                            <Select
                                value={header.bookClassification}
                                onValueChange={(value) => setHeader({ ...header, bookClassification: value })}
                            >
                                <SelectTrigger><SelectValue /></SelectTrigger>
                                <SelectContent>
                                    {header.journalType === 'Opening Balance' && (
                                        <SelectItem value={ALL_ACTIVE_BOOKS_CODE}>All Active Books</SelectItem>
                                    )}
                                    {accountingBooks.map((book) => (
                                        <SelectItem key={book.code} value={book.code}>{book.name}</SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>

                        <div className="space-y-2 md:col-span-2">
                            <Label htmlFor="description">Description *</Label>
                            <Input
                                id="description"
                                value={header.description}
                                onChange={(e) => setHeader({ ...header, description: e.target.value })}
                                placeholder="e.g., Monthly Rent Payment"
                                required
                            />
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="reference">Reference #</Label>
                            <Input
                                id="reference"
                                value={header.referenceNumber}
                                onChange={(e) => setHeader({ ...header, referenceNumber: e.target.value })}
                                placeholder="Optional"
                            />
                        </div>
                    </div>
                </CardContent>
            </Card>

            {/* Lines Form */}
            <Card>
                <CardHeader className="flex flex-row items-center justify-between">
                    <CardTitle>Transaction Lines</CardTitle>
                    <Button variant="outline" size="sm" onClick={handleAddLine}>
                        <Plus className="mr-2 h-4 w-4" />
                        Add Line
                    </Button>
                </CardHeader>
                <CardContent>
                    <ManualJournalDimensionDefaults
                        definitions={financeDimensions}
                        effectiveDate={header.entryDate}
                        values={defaultDimensions}
                        onChange={setDefaultDimensions}
                        onApplyToAll={() => applyDimensionsToAllLines(defaultDimensions)}
                        disabled={dimensionsLoading}
                    />
                    {accountsLoading ? (
                        <div className="flex items-center justify-center py-8">
                            <Loader2 className="h-6 w-6 animate-spin mr-2 text-muted-foreground" />
                            <span className="text-muted-foreground">Loading chart of accounts...</span>
                        </div>
                    ) : (
                        <div className="rounded-md border overflow-x-auto">
                            <table className="w-full min-w-[1120px]">
                                <thead>
                                    <tr className="border-b bg-muted/50">
                                        <th className="p-3 text-left font-medium w-[20%]">Account</th>
                                        <th className="p-3 text-left font-medium w-[20%]">Description</th>
                                        <th className="p-3 text-left font-medium w-[10%]">Currency</th>
                                        <th className="p-3 text-right font-medium w-[8%]">Ex. Rate</th>
                                        <th className="p-3 text-right font-medium w-[10%]">F. Debit</th>
                                        <th className="p-3 text-right font-medium w-[10%]">F. Credit</th>
                                        <th className="p-3 text-right font-medium w-[10%]">Debit ({functionalCurrency || '—'})</th>
                                        <th className="p-3 text-right font-medium w-[10%]">Credit ({functionalCurrency || '—'})</th>
                                        <th className="p-3 text-left font-medium min-w-[170px]">Coding</th>
                                        <th className="p-3 text-center w-[2%]"></th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {lines.map((line, lineIndex) => {
                                        const isForeign = normalizeCurrencyCode(line.currencyCode) !== functionalCurrency;
                                        const account = accounts.find(a => a.id === line.accountId);
                                        const allowedCurrencies = getAllowedJournalCurrencies(
                                            account,
                                            account ? currencyLinksByAccount[account.id] ?? [] : [],
                                            currencies,
                                            functionalCurrency,
                                            header.entryDate,
                                        );
                                        const isCurrencyEditable = Boolean(account?.isMultiCurrency && allowedCurrencies.length > 1);

                                        return (
                                            <tr key={line.id} className="border-b last:border-0">
                                                <td className="p-3">
                                                    <ManualJournalAccountCombobox
                                                        accounts={accounts}
                                                        selectedAccountId={line.accountId}
                                                        lineNumber={lineIndex + 1}
                                                        targetAccountingBooks={targetAccountingBooks}
                                                        fallbackBookCode={header.bookClassification}
                                                        targetBookLabel={targetBookLabel}
                                                        onSelect={(accountId) => handleAccountChange(line.id, accountId)}
                                                        onClear={() => clearAccount(line.id)}
                                                    />
                                                    {line.accountId && (() => {
                                                        const selected = accounts.find(a => a.id === line.accountId);
                                                        const eligible = selected && targetAccountingBooks.length > 0
                                                            ? targetAccountingBooks.every(book => isAccountEligibleForBook(selected, book.code))
                                                            : selected ? isAccountEligibleForBook(selected, header.bookClassification) : false;
                                                        if (!selected || eligible) return null;
                                                        return (
                                                            <p className="mt-1 text-xs text-red-600">
                                                                Not classified for {targetBookLabel}.
                                                            </p>
                                                        );
                                                    })()}
                                                </td>
                                                <td className="p-3">
                                                    <Input
                                                        value={line.description}
                                                        onChange={(e) => updateLine(line.id, 'description', e.target.value)}
                                                        placeholder="Desc"
                                                    />
                                                </td>
                                                <td className="p-3">
                                                    <Select
                                                        value={line.currencyCode}
                                                        onValueChange={(value) => void handleCurrencyChange(line.id, value)}
                                                        disabled={!isCurrencyEditable || currencyReferenceLoading}
                                                    >
                                                        <SelectTrigger className="w-[80px]">
                                                            <SelectValue />
                                                        </SelectTrigger>
                                                        <SelectContent>
                                                            {allowedCurrencies.map(currency => (
                                                                <SelectItem key={currency} value={currency}>{currency}</SelectItem>
                                                            ))}
                                                        </SelectContent>
                                                    </Select>
                                                </td>
                                                <td className="p-3">
                                                    {isForeign && (
                                                        <div className="space-y-1">
                                                            <Input
                                                                type="number"
                                                                step="0.000001"
                                                                value={line.exchangeRate}
                                                                readOnly
                                                                aria-label={`${line.currencyCode} canonical exchange rate`}
                                                                className="text-right w-full bg-muted"
                                                            />
                                                            {line.rateStatus === 'loading' && (
                                                                <p className="text-[10px] text-muted-foreground">Loading approved rate…</p>
                                                            )}
                                                            {line.rateStatus === 'error' && (
                                                                <p className="text-[10px] text-red-600">{line.rateError}</p>
                                                            )}
                                                            {line.rateStatus === 'ready' && line.rateSource && (
                                                                <p className="text-[10px] text-muted-foreground" title={line.rateDate}>
                                                                    {line.rateSource}
                                                                </p>
                                                            )}
                                                        </div>
                                                    )}
                                                </td>
                                                <td className="p-3">
                                                    <Input
                                                        type="number"
                                                        min="0"
                                                        step="0.01"
                                                        value={line.foreignDebit || ''}
                                                        onChange={(e) => updateLine(line.id, 'foreignDebit', parseFloat(e.target.value) || 0)}
                                                        className={`text-right ${isForeign ? 'bg-blue-50' : 'bg-muted'}`}
                                                        disabled={!isForeign || !!line.foreignCredit}
                                                    />
                                                </td>
                                                <td className="p-3">
                                                    <Input
                                                        type="number"
                                                        min="0"
                                                        step="0.01"
                                                        value={line.foreignCredit || ''}
                                                        onChange={(e) => updateLine(line.id, 'foreignCredit', parseFloat(e.target.value) || 0)}
                                                        className={`text-right ${isForeign ? 'bg-blue-50' : 'bg-muted'}`}
                                                        disabled={!isForeign || !!line.foreignDebit}
                                                    />
                                                </td>
                                                <td className="p-3">
                                                    <Input
                                                        type="number"
                                                        min="0"
                                                        step="0.01"
                                                        value={line.debit || ''}
                                                        onChange={(e) => updateLine(line.id, 'debit', parseFloat(e.target.value) || 0)}
                                                        className="text-right"
                                                        disabled={isForeign || line.credit > 0}
                                                        readOnly={isForeign}
                                                    />
                                                </td>
                                                <td className="p-3">
                                                    <Input
                                                        type="number"
                                                        min="0"
                                                        step="0.01"
                                                        value={line.credit || ''}
                                                        onChange={(e) => updateLine(line.id, 'credit', parseFloat(e.target.value) || 0)}
                                                        className="text-right"
                                                        disabled={isForeign || line.debit > 0}
                                                        readOnly={isForeign}
                                                    />
                                                </td>
                                                <td className="p-3 align-top">
                                                    <ManualJournalDimensionCell
                                                        definitions={financeDimensions}
                                                        rules={dimensionRules}
                                                        effectiveDate={header.entryDate}
                                                        lineNumber={lineIndex + 1}
                                                        accountId={line.accountId}
                                                        accountLabel={account ? `${account.accountNumber} - ${account.accountName}` : undefined}
                                                        values={line.dimensions}
                                                        defaults={defaultDimensions}
                                                        previousValues={lineIndex > 0 ? lines[lineIndex - 1].dimensions : undefined}
                                                        loading={dimensionsLoading}
                                                        onChange={dimensions => setLines(current => current.map(item =>
                                                            item.id === line.id ? { ...item, dimensions } : item))}
                                                        onApplyToAll={applyDimensionsToAllLines}
                                                    />
                                                </td>
                                                <td className="p-3 text-center">
                                                    <Button
                                                        variant="ghost"
                                                        size="sm"
                                                        onClick={() => handleRemoveLine(line.id)}
                                                        disabled={lines.length <= 2}
                                                        className="text-destructive hover:text-destructive"
                                                    >
                                                        <Trash2 className="h-4 w-4" />
                                                    </Button>
                                                </td>
                                            </tr>
                                        );
                                    })}
                                </tbody>
                                <tfoot>
                                    <tr className="bg-muted/50 font-bold">
                                        <td colSpan={6} className="p-3 text-right">Totals ({functionalCurrency || '—'}):</td>
                                        <td className="p-3 text-right">{totalDebit.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
                                        <td className="p-3 text-right">{totalCredit.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
                                        <td></td>
                                        <td></td>
                                    </tr>
                                </tfoot>
                            </table>
                        </div>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}
