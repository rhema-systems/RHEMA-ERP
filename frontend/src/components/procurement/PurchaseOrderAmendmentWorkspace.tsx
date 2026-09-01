'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  CheckCircle2,
  FileDiff,
  Loader2,
  Plus,
  RefreshCw,
  Send,
  ShieldAlert,
  Truck,
  XCircle,
} from 'lucide-react';
import { toast } from 'sonner';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { procurementPurchaseOrderAmendmentService as service } from '@/services/procurement-purchase-order-amendment.service';
import type {
  ProcurementPurchaseOrderSourceType,
  PurchaseOrderDetailDto,
} from '@/services/purchasingService';
import type {
  CreatePurchaseOrderAmendmentRequest,
  DispatchPurchaseOrderAmendmentRequest,
  PurchaseOrderAmendment,
  PurchaseOrderAmendmentItemRequest,
  PurchaseOrderAmendmentOverview,
  PurchaseOrderDispatchChannel,
} from '@/types/procurement-purchase-order-amendment';

type LifecycleAction = 'submit' | 'approve' | 'reject';

const sourceTypes: ProcurementPurchaseOrderSourceType[] = [
  'RfqAward',
  'TenderAward',
  'Contract',
  'ApprovedException',
];

const statusTone: Record<string, string> = {
  Draft: 'bg-slate-100 text-slate-800',
  PendingApproval: 'bg-amber-100 text-amber-900',
  Applied: 'bg-emerald-100 text-emerald-900',
  Rejected: 'bg-red-100 text-red-900',
  Cancelled: 'bg-slate-100 text-slate-700',
  Dispatched: 'bg-blue-100 text-blue-900',
  Acknowledged: 'bg-violet-100 text-violet-900',
};

const dateValue = (value?: string) =>
  value ? new Date(value).toISOString().slice(0, 10) : '';

const idempotencyKey = (prefix: string) =>
  `${prefix}-${crypto.randomUUID()}`;

const errorMessage = (error: unknown) =>
  error instanceof Error ? error.message : 'The request could not be completed.';

interface EditorState {
  reason: string;
  changeScope: string;
  evidenceReference: string;
  requiredDate: string;
  promisedDate: string;
  businessPartnerId: string;
  sourceType: string;
  sourceId: string;
  paymentTerms: string;
  shippingTerms: string;
  terms: string;
  notes: string;
  deliveryWarehouseId: string;
  deliveryAddress: string;
  deliveryInstructions: string;
  taxAmount: string;
  shippingCost: string;
  miscellaneousCost: string;
  discountAmount: string;
  items: PurchaseOrderAmendmentItemRequest[];
}

const editorFromOrder = (order: PurchaseOrderDetailDto): EditorState => ({
  reason: '',
  changeScope: '',
  evidenceReference: '',
  requiredDate: dateValue(order.requiredDate),
  promisedDate: dateValue(order.promisedDate),
  businessPartnerId: order.supplierId,
  sourceType: '',
  sourceId: '',
  paymentTerms: order.paymentTerms || '',
  shippingTerms: order.shippingTerms || '',
  terms: order.terms || '',
  notes: order.notes || '',
  deliveryWarehouseId: order.deliveryWarehouseId || '',
  deliveryAddress: order.deliveryAddress || '',
  deliveryInstructions: order.deliveryInstructions || '',
  taxAmount: String(order.taxAmount || 0),
  shippingCost: String(order.shippingCost || 0),
  miscellaneousCost: String(order.miscellaneousCost || 0),
  discountAmount: String(order.discountAmount || 0),
  items: order.items.map((item) => ({
    purchaseOrderItemId: item.id,
    inventoryItemId: item.inventoryItemId,
    supplierItemCode: item.supplierItemCode,
    itemDescription: item.itemDescription || item.itemName,
    orderedQuantity: item.orderedQuantity,
    unitOfMeasure: item.unitOfMeasure || 'EA',
    itemUnitOfMeasureId: item.itemUnitOfMeasureId,
    warehouseId: item.warehouseId,
    unitPrice: item.unitPrice,
    expectedDeliveryDate: item.expectedDeliveryDate,
    notes: item.notes,
  })),
});

