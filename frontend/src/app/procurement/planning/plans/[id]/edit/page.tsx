'use client';

import { useState, useEffect } from 'react';
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
import { ArrowLeft, Save, Loader2, Plus, Trash2, Search, Package, Pencil, Users } from 'lucide-react';
import { toast } from 'sonner';
import { procurementPlanService, commonService, type UpdateProcurementPlanDto, type ProcurementPlanDetailDto, type DepartmentDto, type InventoryItemDto, type CreateProcurementPlanItemDto, type UpdateProcurementPlanItemDto, type ProcurementPlanItemDto, type CreateProcurementPlanItemSupplierDto, type ProcurementPlanItemSupplierDto } from '@/services/procurementPlanningService';
import { businessPartnerService, type BusinessPartnerDto } from '@/services/businessPartnerService';

export default function EditProcurementPlanPage() {
  const params = useParams();
  const router = useRouter();
  const planId = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [departments, setDepartments] = useState<DepartmentDto[]>([]);
  const [loadingDepartments, setLoadingDepartments] = useState(true);
  const [plan, setPlan] = useState<ProcurementPlanDetailDto | null>(null);
  const [formData, setFormData] = useState<UpdateProcurementPlanDto>({
    title: '',
    description: '',
    departmentId: '',
    fiscalYear: new Date().getFullYear(),
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
  const [deletingItemId, setDeletingItemId] = useState<string | null>(null);
  const [itemForm, setItemForm] = useState<CreateProcurementPlanItemDto>({
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

  // Supplier selection state
  const [suppliers, setSuppliers] = useState<BusinessPartnerDto[]>([]);
  const [loadingSuppliers, setLoadingSuppliers] = useState(false);
  const [supplierSearchTerm, setSupplierSearchTerm] = useState('');
  const [selectedItemSuppliers, setSelectedItemSuppliers] = useState<CreateProcurementPlanItemSupplierDto[]>([]);

  useEffect(() => {
    const fetchData = async () => {
      try {
        setLoading(true);
        setLoadingDepartments(true);

        // Fetch both plan and departments in parallel
        const [planData, deptData] = await Promise.all([
          procurementPlanService.getPlanById(planId),
          commonService.getDepartments()
        ]);

        setPlan(planData);
        setDepartments(deptData);

        // Populate form data
        setFormData({
          title: planData.title,
          description: planData.description || '',
          departmentId: planData.departmentId,
          fiscalYear: planData.fiscalYear,
          planStartDate: planData.planStartDate?.split('T')[0] || '',
          planEndDate: planData.planEndDate?.split('T')[0] || '',
          planDurationYears: planData.planDurationYears,
          totalEstimatedBudget: planData.totalEstimatedBudget,
          currency: planData.currency || 'USD',
          notes: planData.notes || '',
        });
      } catch (error) {
        console.error('Error loading data:', error);
        toast.error('Failed to load procurement plan');
        router.push('/procurement/planning/plans');
      } finally {
        setLoading(false);
        setLoadingDepartments(false);
      }
    };

    if (planId) {
      fetchData();
    }
  }, [planId, router]);

  const handleInputChange = (field: keyof UpdateProcurementPlanDto, value: string | number) => {
    setFormData((prev) => ({ ...prev, [field]: value }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!formData.title.trim()) {
      toast.error('Title is required');
      return;
    }
    if (!formData.departmentId) {
      toast.error('Department is required');
      return;
    }

    try {
      setSaving(true);
      await procurementPlanService.updatePlan(planId, formData);
      toast.success('Procurement plan updated successfully');
      router.push(`/procurement/planning/plans/${planId}`);
    } catch (error) {
      console.error('Error updating plan:', error);
      toast.error('Failed to update procurement plan');
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
      const response = await businessPartnerService.getPartners({
        partnerType: 'Supplier',
        approvalStatus: 'Approved',
        pageSize: 1000
      });
      setSuppliers(response.items || []);
    } catch (error) {
      console.error('Error loading suppliers:', error);
      toast.error('Failed to load suppliers');
    } finally {
      setLoadingSuppliers(false);
    }
  };

  const handleOpenAddItemDialog = () => {
    setEditingItem(null);
    setItemDialogOpen(true);
    loadInventoryItems();
    loadSuppliers();
    setSelectedInventoryItem(null);
    setSelectedItemSuppliers([]);
    setSupplierSearchTerm('');
    setItemForm({
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
  };

  const handleOpenEditItemDialog = (item: ProcurementPlanItemDto) => {
    setEditingItem(item);
    setItemDialogOpen(true);
    loadSuppliers();
    setSelectedInventoryItem(null);
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
      itemDescription: item.itemDescription,
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
      procurementMethod: item.procurementMethod || 'DirectPurchase',
      notes: item.notes || '',
      itemSuppliers: existingSuppliers,
    });
  };

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
    setItemForm({ ...itemForm, itemSuppliers: updatedSuppliers });
  };

  const handleRemoveSupplierFromItem = (supplierId: string) => {
    const updatedSuppliers = selectedItemSuppliers
      .filter(s => s.supplierId !== supplierId)
      .map((s, index) => ({ ...s, priority: index + 1 }));
    setSelectedItemSuppliers(updatedSuppliers);
    setItemForm({ ...itemForm, itemSuppliers: updatedSuppliers });
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
    setItemForm({ ...itemForm, itemSuppliers: updatedSuppliers });
  };

  const filteredSuppliers = suppliers.filter(s =>
    s.partnerName.toLowerCase().includes(supplierSearchTerm.toLowerCase()) ||
    s.partnerCode.toLowerCase().includes(supplierSearchTerm.toLowerCase())
  );

  const handleSelectInventoryItem = (item: InventoryItemDto) => {
    setSelectedInventoryItem(item);
    setItemForm({
      ...itemForm,
      inventoryItemId: item.id,
      itemDescription: item.name,
      specifications: item.description || '',
      itemCategory: item.categoryName || '',
      unitOfMeasure: item.unitOfMeasure || 'EA',
      estimatedUnitPrice: item.standardCost || item.averageCost || 0,
    });
  };

  const handleSaveItem = async () => {
    if (!itemForm.itemDescription.trim()) {
      toast.error('Item description is required');
      return;
    }
    if (itemForm.estimatedQuantity <= 0) {
      toast.error('Quantity must be greater than 0');
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
      toast.error(editingItem ? 'Failed to update item' : 'Failed to add item');
    } finally {
      setSavingItem(false);
    }
  };

  const handleDeleteItem = async (itemId: string) => {
    if (!confirm('Are you sure you want to delete this item?')) return;

    try {
      setDeletingItemId(itemId);
      await procurementPlanService.removeItem(planId, itemId);
      toast.success('Item deleted successfully');
      // Refresh plan data
      const updatedPlan = await procurementPlanService.getPlanById(planId);
      setPlan(updatedPlan);
    } catch (error) {
      console.error('Error deleting item:', error);
      toast.error('Failed to delete item');
    } finally {
      setDeletingItemId(null);
    }
  };

  const filteredInventoryItems = inventoryItems.filter(item =>
    item.name?.toLowerCase().includes(inventorySearchTerm.toLowerCase()) ||
    item.itemCode?.toLowerCase().includes(inventorySearchTerm.toLowerCase()) ||
    item.categoryName?.toLowerCase().includes(inventorySearchTerm.toLowerCase())
  );

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
                  <Label htmlFor="departmentId">Department *</Label>
                  <Select
                    value={formData.departmentId}
                    onValueChange={(value) => handleInputChange('departmentId', value)}
                    disabled={loadingDepartments}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder={loadingDepartments ? "Loading departments..." : "Select department"} />
                    </SelectTrigger>
                    <SelectContent>
                      {departments.map((dept) => (
                        <SelectItem key={dept.id} value={dept.id}>
                          {dept.code ? `${dept.code} - ${dept.name}` : dept.name}
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
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="fiscalYear">Fiscal Year *</Label>
                  <Input
                    id="fiscalYear"
                    type="number"
                    value={formData.fiscalYear}
                    onChange={(e) => handleInputChange('fiscalYear', parseInt(e.target.value))}
                    min={2020}
                    max={2050}
                    required
                  />
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
              <CardDescription>Define the budget for this plan</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
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
                  <Select
                    value={formData.currency}
                    onValueChange={(value) => handleInputChange('currency', value)}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select currency" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="USD">USD - US Dollar</SelectItem>
                      <SelectItem value="EUR">EUR - Euro</SelectItem>
                      <SelectItem value="GBP">GBP - British Pound</SelectItem>
                      <SelectItem value="GHS">GHS - Ghanaian Cedi</SelectItem>
                      <SelectItem value="ETB">ETB - Ethiopian Birr</SelectItem>
                    </SelectContent>
                  </Select>
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

      {/* Add/Edit Item Dialog */}
      <Dialog open={itemDialogOpen} onOpenChange={setItemDialogOpen}>
        <DialogContent className="max-w-6xl max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{editingItem ? 'Edit Item' : 'Add Item to Procurement Plan'}</DialogTitle>
            <DialogDescription>
              {editingItem ? 'Update item details' : 'Select an item from inventory or enter details manually'}
            </DialogDescription>
          </DialogHeader>

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
                    <Input
                      id="unitOfMeasure"
                      value={itemForm.unitOfMeasure || 'EA'}
                      onChange={(e) => setItemForm({ ...itemForm, unitOfMeasure: e.target.value })}
                      placeholder="EA"
                    />
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
    </div>
  );
}
