'use client';

import React, { useState, useMemo } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { ArrowLeft, Save, Plus, Trash2, AlertCircle, FileText } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import type { JournalType, Account, JournalEntry } from '@/types/finance';
import { BOOK_CLASSIFICATIONS } from '@/constants/finance';

// MOCK ACCOUNTS
const MOCK_ACCOUNTS: Account[] = [
    { id: 'acc-1', accountCode: '1000', accountName: 'Cash (GHS)', accountType: 'Asset', status: 'Active', isMultiCurrency: false, tenantId: 't1', accountNumber: '1000', isSegmented: false, isIFRSClassified: true, isBaseClassified: true, isLocalClassified: true, allowDirectPosting: true, isControlAccount: false, budgetTrackingEnabled: false, createdAt: '', updatedAt: '', currencyCode: 'GHS' },
    { id: 'acc-1b', accountCode: '1001', accountName: 'Cash (USD)', accountType: 'Asset', status: 'Active', isMultiCurrency: false, tenantId: 't1', accountNumber: '1001', isSegmented: false, isIFRSClassified: true, isBaseClassified: true, isLocalClassified: true, allowDirectPosting: true, isControlAccount: false, budgetTrackingEnabled: false, createdAt: '', updatedAt: '', currencyCode: 'USD' },
    { id: 'acc-1c', accountCode: '1002', accountName: 'Cash (EUR)', accountType: 'Asset', status: 'Active', isMultiCurrency: false, tenantId: 't1', accountNumber: '1002', isSegmented: false, isIFRSClassified: true, isBaseClassified: true, isLocalClassified: true, allowDirectPosting: true, isControlAccount: false, budgetTrackingEnabled: false, createdAt: '', updatedAt: '', currencyCode: 'EUR' },
    { id: 'acc-2', accountCode: '1100', accountName: 'Accounts Receivable', accountType: 'Asset', status: 'Active', isMultiCurrency: true, tenantId: 't1', accountNumber: '1100', isSegmented: false, isIFRSClassified: true, isBaseClassified: true, isLocalClassified: true, allowDirectPosting: true, isControlAccount: true, budgetTrackingEnabled: false, createdAt: '', updatedAt: '', currencyCode: 'GHS' },
    { id: 'acc-3', accountCode: '2000', accountName: 'Accounts Payable', accountType: 'Liability', status: 'Active', isMultiCurrency: true, tenantId: 't1', accountNumber: '2000', isSegmented: false, isIFRSClassified: true, isBaseClassified: true, isLocalClassified: true, allowDirectPosting: true, isControlAccount: true, budgetTrackingEnabled: false, createdAt: '', updatedAt: '', currencyCode: 'GHS' },
    { id: 'acc-4', accountCode: '4000', accountName: 'Sales Revenue', accountType: 'Revenue', status: 'Active', isMultiCurrency: true, tenantId: 't1', accountNumber: '4000', isSegmented: false, isIFRSClassified: true, isBaseClassified: true, isLocalClassified: true, allowDirectPosting: true, isControlAccount: false, budgetTrackingEnabled: true, createdAt: '', updatedAt: '', currencyCode: 'GHS' },
    { id: 'acc-5', accountCode: '5000', accountName: 'Cost of Goods Sold', accountType: 'Expense', status: 'Active', isMultiCurrency: false, tenantId: 't1', accountNumber: '5000', isSegmented: false, isIFRSClassified: true, isBaseClassified: true, isLocalClassified: true, allowDirectPosting: true, isControlAccount: false, budgetTrackingEnabled: true, createdAt: '', updatedAt: '', currencyCode: 'GHS' },
];

// MOCK ENTRY TO EDIT
const MOCK_ENTRY: JournalEntry = {
    id: 'je-2',
    journalEntryNumber: 'JE-2024-002',
    journalType: 'General',
    entryDate: '2024-01-20T00:00:00Z',
    description: 'Consulting Revenue Recognition',
    totalDebitAmount: 12500.00,
    totalCreditAmount: 12500.00,
    isBalanced: true,
    isMultiCurrency: true,
    primaryCurrency: 'USD',
    bookClassification: 'Base',
    fiscalPeriodId: 'fp-1',
    postingStatus: 'Draft',
    requiresApproval: true,
    approvalStatus: 'Pending',
    isReversed: false,
    isRevaluationEntry: false,
    transactions: [
        { id: '1', journalEntryId: 'je-2', lineNumber: 1, accountId: 'acc-2', accountCode: '1100', accountName: 'Accounts Receivable', description: 'Inv #1001', debitAmount: 12500.00, creditAmount: 0, foreignCurrencyAmount: 1000.00, transactionCurrency: 'USD', createdAt: '', updatedAt: '', isRevaluationEntry: false },
        { id: '2', journalEntryId: 'je-2', lineNumber: 2, accountId: 'acc-4', accountCode: '4000', accountName: 'Sales Revenue', description: 'Inv #1001', debitAmount: 0, creditAmount: 12500.00, foreignCurrencyAmount: 1000.00, transactionCurrency: 'USD', createdAt: '', updatedAt: '', isRevaluationEntry: false },
    ],
    createdAt: '2024-01-20T14:00:00Z',
    updatedAt: '2024-01-20T14:00:00Z',
    createdBy: 'user-1',
    updatedBy: 'user-1',
};

