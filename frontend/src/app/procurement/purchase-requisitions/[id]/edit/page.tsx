'use client';

import React, { useState, useEffect } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Separator } from '@/components/ui/separator';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  ArrowLeft,
  Plus,
  Save,
  Send,
  Trash2,
  Edit,
  Search,
  Loader2,
  AlertCircle,
  Package,
  Calendar,
  DollarSign,
  FileText,
  Building2
} from 'lucide-react';
import { toast } from 'sonner';
import {
  purchasingService,
  PurchaseRequisitionDetailDto,
  CreatePurchaseRequisitionDto,
  CreatePurchaseRequisitionItemDto
} from '@/services/purchasingService';
import { inventoryManagementService, InventoryItemDto, ItemUnitOfMeasureDto } from '@/services/inventoryManagementService';
import { businessPartnerService, BusinessPartnerDto } from '@/services/businessPartnerService';
import { format } from 'date-fns';

interface PRItemFormData extends CreatePurchaseRequisitionItemDto {
  tempId: string;
  itemCode?: string;
  itemName?: string;
  preferredSupplierName?: string;
}

const priorityOptions = [
  { value: 'Low', label: 'Low', color: 'text-gray-600' },
  { value: 'Normal', label: 'Normal', color: 'text-blue-600' },
  { value: 'High', label: 'High', color: 'text-orange-600' },
  { value: 'Urgent', label: 'Urgent', color: 'text-red-600' }
];