export function PurchaseOrderAmendmentWorkspace({
  order,
  onApplied,
  editorRequestToken = 0,
}: {
  order: PurchaseOrderDetailDto;
  onApplied?: () => Promise<void> | void;
  editorRequestToken?: number;
}) {
  const { hasPermission } = useAuth();
  const canManage = hasPermission('procurement.purchase-order.create');
  const canApprove = hasPermission('procurement.purchase-order.approve');
  const [overview, setOverview] =
    useState<PurchaseOrderAmendmentOverview | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [showEditor, setShowEditor] = useState(false);
  const [editor, setEditor] = useState<EditorState>(() =>
    editorFromOrder(order)
  );
  const [lifecycle, setLifecycle] = useState<{
    action: LifecycleAction;
    amendment: PurchaseOrderAmendment;
  } | null>(null);
  const [comment, setComment] = useState('');
  const [evidenceReference, setEvidenceReference] = useState('');
  const [dispatchFor, setDispatchFor] =
    useState<PurchaseOrderAmendment | null>(null);
  const [dispatch, setDispatch] =
    useState<Omit<DispatchPurchaseOrderAmendmentRequest, 'idempotencyKey'>>({
      channel: 'SupplierPortal',
      destination: order.supplierId,
      dispatchReference: '',
      documentReference: '',
      organizationSignatureEvidenceReference: '',
      dispatchEvidenceReference: '',
    });

  useEffect(() => {
    setEditor(editorFromOrder(order));
  }, [order]);

  const load = useCallback(async () => {
    try {
      setLoading(true);
      setLoadError(null);
      setOverview(await service.overview(order.id));
    } catch (error) {
      setLoadError(errorMessage(error));
    } finally {
      setLoading(false);
    }
  }, [order.id]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (editorRequestToken > 0 && overview?.canCreateAmendment) {
      setShowEditor(true);
    }
  }, [editorRequestToken, overview?.canCreateAmendment]);

  const proposedTotal = useMemo(() => {
    const subtotal = editor.items.reduce(
      (sum, item) => sum + item.orderedQuantity * item.unitPrice,
      0
    );
    return (
      subtotal +
      Number(editor.taxAmount || 0) +
      Number(editor.shippingCost || 0) +
      Number(editor.miscellaneousCost || 0) -
      Number(editor.discountAmount || 0)
    );
  }, [editor]);

  const updateItem = (
    index: number,
    patch: Partial<PurchaseOrderAmendmentItemRequest>
  ) =>
    setEditor((current) => ({
      ...current,
      items: current.items.map((item, itemIndex) =>
        itemIndex === index ? { ...item, ...patch } : item
      ),
    }));

  const create = async () => {
    if (
      editor.reason.trim().length < 10 ||
      !editor.changeScope.trim() ||
      !editor.evidenceReference.trim()
    ) {
      toast.error('Enter a reason, change scope, and evidence reference.');
      return;
    }
    if (
      Boolean(editor.sourceType) !== Boolean(editor.sourceId) ||
      (editor.businessPartnerId !== order.supplierId && !editor.sourceId)
    ) {
      toast.error(
        'A supplier change requires both a new approved source type and source ID.'
      );
      return;
    }
    const request: CreatePurchaseOrderAmendmentRequest = {
      reason: editor.reason.trim(),
      changeScope: editor.changeScope.trim(),
      sourceType: editor.sourceType
        ? (editor.sourceType as ProcurementPurchaseOrderSourceType)
        : undefined,
      sourceId: editor.sourceId.trim() || undefined,
      businessPartnerId: editor.businessPartnerId.trim() || undefined,
      requiredDate: editor.requiredDate || undefined,
      promisedDate: editor.promisedDate || undefined,
      paymentTerms: editor.paymentTerms || undefined,
      shippingTerms: editor.shippingTerms || undefined,
      terms: editor.terms || undefined,
      notes: editor.notes || undefined,
      deliveryWarehouseId: editor.deliveryWarehouseId || undefined,
      deliveryAddress: editor.deliveryAddress || undefined,
      deliveryInstructions: editor.deliveryInstructions || undefined,
      taxAmount: Number(editor.taxAmount || 0),
      shippingCost: Number(editor.shippingCost || 0),
      miscellaneousCost: Number(editor.miscellaneousCost || 0),
      discountAmount: Number(editor.discountAmount || 0),
      items: editor.items,
      evidenceReference: editor.evidenceReference.trim(),
      idempotencyKey: idempotencyKey('po-amendment-create'),
    };
    try {
      setBusy(true);
      await service.create(order.id, request);
      toast.success('The documented amendment draft was created.');
      setShowEditor(false);
      setEditor(editorFromOrder(order));
      await load();
    } catch (error) {
      toast.error(errorMessage(error));
    } finally {
      setBusy(false);
    }
  };

  const runLifecycle = async () => {
    if (!lifecycle || !comment.trim() || !evidenceReference.trim()) {
      toast.error('A comment and evidence reference are required.');
      return;
    }
    try {
      setBusy(true);
      if (lifecycle.action === 'submit') {
        await service.submit(lifecycle.amendment.id, {
          comment: comment.trim(),
          rowVersion: lifecycle.amendment.rowVersion,
          evidenceReference: evidenceReference.trim(),
        });
        toast.success('The amendment entered the shared approval workflow.');
      } else {
        await service.decide(lifecycle.amendment.id, {
          approved: lifecycle.action === 'approve',
          comment: comment.trim(),
          rowVersion: lifecycle.amendment.rowVersion,
          evidenceReference: evidenceReference.trim(),
        });
        toast.success(
          lifecycle.action === 'approve'
            ? 'The approved revision and budget adjustment were applied.'
            : 'The amendment was rejected without changing the PO.'
        );
      }
      setLifecycle(null);
      setComment('');
      setEvidenceReference('');
      await load();
      await onApplied?.();
    } catch (error) {
      toast.error(errorMessage(error));
    } finally {
      setBusy(false);
    }
  };

  const runDispatch = async () => {
    if (
      !dispatchFor ||
      !dispatch.destination.trim() ||
      !dispatch.dispatchReference.trim() ||
      !dispatch.documentReference.trim() ||
      !dispatch.organizationSignatureEvidenceReference.trim() ||
      !dispatch.dispatchEvidenceReference.trim()
    ) {
      toast.error('Complete every signed dispatch evidence field.');
      return;
    }
    try {
      setBusy(true);
      await service.dispatch(dispatchFor.id, {
        ...dispatch,
        destination: dispatch.destination.trim(),
        dispatchReference: dispatch.dispatchReference.trim(),
        documentReference: dispatch.documentReference.trim(),
        organizationSignatureEvidenceReference:
          dispatch.organizationSignatureEvidenceReference.trim(),
        dispatchEvidenceReference: dispatch.dispatchEvidenceReference.trim(),
        idempotencyKey: idempotencyKey('po-amendment-dispatch'),
      });
      toast.success('The signed approved revision was dispatched.');
      setDispatchFor(null);
      await load();
      await onApplied?.();
    } catch (error) {
      toast.error(errorMessage(error));
    } finally {
      setBusy(false);
    }
  };

  if (loading) {
    return (
      <Card data-testid="po-amendment-loading">
        <CardContent className="flex items-center gap-2 py-10 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Loading controlled PO amendment history…
        </CardContent>
      </Card>
    );
  }

  if (loadError || !overview) {
    return (
      <Alert variant="destructive" data-testid="po-amendment-error">
        <ShieldAlert className="h-4 w-4" />
        <AlertTitle>Amendment control unavailable</AlertTitle>
        <AlertDescription className="space-y-3">
          <p>{loadError || 'No amendment overview was returned.'}</p>
          <Button variant="outline" size="sm" onClick={() => void load()}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Retry
          </Button>
        </AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="space-y-4" data-testid="po-amendment-workspace">
      <Card>
        <CardHeader className="flex-row items-start justify-between gap-4">
          <div>
            <CardTitle className="flex items-center gap-2">
              <FileDiff className="h-5 w-5" />
              Controlled amendments
            </CardTitle>
            <p className="mt-1 text-sm text-muted-foreground">
              Revision {overview.currentRevisionNumber} · immutable diffs ·
              reapproval · budget adjustment · signed redispatch · supplier
              acknowledgement
            </p>
          </div>
          {canManage && overview.canCreateAmendment && (
            <Button onClick={() => setShowEditor((value) => !value)}>
              <Plus className="mr-2 h-4 w-4" />
              Document amendment
            </Button>
          )}
        </CardHeader>
        <CardContent className="space-y-4">
          {!overview.canCreateAmendment && (
            <Alert>
              <ShieldAlert className="h-4 w-4" />
              <AlertTitle>New amendment blocked</AlertTitle>
              <AlertDescription>{overview.blockedReason}</AlertDescription>
            </Alert>
          )}

          {showEditor && (
            <div className="space-y-4 rounded-lg border p-4">
              <div className="grid gap-4 md:grid-cols-3">
                <div className="md:col-span-2">
                  <Label htmlFor="amendment-reason">Reason</Label>
                  <Textarea
                    id="amendment-reason"
                    value={editor.reason}
                    onChange={(event) =>
                      setEditor({ ...editor, reason: event.target.value })
                    }
                    placeholder="Document why the approved PO must change."
                  />
                </div>
                <div>
                  <Label htmlFor="amendment-scope">Change scope</Label>
                  <Input
                    id="amendment-scope"
                    value={editor.changeScope}
                    onChange={(event) =>
                      setEditor({ ...editor, changeScope: event.target.value })
                    }
                    placeholder="Quantity, price, delivery…"
                  />
                </div>
                <div>
                  <Label htmlFor="amendment-evidence">Request evidence</Label>
                  <Input
                    id="amendment-evidence"
                    value={editor.evidenceReference}
                    onChange={(event) =>
                      setEditor({
                        ...editor,
                        evidenceReference: event.target.value,
                      })
                    }
                    placeholder="DMS/workflow reference"
                  />
                </div>
                <div>
                  <Label htmlFor="amendment-required">Required date</Label>
                  <Input
                    id="amendment-required"
                    type="date"
                    value={editor.requiredDate}
                    onChange={(event) =>
                      setEditor({ ...editor, requiredDate: event.target.value })
                    }
                  />
                </div>
                <div>
                  <Label htmlFor="amendment-promised">Promised date</Label>
                  <Input
                    id="amendment-promised"
                    type="date"
                    value={editor.promisedDate}
                    onChange={(event) =>
                      setEditor({ ...editor, promisedDate: event.target.value })
                    }
                  />
                </div>
                <div>
                  <Label htmlFor="amendment-supplier">Supplier ID</Label>
                  <Input
                    id="amendment-supplier"
                    value={editor.businessPartnerId}
                    onChange={(event) =>
                      setEditor({
                        ...editor,
                        businessPartnerId: event.target.value,
                      })
                    }
                  />
                </div>
                <div>
                  <Label>New approved source type</Label>
                  <Select
                    value={editor.sourceType || 'unchanged'}
                    onValueChange={(value) =>
                      setEditor({
                        ...editor,
                        sourceType: value === 'unchanged' ? '' : value,
                        sourceId:
                          value === 'unchanged' ? '' : editor.sourceId,
                      })
                    }
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="unchanged">
                        Retain current source
                      </SelectItem>
                      {sourceTypes.map((value) => (
                        <SelectItem key={value} value={value}>
                          {value}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div>
                  <Label htmlFor="amendment-source-id">
                    New approved source ID
                  </Label>
                  <Input
                    id="amendment-source-id"
                    value={editor.sourceId}
                    disabled={!editor.sourceType}
                    onChange={(event) =>
                      setEditor({ ...editor, sourceId: event.target.value })
                    }
                    placeholder="Exact approved award/contract/exception ID"
                  />
                </div>
              </div>

              <div className="overflow-x-auto rounded-md border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Item</TableHead>
                      <TableHead className="w-40">Quantity</TableHead>
                      <TableHead className="w-40">Unit price</TableHead>
                      <TableHead className="w-44">Delivery</TableHead>
                      <TableHead className="text-right">Line total</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {editor.items.map((item, index) => (
                      <TableRow key={item.purchaseOrderItemId || index}>
                        <TableCell>
                          <p className="font-medium">{item.itemDescription}</p>
                          <p className="text-xs text-muted-foreground">
                            {item.inventoryItemId}
                          </p>
                        </TableCell>
                        <TableCell>
                          <Input
                            type="number"
                            min="0.000001"
                            step="0.000001"
                            value={item.orderedQuantity}
                            onChange={(event) =>
                              updateItem(index, {
                                orderedQuantity: Number(event.target.value),
                              })
                            }
                          />
                        </TableCell>
                        <TableCell>
                          <Input
                            type="number"
                            min="0"
                            step="0.01"
                            value={item.unitPrice}
                            onChange={(event) =>
                              updateItem(index, {
                                unitPrice: Number(event.target.value),
                              })
                            }
                          />
                        </TableCell>
                        <TableCell>
                          <Input
                            type="date"
                            value={dateValue(item.expectedDeliveryDate)}
                            onChange={(event) =>
                              updateItem(index, {
                                expectedDeliveryDate:
                                  event.target.value || undefined,
                              })
                            }
                          />
                        </TableCell>
                        <TableCell className="text-right font-medium">
                          {(item.orderedQuantity * item.unitPrice).toLocaleString(
                            undefined,
                            { minimumFractionDigits: 2 }
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
              <div className="flex items-center justify-between">
                <p className="text-sm">
                  Proposed total:{' '}
                  <span className="font-semibold">
                    {proposedTotal.toLocaleString(undefined, {
                      minimumFractionDigits: 2,
                    })}
                  </span>
                </p>
                <div className="flex gap-2">
                  <Button
                    variant="outline"
                    onClick={() => setShowEditor(false)}
                    disabled={busy}
                  >
                    Close
                  </Button>
                  <Button onClick={() => void create()} disabled={busy}>
                    {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                    Save immutable draft
                  </Button>
                </div>
              </div>
            </div>
          )}

          {overview.amendments.length === 0 ? (
            <div className="rounded-lg border border-dashed py-10 text-center text-sm text-muted-foreground">
              No documented amendments exist for this purchase order.
            </div>
          ) : (
            overview.amendments.map((amendment) => (
              <Card key={amendment.id} className="shadow-none">
                <CardHeader className="pb-3">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div>
                      <CardTitle className="text-base">
                        {amendment.amendmentNumber}
                      </CardTitle>
                      <p className="text-sm text-muted-foreground">
                        Revision {amendment.baseRevisionNumber} →{' '}
                        {amendment.proposedRevisionNumber} ·{' '}
                        {amendment.changeScope}
                      </p>
                    </div>
                    <Badge className={statusTone[amendment.status]}>
                      {amendment.status}
                    </Badge>
                  </div>
                </CardHeader>
                <CardContent className="space-y-4">
                  <p className="text-sm">{amendment.reason}</p>
                  <div className="grid gap-3 text-sm md:grid-cols-4">
                    <div>
                      <p className="text-muted-foreground">Before</p>
                      <p className="font-medium">
                        {amendment.beforeTotalAmount.toLocaleString()}{' '}
                        {amendment.currency}
                      </p>
                    </div>
                    <div>
                      <p className="text-muted-foreground">Proposed</p>
                      <p className="font-medium">
                        {amendment.proposedTotalAmount.toLocaleString()}{' '}
                        {amendment.currency}
                      </p>
                    </div>
                    <div>
                      <p className="text-muted-foreground">
                        Commitment delta
                      </p>
                      <p className="font-medium">
                        {amendment.commitmentDelta.toLocaleString()}{' '}
                        {amendment.currency}
                      </p>
                    </div>
                    <div>
                      <p className="text-muted-foreground">Source</p>
                      <p className="font-medium">
                        {amendment.proposedSourceType} ·{' '}
                        {amendment.proposedSourceReference}
                      </p>
                    </div>
                  </div>
                  <div className="overflow-x-auto rounded-md border">
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Changed field</TableHead>
                          <TableHead>Before</TableHead>
                          <TableHead>After</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {amendment.diffs.map((diff) => (
                          <TableRow key={diff.path}>
                            <TableCell className="font-mono text-xs">
                              {diff.path}
                            </TableCell>
                            <TableCell>{diff.before || '—'}</TableCell>
                            <TableCell>{diff.after || '—'}</TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                  {amendment.commitmentAdjustments.map((adjustment) => (
                    <Alert key={adjustment.id}>
                      <CheckCircle2 className="h-4 w-4" />
                      <AlertTitle>
                        Budget commitment adjusted atomically
                      </AlertTitle>
                      <AlertDescription>
                        {adjustment.commitmentAmountBefore.toLocaleString()} →{' '}
                        {adjustment.commitmentAmountAfter.toLocaleString()}{' '}
                        {adjustment.currency}; budget available{' '}
                        {adjustment.budgetAvailableBefore.toLocaleString()} →{' '}
                        {adjustment.budgetAvailableAfter.toLocaleString()}.
                      </AlertDescription>
                    </Alert>
                  ))}
                  {amendment.dispatches.map((row) => (
                    <div
                      key={row.id}
                      className="rounded-md border bg-muted/30 p-3 text-sm"
                    >
                      <p className="font-medium">
                        Dispatch {row.sequence}: {row.dispatchReference}
                      </p>
                      <p className="text-muted-foreground">
                        {row.channel} · {row.documentReference} ·{' '}
                        {row.acknowledgements.length
                          ? `${row.acknowledgements.length} acknowledgement(s)`
                          : 'Awaiting supplier acknowledgement'}
                      </p>
                    </div>
                  ))}
                  <div className="flex flex-wrap gap-2">
                    {canManage && amendment.status === 'Draft' && (
                      <Button
                        size="sm"
                        onClick={() =>
                          setLifecycle({ action: 'submit', amendment })
                        }
                      >
                        <Send className="mr-2 h-4 w-4" />
                        Submit for reapproval
                      </Button>
                    )}
                    {canApprove &&
                      amendment.status === 'PendingApproval' && (
                        <>
                          <Button
                            size="sm"
                            onClick={() =>
                              setLifecycle({ action: 'approve', amendment })
                            }
                          >
                            <CheckCircle2 className="mr-2 h-4 w-4" />
                            Approve and apply
                          </Button>
                          <Button
                            size="sm"
                            variant="destructive"
                            onClick={() =>
                              setLifecycle({ action: 'reject', amendment })
                            }
                          >
                            <XCircle className="mr-2 h-4 w-4" />
                            Reject
                          </Button>
                        </>
                      )}
                    {canManage &&
                      ['Applied', 'Dispatched', 'Acknowledged'].includes(
                        amendment.status
                      ) && (
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => {
                            setDispatchFor(amendment);
                            setDispatch((current) => ({
                              ...current,
                              destination: amendment.proposedBusinessPartnerId,
                            }));
                          }}
                        >
                          <Truck className="mr-2 h-4 w-4" />
                          Dispatch signed revision
                        </Button>
                      )}
                  </div>
                  <p className="break-all font-mono text-[11px] text-muted-foreground">
                    Before {amendment.beforeIntegrityHash} · Proposed{' '}
                    {amendment.proposedIntegrityHash} · Diff{' '}
                    {amendment.diffIntegrityHash}
                  </p>
                </CardContent>
              </Card>
            ))
          )}
        </CardContent>
      </Card>

      <Dialog
        open={Boolean(lifecycle)}
        onOpenChange={(open) => !open && setLifecycle(null)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {lifecycle?.action === 'submit'
                ? 'Submit amendment for reapproval'
                : lifecycle?.action === 'approve'
                  ? 'Approve and apply amendment'
                  : 'Reject amendment'}
            </DialogTitle>
            <DialogDescription>
              The shared workflow, SOD, source, compliance, and budget controls
              are revalidated on the server.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div>
              <Label htmlFor="amendment-action-comment">Comment</Label>
              <Textarea
                id="amendment-action-comment"
                value={comment}
                onChange={(event) => setComment(event.target.value)}
              />
            </div>
            <div>
              <Label htmlFor="amendment-action-evidence">
                DMS/workflow evidence reference
              </Label>
              <Input
                id="amendment-action-evidence"
                value={evidenceReference}
                onChange={(event) => setEvidenceReference(event.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setLifecycle(null)}
              disabled={busy}
            >
              Close
            </Button>
            <Button
              variant={
                lifecycle?.action === 'reject' ? 'destructive' : 'default'
              }
              onClick={() => void runLifecycle()}
              disabled={busy}
            >
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Confirm
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(dispatchFor)}
        onOpenChange={(open) => !open && setDispatchFor(null)}
      >
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Dispatch signed approved revision</DialogTitle>
            <DialogDescription>
              Record the exact signed document, organization signature, and
              dispatch evidence before the supplier can acknowledge it.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-3 md:grid-cols-2">
            <div>
              <Label>Channel</Label>
              <Select
                value={dispatch.channel}
                onValueChange={(value) =>
                  setDispatch({
                    ...dispatch,
                    channel: value as PurchaseOrderDispatchChannel,
                  })
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {[
                    'SupplierPortal',
                    'Email',
                    'Courier',
                    'HandDelivery',
                    'Other',
                  ].map((value) => (
                    <SelectItem key={value} value={value}>
                      {value}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label htmlFor="dispatch-destination">Destination</Label>
              <Input
                id="dispatch-destination"
                value={dispatch.destination}
                onChange={(event) =>
                  setDispatch({ ...dispatch, destination: event.target.value })
                }
              />
            </div>
            <div>
              <Label htmlFor="dispatch-reference">Dispatch reference</Label>
              <Input
                id="dispatch-reference"
                value={dispatch.dispatchReference}
                onChange={(event) =>
                  setDispatch({
                    ...dispatch,
                    dispatchReference: event.target.value,
                  })
                }
              />
            </div>
            <div>
              <Label htmlFor="dispatch-document">Approved document</Label>
              <Input
                id="dispatch-document"
                value={dispatch.documentReference}
                onChange={(event) =>
                  setDispatch({
                    ...dispatch,
                    documentReference: event.target.value,
                  })
                }
              />
            </div>
            <div>
              <Label htmlFor="dispatch-signature">
                Organization signature evidence
              </Label>
              <Input
                id="dispatch-signature"
                value={dispatch.organizationSignatureEvidenceReference}
                onChange={(event) =>
                  setDispatch({
                    ...dispatch,
                    organizationSignatureEvidenceReference:
                      event.target.value,
                  })
                }
              />
            </div>
            <div>
              <Label htmlFor="dispatch-evidence">Dispatch evidence</Label>
              <Input
                id="dispatch-evidence"
                value={dispatch.dispatchEvidenceReference}
                onChange={(event) =>
                  setDispatch({
                    ...dispatch,
                    dispatchEvidenceReference: event.target.value,
                  })
                }
              />
            </div>
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setDispatchFor(null)}
              disabled={busy}
            >
              Close
            </Button>
            <Button onClick={() => void runDispatch()} disabled={busy}>
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Dispatch revision
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
