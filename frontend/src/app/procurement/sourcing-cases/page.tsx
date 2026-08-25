'use client';

import { useEffect, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Archive,
  CheckCircle2,
  CircleAlert,
  FileCheck2,
  FolderKanban,
  History,
  Loader2,
  Plus,
  RefreshCw,
  Search,
  ShieldCheck,
  Trash2,
} from 'lucide-react';
import { toast } from 'sonner';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
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
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import {
  procurementSourcingCaseActions,
  procurementSourcingCaseMethodLabels,
  validateProcurementSourcingCase,
} from '@/lib/procurement-sourcing-case';
import { procurementSourcingCaseService } from '@/services/procurement-sourcing-case.service';
import type { ProcurementMethodType } from '@/types/procurement-policy';
import type {
  CreateProcurementSourcingCase,
  ProcurementSourcingCase,
  ProcurementSourcingCaseStatus,
} from '@/types/procurement-sourcing-case';

const methods = Object.entries(procurementSourcingCaseMethodLabels) as Array<
  [ProcurementMethodType, string]
>;
const formatDate = (value?: string) =>
  value
    ? new Intl.DateTimeFormat(undefined, {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(value))
    : '—';
const formatMoney = (value: number, currency: string) =>
  new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: currency || 'GHS',
    maximumFractionDigits: 2,
  }).format(value);
const statusClass = (status: ProcurementSourcingCaseStatus) =>
  status === 'Ready'
    ? 'border-emerald-300 bg-emerald-50 text-emerald-700'
    : status === 'InProgress'
      ? 'border-blue-300 bg-blue-50 text-blue-700'
      : status === 'Closed'
        ? 'border-slate-300 bg-slate-100 text-slate-700'
        : 'border-amber-300 bg-amber-50 text-amber-700';

type LifecycleAction = {
  kind: 'close' | 'cancel';
  sourcingCase: ProcurementSourcingCase;
};

const emptyForm = (): CreateProcurementSourcingCase => ({
  requisitionId: '',
  justification: '',
  lots: [],
});