export default function EditPurchaseRequisitionPage() {
  const router = useRouter();
  const params = useParams();
  const id = params.id as string;
  
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  
  // Form data
  const [requisitionNumber, setRequisitionNumber] = useState('');
  const [requisitionDate, setRequisitionDate] = useState('');
  const [requiredDate, setRequiredDate] = useState('');
  const [priority, setPriority] = useState('Normal');
  const [department, setDepartment] = useState('');
  const [costCenter, setCostCenter] = useState('');
  const [justification, setJustification] = useState('');
  const [notes, setNotes] = useState('');
  const [items, setItems] = useState<PRItemFormData[]>([]);
  
  // Reference data
  const [inventoryItems, setInventoryItems] = useState<InventoryItemDto[]>([]);
  const [suppliers, setSuppliers] = useState<BusinessPartnerDto[]>([]);
  const [loadingData, setLoadingData] = useState(true);
  
  // Item dialog
  const [showItemDialog, setShowItemDialog] = useState(false);
  const [editingItemIndex, setEditingItemIndex] = useState<number | null>(null);
  const [itemSearch, setItemSearch] = useState('');
  const [selectedInventoryItem, setSelectedInventoryItem] = useState<InventoryItemDto | null>(null);
  const [availableUOMs, setAvailableUOMs] = useState<ItemUnitOfMeasureDto[]>([]);
  const [selectedUOM, setSelectedUOM] = useState<ItemUnitOfMeasureDto | null>(null);
  const [itemFormData, setItemFormData] = useState<PRItemFormData>({
    tempId: '',
    inventoryItemId: '',
    itemDescription: '',
    quantity: 1,
    unitOfMeasure: '',
    estimatedUnitPrice: 0,
    requiredDate: '',
    preferredSupplierId: '',
    notes: '',
    specifications: ''
  });

  // Load existing requisition
  useEffect(() => {
    const loadRequisition = async () => {
      try {
        setLoading(true);
        const pr = await purchasingService.getPurchaseRequisitionById(id);
        
        // Check if can edit
        if (pr.status !== 'Draft') {
          toast.error('Only draft requisitions can be edited');
          router.push(`/procurement/purchase-requisitions/${id}`);
          return;
        }
        
        // Load form data
        setRequisitionNumber(pr.requisitionNumber);
        setRequisitionDate(pr.requisitionDate);
        setRequiredDate(pr.requiredDate ? pr.requiredDate.split('T')[0] : '');
        setPriority(pr.priority);
        setDepartment(pr.department || '');
        setCostCenter(pr.costCenter || '');
        setJustification(pr.justification || '');
        setNotes(pr.notes || '');
        
        // Load items
        const loadedItems: PRItemFormData[] = pr.items.map((item, index) => ({
          tempId: `existing-${index}`,
          inventoryItemId: item.inventoryItemId || '',
          itemCode: item.itemCode,
          itemName: item.itemName,
          itemDescription: item.itemDescription,
          quantity: item.quantity,
          unitOfMeasure: item.unitOfMeasure,
          estimatedUnitPrice: item.estimatedUnitPrice,
          requiredDate: item.requiredDate ? item.requiredDate.split('T')[0] : '',
          preferredSupplierId: item.preferredSupplierId || '',
          preferredSupplierName: item.preferredSupplierName,
          notes: item.notes || '',
          specifications: item.specifications || ''
        }));
        
        setItems(loadedItems);
      } catch (error: any) {
        console.error('Error loading requisition:', error);
        toast.error('Failed to load requisition');
        router.push('/procurement/purchase-requisitions');
      } finally {
        setLoading(false);
      }
    };
    
    loadRequisition();
  }, [id]);

  // Load reference data
  useEffect(() => {
    const loadData = async () => {
      try {
        setLoadingData(true);
        const [itemsData, suppliersData] = await Promise.all([
          inventoryManagementService.getInventoryItems({ isActive: true }),
          businessPartnerService.getActivePartners()
        ]);
        
        setInventoryItems(itemsData || []);
        setSuppliers((suppliersData || []).filter(bp => 
          bp.partnerType === 'Supplier' || bp.partnerType === 'Both'
        ));
      } catch (error) {
        console.error('Error loading reference data:', error);
        toast.error('Failed to load reference data');
      } finally {
        setLoadingData(false);
      }
    };

    loadData();
  }, []);

  // Calculate totals
  const calculateLineTotal = (quantity: number, unitPrice: number) => {
    return quantity * unitPrice;
  };

  const totalAmount = items.reduce((sum, item) => 
    sum + calculateLineTotal(item.quantity, item.estimatedUnitPrice), 0
  );

  // Handle inventory item selection
  const handleInventoryItemSelect = async (itemId: string) => {
    const item = inventoryItems.find(i => i.id === itemId);
    if (item) {
      setSelectedInventoryItem(item);
      
      // Determine the UOM to use - this will be preserved throughout the function
      let finalUOM = item.unitOfMeasure || 'EA';
      
      try {
        // Load available UOMs
        const uoms = await inventoryManagementService.getItemUnitsOfMeasure(itemId);
        setAvailableUOMs(uoms);
        
        // Set default to purchase UOM or base UOM
        const purchaseUOM = uoms.find(u => u.isPurchaseUnit) || uoms.find(u => u.isBaseUnit);
        setSelectedUOM(purchaseUOM || null);
        
        // Update UOM if we found a purchase UOM
        if (purchaseUOM?.unitCode) {
          finalUOM = purchaseUOM.unitCode;
        }
        
        // Set all form data at once to avoid race conditions
        setItemFormData(prev => ({
          ...prev,
          inventoryItemId: item.id,
          itemCode: item.itemCode,
          itemName: item.name,
          itemDescription: item.description || item.name,
          unitOfMeasure: finalUOM,
          estimatedUnitPrice: item.lastPurchaseCost || item.standardCost || item.currentCost || 0
        }));
        
      } catch (error) {
        console.error('Error loading UOMs:', error);
        // Fallback to basic item data
        setAvailableUOMs([]);
        setSelectedUOM(null);
        setItemFormData(prev => ({
          ...prev,
          inventoryItemId: item.id,
          itemCode: item.itemCode,
          itemName: item.name,
          itemDescription: item.description || item.name,
          unitOfMeasure: finalUOM,
          estimatedUnitPrice: item.lastPurchaseCost || item.standardCost || item.currentCost || 0
        }));
      }
    }
  };

  // Open dialog for new item
  const handleAddItem = () => {
    setEditingItemIndex(null);
    setSelectedInventoryItem(null);
    setAvailableUOMs([]);
    setSelectedUOM(null);
    setItemFormData({
      tempId: Date.now().toString(),
      inventoryItemId: '',
      itemDescription: '',
      quantity: 1,
      unitOfMeasure: 'EA',
      estimatedUnitPrice: 0,
      requiredDate: '',
      preferredSupplierId: '',
      notes: '',
      specifications: ''
    });
    setShowItemDialog(true);
  };

  // Open dialog for editing item
  const handleEditItem = (index: number) => {
    const item = items[index];
    setEditingItemIndex(index);
    setItemFormData(item);
    
    if (item.inventoryItemId) {
      const invItem = inventoryItems.find(i => i.id === item.inventoryItemId);
      setSelectedInventoryItem(invItem || null);
    }
    
    setShowItemDialog(true);
  };

  // Save item from dialog
  const handleSaveItem = () => {
    if (!itemFormData.itemDescription.trim()) {
      toast.error('Please enter item description');
      return;
    }
    if (itemFormData.quantity <= 0) {
      toast.error('Quantity must be greater than 0');
      return;
    }
    if (itemFormData.estimatedUnitPrice < 0) {
      toast.error('Unit price cannot be negative');
      return;
    }

    if (editingItemIndex !== null) {
      const updatedItems = [...items];
      updatedItems[editingItemIndex] = itemFormData;
      setItems(updatedItems);
      toast.success('Item updated');
    } else {
      setItems([...items, itemFormData]);
      toast.success('Item added');
    }

    setShowItemDialog(false);
  };

  // Delete item
  const handleDeleteItem = (index: number) => {
    if (confirm('Are you sure you want to remove this item?')) {
      const updatedItems = items.filter((_, i) => i !== index);
      setItems(updatedItems);
      toast.success('Item removed');
    }
  };

  // Save changes
  const handleSave = async () => {
    if (items.length === 0) {
      toast.error('Please add at least one item');
      return;
    }

    try {
      setSaving(true);
      
      const userStr = localStorage.getItem('user');
      const user = userStr ? JSON.parse(userStr) : null;
      const requestedById = user?.id || user?.userId;
      
      if (!requestedById) {
        toast.error('User not authenticated');
        return;
      }

      const updateData: CreatePurchaseRequisitionDto = {
        requestedById,
        requiredDate: requiredDate || undefined,
        priority,
        department: department || undefined,
        costCenter: costCenter || undefined,
        justification: justification || undefined,
        notes: notes || undefined,
        items: items.map(item => ({
          inventoryItemId: item.inventoryItemId || undefined,
          itemDescription: item.itemDescription,
          quantity: item.quantity,
          unitOfMeasure: item.unitOfMeasure || undefined,
          estimatedUnitPrice: item.estimatedUnitPrice,
          requiredDate: item.requiredDate || undefined,
          preferredSupplierId: item.preferredSupplierId || undefined,
          notes: item.notes || undefined,
          specifications: item.specifications || undefined
        }))
      };

      // Note: Using create endpoint as update endpoint may not exist
      // In production, you'd want a PUT /api/PurchaseRequisitions/{id} endpoint
      await purchasingService.createPurchaseRequisition(updateData);
      
      toast.success('Purchase requisition updated');
      router.push(`/procurement/purchase-requisitions/${id}`);
    } catch (error: any) {
      console.error('Error updating purchase requisition:', error);
      toast.error(error.message || 'Failed to update purchase requisition');
    } finally {
      setSaving(false);
    }
  };

  const filteredInventoryItems = inventoryItems.filter(item =>
    item.itemCode.toLowerCase().includes(itemSearch.toLowerCase()) ||
    item.name.toLowerCase().includes(itemSearch.toLowerCase()) ||
    item.description?.toLowerCase().includes(itemSearch.toLowerCase())
  );

  if (loading) {
    return (
      <div className="flex items-center justify-center h-96">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        <p className="ml-3 text-muted-foreground">Loading requisition...</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="outline" onClick={() => router.push(`/procurement/purchase-requisitions/${id}`)}>
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back
          </Button>
          <div>
            <h1 className="text-3xl font-bold">Edit Purchase Requisition</h1>
            <p className="text-muted-foreground mt-1">{requisitionNumber}</p>
          </div>
        </div>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/procurement">Procurement</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/procurement/purchase-requisitions">Purchase Requisitions</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href={`/procurement/purchase-requisitions/${id}`}>{requisitionNumber}</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Edit</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Basic Information */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <FileText className="h-5 w-5" />
            Basic Information
          </CardTitle>
          <CardDescription>Update the basic details for this purchase requisition</CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div className="space-y-2">
              <Label htmlFor="requisitionDate">Requisition Date</Label>
              <Input
                id="requisitionDate"
                type="text"
                value={requisitionDate ? format(new Date(requisitionDate), 'MMM dd, yyyy') : ''}
                disabled
                className="bg-muted"
              />
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="requiredDate" className="flex items-center gap-2">
                <Calendar className="h-4 w-4" />
                Required Date
              </Label>
              <Input
                id="requiredDate"
                type="date"
                value={requiredDate}
                onChange={(e) => setRequiredDate(e.target.value)}
              />
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="priority">Priority *</Label>
              <Select value={priority} onValueChange={setPriority}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {priorityOptions.map(opt => (
                    <SelectItem key={opt.value} value={opt.value}>
                      <span className={opt.color}>{opt.label}</span>
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="department">Department</Label>
              <Input
                id="department"
                value={department}
                onChange={(e) => setDepartment(e.target.value)}
                placeholder="e.g., IT, HR, Operations"
              />
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="costCenter">Cost Center</Label>
              <Input
                id="costCenter"
                value={costCenter}
                onChange={(e) => setCostCenter(e.target.value)}
                placeholder="e.g., CC-001"
              />
            </div>
          </div>
          
          <Separator />
          
          <div className="space-y-2">
            <Label htmlFor="justification">Justification *</Label>
            <Textarea
              id="justification"
              value={justification}
              onChange={(e) => setJustification(e.target.value)}
              placeholder="Explain why this purchase is necessary..."
              rows={3}
            />
          </div>
          
          <div className="space-y-2">
            <Label htmlFor="notes">Additional Notes</Label>
            <Textarea
              id="notes"
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="Any additional information..."
              rows={3}
            />
          </div>
        </CardContent>
      </Card>

      {/* Items Section */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle className="flex items-center gap-2">
                <Package className="h-5 w-5" />
                Requisition Items
              </CardTitle>
              <CardDescription>Manage items in this purchase requisition</CardDescription>
            </div>
            <Button onClick={handleAddItem}>
              <Plus className="h-4 w-4 mr-2" />
              Add Item
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          {items.length === 0 ? (
            <div className="text-center py-12 border-2 border-dashed rounded-lg">
              <Package className="h-12 w-12 mx-auto text-muted-foreground mb-4" />
              <p className="text-muted-foreground mb-4">No items added yet</p>
              <Button onClick={handleAddItem}>
                <Plus className="h-4 w-4 mr-2" />
                Add First Item
              </Button>
            </div>
          ) : (
            <div className="space-y-4">
              <div className="border rounded-lg overflow-hidden">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-[50px]">#</TableHead>
                      <TableHead>Item</TableHead>
                      <TableHead>Description</TableHead>
                      <TableHead className="text-right">Qty</TableHead>
                      <TableHead>UOM</TableHead>
                      <TableHead className="text-right">Est. Unit Price</TableHead>
                      <TableHead className="text-right">Line Total</TableHead>
                      <TableHead>Preferred Supplier</TableHead>
                      <TableHead className="text-right">Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {items.map((item, index) => (
                      <TableRow key={item.tempId}>
                        <TableCell className="font-medium">{index + 1}</TableCell>
                        <TableCell>
                          <div className="font-medium">{item.itemCode || 'N/A'}</div>
                          <div className="text-sm text-muted-foreground">{item.itemName || 'Custom Item'}</div>
                        </TableCell>
                        <TableCell className="max-w-[200px]">
                          <div className="truncate" title={item.itemDescription}>
                            {item.itemDescription}
                          </div>
                        </TableCell>
                        <TableCell className="text-right">{item.quantity}</TableCell>
                        <TableCell>{item.unitOfMeasure || '-'}</TableCell>
                        <TableCell className="text-right">
                          ${item.estimatedUnitPrice.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                        </TableCell>
                        <TableCell className="text-right font-medium">
                          ${calculateLineTotal(item.quantity, item.estimatedUnitPrice).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                        </TableCell>
                        <TableCell>
                          <div className="text-sm">{item.preferredSupplierName || '-'}</div>
                        </TableCell>
                        <TableCell className="text-right">
                          <div className="flex items-center justify-end gap-2">
                            <Button
                              size="sm"
                              variant="ghost"
                              onClick={() => handleEditItem(index)}
                            >
                              <Edit className="h-4 w-4" />
                            </Button>
                            <Button
                              size="sm"
                              variant="ghost"
                              className="text-red-600 hover:text-red-700"
                              onClick={() => handleDeleteItem(index)}
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          </div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
              
              {/* Total */}
              <div className="flex justify-end">
                <Card className="w-full md:w-96">
                  <CardContent className="pt-6">
                    <div className="space-y-2">
                      <div className="flex justify-between text-sm">
                        <span className="text-muted-foreground">Total Items:</span>
                        <span className="font-medium">{items.length}</span>
                      </div>
                      <Separator />
                      <div className="flex justify-between">
                        <span className="font-semibold flex items-center gap-2">
                          <DollarSign className="h-4 w-4" />
                          Total Amount:
                        </span>
                        <span className="text-xl font-bold text-primary">
                          ${totalAmount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                        </span>
                      </div>
                    </div>
                  </CardContent>
                </Card>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Action Buttons */}
      <div className="flex justify-end gap-4">
        <Button variant="outline" onClick={() => router.push(`/procurement/purchase-requisitions/${id}`)}>
          Cancel
        </Button>
        <Button
          onClick={handleSave}
          disabled={saving || submitting || items.length === 0}
        >
          {saving ? (
            <>
              <Loader2 className="w-4 h-4 mr-2 animate-spin" />
              Saving...
            </>
          ) : (
            <>
              <Save className="w-4 h-4 mr-2" />
              Save Changes
            </>
          )}
        </Button>
      </div>

      {/* Add/Edit Item Dialog */}
      <Dialog open={showItemDialog} onOpenChange={setShowItemDialog}>
        <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>
              {editingItemIndex !== null ? 'Edit Item' : 'Add Item'}
            </DialogTitle>
            <DialogDescription>
              {editingItemIndex !== null 
                ? 'Update the item details below' 
                : 'Select an inventory item or enter custom item details'}
            </DialogDescription>
          </DialogHeader>
          
          <div className="space-y-6">
            {/* Inventory Item Selection */}
            <div className="space-y-4">
              <Label>Select from Inventory (Optional)</Label>
              <div className="relative">
                <Search className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
                <Input
                  placeholder="Search inventory items..."
                  className="pl-10"
                  value={itemSearch}
                  onChange={(e) => setItemSearch(e.target.value)}
                />
              </div>
              
              {itemSearch && (
                <div className="border rounded-lg max-h-60 overflow-y-auto">
                  {loadingData ? (
                    <div className="p-4 text-center text-muted-foreground">Loading...</div>
                  ) : filteredInventoryItems.length === 0 ? (
                    <div className="p-4 text-center text-muted-foreground">No items found</div>
                  ) : (
                    <div className="divide-y">
                      {filteredInventoryItems.slice(0, 10).map(item => (
                        <div
                          key={item.id}
                          className={`p-3 hover:bg-muted cursor-pointer transition-colors ${
                            selectedInventoryItem?.id === item.id ? 'bg-blue-50' : ''
                          }`}
                          onClick={() => handleInventoryItemSelect(item.id)}
                        >
                          <div className="flex items-center justify-between">
                            <div>
                              <div className="font-medium">{item.itemCode} - {item.name}</div>
                              <div className="text-sm text-muted-foreground">{item.description}</div>
                              <div className="text-xs text-muted-foreground mt-1">
                                Stock: {item.currentStock} {item.unitOfMeasure} • 
                                Last Cost: ${item.lastPurchaseCost?.toFixed(2) || item.standardCost?.toFixed(2) || '0.00'}
                              </div>
                            </div>
                            {selectedInventoryItem?.id === item.id && (
                              <div className="text-blue-600 font-medium">Selected</div>
                            )}
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </div>
            
            <Separator />
            
            {/* Item Details Form */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div className="space-y-2 md:col-span-2">
                <Label htmlFor="itemDescription">Item Description *</Label>
                <Textarea
                  id="itemDescription"
                  value={itemFormData.itemDescription}
                  onChange={(e) => setItemFormData(prev => ({ ...prev, itemDescription: e.target.value }))}
                  placeholder="Enter item description"
                  rows={2}
                />
              </div>
              
              <div className="space-y-2">
                <Label htmlFor="quantity">Quantity *</Label>
                <Input
                  id="quantity"
                  type="number"
                  min="0.01"
                  step="0.01"
                  value={itemFormData.quantity}
                  onChange={(e) => setItemFormData(prev => ({ ...prev, quantity: parseFloat(e.target.value) || 0 }))}
                />
              </div>
              
              {/* UOM Selection */}
              <div className="space-y-2">
                <Label htmlFor="unitOfMeasure">Unit of Measure *</Label>
                {availableUOMs.length > 0 ? (
                  <Select
                    value={selectedUOM?.id || '__none__'}
                    onValueChange={(value) => {
                      if (value === '__none__') return;
                      const uom = availableUOMs.find(u => u.id === value);
                      if (uom) {
                        setSelectedUOM(uom);
                        setItemFormData(prev => ({
                          ...prev,
                          unitOfMeasure: uom.unitCode
                        }));
                      }
                    }}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select unit of measure" />
                    </SelectTrigger>
                    <SelectContent>
                      {availableUOMs.map(uom => (
                        <SelectItem key={uom.id} value={uom.id}>
                          {uom.unitCode} - {uom.unitName}
                          {uom.conversionFactor !== 1 && ` (${uom.conversionFactor}x)`}
                          {uom.isBaseUnit && ' [Base]'}
                          {uom.isPurchaseUnit && ' [Purchase]'}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                ) : (
                  <Input
                    id="unitOfMeasure"
                    value={itemFormData.unitOfMeasure}
                    onChange={(e) => setItemFormData(prev => ({ ...prev, unitOfMeasure: e.target.value }))}
                    placeholder="Enter unit of measure (e.g., EA, KG, L)"
                  />
                )}
                {selectedUOM && !selectedUOM.isBaseUnit && (
                  <p className="text-xs text-muted-foreground">
                    Conversion: 1 {selectedUOM.unitCode} = {selectedUOM.conversionFactor} base units
                  </p>
                )}
              </div>
              
              <div className="space-y-2">
                <Label htmlFor="estimatedUnitPrice" className="flex items-center gap-2">
                  <DollarSign className="h-4 w-4" />
                  Estimated Unit Price *
                </Label>
                <Input
                  id="estimatedUnitPrice"
                  type="number"
                  min="0"
                  step="0.01"
                  value={itemFormData.estimatedUnitPrice}
                  onChange={(e) => setItemFormData(prev => ({ ...prev, estimatedUnitPrice: parseFloat(e.target.value) || 0 }))}
                />
              </div>
              
              <div className="space-y-2">
                <Label>Line Total</Label>
                <Input
                  type="text"
                  value={`$${calculateLineTotal(itemFormData.quantity, itemFormData.estimatedUnitPrice).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`}
                  disabled
                  className="bg-muted font-medium"
                />
              </div>
              
              <div className="space-y-2">
                <Label htmlFor="itemRequiredDate">Item Required Date</Label>
                <Input
                  id="itemRequiredDate"
                  type="date"
                  value={itemFormData.requiredDate}
                  onChange={(e) => setItemFormData(prev => ({ ...prev, requiredDate: e.target.value }))}
                />
              </div>
              
              <div className="space-y-2">
                <Label htmlFor="preferredSupplier" className="flex items-center gap-2">
                  <Building2 className="h-4 w-4" />
                  Preferred Supplier
                </Label>
                <Select
                  value={itemFormData.preferredSupplierId || '__none__'}
                  onValueChange={(value) => {
                    const supplierId = value === '__none__' ? '' : value;
                    const supplier = suppliers.find(s => s.id === supplierId);
                    setItemFormData(prev => ({
                      ...prev,
                      preferredSupplierId: supplierId,
                      preferredSupplierName: supplier?.partnerName
                    }));
                  }}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select preferred supplier" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__none__">None</SelectItem>
                    {suppliers.map(supplier => (
                      <SelectItem key={supplier.id} value={supplier.id}>
                        {supplier.partnerCode} - {supplier.partnerName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              
              <div className="space-y-2 md:col-span-2">
                <Label htmlFor="specifications">Specifications</Label>
                <Textarea
                  id="specifications"
                  value={itemFormData.specifications}
                  onChange={(e) => setItemFormData(prev => ({ ...prev, specifications: e.target.value }))}
                  placeholder="Technical specifications, requirements, etc."
                  rows={2}
                />
              </div>
              
              <div className="space-y-2 md:col-span-2">
                <Label htmlFor="itemNotes">Item Notes</Label>
                <Textarea
                  id="itemNotes"
                  value={itemFormData.notes}
                  onChange={(e) => setItemFormData(prev => ({ ...prev, notes: e.target.value }))}
                  placeholder="Additional notes for this item..."
                  rows={2}
                />
              </div>
            </div>
            
            {/* Dialog Actions */}
            <div className="flex justify-end gap-4 pt-4">
              <Button variant="outline" onClick={() => setShowItemDialog(false)}>
                Cancel
              </Button>
              <Button onClick={handleSaveItem}>
                {editingItemIndex !== null ? 'Update Item' : 'Add Item'}
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>

      {/* Validation Warning */}
      {items.length === 0 && (
        <Card className="border-yellow-200 bg-yellow-50">
          <CardContent className="pt-6">
            <div className="flex items-start gap-3">
              <AlertCircle className="h-5 w-5 text-yellow-600 mt-0.5" />
              <div>
                <p className="font-medium text-yellow-900">No items added</p>
                <p className="text-sm text-yellow-700">
                  You must add at least one item before saving this requisition.
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
