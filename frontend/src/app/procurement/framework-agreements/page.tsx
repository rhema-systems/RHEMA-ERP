'use client';

import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  CheckCircle2,
  Clock3,
  Copy,
  Download,
  FileCheck2,
  FilePlus2,
  History,
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
import {
  frameworkActionState,
  frameworkExtensionTone,
  frameworkLocalInputToUtcInstant,
  frameworkRemainingCeilingNote,
  frameworkStatusTone,
  frameworkUtcInstantToLocalInput,
} from '@/lib/procurement-framework-agreement';
import { procurementFrameworkAgreementService as service } from '@/services/procurement-framework-agreement.service';
import type {
  CreateFrameworkAgreement,
  FrameworkAgreement,
  FrameworkAgreementExtension,
  FrameworkAgreementStatus,
  FrameworkAuthorityKind,
  FrameworkLifecycleRequest,
  SaveFrameworkCallOffAuthority,
  SaveFrameworkPriceListLine,
} from '@/types/procurement-framework-agreement';

const statuses: FrameworkAgreementStatus[] = [
  'Draft',
  'PendingApproval',
  'Published',
  'Rejected',
  'Superseded',
  'Expired',
  'Terminated',
];

const authorityKinds: FrameworkAuthorityKind[] = [
  'User',
  'Role',
  'OrganizationalUnit',
  'Permission',
];

const toInputDate = frameworkUtcInstantToLocalInput;
const toUtc = frameworkLocalInputToUtcInstant;
const dateAfter = (days: number) => {
  const date = new Date();
  date.setUTCDate(date.getUTCDate() + days);
  date.setUTCSeconds(0, 0);
  return toInputDate(date.toISOString());
};
const formatDate = (value?: string) =>
  value ? new Date(value).toLocaleDateString() : '—';
const shortHash = (value?: string) =>
  value ? `${value.slice(0, 12)}…${value.slice(-8)}` : '—';
const money = (value: number, currency: string) =>
  new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: currency || 'GHS',
    maximumFractionDigits: 2,
  }).format(value);

type EditorMode = 'create' | 'edit';
type LifecycleAction = 'submit' | 'approve' | 'reject' | 'terminate';

interface EditorState
  extends Omit<
    CreateFrameworkAgreement,
    'effectiveFromUtc' | 'effectiveToUtc'
  > {
  effectiveFromUtc: string;
  effectiveToUtc: string;
}

const emptyEditor = (): EditorState => ({
  sourceType: 'Tender',
  sourceId: '',
  businessPartnerId: '',
  title: '',
  ceilingAmount: 0,
  currencyCode: 'GHS',
  effectiveFromUtc: dateAfter(1),
  effectiveToUtc: dateAfter(366),
  workflowDefinitionId: '',
  description: '',
  termsSummary: '',
  categoryIds: [],
  priceLines: [],
  callOffAuthorities: [],
});

const evidence = (
  action: string,
  reference: string
): FrameworkLifecycleRequest['evidence'] => [
  {
    referenceKind: 'ExternalReference',
    reference: reference.trim(),
    label: `${action} retained evidence`,
    requirementKey: `FrameworkAgreement.${action}`,
  },
];

