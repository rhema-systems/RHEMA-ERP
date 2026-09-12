'use client';

import React, { useEffect, useState } from 'react';
import { format } from 'date-fns';
import { CheckCircle, Download } from 'lucide-react';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Card, CardContent } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useAuth } from '@/hooks/use-auth';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';
import { canDecideInventoryRecord, canPostInventoryRecord } from '@/lib/inventory-approval-actions';
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
import {
  InventoryTrackingExceptionSelect,
  useAvailableInventoryTrackingExceptions,
} from '@/components/inventory/InventoryTrackingExceptionSelect';

interface ReturnRequisitionDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  requisitionId: string | null;
  onSuccess: () => void;
}

interface ReturnItemState {
  itemId: string;
  inventoryItemId: string;
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
  const { user, hasPermission } = useAuth();
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
  const [decision, setDecision] = useState<{ voucher: InventoryReturnVoucherDto; action: 'approve' | 'reject' | 'reverse' } | null>(null);
  const [decisionComment, setDecisionComment] = useState('');
  const [actionError, setActionError] = useState('');
  const [reviewMode, setReviewMode] = useState(false);
  const canRequestReturn = Boolean(user?.id && hasPermission('procurement.inventory.issue'));
  const isReviewing = reviewMode || !canRequestReturn;
  const canActOnVoucher = (voucher: InventoryReturnVoucherDto) => Boolean(
    user?.id && voucher.requestedById && user.id.toLowerCase() !== voucher.requestedById.toLowerCase()
    && hasPermission('procurement.inventory.adjust.approve')
  );
  const trackingExceptions = useAvailableInventoryTrackingExceptions(open && Boolean(requisitionId));

