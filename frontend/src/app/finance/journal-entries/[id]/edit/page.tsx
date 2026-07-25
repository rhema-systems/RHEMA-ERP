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
import type { JournalType, Account, JournalEntry, CreateJournalEntryDto, CreateAccountTransactionDto } from '@/types/finance';
import { financeDataService } from '@/services/finance/finance-data.service';
import { useToast } from '@/hooks/use-toast';
import { validateJournalEntryForm } from '@/lib/finance/journal-entry-mapper';
import {
    ALL_ACTIVE_BOOKS_CODE,
    DEFAULT_ACCOUNTING_BOOKS,
    getAccountingBookName,
    getPostingTargetBooks,
    isAccountEligibleForBook,
    isAllActiveBooksCode,
} from '@/lib/finance/accounting-books';

interface JournalLine {
    id: string;
    accountId: string;
    description: string;
    currencyCode: string;
    exchangeRate: number | '';
    debit: number;
    credit: number;
    foreignDebit?: number;
    foreignCredit?: number;
}

const toDateInputValue = (dateString?: string) => {
    if (!dateString) return new Date().toISOString().split('T')[0];
    return new Date(dateString).toISOString().split('T')[0];
};

const mapEntryToLines = (entry: JournalEntry): JournalLine[] => {
    const transactions = entry.transactions || [];
    if (transactions.length === 0) {
        return [
            { id: '1', accountId: '', description: '', currencyCode: 'GHS', exchangeRate: 1, debit: 0, credit: 0 },
            { id: '2', accountId: '', description: '', currencyCode: 'GHS', exchangeRate: 1, debit: 0, credit: 0 },
        ];
    }

    return transactions.map((transaction, index) => {
        const raw = transaction as any;
        const currencyCode = raw.currencyCode || raw.transactionCurrency || entry.primaryCurrency || 'GHS';
        const exchangeRate = raw.exchangeRate ?? 1;
        const foreignAmount = raw.foreignAmount ?? raw.foreignCurrencyAmount;
        const debit = transaction.transactionType === 'Debit' ? transaction.amount : 0;
        const credit = transaction.transactionType === 'Credit' ? transaction.amount : 0;

        return {
            id: transaction.id || `${index + 1}`,
            accountId: transaction.accountId,
            description: transaction.description || '',
            currencyCode,
            exchangeRate,
            debit,
            credit,
            foreignDebit: transaction.transactionType === 'Debit' ? foreignAmount : 0,
            foreignCredit: transaction.transactionType === 'Credit' ? foreignAmount : 0,
        };
    });
};

