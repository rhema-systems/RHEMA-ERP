'use client';

import { useState, useEffect } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { ArrowLeft, Edit, FileText, Package, Calendar, Clock, CheckCircle, XCircle, AlertCircle, Send, Loader2, Plus, Trash2, Search, Users, History, GitBranch, ClipboardPlus } from 'lucide-react';
import { toast } from 'sonner';
import { procurementBudgetService, procurementPlanService, commonService, marketAnalysisService, type ProcurementBudgetDetailDto, type ProcurementPlanDetailDto, type ProcurementPlanDto, type CreateProcurementPlanItemDto, type InventoryItemDto, type ProcurementPlanItemDto, type CreateProcurementPlanItemSupplierDto, type MarketAnalysisDto } from '@/services/procurementPlanningService';
import { businessPartnerService, type BusinessPartnerDto } from '@/services/businessPartnerService';
import { inventoryManagementService, type UnitOfMeasureDto } from '@/services/inventoryManagementService';
import { format } from 'date-fns';
import { FileCheck } from 'lucide-react';
import { Checkbox } from '@/components/ui/checkbox';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { WorkflowApprovalActions, useWorkflowRecord } from '@/components/workflow';
import { WorkflowTabContent, WorkflowTabTrigger } from '@/components/workflow/WorkflowRecordTab';
import { ProcurementPlanItemDialogBody } from '@/app/procurement/planning/components/ProcurementPlanItemDialogBody';

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
  procurementMethod: 'DirectPurchase',
  notes: '',
  itemSuppliers: [],
});

const getItemEstimatedTotal = (item: Pick<CreateProcurementPlanItemDto, 'estimatedQuantity' | 'estimatedUnitPrice'>) =>
  (Number(item.estimatedQuantity) || 0) * (Number(item.estimatedUnitPrice) || 0);

const normalizeLookup = (value?: string | null) => value?.trim().toLowerCase() || '';

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

const getUnitOfMeasureValue = (unit: UnitOfMeasureDto) => unit.code?.trim() || unit.name?.trim() || unit.id;

const getUnitOfMeasureLabel = (unit: UnitOfMeasureDto) => {
  const value = getUnitOfMeasureValue(unit);
  const name = unit.name?.trim();
  const symbol = unit.symbol?.trim();

  if (name && name !== value) return symbol ? `${value} - ${name} (${symbol})` : `${value} - ${name}`;
  return symbol && symbol !== value ? `${value} (${symbol})` : value;
};

const buildUnitOfMeasureOptions = (units: UnitOfMeasureDto[], currentValue?: string) => {
  const seen = new Set<string>();
  const options = units
    .map((unit) => ({ value: getUnitOfMeasureValue(unit), label: getUnitOfMeasureLabel(unit) }))
    .filter((option) => {
      if (!option.value || seen.has(option.value)) return false;
      seen.add(option.value);
      return true;
    });

  const savedValue = currentValue?.trim();
  if (savedValue && !seen.has(savedValue)) {
    options.unshift({ value: savedValue, label: `${savedValue} (saved value)` });
  }

  return options;
};

