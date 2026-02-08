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
import { Separator } from '@/components/ui/separator';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowApprovalHistoryPanel } from '@/components/workflow/WorkflowApprovalHistoryPanel';
import { Plus, Trash2, Search, Package, AlertCircle, Barcode, Layers } from 'lucide-react';
import {
  inventoryManagementService,
  InventoryTransferDto, InventoryTransferDetailDto, InventoryTransferItemDto,
  WarehouseDto, InventoryItemDto, AddTransferItemDto, UpdateTransferItemDto, WarehouseLocationDto
} from '@/services/inventoryManagementService';
import { useToast } from '@/hooks/use-toast';
import { format } from 'date-fns';

// DTO for warehouse inventory items returned by the by-warehouse endpoint
interface WarehouseInventoryItemDto {
  inventoryItemId: string;
  itemCode: string;
  itemName: string;
  itemType: number;
  description?: string;
  unitOfMeasure?: string;
  currentStock: number;
  availableStock: number;
  allocatedStock: number;
  unitCost: number;
  dailyRentalRate: number;
  categoryName?: string;
  isSerialTracked?: boolean;
  isLotTracked?: boolean;
}

interface TransferDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  transfer?: InventoryTransferDto | null;
  mode: 'create' | 'edit' | 'view';
  warehouses: WarehouseDto[];
  onSuccess: () => void;
}

interface FormData {
  sourceWarehouseId: string;
  destinationWarehouseId: string;
  requiredDate: string;
  notes: string;
}

interface ItemFormData {
  inventoryItemId: string;
  requestedQuantity: number;
  sourceLocationId: string;
  destinationLocationId: string;
  lotNumber: string;
  serialNumber: string;
  notes: string;
}

const TransferStatuses = [
  { value: 'Draft', label: 'Draft', color: 'bg-gray-100 text-gray-800' },
  { value: 'Submitted', label: 'Pending Approval', color: 'bg-yellow-100 text-yellow-800' },
  { value: 'Approved', label: 'Approved', color: 'bg-blue-100 text-blue-800' },
  { value: 'InTransit', label: 'In Transit', color: 'bg-purple-100 text-purple-800' },
  { value: 'Received', label: 'Received', color: 'bg-green-100 text-green-800' },
  { value: 'Completed', label: 'Completed', color: 'bg-green-200 text-green-900' },
  { value: 'Cancelled', label: 'Cancelled', color: 'bg-red-100 text-red-800' }
];

