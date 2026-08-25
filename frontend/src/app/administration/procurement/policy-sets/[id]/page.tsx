'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams, useRouter } from 'next/navigation';
import {
  ArrowLeft,
  CheckCircle2,
  Copy,
  Edit,
  Eye,
  Plus,
  RefreshCw,
  ShieldCheck,
  Trash2,
} from 'lucide-react';

import { ProcurementPolicyRuleEditor } from '@/components/procurement/policy/ProcurementPolicyRuleEditor';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
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
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import {
  procurementPolicyRuleKinds,
  procurementPolicyRuleRegistry,
} from '@/lib/procurement-policy-rule-registry';
import { procurementPolicyService } from '@/services/procurement-policy.service';
import { procurementSodService } from '@/services/procurement-sod.service';
import type {
  ProcurementPolicyRule,
  ProcurementPolicyRuleKind,
  ProcurementPolicyValidationIssue,
  UpdateProcurementPolicySetRequest,
} from '@/types/procurement-policy';

type ConfirmAction =
  'publish' | 'retire' | 'deletePolicy' | 'deleteRule' | null;
type TableCellProps<T> = { row: { original: T } };
const errorMessage = (error: unknown) => {
  const candidate = error as {
    message?: string;
    response?: {
      detail?: string;
      title?: string;
      errors?: Record<string, string[]>;
    };
  };
  const validationMessages = Object.values(candidate.response?.errors ?? {})
    .flat()
    .filter(Boolean);
  return (
    (validationMessages.length
      ? `${validationMessages.slice(0, 3).join(' ')}${validationMessages.length > 3 ? ` (+${validationMessages.length - 3} more; open Validation)` : ''}`
      : undefined) ||
    candidate.response?.detail ||
    candidate.response?.title ||
    candidate.message ||
    'The request failed.'
  );
};
const formatKind = (kind: ProcurementPolicyRuleKind) =>
  procurementPolicyRuleRegistry[kind].label;