export default function ProcurementSourcingCasesPage() {
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const canManage = hasPermission('procurement.sourcing.manage');
  const canApprove = hasPermission('procurement.sourcing.approve');
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('all');
  const [method, setMethod] = useState('all');
  const [createOpen, setCreateOpen] = useState(false);
  const [form, setForm] = useState<CreateProcurementSourcingCase>(emptyForm());
  const [selected, setSelected] = useState<ProcurementSourcingCase>();
  const [action, setAction] = useState<LifecycleAction>();
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);

  const summary = useQuery({
    queryKey: ['procurement-sourcing-case-summary'],
    queryFn: procurementSourcingCaseService.summary,
  });
  const cases = useQuery({
    queryKey: ['procurement-sourcing-cases', search, status, method],
    queryFn: () =>
      procurementSourcingCaseService.search({
        page: 1,
        pageSize: 100,
        search: search.trim() || undefined,
        status: status === 'all' ? undefined : status,
        method: method === 'all' ? undefined : method,
      }),
  });
  const sources = useQuery({
    queryKey: ['procurement-sourcing-case-sources'],
    queryFn: procurementSourcingCaseService.sourceOptions,
  });
  const readiness = useQuery({
    queryKey: [
      'procurement-sourcing-case-readiness',
      form.requisitionId,
      form.selectedMethod,
      form.methodOverrideReason,
    ],
    queryFn: () =>
      procurementSourcingCaseService.readiness(
        form.requisitionId,
        form.selectedMethod,
        form.methodOverrideReason
      ),
    enabled: createOpen && Boolean(form.requisitionId),
  });
  const selectedSource = sources.data?.find(
    (source) => source.requisitionId === form.requisitionId
  );
  const recommendedMethod = readiness.data?.recommendedMethod;
  const effectiveMethod = form.selectedMethod ?? recommendedMethod;
  const isMethodOverride = Boolean(
    form.selectedMethod &&
    recommendedMethod &&
    form.selectedMethod !== recommendedMethod
  );
  const recommendationCandidate = readiness.data?.methodCandidates.find(
    (candidate) => candidate.method === recommendedMethod
  );

  useEffect(() => {
    if (!readiness.data?.lines.length || form.lots.length) return;
    setForm((current) => ({
      ...current,
      lots: [
        {
          lotCode: 'LOT-01',
          title:
            selectedSource?.sourcePlanItemDescription ??
            `Sourcing lot for ${readiness.data?.requisitionNumber}`,
          purchaseRequisitionItemIds:
            readiness.data?.lines.map((line) => line.id) ?? [],
        },
      ],
    }));
  }, [
    form.lots.length,
    readiness.data,
    selectedSource?.sourcePlanItemDescription,
  ]);

  const refresh = async () => {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: ['procurement-sourcing-case-summary'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['procurement-sourcing-cases'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['procurement-sourcing-case-sources'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['procurement-sourcing-case-readiness'],
      }),
    ]);
  };

  const openCreate = (requisitionId?: string) => {
    setForm({ ...emptyForm(), requisitionId: requisitionId ?? '' });
    setCreateOpen(true);
  };

  const assignLine = (lotIndex: number, itemId: string, checked: boolean) =>
    setForm((current) => ({
      ...current,
      lots: current.lots.map((lot, index) => ({
        ...lot,
        purchaseRequisitionItemIds: checked
          ? index === lotIndex
            ? Array.from(new Set([...lot.purchaseRequisitionItemIds, itemId]))
            : lot.purchaseRequisitionItemIds.filter((id) => id !== itemId)
          : index === lotIndex
            ? lot.purchaseRequisitionItemIds.filter((id) => id !== itemId)
            : lot.purchaseRequisitionItemIds,
      })),
    }));

  const saveCase = async () => {
    const validation = validateProcurementSourcingCase(
      form,
      readiness.data?.lines ?? [],
      readiness.data?.recommendedMethod
    );
    if (validation) {
      toast.error(validation);
      return;
    }
    if (!readiness.data?.canCreate) {
      toast.error(
        readiness.data?.message ??
          'The current release and policy-derived method are not ready.'
      );
      return;
    }
    setSaving(true);
    try {
      const created = await procurementSourcingCaseService.create(form);
      toast.success(`Sourcing case ${created.caseNumber} created.`);
      setCreateOpen(false);
      setSelected(created);
      await refresh();
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Sourcing case was not created.'
      );
    } finally {
      setSaving(false);
    }
  };

  const executeAction = async () => {
    if (!action || reason.trim().length < 5) return;
    setSaving(true);
    try {
      const updated =
        action.kind === 'close'
          ? await procurementSourcingCaseService.close(
              action.sourcingCase.id,
              action.sourcingCase.rowVersion,
              reason
            )
          : await procurementSourcingCaseService.cancel(
              action.sourcingCase.id,
              action.sourcingCase.rowVersion,
              reason
            );
      toast.success(`Sourcing case ${updated.caseNumber} ${action.kind}d.`);
      setAction(undefined);
      setSelected(updated);
      setReason('');
      await refresh();
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Sourcing-case action failed.'
      );
    } finally {
      setSaving(false);
    }
  };

  const loading = summary.isLoading || cases.isLoading || sources.isLoading;
  const loadError = summary.isError || cases.isError || sources.isError;
  const cards = [
    ['All cases', summary.data?.totalCount ?? 0, FolderKanban],
    ['Ready', summary.data?.readyCount ?? 0, ShieldCheck],
    ['In progress', summary.data?.inProgressCount ?? 0, FileCheck2],
    ['Closed', summary.data?.closedCount ?? 0, CheckCircle2],
    ['Stale controls', summary.data?.staleCount ?? 0, CircleAlert],
  ] as const;

  return (
    <div className="space-y-6">
      <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-start">
        <div>
          <h1 className="text-3xl font-bold">Procurement sourcing cases</h1>
          <p className="mt-1 max-w-4xl text-muted-foreground">
            History-first control register locking each approved requisition to
            its current release, server-recommended procurement method, exact
            policy and threshold rules, approved override lineage, authority
            route, source request, and lots.
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => void refresh()}>
            <RefreshCw className="mr-2 h-4 w-4" /> Refresh
          </Button>
          {canManage && (
            <Button onClick={() => openCreate()}>
              <Plus className="mr-2 h-4 w-4" /> New sourcing case
            </Button>
          )}
        </div>
      </div>

      {loadError && (
        <Alert variant="destructive">
          <CircleAlert className="h-4 w-4" />
          <AlertTitle>Sourcing cases could not be loaded</AlertTitle>
          <AlertDescription>
            Check the tenant session and procurement record permission, then
            retry.
          </AlertDescription>
        </Alert>
      )}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
        {cards.map(([label, value, Icon]) => (
          <Card key={label}>
            <CardContent className="flex items-center justify-between p-5">
              <div>
                <p className="text-sm text-muted-foreground">{label}</p>
                <p className="mt-1 text-2xl font-semibold">{value}</p>
              </div>
              <Icon className="h-5 w-5 text-muted-foreground" />
            </CardContent>
          </Card>
        ))}
      </div>

      <Tabs defaultValue="history" className="space-y-4">
        <TabsList>
          <TabsTrigger value="history">
            <History className="mr-2 h-4 w-4" /> Case history
          </TabsTrigger>
          <TabsTrigger value="readiness">
            <ShieldCheck className="mr-2 h-4 w-4" /> Source readiness
          </TabsTrigger>
        </TabsList>

        <TabsContent value="history" className="space-y-4">
          <div className="flex flex-col gap-3 rounded-lg border bg-card p-4 lg:flex-row">
            <div className="relative flex-1">
              <Search className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
              <Input
                aria-label="Search sourcing cases"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Search case, requisition, policy, or justification"
                className="pl-9"
              />
            </div>
            <Select value={status} onValueChange={setStatus}>
              <SelectTrigger className="w-full lg:w-48">
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All statuses</SelectItem>
                {(['Ready', 'InProgress', 'Closed', 'Cancelled'] as const).map(
                  (value) => (
                    <SelectItem key={value} value={value}>
                      {value === 'InProgress' ? 'In progress' : value}
                    </SelectItem>
                  )
                )}
              </SelectContent>
            </Select>
            <Select value={method} onValueChange={setMethod}>
              <SelectTrigger className="w-full lg:w-72">
                <SelectValue placeholder="Method" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All methods</SelectItem>
                {methods.map(([value, label]) => (
                  <SelectItem key={value} value={value}>
                    {label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="overflow-hidden rounded-lg border bg-card">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Case / requisition</TableHead>
                  <TableHead>Method and value</TableHead>
                  <TableHead>Locked control lineage</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Created</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {loading && (
                  <TableRow>
                    <TableCell colSpan={6} className="py-12 text-center">
                      <Loader2 className="mx-auto h-5 w-5 animate-spin" />
                    </TableCell>
                  </TableRow>
                )}
                {!loading && !(cases.data?.items.length ?? 0) && (
                  <TableRow>
                    <TableCell
                      colSpan={6}
                      className="py-12 text-center text-muted-foreground"
                    >
                      No sourcing cases match the current filters.
                    </TableCell>
                  </TableRow>
                )}
                {cases.data?.items.map((item) => {
                  const actions = procurementSourcingCaseActions(
                    item,
                    canApprove
                  );
                  return (
                    <TableRow key={item.id}>
                      <TableCell>
                        <button
                          type="button"
                          onClick={() => setSelected(item)}
                          className="font-medium text-primary hover:underline"
                        >
                          {item.caseNumber}
                        </button>
                        <p className="text-xs text-muted-foreground">
                          {item.requisitionNumber} ·{' '}
                          {item.sourcingReleaseReference}
                        </p>
                      </TableCell>
                      <TableCell>
                        <p>
                          {
                            procurementSourcingCaseMethodLabels[
                              item.selectedMethod
                            ]
                          }
                        </p>
                        <p className="text-xs text-muted-foreground">
                          {formatMoney(item.estimatedValue, item.currencyCode)}
                        </p>
                        <Badge variant="outline" className="mt-1 text-[10px]">
                          {item.methodSelectionBasis === 'ApprovedOverride'
                            ? 'Approved override'
                            : 'Server recommended'}
                        </Badge>
                      </TableCell>
                      <TableCell>
                        <p>
                          {item.policyCode} v{item.policyVersion}
                        </p>
                        <p className="text-xs text-muted-foreground">
                          {item.methodRuleCode} · {item.thresholdRuleCode}
                        </p>
                      </TableCell>
                      <TableCell>
                        <Badge
                          variant="outline"
                          className={statusClass(item.status)}
                        >
                          {item.status === 'InProgress'
                            ? 'In progress'
                            : item.status}
                        </Badge>
                        {!item.isSourceCurrent && (
                          <p className="mt-1 text-xs text-destructive">
                            Stale control
                          </p>
                        )}
                      </TableCell>
                      <TableCell>
                        <p>{formatDate(item.createdAtUtc)}</p>
                        <p className="text-xs text-muted-foreground">
                          {item.createdByName}
                        </p>
                      </TableCell>
                      <TableCell className="text-right">
                        <div className="flex justify-end gap-2">
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => setSelected(item)}
                          >
                            View
                          </Button>
                          {actions.canClose && (
                            <Button
                              size="sm"
                              onClick={() =>
                                setAction({ kind: 'close', sourcingCase: item })
                              }
                            >
                              Close
                            </Button>
                          )}
                          {actions.canCancel && (
                            <Button
                              size="sm"
                              variant="destructive"
                              onClick={() =>
                                setAction({
                                  kind: 'cancel',
                                  sourcingCase: item,
                                })
                              }
                            >
                              Cancel
                            </Button>
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </div>
        </TabsContent>

        <TabsContent value="readiness">
          <div className="overflow-hidden rounded-lg border bg-card">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Released requisition</TableHead>
                  <TableHead>Plan demand</TableHead>
                  <TableHead>Category</TableHead>
                  <TableHead>Estimated value</TableHead>
                  <TableHead>Control state</TableHead>
                  <TableHead className="text-right">Action</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {sources.data?.map((source) => (
                  <TableRow key={source.sourcingReleaseId}>
                    <TableCell>
                      <p className="font-medium">{source.requisitionNumber}</p>
                      <p className="text-xs text-muted-foreground">
                        {source.releaseReference}
                      </p>
                    </TableCell>
                    <TableCell>
                      <p>{source.sourcePlanNumber ?? 'Plan'}</p>
                      <p className="max-w-sm truncate text-xs text-muted-foreground">
                        {source.sourcePlanItemDescription ??
                          source.sourcePlanItemId}
                      </p>
                    </TableCell>
                    <TableCell>{source.category}</TableCell>
                    <TableCell>
                      {formatMoney(source.estimatedValue, source.currencyCode)}
                    </TableCell>
                    <TableCell>
                      {source.currentCaseId ? (
                        <Badge variant="secondary">
                          {source.currentCaseNumber}
                        </Badge>
                      ) : (
                        <Badge
                          variant="outline"
                          className="border-emerald-300 text-emerald-700"
                        >
                          Available
                        </Badge>
                      )}
                    </TableCell>
                    <TableCell className="text-right">
                      {canManage && !source.currentCaseId ? (
                        <Button
                          size="sm"
                          onClick={() => openCreate(source.requisitionId)}
                        >
                          Create case
                        </Button>
                      ) : source.currentCaseId ? (
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => {
                            const item = cases.data?.items.find(
                              (entry) => entry.id === source.currentCaseId
                            );
                            if (item) setSelected(item);
                          }}
                        >
                          View case
                        </Button>
                      ) : null}
                    </TableCell>
                  </TableRow>
                ))}
                {!sources.isLoading && !(sources.data?.length ?? 0) && (
                  <TableRow>
                    <TableCell
                      colSpan={6}
                      className="py-12 text-center text-muted-foreground"
                    >
                      No current immutable requisition releases are available.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
        </TabsContent>
      </Tabs>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-h-[92vh] max-w-5xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Create controlled sourcing case</DialogTitle>
            <DialogDescription>
              The server derives the procurement method from the effective
              policy and threshold. Assign every released requisition line to
              exactly one lot; any different method must carry an already
              approved exception and shared-workflow lineage.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label>Current immutable release</Label>
              <Select
                value={form.requisitionId}
                onValueChange={(value) =>
                  setForm({ ...emptyForm(), requisitionId: value })
                }
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select released requisition" />
                </SelectTrigger>
                <SelectContent>
                  {sources.data
                    ?.filter((source) => !source.currentCaseId)
                    .map((source) => (
                      <SelectItem
                        key={source.requisitionId}
                        value={source.requisitionId}
                      >
                        {source.requisitionNumber} · {source.releaseReference}
                      </SelectItem>
                    ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Policy-selected procurement method</Label>
              <Select
                value={effectiveMethod ?? ''}
                onValueChange={(value) =>
                  setForm((current) => {
                    const selectedMethod = value as ProcurementMethodType;
                    const acceptsRecommendation =
                      selectedMethod === readiness.data?.recommendedMethod;
                    return {
                      ...current,
                      selectedMethod: acceptsRecommendation
                        ? undefined
                        : selectedMethod,
                      methodOverrideReason: acceptsRecommendation
                        ? undefined
                        : current.methodOverrideReason,
                    };
                  })
                }
              >
                <SelectTrigger>
                  <SelectValue placeholder="Waiting for policy recommendation" />
                </SelectTrigger>
                <SelectContent>
                  {methods.map(([value, label]) => (
                    <SelectItem
                      key={value}
                      value={value}
                      disabled={
                        Boolean(recommendedMethod) &&
                        value !== recommendedMethod &&
                        !canApprove
                      }
                    >
                      {label}
                      {value === recommendedMethod ? ' · recommended' : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                {recommendedMethod
                  ? `Current policy recommends ${procurementSourcingCaseMethodLabels[recommendedMethod]}.`
                  : 'Select a released requisition to resolve the recommendation.'}
              </p>
            </div>
          </div>

          {recommendedMethod && !isMethodOverride && (
            <div className="grid gap-4 rounded-lg border border-emerald-300 bg-emerald-50/60 p-4 md:grid-cols-[1fr_auto]">
              <div>
                <div className="flex items-center gap-2">
                  <ShieldCheck className="h-4 w-4 text-emerald-700" />
                  <h3 className="font-semibold text-emerald-950">
                    Automatic server recommendation
                  </h3>
                </div>
                <p className="mt-1 text-sm text-emerald-900">
                  {recommendationCandidate?.explanation ??
                    readiness.data?.message}
                </p>
                <p className="mt-2 text-xs text-emerald-800">
                  {recommendationCandidate?.methodRuleCode ?? 'Method rule'} ·{' '}
                  {recommendationCandidate?.thresholdRuleCode ??
                    'threshold rule'}
                </p>
              </div>
              <Badge className="h-fit bg-emerald-700">
                {procurementSourcingCaseMethodLabels[recommendedMethod]}
              </Badge>
            </div>
          )}

          {isMethodOverride && (
            <div className="space-y-4 rounded-lg border border-amber-300 bg-amber-50/60 p-4">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <h3 className="font-semibold text-amber-950">
                    Approved method override
                  </h3>
                  <p className="text-sm text-amber-900">
                    Changing{' '}
                    {recommendedMethod
                      ? procurementSourcingCaseMethodLabels[recommendedMethod]
                      : 'the recommendation'}{' '}
                    to{' '}
                    {form.selectedMethod
                      ? procurementSourcingCaseMethodLabels[form.selectedMethod]
                      : 'another method'}{' '}
                    is never treated as an ordinary selection.
                  </p>
                </div>
                <Badge variant="outline">
                  {readiness.data?.override?.isEligible
                    ? 'Approval verified'
                    : 'Approval required'}
                </Badge>
              </div>
              {!canApprove && (
                <Alert variant="destructive">
                  <CircleAlert className="h-4 w-4" />
                  <AlertTitle>Override capability required</AlertTitle>
                  <AlertDescription>
                    Your account does not have procurement.sourcing.approve. Use
                    the server recommendation or ask an authorized procurement
                    approver to complete this action.
                  </AlertDescription>
                </Alert>
              )}
              <div className="space-y-2">
                <Label>Method override reason</Label>
                <Textarea
                  value={form.methodOverrideReason ?? ''}
                  onChange={(event) =>
                    setForm((current) => ({
                      ...current,
                      methodOverrideReason: event.target.value,
                    }))
                  }
                  placeholder="Explain why the approved exception authorizes departure from the policy recommendation."
                  rows={3}
                />
              </div>
              <div className="grid gap-3 text-sm md:grid-cols-2 lg:grid-cols-3">
                <div>
                  <p className="text-xs text-muted-foreground">
                    Exception rule
                  </p>
                  <p className="font-medium">
                    {readiness.data?.override?.ruleCode ?? 'Not linked'}
                  </p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">
                    Shared workflow
                  </p>
                  <p className="break-all font-medium">
                    {readiness.data?.override?.workflowInstanceId ??
                      'Not linked'}
                  </p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">
                    Approval reference
                  </p>
                  <p className="font-medium">
                    {readiness.data?.override?.approvalReference ??
                      'Not linked'}
                  </p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">
                    Evidence reference
                  </p>
                  <p className="font-medium">
                    {readiness.data?.override?.evidenceReference ??
                      'Not linked'}
                  </p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">Approved</p>
                  <p className="font-medium">
                    {formatDate(readiness.data?.override?.approvedAtUtc)}
                  </p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">
                    Approval actors
                  </p>
                  <p className="font-medium">
                    {readiness.data?.override?.approvalActorUserIds.length ?? 0}{' '}
                    verified
                  </p>
                </div>
              </div>
            </div>
          )}

          {form.requisitionId && (
            <Alert
              variant={readiness.data?.canCreate ? 'default' : 'destructive'}
            >
              {readiness.isLoading ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <ShieldCheck className="h-4 w-4" />
              )}
              <AlertTitle>
                {readiness.data?.decisionCode ?? 'Checking current controls'}
              </AlertTitle>
              <AlertDescription>
                {readiness.data?.message ??
                  'Validating release and effective policy lineage.'}
              </AlertDescription>
            </Alert>
          )}

          <div className="space-y-2">
            <Label>
              Sourcing and lot notes
              {form.lots.length > 1 ? ' *' : ' (optional)'}
            </Label>
            <Textarea
              value={form.justification}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  justification: event.target.value,
                }))
              }
              placeholder={
                form.lots.length > 1
                  ? 'Explain why this requisition is divided into multiple sourcing lots.'
                  : 'Add any useful operational notes. The system records the policy-selected method and rationale automatically.'
              }
              rows={3}
            />
            <p className="text-xs text-muted-foreground">
              {form.lots.length > 1
                ? 'Multiple lots require a short structure explanation. A separate approval reason is required for a method override.'
                : 'A separate reason is required only when requesting a method other than the policy recommendation.'}
            </p>
          </div>

          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <div>
                <Label>Sourcing lots</Label>
                <p className="text-xs text-muted-foreground">
                  Selecting a line in one lot automatically removes it from
                  another.
                </p>
              </div>
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={!readiness.data?.lines.length}
                onClick={() =>
                  setForm((current) => ({
                    ...current,
                    lots: [
                      ...current.lots,
                      {
                        lotCode: `LOT-${String(current.lots.length + 1).padStart(2, '0')}`,
                        title: `Lot ${current.lots.length + 1}`,
                        purchaseRequisitionItemIds: [],
                      },
                    ],
                  }))
                }
              >
                <Plus className="mr-2 h-4 w-4" /> Add lot
              </Button>
            </div>
            {form.lots.map((lot, lotIndex) => (
              <Card key={`${lot.lotCode}-${lotIndex}`}>
                <CardContent className="space-y-4 p-4">
                  <div className="grid gap-3 md:grid-cols-[140px_1fr_auto]">
                    <Input
                      aria-label={`Lot ${lotIndex + 1} code`}
                      value={lot.lotCode ?? ''}
                      onChange={(event) =>
                        setForm((current) => ({
                          ...current,
                          lots: current.lots.map((entry, index) =>
                            index === lotIndex
                              ? { ...entry, lotCode: event.target.value }
                              : entry
                          ),
                        }))
                      }
                      placeholder="Lot code"
                    />
                    <Input
                      aria-label={`Lot ${lotIndex + 1} title`}
                      value={lot.title}
                      onChange={(event) =>
                        setForm((current) => ({
                          ...current,
                          lots: current.lots.map((entry, index) =>
                            index === lotIndex
                              ? { ...entry, title: event.target.value }
                              : entry
                          ),
                        }))
                      }
                      placeholder="Lot title"
                    />
                    {form.lots.length > 1 && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        aria-label={`Remove lot ${lotIndex + 1}`}
                        onClick={() =>
                          setForm((current) => ({
                            ...current,
                            lots: current.lots.filter(
                              (_, index) => index !== lotIndex
                            ),
                          }))
                        }
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    )}
                  </div>
                  <div className="grid gap-2 md:grid-cols-2">
                    {readiness.data?.lines.map((line) => (
                      <label
                        key={line.id}
                        className="flex items-start gap-3 rounded-md border p-3 text-sm"
                      >
                        <Checkbox
                          checked={lot.purchaseRequisitionItemIds.includes(
                            line.id
                          )}
                          onCheckedChange={(checked) =>
                            assignLine(lotIndex, line.id, checked === true)
                          }
                        />
                        <span className="min-w-0 flex-1">
                          <span className="font-medium">
                            Line {line.lineNumber}: {line.description}
                          </span>
                          <span className="block text-xs text-muted-foreground">
                            {line.quantity} {line.unitOfMeasure} ·{' '}
                            {formatMoney(
                              line.lineTotal,
                              selectedSource?.currencyCode ?? 'GHS'
                            )}
                          </span>
                        </span>
                      </label>
                    ))}
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => void saveCase()}
              disabled={saving || !readiness.data?.canCreate}
            >
              {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Lock sourcing case
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(selected)}
        onOpenChange={(open) => !open && setSelected(undefined)}
      >
        <DialogContent className="max-h-[92vh] max-w-5xl overflow-y-auto">
          {selected && (
            <>
              <DialogHeader>
                <DialogTitle>{selected.caseNumber}</DialogTitle>
                <DialogDescription>
                  {selected.requisitionNumber} ·{' '}
                  {procurementSourcingCaseMethodLabels[selected.selectedMethod]}{' '}
                  ·{' '}
                  {formatMoney(selected.estimatedValue, selected.currencyCode)}
                </DialogDescription>
              </DialogHeader>
              {!selected.isSourceCurrent && (
                <Alert variant="destructive">
                  <CircleAlert className="h-4 w-4" />
                  <AlertTitle>{selected.sourceStateCode}</AlertTitle>
                  <AlertDescription>
                    {selected.sourceStateMessage}
                  </AlertDescription>
                </Alert>
              )}
              <div className="grid gap-4 rounded-lg border p-4 md:grid-cols-3">
                <div>
                  <p className="text-xs text-muted-foreground">
                    Server recommendation
                  </p>
                  <p className="font-medium">
                    {
                      procurementSourcingCaseMethodLabels[
                        selected.recommendedMethod
                      ]
                    }
                  </p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">Locked method</p>
                  <p className="font-medium">
                    {
                      procurementSourcingCaseMethodLabels[
                        selected.selectedMethod
                      ]
                    }
                  </p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">
                    Selection basis
                  </p>
                  <p className="font-medium">
                    {selected.methodSelectionBasis === 'ApprovedOverride'
                      ? 'Approved override'
                      : 'Automatic recommendation'}
                  </p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">
                    Immutable release
                  </p>
                  <p className="font-medium">
                    {selected.sourcingReleaseReference}
                  </p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">
                    Policy version
                  </p>
                  <p className="font-medium">
                    {selected.policyCode} v{selected.policyVersion}
                  </p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">
                    Authority route
                  </p>
                  <p className="font-medium">
                    {selected.authorityRouteReference}
                  </p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">Method rule</p>
                  <p className="font-medium">{selected.methodRuleCode}</p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">
                    Threshold rule
                  </p>
                  <p className="font-medium">{selected.thresholdRuleCode}</p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">Created</p>
                  <p className="font-medium">
                    {formatDate(selected.createdAtUtc)}
                  </p>
                </div>
              </div>
              {selected.methodSelectionBasis === 'ApprovedOverride' && (
                <div className="space-y-4 rounded-lg border border-amber-300 bg-amber-50/60 p-4">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <h3 className="font-semibold text-amber-950">
                      Immutable approved-override lineage
                    </h3>
                    <Badge variant="outline">DEC-006 controlled</Badge>
                  </div>
                  <div className="grid gap-3 text-sm md:grid-cols-2 lg:grid-cols-3">
                    <div>
                      <p className="text-xs text-muted-foreground">
                        Exception rule ID
                      </p>
                      <p className="break-all font-medium">
                        {selected.approvedExceptionRuleId ?? '—'}
                      </p>
                    </div>
                    <div>
                      <p className="text-xs text-muted-foreground">
                        Shared workflow instance
                      </p>
                      <p className="break-all font-medium">
                        {selected.methodOverrideWorkflowInstanceId ?? '—'}
                      </p>
                    </div>
                    <div>
                      <p className="text-xs text-muted-foreground">
                        Approved at
                      </p>
                      <p className="font-medium">
                        {formatDate(selected.methodOverrideApprovedAtUtc)}
                      </p>
                    </div>
                    <div>
                      <p className="text-xs text-muted-foreground">
                        Approval reference
                      </p>
                      <p className="font-medium">
                        {selected.exceptionApprovalReference ?? '—'}
                      </p>
                    </div>
                    <div>
                      <p className="text-xs text-muted-foreground">
                        Evidence reference
                      </p>
                      <p className="font-medium">
                        {selected.exceptionEvidenceReference ?? '—'}
                      </p>
                    </div>
                    <div>
                      <p className="text-xs text-muted-foreground">
                        Traceable approval actors
                      </p>
                      <p className="font-medium">
                        {selected.methodOverrideApprovalActorUserIds.length}{' '}
                        verified
                      </p>
                    </div>
                  </div>
                  <div>
                    <p className="text-xs text-muted-foreground">
                      Recorded override reason
                    </p>
                    <p className="mt-1 rounded-md border bg-background p-3 text-sm">
                      {selected.methodOverrideReason ?? '—'}
                    </p>
                  </div>
                </div>
              )}
              <div>
                <h3 className="mb-2 font-semibold">Recorded justification</h3>
                <p className="rounded-lg border bg-muted/30 p-4 text-sm">
                  {selected.justification}
                </p>
              </div>
              <div className="space-y-3">
                <h3 className="font-semibold">Lots and requisition lines</h3>
                {selected.lots.map((lot) => (
                  <div key={lot.id} className="rounded-lg border p-4">
                    <div className="flex justify-between gap-3">
                      <div>
                        <p className="font-medium">
                          {lot.lotCode} · {lot.title}
                        </p>
                        <p className="text-xs text-muted-foreground">
                          {lot.items.length} lines
                        </p>
                      </div>
                      <p className="font-medium">
                        {formatMoney(lot.estimatedValue, lot.currencyCode)}
                      </p>
                    </div>
                    <div className="mt-3 space-y-2">
                      {lot.items.map((item) => (
                        <div
                          key={item.requisitionItemId}
                          className="flex justify-between gap-3 text-sm"
                        >
                          <span>
                            {item.description} · {item.quantity}{' '}
                            {item.unitOfMeasure}
                          </span>
                          <span>
                            {formatMoney(item.lineTotal, lot.currencyCode)}
                          </span>
                        </div>
                      ))}
                    </div>
                  </div>
                ))}
              </div>
              <div>
                <h3 className="mb-2 font-semibold">
                  Controlled source requests
                </h3>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Reference</TableHead>
                      <TableHead>Type</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Registered source</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {selected.sourceRequests.map((request) => (
                      <TableRow key={request.id}>
                        <TableCell>{request.requestReference}</TableCell>
                        <TableCell>{request.sourceType}</TableCell>
                        <TableCell>
                          <Badge variant="outline">{request.status}</Badge>
                        </TableCell>
                        <TableCell>
                          {request.sourceEntityReference ?? 'Not created'}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
              <DialogFooter>
                <Button
                  variant="outline"
                  onClick={() => setSelected(undefined)}
                >
                  Close
                </Button>
              </DialogFooter>
            </>
          )}
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(action)}
        onOpenChange={(open) => !open && setAction(undefined)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {action?.kind === 'close'
                ? 'Close sourcing case'
                : 'Cancel sourcing case'}
            </DialogTitle>
            <DialogDescription>
              {action?.kind === 'close'
                ? 'Close only after the controlled RFQ or tender has been created.'
                : 'Cancellation is terminal and leaves the immutable case in the audit history.'}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label>Reason</Label>
            <Textarea
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              rows={4}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAction(undefined)}>
              Back
            </Button>
            <Button
              variant={action?.kind === 'cancel' ? 'destructive' : 'default'}
              disabled={saving || reason.trim().length < 5}
              onClick={() => void executeAction()}
            >
              {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {action?.kind === 'close' ? (
                <Archive className="mr-2 h-4 w-4" />
              ) : null}
              Confirm
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
