'use client';

import React, { useState, useEffect, useRef } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { ArrowLeft, Save, Loader2, Plus, Trash2, Search, Package, Pencil, Users } from 'lucide-react';
import { toast } from 'sonner';
import { procurementBudgetService, procurementPlanService, commonService, marketAnalysisService, type UpdateProcurementPlanDto, type ProcurementPlanDetailDto, type ProcurementBudgetDto, type ProcurementBudgetDetailDto, type InventoryItemDto, type CreateProcurementPlanItemDto, type UpdateProcurementPlanItemDto, type ProcurementPlanItemDto, type CreateProcurementPlanItemSupplierDto, type ProcurementPlanItemSupplierDto, type MarketAnalysisDto } from '@/services/procurementPlanningService';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import type { OrganizationUnitSummary } from '@/types/hr/organization';
import { businessPartnerService, type BusinessPartnerDto } from '@/services/businessPartnerService';
import { inventoryManagementService, type UnitOfMeasureDto } from '@/services/inventoryManagementService';
import { FiscalYearSelect } from '../../../components/FiscalYearSelect';
import { ProcurementPlanItemDialogBody } from '@/app/procurement/planning/components/ProcurementPlanItemDialogBody';
import { applyProcurementPlanBudgetSelection } from '../../../components/procurementPlanBudgetSelection';

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

