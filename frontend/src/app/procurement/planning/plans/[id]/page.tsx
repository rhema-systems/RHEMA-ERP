'use client';

import { useState, useEffect } from 'react';
import * as XLSX from 'xlsx';
import { ProcurementControlAccordion } from '@/components/procurement/ProcurementControlAccordion';
import { useParams, useRouter } from 'next/navigation';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import {
  ArrowLeft,
  Edit,
  FileText,
  Package,
  Calendar,
  Clock,
  CheckCircle,
  XCircle,
  AlertCircle,
  Send,
  Loader2,
  Plus,
  Trash2,
  Search,
  Users,
  History,
  GitBranch,
  ClipboardPlus,
  Gavel,
  ShoppingCart,
  Download,
} from 'lucide-react';
import { toast } from 'sonner';
import {
  procurementBudgetService,
  procurementPlanService,
  commonService,
  marketAnalysisService,
  type ProcurementBudgetDetailDto,
  type ProcurementPlanDetailDto,
  type ProcurementPlanDto,
  type CreateProcurementPlanItemDto,
  type InventoryItemDto,
  type ProcurementPlanItemDto,
  type CreateProcurementPlanItemSupplierDto,
  type MarketAnalysisDto,
} from '@/services/procurementPlanningService';
import {
  businessPartnerService,
  type BusinessPartnerDto,
} from '@/services/businessPartnerService';
import {
  inventoryManagementService,
  type UnitOfMeasureDto,
} from '@/services/inventoryManagementService';
import { format } from 'date-fns';
import { FileCheck } from 'lucide-react';
import { Checkbox } from '@/components/ui/checkbox';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import {
  WorkflowApprovalActions,
  useWorkflowRecord,
} from '@/components/workflow';
import {
  WorkflowTabContent,
  WorkflowTabTrigger,
} from '@/components/workflow/WorkflowRecordTab';
import { ProcurementPlanItemDialogBody } from '@/app/procurement/planning/components/ProcurementPlanItemDialogBody';
import {
  purchasingService,
  type CreatePurchaseRequisitionDto,
  type PurchaseRequisitionType,
  type PurchaseRequisitionSummaryDto,
} from '@/services/purchasingService';
import { procurementSourcingCaseService } from '@/services/procurement-sourcing-case.service';
import type { ProcurementMethodType } from '@/types/procurement-policy';

type PlanExecutionAction = 'pr' | 'tender' | 'rfq' | 'po';

const tenderSourcingMethods = new Set<ProcurementMethodType>([
  'NationalCompetitiveTendering',
  'InternationalCompetitiveTendering',
  'RestrictedTendering',
  'SingleSource',
  'QualityBasedSelection',
  'QualityAndCostBasedSelection',
]);

const formatSourcingMethod = (method?: ProcurementMethodType) =>
  method ? method.replace(/([a-z])([A-Z])/g, '$1 $2') : 'no method';

const createEmptyItemForm = (): CreateProcurementPlanItemDto => ({
  itemDescription: '',
  specifications: '',
  itemCategory: '',
  estimatedQuantity: 1,
  unitOfMeasure: 'EA',
  estimatedUnitPrice: 0,
  priority: 'Medium',
  isCritical: false,
  requiredDate: '',
  plannedQuarter: '',
  justification: '',
  notes: '',
  itemSuppliers: [],
});

const getItemEstimatedTotal = (
  item: Pick<
    CreateProcurementPlanItemDto,
    'estimatedQuantity' | 'estimatedUnitPrice'
  >
) =>
  (Number(item.estimatedQuantity) || 0) *
  (Number(item.estimatedUnitPrice) || 0);

const normalizeLookup = (value?: string | null) =>
  value?.trim().toLowerCase() || '';

const isSupplierBusinessPartner = (partner: BusinessPartnerDto) => {
  const partnerType = normalizeLookup(partner.partnerType);
  const approvalStatus = normalizeLookup(partner.approvalStatus);
  const status = normalizeLookup(partner.status);

  return (
    ['supplier', 'both', 'vendor'].includes(partnerType) &&
    (!approvalStatus || approvalStatus === 'approved') &&
    (!status || status === 'active' || status === 'approved')
  );
};

const getUnitOfMeasureValue = (unit: UnitOfMeasureDto) =>
  unit.code?.trim() || unit.name?.trim() || unit.id;

const getUnitOfMeasureLabel = (unit: UnitOfMeasureDto) => {
  const value = getUnitOfMeasureValue(unit);
  const name = unit.name?.trim();
  const symbol = unit.symbol?.trim();

  if (name && name !== value)
    return symbol ? `${value} - ${name} (${symbol})` : `${value} - ${name}`;
  return symbol && symbol !== value ? `${value} (${symbol})` : value;
};

const buildUnitOfMeasureOptions = (
  units: UnitOfMeasureDto[],
  currentValue?: string
) => {
  const seen = new Set<string>();
  const options = units
    .map((unit) => ({
      value: getUnitOfMeasureValue(unit),
      label: getUnitOfMeasureLabel(unit),
    }))
    .filter((option) => {
      if (!option.value || seen.has(option.value)) return false;
      seen.add(option.value);
      return true;
    });

  const savedValue = currentValue?.trim();
  if (savedValue && !seen.has(savedValue)) {
    options.unshift({
      value: savedValue,
      label: `${savedValue} (saved value)`,
    });
  }

  return options;
};

