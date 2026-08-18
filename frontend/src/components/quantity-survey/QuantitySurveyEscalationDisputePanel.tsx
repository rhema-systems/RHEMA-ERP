'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Download,
  FileArchive,
  FileText,
  History,
  Plus,
  RefreshCw,
  Send,
  ShieldCheck,
  Upload,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
  quantitySurveyEscalationDisputeService,
  type QuantitySurveyEscalationDispute,
  type QuantitySurveyEscalationDisputeAttachmentType,
  type QuantitySurveyEscalationDisputeOutcome,
} from '@/services/quantity-survey-escalation-dispute.service';

const newId = () => crypto.randomUUID();
const dateTime = (value?: string | null) =>
  value
    ? new Intl.DateTimeFormat('en-GB', {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(value))
    : '—';
const enumText = (
  value: string | number | null | undefined,
  names: string[]
) =>
  typeof value === 'number' ? (names[value] ?? String(value)) : (value ?? '—');
const download = (blob: Blob, fileName: string) => {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
};

export function QuantitySurveyEscalationDisputePanel() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const client = useQueryClient();
  const canRead = hasPermission('quantity-survey.workspace.read');
  const canManage = hasPermission('quantity-survey.claims.manage');
  const canApprove = hasPermission('quantity-survey.transactions.approve');
  const canAudit = hasPermission('quantity-survey.audit.read');
  const [status, setStatus] = useState('all');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [openDialog, setOpenDialog] = useState(false);
  const [calculationRunId, setCalculationRunId] = useState('');
  const [subject, setSubject] = useState('');
  const [reason, setReason] = useState('');
  const [openRequestId, setOpenRequestId] = useState(newId);
  const [responseOpen, setResponseOpen] = useState(false);
  const [response, setResponse] = useState('');
  const [responseRequestId, setResponseRequestId] = useState(newId);
  const [resolveOpen, setResolveOpen] = useState(false);
  const [outcome, setOutcome] =
    useState<QuantitySurveyEscalationDisputeOutcome>('Accepted');
  const [resolutionNotes, setResolutionNotes] = useState('');
  const [resolutionRequestId, setResolutionRequestId] = useState(newId);
  const [attachmentOpen, setAttachmentOpen] = useState(false);
  const [attachmentType, setAttachmentType] =
    useState<QuantitySurveyEscalationDisputeAttachmentType>(
      'ContractorSubmission'
    );
  const [attachmentTitle, setAttachmentTitle] = useState('');
  const [attachmentFile, setAttachmentFile] = useState<File | null>(null);
  const [attachmentRequestId, setAttachmentRequestId] = useState(newId);
  const [historyOpen, setHistoryOpen] = useState(false);

  const lookups = useQuery({
    queryKey: ['qs-escalation-dispute-lookups'],
    queryFn: quantitySurveyEscalationDisputeService.calculationLookups,
    enabled: canRead,
  });
  const disputes = useQuery({
    queryKey: ['qs-escalation-disputes', status],
    queryFn: () =>
      quantitySurveyEscalationDisputeService.list({
        status: status === 'all' ? undefined : status,
        page: 1,
        pageSize: 200,
      }),
    enabled: canRead,
  });
  const detail = useQuery({
    queryKey: ['qs-escalation-dispute', selectedId],
    queryFn: () => quantitySurveyEscalationDisputeService.get(selectedId!),
    enabled: Boolean(canRead && selectedId),
  });
  const history = useQuery({
    queryKey: ['qs-escalation-dispute-history', selectedId],
    queryFn: () => quantitySurveyEscalationDisputeService.history(selectedId!),
    enabled: Boolean(canAudit && selectedId && historyOpen),
  });

  const invalidate = async (id?: string) => {
    await Promise.all([
      client.invalidateQueries({ queryKey: ['qs-escalation-dispute-lookups'] }),
      client.invalidateQueries({ queryKey: ['qs-escalation-disputes'] }),
      client.invalidateQueries({
        queryKey: ['qs-escalation-dispute', id ?? selectedId],
      }),
      client.invalidateQueries({
        queryKey: ['qs-escalation-dispute-history', id ?? selectedId],
      }),
    ]);
  };
  const showError = (title: string) => (error: Error) =>
    toast({ title, description: error.message, variant: 'destructive' });

  const openMutation = useMutation({
    mutationFn: () =>
      quantitySurveyEscalationDisputeService.open({
        clientRequestId: openRequestId,
        calculationRunId,
        subject: subject.trim(),
        disputeReason: reason.trim(),
      }),
    onSuccess: async (value) => {
      setOpenDialog(false);
      setCalculationRunId('');
      setSubject('');
      setReason('');
      setOpenRequestId(newId());
      setSelectedId(value.id);
      await invalidate(value.id);
      toast({ title: 'Escalation dispute opened', variant: 'success' });
    },
    onError: showError('Unable to open escalation dispute'),
  });
  const responseMutation = useMutation({
    mutationFn: () =>
      quantitySurveyEscalationDisputeService.respond(detail.data!.id, {
        clientRequestId: responseRequestId,
        rowVersion: detail.data!.rowVersion,
        response: response.trim(),
      }),
    onSuccess: async (value) => {
      setResponseOpen(false);
      setResponse('');
      setResponseRequestId(newId());
      await invalidate(value.id);
      toast({ title: 'Contractor response retained', variant: 'success' });
    },
    onError: showError('Unable to retain contractor response'),
  });
  const resolveMutation = useMutation({
    mutationFn: () =>
      quantitySurveyEscalationDisputeService.resolve(detail.data!.id, {
        clientRequestId: resolutionRequestId,
        rowVersion: detail.data!.rowVersion,
        outcome,
        resolutionNotes: resolutionNotes.trim(),
      }),
    onSuccess: async (value) => {
      setResolveOpen(false);
      setResolutionNotes('');
      setResolutionRequestId(newId());
      await invalidate(value.id);
      toast({ title: 'Escalation dispute resolved', variant: 'success' });
    },
    onError: showError('Unable to resolve escalation dispute'),
  });
  const attachmentMutation = useMutation({
    mutationFn: () =>
      quantitySurveyEscalationDisputeService.addAttachment(detail.data!.id, {
        clientRequestId: attachmentRequestId,
        attachmentType,
        title: attachmentTitle.trim(),
        file: attachmentFile!,
      }),
    onSuccess: async () => {
      setAttachmentOpen(false);
      setAttachmentTitle('');
      setAttachmentFile(null);
      setAttachmentRequestId(newId());
      await invalidate(detail.data!.id);
      toast({
        title: 'Clean scanned evidence retained in central DMS',
        variant: 'success',
      });
    },
    onError: showError('Unable to attach dispute evidence'),
  });
  const exportMutation = useMutation({
    mutationFn: async (format: 'pdf' | 'zip') => ({
      format,
      blob: await quantitySurveyEscalationDisputeService.auditPack(
        detail.data!.id,
        format
      ),
    }),
    onSuccess: ({ format, blob }) =>
      download(blob, `${detail.data!.disputeReference}-audit-pack.${format}`),
    onError: showError('Unable to export dispute audit pack'),
  });

  if (!canRead)
    return (
      <Card className="border-destructive">
        <CardContent className="pt-6 text-sm text-destructive">
          You do not have permission to view escalation disputes.
        </CardContent>
      </Card>
    );
  const current = detail.data;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h2 className="text-lg font-semibold">
            Escalation disputes and audit packs
          </h2>
          <p className="text-sm text-muted-foreground">
            Disputes remain bound to the approved calculation version, Works
            contractor, central DMS evidence, independent outcome, and immutable
            history.
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => invalidate()}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
          {canManage && (
            <Button
              onClick={() => setOpenDialog(true)}
              disabled={!lookups.data?.length}
            >
              <Plus className="mr-2 h-4 w-4" />
              Open dispute
            </Button>
          )}
        </div>
      </div>

      <Card>
        <CardHeader className="pb-3">
          <div className="flex items-center justify-between gap-3">
            <CardTitle className="text-base">Dispute register</CardTitle>
            <Select value={status} onValueChange={setStatus}>
              <SelectTrigger className="w-52">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All statuses</SelectItem>
                <SelectItem value="Open">Open</SelectItem>
                <SelectItem value="ContractorResponded">
                  Contractor responded
                </SelectItem>
                <SelectItem value="Resolved">Resolved</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardHeader>
        <CardContent className="overflow-x-auto">
          <table className="w-full min-w-[800px] text-sm">
            <thead>
              <tr className="border-b text-left text-xs uppercase text-muted-foreground">
                <th className="p-2">Reference</th>
                <th className="p-2">Project / contract</th>
                <th className="p-2">Contractor</th>
                <th className="p-2">Subject</th>
                <th className="p-2">Status</th>
                <th className="p-2">Opened</th>
                <th className="p-2 text-right">Action</th>
              </tr>
            </thead>
            <tbody>
              {(disputes.data?.items ?? []).map((item) => (
                <tr key={item.id} className="border-b">
                  <td className="p-2 font-medium">
                    {item.disputeReference}
                    <div className="text-xs text-muted-foreground">
                      {item.calculationRunReference}
                    </div>
                  </td>
                  <td className="p-2">
                    {item.projectCode}
                    <div className="text-xs text-muted-foreground">
                      {item.contractNumber}
                    </div>
                  </td>
                  <td className="p-2">{item.contractorName}</td>
                  <td className="max-w-xs p-2">
                    <span className="line-clamp-2">{item.subject}</span>
                  </td>
                  <td className="p-2">
                    <Badge
                      variant={
                        item.status === 'Resolved' ? 'secondary' : 'default'
                      }
                    >
                      {item.status}
                    </Badge>
                  </td>
                  <td className="p-2">{dateTime(item.openedAt)}</td>
                  <td className="p-2 text-right">
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => setSelectedId(item.id)}
                    >
                      Review
                    </Button>
                  </td>
                </tr>
              ))}
              {!disputes.isLoading && !disputes.data?.items.length && (
                <tr>
                  <td
                    colSpan={7}
                    className="p-8 text-center text-muted-foreground"
                  >
                    No escalation disputes match the selected status.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </CardContent>
      </Card>

      <Dialog
        open={Boolean(selectedId)}
        onOpenChange={(value) => !value && setSelectedId(null)}
      >
        <DialogContent className="max-h-[90vh] max-w-5xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>
              {current?.disputeReference ?? 'Escalation dispute'}
            </DialogTitle>
            <DialogDescription>
              {current
                ? `${current.projectCode} · ${current.contractNumber} · ${current.contractorName}`
                : 'Loading governed dispute record…'}
            </DialogDescription>
          </DialogHeader>
          {current && (
            <div className="space-y-4 text-sm">
              <div className="grid gap-3 md:grid-cols-4">
                <Meta label="Status" value={current.status} />
                <Meta
                  label="Calculation"
                  value={current.calculationRunReference}
                />
                <Meta label="Opened" value={dateTime(current.openedAt)} />
                <Meta
                  label="Outcome"
                  value={enumText(current.outcome, [
                    'Accepted',
                    'Partially accepted',
                    'Rejected',
                  ])}
                />
              </div>
              <section className="rounded-md border p-3">
                <h3 className="font-semibold">{current.subject}</h3>
                <p className="mt-2 whitespace-pre-wrap text-muted-foreground">
                  {current.disputeReason}
                </p>
              </section>
              <section className="rounded-md border p-3">
                <h3 className="font-semibold">Contractor response</h3>
                <p className="mt-2 whitespace-pre-wrap text-muted-foreground">
                  {current.contractorResponse ||
                    'No response has been recorded.'}
                </p>
                {current.contractorRespondedAt && (
                  <p className="mt-2 text-xs">
                    Recorded {dateTime(current.contractorRespondedAt)}
                  </p>
                )}
              </section>
              <section className="rounded-md border p-3">
                <h3 className="font-semibold">Resolution</h3>
                <p className="mt-2 whitespace-pre-wrap text-muted-foreground">
                  {current.resolutionNotes || 'Pending independent resolution.'}
                </p>
                {current.resolvedAt && (
                  <p className="mt-2 text-xs">
                    Resolved {dateTime(current.resolvedAt)}
                  </p>
                )}
              </section>
              <section className="rounded-md border p-3">
                <div className="mb-3 flex items-center justify-between">
                  <h3 className="font-semibold">Central-DMS evidence</h3>
                  {canManage && current.status !== 'Resolved' && (
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => setAttachmentOpen(true)}
                    >
                      <Upload className="mr-2 h-4 w-4" />
                      Attach
                    </Button>
                  )}
                </div>
                <div className="space-y-2">
                  {current.attachments.map((item) => (
                    <div
                      key={item.id}
                      className="flex items-center justify-between gap-2 rounded border p-2"
                    >
                      <div>
                        <div className="font-medium">{item.title}</div>
                        <div className="text-xs text-muted-foreground">
                          {enumText(item.attachmentType, [
                            'Contractor submission',
                            'TDC review',
                            'Resolution evidence',
                          ])}{' '}
                          · {item.originalFileName} ·{' '}
                          {(item.fileSize / 1024).toFixed(1)} KB
                        </div>
                        <div className="max-w-xl truncate font-mono text-[10px] text-muted-foreground">
                          {item.checksumSha256}
                        </div>
                      </div>
                      {canAudit && (
                        <Button
                          size="icon"
                          variant="ghost"
                          title="Download evidence"
                          onClick={async () =>
                            download(
                              await quantitySurveyEscalationDisputeService.attachmentContent(
                                current.id,
                                item.id
                              ),
                              item.originalFileName
                            )
                          }
                        >
                          <Download className="h-4 w-4" />
                        </Button>
                      )}
                    </div>
                  ))}
                  {!current.attachments.length && (
                    <p className="text-muted-foreground">
                      No evidence files attached.
                    </p>
                  )}
                </div>
              </section>
              <div className="flex flex-wrap justify-end gap-2">
                {canAudit && (
                  <Button
                    variant="outline"
                    onClick={() => setHistoryOpen(true)}
                  >
                    <History className="mr-2 h-4 w-4" />
                    History
                  </Button>
                )}
                {canManage && current.status === 'Open' && (
                  <Button
                    variant="outline"
                    onClick={() => setResponseOpen(true)}
                  >
                    <Send className="mr-2 h-4 w-4" />
                    Record response
                  </Button>
                )}
                {canApprove && current.status === 'ContractorResponded' && (
                  <Button onClick={() => setResolveOpen(true)}>
                    <ShieldCheck className="mr-2 h-4 w-4" />
                    Resolve independently
                  </Button>
                )}
                {canAudit && current.status === 'Resolved' && (
                  <Button
                    variant="outline"
                    onClick={() => exportMutation.mutate('pdf')}
                  >
                    <FileText className="mr-2 h-4 w-4" />
                    PDF
                  </Button>
                )}
                {canAudit && current.status === 'Resolved' && (
                  <Button
                    variant="outline"
                    onClick={() => exportMutation.mutate('zip')}
                  >
                    <FileArchive className="mr-2 h-4 w-4" />
                    ZIP + evidence
                  </Button>
                )}
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={openDialog} onOpenChange={setOpenDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Open escalation dispute</DialogTitle>
            <DialogDescription>
              Select an approved calculation. Its project, contract, contractor,
              formula version, inputs, and snapshot are fixed by the system.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <Field label="Approved calculation">
              <Select
                value={calculationRunId}
                onValueChange={(value) => {
                  setCalculationRunId(value);
                  setOpenRequestId(newId());
                }}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select approved calculation" />
                </SelectTrigger>
                <SelectContent>
                  {(lookups.data ?? []).map((item) => (
                    <SelectItem key={item.id} value={item.id}>
                      {item.group} · {item.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </Field>
            <Field label="Subject">
              <Input
                value={subject}
                onChange={(event) => {
                  setSubject(event.target.value);
                  setOpenRequestId(newId());
                }}
                maxLength={200}
              />
            </Field>
            <Field label="Dispute reason">
              <Textarea
                value={reason}
                onChange={(event) => {
                  setReason(event.target.value);
                  setOpenRequestId(newId());
                }}
                maxLength={4000}
                rows={5}
              />
            </Field>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpenDialog(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => openMutation.mutate()}
              disabled={
                !calculationRunId ||
                subject.trim().length < 5 ||
                reason.trim().length < 10 ||
                openMutation.isPending
              }
            >
              Open dispute
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <Dialog open={responseOpen} onOpenChange={setResponseOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record contractor response</DialogTitle>
            <DialogDescription>
              This becomes part of the immutable dispute history. The responding
              user cannot later resolve the dispute.
            </DialogDescription>
          </DialogHeader>
          <Field label="Contractor response">
            <Textarea
              value={response}
              onChange={(event) => {
                setResponse(event.target.value);
                setResponseRequestId(newId());
              }}
              maxLength={4000}
              rows={7}
            />
          </Field>
          <DialogFooter>
            <Button variant="outline" onClick={() => setResponseOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => responseMutation.mutate()}
              disabled={
                response.trim().length < 10 || responseMutation.isPending
              }
            >
              Retain response
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <Dialog open={resolveOpen} onOpenChange={setResolveOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Independent dispute resolution</DialogTitle>
            <DialogDescription>
              The resolver must differ from both the dispute opener and
              contractor-response recorder.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <Field label="Controlled outcome">
              <Select
                value={outcome}
                onValueChange={(value) => {
                  setOutcome(value as QuantitySurveyEscalationDisputeOutcome);
                  setResolutionRequestId(newId());
                }}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Accepted">Accepted</SelectItem>
                  <SelectItem value="PartiallyAccepted">
                    Partially accepted
                  </SelectItem>
                  <SelectItem value="Rejected">Rejected</SelectItem>
                </SelectContent>
              </Select>
            </Field>
            <Field label="Resolution notes">
              <Textarea
                value={resolutionNotes}
                onChange={(event) => {
                  setResolutionNotes(event.target.value);
                  setResolutionRequestId(newId());
                }}
                maxLength={4000}
                rows={7}
              />
            </Field>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setResolveOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => resolveMutation.mutate()}
              disabled={
                resolutionNotes.trim().length < 10 || resolveMutation.isPending
              }
            >
              Confirm outcome
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <Dialog open={attachmentOpen} onOpenChange={setAttachmentOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Attach dispute evidence</DialogTitle>
            <DialogDescription>
              The file is virus-scanned and retained in the central document
              repository. Active content and unsafe files are rejected.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <Field label="Evidence type">
              <Select
                value={attachmentType}
                onValueChange={(value) => {
                  setAttachmentType(
                    value as QuantitySurveyEscalationDisputeAttachmentType
                  );
                  setAttachmentRequestId(newId());
                }}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="ContractorSubmission">
                    Contractor submission
                  </SelectItem>
                  <SelectItem value="TdcReview">TDC review</SelectItem>
                  <SelectItem value="ResolutionEvidence">
                    Resolution evidence
                  </SelectItem>
                </SelectContent>
              </Select>
            </Field>
            <Field label="Title">
              <Input
                value={attachmentTitle}
                onChange={(event) => {
                  setAttachmentTitle(event.target.value);
                  setAttachmentRequestId(newId());
                }}
                maxLength={200}
              />
            </Field>
            <Field label="File (maximum 10 MB)">
              <Input
                type="file"
                onChange={(event) => {
                  setAttachmentFile(event.target.files?.[0] ?? null);
                  setAttachmentRequestId(newId());
                }}
              />
            </Field>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAttachmentOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => attachmentMutation.mutate()}
              disabled={
                !attachmentFile ||
                attachmentTitle.trim().length < 3 ||
                attachmentMutation.isPending
              }
            >
              Scan and retain
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <Dialog open={historyOpen} onOpenChange={setHistoryOpen}>
        <DialogContent className="max-h-[80vh] max-w-3xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Immutable dispute history</DialogTitle>
            <DialogDescription>
              Actor, assigned roles, reason, correlation, and before/after
              snapshots.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            {(history.data ?? []).map((item) => (
              <div key={item.id} className="rounded-md border p-3 text-sm">
                <div className="flex justify-between gap-2">
                  <span className="font-semibold">{item.action}</span>
                  <span className="text-xs text-muted-foreground">
                    {dateTime(item.createdAt)}
                  </span>
                </div>
                <p className="mt-1">
                  {item.actorName}{' '}
                  <span className="text-xs text-muted-foreground">
                    {item.actorRoles || 'No role snapshot'}
                  </span>
                </p>
                <p className="mt-1 whitespace-pre-wrap text-muted-foreground">
                  {item.reason || '—'}
                </p>
                <p className="mt-1 break-all font-mono text-[10px]">
                  {item.correlationId}
                </p>
              </div>
            ))}
          </div>
        </DialogContent>
      </Dialog>
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
    <div className="space-y-1.5">
      <Label>{label}</Label>
      {children}
    </div>
  );
}
function Meta({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border p-3">
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className="mt-1 font-medium">{value}</div>
    </div>
  );
}
