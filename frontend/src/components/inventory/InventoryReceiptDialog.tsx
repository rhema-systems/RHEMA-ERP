'use client';

import React, { useState, useEffect } from 'react';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Plus, Trash2, Search, Pencil, Check, X, ArrowUpCircle, Package } from 'lucide-react';
import {
  stockAdjustmentService,
  StockAdjustmentDetailDto,
  StockAdjustmentReasonCodes,
  StockAdjustmentStatusColors,
  CreateStockAdjustmentDto,
  CreateStockAdjustmentItemDto,
  UpdateStockAdjustmentDto
} from '@/services/stockAdjustmentService';
import { inventoryManagementService, WarehouseDto, WarehouseInventoryItemDto } from '@/services/inventoryManagementService';
import { useToast } from '@/hooks/use-toast';
import { format } from 'date-fns';

interface InventoryReceiptDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  mode: 'create' | 'edit' | 'view';
  receiptId?: string;
  onSuccess: () => void;
}

interface FormData {
  warehouseId: string;
  reasonCode: string;
  receiptDate: string;
  description: string;
  reference: string;
}

interface ItemFormData {
  inventoryItemId: string;
  quantity: number;
  unitCost: number;
  reason: string;
}

export function InventoryReceiptDialog({ open, onOpenChange, mode, receiptId, onSuccess }: InventoryReceiptDialogProps) {
  const { toast } = useToast();
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [activeTab, setActiveTab] = useState('details');
  const [receiptDetail, setReceiptDetail] = useState<StockAdjustmentDetailDto | null>(null);
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [warehouseInventoryItems, setWarehouseInventoryItems] = useState<WarehouseInventoryItemDto[]>([]);
  const [loadingItems, setLoadingItems] = useState(false);
  const [itemSearchTerm, setItemSearchTerm] = useState('');
  const [showAddItem, setShowAddItem] = useState(false);
  const [editingItemId, setEditingItemId] = useState<string | null>(null);
  const [editingItemData, setEditingItemData] = useState<{ quantity: number; reason: string }>({ quantity: 0, reason: '' });
  const [pendingItems, setPendingItems] = useState<CreateStockAdjustmentItemDto[]>([]);
  const [deletedItemIds, setDeletedItemIds] = useState<string[]>([]);

  const [formData, setFormData] = useState<FormData>({
    warehouseId: '',
    reasonCode: 'PURCHASE_RECEIPT',
    receiptDate: format(new Date(), 'yyyy-MM-dd'),
    description: '',
    reference: ''
  });

  const [itemFormData, setItemFormData] = useState<ItemFormData>({
    inventoryItemId: '',
    quantity: 1,
    unitCost: 0,
    reason: ''
  });

  const isViewMode = mode === 'view';
  const isEditMode = mode === 'edit';
  const isCreateMode = mode === 'create';
  const canEdit = !isViewMode && (isCreateMode || receiptDetail?.status === 'Draft');

  // Reset state when dialog opens/closes
  useEffect(() => {
    if (open) {
      setActiveTab('details');
      setPendingItems([]);
      setDeletedItemIds([]);
      if (mode === 'create') {
        setFormData({
          warehouseId: '',
          reasonCode: 'PURCHASE_RECEIPT',
          receiptDate: format(new Date(), 'yyyy-MM-dd'),
          description: '',
          reference: ''
        });
        setReceiptDetail(null);
      }
      loadWarehouses();
    }
  }, [open, mode]);

  // Load receipt details when editing/viewing
  useEffect(() => {
    if (open && receiptId && (mode === 'edit' || mode === 'view')) {
      loadReceiptDetail();
    }
  }, [open, receiptId, mode]);

  // Load warehouse items when warehouse changes
  useEffect(() => {
    if (formData.warehouseId && canEdit) {
      loadWarehouseItems(formData.warehouseId);
    }
  }, [formData.warehouseId, canEdit]);

  const loadWarehouses = async () => {
    try {
      const data = await inventoryManagementService.getWarehouses();
      setWarehouses(data);
    } catch (err) {
      console.error('Error loading warehouses:', err);
    }
  };

  const loadReceiptDetail = async () => {
    if (!receiptId) return;
    try {
      setLoading(true);
      const detail = await stockAdjustmentService.getById(receiptId);
      setReceiptDetail(detail);
      setFormData({
        warehouseId: detail.warehouseId || '',
        reasonCode: detail.reasonCode || 'PURCHASE_RECEIPT',
        receiptDate: detail.adjustmentDate ? format(new Date(detail.adjustmentDate), 'yyyy-MM-dd') : format(new Date(), 'yyyy-MM-dd'),
        description: detail.description || '',
        reference: detail.reference || ''
      });
    } catch (err) {
      console.error('Error loading receipt:', err);
      toast({ title: 'Error', description: 'Failed to load receipt details', variant: 'destructive' });
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

  const handleSave = async () => {
    if (!formData.warehouseId || !formData.reasonCode) {
      toast({ title: 'Validation Error', description: 'Please select warehouse and receipt reason', variant: 'destructive' });
      return;
    }
    try {
      setSaving(true);
      if (isCreateMode) {
        const createDto: CreateStockAdjustmentDto = {
          warehouseId: formData.warehouseId,
          reasonCode: formData.reasonCode,
          adjustmentDate: formData.receiptDate,
          description: formData.description || undefined,
          reference: formData.reference || undefined,
          items: pendingItems.map(item => ({
            ...item,
            // Ensure quantity is always positive for receipts
            adjustmentQuantity: Math.abs(item.adjustmentQuantity)
          }))
        };
        await stockAdjustmentService.create(createDto);
        toast({ title: 'Success', description: 'Inventory receipt created successfully' });
      } else if (isEditMode && receiptId) {
        const updateDto: UpdateStockAdjustmentDto = {
          reasonCode: formData.reasonCode,
          adjustmentDate: formData.receiptDate,
          description: formData.description || undefined,
          reference: formData.reference || undefined
        };
        await stockAdjustmentService.update(receiptId, updateDto);
        toast({ title: 'Success', description: 'Inventory receipt updated successfully' });
      }
      onSuccess();
      onOpenChange(false);
    } catch (err: unknown) {
      console.error('Error saving receipt:', err);
      const errorMessage = err instanceof Error ? err.message : 'Failed to save receipt';
      toast({ title: 'Error', description: errorMessage, variant: 'destructive' });
    } finally {
      setSaving(false);
    }
  };

  const handleAddItem = async () => {
    if (!itemFormData.inventoryItemId || itemFormData.quantity <= 0) {
      toast({ title: 'Validation Error', description: 'Please select an item and enter a positive quantity', variant: 'destructive' });
      return;
    }
    try {
      if (isCreateMode) {
        // Add to pending items for create mode
        const selectedItem = warehouseInventoryItems.find(i => i.inventoryItemId === itemFormData.inventoryItemId);
        if (selectedItem) {
          setPendingItems([...pendingItems, {
            inventoryItemId: itemFormData.inventoryItemId,
            // Always positive for receipts
            adjustmentQuantity: Math.abs(itemFormData.quantity),
            unitCost: itemFormData.unitCost || selectedItem.unitCost || 0,
            reason: itemFormData.reason || undefined
          }]);
        }
      } else if (receiptId) {
        // For edit mode, we would need an API to add items - for now just reload
        await loadReceiptDetail();
        toast({ title: 'Info', description: 'Item management in edit mode requires saving first' });
      }
      setItemFormData({ inventoryItemId: '', quantity: 1, unitCost: 0, reason: '' });
      setShowAddItem(false);
    } catch (err) {
      console.error('Error adding item:', err);
      toast({ title: 'Error', description: 'Failed to add item', variant: 'destructive' });
    }
  };

  const handleRemoveItem = async (itemId: string) => {
    console.log('handleRemoveItem called with itemId:', itemId, 'isCreateMode:', isCreateMode, 'isEditMode:', isEditMode);
    if (isCreateMode) {
      const indexToRemove = parseInt(itemId, 10);
      console.log('indexToRemove:', indexToRemove, 'pendingItems length:', pendingItems.length);
      if (!isNaN(indexToRemove) && indexToRemove >= 0 && indexToRemove < pendingItems.length) {
        const newItems = pendingItems.filter((_, idx) => idx !== indexToRemove);
        console.log('New items after filter:', newItems.length);
        setPendingItems(newItems);
      }
    } else if (isEditMode && receiptId) {
      // In edit mode, call API to delete the item immediately
      try {
        console.log('Calling API to delete item:', itemId, 'from receipt:', receiptId);
        await stockAdjustmentService.deleteItem(receiptId, itemId);
        // Update local state to remove the item from display
        setDeletedItemIds(prev => [...prev, itemId]);
        toast({
          title: 'Item Deleted',
          description: 'Item has been removed from the receipt.'
        });
      } catch (err) {
        console.error('Error deleting item:', err);
        toast({
          title: 'Error',
          description: 'Failed to delete item. Please try again.',
          variant: 'destructive'
        });
      }
    }
  };

  const handleEditItem = (item: { id: string; quantity: number; reason?: string }) => {
    setEditingItemId(item.id);
    setEditingItemData({
      quantity: item.quantity,
      reason: item.reason || ''
    });
  };

  const handleCancelEdit = () => {
    setEditingItemId(null);
    setEditingItemData({ quantity: 0, reason: '' });
  };

  const handleSaveItemEdit = async () => {
    if (!editingItemId || editingItemData.quantity <= 0) {
      toast({ title: 'Validation Error', description: 'Please enter a positive quantity', variant: 'destructive' });
      return;
    }

    if (isCreateMode) {
      // Update pending items for create mode
      const idx = parseInt(editingItemId);
      const updatedItems = [...pendingItems];
      if (updatedItems[idx]) {
        updatedItems[idx] = {
          ...updatedItems[idx],
          // Always positive for receipts
          adjustmentQuantity: Math.abs(editingItemData.quantity),
          reason: editingItemData.reason || undefined
        };
        setPendingItems(updatedItems);
      }
      setEditingItemId(null);
      setEditingItemData({ quantity: 0, reason: '' });
    }
  };

  const getStatusBadge = (status: string) => {
    const colorClass = StockAdjustmentStatusColors[status] || 'bg-gray-100 text-gray-800';
    return <Badge className={colorClass}>{status}</Badge>;
  };

  const getReasonLabel = (reasonCode: string) => {
    return StockAdjustmentReasonCodes[reasonCode as keyof typeof StockAdjustmentReasonCodes] || reasonCode;
  };

  const filteredWarehouseItems = warehouseInventoryItems.filter(item =>
    item.itemCode.toLowerCase().includes(itemSearchTerm.toLowerCase()) ||
    item.itemName.toLowerCase().includes(itemSearchTerm.toLowerCase())
  );

  const displayItems = isCreateMode ? pendingItems.map((item, idx) => {
    const invItem = warehouseInventoryItems.find(i => i.inventoryItemId === item.inventoryItemId);
    const quantity = Math.abs(item.adjustmentQuantity);
    return {
      id: idx.toString(),
      inventoryItemId: item.inventoryItemId,
      itemCode: invItem?.itemCode || '',
      itemName: invItem?.itemName || '',
      quantity: quantity,
      unitCost: item.unitCost || invItem?.unitCost || 0,
      totalValue: (item.unitCost || invItem?.unitCost || 0) * quantity,
      currentStock: invItem?.currentStock || 0,
      newStock: (invItem?.currentStock || 0) + quantity,
      unitOfMeasure: invItem?.unitOfMeasure || '',
      reason: item.reason
    };
  }) : (receiptDetail?.items || []).filter(item => !deletedItemIds.includes(item.id)).map(item => {
    const quantity = Math.abs(item.adjustmentQuantity);
    return {
      id: item.id,
      inventoryItemId: item.inventoryItemId,
      itemCode: item.itemCode,
      itemName: item.itemName,
      quantity: quantity,
      unitCost: item.unitCost,
      totalValue: item.totalValue || quantity * item.unitCost,
      currentStock: item.previousQuantity || item.systemQuantity || 0,
      newStock: item.newQuantity || ((item.previousQuantity || item.systemQuantity || 0) + quantity),
      unitOfMeasure: item.unitOfMeasure,
      reason: item.reason
    };
  });

  // Calculate totals (always positive for receipts)
  const totalReceiptValue = displayItems.reduce((sum, i) => sum + i.totalValue, 0);
  const totalQuantity = displayItems.reduce((sum, i) => sum + i.quantity, 0);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <div className="flex flex-col gap-1">
            <DialogTitle>
              {isCreateMode ? 'New Inventory Receipt' : isEditMode ? 'Edit Inventory Receipt' : 'View Inventory Receipt'}
              {receiptDetail && <span className="ml-2 text-muted-foreground">#{receiptDetail.adjustmentNumber}</span>}
            </DialogTitle>
            <div className="flex items-center gap-2">
              <span className="text-sm text-muted-foreground">
                {isCreateMode ? 'Receive inventory into warehouse' : isEditMode ? 'Edit receipt details' : 'View receipt details'}
              </span>
              {receiptDetail && getStatusBadge(receiptDetail.status)}
            </div>
          </div>
        </DialogHeader>

        {loading ? (
          <div className="flex justify-center py-8"><div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div></div>
        ) : (
          <Tabs value={activeTab} onValueChange={setActiveTab}>
            <TabsList className="grid w-full grid-cols-2">
              <TabsTrigger value="details">Details</TabsTrigger>
              <TabsTrigger value="items">Items ({displayItems.length})</TabsTrigger>
            </TabsList>

            <TabsContent value="details" className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Warehouse *</Label>
                  <Select value={formData.warehouseId} onValueChange={(v) => setFormData({ ...formData, warehouseId: v })} disabled={!canEdit || isEditMode}>
                    <SelectTrigger><SelectValue placeholder="Select warehouse" /></SelectTrigger>
                    <SelectContent>
                      {warehouses.map(w => <SelectItem key={w.id} value={w.id}>{w.name}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>Receipt Reason *</Label>
                  <Select value={formData.reasonCode} onValueChange={(v) => setFormData({ ...formData, reasonCode: v })} disabled={!canEdit}>
                    <SelectTrigger><SelectValue placeholder="Select reason" /></SelectTrigger>
                    <SelectContent>
                      {Object.entries(StockAdjustmentReasonCodes).map(([code, label]) => (
                        <SelectItem key={code} value={code}>{label}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>Receipt Date</Label>
                  <Input type="date" value={formData.receiptDate} onChange={(e) => setFormData({ ...formData, receiptDate: e.target.value })} disabled={!canEdit} />
                </div>
                <div className="space-y-2">
                  <Label>Reference</Label>
                  <Input value={formData.reference} onChange={(e) => setFormData({ ...formData, reference: e.target.value })} disabled={!canEdit} placeholder="e.g., PO-2024-001" />
                </div>
              </div>
              <div className="space-y-2">
                <Label>Description</Label>
                <Textarea value={formData.description} onChange={(e) => setFormData({ ...formData, description: e.target.value })} disabled={!canEdit} rows={3} placeholder="Describe the reason for this receipt..." />
              </div>

              {/* Summary Cards */}
              {displayItems.length > 0 && (
                <div className="grid grid-cols-3 gap-4 mt-4">
                  <Card>
                    <CardContent className="pt-4">
                      <div className="flex items-center justify-between">
                        <div>
                          <p className="text-sm text-muted-foreground">Total Items</p>
                          <p className="text-2xl font-bold">{displayItems.length}</p>
                        </div>
                        <Package className="h-8 w-8 text-blue-500" />
                      </div>
                    </CardContent>
                  </Card>
                  <Card>
                    <CardContent className="pt-4">
                      <div className="flex items-center justify-between">
                        <div>
                          <p className="text-sm text-muted-foreground">Total Quantity</p>
                          <p className="text-2xl font-bold text-green-600">+{totalQuantity}</p>
                        </div>
                        <ArrowUpCircle className="h-8 w-8 text-green-500" />
                      </div>
                    </CardContent>
                  </Card>
                  <Card>
                    <CardContent className="pt-4">
                      <div className="flex items-center justify-between">
                        <div>
                          <p className="text-sm text-muted-foreground">Total Value</p>
                          <p className="text-2xl font-bold text-green-600">+${totalReceiptValue.toFixed(2)}</p>
                        </div>
                        <ArrowUpCircle className="h-8 w-8 text-green-500" />
                      </div>
                    </CardContent>
                  </Card>
                </div>
              )}
            </TabsContent>

            <TabsContent value="items" className="space-y-4">
              {canEdit && (
                <div className="flex justify-between items-center">
                  <Button variant="outline" size="sm" onClick={() => setShowAddItem(!showAddItem)}>
                    <Plus className="h-4 w-4 mr-1" />Add Item
                  </Button>
                  <div className="text-sm text-muted-foreground">
                    All quantities will be added to inventory (positive receipt)
                  </div>
                </div>
              )}

              {showAddItem && canEdit && (
                <Card>
                  <CardHeader><CardTitle className="text-sm">Add Receipt Item</CardTitle></CardHeader>
                  <CardContent className="space-y-4">
                    <div className="space-y-2">
                      <Label>Search and Select Item</Label>
                      <div className="relative">
                        <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                        <Input
                          placeholder="Type to search items by code or name..."
                          className="pl-8"
                          value={itemSearchTerm}
                          onChange={(e) => setItemSearchTerm(e.target.value)}
                        />
                      </div>
                      {itemSearchTerm && filteredWarehouseItems.length > 0 && (
                        <div className="border rounded-md max-h-48 overflow-y-auto">
                          {filteredWarehouseItems.map(item => (
                            <div
                              key={item.inventoryItemId}
                              className={`p-2 cursor-pointer hover:bg-accent ${itemFormData.inventoryItemId === item.inventoryItemId ? 'bg-accent' : ''}`}
                              onClick={() => {
                                setItemFormData({ ...itemFormData, inventoryItemId: item.inventoryItemId, unitCost: item.unitCost || 0 });
                                setItemSearchTerm('');
                              }}
                            >
                              <div className="font-medium">{item.itemCode} - {item.itemName}</div>
                              <div className="text-sm text-muted-foreground">Current Stock: {item.currentStock} {item.unitOfMeasure}</div>
                            </div>
                          ))}
                        </div>
                      )}
                      {itemSearchTerm && filteredWarehouseItems.length === 0 && (
                        <div className="text-sm text-muted-foreground p-2">No items found matching "{itemSearchTerm}"</div>
                      )}
                      {itemFormData.inventoryItemId && (
                        <div className="p-2 bg-accent rounded-md">
                          <div className="text-sm font-medium">Selected Item:</div>
                          {(() => {
                            const selectedItem = warehouseInventoryItems.find(i => i.inventoryItemId === itemFormData.inventoryItemId);
                            return selectedItem ? (
                              <div className="flex justify-between items-center">
                                <span>{selectedItem.itemCode} - {selectedItem.itemName}</span>
                                <Button variant="ghost" size="sm" onClick={() => setItemFormData({ ...itemFormData, inventoryItemId: '', unitCost: 0 })}>
                                  <X className="h-4 w-4" />
                                </Button>
                              </div>
                            ) : null;
                          })()}
                        </div>
                      )}
                    </div>
                    <div className="grid grid-cols-3 gap-4">
                      <div className="space-y-2">
                        <Label>Quantity to Receive</Label>
                        <Input 
                          type="number" 
                          min="1"
                          value={itemFormData.quantity} 
                          onChange={(e) => setItemFormData({ ...itemFormData, quantity: Math.max(1, parseInt(e.target.value) || 1) })} 
                          placeholder="Enter quantity"
                        />
                        <p className="text-xs text-muted-foreground">Quantity will be added to inventory</p>
                      </div>
                      <div className="space-y-2">
                        <Label>Unit Cost</Label>
                        <Input 
                          type="number" 
                          step="0.01"
                          min="0"
                          value={itemFormData.unitCost} 
                          onChange={(e) => setItemFormData({ ...itemFormData, unitCost: parseFloat(e.target.value) || 0 })} 
                        />
                      </div>
                      <div className="space-y-2">
                        <Label>Item Note</Label>
                        <Input value={itemFormData.reason} onChange={(e) => setItemFormData({ ...itemFormData, reason: e.target.value })} placeholder="Optional note" />
                      </div>
                    </div>
                    <Button size="sm" onClick={handleAddItem}>Add Item</Button>
                  </CardContent>
                </Card>
              )}

              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Item</TableHead>
                    <TableHead className="text-right">{isCreateMode ? 'Current Stock' : 'Stock Before'}</TableHead>
                    <TableHead className="text-right">Qty to Receive</TableHead>
                    <TableHead className="text-right">{isCreateMode ? 'New Stock' : 'Stock After'}</TableHead>
                    <TableHead>UoM</TableHead>
                    <TableHead className="text-right">Unit Cost</TableHead>
                    <TableHead className="text-right">Total Value</TableHead>
                    {canEdit && <TableHead></TableHead>}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {displayItems.length === 0 ? (
                    <TableRow><TableCell colSpan={canEdit ? 8 : 7} className="text-center text-muted-foreground">No items added</TableCell></TableRow>
                  ) : (
                    displayItems.map((item) => (
                      <TableRow key={item.id}>
                        <TableCell>
                          <div className="font-medium">{item.itemCode}</div>
                          <div className="text-sm text-muted-foreground">{item.itemName}</div>
                          {item.reason && <div className="text-xs text-muted-foreground italic">{item.reason}</div>}
                        </TableCell>
                        <TableCell className="text-right">{item.currentStock}</TableCell>
                        <TableCell className="text-right">
                          {editingItemId === item.id ? (
                            <Input
                              type="number"
                              min="1"
                              className="w-24"
                              value={editingItemData.quantity}
                              onChange={(e) => setEditingItemData({ ...editingItemData, quantity: Math.max(1, parseInt(e.target.value) || 1) })}
                            />
                          ) : (
                            <span className="text-green-600 font-medium">+{item.quantity}</span>
                          )}
                        </TableCell>
                        <TableCell className="text-right font-medium">{item.newStock}</TableCell>
                        <TableCell>{item.unitOfMeasure}</TableCell>
                        <TableCell className="text-right">{item.unitCost.toFixed(2)}</TableCell>
                        <TableCell className="text-right font-medium text-green-600">
                          +${item.totalValue.toFixed(2)}
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
                                  <Button variant="ghost" size="sm" onClick={() => handleEditItem({ id: item.id, quantity: item.quantity, reason: item.reason })} title="Edit">
                                    <Pencil className="h-4 w-4 text-blue-500" />
                                  </Button>
                                  <Button
                                    variant="ghost"
                                    size="sm"
                                    onClick={(e) => {
                                      e.preventDefault();
                                      e.stopPropagation();
                                      console.log('Delete button clicked for item:', item.id);
                                      handleRemoveItem(item.id);
                                    }}
                                    title="Delete"
                                    type="button"
                                  >
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
          </Tabs>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Close</Button>
          {canEdit && <Button onClick={handleSave} disabled={saving}>{saving ? 'Saving...' : 'Save'}</Button>}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
