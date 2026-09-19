'use client';

import { useMemo, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { format } from 'date-fns';
import {
    AlertCircle,
    ArrowLeft,
    Check,
    ChevronsUpDown,
    Download,
    Link2,
    Loader2,
    RefreshCw,
    RotateCcw,
    Scale,
    Upload,
    WandSparkles,
    XCircle,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import {
    Command,
    CommandEmpty,
    CommandGroup,
    CommandInput,
    CommandItem,
    CommandList,
} from '@/components/ui/command';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import { parseBankStatementImportFile } from '@/lib/finance/bank-statement-import';
import { calendarDayDifference, isWithinStatementDateTolerance } from '@/lib/finance/banking-policy';
import { cn, formatCurrency } from '@/lib/utils';
import { financeDataService } from '@/services/finance/finance-data.service';
import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import { SourceDocumentDimensionPanel } from '@/components/finance/dimensions/source-document-dimension-panel';
import { toFinancePostingDimensionValues } from '@/lib/finance/source-document-dimensions';
import {
    BankAccount,
    BankReconciliation,
    BankStatement,
    CashTransactionType,
    CreateReconciliationAdjustmentDto,
    ReconciliationAdjustmentType,
    ReconciliationMatch,
    ReconciliationStatus,
    StartReconciliationDto,
    UnmatchedStatementLine,
    UnmatchedTransaction,
} from '@/types/cash-management';

const OPEN_STATUSES = new Set<ReconciliationStatus>([
    ReconciliationStatus.Pending,
    ReconciliationStatus.InProgress,
    ReconciliationStatus.Completed,
]);

const ADJUSTMENT_LABELS: Record<ReconciliationAdjustmentType, string> = {
    [ReconciliationAdjustmentType.BankCharge]: 'Bank charge',
    [ReconciliationAdjustmentType.BankFee]: 'Bank fee',
    [ReconciliationAdjustmentType.InterestIncome]: 'Interest income',
    [ReconciliationAdjustmentType.AdjustmentReceipt]: 'Adjustment receipt',
    [ReconciliationAdjustmentType.AdjustmentPayment]: 'Adjustment payment',
    [ReconciliationAdjustmentType.CorrectionReceipt]: 'Correction receipt',
    [ReconciliationAdjustmentType.CorrectionPayment]: 'Correction payment',
};

function errorMessage(error: unknown, fallback: string) {
    return error instanceof Error && error.message ? error.message : fallback;
}

function formatDate(value: string) {
    return format(new Date(value), 'dd MMM yyyy');
}

function isDirectionCompatible(transaction: UnmatchedTransaction, line: UnmatchedStatementLine) {
    if (transaction.transactionType === CashTransactionType.Receipt) return line.creditAmount > 0 && line.debitAmount === 0;
    if (transaction.transactionType === CashTransactionType.Payment) return line.debitAmount > 0 && line.creditAmount === 0;
    if (transaction.transactionType === CashTransactionType.Transfer && transaction.transactionNumber.toUpperCase().endsWith('-OUT')) {
        return line.debitAmount > 0 && line.creditAmount === 0;
    }
    if (transaction.transactionType === CashTransactionType.Transfer && transaction.transactionNumber.toUpperCase().endsWith('-IN')) {
        return line.creditAmount > 0 && line.debitAmount === 0;
    }
    return false;
}

export default function BankReconciliationPage() {
    const router = useRouter();
    const searchParams = useSearchParams();
    const { toast } = useToast();
    const queryClient = useQueryClient();
    const [selectedAccountId, setSelectedAccountId] = useState(searchParams.get('account') ?? '');
    const [statementId, setStatementId] = useState('');
    const [statementBalance, setStatementBalance] = useState('');
    const [reconciliationDate, setReconciliationDate] = useState(format(new Date(), 'yyyy-MM-dd'));
    const [notes, setNotes] = useState('');
    const [accountPickerOpen, setAccountPickerOpen] = useState(false);
    const [importDialogOpen, setImportDialogOpen] = useState(false);

    const accountsQuery = useQuery({
        queryKey: ['bank-accounts', 'active'],
        queryFn: () => cashManagementDataService.getActiveBankAccounts(),
    });

    const statementsQuery = useQuery({
        queryKey: ['bank-statements', selectedAccountId],
        queryFn: () => cashManagementDataService.getBankStatements(selectedAccountId),
        enabled: Boolean(selectedAccountId),
    });

    const activeReconciliationQuery = useQuery({
        queryKey: ['active-reconciliation', selectedAccountId],
        queryFn: async () => {
            const reconciliations = await cashManagementDataService.getBankReconciliations(selectedAccountId);
            return reconciliations.find((item) => OPEN_STATUSES.has(item.status)) ?? null;
        },
        enabled: Boolean(selectedAccountId),
    });

    const startMutation = useMutation({
        mutationFn: (dto: StartReconciliationDto) => cashManagementDataService.startReconciliation(dto),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['active-reconciliation', selectedAccountId] });
            queryClient.invalidateQueries({ queryKey: ['bank-account-reconciliations', selectedAccountId] });
            toast({ title: 'Reconciliation started', description: 'The matching workspace is ready.' });
        },
        onError: (error) => toast({
            title: 'Unable to start reconciliation',
            description: errorMessage(error, 'Check the statement and bank account configuration.'),
            variant: 'destructive',
        }),
    });

    const selectedAccount = accountsQuery.data?.find((account) => account.id === selectedAccountId);
    const statements = useMemo(
        () => [...(statementsQuery.data ?? [])].sort((a, b) => new Date(b.statementDate).getTime() - new Date(a.statementDate).getTime()),
        [statementsQuery.data],
    );

    const selectAccount = (account: BankAccount) => {
        setSelectedAccountId(account.id);
        setStatementId('');
        setStatementBalance('');
        setAccountPickerOpen(false);
        router.replace(`/finance/cash/reconciliation?account=${account.id}`);
    };

    const selectStatement = (id: string) => {
        const statement = statements.find((item) => item.id === id);
        setStatementId(id);
        if (statement) {
            setStatementBalance(String(statement.closingBalance));
            setReconciliationDate(format(new Date(statement.statementDate), 'yyyy-MM-dd'));
        }
    };

    const handleStart = () => {
        const balance = Number(statementBalance);
        if (!selectedAccountId || !statementId || !reconciliationDate || !Number.isFinite(balance)) {
            toast({
                title: 'Statement details required',
                description: 'Select an imported statement and confirm its ending balance.',
                variant: 'destructive',
            });
            return;
        }

        startMutation.mutate({
            bankAccountId: selectedAccountId,
            statementId,
            reconciliationDate: new Date(`${reconciliationDate}T23:59:59`).toISOString(),
            statementBalance: balance,
            notes: notes.trim() || undefined,
        });
    };

    const handleStatementImported = async (statement: BankStatement) => {
        await queryClient.invalidateQueries({ queryKey: ['bank-statements', selectedAccountId] });
        setStatementId(statement.id);
        setStatementBalance(String(statement.closingBalance));
        setReconciliationDate(format(new Date(statement.statementDate), 'yyyy-MM-dd'));
    };

    if (!selectedAccountId) {
        return (
            <div className="mx-auto max-w-4xl space-y-6 p-8">
                <div>
                    <h1 className="text-3xl font-bold">Bank Reconciliation</h1>
                    <p className="mt-1 text-muted-foreground">Match posted cash transactions to imported bank statement lines.</p>
                </div>
                <Card>
                    <CardHeader>
                        <CardTitle>Select bank account</CardTitle>
                        <CardDescription>Choose the account whose statement you want to reconcile.</CardDescription>
                    </CardHeader>
                    <CardContent>
                        <Popover open={accountPickerOpen} onOpenChange={setAccountPickerOpen}>
                            <PopoverTrigger asChild>
                                <Button variant="outline" role="combobox" aria-expanded={accountPickerOpen} className="w-full justify-between">
                                    Select bank account...
                                    <ChevronsUpDown className="ml-2 h-4 w-4 opacity-50" />
                                </Button>
                            </PopoverTrigger>
                            <PopoverContent className="w-[var(--radix-popover-trigger-width)] p-0">
                                <Command>
                                    <CommandInput placeholder="Search bank accounts..." />
                                    <CommandList>
                                        <CommandEmpty>{accountsQuery.isLoading ? 'Loading accounts...' : 'No active bank account found.'}</CommandEmpty>
                                        <CommandGroup>
                                            {accountsQuery.data?.map((account) => (
                                                <CommandItem
                                                    key={account.id}
                                                    value={`${account.accountName} ${account.bankName} ${account.accountNumber}`}
                                                    onSelect={() => selectAccount(account)}
                                                >
                                                    <Check className="mr-2 h-4 w-4 opacity-0" />
                                                    {account.accountName} - {account.bankName} ({account.currency})
                                                </CommandItem>
                                            ))}
                                        </CommandGroup>
                                    </CommandList>
                                </Command>
                            </PopoverContent>
                        </Popover>
                    </CardContent>
                </Card>
            </div>
        );
    }

    const activeReconciliation = activeReconciliationQuery.data;

    return (
        <div className="mx-auto max-w-[1600px] space-y-6 p-8">
            <div className="flex flex-wrap items-center justify-between gap-4">
                <div className="flex items-center gap-4">
                    <Button variant="ghost" size="icon" onClick={() => router.push('/finance/cash/accounts')} aria-label="Back to bank accounts">
                        <ArrowLeft className="h-4 w-4" />
                    </Button>
                    <div>
                        <h1 className="text-3xl font-bold">Bank Reconciliation</h1>
                        <p className="text-muted-foreground">{selectedAccount?.accountName} ({selectedAccount?.currency})</p>
                    </div>
                </div>
                <div className="flex items-center gap-2">
                    {activeReconciliation && <StatusBadge status={activeReconciliation.status} />}
                    {!activeReconciliation && (
                        <Button variant="outline" onClick={() => setImportDialogOpen(true)}>
                            <Upload className="mr-2 h-4 w-4" />
                            Import statement
                        </Button>
                    )}
                    <Button variant="outline" onClick={() => {
                        setSelectedAccountId('');
                        router.replace('/finance/cash/reconciliation');
                    }}>
                        Change account
                    </Button>
                </div>
            </div>

            {activeReconciliationQuery.isLoading ? (
                <div className="flex justify-center p-16"><Loader2 className="h-8 w-8 animate-spin" /></div>
            ) : activeReconciliation ? (
                <ReconciliationWorkspace reconciliation={activeReconciliation} account={selectedAccount} />
            ) : (
                <Card className="mx-auto mt-12 max-w-2xl">
                    <CardHeader>
                        <CardTitle>Start a new reconciliation</CardTitle>
                        <CardDescription>Select the imported statement that defines this reconciliation period.</CardDescription>
                    </CardHeader>
                    <CardContent className="space-y-5">
                        <div className="space-y-2">
                            <Label htmlFor="statement">Bank statement</Label>
                            <Select value={statementId} onValueChange={selectStatement} disabled={statementsQuery.isLoading}>
                                <SelectTrigger id="statement">
                                    <SelectValue placeholder={statementsQuery.isLoading ? 'Loading statements...' : 'Select an imported statement'} />
                                </SelectTrigger>
                                <SelectContent>
                                    {statements.map((statement) => (
                                        <SelectItem key={statement.id} value={statement.id}>
                                            {statement.statementNumber || 'Statement'} · {formatDate(statement.statementDate)} · {formatCurrency(statement.closingBalance, selectedAccount?.currency ?? '')}
                                        </SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                            {!statementsQuery.isLoading && statements.length === 0 && (
                                <p className="text-sm text-amber-700">
                                    No statement has been imported for this account.{' '}
                                    <button type="button" onClick={() => setImportDialogOpen(true)} className="font-medium underline">
                                        Import a bank statement
                                    </button>{' '}
                                    first.
                                </p>
                            )}
                        </div>
                        <div className="grid gap-4 sm:grid-cols-2">
                            <div className="space-y-2">
                                <Label htmlFor="reconciliation-date">Statement date</Label>
                                <Input id="reconciliation-date" type="date" value={reconciliationDate} onChange={(event) => setReconciliationDate(event.target.value)} />
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="statement-balance">Statement ending balance</Label>
                                <div className="relative">
                                    <span className="absolute left-3 top-2.5 text-sm font-medium text-muted-foreground">{selectedAccount?.currency}</span>
                                    <Input id="statement-balance" type="number" step="0.01" className="pl-14" value={statementBalance} onChange={(event) => setStatementBalance(event.target.value)} />
                                </div>
                            </div>
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="notes">Notes (optional)</Label>
                            <Textarea id="notes" value={notes} onChange={(event) => setNotes(event.target.value)} placeholder="Statement period or reconciliation context" />
                        </div>
                    </CardContent>
                    <CardFooter>
                        <Button className="w-full" onClick={handleStart} disabled={startMutation.isPending || !statementId}>
                            {startMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Start reconciliation
                        </Button>
                    </CardFooter>
                </Card>
            )}
            <StatementImportDialog
                open={importDialogOpen}
                onOpenChange={setImportDialogOpen}
                bankAccountId={selectedAccountId}
                bankAccountName={selectedAccount?.accountName ?? 'selected account'}
                onImported={handleStatementImported}
            />
        </div>
    );
}

function StatementImportDialog({
    open,
    onOpenChange,
    bankAccountId,
    bankAccountName,
    onImported,
}: {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    bankAccountId: string;
    bankAccountName: string;
    onImported: (statement: BankStatement) => Promise<void>;
}) {
    const { toast } = useToast();
    const [file, setFile] = useState<File | null>(null);
    const [statementNumber, setStatementNumber] = useState('');
    const [importNotes, setImportNotes] = useState('');
    const [validationError, setValidationError] = useState('');

    const importMutation = useMutation({
        mutationFn: async () => {
            if (!file) throw new Error('Select a CSV or XLSX statement file.');
            const parsed = await parseBankStatementImportFile(file);
            if (!parsed.file || parsed.errors.length > 0) {
                throw new Error(parsed.errors.slice(0, 8).join('\n') || 'The statement could not be prepared for import.');
            }
            return {
                statement: await cashManagementDataService.importBankStatement(
                    bankAccountId,
                    parsed.file,
                    statementNumber,
                    importNotes,
                ),
                rowCount: parsed.rowCount,
            };
        },
        onSuccess: async ({ statement, rowCount }) => {
            await onImported(statement);
            toast({
                title: 'Bank statement imported',
                description: `${rowCount} transaction ${rowCount === 1 ? 'line is' : 'lines are'} ready for reconciliation.`,
            });
            setFile(null);
            setStatementNumber('');
            setImportNotes('');
            setValidationError('');
            onOpenChange(false);
        },
        onError: (error) => setValidationError(errorMessage(error, 'Unable to import the bank statement.')),
    });

    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent className="sm:max-w-xl">
                <DialogHeader>
                    <DialogTitle>Import bank statement</DialogTitle>
                    <DialogDescription>
                        Upload transactions for {bankAccountName}. XLSX and CSV files are accepted.
                    </DialogDescription>
                </DialogHeader>
                <div className="space-y-5">
                    <div className="rounded-lg border bg-muted/40 p-4">
                        <p className="text-sm font-medium">Need the correct layout?</p>
                        <p className="mt-1 text-sm text-muted-foreground">
                            The template includes examples and explains the Date, Description, Reference, Debit, Credit, and Balance columns.
                        </p>
                        <Button asChild variant="outline" size="sm" className="mt-3">
                            <a href="/templates/bank-statement-import-template.xlsx" download>
                                <Download className="mr-2 h-4 w-4" />
                                Download import template
                            </a>
                        </Button>
                    </div>
                    <div className="space-y-2">
                        <Label htmlFor="statement-file">Statement file</Label>
                        <Input
                            id="statement-file"
                            type="file"
                            accept=".csv,.xlsx,text/csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                            onChange={(event) => {
                                setFile(event.target.files?.[0] ?? null);
                                setValidationError('');
                            }}
                        />
                        <p className="text-xs text-muted-foreground">One transaction per row. Debit and Credit must not both contain an amount.</p>
                    </div>
                    <div className="grid gap-4 sm:grid-cols-2">
                        <div className="space-y-2">
                            <Label htmlFor="statement-number">Statement number (optional)</Label>
                            <Input id="statement-number" value={statementNumber} onChange={(event) => setStatementNumber(event.target.value)} placeholder="e.g. JUN-2026" />
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="import-notes">Notes (optional)</Label>
                            <Input id="import-notes" value={importNotes} onChange={(event) => setImportNotes(event.target.value)} placeholder="e.g. June operating account" />
                        </div>
                    </div>
                    {validationError && (
                        <div className="whitespace-pre-line rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
                            {validationError}
                        </div>
                    )}
                </div>
                <DialogFooter>
                    <Button variant="outline" onClick={() => onOpenChange(false)} disabled={importMutation.isPending}>Cancel</Button>
                    <Button onClick={() => importMutation.mutate()} disabled={!file || importMutation.isPending}>
                        {importMutation.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Upload className="mr-2 h-4 w-4" />}
                        Validate and import
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}

function StatusBadge({ status }: { status: ReconciliationStatus }) {
    const variant = status === ReconciliationStatus.Approved
        ? 'default'
        : status === ReconciliationStatus.Cancelled || status === ReconciliationStatus.Rejected
            ? 'destructive'
            : 'outline';
    return <Badge variant={variant} className="px-4 py-1 text-base">{status.replace(/([a-z])([A-Z])/g, '$1 $2')}</Badge>;
}

function ReconciliationWorkspace({ reconciliation, account }: { reconciliation: BankReconciliation; account?: BankAccount }) {
    const { toast } = useToast();
    const queryClient = useQueryClient();
    const [selectedBookId, setSelectedBookId] = useState('');
    const [selectedLineId, setSelectedLineId] = useState('');
    const [adjustmentOpen, setAdjustmentOpen] = useState(false);
    const [cancelOpen, setCancelOpen] = useState(false);
    const currency = account?.currency ?? '';
    const canEdit = reconciliation.status === ReconciliationStatus.InProgress || reconciliation.status === ReconciliationStatus.Pending;

    const summaryQuery = useQuery({
        queryKey: ['reconciliation-summary', reconciliation.id],
        queryFn: () => cashManagementDataService.getReconciliationSummary(reconciliation.id),
    });
    const matchesQuery = useQuery({
        queryKey: ['reconciliation-matches', reconciliation.id],
        queryFn: () => cashManagementDataService.getReconciliationMatches(reconciliation.id),
    });
    const settingsQuery = useQuery({
        queryKey: ['finance-settings'],
        queryFn: () => financeDataService.getFinanceSettings(),
    });

    const refresh = async () => {
        await Promise.all([
            queryClient.invalidateQueries({ queryKey: ['reconciliation-summary', reconciliation.id] }),
            queryClient.invalidateQueries({ queryKey: ['reconciliation-matches', reconciliation.id] }),
            queryClient.invalidateQueries({ queryKey: ['active-reconciliation', reconciliation.bankAccountId] }),
            queryClient.invalidateQueries({ queryKey: ['bank-account-reconciliations', reconciliation.bankAccountId] }),
        ]);
    };

    const mutationError = (title: string, fallback: string) => (error: unknown) => toast({
        title,
        description: errorMessage(error, fallback),
        variant: 'destructive',
    });

    const autoMatchMutation = useMutation({
        mutationFn: () => cashManagementDataService.autoMatchReconciliation(reconciliation.id),
        onSuccess: async (matches) => {
            await refresh();
            toast({ title: 'Auto-match complete', description: matches.length ? `${matches.length} transaction pair(s) matched.` : 'No additional confident matches were found.' });
        },
        onError: mutationError('Auto-match failed', 'Unable to auto-match these transactions.'),
    });

    const manualMatchMutation = useMutation({
        mutationFn: () => cashManagementDataService.createReconciliationMatch(reconciliation.id, {
            reconciliationId: reconciliation.id,
            cashTransactionId: selectedBookId,
            bankStatementLineId: selectedLineId,
        }),
        onSuccess: async () => {
            setSelectedBookId('');
            setSelectedLineId('');
            await refresh();
            toast({ title: 'Transactions matched' });
        },
        onError: mutationError('Match failed', 'The selected rows could not be matched.'),
    });

    const removeMatchMutation = useMutation({
        mutationFn: (matchId: string) => cashManagementDataService.removeReconciliationMatch(matchId),
        onSuccess: async () => {
            await refresh();
            toast({ title: 'Match removed' });
        },
        onError: mutationError('Unable to remove match', 'The matched pair could not be reopened.'),
    });

    const finalizeMutation = useMutation({
        mutationFn: () => cashManagementDataService.completeReconciliation(reconciliation.id),
        onSuccess: async () => {
            await refresh();
            toast({ title: 'Reconciliation finalized', description: 'It is ready for workflow approval.' });
        },
        onError: mutationError('Finalization failed', 'The reconciliation is not ready to finalize.'),
    });

    const approveMutation = useMutation({
        mutationFn: () => cashManagementDataService.approveReconciliation(reconciliation.id),
        onSuccess: async (result) => {
            await refresh();
            toast({
                title: result.status === ReconciliationStatus.Approved ? 'Reconciliation approved' : 'Approval advanced',
                description: result.status === ReconciliationStatus.Approved ? 'The reconciliation is now locked.' : 'The next workflow approver can continue the approval.',
            });
        },
        onError: mutationError('Approval failed', 'The approval workflow could not be completed.'),
    });

    const summary = summaryQuery.data;
    const matches = matchesQuery.data ?? [];
    const selectedBook = summary?.unmatchedBookTransactions.find((item) => item.id === selectedBookId);
    const selectedLine = summary?.unmatchedStatementLines.find((item) => item.id === selectedLineId);
    const amountsAgree = Boolean(selectedBook && selectedLine && Math.abs(selectedBook.amount - selectedLine.amount) < 0.005);
    const directionsAgree = Boolean(selectedBook && selectedLine && isDirectionCompatible(selectedBook, selectedLine));
    const statementDateToleranceDays = settingsQuery.data?.bankStatementMatchDateToleranceDays ?? 3;
    const selectedDateDifferenceDays = selectedBook && selectedLine
        ? calendarDayDifference(selectedBook.transactionDate, selectedLine.transactionDate)
        : null;
    const selectedDatesWithinTolerance = Boolean(selectedBook && selectedLine && isWithinStatementDateTolerance(
        selectedBook.transactionDate,
        selectedLine.transactionDate,
        statementDateToleranceDays,
    ));
    const isRefreshing = summaryQuery.isFetching || matchesQuery.isFetching;

    if (summaryQuery.isLoading || matchesQuery.isLoading) {
        return <div className="flex justify-center p-16"><Loader2 className="h-8 w-8 animate-spin" /></div>;
    }

    if (summaryQuery.isError || matchesQuery.isError || !summary) {
        return (
            <Card className="border-destructive/40">
                <CardContent className="flex flex-col items-center gap-3 p-10 text-center">
                    <AlertCircle className="h-10 w-10 text-destructive" />
                    <h2 className="text-lg font-semibold">Unable to load the reconciliation workspace</h2>
                    <p className="text-sm text-muted-foreground">{errorMessage(summaryQuery.error ?? matchesQuery.error, 'Refresh the page and try again.')}</p>
                    <Button variant="outline" onClick={() => refresh()}><RefreshCw className="mr-2 h-4 w-4" />Retry</Button>
                </CardContent>
            </Card>
        );
    }

    return (
        <div className="space-y-6">
            <div className="grid gap-4 md:grid-cols-3">
                <BalanceCard label="Statement balance" value={summary.statementBalance} currency={currency} />
                <BalanceCard label="Book balance (GL)" value={summary.bookBalance} currency={currency} />
                <BalanceCard label="Difference" value={summary.difference} currency={currency} difference />
            </div>

            <div className="flex flex-wrap items-center justify-between gap-3">
                <div className="flex flex-wrap gap-2 text-sm text-muted-foreground">
                    <Badge variant="secondary">{summary.totalMatches} matched</Badge>
                    <Badge variant="outline">{summary.unmatchedBookTransactions.length} unmatched book</Badge>
                    <Badge variant="outline">{summary.unmatchedStatementLines.length} unmatched statement</Badge>
                    <Badge variant="outline">Auto-match date window: ±{statementDateToleranceDays} calendar days</Badge>
                </div>
                <div className="flex flex-wrap gap-2">
                    <Button variant="outline" size="sm" onClick={() => refresh()} disabled={isRefreshing}>
                        <RefreshCw className={cn('mr-2 h-4 w-4', isRefreshing && 'animate-spin')} />Refresh
                    </Button>
                    {canEdit && (
                        <Button variant="outline" size="sm" onClick={() => autoMatchMutation.mutate()} disabled={autoMatchMutation.isPending}>
                            {autoMatchMutation.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <WandSparkles className="mr-2 h-4 w-4" />}
                            Auto-match
                        </Button>
                    )}
                </div>
            </div>

            {canEdit && (
                <div className="grid gap-5 xl:grid-cols-2">
                    <UnmatchedBookTable rows={summary.unmatchedBookTransactions} selectedId={selectedBookId} onSelect={setSelectedBookId} currency={currency} />
                    <UnmatchedStatementTable rows={summary.unmatchedStatementLines} selectedId={selectedLineId} onSelect={setSelectedLineId} currency={currency} />
                </div>
            )}

            {canEdit && (selectedBook || selectedLine) && (
                <Card className={cn(
                    'border-dashed',
                    selectedBook && selectedLine && (!amountsAgree || !directionsAgree) && 'border-destructive/60',
                    selectedBook && selectedLine && amountsAgree && directionsAgree && !selectedDatesWithinTolerance && 'border-amber-500/70',
                )}>
                    <CardContent className="flex flex-wrap items-center justify-between gap-4 p-4">
                        <div>
                            <p className="font-medium">{selectedBook && selectedLine && amountsAgree && directionsAgree ? 'Ready to create a manual match' : selectedBook && selectedLine ? 'The selected rows cannot be matched' : 'Select one row from each table'}</p>
                            <p className="text-sm text-muted-foreground">
                                {selectedBook && selectedLine
                                    ? !amountsAgree
                                        ? `Amounts differ: ${formatCurrency(selectedBook.amount, currency)} vs ${formatCurrency(selectedLine.amount, currency)}`
                                        : directionsAgree && !selectedDatesWithinTolerance
                                        ? `Amounts and direction agree, but the dates are ${selectedDateDifferenceDays} calendar day(s) apart—outside the ±${statementDateToleranceDays}-day auto-match policy. Manual matching remains available for a reviewed exception.`
                                        : directionsAgree
                                        ? `${formatCurrency(selectedBook.amount, currency)} on both sides; dates are within the ±${statementDateToleranceDays}-day policy window.`
                                        : 'The receipt/payment direction does not agree with the statement credit/debit.'
                                    : 'A match requires one posted book transaction and one statement line.'}
                            </p>
                        </div>
                        <Button onClick={() => manualMatchMutation.mutate()} disabled={!amountsAgree || !directionsAgree || manualMatchMutation.isPending}>
                            {manualMatchMutation.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Link2 className="mr-2 h-4 w-4" />}
                            Match selected rows
                        </Button>
                    </CardContent>
                </Card>
            )}

            <MatchedPairsTable
                matches={matches}
                currency={currency}
                canEdit={canEdit}
                removingId={removeMatchMutation.variables}
                onRemove={(id) => removeMatchMutation.mutate(id)}
            />

            <Card>
                <CardHeader>
                    <CardTitle>Complete reconciliation</CardTitle>
                    <CardDescription>
                        {reconciliation.status === ReconciliationStatus.Completed
                            ? 'The balances agree. Send this completed reconciliation through its approval workflow.'
                            : 'Post genuine bank-only adjustments when needed, then finalize once the statement and posted GL balance agree.'}
                    </CardDescription>
                </CardHeader>
                <CardFooter className="flex flex-wrap justify-between gap-3">
                    <div className="flex gap-2">
                        {canEdit && <Button variant="outline" onClick={() => setAdjustmentOpen(true)}><Scale className="mr-2 h-4 w-4" />Post adjustment</Button>}
                        {canEdit && <Button variant="ghost" className="text-destructive" onClick={() => setCancelOpen(true)}><XCircle className="mr-2 h-4 w-4" />Cancel reconciliation</Button>}
                    </div>
                    {canEdit ? (
                        <Button onClick={() => finalizeMutation.mutate()} disabled={Math.abs(summary.difference) >= 0.005 || finalizeMutation.isPending}>
                            {finalizeMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Finalize reconciliation
                        </Button>
                    ) : reconciliation.status === ReconciliationStatus.Completed ? (
                        <Button onClick={() => approveMutation.mutate()} disabled={approveMutation.isPending}>
                            {approveMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Approve reconciliation
                        </Button>
                    ) : null}
                </CardFooter>
            </Card>

            <AdjustmentDialog
                open={adjustmentOpen}
                onOpenChange={setAdjustmentOpen}
                reconciliation={reconciliation}
                currency={currency}
                bankGlAccountId={account?.glAccountId}
                onPosted={refresh}
            />
            <CancelDialog
                open={cancelOpen}
                onOpenChange={setCancelOpen}
                reconciliation={reconciliation}
                onCancelled={refresh}
            />
        </div>
    );
}

function BalanceCard({ label, value, currency, difference = false }: { label: string; value: number; currency: string; difference?: boolean }) {
    return (
        <Card>
            <CardHeader className="pb-2"><CardTitle className="text-sm text-muted-foreground">{label}</CardTitle></CardHeader>
            <CardContent>
                <div className={cn('text-2xl font-bold', difference && (Math.abs(value) < 0.005 ? 'text-green-600' : 'text-red-600'))}>
                    {formatCurrency(value, currency)}
                </div>
            </CardContent>
        </Card>
    );
}

function UnmatchedBookTable({ rows, selectedId, onSelect, currency }: { rows: UnmatchedTransaction[]; selectedId: string; onSelect: (id: string) => void; currency: string }) {
    return (
        <Card className="overflow-hidden">
            <CardHeader>
                <CardTitle>Unmatched book transactions</CardTitle>
                <CardDescription>Posted cash transactions through the statement date.</CardDescription>
            </CardHeader>
            <CardContent className="max-h-[430px] overflow-auto p-0">
                <Table>
                    <TableHeader><TableRow><TableHead>Date</TableHead><TableHead>Transaction</TableHead><TableHead className="text-right">Amount</TableHead></TableRow></TableHeader>
                    <TableBody>
                        {rows.length === 0 ? <EmptyRow label="All eligible book transactions are matched." /> : rows.map((row) => (
                            <TableRow key={row.id} onClick={() => onSelect(row.id)} aria-selected={selectedId === row.id} className="cursor-pointer aria-selected:bg-primary/10">
                                <TableCell className="whitespace-nowrap">{formatDate(row.transactionDate)}</TableCell>
                                <TableCell>
                                    <div className="font-medium">{row.description || row.transactionType}</div>
                                    <div className="text-xs text-muted-foreground">{row.referenceNumber || row.transactionNumber} · {row.transactionType}</div>
                                </TableCell>
                                <TableCell className="whitespace-nowrap text-right font-medium">{formatCurrency(row.amount, currency)}</TableCell>
                            </TableRow>
                        ))}
                    </TableBody>
                </Table>
            </CardContent>
        </Card>
    );
}

function UnmatchedStatementTable({ rows, selectedId, onSelect, currency }: { rows: UnmatchedStatementLine[]; selectedId: string; onSelect: (id: string) => void; currency: string }) {
    return (
        <Card className="overflow-hidden">
            <CardHeader>
                <CardTitle>Unmatched statement lines</CardTitle>
                <CardDescription>Lines from the statement selected for this reconciliation.</CardDescription>
            </CardHeader>
            <CardContent className="max-h-[430px] overflow-auto p-0">
                <Table>
                    <TableHeader><TableRow><TableHead>Date</TableHead><TableHead>Statement line</TableHead><TableHead className="text-right">Amount</TableHead></TableRow></TableHeader>
                    <TableBody>
                        {rows.length === 0 ? <EmptyRow label="All statement lines are matched." /> : rows.map((row) => (
                            <TableRow key={row.id} onClick={() => onSelect(row.id)} aria-selected={selectedId === row.id} className="cursor-pointer aria-selected:bg-primary/10">
                                <TableCell className="whitespace-nowrap">{formatDate(row.transactionDate)}</TableCell>
                                <TableCell>
                                    <div className="font-medium">{row.description || 'Bank statement line'}</div>
                                    <div className="text-xs text-muted-foreground">{row.referenceNumber || 'No reference'} · {row.creditAmount > 0 ? 'Credit' : 'Debit'}</div>
                                </TableCell>
                                <TableCell className="whitespace-nowrap text-right font-medium">{formatCurrency(row.amount, currency)}</TableCell>
                            </TableRow>
                        ))}
                    </TableBody>
                </Table>
            </CardContent>
        </Card>
    );
}

function EmptyRow({ label, colSpan = 3 }: { label: string; colSpan?: number }) {
    return <TableRow><TableCell colSpan={colSpan} className="h-28 text-center text-muted-foreground">{label}</TableCell></TableRow>;
}

function MatchedPairsTable({ matches, currency, canEdit, removingId, onRemove }: { matches: ReconciliationMatch[]; currency: string; canEdit: boolean; removingId?: string; onRemove: (id: string) => void }) {
    return (
        <Card className="overflow-hidden">
            <CardHeader>
                <CardTitle>Matched pairs</CardTitle>
                <CardDescription>Review automatic and manual matches. Open reconciliations can be corrected by removing a pair.</CardDescription>
            </CardHeader>
            <CardContent className="p-0">
                <Table>
                    <TableHeader><TableRow><TableHead>Book transaction</TableHead><TableHead>Statement line</TableHead><TableHead>Method</TableHead><TableHead className="text-right">Amount</TableHead>{canEdit && <TableHead className="w-16" />}</TableRow></TableHeader>
                    <TableBody>
                        {matches.length === 0 ? <EmptyRow label="No transactions have been matched yet." colSpan={canEdit ? 5 : 4} /> : matches.map((match) => (
                            <TableRow key={match.id}>
                                <TableCell>
                                    <div className="font-medium">{match.cashTransactionDescription || match.cashTransactionNumber}</div>
                                    <div className="text-xs text-muted-foreground">{formatDate(match.cashTransactionDate)} · {match.cashTransactionReference || match.cashTransactionNumber}</div>
                                </TableCell>
                                <TableCell>
                                    <div className="font-medium">{match.statementDescription || 'Bank statement line'}</div>
                                    <div className="text-xs text-muted-foreground">{formatDate(match.statementTransactionDate)} · {match.statementReference || 'No reference'}</div>
                                </TableCell>
                                <TableCell><Badge variant="outline">{match.isAutoMatched ? `Auto ${match.matchConfidence ?? ''}%` : 'Manual'}</Badge></TableCell>
                                <TableCell className="text-right font-medium">{formatCurrency(match.cashTransactionAmount, currency)}</TableCell>
                                {canEdit && (
                                    <TableCell>
                                        <Button variant="ghost" size="icon" aria-label="Remove match" onClick={() => onRemove(match.id)} disabled={removingId === match.id}>
                                            {removingId === match.id ? <Loader2 className="h-4 w-4 animate-spin" /> : <RotateCcw className="h-4 w-4" />}
                                        </Button>
                                    </TableCell>
                                )}
                            </TableRow>
                        ))}
                    </TableBody>
                </Table>
            </CardContent>
        </Card>
    );
}

function AdjustmentDialog({ open, onOpenChange, reconciliation, currency, bankGlAccountId, onPosted }: { open: boolean; onOpenChange: (open: boolean) => void; reconciliation: BankReconciliation; currency: string; bankGlAccountId?: string; onPosted: () => Promise<void> }) {
    const { toast } = useToast();
    const [adjustmentType, setAdjustmentType] = useState<ReconciliationAdjustmentType>(ReconciliationAdjustmentType.BankCharge);
    const [amount, setAmount] = useState('');
    const [offsetAccountId, setOffsetAccountId] = useState('');
    const [transactionDate, setTransactionDate] = useState(format(new Date(reconciliation.reconciliationDate), 'yyyy-MM-dd'));
    const [referenceNumber, setReferenceNumber] = useState('');
    const [description, setDescription] = useState('');
    const [bankDimensionLineId] = useState(() => crypto.randomUUID());
    const [offsetDimensionLineId] = useState(() => crypto.randomUUID());
    const [defaultDimensionValues, setDefaultDimensionValues] = useState<Record<string, string>>({});
    const [lineDimensionValues, setLineDimensionValues] = useState<Record<string, Record<string, string>>>({});
    const [applyDefaultToAll, setApplyDefaultToAll] = useState(false);

    const accountsQuery = useQuery({
        queryKey: ['reconciliation-adjustment-accounts'],
        queryFn: () => financeDataService.getAccounts({ status: 'Active', pageSize: 1000 }),
        enabled: open,
    });
    const postingAccounts = useMemo(() => (accountsQuery.data ?? [])
        .filter((item) => item.id !== bankGlAccountId && item.allowDirectPosting && item.isPostingAllowed !== false && !item.isControlAccount)
        .sort((a, b) => a.accountNumber.localeCompare(b.accountNumber)), [accountsQuery.data, bankGlAccountId]);
    const selectedOffsetAccount = postingAccounts.find((item) => item.id === offsetAccountId);

    const mutation = useMutation({
        mutationFn: (dto: CreateReconciliationAdjustmentDto) => cashManagementDataService.postReconciliationAdjustment(reconciliation.id, dto),
        onSuccess: async (result) => {
            await onPosted();
            onOpenChange(false);
            setAmount('');
            setDefaultDimensionValues({});
            setLineDimensionValues({});
            setApplyDefaultToAll(false);
            toast({ title: result.wasDuplicate ? 'Adjustment already posted' : 'Adjustment posted', description: `${formatCurrency(result.amount, currency)} was posted to the GL.` });
        },
        onError: (error) => toast({ title: 'Adjustment failed', description: errorMessage(error, 'The adjustment could not be posted.'), variant: 'destructive' }),
    });

    const submit = () => {
        const numericAmount = Number(amount);
        if (!bankGlAccountId) {
            toast({ title: 'Bank GL account required', description: 'Link the bank account to a posting account before recording an adjustment.', variant: 'destructive' });
            return;
        }
        if (!offsetAccountId || !Number.isFinite(numericAmount) || numericAmount <= 0) return;
        mutation.mutate({
            adjustmentType,
            transactionDate: new Date(`${transactionDate}T12:00:00`).toISOString(),
            amount: numericAmount,
            offsetAccountId,
            referenceNumber: referenceNumber.trim() || undefined,
            description: description.trim() || ADJUSTMENT_LABELS[adjustmentType],
            idempotencyKey: crypto.randomUUID(),
            financeDimensions: {
                defaultDimensions: toFinancePostingDimensionValues(defaultDimensionValues),
                lines: [
                    {
                        sourceLineId: bankDimensionLineId,
                        accountId: bankGlAccountId,
                        dimensions: toFinancePostingDimensionValues(lineDimensionValues[bankDimensionLineId] || {}),
                    },
                    {
                        sourceLineId: offsetDimensionLineId,
                        accountId: offsetAccountId,
                        dimensions: toFinancePostingDimensionValues(lineDimensionValues[offsetDimensionLineId] || {}),
                    },
                ],
                applyDefaultToEligibleLines: applyDefaultToAll,
            },
        });
    };

    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent>
                <DialogHeader>
                    <DialogTitle>Post reconciliation adjustment</DialogTitle>
                    <DialogDescription>This creates and posts a real cash transaction and journal entry. Use it only for genuine bank-only items such as fees or interest.</DialogDescription>
                </DialogHeader>
                <div className="space-y-4">
                    <div className="space-y-2">
                        <Label>Adjustment type</Label>
                        <Select value={adjustmentType} onValueChange={(value) => setAdjustmentType(value as ReconciliationAdjustmentType)}>
                            <SelectTrigger><SelectValue /></SelectTrigger>
                            <SelectContent>{Object.entries(ADJUSTMENT_LABELS).map(([value, label]) => <SelectItem key={value} value={value}>{label}</SelectItem>)}</SelectContent>
                        </Select>
                    </div>
                    <div className="grid grid-cols-2 gap-4">
                        <div className="space-y-2"><Label htmlFor="adjustment-date">Transaction date</Label><Input id="adjustment-date" type="date" value={transactionDate} onChange={(event) => setTransactionDate(event.target.value)} /></div>
                        <div className="space-y-2"><Label htmlFor="adjustment-amount">Amount ({currency})</Label><Input id="adjustment-amount" type="number" min="0.01" step="0.01" value={amount} onChange={(event) => setAmount(event.target.value)} /></div>
                    </div>
                    <div className="space-y-2">
                        <Label>Offset account</Label>
                        <Select value={offsetAccountId} onValueChange={setOffsetAccountId} disabled={accountsQuery.isLoading}>
                            <SelectTrigger><SelectValue placeholder={accountsQuery.isLoading ? 'Loading accounts...' : 'Select a posting account'} /></SelectTrigger>
                            <SelectContent>{postingAccounts.map((item) => <SelectItem key={item.id} value={item.id}>{item.accountNumber} · {item.accountName}</SelectItem>)}</SelectContent>
                        </Select>
                    </div>
                    <SourceDocumentDimensionPanel
                        context={{
                            sourceModule: 'CASHBANK',
                            sourceDocumentType: 'BankReconciliationAdjustment',
                            postingAction: 'Post',
                            sourceRoute: 'finance.cash.bank-reconciliation-adjustments',
                            contractVersion: '1.0',
                        }}
                        effectiveDate={transactionDate}
                        lines={[
                            {
                                id: bankDimensionLineId,
                                accountId: bankGlAccountId,
                                accountLabel: 'Reconciliation bank leg',
                            },
                            {
                                id: offsetDimensionLineId,
                                accountId: offsetAccountId || undefined,
                                accountLabel: selectedOffsetAccount
                                    ? `${selectedOffsetAccount.accountNumber} · ${selectedOffsetAccount.accountName}`
                                    : 'Adjustment offset leg',
                            },
                        ]}
                        defaultValues={defaultDimensionValues}
                        lineValues={lineDimensionValues}
                        onDefaultValuesChange={(values) => {
                            setDefaultDimensionValues(values);
                            setApplyDefaultToAll(false);
                        }}
                        onLineValuesChange={setLineDimensionValues}
                        onApplyDefaultToAll={() => setApplyDefaultToAll(true)}
                        disabled={mutation.isPending}
                    />
                    <div className="space-y-2"><Label htmlFor="adjustment-reference">Reference</Label><Input id="adjustment-reference" value={referenceNumber} onChange={(event) => setReferenceNumber(event.target.value)} /></div>
                    <div className="space-y-2"><Label htmlFor="adjustment-description">Description</Label><Textarea id="adjustment-description" value={description} onChange={(event) => setDescription(event.target.value)} placeholder={ADJUSTMENT_LABELS[adjustmentType]} /></div>
                </div>
                <DialogFooter>
                    <Button variant="outline" onClick={() => onOpenChange(false)}>Close</Button>
                    <Button onClick={submit} disabled={mutation.isPending || !bankGlAccountId || !offsetAccountId || Number(amount) <= 0}>
                        {mutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Post adjustment
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}

function CancelDialog({ open, onOpenChange, reconciliation, onCancelled }: { open: boolean; onOpenChange: (open: boolean) => void; reconciliation: BankReconciliation; onCancelled: () => Promise<void> }) {
    const { toast } = useToast();
    const [reason, setReason] = useState('');
    const mutation = useMutation({
        mutationFn: () => cashManagementDataService.cancelReconciliation(reconciliation.id, reason),
        onSuccess: async () => {
            await onCancelled();
            onOpenChange(false);
            toast({ title: 'Reconciliation cancelled' });
        },
        onError: (error) => toast({ title: 'Cancellation failed', description: errorMessage(error, 'The reconciliation could not be cancelled.'), variant: 'destructive' }),
    });
    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent>
                <DialogHeader><DialogTitle>Cancel this reconciliation?</DialogTitle><DialogDescription>The session will close and its reason will be retained in the audit trail. Existing posted adjustments are not reversed.</DialogDescription></DialogHeader>
                <div className="space-y-2"><Label htmlFor="cancel-reason">Reason</Label><Textarea id="cancel-reason" value={reason} onChange={(event) => setReason(event.target.value)} placeholder="Explain why this reconciliation is being cancelled" /></div>
                <DialogFooter>
                    <Button variant="outline" onClick={() => onOpenChange(false)}>Keep reconciliation</Button>
                    <Button variant="destructive" onClick={() => mutation.mutate()} disabled={mutation.isPending || !reason.trim()}>
                        {mutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Cancel reconciliation
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}
