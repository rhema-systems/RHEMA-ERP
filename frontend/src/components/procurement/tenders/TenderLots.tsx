'use client';

import { useState } from 'react';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Plus, Trash2, Edit, ChevronDown, ChevronRight, Package, Box } from 'lucide-react';
import { toast } from 'sonner';
import { type TenderFormData, type TenderLotFormData, type TenderLotItemFormData } from '@/app/procurement/tenders/new/page';
import { tenderService, type CreateTenderLotDto, type CreateTenderItemDto } from '@/services/tenderService';

interface TenderLotsProps {
  formData: TenderFormData;
  updateFormData: (data: Partial<TenderFormData>) => void;
  tenderId?: string | null;
}

const formatDate = (dateString: string | undefined) => {
  if (!dateString) return '-';
  try {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric'
    });
  } catch {
    return dateString;
  }
};

// Empty LOT template
const emptyLot: TenderLotFormData = {
  lotNumber: 1,
  lotCode: '',
  title: '',
  description: '',
  estimatedValue: undefined,
  currency: 'USD',
  requiredDeliveryDate: '',
  deliveryLocation: '',
  specifications: '',
  notes: '',
  displayOrder: 0,
  items: [],
};

// Empty item template
const emptyItem: TenderLotItemFormData = {
  lineNumber: 1,
  itemCode: '',
  description: '',
  quantity: 1,
  unitOfMeasure: '',
  specifications: '',
  requiredDeliveryDate: '',
  deliveryLocation: '',
};

