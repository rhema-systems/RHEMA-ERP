'use client';

import React, { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
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
  DialogTrigger,
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
  CreatePurchaseRequisitionDto,
  CreatePurchaseRequisitionItemDto,
  PurchaseRequisitionLinkageOptionsDto,
  SavePurchaseRequisitionLinkageRequest
} from '@/services/purchasingService';
import { commonService, type DepartmentDto } from '@/services/procurementPlanningService';
import { PurchaseRequisitionLinkageFields } from '@/components/procurement/PurchaseRequisitionLinkageFields';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import {
  PendingPurchaseRequisitionDocument,
  PurchaseRequisitionDocuments,
  uploadPendingPurchaseRequisitionDocuments,
} from '@/components/procurement/PurchaseRequisitionDocuments';
import {
  EMPTY_REQUISITION_LINKAGE,
  formatRequisitionMoney,
  getRequisitionCurrency,
  normalizeRequisitionLinkage,
  validateExceptionLinkage,
} from '@/lib/procurement-requisition-linkage';
import { inventoryManagementService, InventoryItemDto, ItemUnitOfMeasureDto } from '@/services/inventoryManagementService';
import { businessPartnerService, BusinessPartnerDto } from '@/services/businessPartnerService';
import { currencyService } from '@/services/financeCommonService';
import { format } from 'date-fns';

