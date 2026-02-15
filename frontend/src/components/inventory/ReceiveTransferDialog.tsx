'use client';

import React, { useState, useEffect } from 'react';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Textarea } from '@/components/ui/textarea';
import { Download, FileText, Loader2, AlertTriangle } from 'lucide-react';
import {
  inventoryManagementService,
  InventoryTransferDetailDto, ReceiveTransferItemDto
} from '@/services/inventoryManagementService';
import { useToast } from '@/hooks/use-toast';

interface ReceiveTransferDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  transferId: string | null;
  onSuccess: () => void;
}

interface ReceiveQuantity {
  itemId: string;
  shippedQuantity: number;
  alreadyReceived: number;
  toReceive: number;
  itemCode?: string;
  itemName?: string;
  unitOfMeasure?: string;
  sourceLocationName?: string;
  destinationLocationName?: string;
}

export function ReceiveTransferDialog({ open, onOpenChange, transferId, onSuccess }: ReceiveTransferDialogProps) {
  const { toast } = useToast();
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [transfer, setTransfer] = useState<InventoryTransferDetailDto | null>(null);
  const [receiveQuantities, setReceiveQuantities] = useState<ReceiveQuantity[]>([]);
  const [notes, setNotes] = useState('');

  useEffect(() => {
    if (open && transferId) {
      loadTransferDetails();
    } else {
      setTransfer(null);
      setReceiveQuantities([]);
      setNotes('');
    }
  }, [open, transferId]);

  const loadTransferDetails = async () => {
    if (!transferId) return;
    try {
      setLoading(true);
      const detail = await inventoryManagementService.getInventoryTransferById(transferId);
      setTransfer(detail);
      
      // Initialize receive quantities from items
      const quantities: ReceiveQuantity[] = detail.items.map(item => ({
        itemId: item.id,
        shippedQuantity: item.shippedQuantity,
        alreadyReceived: item.receivedQuantity,
        toReceive: item.shippedQuantity - item.receivedQuantity, // Default to remaining qty
        itemCode: item.itemCode,
        itemName: item.itemName,
        unitOfMeasure: item.unitOfMeasure,
        sourceLocationName: item.sourceLocationName,
        destinationLocationName: item.destinationLocationName
      }));
      setReceiveQuantities(quantities);
    } catch (err) {
      console.error('Error loading transfer:', err);
      toast({ title: 'Error', description: 'Failed to load transfer details', variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  };

  const updateReceiveQuantity = (itemId: string, qty: number) => {
    setReceiveQuantities(prev => prev.map(q => 
      q.itemId === itemId ? { ...q, toReceive: Math.max(0, qty) } : q
    ));
  };

  const handleReceive = async () => {
    if (!transferId) return;
    
    // Filter items with quantity to receive
    const itemsToReceive: ReceiveTransferItemDto[] = receiveQuantities
      .filter(q => q.toReceive > 0)
      .map(q => ({ id: q.itemId, receivedQuantity: q.toReceive }));

    if (itemsToReceive.length === 0) {
      toast({ title: 'Warning', description: 'Please enter quantities to receive', variant: 'destructive' });
      return;
    }

    try {
      setSaving(true);
      await inventoryManagementService.receiveTransfer(transferId, itemsToReceive);
      toast({ title: 'Success', description: 'Transfer received successfully' });
      onSuccess();
      onOpenChange(false);
    } catch (err: any) {
      console.error('Error receiving transfer:', err);
      toast({ 
        title: 'Error', 
        description: err.response?.data?.message || err.response?.data || 'Failed to receive transfer', 
        variant: 'destructive' 
      });
    } finally {
      setSaving(false);
    }
  };

  const handlePrintGRN = async () => {
    if (!transferId) return;
    try {
      const blob = await inventoryManagementService.getGoodsReceivedNotePdf(transferId);
      const url = window.URL.createObjectURL(blob);
      window.open(url, '_blank');
    } catch (err) {
      console.error('Error generating GRN:', err);
      toast({ title: 'Error', description: 'Failed to generate GRN', variant: 'destructive' });
    }
  };

  const handlePrintShipmentNote = async () => {
    if (!transferId) return;
    try {
      const blob = await inventoryManagementService.getShipmentNotePdf(transferId);
      const url = window.URL.createObjectURL(blob);
      window.open(url, '_blank');
    } catch (err) {
      console.error('Error generating shipment note:', err);
      toast({ title: 'Error', description: 'Failed to generate shipment note', variant: 'destructive' });
    }
  };

  const totalToReceive = receiveQuantities.reduce((sum, q) => sum + q.toReceive, 0);
  const totalShipped = receiveQuantities.reduce((sum, q) => sum + q.shippedQuantity, 0);
  const hasVariance = receiveQuantities.some(q => {
    const expectedRemaining = q.shippedQuantity - q.alreadyReceived;
    return q.toReceive !== expectedRemaining && q.toReceive > 0;
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-5xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Download className="h-5 w-5" />
            Receive Transfer {transfer?.transferNumber}
          </DialogTitle>
          <DialogDescription>
            Enter the quantities received for each item. You can receive partial quantities or report variances.
          </DialogDescription>
        </DialogHeader>

        {loading ? (
          <div className="flex items-center justify-center py-8">
            <Loader2 className="h-8 w-8 animate-spin" />
          </div>
        ) : transfer ? (
          <div className="space-y-4">
            {/* Transfer Info */}
            <div className="grid grid-cols-3 gap-4 p-4 bg-muted rounded-lg">
              <div>
                <span className="text-sm text-muted-foreground">From:</span>
                <p className="font-medium">{transfer.sourceWarehouseName}</p>
              </div>
              <div>
                <span className="text-sm text-muted-foreground">To:</span>
                <p className="font-medium">{transfer.destinationWarehouseName}</p>
              </div>
              <div>
                <span className="text-sm text-muted-foreground">Tracking #:</span>
                <p className="font-medium">{transfer.trackingNumber || 'N/A'}</p>
              </div>
            </div>

            {/* Variance Warning */}
            {hasVariance && (
              <div className="flex items-center gap-2 p-3 bg-yellow-50 border border-yellow-200 rounded-lg text-yellow-800">
                <AlertTriangle className="h-5 w-5" />
                <span className="text-sm">Some quantities differ from shipped amounts. This will be recorded as a variance.</span>
              </div>
            )}

            {/* Items Table */}
            <div className="border rounded-lg">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Item Code</TableHead>
                    <TableHead>Description</TableHead>
                    <TableHead>From Bin</TableHead>
                    <TableHead>To Bin</TableHead>
                    <TableHead>UoM</TableHead>
                    <TableHead className="text-right">Shipped</TableHead>
                    <TableHead className="text-right">Already Received</TableHead>
                    <TableHead className="text-right">Pending</TableHead>
                    <TableHead className="text-right w-32">Qty to Receive</TableHead>
                    <TableHead className="text-right">Variance</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {receiveQuantities.map((item) => {
                    const pending = item.shippedQuantity - item.alreadyReceived;
                    const variance = item.toReceive - pending;
                    return (
                      <TableRow key={item.itemId}>
                        <TableCell className="font-mono text-sm">{item.itemCode}</TableCell>
                        <TableCell>{item.itemName}</TableCell>
                        <TableCell className="text-sm">{item.sourceLocationName || <span className="text-muted-foreground">-</span>}</TableCell>
                        <TableCell className="text-sm">{item.destinationLocationName || <span className="text-muted-foreground">-</span>}</TableCell>
                        <TableCell>{item.unitOfMeasure}</TableCell>
                        <TableCell className="text-right">{item.shippedQuantity.toFixed(2)}</TableCell>
                        <TableCell className="text-right">
                          {item.alreadyReceived > 0 ? (
                            <Badge variant="secondary">{item.alreadyReceived.toFixed(2)}</Badge>
                          ) : '-'}
                        </TableCell>
                        <TableCell className="text-right">{pending.toFixed(2)}</TableCell>
                        <TableCell className="text-right">
                          <Input
                            type="number"
                            min="0"
                            step="0.01"
                            value={item.toReceive}
                            onChange={(e) => updateReceiveQuantity(item.itemId, parseFloat(e.target.value) || 0)}
                            className="w-24 text-right"
                            disabled={pending <= 0}
                          />
                        </TableCell>
                        <TableCell className="text-right">
                          {variance !== 0 ? (
                            <Badge variant={variance < 0 ? 'destructive' : 'default'}>
                              {variance > 0 ? '+' : ''}{variance.toFixed(2)}
                            </Badge>
                          ) : '-'}
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </div>

            {/* Notes */}
            <div className="space-y-2">
              <Label htmlFor="notes">Receiving Notes (Optional)</Label>
              <Textarea
                id="notes"
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                placeholder="Add notes about the received items, condition, or any discrepancies..."
                rows={3}
              />
            </div>

            {/* Summary */}
            <div className="flex justify-between items-center p-4 bg-muted rounded-lg">
              <div className="flex gap-4">
                <div>
                  <span className="text-sm text-muted-foreground">Total Shipped:</span>
                  <Badge variant="outline" className="ml-2">{totalShipped.toFixed(2)}</Badge>
                </div>
                <div>
                  <span className="text-sm text-muted-foreground">To Receive:</span>
                  <Badge variant={totalToReceive > 0 ? 'default' : 'secondary'} className="ml-2">
                    {totalToReceive.toFixed(2)}
                  </Badge>
                </div>
              </div>
            </div>
          </div>
        ) : null}

        <DialogFooter className="flex justify-between sm:justify-between">
          <div className="flex gap-2">
            {transfer && (
              <Button type="button" variant="outline" onClick={handlePrintShipmentNote}>
                <FileText className="h-4 w-4 mr-2" />
                Shipment Note
              </Button>
            )}
            {transfer && (
              <Button type="button" variant="outline" onClick={handlePrintGRN}>
                <FileText className="h-4 w-4 mr-2" />
                {transfer.receivedDate ? 'Print GRN' : 'Preview GRN'}
              </Button>
            )}
          </div>
          <div className="flex gap-2">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button onClick={handleReceive} disabled={saving || totalToReceive <= 0}>
              {saving && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}
              <Download className="h-4 w-4 mr-2" />
              Receive Items
            </Button>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