export default function ProcurementPolicySetDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { hasRole } = useAuth();
  const { toast } = useToast();
  const isSuperAdmin = hasRole('SuperAdmin');
  const [kindFilter, setKindFilter] = useState<
    ProcurementPolicyRuleKind | 'All'
  >('All');
  const [editorKind, setEditorKind] = useState<ProcurementPolicyRuleKind>();
  const [selectedRule, setSelectedRule] = useState<ProcurementPolicyRule>();
  const [profileEditorOpen, setProfileEditorOpen] = useState(false);
  const [cloneOpen, setCloneOpen] = useState(false);
  const [confirmAction, setConfirmAction] = useState<ConfirmAction>(null);
  const [deleteRuleTarget, setDeleteRuleTarget] =
    useState<ProcurementPolicyRule>();
  const [reason, setReason] = useState('');
  const [changeSummary, setChangeSummary] = useState('');
  const [conflictMessage, setConflictMessage] = useState<string>();
  const [editForm, setEditForm] = useState<UpdateProcurementPolicySetRequest>();
  const [activeTab, setActiveTab] = useState('rules');
  const [sodDialogOpen, setSodDialogOpen] = useState(false);
  const [sodReason, setSodReason] = useState('');

  const policyQuery = useQuery({
    queryKey: ['procurement-policy-set', id],
    queryFn: () => procurementPolicyService.get(id),
    enabled: Boolean(id),
  });
  const policy = policyQuery.data;
  const editable = policy?.lifecycleStatus === 'Draft';
  const visibleRules = useMemo(
    () =>
      policy?.rules.filter(
        (rule) => kindFilter === 'All' || rule.kind === kindFilter
      ) ?? [],
    [policy, kindFilter]
  );
  const refresh = async () => {
    setConflictMessage(undefined);
    setEditorKind(undefined);
    setSelectedRule(undefined);
    await queryClient.invalidateQueries({
      queryKey: ['procurement-policy-set', id],
    });
    await queryClient.invalidateQueries({
      queryKey: ['procurement-policy-sets'],
    });
  };

  const validate = useMutation({
    mutationFn: () => procurementPolicyService.validate(id),
    onSuccess: async (result) => {
      toast({
        title: result.isValid
          ? 'Policy validation passed'
          : 'Policy needs attention',
        description: result.isValid
          ? 'All mandatory rule families, relationships, and governance controls passed.'
          : `${result.errors.length} blocking issue(s) remain.`,
        variant: result.isValid ? 'success' : 'destructive',
      });
      await refresh();
    },
    onError: (error) =>
      toast({
        title: 'Validation failed',
        description: errorMessage(error),
        variant: 'destructive',
      }),
  });

  const applyRequiredSod = useMutation({
    mutationFn: () => procurementSodService.applyRequired(id, sodReason.trim()),
    onSuccess: async (result) => {
      setSodDialogOpen(false);
      setSodReason('');
      toast({
        title: 'Required SOD controls applied',
        description: `${result.createdCount} control(s) created; ${result.existingCount} already existed.`,
        variant: 'success',
      });
      await refresh();
      setActiveTab('validation');
    },
    onError: (error) =>
      toast({
        title: 'Unable to apply SOD controls',
        description: errorMessage(error),
        variant: 'destructive',
      }),
  });

  const updatePolicy = useMutation({
    mutationFn: () => {
      if (!editForm) throw new Error('Policy edit data is unavailable.');
      return procurementPolicyService.update(id, {
        ...editForm,
        effectiveTo: editForm.effectiveTo || undefined,
        reason: editForm.reason?.trim() || undefined,
      });
    },
    onSuccess: async () => {
      setProfileEditorOpen(false);
      toast({
        title: 'Policy updated',
        description: 'Draft metadata and audit history were updated.',
        variant: 'success',
      });
      await refresh();
    },
    onError: (error) => {
      if ((error as { status?: number }).status === 409)
        setConflictMessage(
          'This policy changed elsewhere. Reload it before saving again.'
        );
      toast({
        title: 'Unable to update policy',
        description: errorMessage(error),
        variant: 'destructive',
      });
    },
  });

  const clone = useMutation({
    mutationFn: () =>
      procurementPolicyService.cloneDraft(
        id,
        changeSummary.trim() || undefined
      ),
    onSuccess: async (result) => {
      setCloneOpen(false);
      toast({
        title: 'Next policy draft created',
        description: 'All relational rules and source lineage were cloned.',
        variant: 'success',
      });
      await refresh();
      router.push(`/administration/procurement/policy-sets/${result.id}`);
    },
    onError: (error) =>
      toast({
        title: 'Unable to clone policy',
        description: errorMessage(error),
        variant: 'destructive',
      }),
  });

  const action = useMutation({
    mutationFn: async () => {
      if (!policy || !confirmAction) throw new Error('No action is selected.');
      if (confirmAction === 'deleteRule') {
        if (!deleteRuleTarget) throw new Error('No policy rule is selected.');
        await procurementPolicyService.deleteRule(
          id,
          deleteRuleTarget.kind,
          deleteRuleTarget.id,
          { rowVersion: deleteRuleTarget.rowVersion, reason: reason.trim() }
        );
        return 'rule';
      }
      const request = { rowVersion: policy.rowVersion, reason: reason.trim() };
      if (confirmAction === 'publish') {
        await procurementPolicyService.publish(id, request);
        return 'publish';
      }
      if (confirmAction === 'retire') {
        await procurementPolicyService.retire(id, request);
        return 'retire';
      }
      await procurementPolicyService.deleteDraft(id, request);
      return 'policy';
    },
    onSuccess: async (result) => {
      setConfirmAction(null);
      setDeleteRuleTarget(undefined);
      setReason('');
      toast({
        title:
          result === 'publish'
            ? 'Policy published'
            : result === 'retire'
              ? 'Policy retired'
              : result === 'rule'
                ? 'Rule deleted'
                : 'Draft deleted',
        description: 'The action completed with an immutable audit revision.',
        variant: 'success',
      });
      await refresh();
      if (result === 'policy')
        router.push('/administration/procurement/policy-sets');
    },
    onError: (error) => {
      if ((error as { status?: number }).status === 409)
        setConflictMessage(
          'The policy changed while this action was prepared. Reload and retry.'
        );
      toast({
        title: 'Policy action rejected',
        description: errorMessage(error),
        variant: 'destructive',
      });
      if (confirmAction === 'publish') setActiveTab('validation');
    },
  });

  const columns = useMemo<Array<DataTableColumn<ProcurementPolicyRule>>>(
    () => [
      {
        id: 'rule',
        header: 'Rule',
        accessorKey: 'ruleCode',
        cell: ({ row }: TableCellProps<ProcurementPolicyRule>) => (
          <button
            className="text-left"
            onClick={() => setSelectedRule(row.original)}
          >
            <span className="block font-medium text-primary hover:underline">
              {row.original.name}
            </span>
            <span className="font-mono text-xs text-muted-foreground">
              {row.original.ruleCode}
            </span>
          </button>
        ),
      },
      {
        id: 'kind',
        header: 'Family',
        accessorKey: 'kind',
        cell: ({ row }: TableCellProps<ProcurementPolicyRule>) => (
          <Badge variant="outline">{formatKind(row.original.kind)}</Badge>
        ),
      },
      {
        id: 'source',
        header: 'Lineage',
        cell: ({ row }: TableCellProps<ProcurementPolicyRule>) => (
          <div>
            {row.original.sourceDecisionKey}
            <span className="block text-xs text-muted-foreground">
              {row.original.overrideAction}
              {row.original.sourceRuleId
                ? ` · ${row.original.sourceRuleId.slice(0, 8)}…`
                : ''}
            </span>
          </div>
        ),
      },
      {
        id: 'effective',
        header: 'Effective period',
        cell: ({ row }: TableCellProps<ProcurementPolicyRule>) =>
          `${new Date(row.original.effectiveFrom).toLocaleDateString()} – ${row.original.effectiveTo ? new Date(row.original.effectiveTo).toLocaleDateString() : 'open-ended'}`,
      },
      {
        id: 'state',
        header: 'State',
        cell: ({ row }: TableCellProps<ProcurementPolicyRule>) => (
          <Badge variant={row.original.isEnabled ? 'default' : 'secondary'}>
            {row.original.isEnabled ? 'Enabled' : 'Disabled'}
          </Badge>
        ),
      },
      { id: 'priority', header: 'Priority', accessorKey: 'priority' },
    ],
    []
  );

  if (policyQuery.isLoading)
    return (
      <div className="flex min-h-[240px] items-center justify-center">
        <RefreshCw className="h-6 w-6 animate-spin" />
      </div>
    );
  if (!policy)
    return (
      <Alert variant="destructive">
        <AlertTitle>Policy unavailable</AlertTitle>
        <AlertDescription>
          {policyQuery.error
            ? errorMessage(policyQuery.error)
            : 'The policy was not found for this tenant.'}
        </AlertDescription>
      </Alert>
    );

  const openPolicyEditor = () => {
    setEditForm({
      name: policy.name,
      description: policy.description,
      defaultCurrencyCode: policy.defaultCurrencyCode,
      effectiveFrom: policy.effectiveFrom.slice(0, 10),
      effectiveTo: policy.effectiveTo?.slice(0, 10),
      changeSummary: policy.changeSummary,
      isDefault: policy.isDefault,
      rowVersion: policy.rowVersion,
      reason: '',
    });
    setProfileEditorOpen(true);
  };
  const editorRule = selectedRule;
  const activeEditorKind = editorRule?.kind ?? editorKind;
  const familyCounts = procurementPolicyRuleKinds.map((kind) => ({
    kind,
    count: policy.rules.filter((rule) => rule.kind === kind && rule.isEnabled)
      .length,
  }));
  const missingRequiredSod = policy.validation.errors.some(
    (issue) =>
      issue.code === 'SOD_REQUIRED_CONTROL_MISSING' ||
      (issue.code === 'RULE_FAMILY_MISSING' &&
        issue.ruleKind === 'SegregationOfDuties')
  );
  const resolveValidationIssue = (issue: ProcurementPolicyValidationIssue) => {
    if (
      issue.code === 'SOD_REQUIRED_CONTROL_MISSING' ||
      (issue.code === 'RULE_FAMILY_MISSING' &&
        issue.ruleKind === 'SegregationOfDuties')
    ) {
      setSodDialogOpen(true);
      return;
    }

    const affectedRule = issue.ruleId
      ? policy.rules.find((candidate) => candidate.id === issue.ruleId)
      : undefined;
    if (affectedRule) setSelectedRule(affectedRule);
    if (issue.ruleKind) {
      setKindFilter(issue.ruleKind);
      if (issue.code === 'RULE_FAMILY_MISSING') setEditorKind(issue.ruleKind);
    }
    setActiveTab('rules');
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-start">
        <div>
          <Button
            variant="ghost"
            className="mb-2 -ml-3"
            onClick={() =>
              router.push('/administration/procurement/policy-sets')
            }
          >
            <ArrowLeft className="mr-2 h-4 w-4" />
            Executable policies
          </Button>
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="text-3xl font-bold">{policy.name}</h1>
            <Badge
              variant={
                policy.lifecycleStatus === 'Published' ? 'default' : 'secondary'
              }
            >
              {policy.lifecycleStatus}
            </Badge>
            <Badge variant="outline">
              {policy.scopeType === 'TenantOverride'
                ? 'Tenant override'
                : 'Tenant baseline'}
            </Badge>
          </div>
          <p className="mt-1 text-muted-foreground">
            {policy.code} · version {policy.version} ·{' '}
            {policy.defaultCurrencyCode} · source{' '}
            {policy.sourceConfigurationProfileCode}/v
            {policy.sourceConfigurationProfileVersion}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={() => policyQuery.refetch()}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
          {editable && (
            <Button variant="outline" onClick={openPolicyEditor}>
              <Edit className="mr-2 h-4 w-4" />
              Edit policy
            </Button>
          )}
          {!editable && (
            <Button
              variant="outline"
              onClick={() => {
                setChangeSummary('');
                setCloneOpen(true);
              }}
            >
              <Copy className="mr-2 h-4 w-4" />
              Clone draft
            </Button>
          )}
          {editable && (
            <Button
              variant="outline"
              onClick={() => validate.mutate()}
              disabled={validate.isPending}
            >
              <ShieldCheck className="mr-2 h-4 w-4" />
              Validate
            </Button>
          )}
          {editable && isSuperAdmin && (
            <Button onClick={() => setConfirmAction('publish')}>
              <CheckCircle2 className="mr-2 h-4 w-4" />
              Publish
            </Button>
          )}
          {policy.lifecycleStatus === 'Published' && isSuperAdmin && (
            <Button
              variant="destructive"
              onClick={() => setConfirmAction('retire')}
            >
              Retire
            </Button>
          )}
          {editable && (
            <Button
              variant="destructive"
              onClick={() => setConfirmAction('deletePolicy')}
            >
              <Trash2 className="mr-2 h-4 w-4" />
              Delete draft
            </Button>
          )}
        </div>
      </div>

      {policy.lifecycleStatus !== 'Draft' && (
        <Alert>
          <Eye className="h-4 w-4" />
          <AlertTitle>Immutable executable policy</AlertTitle>
          <AlertDescription>
            Published and retired policy versions and their relational rules are
            read-only. Clone this version to make a governed change.
          </AlertDescription>
        </Alert>
      )}
      {policy.lifecycleStatus === 'Draft' && (
        <Alert>
          <Edit className="h-4 w-4" />
          <AlertTitle>This policy is already an editable Draft</AlertTitle>
          <AlertDescription>
            Use Rules → Rule family → Authority → Add rule for Purchase
            Requisition approval bands. Clone draft appears only on Published or
            Retired versions.
          </AlertDescription>
        </Alert>
      )}
      {!isSuperAdmin && editable && (
        <Alert>
          <ShieldCheck className="h-4 w-4" />
          <AlertTitle>Preparation access</AlertTitle>
          <AlertDescription>
            Tenant administrators can prepare and validate drafts. Publication
            and retirement require SuperAdmin.
          </AlertDescription>
        </Alert>
      )}
      {editable && missingRequiredSod && (
        <Alert>
          <ShieldCheck className="h-4 w-4" />
          <AlertTitle>Required maker-checker controls are missing</AlertTitle>
          <AlertDescription className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <span>
              SOD prevents the same actor from performing incompatible actions
              such as initiating and approving, creating and receiving a PO, or
              processing and paying an invoice. Apply the prescribed six
              controls instead of entering them manually.
            </span>
            <Button
              type="button"
              variant="outline"
              className="shrink-0"
              onClick={() => setSodDialogOpen(true)}
            >
              Apply required SOD controls
            </Button>
          </AlertDescription>
        </Alert>
      )}
      {conflictMessage && (
        <Alert variant="destructive">
          <AlertTitle>Concurrent change detected</AlertTitle>
          <AlertDescription className="flex flex-wrap items-center justify-between gap-3">
            <span>{conflictMessage}</span>
            <Button variant="outline" size="sm" onClick={() => refresh()}>
              Reload policy
            </Button>
          </AlertDescription>
        </Alert>
      )}

      <Tabs
        value={activeTab}
        onValueChange={setActiveTab}
        className="space-y-4"
      >
        <TabsList className="h-auto flex-wrap">
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="rules">Rules ({policy.ruleCount})</TabsTrigger>
          <TabsTrigger value="validation">
            Validation ({policy.validation.errors.length})
          </TabsTrigger>
          <TabsTrigger value="history">History</TabsTrigger>
        </TabsList>
        <TabsContent value="overview" className="space-y-4">
          <div className="grid gap-4 md:grid-cols-3">
            <Card>
              <CardHeader className="pb-2">
                <CardDescription>Normalized rules</CardDescription>
                <CardTitle>{policy.ruleCount}</CardTitle>
              </CardHeader>
              <CardContent className="text-sm text-muted-foreground">
                {policy.ruleFamilyCount}/7 enabled families
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardDescription>Publication readiness</CardDescription>
                <CardTitle>
                  {policy.validation.isValid
                    ? 'Passed'
                    : `${policy.validation.errors.length} issue(s)`}
                </CardTitle>
              </CardHeader>
              <CardContent className="text-sm text-muted-foreground">
                {policy.validation.warnings.length} warning(s)
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardDescription>Latest activity</CardDescription>
                <CardTitle className="text-base">
                  {new Date(policy.updatedAt).toLocaleString()}
                </CardTitle>
              </CardHeader>
              <CardContent className="text-sm text-muted-foreground">
                {policy.updatedBy || 'System'}
              </CardContent>
            </Card>
          </div>
          <Card>
            <CardHeader>
              <CardTitle>Rule-family coverage</CardTitle>
              <CardDescription>
                Category, method, threshold, and authority define execution.
                Evidence defines the audit documents; Exception records the
                explicit permitted or prohibited exception stance; SOD enforces
                maker-checker conflicts. Publication remains fail-closed when a
                mandatory family is absent.
              </CardDescription>
            </CardHeader>
            <CardContent className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              {familyCounts.map((item) => (
                <div
                  key={item.kind}
                  className="flex items-center justify-between border-b py-2 text-sm"
                >
                  <span>{formatKind(item.kind)}</span>
                  <Badge variant={item.count ? 'default' : 'destructive'}>
                    {item.count}
                  </Badge>
                </div>
              ))}
            </CardContent>
          </Card>
          <Card>
            <CardHeader>
              <CardTitle>Policy lineage</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-4 text-sm md:grid-cols-2">
              <div>
                <p className="text-muted-foreground">Effective period</p>
                <p>
                  {new Date(policy.effectiveFrom).toLocaleDateString()} –{' '}
                  {policy.effectiveTo
                    ? new Date(policy.effectiveTo).toLocaleDateString()
                    : 'open-ended'}
                </p>
              </div>
              <div>
                <p className="text-muted-foreground">Supersedes</p>
                <p>{policy.supersedesPolicySetId || 'Initial version'}</p>
              </div>
              <div>
                <p className="text-muted-foreground">Base policy</p>
                <p>{policy.basePolicySetId || 'Tenant baseline'}</p>
              </div>
              <div>
                <p className="text-muted-foreground">Change summary</p>
                <p>{policy.changeSummary || 'No summary supplied.'}</p>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="rules" className="space-y-4">
          <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-end">
            <div className="w-full max-w-xs space-y-2">
              <Label>Rule family</Label>
              <Select
                value={kindFilter}
                onValueChange={(next) =>
                  setKindFilter(next as ProcurementPolicyRuleKind | 'All')
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="All">All rule families</SelectItem>
                  {procurementPolicyRuleKinds.map((kind) => (
                    <SelectItem key={kind} value={kind}>
                      {formatKind(kind)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {editable && (
              <Button
                onClick={() =>
                  setEditorKind(kindFilter === 'All' ? 'Category' : kindFilter)
                }
              >
                <Plus className="mr-2 h-4 w-4" />
                Add rule
              </Button>
            )}
          </div>
          <DataTable
            compact
            title="Normalized policy rules"
            description="Typed, effective-dated rule rows with DEC lineage and optional immutable base-rule references."
            data={visibleRules}
            columns={columns}
            enableSearch
            enablePagination
            pageSize={20}
            emptyStateMessage="No rules match this family."
            onRowDoubleClick={(row) => setSelectedRule(row.original)}
            rowActions={
              editable
                ? [
                    {
                      id: 'edit',
                      label: 'Edit rule',
                      icon: Edit,
                      onClick: (row) => setSelectedRule(row.original),
                    },
                    {
                      id: 'delete',
                      label: 'Delete rule',
                      icon: Trash2,
                      variant: 'destructive',
                      onClick: (row) => {
                        setDeleteRuleTarget(row.original);
                        setConfirmAction('deleteRule');
                      },
                    },
                  ]
                : [
                    {
                      id: 'view',
                      label: 'View rule',
                      icon: Eye,
                      onClick: (row) => setSelectedRule(row.original),
                    },
                  ]
            }
          />
        </TabsContent>

        <TabsContent value="validation" className="space-y-4">
          {!policy.validation.isValid && (
            <Alert>
              <ShieldCheck className="h-4 w-4" />
              <AlertTitle>Why publication is stopped</AlertTitle>
              <AlertDescription>
                The policy is executable configuration, so an incomplete rule
                can approve the wrong amount or bypass a required control.
                Evidence makes document/audit requirements explicit; Exception
                makes the permitted or prohibited exception route explicit; SOD
                prevents one actor from performing incompatible stages. Use
                Resolve on each issue. Prescribed SOD controls can be
                provisioned automatically.
              </AlertDescription>
            </Alert>
          )}
          {policy.validation.isValid && (
            <Alert>
              <CheckCircle2 className="h-4 w-4" />
              <AlertTitle>Publication validation passed</AlertTitle>
              <AlertDescription>
                All rule families, relationships, periods, bounds, DEC lineage,
                and override references currently pass.
              </AlertDescription>
            </Alert>
          )}
          {policy.validation.errors.map((issue, index) => (
            <Alert key={`${issue.code}-${index}`} variant="destructive">
              <AlertTitle>
                {issue.ruleCode ? `${issue.ruleCode}: ` : ''}
                {issue.code}
              </AlertTitle>
              <AlertDescription className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <span>{issue.message}</span>
                {(issue.ruleKind || issue.ruleId) && (
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    className="shrink-0"
                    onClick={() => resolveValidationIssue(issue)}
                  >
                    Resolve
                  </Button>
                )}
              </AlertDescription>
            </Alert>
          ))}
          {policy.validation.warnings.map((issue, index) => (
            <Alert key={`${issue.code}-warning-${index}`}>
              <AlertTitle>{issue.code}</AlertTitle>
              <AlertDescription>{issue.message}</AlertDescription>
            </Alert>
          ))}
        </TabsContent>

        <TabsContent value="history">
          <Card>
            <CardHeader>
              <CardTitle>Immutable policy history</CardTitle>
              <CardDescription>
                Lifecycle and rule changes record actor, roles, result, reason,
                and correlation context.
              </CardDescription>
            </CardHeader>
            <CardContent className="divide-y p-0">
              {policy.recentHistory.length ? (
                policy.recentHistory.map((item) => (
                  <div
                    key={item.id}
                    className="grid gap-2 px-6 py-4 text-sm md:grid-cols-[180px_160px_1fr]"
                  >
                    <div>
                      <p className="font-medium">{item.action}</p>
                      <p className="text-xs text-muted-foreground">
                        {item.result}
                        {item.ruleKind ? ` · ${formatKind(item.ruleKind)}` : ''}
                      </p>
                    </div>
                    <div>
                      <p>{item.actorName}</p>
                      <p className="text-xs text-muted-foreground">
                        {item.actorRoles || 'System'}
                      </p>
                    </div>
                    <div>
                      <p>{item.reason || 'No reason supplied'}</p>
                      <p className="mt-1 break-all text-xs text-muted-foreground">
                        {new Date(item.timestamp).toLocaleString()} ·{' '}
                        {item.correlationId}
                      </p>
                    </div>
                  </div>
                ))
              ) : (
                <p className="p-6 text-sm text-muted-foreground">
                  No policy history is available.
                </p>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      <Dialog
        open={Boolean(activeEditorKind)}
        onOpenChange={(open) => {
          if (!open) {
            setEditorKind(undefined);
            setSelectedRule(undefined);
          }
        }}
      >
        <DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-4xl">
          {activeEditorKind && (
            <>
              <DialogHeader>
                <DialogTitle>
                  {editorRule
                    ? `${editorRule.ruleCode} · ${editorRule.name}`
                    : `Add ${formatKind(activeEditorKind)} rule`}
                </DialogTitle>
                <DialogDescription>
                  {procurementPolicyRuleRegistry[activeEditorKind].description}
                </DialogDescription>
              </DialogHeader>
              <ProcurementPolicyRuleEditor
                key={`${editorRule?.id ?? activeEditorKind}-${editorRule?.rowVersion ?? 'new'}`}
                policyId={id}
                scopeType={policy.scopeType}
                kind={activeEditorKind}
                policyEffectiveFrom={policy.effectiveFrom}
                policyEffectiveTo={policy.effectiveTo}
                rule={editorRule}
                availableRules={policy.rules}
                editable={editable}
                onChanged={refresh}
                onCancel={() => {
                  setEditorKind(undefined);
                  setSelectedRule(undefined);
                }}
              />
            </>
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={profileEditorOpen} onOpenChange={setProfileEditorOpen}>
        <DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>Edit executable policy draft</DialogTitle>
            <DialogDescription>
              Rule periods must remain contained by the policy period.
            </DialogDescription>
          </DialogHeader>
          {editForm && (
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2 sm:col-span-2">
                <Label>Name</Label>
                <Input
                  value={editForm.name}
                  onChange={(event) =>
                    setEditForm(
                      (current) =>
                        current && { ...current, name: event.target.value }
                    )
                  }
                />
              </div>
              <div className="space-y-2">
                <Label>Default currency</Label>
                <Input
                  maxLength={3}
                  value={editForm.defaultCurrencyCode}
                  onChange={(event) =>
                    setEditForm(
                      (current) =>
                        current && {
                          ...current,
                          defaultCurrencyCode: event.target.value.toUpperCase(),
                        }
                    )
                  }
                />
              </div>
              <div className="flex items-center gap-3 pt-7">
                <Switch
                  checked={editForm.isDefault}
                  onCheckedChange={(next) =>
                    setEditForm(
                      (current) => current && { ...current, isDefault: next }
                    )
                  }
                />
                <span className="text-sm">Default policy family</span>
              </div>
              <div className="space-y-2">
                <Label>Effective from</Label>
                <Input
                  type="date"
                  value={editForm.effectiveFrom}
                  onChange={(event) =>
                    setEditForm(
                      (current) =>
                        current && {
                          ...current,
                          effectiveFrom: event.target.value,
                        }
                    )
                  }
                />
              </div>
              <div className="space-y-2">
                <Label>Effective to</Label>
                <Input
                  type="date"
                  value={editForm.effectiveTo ?? ''}
                  onChange={(event) =>
                    setEditForm(
                      (current) =>
                        current && {
                          ...current,
                          effectiveTo: event.target.value,
                        }
                    )
                  }
                />
              </div>
              <div className="space-y-2 sm:col-span-2">
                <Label>Description</Label>
                <Textarea
                  value={editForm.description ?? ''}
                  onChange={(event) =>
                    setEditForm(
                      (current) =>
                        current && {
                          ...current,
                          description: event.target.value,
                        }
                    )
                  }
                />
              </div>
              <div className="space-y-2 sm:col-span-2">
                <Label>Change summary</Label>
                <Textarea
                  value={editForm.changeSummary ?? ''}
                  onChange={(event) =>
                    setEditForm(
                      (current) =>
                        current && {
                          ...current,
                          changeSummary: event.target.value,
                        }
                    )
                  }
                />
              </div>
              <div className="space-y-2 sm:col-span-2">
                <Label>Change reason</Label>
                <Input
                  value={editForm.reason ?? ''}
                  onChange={(event) =>
                    setEditForm(
                      (current) =>
                        current && { ...current, reason: event.target.value }
                    )
                  }
                />
              </div>
            </div>
          )}
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setProfileEditorOpen(false)}
            >
              Cancel
            </Button>
            <Button
              onClick={() => updatePolicy.mutate()}
              disabled={updatePolicy.isPending}
            >
              {updatePolicy.isPending ? 'Saving…' : 'Save changes'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={cloneOpen} onOpenChange={setCloneOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Create next policy draft</DialogTitle>
            <DialogDescription>
              All normalized rules are copied with source-rule lineage. The
              immutable source configuration reference is retained.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label>Change summary</Label>
            <Textarea
              value={changeSummary}
              onChange={(event) => setChangeSummary(event.target.value)}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCloneOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => clone.mutate()} disabled={clone.isPending}>
              {clone.isPending ? 'Cloning…' : 'Create draft'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <AlertDialog
        open={Boolean(confirmAction)}
        onOpenChange={(open) => {
          if (!open) {
            setConfirmAction(null);
            setDeleteRuleTarget(undefined);
          }
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {confirmAction === 'publish'
                ? 'Publish this executable policy?'
                : confirmAction === 'retire'
                  ? 'Retire this policy version?'
                  : confirmAction === 'deleteRule'
                    ? `Delete ${deleteRuleTarget?.ruleCode ?? 'this rule'}?`
                    : 'Delete this policy draft?'}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {confirmAction === 'publish'
                ? 'Publication validates every relational rule family and retires the prior published family version atomically. Runtime consumers remain unchanged until later tasks.'
                : confirmAction === 'deleteRule'
                  ? 'The rule is soft-deleted and the change is appended to policy history.'
                  : 'This action preserves immutable published history and records the actor and reason.'}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <div className="space-y-2">
            <Label>Reason *</Label>
            <Textarea
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              placeholder="Required for the audit trail"
            />
          </div>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={(event) => {
                event.preventDefault();
                action.mutate();
              }}
              disabled={action.isPending || !reason.trim()}
            >
              {action.isPending ? 'Applying…' : 'Confirm'}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <Dialog open={sodDialogOpen} onOpenChange={setSodDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Apply required SOD controls</DialogTitle>
            <DialogDescription>
              This creates only missing prescribed HardStop controls in this
              Draft. Existing controls are retained and every change is audited.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label>Reason *</Label>
            <Textarea
              value={sodReason}
              onChange={(event) => setSodReason(event.target.value)}
              placeholder="Why these mandatory controls are being provisioned"
            />
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setSodDialogOpen(false)}
              disabled={applyRequiredSod.isPending}
            >
              Cancel
            </Button>
            <Button
              onClick={() => applyRequiredSod.mutate()}
              disabled={applyRequiredSod.isPending || !sodReason.trim()}
            >
              {applyRequiredSod.isPending
                ? 'Applying controls…'
                : 'Apply six controls'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
