'use client';

import React, { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowTabContent, WorkflowTabTrigger } from '@/components/workflow/WorkflowRecordTab';
import { Plus, Trash2, Search, Package, AlertCircle, Pencil, Check, X } from 'lucide-react';
import {
  inventoryRequisitionService,
  InventoryRequisitionDetailDto, InventoryRequisitionItemDto,
  CreateInventoryRequisitionDto, CreateInventoryRequisitionItemDto,
  AddRequisitionItemDto, UpdateRequisitionItemDto, DepartmentDto,
  RequisitionStatusMap, RequisitionTypeMap
} from '@/services/inventoryRequisitionService';
import { inventoryManagementService, WarehouseDto, WarehouseInventoryItemDto, WarehouseLocationDto, BinStockDto } from '@/services/inventoryManagementService';
import { useToast } from '@/hooks/use-toast';
import { format } from 'date-fns';

interface RequisitionDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  mode: 'create' | 'edit' | 'view';
  requisitionId?: string;
  warehouses: WarehouseDto[];
  onSuccess: () => void;
  projectContext?: {
    projectId: string;
    projectCode: string;
    projectTitle: string;
    departmentId?: string;
    departmentName?: string;
  };
}

interface FormData {
  departmentId: string;
  departmentName: string;
  costCenter: string;
  warehouseId: string;
  locationId: string;
  requisitionType: number;
  priority: string;
  requiredDate: string;
  purpose: string;
  notes: string;
}

interface ItemFormData {
  inventoryItemId: string;
  locationId: string;
  requestedQuantity: number;
  lotNumber: string;
  batchNumber: string;
  serialNumber: string;
  notes: string;
}

const RequisitionTypes = [
  { value: 1, label: 'Department Requisition' },
  { value: 2, label: 'Project Requisition' },
  { value: 3, label: 'Maintenance Requisition' },
  { value: 4, label: 'Production Requisition' },
  { value: 5, label: 'Emergency Requisition' },
  { value: 6, label: 'Return to Stock' }
];

const Priorities = [
  { value: 'Low', label: 'Low' },
  { value: 'Normal', label: 'Normal' },
  { value: 'High', label: 'High' },
  { value: 'Urgent', label: 'Urgent' }
];

const RequisitionStatuses = [
  { value: 1, label: 'Draft', color: 'bg-gray-100 text-gray-800' },
  { value: 2, label: 'Submitted', color: 'bg-yellow-100 text-yellow-800' },
  { value: 3, label: 'Approved', color: 'bg-blue-100 text-blue-800' },
  { value: 4, label: 'In Progress', color: 'bg-indigo-100 text-indigo-800' },
  { value: 5, label: 'Partially Issued', color: 'bg-purple-100 text-purple-800' },
  { value: 6, label: 'Issued', color: 'bg-green-100 text-green-800' },
  { value: 7, label: 'Completed', color: 'bg-green-200 text-green-900' },
  { value: 8, label: 'Cancelled', color: 'bg-red-100 text-red-800' },
  { value: 9, label: 'Rejected', color: 'bg-red-100 text-red-800' }
];

// Helper to normalize status to number (API may return string or number)
const normalizeStatus = (status: number | string | undefined): number => {
  if (status === undefined || status === null) return 0;
  if (typeof status === 'number') return status;
  const statusMap: Record<string, number> = {
    'Draft': 1, 'Submitted': 2, 'Approved': 3, 'InProgress': 4,
    'PartiallyIssued': 5, 'Issued': 6, 'Completed': 7, 'Cancelled': 8, 'Rejected': 9
  };
  return statusMap[status] || 0;
};

