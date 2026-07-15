'use client';

import { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import {
    AlertCircle,
    ArrowRight,
    Check,
    ChevronsUpDown,
    ClipboardCheck,
    Database,
    FileText,
    Loader2,
    Plus,
    RefreshCw,
    Send,
    Trash2,
} from 'lucide-react';
import { useQuery } from '@tanstack/react-query';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Separator } from '@/components/ui/separator';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import { DEFAULT_ACCOUNTING_BOOKS, getAccountingBookName, isAccountEligibleForBook } from '@/lib/finance/accounting-books';
import { cn, formatCurrency } from '@/lib/utils';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { Account, AccountingBook, FiscalPeriod, OpeningBalanceBatch, OpeningBalanceDiagnostic, OpeningBalanceValidationResult } from '@/types/finance';

type OpeningLine = {
    id: string;
    accountId: string;
    debitAmount: number | '';
    creditAmount: number | '';
    sourceReference: string;
    notes: string;
};

type BusyAction = 'create' | 'validate' | 'submit' | 'post' | 'load' | null;

const BASE_CURRENCY = 'GHS';

function todayInputValue() {
    return new Date().toISOString().slice(0, 10);
}

function newLine(): OpeningLine {
    return {
        id: `${Date.now()}-${Math.random().toString(36).slice(2)}`,
        accountId: '',
        debitAmount: '',
        creditAmount: '',
        sourceReference: '',
        notes: '',
    };
}

function toAmount(value: number | '') {
    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
}

function formatAmount(value: number) {
    return formatCurrency(value || 0, BASE_CURRENCY);
}

function isPostingAccount(account: Account) {
    const postingAllowed = account.isPostingAllowed ?? account.allowDirectPosting;
    return account.status === 'Active' && postingAllowed !== false;
}

function normalizeDate(value?: string | null) {
    return value ? value.slice(0, 10) : '';
}

function statusVariant(status?: string): 'default' | 'secondary' | 'destructive' | 'outline' {
    switch ((status || '').toLowerCase()) {
        case 'posted':
        case 'approved':
            return 'default';
        case 'failed':
        case 'rejected':
            return 'destructive';
        case 'pendingapproval':
        case 'validated':
            return 'secondary';
        default:
            return 'outline';
    }
}

