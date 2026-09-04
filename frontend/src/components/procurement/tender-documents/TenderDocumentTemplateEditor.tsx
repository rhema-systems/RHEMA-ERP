'use client';

import Link from 'next/link';
import { useEffect, useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArrowLeft,
  CheckCircle2,
  Copy,
  FileCheck2,
  History,
  Loader2,
  RefreshCw,
  Save,
  ShieldAlert,
} from 'lucide-react';
import { toast } from 'sonner';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
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
import { TenderDocumentContentArtifactField } from './TenderDocumentContentArtifactField';
import { useAuth } from '@/hooks/use-auth';
import {
  hasAnyTenderDocumentAction,
  applyTenderDocumentContentArtifact,
  procurementMethodLabel,
  tenderDocumentTemplateStatusLabel,
  validateTenderDocumentTemplate,
} from '@/lib/procurement-tender-document';
import { procurementControlEventService } from '@/services/procurement-control-event.service';
import { procurementTenderDocumentService as service } from '@/services/procurement-tender-document.service';
import type {
  ProcurementTenderDocumentLifecycleRequest,
  ProcurementTenderDocumentTemplate,
  SaveProcurementTenderDocumentTemplate,
} from '@/types/procurement-tender-document';
import type { ProcurementMethodType } from '@/types/procurement-policy';

const methods = Object.keys(procurementMethodLabel) as ProcurementMethodType[];

const localInput = (value?: string) => {
  if (!value) return '';
  const date = new Date(value);
  const shifted = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return shifted.toISOString().slice(0, 16);
};

const formatDate = (value?: string) =>
  value ? new Date(value).toLocaleString() : '—';

const toForm = (
  template: ProcurementTenderDocumentTemplate
): SaveProcurementTenderDocumentTemplate => ({
  templateCode: template.templateCode,
  name: template.name,
  description: template.description ?? '',
  documentTypeCode: template.documentTypeCode,
  effectiveFromUtc: localInput(template.effectiveFromUtc),
  effectiveToUtc: localInput(template.effectiveToUtc),
  policySetId: template.policySetId,
  policySetCode: template.policySetCode,
  policySetVersion: template.policySetVersion,
  sourceConfigurationProfileId: template.sourceConfigurationProfileId,
  contentReference: template.contentReference,
  contentWorkflowEvidenceDocumentId:
    template.contentWorkflowEvidenceDocumentId,
  contentFileUploadRecordId: template.contentFileUploadRecordId,
  contentChecksumSha256: template.contentChecksumSha256,
  workflowDefinitionId: template.workflowDefinitionId,
  applicableMethods: template.applicableMethods,
  changeSummary: template.changeSummary ?? '',
  rowVersion: template.rowVersion,
});

type LifecycleAction = 'submit' | 'publish' | 'reject' | 'retire';