export default function EditJournalEntryPage() {
    const router = useRouter();
    const params = useParams();
    const { toast } = useToast();
    const BASE_CURRENCY = 'GHS';
    const id = params.id as string;

    const [entry, setEntry] = useState<JournalEntry | null>(null);
    const [accounts, setAccounts] = useState<Account[]>([]);
    const [accountingBooks, setAccountingBooks] = useState(DEFAULT_ACCOUNTING_BOOKS);
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);

    const [header, setHeader] = useState({
        entryDate: new Date().toISOString().split('T')[0],
        journalType: 'General' as JournalType,
        description: '',
        referenceNumber: '',
        notes: '',
        bookClassification: 'IFRS',
    });

    const [lines, setLines] = useState<JournalLine[]>([
        { id: '1', accountId: '', description: '', currencyCode: BASE_CURRENCY, exchangeRate: 1, debit: 0, credit: 0 },
        { id: '2', accountId: '', description: '', currencyCode: BASE_CURRENCY, exchangeRate: 1, debit: 0, credit: 0 },
    ]);

    useEffect(() => {
        const loadData = async () => {
            try {
                setLoading(true);
                const [journalEntry, allAccounts, books] = await Promise.all([
                    financeDataService.getJournalEntryById(id),
                    financeDataService.getAccounts(),
                    financeDataService.getAccountingBooks().catch(() => DEFAULT_ACCOUNTING_BOOKS),
                ]);

                setEntry(journalEntry);
                setHeader({
                    entryDate: toDateInputValue(journalEntry.entryDate || journalEntry.transactionDate),
                    journalType: (journalEntry.journalType || 'General') as JournalType,
                    description: journalEntry.description || '',
                    referenceNumber: journalEntry.referenceNumber || journalEntry.reference || '',
                    notes: journalEntry.notes || '',
                    bookClassification: journalEntry.bookClassification || 'IFRS',
                });
                setLines(mapEntryToLines(journalEntry));
                if (books.length > 0) {
                    setAccountingBooks(books);
                }
                setAccounts(allAccounts.filter((account) => {
                    const canPost = (account as any).isPostingAllowed ?? account.allowDirectPosting ?? true;
                    return account.status === 'Active' && canPost;
                }));
            } catch (error: any) {
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
                    : { id: line.id, index: index + 1, accountLabel: `${account.accountNumber || account.accountCode} - ${account.accountName}` };
            })
            .filter((v): v is { id: string; index: number; accountLabel: string } => v !== null);
    }, [accounts, header.bookClassification, lines, targetAccountingBooks]);

    const handleAddLine = () => {
        setLines([
            ...lines,
            {
                id: crypto.randomUUID(),
                accountId: '',
                description: '',
                currencyCode: BASE_CURRENCY,
                exchangeRate: 1,
                debit: 0,
                credit: 0,
            },
        ]);
    };

    const handleRemoveLine = (id: string) => {
        if (lines.length > 2) {
            setLines(lines.filter(line => line.id !== id));
        }
    };

    const updateLine = (id: string, field: keyof JournalLine, value: any) => {
        setLines(lines.map(line => {
            if (line.id !== id) return line;

            const updatedLine = { ...line, [field]: value };
            const isForeign = updatedLine.currencyCode !== BASE_CURRENCY;

            if (field === 'accountId') {
                const account = accounts.find(a => a.id === value);
                if (account) {
                    const eligible = targetAccountingBooks.length > 0
                        ? targetAccountingBooks.every(book => isAccountEligibleForBook(account, book.code))
                        : isAccountEligibleForBook(account, header.bookClassification);
                    if (!eligible) return line;
                    if (account.currencyCode && account.currencyCode !== BASE_CURRENCY) {
                        updatedLine.currencyCode = account.currencyCode;
                        updatedLine.exchangeRate = 1;
                    } else if (!account.isMultiCurrency) {
                        updatedLine.currencyCode = BASE_CURRENCY;
                        updatedLine.exchangeRate = 1;
                    }
                }
            }

            if (field === 'currencyCode') {
                if (value === BASE_CURRENCY) {
                    updatedLine.exchangeRate = 1;
                    updatedLine.foreignDebit = 0;
                    updatedLine.foreignCredit = 0;
                } else {
                    updatedLine.exchangeRate = 1;
                    updatedLine.foreignDebit = 0;
                    updatedLine.foreignCredit = 0;
                    updatedLine.debit = 0;
                    updatedLine.credit = 0;
                }
            }

            if (field === 'exchangeRate') {
                const rate = typeof value === 'number' ? value : 1;
                if (updatedLine.foreignDebit) updatedLine.debit = updatedLine.foreignDebit * rate;
                if (updatedLine.foreignCredit) updatedLine.credit = updatedLine.foreignCredit * rate;
            }

            if (isForeign) {
                if (field === 'foreignDebit') {
                    const rate = typeof updatedLine.exchangeRate === 'number' ? updatedLine.exchangeRate : 1;
                    updatedLine.debit = (value || 0) * rate;
                    updatedLine.foreignCredit = 0;
                    updatedLine.credit = 0;
                } else if (field === 'foreignCredit') {
                    const rate = typeof updatedLine.exchangeRate === 'number' ? updatedLine.exchangeRate : 1;
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
                    const isForeign = line.currencyCode !== BASE_CURRENCY;
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
                        currencyCode: isForeign ? line.currencyCode : undefined,
                        exchangeRate: isForeign ? exchangeRate : undefined,
                        foreignAmount: isForeign ? foreignAmount || undefined : undefined,
                        lineNumber: index + 1,
                    };

                    return transaction;
                }),
        };
    };

    const handleSaveDraft = async () => {
        if (!entry) return;

        if (invalidLines.length > 0) {
                toast({
                    title: 'Classification Validation',
                    description: `Line ${invalidLines[0].index} account is not classified for ${targetBookLabel}.`,
                    variant: 'destructive',
                });
            return;
        }

        const validationErrors = validateJournalEntryForm(header, lines, entry.journalEntryNumber, {
            openingBalanceAutoRoutingEnabled: false,
        });
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
                    <Button onClick={handleSaveDraft} disabled={saving || !isBalanced || totalDebit === 0}>
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
            {invalidLines.length > 0 && (
                <Alert variant="destructive">
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Book Classification Mismatch</AlertTitle>
                    <AlertDescription>
                        {invalidLines.length} line(s) use account(s) not classified for {targetBookLabel}: {invalidLines.map(line => line.index).join(', ')}.
                    </AlertDescription>
                </Alert>
            )}
            {header.journalType === 'Opening Balance' && isAllActiveBooks && (
                <Alert>
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>All Active Books Posting</AlertTitle>
                    <AlertDescription>
                        This draft will be duplicated when posted to: {targetBookListText || 'No active posting books configured'}.
                        The active book list is resolved again at posting time.
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
                            <Input id="entryDate" type="date" value={header.entryDate} onChange={(event) => setHeader({ ...header, entryDate: event.target.value })} required />
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
                                    {(header.journalType === 'Opening Balance' || isAllActiveBooks) && (
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
                    <div className="rounded-md border overflow-x-auto">
                        <table className="w-full min-w-[1000px]">
                            <thead>
                                <tr className="border-b bg-muted/50">
                                    <th className="p-3 text-left font-medium w-[20%]">Account</th>
                                    <th className="p-3 text-left font-medium w-[20%]">Description</th>
                                    <th className="p-3 text-left font-medium w-[10%]">Currency</th>
                                    <th className="p-3 text-right font-medium w-[8%]">Ex. Rate</th>
                                    <th className="p-3 text-right font-medium w-[10%]">F. Debit</th>
                                    <th className="p-3 text-right font-medium w-[10%]">F. Credit</th>
                                    <th className="p-3 text-right font-medium w-[10%]">Debit ({BASE_CURRENCY})</th>
                                    <th className="p-3 text-right font-medium w-[10%]">Credit ({BASE_CURRENCY})</th>
                                    <th className="p-3 text-center w-[2%]"></th>
                                </tr>
                            </thead>
                            <tbody>
                                {lines.map((line) => {
                                    const isForeign = line.currencyCode !== BASE_CURRENCY;
                                    const account = accounts.find(a => a.id === line.accountId);
                                    const isCurrencyEditable = !account || account.isMultiCurrency;

                                    return (
                                        <tr key={line.id} className="border-b last:border-0">
                                            <td className="p-3">
                                                <Select value={line.accountId} onValueChange={(value) => updateLine(line.id, 'accountId', value)}>
                                                    <SelectTrigger><SelectValue placeholder="Select Account" /></SelectTrigger>
                                                    <SelectContent>
                                                        {accounts.map((acc) => {
                                                            const eligible = targetAccountingBooks.length > 0
                                                                ? targetAccountingBooks.every(book => isAccountEligibleForBook(acc, book.code))
                                                                : isAccountEligibleForBook(acc, header.bookClassification);
                                                            const code = acc.accountNumber || acc.accountCode;
                                                            return (
                                                                <SelectItem key={acc.id} value={acc.id} disabled={!eligible}>
                                                                    {code} - {acc.accountName}{!eligible ? ` (Not classified for ${targetBookLabel})` : ''}
                                                                </SelectItem>
                                                            );
                                                        })}
                                                    </SelectContent>
                                                </Select>
                                            </td>
                                            <td className="p-3">
                                                <Input value={line.description} onChange={(event) => updateLine(line.id, 'description', event.target.value)} placeholder="Line description" />
                                            </td>
                                            <td className="p-3">
                                                <Select value={line.currencyCode} onValueChange={(value) => updateLine(line.id, 'currencyCode', value)} disabled={!isCurrencyEditable}>
                                                    <SelectTrigger className="w-[80px]"><SelectValue /></SelectTrigger>
                                                    <SelectContent>
                                                        <SelectItem value="GHS">GHS</SelectItem>
                                                        <SelectItem value="USD">USD</SelectItem>
                                                        <SelectItem value="EUR">EUR</SelectItem>
                                                    </SelectContent>
                                                </Select>
                                            </td>
                                            <td className="p-3">
                                                {isForeign && (
                                                    <Input type="number" step="0.0001" value={line.exchangeRate} onChange={(event) => updateLine(line.id, 'exchangeRate', parseFloat(event.target.value) || 1)} className="text-right w-full" />
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
                                    <td colSpan={6} className="p-3 text-right">Totals ({BASE_CURRENCY}):</td>
                                    <td className="p-3 text-right">{formatAmount(totalDebit)}</td>
                                    <td className="p-3 text-right">{formatAmount(totalCredit)}</td>
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
