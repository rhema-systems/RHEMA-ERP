'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { format } from 'date-fns';
import { ArrowLeft, ArrowRight, ArrowRightLeft, CalendarIcon, FileText, Loader2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import {
    Card,
    CardContent,
    CardDescription,
    CardFooter,
    CardHeader,
    CardTitle,
} from '@/components/ui/card';
import { Calendar } from '@/components/ui/calendar';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
    Popover,
    PopoverContent,
    PopoverTrigger,
} from '@/components/ui/popover';
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import { cn } from '@/lib/utils';

import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import { financeService } from '@/services/finance.service';
import type { BankAccount, CreateBankTransferDto } from '@/types/cash-management';

const transferSchema = z.object({
    transactionDate: z.date({ message: 'Date is required' }),
    fromBankAccountId: z.string().min(1, 'Source bank account is required'),
    toBankAccountId: z.string().min(1, 'Destination bank account is required'),
    amount: z.number().min(0.01, 'Amount must be greater than 0'),
    exchangeRate: z.number().min(0.0001, 'Exchange rate must be greater than 0'),
    referenceNumber: z.string().optional(),
    description: z.string().optional(),
}).refine((data) => data.fromBankAccountId !== data.toBankAccountId, {
    message: 'Destination account must be different from source account',
    path: ['toBankAccountId'],
});

type TransferFormValues = z.infer<typeof transferSchema>;

