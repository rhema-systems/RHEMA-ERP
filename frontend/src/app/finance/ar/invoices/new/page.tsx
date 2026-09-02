'use client';

import { useState, useEffect, useRef } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useForm, useFieldArray, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
    ArrowLeft,
    Loader2,
    Plus,
    Trash2,
    Calendar as CalendarIcon,
    AlertCircle,
    Check,
    ChevronsUpDown,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
    Card,
    CardContent,
    CardDescription,
    CardFooter,
    CardHeader,
    CardTitle,
} from '@/components/ui/card';
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import {
    Popover,
    PopoverContent,
    PopoverTrigger,
} from '@/components/ui/popover';
import {
    Command,
    CommandEmpty,
    CommandGroup,
    CommandInput,
    CommandList,
} from '@/components/ui/command';
import { Calendar } from '@/components/ui/calendar';
import { arService } from '@/services/ar-service';
import { financeDataService } from '@/services/finance/finance-data.service';
import { taxDataService } from '@/services/finance/tax-data.service';
import { financeService } from '@/services/finance.service';
import { paymentTermService, type PaymentTermListDto } from '@/services/financeCommonService';
import { useToast } from '@/components/ui/use-toast';
import { formatCurrency, cn } from '@/lib/utils';
import { format, addDays } from 'date-fns';
import { useQuery } from '@tanstack/react-query';
import { loadApprovedInvoiceRate } from '@/lib/finance/invoice-exchange-rate';
import { useTenant } from '@/contexts/TenantContext';
import { SourceDocumentDimensionPanel } from '@/components/finance/dimensions/source-document-dimension-panel';
import { toFinancePostingDimensionValues } from '@/lib/finance/source-document-dimensions';
import {
    allocateDocumentTradeDiscount,
    calculateNetTradeDiscountLineAmount,
} from '@/lib/finance/invoice-trade-discount';

const lineItemSchema = z.object({
    sourceLineId: z.string().uuid(),
    lineItemType: z.enum(['Product', 'GLAccount']).default('Product'),
    productId: z.string().optional(),
    glAccountId: z.string().optional(),
    description: z.string().min(1, 'Description is required'),
    quantity: z.coerce.number().min(0.01, 'Quantity must be positive'),
    unitPrice: z.coerce.number().min(0, 'Unit price must be positive'),
    discountPercentage: z.coerce.number().min(0).max(100).optional().default(0),
    taxGroupId: z.string().optional(),
});

const invoiceSchema = z.object({
    customerId: z.string().min(1, 'Customer is required'),
    invoiceDate: z.date(),
    dueDate: z.date(),
    currencyCode: z.string().default('GHS'),
    exchangeRate: z.coerce.number().min(0.0001).optional().default(1.0),
    exchangeRateId: z.string().optional(),
    exchangeRateDate: z.date().optional(),
    exchangeRateSource: z.string().optional().default('Daily'),
    paymentTermId: z.string().optional(),
    discountAmount: z.coerce.number().min(0).optional().default(0),
    isOpeningBalance: z.boolean().default(false),
    notes: z.string().optional(),
    taxGroupId: z.string().optional(),
    lineItems: z.array(lineItemSchema).min(1, 'At least one line item is required'),
}).superRefine((invoice, context) => {
    const eligibleAmount = invoice.lineItems.reduce((total, line) => {
        const gross = line.quantity * line.unitPrice;
        return total + Math.max(0, calculateNetTradeDiscountLineAmount(gross, line.discountPercentage || 0));
    }, 0);
    if ((invoice.discountAmount || 0) > eligibleAmount) {
        context.addIssue({
            code: z.ZodIssueCode.custom,
            path: ['discountAmount'],
            message: 'Document trade discount cannot exceed the net line amount',
        });
    }
});

type InvoiceFormValues = z.infer<typeof invoiceSchema>;

