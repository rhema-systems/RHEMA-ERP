'use client';

import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  CheckCircle2,
  ClipboardCheck,
  Clock3,
  Plus,
  RefreshCw,
  Save,
  Search,
  ShieldCheck,
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
import { useAuth } from '@/hooks/use-auth';
import { procurementSupplierDueDiligenceService as service } from '@/services/procurement-supplier-due-diligence.service';
import type {
  CreateSupplierDueDiligence,
  SaveSupplierDueDiligenceCheck,
  SupplierDueDiligence,
  SupplierDueDiligenceCheckStatus,
  SupplierDueDiligenceCheckType,
  SupplierDueDiligenceLifecycleRequest,
  SupplierDueDiligenceReviewType,
  SupplierDueDiligenceStatus,
} from '@/types/procurement-supplier-due-diligence';

const checkTypes: Array<{
  value: SupplierDueDiligenceCheckType;
  label: string;
}> = [
  { value: 'PpaDebarment', label: 'PPA debarment' },
  { value: 'GraTaxClearance', label: 'GRA / tax clearance' },
  { value: 'Sanctions', label: 'Sanctions screening' },
  { value: 'BankVerification', label: 'Bank verification' },
  { value: 'FinancialStability', label: 'Financial stability' },
  { value: 'Reputation', label: 'Reputation' },
];

const statuses: SupplierDueDiligenceStatus[] = [
  'Draft',
  'PendingApproval',
  'Approved',
  'Rejected',
  'Expired',
  'Superseded',
];

const checkStatuses: SupplierDueDiligenceCheckStatus[] = [
  'Pending',
  'Clear',
  'Adverse',
  'NotApplicable',
];

const emptyChecks = (): SaveSupplierDueDiligenceCheck[] =>
  checkTypes.map(({ value }) => ({
    checkType: value,
    status: 'Pending',
    sourceName: '',
    sourceReference: '',
    evidence: [],
  }));

const asInputDate = (value?: string) => (value ? value.slice(0, 16) : '');
const asUtc = (value: string) =>
  value ? new Date(value).toISOString() : undefined;
const formatDate = (value?: string) =>
  value ? new Date(value).toLocaleDateString() : '—';
const shortHash = (value?: string) =>
  value ? `${value.slice(0, 12)}…${value.slice(-8)}` : '—';

const statusVariant = (
  status: SupplierDueDiligenceStatus
): 'default' | 'secondary' | 'destructive' | 'outline' => {
  if (status === 'Approved') return 'default';
  if (status === 'Rejected' || status === 'Expired') return 'destructive';
  if (status === 'PendingApproval') return 'secondary';
  return 'outline';
};

