'use client';

import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  CheckCircle2,
  Clock3,
  Copy,
  FileCheck2,
  History,
  Loader2,
  Plus,
  RefreshCw,
  Save,
  Search,
  ShieldCheck,
  Trash2,
} from 'lucide-react';
import { toast } from 'sonner';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import {
  emptySupplierEvidenceRequirement,
  supplierCategoryLabel,
  supplierEvidencePackStatusLabel,
  validateSupplierEvidencePack,
} from '@/lib/procurement-supplier-evidence-pack';
import { procurementControlEventService } from '@/services/procurement-control-event.service';
import { procurementSupplierEvidencePackService as service } from '@/services/procurement-supplier-evidence-pack.service';
import type {
  SaveSupplierEvidencePack,
  SupplierEvidencePack,
  SupplierEvidencePackSearch,
  SupplierEvidencePackStatus,
  SupplierRegistrationCategory,
} from '@/types/procurement-supplier-evidence-pack';

const categories: SupplierRegistrationCategory[] = [
  'Goods',
  'Works',
  'Services',
];
const statuses: SupplierEvidencePackStatus[] = [
  'Draft',
  'PendingApproval',
  'Published',
  'Retired',
];

const localInput = (value?: string | Date) => {
  if (!value) return '';
  const date = value instanceof Date ? value : new Date(value);
  const shifted = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return shifted.toISOString().slice(0, 16);
};

const emptyForm = (): SaveSupplierEvidencePack => ({
  packCode: '',
  name: '',
  description: '',
  category: 'Goods',
  effectiveFromUtc: localInput(new Date(Date.now() + 60 * 60 * 1000)),
  sourceConfigurationProfileId: '',
  workflowDefinitionId: '',
  changeSummary: '',
  requirements: [emptySupplierEvidenceRequirement()],
});

const toForm = (item: SupplierEvidencePack): SaveSupplierEvidencePack => ({
  packCode: item.packCode,
  name: item.name,
  description: item.description ?? '',
  category: item.category,
  effectiveFromUtc: localInput(item.effectiveFromUtc),
  effectiveToUtc: localInput(item.effectiveToUtc),
  sourceConfigurationProfileId: item.sourceConfigurationProfileId,
  workflowDefinitionId: item.workflowDefinitionId,
  changeSummary: item.changeSummary ?? '',
  rowVersion: item.rowVersion,
  requirements: item.requirements.map((requirement) => ({
    requirementCode: requirement.requirementCode,
    name: requirement.name,
    description: requirement.description,
    kind: requirement.kind,
    documentType: requirement.documentType,
    isMandatory: requirement.isMandatory,
    classificationScheme: requirement.classificationScheme,
    allowedClassifications: requirement.allowedClassifications,
    validityMode: requirement.validityMode,
    minimumRemainingDays: requirement.minimumRemainingDays,
    approvalStepOrder: requirement.approvalStepOrder,
    approvalStepName: requirement.approvalStepName,
    maxFileSizeBytes: requirement.maxFileSizeBytes,
    allowedMimeTypes: requirement.allowedMimeTypes,
  })),
});

type LifecycleAction = 'submit' | 'publish' | 'reject' | 'retire' | 'delete';

