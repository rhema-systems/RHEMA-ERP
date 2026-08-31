'use client';

import React, { useEffect, useMemo, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { ArrowLeft, Save, Plus, Trash2, AlertCircle, Loader2 } from 'lucide-react';
import { useParams, useRouter } from 'next/navigation';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { ManualJournalDimensionCell, ManualJournalDimensionDefaults } from '@/components/finance/journal-entries/manual-journal-dimension-editor';
import { ManualJournalAccountCombobox } from '@/components/finance/journal-entries/manual-journal-account-combobox';
import type {
    Account,
    AccountCurrencyLink,
    CreateAccountTransactionDto,
    CreateJournalEntryDto,
    Currency,
    FinanceDimensionAccountRule,
    FinanceDimensionDefinition,
    FinanceSettings,
    JournalEntry,
    JournalType,
} from '@/types/finance';
import { financeDataService } from '@/services/finance/finance-data.service';
import { financeService } from '@/services/finance.service';
import { useToast } from '@/hooks/use-toast';
import { validateJournalEntryForm } from '@/lib/finance/journal-entry-mapper';
import {
    DEFAULT_ACCOUNTING_BOOKS,
    getAccountingBookName,
    getPostingTargetBooks,
    isAccountEligibleForBook,
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
    getCommonManualDimensionValues,
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

const toDateInputValue = (dateString?: string) => {
    if (!dateString) return new Date().toISOString().split('T')[0];
    return new Date(dateString).toISOString().split('T')[0];
};

const mapEntryToLines = (entry: JournalEntry, functionalCurrency: string): JournalLine[] => {
    const transactions = entry.transactions || [];
    if (transactions.length === 0) {
        return [
            { id: '1', accountId: '', description: '', currencyCode: functionalCurrency, exchangeRate: 1, debit: 0, credit: 0, rateStatus: 'ready', dimensions: {} },
            { id: '2', accountId: '', description: '', currencyCode: functionalCurrency, exchangeRate: 1, debit: 0, credit: 0, rateStatus: 'ready', dimensions: {} },
        ];
    }

    return transactions.map((transaction, index) => {
        const raw = transaction as any;
        const currencyCode = normalizeCurrencyCode(raw.currencyCode || raw.transactionCurrency || entry.primaryCurrency) || functionalCurrency;
        const exchangeRate = raw.exchangeRate ?? 1;
        const exchangeRateId = raw.exchangeRateId as string | undefined;
        const foreignAmount = raw.foreignAmount ?? raw.foreignCurrencyAmount;
        const debit = transaction.transactionType === 'Debit' ? transaction.amount : 0;
        const credit = transaction.transactionType === 'Credit' ? transaction.amount : 0;

        return {
            id: transaction.id || `${index + 1}`,
            accountId: transaction.accountId,
            description: transaction.description || '',
            currencyCode,
            exchangeRateId,
            exchangeRate,
            debit,
            credit,
            foreignDebit: transaction.transactionType === 'Debit' ? foreignAmount : 0,
            foreignCredit: transaction.transactionType === 'Credit' ? foreignAmount : 0,
            rateStatus: currencyCode === functionalCurrency || exchangeRateId ? 'ready' : 'idle',
            rateSource: raw.exchangeRateSource,
            rateDate: raw.exchangeRateDate,
            dimensions: Object.fromEntries((transaction.dimensions ?? []).map(item => [item.dimensionCode, item.valueCode])),
        };
    });
};

export default function EditJournalEntryPage() {
    const router = useRouter();
    const params = useParams();
    const { toast } = useToast();
    const id = params.id as string;

    const [entry, setEntry] = useState<JournalEntry | null>(null);
    const [accounts, setAccounts] = useState<Account[]>([]);
    const [accountingBooks, setAccountingBooks] = useState(DEFAULT_ACCOUNTING_BOOKS);
    const [financeSettings, setFinanceSettings] = useState<FinanceSettings | null>(null);
    const [currencies, setCurrencies] = useState<Currency[]>([]);
    const [currencyLinksByAccount, setCurrencyLinksByAccount] = useState<Record<string, AccountCurrencyLink[]>>({});
    const [currencyReferenceError, setCurrencyReferenceError] = useState<string | null>(null);
    const [financeDimensions, setFinanceDimensions] = useState<FinanceDimensionDefinition[]>([]);
    const [dimensionRules, setDimensionRules] = useState<FinanceDimensionAccountRule[]>([]);
    const [defaultDimensions, setDefaultDimensions] = useState<Record<string, string>>({});
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const functionalCurrency = financeSettings ? normalizeCurrencyCode(financeSettings.baseCurrency) : '';

    const [header, setHeader] = useState({
        entryDate: new Date().toISOString().split('T')[0],
        journalType: 'General' as JournalType,
        description: '',
        referenceNumber: '',
        notes: '',
        bookClassification: 'IFRS',
    });

    const [lines, setLines] = useState<JournalLine[]>([
        { id: '1', accountId: '', description: '', currencyCode: '', exchangeRate: 1, debit: 0, credit: 0, dimensions: {} },
        { id: '2', accountId: '', description: '', currencyCode: '', exchangeRate: 1, debit: 0, credit: 0, dimensions: {} },
    ]);

    useEffect(() => {
        const loadData = async () => {
            try {
                setLoading(true);
                const [journalEntry, allAccounts, books, settings, activeCurrencies, dimensions, rules] = await Promise.all([
                    financeDataService.getJournalEntryById(id),
                    financeDataService.getAccounts(),
                    financeDataService.getAccountingBooks().catch(() => DEFAULT_ACCOUNTING_BOOKS),
                    financeDataService.getFinanceSettings(),
                    financeDataService.getCurrencies({ isActive: true }),
                    financeDataService.getFinanceDimensions(),
                    financeDataService.getFinanceDimensionRules(),
                ]);
                const baseCurrency = requireFunctionalCurrency(settings);
                if (!activeCurrencies.some(currency =>
                    currency.isActive && normalizeCurrencyCode(currency.currencyCode) === baseCurrency)) {
                    throw new Error(`Functional currency ${baseCurrency} is not active in the Finance currency catalogue.`);
                }

                const mappedLines = mapEntryToLines(journalEntry, baseCurrency);
                const loadedLinks: Record<string, AccountCurrencyLink[]> = {};
                const hydratedLines = await Promise.all(mappedLines.map(async line => {
                    if (!line.accountId || normalizeCurrencyCode(line.currencyCode) === baseCurrency) return line;
                    const account = allAccounts.find(item => item.id === line.accountId);
                    if (!account) {
                        return { ...line, exchangeRate: '' as const, rateStatus: 'error' as const, rateError: 'The journal account is no longer available.' };
                    }

                    try {
                        const links = account.isMultiCurrency
                            ? await financeDataService.getAccountCurrencyLinks(account.id)
                            : [];
                        loadedLinks[account.id] = links;
                        const allowedCurrencies = getAllowedJournalCurrencies(
                            account,
                            links,
                            activeCurrencies,
                            baseCurrency,
                            toDateInputValue(journalEntry.entryDate || journalEntry.transactionDate),
                        );
                        if (!allowedCurrencies.includes(normalizeCurrencyCode(line.currencyCode))) {
                            throw new Error(`${line.currencyCode} is not an active permitted currency for account ${account.accountNumber || account.accountCode}.`);
                        }
                        const request = getManualJournalRateRequest(account, line.currencyCode, links, settings, toDateInputValue(journalEntry.entryDate || journalEntry.transactionDate));
                        if (!request) throw new Error(`No rate policy exists for ${line.currencyCode}.`);
                        const snapshot = line.exchangeRateId
                            ? await financeDataService.getExchangeRateById(line.exchangeRateId)
                            : await financeService.getCurrentExchangeRate(line.currencyCode, request);
                        return applyCanonicalJournalRate(line, snapshot);
                    } catch (error) {
                        return {
                            ...line,
                            exchangeRate: '' as const,
                            rateStatus: 'error' as const,
                            rateError: error instanceof Error ? error.message : `No approved ${line.currencyCode} rate is available.`,
                        };
                    }
                }));

                setEntry(journalEntry);
                setFinanceSettings(settings);
                setCurrencies(activeCurrencies);
                setCurrencyLinksByAccount(loadedLinks);
                setFinanceDimensions(dimensions.filter(item => item.isActive
                    && item.valueSourceType === 'Lookup' && item.classification !== 'Derived'));
                setDimensionRules(rules);
                setCurrencyReferenceError(null);
                setHeader({
                    entryDate: toDateInputValue(journalEntry.entryDate || journalEntry.transactionDate),
                    journalType: (journalEntry.journalType || 'General') as JournalType,
                    description: journalEntry.description || '',
                    referenceNumber: journalEntry.referenceNumber || journalEntry.reference || '',
                    notes: journalEntry.notes || '',
                    bookClassification: journalEntry.bookClassification || 'IFRS',
                });
                setLines(hydratedLines);
                setDefaultDimensions(getCommonManualDimensionValues(hydratedLines.map(line => line.dimensions)));
                if (books.length > 0) {
                    setAccountingBooks(books);
                }
                setAccounts(allAccounts.filter((account) => {
                    const canPost = (account as any).isPostingAllowed ?? account.allowDirectPosting ?? true;
                    return account.status === 'Active' && canPost;
                }));
            } catch (error: any) {
                setCurrencyReferenceError(error?.message || 'Failed to load Finance currency settings.');
                toast({ title: 'Error', description: error?.message || 'Failed to load journal entry for editing', variant: 'destructive' });
            } finally {
                setLoading(false);
            }
        };

        loadData();
    }, [id, toast]);

    const totalDebit = lines.reduce((sum, line) => sum + (line.debit || 0), 0);
    const totalCredit = lines.reduce((sum, line) => sum + (line.credit || 0), 0);
    const isBalanced = Math.abs(totalDebit - totalCredit) < 0.01;
    const formatAmount = (amount: number) =>
        amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const isRetiredOpeningBalance = header.journalType === 'Opening Balance';
    const targetAccountingBooks = useMemo(
        () => getPostingTargetBooks(accountingBooks, header.bookClassification),
        [accountingBooks, header.bookClassification]
    );
    const selectedBookName = getAccountingBookName(accountingBooks, header.bookClassification);
    const targetBookLabel = selectedBookName;
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
                    : { id: line.id, index: index + 1, accountLabel: `${account.accountNumber || account.accountCode} - ${account.accountName}` };
            })
            .filter((v): v is { id: string; index: number; accountLabel: string } => v !== null);
    }, [accounts, header.bookClassification, lines, targetAccountingBooks]);
    const fxBlockingMessage = useMemo(() => lines
        .filter(line => line.accountId && (line.debit > 0 || line.credit > 0 || line.foreignDebit || line.foreignCredit))
        .map(line => getManualJournalFxBlocker(line, functionalCurrency))
        .find((message): message is string => Boolean(message)) ?? null,
    [functionalCurrency, lines]);

    const handleAddLine = () => {
        setLines([
            ...lines,
            {
                id: crypto.randomUUID(),
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
            setLines(lines.filter(line => line.id !== id));
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
                rateError: `${currency} is not effective for account ${account.accountNumber || account.accountCode} on ${effectiveDate}.`,
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
                throw new Error(`Account ${account.accountNumber || account.accountCode} has no active permitted transaction currency.`);
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

    const buildUpdateDto = (): Partial<CreateJournalEntryDto> => {
        return {
            transactionDate: header.entryDate,
            description: header.description || undefined,
            reference: header.referenceNumber || undefined,
            bookClassification: header.bookClassification,
            transactions: lines
                .filter(line => line.accountId && (line.debit > 0 || line.credit > 0))
                .map((line, index) => {
                    const transactionCurrency = normalizeCurrencyCode(line.currencyCode);
                    const isForeign = transactionCurrency !== functionalCurrency;
                    const exchangeRate = typeof line.exchangeRate === 'number' ? line.exchangeRate : 1;
                    const transactionType = line.debit > 0 ? 'Debit' : 'Credit';
                    const amount = transactionType === 'Debit' ? line.debit : line.credit;
                    const foreignAmount = transactionType === 'Debit' ? line.foreignDebit : line.foreignCredit;
                    const transaction: CreateAccountTransactionDto = {
                        accountId: line.accountId,
                        amount,
                        transactionType,
                        description: line.description || undefined,
                        reference: header.referenceNumber || entry?.journalEntryNumber || 'JE',
                        currencyCode: isForeign ? transactionCurrency : undefined,
                        exchangeRateId: isForeign ? line.exchangeRateId : undefined,
                        exchangeRate: isForeign ? exchangeRate : undefined,
                        foreignAmount: isForeign ? foreignAmount || undefined : undefined,
                        lineNumber: index + 1,
                        dimensions: Object.entries(line.dimensions)
                            .filter(([, valueCode]) => Boolean(valueCode))
                            .map(([dimensionCode, valueCode]) => ({ dimensionCode, valueCode })),
                    };

                    return transaction;
                }),
        };
    };

    const handleSaveDraft = async () => {
        if (!entry) return;
        if (isRetiredOpeningBalance) {
            toast({
                title: 'Legacy opening balance is read-only',
                description: 'Use the controlled Opening Balances workspace for cutover corrections.',
                variant: 'destructive',
            });
            return;
        }

        if (currencyReferenceError || !functionalCurrency) {
            toast({
                title: 'Currency Configuration',
                description: currencyReferenceError || 'Finance currency configuration is unavailable.',
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

        if (invalidLines.length > 0) {
                toast({
                    title: 'Classification Validation',
                    description: `Line ${invalidLines[0].index} account is not classified for ${targetBookLabel}.`,
                    variant: 'destructive',
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

        const validationErrors = validateJournalEntryForm(header, lines, entry.journalEntryNumber);
        if (validationErrors.length > 0) {
            toast({ title: 'Validation', description: validationErrors[0], variant: 'destructive' });
            return;
        }

        try {
            setSaving(true);
            const updated = await financeDataService.updateJournalEntry(entry.id, buildUpdateDto());
            toast({ title: 'Saved', description: `Journal entry ${updated.journalEntryNumber} updated.` });
            router.push(`/finance/journal-entries/${updated.id}`);
        } catch (error: any) {
            toast({ title: 'Error', description: error?.message || 'Failed to update journal entry', variant: 'destructive' });
        } finally {
            setSaving(false);
        }
    };

    if (loading) {
        return (
            <div className="flex min-h-[320px] items-center justify-center">
                <Loader2 className="mr-2 h-5 w-5 animate-spin" /> Loading journal entry...
            </div>
        );
    }

    if (!entry) {
        return <div className="text-muted-foreground">Journal entry not found.</div>;
    }

    return (
        <div className="space-y-6">
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Edit Journal Entry</h1>
                    <p className="text-muted-foreground">{entry.journalEntryNumber}</p>
                </div>
                <div className="flex gap-2">
                    <Button variant="outline" onClick={() => router.back()} disabled={saving}>
                        <ArrowLeft className="mr-2 h-4 w-4" />
                        Cancel
                    </Button>
                    <Button onClick={handleSaveDraft} disabled={isRetiredOpeningBalance || saving || !isBalanced || totalDebit === 0 || Boolean(currencyReferenceError) || Boolean(fxBlockingMessage)}>
                        {saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                        Save Draft
                    </Button>
                </div>
            </div>

            <Breadcrumb>
                <BreadcrumbList>
                    <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem><BreadcrumbLink href="/finance">Finance</BreadcrumbLink></BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem><BreadcrumbLink href="/finance/journal-entries">Journal Entries</BreadcrumbLink></BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem><BreadcrumbLink href={`/finance/journal-entries/${entry.id}`}>{entry.journalEntryNumber}</BreadcrumbLink></BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem><BreadcrumbPage>Edit</BreadcrumbPage></BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {!isBalanced && (
                <Alert variant="destructive">
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Entry is not balanced</AlertTitle>
                    <AlertDescription>
                        Total Debits ({formatAmount(totalDebit)}) must equal Total Credits ({formatAmount(totalCredit)}).
                        Difference: {formatAmount(Math.abs(totalDebit - totalCredit))}
                    </AlertDescription>
                </Alert>
            )}
            {currencyReferenceError && (
                <Alert variant="destructive">
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Currency configuration unavailable</AlertTitle>
                    <AlertDescription>{currencyReferenceError}</AlertDescription>
                </Alert>
            )}
            {fxBlockingMessage && (
                <Alert variant="destructive">
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Exchange rate required</AlertTitle>
                    <AlertDescription>{fxBlockingMessage}</AlertDescription>
                </Alert>
            )}
            {invalidLines.length > 0 && (
                <Alert variant="destructive">
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Book Classification Mismatch</AlertTitle>
                    <AlertDescription>
                        {invalidLines.length} line(s) use account(s) not classified for {targetBookLabel}: {invalidLines.map(line => line.index).join(', ')}.
                    </AlertDescription>
                </Alert>
            )}
            {isRetiredOpeningBalance && (
                <Alert variant="destructive">
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Legacy opening-balance journal is read-only</AlertTitle>
                    <AlertDescription>
                        Manual opening balances are retired. Use the controlled Opening Balances workspace for corrections or new cutover processing.
                    </AlertDescription>
                </Alert>
            )}

            <Card>
                <CardHeader><CardTitle>Header Information</CardTitle></CardHeader>
                <CardContent>
                    <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                        <div className="space-y-2">
                            <Label htmlFor="journalId">Journal ID</Label>
                            <Input id="journalId" value={entry.journalEntryNumber} disabled className="bg-muted font-mono" />
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="entryDate">Date *</Label>
                            <Input id="entryDate" type="date" value={header.entryDate} onChange={(event) => handleEntryDateChange(event.target.value)} required />
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="journalType">Type</Label>
                            <Input id="journalType" value={header.journalType} disabled className="bg-muted" />
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="bookClassification">Book Classification</Label>
                            <Select value={header.bookClassification} onValueChange={(value) => setHeader({ ...header, bookClassification: value })}>
                                <SelectTrigger><SelectValue /></SelectTrigger>
                                <SelectContent>
                                    {accountingBooks.map((book) => (
                                        <SelectItem key={book.code} value={book.code}>{book.name}</SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="space-y-2 md:col-span-2">
                            <Label htmlFor="description">Description *</Label>
                            <Input id="description" value={header.description} onChange={(event) => setHeader({ ...header, description: event.target.value })} required />
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="reference">Reference #</Label>
                            <Input id="reference" value={header.referenceNumber} onChange={(event) => setHeader({ ...header, referenceNumber: event.target.value })} />
                        </div>
                    </div>
                </CardContent>
            </Card>

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
                    />
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
                                            </td>
                                            <td className="p-3">
                                                <Input value={line.description} onChange={(event) => updateLine(line.id, 'description', event.target.value)} placeholder="Line description" />
                                            </td>
                                            <td className="p-3">
                                                <Select value={line.currencyCode} onValueChange={(value) => void handleCurrencyChange(line.id, value)} disabled={!isCurrencyEditable}>
                                                    <SelectTrigger className="w-[80px]"><SelectValue /></SelectTrigger>
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
                                                <Input type="number" min="0" step="0.01" value={line.foreignDebit || ''} onChange={(event) => updateLine(line.id, 'foreignDebit', parseFloat(event.target.value) || 0)} className={`text-right ${isForeign ? 'bg-blue-50' : 'bg-muted'}`} disabled={!isForeign || !!line.foreignCredit} />
                                            </td>
                                            <td className="p-3">
                                                <Input type="number" min="0" step="0.01" value={line.foreignCredit || ''} onChange={(event) => updateLine(line.id, 'foreignCredit', parseFloat(event.target.value) || 0)} className={`text-right ${isForeign ? 'bg-blue-50' : 'bg-muted'}`} disabled={!isForeign || !!line.foreignDebit} />
                                            </td>
                                            <td className="p-3">
                                                <Input type="number" min="0" step="0.01" value={line.debit || ''} onChange={(event) => updateLine(line.id, 'debit', parseFloat(event.target.value) || 0)} className="text-right" disabled={isForeign || line.credit > 0} readOnly={isForeign} />
                                            </td>
                                            <td className="p-3">
                                                <Input type="number" min="0" step="0.01" value={line.credit || ''} onChange={(event) => updateLine(line.id, 'credit', parseFloat(event.target.value) || 0)} className="text-right" disabled={isForeign || line.debit > 0} readOnly={isForeign} />
                                            </td>
                                            <td className="p-3 align-top">
                                                <ManualJournalDimensionCell
                                                    definitions={financeDimensions}
                                                    rules={dimensionRules}
                                                    effectiveDate={header.entryDate}
                                                    lineNumber={lineIndex + 1}
                                                    accountId={line.accountId}
                                                    accountLabel={account ? `${account.accountNumber || account.accountCode} - ${account.accountName}` : undefined}
                                                    values={line.dimensions}
                                                    defaults={defaultDimensions}
                                                    previousValues={lineIndex > 0 ? lines[lineIndex - 1].dimensions : undefined}
                                                    onChange={dimensions => setLines(current => current.map(item =>
                                                        item.id === line.id ? { ...item, dimensions } : item))}
                                                    onApplyToAll={applyDimensionsToAllLines}
                                                />
                                            </td>
                                            <td className="p-3 text-center">
                                                <Button variant="ghost" size="sm" onClick={() => handleRemoveLine(line.id)} disabled={lines.length <= 2} className="text-destructive hover:text-destructive">
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
                                    <td className="p-3 text-right">{formatAmount(totalDebit)}</td>
                                    <td className="p-3 text-right">{formatAmount(totalCredit)}</td>
                                    <td></td>
                                    <td></td>
                                </tr>
                            </tfoot>
                        </table>
                    </div>
                </CardContent>
            </Card>
        </div>
    );
}