export default function SupplierDueDiligencePage() {
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const canRead =
    hasPermission('procurement.supplier.review') ||
    hasPermission('procurement.supplier.manage') ||
    hasPermission('procurement.supplier.approve');
  const canManage = hasPermission('procurement.supplier.manage');
  const canApprove = hasPermission('procurement.supplier.approve');

  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<SupplierDueDiligenceStatus | 'all'>(
    'all'
  );
  const [selectedId, setSelectedId] = useState<string>();
  const [createOpen, setCreateOpen] = useState(false);
  const [lifecycleAction, setLifecycleAction] = useState<
    'submit' | 'approve' | 'reject'
  >();
  const [createRequest, setCreateRequest] =
    useState<CreateSupplierDueDiligence>({
      businessPartnerId: '',
      reviewType: 'Initial',
      workflowDefinitionId: '',
    });
  const [notes, setNotes] = useState('');
  const [checks, setChecks] =
    useState<SaveSupplierDueDiligenceCheck[]>(emptyChecks);
  const [lifecycleComment, setLifecycleComment] = useState('');
  const [lifecycleEvidence, setLifecycleEvidence] = useState('');

  const summary = useQuery({
    queryKey: ['supplier-due-diligence-summary'],
    queryFn: service.summary,
    enabled: canRead,
  });
  const history = useQuery({
    queryKey: ['supplier-due-diligence-history', search, status],
    queryFn: () =>
      service.search({
        search: search || undefined,
        status: status === 'all' ? undefined : status,
        page: 1,
        pageSize: 100,
      }),
    enabled: canRead,
  });
  const suppliers = useQuery({
    queryKey: ['supplier-due-diligence-suppliers'],
    queryFn: service.supplierOptions,
    enabled: canRead,
  });
  const workflows = useQuery({
    queryKey: ['supplier-due-diligence-workflows'],
    queryFn: service.workflowOptions,
    enabled: canRead,
  });
  const detail = useQuery({
    queryKey: ['supplier-due-diligence-detail', selectedId],
    queryFn: () => service.get(selectedId ?? ''),
    enabled: canRead && Boolean(selectedId),
  });

  useEffect(() => {
    const value = detail.data;
    if (!value) return;
    setNotes(value.notes ?? '');
    setChecks(
      value.checks.map((check) => ({
        checkType: check.checkType,
        status: check.status,
        sourceName: check.sourceName,
        sourceReference: check.sourceReference,
        checkedAtUtc: check.checkedAtUtc,
        validUntilUtc: check.validUntilUtc,
        notes: check.notes,
        evidence: check.evidence.map((evidence) => ({
          referenceKind: evidence.referenceKind,
          referenceId:
            evidence.workflowEvidenceDocumentId ?? evidence.fileUploadRecordId,
          reference:
            evidence.referenceKind === 'ExternalReference'
              ? evidence.reference
              : undefined,
          label: evidence.label,
          requirementKey: evidence.requirementKey,
        })),
      }))
    );
  }, [detail.data]);

  const refresh = async (id?: string) => {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: ['supplier-due-diligence-summary'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['supplier-due-diligence-history'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['supplier-due-diligence-suppliers'],
      }),
      id
        ? queryClient.invalidateQueries({
            queryKey: ['supplier-due-diligence-detail', id],
          })
        : Promise.resolve(),
    ]);
  };

  const createMutation = useMutation({
    mutationFn: service.create,
    onSuccess: async (value) => {
      setCreateOpen(false);
      setSelectedId(value.id);
      setCreateRequest({
        businessPartnerId: '',
        reviewType: 'Initial',
        workflowDefinitionId: '',
      });
      await refresh(value.id);
      toast.success('Due-diligence draft created.');
    },
    onError: () => toast.error('The due-diligence draft could not be created.'),
  });

  const saveMutation = useMutation({
    mutationFn: async (value: SupplierDueDiligence) =>
      service.update(value.id, {
        rowVersion: value.rowVersion,
        notes,
        checks,
      }),
    onSuccess: async (value) => {
      await refresh(value.id);
      toast.success('Reviewer checks and evidence saved.');
    },
    onError: () =>
      toast.error('Checks could not be saved. Refresh and verify the evidence.'),
  });

  const lifecycleMutation = useMutation({
    mutationFn: async ({
      value,
      action,
      request,
    }: {
      value: SupplierDueDiligence;
      action: 'submit' | 'approve' | 'reject';
      request: SupplierDueDiligenceLifecycleRequest;
    }) => service[action](value.id, request),
    onSuccess: async (value) => {
      setLifecycleAction(undefined);
      setLifecycleComment('');
      setLifecycleEvidence('');
      await refresh(value.id);
      toast.success('Due-diligence lifecycle updated.');
    },
    onError: () =>
      toast.error(
        'The lifecycle action was blocked. Verify workflow outcome, independence, evidence, and current policy.'
      ),
  });

  const expiryMutation = useMutation({
    mutationFn: service.processExpiry,
    onSuccess: async ({ processed }) => {
      await refresh(selectedId);
      toast.success(`${processed} expired review(s) processed.`);
    },
    onError: () => toast.error('Expiry processing could not be completed.'),
  });

  const selected = detail.data;
  const selectedActions = useMemo(
    () => new Set(selected?.allowedActions ?? []),
    [selected]
  );

  const updateCheck = (
    type: SupplierDueDiligenceCheckType,
    patch: Partial<SaveSupplierDueDiligenceCheck>
  ) =>
    setChecks((current) =>
      current.map((check) =>
        check.checkType === type ? { ...check, ...patch } : check
      )
    );

  const updateExternalEvidence = (
    type: SupplierDueDiligenceCheckType,
    reference: string
  ) =>
    setChecks((current) =>
      current.map((check) => {
        if (check.checkType !== type) return check;
        const retained = check.evidence.filter(
          (item) => item.referenceKind !== 'ExternalReference'
        );
        return {
          ...check,
          evidence: reference.trim()
            ? [
                ...retained,
                {
                  referenceKind: 'ExternalReference',
                  reference: reference.trim(),
                  label: `${type} reviewer evidence`,
                  requirementKey: type,
                },
              ]
            : retained,
        };
      })
    );

  const submitLifecycle = () => {
    if (!selected || !lifecycleAction) return;
    if (!lifecycleComment.trim() || !lifecycleEvidence.trim()) {
      toast.error('A comment and retained evidence reference are required.');
      return;
    }
    lifecycleMutation.mutate({
      value: selected,
      action: lifecycleAction,
      request: {
        rowVersion: selected.rowVersion,
        comment: lifecycleComment.trim(),
        evidence: [
          {
            referenceKind: 'ExternalReference',
            reference: lifecycleEvidence.trim(),
            label: `${lifecycleAction} evidence`,
            requirementKey: `SupplierDueDiligence.${lifecycleAction}`,
          },
        ],
      },
    });
  };

  const summaryCards = [
    { label: 'Total', value: summary.data?.totalReviews ?? 0, Icon: ClipboardCheck },
    { label: 'Draft', value: summary.data?.draftCount ?? 0, Icon: Save },
    { label: 'Pending', value: summary.data?.pendingApprovalCount ?? 0, Icon: Clock3 },
    {
      label: 'Current',
      value: summary.data?.currentApprovedCount ?? 0,
      Icon: ShieldCheck,
    },
    {
      label: 'Due / expired',
      value: summary.data?.dueOrExpiredCount ?? 0,
      Icon: RefreshCw,
    },
    { label: 'Adverse', value: summary.data?.adverseCount ?? 0, Icon: AlertTriangle },
  ];

  return (
    <div className="space-y-6 p-6" data-testid="supplier-due-diligence-page">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">
            Supplier due diligence
          </h1>
          <p className="text-sm text-muted-foreground">
            Initial review and annual reassessment with exact policy, reviewer,
            evidence, workflow, expiry, and supersession lineage.
          </p>
        </div>
        <div className="flex gap-2">
          <Button
            variant="outline"
            disabled={!canManage || expiryMutation.isPending}
            onClick={() => expiryMutation.mutate()}
          >
            <RefreshCw className="mr-2 h-4 w-4" />
            Process expiry
          </Button>
          <Button
            disabled={!canManage || !summary.data?.policyAvailable}
            onClick={() => setCreateOpen(true)}
          >
            <Plus className="mr-2 h-4 w-4" />
            New review
          </Button>
        </div>
      </div>

      {!canRead && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Permission required</AlertTitle>
          <AlertDescription>
            Supplier review, management, or approval permission is required.
          </AlertDescription>
        </Alert>
      )}

      {summary.data && !summary.data.policyAvailable && (
        <Alert variant="destructive" data-testid="due-diligence-policy-gate">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>DEC-011 release configuration required</AlertTitle>
          <AlertDescription>
            {summary.data.policyReleaseGate ??
              'Publish and evidence one effective DEC-011 policy before creating reviews.'}
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
        <CardHeader>
          <CardTitle className="text-base">Review history</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap gap-2">
            <div className="relative min-w-64 flex-1">
              <Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                className="pl-9"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Supplier, code, or review reference"
              />
            </div>
            <Select
              value={status}
              onValueChange={(value) =>
                setStatus(value as SupplierDueDiligenceStatus | 'all')
              }
            >
              <SelectTrigger className="w-52">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All statuses</SelectItem>
                {statuses.map((item) => (
                  <SelectItem key={item} value={item}>
                    {item}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Review</TableHead>
                  <TableHead>Supplier</TableHead>
                  <TableHead>Cycle</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Outcome</TableHead>
                  <TableHead>Due</TableHead>
                  <TableHead>Evidence</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(history.data?.items ?? []).map((item) => (
                  <TableRow
                    key={item.id}
                    className="cursor-pointer"
                    data-state={selectedId === item.id ? 'selected' : undefined}
                    onClick={() => setSelectedId(item.id)}
                  >
                    <TableCell className="font-medium">
                      {item.reviewReference}
                    </TableCell>
                    <TableCell>
                      {item.partnerCode} · {item.partnerName}
                    </TableCell>
                    <TableCell>
                      {item.reviewType} #{item.cycleNumber}
                    </TableCell>
                    <TableCell>
                      <Badge variant={statusVariant(item.status)}>
                        {item.status}
                      </Badge>
                    </TableCell>
                    <TableCell>{item.outcome}</TableCell>
                    <TableCell>{formatDate(item.reviewPeriodEndUtc)}</TableCell>
                    <TableCell>{item.evidenceCount}</TableCell>
                  </TableRow>
                ))}
                {!history.isLoading && !history.data?.items.length && (
                  <TableRow>
                    <TableCell
                      colSpan={7}
                      className="h-28 text-center text-muted-foreground"
                    >
                      No supplier due-diligence reviews match this filter.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      {selected && (
        <Card data-testid="supplier-due-diligence-detail">
          <CardHeader>
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <CardTitle className="text-base">
                  {selected.reviewReference} · {selected.partnerName}
                </CardTitle>
                <p className="mt-1 text-xs text-muted-foreground">
                  {selected.policyProfileCode} v{selected.policyProfileVersion}{' '}
                  · DEC-011 {shortHash(selected.policyValueHash)} · integrity{' '}
                  {shortHash(selected.integrityHash)}
                </p>
              </div>
              <div className="flex flex-wrap gap-2">
                {selectedActions.has('Submit') && canManage && (
                  <Button
                    variant="outline"
                    onClick={() => setLifecycleAction('submit')}
                  >
                    Submit
                  </Button>
                )}
                {selectedActions.has('Approve') && canApprove && (
                  <Button onClick={() => setLifecycleAction('approve')}>
                    Approve
                  </Button>
                )}
                {selectedActions.has('Reject') && canApprove && (
                  <Button
                    variant="destructive"
                    onClick={() => setLifecycleAction('reject')}
                  >
                    Reject
                  </Button>
                )}
              </div>
            </div>
          </CardHeader>
          <CardContent className="space-y-5">
            <div className="grid gap-3 text-sm md:grid-cols-4">
              <div>
                <p className="text-xs text-muted-foreground">Review period</p>
                <p>
                  {formatDate(selected.reviewPeriodStartUtc)} –{' '}
                  {formatDate(selected.reviewPeriodEndUtc)}
                </p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Workflow</p>
                <p className="font-mono text-xs">
                  {selected.workflowInstanceId ?? 'Not submitted'}
                </p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Clear checks</p>
                <p>
                  {selected.clearCheckCount} / {checkTypes.length}
                </p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Current state</p>
                <p className="flex items-center gap-1">
                  {selected.isCurrent ? (
                    <CheckCircle2 className="h-4 w-4 text-emerald-600" />
                  ) : (
                    <AlertTriangle className="h-4 w-4 text-amber-600" />
                  )}
                  {selected.isCurrent ? 'Current' : 'Not current'}
                </p>
              </div>
            </div>

            {checks.map((check) => {
              const label =
                checkTypes.find((item) => item.value === check.checkType)
                  ?.label ?? check.checkType;
              const externalEvidence =
                check.evidence.find(
                  (item) => item.referenceKind === 'ExternalReference'
                )?.reference ?? '';
              return (
                <div
                  key={check.checkType}
                  className="space-y-3 rounded-lg border p-4"
                >
                  <div className="flex items-center justify-between gap-2">
                    <h3 className="font-medium">{label}</h3>
                    <Badge variant="outline">{check.status}</Badge>
                  </div>
                  <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
                    <div className="space-y-1">
                      <Label>Status</Label>
                      <Select
                        value={check.status}
                        disabled={selected.status !== 'Draft' || !canManage}
                        onValueChange={(value) =>
                          updateCheck(check.checkType, {
                            status: value as SupplierDueDiligenceCheckStatus,
                          })
                        }
                      >
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {checkStatuses.map((item) => (
                            <SelectItem key={item} value={item}>
                              {item}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="space-y-1">
                      <Label>Authoritative source</Label>
                      <Input
                        value={check.sourceName}
                        disabled={selected.status !== 'Draft' || !canManage}
                        onChange={(event) =>
                          updateCheck(check.checkType, {
                            sourceName: event.target.value,
                          })
                        }
                      />
                    </div>
                    <div className="space-y-1">
                      <Label>Source reference</Label>
                      <Input
                        value={check.sourceReference}
                        disabled={selected.status !== 'Draft' || !canManage}
                        onChange={(event) =>
                          updateCheck(check.checkType, {
                            sourceReference: event.target.value,
                          })
                        }
                      />
                    </div>
                    <div className="space-y-1">
                      <Label>Evidence reference</Label>
                      <Input
                        value={externalEvidence}
                        disabled={selected.status !== 'Draft' || !canManage}
                        placeholder="External register/certificate reference"
                        onChange={(event) =>
                          updateExternalEvidence(
                            check.checkType,
                            event.target.value
                          )
                        }
                      />
                    </div>
                    <div className="space-y-1">
                      <Label>Checked at</Label>
                      <Input
                        type="datetime-local"
                        value={asInputDate(check.checkedAtUtc)}
                        disabled={selected.status !== 'Draft' || !canManage}
                        onChange={(event) =>
                          updateCheck(check.checkType, {
                            checkedAtUtc: asUtc(event.target.value),
                          })
                        }
                      />
                    </div>
                    <div className="space-y-1">
                      <Label>Valid until</Label>
                      <Input
                        type="datetime-local"
                        value={asInputDate(check.validUntilUtc)}
                        disabled={selected.status !== 'Draft' || !canManage}
                        onChange={(event) =>
                          updateCheck(check.checkType, {
                            validUntilUtc: asUtc(event.target.value),
                          })
                        }
                      />
                    </div>
                    <div className="space-y-1 md:col-span-2">
                      <Label>Reviewer notes / not-applicable reason</Label>
                      <Input
                        value={check.notes ?? ''}
                        disabled={selected.status !== 'Draft' || !canManage}
                        onChange={(event) =>
                          updateCheck(check.checkType, {
                            notes: event.target.value,
                          })
                        }
                      />
                    </div>
                  </div>
                </div>
              );
            })}

            <div className="space-y-2">
              <Label>Review notes</Label>
              <Textarea
                value={notes}
                disabled={selected.status !== 'Draft' || !canManage}
                onChange={(event) => setNotes(event.target.value)}
              />
            </div>
            {selected.status === 'Draft' && canManage && (
              <div className="flex justify-end">
                <Button
                  disabled={saveMutation.isPending}
                  onClick={() => saveMutation.mutate(selected)}
                >
                  <Save className="mr-2 h-4 w-4" />
                  Save checks and evidence
                </Button>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Create governed supplier review</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Supplier</Label>
              <Select
                value={createRequest.businessPartnerId}
                onValueChange={(value) =>
                  setCreateRequest((current) => ({
                    ...current,
                    businessPartnerId: value,
                  }))
                }
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select supplier" />
                </SelectTrigger>
                <SelectContent>
                  {(suppliers.data ?? []).map((item) => (
                    <SelectItem key={item.id} value={item.id}>
                      {item.code} · {item.name}
                      {item.hasCurrentReview ? ' · current review exists' : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Review type</Label>
              <Select
                value={createRequest.reviewType}
                onValueChange={(value) =>
                  setCreateRequest((current) => ({
                    ...current,
                    reviewType: value as SupplierDueDiligenceReviewType,
                  }))
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Initial">Initial</SelectItem>
                  <SelectItem value="Annual">Annual reassessment</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Published workflow</Label>
              <Select
                value={createRequest.workflowDefinitionId}
                onValueChange={(value) =>
                  setCreateRequest((current) => ({
                    ...current,
                    workflowDefinitionId: value,
                  }))
                }
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select workflow" />
                </SelectTrigger>
                <SelectContent>
                  {(workflows.data ?? []).map((item) => (
                    <SelectItem key={item.id} value={item.id}>
                      {item.name} v{item.version}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Notes</Label>
              <Textarea
                value={createRequest.notes ?? ''}
                onChange={(event) =>
                  setCreateRequest((current) => ({
                    ...current,
                    notes: event.target.value,
                  }))
                }
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>
              Cancel
            </Button>
            <Button
              disabled={
                createMutation.isPending ||
                !createRequest.businessPartnerId ||
                !createRequest.workflowDefinitionId
              }
              onClick={() => createMutation.mutate(createRequest)}
            >
              Create draft
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(lifecycleAction)}
        onOpenChange={(open) => !open && setLifecycleAction(undefined)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {lifecycleAction === 'submit'
                ? 'Submit for independent approval'
                : lifecycleAction === 'approve'
                  ? 'Approve completed workflow outcome'
                  : 'Reject failed or cancelled workflow outcome'}
            </DialogTitle>
          </DialogHeader>
          <Alert>
            <ShieldCheck className="h-4 w-4" />
            <AlertDescription>
              The server revalidates exact DEC-011 policy, all six checks,
              evidence, workflow outcome, row version, tenant, and
              initiator/approver separation.
            </AlertDescription>
          </Alert>
          <div className="space-y-3">
            <div className="space-y-2">
              <Label>Decision comment</Label>
              <Textarea
                value={lifecycleComment}
                onChange={(event) => setLifecycleComment(event.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label>Retained evidence reference</Label>
              <Input
                value={lifecycleEvidence}
                onChange={(event) => setLifecycleEvidence(event.target.value)}
                placeholder="Workflow minute, approval record, or review reference"
              />
            </div>
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setLifecycleAction(undefined)}
            >
              Cancel
            </Button>
            <Button
              variant={
                lifecycleAction === 'reject' ? 'destructive' : 'default'
              }
              disabled={lifecycleMutation.isPending}
              onClick={submitLifecycle}
            >
              Confirm {lifecycleAction}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