interface JournalLine {
    id: string;
    accountId: string;
    description: string;
    currencyCode: string;
    exchangeRate: number;
    debit: number;
    credit: number;
    foreignDebit?: number;
    foreignCredit?: number;
}

const getBookClassificationFlag = (bookClassification: string): keyof Account | null => {
    if (bookClassification === BOOK_CLASSIFICATIONS.IFRS) return 'isIFRSClassified';
    if (bookClassification === BOOK_CLASSIFICATIONS.MANAGEMENT) return 'isManagementClassified';
    if (bookClassification === BOOK_CLASSIFICATIONS.LOCAL) return 'isLocalClassified';
    return null;
};

const isAccountEligibleForBook = (account: Account, bookClassification: string): boolean => {
    const flag = getBookClassificationFlag(bookClassification);
    if (!flag) return true;
    return Boolean(account[flag]);
};

export default function EditJournalEntryPage({ params }: { params: { id: string } }) {
    const router = useRouter();
    const BASE_CURRENCY = 'GHS';

    // Initialize state with mock data
    const [header, setHeader] = useState({
        entryDate: MOCK_ENTRY.entryDate.split('T')[0],
        journalType: MOCK_ENTRY.journalType,
        description: MOCK_ENTRY.description,
        referenceNumber: MOCK_ENTRY.referenceNumber || '',
        defaultCurrency: MOCK_ENTRY.primaryCurrency || 'GHS', // Used as default for new lines
    });

    const [lines, setLines] = useState<JournalLine[]>(
        MOCK_ENTRY.transactions.map(t => {
            // Calculate implied rate if not available
            const impliedRate = (t.debitAmount + t.creditAmount) > 0 && (t.foreignCurrencyAmount ?? 0) > 0
                ? (t.debitAmount + t.creditAmount) / (t.foreignCurrencyAmount ?? 1)
                : 1;

            return {
                id: t.id,
                accountId: t.accountId,
                description: t.description,
                currencyCode: t.transactionCurrency || 'GHS',
                exchangeRate: impliedRate,
                debit: t.debitAmount,
                credit: t.creditAmount,
                foreignDebit: t.debitAmount > 0 ? t.foreignCurrencyAmount : 0,
                foreignCredit: t.creditAmount > 0 ? t.foreignCurrencyAmount : 0,
            };
        })
    );

    // Computed Totals
    const totalDebit = lines.reduce((sum, line) => sum + (line.debit || 0), 0);
    const totalCredit = lines.reduce((sum, line) => sum + (line.credit || 0), 0);
    const isBalanced = Math.abs(totalDebit - totalCredit) < 0.01;
    const invalidLines = useMemo(() => {
        return lines
            .map((line, index) => {
                const account = MOCK_ACCOUNTS.find(a => a.id === line.accountId);
                if (!account) return null;
                const eligible = isAccountEligibleForBook(account, header.bookClassification);
                return eligible
                    ? null
                    : { id: line.id, index: index + 1, accountLabel: `${account.accountCode} - ${account.accountName}` };
            })
            .filter((v): v is { id: string; index: number; accountLabel: string } => v !== null);
    }, [header.bookClassification, lines]);

    const handleAddLine = () => {
        setLines([
            ...lines,
            {
                id: Date.now().toString(),
                accountId: '',
                description: '',
                currencyCode: header.defaultCurrency,
                exchangeRate: header.defaultCurrency === BASE_CURRENCY ? 1 : 12.5,
                debit: 0,
                credit: 0
            },
        ]);
    };

    const handleRemoveLine = (id: string) => {
        if (lines.length > 2) {
            setLines(lines.filter(l => l.id !== id));
        }
    };

    const updateLine = (id: string, field: keyof JournalLine, value: any) => {
        setLines(lines.map(line => {
            if (line.id === id) {
                const updatedLine = { ...line, [field]: value };
                const isForeign = updatedLine.currencyCode !== BASE_CURRENCY;

                // 1. Handle Account Selection
                if (field === 'accountId') {
                    const account = MOCK_ACCOUNTS.find(a => a.id === value);
                    if (account) {
                        if (!isAccountEligibleForBook(account, header.bookClassification)) {
                            return line;
                        }
                        if (account.currencyCode && account.currencyCode !== BASE_CURRENCY) {
                            updatedLine.currencyCode = account.currencyCode;
                            updatedLine.exchangeRate = 12.5;
                        } else if (!account.isMultiCurrency) {
                            updatedLine.currencyCode = BASE_CURRENCY;
                            updatedLine.exchangeRate = 1;
                        }
                    }
                }

                // 2. Handle Currency Change
                if (field === 'currencyCode') {
                    if (value === BASE_CURRENCY) {
                        updatedLine.exchangeRate = 1;
                        updatedLine.foreignDebit = 0;
                        updatedLine.foreignCredit = 0;
                    } else {
                        updatedLine.exchangeRate = 12.5;
                        updatedLine.foreignDebit = 0;
                        updatedLine.foreignCredit = 0;
                        updatedLine.debit = 0;
                        updatedLine.credit = 0;
                    }
                }

                // 3. Handle Exchange Rate Change
                if (field === 'exchangeRate') {
                    if (updatedLine.foreignDebit) updatedLine.debit = updatedLine.foreignDebit * value;
                    if (updatedLine.foreignCredit) updatedLine.credit = updatedLine.foreignCredit * value;
                }

                // 4. Handle Foreign Amount Changes
                if (isForeign) {
                    if (field === 'foreignDebit') {
                        updatedLine.debit = (value || 0) * updatedLine.exchangeRate;
                        updatedLine.foreignCredit = 0;
                        updatedLine.credit = 0;
                    } else if (field === 'foreignCredit') {
                        updatedLine.credit = (value || 0) * updatedLine.exchangeRate;
                        updatedLine.foreignDebit = 0;
                        updatedLine.debit = 0;
                    }
                } else {
                    if (field === 'debit' && value > 0) updatedLine.credit = 0;
                    if (field === 'credit' && value > 0) updatedLine.debit = 0;
                }

                return updatedLine;
            }
            return line;
        }));
    };

    const handleSubmit = (status: 'Draft' | 'Posted') => {
        if (invalidLines.length > 0) {
            alert(`Line ${invalidLines[0].index} account is not classified for ${header.bookClassification}.`);
            return;
        }

        if (status === 'Posted') {
            if (!isBalanced) {
                alert('Journal entry must be balanced to Post.');
                return;
            }
            if (totalDebit === 0) {
                alert('Journal entry cannot be zero.');
                return;
            }
        }

        console.log(`Updating JE ${params.id} (${status}):`, { header, lines, status });
        // Simulate save
        setTimeout(() => {
            router.push(`/finance/journal-entries/${params.id}`);
        }, 500);
    };

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Edit Journal Entry</h1>
                    <p className="text-muted-foreground">{MOCK_ENTRY.journalEntryNumber}</p>
                </div>
                <div className="flex gap-2">
                    <Button variant="outline" onClick={() => router.back()}>
                        <ArrowLeft className="mr-2 h-4 w-4" />
                        Cancel
                    </Button>
                    <Button variant="secondary" onClick={() => handleSubmit('Draft')}>
                        <FileText className="mr-2 h-4 w-4" />
                        Save Draft
                    </Button>
                    <Button onClick={() => handleSubmit('Posted')} disabled={!isBalanced || totalDebit === 0}>
                        <Save className="mr-2 h-4 w-4" />
                        Post Entry
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
                        <BreadcrumbLink href={`/finance/journal-entries/${params.id}`}>{MOCK_ENTRY.journalEntryNumber}</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>Edit</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Validation Alert */}
            {!isBalanced && (
                <Alert variant="destructive">
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Entry is not balanced</AlertTitle>
                    <AlertDescription>
                        Total Debits ({totalDebit.toFixed(2)}) must equal Total Credits ({totalCredit.toFixed(2)}).
                        Difference: {Math.abs(totalDebit - totalCredit).toFixed(2)}
                    </AlertDescription>
                </Alert>
            )}
            {invalidLines.length > 0 && (
                <Alert variant="destructive">
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Book Classification Mismatch</AlertTitle>
                    <AlertDescription>
                        {invalidLines.length} line(s) use account(s) not classified for {header.bookClassification}: {invalidLines.map(l => l.index).join(', ')}.
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
                                value={MOCK_ENTRY.journalEntryNumber}
                                disabled
                                className="bg-muted font-mono"
                            />
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="entryDate">Date *</Label>
                            <Input
                                id="entryDate"
                                type="date"
                                value={header.entryDate}
                                onChange={(e) => setHeader({ ...header, entryDate: e.target.value })}
                                required
                            />
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="journalType">Type *</Label>
                            <Select
                                value={header.journalType}
                                onValueChange={(value: JournalType) => setHeader({ ...header, journalType: value })}
                            >
                                <SelectTrigger>
                                    <SelectValue />
                                </SelectTrigger>
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
                                    <SelectItem value={BOOK_CLASSIFICATIONS.IFRS}>IFRS</SelectItem>
                                    <SelectItem value={BOOK_CLASSIFICATIONS.MANAGEMENT}>Management</SelectItem>
                                    <SelectItem value={BOOK_CLASSIFICATIONS.LOCAL}>Local</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>

                        <div className="space-y-2 md:col-span-2">
                            <Label htmlFor="description">Description *</Label>
                            <Input
                                id="description"
                                value={header.description}
                                onChange={(e) => setHeader({ ...header, description: e.target.value })}
                                required
                            />
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="reference">Reference #</Label>
                            <Input
                                id="reference"
                                value={header.referenceNumber}
                                onChange={(e) => setHeader({ ...header, referenceNumber: e.target.value })}
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
                                {lines.map((line, index) => {
                                    const isForeign = line.currencyCode !== BASE_CURRENCY;
                                    const account = MOCK_ACCOUNTS.find(a => a.id === line.accountId);
                                    const isCurrencyEditable = !account || account.isMultiCurrency;

                                    return (
                                        <tr key={line.id} className="border-b last:border-0">
                                            <td className="p-3">
                                                <Select
                                                    value={line.accountId}
                                                    onValueChange={(value) => updateLine(line.id, 'accountId', value)}
                                                >
                                                    <SelectTrigger>
                                                        <SelectValue placeholder="Select Account" />
                                                    </SelectTrigger>
                                                    <SelectContent>
                                                        {MOCK_ACCOUNTS.map((acc) => {
                                                            const eligible = isAccountEligibleForBook(acc, header.bookClassification);
                                                            return (
                                                                <SelectItem key={acc.id} value={acc.id} disabled={!eligible}>
                                                                    {acc.accountCode} - {acc.accountName}
                                                                    {!eligible ? ` (Not classified for ${header.bookClassification})` : ''}
                                                                </SelectItem>
                                                            );
                                                        })}
                                                    </SelectContent>
                                                </Select>
                                                {line.accountId && (() => {
                                                    const selected = MOCK_ACCOUNTS.find(a => a.id === line.accountId);
                                                    if (!selected || isAccountEligibleForBook(selected, header.bookClassification)) return null;
                                                    return (
                                                        <p className="mt-1 text-xs text-red-600">
                                                            Not classified for {header.bookClassification}.
                                                        </p>
                                                    );
                                                })()}
                                            </td>
                                            <td className="p-3">
                                                <Input
                                                    value={line.description}
                                                    onChange={(e) => updateLine(line.id, 'description', e.target.value)}
                                                    placeholder="Line description"
                                                />
                                            </td>
                                            <td className="p-3">
                                                <Select
                                                    value={line.currencyCode}
                                                    onValueChange={(value) => updateLine(line.id, 'currencyCode', value)}
                                                    disabled={!isCurrencyEditable}
                                                >
                                                    <SelectTrigger className="w-[80px]">
                                                        <SelectValue />
                                                    </SelectTrigger>
                                                    <SelectContent>
                                                        <SelectItem value="GHS">GHS</SelectItem>
                                                        <SelectItem value="USD">USD</SelectItem>
                                                        <SelectItem value="EUR">EUR</SelectItem>
                                                    </SelectContent>
                                                </Select>
                                            </td>
                                            <td className="p-3">
                                                {isForeign && (
                                                    <Input
                                                        type="number"
                                                        step="0.0001"
                                                        value={line.exchangeRate}
                                                        onChange={(e) => updateLine(line.id, 'exchangeRate', parseFloat(e.target.value) || 1)}
                                                        className="text-right w-full"
                                                    />
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
                                    <td colSpan={6} className="p-3 text-right">Totals ({BASE_CURRENCY}):</td>
                                    <td className="p-3 text-right">{totalDebit.toFixed(2)}</td>
                                    <td className="p-3 text-right">{totalCredit.toFixed(2)}</td>
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
