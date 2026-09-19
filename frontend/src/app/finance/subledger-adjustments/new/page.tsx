'use client';

import { useEffect, useMemo, useRef, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { ArrowLeft, Check, ChevronsUpDown, FileText, Loader2 } from 'lucide-react';
import { useQuery } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/use-toast';
import { arService } from '@/services/ar-service';
import { businessPartnerService, type BusinessPartnerDto } from '@/services/businessPartnerService';
import { financeDataService } from '@/services/finance/finance-data.service';
import { financeService } from '@/services/finance.service';
import { cn, formatCurrency } from '@/lib/utils';
import type { Account, Currency, SubledgerAdjustmentType, SubledgerModule } from '@/types/finance';
import type { Customer } from '@/types/ar';

const moduleSchema = z.enum(['AR', 'AP']);
const purposeSchema = z.literal('StandardAdjustment');

const adjustmentSchema = z.object({
    module: moduleSchema,
    purpose: purposeSchema,
    customerId: z.string().optional(),
    supplierId: z.string().optional(),
    adjustmentDate: z.string().min(1, 'Adjustment date is required'),
    dueDate: z.string().optional(),
    adjustmentType: z.enum(['Debit', 'Credit']),
    amount: z.number().min(0.01, 'Amount must be greater than zero'),
    currencyCode: z.string().min(3, 'Currency is required').max(3, 'Use a 3-letter currency code'),
    exchangeRate: z.number().min(0.000001, 'Exchange rate must be greater than zero'),
    contraAccountId: z.string().min(1, 'Contra account is required'),
    reference: z.string().max(100).optional(),
    reason: z.string().min(1, 'Reason is required').max(500, 'Reason cannot exceed 500 characters'),
    notes: z.string().max(2000).optional(),
}).superRefine((value, ctx) => {
    if (value.module === 'AR' && !value.customerId) {
        ctx.addIssue({
            code: z.ZodIssueCode.custom,
            path: ['customerId'],
            message: 'Customer is required',
        });
    }

    if (value.module === 'AP' && !value.supplierId) {
        ctx.addIssue({
            code: z.ZodIssueCode.custom,
            path: ['supplierId'],
            message: 'Supplier is required',
        });
    }

});

type AdjustmentFormValues = z.infer<typeof adjustmentSchema>;

interface SearchOption {
    id: string;
    label: string;
    secondary?: string;
}

function todayAsInputValue() {
    return new Date().toISOString().slice(0, 10);
}

function normalizeModule(value: string | null): SubledgerModule {
    return value?.toUpperCase() === 'AP' ? 'AP' : 'AR';
}

function isPostingAccount(account: Account) {
    const postingAllowed = account.isPostingAllowed ?? account.allowDirectPosting;
    return account.status === 'Active' && postingAllowed !== false && !account.isControlAccount;
}

function normalizeCurrencyCode(value?: string | null) {
    return String(value || '').trim().toUpperCase();
}

function readExchangeRateValue(rate: { currentExchangeRate?: number; rate?: number } | null | undefined) {
    const value = Number(rate?.currentExchangeRate ?? rate?.rate);
    return Number.isFinite(value) && value > 0 ? value : null;
}

export default function NewSubledgerAdjustmentPage() {
    const router = useRouter();
    const searchParams = useSearchParams();
    const initialModule = normalizeModule(searchParams.get('module'));
    const { toast } = useToast();
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [partnerOpen, setPartnerOpen] = useState(false);
    const [currencyOpen, setCurrencyOpen] = useState(false);
    const [isRateLoading, setIsRateLoading] = useState(false);
    const [accountOpen, setAccountOpen] = useState(false);
    const rateRequestRef = useRef(0);

    const form = useForm<AdjustmentFormValues>({
        resolver: zodResolver(adjustmentSchema),
        defaultValues: {
            module: initialModule,
            purpose: 'StandardAdjustment',
            customerId: '',
            supplierId: '',
            adjustmentDate: todayAsInputValue(),
            dueDate: '',
            adjustmentType: initialModule === 'AP' ? 'Credit' : 'Debit',
            amount: 0,
            currencyCode: 'GHS',
            exchangeRate: 1,
            contraAccountId: '',
            reference: '',
            reason: '',
            notes: '',
        },
    });

    const selectedModule = form.watch('module');
    const adjustmentType = form.watch('adjustmentType');
    const amount = Number(form.watch('amount')) || 0;
    const currencyCode = form.watch('currencyCode') || 'GHS';
    const selectedCustomerId = form.watch('customerId');
    const selectedSupplierId = form.watch('supplierId');
    const selectedAccountId = form.watch('contraAccountId');

    const { data: customersData, isLoading: customersLoading } = useQuery({
        queryKey: ['subledger-adjustment-customers'],
        queryFn: () => arService.getCustomers({ pageSize: 200, includeBalances: true, status: 'Active' }),
        enabled: selectedModule === 'AR',
    });

    const { data: suppliers, isLoading: suppliersLoading } = useQuery({
        queryKey: ['subledger-adjustment-suppliers'],
        queryFn: () => businessPartnerService.getActivePartners(),
        enabled: selectedModule === 'AP',
    });

    const { data: accounts, isLoading: accountsLoading } = useQuery({
        queryKey: ['subledger-adjustment-contra-accounts'],
        queryFn: () => financeDataService.getAccounts({ status: 'Active', pageSize: 500 }),
    });

    const { data: currencies, isLoading: currenciesLoading } = useQuery({
        queryKey: ['subledger-adjustment-currencies'],
        queryFn: () => financeDataService.getCurrencies({ isActive: true }),
    });

    const customers = customersData?.items ?? [];
    const activeCurrencies = useMemo(() => {
        return (currencies ?? [])
            .filter((currency: Currency) => currency.isActive !== false && normalizeCurrencyCode(currency.currencyCode))
            .sort((a, b) => {
                if (a.isBaseCurrency !== b.isBaseCurrency) return a.isBaseCurrency ? -1 : 1;
                return normalizeCurrencyCode(a.currencyCode).localeCompare(normalizeCurrencyCode(b.currencyCode));
            });
    }, [currencies]);

    const baseCurrencyCode = useMemo(() => {
        return normalizeCurrencyCode(activeCurrencies.find((currency) => currency.isBaseCurrency)?.currencyCode) || 'GHS';
    }, [activeCurrencies]);

    const supplierOptions = useMemo(() => {
        return (suppliers ?? [])
            .filter((partner: BusinessPartnerDto) =>
                ['supplier', 'contractor', 'both'].includes((partner.partnerType ?? '').toLowerCase()) &&
                !partner.isBlacklisted
            )
            .sort((a, b) => (a.partnerName || '').localeCompare(b.partnerName || ''));
    }, [suppliers]);

    const postingAccounts = useMemo(() => {
        return (accounts ?? [])
            .filter(isPostingAccount)
            .sort((a, b) => `${a.accountNumber || a.accountCode}`.localeCompare(`${b.accountNumber || b.accountCode}`));
    }, [accounts]);

    const partnerOptions: SearchOption[] = selectedModule === 'AR'
        ? customers.map((customer: Customer) => ({
            id: customer.id,
            label: customer.customerName,
            secondary: customer.customerCode,
        }))
        : supplierOptions.map((supplier) => ({
            id: supplier.id,
            label: supplier.partnerName || supplier.companyName || 'Unnamed supplier',
            secondary: supplier.partnerCode,
        }));

    const accountOptions: SearchOption[] = postingAccounts.map((account) => ({
        id: account.id,
        label: account.accountName,
        secondary: account.accountNumber || account.accountCode,
    }));

    const currencyOptions: SearchOption[] = useMemo(() => {
        const options: SearchOption[] = activeCurrencies.map((currency) => ({
            id: normalizeCurrencyCode(currency.currencyCode),
            label: normalizeCurrencyCode(currency.currencyCode),
            secondary: currency.currencyName,
        }));
        const selectedCode = normalizeCurrencyCode(currencyCode);
        if (selectedCode && !options.some((option) => option.id === selectedCode)) {
            options.unshift({ id: selectedCode, label: selectedCode });
        }
        if (baseCurrencyCode && !options.some((option) => option.id === baseCurrencyCode)) {
            options.unshift({ id: baseCurrencyCode, label: baseCurrencyCode });
        }
        return options;
    }, [activeCurrencies, baseCurrencyCode, currencyCode]);

    const selectedPartner = partnerOptions.find((option) =>
        option.id === (selectedModule === 'AR' ? selectedCustomerId : selectedSupplierId)
    );
    const selectedCurrency = currencyOptions.find((option) => option.id === normalizeCurrencyCode(currencyCode));
    const selectedAccount = accountOptions.find((option) => option.id === selectedAccountId);
    const selectedCustomer = customers.find((customer) => customer.id === selectedCustomerId);
    const selectedSupplier = supplierOptions.find((supplier) => supplier.id === selectedSupplierId);

    const applyCurrency = async (value?: string | null) => {
        const nextCurrencyCode = normalizeCurrencyCode(value) || baseCurrencyCode;
        form.setValue('currencyCode', nextCurrencyCode, { shouldDirty: true, shouldValidate: true });
        form.clearErrors('exchangeRate');

        const requestId = ++rateRequestRef.current;
        if (nextCurrencyCode === baseCurrencyCode) {
            form.setValue('exchangeRate', 1, { shouldDirty: true, shouldValidate: true });
            setIsRateLoading(false);
            return;
        }

        setIsRateLoading(true);
        try {
            const rate = await financeService.getCurrentExchangeRate(nextCurrencyCode);
            if (requestId !== rateRequestRef.current) return;

            const exchangeRate = readExchangeRateValue(rate);
            if (!exchangeRate) {
                throw new Error(`No current exchange rate found for ${nextCurrencyCode}.`);
            }

            form.setValue('exchangeRate', exchangeRate, { shouldDirty: true, shouldValidate: true });
        } catch (error) {
            if (requestId !== rateRequestRef.current) return;

            form.setValue('exchangeRate', 1, { shouldDirty: true, shouldValidate: true });
            form.setError('exchangeRate', {
                type: 'manual',
                message: `No current exchange rate was found for ${nextCurrencyCode}. Enter the rate manually before posting.`,
            });
            toast({
                title: 'Exchange rate not found',
                description: error instanceof Error
                    ? error.message
                    : `Enter the exchange rate for ${nextCurrencyCode} manually.`,
                variant: 'destructive',
            });
        } finally {
            if (requestId === rateRequestRef.current) {
                setIsRateLoading(false);
            }
        }
    };

    useEffect(() => {
        const currentCurrencyCode = normalizeCurrencyCode(form.getValues('currencyCode'));
        if (currentCurrencyCode === 'GHS' && baseCurrencyCode !== 'GHS' && !selectedCustomerId && !selectedSupplierId) {
            form.setValue('currencyCode', baseCurrencyCode, { shouldValidate: true });
        }
    }, [baseCurrencyCode, form, selectedCustomerId, selectedSupplierId]);

    useEffect(() => {
        form.setValue(selectedModule === 'AR' ? 'supplierId' : 'customerId', '');
        form.setValue('adjustmentType', selectedModule === 'AP' ? 'Credit' : 'Debit');
    }, [form, selectedModule]);

    const signedSubledgerAmount = selectedModule === 'AR'
        ? (adjustmentType === 'Debit' ? amount : -amount)
        : (adjustmentType === 'Credit' ? amount : -amount);
    const controlLineType = adjustmentType;
    const contraLineType: SubledgerAdjustmentType = adjustmentType === 'Debit' ? 'Credit' : 'Debit';
    const baseAmount = amount * (Number(form.watch('exchangeRate')) || 0);

    const handleModuleChange = (value: SubledgerModule) => {
        form.setValue('module', value);
        setPartnerOpen(false);
        void applyCurrency(baseCurrencyCode);
    };

    const handlePartnerSelect = (id: string) => {
        if (selectedModule === 'AR') {
            const customer = customers.find((item) => item.id === id);
            form.setValue('customerId', id, { shouldValidate: true });
            form.setValue('supplierId', '');
            void applyCurrency(customer?.currencyCode);
        } else {
            const supplier = supplierOptions.find((item) => item.id === id);
            form.setValue('supplierId', id, { shouldValidate: true });
            form.setValue('customerId', '');
            void applyCurrency(supplier?.currency);
        }
        setPartnerOpen(false);
    };

    const onSubmit = async (data: AdjustmentFormValues) => {
        setIsSubmitting(true);
        try {
            const result = await financeDataService.createSubledgerAdjustmentJournal({
                module: data.module,
                purpose: data.purpose,
                customerId: data.module === 'AR' ? data.customerId : undefined,
                supplierId: data.module === 'AP' ? data.supplierId : undefined,
                adjustmentDate: new Date(`${data.adjustmentDate}T00:00:00`).toISOString(),
                dueDate: data.dueDate ? new Date(`${data.dueDate}T00:00:00`).toISOString() : undefined,
                adjustmentType: data.adjustmentType,
                amount: data.amount,
                currencyCode: data.currencyCode.toUpperCase(),
                exchangeRate: data.exchangeRate,
                contraAccountId: data.contraAccountId,
                reference: data.reference || undefined,
                reason: data.reason,
                notes: data.notes || undefined,
            });

            toast({
                title: 'Adjustment posted',
                description: `${result.adjustmentNumber} posted successfully.`,
            });

            router.push(result.journalEntryId
                ? `/finance/journal-entries/${result.journalEntryId}`
                : `/finance/journal-entries?sourceModule=${data.module}`);
        } catch (error) {
            toast({
                title: 'Posting failed',
                description: error instanceof Error ? error.message : 'Unable to post the adjustment journal.',
                variant: 'destructive',
            });
        } finally {
            setIsSubmitting(false);
        }
    };

    const partnerLoading = selectedModule === 'AR' ? customersLoading : suppliersLoading;
    const partnerLabel = selectedModule === 'AR' ? 'Customer' : 'Supplier';
    const controlAccountLabel = selectedModule === 'AR' ? 'AR control account' : 'AP control account';

    return (
        <div className="space-y-6 p-8">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
                <div className="flex items-start gap-3">
                    <Button variant="ghost" size="icon" onClick={() => router.back()}>
                        <ArrowLeft className="h-4 w-4" />
                    </Button>
                    <div>
                        <h1 className="flex items-center gap-2 text-3xl font-bold tracking-tight">
                            <FileText className="h-8 w-8" />
                            New {selectedModule} Adjustment Journal
                        </h1>
                        <p className="text-muted-foreground">
                            Post a subledger adjustment with a matching general ledger journal.
                        </p>
                    </div>
                </div>
                <Button
                    type="button"
                    variant="outline"
                    onClick={() => router.push(`/finance/journal-entries?sourceModule=${selectedModule}`)}
                >
                    View {selectedModule} Journals
                </Button>
            </div>

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
                        <BreadcrumbLink href={`/finance/journal-entries?sourceModule=${selectedModule}`}>{selectedModule} Journals</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>New Adjustment</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            <form onSubmit={form.handleSubmit(onSubmit)} className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_360px]">
                <Card>
                    <CardHeader>
                        <CardTitle>Adjustment Details</CardTitle>
                    </CardHeader>
                    <CardContent className="grid gap-6 md:grid-cols-2">
                        <div className="space-y-2">
                            <Label>Module</Label>
                            <Select value={selectedModule} onValueChange={handleModuleChange}>
                                <SelectTrigger>
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="AR">Accounts Receivable</SelectItem>
                                    <SelectItem value="AP">Accounts Payable</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>

                        <div className="space-y-2">
                            <Label>{partnerLabel}</Label>
                            <SearchSelect
                                open={partnerOpen}
                                onOpenChange={setPartnerOpen}
                                options={partnerOptions}
                                value={selectedModule === 'AR' ? selectedCustomerId : selectedSupplierId}
                                selectedOption={selectedPartner}
                                placeholder={partnerLoading ? `Loading ${partnerLabel.toLowerCase()}s...` : `Select ${partnerLabel.toLowerCase()}`}
                                searchPlaceholder={`Search ${partnerLabel.toLowerCase()}s...`}
                                emptyText={`No ${partnerLabel.toLowerCase()}s found.`}
                                disabled={partnerLoading}
                                onSelect={handlePartnerSelect}
                            />
                            {(form.formState.errors.customerId || form.formState.errors.supplierId) && (
                                <p className="text-sm text-destructive">
                                    {form.formState.errors.customerId?.message || form.formState.errors.supplierId?.message}
                                </p>
                            )}
                        </div>

                        <div className="space-y-2">
                            <Label htmlFor="adjustmentDate">Adjustment Date</Label>
                            <Input id="adjustmentDate" type="date" {...form.register('adjustmentDate')} />
                            {form.formState.errors.adjustmentDate && (
                                <p className="text-sm text-destructive">{form.formState.errors.adjustmentDate.message}</p>
                            )}
                        </div>

                        <div className="space-y-2">
                            <Label htmlFor="dueDate">Due Date</Label>
                            <Input id="dueDate" type="date" {...form.register('dueDate')} />
                        </div>

                        <div className="space-y-2">
                            <Label>Adjustment Type</Label>
                            <Select
                                value={adjustmentType}
                                onValueChange={(value: SubledgerAdjustmentType) => form.setValue('adjustmentType', value)}
                            >
                                <SelectTrigger>
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="Debit">Debit</SelectItem>
                                    <SelectItem value="Credit">Credit</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>

                        <div className="space-y-2">
                            <Label htmlFor="amount">Amount</Label>
                            <Input id="amount" type="number" min="0" step="0.01" {...form.register('amount', { valueAsNumber: true })} />
                            {form.formState.errors.amount && (
                                <p className="text-sm text-destructive">{form.formState.errors.amount.message}</p>
                            )}
                        </div>

                        <div className="space-y-2">
                            <Label>Currency</Label>
                            <SearchSelect
                                open={currencyOpen}
                                onOpenChange={setCurrencyOpen}
                                options={currencyOptions}
                                value={normalizeCurrencyCode(currencyCode)}
                                selectedOption={selectedCurrency}
                                placeholder={currenciesLoading ? 'Loading currencies...' : 'Select currency'}
                                searchPlaceholder="Search currencies..."
                                emptyText="No active currencies found."
                                disabled={currenciesLoading && currencyOptions.length === 0}
                                onSelect={(code) => {
                                    void applyCurrency(code);
                                    setCurrencyOpen(false);
                                }}
                            />
                            {form.formState.errors.currencyCode && (
                                <p className="text-sm text-destructive">{form.formState.errors.currencyCode.message}</p>
                            )}
                        </div>

                        <div className="space-y-2">
                            <Label htmlFor="exchangeRate">Exchange Rate</Label>
                            <div className="relative">
                                <Input
                                    id="exchangeRate"
                                    type="number"
                                    min="0"
                                    step="0.000001"
                                    readOnly={isRateLoading}
                                    {...form.register('exchangeRate', {
                                        valueAsNumber: true,
                                        onChange: () => form.clearErrors('exchangeRate'),
                                    })}
                                />
                                {isRateLoading && (
                                    <Loader2 className="absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 animate-spin text-muted-foreground" />
                                )}
                            </div>
                            {normalizeCurrencyCode(currencyCode) !== baseCurrencyCode && (
                                <p className="text-xs text-muted-foreground">
                                    1 {normalizeCurrencyCode(currencyCode)} = {form.watch('exchangeRate') || 1} {baseCurrencyCode}
                                </p>
                            )}
                            {form.formState.errors.exchangeRate && (
                                <p className="text-sm text-destructive">{form.formState.errors.exchangeRate.message}</p>
                            )}
                        </div>

                        <div className="space-y-2 md:col-span-2">
                            <Label>Contra GL Account</Label>
                            {accountsLoading ? (
                                <Skeleton className="h-10 w-full" />
                            ) : (
                                <SearchSelect
                                    open={accountOpen}
                                    onOpenChange={setAccountOpen}
                                    options={accountOptions}
                                    value={selectedAccountId}
                                    selectedOption={selectedAccount}
                                    placeholder="Select contra account"
                                    searchPlaceholder="Search accounts..."
                                    emptyText="No posting accounts found."
                                    onSelect={(id) => {
                                        form.setValue('contraAccountId', id, { shouldValidate: true });
                                        setAccountOpen(false);
                                    }}
                                />
                            )}
                            {form.formState.errors.contraAccountId && (
                                <p className="text-sm text-destructive">{form.formState.errors.contraAccountId.message}</p>
                            )}
                        </div>

                        <div className="space-y-2">
                            <Label htmlFor="reference">Reference</Label>
                            <Input id="reference" {...form.register('reference')} />
                        </div>

                        <div className="space-y-2">
                            <Label htmlFor="reason">Reason</Label>
                            <Input id="reason" {...form.register('reason')} />
                            {form.formState.errors.reason && (
                                <p className="text-sm text-destructive">{form.formState.errors.reason.message}</p>
                            )}
                        </div>

                        <div className="space-y-2 md:col-span-2">
                            <Label htmlFor="notes">Notes</Label>
                            <Textarea id="notes" rows={4} {...form.register('notes')} />
                            {form.formState.errors.notes && (
                                <p className="text-sm text-destructive">{form.formState.errors.notes.message}</p>
                            )}
                        </div>
                    </CardContent>
                    <CardFooter className="flex justify-end gap-3">
                        <Button type="button" variant="outline" onClick={() => router.back()}>
                            Cancel
                        </Button>
                        <Button type="submit" disabled={isSubmitting || isRateLoading}>
                            {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Post Adjustment
                        </Button>
                    </CardFooter>
                </Card>

                <div className="space-y-6">
                    <Card>
                        <CardHeader>
                            <CardTitle>Posting Preview</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-5">
                            <div className="rounded-md border p-4">
                                <div className="text-sm text-muted-foreground">Subledger movement</div>
                                <div className={cn('mt-1 text-2xl font-semibold', signedSubledgerAmount < 0 && 'text-destructive')}>
                                    {signedSubledgerAmount >= 0 ? '+' : ''}{formatCurrency(signedSubledgerAmount, currencyCode)}
                                </div>
                                <div className="mt-1 text-sm text-muted-foreground">
                                    {selectedModule === 'AR' ? selectedCustomer?.customerName : selectedSupplier?.partnerName || selectedSupplier?.companyName || partnerLabel}
                                </div>
                            </div>

                            <div className="space-y-3">
                                <PreviewLine
                                    label={controlAccountLabel}
                                    side={controlLineType}
                                    amount={baseAmount}
                                    currencyCode={currencyCode}
                                />
                                <PreviewLine
                                    label={selectedAccount
                                        ? selectedAccount.secondary
                                            ? `${selectedAccount.secondary} - ${selectedAccount.label}`
                                            : selectedAccount.label
                                        : 'Contra GL account'}
                                    side={contraLineType}
                                    amount={baseAmount}
                                    currencyCode={currencyCode}
                                />
                            </div>
                        </CardContent>
                    </Card>
                </div>
            </form>
        </div>
    );
}

function SearchSelect({
    open,
    onOpenChange,
    options,
    value,
    selectedOption,
    placeholder,
    searchPlaceholder,
    emptyText,
    disabled,
    onSelect,
}: {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    options: SearchOption[];
    value?: string;
    selectedOption?: SearchOption;
    placeholder: string;
    searchPlaceholder: string;
    emptyText: string;
    disabled?: boolean;
    onSelect: (id: string) => void;
}) {
    return (
        <Popover open={open} onOpenChange={onOpenChange}>
            <PopoverTrigger asChild>
                <Button
                    type="button"
                    variant="outline"
                    role="combobox"
                    aria-expanded={open}
                    disabled={disabled}
                    className="w-full justify-between overflow-hidden"
                >
                    <span className="truncate">
                        {selectedOption ? `${selectedOption.label}${selectedOption.secondary ? ` (${selectedOption.secondary})` : ''}` : placeholder}
                    </span>
                    <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                </Button>
            </PopoverTrigger>
            <PopoverContent className="w-[--radix-popover-trigger-width] p-0" align="start">
                <Command>
                    <CommandInput placeholder={searchPlaceholder} />
                    <CommandList>
                        <CommandEmpty>{emptyText}</CommandEmpty>
                        <CommandGroup>
                            {options.map((option) => (
                                <CommandItem
                                    key={option.id}
                                    value={`${option.label} ${option.secondary ?? ''}`}
                                    onSelect={() => onSelect(option.id)}
                                >
                                    <Check className={cn('mr-2 h-4 w-4', value === option.id ? 'opacity-100' : 'opacity-0')} />
                                    <span className="truncate">{option.label}</span>
                                    {option.secondary && (
                                        <span className="ml-auto pl-3 text-xs text-muted-foreground">{option.secondary}</span>
                                    )}
                                </CommandItem>
                            ))}
                        </CommandGroup>
                    </CommandList>
                </Command>
            </PopoverContent>
        </Popover>
    );
}

function PreviewLine({
    label,
    side,
    amount,
    currencyCode,
}: {
    label: string;
    side: SubledgerAdjustmentType;
    amount: number;
    currencyCode: string;
}) {
    return (
        <div className="flex items-center justify-between gap-4 rounded-md border px-3 py-2">
            <div className="min-w-0">
                <div className="truncate text-sm font-medium">{label}</div>
                <div className="text-xs text-muted-foreground">{side}</div>
            </div>
            <div className="shrink-0 font-mono text-sm">{formatCurrency(amount, currencyCode)}</div>
        </div>
    );
}
