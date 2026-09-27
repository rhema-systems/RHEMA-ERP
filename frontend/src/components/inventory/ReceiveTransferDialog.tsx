'use client';

import { CentralDocumentViewerDialog, type CentralDocumentViewerFile } from '@/components/document-management/CentralDocumentViewerDialog';
import React, { useState, useEffect, useRef } from 'react';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Textarea } from '@/components/ui/textarea';
import { Download, FileText, Loader2, AlertTriangle, Columns3, Maximize2, Minimize2 } from 'lucide-react';
import { DropdownMenu, DropdownMenuTrigger, DropdownMenuContent, DropdownMenuCheckboxItem, DropdownMenuLabel } from '@/components/ui/dropdown-menu';
import {
  inventoryManagementService,
  InventoryTransferDetailDto, ReceiveTransferItemDto, WarehouseLocationDto
} from '@/services/inventoryManagementService';
import { useToast } from '@/hooks/use-toast';
import { getInventoryTransferProblemMessage } from '@/lib/inventory-transfer-controls';

interface ReceiveTransferDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  transferId: string | null;
  onSuccess: () => void;
}

interface ReceiveQuantity {
  rowKey: string;
  itemId: string;
  dispatchAllocationId?: string;
  destinationLocationId?: string;
  carrierName?: string;
  vehicleNumber?: string;
  shippedQuantity: number;
  alreadyReceived: number;
  legacyReservedQuantity: number;
  outstandingQuantity?: number;
  toReceive: number;
  itemCode?: string;
  itemName?: string;
  unitOfMeasure?: string;
  sourceLocationName?: string;
  sourceLocationId?: string;
  destinationLocationName?: string;
}