export default function OpeningBalancesPage() {
    const { toast } = useToast();
    const [busyAction, setBusyAction] = useState<BusyAction>(null);
    const [openAccountLineId, setOpenAccountLineId] = useState<string | null>(null);
    const [currentBatch, setCurrentBatch] = useState<OpeningBalanceBatch | null>(null);
    const [validation, setValidation] = useState<OpeningBalanceValidationResult | null>(null);
    const [comment, setComment] = useState('');
    const [header, setHeader] = useState({
        batchNumber: '',
        sourceReference: '',
        description: '',
        openingDate: todayInputValue(),
        fiscalPeriodId: '',
        bookClassification: 'IFRS',
    });
    const [lines, setLines] = useState<OpeningLine[]>([newLine(), newLine()]);

    const accountsQuery = useQuery({
        queryKey: ['opening-balance-accounts'],
        queryFn: () => financeDataService.getAccounts({ status: 'Active', pageSize: 1000 }),
    });

    const periodsQuery = useQuery({
        queryKey: ['opening-balance-periods'],
        queryFn: () => financeDataService.getFiscalPeriods(),
    });

    const booksQuery = useQuery({
        queryKey: ['opening-balance-accounting-books'],
        queryFn: () => financeDataService.getAccountingBooks(),
    });

    const settingsQuery = useQuery({
        queryKey: ['opening-balance-finance-settings'],
        queryFn: () => financeDataService.getFinanceSettings(),
    });

    const diagnosticsQuery = useQuery({
        queryKey: ['opening-balance-diagnostics'],
        queryFn: () => financeDataService.getOpeningBalanceDiagnostics(),
    });

    const accountingBooks = useMemo<AccountingBook[]>(() => {
        const books = (booksQuery.data && booksQuery.data.length > 0 ? booksQuery.data : DEFAULT_ACCOUNTING_BOOKS)
            .filter(book => book.isActive !== false && book.allowsPosting !== false);
        return books.length > 0 ? books : DEFAULT_ACCOUNTING_BOOKS;
    }, [booksQuery.data]);

    const fiscalPeriods = periodsQuery.data ?? [];
    const openPeriods = useMemo(() => {
        const open = fiscalPeriods.filter(period => period.isOpen || period.periodStatus === 'Open' || period.status === 'Open');
        return open.length > 0 ? open : fiscalPeriods;
    }, [fiscalPeriods]);

    const accounts = useMemo(() => {
        return (accountsQuery.data ?? [])
            .filter(isPostingAccount)
            .filter(account => isAccountEligibleForBook(account, header.bookClassification))
            .sort((a, b) => `${a.accountNumber || a.accountCode}`.localeCompare(`${b.accountNumber || b.accountCode}`));
    }, [accountsQuery.data, header.bookClassification]);

    useEffect(() => {
        if (!header.fiscalPeriodId && openPeriods.length > 0) {
            const preferred = openPeriods.find(period => {
                const date = header.openingDate;
                return date >= normalizeDate(period.startDate) && date <= normalizeDate(period.endDate);
            }) ?? openPeriods[0];
            setHeader(current => ({ ...current, fiscalPeriodId: preferred.id }));
        }
    }, [header.fiscalPeriodId, header.openingDate, openPeriods]);

    useEffect(() => {
        if (!accountingBooks.some(book => book.code === header.bookClassification)) {
            setHeader(current => ({ ...current, bookClassification: accountingBooks[0]?.code ?? 'IFRS' }));
        }
    }, [accountingBooks, header.bookClassification]);

    const totalDebit = useMemo(() => lines.reduce((sum, line) => sum + toAmount(line.debitAmount), 0), [lines]);
    const totalCredit = useMemo(() => lines.reduce((sum, line) => sum + toAmount(line.creditAmount), 0), [lines]);
    const difference = Math.round((totalDebit - totalCredit) * 100) / 100;
    const isBalanced = Math.abs(difference) < 0.01;
    const selectedPeriod = fiscalPeriods.find(period => period.id === header.fiscalPeriodId);
    const migrationClearingConfigured = Boolean(settingsQuery.data?.migrationClearingAccountId);

    const clientErrors = useMemo(() => {
        const errors: string[] = [];
        const populatedLines = lines.filter(line => line.accountId || toAmount(line.debitAmount) > 0 || toAmount(line.creditAmount) > 0);
        if (!header.openingDate) errors.push('Opening date is required.');
        if (!header.fiscalPeriodId) errors.push('Fiscal period is required.');
        if (!header.bookClassification) errors.push('Book classification is required.');
        if (populatedLines.length === 0) errors.push('At least one opening balance line is required.');
        populatedLines.forEach((line, index) => {
            if (!line.accountId) errors.push(`Line ${index + 1}: account is required.`);
            if (toAmount(line.debitAmount) > 0 && toAmount(line.creditAmount) > 0) {
                errors.push(`Line ${index + 1}: use either debit or credit.`);
            }
            if (toAmount(line.debitAmount) === 0 && toAmount(line.creditAmount) === 0) {
                errors.push(`Line ${index + 1}: amount is required.`);
            }
        });
        if (!isBalanced) errors.push('Opening balance batch must be balanced.');
        return errors;
    }, [header, isBalanced, lines]);

    const updateLine = (id: string, patch: Partial<OpeningLine>) => {
        setValidation(null);
        setLines(current => current.map(line => {
            if (line.id !== id) return line;
            const updated = { ...line, ...patch };
            if (patch.debitAmount !== undefined && toAmount(patch.debitAmount) > 0) {
                updated.creditAmount = '';
            }
            if (patch.creditAmount !== undefined && toAmount(patch.creditAmount) > 0) {
                updated.debitAmount = '';
            }
            return updated;
        }));
    };

    const addLine = () => {
        setLines(current => [...current, newLine()]);
    };

    const removeLine = (id: string) => {
        setLines(current => current.length > 1 ? current.filter(line => line.id !== id) : current);
        setValidation(null);
    };

    const buildPayload = () => ({
        batchNumber: header.batchNumber.trim() || undefined,
        sourceReference: header.sourceReference.trim() || undefined,
        description: header.description.trim() || undefined,
        openingDate: header.openingDate,
        fiscalPeriodId: header.fiscalPeriodId,
        bookClassification: header.bookClassification,
        lines: lines
            .filter(line => line.accountId && (toAmount(line.debitAmount) > 0 || toAmount(line.creditAmount) > 0))
            .map(line => ({
                accountId: line.accountId,
                debitAmount: toAmount(line.debitAmount),
                creditAmount: toAmount(line.creditAmount),
                transactionCurrencyCode: settingsQuery.data?.baseCurrency || BASE_CURRENCY,
                functionalCurrencyCode: settingsQuery.data?.baseCurrency || BASE_CURRENCY,
                sourceReference: line.sourceReference.trim() || undefined,
                notes: line.notes.trim() || undefined,
            })),
    });

    const handleCreateBatch = async () => {
        if (clientErrors.length > 0) {
            toast({ title: 'Validation', description: clientErrors[0], variant: 'destructive' });
            return;
        }

        try {
            setBusyAction('create');
            const created = await financeDataService.createOpeningBalanceBatch(buildPayload());
            setCurrentBatch(created);
            setValidation(null);
            await diagnosticsQuery.refetch();
            toast({ title: 'Opening batch created', description: created.batchNumber });
        } catch (error: any) {
            toast({ title: 'Create failed', description: error?.message || 'Unable to create opening balance batch.', variant: 'destructive' });
        } finally {
            setBusyAction(null);
        }
    };

    const handleValidateBatch = async () => {
        if (!currentBatch) return;
        try {
            setBusyAction('validate');
            const result = await financeDataService.validateOpeningBalanceBatch(currentBatch.id);
            const refreshed = await financeDataService.getOpeningBalanceBatch(currentBatch.id);
            setValidation(result);
            setCurrentBatch(refreshed);
            await diagnosticsQuery.refetch();
            toast({
                title: result.isValid ? 'Validation passed' : 'Validation failed',
                description: result.isValid ? currentBatch.batchNumber : result.errors[0],
                variant: result.isValid ? 'default' : 'destructive',
            });
        } catch (error: any) {
            toast({ title: 'Validation failed', description: error?.message || 'Unable to validate opening balance batch.', variant: 'destructive' });
        } finally {
            setBusyAction(null);
        }
    };

    const handleSubmitBatch = async () => {
        if (!currentBatch) return;
        try {
            setBusyAction('submit');
            const submitted = await financeDataService.submitOpeningBalanceBatch(currentBatch.id, comment || undefined);
            setCurrentBatch(submitted);
            await diagnosticsQuery.refetch();
            toast({ title: 'Opening batch submitted', description: submitted.status });
        } catch (error: any) {
            toast({ title: 'Submit failed', description: error?.message || 'Unable to submit opening balance batch.', variant: 'destructive' });
        } finally {
            setBusyAction(null);
        }
    };

    const handlePostBatch = async () => {
        if (!currentBatch) return;
        try {
            setBusyAction('post');
            const posted = await financeDataService.postOpeningBalanceBatch(currentBatch.id, comment || undefined);
            setCurrentBatch(posted);
            await diagnosticsQuery.refetch();
            toast({ title: 'Opening batch posted', description: posted.batchNumber });
        } catch (error: any) {
            toast({ title: 'Post failed', description: error?.message || 'Unable to post opening balance batch.', variant: 'destructive' });
        } finally {
            setBusyAction(null);
        }
    };

    const loadBatch = async (batchId: string) => {
        try {
            setBusyAction('load');
            const batch = await financeDataService.getOpeningBalanceBatch(batchId);
            setCurrentBatch(batch);
            setValidation(null);
            setHeader({
                batchNumber: batch.batchNumber,
                sourceReference: batch.sourceReference || '',
                description: batch.description || '',
                openingDate: normalizeDate(batch.openingDate),
                fiscalPeriodId: batch.fiscalPeriodId,
                bookClassification: batch.bookClassification,
            });
            setLines(batch.lines.map(line => ({
                id: line.id,
                accountId: line.accountId,
                debitAmount: line.debitAmount || '',
                creditAmount: line.creditAmount || '',
                sourceReference: line.sourceReference || '',
                notes: line.notes || '',
            })));
        } catch (error: any) {
            toast({ title: 'Load failed', description: error?.message || 'Unable to load opening balance batch.', variant: 'destructive' });
        } finally {
            setBusyAction(null);
        }
    };

    const canSubmit = Boolean(currentBatch) && ['Draft', 'Validated', 'Failed'].includes(currentBatch?.status || '');
    const canPost = Boolean(currentBatch) && ['Approved', 'Failed'].includes(currentBatch?.status || '');
    const selectedBookName = getAccountingBookName(accountingBooks, header.bookClassification);

    return (
        <div className="space-y-6 p-8 max-w-[1600px] mx-auto">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Opening Balances</h1>
                    <p className="text-muted-foreground mt-2">Controlled GL opening batches and subledger opening documents.</p>
                </div>
                <div className="flex flex-wrap gap-2">
                    <Button variant="outline" onClick={() => diagnosticsQuery.refetch()} disabled={diagnosticsQuery.isFetching}>
                        {diagnosticsQuery.isFetching ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <RefreshCw className="mr-2 h-4 w-4" />}
                        Refresh
                    </Button>
                    <Button variant="outline" asChild>
                        <Link href="/finance/approvals">
                            <ClipboardCheck className="mr-2 h-4 w-4" />
                            Approvals
                        </Link>
                    </Button>
                </div>
            </div>

            <Breadcrumb>
                <BreadcrumbList>
                    <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem><BreadcrumbLink href="/finance">Finance</BreadcrumbLink></BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem><BreadcrumbPage>Opening Balances</BreadcrumbPage></BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {!migrationClearingConfigured && (
                <Alert>
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Migration clearing is not configured</AlertTitle>
                    <AlertDescription>
                        AR/AP opening documents and some migration adjustments require a Migration Clearing Account in Finance Settings.
                    </AlertDescription>
                </Alert>
            )}

            <Tabs defaultValue="gl" className="space-y-6">
                <TabsList>
                    <TabsTrigger value="gl">GL Batch</TabsTrigger>
                    <TabsTrigger value="subledger">Subledger</TabsTrigger>
                    <TabsTrigger value="diagnostics">Diagnostics</TabsTrigger>
                </TabsList>

                <TabsContent value="gl" className="space-y-6">
                    <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_360px]">
                        <div className="space-y-6">
                            <Card>
                                <CardHeader>
                                    <CardTitle>Batch Header</CardTitle>
                                </CardHeader>
                                <CardContent>
                                    <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                                        <div className="space-y-2">
                                            <Label htmlFor="batchNumber">Batch Number</Label>
                                            <Input
                                                id="batchNumber"
                                                value={header.batchNumber}
                                                onChange={(event) => {
                                                    setCurrentBatch(null);
                                                    setHeader(current => ({ ...current, batchNumber: event.target.value }));
                                                }}
                                                placeholder="Auto-generated"
                                            />
                                        </div>
                                        <div className="space-y-2">
                                            <Label htmlFor="openingDate">Opening Date</Label>
                                            <Input
                                                id="openingDate"
                                                type="date"
                                                value={header.openingDate}
                                                onChange={(event) => {
                                                    setCurrentBatch(null);
                                                    setHeader(current => ({ ...current, openingDate: event.target.value }));
                                                }}
                                            />
                                        </div>
                                        <div className="space-y-2">
                                            <Label>Fiscal Period</Label>
                                            <Select
                                                value={header.fiscalPeriodId}
                                                onValueChange={(value) => {
                                                    setCurrentBatch(null);
                                                    setHeader(current => ({ ...current, fiscalPeriodId: value }));
                                                }}
                                            >
                                                <SelectTrigger>
                                                    <SelectValue placeholder={periodsQuery.isLoading ? 'Loading periods...' : 'Select period'} />
                                                </SelectTrigger>
                                                <SelectContent>
                                                    {openPeriods.map(period => (
                                                        <SelectItem key={period.id} value={period.id}>
                                                            {period.periodCode} - {period.periodName}
                                                        </SelectItem>
                                                    ))}
                                                </SelectContent>
                                            </Select>
                                        </div>
                                        <div className="space-y-2">
                                            <Label>Book</Label>
                                            <Select
                                                value={header.bookClassification}
                                                onValueChange={(value) => {
                                                    setCurrentBatch(null);
                                                    setHeader(current => ({ ...current, bookClassification: value }));
                                                    setLines(current => current.map(line => {
                                                        const account = accountsQuery.data?.find(item => item.id === line.accountId);
                                                        return account && !isAccountEligibleForBook(account, value) ? { ...line, accountId: '' } : line;
                                                    }));
                                                }}
                                            >
                                                <SelectTrigger><SelectValue /></SelectTrigger>
                                                <SelectContent>
                                                    {accountingBooks.map(book => (
                                                        <SelectItem key={book.code} value={book.code}>{book.name}</SelectItem>
                                                    ))}
                                                </SelectContent>
                                            </Select>
                                        </div>
                                        <div className="space-y-2">
                                            <Label htmlFor="sourceReference">Source Reference</Label>
                                            <Input
                                                id="sourceReference"
                                                value={header.sourceReference}
                                                onChange={(event) => {
                                                    setCurrentBatch(null);
                                                    setHeader(current => ({ ...current, sourceReference: event.target.value }));
                                                }}
                                                placeholder="Migration file or working paper"
                                            />
                                        </div>
                                        <div className="space-y-2">
                                            <Label>Status</Label>
                                            <div className="h-10 flex items-center">
                                                <Badge variant={statusVariant(currentBatch?.status)}>
                                                    {currentBatch?.status || 'Not Created'}
                                                </Badge>
                                            </div>
                                        </div>
                                        <div className="space-y-2 md:col-span-3">
                                            <Label htmlFor="description">Description</Label>
                                            <Textarea
                                                id="description"
                                                value={header.description}
                                                onChange={(event) => {
                                                    setCurrentBatch(null);
                                                    setHeader(current => ({ ...current, description: event.target.value }));
                                                }}
                                                rows={2}
                                                placeholder="Opening trial balance at migration cutover"
                                            />
                                        </div>
                                    </div>
                                </CardContent>
                            </Card>

                            <Card>
                                <CardHeader className="flex flex-row items-center justify-between">
                                    <CardTitle>GL Opening Lines</CardTitle>
                                    <Button variant="outline" size="sm" onClick={addLine}>
                                        <Plus className="mr-2 h-4 w-4" />
                                        Add Line
                                    </Button>
                                </CardHeader>
                                <CardContent>
                                    {accountsQuery.isLoading ? (
                                        <div className="space-y-3">
                                            <Skeleton className="h-10 w-full" />
                                            <Skeleton className="h-10 w-full" />
                                            <Skeleton className="h-10 w-full" />
                                        </div>
                                    ) : (
                                        <div className="overflow-x-auto rounded-md border">
                                            <table className="w-full min-w-[980px]">
                                                <thead>
                                                    <tr className="bg-muted/50 border-b">
                                                        <th className="p-3 text-left font-medium w-[28%]">Account</th>
                                                        <th className="p-3 text-right font-medium w-[14%]">Debit</th>
                                                        <th className="p-3 text-right font-medium w-[14%]">Credit</th>
                                                        <th className="p-3 text-left font-medium w-[18%]">Reference</th>
                                                        <th className="p-3 text-left font-medium">Notes</th>
                                                        <th className="p-3 w-[48px]"></th>
                                                    </tr>
                                                </thead>
                                                <tbody>
                                                    {lines.map((line, index) => {
                                                        const selectedAccount = accountsQuery.data?.find(account => account.id === line.accountId);
                                                        return (
                                                            <tr key={line.id} className="border-b last:border-0">
                                                                <td className="p-3">
                                                                    <Popover
                                                                        open={openAccountLineId === line.id}
                                                                        onOpenChange={(open) => setOpenAccountLineId(open ? line.id : null)}
                                                                    >
                                                                        <PopoverTrigger asChild>
                                                                            <Button
                                                                                variant="outline"
                                                                                role="combobox"
                                                                                className="w-full justify-between font-normal"
                                                                            >
                                                                                <span className="truncate">
                                                                                    {selectedAccount
                                                                                        ? `${selectedAccount.accountNumber || selectedAccount.accountCode} - ${selectedAccount.accountName}`
                                                                                        : 'Select account'}
                                                                                </span>
                                                                                <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                                                                            </Button>
                                                                        </PopoverTrigger>
                                                                        <PopoverContent className="w-[420px] p-0" align="start">
                                                                            <Command>
                                                                                <CommandInput placeholder="Search accounts..." />
                                                                                <CommandList>
                                                                                    <CommandEmpty>No eligible posting accounts.</CommandEmpty>
                                                                                    <CommandGroup>
                                                                                        {accounts.map(account => (
                                                                                            <CommandItem
                                                                                                key={account.id}
                                                                                                value={`${account.accountNumber} ${account.accountCode} ${account.accountName}`}
                                                                                                onSelect={() => {
                                                                                                    updateLine(line.id, { accountId: account.id });
                                                                                                    setOpenAccountLineId(null);
                                                                                                }}
                                                                                            >
                                                                                                <Check className={cn('mr-2 h-4 w-4', line.accountId === account.id ? 'opacity-100' : 'opacity-0')} />
                                                                                                <div className="min-w-0">
                                                                                                    <div className="truncate font-medium">{account.accountNumber || account.accountCode} - {account.accountName}</div>
                                                                                                    <div className="text-xs text-muted-foreground">{account.accountType} · {account.currencyCode || BASE_CURRENCY}</div>
                                                                                                </div>
                                                                                            </CommandItem>
                                                                                        ))}
                                                                                    </CommandGroup>
                                                                                </CommandList>
                                                                            </Command>
                                                                        </PopoverContent>
                                                                    </Popover>
                                                                </td>
                                                                <td className="p-3">
                                                                    <Input
                                                                        type="number"
                                                                        min="0"
                                                                        step="0.01"
                                                                        value={line.debitAmount}
                                                                        onChange={(event) => updateLine(line.id, { debitAmount: event.target.value === '' ? '' : Number(event.target.value) })}
                                                                        className="text-right"
                                                                        aria-label={`Line ${index + 1} debit`}
                                                                    />
                                                                </td>
                                                                <td className="p-3">
                                                                    <Input
                                                                        type="number"
                                                                        min="0"
                                                                        step="0.01"
                                                                        value={line.creditAmount}
                                                                        onChange={(event) => updateLine(line.id, { creditAmount: event.target.value === '' ? '' : Number(event.target.value) })}
                                                                        className="text-right"
                                                                        aria-label={`Line ${index + 1} credit`}
                                                                    />
                                                                </td>
                                                                <td className="p-3">
                                                                    <Input
                                                                        value={line.sourceReference}
                                                                        onChange={(event) => updateLine(line.id, { sourceReference: event.target.value })}
                                                                        placeholder="Optional"
                                                                    />
                                                                </td>
                                                                <td className="p-3">
                                                                    <Input
                                                                        value={line.notes}
                                                                        onChange={(event) => updateLine(line.id, { notes: event.target.value })}
                                                                        placeholder="Optional"
                                                                    />
                                                                </td>
                                                                <td className="p-3 text-center">
                                                                    <Button variant="ghost" size="icon" onClick={() => removeLine(line.id)} disabled={lines.length === 1}>
                                                                        <Trash2 className="h-4 w-4" />
                                                                    </Button>
                                                                </td>
                                                            </tr>
                                                        );
                                                    })}
                                                </tbody>
                                                <tfoot>
                                                    <tr className="bg-muted/40 border-t">
                                                        <td className="p-3 font-medium">Totals</td>
                                                        <td className="p-3 text-right font-semibold">{formatAmount(totalDebit)}</td>
                                                        <td className="p-3 text-right font-semibold">{formatAmount(totalCredit)}</td>
                                                        <td className="p-3" colSpan={3}>
                                                            <Badge variant={isBalanced ? 'default' : 'destructive'}>
                                                                Difference {formatAmount(Math.abs(difference))}
                                                            </Badge>
                                                        </td>
                                                    </tr>
                                                </tfoot>
                                            </table>
                                        </div>
                                    )}
                                </CardContent>
                            </Card>
                        </div>

                        <div className="space-y-6">
                            <Card>
                                <CardHeader>
                                    <CardTitle>Control Panel</CardTitle>
                                </CardHeader>
                                <CardContent className="space-y-4">
                                    <div className="grid grid-cols-2 gap-3 text-sm">
                                        <div>
                                            <div className="text-muted-foreground">Book</div>
                                            <div className="font-medium">{selectedBookName}</div>
                                        </div>
                                        <div>
                                            <div className="text-muted-foreground">Period</div>
                                            <div className="font-medium">{selectedPeriod?.periodCode || '-'}</div>
                                        </div>
                                        <div>
                                            <div className="text-muted-foreground">Debit</div>
                                            <div className="font-medium">{formatAmount(totalDebit)}</div>
                                        </div>
                                        <div>
                                            <div className="text-muted-foreground">Credit</div>
                                            <div className="font-medium">{formatAmount(totalCredit)}</div>
                                        </div>
                                    </div>

                                    {clientErrors.length > 0 && (
                                        <Alert variant="destructive">
                                            <AlertCircle className="h-4 w-4" />
                                            <AlertTitle>Entry Check</AlertTitle>
                                            <AlertDescription>{clientErrors[0]}</AlertDescription>
                                        </Alert>
                                    )}

                                    {validation && (
                                        <Alert variant={validation.isValid ? 'default' : 'destructive'}>
                                            <AlertCircle className="h-4 w-4" />
                                            <AlertTitle>{validation.isValid ? 'Validation Passed' : 'Validation Failed'}</AlertTitle>
                                            <AlertDescription>
                                                {validation.isValid
                                                    ? `${formatAmount(validation.totalDebit)} debit and ${formatAmount(validation.totalCredit)} credit.`
                                                    : validation.errors[0]}
                                            </AlertDescription>
                                        </Alert>
                                    )}

                                    {currentBatch?.failureReason && (
                                        <Alert variant="destructive">
                                            <AlertCircle className="h-4 w-4" />
                                            <AlertTitle>Failure Reason</AlertTitle>
                                            <AlertDescription>{currentBatch.failureReason}</AlertDescription>
                                        </Alert>
                                    )}

                                    <div className="space-y-2">
                                        <Label htmlFor="comment">Approval / Posting Comment</Label>
                                        <Textarea
                                            id="comment"
                                            value={comment}
                                            onChange={(event) => setComment(event.target.value)}
                                            rows={3}
                                            placeholder="Optional"
                                        />
                                    </div>

                                    <div className="grid grid-cols-1 gap-2">
                                        <Button onClick={handleCreateBatch} disabled={busyAction !== null || clientErrors.length > 0}>
                                            {busyAction === 'create' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Database className="mr-2 h-4 w-4" />}
                                            Create Batch
                                        </Button>
                                        <Button variant="outline" onClick={handleValidateBatch} disabled={!currentBatch || busyAction !== null}>
                                            {busyAction === 'validate' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <ClipboardCheck className="mr-2 h-4 w-4" />}
                                            Validate
                                        </Button>
                                        <Button variant="outline" onClick={handleSubmitBatch} disabled={!canSubmit || busyAction !== null}>
                                            {busyAction === 'submit' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Send className="mr-2 h-4 w-4" />}
                                            Submit
                                        </Button>
                                        <Button onClick={handlePostBatch} disabled={!canPost || busyAction !== null}>
                                            {busyAction === 'post' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <ArrowRight className="mr-2 h-4 w-4" />}
                                            Post
                                        </Button>
                                    </div>
                                </CardContent>
                            </Card>

                            {currentBatch && (
                                <Card>
                                    <CardHeader>
                                        <CardTitle>Current Batch</CardTitle>
                                    </CardHeader>
                                    <CardContent className="space-y-3 text-sm">
                                        <div className="flex items-center justify-between gap-3">
                                            <span className="text-muted-foreground">Batch</span>
                                            <span className="font-medium">{currentBatch.batchNumber}</span>
                                        </div>
                                        <div className="flex items-center justify-between gap-3">
                                            <span className="text-muted-foreground">Status</span>
                                            <Badge variant={statusVariant(currentBatch.status)}>{currentBatch.status}</Badge>
                                        </div>
                                        <div className="flex items-center justify-between gap-3">
                                            <span className="text-muted-foreground">Journal</span>
                                            {currentBatch.journalEntryId ? (
                                                <Link className="font-medium text-primary hover:underline" href={`/finance/journal-entries/${currentBatch.journalEntryId}`}>
                                                    View journal
                                                </Link>
                                            ) : (
                                                <span>-</span>
                                            )}
                                        </div>
                                        <Separator />
                                        <div className="flex items-center justify-between gap-3">
                                            <span className="text-muted-foreground">Validated</span>
                                            <span>{currentBatch.validatedAt ? normalizeDate(currentBatch.validatedAt) : '-'}</span>
                                        </div>
                                        <div className="flex items-center justify-between gap-3">
                                            <span className="text-muted-foreground">Submitted</span>
                                            <span>{currentBatch.submittedAt ? normalizeDate(currentBatch.submittedAt) : '-'}</span>
                                        </div>
                                        <div className="flex items-center justify-between gap-3">
                                            <span className="text-muted-foreground">Posted</span>
                                            <span>{currentBatch.postedAt ? normalizeDate(currentBatch.postedAt) : '-'}</span>
                                        </div>
                                    </CardContent>
                                </Card>
                            )}
                        </div>
                    </div>
                </TabsContent>

                <TabsContent value="subledger" className="space-y-6">
                    <div className="grid gap-6 md:grid-cols-2">
                        <Card>
                            <CardHeader>
                                <CardTitle>AR Opening Invoices</CardTitle>
                            </CardHeader>
                            <CardContent className="space-y-4">
                                <div className="text-sm text-muted-foreground">
                                    Customer-level opening invoices post AR control against migration clearing and remain available for aging, payment allocation, and statements.
                                </div>
                                <div className="flex flex-wrap gap-2">
                                    <Button asChild>
                                        <Link href="/finance/ar/invoices/new?openingBalance=true">
                                            <Plus className="mr-2 h-4 w-4" />
                                            New AR Opening Invoice
                                        </Link>
                                    </Button>
                                    <Button variant="outline" asChild>
                                        <Link href="/finance/ar/invoices?isOpeningBalance=true">
                                            <FileText className="mr-2 h-4 w-4" />
                                            AR Invoices
                                        </Link>
                                    </Button>
                                </div>
                            </CardContent>
                        </Card>

                        <Card>
                            <CardHeader>
                                <CardTitle>AP Opening Bills</CardTitle>
                            </CardHeader>
                            <CardContent className="space-y-4">
                                <div className="text-sm text-muted-foreground">
                                    Supplier-level opening bills post migration clearing against AP control and remain available for aging, payment allocation, and statements.
                                </div>
                                <div className="flex flex-wrap gap-2">
                                    <Button asChild>
                                        <Link href="/finance/ap/invoices/create?openingBalance=true">
                                            <Plus className="mr-2 h-4 w-4" />
                                            New AP Opening Bill
                                        </Link>
                                    </Button>
                                    <Button variant="outline" asChild>
                                        <Link href="/finance/ap/invoices?isOpeningBalance=true">
                                            <FileText className="mr-2 h-4 w-4" />
                                            AP Bills
                                        </Link>
                                    </Button>
                                </div>
                            </CardContent>
                        </Card>
                    </div>
                </TabsContent>

                <TabsContent value="diagnostics" className="space-y-6">
                    <Card>
                        <CardHeader className="flex flex-row items-center justify-between">
                            <CardTitle>Migration Diagnostics</CardTitle>
                            <Button variant="outline" size="sm" onClick={() => diagnosticsQuery.refetch()} disabled={diagnosticsQuery.isFetching}>
                                {diagnosticsQuery.isFetching ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <RefreshCw className="mr-2 h-4 w-4" />}
                                Refresh
                            </Button>
                        </CardHeader>
                        <CardContent>
                            {diagnosticsQuery.isLoading ? (
                                <div className="space-y-3">
                                    <Skeleton className="h-12 w-full" />
                                    <Skeleton className="h-12 w-full" />
                                </div>
                            ) : (diagnosticsQuery.data ?? []).length === 0 ? (
                                <div className="rounded-md border border-dashed p-8 text-center text-muted-foreground">
                                    No opening-balance diagnostics found.
                                </div>
                            ) : (
                                <div className="rounded-md border overflow-hidden">
                                    <table className="w-full">
                                        <thead>
                                            <tr className="bg-muted/50 border-b">
                                                <th className="p-3 text-left font-medium">Severity</th>
                                                <th className="p-3 text-left font-medium">Reference</th>
                                                <th className="p-3 text-left font-medium">Diagnostic</th>
                                                <th className="p-3 text-left font-medium">Message</th>
                                                <th className="p-3 w-[92px]"></th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {(diagnosticsQuery.data ?? []).map((diagnostic: OpeningBalanceDiagnostic, index) => {
                                                const diagnosticBatchId = diagnostic.batchId;
                                                return (
                                                    <tr key={`${diagnostic.diagnosticCode}-${diagnosticBatchId || index}`} className="border-b last:border-0">
                                                        <td className="p-3">
                                                            <Badge variant={diagnostic.severity === 'Error' ? 'destructive' : 'secondary'}>{diagnostic.severity}</Badge>
                                                        </td>
                                                        <td className="p-3 font-medium">{diagnostic.reference || diagnosticBatchId || '-'}</td>
                                                        <td className="p-3">{diagnostic.diagnosticCode}</td>
                                                        <td className="p-3 text-muted-foreground">{diagnostic.message}</td>
                                                        <td className="p-3 text-right">
                                                            {diagnosticBatchId && (
                                                                <Button size="sm" variant="outline" onClick={() => loadBatch(diagnosticBatchId)} disabled={busyAction !== null}>
                                                                    Load
                                                                </Button>
                                                            )}
                                                        </td>
                                                    </tr>
                                                );
                                            })}
                                        </tbody>
                                    </table>
                                </div>
                            )}
                        </CardContent>
                    </Card>
                </TabsContent>
            </Tabs>
        </div>
    );
}
