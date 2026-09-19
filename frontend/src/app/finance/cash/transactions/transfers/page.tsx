'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { format } from 'date-fns';
import {
    ArrowLeft,
    ArrowRight,
    ArrowRightLeft,
    CalendarIcon,
    Calculator,
    Loader2,
    ShieldCheck,
} from 'lucide-react';

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
import { SourceDocumentDimensionPanel } from '@/components/finance/dimensions/source-document-dimension-panel';
import { toFinancePostingDimensionValues } from '@/lib/finance/source-document-dimensions';
import { cn } from '@/lib/utils';

import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import type {
    BankAccount,
    BankTransferPreview,
    CreateBankTransferDto,
} from '@/types/cash-management';

const transferSchema = z.object({
    transactionDate: z.date({ message: 'Date is required' }),
    fromBankAccountId: z.string().min(1, 'Source bank account is required'),
    toBankAccountId: z.string().min(1, 'Destination bank account is required'),
    amount: z.number().min(0.01, 'Source amount must be greater than 0'),
    destinationAmount: z.number().nonnegative('Destination amount cannot be negative'),
    referenceNumber: z.string().optional(),
    description: z.string().optional(),
}).refine((data) => data.fromBankAccountId !== data.toBankAccountId, {
    message: 'Destination account must be different from source account',
    path: ['toBankAccountId'],
});

type TransferFormValues = z.infer<typeof transferSchema>;

function formatMoney(value: number | undefined, currency = 'GHS') {
    return `${currency} ${(value ?? 0).toLocaleString('en-GH', {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    })}`;
}

function formatAccount(account: BankAccount) {
    return `${account.accountName} (${account.currency}) - ${account.bankName}`;
}

function buildPreviewRequest(
    data: Pick<TransferFormValues, 'transactionDate' | 'fromBankAccountId' | 'toBankAccountId' | 'amount' | 'destinationAmount'>
): CreateBankTransferDto {
    return {
        transactionDate: data.transactionDate.toISOString(),
        fromBankAccountId: data.fromBankAccountId,
        toBankAccountId: data.toBankAccountId,
        amount: data.amount,
        destinationAmount: data.destinationAmount > 0 ? data.destinationAmount : undefined,
    };
}

