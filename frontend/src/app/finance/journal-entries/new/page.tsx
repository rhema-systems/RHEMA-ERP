'use client';

import React, { useState, useEffect, useMemo } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { ArrowLeft, Save, Plus, Trash2, AlertCircle, FileText, Loader2, ChevronsUpDown, Check } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { cn } from '@/lib/utils';
import type { JournalType, Account, CreateJournalEntryDto } from '@/types/finance';
import { financeDataService } from '@/services/finance/finance-data.service';
import { useToast } from '@/hooks/use-toast';
import { mapJournalEntryFormToCreateDto, validateJournalEntryForm } from '@/lib/finance/journal-entry-mapper';

const BOOK_CLASSIFICATIONS = {
    IFRS: 'IFRS',
    MANAGEMENT: 'Management',
    LOCAL: 'Local',
} as const;

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

export default function NewJournalEntryPage() {
    const router = useRouter();
    const { toast } = useToast();
    const BASE_CURRENCY = 'GHS';

    // Accounts from API
    const [accounts, setAccounts] = useState<Account[]>([]);
    const [accountsLoading, setAccountsLoading] = useState(true);

    // Header State
    const [header, setHeader] = useState({
        entryDate: new Date().toISOString().split('T')[0],
        journalType: 'General' as JournalType,
        description: '',
        referenceNumber: '',
        notes: '',
        bookClassification: BOOK_CLASSIFICATIONS.IFRS as string,
    });
    const [journalNumber, setJournalNumber] = useState('Generating...');
    const [saving, setSaving] = useState(false);
    const [openAccountPopover, setOpenAccountPopover] = useState<string | null>(null);
    const [migrationClearingConfigured, setMigrationClearingConfigured] = useState(true);
    const [openingBalanceAutoRoutingEnabled, setOpeningBalanceAutoRoutingEnabled] = useState(true);

    // Load journal number and accounts on mount
    useEffect(() => {
        const loadNumber = async () => {
            try {
                const num = await financeDataService.getNextJournalNumber();
                setJournalNumber(num + '*');
            } catch (e) {
                console.error("Failed to load journal number", e);
                setJournalNumber("Unavailable");
            }
        };

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

        const loadFinanceSettings = async () => {
            try {
                const financeSettings = await financeDataService.getFinanceSettings();
                setMigrationClearingConfigured(Boolean(financeSettings.migrationClearingAccountId));
                setOpeningBalanceAutoRoutingEnabled(financeSettings.openingBalanceAutoRoutingEnabled ?? true);
            } catch (e) {
                console.error("Failed to load finance settings", e);
            }
        };

        loadNumber();
        loadAccounts();
        loadFinanceSettings();
    }, []);

    // Lines State
    const [lines, setLines] = useState<JournalLine[]>([
        { id: '1', accountId: '', description: '', currencyCode: 'GHS', exchangeRate: 1, debit: 0, credit: 0 },
        { id: '2', accountId: '', description: '', currencyCode: 'GHS', exchangeRate: 1, debit: 0, credit: 0 },
    ]);

    // Computed Totals
    const totalDebit = lines.reduce((sum, line) => sum + (line.debit || 0), 0);
    const totalCredit = lines.reduce((sum, line) => sum + (line.credit || 0), 0);
    const isBalanced = Math.abs(totalDebit - totalCredit) < 0.01;
    const allowOpeningBalanceAutoBalance = header.journalType === 'Opening Balance' && openingBalanceAutoRoutingEnabled;
    const invalidLines = useMemo(() => {
        return lines
            .map((line, index) => {
                const account = accounts.find(a => a.id === line.accountId);
                if (!account) return null;
                const eligible = isAccountEligibleForBook(account, header.bookClassification);
                return eligible
                    ? null
                    : { id: line.id, index: index + 1, accountLabel: `${account.accountNumber} - ${account.accountName}` };
            })
            .filter((v): v is { id: string; index: number; accountLabel: string } => v !== null);
    }, [accounts, header.bookClassification, lines]);

    const handleAddLine = () => {
        setLines([
            ...lines,
            {
                id: Date.now().toString(),
                accountId: '',
                description: '',
                currencyCode: BASE_CURRENCY,
                exchangeRate: 1,
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

                if (field === 'accountId') {
                    const account = accounts.find(a => a.id === value);
                    if (account) {
                        if (!isAccountEligibleForBook(account, header.bookClassification)) {
                            return line;
                        }
                        if (account.currencyCode && account.currencyCode !== BASE_CURRENCY) {
                            updatedLine.currencyCode = account.currencyCode;
                            updatedLine.exchangeRate = 12.5; // Default rate � user can adjust
                        } else if (account.isMultiCurrency) {
                            // Multi-currency, user can select currency
                        } else {
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
                        updatedLine.exchangeRate = 12.5;
                        updatedLine.foreignDebit = 0;
                        updatedLine.foreignCredit = 0;
                        updatedLine.debit = 0;
                        updatedLine.credit = 0;
                    }
                }

                if (field === 'exchangeRate') {
                    if (updatedLine.foreignDebit) updatedLine.debit = updatedLine.foreignDebit * value;
                    if (updatedLine.foreignCredit) updatedLine.credit = updatedLine.foreignCredit * value;
                }

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

    const handleSaveDraft = async () => {
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
                description: `Line ${invalidLines[0].index} account is not classified for ${header.bookClassification}.`,
                variant: 'destructive'
            });
            return;
        }

        // Use centralised contract guard for validation and mapping
        const validationErrors = validateJournalEntryForm(header, lines, journalNumber, {
            openingBalanceAutoRoutingEnabled
        });
        if (validationErrors.length > 0) {
            toast({ title: 'Validation', description: validationErrors[0], variant: 'destructive' });
            return;
        }

        const dto = mapJournalEntryFormToCreateDto(header, lines, journalNumber, BASE_CURRENCY);

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
                        disabled={saving || (header.journalType === 'Opening Balance' && openingBalanceAutoRoutingEnabled && !migrationClearingConfigured)}
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
            {header.journalType === 'Opening Balance' && openingBalanceAutoRoutingEnabled && !migrationClearingConfigured && (
                <Alert variant="destructive">
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Migration Clearing Account Not Configured</AlertTitle>
                    <AlertDescription>
                        Opening Balance journals require Migration Clearing Account in Finance Settings.
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
                            <Input id="journalId" value={journalNumber} disabled className="bg-muted font-mono" />
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
                    {accountsLoading ? (
                        <div className="flex items-center justify-center py-8">
                            <Loader2 className="h-6 w-6 animate-spin mr-2 text-muted-foreground" />
                            <span className="text-muted-foreground">Loading chart of accounts...</span>
                        </div>
                    ) : (
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
                                                    <Popover
                                                        open={openAccountPopover === line.id}
                                                        onOpenChange={(open) => setOpenAccountPopover(open ? line.id : null)}
                                                    >
                                                        <PopoverTrigger asChild>
                                                            <Button
                                                                variant="outline"
                                                                role="combobox"
                                                                aria-expanded={openAccountPopover === line.id}
                                                                className="w-full justify-between font-normal text-left h-10 truncate"
                                                            >
                                                                <span className="truncate">
                                                                    {line.accountId
                                                                        ? (() => {
                                                                            const acc = accounts.find(a => a.id === line.accountId);
                                                                            return acc ? `${acc.accountNumber} - ${acc.accountName}` : 'Select Account';
                                                                        })()
                                                                        : 'Select Account'}
                                                                </span>
                                                                <ChevronsUpDown className="ml-1 h-4 w-4 shrink-0 opacity-50" />
                                                            </Button>
                                                        </PopoverTrigger>
                                                        <PopoverContent className="w-[350px] p-0" align="start">
                                                            <Command>
                                                                <CommandInput placeholder="Search accounts..." />
                                                                <CommandList>
                                                                    <CommandEmpty>No account found.</CommandEmpty>
                                                                    <CommandGroup>
                                                                        {accounts.map((acc) => {
                                                                            const eligible = isAccountEligibleForBook(acc, header.bookClassification);
                                                                            return (
                                                                            <CommandItem
                                                                                key={acc.id}
                                                                                value={`${acc.accountNumber} ${acc.accountName}`}
                                                                                disabled={!eligible}
                                                                                onSelect={() => {
                                                                                    if (!eligible) return;
                                                                                    updateLine(line.id, 'accountId', acc.id);
                                                                                    setOpenAccountPopover(null);
                                                                                }}
                                                                                className={!eligible ? 'opacity-50 cursor-not-allowed' : undefined}
                                                                                title={!eligible ? `Not classified for ${header.bookClassification}` : undefined}
                                                                            >
                                                                                <Check
                                                                                    className={cn(
                                                                                        "mr-2 h-4 w-4",
                                                                                        line.accountId === acc.id ? "opacity-100" : "opacity-0"
                                                                                    )}
                                                                                />
                                                                                <span className="truncate">{acc.accountNumber} - {acc.accountName}</span>
                                                                                {!eligible && (
                                                                                    <span className="ml-2 rounded border border-amber-400 bg-amber-50 px-1.5 py-0.5 text-[10px] font-medium text-amber-800">
                                                                                        Not classified for {header.bookClassification}
                                                                                    </span>
                                                                                )}
                                                                            </CommandItem>
                                                                        )})}
                                                                    </CommandGroup>
                                                                </CommandList>
                                                            </Command>
                                                        </PopoverContent>
                                                    </Popover>
                                                    {line.accountId && (() => {
                                                        const selected = accounts.find(a => a.id === line.accountId);
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
                                                        placeholder="Desc"
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
                                                            onChange={(e) => updateLine(line.id, 'exchangeRate', e.target.value === '' ? '' : (parseFloat(e.target.value) || 0))}
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
                                        <td className="p-3 text-right">{totalDebit.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
                                        <td className="p-3 text-right">{totalCredit.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
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
