 
'use client';

import React, { useState, useEffect, useMemo, useRef } from 'react';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from '@/components/ui/accordion';
import { Package, CheckCircle, Download, ClipboardCheck } from 'lucide-react';
import {
  inventoryRequisitionService,
  InventoryRequisitionDetailDto,
  InventoryIssueAccountingOptionsDto, InventoryIssueReceiverDto, InventoryIssueVoucherDto,
  IssueRequisitionDto, RequisitionStatusMap
} from '@/services/inventoryRequisitionService';
import { useToast } from '@/hooks/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { canIssueRequisition } from '@/lib/inventory-requisition-access';
import { inventoryManagementService, type WarehouseLocationDto } from '@/services/inventoryManagementService';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';
import { CentralDocumentViewerDialog, type CentralDocumentViewerFile } from '@/components/document-management/CentralDocumentViewerDialog';
import { format } from 'date-fns';
import {
  InventoryTrackingExceptionSelect,
  useAvailableInventoryTrackingExceptions,
} from '@/components/inventory/InventoryTrackingExceptionSelect';

interface IssueRequisitionDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  requisitionId: string | null;
  onSuccess: () => void;
}

interface IssueItemState {
  itemId: string;
  inventoryItemId: string;
  itemCode: string;
  itemName: string;
  requestedQuantity: number;
  approvedQuantity: number;
  previouslyIssued: number;
  remainingToIssue: number;
  issuingQuantity: number;
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

const RequisitionStatuses = [
  { value: 1, label: 'Draft', color: 'bg-gray-100 text-gray-800' },
  { value: 2, label: 'Submitted', color: 'bg-yellow-100 text-yellow-800' },
  { value: 3, label: 'Approved', color: 'bg-blue-100 text-blue-800' },
  { value: 4, label: 'In Progress', color: 'bg-indigo-100 text-indigo-800' },
  { value: 5, label: 'Partially Issued', color: 'bg-purple-100 text-purple-800' },
  { value: 6, label: 'Issued', color: 'bg-green-100 text-green-800' },
  { value: 7, label: 'Completed', color: 'bg-green-200 text-green-900' },
  { value: 8, label: 'Cancelled', color: 'bg-red-100 text-red-800' },
  { value: 9, label: 'Rejected', color: 'bg-red-100 text-red-800' }
];

// Helper to normalize status to number (API may return string or number)
const normalizeStatus = (status: number | string | undefined): number => {
  if (status === undefined || status === null) return 0;
  if (typeof status === 'number') return status;
  const statusMap: Record<string, number> = {
    'Draft': 1, 'Submitted': 2, 'Approved': 3, 'InProgress': 4,
    'PartiallyIssued': 5, 'Issued': 6, 'Completed': 7, 'Cancelled': 8, 'Rejected': 9
  };
  return statusMap[status] || 0;
};

export function IssueRequisitionDialog({ open, onOpenChange, requisitionId, onSuccess }: IssueRequisitionDialogProps) {
  const { toast } = useToast();
  const { user, hasPermission } = useAuth();
  const hasIssuePermission = hasPermission('procurement.inventory.issue');
  const [loading, setLoading] = useState(false);
  const [issuing, setIssuing] = useState(false);
  const [requisition, setRequisition] = useState<InventoryRequisitionDetailDto | null>(null);
  const [issueItems, setIssueItems] = useState<IssueItemState[]>([]);
  const [issueLocations, setIssueLocations] = useState<WarehouseLocationDto[]>([]);
  const [defaultIssueLocation, setDefaultIssueLocation] = useState('');
  const [issueError, setIssueError] = useState<string | null>(null);
  const [notes, setNotes] = useState('');
  const [receivers, setReceivers] = useState<InventoryIssueReceiverDto[]>([]);
  const [receiverUserId, setReceiverUserId] = useState('');
  const [changingReceiver, setChangingReceiver] = useState(false);
  const [movementReasonCode, setMovementReasonCode] = useState('');
  const [accountingOptions, setAccountingOptions] = useState<InventoryIssueAccountingOptionsDto | null>(null);
  const [vouchers, setVouchers] = useState<InventoryIssueVoucherDto[]>([]);
  const [acknowledgementComments, setAcknowledgementComments] = useState<Record<string, string>>({});
  const [receiptQuantities, setReceiptQuantities] = useState<Record<string, string>>({});
  const [receiptErrors, setReceiptErrors] = useState<Record<string, string>>({});
  const receiptRetries = useRef(new Map<string, { payload: string; key: string }>());
  const [voucherActionId, setVoucherActionId] = useState<string | null>(null);
  const [voucherPreview, setVoucherPreview] = useState<CentralDocumentViewerFile | null>(null);
  useEffect(() => {
    setVoucherPreview(null);
    setReceiptQuantities({});
    setAcknowledgementComments({});
    setReceiptErrors({});
    receiptRetries.current.clear();
  }, [open, requisitionId]);
  const canIssue = canIssueRequisition(requisition, user?.id, hasIssuePermission);
  const trackingExceptions = useAvailableInventoryTrackingExceptions(open && canIssue);

  useEffect(() => {
    if (open && requisitionId) {
      loadRequisition();
    }
  }, [open, requisitionId, user?.id, hasIssuePermission]);

  const loadRequisition = async () => {
    if (!requisitionId) return;
    try {
      setLoading(true);
      setIssueError(null);
      setIssueLocations([]);
      setDefaultIssueLocation('');
      const [detail, issueVouchers] = await Promise.all([
        inventoryRequisitionService.getById(requisitionId),
        inventoryRequisitionService.getIssueVouchers(requisitionId),
      ]);
      setRequisition(detail);
      setVouchers(issueVouchers);
      setReceivers([]);
      setReceiverUserId('');
      setChangingReceiver(false);
      setAccountingOptions(null);
      setMovementReasonCode('');
      // Viewing/acknowledging an issued voucher must not require issue-configuration access.
      if (canIssueRequisition(detail, user?.id, hasIssuePermission)) {
        const [receiverOptions, governedOptions, locations] = await Promise.all([
          inventoryRequisitionService.getIssueReceivers(),
          inventoryRequisitionService.getIssueAccountingOptions(requisitionId),
          inventoryManagementService.getWarehouseLocations(detail.warehouseId),
        ]);
        setIssueLocations(locations.filter(location => location.isActive &&
          (location.warehouseId === detail.warehouseId ||
            (location.isConsignmentBin && location.consignmentWarehouseId === detail.warehouseId))));
        setReceivers(receiverOptions);
        const requesterIsEligible = receiverOptions.some(receiver => receiver.userId === detail.requestedById);
        setReceiverUserId(requesterIsEligible ? detail.requestedById ?? '' : '');
        setChangingReceiver(!requesterIsEligible);
        setAccountingOptions(governedOptions);
        setMovementReasonCode(governedOptions.applicableMovementReasonCodes.length === 1
          ? governedOptions.applicableMovementReasonCodes[0] : '');
      }
      // Initialize issue items from requisition items
      const items: IssueItemState[] = detail.items.map(item => ({
        itemId: item.id,
        inventoryItemId: item.inventoryItemId,
        itemCode: item.itemCode,
        itemName: item.itemName,
        requestedQuantity: item.requestedQuantity,
        approvedQuantity: item.approvedQuantity || item.requestedQuantity,
        previouslyIssued: item.grossIssuedQuantity ?? (item.issuedQuantity || 0) + (item.returnedQuantity ?? 0),
        remainingToIssue: item.remainingToIssueQuantity ?? Math.max(0,
          item.approvedQuantity - (item.grossIssuedQuantity ?? (item.issuedQuantity || 0) + (item.returnedQuantity ?? 0))),
        issuingQuantity: 0,
        unitOfMeasure: item.unitOfMeasure,
        locationId: item.locationId || detail.locationId,
        locationName: item.locationName || detail.locationName,
        lotNumber: item.lotNumber,
        batchNumber: item.batchNumber,
        serialNumber: item.serialNumber,
        manufactureDate: item.manufactureDate,
        expiryDate: item.expiryDate,
        inventoryTrackingExceptionId: item.inventoryTrackingExceptionId,
      }));
      setIssueItems(items);
      setNotes('');
    } catch (err) {
      console.error('Error loading requisition:', err);
      setIssueError(getProcurementProblemMessage(err, 'Failed to load issue details.'));
      toast({ title: 'Error', description: 'Failed to load requisition', variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  };

  const handleQuantityChange = (itemId: string, quantity: number) => {
    setIssueItems(items => items.map(item => {
      if (item.itemId === itemId) {
        const maxQty = item.remainingToIssue;
        const validQty = Math.max(0, Math.min(quantity, maxQty));
        return { ...item, issuingQuantity: validQty };
      }
      return item;
    }));
  };

  const handleDefaultIssueLocationChange = (value: string) => {
    if (value === 'none') {
      setDefaultIssueLocation('');
      return;
    }
    const location = issueLocations.find(option => option.id === value);
    if (!location) return;
    setDefaultIssueLocation(value);
    setIssueItems(items => items.map(item => item.remainingToIssue > 0 &&
      !issueLocations.some(option => option.id === item.locationId) ? {
      ...item,
      locationId: location.id,
      locationName: `${location.locationCode}${location.name ? ` - ${location.name}` : ''}`,
      inventoryTrackingExceptionId: undefined,
    } : item));
    setIssueError(null);
  };

  const handleFillRemainingQuantities = () => {
    setIssueItems(items => items.map(item => ({
      ...item,
      issuingQuantity: Math.max(0, item.remainingToIssue)
    })));
  };

  const handleIssue = async () => {
    if (!canIssue) return;
    const itemsToIssue = issueItems.filter(item => item.issuingQuantity > 0);
    if (itemsToIssue.length === 0) {
      toast({ title: 'Validation Error', description: 'Please enter quantities to issue', variant: 'destructive' });
      return;
    }
    if (!movementReasonCode) {
      toast({ title: 'Movement reason required', description: 'Select a configured movement reason before issuing stock.', variant: 'destructive' });
      return;
    }
    if (itemsToIssue.some(item => !issueLocations.some(location => location.id === item.locationId))) {
      setIssueError('Select an active storage location for every line being issued. You can use Default issue location to fill the lines.');
      return;
    }
    try {
      setIssueError(null);
      setIssuing(true);
      const dto: IssueRequisitionDto = {
        idempotencyKey: crypto.randomUUID(),
        rowVersion: requisition?.rowVersion || '',
        receiverUserId,
        movementReasonCode,
        items: itemsToIssue.map(item => ({
          itemId: item.itemId,
          issuedQuantity: item.issuingQuantity,
          locationId: item.locationId,
          lotNumber: item.lotNumber,
          batchNumber: item.batchNumber,
          serialNumber: item.serialNumber,
          manufactureDate: item.manufactureDate,
          expiryDate: item.expiryDate,
          inventoryTrackingExceptionId: item.inventoryTrackingExceptionId,
        })),
        notes: notes || undefined
      };
      if (!requisitionId) {
        throw new Error('Requisition ID is missing');
      }
      await inventoryRequisitionService.issue(requisitionId, dto);
      toast({ title: 'Issued', description: 'The Store Issue Voucher is ready for receiver acknowledgement.' });
      onSuccess();
      await loadRequisition();
    } catch (err: unknown) {
      console.error('Error issuing items:', err);
      const errorMessage = getProcurementProblemMessage(err, 'Failed to issue items');
      setIssueError(errorMessage);
      toast({ title: 'Error', description: errorMessage, variant: 'destructive' });
    } finally {
      setIssuing(false);
    }
  };

  const handleAcknowledge = async (voucher: InventoryIssueVoucherDto) => {
    const comment = (acknowledgementComments[voucher.id] ?? '').trim();
    const outstanding = voucher.lines.filter(line => line.outstandingQuantity > 0);
    const lines = outstanding.map(line => ({ issueVoucherLineId: line.id, receivedQuantity: Number(receiptQuantities[line.id]) }));
    let validation = '';
    if (voucher.lines.some(line => !Number.isFinite(line.outstandingQuantity) || !Number.isFinite(line.receivedQuantity))) {
      validation = 'Receipt balances are unavailable. Reload this requisition before acknowledging.';
    } else if (!outstanding.length) {
      validation = 'This voucher has no outstanding quantity to receive.';
    } else {
      for (const line of outstanding) {
        const raw = receiptQuantities[line.id]?.trim() ?? '';
        const quantity = Number(raw);
        if (!raw || !/^\d+(?:\.\d{1,4})?$/.test(raw) || !Number.isFinite(quantity) || quantity > line.outstandingQuantity || (line.serialNumber && !Number.isInteger(quantity))) {
          validation = `${line.itemCode}: enter an actual received quantity from 0 to ${line.outstandingQuantity}${line.serialNumber ? ' in whole units' : ' with at most four decimal places'}. Enter 0 if none was received.`;
          break;
        }
      }
    }
    if (!validation && !lines.some(line => line.receivedQuantity > 0)) validation = 'Enter a positive received quantity for at least one line.';
    if (!validation && !comment) validation = 'Enter the receiver handover comment before acknowledging.';
    if (validation) {
      setReceiptErrors(previous => ({ ...previous, [voucher.id]: validation }));
      return;
    }
    const payload = JSON.stringify({ rowVersion: voucher.rowVersion, comment, lines });
    let retry = receiptRetries.current.get(voucher.id);
    if (retry?.payload !== payload) {
      retry = { payload, key: crypto.randomUUID() };
      receiptRetries.current.set(voucher.id, retry);
    }
    try {
      setVoucherActionId(voucher.id);
      setReceiptErrors(previous => ({ ...previous, [voucher.id]: '' }));
      const updated = await inventoryRequisitionService.acknowledgeIssueVoucher(voucher.id, voucher.rowVersion, comment, lines, retry.key);
      setVouchers(previous => previous.map(value => value.id === voucher.id ? updated : value));
      toast({ title: 'Receipt recorded', description: `${voucher.voucherNumber}: actual received quantities saved. Any remaining quantity stays outstanding.` });
      setAcknowledgementComments(previous => ({ ...previous, [voucher.id]: '' }));
      setReceiptQuantities(previous => Object.fromEntries(Object.entries(previous).filter(([id]) => !voucher.lines.some(line => line.id === id))));
      receiptRetries.current.delete(voucher.id);
      await loadRequisition();
      onSuccess();
    } catch (err: unknown) {
      const message = getProcurementProblemMessage(err, 'Failed to acknowledge the Store Issue Voucher');
      setReceiptErrors(previous => ({ ...previous, [voucher.id]: message }));
      toast({ title: 'Acknowledgement failed', description: message, variant: 'destructive' });
    } finally {
      setVoucherActionId(null);
    }
  };

  const handleDownload = (voucher: InventoryIssueVoucherDto) => setVoucherPreview({
    title: voucher.voucherNumber,
    fileName: `${voucher.voucherNumber}.pdf`,
    contentType: 'application/pdf',
    repositoryPath: `/api/inventory/requisitions/issue-vouchers/${encodeURIComponent(voucher.id)}/download`,
    sourceLabel: 'Store Issue Voucher',
  });

  const getStatusBadge = (status: number | string) => {
    const numStatus = normalizeStatus(status);
    const s = RequisitionStatuses.find(st => st.value === numStatus);
    return <Badge className={s?.color || 'bg-gray-100'}>{s?.label || RequisitionStatusMap[numStatus] || status}</Badge>;
  };

  const totalIssuing = issueItems.reduce((sum, item) => sum + item.issuingQuantity, 0);
  const hasItemsToIssue = issueItems.some(item => item.remainingToIssue > 0);
  const applicableMovementReasons = useMemo(() => {
    if (!accountingOptions) return [];
    const selected = issueItems.filter(item => item.issuingQuantity > 0);
    if (selected.length === 0) return accountingOptions.applicableMovementReasonCodes;
    return accountingOptions.applicableMovementReasonCodes.filter(reason => selected.every(item =>
      (accountingOptions.applicableMovementReasonCodesByRequisitionItem[item.itemId] ?? []).includes(reason)));
  }, [accountingOptions, issueItems]);

  useEffect(() => {
    if (movementReasonCode && !applicableMovementReasons.includes(movementReasonCode)) setMovementReasonCode('');
    else if (!movementReasonCode && applicableMovementReasons.length === 1) setMovementReasonCode(applicableMovementReasons[0]);
  }, [applicableMovementReasons, movementReasonCode]);

  return (
    <>
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="w-[96vw] max-w-[96vw] sm:max-w-6xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <div className="flex flex-col gap-1">
            <DialogTitle>
              {canIssue ? 'Issue Items' : 'Issue vouchers'}
              {requisition && <span className="ml-2 text-muted-foreground">#{requisition.requisitionNumber}</span>}
            </DialogTitle>
            <div className="flex items-center gap-2">
              <DialogDescription className="text-sm text-muted-foreground">
                {canIssue ? 'Issue inventory items for this requisition' : 'Review handover evidence and receiver acknowledgement'}
              </DialogDescription>
              {requisition && getStatusBadge(requisition.status)}
            </div>
          </div>
        </DialogHeader>

        {loading ? (
          <div className="flex justify-center py-8"><div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div></div>
        ) : requisition ? (
          <div className="min-w-0 max-w-full space-y-4">
            {/* Requisition Info */}
            <Card>
              <CardContent className="pt-4">
                <div className="grid grid-cols-3 gap-4 text-sm">
                  <div><span className="text-muted-foreground">Department:</span> <span className="font-medium">{requisition.departmentName}</span></div>
                  <div><span className="text-muted-foreground">Warehouse:</span> <span className="font-medium">{requisition.warehouseName}</span></div>
                  <div><span className="text-muted-foreground">Request Date:</span> <span className="font-medium">{requisition.requestDateFormatted || format(new Date(requisition.requestDate), 'dd/MM/yyyy')}</span></div>
                  <div><span className="text-muted-foreground">Requested location:</span> <span className="font-medium">{requisition.locationName || 'Not specified'}</span></div>
                </div>
              </CardContent>
            </Card>

            {canIssue && <>
            {issueError && <div role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">{issueError}</div>}
            <Card className="border-blue-200 bg-blue-50/40">
              <CardHeader className="pb-2">
                <CardTitle className="text-base">Controlled handover</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                <div className="grid gap-3 md:grid-cols-2">
                  <div className="space-y-1">
                    <Label htmlFor={changingReceiver ? 'issue-receiver' : undefined}>Receiver</Label>
                    {changingReceiver ? (
                      <>
                        <Select disabled={issuing} value={receiverUserId || 'none'} onValueChange={value => {
                          setReceiverUserId(value === 'none' ? '' : value);
                          if (value !== 'none') setChangingReceiver(false);
                        }}>
                          <SelectTrigger id="issue-receiver"><SelectValue placeholder="Select an active internal receiver" /></SelectTrigger>
                          <SelectContent>
                            <SelectItem value="none">Select an active internal receiver</SelectItem>
                            {receivers.map(receiver => (
                              <SelectItem key={receiver.userId} value={receiver.userId}>
                                {receiver.displayName || receiver.username}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                        {!receiverUserId && <p className="text-xs text-muted-foreground">The requester is not available as an eligible receiver. Select the person collecting the items.</p>}
                        {receiverUserId && <Button type="button" size="sm" variant="ghost" disabled={issuing} onClick={() => setChangingReceiver(false)}>Cancel change</Button>}
                      </>
                    ) : (
                      <div className="flex min-h-10 flex-wrap items-center justify-between gap-2">
                        <span className="text-sm font-medium">
                          {receivers.find(receiver => receiver.userId === receiverUserId)?.displayName ||
                            receivers.find(receiver => receiver.userId === receiverUserId)?.username || requisition.requestedByName}
                          {receiverUserId === requisition.requestedById && ' (requester)'}
                        </span>
                        <Button type="button" size="sm" variant="outline" disabled={issuing} onClick={() => setChangingReceiver(true)}>Change receiver</Button>
                      </div>
                    )}
                    <p className="text-xs text-muted-foreground">Change only if someone else will collect and acknowledge receipt.</p>
                  </div>
                  <div className="space-y-1">
                    <Label htmlFor="movement-reason">Movement reason</Label>
                    <Select value={movementReasonCode || 'none'} onValueChange={value => setMovementReasonCode(value === 'none' ? '' : value)}>
                      <SelectTrigger id="movement-reason"><SelectValue placeholder="Select configured reason" /></SelectTrigger>
                      <SelectContent>
                        <SelectItem value="none">Select configured reason</SelectItem>
                        {applicableMovementReasons.map(code => (
                          <SelectItem key={code} value={code}>{accountingOptions?.movementReasons[code] ?? code}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                </div>
                {accountingOptions && applicableMovementReasons.length === 0 && (
                  <p className="rounded-md border border-amber-300 bg-amber-50 p-2 text-sm text-amber-900">
                    No active issue-accounting rule covers the selected issue lines. Configure the category, item type and movement reason before issuing.
                  </p>
                )}
              </CardContent>
            </Card>

            {/* Items Table */}
            <div className="flex flex-wrap items-center justify-between gap-3">
              <h4 className="font-medium">Items to Issue</h4>
            </div>
            {hasItemsToIssue && <div className="flex flex-wrap items-end justify-between gap-3">
              <div className="w-full space-y-1 sm:max-w-sm">
                <Label htmlFor="default-issue-location">Default issue location</Label>
                <Select value={defaultIssueLocation || 'none'} onValueChange={handleDefaultIssueLocationChange} disabled={issuing}>
                  <SelectTrigger id="default-issue-location"><SelectValue placeholder="Select default location" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No default location</SelectItem>
                    {issueLocations.map(location => <SelectItem key={location.id} value={location.id}>
                      {location.locationCode}{location.name ? ` - ${location.name}` : ''}
                    </SelectItem>)}
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">Fills missing or unavailable locations. Existing active selections stay unchanged; you can change each line below.</p>
                {issueLocations.length === 0 && <p className="text-sm text-destructive">No active storage locations are available in this warehouse. Ask Stores to configure a location before issuing.</p>}
              </div>
              <Button type="button" variant="outline" size="sm" disabled={issuing} onClick={handleFillRemainingQuantities}>Fill remaining quantities</Button>
            </div>}

            <div className="overflow-x-auto">
            <Table className="min-w-[1040px]">
              <TableHeader>
                <TableRow>
                  <TableHead>Item</TableHead>
                  <TableHead className="text-center">Requested</TableHead>
                  <TableHead className="text-center">Approved</TableHead>
                  <TableHead className="text-center">Previously Issued</TableHead>
                  <TableHead className="text-center">Remaining</TableHead>
                  <TableHead className="text-center">Issue Qty</TableHead>
                    <TableHead>Issue location</TableHead>
                  <TableHead>Tracking</TableHead>
                  <TableHead>UoM</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {issueItems.map((item) => (
                  <TableRow key={item.itemId}>
                    <TableCell>
                      <div className="font-medium">{item.itemCode}</div>
                      <div className="text-sm text-muted-foreground">{item.itemName}</div>
                    </TableCell>
                    <TableCell className="text-center">{item.requestedQuantity}</TableCell>
                    <TableCell className="text-center">{item.approvedQuantity}</TableCell>
                    <TableCell className="text-center">
                      {item.previouslyIssued > 0 ? (
                        <Badge variant="outline" className="bg-green-50">{item.previouslyIssued}</Badge>
                      ) : '-'}
                    </TableCell>
                    <TableCell className="text-center">
                      {item.remainingToIssue > 0 ? (
                        <Badge variant="outline" className="bg-yellow-50">{item.remainingToIssue}</Badge>
                      ) : (
                        <Badge className="bg-green-100 text-green-800"><CheckCircle className="h-3 w-3 mr-1" />Complete</Badge>
                      )}
                    </TableCell>
                    <TableCell className="text-center">
                      {item.remainingToIssue > 0 ? (
                        <Input
                          type="number"
                          min="0"
                          max={item.remainingToIssue}
                          value={item.issuingQuantity}
                          onChange={(e) => handleQuantityChange(item.itemId, parseInt(e.target.value) || 0)}
                          className="w-20 text-center"
                        />
                      ) : '-'}
                    </TableCell>
                    <TableCell className="min-w-[200px]">
                      {item.remainingToIssue > 0 ? (
                        <Select
                          value={item.locationId || ''}
                          disabled={issuing}
                          onValueChange={value => {
                            const location = issueLocations.find(option => option.id === value);
                            if (!location) return;
                            setIssueItems(values => values.map(line => line.itemId === item.itemId ? {
                              ...line,
                              locationId: location.id,
                              locationName: `${location.locationCode}${location.name ? ` - ${location.name}` : ''}`,
                              inventoryTrackingExceptionId: undefined,
                            } : line));
                            setIssueError(null);
                          }}>
                          <SelectTrigger aria-label={`Issue location for ${item.itemCode}`}><SelectValue placeholder="Select issue location" /></SelectTrigger>
                          <SelectContent>
                            {issueLocations.map(location => <SelectItem key={location.id} value={location.id}>
                              {location.locationCode}{location.name ? ` - ${location.name}` : ''}
                            </SelectItem>)}
                            {item.locationId && !issueLocations.some(location => location.id === item.locationId) &&
                              <SelectItem value={item.locationId} disabled>{item.locationName || 'Previously selected location (unavailable)'}</SelectItem>}
                          </SelectContent>
                        </Select>
                      ) : item.locationName || 'Not recorded'}
                    </TableCell>
                    <TableCell className="min-w-[360px]">
                      <div className="grid grid-cols-3 gap-1">
                        <Input placeholder="Lot" value={item.lotNumber || ''} onChange={event => setIssueItems(values => values.map(value => value.itemId === item.itemId ? { ...value, lotNumber: event.target.value || undefined } : value))} />
                        <Input placeholder="Batch" value={item.batchNumber || ''} onChange={event => setIssueItems(values => values.map(value => value.itemId === item.itemId ? { ...value, batchNumber: event.target.value || undefined } : value))} />
                        <Input placeholder="Serial" value={item.serialNumber || ''} onChange={event => setIssueItems(values => values.map(value => value.itemId === item.itemId ? { ...value, serialNumber: event.target.value || undefined } : value))} />
                        <Accordion type="single" collapsible defaultValue={item.inventoryTrackingExceptionId ? 'exception' : undefined} className="col-span-3">
                          <AccordionItem value="exception" className="border-0">
                            <AccordionTrigger disabled={issuing} className="gap-2 py-2 text-xs text-muted-foreground">
                              <span>Advanced tracking options</span>
                              {item.inventoryTrackingExceptionId && <Badge variant="outline" className="ml-auto text-xs">Exception selected</Badge>}
                            </AccordionTrigger>
                            <AccordionContent className="pb-0">
                              <p className="mb-2 text-xs text-muted-foreground">Use only when an approved exception applies to this issue.</p>
                              <InventoryTrackingExceptionSelect
                                value={item.inventoryTrackingExceptionId}
                                onValueChange={inventoryTrackingExceptionId => setIssueItems(values => values.map(value => value.itemId === item.itemId ? { ...value, inventoryTrackingExceptionId } : value))}
                                exceptions={trackingExceptions.exceptions}
                                loading={trackingExceptions.loading}
                                error={trackingExceptions.error}
                                onRetry={trackingExceptions.refresh}
                                disabled={issuing}
                                context={{ inventoryItemId: item.inventoryItemId, warehouseId: requisition.warehouseId, locationId: item.locationId, referenceId: requisition.id, lotNumber: item.lotNumber, batchNumber: item.batchNumber, serialNumber: item.serialNumber }}
                              />
                            </AccordionContent>
                          </AccordionItem>
                        </Accordion>
                      </div>
                    </TableCell>
                    <TableCell>{item.unitOfMeasure}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
            </div>

            {/* Notes */}
            <div className="space-y-2">
              <Label>Issue Notes</Label>
              <Textarea value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="Optional notes for this issue..." rows={2} />
            </div>

            {/* Summary */}
            {totalIssuing > 0 && (
              <Card className="bg-blue-50">
                <CardContent className="pt-4">
                  <div className="flex items-center gap-2">
                    <Package className="h-5 w-5 text-blue-600" />
                    <span className="font-medium">Total items to issue: {totalIssuing}</span>
                  </div>
                </CardContent>
              </Card>
            )}

            </>}
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-base">Store Issue Voucher register</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                {vouchers.length === 0 ? (
                  <p className="text-sm text-muted-foreground">No issue voucher has been posted for this requisition.</p>
                ) : vouchers.map(voucher => (
                  <div key={voucher.id} className="rounded-md border p-3 space-y-2">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <div>
                        <div className="font-medium">{voucher.voucherNumber}</div>
                        <div className="text-xs text-muted-foreground">
                          {format(new Date(voucher.issuedAtUtc), 'dd MMM yyyy HH:mm')} · issued by {voucher.issuedByName} · receiver {voucher.receiverName}
                        </div>
                      </div>
                      <div className="flex items-center gap-2">
                        <Badge className={voucher.status === 2 || String(voucher.status) === 'Acknowledged' ? 'bg-green-100 text-green-800' : 'bg-amber-100 text-amber-800'}>
                          {voucher.status === 2 || String(voucher.status) === 'Acknowledged' ? 'Acknowledged' : voucher.lines.some(line => line.receivedQuantity > 0) ? 'Partially received' : 'Awaiting receiver'}
                        </Badge>
                        <Button variant="outline" size="sm" onClick={() => handleDownload(voucher)} disabled={voucherActionId === voucher.id}>
                          <Download className="mr-1 h-3.5 w-3.5" /> PDF
                        </Button>
                      </div>
                    </div>
                    <div className="text-xs text-muted-foreground">
                      {voucher.lines.length} line(s) · {accountingOptions?.movementReasons[voucher.movementReasonCode] ?? voucher.movementReasonCode} · Finance {voucher.financeJournalEntryId ? 'posted' : 'pending'}
                    </div>
                    <p className="text-xs text-muted-foreground">Issuing store: {voucher.warehouseName} · Requisition: {voucher.requisitionNumber}</p>
                    {voucher.isLegacyAcknowledgement && <p className="text-sm text-muted-foreground">Historical acknowledgement: actual quantities were not captured per line.</p>}
                    <Table aria-label={`Receipt lines ${voucher.voucherNumber}`}>
                      <TableHeader><TableRow>
                        <TableHead>Item code</TableHead><TableHead>Description</TableHead><TableHead>UOM</TableHead>
                        <TableHead className="text-right">Requested</TableHead><TableHead className="text-right">Issued</TableHead>
                        <TableHead className="text-right">Previously received</TableHead><TableHead className="text-right">Outstanding</TableHead>
                        <TableHead className="text-right">Actual quantity received</TableHead>
                      </TableRow></TableHeader>
                      <TableBody>{voucher.lines.map(line => <TableRow key={line.id}>
                        <TableCell>{line.itemCode}</TableCell><TableCell>{line.itemName}{line.serialNumber && <div className="text-xs text-muted-foreground">Serial: {line.serialNumber}</div>}</TableCell>
                        <TableCell>{line.unitOfMeasure ?? '—'}</TableCell><TableCell className="text-right">{line.requestedQuantity ?? '—'}</TableCell>
                        <TableCell className="text-right">{line.quantity}</TableCell><TableCell className="text-right">{voucher.isLegacyAcknowledgement ? 'Not recorded' : line.receivedQuantity ?? '—'}</TableCell>
                        <TableCell className="text-right">{voucher.isLegacyAcknowledgement ? '—' : line.outstandingQuantity ?? '—'}</TableCell>
                        <TableCell className="text-right">{(voucher.status === 1 || String(voucher.status) === 'Issued') && user?.id === voucher.receiverUserId && line.outstandingQuantity > 0
                          ? <Input aria-label={`Actual quantity received ${voucher.voucherNumber} ${line.itemCode}`} className="ml-auto w-28 text-right" type="number" min="0" max={line.outstandingQuantity} step={line.serialNumber ? '1' : '0.0001'}
                            placeholder="Enter quantity" value={receiptQuantities[line.id] ?? ''} disabled={voucherActionId !== null}
                            onChange={event => setReceiptQuantities(previous => ({ ...previous, [line.id]: event.target.value }))} />
                          : '—'}</TableCell>
                      </TableRow>)}</TableBody>
                    </Table>
                    {receiptErrors[voucher.id] && <p role="alert" className="text-sm text-destructive">{receiptErrors[voucher.id]}</p>}
                    {(voucher.status === 1 || String(voucher.status) === 'Issued') && user?.id === voucher.receiverUserId ? (
                      <div className="space-y-2">
                        <p className="text-xs text-muted-foreground">Enter the quantity physically received for every outstanding line, including 0 for items not received.</p>
                        <div className="flex flex-col gap-2 sm:flex-row">
                        <Input
                          aria-label={`Receiver handover comment ${voucher.voucherNumber}`}
                          value={acknowledgementComments[voucher.id] ?? ''}
                          onChange={event => setAcknowledgementComments(previous => ({ ...previous, [voucher.id]: event.target.value }))}
                          placeholder="Receiver handover comment"
                          maxLength={1000}
                          disabled={voucherActionId !== null}
                        />
                        <Button size="sm" onClick={() => handleAcknowledge(voucher)} disabled={voucherActionId !== null || !voucher.lines.some(line => line.outstandingQuantity > 0)}>
                          <ClipboardCheck className="mr-1 h-4 w-4" /> Acknowledge receipt
                        </Button>
                        </div>
                      </div>
                    ) : (voucher.status === 2 || String(voucher.status) === 'Acknowledged') ? (
                      <p className="text-xs text-green-700">Acknowledged {voucher.acknowledgedAtUtc ? format(new Date(voucher.acknowledgedAtUtc), 'dd MMM yyyy HH:mm') : ''}: {voucher.receiverComment}</p>
                    ) : <p className="text-xs text-muted-foreground">Awaiting acknowledgement by {voucher.receiverName}.</p>}
                  </div>
                ))}
              </CardContent>
            </Card>
          </div>
        ) : (
          <div className="text-center py-8 text-muted-foreground">No requisition data</div>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>{canIssue ? 'Cancel' : 'Close'}</Button>
          {canIssue && <Button onClick={handleIssue} disabled={issuing || totalIssuing === 0 || !receiverUserId || !movementReasonCode}>
            {issuing ? 'Issuing...' : `Issue ${totalIssuing} Items`}
          </Button>}
        </DialogFooter>
      </DialogContent>
    </Dialog>
    <CentralDocumentViewerDialog file={voucherPreview} open={open && Boolean(voucherPreview)}
      onOpenChange={(value) => { if (!value) setVoucherPreview(null); }} enableAnnotations={false} />
    </>
  );
}