export default function FrameworkAgreementsPage() {
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const canManage = hasPermission('procurement.contract.manage');
  const canApprove = hasPermission('procurement.contract.approve');
  const canRead =
    canManage ||
    canApprove ||
    hasPermission('procurement.records.read') ||
    hasPermission('procurement.audit.read');

  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<FrameworkAgreementStatus | 'all'>('all');
  const [effectiveOnly, setEffectiveOnly] = useState(false);
  const [selectedId, setSelectedId] = useState<string>();
  const [editorMode, setEditorMode] = useState<EditorMode>();
  const [editor, setEditor] = useState<EditorState>(emptyEditor);
  const [lifecycleAction, setLifecycleAction] = useState<LifecycleAction>();
  const [comment, setComment] = useState('');
  const [evidenceReference, setEvidenceReference] = useState('');
  const [cloneOpen, setCloneOpen] = useState(false);
  const [cloneFrom, setCloneFrom] = useState(dateAfter(1));
  const [cloneTo, setCloneTo] = useState(dateAfter(366));
  const [cloneWorkflowId, setCloneWorkflowId] = useState('');
  const [cloneSummary, setCloneSummary] = useState('');
  const [documentOpen, setDocumentOpen] = useState(false);
  const [documentFile, setDocumentFile] = useState<File>();
  const [documentType, setDocumentType] = useState('Signed agreement');
  const [documentTitle, setDocumentTitle] = useState('');
  const [documentRequired, setDocumentRequired] = useState(true);
  const [extensionOpen, setExtensionOpen] = useState(false);
  const [extensionEnd, setExtensionEnd] = useState(dateAfter(730));
  const [extensionWorkflowId, setExtensionWorkflowId] = useState('');
  const [extensionReason, setExtensionReason] = useState('');
  const [extensionEvidence, setExtensionEvidence] = useState('');
  const [extensionDecision, setExtensionDecision] = useState<{
    extension: FrameworkAgreementExtension;
    approve: boolean;
  }>();
  const [extensionDecisionComment, setExtensionDecisionComment] = useState('');
  const [extensionDecisionEvidence, setExtensionDecisionEvidence] =
    useState('');

  const summary = useQuery({
    queryKey: ['framework-agreement-summary'],
    queryFn: service.summary,
    enabled: canRead,
  });
  const history = useQuery({
    queryKey: ['framework-agreement-history', search, status, effectiveOnly],
    queryFn: () =>
      service.search({
        search: search || undefined,
        status: status === 'all' ? undefined : status,
        effectiveOnly: effectiveOnly || undefined,
        page: 1,
        pageSize: 100,
      }),
    enabled: canRead,
  });
  const detail = useQuery({
    queryKey: ['framework-agreement-detail', selectedId],
    queryFn: () => service.get(selectedId ?? ''),
    enabled: canRead && Boolean(selectedId),
  });
  const workflows = useQuery({
    queryKey: ['framework-agreement-workflows'],
    queryFn: service.workflowOptions,
    enabled: canRead,
  });
  const categories = useQuery({
    queryKey: ['framework-agreement-categories'],
    queryFn: service.categoryOptions,
    enabled: canRead,
  });
  const items = useQuery({
    queryKey: ['framework-agreement-items'],
    queryFn: service.itemOptions,
    enabled: canRead,
  });
  const sources = useQuery({
    queryKey: ['framework-agreement-sources'],
    queryFn: service.sourceOptions,
    enabled: canRead,
  });

  useEffect(() => {
    if (!selectedId && history.data?.items[0])
      setSelectedId(history.data.items[0].id);
  }, [history.data, selectedId]);

  const refresh = async (id?: string) => {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: ['framework-agreement-summary'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['framework-agreement-history'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['framework-agreement-sources'],
      }),
      id
        ? queryClient.invalidateQueries({
            queryKey: ['framework-agreement-detail', id],
          })
        : Promise.resolve(),
    ]);
  };

  const editorPayload = (): CreateFrameworkAgreement => ({
    ...editor,
    title: editor.title.trim(),
    currencyCode: editor.currencyCode.trim().toUpperCase(),
    effectiveFromUtc: toUtc(editor.effectiveFromUtc),
    effectiveToUtc: toUtc(editor.effectiveToUtc),
    description: editor.description?.trim() || undefined,
    termsSummary: editor.termsSummary?.trim() || undefined,
    callOffAuthorities: editor.callOffAuthorities.map((authority) => ({
      ...authority,
      authorityValue: authority.authorityValue.trim(),
      displayName: authority.displayName.trim(),
      validFromUtc: toUtc(authority.validFromUtc),
      validToUtc: toUtc(authority.validToUtc),
    })),
  });

  const validateEditor = () => {
    if (
      !editor.sourceId ||
      !editor.businessPartnerId ||
      !editor.title.trim() ||
      !editor.workflowDefinitionId ||
      editor.ceilingAmount <= 0 ||
      editor.categoryIds.length === 0 ||
      editor.priceLines.length === 0 ||
      editor.callOffAuthorities.length === 0
    ) {
      toast.error(
        'Source, title, workflow, ceiling, category, price and authority are required.'
      );
      return false;
    }
    if (
      editor.priceLines.some(
        (line) => !line.inventoryItemId || line.unitPrice <= 0
      ) ||
      editor.callOffAuthorities.some(
        (authority) =>
          !authority.authorityValue.trim() ||
          !authority.displayName.trim() ||
          (authority.authorityKind === 'User' && !authority.authorityUserId)
      )
    ) {
      toast.error('Complete every governed price and authority line.');
      return false;
    }
    return true;
  };

  const saveMutation = useMutation({
    mutationFn: async () => {
      if (!validateEditor()) throw new Error('validation');
      const payload = editorPayload();
      if (editorMode === 'edit' && detail.data) {
        return service.update(detail.data.id, {
          rowVersion: detail.data.rowVersion,
          title: payload.title,
          ceilingAmount: payload.ceilingAmount,
          currencyCode: payload.currencyCode,
          effectiveFromUtc: payload.effectiveFromUtc,
          effectiveToUtc: payload.effectiveToUtc,
          workflowDefinitionId: payload.workflowDefinitionId,
          description: payload.description,
          termsSummary: payload.termsSummary,
          categoryIds: payload.categoryIds,
          priceLines: payload.priceLines,
          callOffAuthorities: payload.callOffAuthorities,
        });
      }
      return service.create(payload);
    },
    onSuccess: async (value) => {
      setEditorMode(undefined);
      setSelectedId(value.id);
      setEditor(emptyEditor());
      await refresh(value.id);
      toast.success(
        value.version === 1
          ? 'Framework agreement draft created.'
          : 'Framework agreement draft saved.'
      );
    },
    onError: (error) => {
      if ((error as Error).message !== 'validation')
        toast.error(
          'The framework draft was blocked. Verify source readiness, supplier eligibility and tenant references.'
        );
    },
  });

  const lifecycleMutation = useMutation({
    mutationFn: ({
      agreement,
      action,
      request,
    }: {
      agreement: FrameworkAgreement;
      action: LifecycleAction;
      request: FrameworkLifecycleRequest;
    }) => service[action](agreement.id, request),
    onSuccess: async (value) => {
      setLifecycleAction(undefined);
      setComment('');
      setEvidenceReference('');
      await refresh(value.id);
      toast.success('Framework lifecycle updated.');
    },
    onError: () =>
      toast.error(
        'The lifecycle action was blocked. Verify workflow outcome, independent actor, evidence and current supplier/source state.'
      ),
  });

  const cloneMutation = useMutation({
    mutationFn: (agreement: FrameworkAgreement) =>
      service.clone(agreement.id, {
        rowVersion: agreement.rowVersion,
        effectiveFromUtc: toUtc(cloneFrom),
        effectiveToUtc: toUtc(cloneTo),
        workflowDefinitionId: cloneWorkflowId,
        changeSummary: cloneSummary.trim(),
      }),
    onSuccess: async (value) => {
      setCloneOpen(false);
      setSelectedId(value.id);
      setCloneSummary('');
      await refresh(value.id);
      toast.success(`Draft revision v${value.version} created.`);
    },
    onError: () =>
      toast.error(
        'The revision could not be cloned. Only one open revision is permitted.'
      ),
  });

  const documentMutation = useMutation({
    mutationFn: (agreement: FrameworkAgreement) => {
      if (!documentFile) throw new Error('file');
      return service.uploadDocument(
        agreement.id,
        documentFile,
        documentType.trim(),
        documentTitle.trim() || documentFile.name,
        documentRequired
      );
    },
    onSuccess: async () => {
      setDocumentOpen(false);
      setDocumentFile(undefined);
      setDocumentTitle('');
      await refresh(selectedId);
      toast.success('Clean document registered in central DMS.');
    },
    onError: () =>
      toast.error(
        'Upload was blocked. The file must pass the centralized malware scan.'
      ),
  });

  const retireDocumentMutation = useMutation({
    mutationFn: ({
      agreementId,
      documentId,
    }: {
      agreementId: string;
      documentId: string;
    }) => service.retireDocument(agreementId, documentId),
    onSuccess: async () => {
      await refresh(selectedId);
      toast.success('Draft document link retired; DMS history retained.');
    },
    onError: () => toast.error('The document link could not be retired.'),
  });

  const extensionMutation = useMutation({
    mutationFn: (agreement: FrameworkAgreement) =>
      service.requestExtension(agreement.id, {
        agreementRowVersion: agreement.rowVersion,
        proposedEndUtc: toUtc(extensionEnd),
        workflowDefinitionId: extensionWorkflowId,
        reason: extensionReason.trim(),
        evidence: evidence('ExtensionSubmitted', extensionEvidence),
      }),
    onSuccess: async () => {
      setExtensionOpen(false);
      setExtensionReason('');
      setExtensionEvidence('');
      await refresh(selectedId);
      toast.success('Extension submitted to its independent workflow.');
    },
    onError: () =>
      toast.error(
        'The extension was blocked. Verify the proposed end, workflow and evidence.'
      ),
  });

  const extensionDecisionMutation = useMutation({
    mutationFn: ({
      agreement,
      decision,
    }: {
      agreement: FrameworkAgreement;
      decision: NonNullable<typeof extensionDecision>;
    }) =>
      service.decideExtension(agreement.id, decision.extension.id, {
        rowVersion: decision.extension.rowVersion,
        approve: decision.approve,
        comment: extensionDecisionComment.trim(),
        evidence: evidence(
          decision.approve ? 'ExtensionApproved' : 'ExtensionRejected',
          extensionDecisionEvidence
        ),
      }),
    onSuccess: async () => {
      setExtensionDecision(undefined);
      setExtensionDecisionComment('');
      setExtensionDecisionEvidence('');
      await refresh(selectedId);
      toast.success('Extension decision recorded.');
    },
    onError: () =>
      toast.error(
        'Extension decision was blocked. Only Completed approves; Cancelled or Failed rejects.'
      ),
  });

  const processMutation = useMutation({
    mutationFn: service.processLifecycle,
    onSuccess: async ({ processed }) => {
      await refresh(selectedId);
      toast.success(`${processed} scheduled lifecycle change(s) processed.`);
    },
    onError: () => toast.error('Scheduled lifecycle processing failed.'),
  });

  const selected = detail.data;
  const actions = useMemo(() => frameworkActionState(selected), [selected]);

  const startCreate = () => {
    setEditor(emptyEditor());
    setEditorMode('create');
  };

  const startEdit = (agreement: FrameworkAgreement) => {
    setEditor({
      sourceType: agreement.sourceType,
      sourceId: agreement.sourceId,
      businessPartnerId: agreement.businessPartnerId,
      title: agreement.title,
      ceilingAmount: agreement.ceilingAmount,
      currencyCode: agreement.currencyCode,
      effectiveFromUtc: toInputDate(agreement.effectiveFromUtc),
      effectiveToUtc: toInputDate(agreement.effectiveToUtc),
      workflowDefinitionId: agreement.workflowDefinitionId,
      description: agreement.description ?? '',
      termsSummary: agreement.termsSummary ?? '',
      categoryIds: agreement.categories.map((item) => item.partnerCategoryId),
      priceLines: agreement.priceLines.map((line) => ({
        inventoryItemId: line.inventoryItemId,
        unitPrice: line.unitPrice,
        minimumQuantity: line.minimumQuantity,
        maximumQuantity: line.maximumQuantity,
        leadTimeDays: line.leadTimeDays,
        specifications: line.specifications,
      })),
      callOffAuthorities: agreement.callOffAuthorities
        .filter((authority) => authority.isActive)
        .map((authority) => ({
          authorityKind: authority.authorityKind,
          authorityUserId: authority.authorityUserId,
          authorityValue: authority.authorityValue,
          displayName: authority.displayName,
          maximumCallOffAmount: authority.maximumCallOffAmount,
          validFromUtc: toInputDate(authority.validFromUtc),
          validToUtc: toInputDate(authority.validToUtc),
          isActive: authority.isActive,
        })),
    });
    setEditorMode('edit');
  };

  const selectSource = (value: string) => {
    const source = sources.data?.find(
      (item) =>
        `${item.sourceType}:${item.sourceId}:${item.businessPartnerId}` === value
    );
    if (!source) return;
    setEditor((current) => ({
      ...current,
      sourceType: source.sourceType,
      sourceId: source.sourceId,
      businessPartnerId: source.businessPartnerId,
      title:
        current.title ||
        `${source.supplierName} framework — ${source.sourceReference}`,
      ceilingAmount: source.awardAmount,
      currencyCode: source.currencyCode,
    }));
  };

  const toggleCategory = (id: string) =>
    setEditor((current) => ({
      ...current,
      categoryIds: current.categoryIds.includes(id)
        ? current.categoryIds.filter((value) => value !== id)
        : [...current.categoryIds, id],
    }));

  const addPriceLine = () =>
    setEditor((current) => ({
      ...current,
      priceLines: [
        ...current.priceLines,
        {
          inventoryItemId: '',
          unitPrice: 0,
          minimumQuantity: 1,
          leadTimeDays: 0,
        },
      ],
    }));

  const updatePriceLine = (
    index: number,
    patch: Partial<SaveFrameworkPriceListLine>
  ) =>
    setEditor((current) => ({
      ...current,
      priceLines: current.priceLines.map((line, itemIndex) =>
        itemIndex === index ? { ...line, ...patch } : line
      ),
    }));

  const addAuthority = () =>
    setEditor((current) => ({
      ...current,
      callOffAuthorities: [
        ...current.callOffAuthorities,
        {
          authorityKind: 'Role',
          authorityValue: 'Procurement Officer',
          displayName: 'Procurement Officer',
          validFromUtc: current.effectiveFromUtc,
          validToUtc: current.effectiveToUtc,
          isActive: true,
        },
      ],
    }));

  const updateAuthority = (
    index: number,
    patch: Partial<SaveFrameworkCallOffAuthority>
  ) =>
    setEditor((current) => ({
      ...current,
      callOffAuthorities: current.callOffAuthorities.map(
        (authority, itemIndex) =>
          itemIndex === index ? { ...authority, ...patch } : authority
      ),
    }));

  const runLifecycle = () => {
    if (
      !selected ||
      !lifecycleAction ||
      !comment.trim() ||
      !evidenceReference.trim()
    ) {
      toast.error(
        'A decision comment and retained evidence reference are required.'
      );
      return;
    }
    lifecycleMutation.mutate({
      agreement: selected,
      action: lifecycleAction,
      request: {
        rowVersion: selected.rowVersion,
        comment: comment.trim(),
        evidence: evidence(lifecycleAction, evidenceReference),
      },
    });
  };

  const download = async (
    agreement: FrameworkAgreement,
    documentId: string,
    fileName: string
  ) => {
    try {
      const blob = await service.downloadDocument(agreement.id, documentId);
      const url = URL.createObjectURL(blob);
      const link = window.document.createElement('a');
      link.href = url;
      link.download = fileName;
      link.click();
      URL.revokeObjectURL(url);
    } catch {
      toast.error('The protected DMS document could not be downloaded.');
    }
  };

  const summaryCards = [
    {
      label: 'All revisions',
      value: summary.data?.totalVersions ?? 0,
      Icon: History,
    },
    {
      label: 'Draft',
      value: summary.data?.draftCount ?? 0,
      Icon: Save,
    },
    {
      label: 'Pending',
      value: summary.data?.pendingApprovalCount ?? 0,
      Icon: Clock3,
    },
    {
      label: 'Effective',
      value: summary.data?.effectiveCount ?? 0,
      Icon: CheckCircle2,
    },
    {
      label: 'Expiring ≤90d',
      value: summary.data?.expiringWithin90DaysCount ?? 0,
      Icon: AlertTriangle,
    },
    {
      label: 'Extensions pending',
      value: summary.data?.pendingExtensionCount ?? 0,
      Icon: RefreshCw,
    },
  ];

  return (
    <div className="space-y-6 p-6" data-testid="framework-agreements-page">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">
            Framework agreements
          </h1>
          <p className="max-w-4xl text-sm text-muted-foreground">
            Versioned supplier agreements, governed price lists, call-off
            authority configuration, central-DMS documents and independently
            approved extensions. Call-off spend enforcement begins in TDC-0402.
          </p>
        </div>
        <div className="flex gap-2">
          <Button
            variant="outline"
            disabled={!canManage || processMutation.isPending}
            onClick={() => processMutation.mutate()}
          >
            <RefreshCw className="mr-2 h-4 w-4" />
            Process lifecycle
          </Button>
          <Button disabled={!canManage} onClick={startCreate}>
            <Plus className="mr-2 h-4 w-4" />
            New agreement
          </Button>
        </div>
      </div>

      {!canRead && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Permission required</AlertTitle>
          <AlertDescription>
            A procurement records, contract management, contract approval, or
            audit permission is required.
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
          <CardTitle className="text-base">
            Agreement revision history
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap gap-3">
            <div className="relative min-w-64 flex-1">
              <Search className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
              <Input
                className="pl-9"
                placeholder="Agreement, supplier, source or price list"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
              />
            </div>
            <Select
              value={status}
              onValueChange={(value) =>
                setStatus(value as FrameworkAgreementStatus | 'all')
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
            <label className="flex items-center gap-2 rounded-md border px-3 text-sm">
              <input
                type="checkbox"
                checked={effectiveOnly}
                onChange={(event) => setEffectiveOnly(event.target.checked)}
              />
              Effective only
            </label>
          </div>

          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Agreement / revision</TableHead>
                  <TableHead>Supplier</TableHead>
                  <TableHead>Source</TableHead>
                  <TableHead>Effective period</TableHead>
                  <TableHead className="text-right">Ceiling</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {history.data?.items.map((agreement) => (
                  <TableRow
                    key={agreement.id}
                    className="cursor-pointer"
                    data-testid={`framework-row-${agreement.id}`}
                    data-state={
                      selectedId === agreement.id ? 'selected' : undefined
                    }
                    onClick={() => setSelectedId(agreement.id)}
                  >
                    <TableCell>
                      <p className="font-medium">
                        {agreement.agreementNumber} · v{agreement.version}
                      </p>
                      <p className="text-xs text-muted-foreground">
                        {agreement.title}
                      </p>
                    </TableCell>
                    <TableCell>
                      {agreement.supplierCode} · {agreement.supplierName}
                    </TableCell>
                    <TableCell>
                      <p>{agreement.sourceReference}</p>
                      <p className="text-xs text-muted-foreground">
                        {agreement.sourceType}
                      </p>
                    </TableCell>
                    <TableCell>
                      {formatDate(agreement.effectiveFromUtc)} —{' '}
                      {formatDate(agreement.effectiveEndUtc)}
                    </TableCell>
                    <TableCell className="text-right">
                      {money(agreement.ceilingAmount, agreement.currencyCode)}
                    </TableCell>
                    <TableCell>
                      <Badge variant={frameworkStatusTone(agreement.status)}>
                        {agreement.status}
                      </Badge>
                      {agreement.isEffective && (
                        <Badge className="ml-2" variant="outline">
                          Effective
                        </Badge>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
                {!history.isLoading && !history.data?.items.length && (
                  <TableRow>
                    <TableCell
                      colSpan={6}
                      className="py-10 text-center text-muted-foreground"
                    >
                      No framework revisions match the current filter.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      {selected && (
        <div className="space-y-4" data-testid="framework-agreement-detail">
          <Card>
            <CardHeader>
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <CardTitle className="text-lg">
                    {selected.agreementNumber} · revision {selected.version}
                  </CardTitle>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {selected.supplierCode} · {selected.supplierName} ·{' '}
                    {selected.sourceReference}
                  </p>
                </div>
                <div className="flex flex-wrap gap-2">
                  {actions.canEdit && canManage && (
                    <Button
                      variant="outline"
                      onClick={() => startEdit(selected)}
                    >
                      <Save className="mr-2 h-4 w-4" />
                      Edit draft
                    </Button>
                  )}
                  {actions.canAddDocument && canManage && (
                    <Button
                      variant="outline"
                      onClick={() => setDocumentOpen(true)}
                    >
                      <FilePlus2 className="mr-2 h-4 w-4" />
                      Add document
                    </Button>
                  )}
                  {actions.canSubmit && canManage && (
                    <Button onClick={() => setLifecycleAction('submit')}>
                      Submit
                    </Button>
                  )}
                  {actions.canApprove && canApprove && (
                    <Button onClick={() => setLifecycleAction('approve')}>
                      Approve & publish
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
                  {actions.canClone && canManage && (
                    <Button
                      variant="outline"
                      onClick={() => {
                        setCloneFrom(
                          toInputDate(
                            new Date(
                              Math.max(
                                Date.now(),
                                new Date(selected.effectiveEndUtc).getTime()
                              )
                            ).toISOString()
                          )
                        );
                        setCloneTo(dateAfter(730));
                        setCloneWorkflowId(selected.workflowDefinitionId);
                        setCloneOpen(true);
                      }}
                    >
                      <Copy className="mr-2 h-4 w-4" />
                      New revision
                    </Button>
                  )}
                  {actions.canRequestExtension && canManage && (
                    <Button
                      variant="outline"
                      onClick={() => {
                        const end = new Date(selected.effectiveEndUtc);
                        end.setUTCFullYear(end.getUTCFullYear() + 1);
                        setExtensionEnd(toInputDate(end.toISOString()));
                        setExtensionWorkflowId(selected.workflowDefinitionId);
                        setExtensionOpen(true);
                      }}
                    >
                      Extend
                    </Button>
                  )}
                  {actions.canTerminate && canApprove && (
                    <Button
                      variant="destructive"
                      onClick={() => setLifecycleAction('terminate')}
                    >
                      Terminate
                    </Button>
                  )}
                </div>
              </div>
            </CardHeader>
            <CardContent className="space-y-5">
              <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
                <Info
                  label="Status"
                  value={`${selected.status}${selected.isEffective ? ' · effective' : ''}`}
                />
                <Info
                  label="Validity"
                  value={`${formatDate(selected.effectiveFromUtc)} — ${formatDate(selected.effectiveEndUtc)}`}
                />
                <Info
                  label="Ceiling"
                  value={money(selected.ceilingAmount, selected.currencyCode)}
                />
                <Info
                  label="Governed price list"
                  value={`${selected.priceListReference} · v${selected.priceListVersion}`}
                />
                <Info
                  label="Award readiness"
                  value={shortHash(selected.sourceIntegrityHash)}
                />
                <Info
                  label="Supplier eligibility"
                  value={shortHash(selected.supplierEligibilityDecisionHash)}
                />
                <Info
                  label="Workflow instance"
                  value={selected.workflowInstanceId ?? 'Not started'}
                />
                <Info
                  label="Agreement integrity"
                  value={shortHash(selected.integrityHash)}
                />
              </div>

              <Alert>
                <ShieldCheck className="h-4 w-4" />
                <AlertTitle>Configuration boundary</AlertTitle>
                <AlertDescription>
                  {frameworkRemainingCeilingNote(selected)} Supplier, award,
                  workflow, evidence and immutable price lineage are active now.
                </AlertDescription>
              </Alert>
            </CardContent>
          </Card>

          <div className="grid gap-4 xl:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle className="text-base">
                  Categories & governed price list
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="flex flex-wrap gap-2">
                  {selected.categories.map((category) => (
                    <Badge key={category.id} variant="secondary">
                      {category.categoryCode} · {category.categoryName}
                    </Badge>
                  ))}
                </div>
                <div className="overflow-x-auto rounded-md border">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Item</TableHead>
                        <TableHead>UOM</TableHead>
                        <TableHead className="text-right">Unit price</TableHead>
                        <TableHead className="text-right">
                          Quantity rule
                        </TableHead>
                        <TableHead className="text-right">Lead days</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {selected.priceLines.map((line) => (
                        <TableRow key={line.id}>
                          <TableCell>
                            <p className="font-medium">{line.itemCode}</p>
                            <p className="text-xs text-muted-foreground">
                              {line.itemName}
                            </p>
                          </TableCell>
                          <TableCell>{line.unitOfMeasure}</TableCell>
                          <TableCell className="text-right">
                            {money(line.unitPrice, selected.currencyCode)}
                          </TableCell>
                          <TableCell className="text-right">
                            {line.minimumQuantity}
                            {line.maximumQuantity
                              ? ` — ${line.maximumQuantity}`
                              : '+'}
                          </TableCell>
                          <TableCell className="text-right">
                            {line.leadTimeDays}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="text-base">
                  Call-off authority configuration
                </CardTitle>
              </CardHeader>
              <CardContent>
                <div className="overflow-x-auto rounded-md border">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Authority</TableHead>
                        <TableHead>Validity</TableHead>
                        <TableHead className="text-right">Limit</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {selected.callOffAuthorities.map((authority) => (
                        <TableRow key={authority.id}>
                          <TableCell>
                            <p className="font-medium">
                              {authority.displayName}
                            </p>
                            <p className="text-xs text-muted-foreground">
                              {authority.authorityKind} ·{' '}
                              {authority.authorityValue}
                            </p>
                          </TableCell>
                          <TableCell>
                            {formatDate(authority.validFromUtc)} —{' '}
                            {formatDate(authority.validToUtc)}
                          </TableCell>
                          <TableCell className="text-right">
                            {authority.maximumCallOffAmount
                              ? money(
                                  authority.maximumCallOffAmount,
                                  selected.currencyCode
                                )
                              : 'Agreement ceiling'}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              </CardContent>
            </Card>
          </div>

          <div className="grid gap-4 xl:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle className="text-base">
                  Central-DMS documents
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                {selected.documents.map((document) => (
                  <div
                    key={document.id}
                    className="flex flex-wrap items-center justify-between gap-3 rounded-md border p-3"
                  >
                    <div>
                      <p className="font-medium">{document.title}</p>
                      <p className="text-xs text-muted-foreground">
                        {document.documentType} · {document.dmsReference}
                        {document.isRequired ? ' · required' : ''}
                        {!document.isCurrent ? ' · retired' : ''}
                      </p>
                    </div>
                    <div className="flex gap-2">
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() =>
                          void download(selected, document.id, document.title)
                        }
                      >
                        <Download className="mr-2 h-4 w-4" />
                        Download
                      </Button>
                      {actions.canRetireDocument &&
                        canManage &&
                        document.isCurrent && (
                          <Button
                            size="sm"
                            variant="destructive"
                            onClick={() =>
                              retireDocumentMutation.mutate({
                                agreementId: selected.id,
                                documentId: document.id,
                              })
                            }
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        )}
                    </div>
                  </div>
                ))}
                {!selected.documents.length && (
                  <p className="text-sm text-muted-foreground">
                    No central-DMS documents are linked to this revision.
                  </p>
                )}
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="text-base">
                  Independently approved extensions
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                {selected.extensions.map((extension) => (
                  <div key={extension.id} className="rounded-md border p-3">
                    <div className="flex flex-wrap items-start justify-between gap-2">
                      <div>
                        <p className="font-medium">
                          Extension {extension.sequenceNumber}
                        </p>
                        <p className="text-xs text-muted-foreground">
                          {formatDate(extension.previousEndUtc)} →{' '}
                          {formatDate(extension.proposedEndUtc)} ·{' '}
                          {extension.submittedByName}
                        </p>
                      </div>
                      <Badge variant={frameworkExtensionTone(extension.status)}>
                        {extension.status}
                      </Badge>
                    </div>
                    <p className="mt-2 text-sm">{extension.reason}</p>
                    {extension.status === 'PendingApproval' && canApprove && (
                      <div className="mt-3 flex gap-2">
                        <Button
                          size="sm"
                          onClick={() =>
                            setExtensionDecision({
                              extension,
                              approve: true,
                            })
                          }
                        >
                          Approve
                        </Button>
                        <Button
                          size="sm"
                          variant="destructive"
                          onClick={() =>
                            setExtensionDecision({
                              extension,
                              approve: false,
                            })
                          }
                        >
                          Reject
                        </Button>
                      </div>
                    )}
                  </div>
                ))}
                {!selected.extensions.length && (
                  <p className="text-sm text-muted-foreground">
                    No term extensions have been requested.
                  </p>
                )}
              </CardContent>
            </Card>
          </div>
        </div>
      )}

      <Dialog
        open={Boolean(editorMode)}
        onOpenChange={(open) => !open && setEditorMode(undefined)}
      >
        <DialogContent className="max-h-[92vh] max-w-6xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>
              {editorMode === 'edit'
                ? 'Edit framework draft'
                : 'Create framework agreement'}
            </DialogTitle>
          </DialogHeader>

          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2 md:col-span-2">
              <Label>Approved award-readiness source</Label>
              <Select
                disabled={editorMode === 'edit'}
                value={
                  editor.sourceId && editor.businessPartnerId
                    ? `${editor.sourceType}:${editor.sourceId}:${editor.businessPartnerId}`
                    : undefined
                }
                onValueChange={selectSource}
              >
                <SelectTrigger data-testid="framework-source-select">
                  <SelectValue placeholder="Select an exact ready source and supplier" />
                </SelectTrigger>
                <SelectContent>
                  {sources.data?.map((source) => (
                    <SelectItem
                      key={`${source.sourceType}:${source.sourceId}:${source.businessPartnerId}`}
                      value={`${source.sourceType}:${source.sourceId}:${source.businessPartnerId}`}
                    >
                      {source.sourceReference} · {source.supplierCode} ·{' '}
                      {source.supplierName} ·{' '}
                      {money(source.awardAmount, source.currencyCode)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label>Agreement title</Label>
              <Input
                value={editor.title}
                onChange={(event) =>
                  setEditor((current) => ({
                    ...current,
                    title: event.target.value,
                  }))
                }
              />
            </div>
            <div className="space-y-2">
              <Label>Ceiling amount</Label>
              <Input
                type="number"
                min="0.01"
                step="0.01"
                value={editor.ceilingAmount}
                onChange={(event) =>
                  setEditor((current) => ({
                    ...current,
                    ceilingAmount: Number(event.target.value),
                  }))
                }
              />
            </div>
            <div className="space-y-2">
              <Label>Currency</Label>
              <Input
                maxLength={3}
                value={editor.currencyCode}
                onChange={(event) =>
                  setEditor((current) => ({
                    ...current,
                    currencyCode: event.target.value.toUpperCase(),
                  }))
                }
              />
            </div>
            <div className="space-y-2">
              <Label>Effective from</Label>
              <Input
                type="datetime-local"
                value={editor.effectiveFromUtc}
                onChange={(event) =>
                  setEditor((current) => ({
                    ...current,
                    effectiveFromUtc: event.target.value,
                  }))
                }
              />
            </div>
            <div className="space-y-2">
              <Label>Effective to</Label>
              <Input
                type="datetime-local"
                value={editor.effectiveToUtc}
                onChange={(event) =>
                  setEditor((current) => ({
                    ...current,
                    effectiveToUtc: event.target.value,
                  }))
                }
              />
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label>Approval workflow</Label>
              <Select
                value={editor.workflowDefinitionId || undefined}
                onValueChange={(value) =>
                  setEditor((current) => ({
                    ...current,
                    workflowDefinitionId: value,
                  }))
                }
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select a published shared workflow" />
                </SelectTrigger>
                <SelectContent>
                  {workflows.data?.map((workflow) => (
                    <SelectItem key={workflow.id} value={workflow.id}>
                      {workflow.name} · v{workflow.version}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Description</Label>
              <Textarea
                value={editor.description}
                onChange={(event) =>
                  setEditor((current) => ({
                    ...current,
                    description: event.target.value,
                  }))
                }
              />
            </div>
            <div className="space-y-2">
              <Label>Terms summary</Label>
              <Textarea
                value={editor.termsSummary}
                onChange={(event) =>
                  setEditor((current) => ({
                    ...current,
                    termsSummary: event.target.value,
                  }))
                }
              />
            </div>
          </div>

          <section className="space-y-3 rounded-md border p-4">
            <div>
              <h3 className="font-medium">Supplier categories</h3>
              <p className="text-xs text-muted-foreground">
                The supplier is revalidated against every selected category.
              </p>
            </div>
            <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
              {categories.data?.map((category) => (
                <label
                  key={category.id}
                  className="flex items-center gap-2 rounded-md border p-2 text-sm"
                >
                  <input
                    type="checkbox"
                    checked={editor.categoryIds.includes(category.id)}
                    onChange={() => toggleCategory(category.id)}
                  />
                  {category.code} · {category.name}
                </label>
              ))}
            </div>
          </section>

          <section className="space-y-3 rounded-md border p-4">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="font-medium">Governed price-list lines</h3>
                <p className="text-xs text-muted-foreground">
                  Published prices become immutable; revisions require cloning.
                </p>
              </div>
              <Button type="button" variant="outline" onClick={addPriceLine}>
                <Plus className="mr-2 h-4 w-4" />
                Price
              </Button>
            </div>
            {editor.priceLines.map((line, index) => (
              <div
                key={`${index}-${line.inventoryItemId}`}
                className="grid gap-3 rounded-md border p-3 md:grid-cols-6"
              >
                <div className="space-y-2 md:col-span-2">
                  <Label>Inventory item</Label>
                  <Select
                    value={line.inventoryItemId || undefined}
                    onValueChange={(value) =>
                      updatePriceLine(index, { inventoryItemId: value })
                    }
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select item" />
                    </SelectTrigger>
                    <SelectContent>
                      {items.data?.map((item) => (
                        <SelectItem key={item.id} value={item.id}>
                          {item.code} · {item.name} · {item.unitOfMeasure}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <NumberField
                  label="Unit price"
                  value={line.unitPrice}
                  onChange={(value) =>
                    updatePriceLine(index, { unitPrice: value })
                  }
                />
                <NumberField
                  label="Minimum qty"
                  value={line.minimumQuantity}
                  onChange={(value) =>
                    updatePriceLine(index, { minimumQuantity: value })
                  }
                />
                <NumberField
                  label="Maximum qty"
                  value={line.maximumQuantity}
                  optional
                  onChange={(value) =>
                    updatePriceLine(index, {
                      maximumQuantity: value || undefined,
                    })
                  }
                />
                <div className="flex items-end gap-2">
                  <NumberField
                    label="Lead days"
                    value={line.leadTimeDays}
                    onChange={(value) =>
                      updatePriceLine(index, {
                        leadTimeDays: Math.max(0, value),
                      })
                    }
                  />
                  <Button
                    type="button"
                    size="icon"
                    variant="destructive"
                    onClick={() =>
                      setEditor((current) => ({
                        ...current,
                        priceLines: current.priceLines.filter(
                          (_, itemIndex) => itemIndex !== index
                        ),
                      }))
                    }
                  >
                    <Trash2 className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            ))}
          </section>

          <section className="space-y-3 rounded-md border p-4">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="font-medium">Call-off authority</h3>
                <p className="text-xs text-muted-foreground">
                  Configuration only; transaction enforcement begins in
                  TDC-0402.
                </p>
              </div>
              <Button type="button" variant="outline" onClick={addAuthority}>
                <Plus className="mr-2 h-4 w-4" />
                Authority
              </Button>
            </div>
            {editor.callOffAuthorities.map((authority, index) => (
              <div
                key={`${index}-${authority.authorityKind}`}
                className="grid gap-3 rounded-md border p-3 md:grid-cols-4"
              >
                <div className="space-y-2">
                  <Label>Kind</Label>
                  <Select
                    value={authority.authorityKind}
                    onValueChange={(value) =>
                      updateAuthority(index, {
                        authorityKind: value as FrameworkAuthorityKind,
                        authorityUserId:
                          value === 'User'
                            ? authority.authorityUserId
                            : undefined,
                      })
                    }
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {authorityKinds.map((kind) => (
                        <SelectItem key={kind} value={kind}>
                          {kind}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>Authority value</Label>
                  <Input
                    placeholder={
                      authority.authorityKind === 'User'
                        ? 'Active tenant user ID'
                        : 'Role, unit or permission code'
                    }
                    value={authority.authorityValue}
                    onChange={(event) =>
                      updateAuthority(index, {
                        authorityValue: event.target.value,
                        authorityUserId:
                          authority.authorityKind === 'User'
                            ? event.target.value
                            : undefined,
                      })
                    }
                  />
                </div>
                <div className="space-y-2">
                  <Label>Display name</Label>
                  <Input
                    value={authority.displayName}
                    onChange={(event) =>
                      updateAuthority(index, {
                        displayName: event.target.value,
                      })
                    }
                  />
                </div>
                <div className="flex items-end gap-2">
                  <NumberField
                    label="Maximum call-off"
                    value={authority.maximumCallOffAmount}
                    optional
                    onChange={(value) =>
                      updateAuthority(index, {
                        maximumCallOffAmount: value || undefined,
                      })
                    }
                  />
                  <Button
                    type="button"
                    size="icon"
                    variant="destructive"
                    onClick={() =>
                      setEditor((current) => ({
                        ...current,
                        callOffAuthorities: current.callOffAuthorities.filter(
                          (_, itemIndex) => itemIndex !== index
                        ),
                      }))
                    }
                  >
                    <Trash2 className="h-4 w-4" />
                  </Button>
                </div>
                <div className="space-y-2">
                  <Label>Valid from</Label>
                  <Input
                    type="datetime-local"
                    value={authority.validFromUtc}
                    onChange={(event) =>
                      updateAuthority(index, {
                        validFromUtc: event.target.value,
                      })
                    }
                  />
                </div>
                <div className="space-y-2">
                  <Label>Valid to</Label>
                  <Input
                    type="datetime-local"
                    value={authority.validToUtc}
                    onChange={(event) =>
                      updateAuthority(index, {
                        validToUtc: event.target.value,
                      })
                    }
                  />
                </div>
              </div>
            ))}
          </section>

          <DialogFooter>
            <Button variant="outline" onClick={() => setEditorMode(undefined)}>
              Cancel
            </Button>
            <Button
              disabled={saveMutation.isPending}
              onClick={() => saveMutation.mutate()}
            >
              <Save className="mr-2 h-4 w-4" />
              Save draft
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
              {lifecycleAction
                ? `${lifecycleAction[0].toUpperCase()}${lifecycleAction.slice(1)} framework agreement`
                : 'Framework lifecycle'}
            </DialogTitle>
          </DialogHeader>
          <LifecycleFields
            comment={comment}
            evidenceReference={evidenceReference}
            onComment={setComment}
            onEvidence={setEvidenceReference}
          />
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setLifecycleAction(undefined)}
            >
              Cancel
            </Button>
            <Button
              variant={
                lifecycleAction === 'reject' || lifecycleAction === 'terminate'
                  ? 'destructive'
                  : 'default'
              }
              disabled={lifecycleMutation.isPending}
              onClick={runLifecycle}
            >
              Confirm
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={cloneOpen} onOpenChange={setCloneOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Create a new agreement revision</DialogTitle>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <DateField
              label="Effective from"
              value={cloneFrom}
              onChange={setCloneFrom}
            />
            <DateField
              label="Effective to"
              value={cloneTo}
              onChange={setCloneTo}
            />
            <div className="space-y-2 sm:col-span-2">
              <Label>Approval workflow</Label>
              <Select
                value={cloneWorkflowId}
                onValueChange={setCloneWorkflowId}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select workflow" />
                </SelectTrigger>
                <SelectContent>
                  {workflows.data?.map((workflow) => (
                    <SelectItem key={workflow.id} value={workflow.id}>
                      {workflow.name} · v{workflow.version}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2 sm:col-span-2">
              <Label>Change summary</Label>
              <Textarea
                value={cloneSummary}
                onChange={(event) => setCloneSummary(event.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCloneOpen(false)}>
              Cancel
            </Button>
            <Button
              disabled={
                !selected ||
                !cloneWorkflowId ||
                !cloneSummary.trim() ||
                cloneMutation.isPending
              }
              onClick={() => selected && cloneMutation.mutate(selected)}
            >
              <Copy className="mr-2 h-4 w-4" />
              Create draft revision
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={documentOpen} onOpenChange={setDocumentOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Register a central-DMS document</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>File</Label>
              <Input
                type="file"
                onChange={(event) => setDocumentFile(event.target.files?.[0])}
              />
              <p className="text-xs text-muted-foreground">
                The shared scanner must report Clean before the document is
                linked.
              </p>
            </div>
            <div className="space-y-2">
              <Label>Document type</Label>
              <Input
                value={documentType}
                onChange={(event) => setDocumentType(event.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label>Title</Label>
              <Input
                placeholder={documentFile?.name ?? 'DMS title'}
                value={documentTitle}
                onChange={(event) => setDocumentTitle(event.target.value)}
              />
            </div>
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={documentRequired}
                onChange={(event) => setDocumentRequired(event.target.checked)}
              />
              Required agreement evidence
            </label>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDocumentOpen(false)}>
              Cancel
            </Button>
            <Button
              disabled={
                !selected ||
                !documentFile ||
                !documentType.trim() ||
                documentMutation.isPending
              }
              onClick={() => selected && documentMutation.mutate(selected)}
            >
              <FilePlus2 className="mr-2 h-4 w-4" />
              Scan & register
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={extensionOpen} onOpenChange={setExtensionOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Request a term extension</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <DateField
              label="Proposed effective end"
              value={extensionEnd}
              onChange={setExtensionEnd}
            />
            <div className="space-y-2">
              <Label>Independent approval workflow</Label>
              <Select
                value={extensionWorkflowId}
                onValueChange={setExtensionWorkflowId}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select workflow" />
                </SelectTrigger>
                <SelectContent>
                  {workflows.data?.map((workflow) => (
                    <SelectItem key={workflow.id} value={workflow.id}>
                      {workflow.name} · v{workflow.version}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Reason</Label>
              <Textarea
                value={extensionReason}
                onChange={(event) => setExtensionReason(event.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label>Retained evidence reference</Label>
              <Input
                value={extensionEvidence}
                onChange={(event) => setExtensionEvidence(event.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setExtensionOpen(false)}>
              Cancel
            </Button>
            <Button
              disabled={
                !selected ||
                !extensionWorkflowId ||
                !extensionReason.trim() ||
                !extensionEvidence.trim() ||
                extensionMutation.isPending
              }
              onClick={() => selected && extensionMutation.mutate(selected)}
            >
              Submit extension
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(extensionDecision)}
        onOpenChange={(open) => !open && setExtensionDecision(undefined)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {extensionDecision?.approve ? 'Approve' : 'Reject'} extension
            </DialogTitle>
          </DialogHeader>
          <LifecycleFields
            comment={extensionDecisionComment}
            evidenceReference={extensionDecisionEvidence}
            onComment={setExtensionDecisionComment}
            onEvidence={setExtensionDecisionEvidence}
          />
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setExtensionDecision(undefined)}
            >
              Cancel
            </Button>
            <Button
              variant={extensionDecision?.approve ? 'default' : 'destructive'}
              disabled={
                !selected ||
                !extensionDecision ||
                !extensionDecisionComment.trim() ||
                !extensionDecisionEvidence.trim() ||
                extensionDecisionMutation.isPending
              }
              onClick={() =>
                selected &&
                extensionDecision &&
                extensionDecisionMutation.mutate({
                  agreement: selected,
                  decision: extensionDecision,
                })
              }
            >
              Confirm decision
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
      <p className="mt-1 break-all text-sm font-medium">{value}</p>
    </div>
  );
}

function NumberField({
  label,
  value,
  optional,
  onChange,
}: {
  label: string;
  value?: number;
  optional?: boolean;
  onChange: (value: number) => void;
}) {
  return (
    <div className="min-w-0 flex-1 space-y-2">
      <Label>
        {label}
        {optional ? ' (optional)' : ''}
      </Label>
      <Input
        type="number"
        min="0"
        step="0.01"
        value={value ?? ''}
        onChange={(event) => onChange(Number(event.target.value))}
      />
    </div>
  );
}

function DateField({
  label,
  value,
  onChange,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <div className="space-y-2">
      <Label>{label}</Label>
      <Input
        type="datetime-local"
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    </div>
  );
}

function LifecycleFields({
  comment,
  evidenceReference,
  onComment,
  onEvidence,
}: {
  comment: string;
  evidenceReference: string;
  onComment: (value: string) => void;
  onEvidence: (value: string) => void;
}) {
  return (
    <div className="space-y-4">
      <div className="space-y-2">
        <Label>Decision comment</Label>
        <Textarea
          value={comment}
          onChange={(event) => onComment(event.target.value)}
        />
      </div>
      <div className="space-y-2">
        <Label>Retained evidence reference</Label>
        <Input
          placeholder="Minute, workflow evidence or external reference"
          value={evidenceReference}
          onChange={(event) => onEvidence(event.target.value)}
        />
      </div>
      <Alert>
        <FileCheck2 className="h-4 w-4" />
        <AlertTitle>Shared control evidence</AlertTitle>
        <AlertDescription>
          The server binds the decision to the shared workflow, independent
          actor, source readiness, supplier eligibility and DEC-001 through
          DEC-014 audit lineage.
        </AlertDescription>
      </Alert>
    </div>
  );
}