export default function RecordBankTransferPage() {
    const router = useRouter();
    const { toast } = useToast();
    const [isSubmitting, setIsSubmitting] = useState(false);

    const { data: bankAccounts, isLoading: accountsLoading } = useQuery({
        queryKey: ['bank-accounts', 'active'],
        queryFn: () => cashManagementDataService.getActiveBankAccounts(),
    });

    const form = useForm<TransferFormValues>({
        resolver: zodResolver(transferSchema),
        defaultValues: {
            transactionDate: new Date(),
            amount: 0,
            exchangeRate: 1,
        },
    });

    const fromBankAccountId = form.watch('fromBankAccountId');
    const toBankAccountId = form.watch('toBankAccountId');
    const amount = form.watch('amount');
    const exchangeRate = form.watch('exchangeRate');

    const fromAccount = useMemo(
        () => bankAccounts?.find((account) => account.id === fromBankAccountId),
        [bankAccounts, fromBankAccountId]
    );

    const toAccount = useMemo(
        () => bankAccounts?.find((account) => account.id === toBankAccountId),
        [bankAccounts, toBankAccountId]
    );

    const destinationAccounts = useMemo(
        () => bankAccounts?.filter((account) =>
            account.id !== fromBankAccountId &&
            (!fromAccount || account.currency === fromAccount.currency)
        ) ?? [],
        [bankAccounts, fromBankAccountId, fromAccount]
    );

    const transferCurrency = fromAccount?.currency ?? 'GHS';
    const requiresExchangeRate = [fromAccount?.currency, toAccount?.currency]
        .filter(Boolean)
        .some((currency) => currency !== 'GHS');

    useEffect(() => {
        if (
            fromBankAccountId &&
            toBankAccountId &&
            (fromBankAccountId === toBankAccountId || fromAccount?.currency !== toAccount?.currency)
        ) {
            form.setValue('toBankAccountId', '');
        }
    }, [fromBankAccountId, toBankAccountId, fromAccount?.currency, toAccount?.currency, form]);

    useEffect(() => {
        const currencyForRate = fromAccount?.currency !== 'GHS'
            ? fromAccount?.currency
            : toAccount?.currency !== 'GHS'
                ? toAccount?.currency
                : 'GHS';

        if (!currencyForRate || currencyForRate === 'GHS') {
            form.setValue('exchangeRate', 1);
            return;
        }

        void financeService.getCurrentExchangeRate(currencyForRate)
            .then((rate) => form.setValue('exchangeRate', Number(rate.currentExchangeRate ?? rate.rate ?? 1)))
            .catch(() => form.setValue('exchangeRate', 1));
    }, [fromAccount?.currency, toAccount?.currency, form]);

    const formatCurrency = (value: number | undefined, currency = 'GHS') => {
        return `${currency} ${(value ?? 0).toLocaleString('en-GH', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2,
        })}`;
    };

    const formatAccount = (account: BankAccount) => {
        return `${account.accountName} (${account.currency}) - ${account.bankName}`;
    };

    const onSubmit = async (data: TransferFormValues) => {
        setIsSubmitting(true);
        try {
            const payload: CreateBankTransferDto = {
                transactionDate: data.transactionDate.toISOString(),
                fromBankAccountId: data.fromBankAccountId,
                toBankAccountId: data.toBankAccountId,
                amount: data.amount,
                exchangeRate: requiresExchangeRate ? data.exchangeRate : undefined,
                referenceNumber: data.referenceNumber,
                description: data.description,
            };

            await cashManagementDataService.createBankTransfer(payload);

            toast({
                title: 'Transfer Recorded',
                description: 'The bank transfer has been recorded successfully.',
            });

            router.push('/finance/cash/transactions');
            router.refresh();
        } catch (error) {
            console.error(error);
            toast({
                title: 'Error',
                description: 'Failed to record bank transfer. Please try again.',
                variant: 'destructive',
            });
        } finally {
            setIsSubmitting(false);
        }
    };

    return (
        <div className="space-y-6 p-8 max-w-[1200px] mx-auto">
            <div className="flex items-center space-x-4">
                <Button variant="ghost" size="icon" onClick={() => router.back()}>
                    <ArrowLeft className="h-4 w-4" />
                </Button>
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Bank Transfer</h1>
                    <p className="text-muted-foreground">
                        Transfer funds between company bank accounts.
                    </p>
                </div>
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                <div className="lg:col-span-2">
                    <Card>
                        <form onSubmit={form.handleSubmit(onSubmit)}>
                            <CardHeader>
                                <CardTitle>Transfer Details</CardTitle>
                                <CardDescription>Enter the source, destination, and transfer amount.</CardDescription>
                            </CardHeader>
                            <CardContent className="space-y-4">
                                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                    <div className="space-y-2">
                                        <Label>Transaction Date</Label>
                                        <Popover>
                                            <PopoverTrigger asChild>
                                                <Button
                                                    variant="outline"
                                                    className={cn(
                                                        'w-full justify-start text-left font-normal',
                                                        !form.watch('transactionDate') && 'text-muted-foreground'
                                                    )}
                                                >
                                                    <CalendarIcon className="mr-2 h-4 w-4" />
                                                    {form.watch('transactionDate')
                                                        ? format(form.watch('transactionDate'), 'PPP')
                                                        : <span>Pick a date</span>}
                                                </Button>
                                            </PopoverTrigger>
                                            <PopoverContent className="w-auto p-0">
                                                <Calendar
                                                    mode="single"
                                                    selected={form.watch('transactionDate')}
                                                    onSelect={(date) => date && form.setValue('transactionDate', date)}
                                                    initialFocus
                                                />
                                            </PopoverContent>
                                        </Popover>
                                    </div>
                                    <div className="space-y-2">
                                        <Label>Reference Number</Label>
                                        <Input {...form.register('referenceNumber')} placeholder="e.g. TRF-001" />
                                    </div>
                                </div>

                                <div className="grid grid-cols-1 md:grid-cols-[1fr_auto_1fr] gap-4 items-start">
                                    <div className="space-y-2">
                                        <Label>From Bank Account</Label>
                                        <Select
                                            onValueChange={(value) => form.setValue('fromBankAccountId', value)}
                                            value={form.watch('fromBankAccountId') || ''}
                                            disabled={accountsLoading}
                                        >
                                            <SelectTrigger>
                                                <SelectValue placeholder={accountsLoading ? 'Loading accounts...' : 'Select source account'} />
                                            </SelectTrigger>
                                            <SelectContent>
                                                {bankAccounts?.map((account) => (
                                                    <SelectItem key={account.id} value={account.id}>
                                                        {formatAccount(account)}
                                                    </SelectItem>
                                                ))}
                                            </SelectContent>
                                        </Select>
                                        {form.formState.errors.fromBankAccountId && (
                                            <p className="text-sm text-red-500">{form.formState.errors.fromBankAccountId.message}</p>
                                        )}
                                    </div>

                                    <div className="hidden md:flex h-10 items-center justify-center pt-8">
                                        <ArrowRight className="h-5 w-5 text-muted-foreground" />
                                    </div>

                                    <div className="space-y-2">
                                        <Label>To Bank Account</Label>
                                        <Select
                                            onValueChange={(value) => form.setValue('toBankAccountId', value)}
                                            value={form.watch('toBankAccountId') || ''}
                                            disabled={accountsLoading || !fromBankAccountId}
                                        >
                                            <SelectTrigger>
                                                <SelectValue
                                                    placeholder={
                                                        !fromBankAccountId
                                                            ? 'Select source first'
                                                            : destinationAccounts.length === 0
                                                                ? 'No same-currency accounts'
                                                                : 'Select destination account'
                                                    }
                                                />
                                            </SelectTrigger>
                                            <SelectContent>
                                                {destinationAccounts.map((account) => (
                                                    <SelectItem key={account.id} value={account.id}>
                                                        {formatAccount(account)}
                                                    </SelectItem>
                                                ))}
                                            </SelectContent>
                                        </Select>
                                        {form.formState.errors.toBankAccountId && (
                                            <p className="text-sm text-red-500">{form.formState.errors.toBankAccountId.message}</p>
                                        )}
                                    </div>
                                </div>

                                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                    <div className="space-y-2">
                                        <Label>Amount</Label>
                                        <div className="relative">
                                            <span className="absolute left-3 top-2.5 text-gray-500 text-sm font-medium">
                                                {transferCurrency}
                                            </span>
                                            <Input
                                                type="number"
                                                step="0.01"
                                                className="pl-12"
                                                {...form.register('amount', { valueAsNumber: true })}
                                            />
                                        </div>
                                        {form.formState.errors.amount && (
                                            <p className="text-sm text-red-500">{form.formState.errors.amount.message}</p>
                                        )}
                                    </div>
                                    <div className="space-y-2">
                                        <Label>Exchange Rate</Label>
                                        <Input
                                            type="number"
                                            step="0.000001"
                                            disabled={!requiresExchangeRate}
                                            {...form.register('exchangeRate', { valueAsNumber: true })}
                                        />
                                        <p className="text-xs text-muted-foreground">
                                            1 transfer currency = {exchangeRate || 1} GHS
                                        </p>
                                        {form.formState.errors.exchangeRate && (
                                            <p className="text-sm text-red-500">{form.formState.errors.exchangeRate.message}</p>
                                        )}
                                    </div>
                                </div>

                                <div className="space-y-2">
                                    <Label>Description</Label>
                                    <Textarea {...form.register('description')} placeholder="Transfer notes..." />
                                </div>
                            </CardContent>
                            <CardFooter className="justify-end space-x-2">
                                <Button variant="ghost" type="button" onClick={() => router.back()}>
                                    Cancel
                                </Button>
                                <Button type="submit" disabled={isSubmitting}>
                                    {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                                    Record Transfer
                                </Button>
                            </CardFooter>
                        </form>
                    </Card>
                </div>

                <div className="space-y-6">
                    <Card className="bg-muted/50">
                        <CardHeader>
                            <CardTitle className="text-sm font-medium">Transfer Preview</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-4 text-sm">
                            <div className="flex gap-2">
                                <ArrowRightLeft className="h-4 w-4 text-blue-500 flex-shrink-0" />
                                <div className="min-w-0">
                                    <p className="font-medium">Account Movement</p>
                                    <p className="text-muted-foreground break-words">
                                        {fromAccount ? fromAccount.accountName : 'Source account'}
                                        {' to '}
                                        {toAccount ? toAccount.accountName : 'destination account'}
                                    </p>
                                </div>
                            </div>
                            <div className="rounded-md border bg-background p-3 space-y-2">
                                <div className="flex items-center justify-between gap-3">
                                    <span className="text-muted-foreground">Debit source</span>
                                    <span className="font-medium text-red-600">
                                        {formatCurrency(amount, transferCurrency)}
                                    </span>
                                </div>
                                <div className="flex items-center justify-between gap-3">
                                    <span className="text-muted-foreground">Credit destination</span>
                                    <span className="font-medium text-green-600">
                                        {formatCurrency(amount, toAccount?.currency ?? transferCurrency)}
                                    </span>
                                </div>
                            </div>
                            <div className="flex gap-2">
                                <FileText className="h-4 w-4 text-slate-500 flex-shrink-0" />
                                <p className="text-muted-foreground">
                                    The transfer creates linked OUT and IN cash transaction records for reconciliation.
                                </p>
                            </div>
                        </CardContent>
                    </Card>
                </div>
            </div>
        </div>
    );
}