export default function ProcurementPlanDetailPage() {
  const params = useParams();
  const router = useRouter();
  const planId = Array.isArray(params?.id) ? params.id[0] : (params?.id ?? '');

  const [plan, setPlan] = useState<ProcurementPlanDetailDto | null>(null);
  const exportPlanLines = () => {
    if (!plan?.items.length) return;
    const sheet = XLSX.utils.json_to_sheet(plan.items.map((item) => ({
      'Plan Number': plan.planNumber,
      'Line Reference': item.referenceNumber,
      'Item Code': item.inventoryItemCode ?? '',
      Description: item.itemDescription,
      Quantity: item.estimatedQuantity,
      'Unit of Measure': item.unitOfMeasure,
      'Estimated Unit Price': item.estimatedUnitPrice,
      'Estimated Total': item.estimatedTotalCost,
      Currency: item.currency,
      'Budget ID': item.procurementBudgetId ?? '',
      'Budget Allocation ID': item.procurementBudgetAllocationId ?? '',
      'Budget Line Code': item.budgetLineCode ?? '',
      'Budget Category': item.budgetCategoryName ?? '',
      'Approved Budget': item.approvedBudgetAmount ?? '',
      Status: item.status,
    })));
    const workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, sheet, 'Plan Lines');
    XLSX.writeFile(workbook, `${plan.planNumber.replace(/[^a-zA-Z0-9_-]/g, '_')}-lines.xlsx`);
  };
  const [loading, setLoading] = useState(true);
  const [linkedBudget, setLinkedBudget] =
    useState<ProcurementBudgetDetailDto | null>(null);
  const [loadingBudgetAllocations, setLoadingBudgetAllocations] =
    useState(false);

  // Add Item Dialog State
  const [addItemDialogOpen, setAddItemDialogOpen] = useState(false);
  const [inventoryItems, setInventoryItems] = useState<InventoryItemDto[]>([]);
  const [loadingInventory, setLoadingInventory] = useState(false);
  const [inventorySearchTerm, setInventorySearchTerm] = useState('');
  const [selectedInventoryItem, setSelectedInventoryItem] =
    useState<InventoryItemDto | null>(null);
  const [addingItem, setAddingItem] = useState(false);
  const [deletingItemId, setDeletingItemId] = useState<string | null>(null);
  const [itemIdToDelete, setItemIdToDelete] = useState<string | null>(null);
  const [newItemForm, setNewItemForm] =
    useState<CreateProcurementPlanItemDto>(createEmptyItemForm);
  const [pendingPlanItems, setPendingPlanItems] = useState<
    CreateProcurementPlanItemDto[]
  >([]);

  // Supplier selection state
  const [suppliers, setSuppliers] = useState<BusinessPartnerDto[]>([]);
  const [loadingSuppliers, setLoadingSuppliers] = useState(false);
  const [supplierSearchTerm, setSupplierSearchTerm] = useState('');
  const [selectedItemSuppliers, setSelectedItemSuppliers] = useState<
    CreateProcurementPlanItemSupplierDto[]
  >([]);
  const [marketAnalyses, setMarketAnalyses] = useState<MarketAnalysisDto[]>([]);
  const [loadingMarketAnalyses, setLoadingMarketAnalyses] = useState(false);
  const [unitsOfMeasure, setUnitsOfMeasure] = useState<UnitOfMeasureDto[]>([]);
  const [loadingUnitsOfMeasure, setLoadingUnitsOfMeasure] = useState(false);

  const [publishDialogOpen, setPublishDialogOpen] = useState(false);
  const [publishComments, setPublishComments] = useState('');
  const [publishLoading, setPublishLoading] = useState(false);
  const [versionHistory, setVersionHistory] = useState<ProcurementPlanDto[]>(
    []
  );
  const [loadingVersions, setLoadingVersions] = useState(false);
  const [amendmentDialogOpen, setAmendmentDialogOpen] = useState(false);
  const [amendmentReason, setAmendmentReason] = useState('');
  const [amendmentTitle, setAmendmentTitle] = useState('');
  const [amendmentDescription, setAmendmentDescription] = useState('');
  const [amendmentLoading, setAmendmentLoading] = useState(false);
  const [linkedRequisitions, setLinkedRequisitions] = useState<
    PurchaseRequisitionSummaryDto[]
  >([]);
  const [selectedPlanItemIds, setSelectedPlanItemIds] = useState<string[]>([]);
  const [executionAction, setExecutionAction] =
    useState<PlanExecutionAction | null>(null);
  const [executionItemIds, setExecutionItemIds] = useState<string[]>([]);
  const [executionLoading, setExecutionLoading] = useState(false);
  const [bulkRequisitionType, setBulkRequisitionType] =
    useState<PurchaseRequisitionType>('StockReplenishment');

  useEffect(() => {
    if (planId) {
      loadPlanDetails();
      loadVersionHistory();
    }
  }, [planId]);

  const loadPlanDetails = async () => {
    try {
      setLoading(true);
      const data = await procurementPlanService.getPlanById(planId);
      setPlan(data);
      setSelectedPlanItemIds((current) =>
        current.filter((id) => (data.items || []).some((item) => item.id === id))
      );
      try {
        const requisitions = await purchasingService.getPurchaseRequisitions({
          page: 1,
          pageSize: 500,
          sourcePlanId: data.id,
        });
        setLinkedRequisitions(requisitions.items || []);
      } catch (requisitionError) {
        console.error(
          'Error loading plan-linked purchase requisitions:',
          requisitionError
        );
        setLinkedRequisitions([]);
      }
      if (data.budgetId) {
        try {
          setLoadingBudgetAllocations(true);
          setLinkedBudget(
            await procurementBudgetService.getBudgetById(data.budgetId)
          );
        } catch (budgetError) {
          console.error(
            'Error loading linked budget allocations:',
            budgetError
          );
          setLinkedBudget(null);
          toast.error('The linked budget allocations could not be loaded');
        } finally {
          setLoadingBudgetAllocations(false);
        }
      } else {
        setLinkedBudget(null);
      }
    } catch (error) {
      console.error('Error loading plan details:', error);
      toast.error('Failed to load procurement plan details');
    } finally {
      setLoading(false);
    }
  };

  const loadVersionHistory = async () => {
    if (!planId) return;
    try {
      setLoadingVersions(true);
      const versions = await procurementPlanService.getVersionHistory(planId);
      setVersionHistory(versions);
    } catch (error) {
      console.error('Error loading plan version history:', error);
      setVersionHistory([]);
    } finally {
      setLoadingVersions(false);
    }
  };

  const loadInventoryItems = async () => {
    try {
      setLoadingInventory(true);
      const items = await commonService.getInventoryItems();
      setInventoryItems(items);
    } catch (error) {
      console.error('Error loading inventory items:', error);
      toast.error('Failed to load inventory items');
    } finally {
      setLoadingInventory(false);
    }
  };

  const loadSuppliers = async () => {
    try {
      setLoadingSuppliers(true);
      const partners = await businessPartnerService.getAllPartnersForDropdown();
      setSuppliers(partners.filter(isSupplierBusinessPartner));
    } catch (error) {
      console.error('Error loading suppliers:', error);
      toast.error('Failed to load suppliers');
    } finally {
      setLoadingSuppliers(false);
    }
  };

  const loadUnitsOfMeasure = async () => {
    try {
      setLoadingUnitsOfMeasure(true);
      const data = await inventoryManagementService.getUnitsOfMeasure(true);
      setUnitsOfMeasure(data);
    } catch (error) {
      console.error('Error loading units of measure:', error);
      toast.error('Failed to load units of measure');
    } finally {
      setLoadingUnitsOfMeasure(false);
    }
  };

  const loadMarketAnalyses = async () => {
    try {
      setLoadingMarketAnalyses(true);
      const response = await marketAnalysisService.getAnalyses({
        page: 1,
        pageSize: 100,
        status: 'Published',
      });
      setMarketAnalyses(response.items || []);
    } catch (error) {
      console.error('Error loading market analyses:', error);
      setMarketAnalyses([]);
    } finally {
      setLoadingMarketAnalyses(false);
    }
  };

  const handleOpenAddItemDialog = () => {
    setAddItemDialogOpen(true);
    loadInventoryItems();
    loadSuppliers();
    loadMarketAnalyses();
    loadUnitsOfMeasure();
    setPendingPlanItems([]);
    setSelectedInventoryItem(null);
    setSelectedItemSuppliers([]);
    setInventorySearchTerm('');
    setSupplierSearchTerm('');
    setNewItemForm(createEmptyItemForm());
  };

  const resetCurrentPlanItem = () => {
    setSelectedInventoryItem(null);
    setSelectedItemSuppliers([]);
    setInventorySearchTerm('');
    setSupplierSearchTerm('');
    setNewItemForm(createEmptyItemForm());
  };

  const handleAddSupplierToItem = (supplier: BusinessPartnerDto) => {
    if (selectedItemSuppliers.some((s) => s.supplierId === supplier.id)) {
      toast.error('Supplier already added');
      return;
    }
    const newSupplier: CreateProcurementPlanItemSupplierDto = {
      supplierId: supplier.id,
      isPreferred: selectedItemSuppliers.length === 0,
      priority: selectedItemSuppliers.length + 1,
    };
    const updatedSuppliers = [...selectedItemSuppliers, newSupplier];
    setSelectedItemSuppliers(updatedSuppliers);
    setNewItemForm({ ...newItemForm, itemSuppliers: updatedSuppliers });
  };

  const handleRemoveSupplierFromItem = (supplierId: string) => {
    const updatedSuppliers = selectedItemSuppliers
      .filter((s) => s.supplierId !== supplierId)
      .map((s, index) => ({ ...s, priority: index + 1 }));
    setSelectedItemSuppliers(updatedSuppliers);
    setNewItemForm({ ...newItemForm, itemSuppliers: updatedSuppliers });
  };

  const handleUpdateItemSupplier = (
    supplierId: string,
    field: keyof CreateProcurementPlanItemSupplierDto,
    value: unknown
  ) => {
    const updatedSuppliers = selectedItemSuppliers.map((s) => {
      if (s.supplierId === supplierId) {
        if (field === 'isPreferred' && value === true) {
          return { ...s, [field]: value };
        }
        return { ...s, [field]: value };
      }
      if (field === 'isPreferred' && value === true) {
        return { ...s, isPreferred: false };
      }
      return s;
    });
    setSelectedItemSuppliers(updatedSuppliers);
    setNewItemForm({ ...newItemForm, itemSuppliers: updatedSuppliers });
  };

  const handleMarketAnalysisSelect = (value: string) => {
    if (value === 'none') {
      setNewItemForm({ ...newItemForm, marketAnalysisId: undefined });
      return;
    }

    const analysis = marketAnalyses.find((item) => item.id === value);
    setNewItemForm({
      ...newItemForm,
      marketAnalysisId: value,
      itemCategory: newItemForm.itemCategory || analysis?.itemCategory || '',
      itemDescription:
        newItemForm.itemDescription ||
        analysis?.itemDescription ||
        analysis?.title ||
        '',
      estimatedUnitPrice:
        newItemForm.estimatedUnitPrice && newItemForm.estimatedUnitPrice > 0
          ? newItemForm.estimatedUnitPrice
          : analysis?.currentMarketPrice || 0,
    });
  };

  const filteredSuppliers = suppliers.filter(
    (s) =>
      (s.partnerName || '')
        .toLowerCase()
        .includes(supplierSearchTerm.toLowerCase()) ||
      (s.partnerCode || '')
        .toLowerCase()
        .includes(supplierSearchTerm.toLowerCase())
  );

  const handleSelectInventoryItem = (item: InventoryItemDto) => {
    setSelectedInventoryItem(item);
    setNewItemForm({
      ...newItemForm,
      inventoryItemId: item.id,
      itemDescription: item.name,
      specifications: item.description || '',
      itemCategory: item.categoryName || '',
      unitOfMeasure: item.unitOfMeasure || 'EA',
      estimatedUnitPrice: item.standardCost || item.averageCost || 0,
    });
  };

  const validateItemForm = (item: CreateProcurementPlanItemDto) => {
    if (!item.itemDescription.trim()) return 'Item description is required';
    if (!item.unitOfMeasure?.trim()) return 'Unit of measure is required';
    if (Number(item.estimatedQuantity) <= 0)
      return 'Quantity must be greater than 0';
    if (Number(item.estimatedUnitPrice) < 0)
      return 'Unit cost cannot be negative';
    const itemBudgetAmount =
      item.approvedBudgetAmount ?? getItemEstimatedTotal(item);
    if (itemBudgetAmount < 0) return 'Item budget amount cannot be negative';

    if (linkedBudget?.allocations.length) {
      const allocation = linkedBudget.allocations.find(
        (value) => value.id === item.procurementBudgetAllocationId
      );
      if (!allocation)
        return 'Select a budget allocation from the linked approved budget';

      const savedExposure = (plan?.items || [])
        .filter(
          (value) => value.procurementBudgetAllocationId === allocation.id
        )
        .reduce(
          (total, value) =>
            total + (value.approvedBudgetAmount ?? value.estimatedTotalCost),
          0
        );
      const queuedExposure = pendingPlanItems
        .filter(
          (value) => value.procurementBudgetAllocationId === allocation.id
        )
        .reduce(
          (total, value) =>
            total +
            (value.approvedBudgetAmount ?? getItemEstimatedTotal(value)),
          0
        );
      if (
        savedExposure + queuedExposure + itemBudgetAmount >
        allocation.remainingAmount
      ) {
        return `${allocation.categoryName} has insufficient remaining allocation for this item`;
      }
    }

    const savedPlanExposure = (plan?.items || []).reduce(
      (total, value) =>
        total + (value.approvedBudgetAmount ?? value.estimatedTotalCost),
      0
    );
    const queuedPlanExposure = pendingPlanItems.reduce(
      (total, value) =>
        total + (value.approvedBudgetAmount ?? getItemEstimatedTotal(value)),
      0
    );
    if (
      plan &&
      savedPlanExposure + queuedPlanExposure + itemBudgetAmount >
        plan.totalEstimatedBudget
    ) {
      return 'The item would exceed the procurement plan total budget';
    }
    return null;
  };

  const hasCurrentItemDraft = () =>
    Boolean(
      selectedInventoryItem ||
      newItemForm.inventoryItemId ||
      newItemForm.itemDescription.trim() ||
      newItemForm.specifications?.trim() ||
      newItemForm.justification?.trim() ||
      selectedItemSuppliers.length > 0
    );

  const buildCurrentPlanItem = (): CreateProcurementPlanItemDto => ({
    ...newItemForm,
    estimatedQuantity: Number(newItemForm.estimatedQuantity) || 0,
    estimatedUnitPrice: Number(newItemForm.estimatedUnitPrice) || 0,
    approvedBudgetAmount:
      newItemForm.approvedBudgetAmount !== undefined
        ? Number(newItemForm.approvedBudgetAmount) || 0
        : undefined,
    itemSuppliers: selectedItemSuppliers,
  });

  const handleAddItem = () => {
    const itemToQueue = buildCurrentPlanItem();
    const validationError = validateItemForm(itemToQueue);
    if (validationError) {
      toast.error(validationError);
      return;
    }

    setPendingPlanItems((items) => [...items, itemToQueue]);
    resetCurrentPlanItem();
    toast.success('Item added to list');
  };

  const handleRemovePendingPlanItem = (index: number) => {
    setPendingPlanItems((items) =>
      items.filter((_, itemIndex) => itemIndex !== index)
    );
  };

  const handleSavePendingItems = async () => {
    const itemsToSave = [...pendingPlanItems];

    if (hasCurrentItemDraft()) {
      const currentItem = buildCurrentPlanItem();
      const validationError = validateItemForm(currentItem);
      if (validationError) {
        toast.error(validationError);
        return;
      }
      itemsToSave.push(currentItem);
    }

    if (itemsToSave.length === 0) {
      toast.error('Add at least one item before saving');
      return;
    }

    try {
      setAddingItem(true);
      for (const item of itemsToSave) {
        await procurementPlanService.addItem(planId, item);
      }
      toast.success(
        `${itemsToSave.length} item${itemsToSave.length === 1 ? '' : 's'} added successfully`
      );
      setPendingPlanItems([]);
      resetCurrentPlanItem();
      setAddItemDialogOpen(false);
      loadPlanDetails(); // Refresh plan data
    } catch (error) {
      console.error('Error adding items:', error);
      toast.error(
        error instanceof Error ? error.message : 'Failed to save plan items'
      );
    } finally {
      setAddingItem(false);
    }
  };

  const handleDeleteItem = async (itemId: string) => {
    setItemIdToDelete(itemId);
  };

  const confirmDeleteItem = async () => {
    if (!itemIdToDelete) return false;

    try {
      setDeletingItemId(itemIdToDelete);
      await procurementPlanService.removeItem(planId, itemIdToDelete);
      toast.success('Item deleted successfully');
      setItemIdToDelete(null);
      loadPlanDetails(); // Refresh plan data
    } catch (error) {
      console.error('Error deleting item:', error);
      toast.error(
        error instanceof Error ? error.message : 'Failed to delete item'
      );
      return false;
    } finally {
      setDeletingItemId(null);
    }
  };

  const inventorySearchQuery = inventorySearchTerm.trim().toLowerCase();
  const filteredInventoryItems =
    inventorySearchQuery.length >= 2
      ? inventoryItems.filter(
          (item) =>
            (item.name || '').toLowerCase().includes(inventorySearchQuery) ||
            (item.itemCode || '')
              .toLowerCase()
              .includes(inventorySearchQuery) ||
            (item.categoryName || '')
              .toLowerCase()
              .includes(inventorySearchQuery)
        )
      : [];

  const getStatusBadge = (status: string) => {
    const statusConfig: Record<
      string,
      {
        variant: 'default' | 'secondary' | 'destructive' | 'outline';
        icon: React.ReactNode;
      }
    > = {
      Draft: {
        variant: 'secondary',
        icon: <FileText className="h-3 w-3 mr-1" />,
      },
      Submitted: {
        variant: 'outline',
        icon: <Send className="h-3 w-3 mr-1" />,
      },
      UnderReview: {
        variant: 'default',
        icon: <Clock className="h-3 w-3 mr-1" />,
      },
      Approved: {
        variant: 'default',
        icon: <CheckCircle className="h-3 w-3 mr-1" />,
      },
      Rejected: {
        variant: 'destructive',
        icon: <XCircle className="h-3 w-3 mr-1" />,
      },
      Active: {
        variant: 'default',
        icon: <CheckCircle className="h-3 w-3 mr-1" />,
      },
      Completed: {
        variant: 'secondary',
        icon: <CheckCircle className="h-3 w-3 mr-1" />,
      },
      Cancelled: {
        variant: 'destructive',
        icon: <XCircle className="h-3 w-3 mr-1" />,
      },
    };
    const config = statusConfig[status] || {
      variant: 'secondary' as const,
      icon: null,
    };
    return (
      <Badge variant={config.variant} className="flex items-center w-fit">
        {config.icon}
        {status}
      </Badge>
    );
  };

  const getPriorityBadge = (priority: string) => {
    const colors: Record<string, string> = {
      Low: 'bg-gray-100 text-gray-800',
      Medium: 'bg-blue-100 text-blue-800',
      High: 'bg-orange-100 text-orange-800',
      Critical: 'bg-red-100 text-red-800',
    };
    return (
      <Badge className={colors[priority] || 'bg-gray-100 text-gray-800'}>
        {priority}
      </Badge>
    );
  };

  const formatCurrency = (amount: number, currency: string = 'USD') => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency,
    }).format(amount);
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return '-';
    try {
      return format(new Date(dateString), 'MMM dd, yyyy');
    } catch {
      return dateString;
    }
  };

  const itemBatchSaveCount =
    pendingPlanItems.length + (hasCurrentItemDraft() ? 1 : 0);
  const pendingPlanItemsTotal = pendingPlanItems.reduce(
    (total, item) => total + getItemEstimatedTotal(item),
    0
  );
  const unitOfMeasureOptions = buildUnitOfMeasureOptions(
    unitsOfMeasure,
    newItemForm.unitOfMeasure
  );

  const workflow = useWorkflowRecord({
    entityType: 'ProcurementPlan',
    entityId: planId,
    entityLabel: 'Procurement Plan',
    entityNumber: plan?.planNumber,
    status: plan?.status ?? '',
    canSubmit: plan?.status === 'Draft',
    canApproveReject:
      plan?.status === 'Submitted' || plan?.status === 'UnderReview',
    enabled: Boolean(plan),
    commands: {
      submit: async () => {
        await procurementPlanService.submitForApproval(planId, {
          comments: '',
        });
      },
      approve: async ({ comments }) => {
        if (!plan) return;
        await procurementPlanService.approvePlan(planId, {
          isApproved: true,
          approvedBudget: plan.approvedBudget || plan.totalEstimatedBudget,
          comments: comments || undefined,
          autoGenerateSchedules: true,
          autoLinkBudget: true,
        });
      },
      reject: async ({ comments }) => {
        await procurementPlanService.approvePlan(planId, {
          isApproved: false,
          comments: comments || undefined,
          autoGenerateSchedules: false,
          autoLinkBudget: false,
        });
      },
      afterAction: loadPlanDetails,
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

  const handleOpenPublishDialog = () => {
    setPublishComments('');
    setPublishDialogOpen(true);
  };

  const handlePublishPlan = async () => {
    if (!plan) return;

    try {
      setPublishLoading(true);
      await procurementPlanService.publishPlan(planId, {
        comments: publishComments || undefined,
      });
      toast.success('Procurement plan published to execution');
      setPublishDialogOpen(false);
      loadPlanDetails();
    } catch (error) {
      console.error('Error publishing plan:', error);
      toast.error(
        error instanceof Error
          ? error.message
          : 'Failed to publish procurement plan'
      );
    } finally {
      setPublishLoading(false);
    }
  };

  const canAmendPlan =
    plan && ['Approved', 'Active', 'Completed'].includes(plan.status);

  const handleOpenAmendmentDialog = () => {
    if (!plan) return;
    setAmendmentReason('');
    setAmendmentTitle(`${plan.title} - Amendment ${plan.revisionNumber + 1}`);
    setAmendmentDescription(plan.description || '');
    setAmendmentDialogOpen(true);
  };

  const handleCreateAmendment = async () => {
    if (!plan || !amendmentReason.trim()) return;

    try {
      setAmendmentLoading(true);
      const amendment = await procurementPlanService.createAmendment(plan.id, {
        reason: amendmentReason.trim(),
        title: amendmentTitle.trim() || undefined,
        description: amendmentDescription.trim() || undefined,
      });
      toast.success('Draft amendment created');
      setAmendmentDialogOpen(false);
      router.push(`/procurement/planning/plans/${amendment.id}`);
    } catch (error) {
      console.error('Error creating amendment:', error);
      toast.error(
        error instanceof Error ? error.message : 'Failed to create amendment'
      );
    } finally {
      setAmendmentLoading(false);
    }
  };

  const isExecutablePlanItem = (item: ProcurementPlanItemDto) =>
    plan?.status === 'Active' &&
    ['Approved', 'Planned', 'InProgress', 'Procured'].includes(item.status);

  const getLinkedRequisition = (itemId: string) =>
    linkedRequisitions.find(
      (requisition) =>
        (requisition.sourcePlanItemId === itemId ||
          requisition.sourcePlanItemIds?.includes(itemId)) &&
        !['Rejected', 'Cancelled'].includes(requisition.status)
    );

  // `plan` is intentionally null during the first client render. Keep all
  // derived collections null-safe until the loading/not-found guards below run.
  const planItems = plan?.items || [];
  const executablePlanItems = planItems.filter(isExecutablePlanItem);
  const selectedExecutionItems = executablePlanItems.filter((item) =>
    selectedPlanItemIds.includes(item.id)
  );
  const allExecutableItemsSelected =
    executablePlanItems.length > 0 &&
    selectedExecutionItems.length === executablePlanItems.length;

  const toggleAllExecutionItems = (checked: boolean) => {
    setSelectedPlanItemIds(
      checked ? executablePlanItems.map((item) => item.id) : []
    );
  };

  const toggleExecutionItem = (itemId: string, checked: boolean) => {
    setSelectedPlanItemIds((current) =>
      checked
        ? Array.from(new Set([...current, itemId]))
        : current.filter((id) => id !== itemId)
    );
  };

  const openExecutionDialog = (
    action: PlanExecutionAction,
    itemIds: string[]
  ) => {
    const eligibleIds = itemIds.filter((itemId) =>
      executablePlanItems.some((item) => item.id === itemId)
    );
    if (!eligibleIds.length) {
      toast.error(
        'Only items in a published Active plan can enter procurement execution'
      );
      return;
    }
    setExecutionAction(action);
    setExecutionItemIds(eligibleIds);
    if (action === 'pr') setBulkRequisitionType('StockReplenishment');
  };

  const executionActionLabel = (action?: PlanExecutionAction | null) => {
    switch (action) {
      case 'pr':
        return 'Purchase Requisition';
      case 'tender':
        return 'Tender';
      case 'rfq':
        return 'RFQ';
      case 'po':
        return 'Purchase Order';
      default:
        return 'procurement document';
    }
  };

  const getExecutionError = (error: unknown) => {
    if (error instanceof Error) return error.message;
    return 'The action could not be completed';
  };

  const executePlanItems = async () => {
    if (!plan || !executionAction || !executionItemIds.length) return false;

    const items = plan.items.filter((item) =>
      executionItemIds.includes(item.id)
    );
    const failures: string[] = [];
    let completed = 0;
    let navigationTarget: string | undefined;
    const processedPrGroups = new Set<string>();
    const processedPrIds = new Set<string>();
    const prGroupKey = (item: ProcurementPlanItemDto) =>
      `${item.procurementBudgetId || plan.budgetId || 'unassigned'}|${(item.itemCategory || 'uncategorized').replace(/\s+/g, '').toLowerCase()}`;
    const unlinkedPrGroups = new Set(
      items.filter((item) => !getLinkedRequisition(item.id)).map(prGroupKey)
    );
    const linkedPrIds = new Set(
      items.map((item) => getLinkedRequisition(item.id)?.id).filter(Boolean)
    );
    const expectedPrDocumentCount = unlinkedPrGroups.size + linkedPrIds.size;

    try {
      setExecutionLoading(true);

      let requestedById = '';
      if (executionAction === 'pr') {
        try {
          const savedUser = localStorage.getItem('user');
          const user = savedUser ? JSON.parse(savedUser) : undefined;
          requestedById = user?.id || user?.userId || '';
        } catch {
          requestedById = '';
        }
        if (!requestedById) {
          toast.error(
            'Your authenticated user record is unavailable. Sign in again and retry.'
          );
          return false;
        }
      }

      for (const item of items) {
        try {
          const linkedRequisition = getLinkedRequisition(item.id);

          if (executionAction === 'pr') {
            if (linkedRequisition) {
              if (!processedPrIds.has(linkedRequisition.id)) {
                processedPrIds.add(linkedRequisition.id);
                completed += 1;
                if (expectedPrDocumentCount === 1) {
                  navigationTarget = `/procurement/purchase-requisitions/${linkedRequisition.id}`;
                }
              }
              continue;
            }

            const groupKey = prGroupKey(item);
            if (processedPrGroups.has(groupKey)) continue;
            processedPrGroups.add(groupKey);
            const groupedItems = items.filter(
              (candidate) =>
                !getLinkedRequisition(candidate.id) &&
                prGroupKey(candidate) === groupKey
            );
            const sourcePlanItemIds = groupedItems.map((candidate) => candidate.id);
            const requiredDates = groupedItems
              .map((candidate) => candidate.requiredDate?.slice(0, 10))
              .filter((value): value is string => Boolean(value))
              .sort();
            const priorityRank: Record<string, number> = {
              Low: 0,
              Normal: 1,
              Medium: 1,
              High: 2,
              Urgent: 3,
              Critical: 3,
            };
            const highestPriority = groupedItems.reduce(
              (current, candidate) =>
                (priorityRank[candidate.priority] ?? 1) >
                (priorityRank[current] ?? 1)
                  ? candidate.priority
                  : current,
              'Normal'
            );
            const singleItemJustification =
              groupedItems.length === 1
                ? groupedItems[0].justification?.trim()
                : undefined;
            const requisition: CreatePurchaseRequisitionDto = {
              requestedById,
              requiredDate: requiredDates[0],
              priority:
                highestPriority === 'Critical'
                  ? 'Urgent'
                  : highestPriority === 'Medium'
                    ? 'Normal'
                    : highestPriority,
              departmentId: plan.departmentId,
              currency: plan.currency,
              justification:
                singleItemJustification
                  ? singleItemJustification
                  : `Planned procurement for ${groupedItems.length} approved items under ${plan.planNumber}.`,
              notes: `Created from ${groupedItems.length} approved plan item${groupedItems.length === 1 ? '' : 's'}.`,
              linkage: {
                sourcePlanItemId: sourcePlanItemIds[0],
                sourcePlanItemIds,
                requisitionType: bulkRequisitionType,
              },
              items: groupedItems.map((candidate) => ({
                sourcePlanItemId: candidate.id,
                inventoryItemId: candidate.inventoryItemId,
                itemDescription: candidate.itemDescription,
                quantity: candidate.estimatedQuantity,
                unitOfMeasure: candidate.unitOfMeasure,
                estimatedUnitPrice: candidate.estimatedUnitPrice,
                requiredDate: candidate.requiredDate?.slice(0, 10),
                preferredSupplierId: candidate.preferredSupplierId,
                notes: candidate.notes,
                specifications: candidate.specifications,
              })),
            };
            const created =
              await purchasingService.createPurchaseRequisition(requisition);
            completed += 1;
            if (expectedPrDocumentCount === 1) {
              navigationTarget = `/procurement/purchase-requisitions/${created.id}`;
            }
            continue;
          }

          if (!linkedRequisition) {
            throw new Error(
              'Create and approve the Purchase Requisition first.'
            );
          }
          if (linkedRequisition.status !== 'Approved') {
            throw new Error(
              `${linkedRequisition.requisitionNumber} is ${linkedRequisition.status}; it must be Approved first.`
            );
          }

          const readiness =
            await purchasingService.getPurchaseRequisitionSourcingReadiness(
              linkedRequisition.id
            );
          if (!readiness.isReleased && !readiness.canRelease) {
            throw new Error(
              readiness.message ||
                `${linkedRequisition.requisitionNumber} is not ready for sourcing.`
            );
          }

          if (executionAction === 'tender') {
            const methodReadiness =
              await procurementSourcingCaseService.readiness(
                linkedRequisition.id
              );
            const resolvedMethod =
              methodReadiness.selectedMethod ?? methodReadiness.recommendedMethod;
            if (
              !methodReadiness.isMethodCompliant ||
              !resolvedMethod ||
              !tenderSourcingMethods.has(resolvedMethod)
            ) {
              throw new Error(
                methodReadiness.isMethodCompliant
                  ? `The effective policy selected ${formatSourcingMethod(resolvedMethod)}, not a Tender route.`
                  : methodReadiness.message
              );
            }
            if (item.tenderId && item.procurementMethod !== 'RFQ') {
              completed += 1;
              navigationTarget =
                items.length === 1
                  ? `/procurement/tenders/${item.tenderId}`
                  : navigationTarget;
              continue;
            }
            const result = await procurementPlanService.convertItemToTender({
              planItemId: item.id,
              purchaseRequisitionId: linkedRequisition.id,
              tenderTitle: `Tender for ${item.itemDescription}`,
              tenderDescription: item.specifications,
              tenderType: 'ITB',
              submissionDeadline: item.requiredDate?.slice(0, 10),
              notes: `Created from ${plan.planNumber} and ${linkedRequisition.requisitionNumber}.`,
              createSchedule: true,
            });
            completed += 1;
            navigationTarget =
              items.length === 1 && result.tenderId
                ? `/procurement/tenders/${result.tenderId}`
                : navigationTarget;
            continue;
          }

          if (executionAction === 'rfq') {
            const methodReadiness =
              await procurementSourcingCaseService.readiness(
                linkedRequisition.id
              );
            const resolvedMethod =
              methodReadiness.selectedMethod ?? methodReadiness.recommendedMethod;
            if (
              !methodReadiness.isMethodCompliant ||
              resolvedMethod !== 'RequestForQuotation'
            ) {
              throw new Error(
                methodReadiness.isMethodCompliant
                  ? `The effective policy selected ${formatSourcingMethod(resolvedMethod)}, not Request for Quotation.`
                  : methodReadiness.message
              );
            }
            if (item.tenderId && item.procurementMethod === 'RFQ') {
              completed += 1;
              navigationTarget =
                items.length === 1
                  ? `/procurement/rfqs/${item.tenderId}/edit`
                  : navigationTarget;
              continue;
            }
            const result = await procurementPlanService.convertItemToRfq({
              planItemId: item.id,
              purchaseRequisitionId: linkedRequisition.id,
              tenderTitle: `RFQ for ${item.itemDescription}`,
              tenderDescription: item.specifications,
              tenderType: 'RFQ',
              submissionDeadline: item.requiredDate?.slice(0, 10),
              notes: `Created from ${plan.planNumber} and ${linkedRequisition.requisitionNumber}.`,
              createSchedule: true,
            });
            completed += 1;
            navigationTarget =
              items.length === 1 && result.tenderId
                ? `/procurement/rfqs/${result.tenderId}/edit`
                : navigationTarget;
            continue;
          }

          if (item.purchaseOrderId) {
            completed += 1;
            navigationTarget =
              items.length === 1
                ? `/procurement/purchase-orders/${item.purchaseOrderId}`
                : navigationTarget;
            continue;
          }

          const sourceStatus =
            await purchasingService.getPurchaseOrderSourceOptions(
              linkedRequisition.id
            );
          if (!sourceStatus.ready) {
            throw new Error(
              sourceStatus.blockedReasons.join(' ') ||
                'No approved Purchase Order source is ready.'
            );
          }
          const supportedSources = sourceStatus.sources.filter((source) =>
            [
              'RfqAward',
              'TenderAward',
              'Contract',
              'ApprovedException',
            ].includes(source.sourceType)
          );
          if (supportedSources.length !== 1) {
            throw new Error(
              supportedSources.length
                ? 'More than one approved source is available; open the Purchase Order page and select the intended source.'
                : 'No supported approved award, contract, or exception source is available.'
            );
          }
          const source = supportedSources[0];
          const result =
            await procurementPlanService.convertItemToPurchaseOrder({
              planItemId: item.id,
              sourceType: source.sourceType as
                'RfqAward' | 'TenderAward' | 'Contract' | 'ApprovedException',
              sourceId: source.sourceId,
              supplierId: source.businessPartnerId,
              requiredDate: item.requiredDate?.slice(0, 10),
              notes: `Created from ${plan.planNumber} and ${linkedRequisition.requisitionNumber}.`,
              createSchedule: true,
            });
          completed += 1;
          navigationTarget =
            items.length === 1 && result.purchaseOrderId
              ? `/procurement/purchase-orders/${result.purchaseOrderId}`
              : navigationTarget;
        } catch (error) {
          failures.push(`${item.itemDescription}: ${getExecutionError(error)}`);
        }
      }

      if (completed) {
        toast.success(
          `${completed} ${executionActionLabel(executionAction)} action${completed === 1 ? '' : 's'} completed`
        );
      }
      if (failures.length) {
        toast.error(
          `${failures.length} item${failures.length === 1 ? '' : 's'} need attention`,
          {
            description: failures.slice(0, 3).join('\n'),
            duration: 10000,
          }
        );
      }

      setExecutionAction(null);
      setExecutionItemIds([]);
      setSelectedPlanItemIds([]);
      await loadPlanDetails();
      if (navigationTarget && !failures.length) router.push(navigationTarget);
      return true;
    } finally {
      setExecutionLoading(false);
    }
  };

  const getItemStatusBadge = (status: string) => {
    switch (status) {
      case 'Planned':
        return (
          <Badge variant="outline" className="bg-gray-100">
            Planned
          </Badge>
        );
      case 'Approved':
        return (
          <Badge variant="outline" className="bg-blue-100 text-blue-800">
            Approved
          </Badge>
        );
      case 'InProgress':
        return (
          <Badge variant="outline" className="bg-yellow-100 text-yellow-800">
            In Progress
          </Badge>
        );
      case 'Procured':
        return (
          <Badge variant="outline" className="bg-green-100 text-green-800">
            Procured
          </Badge>
        );
      case 'Cancelled':
        return (
          <Badge variant="outline" className="bg-red-100 text-red-800">
            Cancelled
          </Badge>
        );
      default:
        return <Badge variant="outline">{status}</Badge>;
    }
  };

  const getProcurementMethodBadge = (method?: string) => {
    switch (method) {
      case 'Tender':
        return (
          <Badge variant="outline" className="bg-purple-100 text-purple-800">
            Tender
          </Badge>
        );
      case 'RFQ':
        return (
          <Badge variant="outline" className="bg-indigo-100 text-indigo-800">
            RFQ
          </Badge>
        );
      case 'DirectPurchase':
        return (
          <Badge variant="outline" className="bg-teal-100 text-teal-800">
            Direct Purchase
          </Badge>
        );
      case 'Contract':
        return (
          <Badge variant="outline" className="bg-orange-100 text-orange-800">
            Contract
          </Badge>
        );
      default:
        return <Badge variant="outline">{method || 'N/A'}</Badge>;
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <Loader2 className="h-12 w-12 animate-spin mx-auto mb-4 text-blue-500" />
          <p className="text-lg text-gray-600">
            Loading procurement plan details...
          </p>
        </div>
      </div>
    );
  }

  if (!plan) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <AlertCircle className="h-12 w-12 mx-auto mb-4 text-red-500" />
          <p className="text-lg text-gray-600">Procurement plan not found</p>
          <Button
            onClick={() => router.push('/procurement/planning/plans')}
            className="mt-4"
          >
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back to Plans
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button
            variant="ghost"
            onClick={() => router.push('/procurement/planning/plans')}
          >
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back
          </Button>
          <div>
            <h1 className="text-3xl font-bold">{plan.title}</h1>
            <p className="text-gray-500">Fiscal Year: {plan.fiscalYear}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {getStatusBadge(plan.status)}
          {plan.status === 'Draft' && (
            <Button
              variant="outline"
              onClick={() =>
                router.push(`/procurement/planning/plans/${planId}/edit`)
              }
            >
              <Edit className="h-4 w-4 mr-2" />
              Edit
            </Button>
          )}
          <WorkflowApprovalActions {...workflow.actionProps} showStepBadge />
          {plan.status === 'Approved' && (
            <Button
              onClick={handleOpenPublishDialog}
              disabled={publishLoading || !plan.items?.length}
            >
              {publishLoading ? (
                <Loader2 className="h-4 w-4 mr-2 animate-spin" />
              ) : (
                <FileCheck className="h-4 w-4 mr-2" />
              )}
              Publish to Execution
            </Button>
          )}
          {canAmendPlan && (
            <Button variant="outline" onClick={handleOpenAmendmentDialog}>
              <GitBranch className="h-4 w-4 mr-2" />
              Amend
            </Button>
          )}
        </div>
      </div>

      {/* Overview Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">
              Total Budget
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {formatCurrency(plan.totalEstimatedBudget, plan.currency)}
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">
              Plan Items
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {plan.itemCount || plan.items?.length || 0}
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">
              Duration
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {plan.planDurationYears} Year(s)
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">
              Organization Unit
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-lg font-medium truncate">
              {plan.departmentName || '-'}
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Tabs */}
      <Tabs defaultValue="details" className="w-full">
        <TabsList>
          <TabsTrigger value="details">
            <FileText className="h-4 w-4 mr-2" />
            Details
          </TabsTrigger>
          <TabsTrigger value="items">
            <Package className="h-4 w-4 mr-2" />
            Items ({plan.items?.length || 0})
          </TabsTrigger>
          <TabsTrigger value="schedule">
            <Calendar className="h-4 w-4 mr-2" />
            Schedule
          </TabsTrigger>
          <TabsTrigger value="versions">
            <History className="h-4 w-4 mr-2" />
            Versions
          </TabsTrigger>
          <WorkflowTabTrigger {...workflow.tabProps} />
        </TabsList>

        <TabsContent value="details" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Plan Information</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-4">
              <div>
                <label className="text-sm font-medium text-gray-500">
                  Title
                </label>
                <p className="mt-1">{plan.title}</p>
              </div>
              <div>
                <label className="text-sm font-medium text-gray-500">
                  Organization Unit
                </label>
                <p className="mt-1">{plan.organizationUnitName || plan.departmentName || '-'}</p>
              </div>
              <div>
                <label className="text-sm font-medium text-gray-500">
                  Fiscal Year
                </label>
                <p className="mt-1">{plan.fiscalYear}</p>
              </div>
              <div>
                <label className="text-sm font-medium text-gray-500">
                  Planning Cycle
                </label>
                <p className="mt-1">
                  {plan.planningCycle || 'Annual'}
                  {plan.planningQuarter ? ` - ${plan.planningQuarter}` : ''}
                </p>
              </div>
              <div>
                <label className="text-sm font-medium text-gray-500">
                  Status
                </label>
                <div className="mt-1">{getStatusBadge(plan.status)}</div>
              </div>
              <div>
                <label className="text-sm font-medium text-gray-500">
                  Start Date
                </label>
                <p className="mt-1">{formatDate(plan.planStartDate)}</p>
              </div>
              <div>
                <label className="text-sm font-medium text-gray-500">
                  End Date
                </label>
                <p className="mt-1">{formatDate(plan.planEndDate)}</p>
              </div>
              <div>
                <label className="text-sm font-medium text-gray-500">
                  Total Estimated Budget
                </label>
                <p className="mt-1">
                  {formatCurrency(plan.totalEstimatedBudget, plan.currency)}
                </p>
              </div>
              <div>
                <label className="text-sm font-medium text-gray-500">
                  Currency
                </label>
                <p className="mt-1">{plan.currency}</p>
              </div>
              {plan.publishedDate && (
                <>
                  <div>
                    <label className="text-sm font-medium text-gray-500">
                      Published By
                    </label>
                    <p className="mt-1">{plan.publishedByName || '-'}</p>
                  </div>
                  <div>
                    <label className="text-sm font-medium text-gray-500">
                      Published Date
                    </label>
                    <p className="mt-1">{formatDate(plan.publishedDate)}</p>
                  </div>
                </>
              )}
              <div className="col-span-2">
                <label className="text-sm font-medium text-gray-500">
                  Description
                </label>
                <p className="mt-1">{plan.description || '-'}</p>
              </div>
              {plan.publishComments && (
                <div className="col-span-2">
                  <label className="text-sm font-medium text-gray-500">
                    Publish Comments
                  </label>
                  <p className="mt-1">{plan.publishComments}</p>
                </div>
              )}
              {plan.notes && (
                <div className="col-span-2">
                  <label className="text-sm font-medium text-gray-500">
                    Notes
                  </label>
                  <p className="mt-1">{plan.notes}</p>
                </div>
              )}
            </CardContent>
          </Card>

          <ProcurementControlAccordion
        title="Linked Procurement Budget"
        summary={linkedBudget ? `${linkedBudget.budgetCode} · ${formatCurrency(linkedBudget.remainingAmount, linkedBudget.currency)} remaining` : 'No budget linked'}
        status={linkedBudget && getStatusBadge(linkedBudget.status)}
        notice={!linkedBudget && 'Select an approved budget before adding further funding exposure.'}
      >
        <p className="mb-4 text-sm text-muted-foreground">
                Approved budget controlling this plan&apos;s funding and
                currency
              </p>
            <CardContent>
              {linkedBudget ? (
                <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-4">
                  <div>
                    <label className="text-sm font-medium text-gray-500">
                      Budget
                    </label>
                    <p className="mt-1 font-medium">
                      {linkedBudget.budgetCode} - {linkedBudget.title}
                    </p>
                  </div>
                  <div>
                    <label className="text-sm font-medium text-gray-500">
                      Status
                    </label>
                    <div className="mt-1">
                      {getStatusBadge(linkedBudget.status)}
                    </div>
                  </div>
                  <div>
                    <label className="text-sm font-medium text-gray-500">
                      Allocated Amount
                    </label>
                    <p className="mt-1">
                      {formatCurrency(
                        linkedBudget.allocatedAmount,
                        linkedBudget.currency
                      )}
                    </p>
                  </div>
                  <div>
                    <label className="text-sm font-medium text-gray-500">
                      Remaining Amount
                    </label>
                    <p className="mt-1">
                      {formatCurrency(
                        linkedBudget.remainingAmount,
                        linkedBudget.currency
                      )}
                    </p>
                  </div>
                  <div>
                    <label className="text-sm font-medium text-gray-500">
                      Committed Amount
                    </label>
                    <p className="mt-1">
                      {formatCurrency(
                        linkedBudget.committedAmount,
                        linkedBudget.currency
                      )}
                    </p>
                  </div>
                  <div>
                    <label className="text-sm font-medium text-gray-500">
                      Utilized Amount
                    </label>
                    <p className="mt-1">
                      {formatCurrency(
                        linkedBudget.utilizedAmount,
                        linkedBudget.currency
                      )}
                    </p>
                  </div>
                  <div>
                    <label className="text-sm font-medium text-gray-500">
                      Currency
                    </label>
                    <p className="mt-1">{linkedBudget.currency}</p>
                  </div>
                  <div>
                    <label className="text-sm font-medium text-gray-500">
                      Fiscal Year
                    </label>
                    <p className="mt-1">{linkedBudget.fiscalYear}</p>
                  </div>
                </div>
              ) : (
                <div className="flex items-start gap-3 rounded-md border border-amber-200 bg-amber-50 p-4 text-amber-900">
                  <AlertCircle className="mt-0.5 h-5 w-5 shrink-0" />
                  <div>
                    <p className="font-medium">
                      No procurement budget is linked to this plan.
                    </p>
                    <p className="mt-1 text-sm">
                      Amend the plan and select an approved budget before adding
                      further funding exposure.
                    </p>
                  </div>
                </div>
              )}
            </CardContent>
          </ProcurementControlAccordion>

          {plan.reviewComments && (
            <ProcurementControlAccordion
        title="Review Information"
        summary={plan.reviewedByName ? `Reviewed by ${plan.reviewedByName}` : 'Retained review details'}
      >
              <CardContent className="grid grid-cols-2 gap-4">
                <div>
                  <label className="text-sm font-medium text-gray-500">
                    Reviewed By
                  </label>
                  <p className="mt-1">{plan.reviewedByName || '-'}</p>
                </div>
                <div>
                  <label className="text-sm font-medium text-gray-500">
                    Review Date
                  </label>
                  <p className="mt-1">{formatDate(plan.reviewedDate)}</p>
                </div>
                <div className="col-span-2">
                  <label className="text-sm font-medium text-gray-500">
                    Comments
                  </label>
                  <p className="mt-1">{plan.reviewComments}</p>
                </div>
              </CardContent>
            </ProcurementControlAccordion>
          )}
        </TabsContent>

        <TabsContent value="items">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <div>
                <CardTitle>Plan Items</CardTitle>
                <CardDescription>
                  Items included in this procurement plan
                </CardDescription>
              </div>
              <div className="flex gap-2">
                <Button variant="outline" onClick={exportPlanLines} disabled={!plan.items?.length}>
                  <Download className="h-4 w-4 mr-2" />
                  Export Lines
                </Button>
              {plan.status === 'Draft' && (
                <Button onClick={handleOpenAddItemDialog}>
                  <Plus className="h-4 w-4 mr-2" />
                  Add Item
                </Button>
              )}
              </div>
            </CardHeader>
            <CardContent>
              {plan.items && plan.items.length > 0 ? (
                <div className="space-y-4">
                  {selectedPlanItemIds.length > 0 && (
                    <div className="flex flex-col gap-3 rounded-lg border border-blue-200 bg-blue-50 p-3 lg:flex-row lg:items-center lg:justify-between">
                      <div>
                        <p className="font-medium text-blue-950">
                          {selectedPlanItemIds.length} plan item
                          {selectedPlanItemIds.length === 1 ? '' : 's'} selected
                        </p>
                        <p className="text-xs text-blue-800">
                          Compatible items create one multi-line PR while every
                          line retains its exact plan and budget lineage.
                        </p>
                      </div>
                      <div className="flex flex-wrap gap-2">
                        <Button
                          size="sm"
                          onClick={() =>
                            openExecutionDialog('pr', selectedPlanItemIds)
                          }
                        >
                          <ClipboardPlus className="mr-2 h-4 w-4" />
                          PR
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() =>
                            openExecutionDialog('tender', selectedPlanItemIds)
                          }
                        >
                          <Gavel className="mr-2 h-4 w-4 text-purple-600" />
                          Tender
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() =>
                            openExecutionDialog('rfq', selectedPlanItemIds)
                          }
                        >
                          <FileText className="mr-2 h-4 w-4 text-blue-600" />
                          RFQ
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() =>
                            openExecutionDialog('po', selectedPlanItemIds)
                          }
                        >
                          <ShoppingCart className="mr-2 h-4 w-4 text-green-600" />
                          PO
                        </Button>
                      </div>
                    </div>
                  )}
                  {plan.status !== 'Active' && plan.status !== 'Draft' && (
                    <div className="flex items-start gap-2 rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                      <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
                      Publish the approved plan to Active before creating
                      Purchase Requisitions or downstream sourcing documents.
                    </div>
                  )}
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead className="w-[44px]">
                          {executablePlanItems.length > 0 && (
                            <Checkbox
                              checked={
                                allExecutableItemsSelected
                                  ? true
                                  : selectedPlanItemIds.length > 0
                                    ? 'indeterminate'
                                    : false
                              }
                              onCheckedChange={(checked) =>
                                toggleAllExecutionItems(checked === true)
                              }
                              aria-label="Select all executable plan items"
                            />
                          )}
                        </TableHead>
                        <TableHead>Item Description</TableHead>
                        <TableHead>Budget Allocation</TableHead>
                        <TableHead>Quantity</TableHead>
                        <TableHead>Unit Cost</TableHead>
                        <TableHead>Total</TableHead>
                        <TableHead>Status</TableHead>
                        <TableHead>Priority</TableHead>
                        <TableHead>Required By</TableHead>
                        <TableHead className="w-[150px]">Actions</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {plan.items.map((item) => {
                        const linkedRequisition = getLinkedRequisition(item.id);
                        const executable = isExecutablePlanItem(item);
                        return (
                          <TableRow
                            key={item.id}
                            data-state={
                              selectedPlanItemIds.includes(item.id)
                                ? 'selected'
                                : undefined
                            }
                          >
                            <TableCell>
                              {executable && (
                                <Checkbox
                                  checked={selectedPlanItemIds.includes(
                                    item.id
                                  )}
                                  onCheckedChange={(checked) =>
                                    toggleExecutionItem(
                                      item.id,
                                      checked === true
                                    )
                                  }
                                  aria-label={`Select ${item.itemDescription}`}
                                />
                              )}
                            </TableCell>
                            <TableCell className="font-medium">
                              <div>
                                {item.itemDescription}
                                <div className="mt-1 break-all font-mono text-xs text-muted-foreground">{item.referenceNumber}</div>
                                {linkedRequisition && (
                                  <button
                                    type="button"
                                    className="mt-1 block text-xs text-blue-600 hover:underline"
                                    onClick={() =>
                                      router.push(
                                        `/procurement/purchase-requisitions/${linkedRequisition.id}`
                                      )
                                    }
                                  >
                                    {linkedRequisition.requisitionNumber} ·{' '}
                                    {linkedRequisition.status}
                                  </button>
                                )}
                                {item.tenderId && (
                                  <div className="text-xs text-blue-600 mt-1">
                                    →{' '}
                                    {item.procurementMethod === 'RFQ'
                                      ? 'RFQ'
                                      : 'Tender'}{' '}
                                    created
                                  </div>
                                )}
                                {item.purchaseOrderId && (
                                  <div className="text-xs text-green-600 mt-1">
                                    → PO created
                                  </div>
                                )}
                              </div>
                            </TableCell>
                            <TableCell>
                              <div className="text-sm">
                                <div>{item.budgetCategoryName || '-'}</div>
                                {item.budgetLineCode && (
                                  <div className="text-xs text-gray-500">
                                    Legacy line: {item.budgetLineCode}
                                  </div>
                                )}
                              </div>
                            </TableCell>
                            <TableCell>
                              {item.estimatedQuantity} {item.unitOfMeasure}
                            </TableCell>
                            <TableCell>
                              {formatCurrency(
                                item.estimatedUnitPrice,
                                plan.currency
                              )}
                            </TableCell>
                            <TableCell>
                              {formatCurrency(
                                item.estimatedTotalCost,
                                plan.currency
                              )}
                            </TableCell>
                            <TableCell>
                              {getItemStatusBadge(item.status)}
                            </TableCell>
                            <TableCell>
                              {getPriorityBadge(item.priority)}
                            </TableCell>
                            <TableCell>
                              {formatDate(item.requiredDate)}
                            </TableCell>
                            <TableCell>
                              <div className="flex items-center gap-1">
                                {plan.status === 'Draft' && (
                                  <Button
                                    variant="ghost"
                                    size="icon"
                                    onClick={() =>
                                      router.push(
                                        `/procurement/planning/plans/${planId}/edit?itemId=${item.id}`
                                      )
                                    }
                                    title="Edit item"
                                  >
                                    <Edit className="h-4 w-4 text-blue-500" />
                                  </Button>
                                )}
                                {plan.status === 'Draft' && (
                                  <Button
                                    variant="ghost"
                                    size="icon"
                                    onClick={() => handleDeleteItem(item.id)}
                                    disabled={deletingItemId === item.id}
                                    title="Delete item"
                                  >
                                    {deletingItemId === item.id ? (
                                      <Loader2 className="h-4 w-4 animate-spin" />
                                    ) : (
                                      <Trash2 className="h-4 w-4 text-red-500" />
                                    )}
                                  </Button>
                                )}
                                {executable && (
                                  <>
                                    <Button
                                      variant="ghost"
                                      size="icon"
                                      onClick={() =>
                                        linkedRequisition
                                          ? router.push(
                                              `/procurement/purchase-requisitions/${linkedRequisition.id}`
                                            )
                                          : router.push(
                                              `/procurement/purchase-requisitions/new?sourcePlanItemId=${item.id}`
                                            )
                                      }
                                      title={
                                        linkedRequisition
                                          ? `Open ${linkedRequisition.requisitionNumber}`
                                          : 'Create Purchase Requisition'
                                      }
                                      aria-label={`${linkedRequisition ? 'Open' : 'Create'} purchase requisition for ${item.itemDescription}`}
                                    >
                                      <ClipboardPlus className="h-4 w-4 text-blue-600" />
                                    </Button>
                                    <Button
                                      variant="ghost"
                                      size="icon"
                                      onClick={() =>
                                        item.tenderId &&
                                        item.procurementMethod !== 'RFQ'
                                          ? router.push(
                                              `/procurement/tenders/${item.tenderId}`
                                            )
                                          : openExecutionDialog('tender', [
                                              item.id,
                                            ])
                                      }
                                      title={
                                        item.tenderId &&
                                        item.procurementMethod !== 'RFQ'
                                          ? 'Open Tender'
                                          : 'Create Tender when the approved PR policy selects a tender route'
                                      }
                                      aria-label={`${item.tenderId ? 'Open' : 'Create'} tender for ${item.itemDescription}`}
                                    >
                                      <Gavel className="h-4 w-4 text-purple-600" />
                                    </Button>
                                    <Button
                                      variant="ghost"
                                      size="icon"
                                      onClick={() =>
                                        item.tenderId &&
                                        item.procurementMethod === 'RFQ'
                                          ? router.push(
                                              `/procurement/rfqs/${item.tenderId}/edit`
                                            )
                                          : openExecutionDialog('rfq', [
                                              item.id,
                                            ])
                                      }
                                      title={
                                        item.tenderId &&
                                        item.procurementMethod === 'RFQ'
                                          ? 'Open RFQ'
                                          : 'Create RFQ when the approved PR policy selects Request for Quotation'
                                      }
                                      aria-label={`${item.tenderId ? 'Open' : 'Create'} RFQ for ${item.itemDescription}`}
                                    >
                                      <FileText className="h-4 w-4 text-indigo-600" />
                                    </Button>
                                    <Button
                                      variant="ghost"
                                      size="icon"
                                      onClick={() =>
                                        item.purchaseOrderId
                                          ? router.push(
                                              `/procurement/purchase-orders/${item.purchaseOrderId}`
                                            )
                                          : openExecutionDialog('po', [item.id])
                                      }
                                      title={
                                        item.purchaseOrderId
                                          ? 'Open Purchase Order'
                                          : 'Create Purchase Order from an approved source'
                                      }
                                      aria-label={`${item.purchaseOrderId ? 'Open' : 'Create'} purchase order for ${item.itemDescription}`}
                                    >
                                      <ShoppingCart className="h-4 w-4 text-green-600" />
                                    </Button>
                                  </>
                                )}
                              </div>
                            </TableCell>
                          </TableRow>
                        );
                      })}
                    </TableBody>
                  </Table>
                </div>
              ) : (
                <div className="text-center py-8 text-gray-500">
                  <Package className="h-12 w-12 mx-auto mb-2 opacity-50" />
                  <p>No items added to this plan yet</p>
                  {plan.status === 'Draft' && (
                    <Button
                      onClick={handleOpenAddItemDialog}
                      variant="outline"
                      className="mt-4"
                    >
                      <Plus className="h-4 w-4 mr-2" />
                      Add First Item
                    </Button>
                  )}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="schedule">
          <Card>
            <CardHeader>
              <CardTitle>Procurement Schedule</CardTitle>
              <CardDescription>
                Timeline and milestones for this plan
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                <div className="flex items-center gap-4 p-4 border rounded-lg">
                  <Calendar className="h-8 w-8 text-blue-500" />
                  <div>
                    <p className="font-medium">Plan Period</p>
                    <p className="text-sm text-gray-500">
                      {formatDate(plan.planStartDate)} -{' '}
                      {formatDate(plan.planEndDate)}
                    </p>
                  </div>
                </div>
                <div className="flex items-center gap-4 p-4 border rounded-lg">
                  <Clock className="h-8 w-8 text-green-500" />
                  <div>
                    <p className="font-medium">Duration</p>
                    <p className="text-sm text-gray-500">
                      {plan.planDurationYears} Year(s)
                    </p>
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="versions">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <div>
                <CardTitle>Version History</CardTitle>
                <CardDescription>
                  Original plan, amendments, and draft revisions linked to this
                  plan.
                </CardDescription>
              </div>
              {canAmendPlan && (
                <Button variant="outline" onClick={handleOpenAmendmentDialog}>
                  <GitBranch className="h-4 w-4 mr-2" />
                  Create Amendment
                </Button>
              )}
            </CardHeader>
            <CardContent>
              {loadingVersions ? (
                <div className="flex items-center justify-center py-8 text-gray-500">
                  <Loader2 className="h-5 w-5 mr-2 animate-spin" />
                  Loading versions...
                </div>
              ) : versionHistory.length === 0 ? (
                <div className="text-center py-8 text-gray-500">
                  No linked versions found.
                </div>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Revision</TableHead>
                      <TableHead>Plan</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Budget</TableHead>
                      <TableHead>Prepared</TableHead>
                      <TableHead className="w-[90px]">Action</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {versionHistory.map((version) => (
                      <TableRow key={version.id}>
                        <TableCell>Rev {version.revisionNumber}</TableCell>
                        <TableCell>
                          <div className="font-medium">
                            {version.planNumber}
                          </div>
                          <div className="text-xs text-gray-500">
                            {version.title}
                          </div>
                        </TableCell>
                        <TableCell>{getStatusBadge(version.status)}</TableCell>
                        <TableCell>
                          {formatCurrency(
                            version.totalEstimatedBudget,
                            version.currency
                          )}
                        </TableCell>
                        <TableCell>
                          {formatDate(
                            version.preparedDate || version.createdAt
                          )}
                        </TableCell>
                        <TableCell>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() =>
                              router.push(
                                `/procurement/planning/plans/${version.id}`
                              )
                            }
                          >
                            Open
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <WorkflowTabContent
          {...workflow.actionProps}
          entityType="ProcurementPlan"
          showActions
        />
      </Tabs>

      {/* Add Item Dialog */}
      <Dialog open={addItemDialogOpen} onOpenChange={setAddItemDialogOpen}>
        <DialogContent className="max-w-[96vw] xl:max-w-7xl max-h-[92vh] overflow-hidden flex flex-col">
          <DialogHeader className="pb-3 border-b">
            <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <DialogTitle>Add Items to Procurement Plan</DialogTitle>
                <DialogDescription>
                  Select a product, enter the required details, add it to the
                  list, then save the batch.
                </DialogDescription>
              </div>
              <Badge variant="outline" className="w-fit">
                Costs in {plan?.currency || 'USD'}
              </Badge>
            </div>
          </DialogHeader>

          <ProcurementPlanItemDialogBody
            currency={plan?.currency || 'USD'}
            form={newItemForm}
            setForm={setNewItemForm}
            inventorySearchTerm={inventorySearchTerm}
            onInventorySearchTermChange={setInventorySearchTerm}
            inventoryResults={filteredInventoryItems}
            loadingInventory={loadingInventory}
            selectedInventoryItem={selectedInventoryItem}
            onSelectInventoryItem={handleSelectInventoryItem}
            unitOfMeasureOptions={unitOfMeasureOptions}
            loadingUnitsOfMeasure={loadingUnitsOfMeasure}
            marketAnalyses={marketAnalyses}
            loadingMarketAnalyses={loadingMarketAnalyses}
            onMarketAnalysisSelect={handleMarketAnalysisSelect}
            budgetCode={linkedBudget?.budgetCode}
            budgetAllocations={linkedBudget?.allocations || []}
            loadingBudgetAllocations={loadingBudgetAllocations}
            suppliers={suppliers}
            supplierSearchTerm={supplierSearchTerm}
            onSupplierSearchTermChange={setSupplierSearchTerm}
            filteredSuppliers={filteredSuppliers}
            loadingSuppliers={loadingSuppliers}
            selectedItemSuppliers={selectedItemSuppliers}
            onAddSupplier={handleAddSupplierToItem}
            onUpdateSupplier={handleUpdateItemSupplier}
            onRemoveSupplier={handleRemoveSupplierFromItem}
            pendingPlanItems={pendingPlanItems}
            pendingPlanItemsTotal={pendingPlanItemsTotal}
            onAddCurrentItem={handleAddItem}
            onClearCurrentItem={resetCurrentPlanItem}
            onRemovePendingPlanItem={handleRemovePendingPlanItem}
          />

          <Tabs defaultValue="details" className="hidden">
            <TabsList className="grid w-full max-w-md grid-cols-2">
              <TabsTrigger value="details">Item Details</TabsTrigger>
              <TabsTrigger value="suppliers">
                Suppliers ({selectedItemSuppliers.length})
              </TabsTrigger>
            </TabsList>
            <TabsContent value="details" className="mt-3 min-h-0">
              <div className="grid grid-cols-1 lg:grid-cols-[minmax(260px,0.85fr)_minmax(460px,1.2fr)_minmax(280px,0.85fr)] gap-4 overflow-y-auto pr-1">
                <div className="space-y-3 lg:border-r lg:pr-4">
                  <div className="flex items-center justify-between">
                    <h4 className="font-medium">Products</h4>
                    <Badge variant="secondary">
                      {filteredInventoryItems.length}
                    </Badge>
                  </div>
                  <div className="relative">
                    <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
                    <Input
                      placeholder="Search inventory items..."
                      value={inventorySearchTerm}
                      onChange={(e) => setInventorySearchTerm(e.target.value)}
                      className="h-9 pl-9"
                    />
                  </div>
                  <div className="h-[360px] lg:h-[56vh] overflow-y-auto border rounded-md">
                    {loadingInventory ? (
                      <div className="flex items-center justify-center h-full">
                        <Loader2 className="h-6 w-6 animate-spin" />
                      </div>
                    ) : filteredInventoryItems.length === 0 ? (
                      <div className="flex items-center justify-center h-full text-gray-500">
                        No items found
                      </div>
                    ) : (
                      <div className="divide-y">
                        {filteredInventoryItems.map((item) => (
                          <div
                            key={item.id}
                            className={`p-2.5 cursor-pointer hover:bg-gray-50 ${
                              selectedInventoryItem?.id === item.id
                                ? 'bg-blue-50 border-l-4 border-blue-500'
                                : ''
                            }`}
                            onClick={() => handleSelectInventoryItem(item)}
                          >
                            <div className="font-medium text-sm leading-tight">
                              {item.name}
                            </div>
                            <div className="text-xs text-gray-500 mt-1">
                              {item.itemCode} |{' '}
                              {item.categoryName || 'No Category'} |{' '}
                              {item.unitOfMeasure}
                            </div>
                            <div className="text-xs text-gray-500">
                              Cost:{' '}
                              {formatCurrency(
                                item.standardCost || item.averageCost || 0,
                                plan?.currency || 'USD'
                              )}{' '}
                              | Stock: {item.availableStock}
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </div>

                <div className="space-y-3">
                  <div className="flex items-center justify-between">
                    <h4 className="font-medium">Item Details</h4>
                    <Badge variant="outline">
                      Line total:{' '}
                      {formatCurrency(
                        getItemEstimatedTotal(newItemForm),
                        plan?.currency || 'USD'
                      )}
                    </Badge>
                  </div>
                  <div className="space-y-2">
                    <div className="space-y-1">
                      <Label htmlFor="itemDescription">
                        Item Description *
                      </Label>
                      <Input
                        id="itemDescription"
                        className="h-9"
                        value={newItemForm.itemDescription}
                        onChange={(e) =>
                          setNewItemForm({
                            ...newItemForm,
                            itemDescription: e.target.value,
                          })
                        }
                        placeholder="Enter item description"
                      />
                    </div>
                    <div className="grid grid-cols-3 gap-3">
                      <div className="space-y-1">
                        <Label htmlFor="unitOfMeasure">Unit of Measure</Label>
                        <Select
                          value={newItemForm.unitOfMeasure || undefined}
                          onValueChange={(value) =>
                            setNewItemForm({
                              ...newItemForm,
                              unitOfMeasure: value,
                            })
                          }
                          disabled={
                            loadingUnitsOfMeasure &&
                            unitOfMeasureOptions.length === 0
                          }
                        >
                          <SelectTrigger id="unitOfMeasure" className="h-9">
                            <SelectValue
                              placeholder={
                                loadingUnitsOfMeasure
                                  ? 'Loading UOMs...'
                                  : 'Select UOM'
                              }
                            />
                          </SelectTrigger>
                          <SelectContent>
                            {unitOfMeasureOptions.map((option) => (
                              <SelectItem
                                key={option.value}
                                value={option.value}
                              >
                                {option.label}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>
                      <div className="space-y-1">
                        <Label htmlFor="estimatedQuantity">Quantity *</Label>
                        <Input
                          id="estimatedQuantity"
                          type="number"
                          min={1}
                          className="h-9"
                          value={newItemForm.estimatedQuantity}
                          onChange={(e) =>
                            setNewItemForm({
                              ...newItemForm,
                              estimatedQuantity:
                                parseFloat(e.target.value) || 0,
                            })
                          }
                        />
                      </div>
                      <div className="space-y-1">
                        <Label htmlFor="estimatedUnitPrice">
                          Unit Cost ({plan?.currency || 'USD'})
                        </Label>
                        <Input
                          id="estimatedUnitPrice"
                          type="number"
                          min={0}
                          step={0.01}
                          className="h-9"
                          value={newItemForm.estimatedUnitPrice || 0}
                          onChange={(e) =>
                            setNewItemForm({
                              ...newItemForm,
                              estimatedUnitPrice:
                                parseFloat(e.target.value) || 0,
                            })
                          }
                        />
                      </div>
                    </div>
                    <div className="grid grid-cols-2 gap-3">
                      <div className="space-y-1">
                        <Label htmlFor="priority">Priority</Label>
                        <Select
                          value={newItemForm.priority || 'Medium'}
                          onValueChange={(value) =>
                            setNewItemForm({ ...newItemForm, priority: value })
                          }
                        >
                          <SelectTrigger className="h-9">
                            <SelectValue placeholder="Select priority" />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="Low">Low</SelectItem>
                            <SelectItem value="Medium">Medium</SelectItem>
                            <SelectItem value="High">High</SelectItem>
                            <SelectItem value="Critical">Critical</SelectItem>
                          </SelectContent>
                        </Select>
                      </div>
                      <div className="space-y-1">
                        <Label htmlFor="requiredDate">Required By</Label>
                        <Input
                          id="requiredDate"
                          type="date"
                          className="h-9"
                          value={newItemForm.requiredDate || ''}
                          onChange={(e) =>
                            setNewItemForm({
                              ...newItemForm,
                              requiredDate: e.target.value,
                            })
                          }
                        />
                      </div>
                    </div>
                    <div className="grid grid-cols-2 gap-3">
                      <div className="space-y-1">
                        <Label htmlFor="plannedQuarter">Quarter</Label>
                        <Select
                          value={newItemForm.plannedQuarter || ''}
                          onValueChange={(value) =>
                            setNewItemForm({
                              ...newItemForm,
                              plannedQuarter: value,
                            })
                          }
                        >
                          <SelectTrigger className="h-9">
                            <SelectValue placeholder="Select quarter" />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="Q1">Q1</SelectItem>
                            <SelectItem value="Q2">Q2</SelectItem>
                            <SelectItem value="Q3">Q3</SelectItem>
                            <SelectItem value="Q4">Q4</SelectItem>
                          </SelectContent>
                        </Select>
                      </div>
                    </div>
                    <div className="grid grid-cols-3 gap-3">
                      <div className="space-y-1">
                        <Label htmlFor="budgetLineCode">Budget Line</Label>
                        <Input
                          id="budgetLineCode"
                          className="h-9"
                          value={newItemForm.budgetLineCode || ''}
                          onChange={(e) =>
                            setNewItemForm({
                              ...newItemForm,
                              budgetLineCode: e.target.value,
                            })
                          }
                          placeholder="Line code"
                        />
                      </div>
                      <div className="space-y-1">
                        <Label htmlFor="budgetCategoryName">
                          Budget Category
                        </Label>
                        <Input
                          id="budgetCategoryName"
                          className="h-9"
                          value={newItemForm.budgetCategoryName || ''}
                          onChange={(e) =>
                            setNewItemForm({
                              ...newItemForm,
                              budgetCategoryName: e.target.value,
                            })
                          }
                          placeholder="Category"
                        />
                      </div>
                      <div className="space-y-1">
                        <Label htmlFor="approvedBudgetAmount">
                          Approved Budget
                        </Label>
                        <Input
                          id="approvedBudgetAmount"
                          type="number"
                          min={0}
                          step={0.01}
                          className="h-9"
                          value={newItemForm.approvedBudgetAmount ?? ''}
                          onChange={(e) =>
                            setNewItemForm({
                              ...newItemForm,
                              approvedBudgetAmount: e.target.value
                                ? parseFloat(e.target.value) || 0
                                : undefined,
                            })
                          }
                          placeholder="0.00"
                        />
                      </div>
                    </div>
                    <div className="space-y-1">
                      <Label>Market Analysis</Label>
                      <Select
                        value={newItemForm.marketAnalysisId || 'none'}
                        onValueChange={handleMarketAnalysisSelect}
                      >
                        <SelectTrigger className="h-9">
                          <SelectValue
                            placeholder={
                              loadingMarketAnalyses
                                ? 'Loading analyses...'
                                : 'Select market analysis'
                            }
                          />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="none">
                            No linked analysis
                          </SelectItem>
                          {marketAnalyses.map((analysis) => (
                            <SelectItem key={analysis.id} value={analysis.id}>
                              {analysis.title} -{' '}
                              {formatCurrency(
                                analysis.currentMarketPrice,
                                analysis.currency
                              )}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="grid grid-cols-2 gap-4">
                      <div className="space-y-1">
                        <Label htmlFor="specifications">Specifications</Label>
                        <Textarea
                          id="specifications"
                          value={newItemForm.specifications || ''}
                          onChange={(e) =>
                            setNewItemForm({
                              ...newItemForm,
                              specifications: e.target.value,
                            })
                          }
                          placeholder="Enter specifications"
                          rows={2}
                          className="min-h-[68px]"
                        />
                      </div>
                      <div className="space-y-1">
                        <Label htmlFor="justification">Justification</Label>
                        <Textarea
                          id="justification"
                          value={newItemForm.justification || ''}
                          onChange={(e) =>
                            setNewItemForm({
                              ...newItemForm,
                              justification: e.target.value,
                            })
                          }
                          placeholder="Enter justification"
                          rows={2}
                          className="min-h-[68px]"
                        />
                      </div>
                    </div>
                    <div className="flex items-center justify-between rounded-md border bg-gray-50 px-3 py-2">
                      <div>
                        <p className="text-xs text-gray-500">
                          Current item cost
                        </p>
                        <p className="font-semibold">
                          {formatCurrency(
                            getItemEstimatedTotal(newItemForm),
                            plan?.currency || 'USD'
                          )}
                        </p>
                      </div>
                      <div className="flex gap-2">
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={resetCurrentPlanItem}
                        >
                          Clear
                        </Button>
                        <Button type="button" size="sm" onClick={handleAddItem}>
                          <Plus className="h-4 w-4 mr-2" />
                          Add to List
                        </Button>
                      </div>
                    </div>
                  </div>
                </div>

                <div className="space-y-3 lg:border-l lg:pl-4">
                  <div className="space-y-2 rounded-md border p-3">
                    <div className="flex items-center justify-between">
                      <h4 className="font-medium">Items to Save</h4>
                      <Badge variant="secondary">
                        {pendingPlanItems.length}
                      </Badge>
                    </div>
                    <div className="h-[160px] overflow-y-auto rounded-md border">
                      {pendingPlanItems.length === 0 ? (
                        <div className="flex h-full items-center justify-center px-4 text-center text-sm text-gray-500">
                          Added items will appear here before saving to the
                          plan.
                        </div>
                      ) : (
                        <div className="divide-y">
                          {pendingPlanItems.map((item, index) => (
                            <div
                              key={`${item.itemDescription}-${index}`}
                              className="flex items-start gap-2 p-2.5"
                            >
                              <div className="min-w-0 flex-1">
                                <div className="truncate text-sm font-medium">
                                  {item.itemDescription}
                                </div>
                                <div className="text-xs text-gray-500">
                                  {item.estimatedQuantity}{' '}
                                  {item.unitOfMeasure || 'EA'} |{' '}
                                  {formatCurrency(
                                    getItemEstimatedTotal(item),
                                    plan?.currency || 'USD'
                                  )}
                                </div>
                                {item.itemSuppliers?.length ? (
                                  <div className="text-xs text-gray-500">
                                    {item.itemSuppliers.length} supplier(s)
                                  </div>
                                ) : null}
                              </div>
                              <Button
                                type="button"
                                variant="ghost"
                                size="icon"
                                className="h-7 w-7 shrink-0"
                                onClick={() =>
                                  handleRemovePendingPlanItem(index)
                                }
                              >
                                <Trash2 className="h-3.5 w-3.5 text-red-500" />
                              </Button>
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                    <div className="flex items-center justify-between text-sm">
                      <span className="text-gray-500">Batch total</span>
                      <span className="font-semibold">
                        {formatCurrency(
                          pendingPlanItemsTotal,
                          plan?.currency || 'USD'
                        )}
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            </TabsContent>

            <TabsContent
              value="suppliers"
              className="mt-3 min-h-0 overflow-y-auto pr-1"
            >
              <div className="grid grid-cols-1 lg:grid-cols-[minmax(280px,0.85fr)_minmax(460px,1.15fr)] gap-4">
                <div className="space-y-2 rounded-md border p-3">
                  <h4 className="font-medium flex items-center gap-2">
                    <Users className="h-4 w-4" />
                    Preferred Suppliers
                  </h4>
                  <div className="relative">
                    <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
                    <Input
                      placeholder="Search suppliers..."
                      value={supplierSearchTerm}
                      onChange={(e) => setSupplierSearchTerm(e.target.value)}
                      className="h-9 pl-9"
                    />
                  </div>
                  <div className="h-[110px] overflow-y-auto border rounded-md">
                    {loadingSuppliers ? (
                      <div className="flex items-center justify-center h-full">
                        <Loader2 className="h-5 w-5 animate-spin" />
                      </div>
                    ) : filteredSuppliers.length === 0 ? (
                      <div className="flex items-center justify-center h-full text-gray-500 text-sm">
                        No suppliers found
                      </div>
                    ) : (
                      <div className="divide-y">
                        {filteredSuppliers.slice(0, 20).map((supplier) => (
                          <div
                            key={supplier.id}
                            className="p-2 cursor-pointer hover:bg-gray-50 flex items-center justify-between"
                            onClick={() => handleAddSupplierToItem(supplier)}
                          >
                            <div>
                              <div className="font-medium text-sm">
                                {supplier.partnerName}
                              </div>
                              <div className="text-xs text-gray-500">
                                {supplier.partnerCode}
                              </div>
                            </div>
                            <Plus className="h-4 w-4 text-green-500" />
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </div>

                <div className="space-y-2 rounded-md border p-3">
                  <Label>
                    Selected Suppliers ({selectedItemSuppliers.length})
                  </Label>
                  <div className="h-[360px] overflow-y-auto border rounded-md">
                    {selectedItemSuppliers.length === 0 ? (
                      <div className="flex items-center justify-center h-full text-gray-500 text-sm">
                        No suppliers selected
                      </div>
                    ) : (
                      <Table>
                        <TableHeader>
                          <TableRow>
                            <TableHead className="text-xs">Supplier</TableHead>
                            <TableHead className="text-xs w-[60px]">
                              Preferred
                            </TableHead>
                            <TableHead className="text-xs w-[80px]">
                              Quote
                            </TableHead>
                            <TableHead className="text-xs w-[40px]"></TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {selectedItemSuppliers.map((itemSupplier) => {
                            const supplier = suppliers.find(
                              (s) => s.id === itemSupplier.supplierId
                            );
                            return (
                              <TableRow key={itemSupplier.supplierId}>
                                <TableCell className="py-1">
                                  <div className="text-xs font-medium">
                                    {supplier?.partnerName || 'Unknown'}
                                  </div>
                                  <div className="text-xs text-gray-500">
                                    {supplier?.partnerCode}
                                  </div>
                                </TableCell>
                                <TableCell className="py-1">
                                  <input
                                    type="checkbox"
                                    checked={itemSupplier.isPreferred || false}
                                    onChange={(e) =>
                                      handleUpdateItemSupplier(
                                        itemSupplier.supplierId,
                                        'isPreferred',
                                        e.target.checked
                                      )
                                    }
                                    className="h-4 w-4"
                                  />
                                </TableCell>
                                <TableCell className="py-1">
                                  <Input
                                    type="number"
                                    min={0}
                                    step={0.01}
                                    value={itemSupplier.quotedUnitPrice || ''}
                                    onChange={(e) =>
                                      handleUpdateItemSupplier(
                                        itemSupplier.supplierId,
                                        'quotedUnitPrice',
                                        parseFloat(e.target.value) || undefined
                                      )
                                    }
                                    className="h-7 text-xs w-[70px]"
                                    placeholder="0.00"
                                  />
                                </TableCell>
                                <TableCell className="py-1">
                                  <Button
                                    variant="ghost"
                                    size="icon"
                                    className="h-6 w-6"
                                    onClick={() =>
                                      handleRemoveSupplierFromItem(
                                        itemSupplier.supplierId
                                      )
                                    }
                                  >
                                    <Trash2 className="h-3 w-3 text-red-500" />
                                  </Button>
                                </TableCell>
                              </TableRow>
                            );
                          })}
                        </TableBody>
                      </Table>
                    )}
                  </div>
                </div>
              </div>
            </TabsContent>
          </Tabs>

          <DialogFooter className="pt-3 border-t gap-2 sm:justify-between">
            <div className="text-xs text-gray-500 text-left">
              Item unit costs are saved using the plan currency:{' '}
              {plan?.currency || 'USD'}.
            </div>
            <div className="flex flex-col-reverse sm:flex-row gap-2">
              <Button
                variant="outline"
                onClick={() => setAddItemDialogOpen(false)}
              >
                Cancel
              </Button>
              <Button
                onClick={handleSavePendingItems}
                disabled={addingItem || itemBatchSaveCount === 0}
              >
                {addingItem ? (
                  <>
                    <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                    Saving...
                  </>
                ) : (
                  <>
                    <Plus className="h-4 w-4 mr-2" />
                    Save {itemBatchSaveCount} Item
                    {itemBatchSaveCount === 1 ? '' : 's'}
                  </>
                )}
              </Button>
            </div>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Publish Dialog */}
      <Dialog open={publishDialogOpen} onOpenChange={setPublishDialogOpen}>
        <DialogContent className="sm:max-w-[520px]">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <FileCheck className="h-5 w-5 text-green-600" />
              Publish to Procurement Execution
            </DialogTitle>
            <DialogDescription>
              Publishing makes this approved plan active for tender, RFQ, and
              purchase order execution.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4 py-4">
            <div className="rounded-md border bg-gray-50 p-3 text-sm">
              <div className="flex justify-between gap-4">
                <span className="text-gray-500">Plan</span>
                <span className="font-medium text-right">
                  {plan?.planNumber}
                </span>
              </div>
              <div className="flex justify-between gap-4 mt-2">
                <span className="text-gray-500">Items</span>
                <span className="font-medium">{plan?.items?.length || 0}</span>
              </div>
              <div className="flex justify-between gap-4 mt-2">
                <span className="text-gray-500">Budget</span>
                <span className="font-medium">
                  {plan
                    ? formatCurrency(
                        plan.approvedBudget || plan.totalEstimatedBudget,
                        plan.currency
                      )
                    : '-'}
                </span>
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="publishComments">Publish Comments</Label>
              <Textarea
                id="publishComments"
                value={publishComments}
                onChange={(e) => setPublishComments(e.target.value)}
                placeholder="Optional notes for the execution handoff"
                rows={3}
              />
            </div>
          </div>

          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setPublishDialogOpen(false)}
              disabled={publishLoading}
            >
              Cancel
            </Button>
            <Button onClick={handlePublishPlan} disabled={publishLoading}>
              {publishLoading ? (
                <>
                  <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                  Publishing...
                </>
              ) : (
                <>
                  <FileCheck className="h-4 w-4 mr-2" />
                  Publish Plan
                </>
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Amendment Dialog */}
      <Dialog open={amendmentDialogOpen} onOpenChange={setAmendmentDialogOpen}>
        <DialogContent className="sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <GitBranch className="h-5 w-5 text-blue-600" />
              Create Plan Amendment
            </DialogTitle>
            <DialogDescription>
              This creates a new draft revision copied from the current plan for
              controlled changes and approval.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4 py-4">
            <div className="space-y-2">
              <Label htmlFor="amendmentTitle">Amendment Title</Label>
              <Input
                id="amendmentTitle"
                value={amendmentTitle}
                onChange={(e) => setAmendmentTitle(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="amendmentDescription">Description</Label>
              <Textarea
                id="amendmentDescription"
                value={amendmentDescription}
                onChange={(e) => setAmendmentDescription(e.target.value)}
                rows={3}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="amendmentReason">Reason *</Label>
              <Textarea
                id="amendmentReason"
                value={amendmentReason}
                onChange={(e) => setAmendmentReason(e.target.value)}
                placeholder="Explain why this amendment is required"
                rows={4}
              />
            </div>
          </div>

          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setAmendmentDialogOpen(false)}
              disabled={amendmentLoading}
            >
              Cancel
            </Button>
            <Button
              onClick={handleCreateAmendment}
              disabled={amendmentLoading || !amendmentReason.trim()}
            >
              {amendmentLoading ? (
                <>
                  <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                  Creating...
                </>
              ) : (
                <>
                  <GitBranch className="h-4 w-4 mr-2" />
                  Create Draft Amendment
                </>
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={executionAction !== null}
        onOpenChange={(open) => {
          if (!open && !executionLoading) {
            setExecutionAction(null);
            setExecutionItemIds([]);
          }
        }}
        title={`${executionAction === 'pr' ? 'Create or open' : 'Create'} ${executionActionLabel(executionAction)}${executionItemIds.length === 1 ? '' : 's'}?`}
        description={
          <div className="space-y-2">
            <p>
              {executionItemIds.length} selected plan item
              {executionItemIds.length === 1 ? '' : 's'} will be processed.
              {executionAction === 'pr'
                ? ' Compatible items will be combined as lines on one Purchase Requisition; different budgets or categories create separate requisitions.'
                : ' Each item keeps its controlled source lineage.'}
            </p>
            {executionAction !== 'pr' && (
              <p>
                The action proceeds only where the linked Purchase Requisition
                is Approved and its effective policy selects that route. The
                sourcing-release audit record is generated automatically.
              </p>
            )}
            {executionAction === 'po' && (
              <p>
                A Purchase Order is created automatically only when exactly one
                approved award, contract, or exception source is available.
              </p>
            )}
          </div>
        }
        confirmText={`Process ${executionItemIds.length} item${executionItemIds.length === 1 ? '' : 's'}`}
        onConfirm={executePlanItems}
        isLoading={executionLoading}
        maxWidth="640px"
      >
        {executionAction === 'pr' && (
          <div className="space-y-2">
            <Label>Requisition type for the selected items</Label>
            <Select
              value={bulkRequisitionType}
              onValueChange={(value) =>
                setBulkRequisitionType(value as PurchaseRequisitionType)
              }
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="StockReplenishment">
                  Stock replenishment
                </SelectItem>
                <SelectItem value="CapitalPurchase">
                  Capital purchase
                </SelectItem>
                <SelectItem value="ProjectPurchase">
                  Project purchase
                </SelectItem>
                <SelectItem value="ServiceProcurement">
                  Service procurement
                </SelectItem>
                <SelectItem value="EmergencyPurchase">
                  Emergency purchase
                </SelectItem>
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">
              Draft PRs inherit the department, approved budget, currency, item
              and preferred supplier from the plan. They remain editable before
              submission.
            </p>
          </div>
        )}
      </ConfirmationDialog>

      <ConfirmationDialog
        open={itemIdToDelete !== null}
        onOpenChange={(open) => {
          if (!open && !deletingItemId) setItemIdToDelete(null);
        }}
        title="Delete procurement plan item?"
        description="This is allowed only while the plan remains a draft. The item cannot be recovered after deletion."
        confirmText="Delete item"
        variant="destructive"
        onConfirm={confirmDeleteItem}
        isLoading={Boolean(deletingItemId)}
      />
    </div>
  );
}
