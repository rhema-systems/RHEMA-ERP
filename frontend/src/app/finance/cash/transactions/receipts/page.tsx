'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { format } from 'date-fns';
import { CalendarIcon, Loader2, Save, ArrowLeft, Check, User, FileText } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue
} from '@/components/ui/select';
import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
    CardFooter
} from '@/components/ui/card';
import {
    Popover,
    PopoverContent,
    PopoverTrigger,
} from '@/components/ui/popover';
import { Calendar } from '@/components/ui/calendar';
import {
    Tabs,
    TabsList,
    TabsTrigger,
} from "@/components/ui/tabs"
import { useToast } from '@/components/ui/use-toast';
import { cn } from '@/lib/utils';

import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import { financeService } from '@/services/finance.service';
import { CreateCashReceiptDto } from '@/types/cash-management';
import { loadApprovedCashRate } from '@/lib/finance/cash-exchange-rate';
import { SourceDocumentDimensionPanel } from '@/components/finance/dimensions/source-document-dimension-panel';
import { toFinancePostingDimensionValues } from '@/lib/finance/source-document-dimensions';

const receiptSchema = z.object({
    transactionDate: z.date({ message: "Date is required" }),
    bankAccountId: z.string().min(1, "Bank account is required"),
    amount: z.number().min(0.01, "Amount must be greater than 0"),
    currency: z.string().min(1, "Currency is required"),
    exchangeRate: z.number().min(0.0001, "Exchange rate must be greater than 0"),
    exchangeRateId: z.string().optional(),
    paymentMethodId: z.string().optional(),
    referenceNumber: z.string().optional(),
    description: z.string().optional(),
    // Specific fields
    payerName: z.string().optional(),
    glAccountId: z.string().optional(), // For Direct Receipt
});

type ReceiptFormValues = z.infer<typeof receiptSchema>;

