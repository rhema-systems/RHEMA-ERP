'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { format } from 'date-fns';
import { CalendarIcon, Loader2, ArrowLeft, Building, FileText, Wallet } from 'lucide-react';

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
    TabsContent,
    TabsList,
    TabsTrigger,
} from "@/components/ui/tabs"
import { useToast } from '@/components/ui/use-toast';
import { cn } from '@/lib/utils';

import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import { financeService } from '@/services/finance.service';
import { CreateCashPaymentDto } from '@/types/cash-management';

const paymentSchema = z.object({
    transactionDate: z.date({ message: "Date is required" }),
    bankAccountId: z.string().min(1, "Bank account is required"),
    amount: z.number().min(0.01, "Amount must be greater than 0"),
    currency: z.string().min(1, "Currency is required"),
    exchangeRate: z.number().min(0.0001, "Exchange rate must be greater than 0"),
    paymentMethodId: z.string().optional(),
    referenceNumber: z.string().optional(),
    description: z.string().optional(),
    // Specific fields
    payeeName: z.string().optional(),
    glAccountId: z.string().optional(),
    // Vendor specific (placeholder)
    vendorName: z.string().optional(),
});

type PaymentFormValues = z.infer<typeof paymentSchema>;

export default function RecordPaymentPage() {
    const router = useRouter();
    const { toast } = useToast();
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [transactionType, setTransactionType] = useState<"direct" | "vendor">("direct");

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

    const form = useForm<PaymentFormValues>({
        resolver: zodResolver(paymentSchema),
        defaultValues: {
            transactionDate: new Date(),
            amount: 0,
            currency: 'GHS',
            exchangeRate: 1,
        },
    });

    const selectedBankAccountId = form.watch('bankAccountId');

    useEffect(() => {
        if (selectedBankAccountId && bankAccounts) {
            const account = bankAccounts.find(a => a.id === selectedBankAccountId);
            if (account) {
                form.setValue('currency', account.currency);
                if (account.currency === 'GHS') {
                    form.setValue('exchangeRate', 1);
                } else {
                    void financeService.getCurrentExchangeRate(account.currency)
                        .then((rate) => form.setValue('exchangeRate', Number(rate.currentExchangeRate ?? rate.rate ?? 1)))
                        .catch(() => form.setValue('exchangeRate', 1));
                }
            }
        }
    }, [selectedBankAccountId, bankAccounts, form]);

    const onSubmit = async (data: PaymentFormValues) => {
        setIsSubmitting(true);
        try {
            const payload: CreateCashPaymentDto = {
                transactionDate: data.transactionDate.toISOString(),
                bankAccountId: data.bankAccountId,
                amount: data.amount,
                currency: data.currency,
                exchangeRate: data.exchangeRate,
                paymentMethodId: data.paymentMethodId,
                referenceNumber: data.referenceNumber,
                description: data.description,
            };

            if (transactionType === 'direct') {
                if (!data.glAccountId) {
                    form.setError('glAccountId', { type: 'manual', message: 'GL Account is required for Direct Payments' });
                    setIsSubmitting(false);
                    return;
                }
                payload.glAccountId = data.glAccountId;
                payload.payeeName = data.payeeName || 'Miscellaneous';
            } else {
                // Vendor Payment Logic (Placeholder)
                if (!data.vendorName) {
                    form.setError('vendorName', { type: 'manual', message: 'Vendor Name is required' });
                    setIsSubmitting(false);
                    return;
                }
                payload.payeeName = data.vendorName;
                payload.description = `Vendor Payment: ${data.vendorName} - ${data.description || ''}`;
                // linking to AP control account?
                // For now, we'll let the backend or future logic handle GL mapping if not provided, 
                // or user can select AP Account if they know it in Direct mode. 
                // But for "Vendor" mode, we primarily capture the name.
            }

            await cashManagementDataService.createCashPayment(payload);

            toast({
                title: "Payment Recorded",
                description: "The cash payment has been recorded successfully.",
            });

            router.push('/finance/cash/transactions');
            router.refresh(); // Refresh server components
        } catch (error: any) {
            console.error(error);
            toast({
                title: "Error",
                description: "Failed to record payment. Please try again.",
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
                    <h1 className="text-3xl font-bold tracking-tight">Record Payment</h1>
                    <p className="text-muted-foreground">
                        Record a cash or bank payment.
                    </p>
                </div>
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                <div className="lg:col-span-2">
                    <Card>
                        <form onSubmit={form.handleSubmit(onSubmit)}>
                            <CardHeader>
                                <CardTitle>Payment Details</CardTitle>
                                <CardDescription>Enter the details of the money paid out.</CardDescription>
                            </CardHeader>
                            <CardContent className="space-y-4">

                                <Tabs value={transactionType} onValueChange={(v) => setTransactionType(v as any)} className="w-full">
                                    <TabsList className="grid w-full grid-cols-2">
                                        <TabsTrigger value="direct">Direct Payment (GL)</TabsTrigger>
                                        <TabsTrigger value="vendor">Vendor Payment (AP)</TabsTrigger>
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
                                        <Input {...form.register('referenceNumber')} placeholder="e.g. CHQ-001" />
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
                                            disabled={form.watch('currency') === 'GHS'}
                                            {...form.register('exchangeRate', { valueAsNumber: true })}
                                        />
                                        <p className="text-xs text-muted-foreground">
                                            1 {form.watch('currency')} = {form.watch('exchangeRate') || 1} GHS
                                        </p>
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

                                {transactionType === 'direct' ? (
                                    <>
                                        <div className="space-y-2">
                                            <Label>Payee Name</Label>
                                            <Input {...form.register('payeeName')} placeholder="To whom was paid?" />
                                        </div>
                                        <div className="space-y-2">
                                            <Label>GL Account (Expense/Liability)</Label>
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
                                    </>
                                ) : (
                                    <div className="space-y-2">
                                        <Label>Vendor Name</Label>
                                        <Input {...form.register('vendorName')} placeholder="Enter vendor name" />
                                        <p className="text-xs text-muted-foreground">Select the vendor (AP) being paid.</p>
                                        {form.formState.errors.vendorName && <p className="text-sm text-red-500">{form.formState.errors.vendorName.message}</p>}
                                    </div>
                                )}

                                <div className="space-y-2">
                                    <Label>Description</Label>
                                    <Textarea {...form.register('description')} placeholder="Additional notes..." />
                                </div>

                            </CardContent>
                            <CardFooter className="justify-end space-x-2">
                                <Button variant="ghost" type="button" onClick={() => router.back()}>Cancel</Button>
                                <Button type="submit" disabled={isSubmitting}>
                                    {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                                    Record Payment
                                </Button>
                            </CardFooter>
                        </form>
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
                                    <p className="font-medium">Direct Payment</p>
                                    <p className="text-muted-foreground">Use for petty cash expenses, bank charges, or one-off payments to non-vendors.</p>
                                </div>
                            </div>
                            <div className="flex gap-2">
                                <Building className="h-4 w-4 text-orange-500 flex-shrink-0" />
                                <div>
                                    <p className="font-medium">Vendor Payment (AP)</p>
                                    <p className="text-muted-foreground">Use to pay registered vendors. Future Update: This will link to Accounts Payable.</p>
                                </div>
                            </div>
                        </CardContent>
                    </Card>
                </div>
            </div>
        </div>
    );
}
