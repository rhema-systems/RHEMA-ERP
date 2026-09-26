'use client';

import { useState, useEffect, useMemo } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
    ArrowLeft,
    Loader2,
    Calendar as CalendarIcon,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
    Card,
    CardContent,
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
import {
    Popover,
    PopoverContent,
    PopoverTrigger,
} from '@/components/ui/popover';
import { Calendar } from '@/components/ui/calendar';
import { accountsPayableService } from '@/services/accountsPayableService';
import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import { financeService } from '@/services/finance.service';
import { taxDataService } from '@/services/finance/tax-data.service';
import { allocateInvoiceCash, invoiceWithholdingChoice, paymentWithholdingChoice } from '@/lib/finance/ap-payment-withholding';
import { PaymentMethodType } from '@/types/cash-management';
import { TaxApplicability, TaxCategory, type Tax, type WhtCalculationResult } from '@/types/tax';
import type { OutstandingVendorInvoice } from '@/types/ap';
import { useToast } from '@/components/ui/use-toast';
import { formatCurrency, cn } from '@/lib/utils';
import { useQuery } from '@tanstack/react-query';
import { Skeleton } from '@/components/ui/skeleton';
import { format } from 'date-fns';
import {
    VendorPaymentReadinessBadge,
    VendorPaymentReadinessControl,
} from '@/components/finance/VendorPaymentReadinessControl';
import { loadApprovedSettlementRate } from '@/lib/finance/settlement-exchange-rate';
import { SourceDocumentDimensionPanel } from '@/components/finance/dimensions/source-document-dimension-panel';
import { toFinancePostingDimensionValues } from '@/lib/finance/source-document-dimensions';
import {
    canSubmitApPayment,
    getApPaymentAllocationState,
    hasApPaymentSettlementComponents,
    type ApPaymentAllocationRow,
} from '@/lib/finance/ap-payment-allocation';

const paymentSchema = z.object({
    supplierId: z.string().min(1, 'Supplier is required'),
    bankAccountId: z.string().min(1, 'Bank account is required'),
    paymentDate: z.date(),
    totalAmount: z.coerce.number().min(0.01, 'Amount must be positive'),
    paymentMethod: z.enum(['BankTransfer', 'Cheque', 'Cash', 'WireTransfer', 'MobileMoney', 'DirectDebit', 'Other']).default('BankTransfer'),
    paymentMethodId: z.string().optional(),
    transactionReference: z.string().optional(),
    currencyCode: z.string().default('GHS'),
    exchangeRate: z.coerce.number().min(0.0001, 'Exchange rate must be greater than 0').default(1),
    exchangeRateId: z.string().optional(),
    withholdingTaxId: z.string().optional(),
    withholdingTaxAccountId: z.string().optional(),
    withholdingTaxRate: z.coerce.number().min(0).max(100).optional().default(0),
    withholdingCertificateNumber: z.string().optional(),
    notes: z.string().optional(),
});

type PaymentFormValues = z.infer<typeof paymentSchema>;

const toVendorPaymentMethod = (type?: PaymentMethodType): PaymentFormValues['paymentMethod'] => {
    switch (type) {
        case PaymentMethodType.Cash:
            return 'Cash';
        case PaymentMethodType.Cheque:
            return 'Cheque';
        case PaymentMethodType.MobileMoney:
            return 'MobileMoney';
        case PaymentMethodType.DirectDebit:
            return 'DirectDebit';
        case PaymentMethodType.EFT:
        case PaymentMethodType.BankTransfer:
        case PaymentMethodType.StandingOrder:
            return 'BankTransfer';
        default:
            return 'Other';
    }
};

const roundMoney = (amount: number) => Math.round((amount + Number.EPSILON) * 100) / 100;