interface PRItemFormData extends CreatePurchaseRequisitionItemDto {
  tempId: string; // For tracking items before submission
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

export default function NewPurchaseRequisitionPage() {
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  
  // Form data
  const [requiredDate, setRequiredDate] = useState('');
  const [priority, setPriority] = useState('Normal');
  const [departmentId, setDepartmentId] = useState('');
  const [justification, setJustification] = useState('');
  const [notes, setNotes] = useState('');
  const [items, setItems] = useState<PRItemFormData[]>([]);
  const [pendingDocuments, setPendingDocuments] = useState<PendingPurchaseRequisitionDocument[]>([]);
  const [linkage, setLinkage] = useState<SavePurchaseRequisitionLinkageRequest>({ ...EMPTY_REQUISITION_LINKAGE });
  
  // Reference data
  const [inventoryItems, setInventoryItems] = useState<InventoryItemDto[]>([]);
  const [suppliers, setSuppliers] = useState<BusinessPartnerDto[]>([]);
  const [linkageOptions, setLinkageOptions] = useState<PurchaseRequisitionLinkageOptionsDto>();
  const [departments, setDepartments] = useState<DepartmentDto[]>([]);
  const [baseCurrencyCode, setBaseCurrencyCode] = useState('');
  const [loadingData, setLoadingData] = useState(true);
  
  // Item dialog
  const [showItemDialog, setShowItemDialog] = useState(false);
  const [editingItemIndex, setEditingItemIndex] = useState<number | null>(null);
  const [deleteItemIndex, setDeleteItemIndex] = useState<number | null>(null);
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

  // Load reference data
  useEffect(() => {
    const loadData = async () => {
      try {
        setLoadingData(true);
        const [itemsData, suppliersData, linkageData, departmentsData, baseCurrency] = await Promise.all([
          inventoryManagementService.getInventoryItems({ isActive: true }),
          businessPartnerService.getActivePartners(),
          purchasingService.getPurchaseRequisitionLinkageOptions(),
          commonService.getDepartments(),
          currencyService.getBaseCurrency(),
        ]);
        
        setInventoryItems(itemsData || []);
        // Filter to only show Supplier or Both types
        setSuppliers((suppliersData || []).filter(bp => 
          bp.partnerType === 'Supplier' || bp.partnerType === 'Both'
        ));
        setLinkageOptions(linkageData);
        setDepartments((departmentsData || []).filter((department) => department.isActive));
        setBaseCurrencyCode(baseCurrency?.code || '');
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
  const displayCurrency = getRequisitionCurrency(linkage, linkageOptions, baseCurrencyCode);

  // Handle inventory item selection
  const handleInventoryItemSelect = async (itemId: string) => {
    const item = inventoryItems.find(i => i.id === itemId);
    if (item) {
      setSelectedInventoryItem(item);
      setItemSearch('');
      
      try {
        // Load available UOMs
        const uoms = await inventoryManagementService.getItemUnitsOfMeasure(itemId);
        setAvailableUOMs(uoms);
        
        // Set default to purchase UOM or base UOM
        const purchaseUOM = uoms.find(u => u.isPurchaseUnit) || uoms.find(u => u.isBaseUnit);
        setSelectedUOM(purchaseUOM || null);
        
        setItemFormData(prev => ({
          ...prev,
          inventoryItemId: item.id,
          itemCode: item.itemCode,
          itemName: item.name,
          itemDescription: item.description || item.name,
          unitOfMeasure: purchaseUOM?.unitCode || item.unitOfMeasure || 'EA',
          estimatedUnitPrice: item.lastPurchaseCost > 0 ? item.lastPurchaseCost : item.standardCost > 0 ? item.standardCost : item.averageCost || 0
        }));
      } catch (error) {
        console.error('Error loading UOMs:', error);
        toast.error('Failed to load unit of measures');
        // Fallback to basic item data
        setItemFormData(prev => ({
          ...prev,
          inventoryItemId: item.id,
          itemCode: item.itemCode,
          itemName: item.name,
          itemDescription: item.description || item.name,
          unitOfMeasure: item.unitOfMeasure || 'EA',
          estimatedUnitPrice: item.lastPurchaseCost > 0 ? item.lastPurchaseCost : item.standardCost > 0 ? item.standardCost : item.averageCost || 0
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
    
    // Find and set the inventory item if it exists
    if (item.inventoryItemId) {
      const invItem = inventoryItems.find(i => i.id === item.inventoryItemId);
      setSelectedInventoryItem(invItem || null);
    }
    
    setShowItemDialog(true);
  };

  // Save item from dialog
  const handleSaveItem = () => {
    // Validation
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
      // Update existing item
      const updatedItems = [...items];
      updatedItems[editingItemIndex] = itemFormData;
      setItems(updatedItems);
      toast.success('Item updated');
    } else {
      // Add new item
      setItems([...items, itemFormData]);
      toast.success('Item added');
    }

    setShowItemDialog(false);
  };

  // Delete item
  const handleDeleteItem = (index: number) => {
    setDeleteItemIndex(index);
  };

  const confirmDeleteItem = () => {
    if (deleteItemIndex === null) return false;
    setItems((current) => current.filter((_, index) => index !== deleteItemIndex));
    setDeleteItemIndex(null);
    toast.success('Item removed');
    return true;
  };

  // Save as draft
  const handleSaveDraft = async () => {
    if (items.length === 0) {
      toast.error('Please add at least one item');
      return;
    }
    if (!departmentId) {
      toast.error('Select an HR department');
      return;
    }
    const linkageError = validateExceptionLinkage(linkage);
    if (linkageError) {
      toast.error(linkageError);
      return;
    }

    try {
      setSaving(true);
      
      // Get current user ID from localStorage
      const userStr = localStorage.getItem('user');
      const user = userStr ? JSON.parse(userStr) : null;
      const requestedById = user?.id || user?.userId;
      
      if (!requestedById) {
        toast.error('User not authenticated');
        return;
      }

      const createData: CreatePurchaseRequisitionDto = {
        requestedById,
        requiredDate: requiredDate || undefined,
        priority,
        departmentId,
        costCenter: linkage.costCenter || undefined,
        justification: justification || undefined,
        notes: notes || undefined,
        linkage: normalizeRequisitionLinkage(linkage),
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

      const result = await purchasingService.createPurchaseRequisition(createData);
      const documentResult = await uploadPendingPurchaseRequisitionDocuments(result.id, pendingDocuments);
      if (documentResult.failed.length > 0) {
        toast.error(
          `Purchase requisition saved as draft, but ${documentResult.failed.length} supporting document(s) could not be uploaded. Open the requisition to retry.`
        );
      } else {
        toast.success('Purchase requisition saved as draft');
      }
      router.push(`/procurement/purchase-requisitions/${result.id}`);
    } catch (error: any) {
      console.error('Error saving purchase requisition:', error);
      toast.error(error.message || 'Failed to save purchase requisition');
    } finally {
      setSaving(false);
    }
  };

  // Submit for approval
  const handleSubmit = async () => {
    if (items.length === 0) {
      toast.error('Please add at least one item');
      return;
    }

    if (!justification.trim()) {
      toast.error('Please provide justification for this requisition');
      return;
    }
    if (!departmentId) {
      toast.error('Select an HR department');
      return;
    }
    const linkageError = validateExceptionLinkage(linkage);
    if (linkageError) {
      toast.error(linkageError);
      return;
    }

    try {
      setSubmitting(true);
      
      // Get current user ID from localStorage
      const userStr = localStorage.getItem('user');
      const user = userStr ? JSON.parse(userStr) : null;
      const requestedById = user?.id || user?.userId;
      
      if (!requestedById) {
        toast.error('User not authenticated');
        return;
      }

      const createData: CreatePurchaseRequisitionDto = {
        requestedById,
        requiredDate: requiredDate || undefined,
        priority,
        departmentId,
        costCenter: linkage.costCenter || undefined,
        justification: justification || undefined,
        notes: notes || undefined,
        linkage: normalizeRequisitionLinkage(linkage),
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

      const result = await purchasingService.createPurchaseRequisition(createData);

      const documentResult = await uploadPendingPurchaseRequisitionDocuments(result.id, pendingDocuments);
      if (documentResult.failed.length > 0) {
        toast.error(
          `Purchase requisition was saved as draft because ${documentResult.failed.length} supporting document(s) could not be uploaded. Open it to retry before submission.`
        );
        router.push(`/procurement/purchase-requisitions/${result.id}`);
        return;
      }
      
      // Submit for approval
      await purchasingService.submitPurchaseRequisition(result.id);
      
      toast.success('Purchase requisition submitted for approval');
      router.push(`/procurement/purchase-requisitions/${result.id}`);
    } catch (error: any) {
      console.error('Error submitting purchase requisition:', error);
      toast.error(error.message || 'Failed to submit purchase requisition');
    } finally {
      setSubmitting(false);
    }
  };

  const filteredInventoryItems = inventoryItems.filter(item =>
    item.itemCode.toLowerCase().includes(itemSearch.toLowerCase()) ||
    item.name.toLowerCase().includes(itemSearch.toLowerCase()) ||
    item.description?.toLowerCase().includes(itemSearch.toLowerCase())
  );

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="outline" onClick={() => router.push('/procurement/purchase-requisitions')}>
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back
          </Button>
          <div>
            <h1 className="text-3xl font-bold">New Purchase Requisition</h1>
            <p className="text-muted-foreground mt-1">Create a new internal purchase request</p>
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
            <BreadcrumbPage>New</BreadcrumbPage>
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
          <CardDescription>Enter the basic details for this purchase requisition</CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div className="space-y-2">
              <Label htmlFor="requisitionDate">Requisition Date</Label>
              <Input
                id="requisitionDate"
                type="text"
                value={format(new Date(), 'MMM dd, yyyy')}
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
              <Label htmlFor="departmentId">Department *</Label>
              <Select value={departmentId} onValueChange={setDepartmentId} disabled={loadingData}>
                <SelectTrigger id="departmentId">
                  <SelectValue placeholder={loadingData ? 'Loading departments...' : 'Select HR department'} />
                </SelectTrigger>
                <SelectContent>
                  {departments.map((department) => (
                    <SelectItem key={department.id} value={department.id}>
                      {department.code ? `${department.code} - ${department.name}` : department.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
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
            <p className="text-sm text-muted-foreground">
              Required for approval process
            </p>
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

      <PurchaseRequisitionLinkageFields
        value={linkage}
        onChange={setLinkage}
        options={linkageOptions}
        loading={loadingData}
      />

      {/* Items Section */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle className="flex items-center gap-2">
                <Package className="h-5 w-5" />
                Requisition Items
              </CardTitle>
              <CardDescription>Add items to this purchase requisition</CardDescription>
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
                          {item.specifications && (
                            <div className="text-xs text-muted-foreground truncate" title={item.specifications}>
                              Specs: {item.specifications}
                            </div>
                          )}
                        </TableCell>
                        <TableCell className="text-right">{item.quantity}</TableCell>
                        <TableCell>{item.unitOfMeasure || '-'}</TableCell>
                        <TableCell className="text-right">
                          {formatRequisitionMoney(item.estimatedUnitPrice, displayCurrency)}
                        </TableCell>
                        <TableCell className="text-right font-medium">
                          {formatRequisitionMoney(calculateLineTotal(item.quantity, item.estimatedUnitPrice), displayCurrency)}
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
                          {formatRequisitionMoney(totalAmount, displayCurrency)}
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

      <PurchaseRequisitionDocuments
        pendingDocuments={pendingDocuments}
        onPendingDocumentsChange={setPendingDocuments}
      />

      {/* Action Buttons */}
      <div className="flex justify-end gap-4">
        <Button variant="outline" onClick={() => router.push('/procurement/purchase-requisitions')}>
          Cancel
        </Button>
        <Button
          variant="outline"
          onClick={handleSaveDraft}
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
              Save as Draft
            </>
          )}
        </Button>
        <Button
          onClick={handleSubmit}
          disabled={saving || submitting || items.length === 0 || !justification.trim()}
        >
          {submitting ? (
            <>
              <Loader2 className="w-4 h-4 mr-2 animate-spin" />
              Submitting...
            </>
          ) : (
            <>
              <Send className="w-4 h-4 mr-2" />
              Submit for Approval
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
                                Managed cost: {formatRequisitionMoney(
                                  item.lastPurchaseCost > 0
                                    ? item.lastPurchaseCost
                                    : item.standardCost > 0
                                      ? item.standardCost
                                      : item.averageCost || 0,
                                  displayCurrency
                                )}
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
                    placeholder="e.g., EA, KG, L"
                  />
                )}
                {selectedUOM && !selectedUOM.isBaseUnit && (
                  <p className="text-xs text-muted-foreground">
                    Conversion: 1 {selectedUOM.unitCode} = {selectedUOM.conversionFactor} base units
                  </p>
                )}
              </div>
              
              <div className="md:col-span-2 rounded-md border bg-muted/40 px-3 py-2 text-sm text-muted-foreground">
                The requester does not enter pricing. For an inventory item, the system uses its managed cost; Procurement completes commercial pricing during sourcing.
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
                  You must add at least one item before saving or submitting this requisition.
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}
      
      {!justification.trim() && items.length > 0 && (
        <Card className="border-yellow-200 bg-yellow-50">
          <CardContent className="pt-6">
            <div className="flex items-start gap-3">
              <AlertCircle className="h-5 w-5 text-yellow-600 mt-0.5" />
              <div>
                <p className="font-medium text-yellow-900">Justification required</p>
                <p className="text-sm text-yellow-700">
                  Please provide justification before submitting for approval.
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      <ConfirmationDialog
        open={deleteItemIndex !== null}
        onOpenChange={(open) => { if (!open) setDeleteItemIndex(null); }}
        title="Remove requisition item?"
        description="The item will be removed from this draft requisition."
        confirmText="Remove item"
        variant="destructive"
        onConfirm={confirmDeleteItem}
      />
    </div>
  );
}
