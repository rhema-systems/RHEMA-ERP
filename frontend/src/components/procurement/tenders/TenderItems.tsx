'use client';

import { useState } from 'react';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Plus, Trash2, Edit } from 'lucide-react';
import { toast } from 'sonner';
import { type TenderFormData } from '@/app/procurement/tenders/new/page';
import { type CreateTenderItemDto, addTenderItem, updateTenderItem, deleteTenderItem } from '@/services/tenderService';

interface TenderItemsProps {
  formData: TenderFormData;
  updateFormData: (data: Partial<TenderFormData>) => void;
  tenderId?: string | null;
}

const formatDate = (dateString: string | null | undefined) => {
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

export default function TenderItems({ formData, updateFormData, tenderId }: TenderItemsProps) {
  const [editingIndex, setEditingIndex] = useState<number | null>(null);
  const [saving, setSaving] = useState(false);
  const [currentItem, setCurrentItem] = useState<CreateTenderItemDto>({
    lineNumber: formData.items.length + 1,
    itemCode: '',
    description: '',
    quantity: 1,
    unitOfMeasure: '',
    specifications: '',
    requiredDeliveryDate: '',
    deliveryLocation: '',
  });

  const handleAddItem = async () => {
    if (!currentItem.description.trim()) {
      toast.error('Lot description is required');
      return;
    }

    if (currentItem.quantity <= 0) {
      toast.error('Quantity must be greater than 0');
      return;
    }

    try {
      setSaving(true);

      // Prepare item data - convert empty strings to null for optional date fields
      const itemToSave = {
        ...currentItem,
        requiredDeliveryDate: currentItem.requiredDeliveryDate?.trim() || null,
      };

      console.log('TenderItems - handleAddItem called');
      console.log('TenderItems - tenderId:', tenderId);
      console.log('TenderItems - itemToSave:', itemToSave);

      if (tenderId) {
        // Tender exists - use API to add/update items
        console.log('TenderItems - Tender exists, saving via API');
        if (editingIndex !== null) {
          // Update existing item via API
          const existingItem = formData.items[editingIndex] as any;
          if (existingItem.id) {
            console.log('TenderItems - Updating existing item:', existingItem.id);
            await updateTenderItem(tenderId, existingItem.id, itemToSave);
            // Update local state
            const updatedItems = [...formData.items];
            updatedItems[editingIndex] = { ...itemToSave, id: existingItem.id };
            updateFormData({ items: updatedItems });
            toast.success('Lot updated successfully');
          }
        } else {
          // Add new lot via API
          console.log('TenderItems - Adding new lot via API');
          const newItem = await addTenderItem(tenderId, itemToSave);
          console.log('TenderItems - Lot saved with ID:', newItem.id);
          updateFormData({ items: [...formData.items, newItem] });
          toast.success('Lot added successfully');
        }
      } else {
        // No tender yet - just update local state (will be saved when tender is created)
        console.log('TenderItems - No tender yet, saving to local state only');
        if (editingIndex !== null) {
          const updatedItems = [...formData.items];
          updatedItems[editingIndex] = itemToSave;
          updateFormData({ items: updatedItems });
          toast.success('Item updated successfully');
        } else {
          updateFormData({ items: [...formData.items, itemToSave] });
          toast.success('Item added successfully (will be saved when you save the tender)');
        }
      }

      // Reset form - calculate next line number
      // If we were editing, the count stays the same, so next line is count + 1
      // If we were adding, the count increased by 1, so next line is (count + 1) + 1 = count + 2
      const wasEditing = editingIndex !== null;
      const currentCount = formData.items.length;
      const nextLineNumber = wasEditing ? currentCount + 1 : currentCount + 2;

      setCurrentItem({
        lineNumber: nextLineNumber,
        itemCode: '',
        description: '',
        quantity: 1,
        unitOfMeasure: '',
        specifications: '',
        requiredDeliveryDate: '',
        deliveryLocation: '',
      });
      setEditingIndex(null);
    } catch (error: any) {
      console.error('Error saving item:', error);
      toast.error(error?.message || 'Failed to save item');
    } finally {
      setSaving(false);
    }
  };

  const handleEditItem = (index: number) => {
    setCurrentItem(formData.items[index]);
    setEditingIndex(index);
  };

  const handleDeleteItem = async (index: number) => {
    try {
      setSaving(true);
      const itemToDelete = formData.items[index] as any;

      if (tenderId && itemToDelete.id) {
        // Delete via API
        await deleteTenderItem(tenderId, itemToDelete.id);
      }

      // Update local state
      const updatedItems = formData.items.filter((_, i) => i !== index);
      // Renumber items
      const renumberedItems = updatedItems.map((item, i) => ({ ...item, lineNumber: i + 1 }));
      updateFormData({ items: renumberedItems });
      toast.success('Item deleted successfully');
    } catch (error: any) {
      console.error('Error deleting item:', error);
      toast.error(error?.message || 'Failed to delete item');
    } finally {
      setSaving(false);
    }
  };

  const handleCancelEdit = () => {
    setCurrentItem({
      lineNumber: formData.items.length + 1,
      itemCode: '',
      description: '',
      quantity: 1,
      unitOfMeasure: '',
      specifications: '',
      requiredDeliveryDate: '',
      deliveryLocation: '',
    });
    setEditingIndex(null);
  };

  return (
    <div className="space-y-6">
      {/* Lot Form */}
      <div className="border rounded-lg p-4 bg-gray-50">
        <h3 className="text-lg font-semibold mb-4">
          {editingIndex !== null ? 'Edit Lot' : 'Add New Lot'}
        </h3>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label htmlFor="itemCode">Lot Code</Label>
            <Input
              id="itemCode"
              value={currentItem.itemCode || ''}
              onChange={(e) => setCurrentItem({ ...currentItem, itemCode: e.target.value })}
              placeholder="Enter lot code"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="quantity">
              Quantity <span className="text-red-500">*</span>
            </Label>
            <Input
              id="quantity"
              type="number"
              value={currentItem.quantity}
              onChange={(e) => setCurrentItem({ ...currentItem, quantity: parseFloat(e.target.value) || 0 })}
              placeholder="Enter quantity"
              min="0"
              step="0.01"
            />
          </div>

          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="description">
              Description <span className="text-red-500">*</span>
            </Label>
            <Textarea
              id="description"
              value={currentItem.description}
              onChange={(e) => setCurrentItem({ ...currentItem, description: e.target.value })}
              placeholder="Enter lot description"
              rows={2}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="unitOfMeasure">Unit of Measure</Label>
            <Input
              id="unitOfMeasure"
              value={currentItem.unitOfMeasure || ''}
              onChange={(e) => setCurrentItem({ ...currentItem, unitOfMeasure: e.target.value })}
              placeholder="e.g., pcs, kg, m"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="requiredDeliveryDate">Required Delivery Date</Label>
            <Input
              id="requiredDeliveryDate"
              type="date"
              value={currentItem.requiredDeliveryDate || ''}
              onChange={(e) => setCurrentItem({ ...currentItem, requiredDeliveryDate: e.target.value })}
            />
          </div>

          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="deliveryLocation">Delivery Location</Label>
            <Input
              id="deliveryLocation"
              value={currentItem.deliveryLocation || ''}
              onChange={(e) => setCurrentItem({ ...currentItem, deliveryLocation: e.target.value })}
              placeholder="Enter delivery location"
            />
          </div>

          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="specifications">Specifications</Label>
            <Textarea
              id="specifications"
              value={currentItem.specifications || ''}
              onChange={(e) => setCurrentItem({ ...currentItem, specifications: e.target.value })}
              placeholder="Enter item specifications"
              rows={3}
            />
          </div>
        </div>

        <div className="flex gap-2 mt-4">
          <Button onClick={handleAddItem} disabled={saving}>
            {saving ? (
              'Saving...'
            ) : editingIndex !== null ? (
              <>
                <Edit className="h-4 w-4 mr-2" />
                Update Lot
              </>
            ) : (
              <>
                <Plus className="h-4 w-4 mr-2" />
                Add Lot
              </>
            )}
          </Button>
          {editingIndex !== null && (
            <Button variant="outline" onClick={handleCancelEdit}>
              Cancel
            </Button>
          )}
        </div>
      </div>

      {/* Lots List */}
      <div>
        <h3 className="text-lg font-semibold mb-4">Tender Lots ({formData.items.length})</h3>
        {formData.items.length === 0 ? (
          <div className="text-center py-8 text-gray-500 border rounded-lg">
            No lots added yet. Add at least one lot to continue.
          </div>
        ) : (
          <div className="border rounded-lg overflow-hidden">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-16">#</TableHead>
                  <TableHead>Lot Code</TableHead>
                  <TableHead>Description</TableHead>
                  <TableHead>Quantity</TableHead>
                  <TableHead>Unit</TableHead>
                  <TableHead>Delivery Date</TableHead>
                  <TableHead className="w-24">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {formData.items.map((item, index) => (
                  <TableRow key={index}>
                    <TableCell>{item.lineNumber}</TableCell>
                    <TableCell>{item.itemCode || '-'}</TableCell>
                    <TableCell>
                      <div className="max-w-xs">
                        <div className="font-medium">{item.description}</div>
                        {item.specifications && (
                          <div className="text-sm text-gray-500 truncate">{item.specifications}</div>
                        )}
                      </div>
                    </TableCell>
                    <TableCell>{item.quantity}</TableCell>
                    <TableCell>{item.unitOfMeasure || '-'}</TableCell>
                    <TableCell>{formatDate(item.requiredDeliveryDate)}</TableCell>
                    <TableCell>
                      <div className="flex gap-2">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleEditItem(index)}
                        >
                          <Edit className="h-4 w-4" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleDeleteItem(index)}
                        >
                          <Trash2 className="h-4 w-4 text-red-500" />
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </div>
    </div>
  );
}
