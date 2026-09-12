'use client';

import React, { useState, useEffect } from 'react';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { FileText, ArrowUp, ArrowDown, ArrowUpDown } from 'lucide-react';
import { 
  inventoryManagementService, 
  StockMovementDto, 
  InventoryTransferDetailDto 
} from '@/services/inventoryManagementService';
import { useToast } from '@/hooks/use-toast';
import { format } from 'date-fns';
import { formatInventoryMoney } from '@/lib/inventory-currency';

interface MovementDetailDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  movement: StockMovementDto | null;
  currencyCode: string | null;
}

// Movement type definitions for display
const getMovementTypeInfo = (type: string) => {
  const inboundTypes = ['Receipt', 'Return', 'Adjustment+', 'Transfer-In', 'Production'];
  const outboundTypes = ['Issue', 'Sale', 'Adjustment-', 'Transfer-Out', 'Consumption', 'Waste', 'Scrap'];
  
  if (inboundTypes.some(t => type.includes(t))) {
    return { color: 'bg-green-100 text-green-800', icon: ArrowDown, isInbound: true };
  } else if (outboundTypes.some(t => type.includes(t))) {
    return { color: 'bg-red-100 text-red-800', icon: ArrowUp, isInbound: false };
  }
  return { color: 'bg-blue-100 text-blue-800', icon: ArrowUpDown, isInbound: null };
};