export function RequisitionDialog({ open, onOpenChange, mode, requisitionId, warehouses, onSuccess, projectContext }: RequisitionDialogProps) {
  const { toast } = useToast();
  const router = useRouter();
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [activeTab, setActiveTab] = useState('details');
  const [requisitionDetail, setRequisitionDetail] = useState<InventoryRequisitionDetailDto | null>(null);
  const [departments, setDepartments] = useState<DepartmentDto[]>([]);
  const [warehouseInventoryItems, setWarehouseInventoryItems] = useState<WarehouseInventoryItemDto[]>([]);
  const [warehouseLocations, setWarehouseLocations] = useState<WarehouseLocationDto[]>([]);
  const [locationStockItems, setLocationStockItems] = useState<BinStockDto[]>([]);
  const [loadingItems, setLoadingItems] = useState(false);
  const [loadingLocations, setLoadingLocations] = useState(false);
  const [loadingLocationStock, setLoadingLocationStock] = useState(false);
  const [itemSearchTerm, setItemSearchTerm] = useState('');
  const [showAddItem, setShowAddItem] = useState(false);
  const [editingItemId, setEditingItemId] = useState<string | null>(null);
  const [editingItemData, setEditingItemData] = useState<{ requestedQuantity: number; notes: string }>({ requestedQuantity: 1, notes: '' });
  const [pendingItems, setPendingItems] = useState<CreateInventoryRequisitionItemDto[]>([]);

  const [formData, setFormData] = useState<FormData>({
    departmentId: '',
    departmentName: '',
    costCenter: '',
    warehouseId: '',
    locationId: '',
    requisitionType: 1,
    priority: 'Normal',
    requiredDate: '',
    purpose: '',
    notes: ''
  });

  const [itemFormData, setItemFormData] = useState<ItemFormData>({
    inventoryItemId: '',
    locationId: '',
    requestedQuantity: 1,
    lotNumber: '',
    batchNumber: '',
    serialNumber: '',
    notes: ''
  });

  const isViewMode = mode === 'view';
  const isEditMode = mode === 'edit';
  const isCreateMode = mode === 'create';
  const isProjectScoped = !!projectContext;
  const requisitionStatus = normalizeStatus(requisitionDetail?.status);
  const canEdit = !isViewMode && (isCreateMode || requisitionStatus === 1);
  const showApprovalsTab = !!requisitionDetail && !isCreateMode;

  // Reset state when dialog opens/closes
  useEffect(() => {
    if (open) {
      setActiveTab('details');
      setPendingItems([]);
      setShowAddItem(false);
      setItemSearchTerm('');
      setItemFormData({ inventoryItemId: '', locationId: '', requestedQuantity: 1, lotNumber: '', batchNumber: '', serialNumber: '', notes: '' });
      if (mode === 'create') {
        setFormData({
          departmentId: projectContext?.departmentId || '',
          departmentName: projectContext?.departmentName || '',
          costCenter: '',
          warehouseId: '',
          locationId: '',
          requisitionType: projectContext ? 2 : 1,
          priority: 'Normal',
          requiredDate: '',
          purpose: '',
          notes: '',
        });
        setRequisitionDetail(null);
        setWarehouseLocations([]);
        setLocationStockItems([]);
      }
      loadDepartments();
    }
  }, [open, mode, projectContext]);

  // Load requisition details when editing/viewing
  useEffect(() => {
    if (open && requisitionId && (mode === 'edit' || mode === 'view')) {
      loadRequisitionDetail();
    }
  }, [open, requisitionId, mode]);

  // Load warehouse items when warehouse changes
  useEffect(() => {
    if (!formData.warehouseId) {
      setWarehouseInventoryItems([]);
      setWarehouseLocations([]);
      setLocationStockItems([]);
      return;
    }

    void loadWarehouseLocations(formData.warehouseId);
    if (canEdit) {
      void loadWarehouseItems(formData.warehouseId);
    }
  }, [formData.warehouseId, canEdit]);

  useEffect(() => {
    if (!canEdit || !formData.warehouseId || !formData.locationId) {
      setLocationStockItems([]);
      return;
    }

    void loadLocationStock(formData.warehouseId, formData.locationId);
  }, [formData.locationId, formData.warehouseId, canEdit]);

  useEffect(() => {
    setItemFormData((current) => ({
      ...current,
      locationId: formData.locationId || '',
    }));

    if (isCreateMode) {
      setPendingItems((current) =>
        current.map((item) => ({
          ...item,
          locationId: formData.locationId || undefined,
        })),
      );
    }
  }, [formData.locationId, isCreateMode]);

  const loadDepartments = async () => {
    try {
      const data = await inventoryRequisitionService.getDepartments();
      setDepartments(data);
    } catch (err) {
      console.error('Error loading departments:', err);
    }
  };

  const loadRequisitionDetail = async () => {
    if (!requisitionId) return;
    try {
      setLoading(true);
      const detail = await inventoryRequisitionService.getById(requisitionId);
      setRequisitionDetail(detail);
      setFormData({
        departmentId: detail.departmentId || '',
        departmentName: detail.departmentName || '',
        costCenter: detail.costCenter || '',
        warehouseId: detail.warehouseId || '',
        locationId: detail.locationId || '',
        requisitionType: detail.requisitionType || 1,
        priority: detail.priority || 'Normal',
        requiredDate: detail.requiredDate ? format(new Date(detail.requiredDate), 'yyyy-MM-dd') : '',
        purpose: detail.purpose || '',
        notes: detail.notes || ''
      });
    } catch (err) {
      console.error('Error loading requisition:', err);
      toast({ title: 'Error', description: 'Failed to load requisition details', variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  };

  const loadWarehouseItems = async (warehouseId: string) => {
    try {
      setLoadingItems(true);
      const items = await inventoryManagementService.getWarehouseInventoryItems(warehouseId);
      setWarehouseInventoryItems(items);
    } catch (err) {
      console.error('Error loading warehouse items:', err);
    } finally {
      setLoadingItems(false);
    }
  };

  const loadWarehouseLocations = async (warehouseId: string) => {
    try {
      setLoadingLocations(true);
      const locations = await inventoryManagementService.getWarehouseLocations(warehouseId);
      setWarehouseLocations(locations.filter((location) => location.isActive));
    } catch (err) {
      console.error('Error loading warehouse locations:', err);
      setWarehouseLocations([]);
    } finally {
      setLoadingLocations(false);
    }
  };

  const loadLocationStock = async (warehouseId: string, locationId: string) => {
    try {
      setLoadingLocationStock(true);
      const result = await inventoryManagementService.getBinStock({
        warehouseId,
        locationId,
        includeZero: false,
        page: 1,
        pageSize: 200,
      });
      setLocationStockItems(result.items || []);
    } catch (err) {
      console.error('Error loading location stock:', err);
      setLocationStockItems([]);
    } finally {
      setLoadingLocationStock(false);
    }
  };

  const handleSave = async () => {
    if (!formData.departmentId || !formData.warehouseId) {
      toast({ title: 'Validation Error', description: 'Please select department and warehouse', variant: 'destructive' });
      return;
    }
    try {
      setSaving(true);
      if (isCreateMode) {
        const createDto: CreateInventoryRequisitionDto = {
          departmentId: formData.departmentId,
          departmentName: formData.departmentName,
          costCenter: formData.costCenter,
          warehouseId: formData.warehouseId,
          locationId: formData.locationId || undefined,
          projectId: projectContext?.projectId,
          projectCode: projectContext?.projectCode,
          requisitionType: formData.requisitionType,
          priority: formData.priority,
          requiredDate: formData.requiredDate || undefined,
          purpose: formData.purpose,
          notes: formData.notes,
          items: pendingItems
        };
        await inventoryRequisitionService.create(createDto);
        toast({ title: 'Success', description: 'Requisition created successfully' });
      } else if (isEditMode && requisitionId) {
        await inventoryRequisitionService.update(requisitionId, {
          departmentId: formData.departmentId,
          departmentName: formData.departmentName,
          costCenter: formData.costCenter,
          warehouseId: formData.warehouseId,
          locationId: formData.locationId || undefined,
          projectId: projectContext?.projectId,
          projectCode: projectContext?.projectCode,
          requiredDate: formData.requiredDate || undefined,
          purpose: formData.purpose,
          notes: formData.notes
        });
        toast({ title: 'Success', description: 'Requisition updated successfully' });
      }
      onSuccess();
      onOpenChange(false);
    } catch (err: unknown) {
      console.error('Error saving requisition:', err);
      const errorMessage = err instanceof Error ? err.message : 'Failed to save requisition';
      toast({ title: 'Error', description: errorMessage, variant: 'destructive' });
    } finally {
      setSaving(false);
    }
  };

  const handleAddItem = async () => {
    if (!itemFormData.inventoryItemId || itemFormData.requestedQuantity <= 0) {
      toast({ title: 'Validation Error', description: 'Please select an item and enter quantity', variant: 'destructive' });
      return;
    }
    try {
      const selectedItem = selectableItems.find((item) => item.inventoryItemId === itemFormData.inventoryItemId);
      if (!selectedItem) {
        toast({
          title: 'Validation Error',
          description: formData.locationId
            ? 'Please choose an item that is available in the selected warehouse location.'
            : 'Please choose an item that is available in the selected warehouse.',
          variant: 'destructive'
        });
        return;
      }

      if (isCreateMode) {
        // Add to pending items for create mode
        setPendingItems((current) => [...current, {
            inventoryItemId: itemFormData.inventoryItemId,
            requestedQuantity: itemFormData.requestedQuantity,
            locationId: itemFormData.locationId || formData.locationId || undefined,
            lotNumber: itemFormData.lotNumber || undefined,
            batchNumber: itemFormData.batchNumber || undefined,
            serialNumber: itemFormData.serialNumber || undefined,
            notes: itemFormData.notes || undefined
          }]);
      } else if (requisitionId) {
        const dto: AddRequisitionItemDto = {
          inventoryItemId: itemFormData.inventoryItemId,
          requestedQuantity: itemFormData.requestedQuantity,
          locationId: itemFormData.locationId || formData.locationId || undefined,
          lotNumber: itemFormData.lotNumber || undefined,
          batchNumber: itemFormData.batchNumber || undefined,
          serialNumber: itemFormData.serialNumber || undefined,
          notes: itemFormData.notes || undefined
        };
        await inventoryRequisitionService.addItem(requisitionId, dto);
        await loadRequisitionDetail();
        toast({ title: 'Success', description: 'Item added successfully' });
      }
      setItemFormData({ inventoryItemId: '', locationId: formData.locationId || '', requestedQuantity: 1, lotNumber: '', batchNumber: '', serialNumber: '', notes: '' });
      setShowAddItem(false);
    } catch (err) {
      console.error('Error adding item:', err);
      toast({ title: 'Error', description: 'Failed to add item', variant: 'destructive' });
    }
  };

  const handleRemoveItem = async (itemId: string) => {
    if (isCreateMode) {
      setPendingItems(pendingItems.filter((_, idx) => idx.toString() !== itemId));
    } else if (requisitionId) {
      try {
        await inventoryRequisitionService.removeItem(requisitionId, itemId);
        await loadRequisitionDetail();
        toast({ title: 'Success', description: 'Item removed successfully' });
      } catch (err) {
        console.error('Error removing item:', err);
        toast({ title: 'Error', description: 'Failed to remove item', variant: 'destructive' });
      }
    }
  };

  const handleEditItem = (item: { id: string; requestedQuantity: number; notes?: string }) => {
    setEditingItemId(item.id);
    setEditingItemData({
      requestedQuantity: item.requestedQuantity,
      notes: item.notes || ''
    });
  };

  const handleCancelEdit = () => {
    setEditingItemId(null);
    setEditingItemData({ requestedQuantity: 1, notes: '' });
  };

  const handleSaveItemEdit = async () => {
    if (!editingItemId || editingItemData.requestedQuantity <= 0) {
      toast({ title: 'Validation Error', description: 'Please enter a valid quantity', variant: 'destructive' });
      return;
    }

    if (isCreateMode) {
      // Update pending items for create mode
      const idx = parseInt(editingItemId);
        const updatedItems = [...pendingItems];
        if (updatedItems[idx]) {
          updatedItems[idx] = {
            ...updatedItems[idx],
            requestedQuantity: editingItemData.requestedQuantity,
            locationId: formData.locationId || updatedItems[idx].locationId,
            notes: editingItemData.notes || undefined
          };
          setPendingItems(updatedItems);
        }
      setEditingItemId(null);
      setEditingItemData({ requestedQuantity: 1, notes: '' });
    } else if (requisitionId) {
      try {
        const dto: UpdateRequisitionItemDto = {
          requestedQuantity: editingItemData.requestedQuantity,
          locationId: formData.locationId || undefined,
          notes: editingItemData.notes || undefined
        };
        await inventoryRequisitionService.updateItem(requisitionId, editingItemId, dto);
        await loadRequisitionDetail();
        toast({ title: 'Success', description: 'Item updated successfully' });
        setEditingItemId(null);
        setEditingItemData({ requestedQuantity: 1, notes: '' });
      } catch (err) {
        console.error('Error updating item:', err);
        toast({ title: 'Error', description: 'Failed to update item', variant: 'destructive' });
      }
    }
  };

  const getStatusBadge = (status: number | string) => {
    const numStatus = normalizeStatus(status);
    const s = RequisitionStatuses.find(st => st.value === numStatus);
    return <Badge className={s?.color || 'bg-gray-100'}>{s?.label || RequisitionStatusMap[numStatus] || status}</Badge>;
  };

  const filteredWarehouseItems = warehouseInventoryItems.filter(item =>
    item.itemCode.toLowerCase().includes(itemSearchTerm.toLowerCase()) ||
    item.itemName.toLowerCase().includes(itemSearchTerm.toLowerCase())
  );
  const filteredLocationStockItems = locationStockItems.filter(item =>
    item.itemCode.toLowerCase().includes(itemSearchTerm.toLowerCase()) ||
    item.itemName.toLowerCase().includes(itemSearchTerm.toLowerCase()) ||
    item.locationCode.toLowerCase().includes(itemSearchTerm.toLowerCase())
  );
  const locationLookup = new Map(warehouseLocations.map((location) => [location.id, location]));
  const selectedLocation = formData.locationId ? locationLookup.get(formData.locationId) : undefined;
  const selectableItems = formData.locationId
    ? filteredLocationStockItems.map((item) => ({
        inventoryItemId: item.inventoryItemId,
        itemCode: item.itemCode,
        itemName: item.itemName,
        unitOfMeasure: item.unitOfMeasure,
        availableStock: item.availableQuantity,
        unitCost: item.averageCost,
        locationId: item.locationId,
        locationLabel: item.locationCode,
      }))
    : filteredWarehouseItems.map((item) => ({
        inventoryItemId: item.inventoryItemId,
        itemCode: item.itemCode,
        itemName: item.itemName,
        unitOfMeasure: item.unitOfMeasure,
        availableStock: item.availableStock,
        unitCost: item.unitCost,
        locationId: '',
        locationLabel: '',
      }));

  const displayItems = isCreateMode ? pendingItems.map((item, idx) => {
    const invItem =
      selectableItems.find((inventoryItem) => inventoryItem.inventoryItemId === item.inventoryItemId) ||
      warehouseInventoryItems.find((inventoryItem) => inventoryItem.inventoryItemId === item.inventoryItemId);
    const location = item.locationId ? locationLookup.get(item.locationId) : undefined;
    return {
      id: idx.toString(),
      inventoryItemId: item.inventoryItemId,
      itemCode: invItem?.itemCode || '',
      itemName: invItem?.itemName || '',
      requestedQuantity: item.requestedQuantity,
      approvedQuantity: 0,
      issuedQuantity: 0,
      unitOfMeasure: invItem?.unitOfMeasure || '',
      unitCost: invItem?.unitCost || 0,
      totalCost: (invItem?.unitCost || 0) * item.requestedQuantity,
      locationId: item.locationId,
      locationName: location?.name || location?.locationCode || selectedLocation?.name || selectedLocation?.locationCode,
      notes: item.notes
    };
  }) : (requisitionDetail?.items || []);

  return (
    <>
      <Dialog open={open} onOpenChange={onOpenChange}>
        <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <div className="flex flex-col gap-1">
              <DialogTitle>
                {isCreateMode ? 'New Requisition' : isEditMode ? 'Edit Requisition' : 'View Requisition'}
                {requisitionDetail && <span className="ml-2 text-muted-foreground">#{requisitionDetail.requisitionNumber}</span>}
              </DialogTitle>
              <div className="flex items-center gap-2">
                <span className="text-sm text-muted-foreground">
                  {isCreateMode
                    ? isProjectScoped
                      ? 'Create a project-linked inventory requisition'
                      : 'Create a new inventory requisition'
                    : isEditMode
                      ? 'Edit requisition details'
                      : 'View requisition details'}
                </span>
                {requisitionDetail && getStatusBadge(requisitionDetail.status)}
                {requisitionDetail && requisitionStatus === 2 && requisitionDetail.currentWorkflowStepName && (
                  <Badge variant="outline" className="text-xs">
                    Step: {requisitionDetail.currentWorkflowStepName}
                  </Badge>
                )}
              </div>
            </div>
          </DialogHeader>

          {loading ? (
            <div className="flex justify-center py-8"><div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div></div>
          ) : (
            <Tabs value={activeTab} onValueChange={setActiveTab}>
              <TabsList className={`grid w-full ${showApprovalsTab ? 'grid-cols-3' : 'grid-cols-2'}`}>
                <TabsTrigger value="details">Details</TabsTrigger>
                <TabsTrigger value="items">Items ({displayItems.length})</TabsTrigger>
                {showApprovalsTab && <WorkflowTabTrigger value="approvals" />}
              </TabsList>

              <TabsContent value="details" className="space-y-4">
                {projectContext ? (
                  <Card className="border-dashed">
                    <CardContent className="pt-4">
                      <div className="grid gap-3 md:grid-cols-3">
                        <div>
                          <div className="text-xs uppercase tracking-wide text-muted-foreground">Project</div>
                          <div className="font-medium">{projectContext.projectTitle}</div>
                        </div>
                        <div>
                          <div className="text-xs uppercase tracking-wide text-muted-foreground">Project Code</div>
                          <div className="font-medium">{projectContext.projectCode}</div>
                        </div>
                        <div>
                          <div className="text-xs uppercase tracking-wide text-muted-foreground">Context</div>
                          <div className="font-medium">Project Requisition</div>
                        </div>
                      </div>
                    </CardContent>
                  </Card>
                ) : null}
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label>Department *</Label>
                    <Select value={formData.departmentId} onValueChange={(v) => {
                      const dept = departments.find(d => d.id === v);
                      setFormData({ ...formData, departmentId: v, departmentName: dept?.name || '' });
                    }} disabled={!canEdit || (!!projectContext?.departmentId && isCreateMode)}>
                      <SelectTrigger><SelectValue placeholder="Select department" /></SelectTrigger>
                      <SelectContent>
                        {departments.map(d => <SelectItem key={d.id} value={d.id}>{d.name}</SelectItem>)}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label>Warehouse *</Label>
                    <Select value={formData.warehouseId} onValueChange={(v) => setFormData({ ...formData, warehouseId: v, locationId: '' })} disabled={!canEdit}>
                      <SelectTrigger><SelectValue placeholder="Select warehouse" /></SelectTrigger>
                      <SelectContent>
                        {warehouses.map(w => <SelectItem key={w.id} value={w.id}>{w.name}</SelectItem>)}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label>Warehouse Location</Label>
                    <Select value={formData.locationId || '__none__'} onValueChange={(v) => setFormData({ ...formData, locationId: v === '__none__' ? '' : v })} disabled={!formData.warehouseId || !canEdit || loadingLocations}>
                      <SelectTrigger><SelectValue placeholder={loadingLocations ? 'Loading locations...' : 'All locations'} /></SelectTrigger>
                      <SelectContent>
                        <SelectItem value="__none__">All locations</SelectItem>
                        {warehouseLocations.map(location => (
                          <SelectItem key={location.id} value={location.id}>
                            {location.locationCode}{location.name ? ` - ${location.name}` : ''}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label>Requisition Type</Label>
                    <Select value={formData.requisitionType.toString()} onValueChange={(v) => setFormData({ ...formData, requisitionType: parseInt(v) })} disabled={!canEdit || isProjectScoped}>
                      <SelectTrigger><SelectValue /></SelectTrigger>
                      <SelectContent>
                        {RequisitionTypes.map(t => <SelectItem key={t.value} value={t.value.toString()}>{t.label}</SelectItem>)}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label>Priority</Label>
                    <Select value={formData.priority} onValueChange={(v) => setFormData({ ...formData, priority: v })} disabled={!canEdit}>
                      <SelectTrigger><SelectValue /></SelectTrigger>
                      <SelectContent>
                        {Priorities.map(p => <SelectItem key={p.value} value={p.value}>{p.label}</SelectItem>)}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label>Cost Center</Label>
                    <Input value={formData.costCenter} onChange={(e) => setFormData({ ...formData, costCenter: e.target.value })} disabled={!canEdit} />
                  </div>
                  <div className="space-y-2">
                    <Label>Required Date</Label>
                    <Input type="date" value={formData.requiredDate} onChange={(e) => setFormData({ ...formData, requiredDate: e.target.value })} disabled={!canEdit} />
                  </div>
                </div>
                <div className="space-y-2">
                  <Label>Purpose</Label>
                  <Textarea value={formData.purpose} onChange={(e) => setFormData({ ...formData, purpose: e.target.value })} disabled={!canEdit} rows={2} />
                </div>
                <div className="space-y-2">
                  <Label>Notes</Label>
                  <Textarea value={formData.notes} onChange={(e) => setFormData({ ...formData, notes: e.target.value })} disabled={!canEdit} rows={2} />
                </div>
              </TabsContent>

              <TabsContent value="items" className="space-y-4">
                {canEdit && (
                  <div className="flex justify-between items-center">
                    <Button variant="outline" size="sm" onClick={() => setShowAddItem(!showAddItem)}>
                      <Plus className="h-4 w-4 mr-1" />Add Item
                    </Button>
                  </div>
                )}

                {showAddItem && canEdit && (
                  <Card>
                    <CardHeader><CardTitle className="text-sm">Add Item</CardTitle></CardHeader>
                    <CardContent className="space-y-4">
                      <div className="rounded-md border border-dashed bg-muted/30 p-3 text-sm text-muted-foreground">
                        {formData.locationId
                          ? `Showing stock from ${selectedLocation?.locationCode || 'selected location'}${selectedLocation?.name ? ` - ${selectedLocation.name}` : ''}.`
                          : 'Showing warehouse-level stock. Select a warehouse location above to filter items by bin/location and view exact available quantities there.'}
                      </div>
                      <div className="relative">
                        <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                        <Input placeholder="Search items..." className="pl-8" value={itemSearchTerm} onChange={(e) => setItemSearchTerm(e.target.value)} />
                      </div>
                      <Select value={itemFormData.inventoryItemId} onValueChange={(v) => setItemFormData({ ...itemFormData, inventoryItemId: v })}>
                        <SelectTrigger><SelectValue placeholder="Select item" /></SelectTrigger>
                        <SelectContent>
                          {selectableItems.map(item => (
                            <SelectItem key={`${item.inventoryItemId}:${item.locationId || 'warehouse'}`} value={item.inventoryItemId}>
                              {item.itemCode} - {item.itemName} (Avail: {item.availableStock}{item.locationLabel ? ` | ${item.locationLabel}` : ''})
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      {(loadingItems || loadingLocationStock) ? (
                        <div className="text-sm text-muted-foreground">
                          Loading stock availability...
                        </div>
                      ) : null}
                      {!loadingItems && !loadingLocationStock && selectableItems.length === 0 ? (
                        <div className="text-sm text-muted-foreground">
                          {formData.locationId
                            ? 'No stocked items are currently available in the selected warehouse location.'
                            : 'No stocked items are currently available in the selected warehouse.'}
                        </div>
                      ) : null}
                      <div className="grid grid-cols-2 gap-4">
                        <div className="space-y-2">
                          <Label>Quantity</Label>
                          <Input type="number" min="1" value={itemFormData.requestedQuantity} onChange={(e) => setItemFormData({ ...itemFormData, requestedQuantity: parseInt(e.target.value) || 1 })} />
                        </div>
                        <div className="space-y-2">
                          <Label>Notes</Label>
                          <Input value={itemFormData.notes} onChange={(e) => setItemFormData({ ...itemFormData, notes: e.target.value })} />
                        </div>
                      </div>
                      <div className="grid grid-cols-3 gap-2"><div className="space-y-2"><Label>Lot</Label><Input value={itemFormData.lotNumber} onChange={event => setItemFormData({ ...itemFormData, lotNumber: event.target.value })} /></div><div className="space-y-2"><Label>Batch</Label><Input value={itemFormData.batchNumber} onChange={event => setItemFormData({ ...itemFormData, batchNumber: event.target.value })} /></div><div className="space-y-2"><Label>Serial</Label><Input value={itemFormData.serialNumber} onChange={event => setItemFormData({ ...itemFormData, serialNumber: event.target.value })} /></div></div>
                      <Button size="sm" onClick={handleAddItem}>Add</Button>
                    </CardContent>
                  </Card>
                )}

                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Item</TableHead>
                      <TableHead>Tracking</TableHead>
                      <TableHead>Location</TableHead>
                      <TableHead>Requested</TableHead>
                      <TableHead>Approved</TableHead>
                      <TableHead>Issued</TableHead>
                      <TableHead>UoM</TableHead>
                      <TableHead>Unit Cost</TableHead>
                      <TableHead>Total</TableHead>
                      {canEdit && <TableHead></TableHead>}
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {displayItems.length === 0 ? (
                      <TableRow><TableCell colSpan={canEdit ? 10 : 9} className="text-center text-muted-foreground">No items added</TableCell></TableRow>
                    ) : (
                      displayItems.map((item) => (
                        <TableRow key={item.id}>
                          <TableCell><div className="font-medium">{item.itemCode}</div><div className="text-sm text-muted-foreground">{item.itemName}</div></TableCell>
                          <TableCell className="text-xs">{[item.lotNumber && `Lot ${item.lotNumber}`, item.batchNumber && `Batch ${item.batchNumber}`, item.serialNumber && `SN ${item.serialNumber}`].filter(Boolean).join(' · ') || '—'}</TableCell>
                          <TableCell>{item.locationName || (requisitionDetail?.locationName ?? 'Warehouse level')}</TableCell>
                          <TableCell>
                            {editingItemId === item.id ? (
                              <Input
                                type="number"
                                min="1"
                                className="w-20"
                                value={editingItemData.requestedQuantity}
                                onChange={(e) => setEditingItemData({ ...editingItemData, requestedQuantity: parseInt(e.target.value) || 1 })}
                              />
                            ) : (
                              item.requestedQuantity
                            )}
                          </TableCell>
                          <TableCell>{item.approvedQuantity}</TableCell>
                          <TableCell>{item.issuedQuantity}</TableCell>
                          <TableCell>{item.unitOfMeasure}</TableCell>
                          <TableCell>{item.unitCost.toFixed(2)}</TableCell>
                          <TableCell>
                            {editingItemId === item.id
                              ? ((item.unitCost || 0) * editingItemData.requestedQuantity).toFixed(2)
                              : item.totalCost.toFixed(2)
                            }
                          </TableCell>
                          {canEdit && (
                            <TableCell>
                              <div className="flex gap-1">
                                {editingItemId === item.id ? (
                                  <>
                                    <Button variant="ghost" size="sm" onClick={handleSaveItemEdit} title="Save">
                                      <Check className="h-4 w-4 text-green-500" />
                                    </Button>
                                    <Button variant="ghost" size="sm" onClick={handleCancelEdit} title="Cancel">
                                      <X className="h-4 w-4 text-gray-500" />
                                    </Button>
                                  </>
                                ) : (
                                  <>
                                    <Button variant="ghost" size="sm" onClick={() => handleEditItem(item)} title="Edit">
                                      <Pencil className="h-4 w-4 text-blue-500" />
                                    </Button>
                                    <Button variant="ghost" size="sm" onClick={() => handleRemoveItem(item.id)} title="Delete">
                                      <Trash2 className="h-4 w-4 text-red-500" />
                                    </Button>
                                  </>
                                )}
                              </div>
                            </TableCell>
                          )}
                        </TableRow>
                      ))
                    )}
                  </TableBody>
                </Table>
              </TabsContent>

              {showApprovalsTab && requisitionDetail && (
                <WorkflowTabContent
                  value="approvals"
                  entityType="InventoryRequisition"
                  entityId={requisitionDetail.id}
                  entityLabel="Inventory Requisition"
                  entityNumber={requisitionDetail.requisitionNumber}
                  status={RequisitionStatusMap[requisitionStatus] || 'Draft'}
                  currentStepName={requisitionDetail.currentWorkflowStepName}
                  canSubmit={requisitionStatus === 1}
                  canApproveReject={requisitionStatus === 2}
                  onSubmit={async () => {
                    await inventoryRequisitionService.submit(requisitionDetail.id);
                  }}
                  onApprove={async (comments) => {
                    await inventoryRequisitionService.approve(requisitionDetail.id, comments || undefined);
                  }}
                  onReject={async (comments) => {
                    await inventoryRequisitionService.reject(requisitionDetail.id, comments);
                  }}
                  onAfterAction={async () => {
                    await loadRequisitionDetail();
                  }}
                />
              )}
            </Tabs>
          )}

          <DialogFooter className="flex justify-between">
            <div className="flex gap-2">
              {requisitionDetail && displayItems.length > 0 && (
                <WorkflowApprovalActions
                  entityType="InventoryRequisition"
                  entityId={requisitionDetail.id}
                  entityLabel="Inventory Requisition"
                  entityNumber={requisitionDetail.requisitionNumber}
                  status={RequisitionStatusMap[requisitionStatus] || 'Draft'}
                  currentStepName={requisitionDetail.currentWorkflowStepName}
                  showStepBadge={requisitionStatus === 2 && !!requisitionDetail.currentWorkflowStepName}
                  loadWorkflowSummary={requisitionStatus === 2}
                  canSubmit={requisitionStatus === 1}
                  canApproveReject={requisitionStatus === 2}
                  onSubmit={async () => {
                    try {
                      await inventoryRequisitionService.submit(requisitionDetail.id);
                    } catch (err: any) {
                      const msg =
                        err?.response?.data?.error ||
                        err?.response?.data?.message ||
                        err?.response?.data ||
                        err?.message ||
                        'Failed to submit requisition for approval';
                      throw new Error(typeof msg === 'string' ? msg : 'Failed to submit requisition for approval');
                    }
                  }}
                  onApprove={async (comments) => {
                    try {
                      await inventoryRequisitionService.approve(requisitionDetail.id, comments || undefined);
                    } catch (err: any) {
                      const msg =
                        err?.response?.data?.error ||
                        err?.response?.data?.message ||
                        err?.response?.data ||
                        err?.message ||
                        'Failed to approve requisition';
                      throw new Error(typeof msg === 'string' ? msg : 'Failed to approve requisition');
                    }
                  }}
                  onReject={async (comments) => {
                    try {
                      await inventoryRequisitionService.reject(requisitionDetail.id, comments);
                    } catch (err: any) {
                      const msg =
                        err?.response?.data?.error ||
                        err?.response?.data?.message ||
                        err?.response?.data ||
                        err?.message ||
                        'Failed to reject requisition';
                      throw new Error(typeof msg === 'string' ? msg : 'Failed to reject requisition');
                    }
                  }}
                  onAfterAction={async () => {
                    await loadRequisitionDetail();
                    onSuccess();
                  }}
                  onOpenWorkflows={() => router.push('/administration/workflow')}
                  size="sm"
                />
              )}
            </div>
            <div className="flex gap-2">
              <Button variant="outline" onClick={() => onOpenChange(false)}>
                {mode === 'view' ? 'Close' : 'Cancel'}
              </Button>
              {canEdit && (
                <Button onClick={handleSave} disabled={saving}>
                  {saving ? 'Saving...' : 'Save'}
                </Button>
              )}
            </div>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
