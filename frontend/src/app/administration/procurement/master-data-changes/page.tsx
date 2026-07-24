'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  FileCheck2,
  Pencil,
  Plus,
  RefreshCw,
  ShieldAlert,
  ShieldCheck,
} from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
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
import { Pagination } from '@/components/ui/pagination';
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
import { useToast } from '@/hooks/use-toast';
import {
  parseRoleList,
  policyActions,
  requestActions,
  statusTone,
  validateChangeDraft,
  validatePolicyDraft,
} from '@/lib/procurement-master-data-change';
import { procurementMasterDataChangeService } from '@/services/procurement-master-data-change.service';
import type {
  ProcurementMasterDataChange,
  ProcurementMasterDataChangeSearch,
  ProcurementMasterDataPolicy,
  ProcurementMasterDataResourceDefinition,
  ProcurementMasterDataResourceType,
  SaveProcurementMasterDataChange,
  SaveProcurementMasterDataPolicy,
} from '@/types/procurement-master-data-change';

const today = () => new Date().toISOString().slice(0, 10);
const dateValue = (value?: string) => value?.slice(0, 10) ?? '';
const dateTime = (value?: string) =>
  value
    ? new Intl.DateTimeFormat(undefined, {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(value))
    : '—';
const prettyJson = (value?: string) => {
  if (!value) return '—';
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
};

const emptyPolicy = (
  resourceType: ProcurementMasterDataResourceType = 'SupplierProfile'
): SaveProcurementMasterDataPolicy => ({
  resourceType,
  name: '',
  makerRoles: ['TDC_PROCUREMENT_OFFICER'],
  checkerRoles: ['TDC_HEAD_OF_PROCUREMENT'],
  requireIndependentApproval: true,
  requireRevalidation: true,
  requireEvidence: false,
  effectiveFromUtc: today(),
});

const emptyChange = (
  resourceType: ProcurementMasterDataResourceType = 'SupplierProfile'
): SaveProcurementMasterDataChange => ({
  resourceType,
  targetId: '',
  proposedChangesJson: '{\n  \n}',
  reason: '',
  effectiveAtUtc: today(),
  evidence: [],
});

type LifecycleAction =
  | {
      kind: 'activate-policy' | 'retire-policy';
      policy: ProcurementMasterDataPolicy;
    }
  | {
      kind: 'submit' | 'revalidate' | 'approve' | 'reject' | 'apply' | 'cancel';
      request: ProcurementMasterDataChange;
    };

export default function ProcurementMasterDataChangesPage() {
  const { hasRole } = useAuth();
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const isAdministrator = hasRole('SuperAdmin') || hasRole('TenantAdmin');
  const isSuperAdmin = hasRole('SuperAdmin');
  const [filters, setFilters] = useState<ProcurementMasterDataChangeSearch>({
    page: 1,
    pageSize: 25,
  });
  const [policyOpen, setPolicyOpen] = useState(false);
  const [policyId, setPolicyId] = useState<string>();
  const [policyForm, setPolicyForm] =
    useState<SaveProcurementMasterDataPolicy>(emptyPolicy);
  const [makerRoles, setMakerRoles] = useState('TDC_PROCUREMENT_OFFICER');
  const [checkerRoles, setCheckerRoles] = useState('TDC_HEAD_OF_PROCUREMENT');
  const [changeOpen, setChangeOpen] = useState(false);
  const [changeId, setChangeId] = useState<string>();
  const [changeForm, setChangeForm] =
    useState<SaveProcurementMasterDataChange>(emptyChange);
  const [evidenceReference, setEvidenceReference] = useState('');
  const [selectedId, setSelectedId] = useState<string>();
  const [lifecycle, setLifecycle] = useState<LifecycleAction>();
  const [lifecycleComment, setLifecycleComment] = useState('');

  const registry = useQuery({
    queryKey: ['procurement-master-data-registry'],
    queryFn: procurementMasterDataChangeService.registry,
  });
  const summary = useQuery({
    queryKey: ['procurement-master-data-summary'],
    queryFn: procurementMasterDataChangeService.summary,
  });
  const policies = useQuery({
    queryKey: ['procurement-master-data-policies'],
    queryFn: procurementMasterDataChangeService.policies,
  });
  const requests = useQuery({
    queryKey: ['procurement-master-data-requests', filters],
    queryFn: () => procurementMasterDataChangeService.search(filters),
  });
  const detail = useQuery({
    queryKey: ['procurement-master-data-request', selectedId],
    queryFn: () =>
      selectedId
        ? procurementMasterDataChangeService.get(selectedId)
        : Promise.reject(new Error('No request selected.')),
    enabled: Boolean(selectedId),
  });

  const selectedResource = registry.data?.find(
    (item) => item.resourceType === changeForm.resourceType
  );
  const policyError = validatePolicyDraft({
    ...policyForm,
    makerRoles: parseRoleList(makerRoles),
    checkerRoles: parseRoleList(checkerRoles),
  });
  const changeError = validateChangeDraft(
    changeForm,
    selectedResource?.allowsTenantSingletonTarget
  );

  const refresh = () =>
    Promise.all([
      summary.refetch(),
      policies.refetch(),
      requests.refetch(),
      registry.refetch(),
    ]);
  const invalidate = async () => {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: ['procurement-master-data-summary'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['procurement-master-data-policies'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['procurement-master-data-requests'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['procurement-master-data-request'],
      }),
    ]);
  };

  const savePolicy = useMutation({
    mutationFn: () =>
      procurementMasterDataChangeService.savePolicy(policyId, {
        ...policyForm,
        makerRoles: parseRoleList(makerRoles),
        checkerRoles: parseRoleList(checkerRoles),
        workflowDefinitionId: policyForm.workflowDefinitionId || undefined,
        effectiveToUtc: policyForm.effectiveToUtc || undefined,
      }),
    onSuccess: async () => {
      setPolicyOpen(false);
      await invalidate();
      toast({
        title: 'Control policy saved',
        description: 'The policy remains Draft until SuperAdmin activation.',
      });
    },
    onError: (error: Error) =>
      toast({
        title: 'Unable to save policy',
        description: error.message,
        variant: 'destructive',
      }),
  });
  const saveChange = useMutation({
    mutationFn: () =>
      procurementMasterDataChangeService.saveDraft(changeId, {
        ...changeForm,
        evidence: evidenceReference.trim()
          ? [
              {
                referenceKind: 'ExternalReference',
                reference: evidenceReference.trim(),
                label: 'External evidence reference',
                requirementKey: 'TDC-0007',
              },
            ]
          : changeForm.evidence,
      }),
    onSuccess: async (value) => {
      setChangeOpen(false);
      setSelectedId(value.id);
      await invalidate();
      toast({
        title: 'Change request saved',
        description:
          'The immutable before snapshot was refreshed from the current tenant target.',
      });
    },
    onError: (error: Error) =>
      toast({
        title: 'Unable to save request',
        description: error.message,
        variant: 'destructive',
      }),
  });
  const runLifecycle = useMutation({
    mutationFn: async () => {
      if (!lifecycle) throw new Error('Select an action first.');
      if ('policy' in lifecycle) {
        return lifecycle.kind === 'activate-policy'
          ? procurementMasterDataChangeService.activatePolicy(
              lifecycle.policy.id,
              lifecycleComment,
              lifecycle.policy.rowVersion
            )
          : procurementMasterDataChangeService.retirePolicy(
              lifecycle.policy.id,
              lifecycleComment,
              lifecycle.policy.rowVersion
            );
      }
      const item = lifecycle.request;
      switch (lifecycle.kind) {
        case 'submit':
          return procurementMasterDataChangeService.submit(
            item.id,
            item.rowVersion,
            lifecycleComment || undefined
          );
        case 'revalidate':
          return procurementMasterDataChangeService.revalidate(
            item.id,
            item.rowVersion,
            lifecycleComment || undefined
          );
        case 'approve':
          return procurementMasterDataChangeService.approve(
            item.id,
            item.rowVersion,
            lifecycleComment
          );
        case 'reject':
          return procurementMasterDataChangeService.reject(
            item.id,
            item.rowVersion,
            lifecycleComment
          );
        case 'apply':
          return procurementMasterDataChangeService.apply(
            item.id,
            item.rowVersion,
            lifecycleComment || undefined
          );
        case 'cancel':
          return procurementMasterDataChangeService.cancel(
            item.id,
            item.rowVersion,
            lifecycleComment
          );
      }
    },
    onSuccess: async (value) => {
      const requestValue = 'requestNumber' in value;
      setLifecycle(undefined);
      setLifecycleComment('');
      if (requestValue) setSelectedId(value.id);
      await invalidate();
      toast({
        title: requestValue
          ? `Request ${value.status}`
          : `Policy ${value.status}`,
      });
    },
    onError: (error: Error) =>
      toast({
        title: 'Control action failed',
        description: error.message,
        variant: 'destructive',
      }),
  });

  const openPolicy = (item?: ProcurementMasterDataPolicy) => {
    setPolicyId(item?.id);
    setPolicyForm(
      item
        ? {
            resourceType: item.resourceType,
            name: item.name,
            description: item.description,
            makerRoles: item.makerRoles,
            checkerRoles: item.checkerRoles,
            requireIndependentApproval: true,
            requireRevalidation: true,
            requireEvidence: item.requireEvidence,
            workflowDefinitionId: item.workflowDefinitionId,
            effectiveFromUtc: dateValue(item.effectiveFromUtc),
            effectiveToUtc: dateValue(item.effectiveToUtc) || undefined,
            rowVersion: item.rowVersion,
          }
        : emptyPolicy(registry.data?.[0]?.resourceType)
    );
    setMakerRoles(item?.makerRoles.join(', ') ?? 'TDC_PROCUREMENT_OFFICER');
    setCheckerRoles(item?.checkerRoles.join(', ') ?? 'TDC_HEAD_OF_PROCUREMENT');
    setPolicyOpen(true);
  };
  const openChange = (item?: ProcurementMasterDataChange) => {
    setChangeId(item?.id);
    setChangeForm(
      item
        ? {
            resourceType: item.resourceType,
            targetId: item.targetId,
            proposedChangesJson: prettyJson(item.proposedChangesJson),
            reason: item.reason,
            effectiveAtUtc: dateValue(item.effectiveAtUtc),
            evidence: item.evidence.map((value) => ({
              referenceKind: value.referenceKind,
              referenceId: value.referenceId,
              reference: value.reference,
              label: value.label,
              requirementKey: value.requirementKey,
            })),
            rowVersion: item.rowVersion,
          }
        : emptyChange(registry.data?.[0]?.resourceType)
    );
    setEvidenceReference(
      item?.evidence.find(
        (value) => value.referenceKind === 'ExternalReference'
      )?.reference ?? ''
    );
    setChangeOpen(true);
  };
  const ask = (action: LifecycleAction) => {
    setLifecycle(action);
    setLifecycleComment('');
  };
  const requiresComment =
    lifecycle?.kind === 'activate-policy' ||
    lifecycle?.kind === 'retire-policy' ||
    lifecycle?.kind === 'approve' ||
    lifecycle?.kind === 'reject' ||
    lifecycle?.kind === 'cancel';
  const loadingError =
    registry.isError || summary.isError || policies.isError || requests.isError;

  const resourceLabel = useMemo(
    () =>
      Object.fromEntries(
        (registry.data ?? []).map((item) => [item.resourceType, item.name])
      ),
    [registry.data]
  );

  return (
    <div className="space-y-6">
      <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-start">
        <div>
          <h1 className="text-3xl font-bold">Controlled master-data changes</h1>
          <p className="mt-1 max-w-4xl text-muted-foreground">
            Configure effective maker-checker protection and stage supplier,
            item, category, UOM, warehouse, location, and procurement-setting
            changes with immutable snapshots and revalidation.
          </p>
        </div>
        <Button
          variant="outline"
          onClick={refresh}
          disabled={summary.isFetching}
        >
          <RefreshCw
            className={`mr-2 h-4 w-4 ${summary.isFetching ? 'animate-spin' : ''}`}
          />
          Refresh controls
        </Button>
      </div>

      <Alert className="border-amber-500/40 bg-amber-500/5">
        <ShieldCheck className="h-4 w-4" />
        <AlertTitle>Shared-control boundary</AlertTitle>
        <AlertDescription>
          Workflow definitions and evidence files remain owned by the shared
          workflow, upload, and evidence controls. This workspace stores
          references only. It cannot change PR, PO, receipt, payment, stock
          movement, or inventory quantity records.
        </AlertDescription>
      </Alert>
      {loadingError && (
        <Alert variant="destructive">
          <ShieldAlert className="h-4 w-4" />
          <AlertTitle>Control data unavailable</AlertTitle>
          <AlertDescription>
            Refresh after checking API connectivity and your tenant-scoped
            procurement role.
          </AlertDescription>
        </Alert>
      )}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-6">
        <Metric
          label="Effective policies"
          value={summary.data?.effectivePolicyCount ?? 0}
          detail={`${summary.data?.policyCount ?? 0} versions`}
        />
        <Metric
          label="Draft"
          value={summary.data?.draftCount ?? 0}
          detail="maker preparation"
        />
        <Metric
          label="Pending"
          value={summary.data?.pendingApprovalCount ?? 0}
          detail="independent check"
        />
        <Metric
          label="Approved"
          value={summary.data?.approvedAwaitingEffectiveDateCount ?? 0}
          detail="awaiting apply"
        />
        <Metric
          label="Revalidation failed"
          value={summary.data?.revalidationFailedCount ?? 0}
          detail="fresh request required"
        />
        <Metric
          label="Applied"
          value={summary.data?.appliedCount ?? 0}
          detail="effective updates"
        />
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Maker-checker workspace</CardTitle>
          <CardDescription>
            Active policies fail closed at existing direct update/delete APIs
            and route changes here.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Tabs defaultValue="requests">
            <TabsList className="grid h-auto w-full grid-cols-3">
              <TabsTrigger value="requests">Change requests</TabsTrigger>
              <TabsTrigger value="policies">Policies</TabsTrigger>
              <TabsTrigger value="registry">Protected fields</TabsTrigger>
            </TabsList>

            <TabsContent value="requests" className="space-y-4 pt-4">
              <div className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
                <div className="grid flex-1 gap-3 sm:grid-cols-3">
                  <div>
                    <Label>Search</Label>
                    <Input
                      value={filters.search ?? ''}
                      onChange={(event) =>
                        setFilters((current) => ({
                          ...current,
                          search: event.target.value || undefined,
                          page: 1,
                        }))
                      }
                      placeholder="Request, target, or reason"
                    />
                  </div>
                  <div>
                    <Label>Resource</Label>
                    <Select
                      value={filters.resourceType ?? 'all'}
                      onValueChange={(value) =>
                        setFilters((current) => ({
                          ...current,
                          resourceType:
                            value === 'all'
                              ? undefined
                              : (value as ProcurementMasterDataResourceType),
                          page: 1,
                        }))
                      }
                    >
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="all">All resources</SelectItem>
                        {(registry.data ?? []).map((item) => (
                          <SelectItem
                            key={item.resourceType}
                            value={item.resourceType}
                          >
                            {item.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div>
                    <Label>Status</Label>
                    <Select
                      value={filters.status ?? 'all'}
                      onValueChange={(value) =>
                        setFilters((current) => ({
                          ...current,
                          status:
                            value === 'all'
                              ? undefined
                              : (value as ProcurementMasterDataChangeSearch['status']),
                          page: 1,
                        }))
                      }
                    >
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="all">All statuses</SelectItem>
                        {[
                          'Draft',
                          'PendingApproval',
                          'Approved',
                          'Rejected',
                          'Applied',
                          'Cancelled',
                          'RevalidationFailed',
                        ].map((value) => (
                          <SelectItem key={value} value={value}>
                            {value.replace(/([A-Z])/g, ' $1').trim()}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                </div>
                <Button onClick={() => openChange()}>
                  <Plus className="mr-2 h-4 w-4" />
                  New staged change
                </Button>
              </div>
              <div className="overflow-x-auto rounded-lg border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Request</TableHead>
                      <TableHead>Resource / target</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Effective</TableHead>
                      <TableHead>Revalidation</TableHead>
                      <TableHead className="text-right">Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(requests.data?.items ?? []).map((item) => {
                      const actions = requestActions(item);
                      return (
                        <TableRow key={item.id}>
                          <TableCell>
                            <button
                              className="text-left font-medium hover:underline"
                              onClick={() => setSelectedId(item.id)}
                            >
                              {item.requestNumber}
                            </button>
                            <p className="max-w-56 truncate text-xs text-muted-foreground">
                              {item.reason}
                            </p>
                          </TableCell>
                          <TableCell>
                            <p>
                              {resourceLabel[item.resourceType] ??
                                item.resourceType}
                            </p>
                            <p className="max-w-64 truncate text-xs text-muted-foreground">
                              {item.targetReference}
                            </p>
                          </TableCell>
                          <TableCell>
                            <Badge
                              variant="outline"
                              className={statusTone(item.status)}
                            >
                              {item.status.replace(/([A-Z])/g, ' $1').trim()}
                            </Badge>
                          </TableCell>
                          <TableCell>{dateTime(item.effectiveAtUtc)}</TableCell>
                          <TableCell>
                            {item.revalidationPassed === true ? (
                              <span className="text-emerald-700">Passed</span>
                            ) : item.revalidationPassed === false ? (
                              <span className="text-red-700">Failed</span>
                            ) : (
                              'Not run'
                            )}
                          </TableCell>
                          <TableCell>
                            <div className="flex flex-wrap justify-end gap-1">
                              <Button
                                size="sm"
                                variant="ghost"
                                onClick={() => setSelectedId(item.id)}
                              >
                                View
                              </Button>
                              {actions.canEdit && (
                                <Button
                                  size="sm"
                                  variant="ghost"
                                  onClick={() => openChange(item)}
                                >
                                  <Pencil className="h-3.5 w-3.5" />
                                </Button>
                              )}
                              {actions.canSubmit && (
                                <Button
                                  size="sm"
                                  variant="outline"
                                  onClick={() =>
                                    ask({ kind: 'submit', request: item })
                                  }
                                >
                                  Submit
                                </Button>
                              )}
                              {actions.canDecide && (
                                <>
                                  <Button
                                    size="sm"
                                    variant="outline"
                                    onClick={() =>
                                      ask({ kind: 'approve', request: item })
                                    }
                                  >
                                    Approve
                                  </Button>
                                  <Button
                                    size="sm"
                                    variant="ghost"
                                    onClick={() =>
                                      ask({ kind: 'reject', request: item })
                                    }
                                  >
                                    Reject
                                  </Button>
                                </>
                              )}
                              {actions.canRevalidate && (
                                <Button
                                  size="sm"
                                  variant="ghost"
                                  onClick={() =>
                                    ask({ kind: 'revalidate', request: item })
                                  }
                                >
                                  Revalidate
                                </Button>
                              )}
                              {actions.canApply && (
                                <Button
                                  size="sm"
                                  onClick={() =>
                                    ask({ kind: 'apply', request: item })
                                  }
                                >
                                  Apply
                                </Button>
                              )}
                              {actions.canCancel && (
                                <Button
                                  size="sm"
                                  variant="ghost"
                                  onClick={() =>
                                    ask({ kind: 'cancel', request: item })
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
                    {!requests.isLoading && !requests.data?.items.length && (
                      <TableRow>
                        <TableCell colSpan={6}>
                          <Empty text="No controlled change requests match this view." />
                        </TableCell>
                      </TableRow>
                    )}
                  </TableBody>
                </Table>
              </div>
              <Pagination
                currentPage={requests.data?.page ?? 1}
                totalPages={Math.max(
                  1,
                  Math.ceil(
                    (requests.data?.totalCount ?? 0) /
                      (requests.data?.pageSize ?? 25)
                  )
                )}
                totalItems={requests.data?.totalCount ?? 0}
                pageSize={requests.data?.pageSize ?? 25}
                onPageChange={(page) =>
                  setFilters((current) => ({ ...current, page }))
                }
                onPageSizeChange={(pageSize) =>
                  setFilters((current) => ({ ...current, page: 1, pageSize }))
                }
              />
            </TabsContent>

            <TabsContent value="policies" className="space-y-4 pt-4">
              {isAdministrator && (
                <div className="flex justify-end">
                  <Button onClick={() => openPolicy()}>
                    <Plus className="mr-2 h-4 w-4" />
                    New policy version
                  </Button>
                </div>
              )}
              <div className="divide-y rounded-lg border">
                {(policies.data ?? []).map((item) => {
                  const actions = policyActions(item);
                  return (
                    <div
                      key={item.id}
                      className="grid gap-3 p-4 lg:grid-cols-[minmax(0,1.2fr)_minmax(0,1fr)_minmax(0,.8fr)_auto] lg:items-center"
                    >
                      <div>
                        <div className="flex flex-wrap items-center gap-2">
                          <p className="font-medium">{item.name}</p>
                          <Badge variant="outline">v{item.version}</Badge>
                          <Badge
                            variant={item.isEffective ? 'default' : 'outline'}
                          >
                            {item.status}
                          </Badge>
                        </div>
                        <p className="mt-1 text-xs text-muted-foreground">
                          {resourceLabel[item.resourceType] ??
                            item.resourceType}
                        </p>
                      </div>
                      <div className="text-sm">
                        <p>Maker: {item.makerRoles.join(', ')}</p>
                        <p>Checker: {item.checkerRoles.join(', ')}</p>
                      </div>
                      <div className="text-sm text-muted-foreground">
                        <p>
                          {dateValue(item.effectiveFromUtc)} →{' '}
                          {dateValue(item.effectiveToUtc) || 'Open'}
                        </p>
                        <p>
                          {item.requireEvidence
                            ? 'Evidence required'
                            : 'Evidence optional'}{' '}
                          ·{' '}
                          {item.workflowDefinitionName
                            ? `Shared workflow v${item.workflowDefinitionVersion}`
                            : 'Role decision'}
                        </p>
                      </div>
                      <div className="flex justify-end gap-1">
                        {isAdministrator && actions.canEdit && (
                          <Button
                            size="sm"
                            variant="ghost"
                            onClick={() => openPolicy(item)}
                          >
                            <Pencil className="mr-1 h-3.5 w-3.5" />
                            Edit
                          </Button>
                        )}
                        {isSuperAdmin && actions.canActivate && (
                          <Button
                            size="sm"
                            onClick={() =>
                              ask({ kind: 'activate-policy', policy: item })
                            }
                          >
                            Activate
                          </Button>
                        )}
                        {isSuperAdmin && actions.canRetire && (
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() =>
                              ask({ kind: 'retire-policy', policy: item })
                            }
                          >
                            Retire
                          </Button>
                        )}
                      </div>
                    </div>
                  );
                })}
                {!policies.isLoading && !policies.data?.length && (
                  <Empty text="No maker-checker policies have been configured. Direct behavior remains unchanged until an effective policy is activated." />
                )}
              </div>
            </TabsContent>

            <TabsContent
              value="registry"
              className="grid gap-4 pt-4 lg:grid-cols-2"
            >
              {(registry.data ?? []).map((item) => (
                <ResourceCard key={item.resourceType} item={item} />
              ))}
            </TabsContent>
          </Tabs>
        </CardContent>
      </Card>

      <Dialog open={policyOpen} onOpenChange={setPolicyOpen}>
        <DialogContent className="max-h-[92vh] max-w-3xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>
              {policyId ? 'Edit Draft policy' : 'New policy version'}
            </DialogTitle>
            <DialogDescription>
              Only Draft policy content is editable. Activation requires
              SuperAdmin and freezes the configured control version.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <Label>Resource *</Label>
              <Select
                value={policyForm.resourceType}
                disabled={Boolean(policyId)}
                onValueChange={(value) =>
                  setPolicyForm((current) => ({
                    ...current,
                    resourceType: value as ProcurementMasterDataResourceType,
                  }))
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {(registry.data ?? []).map((item) => (
                    <SelectItem
                      key={item.resourceType}
                      value={item.resourceType}
                    >
                      {item.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Name *</Label>
              <Input
                value={policyForm.name}
                onChange={(event) =>
                  setPolicyForm((current) => ({
                    ...current,
                    name: event.target.value,
                  }))
                }
              />
            </div>
            <div className="sm:col-span-2">
              <Label>Description</Label>
              <Textarea
                value={policyForm.description ?? ''}
                onChange={(event) =>
                  setPolicyForm((current) => ({
                    ...current,
                    description: event.target.value || undefined,
                  }))
                }
              />
            </div>
            <div>
              <Label>Maker roles *</Label>
              <Input
                value={makerRoles}
                onChange={(event) => setMakerRoles(event.target.value)}
              />
              <p className="mt-1 text-xs text-muted-foreground">
                Comma-separated registered TDC roles.
              </p>
            </div>
            <div>
              <Label>Checker roles *</Label>
              <Input
                value={checkerRoles}
                onChange={(event) => setCheckerRoles(event.target.value)}
              />
              <p className="mt-1 text-xs text-muted-foreground">
                Must be disjoint from maker roles.
              </p>
            </div>
            <div>
              <Label>Effective from *</Label>
              <Input
                type="date"
                value={policyForm.effectiveFromUtc}
                onChange={(event) =>
                  setPolicyForm((current) => ({
                    ...current,
                    effectiveFromUtc: event.target.value,
                  }))
                }
              />
            </div>
            <div>
              <Label>Effective to</Label>
              <Input
                type="date"
                value={policyForm.effectiveToUtc ?? ''}
                onChange={(event) =>
                  setPolicyForm((current) => ({
                    ...current,
                    effectiveToUtc: event.target.value || undefined,
                  }))
                }
              />
            </div>
            <div className="sm:col-span-2">
              <Label>Published shared workflow definition ID</Label>
              <Input
                value={policyForm.workflowDefinitionId ?? ''}
                onChange={(event) =>
                  setPolicyForm((current) => ({
                    ...current,
                    workflowDefinitionId: event.target.value || undefined,
                  }))
                }
                placeholder="Optional — use the shared workflow designer"
              />
            </div>
            <label className="flex items-center gap-2 text-sm">
              <Checkbox
                checked={policyForm.requireEvidence}
                onCheckedChange={(checked) =>
                  setPolicyForm((current) => ({
                    ...current,
                    requireEvidence: checked === true,
                  }))
                }
              />
              Require a shared evidence reference
            </label>
            <div className="rounded-md border bg-muted/30 p-3 text-sm">
              Independent approval and revalidation are mandatory and cannot be
              disabled.
            </div>
          </div>
          {policyError && (
            <p className="text-sm text-destructive">{policyError}</p>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setPolicyOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => savePolicy.mutate()}
              disabled={Boolean(policyError) || savePolicy.isPending}
            >
              {savePolicy.isPending ? 'Saving…' : 'Save Draft'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={changeOpen} onOpenChange={setChangeOpen}>
        <DialogContent className="max-h-[92vh] max-w-3xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>
              {changeId ? 'Edit Draft change' : 'New staged change'}
            </DialogTitle>
            <DialogDescription>
              The server captures the current tenant target as the immutable
              before snapshot. Only registry-whitelisted fields are accepted.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <Label>Resource *</Label>
              <Select
                value={changeForm.resourceType}
                disabled={Boolean(changeId)}
                onValueChange={(value) =>
                  setChangeForm(
                    emptyChange(value as ProcurementMasterDataResourceType)
                  )
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {(registry.data ?? []).map((item) => (
                    <SelectItem
                      key={item.resourceType}
                      value={item.resourceType}
                    >
                      {item.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>
                Target ID{' '}
                {selectedResource?.allowsTenantSingletonTarget
                  ? '(tenant singleton)'
                  : '*'}
              </Label>
              <Input
                value={changeForm.targetId}
                disabled={
                  Boolean(changeId) ||
                  selectedResource?.allowsTenantSingletonTarget
                }
                onChange={(event) =>
                  setChangeForm((current) => ({
                    ...current,
                    targetId: event.target.value,
                  }))
                }
                placeholder={
                  selectedResource?.allowsTenantSingletonTarget
                    ? 'Resolved from tenant context'
                    : 'Existing record UUID'
                }
              />
            </div>
            <div>
              <Label>Effective date *</Label>
              <Input
                type="date"
                value={changeForm.effectiveAtUtc}
                onChange={(event) =>
                  setChangeForm((current) => ({
                    ...current,
                    effectiveAtUtc: event.target.value,
                  }))
                }
              />
            </div>
            <div>
              <Label>External evidence reference</Label>
              <Input
                value={evidenceReference}
                onChange={(event) => setEvidenceReference(event.target.value)}
                placeholder="Optional existing case/document reference"
              />
            </div>
            <div className="sm:col-span-2">
              <Label>Business reason *</Label>
              <Textarea
                value={changeForm.reason}
                onChange={(event) =>
                  setChangeForm((current) => ({
                    ...current,
                    reason: event.target.value,
                  }))
                }
              />
            </div>
            <div className="sm:col-span-2">
              <div className="flex items-center justify-between gap-3">
                <Label>Proposed field changes (JSON object) *</Label>
                <Badge variant="secondary">
                  {selectedResource?.allowedFields.length ?? 0} allowed fields
                </Badge>
              </div>
              <Textarea
                className="min-h-48 font-mono text-xs"
                value={changeForm.proposedChangesJson}
                onChange={(event) =>
                  setChangeForm((current) => ({
                    ...current,
                    proposedChangesJson: event.target.value,
                  }))
                }
              />
              <p className="mt-1 text-xs text-muted-foreground">
                Allowed:{' '}
                {selectedResource?.allowedFields.join(', ') ||
                  'Select a resource.'}
              </p>
            </div>
          </div>
          {changeError && (
            <p className="text-sm text-destructive">{changeError}</p>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setChangeOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => saveChange.mutate()}
              disabled={Boolean(changeError) || saveChange.isPending}
            >
              {saveChange.isPending ? 'Saving…' : 'Save Draft'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(lifecycle)}
        onOpenChange={(open) => !open && setLifecycle(undefined)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {lifecycle?.kind
                .replaceAll('-', ' ')
                .replace(/(^|\s)\S/g, (value) => value.toUpperCase())}
            </DialogTitle>
            <DialogDescription>
              This action is tenant-scoped, row-version checked, and written to
              the immutable procurement control-event ledger.
            </DialogDescription>
          </DialogHeader>
          <div>
            <Label>
              Reason / comment {requiresComment ? '*' : '(optional)'}
            </Label>
            <Textarea
              value={lifecycleComment}
              onChange={(event) => setLifecycleComment(event.target.value)}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setLifecycle(undefined)}>
              Cancel
            </Button>
            <Button
              onClick={() => runLifecycle.mutate()}
              disabled={
                runLifecycle.isPending ||
                (requiresComment && !lifecycleComment.trim())
              }
            >
              {runLifecycle.isPending ? 'Applying…' : 'Confirm'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(selectedId)}
        onOpenChange={(open) => !open && setSelectedId(undefined)}
      >
        <DialogContent className="max-h-[92vh] max-w-5xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>
              {detail.data?.requestNumber ?? 'Change-request detail'}
            </DialogTitle>
            <DialogDescription>
              Immutable before/proposed values, approval and revalidation
              lineage, effective result, workflow link, and shared evidence
              references.
            </DialogDescription>
          </DialogHeader>
          {detail.isLoading && (
            <p className="py-12 text-center text-muted-foreground">
              Loading request…
            </p>
          )}
          {detail.isError && (
            <Alert variant="destructive">
              <ShieldAlert className="h-4 w-4" />
              <AlertTitle>Unable to load request</AlertTitle>
              <AlertDescription>{detail.error.message}</AlertDescription>
            </Alert>
          )}
          {detail.data && (
            <RequestDetail
              item={detail.data}
              resourceName={resourceLabel[detail.data.resourceType]}
            />
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}

function Metric({
  label,
  value,
  detail,
}: {
  label: string;
  value: number;
  detail: string;
}) {
  return (
    <div className="rounded-lg border p-4">
      <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
        {label}
      </p>
      <p className="mt-2 text-2xl font-semibold">{value}</p>
      <p className="text-xs text-muted-foreground">{detail}</p>
    </div>
  );
}

function Empty({ text }: { text: string }) {
  return (
    <div className="flex min-h-36 flex-col items-center justify-center p-6 text-center text-sm text-muted-foreground">
      <FileCheck2 className="mb-2 h-8 w-8" />
      {text}
    </div>
  );
}

function ResourceCard({
  item,
}: {
  item: ProcurementMasterDataResourceDefinition;
}) {
  return (
    <Card>
      <CardHeader>
        <div className="flex items-start justify-between gap-3">
          <div>
            <CardTitle className="text-lg">{item.name}</CardTitle>
            <CardDescription className="mt-1">
              {item.description}
            </CardDescription>
          </div>
          <Badge variant="outline">{item.code}</Badge>
        </div>
      </CardHeader>
      <CardContent>
        <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
          Explicit field whitelist
        </p>
        <div className="mt-2 flex flex-wrap gap-1">
          {item.allowedFields.map((field) => (
            <Badge
              key={field}
              variant="secondary"
              className="font-mono text-[11px]"
            >
              {field}
            </Badge>
          ))}
        </div>
        <p className="mt-3 text-xs text-muted-foreground">
          {item.sourceRequirements}
          {item.allowsTenantSingletonTarget ? ' · tenant singleton target' : ''}
        </p>
      </CardContent>
    </Card>
  );
}

function RequestDetail({
  item,
  resourceName,
}: {
  item: ProcurementMasterDataChange;
  resourceName?: string;
}) {
  return (
    <div className="space-y-5">
      <div className="flex flex-wrap gap-2">
        <Badge variant="outline" className={statusTone(item.status)}>
          {item.status}
        </Badge>
        <Badge variant="outline">{resourceName ?? item.resourceType}</Badge>
        <Badge variant="outline">Policy v{item.policyVersion}</Badge>
        {item.revalidationPassed === true && (
          <Badge className="bg-emerald-600">Revalidated</Badge>
        )}
        {item.revalidationPassed === false && (
          <Badge variant="destructive">Revalidation failed</Badge>
        )}
      </div>
      <dl className="grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-3">
        <Info
          label="Target"
          value={`${item.targetReference} · ${item.targetKind}`}
        />
        <Info label="Effective" value={dateTime(item.effectiveAtUtc)} />
        <Info
          label="Maker / checker"
          value={`${item.makerUserId} / ${item.checkerUserId ?? 'Pending'}`}
        />
        <Info
          label="Submitted / checked"
          value={`${dateTime(item.submittedAtUtc)} / ${dateTime(item.checkedAtUtc)}`}
        />
        <Info
          label="Shared workflow"
          value={
            item.workflowInstanceId ??
            item.workflowDefinitionId ??
            'Role-based maker-checker'
          }
        />
        <Info label="Correlation" value={item.correlationId} />
      </dl>
      <div className="rounded-md border bg-muted/30 p-3 text-sm">
        <span className="font-medium">Reason: </span>
        {item.reason}
        {item.checkerComment && (
          <p className="mt-1">
            <span className="font-medium">Checker: </span>
            {item.checkerComment}
          </p>
        )}
        {item.revalidationMessage && (
          <p className="mt-1">
            <span className="font-medium">Revalidation: </span>
            {item.revalidationMessage}
          </p>
        )}
      </div>
      <div className="grid gap-4 lg:grid-cols-3">
        <JsonPanel
          label="Immutable before"
          value={item.beforeJson}
          hash={item.beforeHash}
        />
        <JsonPanel
          label="Proposed changes"
          value={item.proposedChangesJson}
          hash={item.proposedChangesHash}
        />
        <JsonPanel
          label="Applied after"
          value={item.appliedAfterJson}
          hash={item.appliedAfterHash}
        />
      </div>
      <div>
        <div className="mb-2 flex items-center justify-between">
          <Label>Shared evidence references</Label>
          <Badge variant="secondary">{item.evidence.length}</Badge>
        </div>
        {item.evidence.length ? (
          <div className="divide-y rounded-md border">
            {item.evidence.map((value) => (
              <div
                key={value.id}
                className="flex items-center justify-between gap-3 p-3 text-sm"
              >
                <div>
                  <p className="font-medium">
                    {value.label || value.fileName || value.reference}
                  </p>
                  <p className="text-xs text-muted-foreground">
                    {value.referenceKind} ·{' '}
                    {value.requirementKey || 'No requirement key'}
                  </p>
                </div>
                <Badge variant="outline">
                  {value.referenceAvailable
                    ? value.verificationStatus || 'Available'
                    : 'Unavailable'}
                </Badge>
              </div>
            ))}
          </div>
        ) : (
          <p className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
            No evidence reference was configured for this request.
          </p>
        )}
      </div>
      <div className="grid gap-3 rounded-md border bg-muted/20 p-3 text-xs sm:grid-cols-2">
        <p className="break-all">
          <span className="font-medium">Revalidated snapshot: </span>
          {item.revalidatedSnapshotHash ?? 'Not recorded'}
        </p>
        <p className="break-all">
          <span className="font-medium">Applied hash: </span>
          {item.appliedAfterHash ?? 'Not applied'}
        </p>
      </div>
    </div>
  );
}

function Info({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="break-all font-medium">{value}</dd>
    </div>
  );
}

function JsonPanel({
  label,
  value,
  hash,
}: {
  label: string;
  value?: string;
  hash?: string;
}) {
  return (
    <div className="space-y-1.5">
      <Label>{label}</Label>
      <pre className="max-h-72 overflow-auto rounded-md border bg-slate-950 p-3 text-xs leading-5 text-slate-100">
        {prettyJson(value)}
      </pre>
      <p className="break-all font-mono text-[10px] text-muted-foreground">
        SHA-256 {hash ?? '—'}
      </p>
    </div>
  );
}