export function ReceiveTransferDialog({ open, onOpenChange, transferId, onSuccess }: ReceiveTransferDialogProps) {
  const { toast } = useToast();
  const [loading, setLoading] = useState(false);
  const [documentPreview, setDocumentPreview] = useState<CentralDocumentViewerFile | null>(null);
  const [saving, setSaving] = useState(false);
  const [fullPage, setFullPage] = useState(false);
  const [transfer, setTransfer] = useState<InventoryTransferDetailDto | null>(null);
  const [receiveQuantities, setReceiveQuantities] = useState<ReceiveQuantity[]>([]);
  const [destinationLocations, setDestinationLocations] = useState<WarehouseLocationDto[]>([]);
  const loadSequence = useRef(0);
  const [binColumns, setBinColumns] = useState({ source: false, destination: false });
  const [notes, setNotes] = useState('');
  const mutationKeyRef = useRef<{ fingerprint: string; key: string } | null>(null);

  useEffect(() => {
    setFullPage(false);
    setDocumentPreview(null);
    if (open && transferId) {
      loadTransferDetails();
    } else {
      setTransfer(null);
      setReceiveQuantities([]);
      setNotes('');
      mutationKeyRef.current = null;
    }
    return () => { loadSequence.current++; };
  }, [open, transferId]);

  const loadTransferDetails = async () => {
    if (!transferId) return;
    const sequence = ++loadSequence.current;
    try {
      setLoading(true);
      setTransfer(null);
      const detail = await inventoryManagementService.getInventoryTransferById(transferId);
      const locations = detail.items.some(item => item.dispatchAllocations?.length)
        ? await inventoryManagementService.getWarehouseLocations(detail.destinationWarehouseId) : [];
      if (sequence !== loadSequence.current) return;
      const receivingLocations = locations.filter(location => location.isActive && location.isReceivingLocation &&
        !location.isInTransitLocation && !location.isQuarantineLocation && !location.isInspectionLocation && !location.isDamageLocation &&
        !['InTransit', 'Quarantine'].includes(location.locationType));
      setDestinationLocations(receivingLocations);
      setTransfer(detail);
      
      // Initialize receive quantities from items
      const quantities: ReceiveQuantity[] = detail.items.flatMap<ReceiveQuantity>(item => item.dispatchAllocations?.length
        ? item.dispatchAllocations.filter(allocation => allocation.outstandingQuantity > 0).map(allocation => ({
          rowKey: allocation.id, itemId: item.id, dispatchAllocationId: allocation.id,
          destinationLocationId: receivingLocations.some(location => location.id === item.destinationLocationId && location.id !== allocation.sourceLocationId) ? item.destinationLocationId : '',
          sourceLocationId: allocation.sourceLocationId,
          shippedQuantity: allocation.quantity, alreadyReceived: allocation.receivedQuantity,
          legacyReservedQuantity: 0, outstandingQuantity: allocation.outstandingQuantity,
          toReceive: allocation.outstandingQuantity, itemCode: item.itemCode, itemName: item.itemName,
          unitOfMeasure: item.unitOfMeasure, sourceLocationName: allocation.sourceLocationName,
          carrierName: allocation.carrierName, vehicleNumber: allocation.vehicleNumber,
        })) : [{
        rowKey: item.id,
        itemId: item.id,
        shippedQuantity: item.shippedQuantity,
        alreadyReceived: item.receivedQuantity,
        legacyReservedQuantity: (item.damagedQuantity || 0) + (item.shortageQuantity || 0),
        toReceive: Math.max(0, item.shippedQuantity - item.receivedQuantity - (item.damagedQuantity || 0) - (item.shortageQuantity || 0)),
        itemCode: item.itemCode,
        itemName: item.itemName,
        unitOfMeasure: item.unitOfMeasure,
        sourceLocationName: item.sourceLocationName,
        destinationLocationName: item.destinationLocationName
      }]);
      setReceiveQuantities(quantities);
    } catch (err) {
      if (sequence !== loadSequence.current) return;
      console.error('Error loading transfer:', err);
      toast({ title: 'Error', description: getInventoryTransferProblemMessage(err, 'Failed to load transfer receipt bins'), variant: 'destructive' });
    } finally {
      if (sequence === loadSequence.current) setLoading(false);
    }
  };

  const updateReceiveQuantity = (rowKey: string, qty: number) => {
    setReceiveQuantities((values) => values.map((item) => {
      if (item.rowKey !== rowKey) return item;
      const availableToReceive = item.outstandingQuantity ?? Math.max(0, item.shippedQuantity - item.alreadyReceived - item.legacyReservedQuantity);
      return { ...item, toReceive: Math.max(0, Math.min(Number.isFinite(qty) ? qty : 0, availableToReceive)) };
    }));
  };

  const handleReceive = async () => {
    if (!transferId || !transfer) return;
    if (transfer.requiresTransitReconciliation) return;
    
    // Filter items with quantity to receive
    const grouped = new Map<string, ReceiveTransferItemDto>();
    for (const item of receiveQuantities.filter(row => row.toReceive > 0)) {
      if (item.dispatchAllocationId && !destinationLocations.some(location => location.id === item.destinationLocationId && location.id !== item.sourceLocationId)) {
        toast({ title: 'Validation', description: `Select a destination bin for ${item.itemCode || item.itemName}.`, variant: 'destructive' });
        return;
      }
      if (Math.abs(item.toReceive - Number(item.toReceive.toFixed(4))) > 1e-8) {
        toast({ title: 'Validation', description: 'Enter receiving quantities with at most four decimal places.', variant: 'destructive' });
        return;
      }
      const aggregate = grouped.get(item.itemId) || { id: item.itemId, receivedQuantity: 0 };
      aggregate.receivedQuantity = Number((aggregate.receivedQuantity + item.toReceive).toFixed(4));
      if (item.dispatchAllocationId) {
        aggregate.allocations = [...(aggregate.allocations || []), {
          dispatchAllocationId: item.dispatchAllocationId, receivedQuantity: Number(item.toReceive.toFixed(4)), destinationLocationId: item.destinationLocationId!,
        }];
      }
      grouped.set(item.itemId, aggregate);
    }
    const itemsToReceive = [...grouped.values()];

    if (itemsToReceive.length === 0) {
      toast({ title: 'Warning', description: 'Please enter quantities to receive', variant: 'destructive' });
      return;
    }

    try {
      setSaving(true);
      const fingerprint = JSON.stringify({ itemsToReceive, notes });
      if (mutationKeyRef.current?.fingerprint !== fingerprint) mutationKeyRef.current = { fingerprint, key: crypto.randomUUID() };
      const idempotencyKey = mutationKeyRef.current.key;
      await inventoryManagementService.receiveTransfer(transferId, {
        rowVersion: transfer.rowVersion,
        idempotencyKey,
        correlationId: `transfer-receipt:${transferId}:${idempotencyKey}`,
        comment: notes.trim() || undefined,
      }, itemsToReceive);
      toast({ title: 'Success', description: 'Transfer received successfully' });
      onSuccess();
      onOpenChange(false);
    } catch (err: any) {
      console.error('Error receiving transfer:', err);
      toast({ 
        title: 'Error', 
        description: getInventoryTransferProblemMessage(err, 'Failed to receive transfer'),
        variant: 'destructive' 
      });
    } finally {
      setSaving(false);
    }
  };

  const handlePrintGRN = () => {
    if (!transfer || !transferId) return;
    setDocumentPreview({
      title: `Goods Received Note ${transfer.transferNumber}`,
      fileName: `GRN-${transferId}.pdf`,
      contentType: 'application/pdf',
      repositoryPath: `/api/inventory/transfers/${encodeURIComponent(transferId)}/grn`,
      sourceLabel: 'Inventory transfer',
    });
  };

  const handlePrintShipmentNote = () => {
    if (!transfer || !transferId) return;
    setDocumentPreview({
      title: `Shipment Note ${transfer.transferNumber}`,
      fileName: `ShipmentNote-${transferId}.pdf`,
      contentType: 'application/pdf',
      repositoryPath: `/api/inventory/transfers/${encodeURIComponent(transferId)}/shipment-note`,
      sourceLabel: 'Inventory transfer',
    });
  };

  const totalToReceive = receiveQuantities.reduce((sum, q) => sum + q.toReceive, 0);
  const hasAllocations = receiveQuantities.some(item => item.dispatchAllocationId);
  const sourceBins = new Map((transfer?.items || []).map(item => [item.sourceLocationId || item.sourceLocationName || '', item.sourceLocationName]));
  const destinationBins = new Map((transfer?.items || []).map(item => [item.destinationLocationId || item.destinationLocationName || '', item.destinationLocationName]));
  const dispatchSourceNames = [...new Set(receiveQuantities.map(item => item.sourceLocationName).filter(Boolean))];
  const selectedDestinationNames = [...new Set(receiveQuantities.map(item => destinationLocations.find(location => location.id === item.destinationLocationId)?.locationCode).filter(Boolean))];
  const commonSourceBin = hasAllocations
    ? (dispatchSourceNames.length === 1 ? dispatchSourceNames[0] : 'Multiple source bins')
    : sourceBins.size === 1 ? ([...sourceBins.values()][0] || 'Not specified') : 'Multiple bins — use Columns';
  const commonDestinationBin = hasAllocations
    ? (receiveQuantities.some(item => item.toReceive > 0 && !item.destinationLocationId) ? 'Select below'
      : selectedDestinationNames.length === 1 ? selectedDestinationNames[0] : 'Multiple destination bins')
    : destinationBins.size === 1 ? ([...destinationBins.values()][0] || 'Not specified') : 'Multiple bins — use Columns';
  const totalLegacyReserved = receiveQuantities.reduce((sum, q) => sum + q.legacyReservedQuantity, 0);
  const totalShipped = receiveQuantities.reduce((sum, q) => sum + q.shippedQuantity, 0);

  return (
    <>
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className={`flex w-[calc(100vw-32px)] min-w-0 flex-col overflow-hidden ${fullPage ? 'h-[calc(100dvh-32px)] max-h-[calc(100dvh-32px)] max-w-[calc(100vw-32px)]' : 'max-h-[90vh] max-w-[1100px]'}`}>
        <DialogHeader>
          <div className="flex items-center justify-between gap-3 pr-8">
          <DialogTitle className="flex items-center gap-2">
            <Download className="h-5 w-5" />
            Receive Transfer {transfer?.transferNumber}
          </DialogTitle>
          <Button type="button" variant="outline" size="sm" onClick={() => setFullPage(value => !value)} aria-pressed={fullPage}>
            {fullPage ? <Minimize2 className="mr-2 h-4 w-4" /> : <Maximize2 className="mr-2 h-4 w-4" />}
            {fullPage ? 'Restore' : 'Full page'}
          </Button>
          </div>
          <DialogDescription>
            Receive only items in good condition. Unreceived quantities remain outstanding.
          </DialogDescription>
        </DialogHeader>

        {loading ? (
          <div className="flex items-center justify-center py-8">
            <Loader2 className="h-8 w-8 animate-spin" />
          </div>
        ) : transfer ? (
          <div className="min-h-0 min-w-0 flex-1 space-y-4 overflow-auto pr-1">
            {transfer.requiresTransitReconciliation && <p role="alert" className="rounded border border-amber-300 p-2 text-sm">This historical transfer needs transit reconciliation before dispatch or receipt.</p>}
            {/* Transfer Info */}
            <div className="grid grid-cols-3 gap-4 p-4 bg-muted rounded-lg">
              <div>
                <span className="text-sm text-muted-foreground">From:</span>
                <p className="font-medium">{transfer.sourceWarehouseName}</p>
                <p className="text-sm text-muted-foreground">From Bin: {commonSourceBin}</p>
              </div>
              <div>
                <span className="text-sm text-muted-foreground">To:</span>
                <p className="font-medium">{transfer.destinationWarehouseName}</p>
                <p className="text-sm text-muted-foreground">To Bin: {commonDestinationBin}</p>
              </div>
              <div>
                <span className="text-sm text-muted-foreground">Tracking #:</span>
                <p className="font-medium">{transfer.trackingNumber || 'N/A'}</p>
              </div>
            </div>

            {totalLegacyReserved > 0 && (
              <div className="flex items-center gap-2 p-3 bg-yellow-50 border border-yellow-200 rounded-lg text-yellow-800">
                <AlertTriangle className="h-5 w-5" />
                <span className="text-sm">{totalLegacyReserved.toFixed(2)} previously reported damaged or missing. Review these quantities in History before receiving them.</span>
              </div>
            )}

            {/* Items Table */}
            <div className="flex items-center justify-between gap-2">
              <h3 className="text-sm font-semibold">Items to receive</h3>
              <DropdownMenu>
                <DropdownMenuTrigger asChild><Button type="button" variant="outline" size="sm"><Columns3 className="mr-2 h-4 w-4" />Columns</Button></DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuLabel>Optional bin columns</DropdownMenuLabel>
                  <DropdownMenuCheckboxItem checked={binColumns.source} onSelect={event => event.preventDefault()} onCheckedChange={checked => setBinColumns(previous => ({ ...previous, source: checked === true }))}>From Bin</DropdownMenuCheckboxItem>
                  <DropdownMenuCheckboxItem checked={binColumns.destination} onSelect={event => event.preventDefault()} onCheckedChange={checked => setBinColumns(previous => ({ ...previous, destination: checked === true }))}>To Bin</DropdownMenuCheckboxItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </div>
            <div className="overflow-x-auto border rounded-lg">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Item Code</TableHead>
                    <TableHead>Description</TableHead>
                    {hasAllocations && <TableHead>Dispatch / destination bin</TableHead>}
                    {binColumns.source && <TableHead>From Bin</TableHead>}
                    {binColumns.destination && <TableHead>To Bin</TableHead>}
                    <TableHead>UoM</TableHead>
                    <TableHead className="text-right">Shipped</TableHead>
                    <TableHead className="text-right">Already Received</TableHead>
                    <TableHead className="text-right">Remaining</TableHead>
                    <TableHead className="text-right w-28">Qty to Receive</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {receiveQuantities.map((item) => {
                    const remaining = item.outstandingQuantity ?? Math.max(0, item.shippedQuantity - item.alreadyReceived);
                    const availableToReceive = Math.max(0, remaining - item.legacyReservedQuantity);
                    return (
                        <TableRow key={item.rowKey}>
                          <TableCell className="font-mono text-sm">{item.itemCode}</TableCell>
                          <TableCell>{item.itemName}</TableCell>
                          {hasAllocations && <TableCell className="min-w-52 space-y-1 py-2">
                            <div className="text-xs text-muted-foreground">{item.sourceLocationName} {item.carrierName ? `· ${item.carrierName}` : ''} {item.vehicleNumber ? `· ${item.vehicleNumber}` : ''}</div>
                            <select aria-label={`Destination bin ${item.itemCode} ${item.dispatchAllocationId}`} value={item.destinationLocationId || ''}
                              className="h-8 w-full rounded border bg-background px-1 text-sm" onChange={event => setReceiveQuantities(previous => previous.map(row => row.rowKey === item.rowKey ? { ...row, destinationLocationId: event.target.value } : row))}>
                              <option value="">Select destination bin</option>
                              {destinationLocations.filter(location => location.id !== item.sourceLocationId).map(location => <option key={location.id} value={location.id}>{location.locationCode}{location.name ? ` · ${location.name}` : ''}</option>)}
                            </select>
                          </TableCell>}
                          {binColumns.source && <TableCell className="text-sm">{item.sourceLocationName || <span className="text-muted-foreground">-</span>}</TableCell>}
                          {binColumns.destination && <TableCell className="text-sm">{item.destinationLocationName || <span className="text-muted-foreground">-</span>}</TableCell>}
                          <TableCell>{item.unitOfMeasure}</TableCell>
                          <TableCell className="text-right">{item.shippedQuantity.toFixed(2)}</TableCell>
                          <TableCell className="text-right"><Badge variant="secondary">{item.alreadyReceived.toFixed(2)}</Badge></TableCell>
                          <TableCell className="text-right">{remaining.toFixed(2)}</TableCell>
                          <TableCell><Input aria-label={`Qty to receive ${item.itemCode}${item.dispatchAllocationId ? ` ${item.dispatchAllocationId}` : ''}`} type="number" min="0" max={availableToReceive} step="0.01" value={item.toReceive} onChange={(event) => updateReceiveQuantity(item.rowKey, Number(event.target.value || 0))} className="w-24 text-right" disabled={availableToReceive <= 0} /></TableCell>
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
                placeholder="Add a note"
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

        <DialogFooter className="flex shrink-0 flex-wrap gap-2 border-t pt-3 sm:justify-between">
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
            <Button onClick={handleReceive} disabled={loading || !transfer || transfer.requiresTransitReconciliation || saving || totalToReceive <= 0}>
              {saving && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}
              <Download className="h-4 w-4 mr-2" />
              Receive Items
            </Button>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
    <CentralDocumentViewerDialog file={documentPreview} open={open && Boolean(documentPreview)}
      onOpenChange={(previewOpen) => { if (!previewOpen) setDocumentPreview(null); }} enableAnnotations={false} />
    </>
  );
}
