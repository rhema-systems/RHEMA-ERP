'use client';

import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  CheckCircle2,
  Clock3,
  FileCheck2,
  History,
  PackageCheck,
  Plus,
  RefreshCw,
  Search,
  ShieldCheck,
  WalletCards,
} from 'lucide-react';
import { toast } from 'sonner';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
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
import { PurchaseOrderComplianceGate } from '@/components/procurement/PurchaseOrderComplianceGate';
import { PurchaseOrderSodControl } from '@/components/procurement/PurchaseOrderSodControl';
import { useAuth } from '@/hooks/use-auth';
import {
  frameworkCallOffActionState,
  frameworkCallOffAmount,
  frameworkCallOffStatusTone,
  matchingFrameworkPrice,
} from '@/lib/procurement-framework-call-off';
import { procurementFrameworkCallOffService as service } from '@/services/procurement-framework-call-off.service';
import type {
  ProcurementPurchaseOrderComplianceDto,
  ProcurementPurchaseOrderSodReadinessDto,
} from '@/services/purchasingService';
import type {
  CreateFrameworkCallOff,
  FrameworkCallOffAgreementOption,
  FrameworkCallOffDemandOption,
  FrameworkCallOffPriceOption,
  FrameworkCallOffStatus,
} from '@/types/procurement-framework-call-off';

const statuses: FrameworkCallOffStatus[] = [
  'Draft',
  'PendingApproval',
  'Approved',
  'Issued',
  'Rejected',
  'Cancelled',
];

type LifecycleAction = 'submit' | 'approve' | 'reject' | 'issue' | 'cancel';

interface EditorLine {
  selected: boolean;
  demand: FrameworkCallOffDemandOption;
  price?: FrameworkCallOffPriceOption;
  quantity: number;
}

const dateAfter = (days: number) => {
  const date = new Date();
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
};

const toRequiredDateUtc = (value: string) =>
  new Date(`${value}T12:00:00.000Z`).toISOString();

const formatDate = (value?: string) =>
  value ? new Date(value).toLocaleDateString() : '—';

const formatDateTime = (value?: string) =>
  value ? new Date(value).toLocaleString() : '—';

const money = (value: number, currency: string) =>
  new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: currency || 'GHS',
    maximumFractionDigits: 2,
  }).format(value);

const shortHash = (value?: string) =>
  value ? `${value.slice(0, 12)}…${value.slice(-8)}` : '—';

const currencyTotals = (values?: Record<string, number>) => {
  const entries = Object.entries(values ?? {});
  return entries.length
    ? entries.map(([currency, value]) => money(value, currency)).join(' · ')
    : '—';
};

const errorMessage = (error: unknown) =>
  error instanceof Error ? error.message : 'The controlled operation failed.';

