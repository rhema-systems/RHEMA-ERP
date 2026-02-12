'use client';

import { useState, useEffect } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { format } from 'date-fns';
import { Loader2, CheckCircle2, AlertCircle, ArrowLeft, Save, Check, ChevronsUpDown } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle, CardFooter } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import {
    Command,
    CommandEmpty,
    CommandGroup,
    CommandInput,
    CommandItem,
    CommandList,
} from '@/components/ui/command';
import {
    Popover,
    PopoverContent,
    PopoverTrigger,
} from '@/components/ui/popover';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useToast } from '@/components/ui/use-toast';
import { formatCurrency, cn } from '@/lib/utils';

import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import { BankAccount, BankReconciliation, StartReconciliationDto } from '@/types/cash-management';

export default function BankReconciliationPage() {
    const router = useRouter();
    const searchParams = useSearchParams();
    const accountId = searchParams.get('account');
    const { toast } = useToast();
    const queryClient = useQueryClient();

    const [selectedAccountId, setSelectedAccountId] = useState<string>(accountId || '');
    const [statementBalance, setStatementBalance] = useState<string>('');
    const [reconciliationDate, setReconciliationDate] = useState<string>(format(new Date(), 'yyyy-MM-dd'));
    const [open, setOpen] = useState(false);

    // Fetch Accounts
    const { data: accounts } = useQuery({
        queryKey: ['bank-accounts', 'active'],
        queryFn: () => cashManagementDataService.getActiveBankAccounts(),
    });

    // Fetch Active Reconciliation for selected account
    const { data: activeReconciliation, isLoading: isLoadingRecon } = useQuery({
        queryKey: ['active-reconciliation', selectedAccountId],
        queryFn: async () => {
            if (!selectedAccountId) return null;
            // Ideally we should have an endpoint to get the "Latest Open" reconciliation. 
            // `getBankReconciliations` returns a list. Let's fetch all and find the open one.
            const recons = await cashManagementDataService.getBankReconciliations(selectedAccountId);
            return recons.find(r => r.status === 'InProgress' || r.status === 'Pending') || null;
        },
        enabled: !!selectedAccountId,
    });

    const startReconciliationMutation = useMutation({
        mutationFn: (dto: StartReconciliationDto) => cashManagementDataService.startReconciliation(dto),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['active-reconciliation', selectedAccountId] });
            toast({ title: "Reconciliation Started", description: "You can now begin matching transactions." });
        },
        onError: (error) => {
            toast({ title: "Error", description: "Failed to start reconciliation.", variant: "destructive" });
        }
    });

    // Handle Start
    const handleStart = () => {
        if (!selectedAccountId || !statementBalance) return;

        startReconciliationMutation.mutate({
            bankAccountId: selectedAccountId,
            reconciliationDate: new Date(reconciliationDate).toISOString(),
            statementBalance: parseFloat(statementBalance),
        });
    };

    // If no account selected, show account selector
    if (!selectedAccountId) {
        return (
            <div className="p-8 max-w-4xl mx-auto space-y-6">
                <h1 className="text-3xl font-bold">Bank Reconciliation</h1>
                <Card>
                    <CardHeader>
                        <CardTitle>Select Account</CardTitle>
                        <CardDescription>Choose a bank account to reconcile.</CardDescription>
                    </CardHeader>
                    <CardContent>
                        <Popover open={open} onOpenChange={setOpen}>
                            <PopoverTrigger asChild>
                                <Button
                                    variant="outline"
                                    role="combobox"
                                    aria-expanded={open}
                                    className="w-full justify-between"
                                >
                                    {selectedAccountId
                                        ? accounts?.find((account) => account.id === selectedAccountId)?.accountName + ' - ' + accounts?.find((account) => account.id === selectedAccountId)?.bankName
                                        : "Select bank account..."}
                                    <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                                </Button>
                            </PopoverTrigger>
                            <PopoverContent className="w-full p-0">
                                <Command>
                                    <CommandInput placeholder="Search bank account..." />
                                    <CommandList>
                                        <CommandEmpty>No bank account found.</CommandEmpty>
                                        <CommandGroup>
                                            {accounts?.map((account) => (
                                                <CommandItem
                                                    key={account.id}
                                                    value={account.accountName + ' ' + account.bankName} // Concatenate for efficient searching
                                                    onSelect={() => {
                                                        setSelectedAccountId(account.id);
                                                        setOpen(false);
                                                    }}
                                                >
                                                    <Check
                                                        className={cn(
                                                            "mr-2 h-4 w-4",
                                                            selectedAccountId === account.id ? "opacity-100" : "opacity-0"
                                                        )}
                                                    />
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

    const selectedAccount = accounts?.find(a => a.id === selectedAccountId);

    return (
        <div className="p-8 max-w-[1600px] mx-auto space-y-6">
            <div className="flex items-center justify-between">
                <div className="flex items-center space-x-4">
                    <Button variant="ghost" size="icon" onClick={() => router.push('/finance/cash/accounts')}>
                        <ArrowLeft className="h-4 w-4" />
                    </Button>
                    <div>
                        <h1 className="text-3xl font-bold">Bank Reconciliation</h1>
                        <p className="text-muted-foreground">
                            {selectedAccount?.accountName} ({selectedAccount?.currency})
                        </p>
                    </div>
                </div>
                {activeReconciliation && (
                    <Badge variant="outline" className="text-lg px-4 py-1">
                        {activeReconciliation.status}
                    </Badge>
                )}
            </div>

            {isLoadingRecon ? (
                <div className="flex justify-center p-12"><Loader2 className="animate-spin h-8 w-8" /></div>
            ) : !activeReconciliation ? (
                /* Start New Reconciliation Card */
                <Card className="max-w-md mx-auto mt-12">
                    <CardHeader>
                        <CardTitle>Start New Reconciliation</CardTitle>
                        <CardDescription>Enter the statement details to begin.</CardDescription>
                    </CardHeader>
                    <CardContent className="space-y-4">
                        <div className="space-y-2">
                            <Label>Statement Date</Label>
                            <Input
                                type="date"
                                value={reconciliationDate}
                                onChange={(e) => setReconciliationDate(e.target.value)}
                            />
                        </div>
                        <div className="space-y-2">
                            <Label>Statement Ending Balance</Label>
                            <div className="relative">
                                <span className="absolute left-3 top-2.5 text-gray-500 text-sm font-medium">
                                    {selectedAccount?.currency}
                                </span>
                                <Input
                                    type="number"
                                    className="pl-12"
                                    placeholder="0.00"
                                    value={statementBalance}
                                    onChange={(e) => setStatementBalance(e.target.value)}
                                />
                            </div>
                        </div>
                    </CardContent>
                    <CardFooter>
                        <Button className="w-full" onClick={handleStart} disabled={startReconciliationMutation.isPending}>
                            {startReconciliationMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Start Reconciliation
                        </Button>
                    </CardFooter>
                </Card>
            ) : (
                /* Active Reconciliation Interface */
                <ReconciliationWorkspace reconciliation={activeReconciliation} accountCurrency={selectedAccount?.currency || ''} />
            )}
        </div>
    );
}

// Sub-component for the workspace to keep main clean
function ReconciliationWorkspace({ reconciliation, accountCurrency }: { reconciliation: BankReconciliation, accountCurrency: string }) {
    // This would contain the two lists: Book Transactions vs Bank Lines
    // For now, we'll just show a placeholder summary
    return (
        <div className="grid gap-6">
            <div className="grid grid-cols-3 gap-4">
                <Card>
                    <CardHeader className="pb-2"><CardTitle className="text-sm text-muted-foreground">Statement Balance</CardTitle></CardHeader>
                    <CardContent><div className="text-2xl font-bold">{formatCurrency(reconciliation.statementBalance, accountCurrency)}</div></CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2"><CardTitle className="text-sm text-muted-foreground">Book Balance (GL)</CardTitle></CardHeader>
                    <CardContent><div className="text-2xl font-bold">{formatCurrency(reconciliation.bookBalance, accountCurrency)}</div></CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2"><CardTitle className="text-sm text-muted-foreground">Difference</CardTitle></CardHeader>
                    <CardContent>
                        <div className={`text-2xl font-bold ${reconciliation.difference === 0 ? 'text-green-600' : 'text-red-600'}`}>
                            {formatCurrency(reconciliation.difference, accountCurrency)}
                        </div>
                    </CardContent>
                </Card>
            </div>

            <Card className="h-96 flex items-center justify-center border-dashed">
                <div className="text-center space-y-2">
                    <CheckCircle2 className="h-12 w-12 text-muted-foreground mx-auto" />
                    <h3 className="text-lg font-medium">Matching Workspace</h3>
                    <p className="text-muted-foreground max-w-sm">
                        This is where the transaction matching interface would go.
                        Fetching un-reconciled transactions and bank statement lines.
                    </p>
                    <Button variant="outline" className="mt-4">Match Transactions (Demo)</Button>
                </div>
            </Card>
        </div>
    )
}
