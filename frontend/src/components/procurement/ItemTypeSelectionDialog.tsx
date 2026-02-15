'use client';

import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Package, FileText } from 'lucide-react';

interface ItemTypeSelectionDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  itemName: string;
  onCreateInventoryItem: () => void;
  onUseNonInventoryItem: () => void;
}

export function ItemTypeSelectionDialog({
  open,
  onOpenChange,
  itemName,
  onCreateInventoryItem,
  onUseNonInventoryItem,
}: ItemTypeSelectionDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Item Not Found</DialogTitle>
          <DialogDescription>
            The item &quot;{itemName}&quot; was not found in inventory.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-3 py-4">
          <p className="text-sm font-medium">How would you like to proceed?</p>

          <Button
            variant="outline"
            className="w-full h-auto py-4 px-4 flex flex-col items-start gap-2 hover:bg-blue-50 hover:border-blue-300"
            onClick={() => {
              onCreateInventoryItem();
              onOpenChange(false);
            }}
          >
            <div className="flex items-center gap-2">
              <Package className="h-5 w-5 text-blue-600" />
              <span className="font-semibold">Create as Inventory Item</span>
            </div>
            <span className="text-xs text-muted-foreground text-left">
              Track this item in inventory system with stock levels and valuation
            </span>
          </Button>

          <Button
            variant="outline"
            className="w-full h-auto py-4 px-4 flex flex-col items-start gap-2 hover:bg-gray-50 hover:border-gray-300"
            onClick={() => {
              onUseNonInventoryItem();
              onOpenChange(false);
            }}
          >
            <div className="flex items-center gap-2">
              <FileText className="h-5 w-5 text-gray-600" />
              <span className="font-semibold">Use as Non-Inventory Item</span>
            </div>
            <span className="text-xs text-muted-foreground text-left">
              One-time purchase without inventory tracking (no stock management)
            </span>
          </Button>
        </div>

        <div className="flex justify-end pt-2">
          <Button variant="ghost" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