export default function RecordBankTransferPage() {
    const router = useRouter();
    const { toast } = useToast();
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [transferPairId, setTransferPairId] = useState('');
    const [sourceLineId] = useState(() => globalThis.crypto.randomUUID());
    const [destinationLineId] = useState(() => globalThis.crypto.randomUUID());
    const [defaultDimensionValues, setDefaultDimensionValues] = useState<Record<string, string>>({});
    const [lineDimensionValues, setLineDimensionValues] = useState<Record<string, Record<string, string>>>({});
    const [applyDefaultToAll, setApplyDefaultToAll] = useState(false);

    // The pair id is generated once per screen visit and reused if a network retry occurs. The
    // database's unique OUT/IN pair guard then returns the first transfer instead of duplicating it.
    useEffect(() => {
        setTransferPairId(globalThis.crypto.randomUUID());
    }, []);

    const { data: bankAccounts, isLoading: accountsLoading } = useQuery({
        queryKey: ['bank-accounts', 'active'],
        queryFn: () => cashManagementDataService.getActiveBankAccounts(),
    });

    const form = useForm<TransferFormValues>({
        resolver: zodResolver(transferSchema),
        defaultValues: {
            transactionDate: new Date(),
            fromBankAccountId: '',
            toBankAccountId: '',
            amount: 0,
            destinationAmount: 0,
            referenceNumber: '',
            description: '',
        },
    });

    const transactionDate = form.watch('transactionDate');
    const fromBankAccountId = form.watch('fromBankAccountId');
    const toBankAccountId = form.watch('toBankAccountId');
    const amount = form.watch('amount');
    const destinationAmount = form.watch('destinationAmount');

    const fromAccount = useMemo(
        () => bankAccounts?.find((account) => account.id === fromBankAccountId),
        [bankAccounts, fromBankAccountId]
    );
    const toAccount = useMemo(
        () => bankAccounts?.find((account) => account.id === toBankAccountId),
        [bankAccounts, toBankAccountId]
    );
    const destinationAccounts = useMemo(
        () => bankAccounts?.filter((account) => account.id !== fromBankAccountId) ?? [],
        [bankAccounts, fromBankAccountId]
    );
    const isCrossCurrency = Boolean(
        fromAccount && toAccount && fromAccount.currency !== toAccount.currency
    );

    useEffect(() => {
        if (fromBankAccountId === toBankAccountId && toBankAccountId) {
            form.setValue('toBankAccountId', '');
        }
    }, [form, fromBankAccountId, toBankAccountId]);

    useEffect(() => {
        // A previously quoted destination amount belongs to its old account pair. Clearing it
        // forces the API to derive a fresh indicative amount from the new approved rate set.
        form.setValue('destinationAmount', 0);
    }, [form, fromBankAccountId, toBankAccountId]);

    const previewQuery = useQuery<BankTransferPreview>({
        queryKey: [
            'bank-transfer-preview',
            transactionDate?.toISOString(),
            fromBankAccountId,
            toBankAccountId,
            amount,
            destinationAmount,
        ],
        queryFn: () => cashManagementDataService.previewBankTransfer(buildPreviewRequest({
            transactionDate,
            fromBankAccountId,
            toBankAccountId,
            amount,
            destinationAmount,
        })),
        enabled: Boolean(
            transactionDate
            && fromBankAccountId
            && toBankAccountId
            && fromBankAccountId !== toBankAccountId
            && amount > 0
        ),
        retry: false,
        staleTime: 15_000,
    });

    useEffect(() => {
        if (previewQuery.data?.destinationAmountWasDerived && destinationAmount <= 0) {
            // The derived value is a starting quotation, not a hidden accounting assumption.
            // It is written into the form so Finance must see and submit the actual amount.
            form.setValue('destinationAmount', previewQuery.data.destinationAmount, {
                shouldValidate: true,
            });
        }
    }, [destinationAmount, form, previewQuery.data]);

    const onSubmit = async (data: TransferFormValues) => {
        if (isCrossCurrency && data.destinationAmount <= 0) {
            form.setError('destinationAmount', {
                message: 'Confirm the destination amount for a cross-currency transfer',
            });
            return;
        }

        const preview = previewQuery.data;
        if (!preview || previewQuery.isError) {
            toast({
                title: 'Valuation required',
                description: 'Resolve the transfer preview before recording this transfer.',
                variant: 'destructive',
            });
            return;
        }
        if (!fromAccount?.glAccountId || !toAccount?.glAccountId) {
            toast({
                title: 'Bank ledger mapping required',
                description: 'Both transfer accounts must have their own Finance GL account before dimensions can be captured.',
                variant: 'destructive',
            });
            return;
        }

        setIsSubmitting(true);
        try {
            const payload: CreateBankTransferDto = {
                ...buildPreviewRequest(data),
                transferPairId: transferPairId || undefined,
                // Sending the exact approved records from the preview prevents a rate changing
                // between the user's review and the server's final capture validation.
                sourceExchangeRateId: preview.sourceExchangeRateId,
                destinationExchangeRateId: preview.destinationExchangeRateId,
                referenceNumber: data.referenceNumber?.trim() || undefined,
                description: data.description?.trim() || undefined,
                financeDimensions: {
                    defaultDimensions: toFinancePostingDimensionValues(defaultDimensionValues),
                    lines: [
                        {
                            sourceLineId,
                            accountId: fromAccount.glAccountId,
                            dimensions: toFinancePostingDimensionValues(lineDimensionValues[sourceLineId] || {}),
                        },
                        {
                            sourceLineId: destinationLineId,
                            accountId: toAccount.glAccountId,
                            dimensions: toFinancePostingDimensionValues(lineDimensionValues[destinationLineId] || {}),
                        },
                    ],
                    applyDefaultToEligibleLines: applyDefaultToAll,
                },
            };

            await cashManagementDataService.createBankTransfer(payload);
            toast({
                title: 'Transfer recorded',
                description: preview.isCrossCurrency
                    ? `The ${preview.sourceCurrency}/${preview.destinationCurrency} pair and its FX valuation were captured for approval.`
                    : 'The paired bank transfer was captured for approval.',
            });
            router.push('/finance/cash/transactions');
            router.refresh();
        } catch (error) {
            console.error(error);
            toast({
                title: 'Transfer not recorded',
                description: error instanceof Error
                    ? error.message
                    : 'Review the transfer valuation and try again.',
                variant: 'destructive',
            });
        } finally {
            setIsSubmitting(false);
        }
    };

    const preview = previewQuery.data;
    const previewError = previewQuery.error instanceof Error
        ? previewQuery.error.message
        : 'The approved rate preview could not be calculated.';

    return (
        <div className="space-y-6 p-8 max-w-[1280px] mx-auto">
            <div className="flex items-center space-x-4">
                <Button variant="ghost" size="icon" onClick={() => router.back()}>
                    <ArrowLeft className="h-4 w-4" />
                </Button>
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Bank Transfer</h1>
                    <p className="text-muted-foreground">
                        Move funds between TDC bank accounts with controlled FX valuation.
                    </p>
                </div>
            </div>

            <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
                <div className="space-y-6 lg:col-span-2">
                    <Card>
                        <form onSubmit={form.handleSubmit(onSubmit)}>
                            <CardHeader>
                                <CardTitle>Transfer details</CardTitle>
                                <CardDescription>
                                    Each bank keeps its own currency amount; the system values both legs in the functional currency.
                                </CardDescription>
                            </CardHeader>
                            <CardContent className="space-y-5">
                                <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                                    <div className="space-y-2">
                                        <Label>Transaction date</Label>
                                        <Popover>
                                            <PopoverTrigger asChild>
                                                <Button
                                                    variant="outline"
                                                    className={cn(
                                                        'w-full justify-start text-left font-normal',
                                                        !transactionDate && 'text-muted-foreground'
                                                    )}
                                                >
                                                    <CalendarIcon className="mr-2 h-4 w-4" />
                                                    {transactionDate ? format(transactionDate, 'PPP') : 'Pick a date'}
                                                </Button>
                                            </PopoverTrigger>
                                            <PopoverContent className="w-auto p-0">
                                                <Calendar
                                                    mode="single"
                                                    selected={transactionDate}
                                                    onSelect={(date) => date && form.setValue('transactionDate', date)}
                                                    initialFocus
                                                />
                                            </PopoverContent>
                                        </Popover>
                                    </div>
                                    <div className="space-y-2">
                                        <Label>Bank reference</Label>
                                        <Input {...form.register('referenceNumber')} placeholder="e.g. SWIFT / bank advice reference" />
                                    </div>
                                </div>

                                <div className="grid grid-cols-1 items-start gap-4 md:grid-cols-[1fr_auto_1fr]">
                                    <div className="space-y-2">
                                        <Label>From bank account</Label>
                                        <Select
                                            onValueChange={(value) => form.setValue('fromBankAccountId', value)}
                                            value={fromBankAccountId}
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

                                    <div className="hidden h-10 items-center justify-center pt-8 md:flex">
                                        <ArrowRight className="h-5 w-5 text-muted-foreground" />
                                    </div>

                                    <div className="space-y-2">
                                        <Label>To bank account</Label>
                                        <Select
                                            onValueChange={(value) => form.setValue('toBankAccountId', value)}
                                            value={toBankAccountId}
                                            disabled={accountsLoading || !fromBankAccountId}
                                        >
                                            <SelectTrigger>
                                                <SelectValue placeholder={!fromBankAccountId ? 'Select source first' : 'Select destination account'} />
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

                                <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                                    <div className="space-y-2">
                                        <Label>Amount leaving source</Label>
                                        <div className="relative">
                                            <span className="absolute left-3 top-2.5 text-sm font-medium text-gray-500">
                                                {fromAccount?.currency ?? '---'}
                                            </span>
                                            <Input
                                                type="number"
                                                min="0"
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
                                        <Label>Amount arriving at destination</Label>
                                        <div className="relative">
                                            <span className="absolute left-3 top-2.5 text-sm font-medium text-gray-500">
                                                {toAccount?.currency ?? '---'}
                                            </span>
                                            <Input
                                                type="number"
                                                min="0"
                                                step="0.01"
                                                className="pl-12"
                                                disabled={!isCrossCurrency}
                                                {...form.register('destinationAmount', { valueAsNumber: true })}
                                                value={isCrossCurrency ? destinationAmount : (amount || 0)}
                                            />
                                        </div>
                                        <p className="text-xs text-muted-foreground">
                                            {isCrossCurrency
                                                ? 'Confirm the amount stated on the bank conversion advice.'
                                                : 'Same-currency transfers move an equal amount.'}
                                        </p>
                                        {form.formState.errors.destinationAmount && (
                                            <p className="text-sm text-red-500">{form.formState.errors.destinationAmount.message}</p>
                                        )}
                                    </div>
                                </div>

                                <div className="space-y-2">
                                    <Label>Description</Label>
                                    <Textarea {...form.register('description')} placeholder="Purpose of transfer and supporting advice details..." />
                                </div>
                            </CardContent>
                            <CardFooter className="justify-end space-x-2">
                                <Button variant="ghost" type="button" onClick={() => router.back()}>
                                    Cancel
                                </Button>
                                <Button
                                    type="submit"
                                    disabled={isSubmitting || previewQuery.isFetching || !preview || previewQuery.isError}
                                >
                                    {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                                    Record transfer
                                </Button>
                            </CardFooter>
                        </form>
                    </Card>
                    <Card>
                        <CardHeader>
                            <CardTitle>Finance coding dimensions</CardTitle>
                            <CardDescription>
                                Code each bank leg independently. Defaults are resolved against each leg&apos;s own account rules.
                            </CardDescription>
                        </CardHeader>
                        <CardContent>
                            <SourceDocumentDimensionPanel
                                context={{
                                    sourceModule: 'CASHBANK',
                                    sourceDocumentType: 'CashBankTransfer',
                                    postingAction: 'Post',
                                    sourceRoute: 'finance.cash.bank-transfers',
                                    contractVersion: '1.0',
                                }}
                                effectiveDate={format(transactionDate || new Date(), 'yyyy-MM-dd')}
                                lines={[
                                    ...(fromAccount?.glAccountId ? [{
                                        id: sourceLineId,
                                        accountId: fromAccount.glAccountId,
                                        accountLabel: `Source · ${formatAccount(fromAccount)}`,
                                    }] : []),
                                    ...(toAccount?.glAccountId ? [{
                                        id: destinationLineId,
                                        accountId: toAccount.glAccountId,
                                        accountLabel: `Destination · ${formatAccount(toAccount)}`,
                                    }] : []),
                                ]}
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
                            <CardTitle className="flex items-center gap-2 text-sm font-medium">
                                <Calculator className="h-4 w-4 text-blue-600" />
                                Controlled valuation preview
                            </CardTitle>
                            <CardDescription>
                                Approved tenant rates selected by the posting policy.
                            </CardDescription>
                        </CardHeader>
                        <CardContent className="space-y-4 text-sm">
                            {!preview && !previewQuery.isFetching && !previewQuery.isError && (
                                <p className="text-muted-foreground">Select both accounts and enter a source amount.</p>
                            )}
                            {previewQuery.isFetching && (
                                <div className="flex items-center gap-2 text-muted-foreground">
                                    <Loader2 className="h-4 w-4 animate-spin" />
                                    Resolving approved rates...
                                </div>
                            )}
                            {previewQuery.isError && (
                                <div className="rounded-md border border-red-200 bg-red-50 p-3 text-red-700">
                                    {previewError}
                                </div>
                            )}
                            {preview && (
                                <>
                                    <div className="flex gap-2">
                                        <ArrowRightLeft className="h-4 w-4 flex-shrink-0 text-blue-500" />
                                        <div className="min-w-0">
                                            <p className="font-medium">Account movement</p>
                                            <p className="break-words text-muted-foreground">
                                                {preview.fromBankAccountName} to {preview.toBankAccountName}
                                            </p>
                                        </div>
                                    </div>

                                    <div className="space-y-2 rounded-md border bg-background p-3">
                                        <div className="flex items-center justify-between gap-3">
                                            <span className="text-muted-foreground">Leaves source</span>
                                            <span className="font-medium text-red-600">
                                                {formatMoney(preview.sourceAmount, preview.sourceCurrency)}
                                            </span>
                                        </div>
                                        <div className="flex items-center justify-between gap-3">
                                            <span className="text-muted-foreground">Arrives destination</span>
                                            <span className="font-medium text-green-600">
                                                {formatMoney(preview.destinationAmount, preview.destinationCurrency)}
                                            </span>
                                        </div>
                                        {preview.isCrossCurrency && (
                                            <div className="flex items-center justify-between gap-3 border-t pt-2">
                                                <span className="text-muted-foreground">Cross rate</span>
                                                <span className="font-mono text-xs">
                                                    1 {preview.sourceCurrency} = {preview.crossRate.toFixed(8)} {preview.destinationCurrency}
                                                </span>
                                            </div>
                                        )}
                                    </div>

                                    <RateSnapshot
                                        label="Source valuation"
                                        currency={preview.sourceCurrency}
                                        functionalCurrency={preview.functionalCurrency}
                                        rate={preview.sourceExchangeRate}
                                        rateSource={preview.sourceExchangeRateSource}
                                        rateDate={preview.sourceExchangeRateDate}
                                        quoteSide={preview.sourceExchangeRateQuoteSide}
                                        baseAmount={preview.sourceBaseAmount}
                                    />
                                    <RateSnapshot
                                        label="Destination valuation"
                                        currency={preview.destinationCurrency}
                                        functionalCurrency={preview.functionalCurrency}
                                        rate={preview.destinationExchangeRate}
                                        rateSource={preview.destinationExchangeRateSource}
                                        rateDate={preview.destinationExchangeRateDate}
                                        quoteSide={preview.destinationExchangeRateQuoteSide}
                                        baseAmount={preview.destinationBaseAmount}
                                    />

                                    <div className={cn(
                                        'rounded-md border p-3',
                                        preview.realizedFxOutcome === 'Gain' && 'border-green-200 bg-green-50',
                                        preview.realizedFxOutcome === 'Loss' && 'border-amber-200 bg-amber-50',
                                        preview.realizedFxOutcome === 'None' && 'bg-background'
                                    )}>
                                        <div className="flex items-center justify-between gap-3">
                                            <span className="font-medium">Projected realised FX</span>
                                            <span className="font-semibold">
                                                {preview.realizedFxOutcome}: {formatMoney(
                                                    Math.abs(preview.realizedFxGainLossBaseAmount),
                                                    preview.functionalCurrency
                                                )}
                                            </span>
                                        </div>
                                        <p className="mt-1 text-xs text-muted-foreground">
                                            Posted automatically to the configured realised FX {preview.realizedFxOutcome === 'Loss' ? 'loss' : 'gain'} account.
                                        </p>
                                    </div>

                                    <div className="flex gap-2 rounded-md border border-blue-200 bg-blue-50 p-3 text-xs text-blue-800">
                                        <ShieldCheck className="h-4 w-4 flex-shrink-0" />
                                        <p>
                                            Both bank legs retain their own amount and approved rate evidence, and reconcile independently to their bank statements.
                                        </p>
                                    </div>
                                </>
                            )}
                        </CardContent>
                    </Card>
                </div>
            </div>
        </div>
    );
}

function RateSnapshot({
    label,
    currency,
    functionalCurrency,
    rate,
    rateSource,
    rateDate,
    quoteSide,
    baseAmount,
}: {
    label: string;
    currency: string;
    functionalCurrency: string;
    rate: number;
    rateSource: string;
    rateDate: string;
    quoteSide: string;
    baseAmount: number;
}) {
    return (
        <div className="space-y-1 rounded-md border bg-background p-3">
            <div className="flex items-center justify-between gap-3">
                <span className="font-medium">{label}</span>
                <span className="font-mono text-xs">1 {currency} = {rate.toFixed(6)} {functionalCurrency}</span>
            </div>
            <div className="flex items-center justify-between gap-3 text-xs text-muted-foreground">
                <span>{rateSource} · {quoteSide}</span>
                <span>{new Date(rateDate).toLocaleDateString('en-GH')}</span>
            </div>
            <div className="flex items-center justify-between gap-3 border-t pt-1 text-xs">
                <span className="text-muted-foreground">Functional value</span>
                <span className="font-medium">{formatMoney(baseAmount, functionalCurrency)}</span>
            </div>
        </div>
    );
}
