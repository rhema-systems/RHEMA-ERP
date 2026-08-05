'use client';

import React, { useEffect, useState } from 'react';
import { format } from 'date-fns';
import { CheckCircle, Download } from 'lucide-react';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Card, CardContent } from '@/components/ui/card';
import {
  inventoryRequisitionService,
  InventoryRequisitionDetailDto,
  ReturnRequisitionDto,
  RequisitionStatusMap,
  InventoryControlEvidenceRequest,
  InventoryReturnVoucherDto,
} from '@/services/inventoryRequisitionService';
import { documentManagementService, CentralDocumentRecord } from '@/services/document-management.service';
import { useToast } from '@/hooks/use-toast';

interface ReturnRequisitionDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  requisitionId: string | null;
  onSuccess: () => void;
}

interface ReturnItemState {
  itemId: string;
  itemCode: string;
  itemName: string;
  issuedQuantity: number;
  returningQuantity: number;
  unitOfMeasure: string;
  locationId?: string;
  locationName?: string;
  lotNumber?: string;
  batchNumber?: string;
  serialNumber?: string;
  manufactureDate?: string;
  expiryDate?: string;
  inventoryTrackingExceptionId?: string;
}

const normalizeStatus = (status: number | string | undefined): number => {
  if (status === undefined || status === null) return 0;
  if (typeof status === 'number') return status;
  const statusMap: Record<string, number> = {
    Draft: 1, Submitted: 2, Approved: 3, InProgress: 4,
    PartiallyIssued: 5, Issued: 6, Completed: 7, Cancelled: 8, Rejected: 9,
  };
  return statusMap[status] || 0;
};