export default function FrameworkCallOffsPage() {
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const canManage = hasPermission('procurement.purchase-order.create');
  const canApprove = hasPermission('procurement.purchase-order.approve');
  const canRead =
    canManage ||
    canApprove ||
    hasPermission('procurement.records.read') ||
    hasPermission('procurement.audit.read');

  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<FrameworkCallOffStatus | 'all'>('all');
  const [selectedId, setSelectedId] = useState<string>();
  const [createOpen, setCreateOpen] = useState(false);
  const [agreementId, setAgreementId] = useState('');
  const [requisitionId, setRequisitionId] = useState('');
  const [requiredDate, setRequiredDate] = useState(dateAfter(7));
  const [warehouseId, setWarehouseId] = useState('');
  const [deliveryAddress, setDeliveryAddress] = useState('');
  const [notes, setNotes] = useState('');
  const [editorLines, setEditorLines] = useState<EditorLine[]>([]);
  const [lifecycleAction, setLifecycleAction] = useState<LifecycleAction>();
  const [comment, setComment] = useState('');
  const [evidenceReference, setEvidenceReference] = useState('');
  const [complianceReadiness, setComplianceReadiness] =
    useState<ProcurementPurchaseOrderComplianceDto | null>(null);
  const [sodReadiness, setSodReadiness] =
    useState<ProcurementPurchaseOrderSodReadinessDto | null>(null);

  const summary = useQuery({
    queryKey: ['framework-call-off-summary'],
    queryFn: service.summary,
    enabled: canRead,
  });
  const history = useQuery({
    queryKey: ['framework-call-off-history', search, status],
    queryFn: () =>
      service.search({
        search: search || undefined,
        status: status === 'all' ? undefined : status,
        page: 1,
        pageSize: 100,
      }),
    enabled: canRead,
  });
  const detail = useQuery({
    queryKey: ['framework-call-off-detail', selectedId],
    queryFn: () => service.get(selectedId ?? ''),
    enabled: canRead && Boolean(selectedId),
  });
  const options = useQuery({
    queryKey: ['framework-call-off-options'],
    queryFn: service.options,
    enabled: canRead,
  });

  const selected = detail.data;
  const actions = frameworkCallOffActionState(selected);
  const selectedComplianceReady =
    complianceReadiness?.purchaseOrderId === selected?.purchaseOrderId &&
    complianceReadiness?.isCompliant === true;
  const selectedSodApprovalReady =
    sodReadiness?.purchaseOrderId === selected?.purchaseOrderId &&
    sodReadiness?.canApprove === true;
  const selectedAgreement = options.data?.agreements.find(
    (item) => item.agreementId === agreementId
  );
  const selectedRequisition = options.data?.requisitions.find(
    (item) => item.requisitionId === requisitionId
  );

  useEffect(() => {
    if (!selectedAgreement || !selectedRequisition) {
      setEditorLines([]);
      return;
    }
    setEditorLines(
      selectedRequisition.lines.map((demand) => {
        const price = matchingFrameworkPrice(selectedAgreement, demand);
        const maximum = price?.maximumQuantity ?? demand.remainingQuantity;
        const quantity = Math.min(demand.remainingQuantity, maximum);
        const selectable =
          Boolean(price) &&
          demand.remainingQuantity > 0 &&
          quantity >= (price?.minimumQuantity ?? 0);
        return { selected: selectable, demand, price, quantity };
      })
    );
  }, [agreementId, requisitionId, selectedAgreement, selectedRequisition]);

  const editorTotal = useMemo(
    () =>
      frameworkCallOffAmount(
        editorLines.map((line) => ({
          selected: line.selected,
          quantity: line.quantity,
          unitPrice: line.price?.unitPrice ?? 0,
        }))
      ),
    [editorLines]
  );

  const refresh = async (id?: string) => {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: ['framework-call-off-summary'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['framework-call-off-history'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['framework-call-off-options'],
      }),
      id
        ? queryClient.invalidateQueries({
            queryKey: ['framework-call-off-detail', id],
          })
        : Promise.resolve(),
    ]);
  };

  const createMutation = useMutation({
    mutationFn: (request: CreateFrameworkCallOff) => service.create(request),
    onSuccess: async (created) => {
      toast.success(`${created.callOffNumber} created from governed values.`);
      setCreateOpen(false);
      setSelectedId(created.id);
      await refresh(created.id);
    },
    onError: (error) => toast.error(errorMessage(error)),
  });

  const lifecycleMutation = useMutation({
    mutationFn: async ({
      action,
      id,
      rowVersion,
    }: {
      action: LifecycleAction;
      id: string;
      rowVersion: string;
    }) => {
      const request = {
        rowVersion,
        comment: comment.trim(),
        evidence: [
          {
            referenceKind: 'ExternalReference' as const,
            reference: evidenceReference.trim(),
            label: `${action} retained evidence`,
            requirementKey: `FrameworkCallOff.${action}`,
          },
        ],
      };
      if (action === 'submit') return service.submit(id, request);
      if (action === 'approve')
        return service.decide(id, { ...request, approved: true });
      if (action === 'reject')
        return service.decide(id, { ...request, approved: false });
      if (action === 'issue') return service.issue(id, request);
      return service.cancel(id, request);
    },
    onSuccess: async (updated) => {
      toast.success(`${updated.callOffNumber} ${lifecycleAction} completed.`);
      setLifecycleAction(undefined);
      setComment('');
      setEvidenceReference('');
      await refresh(updated.id);
    },
    onError: (error) => toast.error(errorMessage(error)),
  });

  const expiryMutation = useMutation({
    mutationFn: service.processExpiryAlerts,
    onSuccess: async (result) => {
      toast.success(`${result.alerted} expiry alert(s) processed.`);
      await refresh(selectedId);
    },
    onError: (error) => toast.error(errorMessage(error)),
  });

  const openCreate = () => {
    setAgreementId('');
    setRequisitionId('');
    setRequiredDate(dateAfter(7));
    setWarehouseId('');
    setDeliveryAddress('');
    setNotes('');
    setEditorLines([]);
    setCreateOpen(true);
  };

  const submitCreate = () => {
    if (!selectedAgreement || !selectedRequisition) return;
    createMutation.mutate({
      agreementId: selectedAgreement.agreementId,
      sourceRequisitionId: selectedRequisition.requisitionId,
      requiredDateUtc: toRequiredDateUtc(requiredDate),
      deliveryWarehouseId: warehouseId || undefined,
      deliveryAddress: deliveryAddress.trim() || undefined,
      notes: notes.trim() || undefined,
      lines: editorLines.flatMap((line) =>
        line.selected && line.price
          ? [
              {
                purchaseRequisitionItemId:
                  line.demand.purchaseRequisitionItemId,
                agreementPriceLineId: line.price.agreementPriceLineId,
                quantity: line.quantity,
              },
            ]
          : []
      ),
    });
  };

  const selectedLineCount = editorLines.filter(
    (line) => line.selected && line.price
  ).length;
  const withinAuthority =
    !selectedAgreement?.currentActorMaximumCallOffAmount ||
    editorTotal <= selectedAgreement.currentActorMaximumCallOffAmount;
  const withinBalance =
    !selectedAgreement || editorTotal <= selectedAgreement.availableAmount;
  const canCreate =
    Boolean(selectedAgreement?.currentActorIsAuthorized) &&
    Boolean(selectedRequisition) &&
    Boolean(requiredDate) &&
    selectedLineCount > 0 &&
    editorTotal > 0 &&
    withinAuthority &&
    withinBalance;

  const summaryCards = [
    {
      label: 'All call-offs',
      value: summary.data?.totalCount ?? 0,
      Icon: History,
    },
    {
      label: 'Draft',
      value: summary.data?.draftCount ?? 0,
      Icon: FileCheck2,
    },
    {
      label: 'Pending approval',
      value: summary.data?.pendingApprovalCount ?? 0,
      Icon: Clock3,
    },
    {
      label: 'Approved',
      value: summary.data?.approvedCount ?? 0,
      Icon: CheckCircle2,
    },
    {
      label: 'Issued',
      value: summary.data?.issuedCount ?? 0,
      Icon: PackageCheck,
    },
    {
      label: 'Agreements expiring ≤30d',
      value: summary.data?.expiringAgreementCount ?? 0,
      Icon: AlertTriangle,
    },
  ];

  return (
    <div className="space-y-6 p-6" data-testid="framework-call-offs-page">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">
            Framework call-offs
          </h1>
          <p className="max-w-4xl text-sm text-muted-foreground">
            History-first call-off control with server-derived framework
            prices, approved requisition demand, authority thresholds,
            serializable balance commitments and the shared purchase-order
            workflow.
          </p>
        </div>
        <div className="flex gap-2">
          <Button
            variant="outline"
            disabled={!canManage || expiryMutation.isPending}
            onClick={() => expiryMutation.mutate()}
          >
            <RefreshCw className="mr-2 h-4 w-4" />
            Process expiry alerts
          </Button>
          <Button disabled={!canManage} onClick={openCreate}>
            <Plus className="mr-2 h-4 w-4" />
            New call-off
          </Button>
        </div>
      </div>

      {!canRead && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Permission required</AlertTitle>
          <AlertDescription>
            Procurement records, purchase-order creation, approval, or audit
            access is required.
          </AlertDescription>
        </Alert>
      )}

      {options.data && !options.data.purchaseOrderWorkflowReady && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Shared approval workflow is not ready</AlertTitle>
          <AlertDescription>
            {options.data.workflowReadinessMessage ??
              'Publish the TDC Purchase Order Approval workflow before submitting a call-off.'}
          </AlertDescription>
        </Alert>
      )}

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-6">
        {summaryCards.map(({ label, value, Icon }) => (
          <Card key={label}>
            <CardContent className="flex items-center justify-between p-4">
              <div>
                <p className="text-xs text-muted-foreground">{label}</p>
                <p className="text-2xl font-semibold">{value}</p>
              </div>
              <Icon className="h-5 w-5 text-muted-foreground" />
            </CardContent>
          </Card>
        ))}
      </div>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="flex items-center gap-2 text-base">
            <WalletCards className="h-4 w-4" />
            Agreement balance register
          </CardTitle>
        </CardHeader>
        <CardContent className="grid gap-3 md:grid-cols-3">
          <Info
            label="Committed"
            value={currencyTotals(summary.data?.committedByCurrency)}
          />
          <Info
            label="Issued"
            value={currencyTotals(summary.data?.issuedByCurrency)}
          />
          <Info
            label="Available"
            value={currencyTotals(summary.data?.availableByCurrency)}
          />
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Call-off history</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap gap-3">
            <div className="relative min-w-64 flex-1">
              <Search className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
              <Input
                className="pl-9"
                placeholder="Call-off, agreement, PO, requisition or supplier"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
              />
            </div>
            <Select
              value={status}
              onValueChange={(value) =>
                setStatus(value as FrameworkCallOffStatus | 'all')
              }
            >
              <SelectTrigger className="w-52">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All statuses</SelectItem>
                {statuses.map((value) => (
                  <SelectItem key={value} value={value}>
                    {value}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Call-off / agreement</TableHead>
                  <TableHead>Supplier</TableHead>
                  <TableHead>Demand / PO</TableHead>
                  <TableHead>Required / expiry</TableHead>
                  <TableHead className="text-right">Amount / balance</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {history.data?.items.map((callOff) => (
                  <TableRow
                    key={callOff.id}
                    className="cursor-pointer"
                    data-testid={`framework-call-off-row-${callOff.id}`}
                    data-state={
                      selectedId === callOff.id ? 'selected' : undefined
                    }
                    onClick={() => setSelectedId(callOff.id)}
                  >
                    <TableCell>
                      <p className="font-medium">{callOff.callOffNumber}</p>
                      <p className="text-xs text-muted-foreground">
                        {callOff.agreementNumber} · v{callOff.agreementVersion}
                      </p>
                    </TableCell>
                    <TableCell>
                      {callOff.supplierCode} · {callOff.supplierName}
                    </TableCell>
                    <TableCell>
                      <p>{callOff.sourceRequisitionNumber}</p>
                      <p className="text-xs text-muted-foreground">
                        {callOff.purchaseOrderNumber} · {callOff.lineCount} line(s)
                      </p>
                    </TableCell>
                    <TableCell>
                      <p>{formatDate(callOff.requiredDateUtc)}</p>
                      <p className="text-xs text-muted-foreground">
                        {callOff.daysToExpiry} day(s) to agreement expiry
                      </p>
                    </TableCell>
                    <TableCell className="text-right">
                      <p>{money(callOff.totalAmount, callOff.currencyCode)}</p>
                      <p className="text-xs text-muted-foreground">
                        {money(
                          callOff.agreementAvailableAmount,
                          callOff.currencyCode
                        )}{' '}
                        available
                      </p>
                    </TableCell>
                    <TableCell>
                      <Badge
                        variant={frameworkCallOffStatusTone(callOff.status)}
                      >
                        {callOff.status}
                      </Badge>
                    </TableCell>
                  </TableRow>
                ))}
                {!history.isLoading && !history.data?.items.length && (
                  <TableRow>
                    <TableCell
                      colSpan={6}
                      className="py-10 text-center text-muted-foreground"
                    >
                      No call-offs match the current filter.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      {selected && (
        <div className="space-y-4" data-testid="framework-call-off-detail">
          <Card>
            <CardHeader>
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <CardTitle className="text-lg">
                    {selected.callOffNumber}
                  </CardTitle>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {selected.supplierCode} · {selected.supplierName} ·{' '}
                    {selected.agreementNumber} v{selected.agreementVersion}
                  </p>
                </div>
                <div className="flex flex-wrap gap-2">
                  {actions.canSubmit && canManage && (
                    <Button
                      disabled={!selectedComplianceReady}
                      onClick={() => setLifecycleAction('submit')}
                    >
                      Submit
                    </Button>
                  )}
                  {actions.canApprove && canApprove && (
                    <Button
                      disabled={
                        !selectedComplianceReady || !selectedSodApprovalReady
                      }
                      onClick={() => setLifecycleAction('approve')}
                    >
                      Approve & commit
                    </Button>
                  )}
                  {actions.canReject && canApprove && (
                    <Button
                      variant="destructive"
                      onClick={() => setLifecycleAction('reject')}
                    >
                      Reject
                    </Button>
                  )}
                  {actions.canIssue && canManage && (
                    <Button onClick={() => setLifecycleAction('issue')}>
                      Issue call-off
                    </Button>
                  )}
                  {actions.canCancel && canManage && (
                    <Button
                      variant="outline"
                      onClick={() => setLifecycleAction('cancel')}
                    >
                      Cancel
                    </Button>
                  )}
                </div>
              </div>
            </CardHeader>
            <CardContent className="space-y-5">
              <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
                <Info label="Status" value={selected.status} />
                <Info
                  label="Call-off total"
                  value={money(selected.totalAmount, selected.currencyCode)}
                />
                <Info
                  label="Agreement available"
                  value={money(
                    selected.agreementAvailableAmount,
                    selected.currencyCode
                  )}
                />
                <Info
                  label="Required date"
                  value={formatDate(selected.requiredDateUtc)}
                />
                <Info
                  label="Source demand"
                  value={selected.sourceRequisitionNumber}
                />
                <Info
                  label="Linked purchase order"
                  value={`${selected.purchaseOrderNumber} · ${selected.purchaseOrderStatus}`}
                />
                <Info
                  label="Authority"
                  value={`${selected.authorityKind} · ${selected.authorityValue}`}
                />
                <Info
                  label="Authority threshold"
                  value={
                    selected.authorityThreshold
                      ? money(
                          selected.authorityThreshold,
                          selected.currencyCode
                        )
                      : 'No monetary cap'
                  }
                />
                <Info
                  label="Governed price list"
                  value={`${selected.priceListReference} · v${selected.priceListVersion}`}
                />
                <Info
                  label="Shared workflow"
                  value={selected.workflowInstanceId ?? 'Not started'}
                />
                <Info
                  label="Created by"
                  value={`${selected.createdByName} · ${formatDateTime(
                    selected.createdAtUtc
                  )}`}
                />
                <Info
                  label="Integrity"
                  value={shortHash(selected.integrityHash)}
                />
              </div>

              <Alert>
                <ShieldCheck className="h-4 w-4" />
                <AlertTitle>Enforced control boundary</AlertTitle>
                <AlertDescription>
                  Supplier eligibility, effective agreement revision, authority,
                  source demand, governed prices, workflow outcome and ledger
                  movements are revalidated by the server. Approval commits the
                  balance; issue remains a separate controlled transition.
                </AlertDescription>
              </Alert>

              <div>
                <h3 className="mb-2 font-medium">Server-derived lines</h3>
                <div className="overflow-x-auto rounded-md border">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Item</TableHead>
                        <TableHead>Demand allocation</TableHead>
                        <TableHead className="text-right">Quantity</TableHead>
                        <TableHead className="text-right">Unit price</TableHead>
                        <TableHead className="text-right">Line total</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {selected.lines.map((line) => (
                        <TableRow key={line.id}>
                          <TableCell>
                            <p className="font-medium">
                              {line.itemCode} · {line.itemName}
                            </p>
                            <p className="text-xs text-muted-foreground">
                              {line.unitOfMeasure} · price{' '}
                              {shortHash(line.priceIntegrityHash)}
                            </p>
                          </TableCell>
                          <TableCell>
                            {line.sourceDemandAllocatedQuantity} /{' '}
                            {line.sourceDemandQuantity} allocated ·{' '}
                            {line.sourceDemandRemainingQuantity} remaining
                          </TableCell>
                          <TableCell className="text-right">
                            {line.quantity}
                          </TableCell>
                          <TableCell className="text-right">
                            {money(line.unitPrice, selected.currencyCode)}
                          </TableCell>
                          <TableCell className="text-right">
                            {money(line.lineTotal, selected.currencyCode)}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              </div>

              <div>
                <h3 className="mb-2 font-medium">
                  Immutable balance movements
                </h3>
                <div className="overflow-x-auto rounded-md border">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Movement</TableHead>
                        <TableHead>Actor / time</TableHead>
                        <TableHead className="text-right">Amount</TableHead>
                        <TableHead className="text-right">Before</TableHead>
                        <TableHead className="text-right">After</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {selected.balanceMovements.map((movement) => (
                        <TableRow key={movement.id}>
                          <TableCell>
                            <Badge variant="outline">
                              {movement.movementType}
                            </Badge>
                          </TableCell>
                          <TableCell>
                            <p>{movement.actorName}</p>
                            <p className="text-xs text-muted-foreground">
                              {formatDateTime(movement.occurredAtUtc)}
                            </p>
                          </TableCell>
                          <TableCell className="text-right">
                            {money(movement.amount, selected.currencyCode)}
                          </TableCell>
                          <TableCell className="text-right">
                            {money(
                              movement.balanceBefore,
                              selected.currencyCode
                            )}
                          </TableCell>
                          <TableCell className="text-right">
                            {money(
                              movement.balanceAfter,
                              selected.currencyCode
                            )}
                          </TableCell>
                        </TableRow>
                      ))}
                      {!selected.balanceMovements.length && (
                        <TableRow>
                          <TableCell
                            colSpan={5}
                            className="py-8 text-center text-muted-foreground"
                          >
                            No balance movement is posted while this call-off
                            remains unapproved.
                          </TableCell>
                        </TableRow>
                      )}
                    </TableBody>
                  </Table>
                </div>
              </div>
            </CardContent>
          </Card>
          <PurchaseOrderComplianceGate
            purchaseOrderId={selected.purchaseOrderId}
            status={selected.purchaseOrderStatus}
            onReadinessChange={setComplianceReadiness}
          />
          <PurchaseOrderSodControl
            purchaseOrderId={selected.purchaseOrderId}
            status={selected.purchaseOrderStatus}
            onReadinessChange={setSodReadiness}
          />
        </div>
      )}

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-h-[92vh] max-w-5xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>New governed framework call-off</DialogTitle>
          </DialogHeader>
          <div className="space-y-5">
            <Alert>
              <ShieldCheck className="h-4 w-4" />
              <AlertTitle>Commercial values are not editable</AlertTitle>
              <AlertDescription>
                Supplier, currency, price, UOM, demand and authority are resolved
                from the effective framework and approved requisition.
              </AlertDescription>
            </Alert>

            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Effective framework agreement</Label>
                <Select value={agreementId} onValueChange={setAgreementId}>
                  <SelectTrigger data-testid="call-off-agreement">
                    <SelectValue placeholder="Select agreement" />
                  </SelectTrigger>
                  <SelectContent>
                    {options.data?.agreements.map((agreement) => (
                      <SelectItem
                        key={agreement.agreementId}
                        value={agreement.agreementId}
                        disabled={
                          !agreement.currentActorIsAuthorized ||
                          agreement.availableAmount <= 0
                        }
                      >
                        {agreement.agreementNumber} v{agreement.version} ·{' '}
                        {agreement.supplierName} ·{' '}
                        {money(
                          agreement.availableAmount,
                          agreement.currencyCode
                        )}{' '}
                        available
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Approved purchase requisition</Label>
                <Select value={requisitionId} onValueChange={setRequisitionId}>
                  <SelectTrigger data-testid="call-off-requisition">
                    <SelectValue placeholder="Select approved demand" />
                  </SelectTrigger>
                  <SelectContent>
                    {options.data?.requisitions.map((requisition) => (
                      <SelectItem
                        key={requisition.requisitionId}
                        value={requisition.requisitionId}
                      >
                        {requisition.requisitionNumber} ·{' '}
                        {
                          requisition.lines.filter(
                            (line) => line.remainingQuantity > 0
                          ).length
                        }{' '}
                        open line(s)
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="call-off-required-date">Required date</Label>
                <Input
                  id="call-off-required-date"
                  type="date"
                  value={requiredDate}
                  min={new Date().toISOString().slice(0, 10)}
                  onChange={(event) => setRequiredDate(event.target.value)}
                />
              </div>
              <div className="space-y-2">
                <Label>Delivery warehouse</Label>
                <Select
                  value={warehouseId || 'source'}
                  onValueChange={(value) =>
                    setWarehouseId(value === 'source' ? '' : value)
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="source">
                      Use requisition warehouse
                    </SelectItem>
                    {options.data?.warehouses.map((warehouse) => (
                      <SelectItem
                        key={warehouse.warehouseId}
                        value={warehouse.warehouseId}
                      >
                        {warehouse.code} · {warehouse.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="call-off-address">
                  Delivery address override
                </Label>
                <Input
                  id="call-off-address"
                  value={deliveryAddress}
                  maxLength={500}
                  placeholder={
                    selectedRequisition?.deliveryAddress ||
                    'Use requisition address'
                  }
                  onChange={(event) => setDeliveryAddress(event.target.value)}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="call-off-notes">Notes</Label>
                <Input
                  id="call-off-notes"
                  value={notes}
                  maxLength={1000}
                  onChange={(event) => setNotes(event.target.value)}
                />
              </div>
            </div>

            {selectedAgreement && (
              <div className="grid gap-3 md:grid-cols-4">
                <Info
                  label="Supplier"
                  value={`${selectedAgreement.supplierCode} · ${selectedAgreement.supplierName}`}
                />
                <Info
                  label="Available balance"
                  value={money(
                    selectedAgreement.availableAmount,
                    selectedAgreement.currencyCode
                  )}
                />
                <Info
                  label="Your authority"
                  value={
                    selectedAgreement.currentActorMaximumCallOffAmount
                      ? money(
                          selectedAgreement.currentActorMaximumCallOffAmount,
                          selectedAgreement.currencyCode
                        )
                      : 'Authorized · no monetary cap'
                  }
                />
                <Info
                  label="Effective through"
                  value={formatDate(selectedAgreement.effectiveEndUtc)}
                />
              </div>
            )}

            <div>
              <div className="mb-2 flex items-center justify-between">
                <h3 className="font-medium">Approved demand and governed price</h3>
                <p className="text-sm font-medium">
                  {selectedAgreement
                    ? money(editorTotal, selectedAgreement.currencyCode)
                    : '—'}
                </p>
              </div>
              <div className="overflow-x-auto rounded-md border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-16">Use</TableHead>
                      <TableHead>Demand item</TableHead>
                      <TableHead>Governed price</TableHead>
                      <TableHead>Demand remaining</TableHead>
                      <TableHead className="w-36">Quantity</TableHead>
                      <TableHead className="text-right">Line total</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {editorLines.map((line, index) => (
                      <TableRow key={line.demand.purchaseRequisitionItemId}>
                        <TableCell>
                          <input
                            type="checkbox"
                            checked={line.selected}
                            disabled={!line.price}
                            onChange={(event) =>
                              setEditorLines((current) =>
                                current.map((item, itemIndex) =>
                                  itemIndex === index
                                    ? {
                                        ...item,
                                        selected: event.target.checked,
                                      }
                                    : item
                                )
                              )
                            }
                          />
                        </TableCell>
                        <TableCell>
                          <p className="font-medium">
                            {line.demand.itemDescription}
                          </p>
                          <p className="text-xs text-muted-foreground">
                            {line.demand.unitOfMeasure}
                          </p>
                        </TableCell>
                        <TableCell>
                          {line.price ? (
                            <>
                              <p>
                                {line.price.itemCode} ·{' '}
                                {money(
                                  line.price.unitPrice,
                                  selectedAgreement?.currencyCode ?? 'GHS'
                                )}
                              </p>
                              <p className="text-xs text-muted-foreground">
                                min {line.price.minimumQuantity}
                                {line.price.maximumQuantity
                                  ? ` · max ${line.price.maximumQuantity}`
                                  : ''}
                              </p>
                            </>
                          ) : (
                            <Badge variant="destructive">
                              No item/UOM price
                            </Badge>
                          )}
                        </TableCell>
                        <TableCell>
                          {line.demand.remainingQuantity} /{' '}
                          {line.demand.demandQuantity}
                        </TableCell>
                        <TableCell>
                          <Input
                            type="number"
                            min={line.price?.minimumQuantity ?? 0.0001}
                            max={Math.min(
                              line.demand.remainingQuantity,
                              line.price?.maximumQuantity ??
                                line.demand.remainingQuantity
                            )}
                            step="0.0001"
                            value={line.quantity}
                            disabled={!line.selected || !line.price}
                            onChange={(event) =>
                              setEditorLines((current) =>
                                current.map((item, itemIndex) =>
                                  itemIndex === index
                                    ? {
                                        ...item,
                                        quantity: Number(event.target.value),
                                      }
                                    : item
                                )
                              )
                            }
                          />
                        </TableCell>
                        <TableCell className="text-right">
                          {line.price
                            ? money(
                                line.quantity * line.price.unitPrice,
                                selectedAgreement?.currencyCode ?? 'GHS'
                              )
                            : '—'}
                        </TableCell>
                      </TableRow>
                    ))}
                    {!editorLines.length && (
                      <TableRow>
                        <TableCell
                          colSpan={6}
                          className="py-8 text-center text-muted-foreground"
                        >
                          Select an agreement and approved requisition to
                          resolve demand against its immutable price list.
                        </TableCell>
                      </TableRow>
                    )}
                  </TableBody>
                </Table>
              </div>
            </div>

            {selectedAgreement &&
              (!selectedAgreement.currentActorIsAuthorized ||
                !withinAuthority ||
                !withinBalance) && (
                <Alert variant="destructive">
                  <AlertTriangle className="h-4 w-4" />
                  <AlertTitle>Call-off cannot be created</AlertTitle>
                  <AlertDescription>
                    {!selectedAgreement.currentActorIsAuthorized
                      ? 'The current actor has no effective authority for this agreement.'
                      : !withinAuthority
                        ? 'The selected total exceeds the current actor authority threshold.'
                        : 'The selected total exceeds the available framework balance.'}
                  </AlertDescription>
                </Alert>
              )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>
              Close
            </Button>
            <Button
              data-testid="create-framework-call-off"
              disabled={!canCreate || createMutation.isPending}
              onClick={submitCreate}
            >
              Create controlled draft
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(lifecycleAction)}
        onOpenChange={(open) => {
          if (!open) setLifecycleAction(undefined);
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {lifecycleAction
                ? `${lifecycleAction[0].toUpperCase()}${lifecycleAction.slice(1)} call-off`
                : 'Controlled call-off action'}
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="call-off-comment">Decision comment</Label>
              <Textarea
                id="call-off-comment"
                value={comment}
                maxLength={1000}
                onChange={(event) => setComment(event.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="call-off-evidence">
                Retained evidence reference
              </Label>
              <Input
                id="call-off-evidence"
                value={evidenceReference}
                placeholder="Workflow minute, approval memo, or issue reference"
                onChange={(event) => setEvidenceReference(event.target.value)}
              />
            </div>
            <Alert>
              <ShieldCheck className="h-4 w-4" />
              <AlertTitle>Shared-control transition</AlertTitle>
              <AlertDescription>
                The API revalidates workflow outcome, supplier eligibility,
                framework effectiveness, authority, demand, prices and balance
                under the current tenant before committing this action.
              </AlertDescription>
            </Alert>
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setLifecycleAction(undefined)}
            >
              Close
            </Button>
            <Button
              variant={
                lifecycleAction === 'reject' || lifecycleAction === 'cancel'
                  ? 'destructive'
                  : 'default'
              }
              disabled={
                !selected ||
                !comment.trim() ||
                !evidenceReference.trim() ||
                lifecycleMutation.isPending
              }
              onClick={() => {
                if (selected && lifecycleAction)
                  lifecycleMutation.mutate({
                    action: lifecycleAction,
                    id: selected.id,
                    rowVersion: selected.rowVersion,
                  });
              }}
            >
              Confirm
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function Info({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border p-3">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="mt-1 break-words text-sm font-medium">{value}</p>
    </div>
  );
}
