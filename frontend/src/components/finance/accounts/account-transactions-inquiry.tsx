'use client';

import Link from 'next/link';
import React from 'react';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { AlertCircle, Loader2, RefreshCcw } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useAuth } from '@/hooks/use-auth';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { AccountAccountingBook, AccountingBook, AccountTransactionInquiryItem, AccountTransactionInquiryPage } from '@/types/finance';

const PAGE_SIZE = 10;

interface AccountTransactionsInquiryProps {
    accountId: string;
    accountBooks?: AccountAccountingBook[];
}

function formatAmount(amount: number | null | undefined, currencyCode: string | null | undefined) {
    if (amount == null) return '—';
    const code = currencyCode || '—';
    try {
        return `${code} ${new Intl.NumberFormat('en-GH', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(amount)}`;
    } catch {
        return `${code} ${amount.toFixed(2)}`;
    }
}

function formatDate(value: string) {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString('en-GH');
}

function TransactionCurrencyEvidence({ item }: { item: AccountTransactionInquiryItem }) {
    if (!item.transactionCurrencyCode || item.transactionCurrencyCode === item.functionalCurrencyCode) {
        return <span className="text-muted-foreground">Functional only</span>;
    }

    const transactionAmount = item.transactionDebitAmount ?? item.transactionCreditAmount ?? item.foreignAmount;
    return (
        <div className="space-y-1">
            <div>{formatAmount(transactionAmount, item.transactionCurrencyCode)}</div>
            {item.exchangeRate != null ? (
                <div className="text-xs text-muted-foreground">
                    Rate {item.exchangeRate}{item.exchangeRateSource ? ` · ${item.exchangeRateSource}` : ''}
                    {item.exchangeRateDate ? ` · ${formatDate(item.exchangeRateDate)}` : ''}
                </div>
            ) : null}
        </div>
    );
}

