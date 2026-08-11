'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
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

type BusyAction = 'create' | 'update' | 'validate' | 'submit' | 'post' | 'load' | 'fixed-assets' | null;

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

function periodStartDate(period?: FiscalPeriod) {
    return normalizeDate(period?.startDate);
}

function periodEndDate(period?: FiscalPeriod) {
    return normalizeDate(period?.endDate);
}

function isDateInPeriod(date: string, period?: FiscalPeriod) {
    const start = periodStartDate(period);
    const end = periodEndDate(period);
    return Boolean(date && start && end && date >= start && date <= end);
}

function openingDateForPeriod(period: FiscalPeriod, currentDate: string) {
    if (isDateInPeriod(currentDate, period)) {
        return currentDate;
    }

    // Opening balances are beginning balances for the selected accounting period.
    // Defaulting to period end would make them behave like period-close movements.
    return periodStartDate(period) || currentDate;
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
    const router = useRouter();
    const searchParams = useSearchParams();
    const requestedBatchId = searchParams.get('batchId');
    const suppressRequestedLoadRef = useRef(false);
    const [busyAction, setBusyAction] = useState<BusyAction>(null);
    const [activeTab, setActiveTab] = useState('gl');
    const [openAccountLineId, setOpenAccountLineId] = useState<string | null>(null);
    const [currentBatch, setCurrentBatch] = useState<OpeningBalanceBatch | null>(null);
    const [isDirty, setIsDirty] = useState(false);
    const [validation, setValidation] = useState<OpeningBalanceValidationResult | null>(null);
    const [selectedFixedAssetIds, setSelectedFixedAssetIds] = useState<string[]>([]);
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

    const batchesQuery = useQuery({
        queryKey: ['opening-balance-batches'],
        queryFn: () => financeDataService.getOpeningBalanceBatches(),
    });

    const subledgerReadinessQuery = useQuery({
        queryKey: ['opening-balance-subledger-readiness'],
        queryFn: () => financeDataService.getSubledgerOpeningBalanceReadiness(),
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

    const periodOptions = useMemo(() => {
        if (!currentBatch || openPeriods.some(period => period.id === currentBatch.fiscalPeriodId)) {
            return openPeriods;
        }

        const savedPeriod = fiscalPeriods.find(period => period.id === currentBatch.fiscalPeriodId);
        return savedPeriod ? [savedPeriod, ...openPeriods] : openPeriods;
    }, [currentBatch, fiscalPeriods, openPeriods]);

    const accounts = useMemo(() => {
        return (accountsQuery.data ?? [])
            .filter(isPostingAccount)
            .filter(account => isAccountEligibleForBook(account, header.bookClassification))
            .sort((a, b) => `${a.accountNumber || a.accountCode}`.localeCompare(`${b.accountNumber || b.accountCode}`));
    }, [accountsQuery.data, header.bookClassification]);

    useEffect(() => {
        if (currentBatch || openPeriods.length === 0) {
            return;
        }

        setHeader(current => {
            const selected = openPeriods.find(period => period.id === current.fiscalPeriodId);
            const preferred = selected
                ?? openPeriods.find(period => isDateInPeriod(current.openingDate, period))
                ?? openPeriods[0];
            const openingDate = openingDateForPeriod(preferred, current.openingDate);

            if (current.fiscalPeriodId === preferred.id && current.openingDate === openingDate) {
                return current;
            }

            return {
                ...current,
                fiscalPeriodId: preferred.id,
                openingDate,
            };
        });
    }, [currentBatch, openPeriods]);

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
    const selectedPeriodStart = periodStartDate(selectedPeriod);
    const selectedPeriodEnd = periodEndDate(selectedPeriod);
    const migrationClearingConfigured = Boolean(settingsQuery.data?.migrationClearingAccountId);

    const clientErrors = useMemo(() => {
        const errors: string[] = [];
        const populatedLines = lines.filter(line => line.accountId || toAmount(line.debitAmount) > 0 || toAmount(line.creditAmount) > 0);
        if (!header.openingDate) errors.push('Opening date is required.');
        if (!header.fiscalPeriodId) errors.push('Fiscal period is required.');
        // OpeningBalanceBatch posts one controlled cutover snapshot, so all GL lines inherit
        // the header posting date and period. Keep those aligned before calling the API.
        if (header.openingDate && selectedPeriod && !isDateInPeriod(header.openingDate, selectedPeriod)) {
            errors.push(`Opening date must fall within ${selectedPeriod.periodCode} (${selectedPeriodStart} to ${selectedPeriodEnd}).`);
        }
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
    }, [header, isBalanced, lines, selectedPeriod, selectedPeriodEnd, selectedPeriodStart]);

    const updateLine = (id: string, patch: Partial<OpeningLine>) => {
        setValidation(null);
        setIsDirty(true);
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
        setIsDirty(true);
        setLines(current => [...current, newLine()]);
    };

    const removeLine = (id: string) => {
        setIsDirty(true);
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

    const applyBatchToForm = useCallback((batch: OpeningBalanceBatch) => {
        setCurrentBatch(batch);
        setValidation(null);
        setIsDirty(false);
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
    }, []);

    const handleCreateBatch = async () => {
        if (clientErrors.length > 0) {
            toast({ title: 'Validation', description: clientErrors[0], variant: 'destructive' });
            return;
        }

        try {
            setBusyAction('create');
            const created = await financeDataService.createOpeningBalanceBatch(buildPayload());
            applyBatchToForm(created);
            router.replace(`/finance/opening-balances?batchId=${created.id}`, { scroll: false });
            await Promise.all([diagnosticsQuery.refetch(), batchesQuery.refetch()]);
            toast({ title: 'Opening batch created', description: created.batchNumber });
        } catch (error: any) {
            toast({ title: 'Create failed', description: error?.message || 'Unable to create opening balance batch.', variant: 'destructive' });
        } finally {
            setBusyAction(null);
        }
    };

    const handleCreateFixedAssetBatch = async () => {
        if (!header.openingDate || !header.fiscalPeriodId) {
            toast({ title: 'Opening period required', description: 'Select the opening date and fiscal period on the GL Batch tab first.', variant: 'destructive' });
            return;
        }
        if (selectedFixedAssetIds.length === 0) {
            toast({ title: 'Select assets', description: 'Select at least one unposted fixed-asset opening row.', variant: 'destructive' });
            return;
        }

        try {
            setBusyAction('fixed-assets');
            // The backend derives every GL account and amount from the imported register. The UI
            // sends only the explicit asset selection and cutover header so operators cannot make
            // the asset register and opening journal disagree by editing generated lines.
            const created = await financeDataService.createFixedAssetOpeningBalanceBatch({
                sourceReference: header.sourceReference.trim() || undefined,
                description: `Fixed-asset ${header.bookClassification} opening balances`,
                openingDate: header.openingDate,
                fiscalPeriodId: header.fiscalPeriodId,
                bookClassification: header.bookClassification,
                fixedAssetIds: selectedFixedAssetIds,
            });
            applyBatchToForm(created);
            setSelectedFixedAssetIds([]);
            setActiveTab('gl');
            router.replace(`/finance/opening-balances?batchId=${created.id}`, { scroll: false });
            await Promise.all([batchesQuery.refetch(), diagnosticsQuery.refetch(), subledgerReadinessQuery.refetch()]);
            toast({ title: 'Fixed-asset opening batch prepared', description: 'Validate and submit the generated batch for approval.' });
        } catch (error: any) {
            toast({ title: 'Preparation failed', description: error?.message || 'Unable to prepare the fixed-asset opening batch.', variant: 'destructive' });
        } finally {
            setBusyAction(null);
        }
    };

    const handleUpdateBatch = async () => {
        if (!currentBatch || clientErrors.length > 0) {
            if (clientErrors.length > 0) {
                toast({ title: 'Validation', description: clientErrors[0], variant: 'destructive' });
            }
            return;
        }

        try {
            setBusyAction('update');
            const updated = await financeDataService.updateOpeningBalanceBatch(currentBatch.id, buildPayload());
            applyBatchToForm(updated);
            await Promise.all([diagnosticsQuery.refetch(), batchesQuery.refetch()]);
            toast({ title: 'Opening batch saved', description: updated.batchNumber });
        } catch (error: any) {
            toast({ title: 'Save failed', description: error?.message || 'Unable to update opening balance batch.', variant: 'destructive' });
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
            setIsDirty(false);
            await Promise.all([diagnosticsQuery.refetch(), batchesQuery.refetch()]);
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
            applyBatchToForm(submitted);
            await Promise.all([diagnosticsQuery.refetch(), batchesQuery.refetch()]);
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
            applyBatchToForm(posted);
            await Promise.all([diagnosticsQuery.refetch(), batchesQuery.refetch(), subledgerReadinessQuery.refetch()]);
            toast({ title: 'Opening batch posted', description: posted.batchNumber });
        } catch (error: any) {
            toast({ title: 'Post failed', description: error?.message || 'Unable to post opening balance batch.', variant: 'destructive' });
        } finally {
            setBusyAction(null);
        }
    };

    const loadBatch = useCallback(async (batchId: string, updateUrl = true) => {
        try {
            setBusyAction('load');
            const batch = await financeDataService.getOpeningBalanceBatch(batchId);
            applyBatchToForm(batch);
            setActiveTab('gl');
            if (updateUrl) {
                router.replace(`/finance/opening-balances?batchId=${batch.id}`, { scroll: false });
            }
        } catch (error: any) {
            toast({ title: 'Load failed', description: error?.message || 'Unable to load opening balance batch.', variant: 'destructive' });
        } finally {
            setBusyAction(null);
        }
    }, [applyBatchToForm, router, toast]);

    useEffect(() => {
        if (!requestedBatchId) {
            suppressRequestedLoadRef.current = false;
            return;
        }

        if (suppressRequestedLoadRef.current) {
            return;
        }

        if (requestedBatchId && currentBatch?.id !== requestedBatchId) {
            void loadBatch(requestedBatchId, false);
        }
    }, [currentBatch?.id, loadBatch, requestedBatchId]);

    const handleNewBatch = () => {
        const today = todayInputValue();
        const period = openPeriods.find(item => isDateInPeriod(today, item)) ?? openPeriods[0];
        suppressRequestedLoadRef.current = true;
        setCurrentBatch(null);
        setValidation(null);
        setComment('');
        setIsDirty(false);
        setHeader({
            batchNumber: '',
            sourceReference: '',
            description: '',
            openingDate: period ? openingDateForPeriod(period, today) : today,
            fiscalPeriodId: period?.id || '',
            bookClassification: accountingBooks[0]?.code ?? 'IFRS',
        });
        setLines([newLine(), newLine()]);
        setActiveTab('gl');
        router.replace('/finance/opening-balances', { scroll: false });
    };

    const handleRefresh = async () => {
        const refreshes: Promise<unknown>[] = [batchesQuery.refetch(), diagnosticsQuery.refetch()];
        if (currentBatch) {
            refreshes.push(loadBatch(currentBatch.id, false));
        }
        await Promise.all(refreshes);
    };

    const canSubmit = Boolean(currentBatch) && ['Draft', 'Validated', 'Failed'].includes(currentBatch?.status || '');
    const canPost = Boolean(currentBatch) && ['Approved', 'Failed'].includes(currentBatch?.status || '');
    const canEditCurrent = Boolean(currentBatch) && ['Draft', 'Validated', 'Failed'].includes(currentBatch?.status || '');
    const formReadOnly = Boolean(currentBatch) && !canEditCurrent;
    const selectedBookName = getAccountingBookName(accountingBooks, header.bookClassification);

    return (
        <div className="space-y-6 p-8 max-w-[1600px] mx-auto">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Opening Balances</h1>
                    <p className="text-muted-foreground mt-2">Controlled GL opening batches and subledger opening documents.</p>
                </div>
                <div className="flex flex-wrap gap-2">
                    <Button variant="outline" onClick={handleRefresh} disabled={batchesQuery.isFetching || diagnosticsQuery.isFetching || busyAction !== null}>
                        {(batchesQuery.isFetching || diagnosticsQuery.isFetching) ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <RefreshCw className="mr-2 h-4 w-4" />}
                        Refresh
                    </Button>
                    <Button variant="outline" onClick={handleNewBatch} disabled={busyAction !== null}>
                        <Plus className="mr-2 h-4 w-4" />
                        New Batch
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

            <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-6">
                <TabsList>
                    <TabsTrigger value="gl">GL Batch</TabsTrigger>
                    <TabsTrigger value="subledger">Subledger</TabsTrigger>
                    <TabsTrigger value="diagnostics">Diagnostics</TabsTrigger>
                </TabsList>

                <TabsContent value="gl" className="space-y-6">
                    <Card>
                        <CardHeader className="flex flex-row items-center justify-between">
                            <CardTitle>Saved GL Batches</CardTitle>
                            <Badge variant="outline">{batchesQuery.data?.length ?? 0}</Badge>
                        </CardHeader>
                        <CardContent>
                            {batchesQuery.isLoading ? (
                                <div className="space-y-3">
                                    <Skeleton className="h-12 w-full" />
                                    <Skeleton className="h-12 w-full" />
                                </div>
                            ) : (batchesQuery.data ?? []).length === 0 ? (
                                <div className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
                                    No saved GL opening-balance batches yet.
                                </div>
                            ) : (
                                <div className="overflow-x-auto rounded-md border">
                                    <table className="w-full min-w-[760px]">
                                        <thead>
                                            <tr className="border-b bg-muted/50">
                                                <th className="p-3 text-left font-medium">Batch</th>
                                                <th className="p-3 text-left font-medium">Opening Date</th>
                                                <th className="p-3 text-left font-medium">Period / Book</th>
                                                <th className="p-3 text-right font-medium">Debit</th>
                                                <th className="p-3 text-left font-medium">Status</th>
                                                <th className="p-3 w-[96px]"></th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {(batchesQuery.data ?? []).map(batch => (
                                                <tr key={batch.id} className={cn('border-b last:border-0', currentBatch?.id === batch.id && 'bg-primary/5')}>
                                                    <td className="p-3">
                                                        <div className="font-medium">{batch.batchNumber}</div>
                                                        <div className="max-w-[280px] truncate text-xs text-muted-foreground">{batch.description || batch.sourceReference || 'No description'}</div>
                                                    </td>
                                                    <td className="p-3">{normalizeDate(batch.openingDate)}</td>
                                                    <td className="p-3">{batch.fiscalPeriodCode} / {batch.bookClassification}</td>
                                                    <td className="p-3 text-right font-medium">{formatAmount(batch.totalDebit)}</td>
                                                    <td className="p-3"><Badge variant={statusVariant(batch.status)}>{batch.status}</Badge></td>
                                                    <td className="p-3 text-right">
                                                        <Button size="sm" variant={currentBatch?.id === batch.id ? 'secondary' : 'outline'} onClick={() => loadBatch(batch.id)} disabled={busyAction !== null}>
                                                            {currentBatch?.id === batch.id ? 'Loaded' : 'Open'}
                                                        </Button>
                                                    </td>
                                                </tr>
                                            ))}
                                        </tbody>
                                    </table>
                                </div>
                            )}
                        </CardContent>
                    </Card>

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
                                                    setIsDirty(true);
                                                    setHeader(current => ({ ...current, batchNumber: event.target.value }));
                                                }}
                                                placeholder="Auto-generated"
                                                disabled={Boolean(currentBatch) || formReadOnly}
                                            />
                                        </div>
                                        <div className="space-y-2">
                                            <Label htmlFor="openingDate">Opening Date</Label>
                                            <Input
                                                id="openingDate"
                                                type="date"
                                                value={header.openingDate}
                                                min={selectedPeriodStart || undefined}
                                                max={selectedPeriodEnd || undefined}
                                                onChange={(event) => {
                                                    setIsDirty(true);
                                                    setHeader(current => ({ ...current, openingDate: event.target.value }));
                                                }}
                                                disabled={formReadOnly}
                                            />
                                            {selectedPeriodStart && selectedPeriodEnd && (
                                                <p className="text-xs text-muted-foreground">
                                                    {selectedPeriodStart} to {selectedPeriodEnd}
                                                </p>
                                            )}
                                        </div>
                                        <div className="space-y-2">
                                            <Label>Fiscal Period</Label>
                                            <Select
                                                value={header.fiscalPeriodId}
                                                onValueChange={(value) => {
                                                    const period = periodOptions.find(item => item.id === value);
                                                    setIsDirty(true);
                                                    setHeader(current => ({
                                                        ...current,
                                                        fiscalPeriodId: value,
                                                        openingDate: period ? openingDateForPeriod(period, current.openingDate) : current.openingDate,
                                                    }));
                                                }}
                                                disabled={formReadOnly}
                                            >
                                                <SelectTrigger>
                                                    <SelectValue placeholder={periodsQuery.isLoading ? 'Loading periods...' : 'Select period'} />
                                                </SelectTrigger>
                                                <SelectContent>
                                                    {periodOptions.map(period => (
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
                                                    setIsDirty(true);
                                                    setHeader(current => ({ ...current, bookClassification: value }));
                                                    setLines(current => current.map(line => {
                                                        const account = accountsQuery.data?.find(item => item.id === line.accountId);
                                                        return account && !isAccountEligibleForBook(account, value) ? { ...line, accountId: '' } : line;
                                                    }));
                                                }}
                                                disabled={formReadOnly}
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
                                                    setIsDirty(true);
                                                    setHeader(current => ({ ...current, sourceReference: event.target.value }));
                                                }}
                                                placeholder="Migration file or working paper"
                                                disabled={formReadOnly}
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
                                                    setIsDirty(true);
                                                    setHeader(current => ({ ...current, description: event.target.value }));
                                                }}
                                                rows={2}
                                                placeholder="Opening trial balance at migration cutover"
                                                disabled={formReadOnly}
                                            />
                                        </div>
                                    </div>
                                </CardContent>
                            </Card>

                            <Card>
                                <CardHeader className="flex flex-row items-center justify-between">
                                    <CardTitle>GL Opening Lines</CardTitle>
                                    <Button variant="outline" size="sm" onClick={addLine} disabled={formReadOnly}>
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
                                                                                disabled={formReadOnly}
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
                                                                        disabled={formReadOnly}
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
                                                                        disabled={formReadOnly}
                                                                    />
                                                                </td>
                                                                <td className="p-3">
                                                                    <Input
                                                                        value={line.sourceReference}
                                                                        onChange={(event) => updateLine(line.id, { sourceReference: event.target.value })}
                                                                        placeholder="Optional"
                                                                        disabled={formReadOnly}
                                                                    />
                                                                </td>
                                                                <td className="p-3">
                                                                    <Input
                                                                        value={line.notes}
                                                                        onChange={(event) => updateLine(line.id, { notes: event.target.value })}
                                                                        placeholder="Optional"
                                                                        disabled={formReadOnly}
                                                                    />
                                                                </td>
                                                                <td className="p-3 text-center">
                                                                    <Button variant="ghost" size="icon" onClick={() => removeLine(line.id)} disabled={formReadOnly || lines.length === 1}>
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

                                    {currentBatch && isDirty && (
                                        <Alert>
                                            <AlertCircle className="h-4 w-4" />
                                            <AlertTitle>Unsaved changes</AlertTitle>
                                            <AlertDescription>Save this batch before validating or submitting it.</AlertDescription>
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
                                        <Button
                                            onClick={currentBatch ? handleUpdateBatch : handleCreateBatch}
                                            disabled={busyAction !== null || clientErrors.length > 0 || (Boolean(currentBatch) && (!canEditCurrent || !isDirty))}
                                        >
                                            {busyAction === 'create' || busyAction === 'update'
                                                ? <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                                : <Database className="mr-2 h-4 w-4" />}
                                            {currentBatch ? (isDirty ? 'Save Changes' : 'Saved') : 'Create Batch'}
                                        </Button>
                                        <Button variant="outline" onClick={handleValidateBatch} disabled={!currentBatch || isDirty || busyAction !== null || !canEditCurrent}>
                                            {busyAction === 'validate' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <ClipboardCheck className="mr-2 h-4 w-4" />}
                                            Validate
                                        </Button>
                                        <Button variant="outline" onClick={handleSubmitBatch} disabled={!canSubmit || isDirty || busyAction !== null}>
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
                            <CardHeader className="flex flex-row items-center justify-between">
                                <CardTitle>AR Opening Invoices</CardTitle>
                                <Badge variant="outline">
                                    {subledgerReadinessQuery.data?.postedArOpeningInvoiceCount ?? 0}/{subledgerReadinessQuery.data?.arOpeningInvoiceCount ?? 0} posted
                                </Badge>
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
                            <CardHeader className="flex flex-row items-center justify-between">
                                <CardTitle>AP Opening Bills</CardTitle>
                                <Badge variant="outline">
                                    {subledgerReadinessQuery.data?.postedApOpeningInvoiceCount ?? 0}/{subledgerReadinessQuery.data?.apOpeningInvoiceCount ?? 0} posted
                                </Badge>
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

                    <Card>
                        <CardHeader className="flex flex-row items-center justify-between gap-4">
                            <div>
                                <CardTitle>Fixed-Asset Opening Register</CardTitle>
                                <p className="mt-1 text-sm text-muted-foreground">
                                    Prepare an approved GL batch from imported cost and accumulated-depreciation evidence. Accounts and amounts are derived by Finance and cannot be edited manually.
                                </p>
                            </div>
                            <Badge variant="outline">
                                {subledgerReadinessQuery.data?.postedFixedAssetOpeningBookValueCount ?? 0}/{subledgerReadinessQuery.data?.fixedAssetOpeningBookValueCount ?? 0} posted
                            </Badge>
                        </CardHeader>
                        <CardContent className="space-y-4">
                            {(subledgerReadinessQuery.data?.warnings ?? []).map(warning => (
                                <Alert key={warning}>
                                    <AlertCircle className="h-4 w-4" />
                                    <AlertDescription>{warning}</AlertDescription>
                                </Alert>
                            ))}

                            <div className="grid gap-3 sm:grid-cols-3">
                                <div className="rounded-md border p-3">
                                    <div className="text-xs text-muted-foreground">Opening cost</div>
                                    <div className="font-semibold">{formatAmount(subledgerReadinessQuery.data?.fixedAssetOpeningCost ?? 0)}</div>
                                </div>
                                <div className="rounded-md border p-3">
                                    <div className="text-xs text-muted-foreground">Accumulated depreciation</div>
                                    <div className="font-semibold">{formatAmount(subledgerReadinessQuery.data?.fixedAssetOpeningAccumulatedDepreciation ?? 0)}</div>
                                </div>
                                <div className="rounded-md border p-3">
                                    <div className="text-xs text-muted-foreground">Net book value</div>
                                    <div className="font-semibold">{formatAmount(subledgerReadinessQuery.data?.fixedAssetOpeningNetBookValue ?? 0)}</div>
                                </div>
                            </div>

                            <div className="overflow-x-auto rounded-md border">
                                <table className="w-full min-w-[900px]">
                                    <thead>
                                        <tr className="border-b bg-muted/50">
                                            <th className="w-12 p-3"></th>
                                            <th className="p-3 text-left font-medium">Asset</th>
                                            <th className="p-3 text-left font-medium">Book / As Of</th>
                                            <th className="p-3 text-right font-medium">Cost</th>
                                            <th className="p-3 text-right font-medium">Accum. Dep.</th>
                                            <th className="p-3 text-right font-medium">NBV</th>
                                            <th className="p-3 text-left font-medium">Status</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {(subledgerReadinessQuery.data?.fixedAssetCandidates ?? [])
                                            .filter(candidate => candidate.bookClassification === header.bookClassification)
                                            .map(candidate => {
                                                const selectable = !candidate.openingPostedToGl;
                                                return (
                                                    <tr key={candidate.fixedAssetBookValueId} className="border-b last:border-0">
                                                        <td className="p-3 text-center">
                                                            <input
                                                                type="checkbox"
                                                                aria-label={`Select ${candidate.assetCode}`}
                                                                checked={selectedFixedAssetIds.includes(candidate.fixedAssetId)}
                                                                disabled={!selectable || busyAction !== null}
                                                                onChange={(event) => setSelectedFixedAssetIds(current => event.target.checked
                                                                    ? [...new Set([...current, candidate.fixedAssetId])]
                                                                    : current.filter(id => id !== candidate.fixedAssetId))}
                                                            />
                                                        </td>
                                                        <td className="p-3">
                                                            <div className="font-medium">{candidate.assetCode} - {candidate.assetName}</div>
                                                            <div className="text-xs text-muted-foreground">{candidate.categoryCode}</div>
                                                        </td>
                                                        <td className="p-3">{candidate.bookClassification} / {normalizeDate(candidate.openingAsOfDate)}</td>
                                                        <td className="p-3 text-right">{formatAmount(candidate.acquisitionCost)}</td>
                                                        <td className="p-3 text-right">{formatAmount(candidate.accumulatedDepreciation)}</td>
                                                        <td className="p-3 text-right font-medium">{formatAmount(candidate.netBookValue)}</td>
                                                        <td className="p-3"><Badge variant={candidate.openingPostedToGl ? 'default' : 'secondary'}>{candidate.openingPostedToGl ? 'Posted' : 'Ready'}</Badge></td>
                                                    </tr>
                                                );
                                            })}
                                    </tbody>
                                </table>
                            </div>

                            <div className="flex flex-wrap items-center justify-between gap-3">
                                <p className="text-sm text-muted-foreground">
                                    Uses {header.bookClassification}, opening date {header.openingDate || '-'}, and the selected fiscal period from the GL Batch tab.
                                </p>
                                <div className="flex gap-2">
                                    <Button
                                        variant="outline"
                                        onClick={() => setSelectedFixedAssetIds((subledgerReadinessQuery.data?.fixedAssetCandidates ?? [])
                                            .filter(candidate => candidate.bookClassification === header.bookClassification && !candidate.openingPostedToGl)
                                            .map(candidate => candidate.fixedAssetId))}
                                        disabled={busyAction !== null}
                                    >
                                        Select Ready
                                    </Button>
                                    <Button onClick={handleCreateFixedAssetBatch} disabled={busyAction !== null || selectedFixedAssetIds.length === 0}>
                                        {busyAction === 'fixed-assets' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Database className="mr-2 h-4 w-4" />}
                                        Prepare GL Batch
                                    </Button>
                                </div>
                            </div>
                        </CardContent>
                    </Card>
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
