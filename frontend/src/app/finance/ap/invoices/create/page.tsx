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
    CardHeader,
    CardTitle,
    CardFooter,
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
import { accountsPayableService } from '@/services/accountsPayableService';
import { purchasingService } from '@/services/purchasingService';
import { financeDataService } from '@/services/finance/finance-data.service';
import { businessPartnerService } from '@/services/businessPartnerService';
import { inventoryManagementService } from '@/services/inventoryManagementService';
import { taxDataService } from '@/services/finance/tax-data.service';
import { financeService, resolvePostingExchangeRate } from '@/services/finance.service';
import { paymentTermService, type PaymentTermListDto } from '@/services/financeCommonService';
import { TaxApplicability, TaxCategory, type Tax } from '@/types/tax';
import { useToast } from '@/components/ui/use-toast';
import { formatCurrency, cn } from '@/lib/utils';
import { format, addDays } from 'date-fns';
import { useQuery } from '@tanstack/react-query';
import { loadApprovedInvoiceRate } from '@/lib/finance/invoice-exchange-rate';
import { useTenant } from '@/contexts/TenantContext';

const lineItemSchema = z.object({
    lineItemType: z.enum(['Expense', 'Product', 'Inventory']).default('Expense'),
    glAccountId: z.string().optional(),
    inventoryItemId: z.string().optional(),
    warehouseId: z.string().optional(),
    purchaseOrderItemId: z.string().optional(),
    description: z.string().min(1, 'Description is required'),
    quantity: z.coerce.number().min(0.01, 'Quantity must be positive'),
    unitPrice: z.coerce.number().min(0, 'Unit price must be positive'),
    taxGroupId: z.string().optional(),
    discountPercentage: z.coerce.number().min(0).max(100).optional().default(0),
    unit: z.string().optional(),
});

const invoiceSchema = z.object({
    supplierId: z.string().min(1, 'Supplier is required'),
    supplierInvoiceNumber: z.string().optional(),
    purchaseOrderId: z.string().optional(),
    acceptedSupplyKind: z.enum(['GoodsReceiptInspection', 'ServiceCompletion', 'WorksPaymentCertificate']).optional(),
    acceptedSupplySourceId: z.string().optional(),
    invoiceDate: z.date(),
    dueDate: z.date(),
    paymentTermId: z.string().optional(),
    currencyCode: z.string().default('GHS'),
    exchangeRate: z.coerce.number().min(0.0001).optional().default(1.0),
    exchangeRateId: z.string().optional(),
    exchangeRateDate: z.date().optional(),
    exchangeRateSource: z.string().optional().default('Daily'),
    notes: z.string().optional(),
    reference: z.string().optional(),
    taxGroupId: z.string().optional(),
    withholdingTaxId: z.string().optional().default('none'),
    withholdingTaxRate: z.coerce.number().min(0).max(100).optional().default(0),
    isOpeningBalance: z.boolean().default(false),
    lineItems: z.array(lineItemSchema).min(1, 'At least one line item is required'),
});

type InvoiceFormValues = z.infer<typeof invoiceSchema>;

