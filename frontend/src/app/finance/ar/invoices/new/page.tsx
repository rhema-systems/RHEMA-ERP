'use client';

import { useState, useEffect, useRef } from 'react';
import Link from 'next/link';
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
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { loadApprovedInvoiceRate } from '@/lib/finance/invoice-exchange-rate';
import { useTenant } from '@/contexts/TenantContext';
import { SourceDocumentDimensionPanel } from '@/components/finance/dimensions/source-document-dimension-panel';
import {
    toFinancePostingDimensionValues,
    toFinanceSourceDimensionFormState,
} from '@/lib/finance/source-document-dimensions';
import {
    allocateDocumentTradeDiscount,
    calculateNetTradeDiscountLineAmount,
} from '@/lib/finance/invoice-trade-discount';
import {
    isEligibleManualArRevenueAccount,
    taxGroupForNewArInvoiceLine,
} from '@/lib/finance/ar-invoice-entry';
import { resolveInvoiceLineTaxSelection } from '@/lib/finance/invoice-tax-selection';

const lineItemSchema = z.object({
    sourceLineId: z.string().uuid(),
    lineItemType: z.literal('GLAccount').default('GLAccount'),
    productId: z.string().optional(),
    glAccountId: z.string().optional(),
    description: z.string().min(1, 'Description is required'),
    quantity: z.coerce.number().min(0.01, 'Quantity must be positive'),
    unitPrice: z.coerce.number().min(0, 'Unit price must be positive'),
    discountPercentage: z.coerce.number().min(0).max(100).optional().default(0),
    taxGroupId: z.string().optional(),
    taxTreatment: z.coerce.number().int().min(1).max(5).optional().default(1),
});

const invoiceSchema = z.object({
    businessPartnerId: z.string().min(1, 'Customer is required'),
    invoiceDate: z.date(),
    dueDate: z.date(),
    currencyCode: z.string().default('GHS'),
    exchangeRate: z.coerce.number().min(0.0001).optional().default(1.0),
    exchangeRateId: z.string().optional(),
    exchangeRateDate: z.date().optional(),
    exchangeRateSource: z.string().optional().default('Daily'),
    currencyOverrideReason: z.string().max(500, 'Override reason cannot exceed 500 characters').optional().default(''),
    paymentTermId: z.string().optional(),
    discountAmount: z.coerce.number().min(0).optional().default(0),
    discountReason: z.string().max(500, 'Discount reason cannot exceed 500 characters').optional().default(''),
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
            message: 'Document discount cannot exceed the net line amount',
        });
    }
    const hasDiscount = (invoice.discountAmount || 0) > 0 ||
        invoice.lineItems.some(line => (line.discountPercentage || 0) > 0);
    if (hasDiscount && (invoice.discountReason || '').trim().length < 10) {
        context.addIssue({
            code: z.ZodIssueCode.custom,
            path: ['discountReason'],
            message: 'Explain the commercial reason for the discount in at least 10 characters',
        });
    }
    if (!invoice.isOpeningBalance) {
        invoice.lineItems.forEach((line, index) => {
            if (!line.glAccountId) {
                context.addIssue({
                    code: z.ZodIssueCode.custom,
                    path: ['lineItems', index, 'glAccountId'],
                    message: 'Revenue account is required',
                });
            }
        });
    }
});

type InvoiceFormValues = z.infer<typeof invoiceSchema>;

