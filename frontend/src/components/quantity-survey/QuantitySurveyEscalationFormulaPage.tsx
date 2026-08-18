'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Archive,
  CheckCircle2,
  Edit3,
  FileClock,
  History,
  Plus,
  RefreshCw,
  Send,
  XCircle,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable';
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
import { ScrollArea } from '@/components/ui/scroll-area';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { QuantitySurveyPriceIndexImportPanel } from '@/components/quantity-survey/QuantitySurveyPriceIndexImportPanel';
import { QuantitySurveyEscalationCalculationPanel } from '@/components/quantity-survey/QuantitySurveyEscalationCalculationPanel';
import { QuantitySurveyEscalationDisputePanel } from '@/components/quantity-survey/QuantitySurveyEscalationDisputePanel';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import {
  quantitySurveyEscalationService,
  type QuantitySurveyEscalationFormula,
  type QuantitySurveyEscalationLookupOption,
  type QuantitySurveyIndexFamily,
  type SaveQuantitySurveyEscalationFormula,
  type SaveQuantitySurveyIndexFamily,
} from '@/services/quantity-survey-escalation.service';

const COMPONENTS = ['Material', 'Labour', 'Plant', 'Other'] as const;
type ComponentName = (typeof COMPONENTS)[number];
type LifecycleAction = 'submit' | 'approve' | 'reject' | 'retire';

const today = () => new Date().toISOString().slice(0, 10);
const dateValue = (value?: string | null) => value?.slice(0, 10) ?? '';
const newRequestId = () => crypto.randomUUID();
const statusVariant = (status: string) =>
  status === 'Approved'
    ? 'secondary'
    : status === 'Rejected' || status === 'Retired'
      ? 'outline'
      : 'default';
const formatDate = (value?: string | null) =>
  value
    ? new Intl.DateTimeFormat('en-GB', {
        day: '2-digit',
        month: 'short',
        year: 'numeric',
      }).format(new Date(value))
    : 'Open-ended';

const emptyFormula = (): SaveQuantitySurveyEscalationFormula => ({
  clientRequestId: newRequestId(),
  code: '',
  name: '',
  projectId: '',
  contractId: '',
  contractClauseReference: '',
  formulaType: '',
  baseDate: today(),
  effectiveFrom: today(),
  effectiveTo: null,
  authorityRoleId: '',
  centralDocumentVersionId: '',
  sourceFormulaId: null,
  components: COMPONENTS.map((component) => ({
    component,
    coefficient: 0,
    indexFamilyId: '',
  })),
  reason: '',
});

const emptyFamily = (): SaveQuantitySurveyIndexFamily => ({
  code: '',
  name: '',
  source: '',
  publisher: '',
  description: '',
  isActive: true,
  reason: '',
});