export default function CreateVendorInvoicePage() {
    const router = useRouter();
    const searchParams = useSearchParams();
    const preselectedSupplierId = searchParams.get('supplierId');
    const defaultOpeningBalance = searchParams.get('openingBalance') === 'true';
    const preselectedPurchaseOrderId = searchParams.get('purchaseOrderId');
    const { toast } = useToast();
    const { currentTenantCode } = useTenant();
    const exchangeRateRequestId = useRef(0);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [paymentTerms, setPaymentTerms] = useState<PaymentTermListDto[]>([]);

    // Supplier combobox state
    const [selectedSupplier, setSelectedSupplier] = useState<any>(null);
    const [supplierComboOpen, setSupplierComboOpen] = useState(false);
    const [supplierSearch, setSupplierSearch] = useState('');
    const [selectedPurchaseOrderId, setSelectedPurchaseOrderId] = useState(
        preselectedPurchaseOrderId || ''
    );

    // GL Account combobox state
    const [glAccountOpenIndex, setGlAccountOpenIndex] = useState<number | null>(null);
    const [glAccountSearch, setGlAccountSearch] = useState('');

    // Inventory Item combobox state
    const [inventoryItemOpenIndex, setInventoryItemOpenIndex] = useState<number | null>(null);
    const [inventoryItemSearch, setInventoryItemSearch] = useState('');

    // Queries
    const { data: suppliersData, isLoading: suppliersLoading } = useQuery({
        queryKey: ['active-ap-supplier-business-partners'],
        queryFn: async () => {
            const partners = await businessPartnerService.getActivePartners();
            const payablePartners = (partners || [])
                .filter((partner: any) => String(partner.partnerType || '').toLowerCase() !== 'customer')
                .map((partner: any) => ({
                    ...partner,
                    name: partner.name || partner.partnerName || partner.companyName || 'Unknown supplier',
                    code: partner.code || partner.partnerCode || '',
                }))
                .sort((a: any, b: any) => a.name.localeCompare(b.name));

            return {
                items: Array.from(
                    new Map(payablePartners.map((partner: any) => [partner.id, partner])).values()
                )
            };
        },
    });

    const { data: glAccountsData, isLoading: glAccountsLoading } = useQuery({
        queryKey: ['gl-accounts-active'],
        queryFn: async () => {
            const accounts = await financeDataService.getAccounts({ status: 'Active' });
            return { items: accounts.filter((account: any) => account.isActive !== false) };
        },
    });

    const { data: inventoryItemsData, isLoading: inventoryItemsLoading } = useQuery({
        queryKey: ['inventory-items-active'],
        queryFn: () => inventoryManagementService.getInventoryItems({ pageSize: 100, isActive: true } as any),
    });

    const { data: taxGroupsData } = useQuery({
        queryKey: ['tax-groups-active'],
        queryFn: () => taxDataService.getTaxGroups({ isActive: true, applicability: 'Purchases' }),
    });

    const { data: withholdingTaxes = [] } = useQuery({
        queryKey: ['taxes', 'ap-invoice-withholding'],
        queryFn: () => taxDataService.getTaxes({ isActive: true, category: TaxCategory.Withholding }),
    });

    const { data: warehousesData } = useQuery({
        queryKey: ['warehouses'],
        queryFn: () => inventoryManagementService.getWarehouses(),
    });

    const { data: financeSettings } = useQuery({
        queryKey: ['finance-settings', currentTenantCode, 'ap-invoice-rate-policy'],
        queryFn: () => financeService.getSettings(),
        enabled: Boolean(currentTenantCode),
    });

    const { data: purchaseOrdersData, isLoading: purchaseOrdersLoading } = useQuery({
        queryKey: ['ap-purchase-orders', selectedSupplier?.id],
        queryFn: () => purchasingService.getPurchaseOrders({
            pageSize: 100,
            supplierId: selectedSupplier.id,
        }),
        enabled: Boolean(selectedSupplier?.id),
    });

    const { data: selectedPurchaseOrder } = useQuery({
        queryKey: ['ap-purchase-order', selectedPurchaseOrderId],
        queryFn: () => purchasingService.getPurchaseOrderById(selectedPurchaseOrderId),
        enabled: Boolean(selectedPurchaseOrderId),
    });

    const serviceCategory = selectedPurchaseOrder?.procurementCategory &&
        selectedPurchaseOrder.procurementCategory !== 'Goods' &&
        selectedPurchaseOrder.procurementCategory !== 'Works';
    const { data: acceptedSupplyOptions, isLoading: acceptedSupplyLoading } = useQuery({
        queryKey: ['ap-accepted-supply-options', selectedPurchaseOrderId],
        queryFn: () => accountsPayableService.getAcceptedSupplyOptions(selectedPurchaseOrderId),
        enabled: Boolean(selectedPurchaseOrderId && serviceCategory),
    });

    useEffect(() => {
        paymentTermService.getByApplicableTo('Supplier')
            .then((terms) => setPaymentTerms(terms || []))
            .catch(() => setPaymentTerms([]));
    }, []);

    // Filtering logic
    const filteredSuppliers = suppliersData?.items?.filter((supplier: any) => {
        if (!supplierSearch) return true;
        const search = supplierSearch.toLowerCase();
        return (
            supplier.name?.toLowerCase().includes(search) ||
            supplier.code?.toLowerCase().includes(search)
        );
    }) || [];

    const filteredGlAccounts = glAccountsData?.items?.filter((account: any) => {
        if (!glAccountSearch) return true;
        const search = glAccountSearch.toLowerCase();
        return (
            account.accountCode?.toLowerCase().includes(search) ||
            account.accountName?.toLowerCase().includes(search)
        );
    }) || [];

    const filteredInventoryItems = inventoryItemsData?.filter((item: any) => {
        if (!inventoryItemSearch) return true;
        const search = inventoryItemSearch.toLowerCase();
        return (
            item.itemCode?.toLowerCase().includes(search) ||
            item.name?.toLowerCase().includes(search)
        );
    }) || [];

    // Form setup
    const form = useForm<InvoiceFormValues>({
        // @ts-expect-error TODO: fix type
        resolver: zodResolver(invoiceSchema),
        defaultValues: {
            supplierId: preselectedSupplierId || '',
            supplierInvoiceNumber: '',
            purchaseOrderId: preselectedPurchaseOrderId || undefined,
            invoiceDate: new Date(),
            dueDate: addDays(new Date(), 30),
            paymentTermId: '',
            currencyCode: 'GHS',
            exchangeRate: 1.0,
            exchangeRateId: undefined,
            exchangeRateDate: new Date(),
            exchangeRateSource: 'Daily',
            isOpeningBalance: defaultOpeningBalance,
            withholdingTaxId: 'none',
            withholdingTaxRate: 0,
            notes: '',
            lineItems: [
                { lineItemType: 'Expense', description: '', quantity: 1, unitPrice: 0, discountPercentage: 0, taxGroupId: 'none' }
            ],
        },
    });

    const watchInvoiceDate = form.watch('invoiceDate');
    useEffect(() => {
        if (watchInvoiceDate) {
            form.setValue('exchangeRateDate', watchInvoiceDate);
        }
    }, [watchInvoiceDate]);

    const { fields, append, remove } = useFieldArray({
        control: form.control,
        name: 'lineItems',
    });

    // Totals and dynamic tax calculation previews
    const watchTaxGroupId = form.watch('taxGroupId');
    const watchIsOpeningBalance = form.watch('isOpeningBalance');
    const watchCurrencyCode = form.watch('currencyCode') || 'GHS';
    const watchWithholdingTaxId = form.watch('withholdingTaxId');
    const withholdingTaxOptions = (withholdingTaxes as Tax[]).filter(tax =>
        tax.isActive && (
            tax.applicability === TaxApplicability.Purchases ||
            tax.applicability === TaxApplicability.Both
        )
    );
    const selectedWithholdingTax = withholdingTaxOptions.find(tax => tax.id === watchWithholdingTaxId);
    const watchWithholdingTaxRate = watchIsOpeningBalance ? 0 : Number(selectedWithholdingTax?.rate || 0);
    const watchLineItems = form.watch('lineItems') || [];

    const applyInvoiceExchangeRate = async (currencyCode: string) => {
        const requestId = ++exchangeRateRequestId.current;
        const functionalCurrency = financeSettings?.baseCurrency || 'GHS';
        const isOpeningBalance = form.getValues('isOpeningBalance');

        if (isOpeningBalance) {
            // Clear the prior date/currency evidence before the async lookup so Save cannot race
            // with a stale approved-rate identity while the new historical rate is loading.
            form.setValue('exchangeRateId', undefined);
            if (!financeSettings) {
                throw new Error('Finance settings are still loading. Try again before saving this opening invoice.');
            }
            let snapshot;
            try {
                snapshot = await loadApprovedInvoiceRate(
                    {
                        module: 'AP',
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
            form.setValue('exchangeRateSource', snapshot.source);
            return;
        }

        form.setValue('exchangeRateId', undefined);
        if (currencyCode === functionalCurrency) {
            form.setValue('exchangeRate', 1);
            form.setValue('exchangeRateSource', 'Daily');
            return;
        }

        let rateObj;
        try {
            rateObj = await financeService.getCurrentExchangeRate(currencyCode);
        } catch (error) {
            if (requestId !== exchangeRateRequestId.current) return;
            throw error;
        }
        if (requestId !== exchangeRateRequestId.current) return;
        form.setValue('exchangeRate', resolvePostingExchangeRate(rateObj));
        form.setValue('exchangeRateSource', 'Daily');
    };

    useEffect(() => {
        if (!watchIsOpeningBalance || !financeSettings || !watchInvoiceDate) return;
        void applyInvoiceExchangeRate(watchCurrencyCode).catch((error) => {
            console.error('Failed to resolve governed AP opening-invoice rate', error);
            form.setValue('exchangeRateId', undefined);
            form.setValue('exchangeRateSource', 'Unavailable');
        });
    }, [financeSettings, watchCurrencyCode, watchInvoiceDate, watchIsOpeningBalance]);

    useEffect(() => {
        if (!watchIsOpeningBalance) return;

        // Opening bills bring forward gross AP balances only; tax and WHT history is not
        // reposted through the migration clearing entry created by the posting service.
        form.setValue('taxGroupId', 'none');
        form.setValue('withholdingTaxId', 'none');
        form.setValue('withholdingTaxRate', 0);
        form.getValues('lineItems').forEach((_, index) => {
            form.setValue(`lineItems.${index}.taxGroupId`, 'none');
        });
    }, [form, watchIsOpeningBalance]);

    const subtotal = watchLineItems.reduce((acc, item) => {
        const qty = Number(item.quantity) || 0;
        const price = Number(item.unitPrice) || 0;
        const discount = Number(item.discountPercentage) || 0;
        return acc + (qty * price * (1 - discount / 100));
    }, 0);

    const resolveLineTaxGroupId = (
        item: any,
        isOpeningBalance = watchIsOpeningBalance,
        headerTaxGroupId = watchTaxGroupId
    ) => {
        if (isOpeningBalance) return null;
        const activeGroupId = item.taxGroupId || headerTaxGroupId;
        return activeGroupId && activeGroupId !== 'none' ? activeGroupId : null;
    };

    const calculateLineTax = (
        item: any,
        isOpeningBalance = watchIsOpeningBalance,
        headerTaxGroupId = watchTaxGroupId
    ) => {
        if (isOpeningBalance) {
            return { taxAmount: 0, taxRate: 0 };
        }

        const qty = Number(item.quantity) || 0;
        const price = Number(item.unitPrice) || 0;
        const discount = Number(item.discountPercentage) || 0;
        const lineSubtotal = qty * price * (1 - discount / 100);
        const activeGroupId = resolveLineTaxGroupId(item, isOpeningBalance, headerTaxGroupId);
        const activeGroup = taxGroupsData?.find(tg => tg.id === activeGroupId);

        if (!activeGroup || !activeGroup.components || lineSubtotal <= 0) {
            return { taxAmount: 0, taxRate: 0 };
        }

        let lineTaxAmount = 0;
        let cumulativeBase = lineSubtotal;
        const sortedComponents = [...activeGroup.components].sort((a, b) => a.calculationOrder - b.calculationOrder);

        sortedComponents.forEach(comp => {
            if (comp.taxCategory === 'Withholding') return;

            const taxableBasis = comp.compoundBasis === 'Cumulative'
                ? cumulativeBase
                : lineSubtotal;
            const taxAmt = taxableBasis * (Number(comp.taxRate) / 100);
            lineTaxAmount += taxAmt;

            if (comp.compoundBasis === 'Cumulative' || comp.compoundBasis === 'BaseOnly') {
                cumulativeBase += taxAmt;
            }
        });

        return {
            taxAmount: lineTaxAmount,
            taxRate: (lineTaxAmount / lineSubtotal) * 100
        };
    };

    const getTaxBreakdown = () => {
        if (watchIsOpeningBalance) {
            return {
                totalTaxAmount: 0,
                withholdingTaxAmount: 0,
                grandTotal: subtotal,
                netPayable: subtotal,
                taxList: []
            };
        }

        let totalTaxAmount = 0;
        const breakdowns: { [taxCode: string]: { name: string; rate: number; amount: number } } = {};

        watchLineItems.forEach((item) => {
            const qty = Number(item.quantity) || 0;
            const price = Number(item.unitPrice) || 0;
            const discount = Number(item.discountPercentage) || 0;
            const lineSubtotal = qty * price * (1 - discount / 100);

            // Resolve line tax group or fallback to header
            const activeGroupId = item.taxGroupId || watchTaxGroupId;
            const activeGroup = taxGroupsData?.find(tg => tg.id === activeGroupId);

            if (activeGroup && activeGroup.components) {
                let cumulativeBase = lineSubtotal;
                const sortedComponents = [...activeGroup.components].sort((a, b) => a.calculationOrder - b.calculationOrder);

                sortedComponents.forEach(comp => {
                    if (comp.taxCategory === 'Withholding') return; // standard levies/vat only

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

        // Compute separate withholding tax deduction based on withholdingTaxRate
        const withholdingTaxAmount = subtotal * (watchWithholdingTaxRate / 100);
        const grandTotal = subtotal + totalTaxAmount; // subtotal + standard taxes
        const netPayable = grandTotal - withholdingTaxAmount; // WHT is a deduction

        return {
            totalTaxAmount,
            withholdingTaxAmount,
            grandTotal,
            netPayable,
            taxList: Object.entries(breakdowns).map(([code, data]) => ({ code, ...data }))
        };
    };

    const taxEstimate = getTaxBreakdown();
    const totalTax = taxEstimate.totalTaxAmount;
    const totalAmount = taxEstimate.grandTotal;

    const formatAmountWithCurrency = (amount: number) => {
        return formatCurrency(amount, watchCurrencyCode);
    };

    const onSupplierChange = async (supplierId: string, preservePurchaseOrderId?: string) => {
        form.setValue('supplierId', supplierId);
        form.setValue('purchaseOrderId', preservePurchaseOrderId || undefined);
        form.setValue('acceptedSupplyKind', undefined);
        form.setValue('acceptedSupplySourceId', undefined);
        setSelectedPurchaseOrderId(preservePurchaseOrderId || '');
        if (!suppliersData?.items) return;

        const supplier = suppliersData.items.find(s => s.id === supplierId);
        if (supplier) {
            setSelectedSupplier(supplier);
            const selectedTerm = paymentTerms.find(term => term.id === supplier.paymentTermId)
                || paymentTerms.find(term => term.isDefault);
            if (selectedTerm) {
                form.setValue('paymentTermId', selectedTerm.id);
                form.setValue('dueDate', addDays(form.getValues('invoiceDate'), selectedTerm.dueDays));
            }
            if (supplier.currency) {
                form.setValue('currencyCode', supplier.currency);
                try {
                    await applyInvoiceExchangeRate(supplier.currency);
                } catch (err) {
                    console.error("Failed to fetch exchange rate for supplier currency", err);
                    form.setValue('exchangeRateId', undefined);
                    form.setValue('exchangeRate', 1.0);
                    form.setValue('exchangeRateSource', 'Unavailable');
                }
            } else {
                form.setValue('currencyCode', 'GHS');
                await applyInvoiceExchangeRate('GHS');
            }
        }
    };

    const onPurchaseOrderChange = async (purchaseOrderId: string) => {
        const value = purchaseOrderId === 'none' ? '' : purchaseOrderId;
        setSelectedPurchaseOrderId(value);
        form.setValue('purchaseOrderId', value || undefined);
        form.setValue('acceptedSupplyKind', undefined);
        form.setValue('acceptedSupplySourceId', undefined);
    };

    useEffect(() => {
        if (!selectedPurchaseOrder) return;
        if (selectedPurchaseOrder.procurementCategory === 'Works') {
            toast({
                title: 'Use the QS certificate handoff',
                description: 'Works invoices are created from an approved QS payment certificate.',
                variant: 'destructive',
            });
            onPurchaseOrderChange('none');
            return;
        }
        form.setValue('currencyCode', selectedPurchaseOrder.currency || 'GHS');
        form.setValue('exchangeRate', 1);
        form.setValue('lineItems', selectedPurchaseOrder.items.map(item => ({
            lineItemType: selectedPurchaseOrder.procurementCategory === 'Goods' ? 'Inventory' : 'Expense',
            inventoryItemId: item.inventoryItemId || undefined,
            warehouseId: item.warehouseId || undefined,
            purchaseOrderItemId: item.id,
            description: item.itemDescription || item.itemName || item.itemCode,
            quantity: item.remainingQuantity > 0 ? item.remainingQuantity : item.orderedQuantity,
            unitPrice: item.unitPrice,
            discountPercentage: 0,
            taxGroupId: 'none',
            unit: item.unitOfMeasure,
        })));
        if (selectedPurchaseOrder.procurementCategory === 'Goods')
            form.setValue('acceptedSupplyKind', undefined);
        else
            form.setValue('acceptedSupplyKind', 'ServiceCompletion');
    }, [form, selectedPurchaseOrder, toast]);

    useEffect(() => {
        if (preselectedSupplierId && suppliersData?.items) {
            onSupplierChange(preselectedSupplierId, preselectedPurchaseOrderId || undefined);
        }
    }, [preselectedSupplierId, preselectedPurchaseOrderId, suppliersData, paymentTerms]);

    const getAccountDisplay = (accountId: string | undefined) => {
        if (!accountId) return null;
        const account = glAccountsData?.items?.find((a: any) => a.id === accountId);
        return account ? `${account.accountCode} - ${account.accountName}` : null;
    };

    const getInventoryItemDisplay = (itemId: string | undefined) => {
        if (!itemId) return null;
        const item = inventoryItemsData?.find((i: any) => i.id === itemId);
        return item ? `${item.itemCode} - ${item.name}` : null;
    };

    const onSubmit = async (data: InvoiceFormValues) => {
        setIsSubmitting(true);
        try {
            const isOpeningBalance = data.isOpeningBalance;
            if (serviceCategory && !data.acceptedSupplySourceId) {
                toast({
                    title: 'Service completion required',
                    description: 'Select an approved service completion before recording this invoice.',
                    variant: 'destructive',
                });
                return;
            }
            if (!isOpeningBalance && selectedWithholdingTax && !selectedWithholdingTax.taxPayableAccountId) {
                toast({
                    title: 'WHT account missing',
                    description: `${selectedWithholdingTax.code} needs a payable account in Tax Configuration before it can be used.`,
                    variant: 'destructive',
                });
                return;
            }
            const functionalCurrency = financeSettings?.baseCurrency || 'GHS';
            if (isOpeningBalance && data.currencyCode !== functionalCurrency && !data.exchangeRateId) {
                toast({
                    title: 'Approved exchange rate required',
                    description: 'Select a currency and invoice date with an active approved Daily rate before creating this opening invoice.',
                    variant: 'destructive',
                });
                return;
            }
            await accountsPayableService.createInvoice({
                ...data,
                invoiceDate: data.invoiceDate.toISOString(),
                dueDate: data.dueDate.toISOString(),
                taxGroupId: isOpeningBalance || data.taxGroupId === 'none' ? null : (data.taxGroupId || null),
                exchangeRate: Number(data.exchangeRate) || 1.0,
                exchangeRateId: isOpeningBalance ? data.exchangeRateId : undefined,
                // The backend resolves rate/account again from this tax id. Sending the displayed
                // values keeps the compatibility DTO descriptive but grants them no authority.
                withholdingTaxId: isOpeningBalance || data.withholdingTaxId === 'none' ? null : data.withholdingTaxId,
                withholdingTaxRate: isOpeningBalance ? 0 : watchWithholdingTaxRate,
                withholdingTaxAccountId: isOpeningBalance ? null : selectedWithholdingTax?.taxPayableAccountId || null,
                isOpeningBalance,
                acceptedSupplyKind: serviceCategory ? 'ServiceCompletion' : undefined,
                acceptedSupplySourceId: serviceCategory ? data.acceptedSupplySourceId : undefined,
                lineItems: data.lineItems.map(item => {
                    const lineTax = calculateLineTax(item, isOpeningBalance, data.taxGroupId);
                    return {
                        lineItemType: item.lineItemType,
                        glAccountId: item.glAccountId || null,
                        purchaseOrderItemId: item.purchaseOrderItemId || null,
                        description: item.description,
                        quantity: Number(item.quantity),
                        unitPrice: Number(item.unitPrice),
                        discountPercentage: Number(item.discountPercentage),
                        taxRate: lineTax.taxRate,
                        taxGroupId: resolveLineTaxGroupId(item, isOpeningBalance, data.taxGroupId),
                        unit: item.unit || null,
                        inventoryItemId: item.inventoryItemId || null,
                        warehouseId: item.warehouseId || null,
                    } as any;
                })
            });

            toast({
                title: 'Success',
                description: 'Vendor invoice created successfully',
            });

            router.push('/finance/ap/invoices');
        } catch (error: any) {
            toast({
                title: 'Error',
                description: error.message || 'Failed to create vendor invoice',
                variant: 'destructive',
            });
        } finally {
            setIsSubmitting(false);
        }
    };

    return (
        <div className="space-y-8 p-8 max-w-[1400px] mx-auto">
            <div className="flex items-center space-x-4">
                <Button variant="ghost" size="icon" onClick={() => router.back()}>
                    <ArrowLeft className="h-4 w-4" />
                </Button>
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Record Vendor Invoice</h1>
                    <p className="text-muted-foreground">
                        Enter a bill received from a supplier.
                    </p>
                </div>
            </div>

            <form onSubmit={form.handleSubmit(onSubmit as any)} className="space-y-8">
                <Card>
                    <CardHeader>
                        <CardTitle>Invoice Details</CardTitle>
                    </CardHeader>
                    <CardContent className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
                        <div className="space-y-2">
                            <Label htmlFor="supplier">Supplier</Label>
                            <Popover open={supplierComboOpen} onOpenChange={setSupplierComboOpen}>
                                <PopoverTrigger asChild>
                                    <Button
                                        variant="outline"
                                        role="combobox"
                                        aria-expanded={supplierComboOpen}
                                        className="w-full justify-between"
                                    >
                                        {selectedSupplier
                                            ? `${selectedSupplier.name} (${selectedSupplier.code})`
                                            : suppliersLoading
                                                ? "Loading suppliers..."
                                                : "Select a supplier..."}
                                        <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                                    </Button>
                                </PopoverTrigger>
                                <PopoverContent className="w-[400px] p-0" align="start">
                                    <Command shouldFilter={false}>
                                        <CommandInput
                                            placeholder="Search supplier..."
                                            value={supplierSearch}
                                            onValueChange={setSupplierSearch}
                                        />
                                        <CommandList>
                                            <CommandEmpty>
                                                {suppliersLoading ? "Loading..." : "No supplier found."}
                                            </CommandEmpty>
                                            <CommandGroup>
                                                {filteredSuppliers.map((supplier: any) => (
                                                    <div
                                                        key={supplier.id}
                                                        onClick={() => {
                                                            onSupplierChange(supplier.id);
                                                            setSupplierComboOpen(false);
                                                            setSupplierSearch('');
                                                        }}
                                                        className="relative flex cursor-pointer select-none items-center rounded-sm px-2 py-1.5 text-sm outline-none hover:bg-accent hover:text-accent-foreground"
                                                    >
                                                        <Check className={cn("mr-2 h-4 w-4", selectedSupplier?.id === supplier.id ? "opacity-100" : "opacity-0")} />
                                                        <div className="flex flex-col">
                                                            <span className="font-medium">{supplier.name}</span>
                                                            <span className="text-xs text-muted-foreground">{supplier.code}</span>
                                                        </div>
                                                    </div>
                                                ))}
                                            </CommandGroup>
                                        </CommandList>
                                    </Command>
                                </PopoverContent>
                            </Popover>
                            {form.formState.errors.supplierId && (
                                <p className="text-sm text-red-500">{form.formState.errors.supplierId.message}</p>
                            )}
                        </div>

                        {!watchIsOpeningBalance && selectedSupplier && (
                            <div className="space-y-2">
                                <Label>Purchase order</Label>
                                <Select
                                    value={selectedPurchaseOrderId || 'none'}
                                    onValueChange={onPurchaseOrderChange}
                                >
                                    <SelectTrigger>
                                        <SelectValue placeholder={purchaseOrdersLoading ? 'Loading...' : 'Select a purchase order'} />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="none">No purchase order</SelectItem>
                                        {(purchaseOrdersData?.items || [])
                                            .filter(order => order.procurementCategory !== 'Works' &&
                                                !['Cancelled', 'Rejected'].includes(order.status))
                                            .map(order => (
                                                <SelectItem key={order.id} value={order.id}>
                                                    {order.orderNumber} · {order.procurementCategory || 'Category missing'}
                                                </SelectItem>
                                            ))}
                                    </SelectContent>
                                </Select>
                            </div>
                        )}

                        {serviceCategory && (
                            <div className="space-y-2">
                                <Label>Approved service completion</Label>
                                <Controller
                                    control={form.control}
                                    name="acceptedSupplySourceId"
                                    render={({ field }) => (
                                        <Select value={field.value || ''} onValueChange={field.onChange}>
                                            <SelectTrigger>
                                                <SelectValue placeholder={acceptedSupplyLoading ? 'Loading...' : 'Select approved completion'} />
                                            </SelectTrigger>
                                            <SelectContent>
                                                {(acceptedSupplyOptions?.options || []).map(option => (
                                                    <SelectItem key={option.sourceId} value={option.sourceId}>
                                                        {option.label}
                                                    </SelectItem>
                                                ))}
                                            </SelectContent>
                                        </Select>
                                    )}
                                />
                                {!acceptedSupplyLoading && acceptedSupplyOptions && !acceptedSupplyOptions.ready && (
                                    <p className="text-sm text-destructive">
                                        {acceptedSupplyOptions.blockedReasons.join(' ')}
                                    </p>
                                )}
                            </div>
                        )}

                        <div className="space-y-2">
                            <Label>Supplier Invoice #</Label>
                            <Input {...form.register('supplierInvoiceNumber')} placeholder="INV-2026-001" />
                        </div>

                        <div className="space-y-2">
                            <Label>Invoice Date</Label>
                            <Controller
                                control={form.control}
                                name="invoiceDate"
                                render={({ field }) => (
                                    <Popover>
                                        <PopoverTrigger asChild>
                                            <Button variant="outline" className={cn("w-full justify-start text-left font-normal", !field.value && "text-muted-foreground")}>
                                                <CalendarIcon className="mr-2 h-4 w-4" />
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
                            <Label>Due Date</Label>
                            <Controller
                                control={form.control}
                                name="dueDate"
                                render={({ field }) => (
                                    <Popover>
                                        <PopoverTrigger asChild>
                                            <Button variant="outline" className={cn("w-full justify-start text-left font-normal", !field.value && "text-muted-foreground")}>
                                                <CalendarIcon className="mr-2 h-4 w-4" />
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
                                                form.setValue('exchangeRate', 1.0);
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
                                    readOnly={watchIsOpeningBalance}
                                    aria-readonly={watchIsOpeningBalance}
                                    {...form.register('exchangeRate', {
                                        onChange: () => {
                                            if (!watchIsOpeningBalance) form.setValue('exchangeRateSource', 'Custom');
                                        }
                                    })} 
                                />
                                <span className="text-[11px] text-muted-foreground block mt-1">
                                    1 {watchCurrencyCode} = {form.watch('exchangeRate')} {financeSettings?.baseCurrency || 'GHS'}
                                    {watchIsOpeningBalance ? ' · approved rate locked to this opening invoice' : ''}
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
                            <Label>Payment Terms</Label>
                            <Controller
                                control={form.control}
                                name="paymentTermId"
                                render={({ field }) => (
                                    <Select
                                        value={field.value || ''}
                                        onValueChange={(value) => {
                                            field.onChange(value);
                                            const term = paymentTerms.find(candidate => candidate.id === value);
                                            if (term) form.setValue('dueDate', addDays(form.getValues('invoiceDate'), term.dueDays));
                                        }}
                                    >
                                        <SelectTrigger><SelectValue placeholder="Select payment terms" /></SelectTrigger>
                                        <SelectContent>
                                            {paymentTerms.map(term => (
                                                <SelectItem key={term.id} value={term.id}>{term.code} - {term.name}</SelectItem>
                                            ))}
                                        </SelectContent>
                                    </Select>
                                )}
                            />
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
                                    ? 'Disabled for opening balances; opening bills carry no tax reposting.'
                                    : 'Optional. Pre-populates new lines; can be overridden on each line.'}
                            </span>
                        </div>

                        {watchCurrencyCode !== (financeSettings?.baseCurrency || 'GHS') && (
                            <div className="border p-4 rounded-lg bg-muted/20 md:col-span-2 lg:col-span-3 space-y-4">
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
                                                        <Button variant="outline" disabled={watchIsOpeningBalance} className={cn("w-full justify-start text-left font-normal text-xs", !field.value && "text-muted-foreground")}>
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
                                                watchIsOpeningBalance ? (
                                                    <Input value={field.value || 'Approved Daily rate'} readOnly aria-readonly="true" className="h-10 text-xs" />
                                                ) : (
                                                    <Select value={field.value || 'Daily'} onValueChange={field.onChange}>
                                                        <SelectTrigger className="h-10 text-xs">
                                                            <SelectValue placeholder="Select FX Source" />
                                                        </SelectTrigger>
                                                        <SelectContent>
                                                            <SelectItem value="Daily">Daily</SelectItem>
                                                            <SelectItem value="Spot">Spot</SelectItem>
                                                            <SelectItem value="Official">Official</SelectItem>
                                                            <SelectItem value="Market">Market</SelectItem>
                                                            <SelectItem value="Custom">Custom</SelectItem>
                                                        </SelectContent>
                                                    </Select>
                                                )
                                            )}
                                        />
                                    </div>
                                </div>
                            </div>
                        )}

                        <div className="space-y-2">
                            <Label>Expected Withholding Tax (WHT)</Label>
                            <Controller
                                control={form.control}
                                name="withholdingTaxId"
                                render={({ field }) => (
                                    <Select value={watchIsOpeningBalance ? 'none' : String(field.value || 'none')} onValueChange={field.onChange} disabled={watchIsOpeningBalance}>
                                        <SelectTrigger className={cn(watchIsOpeningBalance && 'bg-muted text-muted-foreground')}>
                                            <SelectValue placeholder="No WHT" />
                                        </SelectTrigger>
                                        <SelectContent>
                                            <SelectItem value="none">No WHT</SelectItem>
                                            {withholdingTaxOptions.map(tax => (
                                                <SelectItem key={tax.id} value={tax.id}>
                                                    {tax.code} - {tax.name} ({Number(tax.rate || 0)}%)
                                                </SelectItem>
                                            ))}
                                        </SelectContent>
                                    </Select>
                                )}
                            />
                            <p className="text-xs text-muted-foreground">
                                Invoice WHT is an estimate from tenant setup. The annual supplier threshold and final deduction are recalculated at payment.
                            </p>
                            {selectedWithholdingTax && !selectedWithholdingTax.taxPayableAccountId && (
                                <p className="text-xs text-destructive">This WHT configuration needs a payable account before the invoice can be saved.</p>
                            )}
                        </div>

                        <div className="space-y-2 lg:col-span-3">
                            <Label htmlFor="notes">Notes/Memo</Label>
                            <Textarea id="notes" placeholder="Reference number, payment instructions, etc." {...form.register('notes')} />
                        </div>
                    </CardContent>
                </Card>

                {/* Line Items Card */}
                <Card>
                    <CardHeader className="flex flex-row items-center justify-between">
                        <CardTitle>Line Items</CardTitle>
                        <Button type="button" variant="outline" size="sm" onClick={() => append({ lineItemType: 'Expense' as const, description: '', quantity: 1, unitPrice: 0, discountPercentage: 0, taxGroupId: 'none' })}>
                            <Plus className="mr-2 h-4 w-4" /> Add Item
                        </Button>
                    </CardHeader>
                    <CardContent>
                        <div className="space-y-4">
                            {fields.map((field, index) => {
                                const lineItemType = form.watch(`lineItems.${index}.lineItemType`);
                                return (
                                    <div key={field.id} className="grid grid-cols-12 gap-4 items-end border-b pb-4">
                                        <div className="col-span-2 space-y-2">
                                            <Label className={index !== 0 ? 'sr-only' : ''}>Type</Label>
                                            <Controller
                                                control={form.control}
                                                name={`lineItems.${index}.lineItemType`}
                                                render={({ field }) => (
                                                    <Select value={field.value} onValueChange={field.onChange}>
                                                        <SelectTrigger><SelectValue placeholder="Type" /></SelectTrigger>
                                                        <SelectContent>
                                                            <SelectItem value="Expense">GL Account / Expense</SelectItem>
                                                            <SelectItem value="Inventory">Inventory Item</SelectItem>
                                                            <SelectItem value="Product">Product / Other</SelectItem>
                                                        </SelectContent>
                                                    </Select>
                                                )}
                                            />
                                        </div>

                                        {lineItemType === 'Expense' ? (
                                            <>
                                                <div className="col-span-2 space-y-2">
                                                    <Label className={index !== 0 ? 'sr-only' : ''}>GL Account</Label>
                                                    <Controller
                                                        control={form.control}
                                                        name={`lineItems.${index}.glAccountId`}
                                                        render={({ field }) => (
                                                            <Popover
                                                                open={glAccountOpenIndex === index}
                                                                onOpenChange={(open) => { setGlAccountOpenIndex(open ? index : null); if (!open) setGlAccountSearch(''); }}
                                                            >
                                                                <PopoverTrigger asChild>
                                                                    <Button variant="outline" role="combobox" className="w-full justify-between text-left font-medium line-clamp-1 h-10 px-3">
                                                                        <span className="truncate text-sm">
                                                                            {getAccountDisplay(field.value) || (glAccountsLoading ? "Loading..." : "Select account...")}
                                                                        </span>
                                                                        <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                                                                    </Button>
                                                                </PopoverTrigger>
                                                                <PopoverContent className="w-[350px] p-0" align="start">
                                                                    <Command shouldFilter={false}>
                                                                        <CommandInput placeholder="Search code or name..." value={glAccountSearch} onValueChange={setGlAccountSearch} />
                                                                        <CommandList>
                                                                            <CommandEmpty>No account found.</CommandEmpty>
                                                                            <CommandGroup>
                                                                                {filteredGlAccounts.map((account: any) => (
                                                                                    <div
                                                                                        key={account.id}
                                                                                        onClick={() => {
                                                                                            field.onChange(account.id);
                                                                                            if (!form.getValues(`lineItems.${index}.description`)) {
                                                                                                form.setValue(`lineItems.${index}.description`, account.accountName);
                                                                                            }
                                                                                            setGlAccountOpenIndex(null);
                                                                                            setGlAccountSearch('');
                                                                                        }}
                                                                                        className="relative flex cursor-pointer select-none items-center rounded-sm px-2 py-1.5 text-sm outline-none hover:bg-accent hover:text-accent-foreground"
                                                                                    >
                                                                                        <div className="flex flex-col">
                                                                                            <span className="font-bold text-sm">{account.accountCode}</span>
                                                                                            <span className="text-xs text-muted-foreground">{account.accountName}</span>
                                                                                        </div>
                                                                                    </div>
                                                                                ))}
                                                                            </CommandGroup>
                                                                        </CommandList>
                                                                    </Command>
                                                                </PopoverContent>
                                                            </Popover>
                                                        )}
                                                    />
                                                </div>
                                                <div className="col-span-2 space-y-2">
                                                    <Label className={index !== 0 ? 'sr-only' : ''}>Description</Label>
                                                    <Input {...form.register(`lineItems.${index}.description` as const)} placeholder="Notes" />
                                                </div>
                                            </>
                                        ) : lineItemType === 'Inventory' ? (
                                            <>
                                                <div className="col-span-2 space-y-2">
                                                    <Label className={index !== 0 ? 'sr-only' : ''}>Inventory Item</Label>
                                                    <Controller
                                                        control={form.control}
                                                        name={`lineItems.${index}.inventoryItemId`}
                                                        render={({ field }) => (
                                                            <Popover
                                                                open={inventoryItemOpenIndex === index}
                                                                onOpenChange={(open) => { setInventoryItemOpenIndex(open ? index : null); if (!open) setInventoryItemSearch(''); }}
                                                            >
                                                                <PopoverTrigger asChild>
                                                                    <Button variant="outline" role="combobox" className="w-full justify-between text-left font-medium line-clamp-1 h-10 px-3">
                                                                        <span className="truncate text-sm">
                                                                            {getInventoryItemDisplay(field.value) || (inventoryItemsLoading ? "Loading..." : "Select item...")}
                                                                        </span>
                                                                        <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                                                                    </Button>
                                                                </PopoverTrigger>
                                                                <PopoverContent className="w-[350px] p-0" align="start">
                                                                    <Command shouldFilter={false}>
                                                                        <CommandInput placeholder="Search item code or name..." value={inventoryItemSearch} onValueChange={setInventoryItemSearch} />
                                                                        <CommandList>
                                                                            <CommandEmpty>No inventory items found.</CommandEmpty>
                                                                            <CommandGroup>
                                                                                {filteredInventoryItems.map((item: any) => (
                                                                                    <div
                                                                                        key={item.id}
                                                                                        onClick={() => {
                                                                                            field.onChange(item.id);
                                                                                            form.setValue(`lineItems.${index}.description`, item.name);
                                                                                            if (item.averageCost > 0) form.setValue(`lineItems.${index}.unitPrice`, item.averageCost);
                                                                                            setInventoryItemOpenIndex(null);
                                                                                            setInventoryItemSearch('');
                                                                                        }}
                                                                                        className="relative flex cursor-pointer select-none items-center rounded-sm px-2 py-1.5 text-sm outline-none hover:bg-accent hover:text-accent-foreground"
                                                                                    >
                                                                                        <div className="flex flex-col w-full">
                                                                                            <span className="font-bold text-sm">{item.itemCode}</span>
                                                                                            <div className="flex justify-between items-center w-full">
                                                                                                <span className="text-xs text-muted-foreground mr-2">{item.name}</span>
                                                                                                <span className="text-xs badge bg-muted px-1 rounded">{formatCurrency(item.averageCost || 0, watchCurrencyCode)}</span>
                                                                                            </div>
                                                                                        </div>
                                                                                    </div>
                                                                                ))}
                                                                            </CommandGroup>
                                                                        </CommandList>
                                                                    </Command>
                                                                </PopoverContent>
                                                            </Popover>
                                                        )}
                                                    />
                                                </div>
                                                <div className="col-span-2 space-y-2">
                                                    <Label className={index !== 0 ? 'sr-only' : ''}>Dest. Whse</Label>
                                                    <Controller
                                                        control={form.control}
                                                        name={`lineItems.${index}.warehouseId`}
                                                        render={({ field }) => (
                                                            <Select value={field.value} onValueChange={field.onChange}>
                                                                <SelectTrigger><SelectValue placeholder="Warehouse..." /></SelectTrigger>
                                                                <SelectContent>
                                                                    {warehousesData?.map((wh: any) => (
                                                                        <SelectItem key={wh.id} value={wh.id}>{wh.name}</SelectItem>
                                                                    ))}
                                                                </SelectContent>
                                                            </Select>
                                                        )}
                                                    />
                                                </div>
                                            </>
                                        ) : (
                                            <div className="col-span-4 space-y-2">
                                                <Label className={index !== 0 ? 'sr-only' : ''}>Description</Label>
                                                <Input {...form.register(`lineItems.${index}.description` as const)} placeholder="Item description" />
                                            </div>
                                        )}


                                        <div className="col-span-1 space-y-2">
                                            <Label className={index !== 0 ? 'sr-only' : ''}>Qty</Label>
                                            <Input type="number" step="1" {...form.register(`lineItems.${index}.quantity` as const)} className="text-center" />
                                        </div>
                                        <div className="col-span-1 space-y-2">
                                            <Label className={index !== 0 ? 'sr-only' : ''}>Price</Label>
                                            <Input type="number" step="0.01" {...form.register(`lineItems.${index}.unitPrice` as const)} className="text-right" />
                                        </div>
                                        <div className="col-span-3 space-y-2">
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
                                                            {taxGroupsData?.map((tg: any) => (
                                                                <SelectItem key={tg.id} value={tg.id}>{tg.name}</SelectItem>
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

                        {/* Dynamic Tax and Payable Breakdown */}
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-6 pt-8 border-t mt-8">
                            <div>
                                {taxEstimate.taxList.length > 0 && (
                                    <div className="p-4 bg-muted/40 rounded-lg space-y-2 border">
                                        <div className="text-sm font-semibold text-muted-foreground uppercase tracking-wider mb-2">Estimated Levies & VAT Details</div>
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
                                    <span>Est. Standard Taxes:</span>
                                    <span className="font-medium text-amber-600">+{formatAmountWithCurrency(totalTax)}</span>
                                </div>
                                {taxEstimate.withholdingTaxAmount > 0 && (
                                    <div className="flex justify-between w-72 text-sm text-muted-foreground">
                                        <span>Withholding Tax Deduction ({watchWithholdingTaxRate}%):</span>
                                        <span className="font-medium text-red-600">-{formatAmountWithCurrency(taxEstimate.withholdingTaxAmount)}</span>
                                    </div>
                                )}
                                <div className="flex justify-between w-72 text-xl font-bold border-t pt-2 mt-2">
                                    <span>Net Payable:</span>
                                    <span className="text-primary">{formatAmountWithCurrency(taxEstimate.netPayable)}</span>
                                </div>
                            </div>
                        </div>
                    </CardContent>
                    <CardFooter className="flex justify-end space-x-2 bg-muted/50 p-4">
                        <Button variant="outline" type="button" onClick={() => router.back()}>Cancel</Button>
                        <Button type="submit" disabled={isSubmitting}>
                            {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Record Invoice
                        </Button>
                    </CardFooter>
                </Card>
            </form>
        </div>
    );
}
