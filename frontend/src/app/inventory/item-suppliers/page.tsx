'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import {
  Plus, Search, Edit, Trash2, Star, Users, Package, DollarSign, Clock
} from 'lucide-react';
import {
  inventoryManagementService,
  ItemSupplierDto, CreateItemSupplierDto, UpdateItemSupplierDto,
  InventoryItemDto
} from '@/services/inventoryManagementService';
import { businessPartnerService, BusinessPartnerDto } from '@/services/businessPartnerService';
import { toast } from 'sonner';

type ProblemDetailsPayload = {
  detail?: string;
  title?: string;
  code?: string;
  extensions?: { code?: string };
};

const getErrorMessage = (error: unknown, fallback: string) => {
  const problem = (error as { response?: { data?: ProblemDetailsPayload } })?.response?.data;
  const detail = problem?.detail || problem?.title || (error instanceof Error ? error.message : fallback);
  const code = problem?.code || problem?.extensions?.code;
  return code ? `${detail} (${code})` : detail;
};

export default function ItemSuppliersPage() {
  const [items, setItems] = useState<InventoryItemDto[]>([]);
  const [selectedItemId, setSelectedItemId] = useState<string>('');
  const [suppliers, setSuppliers] = useState<ItemSupplierDto[]>([]);
  const [availableSuppliers, setAvailableSuppliers] = useState<BusinessPartnerDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');

  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [selectedSupplier, setSelectedSupplier] = useState<ItemSupplierDto | null>(null);
  const [removeTarget, setRemoveTarget] = useState<ItemSupplierDto | null>(null);
  const [removing, setRemoving] = useState(false);

  const [formData, setFormData] = useState<CreateItemSupplierDto>({
    inventoryItemId: '', supplierId: '', supplierItemCode: '', supplierItemName: '',
    unitPrice: 0, currency: 'USD', leadTimeDays: 7, minimumOrderQuantity: 1, isPreferred: false
  });

  const fetchItems = async () => {
    try {
      setLoading(true);
      const data = await inventoryManagementService.getInventoryItems();
      setItems(data);
    } catch (err) {
      console.error('Error fetching items:', err);
    } finally {
      setLoading(false);
    }
  };

  const fetchAvailableSuppliers = async () => {
    try {
      // Fetch business partners where type is "Supplier" or "Both"
      const supplierPartners = await businessPartnerService.getActivePartners('Supplier');
      const bothPartners = await businessPartnerService.getActivePartners('Both');
      setAvailableSuppliers([...supplierPartners, ...bothPartners]);
    } catch (err) {
      console.error('Error fetching available suppliers:', err);
    }
  };

  const fetchSuppliers = async (itemId: string) => {
    if (!itemId) { setSuppliers([]); return; }
    try {
      const data = await inventoryManagementService.getItemSuppliersByItem(itemId);
      setSuppliers(data);
    } catch (err) {
      console.error('Error fetching suppliers:', err);
    }
  };

  useEffect(() => {
    fetchItems();
    fetchAvailableSuppliers();
  }, []);
  useEffect(() => { fetchSuppliers(selectedItemId); }, [selectedItemId]);

  const filteredItems = items.filter(i =>
    i.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
    i.itemCode.toLowerCase().includes(searchTerm.toLowerCase())
  );

  const handleCreate = async () => {
    try {
      const newSupplier = await inventoryManagementService.createItemSupplier({
        ...formData, inventoryItemId: selectedItemId
      });
      setSuppliers(prev => [...prev, newSupplier]);
      setIsCreateDialogOpen(false);
      resetForm();
    } catch (err) {
      console.error('Error creating supplier:', err);
      toast.error('Failed to add supplier');
    }
  };

  const handleEdit = (supplier: ItemSupplierDto) => {
    setSelectedSupplier(supplier);
    setFormData({
      inventoryItemId: supplier.inventoryItemId, supplierId: supplier.supplierId,
      supplierItemCode: supplier.supplierItemCode || '', supplierItemName: supplier.supplierItemName || '',
      unitPrice: supplier.unitPrice, currency: supplier.currency || 'USD',
      leadTimeDays: supplier.leadTimeDays, minimumOrderQuantity: supplier.minimumOrderQuantity,
      isPreferred: supplier.isPreferred, notes: supplier.notes
    });
    setIsEditDialogOpen(true);
  };

  const handleUpdate = async () => {
    if (!selectedSupplier) return;
    try {
      const updateData: UpdateItemSupplierDto = {
        supplierItemCode: formData.supplierItemCode, supplierItemName: formData.supplierItemName,
        unitPrice: formData.unitPrice, currency: formData.currency, leadTimeDays: formData.leadTimeDays,
        minimumOrderQuantity: formData.minimumOrderQuantity, isPreferred: formData.isPreferred,
        isActive: selectedSupplier.isActive ?? true, notes: formData.notes
      };
      const updated = await inventoryManagementService.updateItemSupplier(selectedSupplier.id, updateData);
      setSuppliers(prev => prev.map(s => s.id === selectedSupplier.id ? updated : s));
      setIsEditDialogOpen(false);
      resetForm();
    } catch (err) {
      console.error('Error updating supplier:', err);
      toast.error('Failed to update supplier');
    }
  };

  const handleSetPreferred = async (id: string) => {
    try {
      await inventoryManagementService.setPreferredSupplier(id);
      fetchSuppliers(selectedItemId);
    } catch (err) {
      console.error('Error setting preferred:', err);
    }
  };

  const confirmRemoveSupplier = async () => {
    if (!removeTarget) return false;
    setRemoving(true);
    try {
      await inventoryManagementService.deleteItemSupplier(removeTarget.id);
      setSuppliers(prev => prev.filter(s => s.id !== removeTarget.id));
      toast.success('Supplier removed from the item');
      setRemoveTarget(null);
      return true;
    } catch (err) {
      console.error('Error deleting supplier:', err);
      toast.error(getErrorMessage(err, 'Failed to remove supplier'));
      return false;
    } finally {
      setRemoving(false);
    }
  };

  const resetForm = () => {
    setFormData({
      inventoryItemId: '', supplierId: '', supplierItemCode: '', supplierItemName: '',
      unitPrice: 0, currency: 'USD', leadTimeDays: 7, minimumOrderQuantity: 1, isPreferred: false
    });
    setSelectedSupplier(null);
  };

  const selectedItem = items.find(i => i.id === selectedItemId);

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Item Suppliers</h1>
          <p className="text-muted-foreground">Manage suppliers, pricing, and lead times for inventory items</p>
        </div>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/inventory">Inventory</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbPage>Item Suppliers</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Item Selection Panel */}
        <Card className="lg:col-span-1">
          <CardHeader>
            <CardTitle className="flex items-center"><Package className="h-4 w-4 mr-2" />Select Item</CardTitle>
            <CardDescription>Choose an item to manage its suppliers</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="relative">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input placeholder="Search items..." className="pl-8" value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} />
            </div>
            <div className="border rounded-lg divide-y max-h-96 overflow-y-auto">
              {loading ? (
                <div className="p-4 text-center text-muted-foreground">Loading...</div>
              ) : filteredItems.length === 0 ? (
                <div className="p-4 text-center text-muted-foreground">No items found</div>
              ) : (
                filteredItems.map(item => (
                  <div key={item.id}
                    className={`p-3 cursor-pointer hover:bg-muted/50 ${selectedItemId === item.id ? 'bg-blue-50 border-l-4 border-blue-500' : ''}`}
                    onClick={() => setSelectedItemId(item.id)}>
                    <div className="font-medium">{item.name}</div>
                    <div className="text-sm text-muted-foreground">{item.itemCode}</div>
                  </div>
                ))
              )}
            </div>
          </CardContent>
        </Card>

        {/* Suppliers Panel */}
        <Card className="lg:col-span-2">
          <CardHeader>
            <div className="flex items-center justify-between">
              <div>
                <CardTitle className="flex items-center"><Users className="h-4 w-4 mr-2" />Suppliers</CardTitle>
                <CardDescription>{selectedItem ? `Suppliers for ${selectedItem.name}` : 'Select an item to view suppliers'}</CardDescription>
              </div>
              {selectedItemId && (
                <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
                  <DialogTrigger asChild>
                    <Button size="sm"><Plus className="h-4 w-4 mr-1" />Add Supplier</Button>
                  </DialogTrigger>
                  <DialogContent>
                    <DialogHeader>
                      <DialogTitle>Add Supplier</DialogTitle>
                      <DialogDescription>Link a supplier to {selectedItem?.name}</DialogDescription>
                    </DialogHeader>
                    <div className="grid gap-4 py-4">
                      <div className="space-y-2">
                        <Label>Select Supplier *</Label>
                        <Select value={formData.supplierId} onValueChange={(v) => setFormData({...formData, supplierId: v})}>
                          <SelectTrigger><SelectValue placeholder="Select a supplier..." /></SelectTrigger>
                          <SelectContent>
                            {availableSuppliers.map(s => (
                              <SelectItem key={s.id} value={s.id}>
                                {s.partnerName} ({s.partnerCode})
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>
                      <div className="grid grid-cols-2 gap-4">
                        <div className="space-y-2"><Label>Supplier Item Code</Label>
                          <Input value={formData.supplierItemCode} onChange={(e) => setFormData({...formData, supplierItemCode: e.target.value})} placeholder="Supplier's product code" />
                        </div>
                        <div className="space-y-2"><Label>Supplier Item Name</Label>
                          <Input value={formData.supplierItemName} onChange={(e) => setFormData({...formData, supplierItemName: e.target.value})} placeholder="Supplier's product name" />
                        </div>
                      </div>
                      <div className="grid grid-cols-3 gap-4">
                        <div className="space-y-2"><Label>Unit Price</Label>
                          <Input type="number" value={formData.unitPrice} onChange={(e) => setFormData({...formData, unitPrice: parseFloat(e.target.value) || 0})} />
                        </div>
                        <div className="space-y-2"><Label>Currency</Label>
                          <Select value={formData.currency} onValueChange={(v) => setFormData({...formData, currency: v})}>
                            <SelectTrigger><SelectValue /></SelectTrigger>
                            <SelectContent>
                              <SelectItem value="USD">USD</SelectItem>
                              <SelectItem value="EUR">EUR</SelectItem>
                              <SelectItem value="GBP">GBP</SelectItem>
                            </SelectContent>
                          </Select>
                        </div>
                        <div className="space-y-2"><Label>Lead Time (Days)</Label>
                          <Input type="number" value={formData.leadTimeDays} onChange={(e) => setFormData({...formData, leadTimeDays: parseInt(e.target.value) || 0})} />
                        </div>
                      </div>
                      <div className="grid grid-cols-2 gap-4">
                        <div className="space-y-2"><Label>Min Order Qty</Label>
                          <Input type="number" value={formData.minimumOrderQuantity} onChange={(e) => setFormData({...formData, minimumOrderQuantity: parseFloat(e.target.value) || 1})} />
                        </div>
                        <div className="flex items-center space-x-2 pt-6">
                          <Switch checked={formData.isPreferred} onCheckedChange={(v) => setFormData({...formData, isPreferred: v})} />
                          <Label>Preferred Supplier</Label>
                        </div>
                      </div>
                      <div className="space-y-2"><Label>Notes</Label>
                        <Textarea value={formData.notes || ''} onChange={(e) => setFormData({...formData, notes: e.target.value})} rows={2} />
                      </div>
                    </div>
                    <DialogFooter>
                      <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>Cancel</Button>
                      <Button onClick={handleCreate}>Add Supplier</Button>
                    </DialogFooter>
                  </DialogContent>
                </Dialog>
              )}
            </div>
          </CardHeader>
          <CardContent>
            {!selectedItemId ? (
              <div className="text-center py-12 text-muted-foreground">Select an item from the left panel to view and manage its suppliers</div>
            ) : suppliers.length === 0 ? (
              <div className="text-center py-12 text-muted-foreground">No suppliers linked to this item</div>
            ) : (
              <div className="space-y-3">
                {suppliers.map(supplier => (
                  <div key={supplier.id} className="border rounded-lg p-4 hover:bg-muted/50">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center space-x-4">
                        <div className={`w-10 h-10 rounded-lg flex items-center justify-center ${supplier.isPreferred ? 'bg-yellow-100' : 'bg-gray-100'}`}>
                          {supplier.isPreferred ? <Star className="h-5 w-5 text-yellow-600" /> : <Users className="h-5 w-5 text-gray-600" />}
                        </div>
                        <div>
                          <div className="flex items-center space-x-2">
                            <h3 className="font-semibold">{supplier.supplierName}</h3>
                            {supplier.isPreferred && <Badge className="bg-yellow-100 text-yellow-800">Preferred</Badge>}
                            {supplier.isActive && <Badge className="bg-green-100 text-green-800">Active</Badge>}
                          </div>
                          <p className="text-sm text-muted-foreground">
                            Code: {supplier.supplierItemCode || '-'} • {supplier.supplierItemName || '-'}
                          </p>
                        </div>
                      </div>
                      <div className="flex items-center space-x-6">
                        <div className="text-right">
                          <div className="flex items-center text-lg font-semibold"><DollarSign className="h-4 w-4" />{(supplier.unitPrice ?? 0).toFixed(2)}</div>
                          <div className="text-xs text-muted-foreground">{supplier.currency || 'USD'}</div>
                        </div>
                        <div className="text-right">
                          <div className="flex items-center"><Clock className="h-4 w-4 mr-1" />{supplier.leadTimeDays} days</div>
                          <div className="text-xs text-muted-foreground">Lead Time</div>
                        </div>
                        <div className="flex items-center space-x-1">
                          {!supplier.isPreferred && (
                            <Button size="sm" variant="outline" onClick={() => handleSetPreferred(supplier.id)} title="Set as Preferred">
                              <Star className="h-4 w-4" />
                            </Button>
                          )}
                          <Button size="sm" variant="outline" onClick={() => handleEdit(supplier)}><Edit className="h-4 w-4" /></Button>
                          <Button size="sm" variant="outline" className="text-red-600" onClick={() => setRemoveTarget(supplier)} aria-label={`Remove ${supplier.supplierName || 'supplier'}`}><Trash2 className="h-4 w-4" /></Button>
                        </div>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Edit Dialog */}
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent>
          <DialogHeader><DialogTitle>Edit Supplier</DialogTitle></DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="space-y-2">
              <Label>Supplier</Label>
              <div className="p-2 bg-muted rounded-md text-sm font-medium">
                {selectedSupplier?.supplierName || 'Unknown Supplier'}
              </div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2"><Label>Supplier Item Code</Label>
                <Input value={formData.supplierItemCode} onChange={(e) => setFormData({...formData, supplierItemCode: e.target.value})} />
              </div>
              <div className="space-y-2"><Label>Supplier Item Name</Label>
                <Input value={formData.supplierItemName} onChange={(e) => setFormData({...formData, supplierItemName: e.target.value})} />
              </div>
            </div>
            <div className="grid grid-cols-3 gap-4">
              <div className="space-y-2"><Label>Unit Price</Label>
                <Input type="number" value={formData.unitPrice} onChange={(e) => setFormData({...formData, unitPrice: parseFloat(e.target.value) || 0})} />
              </div>
              <div className="space-y-2"><Label>Lead Time</Label>
                <Input type="number" value={formData.leadTimeDays} onChange={(e) => setFormData({...formData, leadTimeDays: parseInt(e.target.value) || 0})} />
              </div>
              <div className="space-y-2"><Label>Min Order Qty</Label>
                <Input type="number" value={formData.minimumOrderQuantity} onChange={(e) => setFormData({...formData, minimumOrderQuantity: parseFloat(e.target.value) || 1})} />
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsEditDialogOpen(false)}>Cancel</Button>
            <Button onClick={handleUpdate}>Save Changes</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={removeTarget !== null}
        onOpenChange={(open) => { if (!open && !removing) setRemoveTarget(null); }}
        title="Remove item supplier?"
        description={`Remove ${removeTarget?.supplierName || 'this supplier'} from ${selectedItem?.name || 'the selected item'}? The supplier record itself will not be deleted.`}
        confirmText="Remove supplier"
        variant="destructive"
        onConfirm={confirmRemoveSupplier}
        isLoading={removing}
      />
    </div>
  );
}