export default function NewInvoicePage() {
    const router = useRouter();
    const searchParams = useSearchParams();
    const preselectedCustomerId = searchParams.get('customerId');
    const defaultOpeningBalance = searchParams.get('openingBalance') === 'true';
    const { toast } = useToast();
    const { currentTenantCode } = useTenant();
    const exchangeRateRequestId = useRef(0);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [selectedCustomer, setSelectedCustomer] = useState<any>(null);
    const [customerComboOpen, setCustomerComboOpen] = useState(false);
    const [customerSearch, setCustomerSearch] = useState('');
    const [glAccountOpenIndex, setGlAccountOpenIndex] = useState<number | null>(null);
    const [glAccountSearch, setGlAccountSearch] = useState('');
    const [defaultDimensionValues, setDefaultDimensionValues] = useState<Record<string, string>>({});
    const [lineDimensionValues, setLineDimensionValues] = useState<Record<string, Record<string, string>>>({});
    const [applyDefaultToAll, setApplyDefaultToAll] = useState(false);

    // Fetch customers for the dropdown
    const { data: customersData, isLoading: customersLoading } = useQuery({
        queryKey: ['customers-list'],
        queryFn: () => arService.getCustomers({ pageSize: 100 }),
    });

    // Fetch active GL accounts for line item selection using financeDataService
    const { data: glAccountsData, isLoading: glAccountsLoading } = useQuery({
        queryKey: ['gl-accounts-active'],
        queryFn: async () => {
            const accounts = await financeDataService.getAccounts({ status: 'Active' });
            return {
                items: accounts.filter((account: any) => account.isActive !== false)
            };
        },
    });

    // Fetch active tax groups for line item tax selection using taxDataService
    const { data: taxGroupsData } = useQuery({
        queryKey: ['tax-groups-active'],
        queryFn: () => taxDataService.getTaxGroups({ isActive: true, applicability: 'Sales' }),
    });

    const { data: paymentTerms = [], isLoading: paymentTermsLoading } = useQuery({
        queryKey: ['payment-terms', 'Customer'],
        queryFn: () => paymentTermService.getByApplicableTo('Customer'),
    });

    const { data: financeSettings } = useQuery({
        queryKey: ['finance-settings', currentTenantCode, 'ar-invoice-rate-policy'],
        queryFn: () => financeService.getSettings(),
        enabled: Boolean(currentTenantCode),
    });

    // Filter customers based on search
    const filteredCustomers = customersData?.items?.filter((customer: any) => {
        if (!customerSearch) return true;
        const search = customerSearch.toLowerCase();
        return (
            customer.customerName?.toLowerCase().includes(search) ||
            customer.customerCode?.toLowerCase().includes(search) ||
            customer.email?.toLowerCase().includes(search)
        );
    }) || [];

    // Filter GL accounts based on search
    const filteredGlAccounts = glAccountsData?.items?.filter((account: any) => {
        if (!glAccountSearch) return true;
        const search = glAccountSearch.toLowerCase();
        return (
            account.accountCode?.toLowerCase().includes(search) ||
            account.accountName?.toLowerCase().includes(search) ||
            account.accountNumber?.toLowerCase().includes(search)
        );
    }) || [];

    // Helper to get account display name by ID
    const getAccountDisplay = (accountId: string | undefined) => {
        if (!accountId) return null;
        const account = glAccountsData?.items?.find((a: any) => a.id === accountId);
        return account ? `${account.accountCode} - ${account.accountName}` : null;
    };


    const form = useForm<InvoiceFormValues>({
        resolver: zodResolver(invoiceSchema) as any,
        defaultValues: {
            customerId: preselectedCustomerId || '',
            invoiceDate: new Date(),
            dueDate: addDays(new Date(), 30),
            currencyCode: 'GHS',
            exchangeRate: 1.0,
            exchangeRateId: undefined,
            exchangeRateDate: new Date(),
            exchangeRateSource: 'Daily',
            paymentTermId: 'none',
            discountAmount: 0,
            isOpeningBalance: defaultOpeningBalance,
            notes: '',
            lineItems: [
                { sourceLineId: crypto.randomUUID(), lineItemType: 'Product' as const, description: 'Service / Product', quantity: 1, unitPrice: 0, discountPercentage: 0 }
            ],
        },
    });

    const watchInvoiceDate = form.watch('invoiceDate');
    const watchPaymentTermId = form.watch('paymentTermId');
    useEffect(() => {
        if (watchInvoiceDate) {
            form.setValue('exchangeRateDate', watchInvoiceDate);
            const selectedTerm = paymentTerms.find(term => term.id === watchPaymentTermId);
            if (selectedTerm) {
                form.setValue('dueDate', addDays(watchInvoiceDate, selectedTerm.dueDays));
            }
        }
    }, [watchInvoiceDate, watchPaymentTermId, paymentTerms]);

    const { fields, append, remove } = useFieldArray({
        control: form.control,
        name: 'lineItems',
    });

    const watchIsOpeningBalance = form.watch('isOpeningBalance');

    useEffect(() => {
        if (!watchIsOpeningBalance) return;

        // Opening invoices bring forward gross AR balances only; tax history is not reposted
        // through the migration clearing entry created by the posting service.
        form.setValue('taxGroupId', 'none');
        form.getValues('lineItems').forEach((_, index) => {
            form.setValue(`lineItems.${index}.taxGroupId`, 'none');
        });
    }, [form, watchIsOpeningBalance]);

    // Calculate totals
    const watchLineItems = form.watch('lineItems');
    const subtotal = watchLineItems.reduce((acc, item) => {
        const qty = Number(item.quantity) || 0;
        const price = Number(item.unitPrice) || 0;
        const discount = Number(item.discountPercentage) || 0;
        const lineTotal = calculateNetTradeDiscountLineAmount(qty * price, discount);
        return acc + lineTotal;
    }, 0);
    const documentDiscountBasis = watchLineItems.reduce((acc, item) => {
        const gross = (Number(item.quantity) || 0) * (Number(item.unitPrice) || 0);
        const net = calculateNetTradeDiscountLineAmount(
            gross,
            Number(item.discountPercentage) || 0
        );
        return acc + Math.max(0, net);
    }, 0);

    const watchTaxGroupId = form.watch('taxGroupId');
    const watchCurrencyCode = form.watch('currencyCode') || 'GHS';
    const documentDiscount = Number(form.watch('discountAmount')) || 0;

    const applyInvoiceExchangeRate = async (currencyCode: string) => {
        const requestId = ++exchangeRateRequestId.current;
        const functionalCurrency = financeSettings?.baseCurrency || 'GHS';
        // Clear prior evidence before lookup so Save cannot race with stale rate identity.
        form.setValue('exchangeRateId', undefined);
        if (!financeSettings) {
            throw new Error('Finance settings are still loading. Try again before saving this invoice.');
        }
        let snapshot;
        try {
            snapshot = await loadApprovedInvoiceRate(
                {
                    module: 'AR',
                    transactionCurrency: currencyCode,
                    functionalCurrency,
                    invoiceDate: form.getValues('invoiceDate'),
                    settings: financeSettings,
                },
                (code, query) => financeService.getCurrentExchangeRate(code, query)
            );
        } catch (error) {
            if (requestId !== exchangeRateRequestId.current) return;
            throw error;
        }
        if (requestId !== exchangeRateRequestId.current) return;
        form.setValue('exchangeRate', snapshot.rate);
        form.setValue('exchangeRateId', snapshot.exchangeRateId);
        form.setValue('exchangeRateDate', form.getValues('invoiceDate'));
        form.setValue('exchangeRateSource', snapshot.source);
    };

    useEffect(() => {
        if (!financeSettings || !watchInvoiceDate) return;
        void applyInvoiceExchangeRate(watchCurrencyCode).catch((error) => {
            console.error('Failed to resolve governed AR invoice rate', error);
            form.setValue('exchangeRateId', undefined);
            form.setValue('exchangeRate', 0);
            form.setValue('exchangeRateSource', 'Unavailable');
        });
    }, [financeSettings, watchCurrencyCode, watchInvoiceDate]);

    const getTaxBreakdown = () => {
        if (watchIsOpeningBalance) {
            return {
                totalTaxAmount: 0,
                grandTotal: Math.max(0, subtotal - documentDiscount),
                taxList: []
            };
        }

        let totalTaxAmount = 0;
        const breakdowns: { [taxCode: string]: { name: string; rate: number; amount: number } } = {};

        const linesBeforeDocumentDiscount = watchLineItems.map((item) => {
            const qty = Number(item.quantity) || 0;
            const price = Number(item.unitPrice) || 0;
            const discount = Number(item.discountPercentage) || 0;
            return {
                sourceLineId: item.sourceLineId,
                netAmount: calculateNetTradeDiscountLineAmount(qty * price, discount),
            };
        });
        const documentDiscountAllocations = allocateDocumentTradeDiscount(
            linesBeforeDocumentDiscount,
            documentDiscount
        );

        watchLineItems.forEach((item, index) => {
            const qty = Number(item.quantity) || 0;
            const price = Number(item.unitPrice) || 0;
            const discount = Number(item.discountPercentage) || 0;
            const lineSubtotal = Math.max(
                0,
                calculateNetTradeDiscountLineAmount(qty * price, discount) - documentDiscountAllocations[index]
            );

            // Resolve line tax group override or default to header
            const activeGroupId = item.taxGroupId || watchTaxGroupId;
            const activeGroup = taxGroupsData?.find(tg => tg.id === activeGroupId);

            if (activeGroup && activeGroup.components) {
                let cumulativeBase = lineSubtotal;
                const sortedComponents = [...activeGroup.components].sort((a, b) => a.calculationOrder - b.calculationOrder);

                sortedComponents.forEach(comp => {
                    if (comp.taxCategory === 'Withholding') return; // output VAT/levies only

                    let taxableBasis = lineSubtotal;
                    if (comp.compoundBasis === 'Cumulative') {
                        taxableBasis = cumulativeBase;
                    }

                    const taxAmt = taxableBasis * (Number(comp.taxRate) / 100);
                    totalTaxAmount += taxAmt;

                    if (comp.compoundBasis === 'Cumulative' || comp.compoundBasis === 'BaseOnly') {
                        cumulativeBase += taxAmt;
                    }

                    if (breakdowns[comp.taxCode]) {
                        breakdowns[comp.taxCode].amount += taxAmt;
                    } else {
                        breakdowns[comp.taxCode] = {
                            name: comp.taxName,
                            rate: comp.taxRate,
                            amount: taxAmt
                        };
                    }
                });
            }
        });

        const grandTotal = Math.max(0, subtotal + totalTaxAmount - documentDiscount);

        return {
            totalTaxAmount,
            grandTotal,
            taxList: Object.entries(breakdowns).map(([code, data]) => ({ code, ...data }))
        };
    };

    const taxEstimate = getTaxBreakdown();
    const totalTax = taxEstimate.totalTaxAmount;
    const totalAmount = Math.max(0, taxEstimate.grandTotal);

    const resolveLineTaxGroupId = (
        item: any,
        isOpeningBalance = watchIsOpeningBalance,
        headerTaxGroupId = watchTaxGroupId
    ) => {
        if (isOpeningBalance) return null;
        const activeGroupId = item.taxGroupId || headerTaxGroupId;
        return activeGroupId && activeGroupId !== 'none' ? activeGroupId : null;
    };

    const formatAmountWithCurrency = (amount: number) => {
        return formatCurrency(amount, watchCurrencyCode);
    };

    const formatPaymentTerm = (term: PaymentTermListDto) => {
        const discountText = term.discountPercent && term.discountDays
            ? `, ${term.discountPercent}% if paid in ${term.discountDays} days`
            : '';
        return `${term.code} - ${term.name} (${term.dueDays} days${discountText})`;
    };

    const applyPaymentTerm = (paymentTermId: string, invoiceDate = form.getValues('invoiceDate'), fallbackDays?: number) => {
        form.setValue('paymentTermId', paymentTermId);
        const selectedTerm = paymentTerms.find(term => term.id === paymentTermId);
        if (selectedTerm && invoiceDate) {
            form.setValue('dueDate', addDays(invoiceDate, selectedTerm.dueDays));
        } else if (fallbackDays !== undefined && invoiceDate) {
            form.setValue('dueDate', addDays(invoiceDate, fallbackDays));
        }
    };

    // Update due date when customer is selected (based on payment terms)
    const onCustomerChange = async (customerId: string) => {
        form.setValue('customerId', customerId);
        if (!customersData?.items) return;

        const customer = customersData.items.find(c => c.id === customerId);
        if (customer) {
            setSelectedCustomer(customer);
            const invoiceDate = form.getValues('invoiceDate');
            if (customer.paymentTermId) {
                applyPaymentTerm(customer.paymentTermId, invoiceDate, customer.paymentTermsDays || 30);
            } else {
                const terms = customer.paymentTermsDays || 30;
                form.setValue('paymentTermId', 'none');
                form.setValue('dueDate', addDays(invoiceDate, terms));
            }

            if (customer.currencyCode) {
                form.setValue('currencyCode', customer.currencyCode);
                try {
                    await applyInvoiceExchangeRate(customer.currencyCode);
                } catch (err) {
                    console.error("Failed to fetch exchange rate for customer currency", err);
                    form.setValue('exchangeRateId', undefined);
                    form.setValue('exchangeRate', 0);
                    form.setValue('exchangeRateSource', 'Unavailable');
                }
            } else {
                form.setValue('currencyCode', 'GHS');
                await applyInvoiceExchangeRate('GHS');
            }
        }
    };

    // Set initial selected customer if preselected
    useEffect(() => {
        if (preselectedCustomerId && customersData?.items) {
            onCustomerChange(preselectedCustomerId);
        }
    }, [preselectedCustomerId, customersData]);


    const onSubmit = async (data: InvoiceFormValues) => {
        setIsSubmitting(true);
        try {
            const isOpeningBalance = data.isOpeningBalance;
            const functionalCurrency = financeSettings?.baseCurrency || 'GHS';
            if (data.currencyCode !== functionalCurrency && !data.exchangeRateId) {
                toast({
                    title: 'Approved exchange rate required',
                    description: 'Select a currency and invoice date with an active approved Daily rate before creating this invoice.',
                    variant: 'destructive',
                });
                return;
            }
            await arService.createInvoice({
                ...data,
                invoiceDate: data.invoiceDate.toISOString(),
                dueDate: data.dueDate.toISOString(),
                taxGroupId: isOpeningBalance || data.taxGroupId === 'none' ? null : (data.taxGroupId || null),
                exchangeRate: Number(data.exchangeRate) || 1.0,
                exchangeRateId: data.exchangeRateId,
                paymentTermId: data.paymentTermId === 'none' ? null : (data.paymentTermId || null),
                discountAmount: Number(data.discountAmount) || 0,
                isOpeningBalance,
                lineItems: data.lineItems.map(item => ({
                    id: item.sourceLineId,
                    lineItemType: item.lineItemType,
                    productId: item.productId,
                    glAccountId: item.glAccountId,
                    description: item.description,
                    quantity: Number(item.quantity),
                    unitPrice: Number(item.unitPrice),
                    discountPercentage: Number(item.discountPercentage),
                    taxGroupId: resolveLineTaxGroupId(item, isOpeningBalance, data.taxGroupId)
                })),
                financeDimensions: {
                    defaultDimensions: toFinancePostingDimensionValues(defaultDimensionValues),
                    lines: data.lineItems.flatMap(item => {
                        const accountId = !isOpeningBalance && item.lineItemType === 'GLAccount'
                            ? item.glAccountId
                            : undefined;
                        return accountId ? [{
                            sourceLineId: item.sourceLineId,
                            accountId,
                            dimensions: toFinancePostingDimensionValues(lineDimensionValues[item.sourceLineId] || {}),
                        }] : [];
                    }),
                    applyDefaultToEligibleLines: applyDefaultToAll,
                },
            });

            toast({
                title: 'Success',
                description: 'Invoice created successfully',
            });

            router.push('/finance/ar/invoices');
        } catch (error: any) {
            toast({
                title: 'Error',
                description: error.message || 'Failed to create invoice',
                variant: 'destructive',
            });
        } finally {
            setIsSubmitting(false);
        }
    };

    return (
        <div className="space-y-8 p-8 max-w-[1200px] mx-auto">
            <div className="flex items-center space-x-4">
                <Button variant="ghost" size="icon" onClick={() => router.back()}>
                    <ArrowLeft className="h-4 w-4" />
                </Button>
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">New Invoice</h1>
                    <p className="text-muted-foreground">
                        Create a new invoice for a customer.
                    </p>
                </div>
            </div>

            <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-8">
                <Card>
                    <CardHeader>
                        <CardTitle>Invoice Details</CardTitle>
                    </CardHeader>
                    <CardContent className="grid gap-6 md:grid-cols-2">
                        <div className="space-y-2">
                            <Label htmlFor="customer">Customer</Label>
                            <Popover open={customerComboOpen} onOpenChange={setCustomerComboOpen}>
                                <PopoverTrigger asChild>
                                    <Button
                                        variant="outline"
                                        role="combobox"
                                        aria-expanded={customerComboOpen}
                                        className="w-full justify-between"
                                    >
                                        {selectedCustomer
                                            ? `${selectedCustomer.customerName} (${selectedCustomer.customerCode})`
                                            : customersLoading
                                            ? "Loading customers..."
                                            : "Select a customer..."}
                                        <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                                    </Button>
                                </PopoverTrigger>
                                <PopoverContent className="w-[400px] p-0" align="start">
                                    <Command shouldFilter={false}>
                                        <CommandInput
                                            placeholder="Search customer by name, code, or email..."
                                            value={customerSearch}
                                            onValueChange={setCustomerSearch}
                                        />
                                        <CommandList>
                                            <CommandEmpty>
                                                {customersLoading ? "Loading..." : "No customer found."}
                                            </CommandEmpty>
                                            <CommandGroup>
                                                {filteredCustomers.map((customer: any) => {
                                                    const handleSelect = () => {
                                                        onCustomerChange(customer.id);
                                                        setCustomerComboOpen(false);
                                                        setCustomerSearch('');
                                                    };
                                                    return (
                                                        <div
                                                            key={customer.id}
                                                            onClick={handleSelect}
                                                            className="relative flex cursor-pointer select-none items-center rounded-sm px-2 py-1.5 text-sm outline-none hover:bg-accent hover:text-accent-foreground"
                                                        >
                                                            <Check
                                                                className={cn(
                                                                    "mr-2 h-4 w-4",
                                                                    selectedCustomer?.id === customer.id
                                                                        ? "opacity-100"
                                                                        : "opacity-0"
                                                                )}
                                                            />
                                                            <div className="flex flex-col">
                                                                <span className="font-medium">{customer.customerName}</span>
                                                                <span className="text-xs text-muted-foreground">
                                                                    {customer.customerCode} {customer.email && `- ${customer.email}`}
                                                                </span>
                                                            </div>
                                                        </div>
                                                    );
                                                })}
                                            </CommandGroup>
                                        </CommandList>
                                    </Command>
                                </PopoverContent>
                            </Popover>
                            {form.formState.errors.customerId && (
                                <p className="text-sm text-red-500">{form.formState.errors.customerId.message}</p>
                            )}
                        </div>

                        <div className="grid grid-cols-2 gap-4">
                            <div className="space-y-2">
                                <Label>Invoice Date</Label>
                                <Controller
                                    control={form.control}
                                    name="invoiceDate"
                                    render={({ field }) => (
                                        <Popover>
                                            <PopoverTrigger asChild>
                                                <Button
                                                    variant="outline"
                                                    className={cn(
                                                        "w-full justify-start text-left font-normal",
                                                        !field.value && "text-muted-foreground"
                                                    )}
                                                >
                                                    <CalendarIcon className="mr-2 h-4 w-4" />
                                                    {field.value ? format(field.value, "PPP") : <span>Pick a date</span>}
                                                </Button>
                                            </PopoverTrigger>
                                            <PopoverContent className="w-auto p-0">
                                                <Calendar
                                                    mode="single"
                                                    selected={field.value}
                                                    onSelect={field.onChange}
                                                    initialFocus
                                                />
                                            </PopoverContent>
                                        </Popover>
                                    )}
                                />
                            </div>

                            <div className="space-y-2">
                                <Label>Due Date</Label>
                                <Controller
                                    control={form.control}
                                    name="dueDate"
                                    render={({ field }) => (
                                        <Popover>
                                            <PopoverTrigger asChild>
                                                <Button
                                                    variant="outline"
                                                    className={cn(
                                                        "w-full justify-start text-left font-normal",
                                                        !field.value && "text-muted-foreground"
                                                    )}
                                                >
                                                    <CalendarIcon className="mr-2 h-4 w-4" />
                                                    {field.value ? format(field.value, "PPP") : <span>Pick a date</span>}
                                                </Button>
                                            </PopoverTrigger>
                                            <PopoverContent className="w-auto p-0">
                                                <Calendar
                                                    mode="single"
                                                    selected={field.value}
                                                    onSelect={field.onChange}
                                                    initialFocus
                                                />
                                            </PopoverContent>
                                        </Popover>
                                    )}
                                />
                            </div>
                        </div>

                        {/* Default Tax Group removed from main top section to match premium line-driven model */}

                        <div className="space-y-2">
                            <Label>Payment Term</Label>
                            <Controller
                                control={form.control}
                                name="paymentTermId"
                                render={({ field }) => (
                                    <Select value={field.value || 'none'} onValueChange={(value) => applyPaymentTerm(value)}>
                                        <SelectTrigger>
                                            <SelectValue placeholder={paymentTermsLoading ? 'Loading terms...' : 'Select payment term'} />
                                        </SelectTrigger>
                                        <SelectContent>
                                            <SelectItem value="none">Manual due date / no configured term</SelectItem>
                                            {paymentTerms.map(term => (
                                                <SelectItem key={term.id} value={term.id}>
                                                    {formatPaymentTerm(term)}
                                                </SelectItem>
                                            ))}
                                        </SelectContent>
                                    </Select>
                                )}
                            />
                            <span className="text-[11px] text-muted-foreground block mt-1">
                                Defaults from the customer and updates due date; manual due date remains editable.
                            </span>
                        </div>

                        <div className="space-y-2">
                            <Label>Currency</Label>
                            <Controller
                                control={form.control}
                                name="currencyCode"
                                render={({ field }) => (
                                    <Select 
                                        value={field.value} 
                                        onValueChange={async (val) => {
                                            field.onChange(val);
                                            try {
                                                await applyInvoiceExchangeRate(val);
                                            } catch (err) {
                                                console.error("Failed to fetch exchange rate for currency", err);
                                                form.setValue('exchangeRateId', undefined);
                                                form.setValue('exchangeRate', 0);
                                                form.setValue('exchangeRateSource', 'Unavailable');
                                            }
                                        }}
                                    >
                                        <SelectTrigger>
                                            <SelectValue placeholder="Select Currency" />
                                        </SelectTrigger>
                                        <SelectContent>
                                            <SelectItem value="GHS">GHS - Ghana Cedi</SelectItem>
                                            <SelectItem value="USD">USD - US Dollar</SelectItem>
                                            <SelectItem value="EUR">EUR - Euro</SelectItem>
                                            <SelectItem value="GBP">GBP - British Pound</SelectItem>
                                        </SelectContent>
                                    </Select>
                                )}
                            />
                        </div>

                        {watchCurrencyCode !== (financeSettings?.baseCurrency || 'GHS') && (
                            <div className="space-y-2">
                                <Label className="text-amber-600 font-semibold">Exchange Rate to Base Currency</Label>
                                <Input 
                                    type="number" 
                                    step="0.0001" 
                                    min="0.0001" 
                                    readOnly
                                    aria-readonly="true"
                                    {...form.register('exchangeRate')}
                                />
                                <span className="text-[11px] text-muted-foreground block mt-1">
                                    1 {watchCurrencyCode} = {form.watch('exchangeRate')} {financeSettings?.baseCurrency || 'GHS'}
                                    {' · approved rate locked to this invoice'}
                                </span>
                            </div>
                        )}

                        <div className="flex items-center gap-3 rounded-md border p-3">
                            <Controller
                                control={form.control}
                                name="isOpeningBalance"
                                render={({ field }) => (
                                    <Checkbox
                                        id="isOpeningBalance"
                                        checked={field.value}
                                        onCheckedChange={(checked) => field.onChange(checked === true)}
                                    />
                                )}
                            />
                            <Label htmlFor="isOpeningBalance" className="font-medium">
                                Opening Balance
                            </Label>
                        </div>

                        <div className="space-y-2">
                            <Label>Default Tax Group (For new lines)</Label>
                            <Controller
                                control={form.control}
                                name="taxGroupId"
                                render={({ field }) => (
                                    <Select value={watchIsOpeningBalance ? 'none' : (field.value || 'none')} onValueChange={field.onChange} disabled={watchIsOpeningBalance}>
                                        <SelectTrigger className={cn(watchIsOpeningBalance && 'bg-muted text-muted-foreground')}>
                                            <SelectValue placeholder="No Tax (Zero/Exempt)" />
                                        </SelectTrigger>
                                        <SelectContent>
                                            <SelectItem value="none">No Tax (Zero/Exempt)</SelectItem>
                                            {taxGroupsData?.map((tg: any) => (
                                                <SelectItem key={tg.id} value={tg.id}>{tg.name}</SelectItem>
                                            ))}
                                        </SelectContent>
                                    </Select>
                                )}
                            />
                            <span className="text-[11px] text-muted-foreground block mt-1">
                                {watchIsOpeningBalance
                                    ? 'Disabled for opening balances; opening invoices carry no tax reposting.'
                                    : 'Optional. Pre-populates new lines; can be overridden on each line.'}
                            </span>
                        </div>

                        <div className="space-y-2">
                            <Label htmlFor="discountAmount">Document Trade Discount</Label>
                            <Input
                                id="discountAmount"
                                type="number"
                                min="0"
                                max={documentDiscountBasis}
                                step="0.01"
                                {...form.register('discountAmount')}
                            />
                            {form.formState.errors.discountAmount && (
                                <p className="text-sm text-red-500">{form.formState.errors.discountAmount.message}</p>
                            )}
                            <span className="text-[11px] text-muted-foreground block mt-1">
                                Fixed currency amount allocated across invoice lines. It reduces revenue and the taxable base.
                            </span>
                        </div>

                        {watchCurrencyCode !== (financeSettings?.baseCurrency || 'GHS') && (
                            <div className="border p-4 rounded-lg bg-muted/20 md:col-span-2 space-y-4">
                                <div className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Advanced FX Details</div>
                                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                    <div className="space-y-2">
                                        <Label className="text-xs">Exchange Rate Date</Label>
                                        <Controller
                                            control={form.control}
                                            name="exchangeRateDate"
                                            render={({ field }) => (
                                                <Popover>
                                                    <PopoverTrigger asChild>
                                                        <Button variant="outline" disabled className={cn("w-full justify-start text-left font-normal text-xs", !field.value && "text-muted-foreground")}>
                                                            <CalendarIcon className="mr-2 h-3 w-3" />
                                                            {field.value ? format(field.value, "PPP") : <span>Pick a date</span>}
                                                        </Button>
                                                    </PopoverTrigger>
                                                    <PopoverContent className="w-auto p-0">
                                                        <Calendar mode="single" selected={field.value} onSelect={field.onChange} />
                                                    </PopoverContent>
                                                </Popover>
                                            )}
                                        />
                                    </div>
                                    <div className="space-y-2">
                                        <Label className="text-xs">Exchange Rate Source</Label>
                                        <Controller
                                            control={form.control}
                                            name="exchangeRateSource"
                                            render={({ field }) => (
                                                <Input value={field.value || 'Approved Daily rate'} readOnly aria-readonly="true" className="h-10 text-xs" />
                                            )}
                                        />
                                    </div>
                                </div>
                            </div>
                        )}

                        <div className="md:col-span-2">
                            <Label htmlFor="notes">Notes/Memo</Label>
                            <Textarea
                                id="notes"
                                placeholder="Reference number, payment instructions, etc."
                                {...form.register('notes')}
                            />
                        </div>
                    </CardContent>
                </Card>

                <Card>
                    <CardHeader>
                        <CardTitle>Finance coding dimensions</CardTitle>
                        <CardDescription>
                            Defaults are convenient; each revenue line remains authoritative.
                        </CardDescription>
                    </CardHeader>
                    <CardContent>
                        <SourceDocumentDimensionPanel
                            context={{
                                sourceModule: 'AR',
                                sourceDocumentType: 'CustomerInvoice',
                                postingAction: 'Post',
                                sourceRoute: 'finance.ar.customer-invoices.manual',
                                contractVersion: '1.0',
                            }}
                            effectiveDate={format(watchInvoiceDate || new Date(), 'yyyy-MM-dd')}
                            lines={watchLineItems.map((item) => ({
                                id: item.sourceLineId,
                                accountId: !watchIsOpeningBalance && item.lineItemType === 'GLAccount'
                                    ? item.glAccountId
                                    : undefined,
                                accountLabel: item.description || undefined,
                            }))}
                            defaultValues={defaultDimensionValues}
                            lineValues={lineDimensionValues}
                            onDefaultValuesChange={(values) => {
                                setDefaultDimensionValues(values);
                                setApplyDefaultToAll(false);
                            }}
                            onLineValuesChange={setLineDimensionValues}
                            onApplyDefaultToAll={() => setApplyDefaultToAll(true)}
                        />
                    </CardContent>
                </Card>

                {/* Line Items Card */}
                <Card>
                    <CardHeader className="flex flex-row items-center justify-between">
                        <CardTitle>Line Items</CardTitle>
                        <Button type="button" variant="outline" size="sm" onClick={() => append({ sourceLineId: crypto.randomUUID(), lineItemType: 'Product' as const, description: '', quantity: 1, unitPrice: 0, discountPercentage: 0, taxGroupId: watchIsOpeningBalance ? 'none' : undefined })}>
                            <Plus className="mr-2 h-4 w-4" /> Add Item
                        </Button>
                    </CardHeader>
                    <CardContent>
                        <div className="space-y-4">
                            {fields.map((field, index) => {
                                const lineItemType = form.watch(`lineItems.${index}.lineItemType`);
                                return (
                                    <div key={field.id} className="grid grid-cols-12 gap-4 items-end border-b pb-4">
                                        {/* Type Selector */}
                                        <div className="col-span-2 space-y-2">
                                            <Label className={index !== 0 ? 'sr-only' : ''}>Type</Label>
                                            <Controller
                                                control={form.control}
                                                name={`lineItems.${index}.lineItemType`}
                                                render={({ field }) => (
                                                    <Select
                                                        value={field.value}
                                                        onValueChange={field.onChange}
                                                    >
                                                        <SelectTrigger>
                                                            <SelectValue placeholder="Type" />
                                                        </SelectTrigger>
                                                        <SelectContent>
                                                            <SelectItem value="Product">Product</SelectItem>
                                                            <SelectItem value="GLAccount">GL Account</SelectItem>
                                                        </SelectContent>
                                                    </Select>
                                                )}
                                            />
                                        </div>

                                        {/* Conditional: GL Account or Description */}
                                        {lineItemType === 'GLAccount' ? (
                                            <div className="col-span-3 space-y-2">
                                                <Label className={index !== 0 ? 'sr-only' : ''}>GL Account</Label>
                                                <Controller
                                                    control={form.control}
                                                    name={`lineItems.${index}.glAccountId`}
                                                    render={({ field }) => (
                                                        <Popover
                                                            open={glAccountOpenIndex === index}
                                                            onOpenChange={(open) => {
                                                                setGlAccountOpenIndex(open ? index : null);
                                                                if (!open) setGlAccountSearch('');
                                                            }}
                                                        >
                                                            <PopoverTrigger asChild>
                                                                <Button
                                                                    variant="outline"
                                                                    role="combobox"
                                                                    className="w-full justify-between text-left font-medium"
                                                                >
                                                                    <span className="truncate font-semibold">
                                                                        {getAccountDisplay(field.value) ||
                                                                            (glAccountsLoading ? "Loading..." : "Select account...")}
                                                                    </span>
                                                                    <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                                                                </Button>
                                                            </PopoverTrigger>
                                                            <PopoverContent className="w-[350px] p-0" align="start">
                                                                <Command shouldFilter={false}>
                                                                    <CommandInput
                                                                        placeholder="Search by code or name..."
                                                                        value={glAccountSearch}
                                                                        onValueChange={setGlAccountSearch}
                                                                    />
                                                                    <CommandList>
                                                                        <CommandEmpty>
                                                                            {glAccountsLoading ? "Loading..." : "No account found."}
                                                                        </CommandEmpty>
                                                                        <CommandGroup>
                                                                            {filteredGlAccounts.map((account: any) => {
                                                                                const handleSelect = () => {
                                                                                    field.onChange(account.id);
                                                                                    // Auto-fill description with account name
                                                                                    form.setValue(
                                                                                        `lineItems.${index}.description`,
                                                                                        account.accountName
                                                                                    );
                                                                                    setGlAccountOpenIndex(null);
                                                                                    setGlAccountSearch('');
                                                                                };
                                                                                return (
                                                                                    <div
                                                                                        key={account.id}
                                                                                        onClick={handleSelect}
                                                                                        className="relative flex cursor-pointer select-none items-center rounded-sm px-2 py-1.5 text-sm outline-none hover:bg-accent hover:text-accent-foreground"
                                                                                    >
                                                                                        <Check
                                                                                            className={cn(
                                                                                                "mr-2 h-4 w-4",
                                                                                                field.value === account.id
                                                                                                    ? "opacity-100"
                                                                                                    : "opacity-0"
                                                                                            )}
                                                                                        />
                                                                                        <div className="flex flex-col">
                                                                                            <span className="font-bold text-sm">
                                                                                                {account.accountCode}
                                                                                            </span>
                                                                                            <span className="text-xs text-muted-foreground truncate font-medium">
                                                                                                {account.accountName}
                                                                                            </span>
                                                                                        </div>
                                                                                    </div>
                                                                                );
                                                                            })}
                                                                        </CommandGroup>
                                                                    </CommandList>
                                                                </Command>
                                                            </PopoverContent>
                                                        </Popover>
                                                    )}
                                                />
                                            </div>
                                        ) : (
                                            <div className="col-span-3 space-y-2">
                                                <Label className={index !== 0 ? 'sr-only' : ''}>Description</Label>
                                                <Input {...form.register(`lineItems.${index}.description` as const)} placeholder="Item description" />
                                            </div>
                                        )}

                                        <div className="col-span-1 space-y-2">
                                            <Label className={index !== 0 ? 'sr-only' : ''}>Qty</Label>
                                            <Input type="number" step="1" {...form.register(`lineItems.${index}.quantity` as const)} className="text-center" />
                                        </div>
                                        <div className="col-span-2 space-y-2">
                                            <Label className={index !== 0 ? 'sr-only' : ''}>Price</Label>
                                            <Input type="number" step="0.01" {...form.register(`lineItems.${index}.unitPrice` as const)} className="text-right" />
                                        </div>
                                        <div className="col-span-1 space-y-2">
                                            <Label className={index !== 0 ? 'sr-only' : ''}>Trade Disc %</Label>
                                            <Input type="number" min="0" max="100" step="0.5" {...form.register(`lineItems.${index}.discountPercentage` as const)} className="text-center" />
                                            {form.formState.errors.lineItems?.[index]?.discountPercentage && (
                                                <p className="text-xs text-red-500">Use 0–100</p>
                                            )}
                                        </div>
                                        <div className="col-span-2 space-y-2">
                                            <Label className={cn("text-amber-600 font-semibold", index !== 0 ? 'sr-only' : '')}>Tax Group</Label>
                                            <Controller
                                                control={form.control}
                                                name={`lineItems.${index}.taxGroupId`}
                                                render={({ field }) => (
                                                    <Select 
                                                        value={watchIsOpeningBalance ? 'none' : (field.value || 'inherit')}
                                                        onValueChange={(val) => field.onChange(val === 'inherit' ? '' : val)}
                                                        disabled={watchIsOpeningBalance}
                                                    >
                                                        <SelectTrigger className={cn(watchIsOpeningBalance && 'bg-muted text-muted-foreground')}>
                                                            <SelectValue placeholder="Inherit Default" />
                                                        </SelectTrigger>
                                                        <SelectContent>
                                                            <SelectItem value="inherit">
                                                                {watchTaxGroupId && watchTaxGroupId !== 'none'
                                                                    ? `Inherited: ${taxGroupsData?.find((t: any) => t.id === watchTaxGroupId)?.name || ''}`
                                                                    : 'Inherited: Zero-rated / Exempt'}
                                                            </SelectItem>
                                                            <SelectItem value="none">Zero-rated / Exempt</SelectItem>
                                                            {taxGroupsData?.map((group: any) => (
                                                                <SelectItem key={group.id} value={group.id}>
                                                                    {group.name}
                                                                </SelectItem>
                                                            ))}
                                                        </SelectContent>
                                                    </Select>
                                                )}
                                            />
                                        </div>
                                        <div className="col-span-1 flex items-end justify-center">
                                            <Button type="button" variant="ghost" size="icon" onClick={() => remove(index)} disabled={fields.length === 1} className="h-10">
                                                <Trash2 className="h-4 w-4 text-red-500" />
                                            </Button>
                                        </div>
                                    </div>
                                );
                            })}
                        </div>

                        {/* Dynamic Tax and Totals Breakdown */}
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-6 pt-8 border-t mt-8">
                            <div>
                                {taxEstimate.taxList.length > 0 && (
                                    <div className="p-4 bg-muted/40 rounded-lg space-y-2 border">
                                        <div className="text-sm font-semibold text-muted-foreground uppercase tracking-wider mb-2">Estimated Sales Levies & VAT Details</div>
                                        <div className="space-y-1">
                                            {taxEstimate.taxList.map(t => (
                                                <div key={t.code} className="flex justify-between text-sm">
                                                    <span>{t.name} ({t.rate}%)</span>
                                                    <span className="font-medium">{formatAmountWithCurrency(t.amount)}</span>
                                                </div>
                                            ))}
                                        </div>
                                    </div>
                                )}
                            </div>
                            <div className="flex flex-col items-end space-y-2 text-right">
                                <div className="flex justify-between w-72 text-sm text-muted-foreground">
                                    <span>Subtotal (Net):</span>
                                    <span className="font-medium">{formatAmountWithCurrency(subtotal)}</span>
                                </div>
                                <div className="flex justify-between w-72 text-sm text-muted-foreground">
                                    <span>Est. Sales Taxes:</span>
                                    <span className="font-medium text-amber-600">+{formatAmountWithCurrency(totalTax)}</span>
                                </div>
                                {documentDiscount > 0 && (
                                    <div className="flex justify-between w-72 text-sm text-muted-foreground">
                                        <span>Document Trade Discount:</span>
                                        <span className="font-medium text-red-600">-{formatAmountWithCurrency(documentDiscount)}</span>
                                    </div>
                                )}
                                <div className="flex justify-between w-72 text-xl font-bold border-t pt-2 mt-2">
                                    <span>Grand Total:</span>
                                    <span className="text-primary">{formatAmountWithCurrency(totalAmount)}</span>
                                </div>
                            </div>
                        </div>

                    </CardContent>
                    <CardFooter className="flex justify-end space-x-2 bg-muted/50 p-4">
                        <Button variant="outline" type="button" onClick={() => router.back()}>
                            Cancel
                        </Button>
                        <Button type="submit" disabled={isSubmitting}>
                            {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Create Invoice
                        </Button>
                    </CardFooter>
                </Card>
            </form>
        </div>
    );
}
