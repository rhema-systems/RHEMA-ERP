'use client';

import React, { useState, useEffect, useRef } from 'react';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Download, FileText, Loader2, AlertTriangle } from 'lucide-react';
import {
  inventoryManagementService,
  InventoryTransferDetailDto, ReceiveTransferItemDto, InventoryTransferEvidenceRequest
} from '@/services/inventoryManagementService';
import { documentManagementService, CentralDocumentRecord } from '@/services/document-management.service';
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
  alreadyDamaged: number;
  alreadyShortage: number;
  toReceive: number;
  toDamaged: number;
  toShortage: number;
  reasonCode: string;
  reason: string;
  evidence: InventoryTransferEvidenceRequest[];
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
  const [reasonCodes, setReasonCodes] = useState<Record<string, string>>({});
  const [dmsRecords, setDmsRecords] = useState<CentralDocumentRecord[]>([]);
  const mutationKeyRef = useRef<{ fingerprint: string; key: string } | null>(null);

  useEffect(() => {
    if (open && transferId) {
      loadTransferDetails();
    } else {
      setTransfer(null);
      setReceiveQuantities([]);
      setNotes('');
      mutationKeyRef.current = null;
    }
  }, [open, transferId]);

  const loadTransferDetails = async () => {
    if (!transferId) return;
    try {
      setLoading(true);
      const [detail, reasons, records] = await Promise.all([
        inventoryManagementService.getInventoryTransferById(transferId),
        inventoryManagementService.getTransferDiscrepancyReasons(),
        documentManagementService.getRecords(),
      ]);
      setTransfer(detail);
      setReasonCodes(reasons);
      setDmsRecords(records.filter((record) => record.lifecycleStatus === 'Active' && record.versionStatus === 'Published' && Boolean(record.currentVersion)));
      
      // Initialize receive quantities from items
      const quantities: ReceiveQuantity[] = detail.items.map(item => ({
        itemId: item.id,
        shippedQuantity: item.shippedQuantity,
        alreadyReceived: item.receivedQuantity,
        alreadyDamaged: item.damagedQuantity || 0,
        alreadyShortage: item.shortageQuantity || 0,
        toReceive: Math.max(0, item.shippedQuantity - item.receivedQuantity - (item.damagedQuantity || 0) - (item.shortageQuantity || 0)),
        toDamaged: 0,
        toShortage: 0,
        reasonCode: '',
        reason: '',
        evidence: [],
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
    updateAccountedQuantity(itemId, 'toReceive', qty);
  };

  const updateAccountedQuantity = (itemId: string, field: 'toReceive' | 'toDamaged' | 'toShortage', qty: number) => {
    setReceiveQuantities((values) => values.map((item) => {
      if (item.itemId !== itemId) return item;
      const outstanding = item.shippedQuantity - item.alreadyReceived - item.alreadyDamaged - item.alreadyShortage;
      const other = (field === 'toReceive' ? 0 : item.toReceive) +
        (field === 'toDamaged' ? 0 : item.toDamaged) +
        (field === 'toShortage' ? 0 : item.toShortage);
      return { ...item, [field]: Math.max(0, Math.min(qty, Math.max(0, outstanding - other))) };
    }));
  };

  const updateDiscrepancy = (itemId: string, values: Partial<Pick<ReceiveQuantity, 'reasonCode' | 'reason'>>) =>
    setReceiveQuantities((items) => items.map((item) => item.itemId === itemId ? { ...item, ...values } : item));

  const addEvidence = async (itemId: string, recordId: string) => {
    try {
      const detail = await documentManagementService.getRecord(recordId);
      const version = detail?.versions.find((item) => item.versionNumber === detail.record.currentVersion && item.status === 'Published' && item.fileUploadRecordId);
      if (!detail || !version) throw new Error('Select a central-DMS record with a current published repository version.');
      const evidence = { centralDocumentVersionId: version.id, evidenceReference: `${detail.record.documentReference} / ${version.versionNumber}` };
      setReceiveQuantities((items) => items.map((item) => item.itemId !== itemId || item.evidence.some((value) => value.centralDocumentVersionId === version.id)
        ? item : { ...item, evidence: [...item.evidence, evidence] }));
    } catch (error) {
      toast({ title: 'Evidence unavailable', description: error instanceof Error ? error.message : 'Unable to link central-DMS evidence.', variant: 'destructive' });
    }
  };

  const handleReceive = async () => {
    if (!transferId || !transfer) return;
    
    // Filter items with quantity to receive
    const itemsToReceive: ReceiveTransferItemDto[] = receiveQuantities
      .filter(q => q.toReceive > 0 || q.toDamaged > 0 || q.toShortage > 0)
      .map(q => ({
        id: q.itemId,
        receivedQuantity: q.toReceive,
        damagedQuantity: q.toDamaged,
        shortageQuantity: q.toShortage,
        discrepancyReasonCode: q.toDamaged > 0 || q.toShortage > 0 ? q.reasonCode : undefined,
        discrepancyReason: q.toDamaged > 0 || q.toShortage > 0 ? q.reason.trim() : undefined,
        evidence: q.toDamaged > 0 || q.toShortage > 0 ? q.evidence : [],
      }));

    if (itemsToReceive.length === 0) {
      toast({ title: 'Warning', description: 'Please enter quantities to receive', variant: 'destructive' });
      return;
    }

    const invalidDiscrepancy = receiveQuantities.find((item) => (item.toDamaged > 0 || item.toShortage > 0) &&
      (!item.reasonCode || !item.reason.trim() || item.evidence.length === 0));
    if (invalidDiscrepancy) {
      toast({ title: 'Discrepancy control required', description: 'Every damage or shortage line requires a reason code, detailed reason, and current published central-DMS evidence.', variant: 'destructive' });
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
  const totalDamaged = receiveQuantities.reduce((sum, q) => sum + q.toDamaged, 0);
  const totalShortage = receiveQuantities.reduce((sum, q) => sum + q.toShortage, 0);
  const totalAccounted = totalToReceive + totalDamaged + totalShortage;
  const totalShipped = receiveQuantities.reduce((sum, q) => sum + q.shippedQuantity, 0);
  const hasVariance = receiveQuantities.some(q => {
    return q.toDamaged > 0 || q.toShortage > 0;
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
            <div className="overflow-x-auto border rounded-lg">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Item Code</TableHead>
                    <TableHead>Description</TableHead>
                    <TableHead>From Bin</TableHead>
                    <TableHead>To Bin</TableHead>
                    <TableHead>UoM</TableHead>
                    <TableHead className="text-right">Shipped</TableHead>
                    <TableHead className="text-right">Already Accounted</TableHead>
                    <TableHead className="text-right">Pending</TableHead>
                    <TableHead className="text-right w-28">Received</TableHead>
                    <TableHead className="text-right w-28">Damaged</TableHead>
                    <TableHead className="text-right w-28">Shortage</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {receiveQuantities.map((item) => {
                    const alreadyAccounted = item.alreadyReceived + item.alreadyDamaged + item.alreadyShortage;
                    const pending = Math.max(0, item.shippedQuantity - alreadyAccounted);
                    const hasLineDiscrepancy = item.toDamaged > 0 || item.toShortage > 0;
                    return (
                      <React.Fragment key={item.itemId}>
                        <TableRow>
                          <TableCell className="font-mono text-sm">{item.itemCode}</TableCell>
                          <TableCell>{item.itemName}</TableCell>
                          <TableCell className="text-sm">{item.sourceLocationName || <span className="text-muted-foreground">-</span>}</TableCell>
                          <TableCell className="text-sm">{item.destinationLocationName || <span className="text-muted-foreground">-</span>}</TableCell>
                          <TableCell>{item.unitOfMeasure}</TableCell>
                          <TableCell className="text-right">{item.shippedQuantity.toFixed(2)}</TableCell>
                          <TableCell className="text-right"><Badge variant="secondary">{alreadyAccounted.toFixed(2)}</Badge></TableCell>
                          <TableCell className="text-right">{pending.toFixed(2)}</TableCell>
                          <TableCell><Input type="number" min="0" step="0.01" value={item.toReceive} onChange={(event) => updateReceiveQuantity(item.itemId, Number(event.target.value || 0))} className="w-24 text-right" disabled={pending <= 0} /></TableCell>
                          <TableCell><Input type="number" min="0" step="0.01" value={item.toDamaged} onChange={(event) => updateAccountedQuantity(item.itemId, 'toDamaged', Number(event.target.value || 0))} className="w-24 text-right" disabled={pending <= 0} /></TableCell>
                          <TableCell><Input type="number" min="0" step="0.01" value={item.toShortage} onChange={(event) => updateAccountedQuantity(item.itemId, 'toShortage', Number(event.target.value || 0))} className="w-24 text-right" disabled={pending <= 0} /></TableCell>
                        </TableRow>
                        {hasLineDiscrepancy && (
                          <TableRow className="bg-amber-50/50">
                            <TableCell colSpan={11}>
                              <div className="grid gap-3 md:grid-cols-3">
                                <div className="space-y-1"><Label>Discrepancy reason code *</Label><Select value={item.reasonCode} onValueChange={(value) => updateDiscrepancy(item.itemId, { reasonCode: value })}><SelectTrigger><SelectValue placeholder="Select controlled reason" /></SelectTrigger><SelectContent>{Object.entries(reasonCodes).map(([code, label]) => <SelectItem key={code} value={code}>{label}</SelectItem>)}</SelectContent></Select></div>
                                <div className="space-y-1"><Label>Detailed reason *</Label><Input value={item.reason} onChange={(event) => updateDiscrepancy(item.itemId, { reason: event.target.value })} placeholder="State what was observed and verified" /></div>
                                <div className="space-y-1"><Label>Current published Central DMS evidence *</Label><Select onValueChange={(value) => void addEvidence(item.itemId, value)}><SelectTrigger><SelectValue placeholder="Link protected DMS evidence" /></SelectTrigger><SelectContent>{dmsRecords.map((record) => <SelectItem key={record.id} value={record.id}>{record.documentReference} · {record.title}</SelectItem>)}</SelectContent></Select></div>
                              </div>
                              <div className="mt-2 flex flex-wrap gap-2">{item.evidence.map((value) => <Badge key={value.centralDocumentVersionId} variant="outline" className="gap-2">{value.evidenceReference}<button type="button" aria-label={`Remove ${value.evidenceReference}`} onClick={() => setReceiveQuantities((items) => items.map((line) => line.itemId === item.itemId ? { ...line, evidence: line.evidence.filter((evidence) => evidence.centralDocumentVersionId !== value.centralDocumentVersionId) } : line))}>×</button></Badge>)}</div>
                            </TableCell>
                          </TableRow>
                        )}
                      </React.Fragment>
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
                <div><span className="text-sm text-muted-foreground">Damaged:</span><Badge variant={totalDamaged > 0 ? 'destructive' : 'secondary'} className="ml-2">{totalDamaged.toFixed(2)}</Badge></div>
                <div><span className="text-sm text-muted-foreground">Shortage:</span><Badge variant={totalShortage > 0 ? 'destructive' : 'secondary'} className="ml-2">{totalShortage.toFixed(2)}</Badge></div>
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
            <Button onClick={handleReceive} disabled={saving || totalAccounted <= 0}>
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