export function MovementDetailDialog({ open, onOpenChange, movement, currencyCode }: MovementDetailDialogProps) {
  const { toast } = useToast();
  const [activeTab, setActiveTab] = useState('header');
  const [transferDetail, setTransferDetail] = useState<InventoryTransferDetailDto | null>(null);
  const [loadingTransfer, setLoadingTransfer] = useState(false);

  // Check if this movement is related to a transfer
  const isTransferMovement = movement?.referenceType === 'Transfer' && movement?.referenceNumber;

  useEffect(() => {
    if (open && isTransferMovement && movement?.referenceNumber) {
      loadTransferDetails(movement.referenceNumber);
    } else {
      setTransferDetail(null);
    }
  }, [open, movement?.referenceNumber, isTransferMovement]);

  const loadTransferDetails = async (transferNumber: string) => {
    try {
      setLoadingTransfer(true);
      const detail = await inventoryManagementService.getInventoryTransferByNumber(transferNumber);
      setTransferDetail(detail);
    } catch (err) {
      console.error('Error loading transfer details:', err);
      // Transfer might not exist or user doesn't have access
      setTransferDetail(null);
    } finally {
      setLoadingTransfer(false);
    }
  };

  const handlePrintShipmentNote = async () => {
    if (!transferDetail?.id) return;
    try {
      const blob = await inventoryManagementService.getShipmentNotePdf(transferDetail.id);
      const url = window.URL.createObjectURL(blob);
      window.open(url, '_blank');
    } catch (err) {
      console.error('Error generating shipment note:', err);
      toast({ title: 'Error', description: 'Failed to generate shipment note', variant: 'destructive' });
    }
  };

  const handlePrintGRN = async () => {
    if (!transferDetail?.id) return;
    try {
      const blob = await inventoryManagementService.getGoodsReceivedNotePdf(transferDetail.id);
      const url = window.URL.createObjectURL(blob);
      window.open(url, '_blank');
    } catch (err) {
      console.error('Error generating GRN:', err);
      toast({ title: 'Error', description: 'Failed to generate GRN', variant: 'destructive' });
    }
  };

  if (!movement) return null;

  const typeInfo = getMovementTypeInfo(movement.movementType);
  const IconComponent = typeInfo.icon;
  const money = (value: number) => currencyCode ? formatInventoryMoney(value, currencyCode) : '—';

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <IconComponent className={`h-5 w-5 ${
              typeInfo.isInbound === true ? 'text-green-600' :
              typeInfo.isInbound === false ? 'text-red-600' :
              'text-blue-600'
            }`} />
            Movement Details - {movement.movementNumber}
          </DialogTitle>
          <DialogDescription>
            View stock movement information
            {isTransferMovement && ` • Transfer: ${movement.referenceNumber}`}
          </DialogDescription>
        </DialogHeader>

        <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full">
          <TabsList className="grid w-full grid-cols-2">
            <TabsTrigger value="header">Header</TabsTrigger>
            <TabsTrigger value="details">Details</TabsTrigger>
          </TabsList>

          <TabsContent value="header" className="space-y-4 mt-4">
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm">Movement Information</CardTitle>
              </CardHeader>
              <CardContent className="grid grid-cols-2 gap-4 text-sm">
                <div>
                  <p className="text-muted-foreground">Movement Number</p>
                  <p className="font-medium">{movement.movementNumber}</p>
                </div>
                <div>
                  <p className="text-muted-foreground">Movement Type</p>
                  <Badge className={typeInfo.color}>{movement.movementType}</Badge>
                </div>
                <div>
                  <p className="text-muted-foreground">Movement Date</p>
                  <p className="font-medium">
                    {movement.movementDate ? format(new Date(movement.movementDate), 'MMM dd, yyyy HH:mm') : '-'}
                  </p>
                </div>
                <div>
                  <p className="text-muted-foreground">Created By</p>
                  <p className="font-medium">{movement.createdByName || '-'}</p>
                </div>
                {movement.referenceType && (
                  <div>
                    <p className="text-muted-foreground">Reference Type</p>
                    <p className="font-medium">{movement.referenceType}</p>
                  </div>
                )}
                {movement.referenceNumber && (
                  <div>
                    <p className="text-muted-foreground">Reference Number</p>
                    <p className="font-medium">{movement.referenceNumber}</p>
                  </div>
                )}
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm">Location Information</CardTitle>
              </CardHeader>
              <CardContent className="grid grid-cols-2 gap-4 text-sm">
                <div>
                  <p className="text-muted-foreground">Source Location</p>
                  <p className="font-medium">{movement.sourceLocationName || '-'}</p>
                </div>
                <div>
                  <p className="text-muted-foreground">Destination Location</p>
                  <p className="font-medium">{movement.destinationLocationName || '-'}</p>
                </div>
              </CardContent>
            </Card>

            {movement.notes && (
              <Card>
                <CardHeader className="pb-2">
                  <CardTitle className="text-sm">Notes</CardTitle>
                </CardHeader>
                <CardContent>
                  <p className="text-sm">{movement.notes}</p>
                </CardContent>
              </Card>
            )}
          </TabsContent>

          <TabsContent value="details" className="space-y-4 mt-4">
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm">Item Details</CardTitle>
              </CardHeader>
              <CardContent className="grid grid-cols-2 gap-4 text-sm">
                <div>
                  <p className="text-muted-foreground">Item Code</p>
                  <p className="font-medium font-mono">{movement.itemCode || '-'}</p>
                </div>
                <div>
                  <p className="text-muted-foreground">Item Name</p>
                  <p className="font-medium">{movement.itemName || '-'}</p>
                </div>
                <div>
                  <p className="text-muted-foreground">Quantity</p>
                  <p className={`font-medium text-lg ${
                    typeInfo.isInbound === true ? 'text-green-600' :
                    typeInfo.isInbound === false ? 'text-red-600' : ''
                  }`}>
                    {typeInfo.isInbound === true ? '+' : typeInfo.isInbound === false ? '-' : ''}
                    {Math.abs(movement.quantity).toLocaleString()}
                  </p>
                </div>
                <div>
                  <p className="text-muted-foreground">Unit Cost</p>
                  <p className="font-medium">{money(movement.unitCost)}</p>
                </div>
                <div className="col-span-2">
                  <p className="text-muted-foreground">Total Cost</p>
                  <p className="font-medium text-lg">{money(movement.totalCost)}</p>
                </div>
              </CardContent>
            </Card>

            {/* Transfer Details - if this is a transfer movement */}
            {isTransferMovement && (
              <Card>
                <CardHeader className="pb-2">
                  <CardTitle className="text-sm">Transfer Details</CardTitle>
                  <CardDescription>
                    {loadingTransfer ? 'Loading transfer details...' :
                     transferDetail ? `Transfer ${transferDetail.transferNumber}` : 'Transfer details not available'}
                  </CardDescription>
                </CardHeader>
                {transferDetail && (
                  <CardContent className="space-y-4">
                    <div className="grid grid-cols-2 gap-4 text-sm">
                      <div>
                        <p className="text-muted-foreground">Source Warehouse</p>
                        <p className="font-medium">{transferDetail.sourceWarehouseName}</p>
                      </div>
                      <div>
                        <p className="text-muted-foreground">Destination Warehouse</p>
                        <p className="font-medium">{transferDetail.destinationWarehouseName}</p>
                      </div>
                      <div>
                        <p className="text-muted-foreground">Status</p>
                        <p className="font-medium">{transferDetail.status}</p>
                      </div>
                      <div>
                        <p className="text-muted-foreground">Total Items</p>
                        <p className="font-medium">{transferDetail.totalItems}</p>
                      </div>
                    </div>

                    {transferDetail.items && transferDetail.items.length > 0 && (
                      <div className="border rounded-lg">
                        <Table>
                          <TableHeader>
                            <TableRow>
                              <TableHead>Item</TableHead>
                              <TableHead className="text-right">Requested</TableHead>
                              <TableHead className="text-right">Shipped</TableHead>
                              <TableHead className="text-right">Received</TableHead>
                            </TableRow>
                          </TableHeader>
                          <TableBody>
                            {transferDetail.items.map((item) => (
                              <TableRow key={item.id}>
                                <TableCell>
                                  <div className="font-medium">{item.itemName || item.itemCode}</div>
                                  <div className="text-xs text-muted-foreground">{item.itemCode}</div>
                                </TableCell>
                                <TableCell className="text-right">{item.requestedQuantity}</TableCell>
                                <TableCell className="text-right">{item.shippedQuantity}</TableCell>
                                <TableCell className="text-right">{item.receivedQuantity}</TableCell>
                              </TableRow>
                            ))}
                          </TableBody>
                        </Table>
                      </div>
                    )}
                  </CardContent>
                )}
              </Card>
            )}
          </TabsContent>
        </Tabs>

        <DialogFooter className="flex justify-between sm:justify-between">
          <div className="flex gap-2">
            {isTransferMovement && transferDetail && (
              <>
                {['InTransit', 'Received', 'Completed'].includes(transferDetail.status) && (
                  <Button type="button" variant="outline" onClick={handlePrintShipmentNote}>
                    <FileText className="h-4 w-4 mr-2" />
                    Shipment Note
                  </Button>
                )}
                {['Received', 'Completed'].includes(transferDetail.status) && (
                  <Button type="button" variant="outline" onClick={handlePrintGRN}>
                    <FileText className="h-4 w-4 mr-2 text-green-600" />
                    GRN
                  </Button>
                )}
              </>
            )}
          </div>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
            Close
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