export function TransferDialog({ open, onOpenChange, transfer, mode, warehouses, onSuccess }: TransferDialogProps) {
  const { toast } = useToast();
  const router = useRouter();
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [activeTab, setActiveTab] = useState('details');
  const [transferDetail, setTransferDetail] = useState<InventoryTransferDetailDto | null>(null);
  const [warehouseInventoryItems, setWarehouseInventoryItems] = useState<WarehouseInventoryItemDto[]>([]);
  const [loadingItems, setLoadingItems] = useState(false);
  const [sourceLocations, setSourceLocations] = useState<WarehouseLocationDto[]>([]);
  const [destinationLocations, setDestinationLocations] = useState<WarehouseLocationDto[]>([]);
  const [itemSearchTerm, setItemSearchTerm] = useState('');
  const [showAddItem, setShowAddItem] = useState(false);
  const [editingItemId, setEditingItemId] = useState<string | null>(null);
  
  const [formData, setFormData] = useState<FormData>({
    sourceWarehouseId: '',
    destinationWarehouseId: '',
    requiredDate: '',
    notes: ''
  });

  const [itemFormData, setItemFormData] = useState<ItemFormData>({
    inventoryItemId: '',
    requestedQuantity: 1,
    sourceLocationId: '',
    destinationLocationId: '',
    lotNumber: '',
    serialNumber: '',
    notes: ''
  });

  const isEditable = mode !== 'view' && (mode === 'create' || transferDetail?.status === 'Draft');

  // Fetch transfer details when editing/viewing
  useEffect(() => {
    if (open && transfer?.id && mode !== 'create') {
      loadTransferDetails();
    } else if (open && mode === 'create') {
      resetForm();
    }
  }, [open, transfer?.id, mode]);

  // Load inventory items when source warehouse changes
  useEffect(() => {
    if (formData.sourceWarehouseId && isEditable) {
      loadWarehouseInventoryItems(formData.sourceWarehouseId);
      loadSourceLocations(formData.sourceWarehouseId);
    } else {
      setWarehouseInventoryItems([]);
      setSourceLocations([]);
    }
  }, [formData.sourceWarehouseId, isEditable]);

  // Load destination locations when destination warehouse changes
  useEffect(() => {
    if (formData.destinationWarehouseId && isEditable) {
      loadDestinationLocations(formData.destinationWarehouseId);
    } else {
      setDestinationLocations([]);
    }
  }, [formData.destinationWarehouseId, isEditable]);

  const loadTransferDetails = async () => {
    if (!transfer?.id) return;
    try {
      setLoading(true);
      const detail = await inventoryManagementService.getInventoryTransferById(transfer.id);
      setTransferDetail(detail);
      setFormData({
        sourceWarehouseId: detail.sourceWarehouseId,
        destinationWarehouseId: detail.destinationWarehouseId,
        requiredDate: detail.expectedDeliveryDate ? detail.expectedDeliveryDate.split('T')[0] : '',
        notes: detail.notes || ''
      });
    } catch (err) {
      console.error('Error loading transfer details:', err);
    } finally {
      setLoading(false);
    }
  };

  const loadWarehouseInventoryItems = async (warehouseId: string) => {
    if (!warehouseId) {
      setWarehouseInventoryItems([]);
      return;
    }
    try {
      setLoadingItems(true);
      // Load items from the source warehouse - endpoint returns items WITH stock
      const items = await inventoryManagementService.getInventoryByWarehouse(warehouseId);
      // Filter to only show items with available stock > 0
      const itemsWithStock = items.filter((item: WarehouseInventoryItemDto) => item.availableStock > 0);
      setWarehouseInventoryItems(itemsWithStock);
    } catch (err) {
      console.error('Error loading warehouse inventory items:', err);
      setWarehouseInventoryItems([]);
    } finally {
      setLoadingItems(false);
    }
  };

  const resetForm = () => {
    setFormData({
      sourceWarehouseId: '',
      destinationWarehouseId: '',
      requiredDate: '',
      notes: ''
    });
    setTransferDetail(null);
    setActiveTab('details');
    setWarehouseInventoryItems([]);
    resetItemForm();
  };

  const resetItemForm = () => {
    setItemFormData({
      inventoryItemId: '',
      requestedQuantity: 1,
      sourceLocationId: '',
      destinationLocationId: '',
      lotNumber: '',
      serialNumber: '',
      notes: ''
    });
    setShowAddItem(false);
    setEditingItemId(null);
  };

  const loadSourceLocations = async (warehouseId: string) => {
    try {
      const locations = await inventoryManagementService.getWarehouseLocations(warehouseId);
      setSourceLocations((locations || []).filter(l => l.isActive));
    } catch (err) {
      console.error('Error loading source locations:', err);
      setSourceLocations([]);
    }
  };

  const loadDestinationLocations = async (warehouseId: string) => {
    try {
      const locations = await inventoryManagementService.getWarehouseLocations(warehouseId);
      setDestinationLocations((locations || []).filter(l => l.isActive));
    } catch (err) {
      console.error('Error loading destination locations:', err);
      setDestinationLocations([]);
    }
  };

  const getStatusBadge = (status: string) => {
    const s = TransferStatuses.find(st => st.value === status);
    return <Badge className={s?.color || 'bg-gray-100'}>{s?.label || status}</Badge>;
  };

  const transferSubtotal = transferDetail?.items?.reduce((sum, item) => {
    const lineCost = item.totalCost ?? ((item.unitCost || 0) * (item.requestedQuantity || 0));
    return sum + lineCost;
  }, 0) ?? 0;
  const transferShippingCost = transferDetail?.shippingCost || 0;
  const transferMiscCost = transferDetail?.miscellaneousCost || 0;
  const transferAdditionalCost = transferDetail?.totalAdditionalCost ?? (transferShippingCost + transferMiscCost);
  const transferGrandTotal = transferSubtotal + transferAdditionalCost;

  // Use inventoryItemId field for warehouse inventory items
  const selectedItem = warehouseInventoryItems.find(i => i.inventoryItemId === itemFormData.inventoryItemId);

  // Filter warehouse inventory items by search term
  const filteredInventoryItems = warehouseInventoryItems.filter(item =>
    item.itemName.toLowerCase().includes(itemSearchTerm.toLowerCase()) ||
    item.itemCode.toLowerCase().includes(itemSearchTerm.toLowerCase())
  );

  const handleSaveTransfer = async () => {
    try {
      setSaving(true);
      if (mode === 'create') {
        await inventoryManagementService.createInventoryTransfer({
          sourceWarehouseId: formData.sourceWarehouseId,
          destinationWarehouseId: formData.destinationWarehouseId,
          expectedDeliveryDate: formData.requiredDate || undefined,
          notes: formData.notes || undefined,
          items: []
        });
      } else if (mode === 'edit' && transfer?.id) {
        await inventoryManagementService.updateInventoryTransfer(transfer.id, {
          sourceWarehouseId: formData.sourceWarehouseId,
          destinationWarehouseId: formData.destinationWarehouseId,
          requiredDate: formData.requiredDate || undefined,
          notes: formData.notes || undefined
        });
      }
      onSuccess();
      onOpenChange(false);
    } catch (err) {
      console.error('Error saving transfer:', err);
      toast({
        title: 'Error',
        description: 'Failed to save transfer',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const handleAddItem = async () => {
    if (!transfer?.id || !itemFormData.inventoryItemId || itemFormData.requestedQuantity <= 0) return;
    try {
      setSaving(true);

      const isInterBin = !!formData.sourceWarehouseId &&
        !!formData.destinationWarehouseId &&
        formData.sourceWarehouseId === formData.destinationWarehouseId;

      if (isInterBin) {
        if (!itemFormData.sourceLocationId || !itemFormData.destinationLocationId) {
          toast({ title: 'Validation', description: 'For inter-bin transfers, please select both a source bin and a destination bin.', variant: 'destructive' });
          return;
        }
        if (itemFormData.sourceLocationId === itemFormData.destinationLocationId) {
          toast({ title: 'Validation', description: 'Source bin and destination bin must be different.', variant: 'destructive' });
          return;
        }
      }

      const dto: AddTransferItemDto = {
        inventoryItemId: itemFormData.inventoryItemId,
        requestedQuantity: itemFormData.requestedQuantity,
        sourceLocationId: itemFormData.sourceLocationId || undefined,
        destinationLocationId: itemFormData.destinationLocationId || undefined,
        lotNumber: itemFormData.lotNumber || undefined,
        serialNumber: itemFormData.serialNumber || undefined,
        notes: itemFormData.notes || undefined
      };
      await inventoryManagementService.addTransferItem(transfer.id, dto);
      await loadTransferDetails();
      resetItemForm();
    } catch (err: any) {
      console.error('Error adding item:', err);
      toast({
        title: 'Error',
        description: err.response?.data || 'Failed to add item',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const handleUpdateItem = async (itemId: string) => {
    if (!transfer?.id) return;
    try {
      setSaving(true);

      const isInterBin = !!formData.sourceWarehouseId &&
        !!formData.destinationWarehouseId &&
        formData.sourceWarehouseId === formData.destinationWarehouseId;

      if (isInterBin) {
        if (!itemFormData.sourceLocationId || !itemFormData.destinationLocationId) {
          toast({ title: 'Validation', description: 'For inter-bin transfers, please select both a source bin and a destination bin.', variant: 'destructive' });
          return;
        }
        if (itemFormData.sourceLocationId === itemFormData.destinationLocationId) {
          toast({ title: 'Validation', description: 'Source bin and destination bin must be different.', variant: 'destructive' });
          return;
        }
      }

      const dto: UpdateTransferItemDto = {
        requestedQuantity: itemFormData.requestedQuantity,
        sourceLocationId: itemFormData.sourceLocationId || undefined,
        destinationLocationId: itemFormData.destinationLocationId || undefined,
        lotNumber: itemFormData.lotNumber || undefined,
        serialNumber: itemFormData.serialNumber || undefined,
        notes: itemFormData.notes || undefined
      };
      await inventoryManagementService.updateTransferItem(transfer.id, itemId, dto);
      await loadTransferDetails();
      resetItemForm();
    } catch (err: any) {
      console.error('Error updating item:', err);
      toast({
        title: 'Error',
        description: err.response?.data || 'Failed to update item',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const handleRemoveItem = async (itemId: string) => {
    if (!transfer?.id) return;
    try {
      setSaving(true);
      await inventoryManagementService.removeTransferItem(transfer.id, itemId);
      await loadTransferDetails();
      toast({
        title: 'Success',
        description: 'Item removed from transfer',
      });
    } catch (err: any) {
      console.error('Error removing item:', err);
      toast({
        title: 'Error',
        description: err.response?.data || 'Failed to remove item',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const startEditItem = (item: InventoryTransferItemDto) => {
    setItemFormData({
      inventoryItemId: item.inventoryItemId,
      requestedQuantity: item.requestedQuantity,
      sourceLocationId: item.sourceLocationId || '',
      destinationLocationId: item.destinationLocationId || '',
      lotNumber: item.lotNumber || '',
      serialNumber: item.serialNumber || '',
      notes: item.notes || ''
    });
    setEditingItemId(item.id);
    setShowAddItem(true);
  };

  const showApprovalsTab = !!transferDetail && mode !== 'create';

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className={`${mode === 'view' ? 'max-w-[90vw] lg:max-w-5xl' : 'max-w-[95vw] lg:max-w-7xl'} max-h-[90vh] overflow-y-auto`}>
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            {mode === 'create' ? 'Create Inventory Transfer' : `Transfer ${transferDetail?.transferNumber || ''}`}
            {transferDetail && getStatusBadge(transferDetail.status)}
          </DialogTitle>
          <DialogDescription>
            {mode === 'create' ? 'Create a new stock transfer (inter-warehouse or inter-bin)' :
             mode === 'edit' ? 'Edit transfer details and manage items' : 'View transfer details'}
          </DialogDescription>
        </DialogHeader>

        {loading ? (
          <div className="py-8 text-center text-muted-foreground">Loading...</div>
        ) : (
          <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full">
            <TabsList className={`grid w-full ${showApprovalsTab ? 'grid-cols-3' : 'grid-cols-2'}`}>
              <TabsTrigger value="details">Transfer Details</TabsTrigger>
              <TabsTrigger value="items" disabled={mode === 'create' && !transfer?.id}>
                Items ({transferDetail?.items?.length || 0})
              </TabsTrigger>
              {showApprovalsTab && (
                <TabsTrigger value="approvals">Approvals</TabsTrigger>
              )}
            </TabsList>

            {/* Details Tab */}
            <TabsContent value="details" className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Source Warehouse *</Label>
                  <Select
                    value={formData.sourceWarehouseId}
                    onValueChange={(v) => setFormData({...formData, sourceWarehouseId: v})}
                    disabled={!isEditable}
                  >
                    <SelectTrigger><SelectValue placeholder="Select source warehouse" /></SelectTrigger>
                    <SelectContent>
                      {warehouses.map(w => <SelectItem key={w.id} value={w.id}>{w.name}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>Destination Warehouse *</Label>
                  <Select
                    value={formData.destinationWarehouseId}
                    onValueChange={(v) => setFormData({...formData, destinationWarehouseId: v})}
                    disabled={!isEditable}
                  >
                    <SelectTrigger><SelectValue placeholder="Select destination warehouse" /></SelectTrigger>
                    <SelectContent>
                      {warehouses.map(w => <SelectItem key={w.id} value={w.id}>{w.name}</SelectItem>)}
                    </SelectContent>
                  </Select>
                  {formData.sourceWarehouseId && formData.destinationWarehouseId && formData.sourceWarehouseId === formData.destinationWarehouseId && (
                    <p className="text-xs text-muted-foreground">
                      Inter-bin transfer mode: bins are required on each line item.
                    </p>
                  )}
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Required Date</Label>
                  <Input
                    type="date"
                    value={formData.requiredDate}
                    onChange={(e) => setFormData({...formData, requiredDate: e.target.value})}
                    disabled={!isEditable}
                  />
                </div>
                {transferDetail && (
                  <div className="space-y-2">
                    <Label>Request Date</Label>
                    <Input
                      type="text"
                      value={transferDetail.requestedDate ? format(new Date(transferDetail.requestedDate), 'MMM dd, yyyy') : '-'}
                      disabled
                    />
                  </div>
                )}
              </div>

              <div className="space-y-2">
                <Label>Notes</Label>
                <Textarea
                  value={formData.notes}
                  onChange={(e) => setFormData({...formData, notes: e.target.value})}
                  disabled={!isEditable}
                  rows={3}
                  placeholder="Add any notes or instructions for this transfer..."
                />
              </div>

              {/* Transfer Summary */}
              {transferDetail && (
                <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
                  <Card>
                    <CardHeader className="pb-2">
                      <CardTitle className="text-sm">Quantity Summary</CardTitle>
                    </CardHeader>
                    <CardContent>
                      <div className="grid grid-cols-3 gap-4 text-sm">
                        <div>
                          <span className="text-muted-foreground">Total Items:</span>
                          <span className="ml-2 font-medium">{transferDetail.items?.length || 0}</span>
                        </div>
                        <div>
                          <span className="text-muted-foreground">Total Quantity:</span>
                          <span className="ml-2 font-medium">
                            {transferDetail.items?.reduce((sum, i) => sum + i.requestedQuantity, 0) || 0}
                          </span>
                        </div>
                        <div>
                          <span className="text-muted-foreground">Total Value:</span>
                          <span className="ml-2 font-medium">
                            ${transferSubtotal.toFixed(2)}
                          </span>
                        </div>
                      </div>
                    </CardContent>
                  </Card>

                  <Card>
                    <CardHeader className="pb-2">
                      <CardTitle className="text-sm">Additional Cost & Allocation</CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-2 text-sm">
                      <div className="flex justify-between">
                        <span className="text-muted-foreground">Shipping:</span>
                        <span className="font-medium">${(transferDetail.shippingCost || 0).toFixed(2)}</span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-muted-foreground">Miscellaneous:</span>
                        <span className="font-medium">${(transferDetail.miscellaneousCost || 0).toFixed(2)}</span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-muted-foreground">Total Additional:</span>
                        <span className="font-medium">${(transferDetail.totalAdditionalCost || 0).toFixed(2)}</span>
                      </div>
                      <Separator />
                      <div className="flex justify-between">
                        <span className="text-muted-foreground">Allocation Method:</span>
                        <span className="font-medium">
                          {(transferDetail.costAllocationMethod || 'SpreadToItemCost') === 'GLExpense'
                            ? 'Post to GL expense'
                            : 'Spread to item cost'}
                        </span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-muted-foreground">Spread Basis:</span>
                        <span className="font-medium">{transferDetail.costApportionmentBasis || 'Value'}</span>
                      </div>
                      {(transferDetail.costAllocationMethod || 'SpreadToItemCost') === 'GLExpense' && (
                        <div className="flex justify-between">
                          <span className="text-muted-foreground">GL Account:</span>
                          <span className="font-medium">{transferDetail.expenseGLAccount || '-'}</span>
                        </div>
                      )}
                      <div className="flex justify-between">
                        <span className="text-muted-foreground">Costs Allocated:</span>
                        <span className="font-medium">{transferDetail.costsAllocated ? 'Yes' : 'No'}</span>
                      </div>
                    </CardContent>
                  </Card>

                  <Card className="w-full xl:ml-auto xl:max-w-md">
                    <CardHeader className="pb-2">
                      <CardTitle className="text-sm">Financial Summary</CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-2 text-sm">
                      <div className="flex justify-between">
                        <span className="text-muted-foreground">Subtotal:</span>
                        <span className="font-medium">${transferSubtotal.toFixed(2)}</span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-muted-foreground">Shipping:</span>
                        <span className="font-medium">${transferShippingCost.toFixed(2)}</span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-muted-foreground">Miscellaneous:</span>
                        <span className="font-medium">${transferMiscCost.toFixed(2)}</span>
                      </div>
                      <Separator />
                      <div className="flex justify-between">
                        <span className="font-semibold">Total Transfer Value:</span>
                        <span className="font-semibold">${transferGrandTotal.toFixed(2)}</span>
                      </div>
                    </CardContent>
                  </Card>
                </div>
              )}
            </TabsContent>

            {/* Items Tab - will be added next */}
            <TabsContent value="items" className="space-y-4">
              {renderItemsTab()}
            </TabsContent>

            {showApprovalsTab && transferDetail && (
              <TabsContent value="approvals" className="space-y-4 mt-4">
                <WorkflowApprovalHistoryPanel entityType="InventoryTransfer" entityId={transferDetail.id} />
              </TabsContent>
            )}
          </Tabs>
        )}

        <DialogFooter className="flex justify-between">
          <div className="flex gap-2">
            {transferDetail && transferDetail.items?.length > 0 && (
              <WorkflowApprovalActions
                entityType="InventoryTransfer"
                entityId={transferDetail.id}
                entityLabel="Inventory Transfer"
                entityNumber={transferDetail.transferNumber}
                status={transferDetail.status}
                currentStepName={transferDetail.currentWorkflowStepName}
                showStepBadge={transferDetail.status === 'Submitted' && !!transferDetail.currentWorkflowStepName}
                loadWorkflowSummary={transferDetail.status === 'Submitted'}
                canSubmit={transferDetail.status === 'Draft'}
                canApproveReject={transferDetail.status === 'Submitted'}
                onSubmit={async () => {
                  try {
                    await inventoryManagementService.submitTransferForApproval(transferDetail.id);
                  } catch (err: any) {
                    const msg =
                      err?.response?.data?.error ||
                      err?.response?.data ||
                      err?.message ||
                      'Failed to submit transfer for approval';
                    throw new Error(typeof msg === 'string' ? msg : 'Failed to submit transfer for approval');
                  }
                }}
                onApprove={async (comments) => {
                  try {
                    await inventoryManagementService.approveTransfer(transferDetail.id, comments || undefined);
                  } catch (err: any) {
                    const msg =
                      err?.response?.data?.error ||
                      err?.response?.data ||
                      err?.message ||
                      'Failed to approve transfer';
                    throw new Error(typeof msg === 'string' ? msg : 'Failed to approve transfer');
                  }
                }}
                onReject={async (comments) => {
                  try {
                    await inventoryManagementService.rejectTransfer(transferDetail.id, comments);
                  } catch (err: any) {
                    const msg =
                      err?.response?.data?.error ||
                      err?.response?.data ||
                      err?.message ||
                      'Failed to reject transfer';
                    throw new Error(typeof msg === 'string' ? msg : 'Failed to reject transfer');
                  }
                }}
                onAfterAction={async () => {
                  await loadTransferDetails();
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
            {isEditable && (
              <Button onClick={handleSaveTransfer} disabled={saving || !formData.sourceWarehouseId || !formData.destinationWarehouseId}>
                {saving ? 'Saving...' : mode === 'create' ? 'Create Transfer' : 'Save Changes'}
              </Button>
            )}
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );

  function renderItemsTab() {
    return (
      <>
        {/* Add Item Section */}
        {isEditable && (
          <Card>
            <CardHeader className="pb-2">
              <div className="flex items-center justify-between">
                <CardTitle className="text-sm">Add Items to Transfer</CardTitle>
                {!showAddItem && (
                  <Button size="sm" onClick={() => setShowAddItem(true)}>
                    <Plus className="h-4 w-4 mr-1" /> Add Item
                  </Button>
                )}
              </div>
            </CardHeader>
            {showAddItem && (
              <CardContent className="space-y-4">
                {/* Warning if no source warehouse selected */}
                {!formData.sourceWarehouseId && (
                  <div className="flex items-center gap-2 p-3 rounded-md bg-yellow-50 text-yellow-700 dark:bg-yellow-900/20 dark:text-yellow-400">
                    <AlertCircle className="h-4 w-4" />
                    <span className="text-sm">Please select a source warehouse first to see available items.</span>
                  </div>
                )}

                {/* Item Search */}
                {formData.sourceWarehouseId && (
                <>
                <div className="space-y-2">
                  <Label>Search & Select Item from Source Warehouse</Label>
                  <div className="relative">
                    <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                    <Input
                      placeholder="Search by item code or name..."
                      className="pl-8"
                      value={itemSearchTerm}
                      onChange={(e) => setItemSearchTerm(e.target.value)}
                    />
                  </div>
                  <Select
                    value={itemFormData.inventoryItemId}
                    onValueChange={(v) => setItemFormData({...itemFormData, inventoryItemId: v})}
                    disabled={loadingItems}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder={loadingItems ? "Loading items..." : (warehouseInventoryItems.length === 0 ? "No items with stock available" : "Select an inventory item")} />
                    </SelectTrigger>
                    <SelectContent className="max-h-60">
                      {filteredInventoryItems.slice(0, 50).map(item => (
                        <SelectItem key={item.inventoryItemId} value={item.inventoryItemId}>
                          <div className="flex items-center gap-2">
                            <span className="font-mono text-xs">{item.itemCode}</span>
                            <span>{item.itemName}</span>
                            <span className="text-muted-foreground text-xs">({item.availableStock} avail)</span>
                            {item.isSerialTracked && <Barcode className="h-3 w-3 text-blue-500" />}
                            {item.isLotTracked && <Layers className="h-3 w-3 text-purple-500" />}
                          </div>
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                {/* Selected Item Info */}
                {selectedItem && (
                  <Card className="bg-muted/50">
                    <CardContent className="pt-4">
                      <div className="grid grid-cols-4 gap-4 text-sm">
                        <div>
                          <span className="text-muted-foreground">Available in Source:</span>
                          <span className="ml-2 font-medium">{selectedItem.availableStock} {selectedItem.unitOfMeasure}</span>
                        </div>
                        <div>
                          <span className="text-muted-foreground">Unit Cost:</span>
                          <span className="ml-2 font-medium">${selectedItem.unitCost?.toFixed(2)}</span>
                        </div>
                        <div className="flex items-center gap-1">
                          {selectedItem.isSerialTracked && (
                            <Badge variant="outline" className="text-xs"><Barcode className="h-3 w-3 mr-1" />Serial Tracked</Badge>
                          )}
                          {selectedItem.isLotTracked && (
                            <Badge variant="outline" className="text-xs"><Layers className="h-3 w-3 mr-1" />Lot Tracked</Badge>
                          )}
                        </div>
                      </div>
                    </CardContent>
                  </Card>
                )}

                {/* Quantity & Tracking Fields */}
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label>Requested Quantity *</Label>
                    <Input
                      type="number"
                      min="0.01"
                      step="0.01"
                      value={itemFormData.requestedQuantity}
                      onChange={(e) => setItemFormData({...itemFormData, requestedQuantity: parseFloat(e.target.value) || 0})}
                    />
                  </div>
                  {selectedItem?.isSerialTracked && (
                    <div className="space-y-2">
                      <Label>Serial Number</Label>
                      <Input
                        value={itemFormData.serialNumber}
                        onChange={(e) => setItemFormData({...itemFormData, serialNumber: e.target.value})}
                        placeholder="Enter serial number"
                      />
                    </div>
                  )}
                  {selectedItem?.isLotTracked && (
                    <div className="space-y-2">
                      <Label>Lot Number</Label>
                      <Input
                        value={itemFormData.lotNumber}
                        onChange={(e) => setItemFormData({...itemFormData, lotNumber: e.target.value})}
                        placeholder="Enter lot number"
                      />
                    </div>
                  )}
                </div>

                {/* Bin/Location Selection */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label>Source Bin</Label>
                    <Select
                      value={itemFormData.sourceLocationId || '__none__'}
                      onValueChange={(v) => setItemFormData({ ...itemFormData, sourceLocationId: v === '__none__' ? '' : v })}
                      disabled={!formData.sourceWarehouseId}
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="Select source bin (optional)" />
                      </SelectTrigger>
                      <SelectContent className="max-h-60">
                        <SelectItem value="__none__">Not specified</SelectItem>
                        {sourceLocations.map(loc => (
                          <SelectItem key={loc.id} value={loc.id}>
                            {loc.locationCode}{loc.name ? ` - ${loc.name}` : ''}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    {formData.sourceWarehouseId && formData.destinationWarehouseId && formData.sourceWarehouseId === formData.destinationWarehouseId && (
                      <p className="text-xs text-muted-foreground">Required for inter-bin transfers</p>
                    )}
                  </div>

                  <div className="space-y-2">
                    <Label>Destination Bin</Label>
                    <Select
                      value={itemFormData.destinationLocationId || '__none__'}
                      onValueChange={(v) => setItemFormData({ ...itemFormData, destinationLocationId: v === '__none__' ? '' : v })}
                      disabled={!formData.destinationWarehouseId}
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="Select destination bin (optional)" />
                      </SelectTrigger>
                      <SelectContent className="max-h-60">
                        <SelectItem value="__none__">Not specified</SelectItem>
                        {destinationLocations.map(loc => (
                          <SelectItem key={loc.id} value={loc.id}>
                            {loc.locationCode}{loc.name ? ` - ${loc.name}` : ''}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    {formData.sourceWarehouseId && formData.destinationWarehouseId && formData.sourceWarehouseId === formData.destinationWarehouseId && (
                      <p className="text-xs text-muted-foreground">Required for inter-bin transfers</p>
                    )}
                  </div>
                </div>

                <div className="space-y-2">
                  <Label>Item Notes</Label>
                  <Input
                    value={itemFormData.notes}
                    onChange={(e) => setItemFormData({...itemFormData, notes: e.target.value})}
                    placeholder="Optional notes for this item"
                  />
                </div>

                <div className="flex justify-end gap-2">
                  <Button variant="outline" size="sm" onClick={resetItemForm}>Cancel</Button>
                  {editingItemId ? (
                    <Button size="sm" onClick={() => handleUpdateItem(editingItemId)} disabled={saving}>
                      {saving ? 'Saving...' : 'Update Item'}
                    </Button>
                  ) : (
                    <Button size="sm" onClick={handleAddItem} disabled={saving || !itemFormData.inventoryItemId || itemFormData.requestedQuantity <= 0}>
                      {saving ? 'Adding...' : 'Add Item'}
                    </Button>
                  )}
                </div>
                </>
                )}
              </CardContent>
            )}
          </Card>
        )}

        {/* Items Table */}
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">Transfer Items</CardTitle>
            <CardDescription>
              {transferDetail?.items?.length || 0} item(s) in this transfer
            </CardDescription>
          </CardHeader>
          <CardContent>
            {(!transferDetail?.items || transferDetail.items.length === 0) ? (
              <div className="py-8 text-center text-muted-foreground">
                <Package className="h-12 w-12 mx-auto mb-2 opacity-50" />
                <p>No items added yet</p>
                {isEditable && <p className="text-sm">Click "Add Item" to add inventory items to this transfer</p>}
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Item</TableHead>
                    <TableHead>From Bin</TableHead>
                    <TableHead>To Bin</TableHead>
                    <TableHead className="text-right">Qty Requested</TableHead>
                    <TableHead className="text-right">Qty Shipped</TableHead>
                    <TableHead className="text-right">Qty Received</TableHead>
                    <TableHead>Tracking</TableHead>
                    <TableHead className="text-right">Unit Cost</TableHead>
                    <TableHead className="text-right">Total</TableHead>
                    {isEditable && <TableHead></TableHead>}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {transferDetail.items.map((item) => (
                    <TableRow key={item.id}>
                      <TableCell>
                        <div>
                          <div className="font-medium">{item.itemName || item.itemCode}</div>
                          <div className="text-xs text-muted-foreground">{item.itemCode}</div>
                        </div>
                      </TableCell>
                      <TableCell className="text-sm">{item.sourceLocationName || <span className="text-muted-foreground">-</span>}</TableCell>
                      <TableCell className="text-sm">{item.destinationLocationName || <span className="text-muted-foreground">-</span>}</TableCell>
                      <TableCell className="text-right">{item.requestedQuantity} {item.unitOfMeasure}</TableCell>
                      <TableCell className="text-right">{item.shippedQuantity}</TableCell>
                      <TableCell className="text-right">{item.receivedQuantity}</TableCell>
                      <TableCell>
                        <div className="text-xs">
                          {item.serialNumber && <div><Barcode className="h-3 w-3 inline mr-1" />SN: {item.serialNumber}</div>}
                          {item.lotNumber && <div><Layers className="h-3 w-3 inline mr-1" />Lot: {item.lotNumber}</div>}
                          {!item.serialNumber && !item.lotNumber && <span className="text-muted-foreground">-</span>}
                        </div>
                      </TableCell>
                      <TableCell className="text-right">${item.unitCost?.toFixed(2) || '0.00'}</TableCell>
                      <TableCell className="text-right">${item.totalCost?.toFixed(2) || '0.00'}</TableCell>
                      {isEditable && (
                        <TableCell>
                          <div className="flex gap-1">
                            <Button variant="ghost" size="sm" onClick={() => startEditItem(item)}>Edit</Button>
                            <Button variant="ghost" size="sm" className="text-red-600" onClick={() => handleRemoveItem(item.id)}>
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          </div>
                        </TableCell>
                      )}
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      </>
    );
  }
}