export default function EditProcurementPlanPage() {
  const params = useParams();
  const router = useRouter();
  const planId = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [organizationUnits, setOrganizationUnits] = useState<OrganizationUnitSummary[]>([]);
  const [loadingOrganizationUnits, setLoadingOrganizationUnits] = useState(true);
  const [plan, setPlan] = useState<ProcurementPlanDetailDto | null>(null);
  const [budgetOptions, setBudgetOptions] = useState<ProcurementBudgetDto[]>([]);
  const [loadingBudgets, setLoadingBudgets] = useState(false);
  const [linkedBudget, setLinkedBudget] = useState<ProcurementBudgetDetailDto | null>(null);
  const [loadingBudgetAllocations, setLoadingBudgetAllocations] = useState(false);
  const [formData, setFormData] = useState<UpdateProcurementPlanDto>({
    title: '',
    description: '',
    organizationUnitId: '',
    fiscalYear: new Date().getFullYear(),
    planningCycle: 'Annual',
    planningQuarter: '',
    planStartDate: new Date().toISOString().split('T')[0],
    planEndDate: new Date(new Date().getFullYear(), 11, 31).toISOString().split('T')[0],
    planDurationYears: 1,
    totalEstimatedBudget: 0,
    currency: 'USD',
    notes: '',
  });

  // Add/Edit Item Dialog State
  const [itemDialogOpen, setItemDialogOpen] = useState(false);
  const [editingItem, setEditingItem] = useState<ProcurementPlanItemDto | null>(null);
  const [inventoryItems, setInventoryItems] = useState<InventoryItemDto[]>([]);
  const [loadingInventory, setLoadingInventory] = useState(false);
  const [inventorySearchTerm, setInventorySearchTerm] = useState('');
  const [selectedInventoryItem, setSelectedInventoryItem] = useState<InventoryItemDto | null>(null);
  const [savingItem, setSavingItem] = useState(false);
  const [addItemDialogOpen, setAddItemDialogOpen] = useState(false);
  const [addingItem, setAddingItem] = useState(false);
  const [deletingItemId, setDeletingItemId] = useState<string | null>(null);
  const [itemToDelete, setItemToDelete] = useState<ProcurementPlanItemDto | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [itemForm, setItemForm] = useState<CreateProcurementPlanItemDto>(createEmptyItemForm);
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
  const requestedEditItemOpened = useRef(false);

  useEffect(() => {
    const fetchData = async () => {
      try {
        setLoading(true);
        setLoadingOrganizationUnits(true);

        // Fetch both plan and organization units in parallel
        const [planData, units] = await Promise.all([
          procurementPlanService.getPlanById(planId),
          organizationUnitService.getSummary()
        ]);

        setPlan(planData);
        setOrganizationUnits(units.filter((unit) => unit.isActive));

        // Populate form data
        setFormData({
          title: planData.title,
          description: planData.description || '',
          organizationUnitId: planData.organizationUnitId || '',
          fiscalYear: planData.fiscalYear,
          planningCycle: planData.planningCycle || 'Annual',
          planningQuarter: planData.planningQuarter || '',
          planStartDate: planData.planStartDate?.split('T')[0] || '',
          planEndDate: planData.planEndDate?.split('T')[0] || '',
          planDurationYears: planData.planDurationYears,
          totalEstimatedBudget: planData.totalEstimatedBudget,
          budgetId: planData.budgetId,
          currency: planData.currency || 'USD',
          notes: planData.notes || '',
        });
      } catch (error) {
        console.error('Error loading data:', error);
        toast.error('Failed to load procurement plan');
        router.push('/procurement/planning/plans');
      } finally {
        setLoading(false);
        setLoadingOrganizationUnits(false);
      }
    };

    if (planId) {
      fetchData();
    }
  }, [planId, router]);

  useEffect(() => {
    if (!formData.organizationUnitId || !formData.fiscalYear) {
      setBudgetOptions([]);
      return;
    }

    let active = true;
    const loadBudgets = async () => {
      try {
        setLoadingBudgets(true);
        const values = await procurementBudgetService.getAvailableBudgetsForLinking(
          formData.organizationUnitId,
          formData.fiscalYear,
          true,
        );
        if (active) setBudgetOptions(values);
      } catch (error) {
        console.error('Error fetching procurement budget options:', error);
        if (active) {
          setBudgetOptions([]);
          toast.error(error instanceof Error ? error.message : 'Failed to load approved budgets');
        }
      } finally {
        if (active) setLoadingBudgets(false);
      }
    };

    void loadBudgets();
    return () => { active = false; };
  }, [formData.organizationUnitId, formData.fiscalYear]);

  useEffect(() => {
    if (!formData.budgetId) {
      setLinkedBudget(null);
      return;
    }

    const budgetId = formData.budgetId;
    let active = true;
    const loadLinkedBudget = async () => {
      try {
        setLoadingBudgetAllocations(true);
        const value = await procurementBudgetService.getBudgetById(budgetId);
        if (active) setLinkedBudget(value);
      } catch (error) {
        console.error('Error loading linked budget allocations:', error);
        if (active) {
          setLinkedBudget(null);
          toast.error('The linked budget allocations could not be loaded');
        }
      } finally {
        if (active) setLoadingBudgetAllocations(false);
      }
    };

    void loadLinkedBudget();
    return () => { active = false; };
  }, [formData.budgetId]);

  const handleInputChange = (field: keyof UpdateProcurementPlanDto, value: string | number) => {
    setFormData((prev) => ({
      ...prev,
      [field]: value,
      ...(!plan?.budgetId && (field === 'organizationUnitId' || field === 'fiscalYear')
        ? { budgetId: undefined }
        : {}),
    }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!formData.title.trim()) {
      toast.error('Title is required');
      return;
    }
    if (!formData.organizationUnitId) {
      toast.error('Organization unit is required');
      return;
    }

    try {
      setSaving(true);
      await procurementPlanService.updatePlan(planId, formData);
      toast.success('Procurement plan updated successfully');
      router.push(`/procurement/planning/plans/${planId}`);
    } catch (error) {
      console.error('Error updating plan:', error);
      toast.error(error instanceof Error ? error.message : 'Failed to update procurement plan');
    } finally {
      setSaving(false);
    }
  };

  // Item management functions
  const loadInventoryItems = async () => {
    try {
      setLoadingInventory(true);
      console.log('Loading inventory items...');
      const items = await commonService.getInventoryItems();
      console.log('Loaded inventory items:', items);
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
    setEditingItem(null);
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

  const handleOpenEditItemDialog = (item: ProcurementPlanItemDto) => {
    setEditingItem(item);
    setItemDialogOpen(true);
    loadInventoryItems();
    loadSuppliers();
    loadMarketAnalyses();
    loadUnitsOfMeasure();
    setSelectedInventoryItem(null);
    setInventorySearchTerm('');
    setSupplierSearchTerm('');
    // Convert existing item suppliers to the create DTO format
    const existingSuppliers: CreateProcurementPlanItemSupplierDto[] = (item.itemSuppliers || []).map(s => ({
      supplierId: s.supplierId,
      isPreferred: s.isPreferred,
      priority: s.priority,
      quotedUnitPrice: s.quotedUnitPrice,
      leadTimeDays: s.leadTimeDays,
      supplierItemCode: s.supplierItemCode,
      notes: s.notes,
    }));
    setSelectedItemSuppliers(existingSuppliers);
    setItemForm({
      inventoryItemId: item.inventoryItemId,
      marketAnalysisId: item.marketAnalysisId,
      itemDescription: item.itemDescription,
      procurementBudgetId: item.procurementBudgetId,
      procurementBudgetAllocationId: item.procurementBudgetAllocationId,
      budgetLineCode: item.budgetLineCode || '',
      budgetCategoryName: item.budgetCategoryName || '',
      approvedBudgetAmount: item.approvedBudgetAmount,
      budgetNotes: item.budgetNotes || '',
      specifications: item.specifications || '',
      itemCategory: item.itemCategory || '',
      estimatedQuantity: item.estimatedQuantity,
      unitOfMeasure: item.unitOfMeasure || 'EA',
      estimatedUnitPrice: item.estimatedUnitPrice || 0,
      priority: item.priority || 'Medium',
      isCritical: item.isCritical || false,
      requiredDate: item.requiredDate ? item.requiredDate.split('T')[0] : '',
      plannedQuarter: item.plannedQuarter || '',
      justification: item.justification || '',
      procurementMethod: item.procurementMethod || undefined,
      notes: item.notes || '',
      itemSuppliers: existingSuppliers,
    });
  };

  const openEditItemRef = useRef(handleOpenEditItemDialog);
  openEditItemRef.current = handleOpenEditItemDialog;

  useEffect(() => {
    if (requestedEditItemOpened.current || !plan?.items?.length) return;

    const requestedItemId = new URLSearchParams(window.location.search).get('itemId');
    if (!requestedItemId) return;

    const requestedItem = plan.items.find(item => item.id === requestedItemId);
    if (!requestedItem) {
      requestedEditItemOpened.current = true;
      toast.error('The selected plan item could not be found');
      return;
    }

    requestedEditItemOpened.current = true;
    openEditItemRef.current(requestedItem);
  }, [plan]);

  const handleAddSupplierToItem = (supplier: BusinessPartnerDto) => {
    // Check if supplier is already added
    if (selectedItemSuppliers.some(s => s.supplierId === supplier.id)) {
      toast.error('Supplier already added');
      return;
    }
    const newSupplier: CreateProcurementPlanItemSupplierDto = {
      supplierId: supplier.id,
      isPreferred: selectedItemSuppliers.length === 0, // First supplier is preferred by default
      priority: selectedItemSuppliers.length + 1,
    };
    const updatedSuppliers = [...selectedItemSuppliers, newSupplier];
    setSelectedItemSuppliers(updatedSuppliers);
    if (addItemDialogOpen && !editingItem) {
      setNewItemForm((current) => ({ ...current, itemSuppliers: updatedSuppliers }));
    } else {
      setItemForm((current) => ({ ...current, itemSuppliers: updatedSuppliers }));
    }
  };

  const handleMarketAnalysisSelect = (value: string) => {
    const updateFromMarketAnalysis = (current: CreateProcurementPlanItemDto): CreateProcurementPlanItemDto => {
      if (value === 'none') {
        return { ...current, marketAnalysisId: undefined };
      }

      const analysis = marketAnalyses.find((item) => item.id === value);
      return {
        ...current,
        marketAnalysisId: value,
        itemCategory: current.itemCategory || analysis?.itemCategory || '',
        itemDescription: current.itemDescription || analysis?.itemDescription || analysis?.title || '',
        estimatedUnitPrice: current.estimatedUnitPrice && current.estimatedUnitPrice > 0
          ? current.estimatedUnitPrice
          : analysis?.currentMarketPrice || 0,
      };
    };

    if (addItemDialogOpen && !editingItem) {
      setNewItemForm(updateFromMarketAnalysis);
      return;
    }

    setItemForm(updateFromMarketAnalysis);
  };

  const handleRemoveSupplierFromItem = (supplierId: string) => {
    const updatedSuppliers = selectedItemSuppliers
      .filter(s => s.supplierId !== supplierId)
      .map((s, index) => ({ ...s, priority: index + 1 }));
    setSelectedItemSuppliers(updatedSuppliers);
    if (addItemDialogOpen && !editingItem) {
      setNewItemForm((current) => ({ ...current, itemSuppliers: updatedSuppliers }));
    } else {
      setItemForm((current) => ({ ...current, itemSuppliers: updatedSuppliers }));
    }
  };

  const handleUpdateItemSupplier = (supplierId: string, field: keyof CreateProcurementPlanItemSupplierDto, value: unknown) => {
    const updatedSuppliers = selectedItemSuppliers.map(s => {
      if (s.supplierId === supplierId) {
        if (field === 'isPreferred' && value === true) {
          // If setting as preferred, unset others
          return { ...s, [field]: value };
        }
        return { ...s, [field]: value };
      }
      // If setting another as preferred, unset this one
      if (field === 'isPreferred' && value === true) {
        return { ...s, isPreferred: false };
      }
      return s;
    });
    setSelectedItemSuppliers(updatedSuppliers);
    if (addItemDialogOpen && !editingItem) {
      setNewItemForm((current) => ({ ...current, itemSuppliers: updatedSuppliers }));
    } else {
      setItemForm((current) => ({ ...current, itemSuppliers: updatedSuppliers }));
    }
  };

  const filteredSuppliers = suppliers.filter(s =>
    (s.partnerName || '').toLowerCase().includes(supplierSearchTerm.toLowerCase()) ||
    (s.partnerCode || '').toLowerCase().includes(supplierSearchTerm.toLowerCase())
  );

  const handleSelectInventoryItem = (item: InventoryItemDto) => {
    setSelectedInventoryItem(item);
    const applySelectedItem = (current: CreateProcurementPlanItemDto): CreateProcurementPlanItemDto => ({
      ...current,
      inventoryItemId: item.id,
      itemDescription: item.name,
      specifications: item.description || '',
      itemCategory: item.categoryName || '',
      unitOfMeasure: item.unitOfMeasure || 'EA',
      estimatedUnitPrice: item.standardCost || item.averageCost || 0,
    });

    if (itemDialogOpen && editingItem) {
      setItemForm(applySelectedItem);
      return;
    }

    setNewItemForm(applySelectedItem);
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
        .filter(value => value.id !== editingItem?.id && value.procurementBudgetAllocationId === allocation.id)
        .reduce((total, value) => total + (value.approvedBudgetAmount ?? value.estimatedTotalCost), 0);
      const queuedExposure = pendingPlanItems
        .filter(value => value.procurementBudgetAllocationId === allocation.id)
        .reduce((total, value) => total + (value.approvedBudgetAmount ?? getItemEstimatedTotal(value)), 0);
      if (savedExposure + queuedExposure + itemBudgetAmount > allocation.remainingAmount) {
        return `${allocation.categoryName} has insufficient remaining allocation for this item`;
      }
    }

    const savedPlanExposure = (plan?.items || [])
      .filter(value => value.id !== editingItem?.id)
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
      const updatedPlan = await procurementPlanService.getPlanById(planId);
      setPlan(updatedPlan);
    } catch (error) {
      console.error('Error adding items:', error);
      toast.error(error instanceof Error ? error.message : 'Failed to save plan items');
    } finally {
      setAddingItem(false);
    }
  };

  const handleSaveItem = async () => {
    const validationError = validateItemForm(itemForm);
    if (validationError) {
      toast.error(validationError);
      return;
    }

    try {
      setSavingItem(true);
      if (editingItem) {
        // Update existing item
        const updateDto: UpdateProcurementPlanItemDto = {
          id: editingItem.id,
          ...itemForm,
        };
        await procurementPlanService.updateItem(editingItem.id, updateDto);
        toast.success('Item updated successfully');
      } else {
        // Add new item
        await procurementPlanService.addItem(planId, itemForm);
        toast.success('Item added successfully');
      }
      setItemDialogOpen(false);
      // Refresh plan data
      const updatedPlan = await procurementPlanService.getPlanById(planId);
      setPlan(updatedPlan);
    } catch (error) {
      console.error('Error saving item:', error);
      toast.error(error instanceof Error ? error.message : editingItem ? 'Failed to update item' : 'Failed to add item');
    } finally {
      setSavingItem(false);
    }
  };

  const handleDeleteItem = (itemId: string) => {
    const item = plan?.items.find((entry) => entry.id === itemId);
    if (!item || deletingItemId) return;
    setDeleteError(null);
    setItemToDelete(item);
  };

  const confirmDeleteItem = async () => {
    if (!itemToDelete || deletingItemId) return false;
    const itemId = itemToDelete.id;
    try {
      setDeletingItemId(itemId);
      setDeleteError(null);
      await procurementPlanService.removeItem(planId, itemId);
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Failed to delete item';
      setDeleteError(message);
      toast.error(message);
      setDeletingItemId(null);
      return false;
    }

    // The deletion is committed. A refresh failure must not offer deletion again.
    setItemToDelete(null);
    setPlan((current) => current ? { ...current, items: current.items.filter((item) => item.id !== itemId) } : current);
    toast.success('Item deleted successfully');
    try {
      const updatedPlan = await procurementPlanService.getPlanById(planId);
      setPlan(updatedPlan);
    } catch {
      toast.error('Item deleted, but the plan could not be refreshed. Reload the page before continuing.');
    } finally {
      setDeletingItemId(null);
    }
    return true;
  };

  const inventorySearchQuery = inventorySearchTerm.trim().toLowerCase();
  const filteredInventoryItems = inventorySearchQuery.length >= 2
    ? inventoryItems.filter(item =>
        (item.name || '').toLowerCase().includes(inventorySearchQuery) ||
        (item.itemCode || '').toLowerCase().includes(inventorySearchQuery) ||
        (item.categoryName || '').toLowerCase().includes(inventorySearchQuery)
      )
    : [];

  const formatCurrency = (amount: number, currency: string = 'USD') => {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency }).format(amount);
  };

  const getPriorityBadge = (priority: string) => {
    const variants: Record<string, 'default' | 'secondary' | 'destructive' | 'outline'> = {
      'Low': 'secondary',
      'Medium': 'default',
      'High': 'outline',
      'Critical': 'destructive',
    };
    return <Badge variant={variants[priority] || 'default'}>{priority}</Badge>;
  };

  const planCurrency = formData.currency || plan?.currency || 'USD';
  const itemBatchSaveCount = pendingPlanItems.length + (hasCurrentItemDraft() ? 1 : 0);
  const pendingPlanItemsTotal = pendingPlanItems.reduce((total, item) => total + getItemEstimatedTotal(item), 0);
  const unitOfMeasureOptions = buildUnitOfMeasureOptions(unitsOfMeasure, newItemForm.unitOfMeasure);
  const editUnitOfMeasureOptions = buildUnitOfMeasureOptions(unitsOfMeasure, itemForm.unitOfMeasure);

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <Loader2 className="h-12 w-12 animate-spin mx-auto mb-4 text-blue-500" />
          <p className="text-lg text-gray-600">Loading procurement plan...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" onClick={() => router.push(`/procurement/planning/plans/${planId}`)}>
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back
          </Button>
          <div>
            <h1 className="text-3xl font-bold">Edit Procurement Plan</h1>
            <p className="text-gray-500">Update plan details and manage items</p>
          </div>
        </div>
      </div>

      <Tabs defaultValue="details" className="w-full">
        <TabsList className="grid w-full grid-cols-2 max-w-md">
          <TabsTrigger value="details">Plan Details</TabsTrigger>
          <TabsTrigger value="items">Plan Items ({plan?.items?.length || 0})</TabsTrigger>
        </TabsList>

        <TabsContent value="details" className="space-y-6 mt-6">
          <form onSubmit={handleSubmit}>
            <div className="grid gap-6">
          {/* Basic Information */}
          <Card>
            <CardHeader>
              <CardTitle>Basic Information</CardTitle>
              <CardDescription>Update the basic details of the procurement plan</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="title">Title *</Label>
                  <Input
                    id="title"
                    value={formData.title}
                    onChange={(e) => handleInputChange('title', e.target.value)}
                    placeholder="Enter plan title"
                    required
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="organizationUnitId">Organization unit *</Label>
                  <Select
                    value={formData.organizationUnitId}
                    onValueChange={(value) => handleInputChange('organizationUnitId', value)}
                    disabled={loadingOrganizationUnits || Boolean(plan?.budgetId)}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder={loadingOrganizationUnits ? "Loading organization units..." : "Select organization unit"} />
                    </SelectTrigger>
                    <SelectContent>
                      {organizationUnits.map((unit) => (
                        <SelectItem key={unit.id} value={unit.id}>
                          {unit.code ? `${unit.code} - ${unit.name}` : unit.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  value={formData.description || ''}
                  onChange={(e) => handleInputChange('description', e.target.value)}
                  placeholder="Enter plan description"
                  rows={3}
                />
              </div>
            </CardContent>
          </Card>

          {/* Timeline */}
          <Card>
            <CardHeader>
              <CardTitle>Timeline</CardTitle>
              <CardDescription>Define the planning period</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-6 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="fiscalYear">Fiscal Year *</Label>
                  <FiscalYearSelect
                    value={formData.fiscalYear}
                    onValueChange={(year) => handleInputChange('fiscalYear', year)}
                    disabled={Boolean(plan?.budgetId)}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="planningCycle">Cycle</Label>
                  <Select
                    value={formData.planningCycle || 'Annual'}
                    onValueChange={(value) => {
                      setFormData((prev) => ({
                        ...prev,
                        planningCycle: value,
                        planningQuarter: value === 'Quarterly' ? prev.planningQuarter : '',
                      }));
                    }}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select cycle" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Annual">Annual</SelectItem>
                      <SelectItem value="Quarterly">Quarterly</SelectItem>
                      <SelectItem value="MultiYear">Multi-Year</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="planningQuarter">Quarter</Label>
                  <Select
                    value={formData.planningQuarter || 'none'}
                    onValueChange={(value) => handleInputChange('planningQuarter', value === 'none' ? '' : value)}
                    disabled={(formData.planningCycle || 'Annual') !== 'Quarterly'}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select quarter" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">Not applicable</SelectItem>
                      <SelectItem value="Q1">Q1</SelectItem>
                      <SelectItem value="Q2">Q2</SelectItem>
                      <SelectItem value="Q3">Q3</SelectItem>
                      <SelectItem value="Q4">Q4</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="planStartDate">Start Date *</Label>
                  <Input
                    id="planStartDate"
                    type="date"
                    value={formData.planStartDate}
                    onChange={(e) => handleInputChange('planStartDate', e.target.value)}
                    required
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="planEndDate">End Date *</Label>
                  <Input
                    id="planEndDate"
                    type="date"
                    value={formData.planEndDate}
                    onChange={(e) => handleInputChange('planEndDate', e.target.value)}
                    required
                  />
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="planDurationYears">Plan Duration (Years)</Label>
                <Input
                  id="planDurationYears"
                  type="number"
                  value={formData.planDurationYears}
                  onChange={(e) => handleInputChange('planDurationYears', parseInt(e.target.value))}
                  min={1}
                  max={10}
                />
              </div>
            </CardContent>
          </Card>

          {/* Budget */}
          <Card>
            <CardHeader>
              <CardTitle>Budget</CardTitle>
              <CardDescription>Select the approved budget that controls this plan</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2 col-span-2">
                  <Label htmlFor="budgetId">Approved Budget *</Label>
                  <Select
                    value={formData.budgetId || '__none__'}
                    onValueChange={(value) => {
                      setFormData((previous) =>
                        applyProcurementPlanBudgetSelection(previous, budgetOptions, value));
                    }}
                    disabled={!formData.organizationUnitId || loadingBudgets}
                  >
                    <SelectTrigger id="budgetId">
                      <SelectValue placeholder={
                        !formData.organizationUnitId
                          ? 'Select an organization unit first'
                          : loadingBudgets
                            ? 'Loading approved budgets...'
                            : 'Select approved budget'
                      } />
                    </SelectTrigger>
                    <SelectContent>
                      {!plan?.budgetId && (
                        <SelectItem value="__none__">Select later (draft only)</SelectItem>
                      )}
                      {budgetOptions.map((budget) => (
                        <SelectItem
                          key={budget.id}
                          value={budget.id}
                        >
                          {budget.budgetCode} — {budget.title} ({budget.currency} {budget.allocatedAmount.toLocaleString()})
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <p className="text-xs text-muted-foreground">
                    A budget may fund multiple plans while its controlled planning capacity remains sufficient. Currency
                    is inherited from the selected budget.
                  </p>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="totalEstimatedBudget">Total Estimated Budget *</Label>
                  <Input
                    id="totalEstimatedBudget"
                    type="number"
                    value={formData.totalEstimatedBudget}
                    onChange={(e) => handleInputChange('totalEstimatedBudget', parseFloat(e.target.value))}
                    min={0}
                    step={0.01}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="currency">Currency</Label>
                  <Input
                    id="currency"
                    value={formData.budgetId ? formData.currency : 'Select an approved budget'}
                    readOnly
                    aria-readonly="true"
                  />
                  <p className="text-xs text-muted-foreground">
                    Currency is inherited from the linked approved budget.
                  </p>
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="notes">Notes</Label>
                <Textarea
                  id="notes"
                  value={formData.notes || ''}
                  onChange={(e) => handleInputChange('notes', e.target.value)}
                  placeholder="Add any additional notes"
                  rows={3}
                />
              </div>
            </CardContent>
          </Card>

              {/* Actions */}
              <div className="flex justify-end gap-4">
                <Button type="button" variant="outline" onClick={() => router.push(`/procurement/planning/plans/${planId}`)}>
                  Cancel
                </Button>
                <Button type="submit" disabled={saving}>
                  {saving ? (
                    <>
                      <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                      Saving...
                    </>
                  ) : (
                    <>
                      <Save className="h-4 w-4 mr-2" />
                      Save Changes
                    </>
                  )}
                </Button>
              </div>
            </div>
          </form>
        </TabsContent>

        <TabsContent value="items" className="space-y-6 mt-6">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <div>
                <CardTitle>Plan Items</CardTitle>
                <CardDescription>Manage items included in this procurement plan</CardDescription>
              </div>
              <Button onClick={handleOpenAddItemDialog}>
                <Plus className="h-4 w-4 mr-2" />
                Add Item
              </Button>
            </CardHeader>
            <CardContent>
              {plan?.items && plan.items.length > 0 ? (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Item Description</TableHead>
                      <TableHead>Budget Allocation</TableHead>
                      <TableHead>Quantity</TableHead>
                      <TableHead>Unit Cost</TableHead>
                      <TableHead>Total</TableHead>
                      <TableHead>Priority</TableHead>
                      <TableHead className="w-[100px]">Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {plan.items.map((item) => (
                      <TableRow key={item.id}>
                        <TableCell className="font-medium">{item.itemDescription}</TableCell>
                        <TableCell>
                          <div className="text-sm">
                            <div>{item.budgetCategoryName || '-'}</div>
                            {item.budgetLineCode && (
                              <div className="text-xs text-gray-500">Legacy line: {item.budgetLineCode}</div>
                            )}
                          </div>
                        </TableCell>
                        <TableCell>{item.estimatedQuantity} {item.unitOfMeasure}</TableCell>
                        <TableCell>{formatCurrency(item.estimatedUnitPrice, formData.currency)}</TableCell>
                        <TableCell>{formatCurrency(item.estimatedTotalCost, formData.currency)}</TableCell>
                        <TableCell>{getPriorityBadge(item.priority)}</TableCell>
                        <TableCell>
                          <div className="flex items-center gap-1">
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={() => handleOpenEditItemDialog(item)}
                              title="Edit item"
                            >
                              <Pencil className="h-4 w-4 text-blue-500" />
                            </Button>
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
                  <Button onClick={handleOpenAddItemDialog} variant="outline" className="mt-4">
                    <Plus className="h-4 w-4 mr-2" />
                    Add First Item
                  </Button>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
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
                Costs in {planCurrency}
              </Badge>
            </div>
          </DialogHeader>

          <ProcurementPlanItemDialogBody
            currency={planCurrency}
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
                          Cost: {formatCurrency(item.standardCost || item.averageCost || 0, planCurrency)} | Stock: {item.availableStock}
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
                  Line total: {formatCurrency(getItemEstimatedTotal(newItemForm), planCurrency)}
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
                    <Label htmlFor="estimatedUnitPrice">Unit Cost ({planCurrency})</Label>
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
                          {analysis.title} - {formatCurrency(analysis.currentMarketPrice, analysis.currency || planCurrency)}
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
                    <p className="font-semibold">{formatCurrency(getItemEstimatedTotal(newItemForm), planCurrency)}</p>
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
                              {item.estimatedQuantity} {item.unitOfMeasure || 'EA'} | {formatCurrency(getItemEstimatedTotal(item), planCurrency)}
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
                    {formatCurrency(pendingPlanItemsTotal, planCurrency)}
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
              Item unit costs are saved using the plan currency: {planCurrency}.
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

      {/* Edit Item Dialog */}
      <Dialog open={itemDialogOpen} onOpenChange={setItemDialogOpen}>
        <DialogContent className="max-w-6xl max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{editingItem ? 'Edit Item' : 'Add Item to Procurement Plan'}</DialogTitle>
            <DialogDescription>
              {editingItem ? 'Update item details' : 'Select an item from inventory or enter details manually'}
            </DialogDescription>
          </DialogHeader>

          <ProcurementPlanItemDialogBody
            currency={planCurrency}
            form={itemForm}
            setForm={setItemForm}
            inventorySearchTerm={inventorySearchTerm}
            onInventorySearchTermChange={setInventorySearchTerm}
            inventoryResults={filteredInventoryItems}
            loadingInventory={loadingInventory}
            selectedInventoryItem={selectedInventoryItem}
            onSelectInventoryItem={handleSelectInventoryItem}
            unitOfMeasureOptions={editUnitOfMeasureOptions}
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
          />

          <div className="hidden">
          <div className={editingItem ? '' : 'grid grid-cols-2 gap-6'}>
            {/* Left: Inventory Selection (only for add mode) */}
            {!editingItem && (
              <div className="space-y-4 border-r pr-6">
                <h4 className="font-medium">Select from Inventory</h4>
                <div className="relative">
                  <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
                  <Input
                    placeholder="Search inventory items..."
                    value={inventorySearchTerm}
                    onChange={(e) => setInventorySearchTerm(e.target.value)}
                    className="pl-10"
                  />
                </div>
                <div className="h-[300px] overflow-y-auto border rounded-lg">
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
                          className={`p-3 cursor-pointer hover:bg-gray-50 ${
                            selectedInventoryItem?.id === item.id ? 'bg-blue-50 border-l-4 border-blue-500' : ''
                          }`}
                          onClick={() => handleSelectInventoryItem(item)}
                        >
                          <div className="font-medium">{item.name}</div>
                          <div className="text-sm text-gray-500">
                            {item.itemCode} | {item.categoryName || 'No Category'} | {item.unitOfMeasure}
                          </div>
                          <div className="text-sm text-gray-500">
                            Cost: {formatCurrency(item.standardCost || item.averageCost, formData.currency)} | Stock: {item.availableStock}
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            )}

            {/* Right: Item Details Form */}
            <div className="space-y-4">
              <h4 className="font-medium">Item Details</h4>
              <div className="space-y-3">
                <div className="space-y-1">
                  <Label htmlFor="itemDescription">Item Description *</Label>
                  <Input
                    id="itemDescription"
                    value={itemForm.itemDescription}
                    onChange={(e) => setItemForm({ ...itemForm, itemDescription: e.target.value })}
                    placeholder="Enter item description"
                  />
                </div>
                <div className="grid grid-cols-3 gap-3">
                  <div className="space-y-1">
                    <Label htmlFor="unitOfMeasure">Unit of Measure</Label>
                    <Select
                      value={itemForm.unitOfMeasure || undefined}
                      onValueChange={(value) => setItemForm({ ...itemForm, unitOfMeasure: value })}
                      disabled={loadingUnitsOfMeasure && editUnitOfMeasureOptions.length === 0}
                    >
                      <SelectTrigger id="unitOfMeasure">
                        <SelectValue placeholder={loadingUnitsOfMeasure ? 'Loading UOMs...' : 'Select UOM'} />
                      </SelectTrigger>
                      <SelectContent>
                        {editUnitOfMeasureOptions.map((option) => (
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
                      value={itemForm.estimatedQuantity}
                      onChange={(e) => setItemForm({ ...itemForm, estimatedQuantity: parseFloat(e.target.value) || 0 })}
                    />
                  </div>
                  <div className="space-y-1">
                    <Label htmlFor="estimatedUnitPrice">Unit Cost</Label>
                    <Input
                      id="estimatedUnitPrice"
                      type="number"
                      min={0}
                      step={0.01}
                      value={itemForm.estimatedUnitPrice || 0}
                      onChange={(e) => setItemForm({ ...itemForm, estimatedUnitPrice: parseFloat(e.target.value) || 0 })}
                    />
                  </div>
                </div>
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-1">
                    <Label htmlFor="priority">Priority</Label>
                    <Select
                      value={itemForm.priority || 'Medium'}
                      onValueChange={(value) => setItemForm({ ...itemForm, priority: value })}
                    >
                      <SelectTrigger>
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
                      value={itemForm.requiredDate || ''}
                      onChange={(e) => setItemForm({ ...itemForm, requiredDate: e.target.value })}
                    />
                  </div>
                </div>
                <div className="grid grid-cols-3 gap-3">
                  <div className="space-y-1">
                    <Label htmlFor="budgetLineCode">Budget Line</Label>
                    <Input
                      id="budgetLineCode"
                      value={itemForm.budgetLineCode || ''}
                      onChange={(e) => setItemForm({ ...itemForm, budgetLineCode: e.target.value })}
                      placeholder="Line code"
                    />
                  </div>
                  <div className="space-y-1">
                    <Label htmlFor="budgetCategoryName">Budget Category</Label>
                    <Input
                      id="budgetCategoryName"
                      value={itemForm.budgetCategoryName || ''}
                      onChange={(e) => setItemForm({ ...itemForm, budgetCategoryName: e.target.value })}
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
                      value={itemForm.approvedBudgetAmount ?? ''}
                      onChange={(e) => setItemForm({
                        ...itemForm,
                        approvedBudgetAmount: e.target.value ? parseFloat(e.target.value) || 0 : undefined,
                      })}
                      placeholder="0.00"
                    />
                  </div>
                </div>
                <div className="space-y-1">
                  <Label>Market Analysis</Label>
                  <Select
                    value={itemForm.marketAnalysisId || 'none'}
                    onValueChange={handleMarketAnalysisSelect}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder={loadingMarketAnalyses ? 'Loading analyses...' : 'Select market analysis'} />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No linked analysis</SelectItem>
                      {marketAnalyses.map((analysis) => (
                        <SelectItem key={analysis.id} value={analysis.id}>
                          {analysis.title} - {new Intl.NumberFormat('en-US', { style: 'currency', currency: analysis.currency || formData.currency || 'USD', maximumFractionDigits: 0 }).format(analysis.currentMarketPrice || 0)}
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
                      value={itemForm.specifications || ''}
                      onChange={(e) => setItemForm({ ...itemForm, specifications: e.target.value })}
                      placeholder="Enter specifications"
                      rows={2}
                    />
                  </div>
                  <div className="space-y-1">
                    <Label htmlFor="justification">Justification</Label>
                    <Textarea
                      id="justification"
                      value={itemForm.justification || ''}
                      onChange={(e) => setItemForm({ ...itemForm, justification: e.target.value })}
                      placeholder="Enter justification"
                      rows={2}
                    />
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* Preferred Suppliers Section */}
          <div className="border-t pt-4 mt-4">
            <div className="flex items-center justify-between mb-3">
              <h4 className="font-medium flex items-center gap-2">
                <Users className="h-4 w-4" />
                Preferred Suppliers
              </h4>
            </div>

            {/* Supplier Search and Add */}
            <div className="grid grid-cols-2 gap-4 mb-4">
              <div className="space-y-2">
                <Label>Search & Add Supplier</Label>
                <div className="relative">
                  <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
                  <Input
                    placeholder="Search suppliers..."
                    value={supplierSearchTerm}
                    onChange={(e) => setSupplierSearchTerm(e.target.value)}
                    className="pl-10"
                  />
                </div>
                <div className="h-[150px] overflow-y-auto border rounded-lg">
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

              {/* Selected Suppliers Grid */}
              <div className="space-y-2">
                <Label>Selected Suppliers ({selectedItemSuppliers.length})</Label>
                <div className="h-[180px] overflow-y-auto border rounded-lg">
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
          </div>
          </div>

          <DialogFooter className="mt-4">
            <Button variant="outline" onClick={() => setItemDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleSaveItem} disabled={savingItem}>
              {savingItem ? (
                <>
                  <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                  {editingItem ? 'Updating...' : 'Adding...'}
                </>
              ) : (
                <>
                  <Save className="h-4 w-4 mr-2" />
                  {editingItem ? 'Update Item' : 'Add Item'}
                </>
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <ConfirmationDialog
        open={itemToDelete !== null}
        onOpenChange={(open) => {
          if (!open && !deletingItemId) {
            setItemToDelete(null);
            setDeleteError(null);
          }
        }}
        title="Delete procurement plan item?"
        description={`Remove "${itemToDelete?.itemDescription || ''}" from this draft plan? Other plan items will not be changed.`}
        confirmText="Delete item"
        variant="destructive"
        onConfirm={confirmDeleteItem}
        isLoading={Boolean(deletingItemId)}
      >
        {deleteError && <Alert variant="destructive"><AlertDescription>{deleteError}</AlertDescription></Alert>}
      </ConfirmationDialog>
    </div>
  );
}
