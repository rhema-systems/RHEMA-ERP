'use client';

import { useState, useEffect, useRef } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useForm, useFieldArray, Controller, type FieldPath } from 'react-hook-form';
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
import { Switch } from '@/components/ui/switch';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
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
import { inventoryManagementService } from '@/services/inventoryManagementService';
import { taxDataService } from '@/services/finance/tax-data.service';
import { invoiceTaxTreatment } from '@/lib/landed-cost-tax';
import { financeService, resolvePostingExchangeRate } from '@/services/finance.service';
import { paymentTermService, type PaymentTermListDto } from '@/services/financeCommonService';
import { TaxApplicability, TaxCategory, type Tax } from '@/types/tax';
import { useToast } from '@/components/ui/use-toast';
import { formatCurrency, cn } from '@/lib/utils';
import { format, addDays } from 'date-fns';
import { useQuery } from '@tanstack/react-query';
import { loadApprovedInvoiceRate } from '@/lib/finance/invoice-exchange-rate';
import { useTenant } from '@/contexts/TenantContext';
import type { ApBudgetCell } from '@/types/ap';
import { receiptBasedInvoiceLines } from '@/lib/finance/ap-goods-invoice-entry';
import { planApSupplierDefaults } from '@/lib/finance/ap-supplier-defaults';
import { PostingAccountPicker } from '@/components/finance/PostingAccountPicker';
import { SourceDocumentDimensionPanel } from '@/components/finance/dimensions/source-document-dimension-panel';
import {
    getSourceLineDimensionAccounts,
    toFinancePostingDimensionValues,
    toFinanceSourceDimensionFormState,
} from '@/lib/finance/source-document-dimensions';

const lineItemSchema = z.object({
    sourceLineId: z.string().uuid(),
    lineItemType: z.enum(['Expense', 'Product', 'Inventory']).default('Expense'),
    glAccountId: z.string().optional(),
    budgetEntryId: z.string().optional(),
    inventoryItemId: z.string().optional(),
    warehouseId: z.string().optional(),
    purchaseOrderItemId: z.string().optional(),
    description: z.string().min(1, 'Description is required'),
    quantity: z.coerce.number().min(0.01, 'Quantity must be positive'),
    unitPrice: z.coerce.number().min(0, 'Unit price must be positive'),
    taxGroupId: z.string().optional(),
    taxTreatment: z.coerce.number().min(1).max(5).optional(),
    discountPercentage: z.coerce.number().min(0).max(100).optional().default(0),
    unit: z.string().optional(),
});

