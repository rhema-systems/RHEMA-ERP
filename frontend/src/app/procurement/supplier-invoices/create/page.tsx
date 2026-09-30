'use client';

import { useState, useEffect, useRef } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import {
  useForm,
  useFieldArray,
  Controller,
  type FieldPath,
  type FieldErrors,
} from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { supplierInvoiceSchema as invoiceSchema, type SupplierInvoiceFormValues as InvoiceFormValues } from '@/lib/procurement/supplier-invoice-form';
import {
  ArrowLeft,
  Loader2,
  Plus,
  Trash2,
  Check,
  ChevronsUpDown,
  ChevronDown,
  FileText,
  Maximize2,
  Minimize2,
  MoreHorizontal,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { FinanceDateInput } from '@/components/finance/finance-date-input';
import { accountsPayableService } from '@/services/procurementSupplierInvoiceService';
import { purchasingService } from '@/services/purchasingService';
import { financeDataService } from '@/services/finance/finance-data.service';
import { inventoryManagementService } from '@/services/inventoryManagementService';
import { taxDataService } from '@/services/finance/tax-data.service';
import { invoiceTaxTreatment } from '@/lib/landed-cost-tax';
import {
  financeService,
  resolvePostingExchangeRate,
} from '@/services/finance.service';
import {
  paymentTermService,
  type PaymentTermListDto,
} from '@/services/financeCommonService';
import { TaxApplicability, TaxCategory, type Tax } from '@/types/tax';
import { useToast } from '@/components/ui/use-toast';
import { formatCurrency, cn } from '@/lib/utils';
import { format, addDays } from 'date-fns';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { cacheSavedSupplierInvoice } from '@/lib/procurement/supplier-invoice-cache';
import { loadApprovedInvoiceRate } from '@/lib/finance/invoice-exchange-rate';
import { useTenant } from '@/contexts/TenantContext';
import type { ApBudgetCell } from '@/types/ap';
import { receiptBasedInvoiceLines } from '@/lib/finance/ap-goods-invoice-entry';
import { EstateInvoiceSource } from '@/components/procurement/EstateInvoiceSource';
import { ProcurementInvoiceDistribution } from '@/components/procurement/ProcurementInvoiceDistribution';
import lineGridStyles from './invoice-lines.module.css';
import {
  planApSupplierDefaults,
  isApExpenseLineType,
} from '@/lib/finance/ap-supplier-defaults';
import { PostingAccountPicker } from '@/components/finance/PostingAccountPicker';
import { SourceDocumentDimensionPanel } from '@/components/finance/dimensions/source-document-dimension-panel';
import {
  getSourceLineDimensionAccounts,
  toFinancePostingDimensionValues,
  toFinanceSourceDimensionFormState,
} from '@/lib/finance/source-document-dimensions';
import { calculateNetTradeDiscountLineAmount } from '@/lib/finance/invoice-trade-discount';

function getInvoiceValidationMessages(errors: FieldErrors<InvoiceFormValues>): string[] {
  const messages: string[] = [];
  const visit = (value: unknown, path: string[]) => {
    if (!value || typeof value !== 'object') return;
    const error = value as Record<string, unknown>;
    if (typeof error.message === 'string') {
      const label = path[0] === 'lineItems' && /^\d+$/.test(path[1] || '')
        ? `Line ${Number(path[1]) + 1} ${path.slice(2).join(' ')}`
        : path.join(' ');
      messages.push(`${label.replace(/([a-z])([A-Z])/g, '$1 $2')}: ${error.message}`);
      return;
    }
    Object.entries(error).forEach(([key, child]) => {
      if (!['ref', 'type', 'types'].includes(key)) visit(child, [...path, key]);
    });
  };
  visit(errors, []);
  return messages;
}

export function VendorInvoiceFormPage({
  editInvoiceId,
}: {
  editInvoiceId?: string;
}) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const searchParams = useSearchParams();
  const preselectedSupplierId = searchParams.get('businessPartnerId');
  const defaultOpeningBalance = false;
  const preselectedPurchaseOrderId = searchParams.get('purchaseOrderId');
  const prefilledLineDescription = searchParams.get('lineDescription') || '';
  const prefilledQuantity = Number(searchParams.get('quantity') || 1);
  const prefilledUnitPrice = Number(searchParams.get('unitPrice') || 0);
  const prefilledCurrencyCode = (searchParams.get('currencyCode') || 'GHS').trim().toUpperCase();
  const prefilledNotes = searchParams.get('notes') || '';
  const prefilledInvoiceDate = searchParams.get('invoiceDate') || '';
  const prefilledDueDate = searchParams.get('dueDate') || '';
  const prefilledPaymentTermId = searchParams.get('paymentTermId') || '';
  const prefilledPaymentTermsDays = Number(searchParams.get('paymentTermsDays') || NaN);
  const parsePrefilledDate = (value: string, fallback: Date) => {
    if (!value) return fallback;
    const date = new Date(`${value.slice(0, 10)}T00:00:00`);
    return Number.isFinite(date.getTime()) ? date : fallback;
  };
  const defaultInvoiceDate = parsePrefilledDate(prefilledInvoiceDate, new Date());
  const defaultDueDate = parsePrefilledDate(
    prefilledDueDate,
    addDays(
      defaultInvoiceDate,
      Number.isFinite(prefilledPaymentTermsDays) && prefilledPaymentTermsDays >= 0
        ? prefilledPaymentTermsDays
        : 30
    )
  );
  const { toast } = useToast();
  const { currentTenantCode } = useTenant();
  const exchangeRateRequestId = useRef(0);
  const editHydratedRef = useRef(false);
  const editBudgetCellsLoadedRef = useRef(new Set<string>());
  const suppressPurchaseOrderHydrationRef = useRef(false);
  const hydratedPurchaseOrderIdRef = useRef('');
  const isEditMode = Boolean(editInvoiceId);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [linesFullScreen, setLinesFullScreen] = useState(false);
  useEffect(() => {
    if (!linesFullScreen) return;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !event.defaultPrevented)
        setLinesFullScreen(false);
    };
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.body.style.overflow = previousOverflow;
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [linesFullScreen]);
  const [paymentTerms, setPaymentTerms] = useState<PaymentTermListDto[]>([]);
  const [applySupplierDefaults, setApplySupplierDefaults] = useState(
    !editInvoiceId && !defaultOpeningBalance
  );
  const manualSupplierDefaults = useRef(new Set<string>(
    prefilledPaymentTermId || prefilledDueDate ? ['paymentTermId', 'dueDate'] : []
  ));
  const supplierSelectionRef = useRef('');
  const [withholdingDecision, setWithholdingDecision] = useState<
    boolean | null
  >(defaultOpeningBalance ? false : null);
  const [withholdingRateOverride, setWithholdingRateOverride] = useState<
    number | null
  >(null);
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
  const [glAccountOpenIndex, setGlAccountOpenIndex] = useState<number | null>(
    null
  );
  const [glAccountSearch, setGlAccountSearch] = useState('');
  const [budgetCellsByLine, setBudgetCellsByLine] = useState<
    Record<string, ApBudgetCell[]>
  >({});
  const [budgetCellsLoading, setBudgetCellsLoading] = useState<
    Record<string, boolean>
  >({});
  const [defaultDimensionValues, setDefaultDimensionValues] = useState<
    Record<string, string>
  >({});
  const [lineDimensionValues, setLineDimensionValues] = useState<
    Record<string, Record<string, string>>
  >({});
  const [applyDefaultToAll, setApplyDefaultToAll] = useState(false);

  // Inventory Item combobox state
  const [inventoryItemOpenIndex, setInventoryItemOpenIndex] = useState<
    number | null
  >(null);
  const [inventoryItemSearch, setInventoryItemSearch] = useState('');

  // Queries
  const {
    data: suppliersData,
    isLoading: suppliersLoading,
    error: suppliersError,
  } = useQuery({
    queryKey: ['ap-invoice-entry-suppliers'],
    queryFn: async () => {
      // Canonical Business Partner roles include readiness reasons for incomplete profiles.
      const suppliers =
        await accountsPayableService.getInvoiceSupplierEntryOptions();
      return { items: suppliers };
    },
  });

  const {
    data: editInvoice,
    isLoading: editInvoiceLoading,
    error: editInvoiceError,
  } = useQuery({
    queryKey: ['vendor-invoice', editInvoiceId],
    queryFn: () =>
      editInvoiceId
        ? accountsPayableService.getInvoice(editInvoiceId)
        : Promise.reject(new Error('An invoice id is required for editing.')),
    enabled: isEditMode,
  });
  const estateSourceLocked = Boolean(editInvoice?.estateAcquisitionId);
  const receiptSourceLocked =
    Boolean(editInvoice?.isProcurementAutoInvoice) || estateSourceLocked;
  const sourceTaxReview =
    receiptSourceLocked ||
    Boolean(editInvoice?.lineItems.some((line) => line.landedCostItemId));

  const { data: glAccountsData, isLoading: glAccountsLoading } = useQuery({
    queryKey: ['gl-accounts-active'],
    queryFn: async () => {
      const accounts = await financeDataService.getAccounts({
        status: 'Active',
        take: 10000,
      });
      return {
        items: accounts.filter((account: any) => account.isActive !== false),
      };
    },
  });

  const { data: inventoryItemsData, isLoading: inventoryItemsLoading } =
    useQuery({
      queryKey: ['inventory-items-active'],
      queryFn: () =>
        inventoryManagementService.getInventoryItems({
          pageSize: 100,
          isActive: true,
        } as any),
    });

  const { data: taxGroupsData } = useQuery({
    queryKey: [
      'tax-groups-active',
      'procurement-supplier-invoice',
      currentTenantCode,
      'Purchases',
    ],
    queryFn: async () => {
      const groups = await taxDataService.getActiveTaxGroups(
        TaxApplicability.Purchases
      );
      return groups.filter(
        (group) =>
          !group.components.some(
            (component) =>
              component.taxCategory === TaxCategory.Withholding ||
              component.taxCategory === TaxCategory.VatWithholding
          )
      );
    },
  });

  const { data: withholdingTaxes = [] } = useQuery({
    queryKey: ['taxes', 'ap-invoice-withholding'],
    queryFn: () =>
      taxDataService.getActiveTaxes({
        applicability: TaxApplicability.Purchases,
        category: TaxCategory.Withholding,
      }),
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

  const { data: purchaseOrdersData, isLoading: purchaseOrdersLoading } =
    useQuery({
      queryKey: [
        'ap-purchase-orders',
        selectedSupplier?.businessPartnerId || selectedSupplier?.id,
      ],
      queryFn: () =>
        purchasingService.getPurchaseOrders({
          pageSize: 100,
          supplierId: selectedSupplier.businessPartnerId || selectedSupplier.id,
        }),
      enabled: Boolean(selectedSupplier?.id),
    });

  const { data: selectedPurchaseOrder } = useQuery({
    queryKey: ['ap-purchase-order', selectedPurchaseOrderId],
    queryFn: () =>
      purchasingService.getPurchaseOrderById(selectedPurchaseOrderId),
    enabled: Boolean(selectedPurchaseOrderId),
  });

  const serviceCategory =
    selectedPurchaseOrder?.procurementCategory &&
    selectedPurchaseOrder.procurementCategory !== 'Goods' &&
    selectedPurchaseOrder.procurementCategory !== 'Works';
  const goodsCategory = selectedPurchaseOrder?.procurementCategory === 'Goods';
  const {
    data: goodsEntry,
    error: goodsEntryError,
    isFetching: goodsEntryLoading,
  } = useQuery({
    queryKey: [
      'ap-goods-invoice-entry',
      currentTenantCode,
      selectedPurchaseOrderId,
      editInvoiceId,
    ],
    queryFn: () =>
      accountsPayableService.getGoodsInvoiceEntry(
        selectedPurchaseOrderId,
        editInvoiceId
      ),
    enabled: Boolean(selectedPurchaseOrderId && goodsCategory),
  });
  const { data: acceptedSupplyOptions, isLoading: acceptedSupplyLoading } =
    useQuery({
      queryKey: ['ap-accepted-supply-options', selectedPurchaseOrderId],
      queryFn: () =>
        accountsPayableService.getAcceptedSupplyOptions(
          selectedPurchaseOrderId
        ),
      enabled: Boolean(selectedPurchaseOrderId && serviceCategory),
    });

  useEffect(() => {
    paymentTermService
      .getByApplicableTo('Supplier')
      .then((terms) => setPaymentTerms(terms || []))
      .catch(() => setPaymentTerms([]));
  }, []);

  // Filtering logic
  const filteredSuppliers =
    suppliersData?.items?.filter((supplier: any) => {
      if (!supplierSearch) return true;
      const search = supplierSearch.toLowerCase();
      return (
        supplier.name?.toLowerCase().includes(search) ||
        supplier.code?.toLowerCase().includes(search)
      );
    }) || [];

  const filteredGlAccounts =
    glAccountsData?.items?.filter((account: any) => {
      if (!glAccountSearch) return true;
      const search = glAccountSearch.toLowerCase();
      return (
        account.accountCode?.toLowerCase().includes(search) ||
        account.accountName?.toLowerCase().includes(search)
      );
    }) || [];

  const filteredInventoryItems =
    inventoryItemsData?.filter((item: any) => {
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
      invoiceDate: defaultInvoiceDate,
      dueDate: defaultDueDate,
      paymentTermId: prefilledPaymentTermId,
      apAccountId: '',
      expenseAccountId: '',
      currencyCode: prefilledCurrencyCode || 'GHS',
      exchangeRate: 1.0,
      exchangeRateId: undefined,
      exchangeRateDate: new Date(),
      exchangeRateSource: 'Daily',
      isOpeningBalance: defaultOpeningBalance,
      withholdingTaxId: 'none',
      withholdingTaxRate: 0,
      notes: prefilledNotes,
      lineItems: [
        {
          sourceLineId: crypto.randomUUID(),
          lineItemType: 'Expense',
          description: prefilledLineDescription,
          quantity: Number.isFinite(prefilledQuantity) && prefilledQuantity > 0 ? prefilledQuantity : 1,
          unitPrice: Number.isFinite(prefilledUnitPrice) && prefilledUnitPrice > 0 ? prefilledUnitPrice : 0,
          discountPercentage: 0,
          taxGroupId: 'none',
        },
      ],
    },
  });

  const watchInvoiceDate = form.watch('invoiceDate');
  useEffect(() => {
    const subscription = form.watch((_values, event) => {
      if (event.type !== 'change' || !event.name) return;
      if (
        [
          'paymentTermId',
          'dueDate',
          'apAccountId',
          'expenseAccountId',
          'taxGroupId',
        ].includes(event.name)
      )
        manualSupplierDefaults.current.add(event.name);
      const line =
        /^lineItems\.(\d+)\.(taxGroupId|taxTreatment|glAccountId)$/.exec(
          event.name
        );
      if (line)
        manualSupplierDefaults.current.add(
          `${form.getValues(`lineItems.${Number(line[1])}.sourceLineId`)}:${line[2]}`
        );
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
  const withholdingTaxOptions = (withholdingTaxes as Tax[]).filter(
    (tax) =>
      tax.isActive &&
      tax.category === TaxCategory.Withholding &&
      (tax.applicability === TaxApplicability.Purchases ||
        tax.applicability === TaxApplicability.Both)
  );
  const selectedWithholdingTax = withholdingTaxOptions.find(
    (tax) => tax.id === watchWithholdingTaxId
  );
  const watchWithholdingTaxRate =
    watchIsOpeningBalance || withholdingDecision !== true
      ? 0
      : Number(form.watch('withholdingTaxRate') || 0);
  const watchLineItems = form.watch('lineItems') || [];
  const invoiceDiscountRates = new Set(
    watchLineItems.map((line) => Number(line.discountPercentage) || 0)
  );
  const invoiceDiscountRate =
    invoiceDiscountRates.size === 1
      ? Array.from(invoiceDiscountRates)[0]
      : undefined;
  const invoiceDiscountLocked = estateSourceLocked || Boolean(editInvoice?.lineItems.some(line => line.landedCostItemId));
  const invoiceTaxChoices = new Set(
    watchLineItems.map((line) => {
      const group = line.taxGroupId || watchTaxGroupId;
      const treatment = Number(
        line.taxTreatment ?? (group && group !== 'none' ? 1 : 4)
      );
      return treatment === 1 ? '1|' + (group || 'none') : String(treatment);
    })
  );
  const invoiceTaxChoice =
    invoiceTaxChoices.size === 1 ? Array.from(invoiceTaxChoices)[0] : 'mixed';
  const applyInvoiceDiscount = (value: string) => {
    if (invoiceDiscountLocked) return;
    const percentage = Number(value);
    if (!Number.isFinite(percentage) || percentage < 0 || percentage > 100)
      return;
    form.getValues('lineItems').forEach((_line, index) => {
      form.setValue(`lineItems.${index}.discountPercentage`, percentage, {
        shouldDirty: true,
        shouldValidate: true,
      });
    });
  };
  const applyInvoiceTax = (value: string) => {
    if (value === 'mixed') return;
    const [treatment, group = 'none'] = value.split('|');
    manualSupplierDefaults.current.add('taxGroupId');
    form.setValue('taxGroupId', group, { shouldDirty: true });
    form.getValues('lineItems').forEach((line, index) => {
      manualSupplierDefaults.current.add(line.sourceLineId + ':taxGroupId');
      manualSupplierDefaults.current.add(line.sourceLineId + ':taxTreatment');
      form.setValue(`lineItems.${index}.taxTreatment`, Number(treatment), {
        shouldDirty: true,
      });
      form.setValue(`lineItems.${index}.taxGroupId`, group, {
        shouldDirty: true,
      });
    });
  };
  const addInvoiceLine = () => {
    const [treatment, group = 'none'] =
      invoiceTaxChoice === 'mixed'
        ? ['5', 'none']
        : invoiceTaxChoice.split('|');
    append({
      sourceLineId: crypto.randomUUID(),
      lineItemType: 'Expense',
      description: '',
      quantity: 1,
      unitPrice: 0,
      discountPercentage: invoiceDiscountRate ?? 0,
      taxTreatment: Number(treatment),
      taxGroupId: group,
    });
  };
  // React Hook Form may retain the array identity when a Controller changes
  // one line's type. Reapply untouched defaults when the relevant values change.
  const supplierDefaultLineInputs = JSON.stringify(
    watchLineItems.map((line) => [
      line.sourceLineId,
      line.lineItemType,
      line.glAccountId,
      line.taxGroupId,
      line.taxTreatment,
    ])
  );
  const {
    data: supplierDefaults,
    isFetching: supplierDefaultsLoading,
    error: supplierDefaultsError,
  } = useQuery({
    queryKey: [
      'ap-invoice-supplier-defaults',
      currentTenantCode,
      watchSupplierId,
      selectedSupplier?.businessPartnerRoleId,
      selectedPurchaseOrderId,
      watchInvoiceDateTime,
    ],
    queryFn: () =>
      accountsPayableService.getInvoiceSupplierDefaults(
        watchSupplierId,
        selectedPurchaseOrderId || undefined,
        watchInvoiceDate ? format(watchInvoiceDate, 'yyyy-MM-dd') : undefined,
        selectedSupplier?.businessPartnerRoleId
      ),
    enabled: Boolean(watchSupplierId) && !watchIsOpeningBalance,
  });
  const supplierWithholding = supplierDefaults?.withholdingDefault;
  const withholdingAccountId =
    watchIsOpeningBalance || withholdingDecision !== true
      ? null
      : isEditMode &&
          editInvoice?.withholdingTaxId === watchWithholdingTaxId &&
          !manualSupplierDefaults.current.has('withholdingTaxId')
        ? editInvoice.withholdingTaxAccountId || null
        : supplierWithholding?.taxId === watchWithholdingTaxId
          ? supplierWithholding.taxPayableAccountId || null
          : selectedWithholdingTax?.taxPayableAccountId || null;

  const declineWithholding = () => {
    setWithholdingDecision(false);
    setWithholdingRateOverride(null);
    form.setValue('withholdingTaxId', 'none');
    form.setValue('withholdingTaxRate', 0);
  };
  const acceptWithholding = () => {
    setWithholdingDecision(true);
    setWithholdingRateOverride(null);
    if (supplierWithholding?.taxId) {
      form.setValue('withholdingTaxId', supplierWithholding.taxId);
      form.setValue('withholdingTaxRate', supplierWithholding.rate);
      manualSupplierDefaults.current.delete('withholdingTaxId');
    }
    return true;
  };
  useEffect(() => {
    if (
      watchIsOpeningBalance ||
      !watchSupplierId ||
      !supplierWithholding?.required ||
      withholdingDecision !== null
    )
      return;
    const key = `${watchSupplierId}:${editInvoiceId || 'new'}`;
    if (withholdingPromptKey.current === key) return;
    withholdingPromptKey.current = key;
    setWithholdingPromptOpen(true);
  }, [
    watchIsOpeningBalance,
    watchSupplierId,
    supplierWithholding?.required,
    withholdingDecision,
    editInvoiceId,
  ]);
  useEffect(() => {
    if (
      isEditMode ||
      watchIsOpeningBalance ||
      withholdingDecision !== true ||
      withholdingRateOverride !== null ||
      manualSupplierDefaults.current.has('withholdingTaxId')
    )
      return;
    if (
      supplierWithholding?.taxId &&
      supplierWithholding.taxId === watchWithholdingTaxId &&
      Number(form.getValues('withholdingTaxRate')) !== supplierWithholding.rate
    )
      form.setValue('withholdingTaxRate', supplierWithholding.rate);
  }, [
    isEditMode,
    watchIsOpeningBalance,
    withholdingDecision,
    withholdingRateOverride,
    supplierWithholding,
    watchWithholdingTaxId,
    form,
  ]);

  const loadBudgetCells = async (
    lineKey: string,
    index: number,
    accountId: string,
    preserveBudgetEntryId?: string
  ) => {
    if (!preserveBudgetEntryId) {
      form.setValue(`lineItems.${index}.budgetEntryId`, undefined);
    }
    setBudgetCellsByLine((current) => ({ ...current, [lineKey]: [] }));
    if (!accountId || !watchInvoiceDate) return;
    setBudgetCellsLoading((current) => ({ ...current, [lineKey]: true }));
    try {
      const cells = await accountsPayableService.getInvoiceBudgetCells(
        format(watchInvoiceDate, 'yyyy-MM-dd'),
        accountId
      );
      setBudgetCellsByLine((current) => ({ ...current, [lineKey]: cells }));
      if (preserveBudgetEntryId) {
        form.setValue(
          `lineItems.${index}.budgetEntryId`,
          preserveBudgetEntryId
        );
      }
    } catch (error: any) {
      toast({
        title: 'Budget cells unavailable',
        description:
          error.message ||
          'Unable to load adopted Finance budget cells for this account.',
        variant: 'destructive',
      });
    } finally {
      setBudgetCellsLoading((current) => ({ ...current, [lineKey]: false }));
    }
  };

  useEffect(() => {
    if (
      !isEditMode ||
      !editInvoice ||
      !suppliersData?.items ||
      editHydratedRef.current
    )
      return;

    const supplier = suppliersData.items.find(
      (item) => item.businessPartnerId === editInvoice.businessPartnerId && (!editInvoice.businessPartnerRoleId || item.businessPartnerRoleId === editInvoice.businessPartnerRoleId)
    );
    if (!supplier) return;

    editHydratedRef.current = true;
    editBudgetCellsLoadedRef.current.clear();
    setSelectedSupplier(supplier);
    setSelectedPurchaseOrderId(editInvoice.purchaseOrderId || '');
    suppressPurchaseOrderHydrationRef.current = Boolean(
      editInvoice.purchaseOrderId
    );
    const dimensionState = toFinanceSourceDimensionFormState(
      editInvoice.financeDimensions
    );
    setDefaultDimensionValues(dimensionState.defaultValues);
    setLineDimensionValues(dimensionState.lineValues);
    setApplyDefaultToAll(false);

    const invoiceDate = new Date(editInvoice.invoiceDate);
    setWithholdingDecision(
      editInvoice.applySupplierWithholdingDefaults ??
        (editInvoice.withholdingTaxId ? true : null)
    );
    setWithholdingRateOverride(editInvoice.withholdingTaxRateOverride ?? null);
    const dueDate = editInvoice.dueDate
      ? new Date(editInvoice.dueDate)
      : addDays(invoiceDate, editInvoice.paymentTermsDays || 30);

    form.reset({
      supplierId: editInvoice.businessPartnerId,
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
      exchangeRateSource: editInvoice.exchangeRateId
        ? 'Approved rate'
        : 'Daily',
      notes: editInvoice.notes || '',
      reference: editInvoice.reference || '',
      taxGroupId: 'none',
      withholdingTaxId: editInvoice.withholdingTaxId || 'none',
      withholdingTaxRate: editInvoice.withholdingTaxRate || 0,
      isOpeningBalance: editInvoice.isOpeningBalance,
      lineItems: editInvoice.lineItems.map((line) => ({
        sourceLineId: line.id,
        lineItemType: (line.lineItemType || 'Expense') as
          | 'Expense'
          | 'Service'
          | 'Product'
          | 'Inventory'
          | 'Freight'
          | 'Miscellaneous'
          | 'FinanceCharge',
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
    if (!isEditMode || !editInvoice || !editHydratedRef.current) return;
    if (fields.length !== editInvoice.lineItems.length) return;

    editInvoice.lineItems.forEach((line, index) => {
      if (
        line.glAccountId &&
        !editBudgetCellsLoadedRef.current.has(fields[index].id)
      ) {
        editBudgetCellsLoadedRef.current.add(fields[index].id);
        void loadBudgetCells(
          fields[index].id,
          index,
          line.glAccountId,
          line.budgetEntryId
        );
      }
    });
  }, [editInvoice, fields, isEditMode]);

  const applyInvoiceExchangeRate = async (currencyCode: string) => {
    const requestId = ++exchangeRateRequestId.current;
    const functionalCurrency = financeSettings?.baseCurrency || 'GHS';
    form.setValue('exchangeRateId', undefined);
    if (!financeSettings) {
      throw new Error(
        'Finance settings are still loading. Try again before saving this invoice.'
      );
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
    form.setValue('exchangeRateDate', form.getValues('invoiceDate'));
    form.setValue('exchangeRateSource', snapshot.source);
  };

  useEffect(() => {
    if (!financeSettings || !watchInvoiceDate) return;
    void applyInvoiceExchangeRate(watchCurrencyCode).catch((error) => {
      console.error('Failed to resolve governed AP invoice rate', error);
      form.setValue('exchangeRateId', undefined);
      form.setValue('exchangeRate', 0);
      form.setValue('exchangeRateSource', 'Unavailable');
    });
  }, [financeSettings, watchCurrencyCode, watchInvoiceDate]);

  useEffect(() => {
    if (!watchIsOpeningBalance) return;

    // Opening bills bring forward gross AP balances only; tax and WHT history is not
    // reposted through the migration clearing entry created by the posting service.
    form.setValue('taxGroupId', 'none');
    form.setValue('withholdingTaxId', 'none');
    form.setValue('withholdingTaxRate', 0);
    setWithholdingDecision(false);
    setWithholdingRateOverride(null);
    setWithholdingPromptOpen(false);
    form.getValues('lineItems').forEach((_, index) => {
      form.setValue(`lineItems.${index}.taxGroupId`, 'none');
    });
  }, [form, watchIsOpeningBalance]);

  const subtotal = watchLineItems.reduce((acc, item) => {
    const qty = Number(item.quantity) || 0;
    const price = Number(item.unitPrice) || 0;
    const discount = Number(item.discountPercentage) || 0;
    return acc + calculateNetTradeDiscountLineAmount(qty * price, discount);
  }, 0);

  const grossSubtotal = watchLineItems.reduce(
    (sum, item) =>
      sum +
      calculateNetTradeDiscountLineAmount(
        (Number(item.quantity) || 0) * (Number(item.unitPrice) || 0),
        0
      ),
    0
  );
  const invoiceTradeDiscount = Math.max(0, grossSubtotal - subtotal);
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
    const lineSubtotal = calculateNetTradeDiscountLineAmount(
      qty * price,
      discount
    );
    const activeGroupId = resolveLineTaxGroupId(
      item,
      isOpeningBalance,
      headerTaxGroupId
    );
    const activeGroup = taxGroupsData?.find((tg) => tg.id === activeGroupId);

    if (!activeGroup || !activeGroup.components || lineSubtotal <= 0) {
      return { taxAmount: 0, taxRate: 0 };
    }

    let lineTaxAmount = 0;
    let cumulativeBase = lineSubtotal;
    const sortedComponents = [...activeGroup.components].sort(
      (a, b) => a.calculationOrder - b.calculationOrder
    );

    sortedComponents.forEach((comp) => {
      if (comp.taxCategory === 'Withholding') return;

      const taxableBasis =
        comp.compoundBasis === 'Cumulative' ? cumulativeBase : lineSubtotal;
      const taxAmt = taxableBasis * (Number(comp.taxRate) / 100);
      lineTaxAmount += taxAmt;

      if (
        comp.compoundBasis === 'Cumulative' ||
        comp.compoundBasis === 'BaseOnly'
      ) {
        cumulativeBase += taxAmt;
      }
    });

    return {
      taxAmount: lineTaxAmount,
      taxRate: (lineTaxAmount / lineSubtotal) * 100,
    };
  };

  const getTaxBreakdown = () => {
    if (watchIsOpeningBalance) {
      return {
        totalTaxAmount: 0,
        withholdingTaxAmount: 0,
        grandTotal: subtotal,
        estimatedCashPayable: subtotal,
        taxList: [],
      };
    }

    let totalTaxAmount = 0;
    const breakdowns: {
      [taxCode: string]: { name: string; rate: number; amount: number };
    } = {};

    watchLineItems.forEach((item) => {
      const qty = Number(item.quantity) || 0;
      const price = Number(item.unitPrice) || 0;
      const discount = Number(item.discountPercentage) || 0;
      const lineSubtotal = calculateNetTradeDiscountLineAmount(
        qty * price,
        discount
      );

      // Resolve line tax group or fallback to header
      const activeGroupId = resolveLineTaxGroupId(item);
      const activeGroup = taxGroupsData?.find((tg) => tg.id === activeGroupId);

      if (activeGroup && activeGroup.components) {
        let cumulativeBase = lineSubtotal;
        const sortedComponents = [...activeGroup.components].sort(
          (a, b) => a.calculationOrder - b.calculationOrder
        );

        sortedComponents.forEach((comp) => {
          if (comp.taxCategory === 'Withholding') return; // standard levies/vat only

          let taxableBasis = lineSubtotal;
          if (comp.compoundBasis === 'Cumulative') {
            taxableBasis = cumulativeBase;
          }

          const taxAmt = taxableBasis * (Number(comp.taxRate) / 100);
          totalTaxAmount += taxAmt;

          if (
            comp.compoundBasis === 'Cumulative' ||
            comp.compoundBasis === 'BaseOnly'
          ) {
            cumulativeBase += taxAmt;
          }

          if (breakdowns[comp.taxCode]) {
            breakdowns[comp.taxCode].amount += taxAmt;
          } else {
            breakdowns[comp.taxCode] = {
              name: comp.taxName,
              rate: comp.taxRate,
              amount: taxAmt,
            };
          }
        });
      }
    });

    // Compute separate withholding tax deduction based on withholdingTaxRate
    const withholdingTaxAmount =
      Math.round(
        (subtotal * (watchWithholdingTaxRate / 100) + Number.EPSILON) * 100
      ) / 100;
    const grandTotal = subtotal + totalTaxAmount; // subtotal + standard taxes
    const estimatedCashPayable = grandTotal - withholdingTaxAmount;

    return {
      totalTaxAmount,
      withholdingTaxAmount,
      grandTotal,
      estimatedCashPayable,
      taxList: Object.entries(breakdowns).map(([code, data]) => ({
        code,
        ...data,
      })),
    };
  };

  const taxEstimate = getTaxBreakdown();
  const totalTax = taxEstimate.totalTaxAmount;
  const totalAmount = taxEstimate.grandTotal;

  const formatAmountWithCurrency = (amount: number) => {
    return formatCurrency(amount, watchCurrencyCode);
  };

  const onSupplierChange = async (
    selectionId: string,
    preservePurchaseOrderId?: string
  ) => {
    const supplier = suppliersData?.items.find(item => item.businessPartnerRoleId === selectionId || item.businessPartnerId === selectionId);
    if (!supplier || !supplier.isTransactionReady) return;
    const supplierId = supplier.businessPartnerId;
    const selectionKey = `${supplier.businessPartnerRoleId}:${preservePurchaseOrderId || ''}`;
    if (supplierSelectionRef.current === selectionKey) return;
    if (!isEditMode && form.getValues('supplierId') !== supplierId) {
      setWithholdingDecision(null);
      setWithholdingRateOverride(null);
      setWithholdingPromptOpen(false);
      withholdingPromptKey.current = '';
      manualSupplierDefaults.current.delete('withholdingTaxId');
      form.setValue('withholdingTaxId', 'none');
      form.setValue('withholdingTaxRate', 0);
    }
    supplierSelectionRef.current = selectionKey;
    hydratedPurchaseOrderIdRef.current = '';
    form.setValue('supplierId', supplierId);
    form.setValue('purchaseOrderId', preservePurchaseOrderId || undefined);
    form.setValue('acceptedSupplyKind', undefined);
    form.setValue('acceptedSupplySourceId', undefined);
    setSelectedPurchaseOrderId(preservePurchaseOrderId || '');
    if (!suppliersData?.items) return;

    if (supplier) {
      setSelectedSupplier(supplier);
      const selectedTerm =
        paymentTerms.find((term) => term.id === supplier.paymentTermId) ||
        paymentTerms.find((term) => term.isDefault);
      if (
        selectedTerm &&
        !manualSupplierDefaults.current.has('paymentTermId')
      ) {
        form.setValue('paymentTermId', selectedTerm.id);
        form.setValue(
          'dueDate',
          addDays(form.getValues('invoiceDate'), selectedTerm.dueDays)
        );
      }
      if (supplier.currency) {
        form.setValue('currencyCode', supplier.currency);
        try {
          await applyInvoiceExchangeRate(supplier.currency);
        } catch (err) {
          console.error(
            'Failed to fetch exchange rate for supplier currency',
            err
          );
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
      suppressPurchaseOrderHydrationRef.current = false;
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
        description:
          'Works invoices are created from an approved QS payment certificate.',
        variant: 'destructive',
      });
      onPurchaseOrderChange('none');
      return;
    }
    form.setValue('currencyCode', selectedPurchaseOrder.currency || 'GHS');
    form.setValue('exchangeRate', 1);
    const sourceItems = goodsCategory
      ? receiptBasedInvoiceLines(selectedPurchaseOrder.items, goodsEntry)
      : selectedPurchaseOrder.items.map((item) => ({
          ...item,
          invoiceQuantity:
            item.remainingQuantity > 0
              ? item.remainingQuantity
              : item.orderedQuantity,
        }));
    form.setValue(
      'lineItems',
      sourceItems.map((item) => ({
        sourceLineId: crypto.randomUUID(),
        lineItemType:
          selectedPurchaseOrder.procurementCategory === 'Goods'
            ? 'Inventory'
            : 'Expense',
        inventoryItemId: item.inventoryItemId || undefined,
        warehouseId: item.warehouseId || undefined,
        purchaseOrderItemId: item.id,
        description: item.itemDescription || item.itemName || item.itemCode,
        quantity: item.invoiceQuantity,
        unitPrice: item.unitPrice,
        discountPercentage: 0,
        taxGroupId: 'none',
        unit: item.unitOfMeasure,
      }))
    );
    hydratedPurchaseOrderIdRef.current = selectedPurchaseOrder.id;
    if (selectedPurchaseOrder.procurementCategory === 'Goods')
      form.setValue('acceptedSupplyKind', undefined);
    else form.setValue('acceptedSupplyKind', 'ServiceCompletion');
  }, [form, selectedPurchaseOrder, toast, goodsCategory, goodsEntry]);

  useEffect(() => {
    if (
      !applySupplierDefaults ||
      !supplierDefaults ||
      isEditMode ||
      watchIsOpeningBalance
    )
      return;
    // The server returns the saved PO snapshot when available; we do not silently refetch
    // today's business-partner tax/rate and recalculate an existing invoice.
    const plan = planApSupplierDefaults(
      form.getValues(),
      supplierDefaults,
      manualSupplierDefaults.current,
      new Set((taxGroupsData || []).map((group) => group.id))
    );
    for (const assignment of plan.assignments) {
      form.setValue(
        assignment.field as FieldPath<InvoiceFormValues>,
        assignment.value,
        { shouldDirty: false }
      );
    }
    if (
      !manualSupplierDefaults.current.has('paymentTermId') &&
      !manualSupplierDefaults.current.has('dueDate') &&
      supplierDefaults.paymentTermId &&
      form.getValues('paymentTermId') === supplierDefaults.paymentTermId
    ) {
      const dueDays =
        supplierDefaults.paymentTermsDays ??
        paymentTerms.find((term) => term.id === supplierDefaults.paymentTermId)
          ?.dueDays;
      if (dueDays != null) {
        const dueDate = addDays(form.getValues('invoiceDate'), dueDays);
        if (form.getValues('dueDate')?.getTime() !== dueDate.getTime())
          form.setValue('dueDate', dueDate);
      }
    }
  }, [
    applySupplierDefaults,
    supplierDefaults,
    isEditMode,
    watchIsOpeningBalance,
    taxGroupsData,
    paymentTerms,
    form,
    supplierDefaultLineInputs,
    watchTaxGroupId,
    watchApAccountId,
    watchExpenseAccountId,
    watchInvoiceDateTime,
  ]);

  useEffect(() => {
    if (preselectedSupplierId && suppliersData?.items) {
      onSupplierChange(
        preselectedSupplierId,
        preselectedPurchaseOrderId || undefined
      );
    }
  }, [
    preselectedSupplierId,
    preselectedPurchaseOrderId,
    suppliersData,
    paymentTerms,
  ]);

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
          toast({
            title: 'Accepted receipt quantities unavailable',
            description:
              'Wait for the accepted-receipt check before recording this invoice.',
            variant: 'destructive',
          });
          return;
        }
        const totals = new Map<string, number>();
        for (const line of data.lineItems)
          totals.set(
            line.purchaseOrderItemId || '',
            (totals.get(line.purchaseOrderItemId || '') || 0) +
              Number(line.quantity)
          );
        if (
          [...totals].some(
            ([id, quantity]) =>
              quantity >
              (goodsEntry.lines.find((line) => line.purchaseOrderItemId === id)
                ?.availableQuantity || 0)
          )
        ) {
          toast({
            title: 'Receipt quantity exceeded',
            description:
              'Use only accepted quantities not already invoiced. Rejected and pending quantities cannot be billed.',
            variant: 'destructive',
          });
          return;
        }
      }
      const isOpeningBalance = data.isOpeningBalance;
      if (!isEditMode && !data.purchaseOrderId) {
        form.setError('purchaseOrderId', {
          message:
            'Select a purchase order, or use Auto Invoice to select accepted receipts.',
        });
        throw new Error(
          'Select a purchase order, or use Auto Invoice to select accepted receipts.'
        );
      }
      if (serviceCategory && !data.acceptedSupplySourceId) {
        toast({
          title: 'Service completion required',
          description:
            'Select an approved service completion before recording this invoice.',
          variant: 'destructive',
        });
        return;
      }
      if (
        !isOpeningBalance &&
        supplierWithholding?.required &&
        withholdingDecision === null
      ) {
        setWithholdingPromptOpen(true);
        toast({
          title: 'Choose withholding for this invoice',
          description: 'Select Yes or No before saving.',
          variant: 'destructive',
        });
        return;
      }
      if (
        !isOpeningBalance &&
        withholdingDecision === true &&
        (!data.withholdingTaxId ||
          data.withholdingTaxId === 'none' ||
          !withholdingAccountId)
      ) {
        toast({
          title: 'WHT configuration required',
          description:
            supplierWithholding?.message ||
            'Select an applicable WHT configuration with a payable account.',
          variant: 'destructive',
        });
        return;
      }
      const functionalCurrency = financeSettings?.baseCurrency || 'GHS';
      if (data.currencyCode !== functionalCurrency && !data.exchangeRateId) {
        toast({
          title: 'Approved exchange rate required',
          description:
            'Select a currency and invoice date with an active approved Daily rate before creating this invoice.',
          variant: 'destructive',
        });
        return;
      }
      const defaultsPlan = supplierDefaults
        ? planApSupplierDefaults(
            data,
            supplierDefaults,
            manualSupplierDefaults.current,
            new Set((taxGroupsData || []).map((group) => group.id))
          )
        : null;
      const { supplierId, ...invoiceData } = data;
      const request = {
        ...invoiceData,
        businessPartnerId: supplierId,
        businessPartnerRoleId: selectedSupplier?.businessPartnerRoleId,
        apAccountId: data.apAccountId || undefined,
        expenseAccountId: data.expenseAccountId || undefined,
        paymentTermsDays:
          !isOpeningBalance &&
          applySupplierDefaults &&
          supplierDefaults?.paymentTermId === data.paymentTermId &&
          !manualSupplierDefaults.current.has('paymentTermId') &&
          !manualSupplierDefaults.current.has('dueDate')
            ? (supplierDefaults?.paymentTermsDays ?? undefined)
            : undefined,
        applyBusinessPartnerDefaults:
          !isEditMode &&
          !isOpeningBalance &&
          applySupplierDefaults &&
          defaultsPlan?.allowServerDefaults === true,
        applySupplierWithholdingDefaults: isOpeningBalance
          ? false
          : withholdingDecision,
        withholdingTaxRateOverride:
          isOpeningBalance || withholdingDecision !== true
            ? null
            : withholdingRateOverride,
        invoiceDate: data.invoiceDate.toISOString(),
        dueDate: data.dueDate.toISOString(),
        taxGroupId:
          isOpeningBalance || data.taxGroupId === 'none'
            ? null
            : data.taxGroupId || null,
        exchangeRate: Number(data.exchangeRate) || 1.0,
        exchangeRateId: data.exchangeRateId,
        // The backend resolves rate/account again from this tax id. Sending the displayed
        // values keeps the compatibility DTO descriptive but grants them no authority.
        withholdingTaxId:
          isOpeningBalance ||
          withholdingDecision !== true ||
          data.withholdingTaxId === 'none'
            ? null
            : data.withholdingTaxId,
        withholdingTaxRate: isOpeningBalance ? 0 : watchWithholdingTaxRate,
        withholdingTaxAccountId: withholdingAccountId,
        isOpeningBalance,
        acceptedSupplyKind: serviceCategory
          ? ('ServiceCompletion' as const)
          : data.acceptedSupplyKind,
        acceptedSupplySourceId: data.acceptedSupplySourceId,
        lineItems: data.lineItems.map((item) => {
          const lineTax = calculateLineTax(
            item,
            isOpeningBalance,
            data.taxGroupId
          );
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
            taxGroupId: resolveLineTaxGroupId(
              item,
              isOpeningBalance,
              data.taxGroupId
            ),
            unit: item.unit || null,
            inventoryItemId: item.inventoryItemId || null,
            warehouseId: item.warehouseId || null,
          } as any;
        }),
        financeDimensions: {
          defaultDimensions: toFinancePostingDimensionValues(
            defaultDimensionValues
          ),
          lines: data.lineItems.flatMap((item) => {
            const editableAccountId =
              !isOpeningBalance &&
              !data.purchaseOrderId &&
              isApExpenseLineType(item.lineItemType)
                ? item.glAccountId
                : undefined;
            const { accountId } = getSourceLineDimensionAccounts(
              isOpeningBalance ? undefined : editInvoice?.financeDimensions,
              item.sourceLineId,
              editableAccountId
            );
            return accountId
              ? [
                  {
                    sourceLineId: item.sourceLineId,
                    accountId,
                    dimensions: toFinancePostingDimensionValues(
                      lineDimensionValues[item.sourceLineId] || {}
                    ),
                  },
                ]
              : [];
          }),
          applyDefaultToEligibleLines: applyDefaultToAll,
        },
      };

      const savedInvoice = isEditMode && editInvoice
        ? await accountsPayableService.updateInvoice(editInvoice.id, {
          ...request,
          id: editInvoice.id,
          receivedDate: editInvoice.receivedDate,
          paymentTermsDays: editInvoice.paymentTermsDays,
          earlyPaymentDiscountPercentage:
            editInvoice.earlyPaymentDiscountPercentage,
          earlyPaymentDiscountDueDate: editInvoice.earlyPaymentDiscountDueDate,
          withholdingCertificateNumber:
            editInvoice.withholdingCertificateNumber,
          withholdingCertificateDate: editInvoice.withholdingCertificateDate,
          matchingType: editInvoice.matchingType,
          expenseAccountId:
            data.expenseAccountId || editInvoice.expenseAccountId,
          apAccountId: data.apAccountId || editInvoice.apAccountId,
        })
        : await accountsPayableService.createInvoice(request);

      await cacheSavedSupplierInvoice(queryClient, savedInvoice);

      toast({
        title: 'Success',
        description: isEditMode
          ? 'Vendor invoice updated successfully'
          : 'Vendor invoice created successfully',
      });

      router.push(
        isEditMode && editInvoice
          ? `/procurement/supplier-invoices/${editInvoice.id}`
          : '/procurement/supplier-invoices'
      );
    } catch (error: any) {
      toast({
        title: 'Error',
        description:
          error.message ||
          `Failed to ${isEditMode ? 'update' : 'create'} vendor invoice`,
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
      !suppliersData.items.some((item) => item.businessPartnerId === editInvoice.businessPartnerId && (!editInvoice.businessPartnerRoleId || item.businessPartnerRoleId === editInvoice.businessPartnerRoleId))
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

  if (
    isEditMode &&
    (editInvoiceError || suppliersError || editSupplierMissing)
  ) {
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
        <Button
          variant="outline"
          onClick={() => router.push('/procurement/supplier-invoices')}
        >
          Back to invoices
        </Button>
      </div>
    );
  }

  if (
    isEditMode &&
    editInvoice &&
    !['Draft', 'Rejected'].includes(editInvoice.status)
  ) {
    return (
      <div className="mx-auto max-w-2xl space-y-4 p-8">
        <h1 className="text-2xl font-bold">Invoice can no longer be edited</h1>
        <p className="text-muted-foreground">
          Only Draft or Rejected invoices may be changed.
        </p>
        <Button
          variant="outline"
          onClick={() =>
            router.push(`/procurement/supplier-invoices/${editInvoice.id}`)
          }
        >
          View invoice
        </Button>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-[1500px] space-y-5 p-4 sm:p-6">
      <div className="flex flex-wrap items-center gap-3">
        <Button
          variant="outline"
          size="icon"
          aria-label="Back to supplier invoice"
          className="shrink-0"
          onClick={() => router.back()}
        >
          <ArrowLeft className="h-4 w-4" />
        </Button>
        <div>
          <p className="mb-1 text-xs font-medium uppercase tracking-wider text-muted-foreground">
            Procurement / Supplier invoices
          </p>
          <h1 className="text-2xl font-semibold tracking-tight">
            {isEditMode
              ? `Edit ${editInvoice?.invoiceNumber || 'Supplier Invoice'}`
              : 'Record Supplier Invoice'}
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            {isEditMode
              ? 'Review the invoice details, charges and tax before saving.'
              : 'Enter the supplier details and invoice charges.'}
          </p>
        </div>
        {isEditMode && editInvoice && (
          <div className="ml-auto flex max-w-xs flex-col items-end gap-1.5">
            <ProcurementInvoiceDistribution invoiceId={editInvoice.id} />
            <p className="text-right text-xs text-muted-foreground">
              Save invoice changes first to include them in the distribution.
            </p>
          </div>
        )}
      </div>

      <form onSubmit={form.handleSubmit(onSubmit as any, (errors) => {
        toast({ title: 'Invoice not saved', description: getInvoiceValidationMessages(errors).join('; '), variant: 'destructive' });
      })} className="space-y-5">
        <EstateInvoiceSource invoice={editInvoice} />
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <FileText className="h-4 w-4 text-muted-foreground" />
              Invoice details
            </CardTitle>
          </CardHeader>
          <CardContent className="grid gap-x-5 gap-y-4 sm:grid-cols-2 lg:grid-cols-4">
            <div className="min-w-0 space-y-2 sm:col-span-2">
              <Label htmlFor="supplier">Supplier</Label>
              <Popover
                open={supplierComboOpen}
                onOpenChange={setSupplierComboOpen}
              >
                <PopoverTrigger asChild>
                  <Button
                    variant="outline"
                    role="combobox"
                    id="supplier"
                    aria-label="Supplier"
                    aria-expanded={supplierComboOpen}
                    className="w-full min-w-0 justify-between overflow-hidden disabled:opacity-100 disabled:text-foreground disabled:bg-muted/40"
                    disabled={isEditMode}
                  >
                    <span className="min-w-0 flex-1 truncate text-left">
                      {selectedSupplier
                        ? `${selectedSupplier.name} (${selectedSupplier.code})`
                        : suppliersLoading
                          ? 'Loading suppliers...'
                          : 'Select a supplier...'}
                    </span>
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
                        {suppliersLoading ? 'Loading...' : 'No supplier found.'}
                      </CommandEmpty>
                      <CommandGroup>
                        {filteredSuppliers.map((supplier: any) => (
                          <div
                            key={supplier.businessPartnerRoleId}
                            onClick={() => {
                              if (!supplier.isTransactionReady) return;
                              onSupplierChange(supplier.businessPartnerRoleId);
                              setSupplierComboOpen(false);
                              setSupplierSearch('');
                            }}
                            aria-disabled={!supplier.isTransactionReady}
                            title={supplier.isTransactionReady ? undefined : supplier.readinessMessage}
                            className="relative flex cursor-pointer select-none items-center rounded-sm px-2 py-1.5 text-sm outline-none hover:bg-accent hover:text-accent-foreground"
                          >
                            <Check
                              className={cn(
                                'mr-2 h-4 w-4',
                                selectedSupplier?.businessPartnerRoleId === supplier.businessPartnerRoleId
                                  ? 'opacity-100'
                                  : 'opacity-0'
                              )}
                            />
                            <div className="flex flex-col">
                              <span className="font-medium">
                                {supplier.name}
                              </span>
                              <span className="text-xs text-muted-foreground">
                                {supplier.code}{supplier.isTransactionReady ? '' : ` · ${supplier.readinessMessage}`}
                              </span>
                            </div>
                          </div>
                        ))}
                      </CommandGroup>
                    </CommandList>
                  </Command>
                </PopoverContent>
              </Popover>
              {form.formState.errors.supplierId && (
                <p className="text-sm text-red-500">
                  {form.formState.errors.supplierId.message}
                </p>
              )}
            </div>

            {!watchIsOpeningBalance &&
              selectedSupplier &&
              !estateSourceLocked && (
                <div className="space-y-2">
                  <Label>Purchase order</Label>
                  <Select
                    disabled={receiptSourceLocked}
                    value={selectedPurchaseOrderId || 'none'}
                    onValueChange={onPurchaseOrderChange}
                  >
                    <SelectTrigger>
                      <SelectValue
                        placeholder={
                          purchaseOrdersLoading
                            ? 'Loading...'
                            : 'Select a purchase order'
                        }
                      />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No purchase order</SelectItem>
                      {(purchaseOrdersData?.items || [])
                        .filter(
                          (order) =>
                            order.procurementCategory !== 'Works' &&
                            !['Cancelled', 'Rejected'].includes(order.status)
                        )
                        .map((order) => (
                          <SelectItem key={order.id} value={order.id}>
                            {order.orderNumber} ·{' '}
                            {order.procurementCategory || 'Category missing'}
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
                    <Select
                      value={field.value || ''}
                      onValueChange={field.onChange}
                    >
                      <SelectTrigger>
                        <SelectValue
                          placeholder={
                            acceptedSupplyLoading
                              ? 'Loading...'
                              : 'Select approved completion'
                          }
                        />
                      </SelectTrigger>
                      <SelectContent>
                        {(acceptedSupplyOptions?.options || []).map(
                          (option) => (
                            <SelectItem
                              key={option.sourceId}
                              value={option.sourceId}
                            >
                              {option.label}
                            </SelectItem>
                          )
                        )}
                      </SelectContent>
                    </Select>
                  )}
                />
                {!acceptedSupplyLoading &&
                  acceptedSupplyOptions &&
                  !acceptedSupplyOptions.ready && (
                    <p className="text-sm text-destructive">
                      {acceptedSupplyOptions.blockedReasons.join(' ')}
                    </p>
                  )}
              </div>
            )}

            <div className="space-y-2">
              <Label>Supplier Invoice #</Label>
              <Input
                {...form.register('supplierInvoiceNumber')}
                placeholder="INV-2026-001"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="invoiceDate">Invoice Date</Label>
              <Controller
                control={form.control}
                name="invoiceDate"
                render={({ field }) => (
                  <FinanceDateInput
                    id="invoiceDate"
                    name={field.name}
                    ref={field.ref}
                    aria-invalid={Boolean(form.formState.errors.invoiceDate)}
                    value={field.value}
                    onBlur={field.onBlur}
                    onChange={field.onChange}
                    required
                  />
                )}
              />
              {form.formState.errors.invoiceDate && (
                <p className="text-sm text-red-500">
                  {form.formState.errors.invoiceDate.message}
                </p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="dueDate">Due Date</Label>
              <Controller
                control={form.control}
                name="dueDate"
                render={({ field }) => (
                  <FinanceDateInput
                    id="dueDate"
                    name={field.name}
                    ref={field.ref}
                    aria-invalid={Boolean(form.formState.errors.dueDate)}
                    value={field.value}
                    min={watchInvoiceDate}
                    onBlur={field.onBlur}
                    onChange={field.onChange}
                    required
                  />
                )}
              />
              {form.formState.errors.dueDate && (
                <p className="text-sm text-red-500">
                  {form.formState.errors.dueDate.message}
                </p>
              )}
            </div>

            <div className="space-y-2">
              <Label>Currency</Label>
              <Controller
                control={form.control}
                name="currencyCode"
                render={({ field }) => (
                  <Select
                    disabled={receiptSourceLocked}
                    value={field.value}
                    onValueChange={async (val) => {
                      field.onChange(val);
                      try {
                        await applyInvoiceExchangeRate(val);
                      } catch (err) {
                        console.error(
                          'Failed to fetch exchange rate for currency',
                          err
                        );
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
                <Label className="text-amber-600 font-semibold">
                  Exchange Rate to Base Currency
                </Label>
                <Input
                  type="number"
                  step="0.0001"
                  min="0.0001"
                  readOnly
                  aria-readonly="true"
                  {...form.register('exchangeRate')}
                />
                <span className="text-[11px] text-muted-foreground block mt-1">
                  1 {watchCurrencyCode} = {form.watch('exchangeRate')}{' '}
                  {financeSettings?.baseCurrency || 'GHS'}
                  {' · approved rate locked to this invoice'}
                </span>
              </div>
            )}

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
                      const term = paymentTerms.find(
                        (candidate) => candidate.id === value
                      );
                      if (term)
                        form.setValue(
                          'dueDate',
                          addDays(form.getValues('invoiceDate'), term.dueDays)
                        );
                    }}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select payment terms" />
                    </SelectTrigger>
                    <SelectContent>
                      {paymentTerms.map((term) => (
                        <SelectItem key={term.id} value={term.id}>
                          {term.code} - {term.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
            </div>
          </CardContent>
        </Card>

        {/* Line Items Card */}
        {receiptSourceLocked && !estateSourceLocked && (
          <p className="rounded-lg border p-3 text-sm text-muted-foreground">
            This draft reserves the selected receipt quantities. Review prices
            and tax treatment here. To change the receipts or quantities, delete
            the unposted draft and generate a new Auto Invoice.
          </p>
        )}
        {goodsCategory && (
          <div
            role={goodsEntryError ? 'alert' : 'status'}
            className={`rounded-lg border p-3 text-sm ${goodsEntryError ? 'border-destructive text-destructive' : 'text-muted-foreground'}`}
          >
            {goodsEntryLoading
              ? 'Checking accepted receipt quantities…'
              : goodsEntryError
                ? goodsEntryError instanceof Error
                  ? goodsEntryError.message
                  : 'Accepted receipt quantities are unavailable.'
                : `Receipt-based invoice: ${goodsEntry?.lines.reduce((sum, line) => sum + line.availableQuantity, 0) || 0} accepted units remain uninvoiced. Pending and rejected quantities are excluded.`}
          </div>
        )}
        <div className="space-y-5">
          <Card
            id="supplier-invoice-lines"
            className={cn(
              'min-w-0',
              linesFullScreen &&
                'fixed inset-0 z-[60] flex h-dvh flex-col rounded-none border-0 bg-background shadow-xl'
            )}
          >
            <CardHeader className="flex shrink-0 flex-row flex-wrap items-center justify-between gap-3 space-y-0 px-4 py-3">
              <CardTitle className="text-base">
                Invoice lines{' '}
                <span className="ml-2 rounded-full bg-muted px-2 py-0.5 text-xs font-medium text-muted-foreground">
                  {fields.length}
                </span>
              </CardTitle>
              <div className="flex items-center gap-2">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  disabled={receiptSourceLocked}
                  onClick={addInvoiceLine}
                >
                  <Plus className="mr-1.5 h-4 w-4" />
                  Add line
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  aria-controls="supplier-invoice-lines"
                  aria-expanded={linesFullScreen}
                  onClick={() => setLinesFullScreen((value) => !value)}
                >
                  {linesFullScreen ? (
                    <Minimize2 className="mr-1.5 h-4 w-4" />
                  ) : (
                    <Maximize2 className="mr-1.5 h-4 w-4" />
                  )}
                  {linesFullScreen ? 'Exit full screen' : 'Full screen'}
                </Button>
              </div>
            </CardHeader>
            <CardContent
              className={cn(
                'p-0',
                linesFullScreen && 'min-h-0 flex-1 overflow-hidden'
              )}
            >
              <div
                className={cn(
                  lineGridStyles.grid,
                  'overflow-auto',
                  linesFullScreen ? 'h-full' : 'max-h-[520px]'
                )}
              >
                <table
                  className="w-full min-w-[900px] table-fixed text-sm"
                  aria-label="Invoice line items"
                >
                  <colgroup>
                    <col style={{ width: 36 }} />
                    <col style={{ width: 140 }} />
                    <col style={{ width: 235 }} />
                    <col />
                    <col style={{ width: 76 }} />
                    <col style={{ width: 96 }} />
                    <col style={{ width: 112 }} />
                    <col style={{ width: 76 }} />
                  </colgroup>
                  <thead className="sticky top-0 z-10 bg-muted text-xs text-muted-foreground">
                    <tr>
                      {[
                        '#',
                        'Type',
                        'Account / item',
                        'Description',
                        'Qty',
                        'Unit price',
                        'Amount',
                        '',
                      ].map((label, column) => (
                        <th
                          key={column}
                          scope="col"
                          className={cn(
                            'px-2 py-2 text-left font-medium',
                            column >= 4 && column <= 6 && 'text-right',
                            column === 6 && 'text-right'
                          )}
                        >
                          <span className={label ? '' : 'sr-only'}>
                            {label || 'Line actions'}
                          </span>
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {fields.map((field, index) => {
                      const lineItemType = form.watch(
                        `lineItems.${index}.lineItemType`
                      );
                      const selectedGlAccount = glAccountsData?.items?.find(
                        (account: any) =>
                          account.id ===
                          form.watch(`lineItems.${index}.glAccountId`)
                      );
                      const hasBudget = Boolean(
                        budgetCellsLoading[field.id] ||
                          (budgetCellsByLine[field.id]?.length ?? 0) > 0 ||
                          selectedGlAccount?.budgetTrackingEnabled
                      );
                      const needsBudget =
                        hasBudget && !watchLineItems[index]?.budgetEntryId;
                      return (
                        <tr
                          key={field.id}
                          aria-label={`Invoice line ${index + 1}`}
                          className="border-t hover:bg-muted/20"
                        >
                          <td className="px-2 py-1.5 text-center text-xs text-muted-foreground">
                            {index + 1}
                          </td>
                          <td className="px-2 py-1.5">
                            <Controller
                              control={form.control}
                              name={`lineItems.${index}.lineItemType`}
                              render={({ field }) => (
                                <Select
                                  disabled={receiptSourceLocked}
                                  value={field.value}
                                  onValueChange={field.onChange}
                                >
                                  <SelectTrigger>
                                    <SelectValue placeholder="Type" />
                                  </SelectTrigger>
                                  <SelectContent className="z-[80]">
                                    <SelectItem value="Expense">
                                      GL / Expense
                                    </SelectItem>
                                    <SelectItem value="Service">
                                      Service / Works
                                    </SelectItem>
                                    <SelectItem value="Freight">
                                      Freight (expense)
                                    </SelectItem>
                                    <SelectItem value="Miscellaneous">
                                      Miscellaneous charge
                                    </SelectItem>
                                    <SelectItem value="FinanceCharge">
                                      Finance charge
                                    </SelectItem>
                                    <SelectItem value="Inventory">
                                      Inventory Item
                                    </SelectItem>
                                    <SelectItem value="Product">
                                      Product / Other
                                    </SelectItem>
                                  </SelectContent>
                                </Select>
                              )}
                            />
                          </td>
                          <td className="px-2 py-1.5">
                            {isApExpenseLineType(lineItemType) ? (
                              <>
                                <Controller
                                  control={form.control}
                                  name={`lineItems.${index}.glAccountId`}
                                  render={({ field: accountField }) => (
                                    <Popover
                                      open={glAccountOpenIndex === index}
                                      onOpenChange={(open) => {
                                        setGlAccountOpenIndex(
                                          open ? index : null
                                        );
                                        if (!open) setGlAccountSearch('');
                                      }}
                                    >
                                      <PopoverTrigger asChild>
                                        <Button
                                          disabled={estateSourceLocked}
                                          variant="outline"
                                          role="combobox"
                                          className="h-10 w-full min-w-0 justify-between overflow-hidden px-3 text-left font-medium"
                                        >
                                          <span className="min-w-0 flex-1 truncate text-sm">
                                            {getAccountDisplay(
                                              accountField.value
                                            ) ||
                                              (glAccountsLoading
                                                ? 'Loading...'
                                                : 'Select account...')}
                                          </span>
                                          <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                                        </Button>
                                      </PopoverTrigger>
                                      <PopoverContent
                                        className="z-[70] w-[350px] p-0"
                                        align="start"
                                      >
                                        <Command shouldFilter={false}>
                                          <CommandInput
                                            placeholder="Search code or name..."
                                            value={glAccountSearch}
                                            onValueChange={setGlAccountSearch}
                                          />
                                          <CommandList>
                                            <CommandEmpty>
                                              No account found.
                                            </CommandEmpty>
                                            <CommandGroup>
                                              {filteredGlAccounts.map(
                                                (account: any) => (
                                                  <div
                                                    key={account.id}
                                                    onClick={() => {
                                                      manualSupplierDefaults.current.add(
                                                        `${form.getValues(`lineItems.${index}.sourceLineId`)}:glAccountId`
                                                      );
                                                      accountField.onChange(
                                                        account.id
                                                      );
                                                      void loadBudgetCells(
                                                        field.id,
                                                        index,
                                                        account.id
                                                      );
                                                      if (
                                                        !form.getValues(
                                                          `lineItems.${index}.description`
                                                        )
                                                      ) {
                                                        form.setValue(
                                                          `lineItems.${index}.description`,
                                                          account.accountName
                                                        );
                                                      }
                                                      setGlAccountOpenIndex(
                                                        null
                                                      );
                                                      setGlAccountSearch('');
                                                    }}
                                                    className="relative flex cursor-pointer select-none items-center rounded-sm px-2 py-1.5 text-sm outline-none hover:bg-accent hover:text-accent-foreground"
                                                  >
                                                    <div className="flex flex-col">
                                                      <span className="font-bold text-sm">
                                                        {account.accountCode}
                                                      </span>
                                                      <span className="text-xs text-muted-foreground">
                                                        {account.accountName}
                                                      </span>
                                                    </div>
                                                  </div>
                                                )
                                              )}
                                            </CommandGroup>
                                          </CommandList>
                                        </Command>
                                      </PopoverContent>
                                    </Popover>
                                  )}
                                />
                              </>
                            ) : lineItemType === 'Inventory' ? (
                              <>
                                <Controller
                                  control={form.control}
                                  name={`lineItems.${index}.inventoryItemId`}
                                  render={({ field }) => (
                                    <Popover
                                      open={inventoryItemOpenIndex === index}
                                      onOpenChange={(open) => {
                                        setInventoryItemOpenIndex(
                                          open ? index : null
                                        );
                                        if (!open) setInventoryItemSearch('');
                                      }}
                                    >
                                      <PopoverTrigger asChild>
                                        <Button
                                          variant="outline"
                                          role="combobox"
                                          className="h-10 w-full min-w-0 justify-between overflow-hidden px-3 text-left font-medium"
                                        >
                                          <span className="min-w-0 flex-1 truncate text-sm">
                                            {getInventoryItemDisplay(
                                              field.value
                                            ) ||
                                              (inventoryItemsLoading
                                                ? 'Loading...'
                                                : 'Select item...')}
                                          </span>
                                          <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                                        </Button>
                                      </PopoverTrigger>
                                      <PopoverContent
                                        className="z-[70] w-[350px] p-0"
                                        align="start"
                                      >
                                        <Command shouldFilter={false}>
                                          <CommandInput
                                            placeholder="Search item code or name..."
                                            value={inventoryItemSearch}
                                            onValueChange={
                                              setInventoryItemSearch
                                            }
                                          />
                                          <CommandList>
                                            <CommandEmpty>
                                              No inventory items found.
                                            </CommandEmpty>
                                            <CommandGroup>
                                              {filteredInventoryItems.map(
                                                (item: any) => (
                                                  <div
                                                    key={item.id}
                                                    onClick={() => {
                                                      field.onChange(item.id);
                                                      form.setValue(
                                                        `lineItems.${index}.description`,
                                                        item.name
                                                      );
                                                      if (item.averageCost > 0)
                                                        form.setValue(
                                                          `lineItems.${index}.unitPrice`,
                                                          item.averageCost
                                                        );
                                                      setInventoryItemOpenIndex(
                                                        null
                                                      );
                                                      setInventoryItemSearch(
                                                        ''
                                                      );
                                                    }}
                                                    className="relative flex cursor-pointer select-none items-center rounded-sm px-2 py-1.5 text-sm outline-none hover:bg-accent hover:text-accent-foreground"
                                                  >
                                                    <div className="flex flex-col w-full">
                                                      <span className="font-bold text-sm">
                                                        {item.itemCode}
                                                      </span>
                                                      <div className="flex justify-between items-center w-full">
                                                        <span className="text-xs text-muted-foreground mr-2">
                                                          {item.name}
                                                        </span>
                                                        <span className="text-xs badge bg-muted px-1 rounded">
                                                          {formatCurrency(
                                                            item.averageCost ||
                                                              0,
                                                            watchCurrencyCode
                                                          )}
                                                        </span>
                                                      </div>
                                                    </div>
                                                  </div>
                                                )
                                              )}
                                            </CommandGroup>
                                          </CommandList>
                                        </Command>
                                      </PopoverContent>
                                    </Popover>
                                  )}
                                />
                              </>
                            ) : (
                              <span className="text-muted-foreground">—</span>
                            )}
                          </td>
                          <td className="px-2 py-1.5">
                            <Textarea
                              aria-label={`Line ${index + 1} description`}
                              aria-keyshortcuts="Shift+Enter"
                              title="Shift+Enter to add a new line"
                              wrap="off"
                              readOnly={estateSourceLocked}
                              {...form.register(
                                `lineItems.${index}.description` as const
                              )}
                              placeholder="Description"
                              rows={Math.max(1, (watchLineItems[index]?.description || '').split('\n').length)}
                              className={cn('min-w-0', lineGridStyles.description)}
                            />
                          </td>
                          <td className="px-2 py-1.5">
                            <Input
                              aria-label={`Line ${index + 1} quantity`}
                              disabled={receiptSourceLocked}
                              type="number"
                              step="0.0001"
                              {...form.register(
                                `lineItems.${index}.quantity` as const
                              )}
                              className="text-right tabular-nums"
                            />
                          </td>
                          <td className="px-2 py-1.5">
                            <Input
                              aria-label={`Line ${index + 1} unit price`}
                              readOnly={estateSourceLocked}
                              type="number"
                              step="0.01"
                              {...form.register(
                                `lineItems.${index}.unitPrice` as const
                              )}
                              className="text-right tabular-nums"
                            />
                          </td>

                          <td className="whitespace-nowrap px-2 py-1.5 text-right text-xs font-medium tabular-nums">
                            {formatAmountWithCurrency(
                              (Number(watchLineItems[index]?.quantity) || 0) *
                                (Number(watchLineItems[index]?.unitPrice) || 0)
                            )}
                          </td>
                          <td className="px-1 py-1.5">
                            <div className="flex items-center justify-end gap-0.5">
                              {(hasBudget || lineItemType === 'Inventory') && (
                                <Popover>
                                  <PopoverTrigger asChild>
                                    <Button
                                      type="button"
                                      variant="ghost"
                                      size="icon"
                                      className={cn(
                                        'h-7 w-7',
                                        needsBudget && 'text-amber-700'
                                      )}
                                      aria-label={`Line ${index + 1} details${needsBudget ? ': budget required' : ''}`}
                                    >
                                      <MoreHorizontal className="h-4 w-4" />
                                    </Button>
                                  </PopoverTrigger>
                                  <PopoverContent
                                    align="end"
                                    className="z-[70] w-96 space-y-3"
                                  >
                                    <p className="text-sm font-medium">
                                      Line {index + 1} details
                                    </p>
                                    {hasBudget && (
                                      <div className="space-y-1.5">
                                        <Label>Budget allocation</Label>
                                        <Controller
                                          control={form.control}
                                          name={`lineItems.${index}.budgetEntryId`}
                                          render={({ field: budgetField }) => (
                                            <Select
                                              value={budgetField.value}
                                              onValueChange={
                                                budgetField.onChange
                                              }
                                              disabled={
                                                budgetCellsLoading[field.id] ||
                                                (budgetCellsByLine[field.id]
                                                  ?.length ?? 0) === 0
                                              }
                                            >
                                              <SelectTrigger className="mt-2 h-auto min-h-10 text-left">
                                                <SelectValue
                                                  placeholder={
                                                    budgetCellsLoading[field.id]
                                                      ? 'Loading budget cells...'
                                                      : (budgetCellsByLine[
                                                            field.id
                                                          ]?.length ?? 0) === 0
                                                        ? 'No adopted budget cell'
                                                        : 'Select adopted budget cell'
                                                  }
                                                />
                                              </SelectTrigger>
                                              <SelectContent className="z-[80]">
                                                {(
                                                  budgetCellsByLine[field.id] ||
                                                  []
                                                ).map((cell) => (
                                                  <SelectItem
                                                    key={cell.budgetEntryId}
                                                    value={cell.budgetEntryId}
                                                  >
                                                    <span className="flex min-w-72 flex-col gap-1 py-1">
                                                      <span>
                                                        {cell.dimensionAssignments
                                                          .map(
                                                            (item) =>
                                                              `${item.dimensionCode}: ${item.valueCode}`
                                                          )
                                                          .join(' · ') ||
                                                          'Account total'}
                                                      </span>
                                                      <span className="text-xs text-muted-foreground">
                                                        {cell.fiscalPeriodCode}
                                                      </span>
                                                      <span className="grid grid-cols-2 gap-x-4 gap-y-0.5 text-xs text-muted-foreground">
                                                        <span>
                                                          Approved:{' '}
                                                          {formatCurrency(
                                                            cell.approvedAmount,
                                                            cell.functionalCurrencyCode
                                                          )}
                                                        </span>
                                                        <span>
                                                          Actual:{' '}
                                                          {formatCurrency(
                                                            cell.postedActualAmount,
                                                            cell.functionalCurrencyCode
                                                          )}
                                                        </span>
                                                        <span>
                                                          Reserved:{' '}
                                                          {formatCurrency(
                                                            cell.reservedAmount,
                                                            cell.functionalCurrencyCode
                                                          )}
                                                        </span>
                                                        <span>
                                                          Available:{' '}
                                                          {formatCurrency(
                                                            cell.availableAmount,
                                                            cell.functionalCurrencyCode
                                                          )}
                                                        </span>
                                                      </span>
                                                    </span>
                                                  </SelectItem>
                                                ))}
                                              </SelectContent>
                                            </Select>
                                          )}
                                        />
                                      </div>
                                    )}
                                    {lineItemType === 'Inventory' && (
                                      <div className="space-y-1.5">
                                        <Label>Warehouse</Label>
                                        <Controller
                                          control={form.control}
                                          name={`lineItems.${index}.warehouseId`}
                                          render={({ field }) => (
                                            <Select
                                              value={field.value}
                                              onValueChange={field.onChange}
                                            >
                                              <SelectTrigger>
                                                <SelectValue placeholder="Warehouse..." />
                                              </SelectTrigger>
                                              <SelectContent className="z-[80]">
                                                {warehousesData?.map(
                                                  (wh: any) => (
                                                    <SelectItem
                                                      key={wh.id}
                                                      value={wh.id}
                                                    >
                                                      {wh.name}
                                                    </SelectItem>
                                                  )
                                                )}
                                              </SelectContent>
                                            </Select>
                                          )}
                                        />
                                      </div>
                                    )}
                                  </PopoverContent>
                                </Popover>
                              )}
                              <Button
                                type="button"
                                variant="ghost"
                                size="icon"
                                aria-label={`Remove line ${index + 1}`}
                                onClick={() => remove(index)}
                                disabled={
                                  receiptSourceLocked || fields.length === 1
                                }
                                className="h-7 w-7 text-destructive hover:text-destructive"
                              >
                                <Trash2 className="h-3.5 w-3.5" />
                              </Button>
                            </div>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </CardContent>
            <div className="shrink-0 border-t px-4 py-3">
              <div className="flex flex-wrap items-start justify-between gap-4">
                <div className="space-y-2 text-xs text-muted-foreground">
                  <p>
                    {fields.length} {fields.length === 1 ? 'item' : 'items'} ·{' '}
                    {watchCurrencyCode}
                  </p>
                  <p>Tax and trade discount apply to the whole invoice.</p>
                  {estateSourceLocked && (
                    <p>Trade discount is controlled by the Estate source.</p>
                  )}
                  {linesFullScreen && (
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      onClick={() => setLinesFullScreen(false)}
                    >
                      Done
                    </Button>
                  )}
                </div>
                <div
                  className="w-full max-w-md space-y-2 text-sm"
                  aria-label="Invoice totals"
                >
                  <div className="flex items-center justify-between gap-3">
                    <span className="text-muted-foreground">Subtotal</span>
                    <span className="tabular-nums">
                      {formatAmountWithCurrency(grossSubtotal)}
                    </span>
                  </div>
                  <div className="flex items-center justify-between gap-3">
                    <Label
                      htmlFor="invoice-trade-discount"
                      className="font-normal text-muted-foreground"
                    >
                      Trade discount
                    </Label>
                    <div className="flex items-center gap-2">
                      <Input
                        id="invoice-trade-discount"
                        aria-label="Invoice trade discount percentage"
                        type="number"
                        min="0"
                        max="100"
                        step="0.01"
                        className="h-8 w-20 text-right text-xs tabular-nums"
                        value={invoiceDiscountRate ?? ''}
                        placeholder="Mixed"
                        readOnly={invoiceDiscountLocked}
                        onChange={(event) =>
                          applyInvoiceDiscount(event.target.value)
                        }
                      />
                      <span className="text-xs text-muted-foreground">%</span>
                      <span className="min-w-28 text-right tabular-nums">
                        −{formatAmountWithCurrency(invoiceTradeDiscount)}
                      </span>
                    </div>
                  </div>
                  {invoiceDiscountRates.size > 1 && (
                    <p className="text-xs text-amber-700">
                      Existing discounts vary. Enter a rate to replace them
                      across the invoice.
                    </p>
                  )}
                  <div className="flex items-center justify-between gap-3">
                    <Label
                      htmlFor="invoice-tax"
                      className="font-normal text-muted-foreground"
                    >
                      Tax schedule
                    </Label>
                    <Select
                      value={invoiceTaxChoice}
                      onValueChange={applyInvoiceTax}
                      disabled={watchIsOpeningBalance}
                    >
                      <SelectTrigger
                        id="invoice-tax"
                        aria-label="Invoice tax schedule"
                        className="h-8 w-64 text-xs"
                      >
                        <SelectValue placeholder="Select applicable tax" />
                      </SelectTrigger>
                      <SelectContent className="z-[80]">
                        {invoiceTaxChoice === 'mixed' && (
                          <SelectItem value="mixed">
                            Mixed existing taxes — retained
                          </SelectItem>
                        )}
                        <SelectItem value="5">Pending review</SelectItem>
                        <SelectItem value="2">Exempt</SelectItem>
                        <SelectItem value="3">Zero rated</SelectItem>
                        <SelectItem value="4">Out of scope</SelectItem>
                        {invoiceTaxChoice.startsWith('1|') &&
                          !taxGroupsData?.some(
                            (group) => '1|' + group.id === invoiceTaxChoice
                          ) && (
                            <SelectItem value={invoiceTaxChoice} disabled>
                              Tax group unavailable — select another
                            </SelectItem>
                          )}
                        {taxGroupsData?.map((group) => (
                          <SelectItem key={group.id} value={'1|' + group.id}>
                            {group.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  {invoiceTaxChoice === 'mixed' && (
                    <p className="text-xs text-amber-700">
                      Existing tax treatments vary. Select a schedule to apply
                      it to the whole invoice.
                    </p>
                  )}
                  <details className="group">
                    <summary className="flex cursor-pointer list-none items-center justify-between gap-3 [&::-webkit-details-marker]:hidden">
                      <span className="flex items-center gap-1 text-muted-foreground">Tax <ChevronDown className="h-3 w-3 transition-transform group-open:rotate-180" /></span>
                      <span className="tabular-nums">{formatAmountWithCurrency(totalTax)}</span>
                    </summary>
                  {taxEstimate.taxList.map((tax) => (
                    <div
                      key={tax.code}
                      className="flex justify-between gap-3 text-xs text-muted-foreground"
                    >
                      <span>
                        {tax.name} ({tax.rate}%)
                      </span>
                      <span className="tabular-nums">
                        {formatAmountWithCurrency(tax.amount)}
                      </span>
                    </div>
                  ))}
                  </details>
                  <div className="flex items-center justify-between gap-3 border-t pt-2 font-semibold">
                    <span>Total</span>
                    <span className="tabular-nums">
                      {formatAmountWithCurrency(taxEstimate.grandTotal)}
                    </span>
                  </div>
                  {!watchIsOpeningBalance && withholdingDecision === true && (
                    <div className="flex justify-between gap-3 text-xs text-muted-foreground">
                      <span>WHT at payment ({watchWithholdingTaxRate}%)</span>
                      <span className="tabular-nums">
                        {formatAmountWithCurrency(
                          taxEstimate.withholdingTaxAmount
                        )}
                      </span>
                    </div>
                  )}
                  {watchLineItems.some(
                    (item) => Number(item.taxTreatment) === 5
                  ) && (
                    <p
                      role="status"
                      className="text-xs text-amber-700 dark:text-amber-300"
                    >
                      Select the applicable invoice tax before submitting for
                      approval.
                    </p>
                  )}
                </div>
              </div>
            </div>
          </Card>
        </div>

        <details
          className="group rounded-xl border bg-card text-card-foreground"
          open={
            withholdingDecision === true ||
            (supplierWithholding?.required && withholdingDecision === null)
          }
        >
          <summary className="flex cursor-pointer list-none items-center justify-between gap-3 p-5 [&::-webkit-details-marker]:hidden">
            <div>
              <h2 className="text-sm font-semibold">
                Withholding &amp; exchange rate
              </h2>
              <p className="mt-1 text-xs text-muted-foreground">
                Payment withholding and currency settings.
              </p>
            </div>
            <ChevronDown className="h-4 w-4 shrink-0 transition-transform group-open:rotate-180" />
          </summary>
          <div className="grid gap-5 border-t p-5 sm:grid-cols-2">
            {watchCurrencyCode !== (financeSettings?.baseCurrency || 'GHS') && (
              <div className="border p-4 rounded-lg bg-muted/20 sm:col-span-2 space-y-4">
                <div className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
                  Advanced FX Details
                </div>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="exchangeRateDate" className="text-xs">
                      Exchange Rate Date
                    </Label>
                    <Controller
                      control={form.control}
                      name="exchangeRateDate"
                      render={({ field }) => (
                        <FinanceDateInput
                          id="exchangeRateDate"
                          name={field.name}
                          ref={field.ref}
                          className="text-xs"
                          disabled
                          value={field.value}
                          onBlur={field.onBlur}
                          onChange={field.onChange}
                        />
                      )}
                    />
                  </div>
                  <div className="space-y-2">
                    <Label className="text-xs">Exchange Rate Source</Label>
                    <Controller
                      control={form.control}
                      name="exchangeRateSource"
                      render={({ field }) => (
                        <Input
                          value={field.value || 'Approved Daily rate'}
                          readOnly
                          aria-readonly="true"
                          className="h-10 text-xs"
                        />
                      )}
                    />
                  </div>
                </div>
              </div>
            )}

            <div className="space-y-2 sm:col-span-2">
              <div className="flex flex-wrap items-center gap-3">
                <Switch
                  id="invoiceSubjectToWithholding"
                  checked={
                    !watchIsOpeningBalance && withholdingDecision === true
                  }
                  disabled={watchIsOpeningBalance}
                  onCheckedChange={(checked) => {
                    if (checked) setWithholdingPromptOpen(true);
                    else declineWithholding();
                  }}
                />
                <Label htmlFor="invoiceSubjectToWithholding">
                  Subject to withholding
                </Label>
                {supplierWithholding?.required &&
                  withholdingDecision === null &&
                  !watchIsOpeningBalance && (
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      onClick={() => setWithholdingPromptOpen(true)}
                    >
                      Review withholding
                    </Button>
                  )}
              </div>
              <div className="grid gap-3 sm:grid-cols-[1fr_130px]">
                <div className="space-y-1.5">
                  <Label htmlFor="invoiceWithholdingTaxId">
                    WHT Configuration
                  </Label>
                  <Controller
                    control={form.control}
                    name="withholdingTaxId"
                    render={({ field }) => (
                      <Select
                        value={
                          watchIsOpeningBalance
                            ? 'none'
                            : String(field.value || 'none')
                        }
                        onValueChange={(value) => {
                          manualSupplierDefaults.current.add(
                            'withholdingTaxId'
                          );
                          if (value === 'none') {
                            declineWithholding();
                            return;
                          }
                          const rate = Number(
                            withholdingTaxOptions.find(
                              (tax) => tax.id === value
                            )?.rate || 0
                          );
                          field.onChange(value);
                          setWithholdingDecision(true);
                          setWithholdingRateOverride(rate);
                          form.setValue('withholdingTaxRate', rate);
                        }}
                        disabled={
                          watchIsOpeningBalance || withholdingDecision !== true
                        }
                      >
                        <SelectTrigger
                          id="invoiceWithholdingTaxId"
                          className={cn(
                            watchIsOpeningBalance &&
                              'bg-muted text-muted-foreground'
                          )}
                        >
                          <SelectValue placeholder="No WHT" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="none">No WHT</SelectItem>
                          {watchWithholdingTaxId &&
                            watchWithholdingTaxId !== 'none' &&
                            !selectedWithholdingTax && (
                              <SelectItem value={watchWithholdingTaxId}>
                                {isEditMode ? 'Saved' : 'Supplier'} WHT (
                                {watchWithholdingTaxRate}%)
                              </SelectItem>
                            )}
                          {withholdingTaxOptions.map((tax) => (
                            <SelectItem key={tax.id} value={tax.id}>
                              {tax.code} - {tax.name} ({Number(tax.rate || 0)}%)
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="invoiceWithholdingRate">WHT Rate (%)</Label>
                  <Input
                    id="invoiceWithholdingRate"
                    type="number"
                    min="0"
                    max="100"
                    step="0.0001"
                    disabled={
                      watchIsOpeningBalance || withholdingDecision !== true
                    }
                    value={form.watch('withholdingTaxRate') ?? 0}
                    onChange={(event) => {
                      const rate =
                        event.target.value === ''
                          ? 0
                          : Number(event.target.value);
                      form.setValue('withholdingTaxRate', rate, {
                        shouldDirty: true,
                        shouldValidate: true,
                      });
                      setWithholdingRateOverride(rate);
                    }}
                  />
                  {form.formState.errors.withholdingTaxRate && (
                    <p className="text-xs text-destructive">
                      Enter a rate from 0 to 100.
                    </p>
                  )}
                </div>
              </div>
              {withholdingDecision === true && !withholdingAccountId && (
                <p role="status" className="text-xs text-destructive">
                  {supplierWithholding?.message ||
                    'Select a WHT configuration with a payable account.'}
                </p>
              )}
            </div>

            {!isEditMode && selectedSupplier && (
              <div className="space-y-3 rounded-lg border p-3 sm:col-span-2">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div className="flex items-center gap-2">
                    <Checkbox
                      id="applySupplierDefaults"
                      checked={applySupplierDefaults}
                      disabled={watchIsOpeningBalance}
                      onCheckedChange={(checked) =>
                        setApplySupplierDefaults(checked === true)
                      }
                    />
                    <Label htmlFor="applySupplierDefaults">
                      Use supplier defaults
                    </Label>
                  </div>
                  {supplierDefaultsLoading && (
                    <span className="text-xs text-muted-foreground">
                      Loading defaults...
                    </span>
                  )}
                  {applySupplierDefaults &&
                    selectedPurchaseOrderId &&
                    supplierDefaults?.paymentTermsDays != null && (
                      <span className="text-xs text-muted-foreground">
                        PO payment terms: {supplierDefaults.paymentTermsDays}{' '}
                        days
                      </span>
                    )}
                </div>
                {supplierDefaultsError && (
                  <p role="status" className="text-sm text-amber-700">
                    Supplier defaults unavailable. You can enter the invoice
                    details manually.
                  </p>
                )}
                {applySupplierDefaults &&
                  supplierDefaults?.postingDefaults.defaultTaxGroupId &&
                  taxGroupsData &&
                  !taxGroupsData.some(
                    (group) =>
                      group.id ===
                      supplierDefaults.postingDefaults.defaultTaxGroupId
                  ) && (
                    <p role="status" className="text-sm text-amber-700">
                      The saved supplier tax schedule is unavailable. Select tax
                      manually.
                    </p>
                  )}
                {(applySupplierDefaults ||
                  watchApAccountId ||
                  watchExpenseAccountId) && (
                  <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
                    <div className="space-y-1.5">
                      <Label htmlFor="apAccountId">Accounts Payable</Label>
                      <Controller
                        control={form.control}
                        name="apAccountId"
                        render={({ field }) => (
                          <PostingAccountPicker
                            id="apAccountId"
                            value={field.value}
                            accounts={(glAccountsData?.items || []).filter(
                              (account) =>
                                account.accountType === 'Liability' &&
                                (account.allowDirectPosting ||
                                  account.isControlAccount)
                            )}
                            onChange={(value) => {
                              manualSupplierDefaults.current.add('apAccountId');
                              field.onChange(value || '');
                            }}
                          />
                        )}
                      />
                    </div>
                    <div className="space-y-1.5">
                      <Label htmlFor="expenseAccountId">
                        Purchases Account
                      </Label>
                      <Controller
                        control={form.control}
                        name="expenseAccountId"
                        render={({ field }) => (
                          <PostingAccountPicker
                            id="expenseAccountId"
                            value={field.value}
                            accounts={(glAccountsData?.items || []).filter(
                              (account) =>
                                !account.isControlAccount &&
                                account.allowDirectPosting &&
                                ['Asset', 'Expense'].includes(
                                  account.accountType
                                )
                            )}
                            onChange={(value) => {
                              manualSupplierDefaults.current.add(
                                'expenseAccountId'
                              );
                              field.onChange(value || '');
                            }}
                          />
                        )}
                      />
                    </div>
                  </div>
                )}
              </div>
            )}
          </div>
        </details>
        <details className="group rounded-xl border bg-card text-card-foreground">
          <summary className="flex cursor-pointer list-none items-center justify-between gap-3 p-5 [&::-webkit-details-marker]:hidden">
            <div>
              <h2 className="text-sm font-semibold">Accounting dimensions</h2>
              <p className="mt-1 text-xs text-muted-foreground">
                Department, project, property and other reporting allocations.
              </p>
            </div>
            <ChevronDown className="h-4 w-4 shrink-0 transition-transform group-open:rotate-180" />
          </summary>
          <div className="border-t p-5">
            <SourceDocumentDimensionPanel
              context={{
                sourceModule: 'AP',
                sourceDocumentType: 'VendorInvoice',
                postingAction: 'Post',
                sourceRoute: 'finance.ap.vendor-invoices.manual',
                contractVersion: '1.0',
              }}
              effectiveDate={format(
                watchInvoiceDate || new Date(),
                'yyyy-MM-dd'
              )}
              lines={watchLineItems.map((item) => ({
                id: item.sourceLineId,
                ...getSourceLineDimensionAccounts(
                  watchIsOpeningBalance
                    ? undefined
                    : editInvoice?.financeDimensions,
                  item.sourceLineId,
                  !watchIsOpeningBalance &&
                    !selectedPurchaseOrderId &&
                    isApExpenseLineType(item.lineItemType)
                    ? item.glAccountId
                    : undefined
                ),
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
              certificationState={
                editInvoice?.financeDimensions?.certificationState
              }
            />
          </div>
        </details>
        <Card>
          <CardContent className="pt-5">
            <div className="space-y-2">
              <Label htmlFor="notes">Notes</Label>
              <Textarea
                rows={3}
                className="resize-y"
                id="notes"
                placeholder="Reference number, payment instructions, etc."
                {...form.register('notes')}
              />
            </div>
          </CardContent>
        </Card>
        <div className="sticky bottom-0 z-10 flex flex-wrap items-center justify-between gap-3 rounded-xl border bg-background/95 px-5 py-3 shadow-sm backdrop-blur">
          {form.formState.submitCount > 0 && Object.keys(form.formState.errors).length > 0 && (
            <div role="alert" className="w-full text-sm text-destructive">
              <p className="font-medium">Invoice not saved. Correct the following:</p>
              <ul className="list-disc pl-5">{getInvoiceValidationMessages(form.formState.errors).map(message => <li key={message}>{message}</li>)}</ul>
            </div>
          )}
          <div>
            <span className="mr-3 text-xs text-muted-foreground">
              Invoice total
            </span>
            <span className="font-semibold tabular-nums">
              {formatAmountWithCurrency(taxEstimate.grandTotal)}
            </span>
          </div>
          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              type="button"
              onClick={() => router.back()}
            >
              Cancel
            </Button>
            <Button
              type="submit"
              disabled={
                isSubmitting ||
                (!watchIsOpeningBalance &&
                  supplierDefaultsLoading &&
                  (!isEditMode || withholdingDecision === null)) ||
                Boolean(
                  goodsCategory &&
                    (goodsEntryLoading ||
                      goodsEntryError ||
                      !goodsEntry ||
                      fields.length === 0)
                )
              }
            >
              {isSubmitting && (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              )}
              {isEditMode ? 'Save Changes' : 'Record Invoice'}
            </Button>
          </div>
        </div>
      </form>
      <ConfirmationDialog
        open={withholdingPromptOpen && !watchIsOpeningBalance}
        onOpenChange={setWithholdingPromptOpen}
        title="Apply withholding to this invoice?"
        description={
          supplierWithholding?.required
            ? `This supplier is subject to withholding at ${supplierWithholding.rate}%. Apply it to this invoice? You can change the invoice rate after selecting Yes.`
            : 'Apply withholding to this invoice? You can select the configuration and rate after selecting Yes.'
        }
        confirmText="Yes"
        cancelText="No"
        onConfirm={acceptWithholding}
        onCancel={declineWithholding}
      />
    </div>
  );
}

export default function CreateVendorInvoicePage() {
  return <VendorInvoiceFormPage />;
}
