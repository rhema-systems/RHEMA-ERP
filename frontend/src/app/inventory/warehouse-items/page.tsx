'use client';

import React, { useState, useEffect, useCallback } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Checkbox } from '@/components/ui/checkbox';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Plus, Search, Edit, Trash2, Building2, AlertTriangle } from 'lucide-react';
import {
  inventoryManagementService,
  WarehouseDto, WarehouseItemDto, BulkAssignItemsDto, UpdateWarehouseItemDto,
  InventoryItemDto
} from '@/services/inventoryManagementService';
import { format } from 'date-fns';
import { useToast } from '@/hooks/use-toast';

export default function WarehouseItemsPage() {
  const { toast } = useToast();
  const [warehouseItems, setWarehouseItems] = useState<WarehouseItemDto[]>([]);
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [inventoryItems, setInventoryItems] = useState<InventoryItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [searchTerm, setSearchTerm] = useState('');
  const [warehouseFilter, setWarehouseFilter] = useState('all');
  const [itemTypeFilter, setItemTypeFilter] = useState('all');

  const [isAssignDialogOpen, setIsAssignDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [selectedItem, setSelectedItem] = useState<WarehouseItemDto | null>(null);
  const [previewWarehouseId, setPreviewWarehouseId] = useState<string>('');

  const [selectedInventoryItems, setSelectedInventoryItems] = useState<string[]>([]);
  const [selectedWarehouses, setSelectedWarehouses] = useState<string[]>([]);
  const [dialogWarehouseSearch, setDialogWarehouseSearch] = useState('');
  const [dialogItemSearch, setDialogItemSearch] = useState('');
  const [assignForm, setAssignForm] = useState<BulkAssignItemsDto>({
    inventoryItemIds: [], warehouseIds: [], initialQuantity: 0, reorderLevel: 0, maxStock: 0
  });

  const [editForm, setEditForm] = useState<UpdateWarehouseItemDto>({
    currentStock: 0, reorderLevel: 0, maxStock: 0, notes: ''
  });

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const [itemsData, warehousesData, invItemsData] = await Promise.all([
        inventoryManagementService.getWarehouseItems(),
        inventoryManagementService.getWarehouses(),
        inventoryManagementService.getInventoryItems()
      ]);
      setWarehouseItems(itemsData);
      setWarehouses(warehousesData.filter(w => w.isActive));
      setInventoryItems(invItemsData);
    } catch (err: unknown) {
      console.error('Error fetching data:', err);
      setError('Failed to load warehouse items');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);

  const filteredItems = warehouseItems.filter(item => {
    const matchesSearch = !searchTerm || 
      item.itemCode.toLowerCase().includes(searchTerm.toLowerCase()) ||
      item.itemName.toLowerCase().includes(searchTerm.toLowerCase()) ||
      item.warehouseName.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesWarehouse = warehouseFilter === 'all' || item.warehouseId === warehouseFilter;
    const matchesType = itemTypeFilter === 'all' || item.itemType === itemTypeFilter;
    return matchesSearch && matchesWarehouse && matchesType;
  });

  const handleAssign = async () => {
    try {
      const result = await inventoryManagementService.assignItemsToWarehouses({
        ...assignForm,
        inventoryItemIds: selectedInventoryItems,
        warehouseIds: selectedWarehouses
      });

      const hasErrors = result.errors && result.errors.length > 0;
      toast({
        title: hasErrors ? 'Assignment Completed with Warnings' : 'Items Assigned Successfully',
        description: `Created: ${result.created}, Skipped: ${result.skipped}${hasErrors ? '. Errors: ' + result.errors.join(', ') : ''}`,
        variant: hasErrors ? 'destructive' : 'success'
      });

      setIsAssignDialogOpen(false);
      resetAssignForm();
      fetchData();
    } catch (err) {
      console.error('Error assigning items:', err);
      toast({
        title: 'Assignment Failed',
        description: 'Failed to assign items to warehouses',
        variant: 'destructive'
      });
    }
  };

  const handleEdit = (item: WarehouseItemDto) => {
    setSelectedItem(item);
    setEditForm({
      currentStock: item.currentStock,
      reorderLevel: item.reorderLevel,
      maxStock: item.maxStock,
      notes: item.notes || ''
    });
    setIsEditDialogOpen(true);
  };

  const handleUpdate = async () => {
    if (!selectedItem) return;
    try {
      await inventoryManagementService.updateWarehouseItem(selectedItem.id, editForm);
      toast({
        title: 'Success',
        description: 'Warehouse item updated successfully',
        variant: 'success'
      });
      setIsEditDialogOpen(false);
      fetchData();
    } catch (err) {
      console.error('Error updating item:', err);
      toast({
        title: 'Update Failed',
        description: 'Failed to update warehouse item',
        variant: 'destructive'
      });
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to remove this item from the warehouse?')) return;
    try {
      await inventoryManagementService.deleteWarehouseItem(id);
      toast({
        title: 'Success',
        description: 'Item removed from warehouse successfully',
        variant: 'success'
      });
      fetchData();
    } catch (err: unknown) {
      console.error('Error deleting:', err);
      const errorMessage = err instanceof Error ? err.message : 'Failed to remove item';
      toast({
        title: 'Delete Failed',
        description: errorMessage,
        variant: 'destructive'
      });
    }
  };

  const resetAssignForm = () => {
    setSelectedInventoryItems([]);
    setSelectedWarehouses([]);
    setPreviewWarehouseId('');
    setDialogWarehouseSearch('');
    setDialogItemSearch('');
    setAssignForm({ inventoryItemIds: [], warehouseIds: [], initialQuantity: 0, reorderLevel: 0, maxStock: 0 });
  };

  const toggleInventoryItem = (id: string) => {
    setSelectedInventoryItems(prev =>
      prev.includes(id) ? prev.filter(i => i !== id) : [...prev, id]
    );
  };

  const toggleWarehouse = (id: string) => {
    setSelectedWarehouses(prev =>
      prev.includes(id) ? prev.filter(i => i !== id) : [...prev, id]
    );
  };

  const itemTypes = ['Consumable', 'Tool', 'StockItem', 'Service', 'NonStock', 'FixedAsset'];

  // Get items in the preview warehouse
  const getPreviewWarehouseItems = () => {
    if (!previewWarehouseId) return [];
    return warehouseItems.filter(wi => wi.warehouseId === previewWarehouseId);
  };

  // Get items not yet assigned to any selected warehouse for the assign dialog
  const getUnassignedItems = () => {
    if (selectedWarehouses.length === 0) return inventoryItems;
    const assignedItemIds = warehouseItems
      .filter(wi => selectedWarehouses.includes(wi.warehouseId))
      .map(wi => wi.inventoryItemId);
    return inventoryItems.filter(item => !assignedItemIds.includes(item.id));
  };

  // Filter warehouses by search term for dialog
  const getFilteredWarehouses = () => {
    if (!dialogWarehouseSearch.trim()) return warehouses;
    const search = dialogWarehouseSearch.toLowerCase();
    return warehouses.filter(w =>
      w.name.toLowerCase().includes(search) ||
      w.code.toLowerCase().includes(search)
    );
  };

  // Filter items by search term for dialog
  const getFilteredItems = () => {
    const unassigned = getUnassignedItems();
    if (!dialogItemSearch.trim()) return unassigned;
    const search = dialogItemSearch.toLowerCase();
    return unassigned.filter(item =>
      item.name.toLowerCase().includes(search) ||
      item.itemCode.toLowerCase().includes(search)
    );
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Breadcrumb */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem><BreadcrumbLink href="/inventory">Inventory</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbPage>Warehouse Items</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Header */}
      <div className="flex justify-between items-center">
        <div>
          <h1 className="text-3xl font-bold">Warehouse Items</h1>
          <p className="text-muted-foreground">Assign inventory items to warehouses and manage stock quantities</p>
        </div>
        <Button onClick={() => setIsAssignDialogOpen(true)}>
          <Plus className="mr-2 h-4 w-4" /> Assign Items to Warehouses
        </Button>
      </div>

      {error && (
        <div className="bg-destructive/15 text-destructive p-4 rounded-lg flex items-center gap-2">
          <AlertTriangle className="h-5 w-5" />{error}
        </div>
      )}

      {/* Filters */}
      <Card>
        <CardContent className="pt-6">
          <div className="flex gap-4 flex-wrap">
            <div className="flex-1 min-w-[200px]">
              <div className="relative">
                <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                <Input placeholder="Search items..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} className="pl-10" />
              </div>
            </div>
            <Select value={warehouseFilter} onValueChange={setWarehouseFilter}>
              <SelectTrigger className="w-[200px]"><SelectValue placeholder="All Warehouses" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Warehouses</SelectItem>
                {warehouses.map(w => <SelectItem key={w.id} value={w.id}>{w.name}</SelectItem>)}
              </SelectContent>
            </Select>
            <Select value={itemTypeFilter} onValueChange={setItemTypeFilter}>
              <SelectTrigger className="w-[150px]"><SelectValue placeholder="All Types" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Types</SelectItem>
                {itemTypes.map(t => <SelectItem key={t} value={t}>{t}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Items Table */}
      <Card>
        <CardHeader>
          <CardTitle>Warehouse Inventory ({filteredItems.length})</CardTitle>
          <CardDescription>Items assigned to warehouses with their stock levels</CardDescription>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Item Code</TableHead>
                <TableHead>Item Name</TableHead>
                <TableHead>Warehouse</TableHead>
                <TableHead>Type</TableHead>
                <TableHead className="text-right">Current</TableHead>
                <TableHead className="text-right">Available</TableHead>
                <TableHead className="text-right">Allocated</TableHead>
                <TableHead className="text-right">Reorder</TableHead>
                <TableHead>Last Movement</TableHead>
                <TableHead className="text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filteredItems.length === 0 ? (
                <TableRow><TableCell colSpan={10} className="text-center py-8 text-muted-foreground">No warehouse items found</TableCell></TableRow>
              ) : (
                filteredItems.map(item => (
                  <TableRow key={item.id}>
                    <TableCell className="font-medium">{item.itemCode}</TableCell>
                    <TableCell>{item.itemName}</TableCell>
                    <TableCell><Badge variant="outline"><Building2 className="h-3 w-3 mr-1" />{item.warehouseName}</Badge></TableCell>
                    <TableCell><Badge variant="secondary">{item.itemType}</Badge></TableCell>
                    <TableCell className="text-right">{item.currentStock.toLocaleString()}</TableCell>
                    <TableCell className="text-right">{item.availableStock.toLocaleString()}</TableCell>
                    <TableCell className="text-right">{item.allocatedStock.toLocaleString()}</TableCell>
                    <TableCell className="text-right">
                      {item.currentStock <= item.reorderLevel && item.reorderLevel > 0 ? (
                        <Badge variant="destructive">{item.reorderLevel}</Badge>
                      ) : item.reorderLevel}
                    </TableCell>
                    <TableCell>{item.lastMovementDate ? format(new Date(item.lastMovementDate), 'MMM dd, yyyy') : '-'}</TableCell>
                    <TableCell className="text-right">
                      <Button variant="ghost" size="icon" onClick={() => handleEdit(item)}><Edit className="h-4 w-4" /></Button>
                      <Button variant="ghost" size="icon" onClick={() => handleDelete(item.id)} disabled={item.allocatedStock > 0}><Trash2 className="h-4 w-4" /></Button>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {/* Assign Items Dialog */}
      <Dialog open={isAssignDialogOpen} onOpenChange={setIsAssignDialogOpen}>
        <DialogContent className="w-[1200px] max-w-[95vw] h-[750px] max-h-[90vh] flex flex-col">
          <DialogHeader>
            <DialogTitle>Assign Items to Warehouses</DialogTitle>
            <DialogDescription>Preview existing assignments on the left, then select items and warehouses to assign on the right</DialogDescription>
          </DialogHeader>
          <div className="flex-1 overflow-hidden">
            <div className="grid grid-cols-[400px_1fr] gap-6 h-full">
              {/* Left Panel - Preview Existing Warehouse Items */}
              <div className="flex flex-col border-r pr-6">
                <Label className="text-base font-semibold mb-2">Preview Warehouse Items</Label>
                <Select value={previewWarehouseId} onValueChange={setPreviewWarehouseId}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select a warehouse to preview..." />
                  </SelectTrigger>
                  <SelectContent>
                    {warehouses.map(w => (
                      <SelectItem key={w.id} value={w.id}>{w.name} ({w.code})</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <div className="border rounded-lg mt-3 flex-1 overflow-hidden">
                  {!previewWarehouseId ? (
                    <div className="flex items-center justify-center h-full text-muted-foreground text-sm">
                      Select a warehouse to view its items
                    </div>
                  ) : getPreviewWarehouseItems().length === 0 ? (
                    <div className="flex items-center justify-center h-full text-muted-foreground text-sm">
                      No items assigned to this warehouse
                    </div>
                  ) : (
                    <div className="h-full overflow-y-auto">
                      <Table>
                        <TableHeader>
                          <TableRow>
                            <TableHead className="text-xs">Item Code</TableHead>
                            <TableHead className="text-xs">Name</TableHead>
                            <TableHead className="text-xs text-right">Stock</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {getPreviewWarehouseItems().map(item => (
                            <TableRow key={item.id}>
                              <TableCell className="text-xs py-2">{item.itemCode}</TableCell>
                              <TableCell className="text-xs py-2 truncate max-w-[120px]">{item.itemName}</TableCell>
                              <TableCell className="text-xs py-2 text-right">{item.currentStock}</TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                      </Table>
                    </div>
                  )}
                </div>
                <p className="text-xs text-muted-foreground mt-2">
                  {previewWarehouseId ? `${getPreviewWarehouseItems().length} items in warehouse` : ''}
                </p>
              </div>

              {/* Right Panel - Assignment Form */}
              <div className="flex flex-col overflow-hidden">
                <div className="grid grid-cols-2 gap-4 flex-1 overflow-hidden">
                  {/* Warehouses Selection */}
                  <div className="flex flex-col overflow-hidden">
                    <Label className="text-base font-semibold">Select Target Warehouses</Label>
                    <div className="relative mt-2">
                      <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
                      <Input
                        placeholder="Search warehouses..."
                        value={dialogWarehouseSearch}
                        onChange={(e) => setDialogWarehouseSearch(e.target.value)}
                        className="pl-8 h-9"
                      />
                    </div>
                    <div className="border rounded-lg p-3 mt-2 flex-1 overflow-y-auto space-y-2">
                      {getFilteredWarehouses().map(w => (
                        <div key={w.id} className="flex items-center space-x-2">
                          <Checkbox id={`wh-${w.id}`} checked={selectedWarehouses.includes(w.id)} onCheckedChange={() => toggleWarehouse(w.id)} />
                          <label htmlFor={`wh-${w.id}`} className="text-sm cursor-pointer">{w.name} ({w.code})</label>
                        </div>
                      ))}
                      {getFilteredWarehouses().length === 0 && (
                        <p className="text-sm text-muted-foreground text-center py-2">No warehouses found</p>
                      )}
                    </div>
                    <p className="text-xs text-muted-foreground mt-1">{selectedWarehouses.length} selected</p>
                  </div>

                  {/* Items Selection */}
                  <div className="flex flex-col overflow-hidden">
                    <Label className="text-base font-semibold">Select Inventory Items</Label>
                    <div className="relative mt-2">
                      <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
                      <Input
                        placeholder="Search items..."
                        value={dialogItemSearch}
                        onChange={(e) => setDialogItemSearch(e.target.value)}
                        className="pl-8 h-9"
                      />
                    </div>
                    <div className="border rounded-lg p-3 mt-2 flex-1 overflow-y-auto space-y-2">
                      {getFilteredItems().map(item => (
                        <div key={item.id} className="flex items-center space-x-2">
                          <Checkbox id={`item-${item.id}`} checked={selectedInventoryItems.includes(item.id)} onCheckedChange={() => toggleInventoryItem(item.id)} />
                          <label htmlFor={`item-${item.id}`} className="text-sm cursor-pointer">{item.itemCode} - {item.name}</label>
                        </div>
                      ))}
                      {getFilteredItems().length === 0 && (
                        <p className="text-sm text-muted-foreground text-center py-2">No items found</p>
                      )}
                    </div>
                    <p className="text-xs text-muted-foreground mt-1">{selectedInventoryItems.length} selected</p>
                  </div>
                </div>

                {/* Quantity Settings */}
                <div className="grid grid-cols-3 gap-4 mt-4 pt-4 border-t">
                  <div>
                    <Label>Initial Quantity</Label>
                    <Input type="number" min="0" value={assignForm.initialQuantity} onChange={(e) => setAssignForm({...assignForm, initialQuantity: Number(e.target.value)})} />
                  </div>
                  <div>
                    <Label>Reorder Level</Label>
                    <Input type="number" min="0" value={assignForm.reorderLevel} onChange={(e) => setAssignForm({...assignForm, reorderLevel: Number(e.target.value)})} />
                  </div>
                  <div>
                    <Label>Max Stock</Label>
                    <Input type="number" min="0" value={assignForm.maxStock} onChange={(e) => setAssignForm({...assignForm, maxStock: Number(e.target.value)})} />
                  </div>
                </div>
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => { setIsAssignDialogOpen(false); resetAssignForm(); }}>Cancel</Button>
            <Button onClick={handleAssign} disabled={selectedInventoryItems.length === 0 || selectedWarehouses.length === 0}>
              Assign {selectedInventoryItems.length} Item(s) to {selectedWarehouses.length} Warehouse(s)
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Edit Quantity Dialog */}
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Edit Warehouse Item</DialogTitle>
            <DialogDescription>
              {selectedItem && `${selectedItem.itemCode} - ${selectedItem.itemName} at ${selectedItem.warehouseName}`}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div>
              <Label>Current Stock</Label>
              <Input type="number" min="0" value={editForm.currentStock} onChange={(e) => setEditForm({...editForm, currentStock: Number(e.target.value)})} />
              {selectedItem && selectedItem.allocatedStock > 0 && (
                <p className="text-xs text-amber-600 mt-1">Note: {selectedItem.allocatedStock} units are currently allocated</p>
              )}
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label>Reorder Level</Label>
                <Input type="number" min="0" value={editForm.reorderLevel} onChange={(e) => setEditForm({...editForm, reorderLevel: Number(e.target.value)})} />
              </div>
              <div>
                <Label>Max Stock</Label>
                <Input type="number" min="0" value={editForm.maxStock} onChange={(e) => setEditForm({...editForm, maxStock: Number(e.target.value)})} />
              </div>
            </div>
            <div>
              <Label>Notes</Label>
              <Textarea value={editForm.notes || ''} onChange={(e) => setEditForm({...editForm, notes: e.target.value})} placeholder="Optional notes..." />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsEditDialogOpen(false)}>Cancel</Button>
            <Button onClick={handleUpdate}>Save Changes</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