export function InvoiceFormPage({ editInvoiceId }: { editInvoiceId?: string }) {
    const router = useRouter();
    const queryClient = useQueryClient();
    const searchParams = useSearchParams();
    const preselectedBusinessPartnerId = searchParams.get('businessPartnerId');
    const defaultOpeningBalance = searchParams.get('openingBalance') === 'true';
    const { toast } = useToast();
    const { currentTenantCode } = useTenant();
    const exchangeRateRequestId = useRef(0);
    const editHydratedRef = useRef(false);
    const isEditMode = Boolean(editInvoiceId);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [selectedCustomer, setSelectedCustomer] = useState<any>(null);
    const [customerComboOpen, setCustomerComboOpen] = useState(false);
    const [customerSearch, setCustomerSearch] = useState('');
    const [glAccountOpenIndex, setGlAccountOpenIndex] = useState<number | null>(null);
    const [glAccountSearch, setGlAccountSearch] = useState('');
    const [defaultDimensionValues, setDefaultDimensionValues] = useState<Record<string, string>>({});
    const [lineDimensionValues, setLineDimensionValues] = useState<Record<string, Record<string, string>>>({});
    const [applyDefaultToAll, setApplyDefaultToAll] = useState(false);

    const {
        data: editInvoice,
        isLoading: editInvoiceLoading,
        error: editInvoiceError,
    } = useQuery({
        queryKey: ['invoice', editInvoiceId],
        queryFn: () => editInvoiceId
            ? arService.getInvoice(editInvoiceId)
            : Promise.reject(new Error('An invoice id is required for editing.')),
        enabled: isEditMode,
    });

    // Fetch customers for the dropdown
    const { data: customersData, isLoading: customersLoading } = useQuery({
        queryKey: ['customers-list'],
        queryFn: () => arService.getCustomers({ pageSize: 100 }),
    });

    // Fetch active GL accounts for line item selection using financeDataService
    const { data: glAccountsData, isLoading: glAccountsLoading } = useQuery({
        queryKey: ['gl-accounts-active'],
        queryFn: async () => {
            const accounts = await financeDataService.getAccounts({ status: 'Active', accountType: 'Revenue' });
            return {
                items: accounts.filter(isEligibleManualArRevenueAccount)
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

    const { data: activeCurrencies = [] } = useQuery({
        queryKey: ['finance-currencies', currentTenantCode, 'active'],
        queryFn: () => financeDataService.getCurrencies({ isActive: true }),
        enabled: Boolean(currentTenantCode),
    });

    const transactionTaxGroups = (taxGroupsData || []).filter((group: any) =>
        !group.components?.some((component: any) =>
            component.taxCategory === 'Withholding' || component.taxCategory === 'VatWithholding')
    );

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
            businessPartnerId: preselectedBusinessPartnerId || '',
            invoiceDate: new Date(),
            dueDate: addDays(new Date(), 30),
            currencyCode: 'GHS',
            exchangeRate: 1.0,
            exchangeRateId: undefined,
            exchangeRateDate: new Date(),
            exchangeRateSource: 'Daily',
            currencyOverrideReason: '',
            paymentTermId: 'none',
            discountAmount: 0,
            discountReason: '',
            isOpeningBalance: defaultOpeningBalance,
            notes: '',
            lineItems: [
                { sourceLineId: crypto.randomUUID(), lineItemType: 'GLAccount' as const, glAccountId: '', description: '', quantity: 1, unitPrice: 0, discountPercentage: 0 }
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
    const selectedPaymentTerm = paymentTerms.find(term => term.id === watchPaymentTermId);
    const customerCurrency = selectedCustomer?.currencyCode?.trim().toUpperCase() || '';
    const currencyOverridesCustomer = Boolean(selectedCustomer && customerCurrency && watchCurrencyCode !== customerCurrency);
    const currencyOptions = activeCurrencies.map((currency: any) => ({
        id: currency.id,
        currencyCode: currency.currencyCode,
        currencyName: currency.currencyName,
    }));
    for (const code of [watchCurrencyCode, customerCurrency, financeSettings?.baseCurrency || 'GHS'].filter(Boolean)) {
        if (!currencyOptions.some((currency: any) => currency.currencyCode === code)) {
            currencyOptions.push({ id: `fallback-${code}`, currencyCode: code, currencyName: 'Configured currency' });
        }
    }
    const documentDiscount = Number(form.watch('discountAmount')) || 0;
    const hasInvoiceDiscount = documentDiscount > 0 ||
        watchLineItems.some(line => (Number(line.discountPercentage) || 0) > 0);

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
            const activeGroup = transactionTaxGroups.find((tg: any) => tg.id === activeGroupId);

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
    const onCustomerChange = async (businessPartnerId: string) => {
        form.setValue('businessPartnerId', businessPartnerId);
        if (!customersData?.items) return;

        const customer = customersData.items.find(c => c.id === businessPartnerId);
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
        if (preselectedBusinessPartnerId && customersData?.items) {
            onCustomerChange(preselectedBusinessPartnerId);
        }
    }, [preselectedBusinessPartnerId, customersData]);

    useEffect(() => {
        if (!isEditMode || !editInvoice || !customersData?.items || editHydratedRef.current) return;
        const customer = customersData.items.find(item => item.id === editInvoice.businessPartnerId);
        if (!customer) return;

        editHydratedRef.current = true;
        setSelectedCustomer(customer);
        const dimensionState = toFinanceSourceDimensionFormState(editInvoice.financeDimensions);
        setDefaultDimensionValues(dimensionState.defaultValues);
        setLineDimensionValues(dimensionState.lineValues);
        setApplyDefaultToAll(false);
        form.reset({
            businessPartnerId: editInvoice.businessPartnerId,
            invoiceDate: new Date(editInvoice.invoiceDate),
            dueDate: editInvoice.dueDate ? new Date(editInvoice.dueDate) : addDays(new Date(editInvoice.invoiceDate), 30),
            currencyCode: editInvoice.currencyCode,
            exchangeRate: editInvoice.exchangeRate || 1,
            exchangeRateId: editInvoice.exchangeRateId || undefined,
            exchangeRateDate: new Date(editInvoice.invoiceDate),
            exchangeRateSource: editInvoice.exchangeRateId ? 'Daily' : 'Functional',
            currencyOverrideReason: editInvoice.currencyOverrideReason || '',
            paymentTermId: editInvoice.paymentTermId || 'none',
            discountAmount: editInvoice.discountAmount || 0,
            discountReason: editInvoice.discountReason || '',
            isOpeningBalance: editInvoice.isOpeningBalance,
            notes: editInvoice.notes || '',
            taxGroupId: editInvoice.taxGroupId || 'none',
            lineItems: editInvoice.lineItems.map(line => ({
                sourceLineId: line.id,
                lineItemType: 'GLAccount' as const,
                productId: line.productId,
                glAccountId: line.glAccountId || '',
                description: line.description,
                quantity: line.quantity,
                unitPrice: line.unitPrice,
                discountPercentage: line.discountPercentage || 0,
                taxGroupId: line.taxGroupId || 'none',
                taxTreatment: Number(line.taxTreatment ?? 1),
            })),
        });
    }, [customersData?.items, editInvoice, form, isEditMode]);


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
            if (currencyOverridesCustomer && (data.currencyOverrideReason || '').trim().length < 10) {
                form.setError('currencyOverrideReason', { message: 'Explain the customer-currency override in at least 10 characters' });
                toast({
                    title: 'Currency override reason required',
                    description: `This customer defaults to ${customerCurrency}. Record why ${data.currencyCode} is appropriate for this invoice.`,
                    variant: 'destructive',
                });
                return;
            }
            const request = {
                ...data,
                invoiceDate: data.invoiceDate.toISOString(),
                dueDate: data.dueDate.toISOString(),
                taxGroupId: isOpeningBalance || data.taxGroupId === 'none' ? null : (data.taxGroupId || null),
                exchangeRate: Number(data.exchangeRate) || 1.0,
                exchangeRateId: data.exchangeRateId,
                currencyOverrideReason: currencyOverridesCustomer ? data.currencyOverrideReason?.trim() : null,
                paymentTermId: data.paymentTermId === 'none' ? null : (data.paymentTermId || null),
                discountAmount: Number(data.discountAmount) || 0,
                discountReason: hasInvoiceDiscount ? data.discountReason?.trim() : null,
                isOpeningBalance,
                lineItems: data.lineItems.map(item => {
                    const taxSelection = resolveInvoiceLineTaxSelection({
                        lineTaxGroupId: item.taxGroupId,
                        defaultTaxGroupId: data.taxGroupId,
                        taxTreatment: item.taxTreatment,
                        isOpeningBalance,
                    });
                    return {
                        id: item.sourceLineId,
                        lineItemType: item.lineItemType,
                        productId: item.productId,
                        glAccountId: isOpeningBalance ? undefined : (item.glAccountId || undefined),
                        description: item.description,
                        quantity: Number(item.quantity),
                        unitPrice: Number(item.unitPrice),
                        discountPercentage: Number(item.discountPercentage),
                        taxGroupId: taxSelection.taxGroupId,
                        taxTreatment: taxSelection.taxTreatment,
                    };
                }),
                financeDimensions: {
                    defaultDimensions: toFinancePostingDimensionValues(defaultDimensionValues),
                    lines: data.lineItems.flatMap(item => {
                        const accountId = !isOpeningBalance ? item.glAccountId : undefined;
                        return accountId ? [{
                            sourceLineId: item.sourceLineId,
                            accountId,
                            dimensions: toFinancePostingDimensionValues(lineDimensionValues[item.sourceLineId] || {}),
                        }] : [];
                    }),
                    applyDefaultToEligibleLines: applyDefaultToAll,
                },
            };

            if (isEditMode && editInvoice) {
                await arService.updateInvoice(editInvoice.id, {
                    id: editInvoice.id,
                    invoiceDate: request.invoiceDate,
                    dueDate: request.dueDate,
                    reference: editInvoice.reference,
                    notes: request.notes,
                    currencyCode: request.currencyCode,
                    exchangeRate: request.exchangeRate,
                    exchangeRateId: request.exchangeRateId,
                    discountAmount: request.discountAmount,
                    discountReason: request.discountReason,
                    taxGroupId: request.taxGroupId,
                    isOpeningBalance: request.isOpeningBalance,
                    lineItems: request.lineItems,
                    financeDimensions: request.financeDimensions,
                });
            } else {
                await arService.createInvoice(request);
            }

            await queryClient.invalidateQueries({ queryKey: ['invoices'] });
            if (editInvoice) {
                await queryClient.invalidateQueries({ queryKey: ['invoice', editInvoice.id] });
            }

            toast({
                title: 'Success',
                description: isEditMode ? 'Invoice updated successfully' : 'Invoice created successfully',
            });

            router.push(isEditMode && editInvoice ? `/finance/ar/invoices/${editInvoice.id}` : '/finance/ar/invoices');
        } catch (error: any) {
            toast({
                title: 'Error',
                description: error.message || `Failed to ${isEditMode ? 'update' : 'create'} invoice`,
                variant: 'destructive',
            });
        } finally {
            setIsSubmitting(false);
        }
    };

    if (isEditMode && (editInvoiceLoading || !editHydratedRef.current) && !editInvoiceError) {
        return <div className="flex min-h-[50vh] items-center justify-center"><Loader2 className="h-8 w-8 animate-spin text-muted-foreground" /></div>;
    }

    if (isEditMode && (editInvoiceError || !editInvoice || !['Draft', 'Rejected'].includes(editInvoice.status))) {
        return (
            <div className="mx-auto max-w-2xl space-y-4 p-8">
                <h1 className="text-2xl font-bold">Unable to edit customer invoice</h1>
                <p className="text-muted-foreground">
                    {editInvoiceError instanceof Error
                        ? editInvoiceError.message
                        : 'Only Draft or Rejected invoices may be changed.'}
                </p>
                <Button variant="outline" onClick={() => router.push(editInvoice ? `/finance/ar/invoices/${editInvoice.id}` : '/finance/ar/invoices')}>
                    Back to invoice
                </Button>
            </div>
        );
    }

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
                        <CardTitle>{isEditMode ? 'Edit Invoice' : 'Invoice Details'}</CardTitle>
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
                                        disabled={isEditMode}
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
                                            <CommandGroup>
                                                {customersLoading && <div className="px-3 py-6 text-center text-sm text-muted-foreground">Loading customers…</div>}
                                                {!customersLoading && customerSearch.trim() && filteredCustomers.length === 0 && (
                                                    <div className="px-3 py-6 text-center text-sm text-muted-foreground">
                                                        No customer matches “{customerSearch}”.
                                                    </div>
                                                )}
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
                            {form.formState.errors.businessPartnerId && (
                                <p className="text-sm text-red-500">{form.formState.errors.businessPartnerId.message}</p>
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
                                            {currencyOptions.map((currency: any) => (
                                                <SelectItem key={currency.id} value={currency.currencyCode}>
                                                    {currency.currencyCode} - {currency.currencyName}
                                                    {currency.currencyCode === customerCurrency ? ' (customer default)' : ''}
                                                </SelectItem>
                                            ))}
                                        </SelectContent>
                                    </Select>
                                )}
                            />
                        </div>

                        {currencyOverridesCustomer && (
                            <div className="space-y-2 rounded-lg border border-amber-300 bg-amber-50 p-4 md:col-span-2">
                                <Label htmlFor="currencyOverrideReason" className="text-amber-900">Customer-currency override reason</Label>
                                <Textarea
                                    id="currencyOverrideReason"
                                    placeholder={`Explain why this invoice is in ${watchCurrencyCode} instead of the approved customer default ${customerCurrency}.`}
                                    {...form.register('currencyOverrideReason')}
                                />
                                <p className="text-xs text-amber-800">Required for audit whenever a manual invoice departs from the customer currency.</p>
                                {form.formState.errors.currencyOverrideReason && (
                                    <p className="text-sm text-red-600">{form.formState.errors.currencyOverrideReason.message}</p>
                                )}
                            </div>
                        )}

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
                                <div className="flex flex-wrap items-center justify-between gap-2 text-[11px] text-muted-foreground">
                                    <span>
                                        1 {watchCurrencyCode} = {form.watch('exchangeRate')} {financeSettings?.baseCurrency || 'GHS'}
                                        {' · approved rate locked to this invoice'}
                                    </span>
                                    <Button asChild type="button" variant="link" size="sm" className="h-auto p-0 text-xs">
                                        <Link href="/finance/exchange-rates">Manage exchange rates</Link>
                                    </Button>
                                </div>
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
                                            {transactionTaxGroups.map((tg: any) => (
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
                        <CardTitle>Discounts and settlement terms</CardTitle>
                        <CardDescription>
                            Record invoice-time price reductions separately from any early-payment discount in the customer&apos;s payment term.
                        </CardDescription>
                    </CardHeader>
                    <CardContent className="grid grid-cols-1 gap-5 lg:grid-cols-2">
                        <div className="space-y-2">
                            <Label htmlFor="discountAmount">Document discount amount ({watchCurrencyCode})</Label>
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
                            <p className="text-xs text-muted-foreground">
                                Use this only for a discount shared across the invoice. Finance allocates it proportionately after line discounts, reducing revenue and the taxable base. Put a discount specific to one service on that line instead.
                            </p>
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="discountReason">
                                Discount reason {hasInvoiceDiscount && <span className="text-destructive">*</span>}
                            </Label>
                            <Textarea
                                id="discountReason"
                                rows={3}
                                placeholder="Commercial approval, contract clause, promotion, pricing correction, etc."
                                {...form.register('discountReason')}
                            />
                            {form.formState.errors.discountReason && (
                                <p className="text-sm text-red-500">{form.formState.errors.discountReason.message}</p>
                            )}
                            <p className="text-xs text-muted-foreground">
                                Required whenever a line or document discount is used. A discounted manual invoice must pass the configured Invoice approval workflow before posting.
                            </p>
                        </div>
                        <div className="rounded-lg border bg-muted/20 p-4 lg:col-span-2">
                            <div className="text-sm font-semibold">Payment-term discount</div>
                            {selectedPaymentTerm && (selectedPaymentTerm.discountPercent || 0) > 0 ? (
                                <div className="mt-1 space-y-1 text-sm text-muted-foreground">
                                    <p>
                                        {selectedPaymentTerm.discountPercent}% if settled within {selectedPaymentTerm.discountDays} day(s). This is evaluated when payment is allocated; it is not deducted from this invoice now.
                                    </p>
                                    <p>
                                        For invoices carrying VAT or levies, any later reduction in taxable consideration must use the approved sales credit/adjustment-note process. Direct receipt discounts are blocked.
                                    </p>
                                </div>
                            ) : (
                                <p className="mt-1 text-sm text-muted-foreground">The selected payment term does not grant an early-payment discount.</p>
                            )}
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
                                accountId: !watchIsOpeningBalance ? item.glAccountId : undefined,
                                accountLabel: item.description || undefined,
                                accountResolution: watchIsOpeningBalance ? 'SourceDocument' as const : 'UserSelection' as const,
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
                    <CardHeader className="flex flex-row items-start justify-between gap-4">
                        <div className="space-y-1">
                            <CardTitle>Invoice lines</CardTitle>
                            <CardDescription>
                                Manual AR invoices post each line to an approved revenue account. Source-driven sales invoices are created by their owning workflow.
                            </CardDescription>
                        </div>
                        <Button
                            type="button"
                            variant="outline"
                            size="sm"
                            className="shrink-0"
                            onClick={() => append({
                                sourceLineId: crypto.randomUUID(),
                                lineItemType: 'GLAccount' as const,
                                glAccountId: '',
                                description: '',
                                quantity: 1,
                                unitPrice: 0,
                                discountPercentage: 0,
                                taxGroupId: taxGroupForNewArInvoiceLine(watchTaxGroupId, watchIsOpeningBalance),
                                taxTreatment: 1,
                            })}
                        >
                            <Plus className="mr-2 h-4 w-4" /> Add invoice line
                        </Button>
                    </CardHeader>
                    <CardContent>
                        <div className="space-y-4">
                            {fields.map((field, index) => {
                                return (
                                    <div key={field.id} className="space-y-4 rounded-lg border p-4">
                                        <div className="flex flex-wrap items-start justify-between gap-3">
                                            <div>
                                                <div className="flex items-center gap-2">
                                                    <span className="font-semibold">Line {index + 1}</span>
                                                    <span className="rounded-full bg-muted px-2.5 py-1 text-xs font-medium text-muted-foreground">
                                                        {watchIsOpeningBalance ? 'Opening balance' : 'Manual revenue'}
                                                    </span>
                                                </div>
                                                <p className="mt-1 text-sm text-muted-foreground">
                                                    {watchIsOpeningBalance
                                                        ? 'Finance posts this line through the governed opening-balance clearing account.'
                                                        : 'Finance credits the selected revenue account and keeps receivables on the governed AR control account.'}
                                                </p>
                                            </div>
                                            <Button
                                                type="button"
                                                variant="ghost"
                                                size="icon"
                                                onClick={() => remove(index)}
                                                disabled={fields.length === 1}
                                                aria-label={`Remove invoice line ${index + 1}`}
                                            >
                                                <Trash2 className="h-4 w-4 text-red-500" />
                                            </Button>
                                        </div>

                                        {!watchIsOpeningBalance && (
                                            <div className="max-w-xl space-y-2">
                                                <Label>Revenue account</Label>
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
                                                                                    if (!form.getValues(`lineItems.${index}.description`)?.trim()) {
                                                                                        form.setValue(`lineItems.${index}.description`, account.accountName);
                                                                                    }
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
                                                {form.formState.errors.lineItems?.[index]?.glAccountId && (
                                                    <p className="text-xs text-red-500">
                                                        {form.formState.errors.lineItems[index]?.glAccountId?.message}
                                                    </p>
                                                )}
                                                <p className="text-xs text-muted-foreground">
                                                    Only active, direct-posting revenue accounts are available. Control accounts cannot be selected.
                                                </p>
                                            </div>
                                        )}

                                        <div className="grid grid-cols-1 gap-4 md:grid-cols-12 lg:grid-cols-[minmax(190px,1.4fr)_72px_110px_140px_minmax(190px,1fr)] lg:items-end">
                                            <div className="space-y-2 md:col-span-5 lg:col-auto">
                                                <Label>Description</Label>
                                                <Input {...form.register(`lineItems.${index}.description` as const)} placeholder="What is being billed?" />
                                                {form.formState.errors.lineItems?.[index]?.description && (
                                                    <p className="text-xs text-red-500">{form.formState.errors.lineItems[index]?.description?.message}</p>
                                                )}
                                            </div>
                                            <div className="space-y-2 md:col-span-2 lg:col-auto">
                                                <Label>Qty</Label>
                                                <Input type="number" step="0.01" {...form.register(`lineItems.${index}.quantity` as const)} className="text-center" />
                                            </div>
                                            <div className="space-y-2 md:col-span-2 lg:col-auto">
                                                <Label>Unit price</Label>
                                                <Input type="number" step="0.01" {...form.register(`lineItems.${index}.unitPrice` as const)} className="text-right" />
                                            </div>
                                            <div className="space-y-2 md:col-span-3 lg:col-auto">
                                                <Label className="whitespace-nowrap">Line discount %</Label>
                                                <Input type="number" min="0" max="100" step="0.5" {...form.register(`lineItems.${index}.discountPercentage` as const)} className="text-center" />
                                                {form.formState.errors.lineItems?.[index]?.discountPercentage && (
                                                    <p className="text-xs text-red-500">Use 0–100</p>
                                                )}
                                            </div>
                                            <div className="space-y-2 md:col-span-5 lg:col-auto">
                                                <Label className="font-semibold text-amber-600">Tax group</Label>
                                                <Controller
                                                    control={form.control}
                                                    name={`lineItems.${index}.taxGroupId`}
                                                    render={({ field }) => (
                                                        <Select
                                                            value={watchIsOpeningBalance ? 'none' : (field.value || watchTaxGroupId || 'none')}
                                                            onValueChange={field.onChange}
                                                            disabled={watchIsOpeningBalance}
                                                        >
                                                            <SelectTrigger className={cn(watchIsOpeningBalance && 'bg-muted text-muted-foreground')}>
                                                                <SelectValue placeholder="Zero-rated / Exempt" />
                                                            </SelectTrigger>
                                                            <SelectContent>
                                                                <SelectItem value="none">Zero-rated / Exempt</SelectItem>
                                                                {transactionTaxGroups.map((group: any) => (
                                                                    <SelectItem key={group.id} value={group.id}>
                                                                        {group.name}
                                                                    </SelectItem>
                                                                ))}
                                                            </SelectContent>
                                                        </Select>
                                                    )}
                                                />
                                            </div>
                                        </div>
                                    </div>
                                );
                            })}
                        </div>

                        {/* Dynamic Tax and Totals Breakdown */}
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-6 pt-8 border-t mt-8">
                            <div className="space-y-4">
                                <div className="rounded-lg border p-4">
                                    <div className="mb-3 text-sm font-semibold uppercase tracking-wider text-muted-foreground">Posting readiness</div>
                                    <ul className="space-y-2 text-sm">
                                        <li className={selectedCustomer ? 'text-emerald-700' : 'text-amber-700'}>
                                            {selectedCustomer ? '✓' : '○'} Approved customer and AR profile
                                        </li>
                                        <li className={watchCurrencyCode === (financeSettings?.baseCurrency || 'GHS') || Boolean(form.watch('exchangeRateId')) ? 'text-emerald-700' : 'text-amber-700'}>
                                            {watchCurrencyCode === (financeSettings?.baseCurrency || 'GHS') || Boolean(form.watch('exchangeRateId')) ? '✓' : '○'} Currency and approved exchange-rate evidence
                                        </li>
                                        <li className={!currencyOverridesCustomer || (form.watch('currencyOverrideReason') || '').trim().length >= 10 ? 'text-emerald-700' : 'text-amber-700'}>
                                            {!currencyOverridesCustomer || (form.watch('currencyOverrideReason') || '').trim().length >= 10 ? '✓' : '○'} Customer-currency policy or documented override
                                        </li>
                                        <li className={watchLineItems.every(line => line.description && Number(line.quantity) > 0 && Number(line.unitPrice) >= 0 && (watchIsOpeningBalance || line.glAccountId)) ? 'text-emerald-700' : 'text-amber-700'}>
                                            {watchLineItems.every(line => line.description && Number(line.quantity) > 0 && Number(line.unitPrice) >= 0 && (watchIsOpeningBalance || line.glAccountId)) ? '✓' : '○'} Complete, governed invoice lines
                                        </li>
                                        <li className={!hasInvoiceDiscount || (form.watch('discountReason') || '').trim().length >= 10 ? 'text-emerald-700' : 'text-amber-700'}>
                                            {!hasInvoiceDiscount || (form.watch('discountReason') || '').trim().length >= 10 ? '✓' : '○'} Discount reason and approval evidence
                                        </li>
                                    </ul>
                                </div>
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
                            <div className="rounded-lg border p-5">
                                <div className="mb-4 text-sm font-semibold uppercase tracking-wider text-muted-foreground">
                                    Invoice totals
                                </div>
                                <div className="space-y-3">
                                    <div className="grid grid-cols-[1fr_auto] items-baseline gap-6 text-sm text-muted-foreground">
                                        <span>Subtotal after line discounts</span>
                                        <span className="font-medium text-foreground">{formatAmountWithCurrency(subtotal)}</span>
                                    </div>
                                    {documentDiscount > 0 && (
                                        <div className="grid grid-cols-[1fr_auto] items-baseline gap-6 text-sm text-muted-foreground">
                                            <span>Document trade discount</span>
                                            <span className="font-medium text-red-600">-{formatAmountWithCurrency(documentDiscount)}</span>
                                        </div>
                                    )}
                                    <div className="grid grid-cols-[1fr_auto] items-baseline gap-6 text-sm text-muted-foreground">
                                        <span>Estimated levies and VAT</span>
                                        <span className="font-medium text-amber-600">+{formatAmountWithCurrency(totalTax)}</span>
                                    </div>
                                    <div className="grid grid-cols-[1fr_auto] items-baseline gap-6 border-t pt-4 text-xl font-bold">
                                        <span>Invoice total</span>
                                        <span className="text-primary">{formatAmountWithCurrency(totalAmount)}</span>
                                    </div>
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
                            {isEditMode ? 'Save Changes' : 'Create Invoice'}
                        </Button>
                    </CardFooter>
                </Card>
            </form>
        </div>
    );
}

export default function NewInvoicePage() {
    return <InvoiceFormPage />;
}