export function AccountTransactionsInquiry({ accountId, accountBooks }: AccountTransactionsInquiryProps) {
    const { hasPermission, isLoading: authLoading, error: authError } = useAuth();
    const canRead = hasPermission('Finance.Read');
    const [books, setBooks] = useState<AccountingBook[]>([]);
    const [booksLoading, setBooksLoading] = useState(false);
    const [booksError, setBooksError] = useState<string | null>(null);
    const [selectedBookCode, setSelectedBookCode] = useState('');
    const [page, setPage] = useState(1);
    const [result, setResult] = useState<AccountTransactionInquiryPage | null>(null);
    const [requestLoading, setRequestLoading] = useState(false);
    const [requestError, setRequestError] = useState<string | null>(null);
    const [booksAttempt, setBooksAttempt] = useState(0);
    const [requestAttempt, setRequestAttempt] = useState(0);
    const requestSequence = useRef(0);

    const enabledAssignments = useMemo(
        () => (accountBooks ?? []).filter(mapping => mapping.isEnabled),
        [accountBooks],
    );
    const permittedBooks = useMemo(() => books.filter(book =>
        book.isActive && enabledAssignments.some(mapping =>
            mapping.accountingBookId === book.id && mapping.accountingBookCode === book.code)),
    [books, enabledAssignments]);

    useEffect(() => {
        if (authLoading || authError || !canRead) return;
        let current = true;
        setBooksLoading(true);
        setBooksError(null);
        void financeDataService.getAccountingBooks(false)
            .then(data => {
                if (current) setBooks(data);
            })
            .catch(() => {
                if (current) {
                    setBooks([]);
                    setBooksError('Accounting-book authority could not be loaded.');
                }
            })
            .finally(() => {
                if (current) setBooksLoading(false);
            });
        return () => { current = false; };
    }, [authLoading, authError, canRead, booksAttempt]);

    useEffect(() => {
        if (booksLoading || booksError || permittedBooks.length === 0) return;
        const storageKey = `finance.account-inquiry.book.${accountId}`;
        let remembered = '';
        try { remembered = window.localStorage.getItem(storageKey) || ''; } catch { /* storage is optional */ }
        const rememberedBook = permittedBooks.find(book => book.code === remembered);
        const defaultBooks = permittedBooks.filter(book => book.isDefault);
        const automaticallySelected = rememberedBook
            ?? (permittedBooks.length === 1 ? permittedBooks[0] : undefined)
            ?? (defaultBooks.length === 1 ? defaultBooks[0] : undefined);
        setSelectedBookCode(current =>
            permittedBooks.some(book => book.code === current) ? current : automaticallySelected?.code || '');
    }, [accountId, booksError, booksLoading, permittedBooks]);

    useEffect(() => {
        if (!selectedBookCode || !canRead || authLoading || authError) return;
        const sequence = ++requestSequence.current;
        setRequestLoading(true);
        setRequestError(null);
        setResult(null);
        void financeDataService.getAccountTransactions(accountId, selectedBookCode, page, PAGE_SIZE)
            .then(data => {
                if (requestSequence.current !== sequence) return;
                if (data.accountingBookCode !== selectedBookCode) {
                    setResult(null);
                    setRequestError(`The server returned ${data.accountingBookCode || 'an unidentified book'} instead of the selected ${selectedBookCode} accounting book. Retry the inquiry; if this continues, contact Finance support.`);
                    return;
                }
                setResult(data);
            })
            .catch(() => {
                if (requestSequence.current === sequence) {
                    setRequestError('Posted account transactions could not be loaded.');
                }
            })
            .finally(() => {
                if (requestSequence.current === sequence) setRequestLoading(false);
            });
    }, [accountId, authError, authLoading, canRead, page, requestAttempt, selectedBookCode]);

    const selectBook = useCallback((code: string) => {
        requestSequence.current += 1;
        setSelectedBookCode(code);
        setPage(1);
        setResult(null);
        setRequestError(null);
        try { window.localStorage.setItem(`finance.account-inquiry.book.${accountId}`, code); } catch { /* storage is optional */ }
    }, [accountId]);

    if (authLoading) {
        return <Card aria-label="Recent transactions authorization loading"><CardContent className="flex items-center justify-center py-10"><Loader2 className="mr-2 h-5 w-5 animate-spin" />Checking Finance access…</CardContent></Card>;
    }
    if (authError) {
        return <Alert variant="destructive"><AlertCircle className="h-4 w-4" /><AlertTitle>Finance access could not be checked</AlertTitle><AlertDescription>Refresh the page to retry authentication and permission loading.</AlertDescription></Alert>;
    }
    if (!canRead) {
        return <Alert variant="destructive"><AlertCircle className="h-4 w-4" /><AlertTitle>Permission denied</AlertTitle><AlertDescription>Finance.Read is required to view account transactions.</AlertDescription></Alert>;
    }

    return (
        <Card>
            <CardHeader className="gap-4 sm:flex-row sm:items-end sm:justify-between">
                <div>
                    <CardTitle>Recent Transactions</CardTitle>
                    <CardDescription>Posted entries for one exact accounting book. Amounts are not aggregated across currencies.</CardDescription>
                </div>
                {!booksLoading && !booksError && permittedBooks.length > 0 ? (
                    <div className="min-w-56 space-y-1.5">
                        <Label htmlFor="account-inquiry-book">Accounting book</Label>
                        <select
                            id="account-inquiry-book"
                            aria-label="Accounting book"
                            className="flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm shadow-sm"
                            value={selectedBookCode}
                            onChange={event => selectBook(event.target.value)}
                        >
                            {selectedBookCode ? null : <option value="">Select an exact book…</option>}
                            {permittedBooks.map(book => <option key={book.id} value={book.code}>{book.code} — {book.name}</option>)}
                        </select>
                    </div>
                ) : null}
            </CardHeader>
            <CardContent>
                {booksLoading ? <div className="flex items-center justify-center py-10"><Loader2 className="mr-2 h-5 w-5 animate-spin" />Loading permitted accounting books…</div> : null}
                {booksError ? <Alert variant="destructive"><AlertCircle className="h-4 w-4" /><AlertTitle>Accounting books unavailable</AlertTitle><AlertDescription><p>{booksError}</p><Button className="mt-3" size="sm" variant="outline" onClick={() => setBooksAttempt(value => value + 1)}><RefreshCcw className="mr-2 h-4 w-4" />Retry</Button></AlertDescription></Alert> : null}
                {!booksLoading && !booksError && permittedBooks.length === 0 ? <Alert><AlertCircle className="h-4 w-4" /><AlertTitle>Accounting-book setup required</AlertTitle><AlertDescription>This account has no active, permitted, enabled accounting-book membership. Transactions remain hidden until Finance setup is corrected.</AlertDescription></Alert> : null}
                {!booksLoading && !booksError && permittedBooks.length > 1 && !selectedBookCode ? <Alert><AlertCircle className="h-4 w-4" /><AlertTitle>Select an accounting book</AlertTitle><AlertDescription>More than one book is permitted and no unambiguous default is available. Select the exact book to query.</AlertDescription></Alert> : null}
                {requestLoading ? <div className="flex items-center justify-center py-10"><Loader2 className="mr-2 h-5 w-5 animate-spin" />Loading posted transactions…</div> : null}
                {!requestLoading && requestError ? <Alert variant="destructive"><AlertCircle className="h-4 w-4" /><AlertTitle>Transactions unavailable</AlertTitle><AlertDescription><p>{requestError}</p><Button className="mt-3" size="sm" variant="outline" onClick={() => setRequestAttempt(value => value + 1)}><RefreshCcw className="mr-2 h-4 w-4" />Retry</Button></AlertDescription></Alert> : null}
                {!requestLoading && !requestError && result?.items.length === 0 ? <div className="py-10 text-center"><p className="font-medium">No posted transactions</p><p className="mt-1 text-sm text-muted-foreground">This account has no posted entries in {result.accountingBookCode} — {result.accountingBookName}.</p></div> : null}
                {!requestLoading && !requestError && result && result.items.length > 0 ? (
                    <Table>
                        <TableHeader><TableRow><TableHead>Date</TableHead><TableHead>Journal / reference</TableHead><TableHead>Description</TableHead><TableHead>Dimensions</TableHead><TableHead className="text-right">Debit (functional)</TableHead><TableHead className="text-right">Credit (functional)</TableHead><TableHead>Transaction currency / rate</TableHead><TableHead>Book / source</TableHead></TableRow></TableHeader>
                        <TableBody>{result.items.map(item => (
                            <TableRow key={item.id}>
                                <TableCell className="whitespace-nowrap"><div>{formatDate(item.postingDate || item.transactionDate)}</div><div className="text-xs text-muted-foreground">Txn {formatDate(item.transactionDate)}</div></TableCell>
                                <TableCell><Link className="font-medium text-primary hover:underline" href={`/finance/journal-entries/${item.journalEntryId}`}>{item.journalEntryNumber}</Link><div className="text-xs text-muted-foreground">{item.reference || item.sourceReference || 'No reference'} · line {item.lineNumber}</div></TableCell>
                                <TableCell><div>{item.lineDescription || item.journalDescription || '—'}</div>{item.lineDescription && item.journalDescription && item.lineDescription !== item.journalDescription ? <div className="text-xs text-muted-foreground">{item.journalDescription}</div> : null}</TableCell>
                                <TableCell title={item.dimensions.map(value => `${value.dimensionName}: ${value.valueName}`).join('; ')}>{item.dimensionDisplayValue || (item.dimensions.length ? item.dimensions.map(value => `${value.dimensionCode}: ${value.valueCode}`).join(' · ') : '—')}</TableCell>
                                <TableCell className="whitespace-nowrap text-right font-mono">{item.debitAmount ? formatAmount(item.debitAmount, item.functionalCurrencyCode) : '—'}</TableCell>
                                <TableCell className="whitespace-nowrap text-right font-mono">{item.creditAmount ? formatAmount(item.creditAmount, item.functionalCurrencyCode) : '—'}</TableCell>
                                <TableCell className="whitespace-nowrap"><TransactionCurrencyEvidence item={item} /></TableCell>
                                <TableCell><div>{item.accountingBookCode} — {item.accountingBookName}</div><div className="text-xs text-muted-foreground">{item.originModuleCode || item.sourceModule || item.sourceDocumentType || 'Finance posting'}{item.postingEventId ? ` · event ${item.postingEventId}` : ''}</div></TableCell>
                            </TableRow>
                        ))}</TableBody>
                    </Table>
                ) : null}
            </CardContent>
            {result && result.totalPages > 1 ? <CardFooter className="flex items-center justify-between border-t pt-4"><p className="text-sm text-muted-foreground">Page {result.page} of {result.totalPages} · {result.totalCount} posted transactions</p><div className="flex gap-2"><Button variant="outline" size="sm" disabled={requestLoading || result.page <= 1} onClick={() => setPage(value => Math.max(1, value - 1))}>Previous</Button><Button variant="outline" size="sm" disabled={requestLoading || result.page >= result.totalPages} onClick={() => setPage(value => value + 1)}>Next</Button></div></CardFooter> : null}
        </Card>
    );
}