const invoiceSchema = z.object({
    apAccountId: z.string().optional(),
    expenseAccountId: z.string().optional(),
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

export function VendorInvoiceFormPage({ editInvoiceId }: { editInvoiceId?: string }) {
    const router = useRouter();
    const searchParams = useSearchParams();
    const preselectedSupplierId = searchParams.get('supplierId');
    const defaultOpeningBalance = searchParams.get('openingBalance') === 'true';
    const preselectedPurchaseOrderId = searchParams.get('purchaseOrderId');
    const { toast } = useToast();
    const { currentTenantCode } = useTenant();
    const exchangeRateRequestId = useRef(0);
    const editHydratedRef = useRef(false);
    const editBudgetCellsLoadedRef = useRef(false);
    const suppressPurchaseOrderHydrationRef = useRef(false);
    const hydratedPurchaseOrderIdRef = useRef('');
    const isEditMode = Boolean(editInvoiceId);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [paymentTerms, setPaymentTerms] = useState<PaymentTermListDto[]>([]);
    const [applySupplierDefaults, setApplySupplierDefaults] = useState(!editInvoiceId && !defaultOpeningBalance);
    const manualSupplierDefaults = useRef(new Set<string>());
    const supplierSelectionRef = useRef('');
    const [withholdingDecision, setWithholdingDecision] = useState<boolean | null>(defaultOpeningBalance ? false : null);
    const [withholdingRateOverride, setWithholdingRateOverride] = useState<number | null>(null);
    const [withholdingPromptOpen, setWithholdingPromptOpen] = useState(false);
    const withholdingPromptKey = useRef('');

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
    const [budgetCellsByLine, setBudgetCellsByLine] = useState<Record<string, ApBudgetCell[]>>({});
    const [budgetCellsLoading, setBudgetCellsLoading] = useState<Record<string, boolean>>({});
    const [defaultDimensionValues, setDefaultDimensionValues] = useState<Record<string, string>>({});
    const [lineDimensionValues, setLineDimensionValues] = useState<Record<string, Record<string, string>>>({});
    const [applyDefaultToAll, setApplyDefaultToAll] = useState(false);

    // Inventory Item combobox state
    const [inventoryItemOpenIndex, setInventoryItemOpenIndex] = useState<number | null>(null);
    const [inventoryItemSearch, setInventoryItemSearch] = useState('');

    // Queries
    const {
        data: suppliersData,
        isLoading: suppliersLoading,
        error: suppliersError,
    } = useQuery({
        queryKey: ['ap-invoice-suppliers'],
        queryFn: async () => {
            // The entry lookup includes approved Procurement suppliers not yet projected into AP.
            // The command resolves and persists the canonical Supplier id; reports use another lookup.
            const suppliers = await accountsPayableService.getInvoiceSupplierEntryOptions();
            return { items: suppliers };
        },
    });

    const {
        data: editInvoice,
        isLoading: editInvoiceLoading,
        error: editInvoiceError,
    } = useQuery({
        queryKey: ['vendor-invoice', editInvoiceId],
        queryFn: () => editInvoiceId
            ? accountsPayableService.getInvoice(editInvoiceId)
            : Promise.reject(new Error('An invoice id is required for editing.')),
        enabled: isEditMode,
    });

    const { data: glAccountsData, isLoading: glAccountsLoading } = useQuery({
        queryKey: ['gl-accounts-active'],
        queryFn: async () => {
            const accounts = await financeDataService.getAccounts({ status: 'Active', take: 10000 });
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
        queryKey: ['ap-purchase-orders', selectedSupplier?.businessPartnerId || selectedSupplier?.id],
        queryFn: () => purchasingService.getPurchaseOrders({
            pageSize: 100,
            supplierId: selectedSupplier.businessPartnerId || selectedSupplier.id,
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
    const goodsCategory = selectedPurchaseOrder?.procurementCategory === 'Goods';
    const { data: goodsEntry, error: goodsEntryError, isFetching: goodsEntryLoading } = useQuery({
        queryKey: ['ap-goods-invoice-entry', currentTenantCode, selectedPurchaseOrderId, editInvoiceId],
        queryFn: () => accountsPayableService.getGoodsInvoiceEntry(selectedPurchaseOrderId, editInvoiceId),
        enabled: Boolean(selectedPurchaseOrderId && goodsCategory),
    });
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
            apAccountId: '',
            expenseAccountId: '',
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
                { sourceLineId: crypto.randomUUID(), lineItemType: 'Expense', description: '', quantity: 1, unitPrice: 0, discountPercentage: 0, taxGroupId: 'none' }
            ],
        },
    });

    const watchInvoiceDate = form.watch('invoiceDate');
    useEffect(() => {
        const subscription = form.watch((_values, event) => {
            if (event.type !== 'change' || !event.name) return;
            if (['paymentTermId', 'dueDate', 'apAccountId', 'expenseAccountId', 'taxGroupId'].includes(event.name)) manualSupplierDefaults.current.add(event.name);
            const line = /^lineItems\.(\d+)\.(taxGroupId|taxTreatment|glAccountId)$/.exec(event.name);
            if (line) manualSupplierDefaults.current.add(`${form.getValues(`lineItems.${Number(line[1])}.sourceLineId`)}:${line[2]}`);
        });
        return () => subscription.unsubscribe();
    }, [form]);
    const watchInvoiceDateTime = watchInvoiceDate?.getTime();
    useEffect(() => {
        if (watchInvoiceDate) {
            form.setValue('exchangeRateDate', watchInvoiceDate);
        }
    }, [watchInvoiceDate]);

    useEffect(() => {
        setBudgetCellsByLine({});
        form.getValues('lineItems').forEach((_, index) => {
            form.setValue(`lineItems.${index}.budgetEntryId`, undefined);
        });
    }, [form, watchInvoiceDateTime]);

    const { fields, append, remove } = useFieldArray({
        control: form.control,
        name: 'lineItems',
    });

    // Totals and dynamic tax calculation previews
    const watchTaxGroupId = form.watch('taxGroupId');
    const watchApAccountId = form.watch('apAccountId');
    const watchExpenseAccountId = form.watch('expenseAccountId');
    const watchSupplierId = form.watch('supplierId');
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
    const watchWithholdingTaxRate = watchIsOpeningBalance || withholdingDecision !== true ? 0 : Number(form.watch('withholdingTaxRate') || 0);
    const watchLineItems = form.watch('lineItems') || [];
    const { data: supplierDefaults, isFetching: supplierDefaultsLoading, error: supplierDefaultsError } = useQuery({
        queryKey: ['ap-invoice-supplier-defaults', currentTenantCode, watchSupplierId, selectedPurchaseOrderId, watchInvoiceDateTime],
        queryFn: () => accountsPayableService.getInvoiceSupplierDefaults(watchSupplierId, selectedPurchaseOrderId || undefined, watchInvoiceDate ? format(watchInvoiceDate, 'yyyy-MM-dd') : undefined),
        enabled: Boolean(watchSupplierId) && !watchIsOpeningBalance,
    });
    const supplierWithholding = supplierDefaults?.withholdingDefault;
    const withholdingAccountId = watchIsOpeningBalance || withholdingDecision !== true ? null
        : isEditMode && editInvoice?.withholdingTaxId === watchWithholdingTaxId && !manualSupplierDefaults.current.has('withholdingTaxId')
            ? editInvoice.withholdingTaxAccountId || null
            : supplierWithholding?.taxId === watchWithholdingTaxId ? supplierWithholding.taxPayableAccountId || null
                : selectedWithholdingTax?.taxPayableAccountId || null;

    const declineWithholding = () => {
        setWithholdingDecision(false); setWithholdingRateOverride(null);
        form.setValue('withholdingTaxId', 'none'); form.setValue('withholdingTaxRate', 0);
    };
    const acceptWithholding = () => {
        setWithholdingDecision(true); setWithholdingRateOverride(null);
        if (supplierWithholding?.taxId) {
            form.setValue('withholdingTaxId', supplierWithholding.taxId);
            form.setValue('withholdingTaxRate', supplierWithholding.rate);
            manualSupplierDefaults.current.delete('withholdingTaxId');
        }
        return true;
    };
    useEffect(() => {
        if (watchIsOpeningBalance || !watchSupplierId || !supplierWithholding?.required || withholdingDecision !== null) return;
        const key = `${watchSupplierId}:${editInvoiceId || 'new'}`;
        if (withholdingPromptKey.current === key) return;
        withholdingPromptKey.current = key;
        setWithholdingPromptOpen(true);
    }, [watchIsOpeningBalance, watchSupplierId, supplierWithholding?.required, withholdingDecision, editInvoiceId]);
    useEffect(() => {
        if (isEditMode || watchIsOpeningBalance || withholdingDecision !== true || withholdingRateOverride !== null || manualSupplierDefaults.current.has('withholdingTaxId')) return;
        if (supplierWithholding?.taxId && supplierWithholding.taxId === watchWithholdingTaxId && Number(form.getValues('withholdingTaxRate')) !== supplierWithholding.rate)
            form.setValue('withholdingTaxRate', supplierWithholding.rate);
    }, [isEditMode, watchIsOpeningBalance, withholdingDecision, withholdingRateOverride, supplierWithholding, watchWithholdingTaxId, form]);

    const loadBudgetCells = async (
        lineKey: string,
        index: number,
        accountId: string,
        preserveBudgetEntryId?: string
    ) => {
        if (!preserveBudgetEntryId) {
            form.setValue(`lineItems.${index}.budgetEntryId`, undefined);
        }
        setBudgetCellsByLine(current => ({ ...current, [lineKey]: [] }));
        if (!accountId || !watchInvoiceDate) return;
        setBudgetCellsLoading(current => ({ ...current, [lineKey]: true }));
        try {
            const cells = await accountsPayableService.getInvoiceBudgetCells(
                format(watchInvoiceDate, 'yyyy-MM-dd'),
                accountId
            );
            setBudgetCellsByLine(current => ({ ...current, [lineKey]: cells }));
            if (preserveBudgetEntryId) {
                form.setValue(`lineItems.${index}.budgetEntryId`, preserveBudgetEntryId);
            }
        } catch (error: any) {
            toast({
                title: 'Budget cells unavailable',
                description: error.message || 'Unable to load adopted Finance budget cells for this account.',
                variant: 'destructive',
            });
        } finally {
            setBudgetCellsLoading(current => ({ ...current, [lineKey]: false }));
        }
    };

    useEffect(() => {
        if (!isEditMode || !editInvoice || !suppliersData?.items || editHydratedRef.current) return;

        const supplier = suppliersData.items.find(item => item.id === editInvoice.supplierId);
        if (!supplier) return;

        editHydratedRef.current = true;
        editBudgetCellsLoadedRef.current = false;
        setSelectedSupplier(supplier);
        setSelectedPurchaseOrderId(editInvoice.purchaseOrderId || '');
        suppressPurchaseOrderHydrationRef.current = Boolean(editInvoice.purchaseOrderId);
        const dimensionState = toFinanceSourceDimensionFormState(editInvoice.financeDimensions);
        setDefaultDimensionValues(dimensionState.defaultValues);
        setLineDimensionValues(dimensionState.lineValues);
        setApplyDefaultToAll(false);

        const invoiceDate = new Date(editInvoice.invoiceDate);
        setWithholdingDecision(editInvoice.applySupplierWithholdingDefaults ?? (editInvoice.withholdingTaxId ? true : null));
        setWithholdingRateOverride(editInvoice.withholdingTaxRateOverride ?? null);
        const dueDate = editInvoice.dueDate
            ? new Date(editInvoice.dueDate)
            : addDays(invoiceDate, editInvoice.paymentTermsDays || 30);

        form.reset({
            supplierId: editInvoice.supplierId,
            supplierInvoiceNumber: editInvoice.supplierInvoiceNumber || '',
            purchaseOrderId: editInvoice.purchaseOrderId || undefined,
            acceptedSupplyKind: editInvoice.acceptedSupplyKind,
            acceptedSupplySourceId: editInvoice.acceptedSupplySourceId,
            invoiceDate,
            dueDate,
            paymentTermId: editInvoice.paymentTermId || '',
            apAccountId: editInvoice.apAccountId || '',
            expenseAccountId: editInvoice.expenseAccountId || '',
            currencyCode: editInvoice.currencyCode || 'GHS',
            exchangeRate: editInvoice.exchangeRate || 1,
            exchangeRateId: editInvoice.exchangeRateId,
            exchangeRateDate: invoiceDate,
            exchangeRateSource: editInvoice.exchangeRateId ? 'Approved rate' : 'Daily',
            notes: editInvoice.notes || '',
            reference: editInvoice.reference || '',
            taxGroupId: 'none',
            withholdingTaxId: editInvoice.withholdingTaxId || 'none',
            withholdingTaxRate: editInvoice.withholdingTaxRate || 0,
            isOpeningBalance: editInvoice.isOpeningBalance,
            lineItems: editInvoice.lineItems.map(line => ({
                sourceLineId: line.id,
                lineItemType: (line.lineItemType || 'Expense') as 'Expense' | 'Product' | 'Inventory',
                glAccountId: line.glAccountId,
                budgetEntryId: line.budgetEntryId,
                inventoryItemId: line.inventoryItemId,
                warehouseId: line.warehouseId,
                purchaseOrderItemId: line.purchaseOrderItemId,
                description: line.description,
                quantity: line.quantity,
                unitPrice: line.unitPrice,
                taxGroupId: line.taxGroupId || 'none',
                taxTreatment: invoiceTaxTreatment(line.taxTreatment),
                discountPercentage: line.discountPercentage || 0,
                unit: line.unit,
            })),
        });
    }, [editInvoice, form, isEditMode, suppliersData]);

    useEffect(() => {
        if (!isEditMode || !editInvoice || !editHydratedRef.current || editBudgetCellsLoadedRef.current) return;
        if (fields.length !== editInvoice.lineItems.length) return;

        editBudgetCellsLoadedRef.current = true;
        editInvoice.lineItems.forEach((line, index) => {
            if (line.glAccountId) {
                void loadBudgetCells(fields[index].id, index, line.glAccountId, line.budgetEntryId);
            }
        });
    }, [editInvoice, fields, isEditMode]);

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
        form.setValue('exchangeRateId', rateObj.id);
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
        setWithholdingDecision(false); setWithholdingRateOverride(null); setWithholdingPromptOpen(false);
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
        if (item.taxTreatment !== undefined && item.taxTreatment !== 1) return null;
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
            const activeGroupId = resolveLineTaxGroupId(item);
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
        const withholdingTaxAmount = Math.round((subtotal * (watchWithholdingTaxRate / 100) + Number.EPSILON) * 100) / 100;
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
        const selectionKey = `${supplierId}:${preservePurchaseOrderId || ''}`;
        if (supplierSelectionRef.current === selectionKey) return;
        if (!isEditMode && form.getValues('supplierId') !== supplierId) {
            setWithholdingDecision(null); setWithholdingRateOverride(null); setWithholdingPromptOpen(false); withholdingPromptKey.current = '';
            manualSupplierDefaults.current.delete('withholdingTaxId');
            form.setValue('withholdingTaxId', 'none'); form.setValue('withholdingTaxRate', 0);
        }
        supplierSelectionRef.current = selectionKey;
        hydratedPurchaseOrderIdRef.current = '';
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
            if (selectedTerm && !manualSupplierDefaults.current.has('paymentTermId')) {
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
        suppressPurchaseOrderHydrationRef.current = false;
        hydratedPurchaseOrderIdRef.current = '';
        const value = purchaseOrderId === 'none' ? '' : purchaseOrderId;
        setSelectedPurchaseOrderId(value);
        form.setValue('purchaseOrderId', value || undefined);
        form.setValue('acceptedSupplyKind', undefined);
        form.setValue('acceptedSupplySourceId', undefined);
    };

    useEffect(() => {
        if (!selectedPurchaseOrder) return;
        if (suppressPurchaseOrderHydrationRef.current) {
            // The saved draft is authoritative while editing. Loading its PO must not replace
            // existing invoice lines with today's remaining PO quantities.
            return;
        }
        if (hydratedPurchaseOrderIdRef.current === selectedPurchaseOrder.id) return;
        if (goodsCategory && !goodsEntry) {
            form.setValue('lineItems', []);
            return;
        }
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
        const sourceItems = goodsCategory
            ? receiptBasedInvoiceLines(selectedPurchaseOrder.items, goodsEntry)
            : selectedPurchaseOrder.items.map(item => ({ ...item, invoiceQuantity: item.remainingQuantity > 0 ? item.remainingQuantity : item.orderedQuantity }));
        form.setValue('lineItems', sourceItems.map(item => ({
            sourceLineId: crypto.randomUUID(),
            lineItemType: selectedPurchaseOrder.procurementCategory === 'Goods' ? 'Inventory' : 'Expense',
            inventoryItemId: item.inventoryItemId || undefined,
            warehouseId: item.warehouseId || undefined,
            purchaseOrderItemId: item.id,
            description: item.itemDescription || item.itemName || item.itemCode,
            quantity: item.invoiceQuantity,
            unitPrice: item.unitPrice,
            discountPercentage: 0,
            taxGroupId: 'none',
            unit: item.unitOfMeasure,
        })));
        hydratedPurchaseOrderIdRef.current = selectedPurchaseOrder.id;
        if (selectedPurchaseOrder.procurementCategory === 'Goods')
            form.setValue('acceptedSupplyKind', undefined);
        else
            form.setValue('acceptedSupplyKind', 'ServiceCompletion');
    }, [form, selectedPurchaseOrder, toast, goodsCategory, goodsEntry]);

    useEffect(() => {
        if (!applySupplierDefaults || !supplierDefaults || isEditMode || watchIsOpeningBalance) return;
        // The server returns the saved PO snapshot when available; we do not silently refetch
        // today's business-partner tax/rate and recalculate an existing invoice.
        const plan = planApSupplierDefaults(form.getValues(), supplierDefaults, manualSupplierDefaults.current,
            new Set((taxGroupsData || []).map(group => group.id)));
        for (const assignment of plan.assignments) {
            form.setValue(assignment.field as FieldPath<InvoiceFormValues>, assignment.value, { shouldDirty: false });
        }
        if (!manualSupplierDefaults.current.has('paymentTermId') && !manualSupplierDefaults.current.has('dueDate') && supplierDefaults.paymentTermId && form.getValues('paymentTermId') === supplierDefaults.paymentTermId) {
            const dueDays = supplierDefaults.paymentTermsDays ?? paymentTerms.find(term => term.id === supplierDefaults.paymentTermId)?.dueDays;
            if (dueDays != null) {
                const dueDate = addDays(form.getValues('invoiceDate'), dueDays);
                if (form.getValues('dueDate')?.getTime() !== dueDate.getTime()) form.setValue('dueDate', dueDate);
            }
        }
    }, [applySupplierDefaults, supplierDefaults, isEditMode, watchIsOpeningBalance, taxGroupsData, paymentTerms, form, watchLineItems, watchTaxGroupId, watchApAccountId, watchExpenseAccountId, watchInvoiceDateTime]);

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
            if (goodsCategory) {
                if (!goodsEntry || goodsEntryError || goodsEntryLoading) {
                    toast({ title: 'Accepted receipt quantities unavailable', description: 'Wait for the accepted-receipt check before recording this invoice.', variant: 'destructive' });
                    return;
                }
                const totals = new Map<string, number>();
                for (const line of data.lineItems) totals.set(line.purchaseOrderItemId || '', (totals.get(line.purchaseOrderItemId || '') || 0) + Number(line.quantity));
                if ([...totals].some(([id, quantity]) => quantity > (goodsEntry.lines.find(line => line.purchaseOrderItemId === id)?.availableQuantity || 0))) {
                    toast({ title: 'Receipt quantity exceeded', description: 'Use only accepted quantities not already invoiced. Rejected and pending quantities cannot be billed.', variant: 'destructive' });
                    return;
                }
            }
            const isOpeningBalance = data.isOpeningBalance;
            if (serviceCategory && !data.acceptedSupplySourceId) {
                toast({
                    title: 'Service completion required',
                    description: 'Select an approved service completion before recording this invoice.',
                    variant: 'destructive',
                });
                return;
            }
            if (!isOpeningBalance && supplierWithholding?.required && withholdingDecision === null) {
                setWithholdingPromptOpen(true);
                toast({ title: 'Choose withholding for this invoice', description: 'Select Yes or No before saving.', variant: 'destructive' });
                return;
            }
            if (!isOpeningBalance && withholdingDecision === true && (!data.withholdingTaxId || data.withholdingTaxId === 'none' || !withholdingAccountId)) {
                toast({
                    title: 'WHT configuration required',
                    description: supplierWithholding?.message || 'Select an applicable WHT configuration with a payable account.',
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
            const defaultsPlan = supplierDefaults ? planApSupplierDefaults(data, supplierDefaults, manualSupplierDefaults.current,
                new Set((taxGroupsData || []).map(group => group.id))) : null;
            const request = {
                ...data,
                apAccountId: data.apAccountId || undefined,
                expenseAccountId: data.expenseAccountId || undefined,
                paymentTermsDays: !isOpeningBalance && applySupplierDefaults && supplierDefaults?.paymentTermId === data.paymentTermId && !manualSupplierDefaults.current.has('paymentTermId') && !manualSupplierDefaults.current.has('dueDate')
                    ? supplierDefaults?.paymentTermsDays ?? undefined : undefined,
                applyBusinessPartnerDefaults: !isEditMode && !isOpeningBalance && applySupplierDefaults && defaultsPlan?.allowServerDefaults === true,
                applySupplierWithholdingDefaults: isOpeningBalance ? false : withholdingDecision,
                withholdingTaxRateOverride: isOpeningBalance || withholdingDecision !== true ? null : withholdingRateOverride,
                invoiceDate: data.invoiceDate.toISOString(),
                dueDate: data.dueDate.toISOString(),
                taxGroupId: isOpeningBalance || data.taxGroupId === 'none' ? null : (data.taxGroupId || null),
                exchangeRate: Number(data.exchangeRate) || 1.0,
                exchangeRateId: data.exchangeRateId,
                // The backend resolves rate/account again from this tax id. Sending the displayed
                // values keeps the compatibility DTO descriptive but grants them no authority.
                withholdingTaxId: isOpeningBalance || withholdingDecision !== true || data.withholdingTaxId === 'none' ? null : data.withholdingTaxId,
                withholdingTaxRate: isOpeningBalance ? 0 : watchWithholdingTaxRate,
                withholdingTaxAccountId: withholdingAccountId,
                isOpeningBalance,
                acceptedSupplyKind: serviceCategory ? ('ServiceCompletion' as const) : data.acceptedSupplyKind,
                acceptedSupplySourceId: data.acceptedSupplySourceId,
                lineItems: data.lineItems.map(item => {
                    const lineTax = calculateLineTax(item, isOpeningBalance, data.taxGroupId);
                    return {
                        id: item.sourceLineId,
                        lineItemType: item.lineItemType,
                        glAccountId: item.glAccountId || null,
                        budgetEntryId: item.budgetEntryId || null,
                        purchaseOrderItemId: item.purchaseOrderItemId || null,
                        description: item.description,
                        quantity: Number(item.quantity),
                        unitPrice: Number(item.unitPrice),
                        discountPercentage: Number(item.discountPercentage),
                        taxRate: lineTax.taxRate,
                        taxTreatment: item.taxTreatment,
                        taxGroupId: resolveLineTaxGroupId(item, isOpeningBalance, data.taxGroupId),
                        unit: item.unit || null,
                        inventoryItemId: item.inventoryItemId || null,
                        warehouseId: item.warehouseId || null,
                    } as any;
                }),
                financeDimensions: {
                    defaultDimensions: toFinancePostingDimensionValues(defaultDimensionValues),
                    lines: data.lineItems.flatMap(item => {
                        const editableAccountId = !isOpeningBalance
                            && !data.purchaseOrderId
                            && item.lineItemType === 'Expense'
                            ? item.glAccountId
                            : undefined;
                        const { accountId } = getSourceLineDimensionAccounts(isOpeningBalance ? undefined : editInvoice?.financeDimensions, item.sourceLineId, editableAccountId);
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
                await accountsPayableService.updateInvoice(editInvoice.id, {
                    ...request,
                    id: editInvoice.id,
                    receivedDate: editInvoice.receivedDate,
                    paymentTermsDays: editInvoice.paymentTermsDays,
                    earlyPaymentDiscountPercentage: editInvoice.earlyPaymentDiscountPercentage,
                    earlyPaymentDiscountDueDate: editInvoice.earlyPaymentDiscountDueDate,
                    withholdingCertificateNumber: editInvoice.withholdingCertificateNumber,
                    withholdingCertificateDate: editInvoice.withholdingCertificateDate,
                    matchingType: editInvoice.matchingType,
                    expenseAccountId: data.expenseAccountId || editInvoice.expenseAccountId,
                    apAccountId: data.apAccountId || editInvoice.apAccountId,
                });
            } else {
                await accountsPayableService.createInvoice(request);
            }

            toast({
                title: 'Success',
                description: isEditMode
                    ? 'Vendor invoice updated successfully'
                    : 'Vendor invoice created successfully',
            });

            router.push(isEditMode && editInvoice
                ? `/finance/ap/invoices/${editInvoice.id}`
                : '/finance/ap/invoices');
        } catch (error: any) {
            toast({
                title: 'Error',
                description: error.message || `Failed to ${isEditMode ? 'update' : 'create'} vendor invoice`,
                variant: 'destructive',
            });
        } finally {
            setIsSubmitting(false);
        }
    };

    const editSupplierMissing = Boolean(
        isEditMode &&
        editInvoice &&
        suppliersData &&
        !suppliersData.items.some(item => item.id === editInvoice.supplierId)
    );

    if (
        isEditMode &&
        (editInvoiceLoading || !editHydratedRef.current) &&
        !editInvoiceError &&
        !suppliersError &&
        !editSupplierMissing
    ) {
        return (
            <div className="flex min-h-[50vh] items-center justify-center">
                <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
            </div>
        );
    }

    if (isEditMode && (editInvoiceError || suppliersError || editSupplierMissing)) {
        return (
            <div className="mx-auto max-w-2xl space-y-4 p-8">
                <h1 className="text-2xl font-bold">Unable to edit vendor invoice</h1>
                <p className="text-muted-foreground">
                    {(editInvoiceError as Error | undefined)?.message ||
                        (suppliersError as Error | undefined)?.message ||
                        (editSupplierMissing
                            ? 'The invoice supplier is not available in the current tenant.'
                            : 'The invoice could not be loaded.')}
                </p>
                <Button variant="outline" onClick={() => router.push('/finance/ap/invoices')}>
                    Back to invoices
                </Button>
            </div>
        );
    }

    if (isEditMode && editInvoice && !['Draft', 'Rejected'].includes(editInvoice.status)) {
        return (
            <div className="mx-auto max-w-2xl space-y-4 p-8">
                <h1 className="text-2xl font-bold">Invoice can no longer be edited</h1>
                <p className="text-muted-foreground">Only Draft or Rejected invoices may be changed.</p>
                <Button variant="outline" onClick={() => router.push(`/finance/ap/invoices/${editInvoice.id}`)}>
                    View invoice
                </Button>
            </div>
        );
    }

    return (
        <div className="space-y-8 p-8 max-w-[1400px] mx-auto">
            <div className="flex items-center space-x-4">
                <Button variant="ghost" size="icon" onClick={() => router.back()}>
                    <ArrowLeft className="h-4 w-4" />
                </Button>
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">
                        {isEditMode ? `Edit ${editInvoice?.invoiceNumber || 'Vendor Invoice'}` : 'Record Vendor Invoice'}
                    </h1>
                    <p className="text-muted-foreground">
                        {isEditMode ? 'Update this draft without losing its accounting evidence.' : 'Enter a bill received from a supplier.'}
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
                                        id="supplier"
                                        aria-label="Supplier"
                                        aria-expanded={supplierComboOpen}
                                        className="w-full justify-between"
                                        disabled={isEditMode}
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
                                            manualSupplierDefaults.current.add('paymentTermId');
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
                                    <Select value={watchIsOpeningBalance ? 'none' : (field.value || 'none')} onValueChange={value => { manualSupplierDefaults.current.add('taxGroupId'); field.onChange(value); }} disabled={watchIsOpeningBalance}>
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

                        <div className="space-y-2 md:col-span-2">
                            <div className="flex flex-wrap items-center gap-3">
                                <Switch id="invoiceSubjectToWithholding" checked={!watchIsOpeningBalance && withholdingDecision === true} disabled={watchIsOpeningBalance}
                                    onCheckedChange={checked => { if (checked) setWithholdingPromptOpen(true); else declineWithholding(); }} />
                                <Label htmlFor="invoiceSubjectToWithholding">Subject to withholding</Label>
                                {supplierWithholding?.required && withholdingDecision === null && !watchIsOpeningBalance && <Button type="button" variant="outline" size="sm" onClick={() => setWithholdingPromptOpen(true)}>Review withholding</Button>}
                            </div>
                            <div className="grid gap-3 sm:grid-cols-[1fr_130px]">
                            <div className="space-y-1.5"><Label htmlFor="invoiceWithholdingTaxId">WHT Configuration</Label>
                            <Controller
                                control={form.control}
                                name="withholdingTaxId"
                                render={({ field }) => (
                                    <Select value={watchIsOpeningBalance ? 'none' : String(field.value || 'none')} onValueChange={value => {
                                        manualSupplierDefaults.current.add('withholdingTaxId');
                                        if (value === 'none') { declineWithholding(); return; }
                                        const rate = Number(withholdingTaxOptions.find(tax => tax.id === value)?.rate || 0);
                                        field.onChange(value); setWithholdingDecision(true); setWithholdingRateOverride(rate);
                                        form.setValue('withholdingTaxRate', rate);
                                    }} disabled={watchIsOpeningBalance || withholdingDecision !== true}>
                                        <SelectTrigger id="invoiceWithholdingTaxId" className={cn(watchIsOpeningBalance && 'bg-muted text-muted-foreground')}>
                                            <SelectValue placeholder="No WHT" />
                                        </SelectTrigger>
                                        <SelectContent>
                                            <SelectItem value="none">No WHT</SelectItem>
                                            {watchWithholdingTaxId && watchWithholdingTaxId !== 'none' && !selectedWithholdingTax && <SelectItem value={watchWithholdingTaxId}>{isEditMode ? 'Saved' : 'Supplier'} WHT ({watchWithholdingTaxRate}%)</SelectItem>}
                                            {withholdingTaxOptions.map(tax => (
                                                <SelectItem key={tax.id} value={tax.id}>
                                                    {tax.code} - {tax.name} ({Number(tax.rate || 0)}%)
                                                </SelectItem>
                                            ))}
                                        </SelectContent>
                                    </Select>
                                )}
                            />
                            </div>
                            <div className="space-y-1.5"><Label htmlFor="invoiceWithholdingRate">WHT Rate (%)</Label>
                                <Input id="invoiceWithholdingRate" type="number" min="0" max="100" step="0.0001" disabled={watchIsOpeningBalance || withholdingDecision !== true}
                                    value={form.watch('withholdingTaxRate') ?? 0} onChange={event => {
                                        const rate = event.target.value === '' ? 0 : Number(event.target.value);
                                        form.setValue('withholdingTaxRate', rate, { shouldDirty: true, shouldValidate: true }); setWithholdingRateOverride(rate);
                                    }} />
                                {form.formState.errors.withholdingTaxRate && <p className="text-xs text-destructive">Enter a rate from 0 to 100.</p>}
                            </div></div>
                            {withholdingDecision === true && !withholdingAccountId && <p role="status" className="text-xs text-destructive">{supplierWithholding?.message || 'Select a WHT configuration with a payable account.'}</p>}
                        </div>

                        {!isEditMode && selectedSupplier && (
                            <div className="space-y-3 rounded-lg border p-3 md:col-span-2 lg:col-span-3">
                                <div className="flex flex-wrap items-center justify-between gap-3">
                                    <div className="flex items-center gap-2">
                                        <Checkbox id="applySupplierDefaults" checked={applySupplierDefaults} disabled={watchIsOpeningBalance} onCheckedChange={checked => setApplySupplierDefaults(checked === true)} />
                                        <Label htmlFor="applySupplierDefaults">Use supplier defaults</Label>
                                    </div>
                                    {supplierDefaultsLoading && <span className="text-xs text-muted-foreground">Loading defaults...</span>}
                                    {applySupplierDefaults && selectedPurchaseOrderId && supplierDefaults?.paymentTermsDays != null && <span className="text-xs text-muted-foreground">PO payment terms: {supplierDefaults.paymentTermsDays} days</span>}
                                </div>
                                {supplierDefaultsError && <p role="status" className="text-sm text-amber-700">Supplier defaults unavailable. You can enter the invoice details manually.</p>}
                                {applySupplierDefaults && supplierDefaults?.postingDefaults.defaultTaxGroupId && taxGroupsData && !taxGroupsData.some(group => group.id === supplierDefaults.postingDefaults.defaultTaxGroupId) && <p role="status" className="text-sm text-amber-700">The saved supplier tax schedule is unavailable. Select tax manually.</p>}
                                {(applySupplierDefaults || watchApAccountId || watchExpenseAccountId) && <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
                                    <div className="space-y-1.5"><Label htmlFor="apAccountId">Accounts Payable</Label><Controller control={form.control} name="apAccountId" render={({ field }) => <PostingAccountPicker id="apAccountId" value={field.value} accounts={(glAccountsData?.items || []).filter(account => account.accountType === 'Liability' && (account.allowDirectPosting || account.isControlAccount))} onChange={value => { manualSupplierDefaults.current.add('apAccountId'); field.onChange(value || ''); }} />} /></div>
                                    <div className="space-y-1.5"><Label htmlFor="expenseAccountId">Purchases Account</Label><Controller control={form.control} name="expenseAccountId" render={({ field }) => <PostingAccountPicker id="expenseAccountId" value={field.value} accounts={(glAccountsData?.items || []).filter(account => !account.isControlAccount && account.allowDirectPosting && ['Asset', 'Expense'].includes(account.accountType))} onChange={value => { manualSupplierDefaults.current.add('expenseAccountId'); field.onChange(value || ''); }} />} /></div>
                                </div>}
                            </div>
                        )}

                        <div className="space-y-2 lg:col-span-3">
                            <Label htmlFor="notes">Notes/Memo</Label>
                            <Textarea id="notes" placeholder="Reference number, payment instructions, etc." {...form.register('notes')} />
                        </div>
                    </CardContent>
                </Card>

                <Card>
                    <CardHeader>
                        <CardTitle>Finance coding dimensions</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <SourceDocumentDimensionPanel
                            context={{
                                sourceModule: 'AP',
                                sourceDocumentType: 'VendorInvoice',
                                postingAction: 'Post',
                                sourceRoute: 'finance.ap.vendor-invoices.manual',
                                contractVersion: '1.0',
                            }}
                            effectiveDate={format(watchInvoiceDate || new Date(), 'yyyy-MM-dd')}
                            lines={watchLineItems.map((item) => ({
                                id: item.sourceLineId,
                                ...getSourceLineDimensionAccounts(watchIsOpeningBalance ? undefined : editInvoice?.financeDimensions, item.sourceLineId, !watchIsOpeningBalance
                                    && !selectedPurchaseOrderId
                                    && item.lineItemType === 'Expense'
                                    ? item.glAccountId
                                    : undefined),
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
                            certificationState={editInvoice?.financeDimensions?.certificationState}
                        />
                    </CardContent>
                </Card>

                {/* Line Items Card */}
                {goodsCategory && (
                    <div role={goodsEntryError ? 'alert' : 'status'} className={`rounded-lg border p-3 text-sm ${goodsEntryError ? 'border-destructive text-destructive' : 'text-muted-foreground'}`}>
                        {goodsEntryLoading ? 'Checking accepted receipt quantities…' : goodsEntryError
                            ? (goodsEntryError instanceof Error ? goodsEntryError.message : 'Accepted receipt quantities are unavailable.')
                            : `Receipt-based invoice: ${goodsEntry?.lines.reduce((sum, line) => sum + line.availableQuantity, 0) || 0} accepted units remain uninvoiced. Pending and rejected quantities are excluded.`}
                    </div>
                )}
                <Card>
                    <CardHeader className="flex flex-row items-center justify-between">
                        <CardTitle>Line Items</CardTitle>
                        <Button type="button" variant="outline" size="sm" onClick={() => append({ sourceLineId: crypto.randomUUID(), lineItemType: 'Expense' as const, description: '', quantity: 1, unitPrice: 0, discountPercentage: 0, taxGroupId: 'none' })}>
                            <Plus className="mr-2 h-4 w-4" /> Add Item
                        </Button>
                    </CardHeader>
                    <CardContent>
                        <div className="space-y-4">
                            {fields.map((field, index) => {
                                const lineItemType = form.watch(`lineItems.${index}.lineItemType`);
                                const selectedGlAccount = glAccountsData?.items?.find(
                                    (account: any) => account.id === form.watch(`lineItems.${index}.glAccountId`)
                                );
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
                                                        render={({ field: accountField }) => (
                                                            <Popover
                                                                open={glAccountOpenIndex === index}
                                                                onOpenChange={(open) => { setGlAccountOpenIndex(open ? index : null); if (!open) setGlAccountSearch(''); }}
                                                            >
                                                                <PopoverTrigger asChild>
                                                                    <Button variant="outline" role="combobox" className="w-full justify-between text-left font-medium line-clamp-1 h-10 px-3">
                                                                        <span className="truncate text-sm">
                                                                            {getAccountDisplay(accountField.value) || (glAccountsLoading ? "Loading..." : "Select account...")}
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
                                                                                            manualSupplierDefaults.current.add(`${form.getValues(`lineItems.${index}.sourceLineId`)}:glAccountId`);
                                                                                            accountField.onChange(account.id);
                                                                                            void loadBudgetCells(field.id, index, account.id);
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
                                                    {(budgetCellsLoading[field.id]
                                                        || (budgetCellsByLine[field.id]?.length ?? 0) > 0
                                                        || selectedGlAccount?.budgetTrackingEnabled) && (
                                                        <Controller
                                                            control={form.control}
                                                            name={`lineItems.${index}.budgetEntryId`}
                                                            render={({ field: budgetField }) => (
                                                                <Select
                                                                    value={budgetField.value}
                                                                    onValueChange={budgetField.onChange}
                                                                    disabled={budgetCellsLoading[field.id] || (budgetCellsByLine[field.id]?.length ?? 0) === 0}
                                                                >
                                                                    <SelectTrigger className="mt-2 h-auto min-h-10 text-left">
                                                                        <SelectValue placeholder={budgetCellsLoading[field.id]
                                                                            ? 'Loading budget cells...'
                                                                            : (budgetCellsByLine[field.id]?.length ?? 0) === 0
                                                                                ? 'No adopted budget cell'
                                                                                : 'Select adopted budget cell'} />
                                                                    </SelectTrigger>
                                                                    <SelectContent>
                                                                        {(budgetCellsByLine[field.id] || []).map(cell => (
                                                                            <SelectItem key={cell.budgetEntryId} value={cell.budgetEntryId}>
                                                                                <span className="flex flex-col">
                                                                                    <span>{cell.dimensionAssignments.map(item => `${item.dimensionCode}: ${item.valueCode}`).join(' · ') || 'Account total'}</span>
                                                                                    <span className="text-xs text-muted-foreground">
                                                                                        {cell.fiscalPeriodCode} · {formatCurrency(cell.availableAmount, cell.functionalCurrencyCode)} available
                                                                                    </span>
                                                                                </span>
                                                                            </SelectItem>
                                                                        ))}
                                                                    </SelectContent>
                                                                </Select>
                                                            )}
                                                        />
                                                    )}
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
                                            {editInvoice?.lineItems.some(line => line.landedCostItemId) && <>
                                                <Label>Tax treatment</Label>
                                                <Controller control={form.control} name={`lineItems.${index}.taxTreatment`}
                                                    render={({ field }) => <Select value={String(field.value ?? 5)} onValueChange={value => {
                                                        field.onChange(Number(value));
                                                        if (value !== '1') form.setValue(`lineItems.${index}.taxGroupId`, 'none');
                                                    }}>
                                                        <SelectTrigger aria-label={`Line ${index + 1} tax treatment`}><SelectValue /></SelectTrigger>
                                                        <SelectContent>
                                                            <SelectItem value="5">Pending review</SelectItem>
                                                            <SelectItem value="1">Standard — select tax group</SelectItem>
                                                            <SelectItem value="2">Exempt</SelectItem>
                                                            <SelectItem value="3">Zero rated</SelectItem>
                                                            <SelectItem value="4">Out of scope</SelectItem>
                                                        </SelectContent>
                                                    </Select>} />
                                                {form.watch(`lineItems.${index}.taxTreatment`) === 5 && <p className="text-xs text-amber-700">Complete tax review before submitting this draft.</p>}
                                            </>}
                                            <Controller
                                                control={form.control}
                                                name={`lineItems.${index}.taxGroupId`}
                                                render={({ field }) => (
                                                    <Select 
                                                        value={watchIsOpeningBalance ? 'none' : (field.value || 'inherit')}
                                                        onValueChange={(val) => { manualSupplierDefaults.current.add(`${form.getValues(`lineItems.${index}.sourceLineId`)}:taxGroupId`); field.onChange(val === 'inherit' ? '' : val); }}
                                                        disabled={watchIsOpeningBalance || (Boolean(editInvoice?.lineItems.some(line => line.landedCostItemId)) && form.watch(`lineItems.${index}.taxTreatment`) !== 1)}
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
                                                            <SelectItem value="none">{editInvoice?.lineItems.some(line => line.landedCostItemId) ? 'No tax group selected' : 'Zero-rated / Exempt'}</SelectItem>
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
                                {!watchIsOpeningBalance && withholdingDecision === true && (
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
                        <Button type="submit" disabled={isSubmitting || (!watchIsOpeningBalance && supplierDefaultsLoading && (!isEditMode || withholdingDecision === null)) || Boolean(goodsCategory && (goodsEntryLoading || goodsEntryError || !goodsEntry || fields.length === 0))}>
                            {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            {isEditMode ? 'Save Changes' : 'Record Invoice'}
                        </Button>
                    </CardFooter>
                </Card>
            </form>
            <ConfirmationDialog open={withholdingPromptOpen && !watchIsOpeningBalance} onOpenChange={setWithholdingPromptOpen}
                title="Apply withholding to this invoice?"
                description={supplierWithholding?.required
                    ? `This supplier is subject to withholding at ${supplierWithholding.rate}%. Apply it to this invoice? You can change the invoice rate after selecting Yes.`
                    : 'Apply withholding to this invoice? You can select the configuration and rate after selecting Yes.'}
                confirmText="Yes" cancelText="No" onConfirm={acceptWithholding} onCancel={declineWithholding} />
        </div>
    );
}

export default function CreateVendorInvoicePage() {
    return <VendorInvoiceFormPage />;
}