export function TenderDocumentTemplateEditor({ id }: { id: string }) {
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const canManage = hasPermission('procurement.tender.administer');
  const canApprove = hasPermission('procurement.tender.approve');
  const [form, setForm] =
    useState<SaveProcurementTenderDocumentTemplate | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [lifecycleAction, setLifecycleAction] =
    useState<LifecycleAction | null>(null);
  const [comment, setComment] = useState('');
  const [evidenceReference, setEvidenceReference] = useState('');
  const [cloneOpen, setCloneOpen] = useState(false);
  const [cloneFrom, setCloneFrom] = useState('');
  const [cloneTo, setCloneTo] = useState('');
  const [cloneSummary, setCloneSummary] = useState('');
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [deleteEvidenceReference, setDeleteEvidenceReference] = useState('');

  const template = useQuery({
    queryKey: ['procurement-tender-document-template', id],
    queryFn: () => service.getTemplate(id),
    enabled: Boolean(id),
  });
  const workflows = useQuery({
    queryKey: ['procurement-tender-document-workflows'],
    queryFn: service.workflowOptions,
  });
  const policies = useQuery({
    queryKey: ['procurement-tender-document-policies'],
    queryFn: service.policyOptions,
  });
  const audit = useQuery({
    queryKey: [
      'procurement-tender-document-template-audit',
      template.data?.templateCode,
    ],
    queryFn: () =>
      procurementControlEventService.search({
        sourceType: 'ProcurementTenderDocumentTemplate',
        sourceReference: template.data?.templateCode,
        page: 1,
        pageSize: 50,
      }),
    enabled: Boolean(template.data?.templateCode),
  });

  useEffect(() => {
    if (template.data) {
      setForm(toForm(template.data));
      setCloneFrom(localInput(template.data.effectiveFromUtc));
    }
  }, [template.data]);

  const actions = template.data?.allowedActions ?? [];
  const allows = (...candidates: string[]) =>
    hasAnyTenderDocumentAction(actions, candidates);
  const editable =
    canManage &&
    allows('Edit', 'Update', 'EditTemplate', 'UpdateTemplate') &&
    template.data?.status === 'Draft';
  const selectedPolicy = useMemo(
    () => policies.data?.find((item) => item.id === form?.policySetId),
    [form?.policySetId, policies.data]
  );

  const refresh = async () => {
    await Promise.all([template.refetch(), audit.refetch()]);
  };

  const run = async (
    key: string,
    action: () => Promise<unknown>,
    success: string
  ) => {
    try {
      setBusy(key);
      await action();
      toast.success(success);
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: ['procurement-tender-document-template'],
        }),
        queryClient.invalidateQueries({
          queryKey: ['procurement-tender-document-templates'],
        }),
        queryClient.invalidateQueries({
          queryKey: ['procurement-tender-document-template-summary'],
        }),
      ]);
      await refresh();
      return true;
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Document action failed'
      );
      return false;
    } finally {
      setBusy(null);
    }
  };

  const save = async () => {
    if (!form) return;
    const validation = validateTenderDocumentTemplate(form);
    if (validation) {
      toast.error(validation);
      return;
    }
    const request: SaveProcurementTenderDocumentTemplate = {
      ...form,
      effectiveFromUtc: new Date(form.effectiveFromUtc).toISOString(),
      effectiveToUtc: form.effectiveToUtc
        ? new Date(form.effectiveToUtc).toISOString()
        : undefined,
      rowVersion: template.data?.rowVersion,
    };
    await run(
      'save',
      () => service.updateTemplate(id, request),
      'Controlled Draft saved'
    );
  };

  const choosePolicy = (policySetId: string) => {
    if (!form) return;
    const policy = policies.data?.find((item) => item.id === policySetId);
    setForm({
      ...form,
      policySetId,
      policySetCode: policy?.code ?? '',
      policySetVersion: policy?.version ?? 0,
      sourceConfigurationProfileId:
        policy?.sourceConfigurationProfileId ?? '',
    });
  };

  const toggleMethod = (method: ProcurementMethodType, checked: boolean) => {
    if (!form) return;
    setForm({
      ...form,
      applicableMethods: checked
        ? [...new Set([...form.applicableMethods, method])]
        : form.applicableMethods.filter((item) => item !== method),
    });
  };

  const lifecycle = async () => {
    if (!template.data || !lifecycleAction) return false;
    if (!evidenceReference.trim()) {
      toast.error('Shared approval evidence reference is required.');
      return false;
    }
    const request: ProcurementTenderDocumentLifecycleRequest = {
      rowVersion: template.data.rowVersion,
      comment: comment.trim() || undefined,
      evidence: [
        {
          referenceKind: 'ExternalReference',
          reference: evidenceReference.trim(),
          label: `${lifecycleAction} tender-document version`,
          requirementKey: 'SRC-006',
        },
      ],
    };
    const calls = {
      submit: () => service.submitTemplate(id, request),
      publish: () => service.publishTemplate(id, request),
      reject: () => service.rejectTemplate(id, request),
      retire: () => service.retireTemplate(id, request),
    };
    const success = {
      submit: 'Template submitted to the exact workflow',
      publish: 'Approved template version published',
      reject: 'Template approval rejected',
      retire: 'Published template version retired',
    };
    const completed = await run(
      lifecycleAction,
      calls[lifecycleAction],
      success[lifecycleAction]
    );
    if (completed) {
      setLifecycleAction(null);
      setComment('');
      setEvidenceReference('');
    }
    return completed;
  };

  const clone = async () => {
    const currentTemplate = template.data;
    if (!currentTemplate || !cloneSummary.trim() || !cloneFrom) {
      toast.error('Effective-from date and change summary are required.');
      return false;
    }
    const completed = await run(
      'clone',
      () =>
        service.cloneTemplate(id, {
          rowVersion: currentTemplate.rowVersion,
          effectiveFromUtc: new Date(cloneFrom).toISOString(),
          effectiveToUtc: cloneTo
            ? new Date(cloneTo).toISOString()
            : undefined,
          changeSummary: cloneSummary,
        }),
      'New Draft version cloned'
    );
    if (completed) {
      setCloneOpen(false);
      setCloneSummary('');
    }
    return completed;
  };

  if (template.isLoading || !form)
    return (
      <div className="flex min-h-[50vh] items-center justify-center">
        <Loader2 className="h-7 w-7 animate-spin" />
      </div>
    );

  if (template.isError || !template.data)
    return (
      <div className="space-y-4 p-6">
        <Button asChild variant="ghost">
          <Link href="/procurement/tender-documents">
            <ArrowLeft className="mr-2 h-4 w-4" /> Controlled documents
          </Link>
        </Button>
        <Alert variant="destructive">
          <AlertTitle>Controlled template unavailable</AlertTitle>
          <AlertDescription>
            The record is missing, outside the current tenant, or not visible
            to this account.
          </AlertDescription>
        </Alert>
      </div>
    );

  const item = template.data;

  return (
    <div
      className="space-y-6 p-6"
      data-testid="tender-document-template-detail-page"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <Button asChild variant="ghost" className="mb-2 px-0">
            <Link href="/procurement/tender-documents">
              <ArrowLeft className="mr-2 h-4 w-4" /> Controlled documents
            </Link>
          </Button>
          <h1 className="text-2xl font-semibold">
            {item.templateCode} · v{item.version}
          </h1>
          <p className="text-sm text-muted-foreground">{item.name}</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Badge variant={item.status === 'Published' ? 'default' : 'secondary'}>
            {tenderDocumentTemplateStatusLabel[item.status]}
          </Badge>
          <Button variant="outline" size="sm" onClick={() => void refresh()}>
            <RefreshCw className="mr-2 h-4 w-4" /> Refresh
          </Button>
          {editable && (
            <Button size="sm" disabled={busy !== null} onClick={() => void save()}>
              <Save className="mr-2 h-4 w-4" /> Save Draft
            </Button>
          )}
        </div>
      </div>

      {item.blockedReasons.length > 0 && (
        <Alert>
          <ShieldAlert className="h-4 w-4" />
          <AlertTitle>Current blocked reasons</AlertTitle>
          <AlertDescription>
            <ul className="list-disc space-y-1 pl-4">
              {item.blockedReasons.map((reason) => (
                <li key={reason}>{reason}</li>
              ))}
            </ul>
          </AlertDescription>
        </Alert>
      )}

      <div className="grid gap-3 md:grid-cols-4">
        <Summary label="Template family" value={item.templateKey} />
        <Summary
          label="Policy"
          value={`${item.policySetCode} · v${item.policySetVersion}`}
        />
        <Summary label="Source configuration" value={item.sourceConfigurationProfileId} />
        <Summary
          label="Integrity"
          value={`${item.integrityHash.slice(0, 16)}…`}
        />
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Controlled version content</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-2">
            <Field label="Template code">
              <Input
                disabled={!editable}
                value={form.templateCode}
                onChange={(event) =>
                  setForm({ ...form, templateCode: event.target.value })
                }
              />
            </Field>
            <Field label="Document type code">
              <Input
                disabled={!editable}
                value={form.documentTypeCode}
                onChange={(event) =>
                  setForm({ ...form, documentTypeCode: event.target.value })
                }
              />
            </Field>
            <div className="md:col-span-2">
              <Field label="Name">
                <Input
                  disabled={!editable}
                  value={form.name}
                  onChange={(event) =>
                    setForm({ ...form, name: event.target.value })
                  }
                />
              </Field>
            </div>
            <div className="md:col-span-2">
              <Field label="Description">
                <Textarea
                  disabled={!editable}
                  rows={3}
                  value={form.description}
                  onChange={(event) =>
                    setForm({ ...form, description: event.target.value })
                  }
                />
              </Field>
            </div>
            <div className="md:col-span-2">
              <TenderDocumentContentArtifactField
                value={form.contentWorkflowEvidenceDocumentId}
                contentReference={form.contentReference}
                checksumSha256={form.contentChecksumSha256}
                disabled={!editable}
                onSelect={(artifact) =>
                  setForm((current) =>
                    current
                      ? applyTenderDocumentContentArtifact(current, artifact)
                      : current
                  )
                }
              />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Approval and effective lineage</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-2">
            <div className="md:col-span-2">
              <Field label="Exact Published procurement policy">
                <Select
                  disabled={!editable}
                  value={form.policySetId}
                  onValueChange={choosePolicy}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {(policies.data ?? []).map((policy) => (
                      <SelectItem key={policy.id} value={policy.id}>
                        {policy.code} · v{policy.version} ·{' '}
                        {policy.sourceConfigurationProfileCode}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
              {selectedPolicy && (
                <p className="mt-1 text-xs text-muted-foreground">
                  Source configuration:{' '}
                  {selectedPolicy.sourceConfigurationProfileCode}
                </p>
              )}
            </div>
            <div className="md:col-span-2">
              <Field label="Exact Published workflow">
                <Select
                  disabled={!editable}
                  value={form.workflowDefinitionId}
                  onValueChange={(workflowDefinitionId) =>
                    setForm({ ...form, workflowDefinitionId })
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {(workflows.data ?? []).map((workflow) => (
                      <SelectItem key={workflow.id} value={workflow.id}>
                        {workflow.name} · v{workflow.version}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
            </div>
            <Field label="Effective from">
              <Input
                disabled={!editable}
                type="datetime-local"
                value={form.effectiveFromUtc}
                onChange={(event) =>
                  setForm({ ...form, effectiveFromUtc: event.target.value })
                }
              />
            </Field>
            <Field label="Effective to">
              <Input
                disabled={!editable}
                type="datetime-local"
                value={form.effectiveToUtc ?? ''}
                onChange={(event) =>
                  setForm({
                    ...form,
                    effectiveToUtc: event.target.value || undefined,
                  })
                }
              />
            </Field>
            <div className="md:col-span-2">
              <Field label="Change summary">
                <Textarea
                  disabled={!editable}
                  value={form.changeSummary}
                  onChange={(event) =>
                    setForm({ ...form, changeSummary: event.target.value })
                  }
                />
              </Field>
            </div>
            <Line label="Workflow instance" value={item.workflowInstanceId} />
            <Line
              label="Approval evidence"
              value={item.approvalEvidenceReference}
            />
            <Line label="Submitted" value={formatDate(item.submittedAtUtc)} />
            <Line label="Published" value={formatDate(item.publishedAtUtc)} />
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Applicable procurement methods</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
          {methods.map((method) => (
            <label
              key={method}
              className="flex items-center gap-2 rounded border p-3 text-sm"
            >
              <Checkbox
                disabled={!editable}
                checked={form.applicableMethods.includes(method)}
                onCheckedChange={(checked) =>
                  toggleMethod(method, Boolean(checked))
                }
              />
              {procurementMethodLabel[method]}
            </label>
          ))}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <History className="h-5 w-5" /> Immutable control-event history
          </CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Action</TableHead>
                <TableHead>Result</TableHead>
                <TableHead>Actor</TableHead>
                <TableHead>Reason</TableHead>
                <TableHead>Occurred</TableHead>
                <TableHead>Integrity</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(audit.data?.items ?? []).map((event) => (
                <TableRow key={event.id}>
                  <TableCell>{event.action}</TableCell>
                  <TableCell>
                    <Badge variant="outline">{event.result}</Badge>
                  </TableCell>
                  <TableCell>{event.actorName}</TableCell>
                  <TableCell className="max-w-80">{event.reason || '—'}</TableCell>
                  <TableCell>{formatDate(event.occurredAtUtc)}</TableCell>
                  <TableCell className="font-mono text-xs">
                    {event.integrityHash.slice(0, 12)}…
                  </TableCell>
                </TableRow>
              ))}
              {!audit.isLoading && !audit.data?.items.length && (
                <TableRow>
                  <TableCell
                    colSpan={6}
                    className="py-8 text-center text-muted-foreground"
                  >
                    No immutable control events are visible yet.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <div className="flex flex-wrap gap-2">
        {canManage &&
          allows('Submit', 'SubmitTemplate') &&
          item.status === 'Draft' && (
            <Button onClick={() => setLifecycleAction('submit')}>
              Submit exact workflow
            </Button>
          )}
        {canApprove &&
          allows('Publish', 'PublishTemplate') &&
          item.status === 'PendingApproval' && (
            <Button onClick={() => setLifecycleAction('publish')}>
              Publish approved version
            </Button>
          )}
        {canApprove &&
          allows('Reject', 'RejectTemplate') &&
          item.status === 'PendingApproval' && (
            <Button
              variant="destructive"
              onClick={() => setLifecycleAction('reject')}
            >
              Reject
            </Button>
          )}
        {canManage &&
          allows('Clone', 'CloneTemplate') &&
          (item.status === 'Published' || item.status === 'Retired') && (
            <Button variant="outline" onClick={() => setCloneOpen(true)}>
              <Copy className="mr-2 h-4 w-4" /> Clone new Draft
            </Button>
          )}
        {canApprove &&
          allows('Retire', 'RetireTemplate') &&
          item.status === 'Published' && (
            <Button variant="outline" onClick={() => setLifecycleAction('retire')}>
              Retire version
            </Button>
          )}
        {canManage &&
          allows('Delete', 'DeleteDraft') &&
          item.status === 'Draft' && (
            <Button variant="destructive" onClick={() => setDeleteOpen(true)}>
              Delete unused Draft
            </Button>
          )}
      </div>

      {(item.status === 'Published' || item.status === 'Retired') && (
        <Alert>
          <FileCheck2 className="h-4 w-4" />
          <AlertTitle>Immutable approved history</AlertTitle>
          <AlertDescription>
            Content, checksum, policy, workflow, methods and evidence are
            read-only. Create a new Draft version to make a controlled change.
          </AlertDescription>
        </Alert>
      )}

      <Dialog
        open={Boolean(lifecycleAction)}
        onOpenChange={(open) => !open && setLifecycleAction(null)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {lifecycleAction
                ? `${lifecycleAction[0].toUpperCase()}${lifecycleAction.slice(1)} controlled version`
                : 'Lifecycle action'}
            </DialogTitle>
            <DialogDescription>
              Reference evidence already managed by the shared evidence or
              workflow platform.
            </DialogDescription>
          </DialogHeader>
          <Field label="Shared evidence reference">
            <Input
              value={evidenceReference}
              onChange={(event) => setEvidenceReference(event.target.value)}
            />
          </Field>
          <Field label="Comment">
            <Textarea
              value={comment}
              onChange={(event) => setComment(event.target.value)}
            />
          </Field>
          <DialogFooter>
            <Button variant="outline" onClick={() => setLifecycleAction(null)}>
              Cancel
            </Button>
            <Button
              variant={lifecycleAction === 'reject' ? 'destructive' : 'default'}
              disabled={busy !== null || !evidenceReference.trim()}
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
            <DialogTitle>Clone new controlled Draft</DialogTitle>
            <DialogDescription>
              The stable template family is retained while approval and
              evidence reset for the next version.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="Effective from">
              <Input
                type="datetime-local"
                value={cloneFrom}
                onChange={(event) => setCloneFrom(event.target.value)}
              />
            </Field>
            <Field label="Effective to">
              <Input
                type="datetime-local"
                value={cloneTo}
                onChange={(event) => setCloneTo(event.target.value)}
              />
            </Field>
            <div className="md:col-span-2">
              <Field label="Change summary">
                <Textarea
                  value={cloneSummary}
                  onChange={(event) => setCloneSummary(event.target.value)}
                />
              </Field>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCloneOpen(false)}>
              Cancel
            </Button>
            <Button
              disabled={busy !== null}
              onClick={() => void clone()}
            >
              Clone Draft
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={deleteOpen}
        onOpenChange={(open) => {
          setDeleteOpen(open);
          if (!open) setDeleteEvidenceReference('');
        }}
        title="Delete unused Draft?"
        description="Only an unissued Draft may be deleted. Published, retired, bound, or referenced versions remain immutable."
        confirmText="Delete Draft"
        variant="destructive"
        isLoading={busy === 'delete'}
        confirmDisabled={!deleteEvidenceReference.trim()}
        onConfirm={async () => {
          const completed = await run(
            'delete',
            () =>
              service.deleteDraft(id, {
                rowVersion: item.rowVersion,
                comment: 'Delete unused controlled Draft',
                evidence: [
                  {
                    referenceKind: 'ExternalReference',
                    reference: deleteEvidenceReference.trim(),
                    label: 'Draft deletion confirmation',
                    requirementKey: 'SRC-006',
                  },
                ],
              }),
            'Unused Draft deleted'
          );
          if (completed) {
            window.location.href = '/procurement/tender-documents';
          }
          return completed;
        }}
      >
        <Field label="Shared deletion evidence reference">
          <Input
            value={deleteEvidenceReference}
            onChange={(event) =>
              setDeleteEvidenceReference(event.target.value)
            }
          />
        </Field>
      </ConfirmationDialog>
    </div>
  );
}

function Field({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-2">
      <Label>{label}</Label>
      {children}
    </div>
  );
}

function Summary({ label, value }: { label: string; value: string }) {
  return (
    <Card>
      <CardContent className="p-4">
        <p className="text-xs uppercase text-muted-foreground">{label}</p>
        <p className="mt-1 break-all text-sm font-medium">{value}</p>
      </CardContent>
    </Card>
  );
}

function Line({ label, value }: { label: string; value?: string }) {
  return (
    <div className="text-sm">
      <span className="text-muted-foreground">{label}: </span>
      <span>{value || '—'}</span>
    </div>
  );
}
