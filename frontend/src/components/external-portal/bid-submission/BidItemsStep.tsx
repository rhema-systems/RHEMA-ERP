'use client';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Package } from 'lucide-react';
import { type TenderDetailDto } from '@/services/tenderService';
import { type CreateTenderBidDto, type CreateTenderBidItemDto } from '@/services/tenderBidService';

interface BidItemsStepProps {
  tender: TenderDetailDto;
  bidData: CreateTenderBidDto;
  updateBidData: (updates: Partial<CreateTenderBidDto>) => void;
  selectedLotIds?: string[];
}

export default function BidItemsStep({ tender, bidData, updateBidData, selectedLotIds }: BidItemsStepProps) {
  // Filter tender items based on selected lots (if provided)
  const displayItems = selectedLotIds && selectedLotIds.length > 0
    ? tender.items?.filter(item => selectedLotIds.includes(item.id)) || []
    : tender.items || [];
  const updateBidItem = (index: number, updates: Partial<CreateTenderBidItemDto>) => {
    const updatedItems = [...bidData.items];
    updatedItems[index] = { ...updatedItems[index], ...updates };
    updateBidData({ items: updatedItems });
  };

  const calculateItemTotal = (item: CreateTenderBidItemDto) => {
    return item.offeredQuantity * item.unitPrice;
  };

  const calculateTotalBidAmount = () => {
    return bidData.items.reduce((sum, item) => sum + calculateItemTotal(item), 0);
  };

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Package className="h-5 w-5" />
            Bid Lots & Pricing
          </CardTitle>
          <CardDescription>
            Enter your pricing and specifications for each tender lot
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-[50px]">#</TableHead>
                  <TableHead>Lot Description</TableHead>
                  <TableHead>Required Qty</TableHead>
                  <TableHead>Offered Qty</TableHead>
                  <TableHead>Unit Price</TableHead>
                  <TableHead>Total</TableHead>
                  <TableHead>Delivery Days</TableHead>
                  <TableHead>Brand/Model</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {displayItems.map((tenderItem, index) => {
                  // Find the corresponding bid item by tenderItemId
                  const bidItem = bidData.items.find(item => item.tenderItemId === tenderItem.id);
                  if (!bidItem) return null;

                  // Get the actual index in bidData.items for updates
                  const bidItemIndex = bidData.items.findIndex(item => item.tenderItemId === tenderItem.id);

                  return (
                    <TableRow key={tenderItem.id}>
                      <TableCell className="font-medium">{index + 1}</TableCell>
                      <TableCell>
                        <div>
                          <p className="font-medium">{tenderItem.description}</p>
                          <p className="text-xs text-gray-500">{tenderItem.specifications}</p>
                        </div>
                      </TableCell>
                      <TableCell>
                        {tenderItem.quantity} {tenderItem.unitOfMeasure || ''}
                      </TableCell>
                      <TableCell>
                        <Input
                          type="number"
                          min="0"
                          step="0.01"
                          value={bidItem.offeredQuantity}
                          onChange={(e) => updateBidItem(bidItemIndex, { offeredQuantity: parseFloat(e.target.value) || 0 })}
                          className="w-24"
                        />
                      </TableCell>
                      <TableCell>
                        <Input
                          type="number"
                          min="0"
                          step="0.01"
                          value={bidItem.unitPrice}
                          onChange={(e) => updateBidItem(bidItemIndex, { unitPrice: parseFloat(e.target.value) || 0 })}
                          className="w-32"
                          placeholder="0.00"
                        />
                      </TableCell>
                      <TableCell>
                        <span className="font-bold text-green-600">
                          {tender.currency} {calculateItemTotal(bidItem).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                        </span>
                      </TableCell>
                      <TableCell>
                        <Input
                          type="number"
                          min="0"
                          value={bidItem.deliveryDays || ''}
                          onChange={(e) => updateBidItem(bidItemIndex, { deliveryDays: parseInt(e.target.value) || undefined })}
                          className="w-20"
                          placeholder="Days"
                        />
                      </TableCell>
                      <TableCell>
                        <div className="space-y-2">
                          <Input
                            value={bidItem.brand || ''}
                            onChange={(e) => updateBidItem(bidItemIndex, { brand: e.target.value })}
                            placeholder="Brand"
                            className="w-32"
                          />
                          <Input
                            value={bidItem.model || ''}
                            onChange={(e) => updateBidItem(bidItemIndex, { model: e.target.value })}
                            placeholder="Model"
                            className="w-32"
                          />
                        </div>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </div>

          {/* Total */}
          <div className="mt-6 flex justify-end">
            <div className="bg-blue-50 border border-blue-200 rounded-lg p-6 min-w-[450px] max-w-[600px]">
              <div className="space-y-3">
                <div className="flex items-center justify-between text-sm text-gray-600">
                  <span>Total Items:</span>
                  <span className="font-medium">{bidData.items.length}</span>
                </div>
                <div className="border-t border-blue-200 pt-3 mt-2">
                  <div className="space-y-2">
                    <p className="text-base font-semibold text-gray-900">Total Bid Amount:</p>
                    <p className="text-3xl font-bold text-blue-600 break-words">
                      {tender.currency} {calculateTotalBidAmount().toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                    </p>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Additional Item Details */}
      <Card>
        <CardHeader>
          <CardTitle>Additional Item Specifications</CardTitle>
          <CardDescription>Provide detailed specifications for each item (optional)</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {displayItems.map((tenderItem, index) => {
            // Find the corresponding bid item by tenderItemId
            const bidItem = bidData.items.find(item => item.tenderItemId === tenderItem.id);
            if (!bidItem) return null;

            // Get the actual index in bidData.items for updates
            const bidItemIndex = bidData.items.findIndex(item => item.tenderItemId === tenderItem.id);

            return (
              <div key={tenderItem.id} className="border rounded-lg p-4">
                <Label className="font-medium mb-2 block">
                  Item {index + 1}: {tenderItem.description}
                </Label>
                <Textarea
                  value={bidItem.technicalDetails || ''}
                  onChange={(e) => updateBidItem(bidItemIndex, { technicalDetails: e.target.value })}
                  placeholder="Enter detailed technical specifications, certifications, compliance information, etc."
                  rows={3}
                />
              </div>
            );
          })}
        </CardContent>
      </Card>
    </div>
  );
}

