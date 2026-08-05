'use client';

import React, { useState, useEffect, useMemo, useRef } from 'react';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Checkbox } from '@/components/ui/checkbox';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Send, FileText, Loader2, DollarSign, Calculator, Save } from 'lucide-react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Separator } from '@/components/ui/separator';
import { Textarea } from '@/components/ui/textarea';
import {
  inventoryManagementService,
  InventoryTransferDetailDto, InventoryTransferItemDto, ShipTransferItemDto, ShipTransferWithCostsDto
} from '@/services/inventoryManagementService';
import { useToast } from '@/hooks/use-toast';

interface ShipTransferDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  transferId: string | null;
  onSuccess: () => void;
}

interface ShipQuantity {
  itemId: string;
  requestedQuantity: number;
  alreadyShipped: number;
  toShip: number;
  itemCode?: string;
  itemName?: string;
  unitOfMeasure?: string;
  unitCost?: number;
  sourceLocationName?: string;
  destinationLocationName?: string;
}

export function ShipTransferDialog({ open, onOpenChange, transferId, onSuccess }: ShipTransferDialogProps) {
  const { toast } = useToast();
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [savingDraft, setSavingDraft] = useState(false);
  const [transfer, setTransfer] = useState<InventoryTransferDetailDto | null>(null);
  const [trackingNumber, setTrackingNumber] = useState('');
  const [carrierName, setCarrierName] = useState('');
  const [dispatchComment, setDispatchComment] = useState('');
  const [shipQuantities, setShipQuantities] = useState<ShipQuantity[]>([]);
  const mutationKeyRef = useRef<{ fingerprint: string; key: string } | null>(null);

  // Shipping costs state
  const [includeCosts, setIncludeCosts] = useState(false);
  const [shippingCost, setShippingCost] = useState(0);
  const [miscellaneousCost, setMiscellaneousCost] = useState(0);
  const [miscellaneousCostDescription, setMiscellaneousCostDescription] = useState('');
  const [costAllocationMethod, setCostAllocationMethod] = useState<'SpreadToItemCost' | 'GLExpense'>('SpreadToItemCost');
  const [costApportionmentBasis, setCostApportionmentBasis] = useState<'Value' | 'Weight' | 'Quantity'>('Value');
  const [expenseGLAccount, setExpenseGLAccount] = useState('');

  useEffect(() => {
    if (open && transferId) {
      loadTransferDetails();
    } else {
      setTransfer(null);
      setShipQuantities([]);
      setTrackingNumber('');
      setCarrierName('');
      setDispatchComment('');
      mutationKeyRef.current = null;
      setIncludeCosts(false);
      setShippingCost(0);
      setMiscellaneousCost(0);
      setMiscellaneousCostDescription('');
      setCostAllocationMethod('SpreadToItemCost');
      setCostApportionmentBasis('Value');
      setExpenseGLAccount('');
    }
  }, [open, transferId]);

  const loadTransferDetails = async () => {
    if (!transferId) return;
    try {
      setLoading(true);
      const detail = await inventoryManagementService.getInventoryTransferById(transferId);
      setTransfer(detail);
      setShippingCost(detail.shippingCost || 0);
      setMiscellaneousCost(detail.miscellaneousCost || 0);
      setMiscellaneousCostDescription(detail.miscellaneousCostDescription || '');
      setCostAllocationMethod(detail.costAllocationMethod || 'SpreadToItemCost');
      setCostApportionmentBasis(detail.costApportionmentBasis || 'Value');
      setExpenseGLAccount(detail.expenseGLAccount || '');
      setIncludeCosts((detail.shippingCost || 0) > 0 || (detail.miscellaneousCost || 0) > 0);
      
      // Initialize ship quantities from items
      const quantities: ShipQuantity[] = detail.items.map(item => ({
        itemId: item.id,
        requestedQuantity: item.requestedQuantity,
        alreadyShipped: item.shippedQuantity,
        toShip: item.requestedQuantity - item.shippedQuantity, // Default to remaining qty
        itemCode: item.itemCode,
        itemName: item.itemName,
        unitOfMeasure: item.unitOfMeasure,
        unitCost: item.unitCost || 0,
        sourceLocationName: item.sourceLocationName,
        destinationLocationName: item.destinationLocationName
      }));
      setShipQuantities(quantities);
    } catch (err) {
      console.error('Error loading transfer:', err);
      toast({ title: 'Error', description: 'Failed to load transfer details', variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  };

  const updateShipQuantity = (itemId: string, qty: number) => {
    setShipQuantities(prev => prev.map(q => 
      q.itemId === itemId ? { ...q, toShip: Math.max(0, Math.min(qty, q.requestedQuantity - q.alreadyShipped)) } : q
    ));
  };

  const mutationKeyFor = (kind: string, payload: unknown) => {
    const fingerprint = `${kind}:${JSON.stringify(payload)}`;
    if (mutationKeyRef.current?.fingerprint !== fingerprint) {
      mutationKeyRef.current = { fingerprint, key: crypto.randomUUID() };
    }
    return mutationKeyRef.current.key;
  };

  const handleShip = async () => {
    if (!transferId || !transfer) return;

    // Filter items with quantity to ship
    const itemsToShip: ShipTransferItemDto[] = shipQuantities
      .filter(q => q.toShip > 0)
      .map(q => ({ itemId: q.itemId, shippedQuantity: q.toShip }));

    if (itemsToShip.length === 0) {
      toast({ title: 'Warning', description: 'Please enter quantities to ship', variant: 'destructive' });
      return;
    }

    // Validate costs if included
    if (includeCosts) {
      if (costAllocationMethod === 'GLExpense' && !expenseGLAccount.trim()) {
        toast({ title: 'Error', description: 'GL Account is required when posting to expense', variant: 'destructive' });
        return;
      }
    }

    try {
      setSaving(true);

      if (includeCosts) {
        const idempotencyKey = mutationKeyFor('dispatch-with-costs', {
          trackingNumber, carrierName, shippingCost, miscellaneousCost,
          miscellaneousCostDescription, costAllocationMethod, costApportionmentBasis,
          expenseGLAccount, itemsToShip, dispatchComment,
        });
        // Use the new shipping costs API
        const costsDto: ShipTransferWithCostsDto = {
          rowVersion: transfer.rowVersion,
          idempotencyKey,
          correlationId: `transfer-dispatch:${transferId}:${idempotencyKey}`,
          comment: dispatchComment.trim() || undefined,
          trackingNumber: trackingNumber || undefined,
          carrierName: carrierName || undefined,
          shippingCost,
          miscellaneousCost,
          miscellaneousCostDescription: miscellaneousCostDescription || undefined,
          costAllocationMethod,
          costApportionmentBasis: costAllocationMethod === 'SpreadToItemCost' ? costApportionmentBasis : undefined,
          expenseGLAccount: costAllocationMethod === 'GLExpense' ? expenseGLAccount : undefined,
          items: itemsToShip
        };

        await inventoryManagementService.shipTransferWithCosts(transferId, costsDto);
        toast({ title: 'Success', description: 'Transfer shipped with costs successfully' });
      } else {
        const idempotencyKey = mutationKeyFor('dispatch', { trackingNumber, itemsToShip, dispatchComment });
        await inventoryManagementService.shipTransfer(transferId, {
          rowVersion: transfer.rowVersion,
          idempotencyKey,
          correlationId: `transfer-dispatch:${transferId}:${idempotencyKey}`,
          comment: dispatchComment.trim() || undefined,
        }, trackingNumber || undefined, itemsToShip);
        toast({ title: 'Success', description: 'Transfer shipped successfully' });
      }

      onSuccess();
      onOpenChange(false);
    } catch (err: any) {
      console.error('Error shipping transfer:', err);
      toast({
        title: 'Error',
        description: err.response?.data?.message || err.response?.data || 'Failed to ship transfer',
        variant: 'destructive'
      });
    } finally {
      setSaving(false);
    }
  };

  const handlePrintShipmentNote = async () => {
    if (!transferId || !transfer) return;
    try {
      const blob = await inventoryManagementService.getShipmentNotePdf(transferId);
      const url = window.URL.createObjectURL(blob);
      window.open(url, '_blank');
    } catch (err) {
      console.error('Error generating shipment note:', err);
      toast({ title: 'Error', description: 'Failed to generate shipment note', variant: 'destructive' });
    }
  };

  const handleSaveDraft = async () => {
    if (!transferId) return;

    // Validate costs if included
    if (includeCosts) {
      if (costAllocationMethod === 'GLExpense' && !expenseGLAccount.trim()) {
        toast({ title: 'Error', description: 'GL Account is required when posting to expense', variant: 'destructive' });
        return;
      }
    }

    try {
      setSavingDraft(true);

      const idempotencyKey = mutationKeyFor('save-shipping-costs', {
        trackingNumber, carrierName, shippingCost, miscellaneousCost,
        miscellaneousCostDescription, costAllocationMethod, costApportionmentBasis,
        expenseGLAccount,
      });
      const costsDto: ShipTransferWithCostsDto = {
        rowVersion: transfer.rowVersion,
        idempotencyKey,
        correlationId: `transfer-shipping-costs:${transferId}:${idempotencyKey}`,
        comment: dispatchComment.trim() || undefined,
        trackingNumber: trackingNumber || undefined,
        carrierName: carrierName || undefined,
        shippingCost,
        miscellaneousCost,
        miscellaneousCostDescription: miscellaneousCostDescription || undefined,
        costAllocationMethod,
        costApportionmentBasis: costAllocationMethod === 'SpreadToItemCost' ? costApportionmentBasis : undefined,
        expenseGLAccount: costAllocationMethod === 'GLExpense' ? expenseGLAccount : undefined,
        items: undefined // No items when just saving draft
      };

      await inventoryManagementService.saveShippingCosts(transferId, costsDto);
      toast({ title: 'Success', description: 'Shipping costs saved successfully. You can ship later.' });
      onSuccess();
      onOpenChange(false);
    } catch (err: any) {
      console.error('Error saving shipping costs:', err);
      toast({
        title: 'Error',
        description: err.response?.data?.message || err.response?.data || 'Failed to save shipping costs',
        variant: 'destructive'
      });
    } finally {
      setSavingDraft(false);
    }
  };

  const totalToShip = shipQuantities.reduce((sum, q) => sum + q.toShip, 0);
  const totalAdditionalCost = shippingCost + miscellaneousCost;
  const shipmentSubtotal = shipQuantities.reduce((sum, q) => sum + (q.toShip * (q.unitCost || 0)), 0);
  const shipmentAdditionalCost = includeCosts ? totalAdditionalCost : 0;
  const shipmentTotal = shipmentSubtotal + shipmentAdditionalCost;

  const allocationPreview = useMemo(() => {
    const preview: Record<string, { allocated: number; landedUnit: number; shipValue: number }> = {};
    if (!includeCosts || costAllocationMethod !== 'SpreadToItemCost' || totalAdditionalCost <= 0) {
      return preview;
    }

    const rows = shipQuantities.filter(q => q.toShip > 0);
    if (!rows.length) return preview;

    const basisValues = rows.map(q => {
      if (costApportionmentBasis === 'Quantity' || costApportionmentBasis === 'Weight') {
        return q.toShip;
      }
      return q.toShip * (q.unitCost || 0);
    });

    let totalBasis = basisValues.reduce((sum, v) => sum + v, 0);
    if (totalBasis <= 0) {
      // Fallback to quantity when value basis isn't available (e.g., zero costs).
      totalBasis = rows.reduce((sum, q) => sum + q.toShip, 0);
      rows.forEach((q, idx) => { basisValues[idx] = q.toShip; });
    }
    if (totalBasis <= 0) return preview;

    let running = 0;
    rows.forEach((q, index) => {
      const allocated = index === rows.length - 1
        ? Number((totalAdditionalCost - running).toFixed(2))
        : Number((totalAdditionalCost * (basisValues[index] / totalBasis)).toFixed(2));
      if (index !== rows.length - 1) running += allocated;

      const allocatedPerUnit = q.toShip > 0 ? allocated / q.toShip : 0;
      preview[q.itemId] = {
        allocated,
        landedUnit: Number(((q.unitCost || 0) + allocatedPerUnit).toFixed(4)),
        shipValue: Number((q.toShip * (q.unitCost || 0)).toFixed(2))
      };
    });

    return preview;
  }, [shipQuantities, includeCosts, costAllocationMethod, costApportionmentBasis, totalAdditionalCost]);

  const totalAllocatedPreview = includeCosts && costAllocationMethod === 'SpreadToItemCost'
    ? Object.values(allocationPreview).reduce((sum, item) => sum + (item.allocated || 0), 0)
    : 0;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-[95vw] lg:max-w-7xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Send className="h-5 w-5" />
            Ship Transfer {transfer?.transferNumber}
          </DialogTitle>
          <DialogDescription>
            Enter the quantities to ship for each item. You can ship partial quantities.
          </DialogDescription>
        </DialogHeader>

        {loading ? (
          <div className="flex items-center justify-center py-8">
            <Loader2 className="h-8 w-8 animate-spin" />
          </div>
        ) : transfer ? (
          <div className="space-y-4">
            {/* Transfer Info */}
            <div className="grid grid-cols-2 gap-4 p-4 bg-muted rounded-lg">
              <div>
                <span className="text-sm text-muted-foreground">From:</span>
                <p className="font-medium">{transfer.sourceWarehouseName}</p>
              </div>
              <div>
                <span className="text-sm text-muted-foreground">To:</span>
                <p className="font-medium">{transfer.destinationWarehouseName}</p>
              </div>
            </div>

            {/* Tracking Number */}
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="trackingNumber">Tracking Number (Optional)</Label>
                <Input
                  id="trackingNumber"
                  value={trackingNumber}
                  onChange={(e) => setTrackingNumber(e.target.value)}
                  placeholder="Enter tracking number"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="carrierName">Carrier Name (Optional)</Label>
                <Input
                  id="carrierName"
                  value={carrierName}
                  onChange={(e) => setCarrierName(e.target.value)}
                  placeholder="Enter carrier name"
                />
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="dispatchComment">Dispatch control comment</Label>
              <Textarea
                id="dispatchComment"
                value={dispatchComment}
                onChange={(event) => setDispatchComment(event.target.value)}
                placeholder="Optional operational context retained in the immutable transfer action register"
                rows={2}
              />
            </div>

            {/* Shipping Costs Section */}
            <Card>
              <CardHeader className="pb-3">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <DollarSign className="h-4 w-4" />
                    <CardTitle className="text-base">Shipping Costs</CardTitle>
                  </div>
                  <div className="flex items-center space-x-2">
                    <Checkbox
                      id="includeCosts"
                      checked={includeCosts}
                      onCheckedChange={(checked) => setIncludeCosts(checked as boolean)}
                    />
                    <Label htmlFor="includeCosts" className="text-sm">Include shipping costs</Label>
                  </div>
                </div>
                <CardDescription>
                  Capture shipping and miscellaneous costs for this transfer
                </CardDescription>
              </CardHeader>

              {includeCosts && (
                <CardContent className="grid grid-cols-1 gap-4 lg:grid-cols-2">
                  <div className="space-y-4">
                    <div className="grid grid-cols-2 gap-4">
                      <div className="space-y-2">
                        <Label htmlFor="shippingCost">Shipping Cost</Label>
                        <Input
                          id="shippingCost"
                          type="number"
                          min="0"
                          step="0.01"
                          value={shippingCost}
                          onChange={(e) => setShippingCost(parseFloat(e.target.value) || 0)}
                          placeholder="0.00"
                        />
                      </div>
                      <div className="space-y-2">
                        <Label htmlFor="miscellaneousCost">Miscellaneous Cost</Label>
                        <Input
                          id="miscellaneousCost"
                          type="number"
                          min="0"
                          step="0.01"
                          value={miscellaneousCost}
                          onChange={(e) => setMiscellaneousCost(parseFloat(e.target.value) || 0)}
                          placeholder="0.00"
                        />
                      </div>
                    </div>

                    <div className="space-y-2">
                      <Label>Cost Allocation Method</Label>
                      <Select value={costAllocationMethod} onValueChange={(value: 'SpreadToItemCost' | 'GLExpense') => setCostAllocationMethod(value)}>
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="SpreadToItemCost">
                            <div className="flex items-center gap-2">
                              <Calculator className="h-4 w-4" />
                              Spread to Item Cost
                            </div>
                          </SelectItem>
                          <SelectItem value="GLExpense">
                            <div className="flex items-center gap-2">
                              <DollarSign className="h-4 w-4" />
                              Post to GL Expense Account
                            </div>
                          </SelectItem>
                        </SelectContent>
                      </Select>
                    </div>
                  </div>

                  <div className="space-y-4">
                    {costAllocationMethod === 'GLExpense' && (
                      <div className="space-y-2">
                        <Label htmlFor="expenseGLAccount">GL Expense Account *</Label>
                        <Input
                          id="expenseGLAccount"
                          value={expenseGLAccount}
                          onChange={(e) => setExpenseGLAccount(e.target.value)}
                          placeholder="Enter GL account number"
                          required
                        />
                      </div>
                    )}

                    {costAllocationMethod === 'SpreadToItemCost' && (
                      <div className="space-y-2">
                        <Label>Cost Apportionment Basis</Label>
                        <Select
                          value={costApportionmentBasis}
                          onValueChange={(value: 'Value' | 'Weight' | 'Quantity') => setCostApportionmentBasis(value)}
                        >
                          <SelectTrigger>
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="Value">By Item Value</SelectItem>
                            <SelectItem value="Weight">By Item Weight</SelectItem>
                            <SelectItem value="Quantity">By Item Quantity</SelectItem>
                          </SelectContent>
                        </Select>
                        <p className="text-xs text-muted-foreground">
                          Costs will be proportionally allocated to item costs.
                        </p>
                      </div>
                    )}

                    <div className="p-3 bg-muted rounded-lg">
                      <div className="space-y-2 text-sm">
                        <div className="flex justify-between items-center">
                          <span>Shipping Cost:</span>
                          <span className="font-medium">${shippingCost.toFixed(2)}</span>
                        </div>
                        <div className="flex justify-between items-center">
                          <span>Miscellaneous Cost:</span>
                          <span className="font-medium">${miscellaneousCost.toFixed(2)}</span>
                        </div>
                        <Separator />
                        <div className="flex justify-between items-center">
                          <span>Total Additional Cost:</span>
                          <span className="font-semibold">${totalAdditionalCost.toFixed(2)}</span>
                        </div>
                      </div>
                    </div>

                    {costAllocationMethod === 'SpreadToItemCost' && (
                      <p className="text-xs text-muted-foreground">
                        Landed Unit = Unit Cost + (Allocated Additional Cost / Qty to Ship). Allocation basis: {costApportionmentBasis}.
                      </p>
                    )}
                  </div>
                </CardContent>
              )}
            </Card>

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
                    <TableHead className="text-right">Requested</TableHead>
                    <TableHead className="text-right">Already Shipped</TableHead>
                    <TableHead className="text-right">Remaining</TableHead>
                    <TableHead className="text-right w-32">Qty to Ship</TableHead>
                    <TableHead className="text-right">Unit Cost</TableHead>
                    <TableHead className="text-right">Ship Value</TableHead>
                    <TableHead className="text-right">Alloc. Cost</TableHead>
                    <TableHead className="text-right">Landed Unit</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {shipQuantities.map((item) => {
                    const remaining = item.requestedQuantity - item.alreadyShipped;
                    return (
                      <TableRow key={item.itemId}>
                        <TableCell className="font-mono text-sm">{item.itemCode}</TableCell>
                        <TableCell>{item.itemName}</TableCell>
                        <TableCell className="text-sm">{item.sourceLocationName || <span className="text-muted-foreground">-</span>}</TableCell>
                        <TableCell className="text-sm">{item.destinationLocationName || <span className="text-muted-foreground">-</span>}</TableCell>
                        <TableCell>{item.unitOfMeasure}</TableCell>
                        <TableCell className="text-right">{item.requestedQuantity.toFixed(2)}</TableCell>
                        <TableCell className="text-right">
                          {item.alreadyShipped > 0 ? (
                            <Badge variant="secondary">{item.alreadyShipped.toFixed(2)}</Badge>
                          ) : '-'}
                        </TableCell>
                        <TableCell className="text-right">{remaining.toFixed(2)}</TableCell>
                        <TableCell className="text-right">
                          <Input
                            type="number"
                            min="0"
                            max={remaining}
                            step="0.01"
                            value={item.toShip}
                            onChange={(e) => updateShipQuantity(item.itemId, parseFloat(e.target.value) || 0)}
                            className="w-24 text-right"
                            disabled={remaining <= 0}
                          />
                        </TableCell>
                        <TableCell className="text-right">${(item.unitCost || 0).toFixed(2)}</TableCell>
                        <TableCell className="text-right">
                          ${(allocationPreview[item.itemId]?.shipValue ?? ((item.toShip || 0) * (item.unitCost || 0))).toFixed(2)}
                        </TableCell>
                        <TableCell className="text-right">
                          {includeCosts && costAllocationMethod === 'SpreadToItemCost'
                            ? `$${(allocationPreview[item.itemId]?.allocated ?? 0).toFixed(2)}`
                            : '-'}
                        </TableCell>
                        <TableCell className="text-right">
                          {includeCosts && costAllocationMethod === 'SpreadToItemCost'
                            ? `$${(allocationPreview[item.itemId]?.landedUnit ?? (item.unitCost || 0)).toFixed(4)}`
                            : '-'}
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </div>

            {/* Summary */}
            <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
              <Card>
                <CardHeader className="pb-2">
                  <CardTitle className="text-sm">Quantity Summary</CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="flex justify-between items-center">
                    <span className="text-sm font-medium">Total Qty to Ship:</span>
                    <Badge variant={totalToShip > 0 ? 'default' : 'secondary'} className="text-lg px-4 py-1">
                      {totalToShip.toFixed(2)}
                    </Badge>
                  </div>
                </CardContent>
              </Card>

              <Card className="w-full lg:ml-auto lg:max-w-md">
                <CardHeader className="pb-2">
                  <CardTitle className="text-sm">Financial Summary</CardTitle>
                </CardHeader>
                <CardContent className="space-y-2 text-sm">
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Shipment Subtotal:</span>
                    <span className="font-medium">${shipmentSubtotal.toFixed(2)}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Additional Cost:</span>
                    <span className="font-medium">${shipmentAdditionalCost.toFixed(2)}</span>
                  </div>
                  {includeCosts && costAllocationMethod === 'SpreadToItemCost' && (
                    <div className="flex justify-between">
                      <span className="text-muted-foreground">Allocated to Lines:</span>
                      <span className="font-medium">${totalAllocatedPreview.toFixed(2)}</span>
                    </div>
                  )}
                  <Separator />
                  <div className="flex justify-between">
                    <span className="font-semibold">Total Shipment Value:</span>
                    <span className="font-semibold">${shipmentTotal.toFixed(2)}</span>
                  </div>
                </CardContent>
              </Card>
            </div>
          </div>
        ) : null}

        <DialogFooter className="flex justify-between sm:justify-between">
          <div>
            {transfer && (
              <Button type="button" variant="outline" onClick={handlePrintShipmentNote}>
                <FileText className="h-4 w-4 mr-2" />
                {transfer.shippedDate ? 'Print Shipment Note' : 'Preview Shipment Note'}
              </Button>
            )}
          </div>
          <div className="flex gap-2">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            {includeCosts && (
              <Button 
                type="button" 
                variant="secondary" 
                onClick={handleSaveDraft} 
                disabled={savingDraft || saving}
              >
                {savingDraft && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}
                <Save className="h-4 w-4 mr-2" />
                Save Draft
              </Button>
            )}
            <Button onClick={handleShip} disabled={saving || savingDraft || totalToShip <= 0}>
              {saving && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}
              <Send className="h-4 w-4 mr-2" />
              Ship Items
            </Button>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
