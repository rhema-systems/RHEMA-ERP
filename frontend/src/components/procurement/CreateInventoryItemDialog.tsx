'use client';

import { useState, useEffect } from 'react';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { useToast } from '@/hooks/use-toast';
import { Loader2, Package } from 'lucide-react';

interface CreateInventoryItemDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  itemName: string;
  businessPartnerId?: string;
  businessPartnerName?: string;
  autoCreateSupplierItem?: boolean;
  onItemCreated: (itemId: string, supplierItemCreated: boolean) => void;
}

export function CreateInventoryItemDialog({
  open,
  onOpenChange,
  itemName,
  businessPartnerId,
  businessPartnerName,
  autoCreateSupplierItem = false,
  onItemCreated,
}: CreateInventoryItemDialogProps) {
  const { toast } = useToast();
  const [loading, setLoading] = useState(false);
  const [createSupplierItem, setCreateSupplierItem] = useState(autoCreateSupplierItem);
  const [formData, setFormData] = useState({
    name: itemName,
    description: '',
    itemType: 'Inventory',
    valuationMethod: 'FIFO',
    supplierItemCode: '',
    supplierPrice: '',
  });

  useEffect(() => {
    setFormData(prev => ({ ...prev, name: itemName }));
  }, [itemName]);

  useEffect(() => {
    setCreateSupplierItem(autoCreateSupplierItem);
  }, [autoCreateSupplierItem]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!formData.name.trim()) {
      toast({
        title: 'Validation Error',
        description: 'Item name is required',
        variant: 'destructive',
      });
      return;
    }

    try {
      setLoading(true);

      // TODO: Call inventory item creation API
      // For now, we'll simulate the creation
      const newItemId = crypto.randomUUID();
      
      // TODO: If createSupplierItem is true and businessPartnerId is provided,
      // also create supplier item catalog entry
      
      toast({
        title: 'Success',
        description: `Inventory item "${formData.name}" created successfully`,
      });

      onItemCreated(newItemId, createSupplierItem && !!businessPartnerId);
      onOpenChange(false);
      
      // Reset form
      setFormData({
        name: '',
        description: '',
        itemType: 'Inventory',
        valuationMethod: 'FIFO',
        supplierItemCode: '',
        supplierPrice: '',
      });
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error.message || 'Failed to create inventory item',
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Package className="h-5 w-5" />
            Create Inventory Item
          </DialogTitle>
          <DialogDescription>
            Create a new inventory item for use in this purchase order
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="itemName">Item Name *</Label>
            <Input
              id="itemName"
              value={formData.name}
              onChange={(e) => setFormData({ ...formData, name: e.target.value })}
              placeholder="Enter item name"
              required
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="description">Description</Label>
            <Textarea
              id="description"
              value={formData.description}
              onChange={(e) => setFormData({ ...formData, description: e.target.value })}
              placeholder="Enter item description"
              rows={3}
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="itemType">Item Type</Label>
              <Select
                value={formData.itemType}
                onValueChange={(value) => setFormData({ ...formData, itemType: value })}
              >
                <SelectTrigger id="itemType">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Inventory">Inventory</SelectItem>
                  <SelectItem value="Service">Service</SelectItem>
                  <SelectItem value="NonInventory">Non-Inventory</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label htmlFor="valuationMethod">Valuation Method</Label>
              <Select
                value={formData.valuationMethod}
                onValueChange={(value) => setFormData({ ...formData, valuationMethod: value })}
              >
                <SelectTrigger id="valuationMethod">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="FIFO">FIFO (First In, First Out)</SelectItem>
                  <SelectItem value="WAC">WAC (Weighted Average Cost)</SelectItem>
                  <SelectItem value="Standard">Standard Cost</SelectItem>
                  <SelectItem value="LIFO">LIFO (Last In, First Out)</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>

          {businessPartnerId && (
            <>
              <div className="border-t pt-4 mt-4">
                <div className="flex items-center justify-between space-x-4 mb-4">
                  <div className="flex-1 space-y-1">
                    <Label htmlFor="createSupplierItem">Also create supplier item</Label>
                    <p className="text-sm text-muted-foreground">
                      Create supplier catalog entry for {businessPartnerName || 'this supplier'}
                    </p>
                  </div>
                  <Switch
                    id="createSupplierItem"
                    checked={createSupplierItem}
                    onCheckedChange={setCreateSupplierItem}
                  />
                </div>

                {createSupplierItem && (
                  <div className="grid grid-cols-2 gap-4 pl-4 border-l-2 border-blue-200">
                    <div className="space-y-2">
                      <Label htmlFor="supplierItemCode">Supplier Item Code</Label>
                      <Input
                        id="supplierItemCode"
                        value={formData.supplierItemCode}
                        onChange={(e) => setFormData({ ...formData, supplierItemCode: e.target.value })}
                        placeholder="Supplier's item code"
                      />
                    </div>

                    <div className="space-y-2">
                      <Label htmlFor="supplierPrice">Supplier Price</Label>
                      <Input
                        id="supplierPrice"
                        type="number"
                        step="0.01"
                        min="0"
                        value={formData.supplierPrice}
                        onChange={(e) => setFormData({ ...formData, supplierPrice: e.target.value })}
                        placeholder="0.00"
                      />
                    </div>
                  </div>
                )}
              </div>
            </>
          )}

          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => onOpenChange(false)}
              disabled={loading}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={loading}>
              {loading ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Creating...
                </>
              ) : (
                'Create Item'
              )}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