export default function RecordReceiptPage() {
    const router = useRouter();
    const { toast } = useToast();
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [sourceLineId] = useState(() => globalThis.crypto.randomUUID());
    const [defaultDimensionValues, setDefaultDimensionValues] = useState<Record<string, string>>({});
    const [lineDimensionValues, setLineDimensionValues] = useState<Record<string, Record<string, string>>>({});
    const [applyDefaultToAll, setApplyDefaultToAll] = useState(false);

    // Fetch data
    const { data: bankAccounts } = useQuery({
        queryKey: ['bank-accounts', 'active'],
        queryFn: () => cashManagementDataService.getActiveBankAccounts(),
    });

    const { data: paymentMethods } = useQuery({
        queryKey: ['payment-methods', 'active'],
        queryFn: () => cashManagementDataService.getActivePaymentMethods(),
    });

    const { data: glAccounts } = useQuery({
        queryKey: ['gl-accounts', 'active'],
        queryFn: () => financeDataService.getAccounts({ status: 'Active' }),
    });

    const { data: financeSettings } = useQuery({
        queryKey: ['finance-settings'],
        queryFn: () => financeService.getSettings(),
    });

    const form = useForm<ReceiptFormValues>({
        resolver: zodResolver(receiptSchema),
        defaultValues: {
            transactionDate: new Date(),
            amount: 0,
            currency: 'GHS',
            exchangeRate: 1,
            exchangeRateId: undefined,
        },
    });

    const selectedBankAccountId = form.watch('bankAccountId');
    const selectedGLAccountId = form.watch('glAccountId');
    const selectedGLAccount = glAccounts?.find(account => account.id === selectedGLAccountId);

    const openCustomerPaymentFlow = () => {
        const values = form.getValues();
        const params = new URLSearchParams();

        if (values.bankAccountId) params.set('bankAccountId', values.bankAccountId);
        if (values.paymentMethodId) params.set('paymentMethodId', values.paymentMethodId);
        if (values.amount > 0) params.set('amount', String(values.amount));
        if (values.referenceNumber) params.set('referenceNumber', values.referenceNumber);
        if (values.description) params.set('description', values.description);
        if (values.transactionDate) params.set('paymentDate', values.transactionDate.toISOString());

        const queryString = params.toString();
        router.push(`/finance/ar/receipts/new${queryString ? `?${queryString}` : ''}`);
    };

    // Update currency when bank account changes
    useEffect(() => {
        if (selectedBankAccountId && bankAccounts) {
            const account = bankAccounts.find(a => a.id === selectedBankAccountId);
            if (account) {
                form.setValue('currency', account.currency);
            }
        }
    }, [selectedBankAccountId, bankAccounts, form]);

    const watchedCurrency = form.watch('currency');
    const watchedTransactionDate = form.watch('transactionDate');
    const functionalCurrency = financeSettings?.baseCurrency || 'GHS';
    const [exchangeRateSource, setExchangeRateSource] = useState('Functional currency');

    useEffect(() => {
        if (!financeSettings || !watchedTransactionDate) return;
        form.setValue('exchangeRateId', undefined);
        setExchangeRateSource('Loading approved rate…');
        let cancelled = false;
        void loadApprovedCashRate(
            {
                transactionCurrency: watchedCurrency,
                functionalCurrency,
                transactionDate: watchedTransactionDate,
                settings: financeSettings,
            },
            (code, query) => financeService.getCurrentExchangeRate(code, query),
        ).then(snapshot => {
            if (cancelled) return;
            form.setValue('exchangeRate', snapshot.rate);
            form.setValue('exchangeRateId', snapshot.exchangeRateId);
            setExchangeRateSource(`${snapshot.source} · ${snapshot.quoteSide}`);
        }).catch(error => {
            if (cancelled) return;
            form.setValue('exchangeRate', 0);
            setExchangeRateSource(error instanceof Error ? error.message : 'Approved rate unavailable');
        });
        return () => { cancelled = true; };
    }, [financeSettings, form, functionalCurrency, watchedCurrency, watchedTransactionDate]);

    const onSubmit = async (data: ReceiptFormValues) => {
        setIsSubmitting(true);
        try {
            const selectedPaymentMethod = paymentMethods?.find((method) => method.id === data.paymentMethodId);
            if (selectedPaymentMethod?.requiresReference && !data.referenceNumber?.trim()) {
                form.setError('referenceNumber', { type: 'manual', message: `${selectedPaymentMethod.name} requires a reference number` });
                setIsSubmitting(false);
                return;
            }
            if (data.currency.toUpperCase() !== functionalCurrency && !data.exchangeRateId) {
                toast({ title: 'Approved exchange rate required', description: exchangeRateSource, variant: 'destructive' });
                return;
            }

            const payload: CreateCashReceiptDto = {
                transactionDate: data.transactionDate.toISOString(),
                bankAccountId: data.bankAccountId,
                amount: data.amount,
                currency: data.currency,
                exchangeRate: data.exchangeRate,
                exchangeRateId: data.exchangeRateId,
                paymentMethodId: data.paymentMethodId,
                referenceNumber: data.referenceNumber,
                description: data.description,
            };

            if (!data.glAccountId) {
                form.setError('glAccountId', { type: 'manual', message: 'GL Account is required for Direct Receipts' });
                setIsSubmitting(false);
                return;
            }
            payload.glAccountId = data.glAccountId;
            payload.payerName = data.payerName || 'Miscellaneous';
            payload.financeDimensions = {
                defaultDimensions: toFinancePostingDimensionValues(defaultDimensionValues),
                lines: [{
                    sourceLineId,
                    accountId: data.glAccountId,
                    dimensions: toFinancePostingDimensionValues(lineDimensionValues[sourceLineId] || {}),
                }],
                applyDefaultToEligibleLines: applyDefaultToAll,
            };

            await cashManagementDataService.createCashReceipt(payload);

            toast({
                title: "Receipt Recorded",
                description: "The cash receipt has been recorded successfully.",
            });

            router.push('/finance/cash/transactions');
            router.refresh();
        } catch (error: any) {
            console.error(error);
            toast({
                title: "Error",
                description: "Failed to record receipt. Please try again.",
                variant: "destructive",
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
                    <h1 className="text-3xl font-bold tracking-tight">Record Receipt</h1>
                    <p className="text-muted-foreground">
                        Record a cash or bank receipt.
                    </p>
                </div>
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                <div className="space-y-6 lg:col-span-2">
                    <Card>
                        <form onSubmit={form.handleSubmit(onSubmit)}>
                            <CardHeader>
                                <CardTitle>Receipt Details</CardTitle>
                                <CardDescription>Enter the details of the money received.</CardDescription>
                            </CardHeader>
                            <CardContent className="space-y-4">

                                <Tabs
                                    value="direct"
                                    onValueChange={(value) => {
                                        if (value === 'customer') {
                                            openCustomerPaymentFlow();
                                        }
                                    }}
                                    className="w-full"
                                >
                                    <TabsList className="grid w-full grid-cols-2">
                                        <TabsTrigger value="direct">Direct Receipt (GL)</TabsTrigger>
                                        <TabsTrigger value="customer">Customer Payment (AR)</TabsTrigger>
                                    </TabsList>
                                </Tabs>

                                <div className="grid grid-cols-2 gap-4">
                                    <div className="space-y-2">
                                        <Label>Transaction Date</Label>
                                        <Popover>
                                            <PopoverTrigger asChild>
                                                <Button
                                                    variant={"outline"}
                                                    className={cn(
                                                        "w-full justify-start text-left font-normal",
                                                        !form.watch('transactionDate') && "text-muted-foreground"
                                                    )}
                                                >
                                                    <CalendarIcon className="mr-2 h-4 w-4" />
                                                    {form.watch('transactionDate') ? format(form.watch('transactionDate'), "PPP") : <span>Pick a date</span>}
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
                                        <Input {...form.register('referenceNumber')} placeholder="e.g. RCPT-001" />
                                        {form.formState.errors.referenceNumber && <p className="text-sm text-red-500">{form.formState.errors.referenceNumber.message}</p>}
                                    </div>
                                </div>

                                <div className="space-y-2">
                                    <Label>Bank Account</Label>
                                    <Select
                                        onValueChange={(val) => form.setValue('bankAccountId', val)}
                                        defaultValue={form.watch('bankAccountId')}
                                    >
                                        <SelectTrigger>
                                            <SelectValue placeholder="Select bank account" />
                                        </SelectTrigger>
                                        <SelectContent>
                                            {bankAccounts?.map((account) => (
                                                <SelectItem key={account.id} value={account.id}>
                                                    {account.accountName} ({account.currency}) - {account.bankName}
                                                </SelectItem>
                                            ))}
                                        </SelectContent>
                                    </Select>
                                    {form.formState.errors.bankAccountId && <p className="text-sm text-red-500">{form.formState.errors.bankAccountId.message}</p>}
                                </div>

                                <div className="grid grid-cols-2 gap-4">
                                    <div className="space-y-2">
                                        <Label>Amount</Label>
                                        <div className="relative">
                                            <span className="absolute left-3 top-2.5 text-gray-500 text-sm font-medium">
                                                {form.watch('currency')}
                                            </span>
                                            <Input
                                                type="number"
                                                step="0.01"
                                                className="pl-12"
                                                {...form.register('amount', { valueAsNumber: true })}
                                            />
                                        </div>
                                        {form.formState.errors.amount && <p className="text-sm text-red-500">{form.formState.errors.amount.message}</p>}
                                    </div>
                                    <div className="space-y-2">
                                        <Label>Exchange Rate</Label>
                                        <Input
                                            type="number"
                                            step="0.000001"
                                            readOnly
                                            aria-readonly="true"
                                            {...form.register('exchangeRate', { valueAsNumber: true })}
                                        />
                                        <p className="text-xs text-muted-foreground">
                                            1 {form.watch('currency')} = {form.watch('exchangeRate') || 1} {functionalCurrency}
                                        </p>
                                        <p className="text-xs text-muted-foreground">{exchangeRateSource}</p>
                                        {form.formState.errors.exchangeRate && <p className="text-sm text-red-500">{form.formState.errors.exchangeRate.message}</p>}
                                    </div>
                                </div>

                                <div className="grid grid-cols-2 gap-4">
                                    <div className="space-y-2">
                                        <Label>Payment Method</Label>
                                        <Select
                                            onValueChange={(val) => form.setValue('paymentMethodId', val)}
                                            defaultValue={form.watch('paymentMethodId')}
                                        >
                                            <SelectTrigger>
                                                <SelectValue placeholder="Select method" />
                                            </SelectTrigger>
                                            <SelectContent>
                                                {paymentMethods?.map((method) => (
                                                    <SelectItem key={method.id} value={method.id}>
                                                        {method.name}
                                                    </SelectItem>
                                                ))}
                                            </SelectContent>
                                        </Select>
                                    </div>
                                </div>

                                <div className="space-y-2">
                                    <Label>Payer Name</Label>
                                    <Input {...form.register('payerName')} placeholder="From whom was received?" />
                                </div>
                                <div className="space-y-2">
                                    <Label>GL Account (Income/Revenue)</Label>
                                    <Select
                                        onValueChange={(val) => form.setValue('glAccountId', val)}
                                        defaultValue={form.watch('glAccountId')}
                                    >
                                        <SelectTrigger>
                                            <SelectValue placeholder="Select GL account" />
                                        </SelectTrigger>
                                        <SelectContent>
                                            {glAccounts?.map((account) => (
                                                <SelectItem key={account.id} value={account.id}>
                                                    {account.accountCode} - {account.accountName}
                                                </SelectItem>
                                            ))}
                                        </SelectContent>
                                    </Select>
                                    {form.formState.errors.glAccountId && <p className="text-sm text-red-500">{form.formState.errors.glAccountId.message}</p>}
                                </div>

                                <div className="space-y-2">
                                    <Label>Description</Label>
                                    <Textarea {...form.register('description')} placeholder="Additional notes..." />
                                </div>

                            </CardContent>
                            <CardFooter className="justify-end space-x-2">
                                <Button variant="ghost" type="button" onClick={() => router.back()}>Cancel</Button>
                                <Button type="submit" disabled={isSubmitting}>
                                    {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                                    Record Receipt
                                </Button>
                            </CardFooter>
                        </form>
                    </Card>
                    <Card>
                        <CardHeader>
                            <CardTitle>Finance coding dimensions</CardTitle>
                            <CardDescription>
                                The direct income/offset line is authoritative; the bank line resolves its own account rules.
                            </CardDescription>
                        </CardHeader>
                        <CardContent>
                            <SourceDocumentDimensionPanel
                                context={{
                                    sourceModule: 'CASHBANK',
                                    sourceDocumentType: 'CashBankReceipt',
                                    postingAction: 'Post',
                                    sourceRoute: 'finance.cash.receipts.direct',
                                    contractVersion: '1.0',
                                }}
                                effectiveDate={format(form.watch('transactionDate') || new Date(), 'yyyy-MM-dd')}
                                lines={selectedGLAccountId ? [{
                                    id: sourceLineId,
                                    accountId: selectedGLAccountId,
                                    accountLabel: selectedGLAccount
                                        ? `${selectedGLAccount.accountCode} - ${selectedGLAccount.accountName}`
                                        : 'Direct receipt offset',
                                }] : []}
                                defaultValues={defaultDimensionValues}
                                lineValues={lineDimensionValues}
                                onDefaultValuesChange={(values) => {
                                    setDefaultDimensionValues(values);
                                    setApplyDefaultToAll(false);
                                }}
                                onLineValuesChange={setLineDimensionValues}
                                onApplyDefaultToAll={() => setApplyDefaultToAll(true)}
                                disabled={isSubmitting}
                            />
                        </CardContent>
                    </Card>
                </div>

                <div className="space-y-6">
                    <Card className="bg-muted/50">
                        <CardHeader>
                            <CardTitle className="text-sm font-medium">Quick Guide</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-4 text-sm">
                            <div className="flex gap-2">
                                <FileText className="h-4 w-4 text-blue-500 flex-shrink-0" />
                                <div>
                                    <p className="font-medium">Direct Receipt</p>
                                    <p className="text-muted-foreground">Use for miscellaneous income, interest, or refunds where you want to credit a specific GL account directly.</p>
                                </div>
                            </div>
                            <div className="flex gap-2">
                                <User className="h-4 w-4 text-green-500 flex-shrink-0" />
                                <div>
                                    <p className="font-medium">Customer Payment</p>
                                    <p className="text-muted-foreground">Opens the Accounts Receivable payment flow for customer balances and invoice allocation.</p>
                                </div>
                            </div>
                        </CardContent>
                    </Card>
                </div>
            </div>
        </div>
    );
}