export function ReturnRequisitionDialog({ open, onOpenChange, requisitionId, onSuccess }: ReturnRequisitionDialogProps) {
  const { toast } = useToast();
  const [loading, setLoading] = useState(false);
  const [processing, setProcessing] = useState(false);
  const [requisition, setRequisition] = useState<InventoryRequisitionDetailDto | null>(null);
  const [returnItems, setReturnItems] = useState<ReturnItemState[]>([]);
  const [notes, setNotes] = useState('');
  const [reasonCodes, setReasonCodes] = useState<Record<string, string>>({});
  const [reasonCode, setReasonCode] = useState('UNUSED');
  const [reason, setReason] = useState('');
  const [dmsRecords, setDmsRecords] = useState<CentralDocumentRecord[]>([]);
  const [evidence, setEvidence] = useState<InventoryControlEvidenceRequest[]>([]);
  const [vouchers, setVouchers] = useState<InventoryReturnVoucherDto[]>([]);

  useEffect(() => {
    if (open && requisitionId) {
      void loadRequisition();
    }
  }, [open, requisitionId]);

  const loadRequisition = async () => {
    if (!requisitionId) return;
    try {
      setLoading(true);
      const [detail, reasons, records, returnVouchers] = await Promise.all([
        inventoryRequisitionService.getById(requisitionId),
        inventoryRequisitionService.getReturnReasons(),
        documentManagementService.getRecords(),
        inventoryRequisitionService.getReturnVouchers(requisitionId),
      ]);
      setRequisition(detail);
      setReasonCodes(reasons);
      setDmsRecords(records.filter((record) => record.lifecycleStatus === 'Active' && record.versionStatus === 'Published' && Boolean(record.currentVersion)));
      setVouchers(returnVouchers);
      setReturnItems(detail.items.map((item) => ({
        itemId: item.id,
        itemCode: item.itemCode,
        itemName: item.itemName,
        issuedQuantity: item.issuedQuantity || 0,
        returningQuantity: 0,
        unitOfMeasure: item.unitOfMeasure,
        locationId: item.locationId || detail.locationId,
        locationName: item.locationName || detail.locationName,
        lotNumber: item.lotNumber,
        batchNumber: item.batchNumber,
        serialNumber: item.serialNumber,
        manufactureDate: item.manufactureDate,
        expiryDate: item.expiryDate,
        inventoryTrackingExceptionId: item.inventoryTrackingExceptionId,
      })));
      setNotes('');
      setReasonCode('UNUSED');
      setReason('');
      setEvidence([]);
    } catch (error) {
      console.error('Error loading requisition for return', error);
      toast({ title: 'Error', description: 'Failed to load requisition', variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  };

  const runVoucherAction = async (voucher: InventoryReturnVoucherDto, action: 'approve' | 'reject' | 'post' | 'reverse') => {
    const comment = (reason || notes).trim();
    if (action !== 'post' && !comment) {
      toast({ title: 'Comment required', description: 'Enter a detailed reason/comment before this controlled action.', variant: 'destructive' });
      return;
    }
    try {
      setProcessing(true);
      const updated = action === 'approve' ? await inventoryRequisitionService.decideReturnVoucher(voucher, true, comment)
        : action === 'reject' ? await inventoryRequisitionService.decideReturnVoucher(voucher, false, comment)
          : action === 'post' ? await inventoryRequisitionService.postReturnVoucher(voucher)
            : await inventoryRequisitionService.reverseReturnVoucher(voucher, comment);
      setVouchers((items) => items.map((item) => item.id === updated.id ? updated : item));
      toast({ title: 'Return control updated', description: `${updated.voucherNumber} is now ${updated.status}.` });
      onSuccess();
    } catch (error) {
      toast({ title: 'Action blocked', description: error instanceof Error ? error.message : 'The controlled return action failed.', variant: 'destructive' });
    } finally {
      setProcessing(false);
    }
  };

  const downloadVoucher = async (voucher: InventoryReturnVoucherDto) => {
    try {
      const blob = await inventoryRequisitionService.downloadReturnVoucher(voucher.id);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `${voucher.voucherNumber}.pdf`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      toast({ title: 'Download blocked', description: error instanceof Error ? error.message : 'Unable to render the Store Return Voucher.', variant: 'destructive' });
    }
  };

  const addEvidence = async (recordId: string) => {
    try {
      const detail = await documentManagementService.getRecord(recordId);
      if (!detail) throw new Error('The DMS record could not be loaded.');
      const version = detail.versions.find((item) => item.versionNumber === detail.record.currentVersion && item.status === 'Published' && item.fileUploadRecordId);
      if (!version) throw new Error('Select a central-DMS record with a current published repository version.');
      setEvidence((items) => items.some((item) => item.centralDocumentVersionId === version.id) ? items : [...items, {
        centralDocumentVersionId: version.id,
        evidenceReference: `${detail.record.documentReference} / ${version.versionNumber}`,
      }]);
    } catch (error) {
      toast({ title: 'Evidence unavailable', description: error instanceof Error ? error.message : 'Unable to link DMS evidence', variant: 'destructive' });
    }
  };

  const setQuantity = (itemId: string, quantity: number) => {
    setReturnItems((items) => items.map((item) => {
      if (item.itemId !== itemId) return item;
      const valid = Math.max(0, Math.min(quantity, item.issuedQuantity));
      return { ...item, returningQuantity: valid };
    }));
  };

  const returnAll = () => {
    setReturnItems((items) => items.map((item) => ({ ...item, returningQuantity: item.issuedQuantity })));
  };

  const handleReturn = async () => {
    if (!requisitionId || !requisition) {
      return;
    }

    const items = returnItems.filter((item) => item.returningQuantity > 0);
    if (items.length === 0) {
      toast({ title: 'Validation Error', description: 'Enter at least one return quantity', variant: 'destructive' });
      return;
    }
    if (!reason.trim()) {
      toast({ title: 'Validation Error', description: 'Enter the detailed reason for this return', variant: 'destructive' });
      return;
    }
    if ((reasonCode === 'DEFECTIVE' || reasonCode === 'OTHER') && evidence.length === 0) {
      toast({ title: 'Evidence required', description: 'Link a current published central-DMS document for this return reason', variant: 'destructive' });
      return;
    }

    try {
      setProcessing(true);
      const dto: ReturnRequisitionDto = {
        items: items.map((item) => ({
          itemId: item.itemId,
          returnedQuantity: item.returningQuantity,
          locationId: item.locationId,
          lotNumber: item.lotNumber,
          batchNumber: item.batchNumber,
          serialNumber: item.serialNumber,
          manufactureDate: item.manufactureDate,
          expiryDate: item.expiryDate,
          inventoryTrackingExceptionId: item.inventoryTrackingExceptionId,
        })),
        reasonCode,
        reason: reason.trim(),
        notes: notes || undefined,
        idempotencyKey: crypto.randomUUID(),
        correlationId: `return-ui:${crypto.randomUUID()}`,
        rowVersion: requisition.rowVersion,
        evidence,
      };
      const voucher = await inventoryRequisitionService.returnItems(requisitionId, dto);
      toast({ title: 'Return submitted', description: `${voucher.voucherNumber} is pending independent approval; stock has not changed.` });
      onSuccess();
      onOpenChange(false);
    } catch (error: unknown) {
      console.error('Error returning requisition items', error);
      const errorMessage = error instanceof Error ? error.message : 'Failed to return items';
      toast({ title: 'Error', description: errorMessage, variant: 'destructive' });
    } finally {
      setProcessing(false);
    }
  };

  const totalReturning = returnItems.reduce((sum, item) => sum + item.returningQuantity, 0);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>
            Return Issued Items
            {requisition ? <span className="ml-2 text-muted-foreground">#{requisition.requisitionNumber}</span> : null}
          </DialogTitle>
        </DialogHeader>

        {loading ? (
          <div className="flex justify-center py-8"><div className="h-8 w-8 animate-spin rounded-full border-b-2 border-primary"></div></div>
        ) : requisition ? (
          <div className="space-y-4">
            <Card>
              <CardContent className="pt-4">
                <div className="grid grid-cols-3 gap-4 text-sm">
                  <div><span className="text-muted-foreground">Department:</span> <span className="font-medium">{requisition.departmentName}</span></div>
                  <div><span className="text-muted-foreground">Warehouse:</span> <span className="font-medium">{requisition.warehouseName}</span></div>
                  <div><span className="text-muted-foreground">Status:</span> <Badge variant="outline">{RequisitionStatusMap[normalizeStatus(requisition.status)] || requisition.status}</Badge></div>
                  <div><span className="text-muted-foreground">Request Date:</span> <span className="font-medium">{requisition.requestDateFormatted || format(new Date(requisition.requestDate), 'dd/MM/yyyy')}</span></div>
                  <div><span className="text-muted-foreground">Issued Date:</span> <span className="font-medium">{requisition.issuedDate ? format(new Date(requisition.issuedDate), 'dd/MM/yyyy') : 'N/A'}</span></div>
                  <div><span className="text-muted-foreground">Project:</span> <span className="font-medium">{requisition.projectCode || 'Not linked'}</span></div>
                  <div><span className="text-muted-foreground">Location:</span> <span className="font-medium">{requisition.locationName || 'Warehouse level'}</span></div>
                </div>
              </CardContent>
            </Card>

            <div className="flex items-center justify-between">
              <h4 className="font-medium">Issued Items Available For Return</h4>
              <Button variant="outline" size="sm" onClick={returnAll}>Return All Issued</Button>
            </div>

            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Item</TableHead>
                  <TableHead className="text-center">Issued Qty</TableHead>
                  <TableHead className="text-center">Return Qty</TableHead>
                  <TableHead>Location</TableHead>
                  <TableHead>Tracking</TableHead>
                  <TableHead>UoM</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {returnItems.map((item) => (
                  <TableRow key={item.itemId}>
                    <TableCell>
                      <div className="font-medium">{item.itemCode}</div>
                      <div className="text-sm text-muted-foreground">{item.itemName}</div>
                    </TableCell>
                    <TableCell className="text-center">
                      {item.issuedQuantity > 0 ? <Badge variant="outline">{item.issuedQuantity}</Badge> : <Badge variant="secondary"><CheckCircle className="mr-1 h-3 w-3" />No issued qty</Badge>}
                    </TableCell>
                    <TableCell className="text-center">
                      <Input
                        type="number"
                        min="0"
                        max={item.issuedQuantity}
                        value={item.returningQuantity}
                        onChange={(e) => setQuantity(item.itemId, Number(e.target.value || '0'))}
                        className="w-24 text-center"
                        disabled={item.issuedQuantity <= 0}
                      />
                    </TableCell>
                    <TableCell>{item.locationName || requisition.locationName || 'Warehouse level'}</TableCell>
                    <TableCell className="min-w-[280px]"><div className="grid grid-cols-3 gap-1"><Input placeholder="Lot" value={item.lotNumber || ''} onChange={event => setReturnItems(values => values.map(value => value.itemId === item.itemId ? { ...value, lotNumber: event.target.value || undefined } : value))} /><Input placeholder="Batch" value={item.batchNumber || ''} onChange={event => setReturnItems(values => values.map(value => value.itemId === item.itemId ? { ...value, batchNumber: event.target.value || undefined } : value))} /><Input placeholder="Serial" value={item.serialNumber || ''} onChange={event => setReturnItems(values => values.map(value => value.itemId === item.itemId ? { ...value, serialNumber: event.target.value || undefined } : value))} /><Input className="col-span-3" placeholder="Approved exception ID (if required)" value={item.inventoryTrackingExceptionId || ''} onChange={event => setReturnItems(values => values.map(value => value.itemId === item.itemId ? { ...value, inventoryTrackingExceptionId: event.target.value || undefined } : value))} /></div></TableCell>
                    <TableCell>{item.unitOfMeasure}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>

            <div className="space-y-2">
              <Label>Controlled return reason</Label>
              <Select value={reasonCode} onValueChange={setReasonCode}>
                <SelectTrigger><SelectValue placeholder="Select reason" /></SelectTrigger>
                <SelectContent>{Object.entries(reasonCodes).map(([code, label]) => <SelectItem key={code} value={code}>{label}</SelectItem>)}</SelectContent>
              </Select>
              <Textarea rows={3} value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Required detailed reason" />
            </div>

            <div className="space-y-2">
              <Label>Central DMS evidence {reasonCode === 'DEFECTIVE' || reasonCode === 'OTHER' ? '(required)' : '(optional)'}</Label>
              <Select onValueChange={(value) => void addEvidence(value)}>
                <SelectTrigger><SelectValue placeholder="Link a current published DMS record" /></SelectTrigger>
                <SelectContent>{dmsRecords.map((record) => <SelectItem key={record.id} value={record.id}>{record.documentReference} · {record.title}</SelectItem>)}</SelectContent>
              </Select>
              {evidence.map((item) => <div key={item.centralDocumentVersionId} className="flex items-center justify-between rounded-md border px-3 py-2 text-sm"><span>{item.evidenceReference}</span><Button type="button" size="sm" variant="ghost" onClick={() => setEvidence((values) => values.filter((value) => value.centralDocumentVersionId !== item.centralDocumentVersionId))}>Remove</Button></div>)}
            </div>

            <div className="space-y-2">
              <Label>Additional notes</Label>
              <Textarea rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="Optional operational notes" />
            </div>

            <div className="rounded-md bg-muted p-3 text-sm">
              Total return quantity: <span className="font-medium">{totalReturning}</span>
            </div>

            <div className="space-y-2">
              <h4 className="font-medium">Store Return Voucher register</h4>
              {vouchers.length === 0 ? <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">No controlled return has been submitted for this requisition.</div> : vouchers.map((voucher) => (
                <div key={voucher.id} className="flex flex-wrap items-center justify-between gap-3 rounded-md border p-3 text-sm">
                  <div><div className="font-medium">{voucher.voucherNumber}</div><div className="text-muted-foreground">{voucher.reasonCode} · {voucher.lines.length} line(s) · {voucher.totalValue.toFixed(2)}</div></div>
                  <div className="flex items-center gap-2"><Badge variant="outline">{voucher.status}</Badge>
                    <Button size="sm" variant="ghost" onClick={() => void downloadVoucher(voucher)} title="Download Store Return Voucher"><Download className="h-4 w-4" /></Button>
                    {voucher.status === 'PendingApproval' ? <><Button size="sm" onClick={() => void runVoucherAction(voucher, 'approve')} disabled={processing}>Approve</Button><Button size="sm" variant="destructive" onClick={() => void runVoucherAction(voucher, 'reject')} disabled={processing}>Reject</Button></> : null}
                    {voucher.status === 'Approved' ? <Button size="sm" onClick={() => void runVoucherAction(voucher, 'post')} disabled={processing}>Post stock</Button> : null}
                    {voucher.status === 'Posted' ? <Button size="sm" variant="outline" onClick={() => void runVoucherAction(voucher, 'reverse')} disabled={processing}>Reverse</Button> : null}
                  </div>
                </div>
              ))}
            </div>
          </div>
        ) : null}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button onClick={() => void handleReturn()} disabled={processing || loading || totalReturning <= 0}>
            {processing ? 'Submitting...' : 'Submit Return For Approval'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
