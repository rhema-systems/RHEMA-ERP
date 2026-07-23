'use client';

import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import {
  ArrowLeft,
  CheckCircle2,
  Copy,
  FileText,
  History,
  Save,
  Send,
  ShieldCheck,
  Trash2,
  XCircle,
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import {
  procurementSpecificationTemplateActions,
  procurementSpecificationTemplateStatusTone,
  validateProcurementSpecificationTemplate,
} from '@/lib/procurement-specification-template';
import { procurementSpecificationTemplateService } from '@/services/procurement-specification-template.service';
import type { ProcurementControlEvidenceReferenceKind } from '@/types/procurement-control-event';
import type {
  ProcurementSpecificationEvidenceReference,
  ProcurementSpecificationTemplate,
  ProcurementSpecificationTemplateKind,
  SaveProcurementSpecificationTemplate,
} from '@/types/procurement-specification-template';

type LifecycleAction =
  | 'submit'
  | 'publish'
  | 'reject'
  | 'clone'
  | 'retire'
  | 'delete';

const toInputDate = (value?: string) => {
  if (!value) return '';
  const date = new Date(value);
  date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
  return date.toISOString().slice(0, 16);
};

const fromInputDate = (value?: string) =>
  value ? new Date(value).toISOString() : undefined;

const emptyForm = (): SaveProcurementSpecificationTemplate => ({
  templateCode: '',
  name: '',
  description: '',
  kind: 'Goods',
  isDefault: false,
  effectiveFromUtc: toInputDate(new Date().toISOString()),
  effectiveToUtc: '',
  changeSummary: '',
  purpose: '',
  functionalAndPerformanceRequirements: '',
  processAndMaterialsRequirements: '',
  dimensionsAndMarkingRequirements: '',
  testingAndInspectionRequirements: '',
  applicableStandards: '',
  deliverables: '',
  acceptanceCriteria: '',
  workflowDefinitionId: '',
});

const formFrom = (
  value: ProcurementSpecificationTemplate
): SaveProcurementSpecificationTemplate => ({
  templateCode: value.templateCode,
  name: value.name,
  description: value.description ?? '',
  kind: value.kind,
  isDefault: value.isDefault,
  effectiveFromUtc: toInputDate(value.effectiveFromUtc),
  effectiveToUtc: toInputDate(value.effectiveToUtc),
  changeSummary: value.changeSummary ?? '',
  purpose: value.purpose,
  functionalAndPerformanceRequirements:
    value.functionalAndPerformanceRequirements,
  processAndMaterialsRequirements: value.processAndMaterialsRequirements,
  dimensionsAndMarkingRequirements: value.dimensionsAndMarkingRequirements,
  testingAndInspectionRequirements: value.testingAndInspectionRequirements,
  applicableStandards: value.applicableStandards,
  deliverables: value.deliverables,
  acceptanceCriteria: value.acceptanceCriteria,
  workflowDefinitionId: value.workflowDefinitionId ?? '',
  rowVersion: value.rowVersion,
});

const formatDate = (value?: string) =>
  value
    ? new Intl.DateTimeFormat(undefined, {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(value))
    : '—';

const sectionFields: Array<{
  key: keyof SaveProcurementSpecificationTemplate;
  title: string;
  guidance: string;
}> = [
  {
    key: 'purpose',
    title: '1. Purpose and scope',
    guidance:
      'State the need, intended outcome, scope, exclusions, and operating context.',
  },
  {
    key: 'functionalAndPerformanceRequirements',
    title: '2. Functional and performance requirements',
    guidance:
      'Use measurable outputs, capacity, availability, quality, and service-level targets.',
  },
  {
    key: 'processAndMaterialsRequirements',
    title: '3. Processes and materials',
    guidance:
      'Specify permitted processes and materials, or record a justified not-applicable statement.',
  },
  {
    key: 'dimensionsAndMarkingRequirements',
    title: '4. Dimensions and marking',
    guidance:
      'Cover dimensions, tolerances, packaging, identification, labels, and markings.',
  },
  {
    key: 'testingAndInspectionRequirements',
    title: '5. Testing and inspection',
    guidance:
      'Define inspection stages, samples, methods, witnesses, records, and failed-test treatment.',
  },
  {
    key: 'applicableStandards',
    title: '6. Applicable standards',
    guidance:
      'List applicable Ghanaian, international, industry, safety, and environmental standards.',
  },
  {
    key: 'deliverables',
    title: '7. Deliverables',
    guidance:
      'List goods, works, services, reports, manuals, training, handover, and completion records.',
  },
  {
    key: 'acceptanceCriteria',
    title: '8. Acceptance criteria',
    guidance:
      'Define objective measures, evidence, responsible reviewer, approvals, and sign-off conditions.',
  },
];

export function ProcurementSpecificationTemplateEditor({
  id,
}: {
  id?: string;
}) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { hasRole } = useAuth();
  const { toast } = useToast();
  const [form, setForm] =
    useState<SaveProcurementSpecificationTemplate>(emptyForm);
  const [action, setAction] = useState<LifecycleAction>();
  const [comment, setComment] = useState('');
  const [evidenceKind, setEvidenceKind] =
    useState<ProcurementControlEvidenceReferenceKind>('ExternalReference');
  const [evidenceValue, setEvidenceValue] = useState('');
  const [cloneFrom, setCloneFrom] = useState('');
  const [cloneTo, setCloneTo] = useState('');
  const [cloneSummary, setCloneSummary] = useState('');

  const canManage = [
    'SuperAdmin',
    'TenantAdmin',
    'TDC_PROCUREMENT_OFFICER',
    'TDC_SENIOR_PROCUREMENT_OFFICER',
    'TDC_HEAD_OF_PROCUREMENT',
  ].some(hasRole);
  const canApprove = [
    'SuperAdmin',
    'TenantAdmin',
    'TDC_HEAD_OF_PROCUREMENT',
    'TDC_PROCUREMENT_APPROVER',
  ].some(hasRole);

  const detail = useQuery({
    queryKey: ['procurement-specification-template', id],
    queryFn: () =>
      id
        ? procurementSpecificationTemplateService.get(id)
        : Promise.reject(new Error('No template selected.')),
    enabled: Boolean(id),
  });
  const workflows = useQuery({
    queryKey: ['procurement-specification-template-workflows'],
    queryFn: procurementSpecificationTemplateService.workflowOptions,
  });

  useEffect(() => {
    if (detail.data) setForm(formFrom(detail.data));
  }, [detail.data]);

  const value = detail.data;
  const actions = value
    ? procurementSpecificationTemplateActions(value)
    : undefined;
  const validationError = validateProcurementSpecificationTemplate({
    ...form,
    effectiveFromUtc: fromInputDate(form.effectiveFromUtc) ?? '',
    effectiveToUtc: fromInputDate(form.effectiveToUtc),
  });

  const invalidate = async (templateId?: string) => {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: ['procurement-specification-template-summary'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['procurement-specification-templates'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['procurement-specification-template', templateId ?? id],
      }),
    ]);
  };

  const saveRequest = (): SaveProcurementSpecificationTemplate => ({
    ...form,
    effectiveFromUtc: fromInputDate(form.effectiveFromUtc) ?? '',
    effectiveToUtc: fromInputDate(form.effectiveToUtc),
    description: form.description?.trim() || undefined,
    changeSummary: form.changeSummary?.trim() || undefined,
    workflowDefinitionId: form.workflowDefinitionId || undefined,
    rowVersion: value?.rowVersion,
  });

  const save = useMutation({
    mutationFn: () =>
      id
        ? procurementSpecificationTemplateService.update(id, saveRequest())
        : procurementSpecificationTemplateService.create(saveRequest()),
    onSuccess: async (saved) => {
      await invalidate(saved.id);
      toast({
        title: `${saved.templateCode}/v${saved.version} saved`,
        description:
          'The Draft specification/TOR template is ready for controlled review.',
      });
      if (!id)
        router.replace(
          `/procurement/planning/specification-templates/${saved.id}`
        );
    },
    onError: (error: Error) =>
      toast({
        title: 'Template could not be saved',
        description: error.message,
        variant: 'destructive',
      }),
  });

  const evidence = (): ProcurementSpecificationEvidenceReference[] => {
    if (!evidenceValue.trim()) return [];
    return [
      evidenceKind === 'ExternalReference'
        ? {
            referenceKind: evidenceKind,
            reference: evidenceValue.trim(),
            label: 'Specification-template lifecycle evidence',
            requirementKey: `SPEC_${action?.toUpperCase()}`,
          }
        : {
            referenceKind: evidenceKind,
            referenceId: evidenceValue.trim(),
            label: 'Shared evidence reference',
            requirementKey: `SPEC_${action?.toUpperCase()}`,
          },
    ];
  };

  const lifecycle = useMutation({
    mutationFn: async () => {
      if (!value || !action) throw new Error('Select a lifecycle action.');
      const request = {
        rowVersion: value.rowVersion,
        comment: comment.trim() || undefined,
        evidence: evidence(),
      };
      if (action === 'submit')
        return procurementSpecificationTemplateService.submit(
          value.id,
          request
        );
      if (action === 'publish')
        return procurementSpecificationTemplateService.publish(
          value.id,
          request
        );
      if (action === 'reject')
        return procurementSpecificationTemplateService.reject(
          value.id,
          request
        );
      if (action === 'retire')
        return procurementSpecificationTemplateService.retire(
          value.id,
          request
        );
      if (action === 'delete') {
        await procurementSpecificationTemplateService.deleteDraft(
          value.id,
          request
        );
        return undefined;
      }
      return procurementSpecificationTemplateService.clone(value.id, {
        rowVersion: value.rowVersion,
        effectiveFromUtc: fromInputDate(cloneFrom) ?? '',
        effectiveToUtc: fromInputDate(cloneTo),
        changeSummary: cloneSummary.trim(),
      });
    },
    onSuccess: async (saved) => {
      const completedAction = action;
      setAction(undefined);
      setComment('');
      setEvidenceValue('');
      await invalidate(saved?.id);
      if (completedAction === 'delete') {
        router.push('/procurement/planning/specification-templates');
        return;
      }
      if (completedAction === 'clone' && saved) {
        router.push(
          `/procurement/planning/specification-templates/${saved.id}`
        );
        return;
      }
      toast({
        title: `Template ${completedAction} completed`,
        description:
          'The lifecycle timeline and shared evidence references were updated.',
      });
    },
    onError: (error: Error) =>
      toast({
        title: 'Lifecycle action failed',
        description: error.message,
        variant: 'destructive',
      }),
  });

  const openAction = (next: LifecycleAction) => {
    setAction(next);
    setComment('');
    setEvidenceKind('ExternalReference');
    setEvidenceValue('');
    setCloneFrom(toInputDate(new Date(Date.now() + 86400000).toISOString()));
    setCloneTo('');
    setCloneSummary('');
  };

  const actionValid = useMemo(() => {
    if (action === 'submit')
      return !validationError && Boolean(evidenceValue.trim());
    if (action === 'reject' || action === 'retire')
      return Boolean(comment.trim());
    if (action === 'clone') return Boolean(cloneFrom && cloneSummary.trim());
    return Boolean(action);
  }, [
    action,
    cloneFrom,
    cloneSummary,
    comment,
    evidenceValue,
    validationError,
  ]);

  if (id && detail.isLoading)
    return (
      <Card>
        <CardContent className="p-8 text-muted-foreground">
          Loading specification template…
        </CardContent>
      </Card>
    );
  if (id && detail.isError)
    return (
      <Alert variant="destructive">
        <XCircle className="h-4 w-4" />
        <AlertTitle>Template could not be loaded</AlertTitle>
        <AlertDescription>{(detail.error as Error).message}</AlertDescription>
      </Alert>
    );

  const editable = !id || (actions?.canEdit && canManage);

  return (
    <div className="space-y-6">
      <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-start">
        <div>
          <Button
            variant="ghost"
            size="sm"
            className="mb-2 -ml-3"
            onClick={() =>
              router.push('/procurement/planning/specification-templates')
            }
          >
            <ArrowLeft className="mr-2 h-4 w-4" /> Template register
          </Button>
          <div className="flex flex-wrap items-center gap-3">
            <h1 className="text-3xl font-bold">
              {value
                ? `${value.templateCode}/v${value.version}`
                : 'New specification/TOR template'}
            </h1>
            {value && (
              <Badge
                variant="outline"
                className={procurementSpecificationTemplateStatusTone(
                  value.status
                )}
              >
                {value.status === 'PendingApproval'
                  ? 'Pending approval'
                  : value.status}
              </Badge>
            )}
          </div>
          <p className="mt-1 max-w-4xl text-muted-foreground">
            Controlled Goods, Works, and Services content with versioning,
            independent approval, shared workflow support, and immutable
            evidence history.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          {editable && (
            <Button disabled={save.isPending} onClick={() => save.mutate()}>
              <Save className="mr-2 h-4 w-4" />{' '}
              {save.isPending ? 'Saving…' : 'Save Draft'}
            </Button>
          )}
          {value && canManage && actions?.canSubmit && (
            <Button variant="outline" onClick={() => openAction('submit')}>
              <Send className="mr-2 h-4 w-4" /> Submit
            </Button>
          )}
          {value && canApprove && actions?.canPublish && (
            <Button onClick={() => openAction('publish')}>
              <CheckCircle2 className="mr-2 h-4 w-4" /> Publish
            </Button>
          )}
          {value && canApprove && actions?.canReject && (
            <Button variant="destructive" onClick={() => openAction('reject')}>
              Reject
            </Button>
          )}
          {value && canManage && actions?.canClone && (
            <Button variant="outline" onClick={() => openAction('clone')}>
              <Copy className="mr-2 h-4 w-4" /> Clone
            </Button>
          )}
          {value && canApprove && actions?.canRetire && (
            <Button variant="outline" onClick={() => openAction('retire')}>
              Retire
            </Button>
          )}
          {value && canManage && actions?.canDelete && (
            <Button
              variant="ghost"
              className="text-destructive"
              onClick={() => openAction('delete')}
            >
              <Trash2 className="mr-2 h-4 w-4" /> Delete Draft
            </Button>
          )}
        </div>
      </div>

      {validationError && value?.status === 'Draft' && (
        <Alert>
          <FileText className="h-4 w-4" />
          <AlertTitle>Draft is not ready for submission</AlertTitle>
          <AlertDescription>{validationError}</AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Template identity and control</CardTitle>
          <CardDescription>
            The template code remains stable across versioned replacement
            Drafts.
          </CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          <Field label="Template code">
            <Input
              disabled={!editable}
              value={form.templateCode}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  templateCode: event.target.value.toUpperCase(),
                }))
              }
            />
          </Field>
          <Field label="Template name" className="xl:col-span-2">
            <Input
              disabled={!editable}
              value={form.name}
              onChange={(event) =>
                setForm((current) => ({ ...current, name: event.target.value }))
              }
            />
          </Field>
          <Field label="Type">
            <Select
              disabled={!editable}
              value={form.kind}
              onValueChange={(kind) =>
                setForm((current) => ({
                  ...current,
                  kind: kind as ProcurementSpecificationTemplateKind,
                }))
              }
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {(['Goods', 'Works', 'Services'] as const).map((kind) => (
                  <SelectItem key={kind} value={kind}>
                    {kind}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </Field>
          <Field label="Effective from">
            <Input
              disabled={!editable}
              type="datetime-local"
              value={form.effectiveFromUtc}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  effectiveFromUtc: event.target.value,
                }))
              }
            />
          </Field>
          <Field label="Effective to (optional)">
            <Input
              disabled={!editable}
              type="datetime-local"
              value={form.effectiveToUtc ?? ''}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  effectiveToUtc: event.target.value,
                }))
              }
            />
          </Field>
          <Field label="Shared workflow">
            <Select
              disabled={!editable || workflows.isLoading}
              value={form.workflowDefinitionId || 'none'}
              onValueChange={(workflowDefinitionId) =>
                setForm((current) => ({
                  ...current,
                  workflowDefinitionId:
                    workflowDefinitionId === 'none' ? '' : workflowDefinitionId,
                }))
              }
            >
              <SelectTrigger>
                <SelectValue placeholder="No shared workflow" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No shared workflow</SelectItem>
                {(workflows.data ?? []).map((workflow) => (
                  <SelectItem key={workflow.id} value={workflow.id}>
                    {workflow.name} · v{workflow.version}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </Field>
          <div className="flex items-center gap-2 pt-7">
            <Checkbox
              id="is-default"
              disabled={!editable}
              checked={form.isDefault}
              onCheckedChange={(checked) =>
                setForm((current) => ({
                  ...current,
                  isDefault: checked === true,
                }))
              }
            />
            <Label htmlFor="is-default">Default for this type</Label>
          </div>
          <Field label="Description" className="md:col-span-2 xl:col-span-4">
            <Textarea
              disabled={!editable}
              value={form.description ?? ''}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  description: event.target.value,
                }))
              }
            />
          </Field>
          <Field label="Change summary" className="md:col-span-2 xl:col-span-4">
            <Textarea
              disabled={!editable}
              value={form.changeSummary ?? ''}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  changeSummary: event.target.value,
                }))
              }
              placeholder="Required for replacement versions"
            />
          </Field>
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-2">
        {sectionFields.map((section) => (
          <Card key={section.key}>
            <CardHeader className="pb-3">
              <CardTitle className="text-base">{section.title}</CardTitle>
              <CardDescription>{section.guidance}</CardDescription>
            </CardHeader>
            <CardContent>
              <Textarea
                aria-label={section.title}
                disabled={!editable}
                className="min-h-40"
                value={String(form[section.key] ?? '')}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    [section.key]: event.target.value,
                  }))
                }
              />
            </CardContent>
          </Card>
        ))}
      </div>

      {value && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <History className="h-5 w-5" /> Immutable lifecycle timeline
            </CardTitle>
            <CardDescription>
              Shared control events retain actor, result, integrity hash, and
              evidence references.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {value.timeline.map((event) => (
              <div key={event.id} className="rounded-md border p-4">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="font-medium">
                    {event.action} · {event.actorName}
                  </div>
                  <div className="text-sm text-muted-foreground">
                    {formatDate(event.occurredAtUtc)}
                  </div>
                </div>
                {event.reason && <p className="mt-2 text-sm">{event.reason}</p>}
                <div className="mt-2 font-mono text-xs text-muted-foreground">
                  {event.integrityHash}
                </div>
                {event.evidence.length > 0 && (
                  <div className="mt-2 text-xs text-muted-foreground">
                    Evidence:{' '}
                    {event.evidence
                      .map((item) => item.label || item.reference)
                      .join(', ')}
                  </div>
                )}
              </div>
            ))}
            {!value.timeline.length && (
              <p className="text-sm text-muted-foreground">
                No lifecycle events have been recorded.
              </p>
            )}
          </CardContent>
        </Card>
      )}

      <Dialog
        open={Boolean(action)}
        onOpenChange={(open) => !open && setAction(undefined)}
      >
        <DialogContent className="max-w-xl">
          <DialogHeader>
            <DialogTitle>
              {action ? action[0].toUpperCase() + action.slice(1) : ''}{' '}
              specification template
            </DialogTitle>
            <DialogDescription>
              {value?.templateCode}/v{value?.version}. The action is enforced by
              tenant authorization, lifecycle, and audit controls.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            {action === 'clone' ? (
              <>
                <Field label="Replacement effective from">
                  <Input
                    type="datetime-local"
                    value={cloneFrom}
                    onChange={(event) => setCloneFrom(event.target.value)}
                  />
                </Field>
                <Field label="Replacement effective to (optional)">
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
              </>
            ) : (
              <>
                <Field
                  label={
                    action === 'reject' || action === 'retire'
                      ? 'Comment (required)'
                      : 'Review comment'
                  }
                >
                  <Textarea
                    value={comment}
                    onChange={(event) => setComment(event.target.value)}
                  />
                </Field>
                <div className="grid gap-4 sm:grid-cols-2">
                  <Field label="Evidence type">
                    <Select
                      value={evidenceKind}
                      onValueChange={(kind) =>
                        setEvidenceKind(
                          kind as ProcurementControlEvidenceReferenceKind
                        )
                      }
                    >
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="ExternalReference">
                          External reference
                        </SelectItem>
                        <SelectItem value="FileUploadRecord">
                          Shared file record ID
                        </SelectItem>
                        <SelectItem value="WorkflowEvidenceDocument">
                          Workflow evidence ID
                        </SelectItem>
                      </SelectContent>
                    </Select>
                  </Field>
                  <Field
                    label={
                      action === 'submit'
                        ? 'Evidence reference (required)'
                        : 'Evidence reference (optional)'
                    }
                  >
                    <Input
                      value={evidenceValue}
                      onChange={(event) => setEvidenceValue(event.target.value)}
                    />
                  </Field>
                </div>
                <Alert>
                  <ShieldCheck className="h-4 w-4" />
                  <AlertTitle>Shared evidence control</AlertTitle>
                  <AlertDescription>
                    This page stores references only; it does not recreate
                    file-upload or workflow-evidence controls.
                  </AlertDescription>
                </Alert>
              </>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAction(undefined)}>
              Cancel
            </Button>
            <Button
              variant={
                action === 'reject' || action === 'delete'
                  ? 'destructive'
                  : 'default'
              }
              disabled={!actionValid || lifecycle.isPending}
              onClick={() => lifecycle.mutate()}
            >
              {lifecycle.isPending ? 'Recording…' : 'Confirm action'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function Field({
  label,
  children,
  className = '',
}: {
  label: string;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <div className={`space-y-2 ${className}`}>
      <Label>{label}</Label>
      {children}
    </div>
  );
}
