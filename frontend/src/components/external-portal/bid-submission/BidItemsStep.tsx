'use client';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
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
  // Get selected lots
  const selectedLots = selectedLotIds && selectedLotIds.length > 0
    ? tender.lots?.filter(lot => selectedLotIds.includes(lot.id)) || []
    : tender.lots || [];

  const updateBidItem = (bidItemIndex: number, updates: Partial<CreateTenderBidItemDto>) => {
    const updatedItems = [...bidData.items];
    updatedItems[bidItemIndex] = { ...updatedItems[bidItemIndex], ...updates };
    updateBidData({ items: updatedItems });
  };

  const calculateItemTotal = (item: CreateTenderBidItemDto) => {
    return item.offeredQuantity * item.unitPrice;
  };

  const calculateTotalBidAmount = () => {
    return bidData.items.reduce((sum, item) => sum + calculateItemTotal(item), 0);
  };

  const calculateLotTotal = (lotItems: typeof selectedLots[0]['items']) => {
    return (lotItems || []).reduce((sum, tenderItem) => {
      const bidItem = bidData.items.find(item => item.tenderItemId === tenderItem.id);
      return sum + (bidItem ? calculateItemTotal(bidItem) : 0);
    }, 0);
  };

  return (
    <div className="space-y-6">
      {/* Lots with Items */}
      {selectedLots.map((lot) => (
        <Card key={lot.id}>
          <CardHeader className="bg-gray-50 border-b">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-3">
                <Package className="h-5 w-5 text-blue-600" />
                <div>
                  <CardTitle className="text-lg">
                    <Badge variant="outline" className="mr-2">{lot.lotCode}</Badge>
                    {lot.title}
                  </CardTitle>
                  {lot.description && (
                    <CardDescription className="mt-1">{lot.description}</CardDescription>
                  )}
                </div>
              </div>
              <div className="text-right">
                <div className="text-sm text-gray-500">{lot.items?.length || 0} item(s)</div>
                <div className="font-bold text-green-600">
                  {tender.currency} {calculateLotTotal(lot.items).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                </div>
              </div>
            </div>
          </CardHeader>
          <CardContent className="pt-4">
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-[50px]">#</TableHead>
                    <TableHead>Item Description</TableHead>
                    <TableHead>Required Qty</TableHead>
                    <TableHead>Offered Qty</TableHead>
                    <TableHead>Unit Price</TableHead>
                    <TableHead>Total</TableHead>
                    <TableHead>Delivery Days</TableHead>
                    <TableHead>Brand/Model</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {(lot.items || []).map((tenderItem, index) => {
                    const bidItem = bidData.items.find(item => item.tenderItemId === tenderItem.id);
                    if (!bidItem) return null;
                    const bidItemIndex = bidData.items.findIndex(item => item.tenderItemId === tenderItem.id);

                    return (
                      <TableRow key={tenderItem.id}>
                        <TableCell className="font-medium">{index + 1}</TableCell>
                        <TableCell>
                          <div>
                            <p className="font-medium">{tenderItem.description}</p>
                            {tenderItem.specifications && (
                              <p className="text-xs text-gray-500">{tenderItem.specifications}</p>
                            )}
                          </div>
                        </TableCell>
                        <TableCell>{tenderItem.quantity} {tenderItem.unitOfMeasure || ''}</TableCell>
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
          </CardContent>
        </Card>
      ))}

      {/* Total Summary */}
      <Card>
        <CardContent className="pt-6">
          <div className="flex justify-end">
            <div className="bg-blue-50 border border-blue-200 rounded-lg p-6 min-w-[450px] max-w-[600px]">
              <div className="space-y-3">
                <div className="flex items-center justify-between text-sm text-gray-600">
                  <span>Total Lots:</span>
                  <span className="font-medium">{selectedLots.length}</span>
                </div>
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
          {selectedLots.map((lot) => (
            <div key={lot.id} className="space-y-3">
              <h4 className="font-medium text-sm text-gray-600 flex items-center gap-2">
                <Badge variant="outline">{lot.lotCode}</Badge>
                {lot.title}
              </h4>
              {(lot.items || []).map((tenderItem, index) => {
                const bidItem = bidData.items.find(item => item.tenderItemId === tenderItem.id);
                if (!bidItem) return null;
                const bidItemIndex = bidData.items.findIndex(item => item.tenderItemId === tenderItem.id);

                return (
                  <div key={tenderItem.id} className="border rounded-lg p-4 ml-4">
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
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}