export default function TenderLots({ formData, updateFormData, tenderId }: TenderLotsProps) {
  const [expandedLots, setExpandedLots] = useState<Set<number>>(new Set([0]));
  const [editingLotIndex, setEditingLotIndex] = useState<number | null>(null);
  const [editingItemIndex, setEditingItemIndex] = useState<{ lotIndex: number; itemIndex: number } | null>(null);
  const [saving, setSaving] = useState(false);
  
  // Current LOT being edited/added
  const [currentLot, setCurrentLot] = useState<TenderLotFormData>({
    ...emptyLot,
    lotNumber: formData.lots.length + 1,
    lotCode: `LOT-${String(formData.lots.length + 1).padStart(3, '0')}`,
  });
  
  // Current item being edited/added (for a specific LOT)
  const [currentItem, setCurrentItem] = useState<TenderLotItemFormData>({ ...emptyItem });
  const [addingItemToLotIndex, setAddingItemToLotIndex] = useState<number | null>(null);

  const toggleLotExpanded = (index: number) => {
    const newExpanded = new Set(expandedLots);
    if (newExpanded.has(index)) {
      newExpanded.delete(index);
    } else {
      newExpanded.add(index);
    }
    setExpandedLots(newExpanded);
  };

  // Generate next LOT code
  const generateLotCode = () => {
    const nextNumber = formData.lots.length + 1;
    return `LOT-${String(nextNumber).padStart(3, '0')}`;
  };

  // Add or update a LOT
  const handleSaveLot = async () => {
    if (!currentLot.lotCode.trim()) {
      toast.error('LOT Code is required');
      return;
    }
    if (!currentLot.title.trim()) {
      toast.error('LOT Title is required');
      return;
    }

    try {
      setSaving(true);

      if (tenderId) {
        // Save to backend
        const lotData: CreateTenderLotDto = {
          lotNumber: currentLot.lotNumber,
          lotCode: currentLot.lotCode,
          title: currentLot.title,
          description: currentLot.description,
          estimatedValue: currentLot.estimatedValue,
          currency: currentLot.currency,
          requiredDeliveryDate: currentLot.requiredDeliveryDate || undefined,
          deliveryLocation: currentLot.deliveryLocation,
          specifications: currentLot.specifications,
          notes: currentLot.notes,
          displayOrder: currentLot.displayOrder,
        };

        if (editingLotIndex !== null && currentLot.id) {
          // Update existing LOT
          await tenderService.updateTenderLot(currentLot.id, lotData);
          const updatedLots = [...formData.lots];
          updatedLots[editingLotIndex] = { ...currentLot };
          updateFormData({ lots: updatedLots });
          toast.success('LOT updated successfully');
        } else {
          // Add new LOT
          const savedLot = await tenderService.addTenderLot(tenderId, lotData);
          const newLot: TenderLotFormData = {
            ...currentLot,
            id: savedLot.id,
          };
          updateFormData({ lots: [...formData.lots, newLot] });
          toast.success('LOT added successfully');
        }
      } else {
        // No tender yet - just update local state
        if (editingLotIndex !== null) {
          const updatedLots = [...formData.lots];
          updatedLots[editingLotIndex] = { ...currentLot };
          updateFormData({ lots: updatedLots });
          toast.success('LOT updated');
        } else {
          updateFormData({ lots: [...formData.lots, { ...currentLot }] });
          toast.success('LOT added (will be saved when tender is created)');
        }
      }

      // Reset form
      resetLotForm();
    } catch (error: any) {
      console.error('Error saving LOT:', error);
      toast.error(error?.message || 'Failed to save LOT');
    } finally {
      setSaving(false);
    }
  };

  const resetLotForm = () => {
    const nextNumber = formData.lots.length + (editingLotIndex !== null ? 0 : 1);
    setCurrentLot({
      ...emptyLot,
      lotNumber: nextNumber + 1,
      lotCode: `LOT-${String(nextNumber + 1).padStart(3, '0')}`,
    });
    setEditingLotIndex(null);
  };

  const handleEditLot = (index: number) => {
    setCurrentLot({ ...formData.lots[index] });
    setEditingLotIndex(index);
    setExpandedLots(new Set([index]));
  };

  const handleDeleteLot = async (index: number) => {
    try {
      setSaving(true);
      const lotToDelete = formData.lots[index];

      if (tenderId && lotToDelete.id) {
        await tenderService.deleteTenderLot(lotToDelete.id);
      }

      const updatedLots = formData.lots.filter((_, i) => i !== index);
      // Renumber lots
      const renumberedLots = updatedLots.map((lot, i) => ({
        ...lot,
        lotNumber: i + 1,
        displayOrder: i,
      }));
      updateFormData({ lots: renumberedLots });
      toast.success('LOT deleted successfully');
    } catch (error: any) {
      console.error('Error deleting LOT:', error);
      toast.error(error?.message || 'Failed to delete LOT');
    } finally {
      setSaving(false);
    }
  };

  // Item management within a LOT
  const handleStartAddItem = (lotIndex: number) => {
    const lot = formData.lots[lotIndex];
    setAddingItemToLotIndex(lotIndex);
    setCurrentItem({
      ...emptyItem,
      lineNumber: (lot.items?.length || 0) + 1,
    });
    setEditingItemIndex(null);
    setExpandedLots(new Set([...expandedLots, lotIndex]));
  };

  const handleEditItem = (lotIndex: number, itemIndex: number) => {
    setAddingItemToLotIndex(lotIndex);
    setCurrentItem({ ...formData.lots[lotIndex].items[itemIndex] });
    setEditingItemIndex({ lotIndex, itemIndex });
  };

  const handleSaveItem = async () => {
    if (addingItemToLotIndex === null) return;

    if (!currentItem.description.trim()) {
      toast.error('Item description is required');
      return;
    }
    if (currentItem.quantity <= 0) {
      toast.error('Quantity must be greater than 0');
      return;
    }

    try {
      setSaving(true);
      const lot = formData.lots[addingItemToLotIndex];

      if (tenderId && lot.id) {
        // Save item to backend
        const itemData: CreateTenderItemDto = {
          lineNumber: currentItem.lineNumber,
          lotId: lot.id,
          itemCode: currentItem.itemCode,
          description: currentItem.description,
          quantity: currentItem.quantity,
          unitOfMeasure: currentItem.unitOfMeasure,
          specifications: currentItem.specifications,
          requiredDeliveryDate: currentItem.requiredDeliveryDate || null,
          deliveryLocation: currentItem.deliveryLocation,
        };

        if (editingItemIndex !== null && currentItem.id) {
          // Update existing item
          await tenderService.updateTenderItem(tenderId, currentItem.id, itemData);
          const updatedLots = [...formData.lots];
          updatedLots[addingItemToLotIndex].items[editingItemIndex.itemIndex] = { ...currentItem };
          updateFormData({ lots: updatedLots });
          toast.success('Item updated successfully');
        } else {
          // Add new item
          const savedItem = await tenderService.addTenderItem(tenderId, itemData);
          const updatedLots = [...formData.lots];
          updatedLots[addingItemToLotIndex].items = [
            ...updatedLots[addingItemToLotIndex].items,
            { ...currentItem, id: savedItem.id },
          ];
          updateFormData({ lots: updatedLots });
          toast.success('Item added successfully');
        }
      } else {
        // No tender yet - just update local state
        const updatedLots = [...formData.lots];
        if (editingItemIndex !== null) {
          updatedLots[addingItemToLotIndex].items[editingItemIndex.itemIndex] = { ...currentItem };
          toast.success('Item updated');
        } else {
          updatedLots[addingItemToLotIndex].items = [
            ...updatedLots[addingItemToLotIndex].items,
            { ...currentItem },
          ];
          toast.success('Item added');
        }
        updateFormData({ lots: updatedLots });
      }

      // Reset
      setAddingItemToLotIndex(null);
      setCurrentItem({ ...emptyItem });
      setEditingItemIndex(null);
    } catch (error: any) {
      console.error('Error saving item:', error);
      toast.error(error?.message || 'Failed to save item');
    } finally {
      setSaving(false);
    }
  };

  const handleDeleteItem = async (lotIndex: number, itemIndex: number) => {
    try {
      setSaving(true);
      const item = formData.lots[lotIndex].items[itemIndex];

      if (tenderId && item.id) {
        await tenderService.deleteTenderItem(tenderId, item.id);
      }

      const updatedLots = [...formData.lots];
      updatedLots[lotIndex].items = updatedLots[lotIndex].items.filter((_, i) => i !== itemIndex);
      // Renumber items
      updatedLots[lotIndex].items = updatedLots[lotIndex].items.map((item, i) => ({
        ...item,
        lineNumber: i + 1,
      }));
      updateFormData({ lots: updatedLots });
      toast.success('Item deleted successfully');
    } catch (error: any) {
      console.error('Error deleting item:', error);
      toast.error(error?.message || 'Failed to delete item');
    } finally {
      setSaving(false);
    }
  };

  const handleCancelItemEdit = () => {
    setAddingItemToLotIndex(null);
    setCurrentItem({ ...emptyItem });
    setEditingItemIndex(null);
  };

  return (
    <div className="space-y-6">
      {/* LOT Form */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Package className="h-5 w-5" />
            {editingLotIndex !== null ? 'Edit LOT' : 'Add New LOT'}
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="lotCode">LOT Code <span className="text-red-500">*</span></Label>
              <Input
                id="lotCode"
                value={currentLot.lotCode}
                onChange={(e) => setCurrentLot({ ...currentLot, lotCode: e.target.value })}
                placeholder="e.g., LOT-001"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="lotTitle">LOT Title <span className="text-red-500">*</span></Label>
              <Input
                id="lotTitle"
                value={currentLot.title}
                onChange={(e) => setCurrentLot({ ...currentLot, title: e.target.value })}
                placeholder="Enter LOT title"
              />
            </div>

            <div className="space-y-2 md:col-span-2">
              <Label htmlFor="lotDescription">Description</Label>
              <Textarea
                id="lotDescription"
                value={currentLot.description || ''}
                onChange={(e) => setCurrentLot({ ...currentLot, description: e.target.value })}
                placeholder="Enter LOT description"
                rows={2}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="lotEstimatedValue">Estimated Value</Label>
              <Input
                id="lotEstimatedValue"
                type="number"
                value={currentLot.estimatedValue || ''}
                onChange={(e) => setCurrentLot({ ...currentLot, estimatedValue: parseFloat(e.target.value) || undefined })}
                placeholder="Enter estimated value"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="lotDeliveryDate">Required Delivery Date</Label>
              <Input
                id="lotDeliveryDate"
                type="date"
                value={currentLot.requiredDeliveryDate || ''}
                onChange={(e) => setCurrentLot({ ...currentLot, requiredDeliveryDate: e.target.value })}
              />
            </div>

            <div className="space-y-2 md:col-span-2">
              <Label htmlFor="lotDeliveryLocation">Delivery Location</Label>
              <Input
                id="lotDeliveryLocation"
                value={currentLot.deliveryLocation || ''}
                onChange={(e) => setCurrentLot({ ...currentLot, deliveryLocation: e.target.value })}
                placeholder="Enter delivery location"
              />
            </div>
          </div>

          <div className="flex gap-2 mt-4">
            <Button onClick={handleSaveLot} disabled={saving}>
              {saving ? 'Saving...' : editingLotIndex !== null ? (
                <>
                  <Edit className="h-4 w-4 mr-2" />
                  Update LOT
                </>
              ) : (
                <>
                  <Plus className="h-4 w-4 mr-2" />
                  Add LOT
                </>
              )}
            </Button>
            {editingLotIndex !== null && (
              <Button variant="outline" onClick={resetLotForm}>
                Cancel
              </Button>
            )}
          </div>
        </CardContent>
      </Card>

      {/* LOTs List */}
      <div>
        <h3 className="text-lg font-semibold mb-4">Tender LOTs ({formData.lots.length})</h3>
        {formData.lots.length === 0 ? (
          <div className="text-center py-8 text-gray-500 border rounded-lg">
            <Package className="h-12 w-12 mx-auto mb-2 text-gray-300" />
            <p>No LOTs added yet. Add at least one LOT to continue.</p>
            <p className="text-sm mt-1">Each LOT must contain one or more items that bidders will bid on together.</p>
          </div>
        ) : (
          <div className="space-y-4">
            {formData.lots.map((lot, lotIndex) => (
              <Card key={lotIndex}>
                <CardHeader className="pb-3">
                  <div className="flex items-center justify-between">
                    <button
                      type="button"
                      className="flex items-center gap-2 hover:text-primary cursor-pointer"
                      onClick={() => toggleLotExpanded(lotIndex)}
                    >
                      {expandedLots.has(lotIndex) ? (
                        <ChevronDown className="h-4 w-4" />
                      ) : (
                        <ChevronRight className="h-4 w-4" />
                      )}
                      <CardTitle className="text-base">
                        {lot.lotCode}: {lot.title}
                      </CardTitle>
                      <span className="text-sm text-muted-foreground ml-2">
                        ({lot.items?.length || 0} items)
                      </span>
                    </button>
                    <div className="flex gap-2">
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => handleStartAddItem(lotIndex)}
                      >
                        <Plus className="h-4 w-4 mr-1" />
                        Add Item
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => handleEditLot(lotIndex)}
                      >
                        <Edit className="h-4 w-4" />
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => handleDeleteLot(lotIndex)}
                      >
                        <Trash2 className="h-4 w-4 text-red-500" />
                      </Button>
                    </div>
                  </div>
                  {lot.description && (
                    <p className="text-sm text-muted-foreground mt-1">{lot.description}</p>
                  )}
                </CardHeader>
                {expandedLots.has(lotIndex) && (
                  <CardContent className="pt-0">
                      {/* Item Form (shown when adding/editing item for this LOT) */}
                      {addingItemToLotIndex === lotIndex && (
                        <div className="border rounded-lg p-4 bg-blue-50 mb-4">
                          <h4 className="font-medium mb-3 flex items-center gap-2">
                            <Box className="h-4 w-4" />
                            {editingItemIndex !== null ? 'Edit Item' : 'Add Item to LOT'}
                          </h4>
                          <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                            <div className="space-y-1">
                              <Label htmlFor="itemCode" className="text-sm">Item Code</Label>
                              <Input
                                id="itemCode"
                                value={currentItem.itemCode || ''}
                                onChange={(e) => setCurrentItem({ ...currentItem, itemCode: e.target.value })}
                                placeholder="Optional"
                                className="h-9"
                              />
                            </div>
                            <div className="space-y-1 md:col-span-2">
                              <Label htmlFor="itemDescription" className="text-sm">
                                Description <span className="text-red-500">*</span>
                              </Label>
                              <Input
                                id="itemDescription"
                                value={currentItem.description}
                                onChange={(e) => setCurrentItem({ ...currentItem, description: e.target.value })}
                                placeholder="Enter item description"
                                className="h-9"
                              />
                            </div>
                            <div className="space-y-1">
                              <Label htmlFor="itemQuantity" className="text-sm">
                                Quantity <span className="text-red-500">*</span>
                              </Label>
                              <Input
                                id="itemQuantity"
                                type="number"
                                value={currentItem.quantity}
                                onChange={(e) => setCurrentItem({ ...currentItem, quantity: parseFloat(e.target.value) || 0 })}
                                min="0"
                                step="0.01"
                                className="h-9"
                              />
                            </div>
                            <div className="space-y-1">
                              <Label htmlFor="itemUnit" className="text-sm">Unit</Label>
                              <Input
                                id="itemUnit"
                                value={currentItem.unitOfMeasure || ''}
                                onChange={(e) => setCurrentItem({ ...currentItem, unitOfMeasure: e.target.value })}
                                placeholder="e.g., pcs, kg"
                                className="h-9"
                              />
                            </div>
                            <div className="space-y-1">
                              <Label htmlFor="itemDeliveryDate" className="text-sm">Delivery Date</Label>
                              <Input
                                id="itemDeliveryDate"
                                type="date"
                                value={currentItem.requiredDeliveryDate || ''}
                                onChange={(e) => setCurrentItem({ ...currentItem, requiredDeliveryDate: e.target.value })}
                                className="h-9"
                              />
                            </div>
                          </div>
                          <div className="flex gap-2 mt-3">
                            <Button size="sm" onClick={handleSaveItem} disabled={saving}>
                              {saving ? 'Saving...' : editingItemIndex !== null ? 'Update Item' : 'Add Item'}
                            </Button>
                            <Button size="sm" variant="outline" onClick={handleCancelItemEdit}>
                              Cancel
                            </Button>
                          </div>
                        </div>
                      )}

                      {/* Items Table */}
                      {lot.items && lot.items.length > 0 ? (
                        <Table>
                          <TableHeader>
                            <TableRow>
                              <TableHead className="w-12">#</TableHead>
                              <TableHead className="w-24">Code</TableHead>
                              <TableHead>Description</TableHead>
                              <TableHead className="w-20">Qty</TableHead>
                              <TableHead className="w-20">Unit</TableHead>
                              <TableHead className="w-28">Delivery</TableHead>
                              <TableHead className="w-20">Actions</TableHead>
                            </TableRow>
                          </TableHeader>
                          <TableBody>
                            {lot.items.map((item, itemIndex) => (
                              <TableRow key={itemIndex}>
                                <TableCell>{item.lineNumber}</TableCell>
                                <TableCell>{item.itemCode || '-'}</TableCell>
                                <TableCell>
                                  <div className="max-w-xs truncate">{item.description}</div>
                                </TableCell>
                                <TableCell>{item.quantity}</TableCell>
                                <TableCell>{item.unitOfMeasure || '-'}</TableCell>
                                <TableCell>{formatDate(item.requiredDeliveryDate)}</TableCell>
                                <TableCell>
                                  <div className="flex gap-1">
                                    <Button
                                      variant="ghost"
                                      size="sm"
                                      className="h-7 w-7 p-0"
                                      onClick={() => handleEditItem(lotIndex, itemIndex)}
                                    >
                                      <Edit className="h-3 w-3" />
                                    </Button>
                                    <Button
                                      variant="ghost"
                                      size="sm"
                                      className="h-7 w-7 p-0"
                                      onClick={() => handleDeleteItem(lotIndex, itemIndex)}
                                    >
                                      <Trash2 className="h-3 w-3 text-red-500" />
                                    </Button>
                                  </div>
                                </TableCell>
                              </TableRow>
                            ))}
                          </TableBody>
                        </Table>
                      ) : (
                        <div className="text-center py-4 text-gray-500 text-sm">
                          <Box className="h-8 w-8 mx-auto mb-1 text-gray-300" />
                          No items in this LOT. Add at least one item.
                        </div>
                      )}
                    </CardContent>
                )}
              </Card>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