export default function ProcurementPlanDetailPage() {
  const params = useParams();
  const router = useRouter();
  const planId = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';

  const [plan, setPlan] = useState<ProcurementPlanDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [linkedBudget, setLinkedBudget] = useState<ProcurementBudgetDetailDto | null>(null);
  const [loadingBudgetAllocations, setLoadingBudgetAllocations] = useState(false);

  // Add Item Dialog State
  const [addItemDialogOpen, setAddItemDialogOpen] = useState(false);
  const [inventoryItems, setInventoryItems] = useState<InventoryItemDto[]>([]);
  const [loadingInventory, setLoadingInventory] = useState(false);
  const [inventorySearchTerm, setInventorySearchTerm] = useState('');
  const [selectedInventoryItem, setSelectedInventoryItem] = useState<InventoryItemDto | null>(null);
  const [addingItem, setAddingItem] = useState(false);
  const [deletingItemId, setDeletingItemId] = useState<string | null>(null);
  const [itemIdToDelete, setItemIdToDelete] = useState<string | null>(null);
  const [newItemForm, setNewItemForm] = useState<CreateProcurementPlanItemDto>(createEmptyItemForm);
  const [pendingPlanItems, setPendingPlanItems] = useState<CreateProcurementPlanItemDto[]>([]);

  // Supplier selection state
  const [suppliers, setSuppliers] = useState<BusinessPartnerDto[]>([]);
  const [loadingSuppliers, setLoadingSuppliers] = useState(false);
  const [supplierSearchTerm, setSupplierSearchTerm] = useState('');
  const [selectedItemSuppliers, setSelectedItemSuppliers] = useState<CreateProcurementPlanItemSupplierDto[]>([]);
  const [marketAnalyses, setMarketAnalyses] = useState<MarketAnalysisDto[]>([]);
  const [loadingMarketAnalyses, setLoadingMarketAnalyses] = useState(false);
  const [unitsOfMeasure, setUnitsOfMeasure] = useState<UnitOfMeasureDto[]>([]);
  const [loadingUnitsOfMeasure, setLoadingUnitsOfMeasure] = useState(false);

  const [publishDialogOpen, setPublishDialogOpen] = useState(false);
  const [publishComments, setPublishComments] = useState('');
  const [publishLoading, setPublishLoading] = useState(false);
  const [versionHistory, setVersionHistory] = useState<ProcurementPlanDto[]>([]);
  const [loadingVersions, setLoadingVersions] = useState(false);
  const [amendmentDialogOpen, setAmendmentDialogOpen] = useState(false);
  const [amendmentReason, setAmendmentReason] = useState('');
  const [amendmentTitle, setAmendmentTitle] = useState('');
  const [amendmentDescription, setAmendmentDescription] = useState('');
  const [amendmentLoading, setAmendmentLoading] = useState(false);

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
      if (data.budgetId) {
        try {
          setLoadingBudgetAllocations(true);
          setLinkedBudget(await procurementBudgetService.getBudgetById(data.budgetId));
        } catch (budgetError) {
          console.error('Error loading linked budget allocations:', budgetError);
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
      const response = await marketAnalysisService.getAnalyses({ page: 1, pageSize: 100, status: 'Published' });
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
    if (selectedItemSuppliers.some(s => s.supplierId === supplier.id)) {
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
      .filter(s => s.supplierId !== supplierId)
      .map((s, index) => ({ ...s, priority: index + 1 }));
    setSelectedItemSuppliers(updatedSuppliers);
    setNewItemForm({ ...newItemForm, itemSuppliers: updatedSuppliers });
  };

  const handleUpdateItemSupplier = (supplierId: string, field: keyof CreateProcurementPlanItemSupplierDto, value: unknown) => {
    const updatedSuppliers = selectedItemSuppliers.map(s => {
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
      itemDescription: newItemForm.itemDescription || analysis?.itemDescription || analysis?.title || '',
      estimatedUnitPrice: newItemForm.estimatedUnitPrice && newItemForm.estimatedUnitPrice > 0
        ? newItemForm.estimatedUnitPrice
        : analysis?.currentMarketPrice || 0,
    });
  };

  const filteredSuppliers = suppliers.filter(s =>
    (s.partnerName || '').toLowerCase().includes(supplierSearchTerm.toLowerCase()) ||
    (s.partnerCode || '').toLowerCase().includes(supplierSearchTerm.toLowerCase())
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
    if (Number(item.estimatedQuantity) <= 0) return 'Quantity must be greater than 0';
    if (Number(item.estimatedUnitPrice) < 0) return 'Unit cost cannot be negative';
    const itemBudgetAmount = item.approvedBudgetAmount ?? getItemEstimatedTotal(item);
    if (itemBudgetAmount < 0) return 'Item budget amount cannot be negative';

    if (linkedBudget?.allocations.length) {
      const allocation = linkedBudget.allocations.find(value => value.id === item.procurementBudgetAllocationId);
      if (!allocation) return 'Select a budget allocation from the linked approved budget';

      const savedExposure = (plan?.items || [])
        .filter(value => value.procurementBudgetAllocationId === allocation.id)
        .reduce((total, value) => total + (value.approvedBudgetAmount ?? value.estimatedTotalCost), 0);
      const queuedExposure = pendingPlanItems
        .filter(value => value.procurementBudgetAllocationId === allocation.id)
        .reduce((total, value) => total + (value.approvedBudgetAmount ?? getItemEstimatedTotal(value)), 0);
      if (savedExposure + queuedExposure + itemBudgetAmount > allocation.remainingAmount) {
        return `${allocation.categoryName} has insufficient remaining allocation for this item`;
      }
    }

    const savedPlanExposure = (plan?.items || [])
      .reduce((total, value) => total + (value.approvedBudgetAmount ?? value.estimatedTotalCost), 0);
    const queuedPlanExposure = pendingPlanItems
      .reduce((total, value) => total + (value.approvedBudgetAmount ?? getItemEstimatedTotal(value)), 0);
    if (plan && savedPlanExposure + queuedPlanExposure + itemBudgetAmount > plan.totalEstimatedBudget) {
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
    approvedBudgetAmount: newItemForm.approvedBudgetAmount !== undefined ? Number(newItemForm.approvedBudgetAmount) || 0 : undefined,
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
    setPendingPlanItems((items) => items.filter((_, itemIndex) => itemIndex !== index));
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
      toast.success(`${itemsToSave.length} item${itemsToSave.length === 1 ? '' : 's'} added successfully`);
      setPendingPlanItems([]);
      resetCurrentPlanItem();
      setAddItemDialogOpen(false);
      loadPlanDetails(); // Refresh plan data
    } catch (error) {
      console.error('Error adding items:', error);
      toast.error(error instanceof Error ? error.message : 'Failed to save plan items');
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
      toast.error(error instanceof Error ? error.message : 'Failed to delete item');
      return false;
    } finally {
      setDeletingItemId(null);
    }
  };

  const inventorySearchQuery = inventorySearchTerm.trim().toLowerCase();
  const filteredInventoryItems = inventorySearchQuery.length >= 2
    ? inventoryItems.filter(item =>
        (item.name || '').toLowerCase().includes(inventorySearchQuery) ||
        (item.itemCode || '').toLowerCase().includes(inventorySearchQuery) ||
        (item.categoryName || '').toLowerCase().includes(inventorySearchQuery)
      )
    : [];

  const getStatusBadge = (status: string) => {
    const statusConfig: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline'; icon: React.ReactNode }> = {
      Draft: { variant: 'secondary', icon: <FileText className="h-3 w-3 mr-1" /> },
      Submitted: { variant: 'outline', icon: <Send className="h-3 w-3 mr-1" /> },
      UnderReview: { variant: 'default', icon: <Clock className="h-3 w-3 mr-1" /> },
      Approved: { variant: 'default', icon: <CheckCircle className="h-3 w-3 mr-1" /> },
      Rejected: { variant: 'destructive', icon: <XCircle className="h-3 w-3 mr-1" /> },
      Active: { variant: 'default', icon: <CheckCircle className="h-3 w-3 mr-1" /> },
      Completed: { variant: 'secondary', icon: <CheckCircle className="h-3 w-3 mr-1" /> },
      Cancelled: { variant: 'destructive', icon: <XCircle className="h-3 w-3 mr-1" /> },
    };
    const config = statusConfig[status] || { variant: 'secondary' as const, icon: null };
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
    return <Badge className={colors[priority] || 'bg-gray-100 text-gray-800'}>{priority}</Badge>;
  };

  const formatCurrency = (amount: number, currency: string = 'USD') => {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency }).format(amount);
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return '-';
    try {
      return format(new Date(dateString), 'MMM dd, yyyy');
    } catch {
      return dateString;
    }
  };

  const itemBatchSaveCount = pendingPlanItems.length + (hasCurrentItemDraft() ? 1 : 0);
  const pendingPlanItemsTotal = pendingPlanItems.reduce((total, item) => total + getItemEstimatedTotal(item), 0);
  const unitOfMeasureOptions = buildUnitOfMeasureOptions(unitsOfMeasure, newItemForm.unitOfMeasure);

  const workflow = useWorkflowRecord({
    entityType: 'ProcurementPlan',
    entityId: planId,
    entityLabel: 'Procurement Plan',
    entityNumber: plan?.planNumber,
    status: plan?.status ?? '',
    canSubmit: plan?.status === 'Draft',
    canApproveReject: plan?.status === 'Submitted' || plan?.status === 'UnderReview',
    enabled: Boolean(plan),
    commands: {
      submit: async () => { await procurementPlanService.submitForApproval(planId, { comments: '' }); },
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
      toast.error(error instanceof Error ? error.message : 'Failed to publish procurement plan');
    } finally {
      setPublishLoading(false);
    }
  };

  const canAmendPlan = plan && ['Approved', 'Active', 'Completed'].includes(plan.status);

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
      toast.error(error instanceof Error ? error.message : 'Failed to create amendment');
    } finally {
      setAmendmentLoading(false);
    }
  };

  const canCreateRequisition = (item: ProcurementPlanItemDto) => {
    return (plan?.status === 'Approved' || plan?.status === 'Active') &&
           (item.status === 'Approved' || item.status === 'Planned') &&
           Boolean(item.procurementBudgetId || plan.budgetId) &&
           !item.tenderId &&
           !item.purchaseOrderId;
  };

  const getItemStatusBadge = (status: string) => {
    switch (status) {
      case 'Planned':
        return <Badge variant="outline" className="bg-gray-100">Planned</Badge>;
      case 'Approved':
        return <Badge variant="outline" className="bg-blue-100 text-blue-800">Approved</Badge>;
      case 'InProgress':
        return <Badge variant="outline" className="bg-yellow-100 text-yellow-800">In Progress</Badge>;
      case 'Procured':
        return <Badge variant="outline" className="bg-green-100 text-green-800">Procured</Badge>;
      case 'Cancelled':
        return <Badge variant="outline" className="bg-red-100 text-red-800">Cancelled</Badge>;
      default:
        return <Badge variant="outline">{status}</Badge>;
    }
  };

  const getProcurementMethodBadge = (method?: string) => {
    switch (method) {
      case 'Tender':
        return <Badge variant="outline" className="bg-purple-100 text-purple-800">Tender</Badge>;
      case 'RFQ':
        return <Badge variant="outline" className="bg-indigo-100 text-indigo-800">RFQ</Badge>;
      case 'DirectPurchase':
        return <Badge variant="outline" className="bg-teal-100 text-teal-800">Direct Purchase</Badge>;
      case 'Contract':
        return <Badge variant="outline" className="bg-orange-100 text-orange-800">Contract</Badge>;
      default:
        return <Badge variant="outline">{method || 'N/A'}</Badge>;
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <Loader2 className="h-12 w-12 animate-spin mx-auto mb-4 text-blue-500" />
          <p className="text-lg text-gray-600">Loading procurement plan details...</p>
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
          <Button onClick={() => router.push('/procurement/planning/plans')} className="mt-4">
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
          <Button variant="ghost" onClick={() => router.push('/procurement/planning/plans')}>
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
            <Button variant="outline" onClick={() => router.push(`/procurement/planning/plans/${planId}/edit`)}>
              <Edit className="h-4 w-4 mr-2" />
              Edit
            </Button>
          )}
          <WorkflowApprovalActions
            {...workflow.actionProps}
            showStepBadge
          />
          {plan.status === 'Approved' && (
            <Button onClick={handleOpenPublishDialog} disabled={publishLoading || !plan.items?.length}>
              {publishLoading ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : <FileCheck className="h-4 w-4 mr-2" />}
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
            <CardTitle className="text-sm font-medium text-gray-500">Total Budget</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{formatCurrency(plan.totalEstimatedBudget, plan.currency)}</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Plan Items</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{plan.itemCount || plan.items?.length || 0}</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Duration</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{plan.planDurationYears} Year(s)</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Department</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-lg font-medium truncate">{plan.departmentName || '-'}</div>
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
          <WorkflowTabTrigger />
        </TabsList>

        <TabsContent value="details" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Plan Information</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-4">
              <div>
                <label className="text-sm font-medium text-gray-500">Title</label>
                <p className="mt-1">{plan.title}</p>
              </div>
              <div>
                <label className="text-sm font-medium text-gray-500">Department</label>
                <p className="mt-1">{plan.departmentName || '-'}</p>
              </div>
              <div>
                <label className="text-sm font-medium text-gray-500">Fiscal Year</label>
                <p className="mt-1">{plan.fiscalYear}</p>
              </div>
              <div>
                <label className="text-sm font-medium text-gray-500">Planning Cycle</label>
                <p className="mt-1">
                  {plan.planningCycle || 'Annual'}{plan.planningQuarter ? ` - ${plan.planningQuarter}` : ''}
                </p>
              </div>
              <div>
                <label className="text-sm font-medium text-gray-500">Status</label>
                <div className="mt-1">{getStatusBadge(plan.status)}</div>
              </div>
              <div>
                <label className="text-sm font-medium text-gray-500">Start Date</label>
                <p className="mt-1">{formatDate(plan.planStartDate)}</p>
              </div>
              <div>
                <label className="text-sm font-medium text-gray-500">End Date</label>
                <p className="mt-1">{formatDate(plan.planEndDate)}</p>
              </div>
              <div>
                <label className="text-sm font-medium text-gray-500">Total Estimated Budget</label>
                <p className="mt-1">{formatCurrency(plan.totalEstimatedBudget, plan.currency)}</p>
              </div>
              <div>
                <label className="text-sm font-medium text-gray-500">Currency</label>
                <p className="mt-1">{plan.currency}</p>
              </div>
              {plan.publishedDate && (
                <>
                  <div>
                    <label className="text-sm font-medium text-gray-500">Published By</label>
                    <p className="mt-1">{plan.publishedByName || '-'}</p>
                  </div>
                  <div>
                    <label className="text-sm font-medium text-gray-500">Published Date</label>
                    <p className="mt-1">{formatDate(plan.publishedDate)}</p>
                  </div>
                </>
              )}
              <div className="col-span-2">
                <label className="text-sm font-medium text-gray-500">Description</label>
                <p className="mt-1">{plan.description || '-'}</p>
              </div>
              {plan.publishComments && (
                <div className="col-span-2">
                  <label className="text-sm font-medium text-gray-500">Publish Comments</label>
                  <p className="mt-1">{plan.publishComments}</p>
                </div>
              )}
              {plan.notes && (
                <div className="col-span-2">
                  <label className="text-sm font-medium text-gray-500">Notes</label>
                  <p className="mt-1">{plan.notes}</p>
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Linked Procurement Budget</CardTitle>
              <CardDescription>
                Approved budget controlling this plan&apos;s funding and currency
              </CardDescription>
            </CardHeader>
            <CardContent>
              {linkedBudget ? (
                <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-4">
                  <div>
                    <label className="text-sm font-medium text-gray-500">Budget</label>
                    <p className="mt-1 font-medium">{linkedBudget.budgetCode} - {linkedBudget.title}</p>
                  </div>
                  <div>
                    <label className="text-sm font-medium text-gray-500">Status</label>
                    <div className="mt-1">{getStatusBadge(linkedBudget.status)}</div>
                  </div>
                  <div>
                    <label className="text-sm font-medium text-gray-500">Allocated Amount</label>
                    <p className="mt-1">{formatCurrency(linkedBudget.allocatedAmount, linkedBudget.currency)}</p>
                  </div>
                  <div>
                    <label className="text-sm font-medium text-gray-500">Remaining Amount</label>
                    <p className="mt-1">{formatCurrency(linkedBudget.remainingAmount, linkedBudget.currency)}</p>
                  </div>
                  <div>
                    <label className="text-sm font-medium text-gray-500">Committed Amount</label>
                    <p className="mt-1">{formatCurrency(linkedBudget.committedAmount, linkedBudget.currency)}</p>
                  </div>
                  <div>
                    <label className="text-sm font-medium text-gray-500">Utilized Amount</label>
                    <p className="mt-1">{formatCurrency(linkedBudget.utilizedAmount, linkedBudget.currency)}</p>
                  </div>
                  <div>
                    <label className="text-sm font-medium text-gray-500">Currency</label>
                    <p className="mt-1">{linkedBudget.currency}</p>
                  </div>
                  <div>
                    <label className="text-sm font-medium text-gray-500">Fiscal Year</label>
                    <p className="mt-1">{linkedBudget.fiscalYear}</p>
                  </div>
                </div>
              ) : (
                <div className="flex items-start gap-3 rounded-md border border-amber-200 bg-amber-50 p-4 text-amber-900">
                  <AlertCircle className="mt-0.5 h-5 w-5 shrink-0" />
                  <div>
                    <p className="font-medium">No procurement budget is linked to this plan.</p>
                    <p className="mt-1 text-sm">Amend the plan and select an approved budget before adding further funding exposure.</p>
                  </div>
                </div>
              )}
            </CardContent>
          </Card>

          {plan.reviewComments && (
            <Card>
              <CardHeader>
                <CardTitle>Review Information</CardTitle>
              </CardHeader>
              <CardContent className="grid grid-cols-2 gap-4">
                <div>
                  <label className="text-sm font-medium text-gray-500">Reviewed By</label>
                  <p className="mt-1">{plan.reviewedByName || '-'}</p>
                </div>
                <div>
                  <label className="text-sm font-medium text-gray-500">Review Date</label>
                  <p className="mt-1">{formatDate(plan.reviewedDate)}</p>
                </div>
                <div className="col-span-2">
                  <label className="text-sm font-medium text-gray-500">Comments</label>
                  <p className="mt-1">{plan.reviewComments}</p>
                </div>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="items">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <div>
                <CardTitle>Plan Items</CardTitle>
                <CardDescription>Items included in this procurement plan</CardDescription>
              </div>
              {plan.status === 'Draft' && (
                <Button onClick={handleOpenAddItemDialog}>
                  <Plus className="h-4 w-4 mr-2" />
                  Add Item
                </Button>
              )}
            </CardHeader>
            <CardContent>
              {plan.items && plan.items.length > 0 ? (
                <Table>
                  <TableHeader>
                    <TableRow>
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
                    {plan.items.map((item) => (
                      <TableRow key={item.id}>
                        <TableCell className="font-medium">
                          <div>
                            {item.itemDescription}
                            {item.tenderId && (
                              <div className="text-xs text-blue-600 mt-1">
                                → Tender created
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
                              <div className="text-xs text-gray-500">Legacy line: {item.budgetLineCode}</div>
                            )}
                          </div>
                        </TableCell>
                        <TableCell>{item.estimatedQuantity} {item.unitOfMeasure}</TableCell>
                        <TableCell>{formatCurrency(item.estimatedUnitPrice, plan.currency)}</TableCell>
                        <TableCell>{formatCurrency(item.estimatedTotalCost, plan.currency)}</TableCell>
                        <TableCell>{getItemStatusBadge(item.status)}</TableCell>
                        <TableCell>{getPriorityBadge(item.priority)}</TableCell>
                        <TableCell>{formatDate(item.requiredDate)}</TableCell>
                        <TableCell>
                          <div className="flex items-center gap-1">
                            {plan.status === 'Draft' && (
                              <Button
                                variant="ghost"
                                size="icon"
                                onClick={() => router.push(`/procurement/planning/plans/${planId}/edit?itemId=${item.id}`)}
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
                            {canCreateRequisition(item) && (
                              <Button
                                variant="outline"
                                size="sm"
                                onClick={() => router.push(`/procurement/purchase-requisitions/new?sourcePlanItemId=${item.id}`)}
                                title="Create Purchase Requisition"
                                aria-label={`Create purchase requisition for ${item.itemDescription}`}
                              >
                                <ClipboardPlus className="mr-2 h-4 w-4 text-blue-600" />
                                Create PR
                              </Button>
                            )}
                          </div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              ) : (
                <div className="text-center py-8 text-gray-500">
                  <Package className="h-12 w-12 mx-auto mb-2 opacity-50" />
                  <p>No items added to this plan yet</p>
                  {plan.status === 'Draft' && (
                    <Button onClick={handleOpenAddItemDialog} variant="outline" className="mt-4">
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
              <CardDescription>Timeline and milestones for this plan</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                <div className="flex items-center gap-4 p-4 border rounded-lg">
                  <Calendar className="h-8 w-8 text-blue-500" />
                  <div>
                    <p className="font-medium">Plan Period</p>
                    <p className="text-sm text-gray-500">
                      {formatDate(plan.planStartDate)} - {formatDate(plan.planEndDate)}
                    </p>
                  </div>
                </div>
                <div className="flex items-center gap-4 p-4 border rounded-lg">
                  <Clock className="h-8 w-8 text-green-500" />
                  <div>
                    <p className="font-medium">Duration</p>
                    <p className="text-sm text-gray-500">{plan.planDurationYears} Year(s)</p>
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
                <CardDescription>Original plan, amendments, and draft revisions linked to this plan.</CardDescription>
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
                <div className="text-center py-8 text-gray-500">No linked versions found.</div>
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
                          <div className="font-medium">{version.planNumber}</div>
                          <div className="text-xs text-gray-500">{version.title}</div>
                        </TableCell>
                        <TableCell>{getStatusBadge(version.status)}</TableCell>
                        <TableCell>{formatCurrency(version.totalEstimatedBudget, version.currency)}</TableCell>
                        <TableCell>{formatDate(version.preparedDate || version.createdAt)}</TableCell>
                        <TableCell>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => router.push(`/procurement/planning/plans/${version.id}`)}
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
                  Select a product, enter the required details, add it to the list, then save the batch.
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
              <TabsTrigger value="suppliers">Suppliers ({selectedItemSuppliers.length})</TabsTrigger>
            </TabsList>
            <TabsContent value="details" className="mt-3 min-h-0">
              <div className="grid grid-cols-1 lg:grid-cols-[minmax(260px,0.85fr)_minmax(460px,1.2fr)_minmax(280px,0.85fr)] gap-4 overflow-y-auto pr-1">
                <div className="space-y-3 lg:border-r lg:pr-4">
              <div className="flex items-center justify-between">
                <h4 className="font-medium">Products</h4>
                <Badge variant="secondary">{filteredInventoryItems.length}</Badge>
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
                          selectedInventoryItem?.id === item.id ? 'bg-blue-50 border-l-4 border-blue-500' : ''
                        }`}
                        onClick={() => handleSelectInventoryItem(item)}
                      >
                        <div className="font-medium text-sm leading-tight">{item.name}</div>
                        <div className="text-xs text-gray-500 mt-1">
                          {item.itemCode} | {item.categoryName || 'No Category'} | {item.unitOfMeasure}
                        </div>
                        <div className="text-xs text-gray-500">
                          Cost: {formatCurrency(item.standardCost || item.averageCost || 0, plan?.currency || 'USD')} | Stock: {item.availableStock}
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
                  Line total: {formatCurrency(getItemEstimatedTotal(newItemForm), plan?.currency || 'USD')}
                </Badge>
              </div>
              <div className="space-y-2">
                <div className="space-y-1">
                  <Label htmlFor="itemDescription">Item Description *</Label>
                  <Input
                    id="itemDescription"
                    className="h-9"
                    value={newItemForm.itemDescription}
                    onChange={(e) => setNewItemForm({ ...newItemForm, itemDescription: e.target.value })}
                    placeholder="Enter item description"
                  />
                </div>
                <div className="grid grid-cols-3 gap-3">
                  <div className="space-y-1">
                    <Label htmlFor="unitOfMeasure">Unit of Measure</Label>
                    <Select
                      value={newItemForm.unitOfMeasure || undefined}
                      onValueChange={(value) => setNewItemForm({ ...newItemForm, unitOfMeasure: value })}
                      disabled={loadingUnitsOfMeasure && unitOfMeasureOptions.length === 0}
                    >
                      <SelectTrigger id="unitOfMeasure" className="h-9">
                        <SelectValue placeholder={loadingUnitsOfMeasure ? 'Loading UOMs...' : 'Select UOM'} />
                      </SelectTrigger>
                      <SelectContent>
                        {unitOfMeasureOptions.map((option) => (
                          <SelectItem key={option.value} value={option.value}>
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
                      onChange={(e) => setNewItemForm({ ...newItemForm, estimatedQuantity: parseFloat(e.target.value) || 0 })}
                    />
                  </div>
                  <div className="space-y-1">
                    <Label htmlFor="estimatedUnitPrice">Unit Cost ({plan?.currency || 'USD'})</Label>
                    <Input
                      id="estimatedUnitPrice"
                      type="number"
                      min={0}
                      step={0.01}
                      className="h-9"
                      value={newItemForm.estimatedUnitPrice || 0}
                      onChange={(e) => setNewItemForm({ ...newItemForm, estimatedUnitPrice: parseFloat(e.target.value) || 0 })}
                    />
                  </div>
                </div>
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-1">
                    <Label htmlFor="priority">Priority</Label>
                    <Select
                      value={newItemForm.priority || 'Medium'}
                      onValueChange={(value) => setNewItemForm({ ...newItemForm, priority: value })}
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
                      onChange={(e) => setNewItemForm({ ...newItemForm, requiredDate: e.target.value })}
                    />
                  </div>
                </div>
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-1">
                    <Label htmlFor="plannedQuarter">Quarter</Label>
                    <Select
                      value={newItemForm.plannedQuarter || ''}
                      onValueChange={(value) => setNewItemForm({ ...newItemForm, plannedQuarter: value })}
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
                  <div className="space-y-1">
                    <Label htmlFor="procurementMethod">Method</Label>
                    <Select
                      value={newItemForm.procurementMethod || 'DirectPurchase'}
                      onValueChange={(value) => setNewItemForm({ ...newItemForm, procurementMethod: value })}
                    >
                      <SelectTrigger className="h-9">
                        <SelectValue placeholder="Select method" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="DirectPurchase">Direct Purchase</SelectItem>
                        <SelectItem value="RFQ">RFQ</SelectItem>
                        <SelectItem value="Tender">Tender</SelectItem>
                        <SelectItem value="Framework">Framework</SelectItem>
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
                      onChange={(e) => setNewItemForm({ ...newItemForm, budgetLineCode: e.target.value })}
                      placeholder="Line code"
                    />
                  </div>
                  <div className="space-y-1">
                    <Label htmlFor="budgetCategoryName">Budget Category</Label>
                    <Input
                      id="budgetCategoryName"
                      className="h-9"
                      value={newItemForm.budgetCategoryName || ''}
                      onChange={(e) => setNewItemForm({ ...newItemForm, budgetCategoryName: e.target.value })}
                      placeholder="Category"
                    />
                  </div>
                  <div className="space-y-1">
                    <Label htmlFor="approvedBudgetAmount">Approved Budget</Label>
                    <Input
                      id="approvedBudgetAmount"
                      type="number"
                      min={0}
                      step={0.01}
                      className="h-9"
                      value={newItemForm.approvedBudgetAmount ?? ''}
                      onChange={(e) => setNewItemForm({
                        ...newItemForm,
                        approvedBudgetAmount: e.target.value ? parseFloat(e.target.value) || 0 : undefined,
                      })}
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
                      <SelectValue placeholder={loadingMarketAnalyses ? 'Loading analyses...' : 'Select market analysis'} />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No linked analysis</SelectItem>
                      {marketAnalyses.map((analysis) => (
                        <SelectItem key={analysis.id} value={analysis.id}>
                          {analysis.title} - {formatCurrency(analysis.currentMarketPrice, analysis.currency)}
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
                      onChange={(e) => setNewItemForm({ ...newItemForm, specifications: e.target.value })}
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
                      onChange={(e) => setNewItemForm({ ...newItemForm, justification: e.target.value })}
                      placeholder="Enter justification"
                      rows={2}
                      className="min-h-[68px]"
                    />
                  </div>
                </div>
                <div className="flex items-center justify-between rounded-md border bg-gray-50 px-3 py-2">
                  <div>
                    <p className="text-xs text-gray-500">Current item cost</p>
                    <p className="font-semibold">{formatCurrency(getItemEstimatedTotal(newItemForm), plan?.currency || 'USD')}</p>
                  </div>
                  <div className="flex gap-2">
                    <Button type="button" variant="outline" size="sm" onClick={resetCurrentPlanItem}>
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
                  <Badge variant="secondary">{pendingPlanItems.length}</Badge>
                </div>
                <div className="h-[160px] overflow-y-auto rounded-md border">
                  {pendingPlanItems.length === 0 ? (
                    <div className="flex h-full items-center justify-center px-4 text-center text-sm text-gray-500">
                      Added items will appear here before saving to the plan.
                    </div>
                  ) : (
                    <div className="divide-y">
                      {pendingPlanItems.map((item, index) => (
                        <div key={`${item.itemDescription}-${index}`} className="flex items-start gap-2 p-2.5">
                          <div className="min-w-0 flex-1">
                            <div className="truncate text-sm font-medium">{item.itemDescription}</div>
                            <div className="text-xs text-gray-500">
                              {item.estimatedQuantity} {item.unitOfMeasure || 'EA'} | {formatCurrency(getItemEstimatedTotal(item), plan?.currency || 'USD')}
                            </div>
                            {item.itemSuppliers?.length ? (
                              <div className="text-xs text-gray-500">{item.itemSuppliers.length} supplier(s)</div>
                            ) : null}
                          </div>
                          <Button
                            type="button"
                            variant="ghost"
                            size="icon"
                            className="h-7 w-7 shrink-0"
                            onClick={() => handleRemovePendingPlanItem(index)}
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
                    {formatCurrency(pendingPlanItemsTotal, plan?.currency || 'USD')}
                  </span>
                </div>
                  </div>
                </div>
              </div>
            </TabsContent>

            <TabsContent value="suppliers" className="mt-3 min-h-0 overflow-y-auto pr-1">
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
                            <div className="font-medium text-sm">{supplier.partnerName}</div>
                            <div className="text-xs text-gray-500">{supplier.partnerCode}</div>
                          </div>
                          <Plus className="h-4 w-4 text-green-500" />
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                </div>

                <div className="space-y-2 rounded-md border p-3">
                <Label>Selected Suppliers ({selectedItemSuppliers.length})</Label>
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
                          <TableHead className="text-xs w-[60px]">Preferred</TableHead>
                          <TableHead className="text-xs w-[80px]">Quote</TableHead>
                          <TableHead className="text-xs w-[40px]"></TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {selectedItemSuppliers.map((itemSupplier) => {
                          const supplier = suppliers.find(s => s.id === itemSupplier.supplierId);
                          return (
                            <TableRow key={itemSupplier.supplierId}>
                              <TableCell className="py-1">
                                <div className="text-xs font-medium">{supplier?.partnerName || 'Unknown'}</div>
                                <div className="text-xs text-gray-500">{supplier?.partnerCode}</div>
                              </TableCell>
                              <TableCell className="py-1">
                                <input
                                  type="checkbox"
                                  checked={itemSupplier.isPreferred || false}
                                  onChange={(e) => handleUpdateItemSupplier(itemSupplier.supplierId, 'isPreferred', e.target.checked)}
                                  className="h-4 w-4"
                                />
                              </TableCell>
                              <TableCell className="py-1">
                                <Input
                                  type="number"
                                  min={0}
                                  step={0.01}
                                  value={itemSupplier.quotedUnitPrice || ''}
                                  onChange={(e) => handleUpdateItemSupplier(itemSupplier.supplierId, 'quotedUnitPrice', parseFloat(e.target.value) || undefined)}
                                  className="h-7 text-xs w-[70px]"
                                  placeholder="0.00"
                                />
                              </TableCell>
                              <TableCell className="py-1">
                                <Button
                                  variant="ghost"
                                  size="icon"
                                  className="h-6 w-6"
                                  onClick={() => handleRemoveSupplierFromItem(itemSupplier.supplierId)}
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
              Item unit costs are saved using the plan currency: {plan?.currency || 'USD'}.
            </div>
            <div className="flex flex-col-reverse sm:flex-row gap-2">
              <Button variant="outline" onClick={() => setAddItemDialogOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleSavePendingItems} disabled={addingItem || itemBatchSaveCount === 0}>
                {addingItem ? (
                  <>
                    <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                    Saving...
                  </>
                ) : (
                  <>
                    <Plus className="h-4 w-4 mr-2" />
                    Save {itemBatchSaveCount} Item{itemBatchSaveCount === 1 ? '' : 's'}
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
              Publishing makes this approved plan active for tender, RFQ, and purchase order execution.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4 py-4">
            <div className="rounded-md border bg-gray-50 p-3 text-sm">
              <div className="flex justify-between gap-4">
                <span className="text-gray-500">Plan</span>
                <span className="font-medium text-right">{plan?.planNumber}</span>
              </div>
              <div className="flex justify-between gap-4 mt-2">
                <span className="text-gray-500">Items</span>
                <span className="font-medium">{plan?.items?.length || 0}</span>
              </div>
              <div className="flex justify-between gap-4 mt-2">
                <span className="text-gray-500">Budget</span>
                <span className="font-medium">
                  {plan ? formatCurrency(plan.approvedBudget || plan.totalEstimatedBudget, plan.currency) : '-'}
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
            <Button variant="outline" onClick={() => setPublishDialogOpen(false)} disabled={publishLoading}>
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
              This creates a new draft revision copied from the current plan for controlled changes and approval.
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
            <Button variant="outline" onClick={() => setAmendmentDialogOpen(false)} disabled={amendmentLoading}>
              Cancel
            </Button>
            <Button onClick={handleCreateAmendment} disabled={amendmentLoading || !amendmentReason.trim()}>
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
        open={itemIdToDelete !== null}
        onOpenChange={(open) => { if (!open && !deletingItemId) setItemIdToDelete(null); }}
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
