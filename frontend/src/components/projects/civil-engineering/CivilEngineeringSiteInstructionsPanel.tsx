'use client';

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ClipboardCheck,
  FileText,
  RefreshCw,
  Send,
  ShieldCheck,
  UserRound,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
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
import { civilEngineeringSiteInstructionService } from '@/services/civil-engineering-site-instruction.service';
import type {
  CivilEngineeringSiteInstructionLookups,
  CivilEngineeringSiteInstructionRouting,
} from '@/types/civil-engineering-site-instruction';

const supervisionPermission = 'civil-engineering.supervision.manage';
const workspacePermission = 'civil-engineering.workspace.read';
const now = () => new Date().toISOString().slice(0, 10);
const toUtc = (value: string) =>
  new Date(`${value}T00:00:00.000Z`).toISOString();
const formatDate = (value?: string | null) =>
  value
    ? new Intl.DateTimeFormat(undefined, {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(value))
    : 'Not available';
const safeError = (error: unknown, fallback: string) => {
  const value = error as {
    response?: { detail?: string; correlationId?: string };
    message?: string;
  };
  const detail = value.response?.detail || value.message || fallback;
  return `${detail}${value.response?.correlationId ? ` Reference: ${value.response.correlationId}` : ''}`;
};

type Props = { projectId: string };

export function CivilEngineeringSiteInstructionsPanel({ projectId }: Props) {
  const { toast } = useToast();
  const { hasPermission } = useAuth();
  const canRead = hasPermission(workspacePermission);
  const canManage = hasPermission(supervisionPermission);
  const [items, setItems] = useState<CivilEngineeringSiteInstructionRouting[]>(
    []
  );
  const [lookups, setLookups] =
    useState<CivilEngineeringSiteInstructionLookups>();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [referenceNumber, setReferenceNumber] = useState('');
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [effectiveDate, setEffectiveDate] = useState(now());
  const [documentKey, setDocumentKey] = useState('');
  const [supersedesRoutingId, setSupersedesRoutingId] = useState('');
  const [reviewReasons, setReviewReasons] = useState<Record<string, string>>(
    {}
  );
  const [engineeringReasons, setEngineeringReasons] = useState<
    Record<string, string>
  >({});
  const [followUpActions, setFollowUpActions] = useState<
    Record<string, 'EngineeringFollowUp' | 'InstructionClosed'>
  >({});

  const selectedDocument = useMemo(
    () =>
      lookups?.documents.find(
        (item) =>
          `${item.centralDocumentRecordId}:${item.centralDocumentVersionId}` ===
          documentKey
      ),
    [documentKey, lookups]
  );

  const load = useCallback(async () => {
    if (!canRead) {
      setLoading(false);
      return;
    }
    setLoading(true);
    const [list, context] = await Promise.allSettled([
      civilEngineeringSiteInstructionService.list(projectId),
      canManage
        ? civilEngineeringSiteInstructionService.lookups(projectId)
        : Promise.resolve(undefined),
    ]);
    if (list.status === 'fulfilled') setItems(list.value);
    else
      toast({
        variant: 'destructive',
        title: 'Site instructions could not be loaded',
        description: safeError(
          list.reason,
          'Refresh the project workspace and try again.'
        ),
      });
    if (context.status === 'fulfilled' && context.value) {
      setLookups(context.value);
      setDocumentKey(
        (current) =>
          current ||
          (context.value?.documents[0]
            ? `${context.value.documents[0].centralDocumentRecordId}:${context.value.documents[0].centralDocumentVersionId}`
            : '')
      );
    } else setLookups(undefined);
    setLoading(false);
  }, [canManage, canRead, projectId, toast]);

  useEffect(() => {
    void load();
  }, [load]);

  const issue = async () => {
    if (
      !lookups ||
      !selectedDocument ||
      !referenceNumber.trim() ||
      title.trim().length < 3 ||
      description.trim().length < 10 ||
      !effectiveDate
    ) {
      toast({
        variant: 'destructive',
        title: 'Complete the controlled instruction details',
        description:
          'Select a current Published DMS record/version and provide the reference, title, narrative and effective date.',
      });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringSiteInstructionService.issue(projectId, {
        clientRequestId: crypto.randomUUID(),
        projectEngineerAssignmentId: lookups.projectEngineerAssignmentId,
        referenceNumber: referenceNumber.trim(),
        title: title.trim(),
        description: description.trim(),
        effectiveDate: toUtc(effectiveDate),
        ...(supersedesRoutingId
          ? { supersedesRoutingId }
          : {}),
        evidence: [
          {
            centralDocumentRecordId: selectedDocument.centralDocumentRecordId,
            centralDocumentVersionId: selectedDocument.centralDocumentVersionId,
          },
        ],
      });
      setReferenceNumber('');
      setTitle('');
      setDescription('');
      setEffectiveDate(now());
      setSupersedesRoutingId('');
      toast({
        title: 'Site instruction routed',
        description:
          'The instruction has been submitted to the assigned Project Manager through the configured workflow.',
      });
      await load();
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Site instruction was not routed',
        description: safeError(
          error,
          'The governed site instruction could not be issued.'
        ),
      });
    } finally {
      setSaving(false);
    }
  };

  const reviewContractorResponse = async (
    item: CivilEngineeringSiteInstructionRouting,
    approve: boolean
  ) => {
    const reason = engineeringReasons[item.id]?.trim() || '';
    if (!selectedDocument || reason.length < 3) {
      toast({
        variant: 'destructive',
        title: 'Engineering review evidence and reason required',
        description:
          'Select current Published project DMS evidence and provide the engineering review reason.',
      });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringSiteInstructionService.reviewContractorResponse(
        item.id,
        {
          clientRequestId: crypto.randomUUID(),
          rowVersion: item.rowVersion,
          approve,
          reason,
          centralDocumentRecordId: selectedDocument.centralDocumentRecordId,
          centralDocumentVersionId: selectedDocument.centralDocumentVersionId,
        }
      );
      setEngineeringReasons((current) => ({ ...current, [item.id]: '' }));
      toast({
        title: approve
          ? 'Contractor response accepted'
          : 'Contractor response returned',
        description: approve
          ? 'Record the next controlled follow-up or closure decision.'
          : 'The controlled contractor can provide a further response.',
      });
      await load();
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Engineering review was not saved',
        description: safeError(error, 'Refresh and try again.'),
      });
    } finally {
      setSaving(false);
    }
  };

  const followUp = async (item: CivilEngineeringSiteInstructionRouting) => {
    const action = followUpActions[item.id] || 'EngineeringFollowUp';
    const reason = engineeringReasons[item.id]?.trim() || '';
    if (!selectedDocument || reason.length < 3) {
      toast({
        variant: 'destructive',
        title: 'Follow-up evidence and reason required',
        description:
          'Select current Published project DMS evidence and provide the follow-up or closure reason.',
      });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringSiteInstructionService.recordEngineeringFollowUp(
        item.id,
        {
          clientRequestId: crypto.randomUUID(),
          rowVersion: item.rowVersion,
          action,
          reason,
          centralDocumentRecordId: selectedDocument.centralDocumentRecordId,
          centralDocumentVersionId: selectedDocument.centralDocumentVersionId,
        }
      );
      setEngineeringReasons((current) => ({ ...current, [item.id]: '' }));
      toast({
        title:
          action === 'InstructionClosed'
            ? 'Site instruction closed'
            : 'Contractor follow-up requested',
        description:
          action === 'InstructionClosed'
            ? 'Closure is retained in immutable instruction history.'
            : 'The contractor-response route is open for the next controlled response.',
      });
      await load();
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Engineering follow-up was not saved',
        description: safeError(error, 'Refresh and try again.'),
      });
    } finally {
      setSaving(false);
    }
  };

  const process = async (
    item: CivilEngineeringSiteInstructionRouting,
    approve: boolean
  ) => {
    const reason = reviewReasons[item.id]?.trim() || '';
    if (reason.length < 3) {
      toast({
        variant: 'destructive',
        title: 'Project Manager comment required',
        description:
          'Provide a concise routing decision comment before continuing.',
      });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringSiteInstructionService.processProjectManagerRouting(
        item.id,
        {
          clientRequestId: crypto.randomUUID(),
          rowVersion: item.rowVersion,
          approve,
          reason,
        }
      );
      setReviewReasons((current) => ({ ...current, [item.id]: '' }));
      toast({
        title: approve
          ? 'Site instruction approved'
          : 'Site instruction rejected',
        description: approve
          ? 'The controlled contractor acknowledgement route is now active.'
          : 'The instruction remains in immutable history with the rejection decision.',
      });
      await load();
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Routing decision was not saved',
        description: safeError(
          error,
          'Only the assigned Project Manager at the active workflow step can complete this action.'
        ),
      });
    } finally {
      setSaving(false);
    }
  };

  if (!canRead) return null;

  return (
    <Card className="border-slate-200/70 shadow-sm">
      <CardHeader className="gap-3 pb-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <CardTitle className="flex items-center gap-2 text-base">
            <ClipboardCheck className="h-4 w-4 text-blue-600" />
            Governed site instructions
          </CardTitle>
          <CardDescription>
            Project Engineer instructions route to the assigned Project Manager.
            Contractor acknowledgement and responses remain linked to immutable
            DMS versions.
          </CardDescription>
        </div>
        <Button
          variant="outline"
          size="sm"
          onClick={() => void load()}
          disabled={loading || saving}
        >
          <RefreshCw className="mr-2 h-4 w-4" />
          Refresh
        </Button>
      </CardHeader>
      <CardContent className="space-y-4">
        {loading ? (
          <div className="text-sm text-slate-500">
            Loading governed site instructions…
          </div>
        ) : null}
        {canManage && lookups ? (
          <section className="rounded-lg border border-blue-100 bg-blue-50/40 p-4">
            <div className="mb-3 flex items-center gap-2 text-sm font-medium text-slate-900">
              <Send className="h-4 w-4 text-blue-600" />
              Issue through Project Manager
            </div>
            <div className="mb-4 grid gap-3 text-sm sm:grid-cols-3">
              <div className="rounded border border-slate-200 bg-white px-3 py-2">
                <span className="block text-xs text-slate-500">
                  Project Engineer
                </span>
                {lookups.projectEngineerName}
              </div>
              <div className="rounded border border-slate-200 bg-white px-3 py-2">
                <span className="block text-xs text-slate-500">
                  Project Manager
                </span>
                {lookups.projectManagerName}
              </div>
              <div className="rounded border border-slate-200 bg-white px-3 py-2">
                <span className="block text-xs text-slate-500">
                  Controlled contractor
                </span>
                {lookups.contractorName}
              </div>
            </div>
            <div className="grid gap-4 md:grid-cols-[minmax(0,1fr)_minmax(0,2fr)_180px]">
              <div className="space-y-2">
                <Label>Instruction reference</Label>
                <Input
                  value={referenceNumber}
                  onChange={(event) => setReferenceNumber(event.target.value)}
                  placeholder="SI-2026-001"
                />
              </div>
              <div className="space-y-2">
                <Label>Instruction title</Label>
                <Input
                  value={title}
                  onChange={(event) => setTitle(event.target.value)}
                  placeholder="Short controlled instruction title"
                />
              </div>
              <div className="space-y-2">
                <Label>Effective date</Label>
                <Input
                  type="date"
                  value={effectiveDate}
                  onChange={(event) => setEffectiveDate(event.target.value)}
                />
              </div>
            </div>
            <div className="mt-4 grid gap-4 md:grid-cols-[minmax(0,1fr)_minmax(0,2fr)]">
              <div className="space-y-2">
                <Label>Supersede instruction version (optional)</Label>
                <Select
                  value={supersedesRoutingId || '__new__'}
                  onValueChange={(value) =>
                    setSupersedesRoutingId(value === '__new__' ? '' : value)
                  }
                >
                  <SelectTrigger>
                    <SelectValue placeholder="New instruction lineage" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__new__">New instruction lineage</SelectItem>
                    {items
                      .filter(
                        (item) =>
                          ![
                            'PendingApproval',
                            'Rejected',
                            'Closed',
                            'Superseded',
                          ].includes(item.status)
                      )
                      .map((item) => (
                        <SelectItem key={item.id} value={item.id}>
                          {item.referenceNumber} · v{item.instructionVersion}
                        </SelectItem>
                      ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Current Published DMS evidence</Label>
                <Select value={documentKey} onValueChange={setDocumentKey}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select a governed document version" />
                  </SelectTrigger>
                  <SelectContent>
                    {lookups.documents.map((document) => (
                      <SelectItem
                        key={document.centralDocumentVersionId}
                        value={`${document.centralDocumentRecordId}:${document.centralDocumentVersionId}`}
                      >
                        {document.documentReference} · v{document.versionNumber}{' '}
                        · {document.title}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Instruction narrative</Label>
                <Textarea
                  rows={3}
                  value={description}
                  onChange={(event) => setDescription(event.target.value)}
                  placeholder="State the required site action, affected work and timing."
                />
              </div>
            </div>
            {!lookups.documents.length ? (
              <p className="mt-3 text-sm text-amber-700">
                No current Published DMS evidence is available for this tenant.
                Publish the controlled drawing or instruction evidence before
                issuing.
              </p>
            ) : null}
            <div className="mt-4 flex justify-end">
              <Button
                onClick={() => void issue()}
                disabled={saving || !lookups.documents.length}
              >
                <Send className="mr-2 h-4 w-4" />
                Route to Project Manager
              </Button>
            </div>
          </section>
        ) : null}
        {canManage && !loading && !lookups ? (
          <div className="rounded-lg border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-600">
            The instruction history is available, but issuing requires an active
            Project Engineer appointment, assigned Project Manager, controlled
            contractor and effective Civil supervision configuration.
          </div>
        ) : null}
        {!loading && items.length === 0 ? (
          <div className="rounded-lg border border-dashed border-slate-300 bg-slate-50 px-4 py-3 text-sm text-slate-600">
            No governed site instructions have been routed for this project.
          </div>
        ) : null}
        {items.map((item) => (
          <article
            key={item.id}
            className="rounded-lg border border-slate-200 bg-white p-4"
          >
            <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
              <div className="min-w-0 space-y-1">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-medium text-slate-900">
                    {item.referenceNumber} · {item.title}
                  </span>
                  <Badge
                    variant={
                      item.approvalStatus === 'Approved'
                        ? 'default'
                        : item.approvalStatus === 'Rejected'
                          ? 'destructive'
                          : 'secondary'
                    }
                  >
                    {item.approvalStatus}
                  </Badge>
                  <Badge variant="outline">{item.status}</Badge>
                  <Badge variant="outline">v{item.instructionVersion}</Badge>
                </div>
                <p className="text-sm text-slate-600">{item.description}</p>
                <p className="text-xs text-slate-500">
                  PE: {item.projectEngineerName} · PM: {item.projectManagerName}{' '}
                  · Contractor: {item.contractorName} ·{' '}
                  {formatDate(item.createdAt)}
                </p>
                {item.contractId ? (
                  <p className="text-xs text-slate-500">
                    Controlled Procurement contract linked.
                  </p>
                ) : null}
              </div>
            </div>
            <div className="mt-3 flex flex-wrap gap-2 text-xs text-slate-600">
              {item.evidence.map((evidence) => (
                <span
                  key={evidence.centralDocumentVersionId}
                  className="inline-flex items-center gap-1 rounded bg-slate-100 px-2 py-1"
                >
                  <FileText className="h-3 w-3" />
                  {evidence.documentReference} v{evidence.versionNumber}
                </span>
              ))}
            </div>
            {item.approvalStatus === 'Pending' && canManage ? (
              <div className="mt-4 rounded border border-amber-200 bg-amber-50 p-3">
                <div className="mb-2 flex items-center gap-2 text-sm font-medium text-slate-900">
                  <ShieldCheck className="h-4 w-4 text-amber-700" />
                  Project Manager workflow action
                </div>
                <Textarea
                  rows={2}
                  value={reviewReasons[item.id] || ''}
                  onChange={(event) =>
                    setReviewReasons((current) => ({
                      ...current,
                      [item.id]: event.target.value,
                    }))
                  }
                  placeholder="Project Manager routing comment"
                />
                <div className="mt-3 flex justify-end gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={saving}
                    onClick={() => void process(item, false)}
                  >
                    Reject
                  </Button>
                  <Button
                    size="sm"
                    disabled={saving}
                    onClick={() => void process(item, true)}
                  >
                    Approve and issue
                  </Button>
                </div>
              </div>
            ) : null}
            {item.status === 'AwaitingEngineeringReview' && canManage ? (
              <div className="mt-4 rounded border border-blue-200 bg-blue-50 p-3">
                <div className="mb-2 flex items-center gap-2 text-sm font-medium text-slate-900">
                  <ShieldCheck className="h-4 w-4 text-blue-700" />
                  Engineering review of contractor response
                </div>
                <Textarea
                  rows={2}
                  value={engineeringReasons[item.id] || ''}
                  onChange={(event) =>
                    setEngineeringReasons((current) => ({
                      ...current,
                      [item.id]: event.target.value,
                    }))
                  }
                  placeholder="Engineering review reason; selected project DMS evidence is attached."
                />
                <div className="mt-3 flex justify-end gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={saving || !selectedDocument}
                    onClick={() => void reviewContractorResponse(item, false)}
                  >
                    Return to contractor
                  </Button>
                  <Button
                    size="sm"
                    disabled={saving || !selectedDocument}
                    onClick={() => void reviewContractorResponse(item, true)}
                  >
                    Accept response
                  </Button>
                </div>
              </div>
            ) : null}
            {item.status === 'AwaitingEngineeringFollowUp' && canManage ? (
              <div className="mt-4 rounded border border-emerald-200 bg-emerald-50 p-3">
                <div className="mb-2 flex items-center gap-2 text-sm font-medium text-slate-900">
                  <ShieldCheck className="h-4 w-4 text-emerald-700" />
                  Engineering follow-up or closure
                </div>
                <div className="grid gap-3 md:grid-cols-[220px_minmax(0,1fr)]">
                  <Select
                    value={followUpActions[item.id] || 'EngineeringFollowUp'}
                    onValueChange={(value) =>
                      setFollowUpActions((current) => ({
                        ...current,
                        [item.id]: value as
                          | 'EngineeringFollowUp'
                          | 'InstructionClosed',
                      }))
                    }
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="EngineeringFollowUp">
                        Request contractor follow-up
                      </SelectItem>
                      <SelectItem value="InstructionClosed">
                        Close instruction
                      </SelectItem>
                    </SelectContent>
                  </Select>
                  <Textarea
                    rows={2}
                    value={engineeringReasons[item.id] || ''}
                    onChange={(event) =>
                      setEngineeringReasons((current) => ({
                        ...current,
                        [item.id]: event.target.value,
                      }))
                    }
                    placeholder="Follow-up or closure reason; selected project DMS evidence is attached."
                  />
                </div>
                <div className="mt-3 flex justify-end">
                  <Button
                    size="sm"
                    disabled={saving || !selectedDocument}
                    onClick={() => void followUp(item)}
                  >
                    Save controlled decision
                  </Button>
                </div>
              </div>
            ) : null}
            <div className="mt-4 border-t border-slate-100 pt-3">
              <div className="mb-2 flex items-center gap-2 text-sm font-medium text-slate-800">
                <UserRound className="h-4 w-4 text-slate-500" />
                Immutable contractor and engineering history
              </div>
              {item.responses.length ? (
                <div className="space-y-2">
                  {item.responses.map((response) => (
                    <div
                      key={response.id}
                      className="rounded bg-slate-50 px-3 py-2 text-sm"
                    >
                      <div className="flex flex-wrap items-center gap-2">
                        <Badge variant="outline">{response.action}</Badge>
                        <span className="font-medium text-slate-800">
                          {response.actorName}
                        </span>
                        <span className="text-xs text-slate-500">
                          {formatDate(response.timestamp)}
                        </span>
                      </div>
                      <p className="mt-1 whitespace-pre-wrap break-words text-slate-600">
                        {response.message}
                      </p>
                      {response.centralDocumentVersionId ? (
                        <p className="mt-1 text-xs text-slate-500">
                          Controlled DMS evidence linked.
                        </p>
                      ) : null}
                    </div>
                  ))}
                </div>
              ) : (
                <p className="text-sm text-slate-500">
                  No contractor or engineering lifecycle event has been recorded.
                </p>
              )}
            </div>
          </article>
        ))}
      </CardContent>
    </Card>
  );
}