  useEffect(() => {
    setDecision(null);
    setDecisionComment('');
    setActionError('');
    if (open && requisitionId) {
      void loadRequisition();
    }
  }, [open, requisitionId, user?.id]);

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
      // Keep an approval session in review mode even after its status changes.
      // A broad permission grant must not mix a new request with a decision.
      setReviewMode(returnVouchers.some((voucher) => canActOnVoucher(voucher)
        && ['PendingApproval', 'Approved', 'ReadyToPost'].includes(voucher.status)));
      setReturnItems(detail.items.map((item) => ({
        itemId: item.id,
        inventoryItemId: item.inventoryItemId,
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

  const openDecision = (voucher: InventoryReturnVoucherDto, action: 'approve' | 'reject' | 'reverse') => {
    setDecisionComment('');
    setActionError('');
    setDecision({ voucher, action });
  };

  const runVoucherAction = async (voucher: InventoryReturnVoucherDto, action: 'approve' | 'reject' | 'post' | 'reverse', comment = '') => {
    const allowed = action === 'post'
      ? canPostInventoryRecord(voucher, user?.id, hasPermission('procurement.inventory.adjust.approve'))
      : action === 'approve' || action === 'reject'
        ? canDecideInventoryRecord(voucher, user?.id, hasPermission('procurement.inventory.adjust.approve'))
        : canActOnVoucher(voucher);
    if (!allowed) {
      setActionError('This action requires an authorized user other than the return requester.');
      return false;
    }
    if ((action === 'reject' || action === 'reverse') && !comment.trim()) {
      setActionError(`Enter a ${action === 'reject' ? 'rejection' : 'reversal'} reason.`);
      return false;
    }
    try {
      setActionError('');
      setProcessing(true);
      const updated = action === 'approve' ? await inventoryRequisitionService.decideReturnVoucher(voucher, true, comment)
        : action === 'reject' ? await inventoryRequisitionService.decideReturnVoucher(voucher, false, comment)
          : action === 'post' ? await inventoryRequisitionService.postReturnVoucher(voucher)
            : await inventoryRequisitionService.reverseReturnVoucher(voucher, comment);
      setVouchers((items) => items.map((item) => item.id === updated.id ? updated : item));
      toast({ title: 'Return control updated', description: `${updated.voucherNumber} is now ${updated.status}.` });
      onSuccess();
      return true;
    } catch (error) {
      const message = getProcurementProblemMessage(error, 'The return action failed.');
      setActionError(message);
      toast({ title: 'Action blocked', description: message, variant: 'destructive' });
      return false;
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
    if (!requisitionId || !requisition || !canRequestReturn || isReviewing) {
      return;
    }

    const items = returnItems.filter((item) => item.returningQuantity > 0);
    if (items.length === 0) {
      toast({ title: 'Validation Error', description: 'Enter at least one return quantity', variant: 'destructive' });
      return;
    }
    if (!reasonCode || !reasonCodes[reasonCode]) {
      toast({ title: 'Validation Error', description: 'Select a return reason', variant: 'destructive' });
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
      toast({ title: 'Return submitted', description: voucher.approvalRequired === false
        ? `${voucher.voucherNumber} is ready for Post; stock has not changed.`
        : `${voucher.voucherNumber} is pending independent approval; stock has not changed.` });
      onSuccess();
      onOpenChange(false);
    } catch (error: unknown) {
      console.error('Error returning requisition items', error);
      const errorMessage = getProcurementProblemMessage(error, 'Failed to return items');
      setActionError(errorMessage);
      toast({ title: 'Error', description: errorMessage, variant: 'destructive' });
    } finally {
      setProcessing(false);
    }
  };

  const totalReturning = returnItems.reduce((sum, item) => sum + item.returningQuantity, 0);

  return (
    <>
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>
            {isReviewing ? 'Review Stock Returns' : 'Return Issued Items'}
            {requisition ? <span className="ml-2 text-muted-foreground">#{requisition.requisitionNumber}</span> : null}
          </DialogTitle>
          <DialogDescription className="sr-only">Return issued stock or review a saved return voucher.</DialogDescription>
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

            {!isReviewing ? <>
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
                    <TableCell className="min-w-[360px]"><div className="grid grid-cols-3 gap-1"><Input placeholder="Lot" value={item.lotNumber || ''} onChange={event => setReturnItems(values => values.map(value => value.itemId === item.itemId ? { ...value, lotNumber: event.target.value || undefined } : value))} /><Input placeholder="Batch" value={item.batchNumber || ''} onChange={event => setReturnItems(values => values.map(value => value.itemId === item.itemId ? { ...value, batchNumber: event.target.value || undefined } : value))} /><Input placeholder="Serial" value={item.serialNumber || ''} onChange={event => setReturnItems(values => values.map(value => value.itemId === item.itemId ? { ...value, serialNumber: event.target.value || undefined } : value))} /><div className="col-span-3"><InventoryTrackingExceptionSelect value={item.inventoryTrackingExceptionId} onValueChange={inventoryTrackingExceptionId => setReturnItems(values => values.map(value => value.itemId === item.itemId ? { ...value, inventoryTrackingExceptionId } : value))} exceptions={trackingExceptions.exceptions} loading={trackingExceptions.loading} error={trackingExceptions.error} onRetry={trackingExceptions.refresh} context={{ inventoryItemId: item.inventoryItemId, warehouseId: requisition.warehouseId, locationId: item.locationId, referenceId: requisition.id, lotNumber: item.lotNumber, batchNumber: item.batchNumber, serialNumber: item.serialNumber }} /></div></div></TableCell>
                    <TableCell>{item.unitOfMeasure}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>

            <div className="space-y-2">
              <Label htmlFor="return-reason-code">Return reason</Label>
              <Select value={reasonCode} onValueChange={setReasonCode}>
                <SelectTrigger id="return-reason-code"><SelectValue placeholder="Select reason" /></SelectTrigger>
                <SelectContent>{Object.entries(reasonCodes).map(([code, label]) => <SelectItem key={code} value={code}>{label}</SelectItem>)}</SelectContent>
              </Select>
              <Label htmlFor="return-details">Details (optional)</Label>
              <Textarea id="return-details" rows={3} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Add details if needed" />
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
            </> : null}

            <div className="space-y-2">
              <h4 className="font-medium">Store Return Voucher register</h4>
              {vouchers.length === 0 ? <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">No controlled return has been submitted for this requisition.</div> : vouchers.map((voucher) => (
                <div key={voucher.id} className="flex flex-wrap items-center justify-between gap-3 rounded-md border p-3 text-sm">
                  <div><div className="font-medium">{voucher.voucherNumber}</div><div className="text-muted-foreground">{voucher.reasonCode} · {voucher.lines.length} line(s) · {voucher.totalValue.toFixed(2)}</div></div>
                  <div className="flex items-center gap-2"><Badge variant="outline">{voucher.status}</Badge>
                    <Button size="sm" variant="ghost" onClick={() => void downloadVoucher(voucher)} title="Download Store Return Voucher"><Download className="h-4 w-4" /></Button>
                    {canDecideInventoryRecord(voucher, user?.id, hasPermission('procurement.inventory.adjust.approve')) ? <><Button size="sm" onClick={() => openDecision(voucher, 'approve')} disabled={processing}>Approve</Button><Button size="sm" variant="destructive" onClick={() => openDecision(voucher, 'reject')} disabled={processing}>Reject</Button></> : null}
                    {voucher.approvalRequired !== false && voucher.status === 'PendingApproval' && !canActOnVoucher(voucher) ? <span className="text-muted-foreground">Awaiting independent approval</span> : null}
                    {canPostInventoryRecord(voucher, user?.id, hasPermission('procurement.inventory.adjust.approve')) ? <Button size="sm" onClick={() => void runVoucherAction(voucher, 'post')} disabled={processing}>Post</Button> : null}
                    {voucher.status === 'Posted' && canActOnVoucher(voucher) ? <Button size="sm" variant="outline" onClick={() => openDecision(voucher, 'reverse')} disabled={processing}>Reverse</Button> : null}
                  </div>
                  {isReviewing ? <div className="w-full space-y-1 border-t pt-2">
                    <p>Requested by: {voucher.requestedByName}</p>
                    <p>Return reason: {reasonCodes[voucher.reasonCode] || voucher.reasonCode}</p>
                    {voucher.reason && voucher.reason !== reasonCodes[voucher.reasonCode] ? <p>Details: {voucher.reason}</p> : null}
                    {voucher.notes ? <p>Notes: {voucher.notes}</p> : null}
                    {voucher.lines.map((line) => <p key={line.id}>{line.itemCode} · {line.itemName} — return quantity: {line.quantity}</p>)}
                    {voucher.evidence?.map((item) => <p key={item.id}>Evidence: {item.evidenceReference}</p>)}
                  </div> : null}
                </div>
              ))}
            </div>
          </div>
        ) : null}

        {actionError && !decision ? <p role="alert" className="text-sm text-destructive">{actionError}</p> : null}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>{isReviewing ? 'Close' : 'Cancel'}</Button>
          {!isReviewing ? <Button onClick={() => void handleReturn()} disabled={processing || loading || totalReturning <= 0}>
            {processing ? 'Submitting...' : 'Submit return'}
          </Button> : null}
        </DialogFooter>
      </DialogContent>
    </Dialog>
    <ConfirmationDialog
      open={open && Boolean(decision)}
      onOpenChange={(value) => { if (!value && !processing) setDecision(null); }}
      title={decision?.action === 'approve' ? 'Approve return' : decision?.action === 'reject' ? 'Reject return' : 'Reverse return'}
      description={decision?.voucher.voucherNumber}
      confirmText={decision?.action === 'approve' ? 'Approve return' : decision?.action === 'reject' ? 'Reject return' : 'Reverse return'}
      variant={decision?.action === 'approve' ? 'default' : 'destructive'}
      isLoading={processing}
      confirmDisabled={decision?.action !== 'approve' && !decisionComment.trim()}
      onConfirm={() => decision ? runVoucherAction(decision.voucher, decision.action, decisionComment.trim()) : false}
    >
      <div className="space-y-2">
        <Label htmlFor="return-decision-comment">{decision?.action === 'approve' ? 'Comments (optional)' : 'Reason (required)'}</Label>
        <Textarea id="return-decision-comment" value={decisionComment} maxLength={1000} onChange={(event) => setDecisionComment(event.target.value)} />
        {actionError ? <p role="alert" className="text-sm text-destructive">{actionError}</p> : null}
      </div>
    </ConfirmationDialog>
    </>
  );
}