function OptionsSelect({
  value,
  options,
  placeholder,
  onChange,
  disabled,
}: {
  value: string;
  options: QuantitySurveyEscalationLookupOption[];
  placeholder: string;
  onChange: (value: string) => void;
  disabled?: boolean;
}) {
  return (
    <Select
      value={value || undefined}
      onValueChange={onChange}
      disabled={disabled}
    >
      <SelectTrigger>
        <SelectValue placeholder={placeholder} />
      </SelectTrigger>
      <SelectContent>
        {options.map((option) => (
          <SelectItem
            key={`${option.group ?? 'all'}-${option.value}`}
            value={option.value}
          >
            {option.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

export default function QuantitySurveyEscalationFormulaPage() {
  const client = useQueryClient();
  const { toast } = useToast();
  const { hasPermission } = useAuth();
  const canRead = hasPermission('quantity-survey.workspace.read');
  const canManage = hasPermission('quantity-survey.rates.manage');
  const canApprove = hasPermission('quantity-survey.transactions.approve');
  const canAudit = hasPermission('quantity-survey.audit.read');

  const [search, setSearch] = useState('');
  const [projectId, setProjectId] = useState('all');
  const [status, setStatus] = useState('all');
  const [familySearch, setFamilySearch] = useState('');
  const [includeInactive, setIncludeInactive] = useState(true);
  const [formulaOpen, setFormulaOpen] = useState(false);
  const [editingFormula, setEditingFormula] =
    useState<QuantitySurveyEscalationFormula | null>(null);
  const [formulaForm, setFormulaForm] =
    useState<SaveQuantitySurveyEscalationFormula>(emptyFormula);
  const [familyOpen, setFamilyOpen] = useState(false);
  const [editingFamily, setEditingFamily] =
    useState<QuantitySurveyIndexFamily | null>(null);
  const [familyForm, setFamilyForm] =
    useState<SaveQuantitySurveyIndexFamily>(emptyFamily);
  const [lifecycle, setLifecycle] = useState<{
    formula: QuantitySurveyEscalationFormula;
    action: LifecycleAction;
  } | null>(null);
  const [lifecycleReason, setLifecycleReason] = useState('');
  const [detail, setDetail] = useState<QuantitySurveyEscalationFormula | null>(
    null
  );
  const [historyId, setHistoryId] = useState<string | null>(null);

  const lookups = useQuery({
    queryKey: ['quantity-survey-escalation-lookups'],
    queryFn: quantitySurveyEscalationService.lookups,
    enabled: canRead,
  });
  const formulas = useQuery({
    queryKey: [
      'quantity-survey-escalation-formulas',
      search,
      projectId,
      status,
    ],
    queryFn: () =>
      quantitySurveyEscalationService.list({
        search: search.trim() || undefined,
        projectId: projectId === 'all' ? undefined : projectId,
        status: status === 'all' ? undefined : status,
        page: 1,
        pageSize: 250,
      }),
    enabled: canRead,
  });
  const families = useQuery({
    queryKey: ['quantity-survey-index-families', familySearch, includeInactive],
    queryFn: () =>
      quantitySurveyEscalationService.indexFamilies(
        familySearch,
        includeInactive
      ),
    enabled: canRead,
  });
  const history = useQuery({
    queryKey: ['quantity-survey-escalation-history', historyId],
    queryFn: () => {
      if (!historyId) throw new Error('Select a formula first.');
      return quantitySurveyEscalationService.history(historyId);
    },
    enabled: Boolean(canRead && historyId && canAudit),
  });

  const options = (key: string) => lookups.data?.sources?.[key] ?? [];
  const contracts = options('contracts').filter(
    (option) => option.group === formulaForm.projectId
  );
  const evidence = options('evidenceDocuments').filter(
    (option) => option.group === formulaForm.contractId
  );
  const coefficient = (component: ComponentName) => {
    const policy = lookups.data?.policy;
    if (!policy) return 0;
    return policy[
      `${component.toLowerCase()}Coefficient` as keyof typeof policy
    ] as number;
  };
  const invalidate = async () => {
    await Promise.all([
      client.invalidateQueries({
        queryKey: ['quantity-survey-escalation-lookups'],
      }),
      client.invalidateQueries({
        queryKey: ['quantity-survey-escalation-formulas'],
      }),
      client.invalidateQueries({
        queryKey: ['quantity-survey-index-families'],
      }),
      client.invalidateQueries({
        queryKey: ['quantity-survey-escalation-history'],
      }),
    ]);
  };

  const saveFormula = useMutation({
    mutationFn: () =>
      editingFormula
        ? quantitySurveyEscalationService.update(editingFormula.id, {
            ...formulaForm,
            rowVersion: editingFormula.rowVersion,
          })
        : quantitySurveyEscalationService.create(formulaForm),
    onSuccess: async (value) => {
      setFormulaOpen(false);
      setEditingFormula(null);
      setDetail(value);
      await invalidate();
      toast({ title: 'Escalation formula saved', variant: 'success' });
    },
    onError: (error: Error) =>
      toast({
        title: 'Unable to save escalation formula',
        description: error.message,
        variant: 'destructive',
      }),
  });
  const saveFamily = useMutation({
    mutationFn: () =>
      editingFamily
        ? quantitySurveyEscalationService.updateIndexFamily(editingFamily.id, {
            ...familyForm,
            rowVersion: editingFamily.rowVersion,
          })
        : quantitySurveyEscalationService.createIndexFamily(familyForm),
    onSuccess: async () => {
      setFamilyOpen(false);
      setEditingFamily(null);
      await invalidate();
      toast({ title: 'Index family saved', variant: 'success' });
    },
    onError: (error: Error) =>
      toast({
        title: 'Unable to save index family',
        description: error.message,
        variant: 'destructive',
      }),
  });
  const runLifecycle = useMutation({
    mutationFn: () => {
      if (!lifecycle) throw new Error('Select a lifecycle action.');
      return quantitySurveyEscalationService.lifecycle(
        lifecycle.formula.id,
        lifecycle.action,
        lifecycle.formula.rowVersion,
        lifecycleReason
      );
    },
    onSuccess: async (value) => {
      setLifecycle(null);
      setLifecycleReason('');
      setDetail(value);
      await invalidate();
      toast({ title: 'Formula lifecycle updated', variant: 'success' });
    },
    onError: (error: Error) =>
      toast({
        title: 'Unable to update formula lifecycle',
        description: error.message,
        variant: 'destructive',
      }),
  });

  const beginCreateFormula = () => {
    const policy = lookups.data?.policy;
    const form = emptyFormula();
    if (policy) {
      form.formulaType = policy.formulaType;
      form.components = COMPONENTS.map((component) => ({
        component,
        coefficient: coefficient(component),
        indexFamilyId: '',
      }));
    }
    setEditingFormula(null);
    setFormulaForm(form);
    setFormulaOpen(true);
  };
  const beginEditFormula = (
    value: QuantitySurveyEscalationFormula,
    asRevision = false
  ) => {
    setEditingFormula(asRevision ? null : value);
    setFormulaForm({
      clientRequestId: asRevision ? newRequestId() : value.id,
      code: value.code,
      name: value.name,
      projectId: value.projectId,
      contractId: value.contractId,
      contractClauseReference: value.contractClauseReference,
      formulaType: String(value.formulaType),
      baseDate: dateValue(value.baseDate),
      effectiveFrom: dateValue(value.effectiveFrom),
      effectiveTo: dateValue(value.effectiveTo) || null,
      authorityRoleId: value.authorityRoleId,
      centralDocumentVersionId: value.centralDocumentVersionId,
      sourceFormulaId: asRevision ? value.id : value.supersedesFormulaId,
      components: value.components.map((item) => ({
        component: String(item.component),
        coefficient: item.coefficient,
        indexFamilyId: item.indexFamilyId,
      })),
      reason: '',
    });
    setFormulaOpen(true);
  };
  const beginCreateFamily = () => {
    setEditingFamily(null);
    setFamilyForm(emptyFamily());
    setFamilyOpen(true);
  };
  const beginEditFamily = (value: QuantitySurveyIndexFamily) => {
    setEditingFamily(value);
    setFamilyForm({
      code: value.code,
      name: value.name,
      source: String(value.source),
      publisher: value.publisher,
      description: value.description ?? '',
      isActive: value.isActive,
      reason: '',
      rowVersion: value.rowVersion,
    });
    setFamilyOpen(true);
  };

  const formulaColumns = useMemo<
    Array<DataTableColumn<QuantitySurveyEscalationFormula>>
  >(
    () => [
      {
        id: 'code',
        header: 'Formula',
        cell: ({ row }) => (
          <button
            type="button"
            className="text-left font-medium text-primary hover:underline"
            onClick={() => setDetail(row.original)}
          >
            {row.original.code}/v{row.original.version}
            <span className="block font-normal text-muted-foreground">
              {row.original.name}
            </span>
          </button>
        ),
      },
      {
        id: 'project',
        header: 'Project / contract',
        cell: ({ row }) => (
          <span>
            {row.original.projectCode} · {row.original.projectName}
            <span className="block text-muted-foreground">
              {row.original.contractNumber}
            </span>
          </span>
        ),
      },
      {
        id: 'period',
        header: 'Effective period',
        cell: ({ row }) =>
          `${formatDate(row.original.effectiveFrom)} — ${formatDate(row.original.effectiveTo)}`,
      },
      {
        id: 'authority',
        header: 'Authority',
        accessorKey: 'authorityRoleName',
      },
      {
        id: 'status',
        header: 'Status',
        cell: ({ row }) => (
          <Badge variant={statusVariant(row.original.status)}>
            {row.original.status}
          </Badge>
        ),
      },
    ],
    []
  );
  const familyColumns = useMemo<
    Array<DataTableColumn<QuantitySurveyIndexFamily>>
  >(
    () => [
      { id: 'code', header: 'Code', accessorKey: 'code' },
      { id: 'name', header: 'Index family', accessorKey: 'name' },
      { id: 'source', header: 'Controlled source', accessorKey: 'source' },
      { id: 'publisher', header: 'Publisher', accessorKey: 'publisher' },
      {
        id: 'status',
        header: 'Status',
        cell: ({ row }) => (
          <Badge variant={row.original.isActive ? 'secondary' : 'outline'}>
            {row.original.isActive ? 'Active' : 'Inactive'}
          </Badge>
        ),
      },
    ],
    []
  );

  const formulaValid = Boolean(
    formulaForm.code.trim() &&
    formulaForm.name.trim() &&
    formulaForm.projectId &&
    formulaForm.contractId &&
    formulaForm.contractClauseReference.trim() &&
    formulaForm.baseDate &&
    formulaForm.effectiveFrom &&
    formulaForm.authorityRoleId &&
    formulaForm.centralDocumentVersionId &&
    formulaForm.reason.trim() &&
    formulaForm.components.every((item) => item.indexFamilyId) &&
    formulaForm.components.reduce((sum, item) => sum + item.coefficient, 0) ===
      100
  );
  const familyValid = Boolean(
    familyForm.code.trim() &&
    familyForm.name.trim() &&
    familyForm.source &&
    familyForm.publisher.trim() &&
    familyForm.reason.trim()
  );

  if (!canRead) {
    return (
      <Card className="border-destructive">
        <CardHeader>
          <CardTitle>Price adjustment formulas</CardTitle>
        </CardHeader>
        <CardContent className="text-sm text-destructive">
          You do not have permission to view the Quantity Survey formula
          register.
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-col justify-between gap-3 md:flex-row md:items-center">
        <div>
          <h1 className="text-2xl font-bold">Price adjustment formulas</h1>
          <p className="text-sm text-muted-foreground">
            Governed contract formulas using approved QS policy, central DMS
            evidence and shared workflow.
          </p>
        </div>
        <Button variant="outline" onClick={() => invalidate()}>
          <RefreshCw className="mr-2 h-4 w-4" /> Refresh
        </Button>
      </div>

      {lookups.error ? (
        <Card className="border-destructive">
          <CardContent className="pt-6 text-sm text-destructive">
            {lookups.error instanceof Error
              ? lookups.error.message
              : 'The effective QS escalation policy is unavailable.'}
          </CardContent>
        </Card>
      ) : null}

      <Tabs defaultValue="formulas">
        <TabsList>
          <TabsTrigger value="formulas">Formula register</TabsTrigger>
          <TabsTrigger value="families">Index families</TabsTrigger>
          <TabsTrigger value="imports">Index imports</TabsTrigger>
          <TabsTrigger value="calculations">Calculation runs</TabsTrigger>
          <TabsTrigger value="disputes">Disputes &amp; audit packs</TabsTrigger>
        </TabsList>
        <TabsContent value="formulas" className="space-y-3">
          <div className="grid gap-3 rounded-lg border bg-card p-3 md:grid-cols-[minmax(220px,2fr)_minmax(200px,1fr)_180px_auto] md:items-end">
            <div className="space-y-1">
              <Label>Search</Label>
              <Input
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Formula, name or clause…"
              />
            </div>
            <div className="space-y-1">
              <Label>Project</Label>
              <Select value={projectId} onValueChange={setProjectId}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All permitted projects</SelectItem>
                  {options('projects').map((item) => (
                    <SelectItem key={item.value} value={item.value}>
                      {item.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label>Status</Label>
              <Select value={status} onValueChange={setStatus}>
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
                    'Retired',
                  ].map((item) => (
                    <SelectItem key={item} value={item}>
                      {item}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {canManage ? (
              <Button onClick={beginCreateFormula} disabled={!lookups.data}>
                <Plus className="mr-2 h-4 w-4" /> Add formula
              </Button>
            ) : (
              <span />
            )}
          </div>
          <DataTable
            compact
            title="Formula versions"
            description={`${formulas.data?.totalCount ?? 0} governed version(s)`}
            data={formulas.data?.items ?? []}
            columns={formulaColumns}
            loading={formulas.isLoading}
            error={
              formulas.error ? 'Failed to load escalation formulas.' : null
            }
            enableExport
            exportFormats={['csv', 'excel']}
            exportFileName="quantity-survey-escalation-formulas"
            pageSize={20}
            emptyStateMessage="No formula versions match the selected filters."
            rowActions={[
              {
                id: 'open',
                label: 'View formula',
                icon: FileClock,
                onClick: (row) => setDetail(row.original),
              },
              ...(canManage
                ? [
                    {
                      id: 'edit',
                      label: 'Edit draft',
                      icon: Edit3,
                      hidden: (row: {
                        original: QuantitySurveyEscalationFormula;
                      }) =>
                        !['Draft', 'Rejected'].includes(row.original.status),
                      onClick: (row: {
                        original: QuantitySurveyEscalationFormula;
                      }) => beginEditFormula(row.original),
                    },
                  ]
                : []),
              ...(canManage
                ? [
                    {
                      id: 'revise',
                      label: 'Create revision',
                      icon: Plus,
                      hidden: (row: {
                        original: QuantitySurveyEscalationFormula;
                      }) => row.original.status !== 'Approved',
                      onClick: (row: {
                        original: QuantitySurveyEscalationFormula;
                      }) => beginEditFormula(row.original, true),
                    },
                  ]
                : []),
              ...(canManage
                ? [
                    {
                      id: 'submit',
                      label: 'Submit',
                      icon: Send,
                      hidden: (row: {
                        original: QuantitySurveyEscalationFormula;
                      }) =>
                        !['Draft', 'Rejected'].includes(row.original.status),
                      onClick: (row: {
                        original: QuantitySurveyEscalationFormula;
                      }) => {
                        setLifecycle({
                          formula: row.original,
                          action: 'submit',
                        });
                        setLifecycleReason('');
                      },
                    },
                  ]
                : []),
              ...(canApprove
                ? [
                    {
                      id: 'approve',
                      label: 'Approve',
                      icon: CheckCircle2,
                      hidden: (row: {
                        original: QuantitySurveyEscalationFormula;
                      }) => row.original.status !== 'PendingApproval',
                      onClick: (row: {
                        original: QuantitySurveyEscalationFormula;
                      }) => {
                        setLifecycle({
                          formula: row.original,
                          action: 'approve',
                        });
                        setLifecycleReason('');
                      },
                    },
                  ]
                : []),
              ...(canApprove
                ? [
                    {
                      id: 'reject',
                      label: 'Reject',
                      icon: XCircle,
                      hidden: (row: {
                        original: QuantitySurveyEscalationFormula;
                      }) => row.original.status !== 'PendingApproval',
                      onClick: (row: {
                        original: QuantitySurveyEscalationFormula;
                      }) => {
                        setLifecycle({
                          formula: row.original,
                          action: 'reject',
                        });
                        setLifecycleReason('');
                      },
                    },
                  ]
                : []),
              ...(canApprove
                ? [
                    {
                      id: 'retire',
                      label: 'Retire',
                      icon: Archive,
                      hidden: (row: {
                        original: QuantitySurveyEscalationFormula;
                      }) => row.original.status !== 'Approved',
                      onClick: (row: {
                        original: QuantitySurveyEscalationFormula;
                      }) => {
                        setLifecycle({
                          formula: row.original,
                          action: 'retire',
                        });
                        setLifecycleReason('');
                      },
                    },
                  ]
                : []),
              ...(canAudit
                ? [
                    {
                      id: 'history',
                      label: 'Audit history',
                      icon: History,
                      onClick: (row: {
                        original: QuantitySurveyEscalationFormula;
                      }) => setHistoryId(row.original.id),
                    },
                  ]
                : []),
            ]}
          />
        </TabsContent>
        <TabsContent value="families" className="space-y-3">
          <div className="flex flex-col gap-3 rounded-lg border bg-card p-3 md:flex-row md:items-end">
            <div className="min-w-0 flex-1 space-y-1">
              <Label>Search index families</Label>
              <Input
                value={familySearch}
                onChange={(event) => setFamilySearch(event.target.value)}
                placeholder="Code, name or publisher…"
              />
            </div>
            <div className="flex h-10 items-center gap-2 rounded-md border px-3">
              <Switch
                checked={includeInactive}
                onCheckedChange={setIncludeInactive}
              />
              <Label>Include inactive</Label>
            </div>
            {canManage ? (
              <Button onClick={beginCreateFamily} disabled={!lookups.data}>
                <Plus className="mr-2 h-4 w-4" /> Add index family
              </Button>
            ) : null}
          </div>
          <DataTable
            compact
            title="Controlled index families"
            description={`${families.data?.length ?? 0} family record(s)`}
            data={families.data ?? []}
            columns={familyColumns}
            loading={families.isLoading}
            error={families.error ? 'Failed to load index families.' : null}
            enableExport
            exportFormats={['csv', 'excel']}
            exportFileName="quantity-survey-index-families"
            pageSize={20}
            emptyStateMessage="No controlled index families match the selected filters."
            rowActions={
              canManage
                ? [
                    {
                      id: 'edit',
                      label: 'Edit index family',
                      icon: Edit3,
                      onClick: (row) => beginEditFamily(row.original),
                    },
                  ]
                : []
            }
          />
        </TabsContent>
        <TabsContent value="imports" className="space-y-3">
          <QuantitySurveyPriceIndexImportPanel
            families={families.data ?? []}
            allowedIndexSources={options('indexSources')}
            authorityRoles={options('authorityRoles')}
            importFormat={
              lookups.isLoading
                ? undefined
                : (lookups.data?.policy.importFormat ?? null)
            }
            canManage={canManage}
            canApprove={canApprove}
            canAudit={canAudit}
          />
        </TabsContent>
        <TabsContent value="calculations" className="space-y-3">
          <QuantitySurveyEscalationCalculationPanel
            canManage={canManage}
            canApprove={canApprove}
            canAudit={canAudit}
          />
        </TabsContent>
        <TabsContent value="disputes" className="space-y-3">
          <QuantitySurveyEscalationDisputePanel />
        </TabsContent>
      </Tabs>

      <Dialog open={formulaOpen} onOpenChange={setFormulaOpen}>
        <DialogContent className="max-h-[92vh] max-w-5xl">
          <DialogHeader>
            <DialogTitle>
              {editingFormula
                ? 'Edit escalation formula'
                : formulaForm.sourceFormulaId
                  ? 'Create formula revision'
                  : 'Add escalation formula'}
            </DialogTitle>
            <DialogDescription>
              All master references are controlled. Coefficients come from the
              effective QS-DEC-006 policy and cannot be overridden here.
            </DialogDescription>
          </DialogHeader>
          <ScrollArea className="max-h-[68vh] pr-4">
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-1">
                <Label>Formula code</Label>
                <Input
                  value={formulaForm.code}
                  onChange={(event) =>
                    setFormulaForm((value) => ({
                      ...value,
                      code: event.target.value.toUpperCase(),
                    }))
                  }
                  placeholder="PA-FORMULA-01"
                />
              </div>
              <div className="space-y-1">
                <Label>Formula name</Label>
                <Input
                  value={formulaForm.name}
                  onChange={(event) =>
                    setFormulaForm((value) => ({
                      ...value,
                      name: event.target.value,
                    }))
                  }
                />
              </div>
              <div className="space-y-1">
                <Label>Project</Label>
                <OptionsSelect
                  value={formulaForm.projectId}
                  options={options('projects')}
                  placeholder="Select permitted project"
                  onChange={(value) =>
                    setFormulaForm((current) => ({
                      ...current,
                      projectId: value,
                      contractId: '',
                      centralDocumentVersionId: '',
                    }))
                  }
                />
              </div>
              <div className="space-y-1">
                <Label>Works contract</Label>
                <OptionsSelect
                  value={formulaForm.contractId}
                  options={contracts}
                  placeholder={
                    formulaForm.projectId
                      ? 'Select linked Works contract'
                      : 'Select a project first'
                  }
                  disabled={!formulaForm.projectId}
                  onChange={(value) =>
                    setFormulaForm((current) => ({
                      ...current,
                      contractId: value,
                      centralDocumentVersionId: '',
                    }))
                  }
                />
              </div>
              <div className="space-y-1">
                <Label>Contract clause reference</Label>
                <Input
                  value={formulaForm.contractClauseReference}
                  onChange={(event) =>
                    setFormulaForm((value) => ({
                      ...value,
                      contractClauseReference: event.target.value,
                    }))
                  }
                  placeholder="GCC 47.1"
                />
              </div>
              <div className="space-y-1">
                <Label>Formula type</Label>
                <OptionsSelect
                  value={formulaForm.formulaType}
                  options={options('formulaTypes')}
                  placeholder="Policy formula"
                  disabled
                  onChange={() => undefined}
                />
              </div>
              <div className="space-y-1">
                <Label>Base date</Label>
                <Input
                  type="date"
                  value={formulaForm.baseDate}
                  onChange={(event) =>
                    setFormulaForm((value) => ({
                      ...value,
                      baseDate: event.target.value,
                    }))
                  }
                />
              </div>
              <div className="space-y-1">
                <Label>Effective from</Label>
                <Input
                  type="date"
                  value={formulaForm.effectiveFrom}
                  onChange={(event) =>
                    setFormulaForm((value) => ({
                      ...value,
                      effectiveFrom: event.target.value,
                    }))
                  }
                />
              </div>
              <div className="space-y-1">
                <Label>Effective to</Label>
                <Input
                  type="date"
                  value={formulaForm.effectiveTo ?? ''}
                  onChange={(event) =>
                    setFormulaForm((value) => ({
                      ...value,
                      effectiveTo: event.target.value || null,
                    }))
                  }
                />
              </div>
              <div className="space-y-1">
                <Label>Approval authority</Label>
                <OptionsSelect
                  value={formulaForm.authorityRoleId}
                  options={options('authorityRoles')}
                  placeholder="Select QS-DEC-001 authority"
                  onChange={(value) =>
                    setFormulaForm((current) => ({
                      ...current,
                      authorityRoleId: value,
                    }))
                  }
                />
              </div>
              <div className="space-y-1 md:col-span-2">
                <Label>Published central-DMS contract evidence</Label>
                <OptionsSelect
                  value={formulaForm.centralDocumentVersionId}
                  options={evidence}
                  placeholder={
                    formulaForm.contractId
                      ? 'Select evidence linked to this contract'
                      : 'Select a contract first'
                  }
                  disabled={!formulaForm.contractId}
                  onChange={(value) =>
                    setFormulaForm((current) => ({
                      ...current,
                      centralDocumentVersionId: value,
                    }))
                  }
                />
              </div>
              <Card className="md:col-span-2">
                <CardHeader className="pb-2">
                  <CardTitle className="text-base">
                    Policy coefficients and index families
                  </CardTitle>
                </CardHeader>
                <CardContent className="grid gap-3 md:grid-cols-2">
                  {COMPONENTS.map((component, index) => {
                    const item = formulaForm.components[index];
                    return (
                      <div
                        key={component}
                        className="grid grid-cols-[100px_1fr] gap-2 rounded-md border p-2"
                      >
                        <div>
                          <Label>{component}</Label>
                          <Input
                            value={`${item?.coefficient ?? coefficient(component)}%`}
                            disabled
                          />
                        </div>
                        <div>
                          <Label>Controlled index family</Label>
                          <OptionsSelect
                            value={item?.indexFamilyId ?? ''}
                            options={options('indexFamilies')}
                            placeholder={`Select ${component.toLowerCase()} index`}
                            onChange={(value) =>
                              setFormulaForm((current) => ({
                                ...current,
                                components: current.components.map(
                                  (part, partIndex) =>
                                    partIndex === index
                                      ? { ...part, indexFamilyId: value }
                                      : part
                                ),
                              }))
                            }
                          />
                        </div>
                      </div>
                    );
                  })}
                  <div className="md:col-span-2 text-right text-sm font-medium">
                    Total:{' '}
                    {formulaForm.components.reduce(
                      (sum, item) => sum + item.coefficient,
                      0
                    )}
                    %
                  </div>
                </CardContent>
              </Card>
              <div className="space-y-1 md:col-span-2">
                <Label>Change reason</Label>
                <Textarea
                  value={formulaForm.reason}
                  onChange={(event) =>
                    setFormulaForm((value) => ({
                      ...value,
                      reason: event.target.value,
                    }))
                  }
                  placeholder="Explain the controlled formula registration or revision."
                />
              </div>
            </div>
          </ScrollArea>
          <DialogFooter>
            <Button variant="outline" onClick={() => setFormulaOpen(false)}>
              Cancel
            </Button>
            <Button
              disabled={!formulaValid || saveFormula.isPending}
              onClick={() => saveFormula.mutate()}
            >
              {saveFormula.isPending ? 'Saving…' : 'Save draft'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={familyOpen} onOpenChange={setFamilyOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {editingFamily ? 'Edit index family' : 'Add index family'}
            </DialogTitle>
            <DialogDescription>
              Create the controlled family master used by formula component
              selectors.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-3 md:grid-cols-2">
            <div className="space-y-1">
              <Label>Code</Label>
              <Input
                value={familyForm.code}
                onChange={(event) =>
                  setFamilyForm((value) => ({
                    ...value,
                    code: event.target.value.toUpperCase(),
                  }))
                }
                placeholder="GSS-PBCI-MAT"
              />
            </div>
            <div className="space-y-1">
              <Label>Name</Label>
              <Input
                value={familyForm.name}
                onChange={(event) =>
                  setFamilyForm((value) => ({
                    ...value,
                    name: event.target.value,
                  }))
                }
              />
            </div>
            <div className="space-y-1">
              <Label>Policy-allowed source</Label>
              <OptionsSelect
                value={familyForm.source}
                options={options('indexSources')}
                placeholder="Select index source"
                onChange={(value) =>
                  setFamilyForm((current) => ({ ...current, source: value }))
                }
              />
            </div>
            <div className="space-y-1">
              <Label>Publisher</Label>
              <Input
                value={familyForm.publisher}
                onChange={(event) =>
                  setFamilyForm((value) => ({
                    ...value,
                    publisher: event.target.value,
                  }))
                }
                placeholder="Ghana Statistical Service"
              />
            </div>
            <div className="space-y-1 md:col-span-2">
              <Label>Description</Label>
              <Textarea
                value={familyForm.description ?? ''}
                onChange={(event) =>
                  setFamilyForm((value) => ({
                    ...value,
                    description: event.target.value,
                  }))
                }
              />
            </div>
            <div className="space-y-1 md:col-span-2">
              <Label>Change reason</Label>
              <Textarea
                value={familyForm.reason}
                onChange={(event) =>
                  setFamilyForm((value) => ({
                    ...value,
                    reason: event.target.value,
                  }))
                }
                placeholder="Explain the controlled index-family change."
              />
            </div>
            <div className="flex items-center gap-2">
              <Switch
                checked={familyForm.isActive}
                onCheckedChange={(value) =>
                  setFamilyForm((current) => ({ ...current, isActive: value }))
                }
              />
              <Label>Active</Label>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setFamilyOpen(false)}>
              Cancel
            </Button>
            <Button
              disabled={!familyValid || saveFamily.isPending}
              onClick={() => saveFamily.mutate()}
            >
              {saveFamily.isPending ? 'Saving…' : 'Save index family'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(lifecycle)}
        onOpenChange={(open) => !open && setLifecycle(null)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{lifecycle?.action} formula</DialogTitle>
            <DialogDescription>
              This action uses the configured QS_ESCALATION workflow, authority
              and maker-checker rules.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-1">
            <Label>Reason</Label>
            <Textarea
              value={lifecycleReason}
              onChange={(event) => setLifecycleReason(event.target.value)}
              placeholder={`Reason to ${lifecycle?.action ?? 'update'} this formula`}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setLifecycle(null)}>
              Cancel
            </Button>
            <Button
              disabled={!lifecycleReason.trim() || runLifecycle.isPending}
              onClick={() => runLifecycle.mutate()}
            >
              {runLifecycle.isPending ? 'Processing…' : 'Confirm'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(detail)}
        onOpenChange={(open) => !open && setDetail(null)}
      >
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>
              {detail?.code}/v{detail?.version} · {detail?.name}
            </DialogTitle>
            <DialogDescription>
              {detail?.projectCode} · {detail?.contractNumber} · clause{' '}
              {detail?.contractClauseReference}
            </DialogDescription>
          </DialogHeader>
          {detail ? (
            <div className="space-y-3 text-sm">
              <div className="grid gap-3 md:grid-cols-3">
                <div>
                  <span className="text-muted-foreground">Status</span>
                  <div>
                    <Badge variant={statusVariant(detail.status)}>
                      {detail.status}
                    </Badge>
                  </div>
                </div>
                <div>
                  <span className="text-muted-foreground">Base date</span>
                  <div>{formatDate(detail.baseDate)}</div>
                </div>
                <div>
                  <span className="text-muted-foreground">Authority</span>
                  <div>{detail.authorityRoleName}</div>
                </div>
              </div>
              <div>
                <span className="text-muted-foreground">DMS evidence</span>
                <div>{detail.evidenceLabel}</div>
              </div>
              <div className="grid gap-2 md:grid-cols-2">
                {detail.components.map((item) => (
                  <div key={item.id} className="rounded-md border p-2">
                    <strong>{String(item.component)}</strong> ·{' '}
                    {item.coefficient}%
                    <div className="text-muted-foreground">
                      {item.indexFamilyCode} · {item.indexFamilyName}
                    </div>
                  </div>
                ))}
              </div>
              <div className="break-all text-xs text-muted-foreground">
                Integrity: {detail.snapshotHash}
              </div>
            </div>
          ) : null}
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(historyId)}
        onOpenChange={(open) => !open && setHistoryId(null)}
      >
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>Formula audit history</DialogTitle>
            <DialogDescription>
              Immutable lifecycle and change revisions for this formula version.
            </DialogDescription>
          </DialogHeader>
          <ScrollArea className="max-h-[60vh] pr-3">
            <div className="space-y-2">
              {history.isLoading ? (
                <p>Loading history…</p>
              ) : (
                (history.data ?? []).map((item) => (
                  <div key={item.id} className="rounded-md border p-3 text-sm">
                    <div className="flex justify-between gap-3">
                      <strong>{item.action}</strong>
                      <span>{formatDate(item.createdAt)}</span>
                    </div>
                    <div>{item.actorName}</div>
                    <div className="text-muted-foreground">
                      {item.reason || 'No reason recorded'} ·{' '}
                      {item.correlationId}
                    </div>
                  </div>
                ))
              )}
              {!history.isLoading && history.data?.length === 0 ? (
                <p className="text-muted-foreground">
                  No audit revisions are available.
                </p>
              ) : null}
            </div>
          </ScrollArea>
        </DialogContent>
      </Dialog>
    </div>
  );
}