export default function NewVendorPaymentPage() {
    const router = useRouter();
    const searchParams = useSearchParams();
    const preselectedSupplierId = searchParams.get('businessPartnerId') || searchParams.get('supplierId');
    const preselectedInvoiceId = searchParams.get('invoiceId');
    const existingAdvancePaymentId = searchParams.get('paymentId');
    const isLinkedInvoicePayment =
        searchParams.get('locked') === 'true' &&
        searchParams.get('source') === 'land-acquisition' &&
        !!preselectedInvoiceId &&
        !existingAdvancePaymentId;
    const preselectedBankAccountId = searchParams.get('bankAccountId') || '';
    const preselectedPaymentMethodId = searchParams.get('paymentMethodId') || '';
    const preselectedAmountParam = searchParams.get('amount');
    const preselectedAmount = preselectedAmountParam && Number.isFinite(Number(preselectedAmountParam))
        ? Number(preselectedAmountParam)
        : 0;
    const preselectedPaymentDateParam = searchParams.get('paymentDate');
    const preselectedPaymentDate = preselectedPaymentDateParam && !Number.isNaN(Date.parse(preselectedPaymentDateParam))
        ? new Date(preselectedPaymentDateParam)
        : new Date();
    const preselectedReferenceNumber = searchParams.get('referenceNumber') || '';
    const preselectedDescription = searchParams.get('description') || '';
    const { toast } = useToast();
    const [isSubmitting, setIsSubmitting] = useState(false);
    // Invoice allocations and payment consumption are deliberately separate. A USD bank
    // payment can settle a GHS invoice, so reusing one number here would recreate FIN-LIM-0022.
    const [allocations, setAllocations] = useState<Record<string, number>>({});
    const [paymentCurrencyAllocations, setPaymentCurrencyAllocations] = useState<Record<string, number>>({});
    const [discountAllocations, setDiscountAllocations] = useState<Record<string, number>>({});
    const [withholdingAllocations, setWithholdingAllocations] = useState<Record<string, number>>({});
    const [withholdingCalculation, setWithholdingCalculation] = useState<WhtCalculationResult | null>(null);
    const [isCalculatingWithholding, setIsCalculatingWithholding] = useState(false);
    const [defaultDimensionValues, setDefaultDimensionValues] = useState<Record<string, string>>({});
    const [applyDefaultToAll, setApplyDefaultToAll] = useState(false);

    const clearAllocationState = () => {
        setAllocations({});
        setPaymentCurrencyAllocations({});
        setDiscountAllocations({});
        setWithholdingAllocations({});
        setWithholdingCalculation(null);
    };

    const { data: suppliersData } = useQuery({
        queryKey: ['finance', 'ap', 'entry-suppliers'],
        queryFn: () => accountsPayableService.getInvoiceSupplierEntryOptions(),
    });

    const { data: bankAccounts } = useQuery({
        queryKey: ['bank-accounts', 'active'],
        queryFn: () => cashManagementDataService.getActiveBankAccounts(),
    });

    const { data: paymentMethods } = useQuery({
        queryKey: ['payment-methods', 'active'],
        queryFn: () => cashManagementDataService.getActivePaymentMethods(),
    });

    const { data: financeSettings } = useQuery({
        queryKey: ['finance-settings'],
        queryFn: () => financeService.getSettings(),
    });
    // GHS is TDC's configured default, but the settlement UI must follow tenant settings so the
    // component does not turn today's deployment decision into a hidden accounting invariant.
    const functionalCurrencyCode = (financeSettings?.baseCurrency || 'GHS').toUpperCase();

    const { data: withholdingTaxes } = useQuery({
        queryKey: ['taxes', 'withholding', 'active'],
        queryFn: () => taxDataService.getActiveTaxes({
            applicability: TaxApplicability.Purchases,
            category: TaxCategory.Withholding,
        }),
    });

    const form = useForm<PaymentFormValues>({
        // @ts-expect-error TODO: fix type
        resolver: zodResolver(paymentSchema),
        defaultValues: {
            supplierId: preselectedSupplierId || '',
            bankAccountId: preselectedBankAccountId,
            paymentDate: preselectedPaymentDate,
            totalAmount: preselectedAmount,
            paymentMethod: 'BankTransfer',
            paymentMethodId: preselectedPaymentMethodId || undefined,
            transactionReference: preselectedReferenceNumber,
            currencyCode: 'GHS',
            exchangeRate: 1,
            exchangeRateId: undefined,
            withholdingTaxId: undefined,
            withholdingTaxAccountId: undefined,
            withholdingTaxRate: 0,
            withholdingCertificateNumber: undefined,
            notes: preselectedDescription,
        },
    });

    const { data: existingAdvancePayment, isLoading: isLoadingAdvancePayment } = useQuery({
        queryKey: ['vendor-payment', existingAdvancePaymentId],
        queryFn: () => {
            if (!existingAdvancePaymentId) throw new Error('Supplier advance payment id is required.');
            return accountsPayableService.getPayment(existingAdvancePaymentId);
        },
        enabled: !!existingAdvancePaymentId,
    });

    const { data: linkedInvoice, isLoading: isLoadingLinkedInvoice } = useQuery({
        queryKey: ['vendor-invoice', preselectedInvoiceId],
        queryFn: () => {
            if (!preselectedInvoiceId) throw new Error('Vendor invoice id is required.');
            return accountsPayableService.getInvoice(preselectedInvoiceId);
        },
        enabled: !!preselectedInvoiceId,
    });

    useEffect(() => {
        if (!isLinkedInvoicePayment || !linkedInvoice) return;

        const amountDue = roundMoney(
            Number(linkedInvoice.balanceAmount ?? linkedInvoice.totalAmount) ||
            preselectedAmount ||
            0,
        );
        form.setValue('supplierId', linkedInvoice.businessPartnerId);
        form.setValue('totalAmount', amountDue);
        form.setValue('currencyCode', linkedInvoice.currencyCode || functionalCurrencyCode);
        form.setValue('exchangeRate', Number(linkedInvoice.exchangeRate) || 1);
        if (!form.getValues('notes')) {
            form.setValue('notes', `Payment for invoice ${linkedInvoice.invoiceNumber}`);
        }
    }, [
        form,
        functionalCurrencyCode,
        isLinkedInvoicePayment,
        linkedInvoice,
        preselectedAmount,
    ]);

    useEffect(() => {
        if (!existingAdvancePaymentId || !existingAdvancePayment) return;

        // A posted payment is the immutable supplier-advance currency lot. Populate the header
        // from that record and lock it in the UI; this screen creates only new application facts
        // and must never silently replace the lot's currency, origin rate, bank, or supplier.
        const remainingAdvance = roundMoney(
            Number(existingAdvancePayment.totalAmount) - Number(existingAdvancePayment.allocatedAmount || 0),
        );
        form.setValue('supplierId', existingAdvancePayment.supplierId);
        form.setValue('bankAccountId', existingAdvancePayment.bankAccountId || 'advance-origin');
        form.setValue('paymentDate', new Date(existingAdvancePayment.paymentDate));
        form.setValue('totalAmount', remainingAdvance);
        form.setValue('currencyCode', existingAdvancePayment.currencyCode || functionalCurrencyCode);
        form.setValue('exchangeRate', Number(existingAdvancePayment.exchangeRate) || 1);
        form.setValue('exchangeRateId', existingAdvancePayment.exchangeRateId);
        form.setValue('transactionReference', existingAdvancePayment.paymentNumber);
    }, [existingAdvancePayment, existingAdvancePaymentId, form, functionalCurrencyCode]);

    const selectedSupplierId = form.watch('supplierId');
    const selectedBankAccountId = form.watch('bankAccountId');
    const selectedPaymentMethodId = form.watch('paymentMethodId');
    const selectedWithholdingTaxId = form.watch('withholdingTaxId');
    const currentCurrencyCode = form.watch('currencyCode') || 'GHS';
    const selectedWithholdingTax = withholdingTaxes?.find((tax: Tax) => tax.id === selectedWithholdingTaxId);
    const withholdingTaxOptions = (withholdingTaxes ?? []).filter((tax: Tax) =>
        tax.category === TaxCategory.Withholding && (
            tax.applicability === TaxApplicability.Purchases ||
            tax.applicability === TaxApplicability.Both
        )
    );
    const supplierOptions = suppliersData ?? [];
    const selectedSupplierOption = supplierOptions.find(option =>
        option.businessPartnerId === selectedSupplierId && option.isTransactionReady);
    const lockedSupplierName =
        linkedInvoice?.supplierName ||
        supplierOptions.find((supplier) => supplier.id === selectedSupplierId)?.name ||
        'Linked supplier';

    useEffect(() => {
        if (existingAdvancePaymentId) return;
        if (!paymentMethods?.length) return;

        if (selectedPaymentMethodId) {
            const selectedMethod = paymentMethods.find((method) => method.id === selectedPaymentMethodId);
            if (selectedMethod) {
                form.setValue('paymentMethod', toVendorPaymentMethod(selectedMethod.type));
            }
            return;
        }

        const preferredMethod =
            paymentMethods.find((method) => method.type === PaymentMethodType.BankTransfer) ||
            paymentMethods.find((method) => method.type === PaymentMethodType.EFT) ||
            paymentMethods[0];

        form.setValue('paymentMethodId', preferredMethod.id);
        form.setValue('paymentMethod', toVendorPaymentMethod(preferredMethod.type));
    }, [paymentMethods, selectedPaymentMethodId, form, existingAdvancePaymentId]);

    useEffect(() => {
        if (existingAdvancePaymentId) return;
        if (isLinkedInvoicePayment) return;
        if (!selectedBankAccountId || !bankAccounts) return;

        const account = bankAccounts.find((item) => item.id === selectedBankAccountId);
        if (!account) return;

        form.setValue('currencyCode', account.currency);
    }, [selectedBankAccountId, bankAccounts, form, existingAdvancePaymentId, isLinkedInvoicePayment]);

    const watchedPaymentDate = form.watch('paymentDate');
    const [exchangeRateSource, setExchangeRateSource] = useState('Functional currency');

    useEffect(() => {
        if (existingAdvancePaymentId || !financeSettings || !watchedPaymentDate) return;
        form.setValue('exchangeRateId', undefined);
        setExchangeRateSource('Loading approved rate…');
        let cancelled = false;
        void loadApprovedSettlementRate(
            {
                module: 'AP',
                transactionCurrency: currentCurrencyCode,
                functionalCurrency: functionalCurrencyCode,
                settlementDate: watchedPaymentDate,
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
            form.setValue('exchangeRateId', undefined);
            setExchangeRateSource(error instanceof Error ? error.message : 'Approved rate unavailable');
        });
        return () => { cancelled = true; };
    }, [currentCurrencyCode, existingAdvancePaymentId, financeSettings, form, functionalCurrencyCode, watchedPaymentDate]);

    // Fetch outstanding invoices for selected supplier
    const { data: outstandingInvoices, isLoading: isLoadingInvoices } = useQuery({
        queryKey: ['outstanding-vendor-invoices', selectedSupplierId],
        queryFn: () => accountsPayableService.getOutstandingInvoices(selectedSupplierId),
        enabled: !!selectedSupplierId,
    });
    const displayedOutstandingInvoices =
        isLinkedInvoicePayment && preselectedInvoiceId
            ? outstandingInvoices?.filter((invoice) => invoice.invoiceId === preselectedInvoiceId)
            : outstandingInvoices;

    const selectedInvoiceWht = useMemo(() => {
        const selected = (outstandingInvoices ?? []).filter(invoice =>
            Number(allocations[invoice.invoiceId]) > 0 || Number(discountAllocations[invoice.invoiceId]) > 0 ||
            Number(withholdingAllocations[invoice.invoiceId]) > 0);
        try { return { choice: paymentWithholdingChoice(selected), error: undefined }; }
        catch (error) { return { choice: null, error: error instanceof Error ? error.message : 'Review invoice WHT choices.' }; }
    }, [outstandingInvoices, allocations, discountAllocations, withholdingAllocations]);

    useEffect(() => {
        if (selectedInvoiceWht.error) return;
        const choice = selectedInvoiceWht.choice;
        if (choice) {
            form.setValue('withholdingTaxId', choice.taxId);
            form.setValue('withholdingTaxRate', choice.rate);
            form.setValue('withholdingTaxAccountId', choice.accountId);
        } else {
            form.setValue('withholdingTaxRate', Number(selectedWithholdingTax?.rate) || 0);
            form.setValue('withholdingTaxAccountId', selectedWithholdingTax?.taxPayableAccountId ?? undefined);
        }
        if (!choice?.taxId && !selectedWithholdingTax) setWithholdingCalculation(null);
    }, [selectedInvoiceWht.choice?.taxId, selectedInvoiceWht.choice?.rate, selectedInvoiceWht.choice?.accountId,
        selectedInvoiceWht.error, selectedWithholdingTax, form]);

    // Pre-fill amount if invoice selected
    useEffect(() => {
        let cancelled = false;
        if (preselectedInvoiceId && outstandingInvoices) {
            const invoice = outstandingInvoices.find(inv => inv.invoiceId === preselectedInvoiceId);
            if (invoice?.paymentReadiness?.isPaymentReady) {
                void (async () => {
                    try {
                        const choice = invoiceWithholdingChoice(invoice);
                        let calculation = null;
                        if (!existingAdvancePaymentId && choice?.taxId) {
                            if (!invoice.withholdingContractReference || invoice.withholdingSupplyCategory == null) {
                                throw new Error(`${invoice.invoiceNumber} is missing its WHT contract/category scope.`);
                            }
                            calculation = await taxDataService.calculateApWithholding({
                                taxId: choice.taxId, businessPartnerId: form.getValues('supplierId'),
                                paymentDate: form.getValues('paymentDate').toISOString(),
                                taxableBase: invoice.balanceAmount,
                                vendorInvoiceIds: [invoice.invoiceId],
                                contractReference: invoice.withholdingContractReference,
                                supplyCategory: invoice.withholdingSupplyCategory,
                            });
                        }
                        if (cancelled) return;
                        const discountAmount = Number(invoice.discountAmount) || 0;
                        const withholdingAmount = calculation?.withholdingAmount ?? 0;
                        const netPaymentAmount = roundMoney(Math.max(invoice.balanceAmount - discountAmount - withholdingAmount, 0));
                        form.setValue('totalAmount', netPaymentAmount);
                        setAllocations({ [invoice.invoiceId]: netPaymentAmount });
                        setDiscountAllocations({ [invoice.invoiceId]: discountAmount });
                        setWithholdingAllocations(withholdingAmount > 0 ? { [invoice.invoiceId]: withholdingAmount } : {});
                        setWithholdingCalculation(calculation);
                    } catch (error) {
                        if (!cancelled) toast({ title: 'Review invoice withholding', description: error instanceof Error ? error.message : 'Unable to calculate invoice withholding.', variant: 'destructive' });
                    }
                })();
            } else if (isLinkedInvoicePayment && linkedInvoice?.id === preselectedInvoiceId) {
                const netPaymentAmount = roundMoney(
                    Number(linkedInvoice.balanceAmount ?? linkedInvoice.totalAmount) || 0,
                );
                setAllocations({ [preselectedInvoiceId]: netPaymentAmount });
                setDiscountAllocations({});
            }
        }
        return () => { cancelled = true; };
    }, [preselectedInvoiceId, outstandingInvoices, form, isLinkedInvoicePayment, linkedInvoice, existingAdvancePaymentId, toast]);


    const onSubmit = async (data: PaymentFormValues) => {
        setIsSubmitting(true);
        try {
            const selectedPaymentMethod = paymentMethods?.find((method) => method.id === data.paymentMethodId);
            if (!existingAdvancePaymentId && data.paymentMethodId && !selectedPaymentMethod) {
                form.setError('paymentMethodId', { type: 'manual', message: 'Selected payment method is not available' });
                toast({
                    title: 'Payment details incomplete',
                    description: 'Selected payment method is not available.',
                    variant: 'destructive',
                });
                return;
            }

            if (!existingAdvancePaymentId && selectedPaymentMethod?.requiresBankAccount && !data.bankAccountId) {
                form.setError('bankAccountId', { type: 'manual', message: `${selectedPaymentMethod.name} requires a bank account` });
                toast({
                    title: 'Payment details incomplete',
                    description: `${selectedPaymentMethod.name} requires a bank account.`,
                    variant: 'destructive',
                });
                return;
            }

            if (!existingAdvancePaymentId && selectedPaymentMethod?.requiresReference && !data.transactionReference?.trim()) {
                form.setError('transactionReference', { type: 'manual', message: `${selectedPaymentMethod.name} requires a reference number` });
                toast({
                    title: 'Payment details incomplete',
                    description: `${selectedPaymentMethod.name} requires a reference number.`,
                    variant: 'destructive',
                });
                return;
            }

            // Build from the union of all component keys. A discount/WHT-only row is still an
            // invoice allocation and must never be silently converted into a supplier advance.
            const selectedInvoiceIds = Array.from(new Set([
                ...Object.keys(allocations),
                ...Object.keys(paymentCurrencyAllocations),
                ...Object.keys(discountAllocations),
                ...Object.keys(withholdingAllocations),
            ])).filter(invoiceId => hasApPaymentSettlementComponents([{
                invoiceCash: Number(allocations[invoiceId]) || 0,
                paymentCurrencyCash: Number(paymentCurrencyAllocations[invoiceId]) || 0,
                discount: Number(discountAllocations[invoiceId]) || 0,
                withholdingTax: Number(withholdingAllocations[invoiceId]) || 0,
            }]));
            const paymentAllocations = selectedInvoiceIds
                .map((invoiceId) => {
                    const amount = Number(allocations[invoiceId]) || 0;
                    const invoice = outstandingInvoices?.find(item => item.invoiceId === invoiceId);
                    const isCrossCurrency = invoice?.currencyCode !== data.currencyCode;
                    return {
                        vendorInvoiceId: invoiceId,
                        // allocatedAmount always reduces the invoice's native-currency balance.
                        allocatedAmount: amount,
                        // The API requires the bank-currency amount explicitly for an FX allocation.
                        paymentCurrencyAmount: isCrossCurrency
                            ? Number(paymentCurrencyAllocations[invoiceId]) || 0
                            : amount,
                        discountAmount: Number(discountAllocations[invoiceId]) || 0,
                        withholdingTaxAmount: Number(withholdingAllocations[invoiceId]) || 0,
                    };
                });

            const blockedSelection = selectedInvoiceIds.find(invoiceId =>
                outstandingInvoices?.find((invoice) => invoice.invoiceId === invoiceId)
                    ?.paymentReadiness?.isPaymentReady !== true);
            if (blockedSelection) {
                toast({
                    title: 'Payment readiness blocked',
                    description: 'Remove blocked invoices before recording the payment. The server will revalidate all selected invoices.',
                    variant: 'destructive',
                });
                return;
            }

            if (isLinkedInvoicePayment && preselectedInvoiceId) {
                const linkedAllocation = paymentAllocations.find(
                    (allocation) => allocation.vendorInvoiceId === preselectedInvoiceId,
                );
                if (!linkedAllocation) {
                    toast({
                        title: 'Invoice allocation required',
                        description: 'Use Schedule Payment only after the linked invoice is visible and payment-ready.',
                        variant: 'destructive',
                    });
                    return;
                }
            }

            const incompleteCrossCurrencyAllocation = paymentAllocations.find(allocation => {
                const invoice = outstandingInvoices?.find(item => item.invoiceId === allocation.vendorInvoiceId);
                if (!invoice || invoice.currencyCode === data.currencyCode) return false;
                const hasEitherCashAmount = allocation.allocatedAmount > 0 ||
                    (allocation.paymentCurrencyAmount ?? 0) > 0;
                return hasEitherCashAmount && (allocation.allocatedAmount <= 0 ||
                    (allocation.paymentCurrencyAmount ?? 0) <= 0);
            });
            if (incompleteCrossCurrencyAllocation) {
                toast({
                    title: 'Payment-currency amount required',
                    description: 'Enter both the invoice amount settled and the amount consumed from the selected bank currency.',
                    variant: 'destructive',
                });
                return;
            }

            const submissionAllocationState = getApPaymentAllocationState(
                data.totalAmount,
                paymentAllocations.map(allocation => ({
                    invoiceCash: allocation.allocatedAmount,
                    paymentCurrencyCash: allocation.paymentCurrencyAmount ?? allocation.allocatedAmount,
                    discount: allocation.discountAmount,
                    withholdingTax: allocation.withholdingTaxAmount,
                })),
            );
            const totalWithholdingTax = paymentAllocations.reduce((sum, allocation) => sum + (allocation.withholdingTaxAmount || 0), 0);
            if (!existingAdvancePaymentId && submissionAllocationState.disposition === 'over-allocated') {
                toast({
                    title: 'Allocation exceeds payment',
                    description: `Reduce allocations by ${formatCurrency(Math.abs(submissionAllocationState.remainingPaymentCurrencyCash), data.currencyCode)} before recording this payment.`,
                    variant: 'destructive',
                });
                return;
            }

            if (!existingAdvancePaymentId && submissionAllocationState.disposition === 'partially-allocated') {
                toast({
                    title: 'Payment cash is only partially allocated',
                    description: `Allocate the remaining ${formatCurrency(submissionAllocationState.remainingPaymentCurrencyCash, data.currencyCode)} before recording this payment, or clear all allocations to record the full amount as a supplier advance.`,
                    variant: 'destructive',
                });
                return;
            }

            if (existingAdvancePaymentId && paymentAllocations.some(allocation =>
                (allocation.discountAmount || 0) > 0 || (allocation.withholdingTaxAmount || 0) > 0)) {
                toast({
                    title: 'Advance application supports cash only',
                    description: 'Apply the advance amount here, then use the dedicated adjustment workflow for discounts or withholding.',
                    variant: 'destructive',
                });
                return;
            }

            const invoiceWht = paymentWithholdingChoice((outstandingInvoices ?? []).filter(invoice =>
                paymentAllocations.some(allocation => allocation.vendorInvoiceId === invoice.invoiceId)));
            if (invoiceWht) {
                data.withholdingTaxId = invoiceWht.taxId;
                data.withholdingTaxRate = invoiceWht.rate;
                data.withholdingTaxAccountId = invoiceWht.accountId;
            }
            if (totalWithholdingTax > 0 && !data.withholdingTaxId) {
                toast({
                    title: 'WHT tax required',
                    description: 'Select the configured withholding tax before recording WHT on a payment.',
                    variant: 'destructive',
                });
                return;
            }

            if (totalWithholdingTax > 0 && !data.withholdingTaxAccountId) {
                toast({
                    title: 'WHT account missing',
                    description: 'The selected withholding tax needs a payable account before this payment can be posted.',
                    variant: 'destructive',
                });
                return;
            }

            let verifiedWithholding: WhtCalculationResult | null = null;
            const whtScopeKeys = Array.from(new Set(paymentAllocations.map(allocation => {
                const invoice = outstandingInvoices?.find(item => item.invoiceId === allocation.vendorInvoiceId);
                return `${invoice?.withholdingContractReference || ''}|${invoice?.withholdingSupplyCategory ?? ''}`;
            })));
            const requiresServerFunctionalWithholding = whtScopeKeys.length !== 1 || paymentAllocations.some(allocation => {
                const invoice = outstandingInvoices?.find(item => item.invoiceId === allocation.vendorInvoiceId);
                return invoice?.currencyCode !== functionalCurrencyCode;
            });
            const withholdingTaxableBase = paymentAllocations.reduce((sum, allocation) =>
                sum + allocation.allocatedAmount + (allocation.discountAmount || 0) + (allocation.withholdingTaxAmount || 0), 0);
            if (data.withholdingTaxId && !requiresServerFunctionalWithholding) {
                const scopedInvoice = outstandingInvoices?.find(item =>
                    paymentAllocations.some(allocation => allocation.vendorInvoiceId === item.invoiceId));
                if (!scopedInvoice?.withholdingContractReference || scopedInvoice.withholdingSupplyCategory == null) {
                    toast({
                        title: 'Invoice WHT scope missing',
                        description: 'Every WHT invoice needs a contract/reference and supply category before payment.',
                        variant: 'destructive',
                    });
                    return;
                }
                verifiedWithholding = await taxDataService.calculateApWithholding({
                    taxId: data.withholdingTaxId,
                    businessPartnerId: data.supplierId,
                    paymentDate: data.paymentDate.toISOString(),
                    taxableBase: withholdingTaxableBase,
                    vendorInvoiceIds: paymentAllocations.map(allocation => allocation.vendorInvoiceId),
                    contractReference: scopedInvoice.withholdingContractReference,
                    supplyCategory: scopedInvoice.withholdingSupplyCategory,
                });
                if (Math.abs(roundMoney(verifiedWithholding.withholdingAmount - totalWithholdingTax)) > 0.01) {
                    setWithholdingCalculation(verifiedWithholding);
                    toast({
                        title: 'WHT changed',
                        description: `Configured threshold/rate requires ${formatCurrency(verifiedWithholding.withholdingAmount, currentCurrencyCode)}. Use Auto Allocate to refresh the invoice WHT split.`,
                        variant: 'destructive',
                    });
                    return;
                }
            }

            const overSettledInvoice = outstandingInvoices?.find((invoice) => {
                const invoiceSettlement =
                    (Number(allocations[invoice.invoiceId]) || 0) +
                    (Number(discountAllocations[invoice.invoiceId]) || 0) +
                    (Number(withholdingAllocations[invoice.invoiceId]) || 0);

                return invoiceSettlement - invoice.balanceAmount > 0.01;
            });
            if (overSettledInvoice) {
                toast({
                    title: 'Bill over-settled',
                    description: `${overSettledInvoice.invoiceNumber} exceeds its outstanding balance after cash, discount, and WHT.`,
                    variant: 'destructive',
                });
                return;
            }


            if (existingAdvancePaymentId) {
                if (paymentAllocations.length === 0) {
                    toast({ title: 'Allocation required', description: 'Select at least one payment-ready supplier invoice.', variant: 'destructive' });
                    return;
                }
                await accountsPayableService.allocatePayment(existingAdvancePaymentId, paymentAllocations);
                toast({ title: 'Advance applied', description: 'The supplier advance and any realized FX were posted successfully.' });
                router.push(`/finance/ap/payments/${existingAdvancePaymentId}`);
                return;
            }

            if (data.currencyCode.toUpperCase() !== functionalCurrencyCode && !data.exchangeRateId) {
                toast({
                    title: 'Approved exchange rate required',
                    description: exchangeRateSource,
                    variant: 'destructive',
                });
                return;
            }

            const { supplierId, ...paymentData } = data;
            const payment = await accountsPayableService.createPayment({
                ...paymentData,
                businessPartnerId: supplierId,
                businessPartnerRoleId: selectedSupplierOption?.businessPartnerRoleId,
                paymentDate: data.paymentDate.toISOString(),
                // Cross/foreign invoice WHT is converted and validated per allocation by the
                // API. A native header sum would mix currencies and must never become statutory
                // evidence; zero asks Finance to derive its functional roll-up.
                withholdingTaxAmount: requiresServerFunctionalWithholding ? 0 : totalWithholdingTax,
                withholdingTaxBaseAmount: requiresServerFunctionalWithholding ? 0 : verifiedWithholding?.taxableBase ?? 0,
                allocations: paymentAllocations.length > 0 ? paymentAllocations : undefined,
                financeDimensions: {
                    defaultDimensions: toFinancePostingDimensionValues(defaultDimensionValues),
                    lines: [],
                    applyDefaultToEligibleLines: applyDefaultToAll,
                },
            });

            toast({ title: 'Success', description: 'Vendor payment recorded successfully' });
            router.push(`/finance/ap/payments/${payment.id}`);
        } catch (error: any) {
            toast({
                title: 'Error',
                description: error.message || 'Failed to process vendor payment',
                variant: 'destructive',
            });
        } finally {
            setIsSubmitting(false);
        }
    };

    const onInvalidSubmit = (errors: any) => {
        const description =
            errors.bankAccountId?.message ||
            errors.supplierId?.message ||
            errors.totalAmount?.message ||
            errors.paymentDate?.message ||
            'Complete the required payment details before recording the payment.';

        toast({
            title: 'Payment details incomplete',
            description,
            variant: 'destructive',
        });
    };

    const currentAmount = form.watch('totalAmount');
    const selectedAllocationInvoiceIds = Array.from(new Set([
        ...Object.keys(allocations),
        ...Object.keys(paymentCurrencyAllocations),
        ...Object.keys(discountAllocations),
        ...Object.keys(withholdingAllocations),
    ])).filter(invoiceId => hasApPaymentSettlementComponents([{
        invoiceCash: Number(allocations[invoiceId]) || 0,
        paymentCurrencyCash: Number(paymentCurrencyAllocations[invoiceId]) || 0,
        discount: Number(discountAllocations[invoiceId]) || 0,
        withholdingTax: Number(withholdingAllocations[invoiceId]) || 0,
    }]));
    const allocationRows: ApPaymentAllocationRow[] = selectedAllocationInvoiceIds.map(invoiceId => {
        const invoice = displayedOutstandingInvoices?.find(item => item.invoiceId === invoiceId);
        const invoiceCash = Number(allocations[invoiceId]) || 0;
        return {
            invoiceCash,
            paymentCurrencyCash: invoice?.currencyCode === currentCurrencyCode
                ? invoiceCash
                : Number(paymentCurrencyAllocations[invoiceId]) || 0,
            discount: Number(discountAllocations[invoiceId]) || 0,
            withholdingTax: Number(withholdingAllocations[invoiceId]) || 0,
        };
    });
    // The backend rounds the aggregate payment-currency cash and payment total to two decimals,
    // then requires exact equality. Invoice-native cash, discounts and WHT are not bank cash.
    const allocationState = getApPaymentAllocationState(currentAmount, allocationRows);
    const totalAllocated = allocationState.allocatedPaymentCurrencyCash;
    const totalDiscounts = Object.values(discountAllocations).reduce((acc, curr) => acc + curr, 0);
    const totalWithholdingTax = Object.values(withholdingAllocations).reduce((acc, curr) => acc + curr, 0);
    const remainingAmount = allocationState.remainingPaymentCurrencyCash;
    const hasPaymentReadinessFailure = selectedAllocationInvoiceIds.some(invoiceId =>
        displayedOutstandingInvoices?.find(invoice => invoice.invoiceId === invoiceId)
            ?.paymentReadiness?.isPaymentReady !== true);
    const hasIncompleteCurrencyPair = selectedAllocationInvoiceIds.some(invoiceId => {
        const invoice = displayedOutstandingInvoices?.find(item => item.invoiceId === invoiceId);
        if (!invoice || invoice.currencyCode === currentCurrencyCode) return false;
        const invoiceCash = Number(allocations[invoiceId]) || 0;
        const paymentCash = Number(paymentCurrencyAllocations[invoiceId]) || 0;
        return (invoiceCash > 0 || paymentCash > 0) && (invoiceCash <= 0 || paymentCash <= 0);
    });
    const currentExchangeRate = Number(form.watch('exchangeRate')) || 0;
    const currentExchangeRateId = form.watch('exchangeRateId');
    const hasFxEvidenceFailure = !existingAdvancePaymentId &&
        currentCurrencyCode.toUpperCase() !== functionalCurrencyCode &&
        (currentExchangeRate <= 0 || !currentExchangeRateId);
    const canRecordPayment = canSubmitApPayment({
        allocationState,
        isAdvanceApplication: !!existingAdvancePaymentId,
        hasPaymentReadinessFailure,
        hasFxEvidenceFailure,
        hasIncompleteCurrencyPair,
        isBusy: isSubmitting ||
            (!!existingAdvancePaymentId && !existingAdvancePayment) ||
            (isLinkedInvoicePayment && isLoadingLinkedInvoice),
    });
    const allocationStatusMessage = existingAdvancePaymentId
        ? allocationState.hasSettlementComponents
            ? `${formatCurrency(totalAllocated, currentCurrencyCode)} is selected for application from this supplier advance.`
            : 'Select at least one payment-ready invoice to apply this supplier advance.'
        : allocationState.disposition === 'supplier-advance'
            ? `No invoice allocations are entered. The full ${formatCurrency(currentAmount, currentCurrencyCode)} will be recorded as a supplier advance.`
            : allocationState.disposition === 'fully-allocated'
                ? 'Payment cash is fully allocated.'
                : allocationState.disposition === 'partially-allocated'
                    ? `Allocate the remaining ${formatCurrency(remainingAmount, currentCurrencyCode)} before recording this payment, or clear all allocations to record the full amount as a supplier advance.`
                    : `Allocations exceed this payment by ${formatCurrency(Math.abs(remainingAmount), currentCurrencyCode)}. Reduce the allocated payment cash before recording.`;

    const handleAutoAllocate = async () => {
        if (!displayedOutstandingInvoices) return;
        const eligibleInvoices = displayedOutstandingInvoices
            .filter(invoice => invoice.paymentReadiness?.isPaymentReady === true && invoice.currencyCode === currentCurrencyCode)
            .sort((a, b) => new Date(a.dueDate || a.invoiceDate).getTime() - new Date(b.dueDate || b.invoiceDate).getTime());
        let invoicesToAllocate = eligibleInvoices;
        const scopeKey = (invoice: OutstandingVendorInvoice) =>
            `${invoice.withholdingContractReference || ''}|${invoice.withholdingSupplyCategory ?? ''}`;
        const buildAllocation = (rateForInvoice: (invoice: OutstandingVendorInvoice) => number) => {
            let remaining = currentAmount;
            const cash: Record<string, number> = {};
            const discounts: Record<string, number> = {};
            const withholding: Record<string, number> = {};
            // Never auto-allocate cash to a Procurement-blocked invoice. The user can see the
            // readiness reason in the table, and the API independently enforces the same rule.
            for (const inv of invoicesToAllocate) {
                if (remaining <= 0) break;
                const discountAmount = Number(inv.discountAmount) || 0;
                const rate = Math.max(0, Math.min(rateForInvoice(inv), 99.9999)) / 100;
                const fullWithholding = roundMoney(inv.balanceAmount * rate);
                const fullCashRequired = Math.max(inv.balanceAmount - discountAmount - fullWithholding, 0);
                const settlesInFull = remaining + 0.01 >= fullCashRequired;
                const settlementBase = settlesInFull
                    ? inv.balanceAmount
                    : Math.min(inv.balanceAmount, remaining / Math.max(1 - rate, 0.000001));
                const withholdingAmount = roundMoney(settlementBase * rate);
                const appliedDiscount = settlesInFull ? discountAmount : 0;
                const allocateAmount = Math.min(remaining, Math.max(settlementBase - appliedDiscount - withholdingAmount, 0));
                cash[inv.invoiceId] = allocateAmount;
                if (appliedDiscount > 0) discounts[inv.invoiceId] = appliedDiscount;
                if (withholdingAmount > 0) withholding[inv.invoiceId] = withholdingAmount;
                remaining = roundMoney(remaining - allocateAmount);
            }
            return { cash, discounts, withholding };
        };

        setIsCalculatingWithholding(true);
        try {
            const selectedInvoices = eligibleInvoices.filter(invoice => Number(allocations[invoice.invoiceId]) > 0 ||
                Number(discountAllocations[invoice.invoiceId]) > 0 || Number(withholdingAllocations[invoice.invoiceId]) > 0);
            if (selectedInvoices.length) {
                paymentWithholdingChoice(selectedInvoices); // Do not silently change an explicit mixed selection.
                invoicesToAllocate = selectedInvoices;
            } else if (eligibleInvoices.length) {
                const first = invoiceWithholdingChoice(eligibleInvoices[0]);
                invoicesToAllocate = eligibleInvoices.filter(invoice => {
                    const choice = invoiceWithholdingChoice(invoice);
                    return choice?.taxId === first?.taxId && (choice?.rate ?? 0) === (first?.rate ?? 0);
                });
            }
            const invoiceChoice = paymentWithholdingChoice(invoicesToAllocate);
            const taxId = invoiceChoice ? invoiceChoice.taxId : selectedWithholdingTax?.id;
            const configuredRate = invoiceChoice ? invoiceChoice.rate : Number(selectedWithholdingTax?.rate || 0);
            if (invoiceChoice) {
                form.setValue('withholdingTaxId', invoiceChoice.taxId);
                form.setValue('withholdingTaxRate', invoiceChoice.rate);
                form.setValue('withholdingTaxAccountId', invoiceChoice.accountId);
            }
            let next = buildAllocation(() => configuredRate);
            if (taxId && selectedSupplierId) {
                let finalCalculations: WhtCalculationResult[] = [];
                let ratesByScope = new Map<string, number>();
                // Rebuild twice because removing provisional WHT increases the partial-payment
                // base and may itself cross a statutory threshold.
                for (let pass = 0; pass < 2; pass += 1) {
                    const grouped = new Map<string, { invoice: OutstandingVendorInvoice; taxableBase: number; invoiceIds: string[] }>();
                    for (const invoiceId of Object.keys(next.cash)) {
                        const invoice = displayedOutstandingInvoices.find(item => item.invoiceId === invoiceId);
                        if (!invoice) continue;
                        if (!invoice.withholdingContractReference || invoice.withholdingSupplyCategory == null) {
                            throw new Error(`${invoice.invoiceNumber} is missing its WHT contract/category scope.`);
                        }
                        const key = scopeKey(invoice);
                        const taxableBase = (next.cash[invoiceId] || 0) +
                            (next.discounts[invoiceId] || 0) + (next.withholding[invoiceId] || 0);
                        const existing = grouped.get(key);
                        grouped.set(key, {
                            invoice,
                            taxableBase: (existing?.taxableBase || 0) + taxableBase,
                            invoiceIds: [...(existing?.invoiceIds || []), invoiceId],
                        });
                    }
                    finalCalculations = await Promise.all(Array.from(grouped.values()).map(group => {
                        const contractReference = group.invoice.withholdingContractReference;
                        const supplyCategory = group.invoice.withholdingSupplyCategory;
                        if (!contractReference || supplyCategory == null) {
                            throw new Error(`${group.invoice.invoiceNumber} is missing its WHT contract/category scope.`);
                        }
                        return taxDataService.calculateApWithholding({
                            taxId,
                            businessPartnerId: selectedSupplierId,
                            paymentDate: form.getValues('paymentDate').toISOString(),
                            taxableBase: group.taxableBase,
                            vendorInvoiceIds: group.invoiceIds,
                            contractReference,
                            supplyCategory,
                        });
                    }));
                    ratesByScope = new Map(finalCalculations.map(calculation => [
                        `${calculation.contractReference}|${calculation.supplyCategory}`,
                        calculation.thresholdApplied ? calculation.taxRate : 0,
                    ]));
                    next = buildAllocation(invoice => ratesByScope.get(scopeKey(invoice)) || 0);
                }
                setWithholdingCalculation(finalCalculations[0] || null);
            } else {
                setWithholdingCalculation(null);
            }
            setAllocations(next.cash);
            setPaymentCurrencyAllocations({});
            setDiscountAllocations(next.discounts);
            setWithholdingAllocations(next.withholding);
            if (!selectedInvoices.length && invoicesToAllocate.length < eligibleInvoices.length) {
                toast({ title: 'Matching invoices allocated', description: 'Invoices with a different WHT choice or rate need a separate payment.' });
            }
        } catch (error: any) {
            toast({
                title: 'WHT calculation failed',
                description: error?.message || 'Unable to apply the configured WHT threshold.',
                variant: 'destructive',
            });
        } finally {
            setIsCalculatingWithholding(false);
        }
    };

    return (
        <div className="space-y-8 p-8 max-w-[1200px] mx-auto">
            <div className="flex items-center space-x-4">
                <Button variant="ghost" size="icon" onClick={() => router.back()}>
                    <ArrowLeft className="h-4 w-4" />
                </Button>
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">
                        {existingAdvancePaymentId ? 'Apply Supplier Advance' : 'Record Vendor Payment'}
                    </h1>
                    <p className="text-muted-foreground">
                        {existingAdvancePaymentId
                            ? 'Consume the posted advance currency lot against payment-ready supplier invoices.'
                            : 'Post a payment made to a supplier and allocate it to outstanding bills.'}
                    </p>
                </div>
            </div>

            <div className="grid gap-8 md:grid-cols-3">
                {/* Payment Details Form */}
                <Card className="md:col-span-1 h-fit">
                    <CardHeader>
                        <CardTitle>{existingAdvancePaymentId ? 'Advance Lot' : 'Payment Details'}</CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                        <form id="payment-form" onSubmit={form.handleSubmit(onSubmit as any, onInvalidSubmit)} className="space-y-4">
                            <div className="space-y-2">
                                <Label htmlFor="supplier">Supplier</Label>
                                {isLinkedInvoicePayment ? (
                                    <Input value={lockedSupplierName} disabled />
                                ) : (
                                    <Select
                                        onValueChange={(val) => {
                                            const option = supplierOptions.find(item => item.businessPartnerRoleId === val);
                                            if (!option?.isTransactionReady) return;
                                            if (option.businessPartnerId !== selectedSupplierId) clearAllocationState();
                                            form.setValue('supplierId', option.businessPartnerId);
                                        }}
                                        value={selectedSupplierOption?.businessPartnerRoleId}
                                        disabled={isSubmitting || !!existingAdvancePaymentId}
                                    >
                                        <SelectTrigger>
                                            <SelectValue placeholder="Select supplier..." />
                                        </SelectTrigger>
                                        <SelectContent>
                                            {supplierOptions.map((supplier) => (
                                                <SelectItem key={supplier.businessPartnerRoleId} value={supplier.businessPartnerRoleId}
                                                    disabled={!supplier.isTransactionReady}>
                                                    {supplier.name} · {supplier.roleType}
                                                    {supplier.code ? ` (${supplier.code})` : ''}
                                                    {!supplier.isTransactionReady ? ` — ${supplier.readinessMessage}` : ''}
                                                </SelectItem>
                                            ))}
                                        </SelectContent>
                                    </Select>
                                )}
                                {form.formState.errors.supplierId && (
                                    <p className="text-sm text-red-500">{form.formState.errors.supplierId.message}</p>
                                )}
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="bankAccount">Pay From Bank Account</Label>
                                <Select
                                    onValueChange={(val) => {
                                        if (val !== selectedBankAccountId) clearAllocationState();
                                        form.setValue('bankAccountId', val);
                                    }}
                                    value={form.watch('bankAccountId') || undefined}
                                    disabled={isSubmitting || !!existingAdvancePaymentId}
                                >
                                    <SelectTrigger>
                                        <SelectValue placeholder="Select bank account..." />
                                    </SelectTrigger>
                                    <SelectContent>
                                        {bankAccounts?.map((account) => (
                                            <SelectItem key={account.id} value={account.id}>
                                                {account.accountName} ({account.currency}) - {account.bankName}
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                                {form.formState.errors.bankAccountId && (
                                    <p className="text-sm text-red-500">{form.formState.errors.bankAccountId.message}</p>
                                )}
                            </div>

                            <div className="space-y-2">
                                <Label>Payment Date</Label>
                                <Controller
                                    control={form.control}
                                    name="paymentDate"
                                    render={({ field }) => (
                                        <Popover>
                                            <PopoverTrigger asChild>
                                                <Button
                                                    variant="outline"
                                                    className={cn(
                                                        "w-full justify-start text-left font-normal",
                                                        !field.value && "text-muted-foreground"
                                                    )}
                                                    disabled={isSubmitting || !!existingAdvancePaymentId}
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
                                <Label htmlFor="amount">Amount Paid</Label>
                                <div className="relative">
                                    <span className="absolute left-3 top-2.5 text-gray-500 text-sm font-medium">
                                        {currentCurrencyCode}
                                    </span>
                                    <Input
                                        id="amount"
                                        type="number"
                                        className="pl-14"
                                        step="0.01"
                                        {...form.register('totalAmount')}
                                        disabled={isSubmitting || !!existingAdvancePaymentId || isLinkedInvoicePayment}
                                    />
                                </div>
                                {form.formState.errors.totalAmount && (
                                    <p className="text-sm text-red-500">{form.formState.errors.totalAmount.message}</p>
                                )}
                            </div>

                            <div className="space-y-2 rounded-md border p-3">
                                <Label htmlFor="withholdingTax">Withholding Tax</Label>
                                <Select
                                    onValueChange={(val) => {
                                        if (val !== (selectedWithholdingTaxId || 'none')) {
                                            setWithholdingAllocations({});
                                            setWithholdingCalculation(null);
                                        }
                                        if (val === 'none') {
                                            form.setValue('withholdingTaxId', undefined);
                                            form.setValue('withholdingTaxAccountId', undefined);
                                            form.setValue('withholdingTaxRate', 0);
                                            setWithholdingAllocations({});
                                            return;
                                        }

                                        form.setValue('withholdingTaxId', val);
                                    }}
                                    value={form.watch('withholdingTaxId') || 'none'}
                                    disabled={isSubmitting || !!existingAdvancePaymentId || !!selectedInvoiceWht.choice}
                                >
                                    <SelectTrigger>
                                        <SelectValue placeholder="No WHT" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="none">No WHT</SelectItem>
                                        {withholdingTaxOptions.map((tax) => (
                                            <SelectItem key={tax.id} value={tax.id}>
                                                {tax.code} - {tax.name} ({Number(tax.rate || 0)}%)
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                                {selectedInvoiceWht.error && <p className="text-xs text-destructive">{selectedInvoiceWht.error}</p>}
                                {selectedInvoiceWht.choice && <p className="text-xs text-muted-foreground">
                                    {selectedInvoiceWht.choice.taxId ? `Invoice WHT: ${selectedInvoiceWht.choice.rate}%.` : 'WHT is disabled on the invoice.'}
                                </p>}
                                {selectedWithholdingTax && (
                                    <div className="space-y-1 text-xs text-muted-foreground">
                                        <div>
                                            {selectedWithholdingTax.taxPayableAccountId
                                                ? `Posting to configured WHT payable account at ${selectedInvoiceWht.choice?.rate ?? Number(selectedWithholdingTax.rate || 0)}%.`
                                                : 'This tax has no payable account configured; posting will be blocked until it is set.'}
                                        </div>
                                        {withholdingCalculation && (
                                            <div className={withholdingCalculation.thresholdApplied ? 'text-emerald-700' : 'text-amber-700'}>
                                                {withholdingCalculation.calculationNote}
                                            </div>
                                        )}
                                    </div>
                                )}
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="exchangeRate">Exchange Rate</Label>
                                <Input
                                    id="exchangeRate"
                                    type="number"
                                    step="0.000001"
                                    {...form.register('exchangeRate')}
                                    readOnly
                                    aria-readonly="true"
                                    disabled={isSubmitting || !!existingAdvancePaymentId}
                                />
                                <p className="text-xs text-muted-foreground">
                                    1 {currentCurrencyCode} = {form.watch('exchangeRate') || 1} {functionalCurrencyCode}
                                </p>
                                <p className="text-xs text-muted-foreground">{exchangeRateSource}</p>
                                {form.formState.errors.exchangeRate && (
                                    <p className="text-sm text-red-500">{form.formState.errors.exchangeRate.message}</p>
                                )}
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="paymentMethod">Payment Method</Label>
                                <Select
                                    onValueChange={(val) => {
                                        const method = paymentMethods?.find((item) => item.id === val);
                                        form.setValue('paymentMethodId', val);
                                        form.setValue('paymentMethod', toVendorPaymentMethod(method?.type));
                                    }}
                                    value={form.watch('paymentMethodId') || undefined}
                                    disabled={isSubmitting || !!existingAdvancePaymentId}
                                >
                                    <SelectTrigger>
                                        <SelectValue placeholder="Select payment method..." />
                                    </SelectTrigger>
                                    <SelectContent>
                                        {paymentMethods?.map((method) => (
                                            <SelectItem key={method.id} value={method.id}>
                                                {method.name}
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                                {form.formState.errors.paymentMethodId && (
                                    <p className="text-sm text-red-500">{form.formState.errors.paymentMethodId.message}</p>
                                )}
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="reference">Reference #</Label>
                                <Input id="reference" placeholder="e.g. Cheque No. / Receipt" {...form.register('transactionReference')} disabled={isSubmitting || !!existingAdvancePaymentId} />
                                {form.formState.errors.transactionReference && (
                                    <p className="text-sm text-red-500">{form.formState.errors.transactionReference.message}</p>
                                )}
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="notes">Notes</Label>
                                <Textarea id="notes" {...form.register('notes')} disabled={isSubmitting} />
                            </div>
                        </form>
                    </CardContent>
                    <CardFooter>
                        <div className="w-full space-y-2">
                            <p className={cn(
                                'text-xs',
                                !existingAdvancePaymentId &&
                                    (allocationState.disposition === 'partially-allocated' ||
                                        allocationState.disposition === 'over-allocated')
                                    ? 'text-red-600'
                                    : 'text-muted-foreground',
                            )}>
                                {allocationStatusMessage}
                            </p>
                            <Button
                                type="submit"
                                form="payment-form"
                                className="w-full"
                                // Do not allow an application request until the immutable lot header
                                // has loaded; otherwise a fast click could submit default form values.
                                disabled={!canRecordPayment}
                            >
                                {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                                {isLoadingAdvancePayment
                                    ? 'Loading Advance...'
                                    : existingAdvancePaymentId
                                        ? 'Apply Advance'
                                        : allocationState.disposition === 'supplier-advance'
                                            ? 'Record Supplier Advance'
                                            : 'Record Payment'}
                            </Button>
                        </div>
                    </CardFooter>
                </Card>

                {/* Allocation Section */}
                <Card className="md:col-span-2">
                    <CardHeader className="flex flex-row items-center justify-between">
                        <CardTitle>Allocate to Bills</CardTitle>
                        <Button variant="outline" size="sm" onClick={handleAutoAllocate} disabled={isSubmitting || isLinkedInvoicePayment || isCalculatingWithholding || !displayedOutstandingInvoices?.some((invoice) => invoice.paymentReadiness?.isPaymentReady === true)}>
                            {isCalculatingWithholding && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Auto Allocate
                        </Button>
                    </CardHeader>
                    <CardContent>
                        {!selectedSupplierId ? (
                            <div className="text-center py-12 text-muted-foreground">
                                Select a supplier to view outstanding bills.
                            </div>
                        ) : isLoadingInvoices ? (
                            <div className="space-y-4">
                                <Skeleton className="h-12 w-full" />
                                <Skeleton className="h-12 w-full" />
                                <Skeleton className="h-12 w-full" />
                            </div>
                        ) : !displayedOutstandingInvoices || displayedOutstandingInvoices.length === 0 ? (
                            <div className="text-center py-12 text-muted-foreground">
                                No outstanding bills found for this supplier.
                            </div>
                        ) : (
                            <div className="space-y-4">
                                <VendorPaymentReadinessControl
                                    readiness={displayedOutstandingInvoices
                                        .map((invoice) => invoice.paymentReadiness)
                                        .filter((item): item is NonNullable<typeof item> => Boolean(item))}
                                />
                                <div className="flex justify-between items-center bg-muted/50 p-4 rounded-lg font-medium">
                                    <div>
                                        <div>Remaining Cash to Allocate:</div>
                                        {totalDiscounts > 0 && (
                                            <div className="text-xs text-muted-foreground">
                                                Discounts are recorded in each invoice currency below.
                                            </div>
                                        )}
                                        {totalWithholdingTax > 0 && (
                                            <div className="text-xs text-muted-foreground">
                                                WHT is recorded per invoice; Finance derives the functional statutory total.
                                            </div>
                                        )}
                                    </div>
                                    <span className={remainingAmount < 0 ? 'text-red-500' : 'text-green-600'}>
                                        {formatCurrency(remainingAmount, currentCurrencyCode)}
                                    </span>
                                </div>
                                <div
                                    className={cn(
                                        'rounded-md border px-3 py-2 text-sm',
                                        !existingAdvancePaymentId &&
                                            (allocationState.disposition === 'partially-allocated' ||
                                                allocationState.disposition === 'over-allocated')
                                            ? 'border-red-200 bg-red-50 text-red-700'
                                            : 'border-blue-200 bg-blue-50 text-blue-700',
                                    )}
                                    role="status"
                                >
                                    {allocationStatusMessage}
                                </div>

                                <div className="border rounded-md">
                                    <table className="w-full text-sm">
                                        <thead className="bg-muted text-muted-foreground">
                                            <tr>
                                                <th className="p-3 text-left">Bill #</th>
                                                <th className="p-3 text-left">Due Date</th>
                                                <th className="p-3 text-right">Balance Due</th>
                                                <th className="p-3 text-right w-[150px]">Invoice Cash</th>
                                                <th className="p-3 text-right w-[150px]">Payment Cash</th>
                                                <th className="p-3 text-right w-[150px]">Discount</th>
                                                <th className="p-3 text-right w-[150px]">WHT</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {displayedOutstandingInvoices.map((inv) => {
                                                const availableDiscount = Number(inv.discountAmount) || 0;
                                                const maxCashAllocation = Math.max(inv.balanceAmount - availableDiscount, 0);
                                                const currentCashAllocation = Number(allocations[inv.invoiceId]) || 0;
                                                const currentDiscountAllocation = Number(discountAllocations[inv.invoiceId]) || 0;
                                                const maxWithholdingAllocation = Math.max(inv.balanceAmount - currentCashAllocation - currentDiscountAllocation, 0);
                                                const isPaymentReady = inv.paymentReadiness?.isPaymentReady === true;
                                                const isCrossCurrency = inv.currencyCode !== currentCurrencyCode;
                                                const isLockedInvoiceRow = isLinkedInvoicePayment && inv.invoiceId === preselectedInvoiceId;

                                                return (
                                                    <tr key={inv.invoiceId} className="border-t">
                                                        <td className="p-3 font-medium flex flex-col items-start gap-1">
                                                            <span>{inv.invoiceNumber}</span>
                                                            {inv.supplierInvoiceNumber && (
                                                                <span className="text-xs text-muted-foreground">Ref: {inv.supplierInvoiceNumber}</span>
                                                            )}
                                                            <VendorPaymentReadinessBadge readiness={inv.paymentReadiness} />
                                                            {availableDiscount > 0 && (
                                                                <span className="text-xs text-emerald-700">
                                                                    Discount available: {formatCurrency(availableDiscount, inv.currencyCode)}
                                                                </span>
                                                            )}
                                                        </td>
                                                        <td className="p-3">
                                                            {inv.dueDate ? format(new Date(inv.dueDate), 'MMM dd, yyyy') : '-'}
                                                            {inv.dueDate && new Date(inv.dueDate) < new Date() && (
                                                                <span className="ml-2 text-xs text-red-500 font-bold">Overdue</span>
                                                            )}
                                                        </td>
                                                        <td className="p-3 text-right">{formatCurrency(inv.balanceAmount, inv.currencyCode)}</td>
                                                        <td className="p-3">
                                                            <Input
                                                                type="number"
                                                                className="text-right h-8"
                                                                min={0}
                                                                step="0.01"
                                                                max={maxCashAllocation}
                                                                value={allocations[inv.invoiceId] || ''}
                                                                onChange={(e) => {
                                                                    const val = Number(e.target.value);
                                                                    setAllocations(prev => ({
                                                                        ...prev,
                                                                        [inv.invoiceId]: val
                                                                    }));
                                                                }}
                                                                disabled={isSubmitting || isLockedInvoiceRow || !isPaymentReady}
                                                            />
                                                        </td>
                                                        <td className="p-3">
                                                            {isCrossCurrency ? (
                                                                <Input
                                                                    type="number"
                                                                    className="text-right h-8"
                                                                    min={0}
                                                                    step="0.01"
                                                                    value={paymentCurrencyAllocations[inv.invoiceId] || ''}
                                                                    onChange={(e) => {
                                                                        const val = Number(e.target.value);
                                                                        setPaymentCurrencyAllocations(prev => ({
                                                                            ...prev,
                                                                            [inv.invoiceId]: val,
                                                                        }));
                                                                    }}
                                                                    disabled={isSubmitting || !isPaymentReady}
                                                                    aria-label={`Payment amount in ${currentCurrencyCode}`}
                                                                />
                                                            ) : (
                                                                <div className="text-right text-muted-foreground">
                                                                    {formatCurrency(currentCashAllocation, currentCurrencyCode)}
                                                                </div>
                                                            )}
                                                            {isCrossCurrency && (
                                                                <div className="mt-1 text-right text-xs text-muted-foreground">
                                                                    {currentCurrencyCode} paid for {inv.currencyCode} balance
                                                                </div>
                                                            )}
                                                        </td>
                                                        <td className="p-3">
                                                            <Input
                                                                type="number"
                                                                className="text-right h-8"
                                                                min={0}
                                                                step="0.01"
                                                                max={availableDiscount}
                                                                value={discountAllocations[inv.invoiceId] || ''}
                                                                onChange={(e) => {
                                                                    const val = Number(e.target.value);
                                                                    setDiscountAllocations(prev => ({
                                                                        ...prev,
                                                                        [inv.invoiceId]: val
                                                                    }));
                                                                }}
                                                                disabled={isSubmitting || !isPaymentReady || availableDiscount <= 0}
                                                            />
                                                        </td>
                                                        <td className="p-3">
                                                            <Input
                                                                type="number"
                                                                className="text-right h-8"
                                                                min={0}
                                                                step="0.01"
                                                                max={maxWithholdingAllocation}
                                                                value={withholdingAllocations[inv.invoiceId] || ''}
                                                                onChange={(e) => {
                                                                    const val = Number(e.target.value);
                                                                    setWithholdingAllocations(prev => ({
                                                                        ...prev,
                                                                        [inv.invoiceId]: val
                                                                    }));
                                                                }}
                                                                disabled={isSubmitting || !isPaymentReady || !selectedWithholdingTax || inv.applySupplierWithholdingDefaults === false}
                                                            />
                                                        </td>
                                                    </tr>
                                                );
                                            })}
                                        </tbody>
                                    </table>
                                </div>
                            </div>
                        )}
                    </CardContent>
                </Card>
                {!existingAdvancePaymentId && (
                    <Card className="md:col-span-3">
                        <CardHeader><CardTitle>Finance coding dimensions</CardTitle></CardHeader>
                        <CardContent>
                            <SourceDocumentDimensionPanel
                                context={{
                                    sourceModule: 'AP',
                                    sourceDocumentType: 'VendorPayment',
                                    postingAction: 'Post',
                                    sourceRoute: 'finance.ap.vendor-payments.manual',
                                    contractVersion: '1.0',
                                }}
                                effectiveDate={format(form.watch('paymentDate') || new Date(), 'yyyy-MM-dd')}
                                lines={Object.values(allocations).some(amount => Number(amount) > 0)
                                    || Object.values(discountAllocations).some(amount => Number(amount) > 0)
                                    || Object.values(withholdingAllocations).some(amount => Number(amount) > 0)
                                    ? []
                                    : [{ id: 'supplier-advance', accountLabel: 'Supplier advance (server-resolved account)' }]}
                                defaultValues={defaultDimensionValues}
                                lineValues={{}}
                                onDefaultValuesChange={(values) => {
                                    setDefaultDimensionValues(values);
                                    setApplyDefaultToAll(false);
                                }}
                                onLineValuesChange={() => undefined}
                                onApplyDefaultToAll={() => setApplyDefaultToAll(true)}
                                disabled={isSubmitting}
                            />
                        </CardContent>
                    </Card>
                )}
            </div>
        </div>
    );
}