export default function SupplierEvidencePacksPage() {
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const canManage = hasPermission('procurement.supplier.manage');
  const canReview = hasPermission('procurement.supplier.review');
  const canApprove = hasPermission('procurement.supplier.approve');
  const [filters, setFilters] = useState<SupplierEvidencePackSearch>({
    page: 1,
    pageSize: 25,
  });
  const [editorOpen, setEditorOpen] = useState(false);
  const [selectedId, setSelectedId] = useState<string>();
  const [form, setForm] = useState<SaveSupplierEvidencePack>(emptyForm());
  const [busy, setBusy] = useState<string>();
  const [lifecycleAction, setLifecycleAction] = useState<LifecycleAction>();
  const [comment, setComment] = useState('');
  const [evidenceReference, setEvidenceReference] = useState('');
  const [cloneOpen, setCloneOpen] = useState(false);
  const [cloneFrom, setCloneFrom] = useState('');
  const [cloneTo, setCloneTo] = useState('');
  const [cloneSummary, setCloneSummary] = useState('');

  const summary = useQuery({
    queryKey: ['supplier-evidence-pack-summary'],
    queryFn: service.summary,
  });
  const packs = useQuery({
    queryKey: ['supplier-evidence-packs', filters],
    queryFn: () => service.search(filters),
  });
  const workflows = useQuery({
    queryKey: ['supplier-evidence-pack-workflows'],
    queryFn: service.workflowOptions,
  });
  const profiles = useQuery({
    queryKey: ['supplier-evidence-pack-profiles'],
    queryFn: service.configurationProfileOptions,
  });
  const detail = useQuery({
    queryKey: ['supplier-evidence-pack', selectedId],
    queryFn: () => {
      if (!selectedId) throw new Error('Evidence-pack id is required.');
      return service.get(selectedId);
    },
    enabled: Boolean(selectedId),
  });
  const audit = useQuery({
    queryKey: [
      'supplier-evidence-pack-audit',
      detail.data?.packCode,
      detail.data?.version,
    ],
    queryFn: () =>
      procurementControlEventService.search({
        sourceType: 'ProcurementSupplierEvidencePack',
        sourceReference: `${detail.data?.packCode}/v${detail.data?.version}`,
        page: 1,
        pageSize: 50,
      }),
    enabled: Boolean(detail.data),
  });

  useEffect(() => {
    if (!detail.data) return;
    setForm(toForm(detail.data));
    setCloneFrom(localInput(detail.data.effectiveFromUtc));
  }, [detail.data]);

  const selectedWorkflow = useMemo(
    () => workflows.data?.find((item) => item.id === form.workflowDefinitionId),
    [form.workflowDefinitionId, workflows.data]
  );

  const refresh = async () => {
    await Promise.all([
      summary.refetch(),
      packs.refetch(),
      selectedId ? detail.refetch() : Promise.resolve(),
      detail.data ? audit.refetch() : Promise.resolve(),
    ]);
  };

  const invalidate = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['supplier-evidence-pack'] }),
      queryClient.invalidateQueries({ queryKey: ['supplier-evidence-packs'] }),
      queryClient.invalidateQueries({
        queryKey: ['supplier-evidence-pack-summary'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['supplier-evidence-pack-audit'],
      }),
    ]);
  };

  const openCreate = () => {
    setSelectedId(undefined);
    setForm(emptyForm());
    setEditorOpen(true);
  };

  const openDetail = (id: string) => {
    setSelectedId(id);
    setEditorOpen(true);
  };

  const run = async (
    key: string,
    action: () => Promise<unknown>,
    message: string
  ) => {
    try {
      setBusy(key);
      await action();
      toast.success(message);
      await invalidate();
      return true;
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Action failed');
      return false;
    } finally {
      setBusy(undefined);
    }
  };

  const save = async () => {
    const validation = validateSupplierEvidencePack(form);
    if (validation) {
      toast.error(validation);
      return;
    }
    const request: SaveSupplierEvidencePack = {
      ...form,
      packCode: form.packCode.trim().toUpperCase(),
      effectiveFromUtc: new Date(form.effectiveFromUtc).toISOString(),
      effectiveToUtc: form.effectiveToUtc
        ? new Date(form.effectiveToUtc).toISOString()
        : undefined,
      rowVersion: detail.data?.rowVersion,
    };
    if (selectedId) {
      await run(
        'save',
        () => service.update(selectedId, request),
        'Supplier evidence-pack Draft saved'
      );
      return;
    }
    const created = await service.create(request).catch((error: unknown) => {
      toast.error(error instanceof Error ? error.message : 'Create failed');
      return undefined;
    });
    if (!created) return;
    toast.success('Supplier evidence-pack Draft created');
    setSelectedId(created.id);
    await invalidate();
  };

  const lifecycle = async () => {
    if (!detail.data || !selectedId || !lifecycleAction) return;
    if (!evidenceReference.trim()) {
      toast.error('Shared lifecycle evidence reference is required.');
      return;
    }
    const request = {
      rowVersion: detail.data.rowVersion,
      comment: comment.trim() || undefined,
      evidence: [
        {
          referenceKind: 'ExternalReference' as const,
          reference: evidenceReference.trim(),
          label: `${lifecycleAction} supplier evidence pack`,
          requirementKey: 'SUP-002',
        },
      ],
    };
    const calls = {
      submit: () => service.submit(selectedId, request),
      publish: () => service.publish(selectedId, request),
      reject: () => service.reject(selectedId, request),
      retire: () => service.retire(selectedId, request),
      delete: () => service.deleteDraft(selectedId, request),
    };
    const completed = await run(
      lifecycleAction,
      calls[lifecycleAction],
      lifecycleAction === 'delete'
        ? 'Draft deleted'
        : `Evidence pack ${lifecycleAction} action completed`
    );
    if (completed) {
      setLifecycleAction(undefined);
      setComment('');
      setEvidenceReference('');
      if (lifecycleAction === 'delete') {
        setEditorOpen(false);
        setSelectedId(undefined);
      }
    }
  };

  const clone = async () => {
    if (!detail.data || !selectedId || !cloneFrom || !cloneSummary.trim()) {
      toast.error('Effective-from date and change summary are required.');
      return;
    }
    const cloned = await service
      .clone(selectedId, {
        rowVersion: detail.data.rowVersion,
        effectiveFromUtc: new Date(cloneFrom).toISOString(),
        effectiveToUtc: cloneTo ? new Date(cloneTo).toISOString() : undefined,
        changeSummary: cloneSummary.trim(),
      })
      .catch((error: unknown) => {
        toast.error(error instanceof Error ? error.message : 'Clone failed');
        return undefined;
      });
    if (!cloned) return;
    toast.success('New Draft version cloned');
    setCloneOpen(false);
    setSelectedId(cloned.id);
    await invalidate();
  };

  const setRequirement = (
    index: number,
    patch: Partial<SaveSupplierEvidencePack['requirements'][number]>
  ) =>
    setForm((current) => ({
      ...current,
      requirements: current.requirements.map((item, itemIndex) =>
        itemIndex === index ? { ...item, ...patch } : item
      ),
    }));

  const item = detail.data;
  const editable = !selectedId || (item?.status === 'Draft' && canManage);
  const actions = item?.allowedActions ?? [];
  const cards = [
    ['Families', summary.data?.familyCount ?? 0, History],
    ['Drafts', summary.data?.draftCount ?? 0, FileCheck2],
    ['Pending', summary.data?.pendingApprovalCount ?? 0, Clock3],
    ['Published', summary.data?.publishedCount ?? 0, ShieldCheck],
    ['Effective now', summary.data?.effectiveCount ?? 0, CheckCircle2],
  ] as const;

  return (
    <div className="space-y-6 p-6" data-testid="supplier-evidence-pack-page">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">
            Supplier registration evidence packs
          </h1>
          <p className="max-w-4xl text-sm text-muted-foreground">
            Governed Goods, Works, and Services requirements with exact
            configuration-profile, shared-workflow, validity, classification,
            approval-step, and immutable version lineage.
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => void refresh()}>
            <RefreshCw className="mr-2 h-4 w-4" /> Refresh
          </Button>
          {canManage && (
            <Button onClick={openCreate}>
              <Plus className="mr-2 h-4 w-4" /> New controlled Draft
            </Button>
          )}
        </div>
      </div>

      {(summary.isError || packs.isError) && (
        <Alert variant="destructive">
          <AlertTitle>Evidence-pack register could not be loaded</AlertTitle>
          <AlertDescription>
            Check the tenant session and supplier-management capability, then
            retry.
          </AlertDescription>
        </Alert>
      )}

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
        {cards.map(([label, value, Icon]) => (
          <Card key={label}>
            <CardContent className="flex items-center justify-between p-4">
              <div>
                <p className="text-xs uppercase text-muted-foreground">
                  {label}
                </p>
                <p className="mt-1 text-2xl font-semibold">{value}</p>
              </div>
              <Icon className="h-5 w-5 text-muted-foreground" />
            </CardContent>
          </Card>
        ))}
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Pack version and approval history</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-3 md:grid-cols-[1fr_200px_200px]">
            <div className="relative">
              <Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                aria-label="Search supplier evidence packs"
                className="pl-9"
                placeholder="Search code or name"
                value={filters.search ?? ''}
                onChange={(event) =>
                  setFilters((current) => ({
                    ...current,
                    page: 1,
                    search: event.target.value || undefined,
                  }))
                }
              />
            </div>
            <Select
              value={filters.category ?? 'all'}
              onValueChange={(value) =>
                setFilters((current) => ({
                  ...current,
                  page: 1,
                  category:
                    value === 'all'
                      ? undefined
                      : (value as SupplierRegistrationCategory),
                }))
              }
            >
              <SelectTrigger>
                <SelectValue placeholder="All categories" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All categories</SelectItem>
                {categories.map((category) => (
                  <SelectItem key={category} value={category}>
                    {supplierCategoryLabel[category]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Select
              value={filters.status ?? 'all'}
              onValueChange={(value) =>
                setFilters((current) => ({
                  ...current,
                  page: 1,
                  status:
                    value === 'all'
                      ? undefined
                      : (value as SupplierEvidencePackStatus),
                }))
              }
            >
              <SelectTrigger>
                <SelectValue placeholder="All statuses" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All statuses</SelectItem>
                {statuses.map((status) => (
                  <SelectItem key={status} value={status}>
                    {supplierEvidencePackStatusLabel[status]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Pack</TableHead>
                  <TableHead>Category / version</TableHead>
                  <TableHead>Requirements</TableHead>
                  <TableHead>Configuration lineage</TableHead>
                  <TableHead>Effective period</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {(packs.data?.items ?? []).map((pack) => (
                  <TableRow key={pack.id}>
                    <TableCell>
                      <div className="font-medium">{pack.packCode}</div>
                      <div className="max-w-64 truncate text-xs text-muted-foreground">
                        {pack.name}
                      </div>
                    </TableCell>
                    <TableCell>
                      {supplierCategoryLabel[pack.category]} · v{pack.version}
                    </TableCell>
                    <TableCell>
                      {pack.mandatoryRequirementCount} mandatory /{' '}
                      {pack.requirementCount} total
                    </TableCell>
                    <TableCell>
                      {pack.sourceConfigurationProfileCode}/v
                      {pack.sourceConfigurationProfileVersion}
                    </TableCell>
                    <TableCell className="text-xs">
                      {new Date(pack.effectiveFromUtc).toLocaleDateString()} –{' '}
                      {pack.effectiveToUtc
                        ? new Date(pack.effectiveToUtc).toLocaleDateString()
                        : 'open'}
                    </TableCell>
                    <TableCell>
                      <Badge variant={pack.isEffective ? 'default' : 'outline'}>
                        {supplierEvidencePackStatusLabel[pack.status]}
                      </Badge>
                    </TableCell>
                    <TableCell>
                      <Button
                        size="sm"
                        variant="ghost"
                        onClick={() => openDetail(pack.id)}
                      >
                        Open
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
                {!packs.isLoading && !packs.data?.items.length && (
                  <TableRow>
                    <TableCell
                      colSpan={7}
                      className="py-10 text-center text-muted-foreground"
                    >
                      No supplier evidence-pack versions match the filters.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
          {packs.data && packs.data.totalCount > (filters.pageSize ?? 25) && (
            <Pagination
              currentPage={filters.page ?? 1}
              totalPages={Math.ceil(
                packs.data.totalCount / (filters.pageSize ?? 25)
              )}
              onPageChange={(page) =>
                setFilters((current) => ({ ...current, page }))
              }
            />
          )}
        </CardContent>
      </Card>

      <Dialog open={editorOpen} onOpenChange={setEditorOpen}>
        <DialogContent className="max-h-[94vh] max-w-6xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>
              {selectedId
                ? `${item?.packCode ?? 'Evidence pack'} / v${item?.version ?? ''}`
                : 'New supplier evidence-pack Draft'}
            </DialogTitle>
            <DialogDescription>
              Published and retired versions are immutable. Clone a retained
              version to create a replacement without retiring a currently
              effective pack before its successor starts.
            </DialogDescription>
          </DialogHeader>

          {selectedId && detail.isLoading ? (
            <div className="flex min-h-64 items-center justify-center">
              <Loader2 className="h-7 w-7 animate-spin" />
            </div>
          ) : (
            <div className="space-y-5">
              {item && (
                <div className="flex flex-wrap gap-2">
                  <Badge>{supplierEvidencePackStatusLabel[item.status]}</Badge>
                  <Badge variant="outline">
                    {supplierCategoryLabel[item.category]}
                  </Badge>
                  <Badge variant="outline">
                    {item.sourceConfigurationProfileCode}/v
                    {item.sourceConfigurationProfileVersion}
                  </Badge>
                  <Badge variant="outline">
                    Audit events: {audit.data?.totalCount ?? 0}
                  </Badge>
                </div>
              )}

              <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
                <Field label="Pack code">
                  <Input
                    value={form.packCode}
                    disabled={!editable || Boolean(selectedId)}
                    onChange={(event) =>
                      setForm({ ...form, packCode: event.target.value })
                    }
                  />
                </Field>
                <Field label="Name">
                  <Input
                    value={form.name}
                    disabled={!editable}
                    onChange={(event) =>
                      setForm({ ...form, name: event.target.value })
                    }
                  />
                </Field>
                <Field label="Category">
                  <Select
                    disabled={!editable}
                    value={form.category}
                    onValueChange={(category) =>
                      setForm({
                        ...form,
                        category: category as SupplierRegistrationCategory,
                      })
                    }
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {categories.map((category) => (
                        <SelectItem key={category} value={category}>
                          {supplierCategoryLabel[category]}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </Field>
                <Field label="Effective from">
                  <Input
                    type="datetime-local"
                    value={form.effectiveFromUtc}
                    disabled={!editable}
                    onChange={(event) =>
                      setForm({
                        ...form,
                        effectiveFromUtc: event.target.value,
                      })
                    }
                  />
                </Field>
                <Field label="Effective to (optional)">
                  <Input
                    type="datetime-local"
                    value={form.effectiveToUtc ?? ''}
                    disabled={!editable}
                    onChange={(event) =>
                      setForm({
                        ...form,
                        effectiveToUtc: event.target.value || undefined,
                      })
                    }
                  />
                </Field>
                <Field label="Published configuration profile">
                  <Select
                    disabled={!editable}
                    value={form.sourceConfigurationProfileId}
                    onValueChange={(sourceConfigurationProfileId) =>
                      setForm({ ...form, sourceConfigurationProfileId })
                    }
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select profile" />
                    </SelectTrigger>
                    <SelectContent>
                      {(profiles.data ?? []).map((profile) => (
                        <SelectItem key={profile.id} value={profile.id}>
                          {profile.code}/v{profile.version} · {profile.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </Field>
                <Field label="Published shared workflow">
                  <Select
                    disabled={!editable}
                    value={form.workflowDefinitionId}
                    onValueChange={(workflowDefinitionId) =>
                      setForm({ ...form, workflowDefinitionId })
                    }
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select workflow" />
                    </SelectTrigger>
                    <SelectContent>
                      {(workflows.data ?? []).map((workflow) => (
                        <SelectItem key={workflow.id} value={workflow.id}>
                          {workflow.name}/v{workflow.version}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </Field>
                <Field label="Change summary">
                  <Input
                    value={form.changeSummary ?? ''}
                    disabled={!editable}
                    onChange={(event) =>
                      setForm({ ...form, changeSummary: event.target.value })
                    }
                  />
                </Field>
                <div className="md:col-span-2 lg:col-span-3">
                  <Field label="Description">
                    <Textarea
                      value={form.description ?? ''}
                      disabled={!editable}
                      onChange={(event) =>
                        setForm({ ...form, description: event.target.value })
                      }
                    />
                  </Field>
                </div>
              </div>

              <Card>
                <CardHeader className="flex flex-row items-center justify-between">
                  <div>
                    <CardTitle className="text-base">
                      Category-aware mandatory evidence
                    </CardTitle>
                    <p className="text-xs text-muted-foreground">
                      Approval step must match the selected shared workflow
                      exactly. Lists are comma-separated.
                    </p>
                  </div>
                  {editable && (
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() =>
                        setForm((current) => ({
                          ...current,
                          requirements: [
                            ...current.requirements,
                            emptySupplierEvidenceRequirement(),
                          ],
                        }))
                      }
                    >
                      <Plus className="mr-2 h-4 w-4" /> Requirement
                    </Button>
                  )}
                </CardHeader>
                <CardContent className="space-y-4">
                  {form.requirements.map((requirement, index) => (
                    <div
                      key={`${requirement.requirementCode}-${index}`}
                      className="rounded-lg border p-4"
                    >
                      <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-4">
                        <Field label="Requirement code">
                          <Input
                            value={requirement.requirementCode}
                            disabled={!editable}
                            onChange={(event) =>
                              setRequirement(index, {
                                requirementCode: event.target.value,
                              })
                            }
                          />
                        </Field>
                        <Field label="Name">
                          <Input
                            value={requirement.name}
                            disabled={!editable}
                            onChange={(event) =>
                              setRequirement(index, {
                                name: event.target.value,
                              })
                            }
                          />
                        </Field>
                        <Field label="Kind">
                          <Select
                            value={requirement.kind}
                            disabled={!editable}
                            onValueChange={(kind) =>
                              setRequirement(index, {
                                kind: kind as typeof requirement.kind,
                              })
                            }
                          >
                            <SelectTrigger>
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              <SelectItem value="Document">Document</SelectItem>
                              <SelectItem value="Classification">
                                Classification
                              </SelectItem>
                              <SelectItem value="DocumentAndClassification">
                                Document + classification
                              </SelectItem>
                            </SelectContent>
                          </Select>
                        </Field>
                        <Field label="Document type">
                          <Input
                            value={requirement.documentType ?? ''}
                            disabled={
                              !editable || requirement.kind === 'Classification'
                            }
                            onChange={(event) =>
                              setRequirement(index, {
                                documentType: event.target.value,
                              })
                            }
                          />
                        </Field>
                        <Field label="Classification scheme">
                          <Input
                            value={requirement.classificationScheme ?? ''}
                            disabled={
                              !editable || requirement.kind === 'Document'
                            }
                            onChange={(event) =>
                              setRequirement(index, {
                                classificationScheme: event.target.value,
                              })
                            }
                          />
                        </Field>
                        <Field label="Allowed classifications">
                          <Input
                            value={requirement.allowedClassifications.join(
                              ', '
                            )}
                            disabled={
                              !editable || requirement.kind === 'Document'
                            }
                            onChange={(event) =>
                              setRequirement(index, {
                                allowedClassifications: event.target.value
                                  .split(',')
                                  .map((value) => value.trim())
                                  .filter(Boolean),
                              })
                            }
                          />
                        </Field>
                        <Field label="Validity">
                          <Select
                            value={requirement.validityMode}
                            disabled={!editable}
                            onValueChange={(validityMode) =>
                              setRequirement(index, {
                                validityMode:
                                  validityMode as typeof requirement.validityMode,
                              })
                            }
                          >
                            <SelectTrigger>
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              <SelectItem value="NotApplicable">
                                No expiry rule
                              </SelectItem>
                              <SelectItem value="CurrentOnSubmission">
                                Current on submission
                              </SelectItem>
                              <SelectItem value="MinimumRemainingDays">
                                Minimum remaining days
                              </SelectItem>
                            </SelectContent>
                          </Select>
                        </Field>
                        <Field label="Minimum remaining days">
                          <Input
                            type="number"
                            min={1}
                            value={requirement.minimumRemainingDays ?? ''}
                            disabled={
                              !editable ||
                              requirement.validityMode !==
                                'MinimumRemainingDays'
                            }
                            onChange={(event) =>
                              setRequirement(index, {
                                minimumRemainingDays: event.target.value
                                  ? Number(event.target.value)
                                  : undefined,
                              })
                            }
                          />
                        </Field>
                        <Field label="Approval workflow step">
                          <Select
                            disabled={!editable || !selectedWorkflow}
                            value={`${requirement.approvalStepOrder}|${requirement.approvalStepName}`}
                            onValueChange={(value) => {
                              const separator = value.indexOf('|');
                              setRequirement(index, {
                                approvalStepOrder: Number(
                                  value.slice(0, separator)
                                ),
                                approvalStepName: value.slice(separator + 1),
                              });
                            }}
                          >
                            <SelectTrigger>
                              <SelectValue placeholder="Exact workflow step" />
                            </SelectTrigger>
                            <SelectContent>
                              {(selectedWorkflow?.steps ?? []).map((step) => (
                                <SelectItem
                                  key={`${step.order}-${step.name}`}
                                  value={`${step.order}|${step.name}`}
                                >
                                  {step.order}. {step.name}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </Field>
                        <Field label="Allowed MIME types">
                          <Input
                            value={requirement.allowedMimeTypes.join(', ')}
                            disabled={!editable}
                            onChange={(event) =>
                              setRequirement(index, {
                                allowedMimeTypes: event.target.value
                                  .split(',')
                                  .map((value) => value.trim())
                                  .filter(Boolean),
                              })
                            }
                          />
                        </Field>
                        <Field label="Maximum file bytes">
                          <Input
                            type="number"
                            min={1}
                            value={requirement.maxFileSizeBytes}
                            disabled={!editable}
                            onChange={(event) =>
                              setRequirement(index, {
                                maxFileSizeBytes: Number(event.target.value),
                              })
                            }
                          />
                        </Field>
                        <div className="flex items-end justify-between gap-2 pb-2">
                          <label className="flex items-center gap-2 text-sm">
                            <Checkbox
                              checked={requirement.isMandatory}
                              disabled={!editable}
                              onCheckedChange={(checked) =>
                                setRequirement(index, {
                                  isMandatory: checked === true,
                                })
                              }
                            />
                            Mandatory
                          </label>
                          {editable && form.requirements.length > 1 && (
                            <Button
                              size="icon"
                              variant="ghost"
                              aria-label="Remove requirement"
                              onClick={() =>
                                setForm((current) => ({
                                  ...current,
                                  requirements: current.requirements.filter(
                                    (_, itemIndex) => itemIndex !== index
                                  ),
                                }))
                              }
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          )}
                        </div>
                      </div>
                    </div>
                  ))}
                </CardContent>
              </Card>

              {item?.blockedReasons?.length ? (
                <Alert variant="destructive">
                  <AlertTitle>Publication blockers</AlertTitle>
                  <AlertDescription>
                    {item.blockedReasons.join(' ')}
                  </AlertDescription>
                </Alert>
              ) : null}

              <div className="flex flex-wrap gap-2">
                {editable && canManage && (
                  <Button onClick={() => void save()} disabled={Boolean(busy)}>
                    {busy === 'save' ? (
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    ) : (
                      <Save className="mr-2 h-4 w-4" />
                    )}
                    Save Draft
                  </Button>
                )}
                {item && actions.includes('Submit') && canManage && (
                  <Button
                    variant="outline"
                    onClick={() => setLifecycleAction('submit')}
                  >
                    Submit to workflow
                  </Button>
                )}
                {item && actions.includes('Publish') && canApprove && (
                  <Button onClick={() => setLifecycleAction('publish')}>
                    Publish approved version
                  </Button>
                )}
                {item && actions.includes('Reject') && canReview && (
                  <Button
                    variant="destructive"
                    onClick={() => setLifecycleAction('reject')}
                  >
                    Reject
                  </Button>
                )}
                {item && actions.includes('Clone') && canManage && (
                  <Button variant="outline" onClick={() => setCloneOpen(true)}>
                    <Copy className="mr-2 h-4 w-4" /> Clone replacement
                  </Button>
                )}
                {item && actions.includes('Retire') && canApprove && (
                  <Button
                    variant="outline"
                    onClick={() => setLifecycleAction('retire')}
                  >
                    Retire
                  </Button>
                )}
                {item && actions.includes('Delete') && canManage && (
                  <Button
                    variant="ghost"
                    onClick={() => setLifecycleAction('delete')}
                  >
                    Delete Draft
                  </Button>
                )}
              </div>

              {item && (
                <div className="rounded-md border bg-muted/20 p-3 text-xs text-muted-foreground">
                  Integrity: {item.integrityHash} · Workflow instance:{' '}
                  {item.workflowInstanceId ?? 'not started'} · Retained audit
                  events: {audit.data?.totalCount ?? 0}
                </div>
              )}
            </div>
          )}
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(lifecycleAction)}
        onOpenChange={(open) => !open && setLifecycleAction(undefined)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {lifecycleAction
                ? `${lifecycleAction[0].toUpperCase()}${lifecycleAction.slice(1)} evidence pack`
                : 'Evidence-pack action'}
            </DialogTitle>
            <DialogDescription>
              This action is recorded in the shared immutable procurement
              control-event ledger.
            </DialogDescription>
          </DialogHeader>
          <Field label="Shared evidence reference">
            <Input
              value={evidenceReference}
              onChange={(event) => setEvidenceReference(event.target.value)}
              placeholder="Approval minute, workflow evidence, or controlled reference"
            />
          </Field>
          <Field label="Comment">
            <Textarea
              value={comment}
              onChange={(event) => setComment(event.target.value)}
            />
          </Field>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setLifecycleAction(undefined)}
            >
              Cancel
            </Button>
            <Button
              variant={
                lifecycleAction === 'reject' || lifecycleAction === 'delete'
                  ? 'destructive'
                  : 'default'
              }
              disabled={Boolean(busy)}
              onClick={() => void lifecycle()}
            >
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Confirm
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={cloneOpen} onOpenChange={setCloneOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Clone replacement version</DialogTitle>
            <DialogDescription>
              The current Published version stays effective until the
              replacement&apos;s effective date.
            </DialogDescription>
          </DialogHeader>
          <Field label="Effective from">
            <Input
              type="datetime-local"
              value={cloneFrom}
              onChange={(event) => setCloneFrom(event.target.value)}
            />
          </Field>
          <Field label="Effective to (optional)">
            <Input
              type="datetime-local"
              value={cloneTo}
              onChange={(event) => setCloneTo(event.target.value)}
            />
          </Field>
          <Field label="Change summary">
            <Textarea
              value={cloneSummary}
              onChange={(event) => setCloneSummary(event.target.value)}
            />
          </Field>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCloneOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => void clone()}>Create Draft version</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="space-y-1.5">
      <Label>{label}</Label>
      {children}
    </div>
  );
}
