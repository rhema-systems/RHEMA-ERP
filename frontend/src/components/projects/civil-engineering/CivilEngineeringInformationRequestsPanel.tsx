'use client';

import Link from 'next/link';
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  CheckCircle2,
  ExternalLink,
  FileInput,
  Plus,
  RefreshCw,
  RotateCcw,
  Send,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
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
import { useToast } from '@/hooks/use-toast';
import {
  documentManagementService,
  type CentralDocumentRecord,
} from '@/services/document-management.service';
import { civilEngineeringDesignService } from '@/services/civil-engineering-design.service';
import type {
  CivilEngineeringDesignCase,
  CivilEngineeringDesignInputPriority,
  CivilEngineeringDesignInputRequest,
  CivilEngineeringDesignLookups,
} from '@/types/civil-engineering-design';

const priorities: CivilEngineeringDesignInputPriority[] = [
  'Low',
  'Medium',
  'High',
  'Critical',
];

const message = (error: unknown) => {
  const value = error as {
    response?: { detail?: string; correlationId?: string };
    message?: string;
  };
  const detail = value.response?.detail || value.message;
  const correlation = value.response?.correlationId;
  return `${detail || 'The cross-section request could not be completed.'}${
    correlation ? ` Reference: ${correlation}` : ''
  }`;
};

const key = () => crypto.randomUUID();
const endOfDayUtc = (value: string) =>
  new Date(`${value}T23:59:59`).toISOString();
const formatDate = (value?: string | null) =>
  value
    ? new Intl.DateTimeFormat(undefined, {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(value))
    : 'Not set';

type Props = {
  designCase?: CivilEngineeringDesignCase;
  lookups?: CivilEngineeringDesignLookups;
  documents: CentralDocumentRecord[];
  assignedOnly?: boolean;
  canCreate?: boolean;
  canRespond?: boolean;
  canReview?: boolean;
  onWorkflowChanged?: () => Promise<void> | void;
};

export function CivilEngineeringInformationRequestsPanel({
  designCase,
  lookups,
  documents,
  assignedOnly = false,
  canCreate = false,
  canRespond = false,
  canReview = false,
  onWorkflowChanged,
}: Props) {
  const { toast } = useToast();
  const [items, setItems] = useState<CivilEngineeringDesignInputRequest[]>([]);
  const [loading, setLoading] = useState(false);
  const [sectionId, setSectionId] = useState('');
  const [subject, setSubject] = useState('');
  const [question, setQuestion] = useState('');
  const [priority, setPriority] =
    useState<CivilEngineeringDesignInputPriority>('Medium');
  const [dueDate, setDueDate] = useState('');
  const [blocksReadiness, setBlocksReadiness] = useState(true);
  const [activeId, setActiveId] = useState('');
  const [responseText, setResponseText] = useState('');
  const [documentRecordId, setDocumentRecordId] = useState('');
  const [reviewReason, setReviewReason] = useState('');

  const eligibleDocuments = useMemo(
    () =>
      documents.filter(
        (item) =>
          item.lifecycleStatus === 'Active' &&
          item.versionStatus === 'Published' &&
          Boolean(item.currentVersion)
      ),
    [documents]
  );

  const load = useCallback(async () => {
    const designCaseId = designCase?.id;
    if (!assignedOnly && !designCaseId) return;
    setLoading(true);
    try {
      setItems(
        assignedOnly
          ? await civilEngineeringDesignService.listAssignedInformationRequests()
          : await civilEngineeringDesignService.listInformationRequests(
              designCaseId as string
            )
      );
    } catch (error) {
      toast({
        title: 'Unable to load information requests',
        description: message(error),
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  }, [assignedOnly, designCase?.id, toast]);

  useEffect(() => {
    void load();
  }, [load]);

  const refreshAll = async () => {
    await load();
    await onWorkflowChanged?.();
  };

  const create = async () => {
    if (
      !designCase ||
      !sectionId ||
      subject.trim().length < 3 ||
      question.trim().length < 10 ||
      !dueDate
    ) {
      toast({
        title: 'Complete the information request',
        description:
          'Select a section and due date, then enter a clear subject and question.',
        variant: 'destructive',
      });
      return;
    }
    setLoading(true);
    try {
      await civilEngineeringDesignService.createInformationRequest(
        designCase.id,
        {
          clientRequestId: key(),
          requestedSectionId: sectionId,
          subject: subject.trim(),
          question: question.trim(),
          priority,
          responseDueDate: endOfDayUtc(dueDate),
          blocksDesignReadiness: blocksReadiness,
        }
      );
      setSectionId('');
      setSubject('');
      setQuestion('');
      setPriority('Medium');
      setDueDate('');
      setBlocksReadiness(true);
      toast({
        title: 'Information request sent',
        description:
          'The selected section can now respond with controlled evidence.',
        variant: 'success',
      });
      await refreshAll();
    } catch (error) {
      toast({
        title: 'Unable to send information request',
        description: message(error),
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  };

  const currentPublishedVersion = async (recordId: string) => {
    const detail = await documentManagementService.getRecord(recordId);
    const version = detail?.versions.find(
      (item) =>
        item.status === 'Published' &&
        item.versionNumber === detail.record.currentVersion
    );
    if (!detail || !version) {
      throw new Error(
        'The selected DMS document no longer has a current Published version. Refresh and select it again.'
      );
    }
    return { recordId: detail.record.id, versionId: version.id };
  };

  const respond = async (item: CivilEngineeringDesignInputRequest) => {
    if (responseText.trim().length < 10 || !documentRecordId) {
      toast({
        title: 'Complete the response',
        description:
          'Enter the section response and select its current Published DMS evidence.',
        variant: 'destructive',
      });
      return;
    }
    setLoading(true);
    try {
      const evidence = await currentPublishedVersion(documentRecordId);
      await civilEngineeringDesignService.submitInformationResponse(item.id, {
        clientRequestId: key(),
        rowVersion: item.rowVersion,
        responseText: responseText.trim(),
        centralDocumentRecordId: evidence.recordId,
        centralDocumentVersionId: evidence.versionId,
      });
      setActiveId('');
      setResponseText('');
      setDocumentRecordId('');
      toast({
        title: 'Section response submitted',
        description:
          'The Supervising Civil Engineer can now review the response.',
        variant: 'success',
      });
      await refreshAll();
    } catch (error) {
      toast({
        title: 'Unable to submit response',
        description: message(error),
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  };

  const review = async (
    item: CivilEngineeringDesignInputRequest,
    action: 'Accept' | 'Return'
  ) => {
    if (reviewReason.trim().length < 5) {
      toast({
        title: 'Enter the review reason',
        description: 'The review reason must contain at least five characters.',
        variant: 'destructive',
      });
      return;
    }
    setLoading(true);
    try {
      await civilEngineeringDesignService.reviewInformationResponse(item.id, {
        clientRequestId: key(),
        rowVersion: item.rowVersion,
        action,
        reason: reviewReason.trim(),
      });
      setActiveId('');
      setReviewReason('');
      toast({
        title: action === 'Accept' ? 'Response accepted' : 'Response returned',
        description:
          action === 'Accept'
            ? 'The request is closed and its readiness blocker is cleared.'
            : 'The requested section can submit a corrected response.',
        variant: 'success',
      });
      await refreshAll();
    } catch (error) {
      toast({
        title: 'Unable to review response',
        description: message(error),
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  };

  const showCreate =
    canCreate && designCase?.stage === 'SceInformationGathering' && lookups;

  return (
    <div className="rounded-lg border p-4">
      <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h4 className="flex items-center gap-2 font-medium">
            <FileInput className="h-4 w-4" /> Cross-section information
          </h4>
          <p className="mt-1 text-sm text-muted-foreground">
            Requests, section responses, evidence, and design readiness.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Badge variant="outline">
            {items.filter((item) => item.status !== 'Closed').length} open
          </Badge>
          <Button
            type="button"
            size="sm"
            variant="outline"
            onClick={() => void load()}
            disabled={loading}
          >
            <RefreshCw className="mr-2 h-4 w-4" /> Refresh
          </Button>
        </div>
      </div>

      {showCreate ? (
        <div className="mb-4 space-y-3 rounded-md bg-muted/30 p-3">
          <div className="grid gap-3 lg:grid-cols-4">
            <div className="space-y-2">
              <Label>Requested section</Label>
              <Select
                value={sectionId || '__none__'}
                onValueChange={(value) =>
                  setSectionId(value === '__none__' ? '' : value)
                }
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select section" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__none__">Select section</SelectItem>
                  {lookups.informationSourceSections.map((section) => (
                    <SelectItem
                      key={section.sectionId}
                      value={section.sectionId}
                    >
                      {section.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2 lg:col-span-2">
              <Label htmlFor="civil-input-subject">Subject</Label>
              <Input
                id="civil-input-subject"
                value={subject}
                onChange={(event) => setSubject(event.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label>Priority</Label>
              <Select
                value={priority}
                onValueChange={(value) =>
                  setPriority(value as CivilEngineeringDesignInputPriority)
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {priorities.map((value) => (
                    <SelectItem key={value} value={value}>
                      {value}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2 lg:col-span-3">
              <Label htmlFor="civil-input-question">Information required</Label>
              <Textarea
                id="civil-input-question"
                rows={2}
                value={question}
                onChange={(event) => setQuestion(event.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="civil-input-due">Response due date</Label>
              <Input
                id="civil-input-due"
                type="date"
                min={new Date().toISOString().slice(0, 10)}
                value={dueDate}
                onChange={(event) => setDueDate(event.target.value)}
              />
            </div>
          </div>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <label className="flex items-center gap-2 text-sm">
              <Checkbox
                checked={blocksReadiness}
                onCheckedChange={(checked) =>
                  setBlocksReadiness(checked === true)
                }
              />
              Required before design assignment
            </label>
            <Button
              type="button"
              onClick={() => void create()}
              disabled={loading}
            >
              <Plus className="mr-2 h-4 w-4" /> Send request
            </Button>
          </div>
        </div>
      ) : null}

      <div className="space-y-3">
        {items.length === 0 ? (
          <p className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
            No cross-section information request is awaiting action.
          </p>
        ) : (
          items.map((item) => {
            const latest = item.responses[item.responses.length - 1];
            const active = activeId === item.id;
            return (
              <div key={item.id} className="rounded-md border p-3">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-medium">
                        {item.referenceNumber} · {item.subject}
                      </span>
                      <Badge variant="secondary">{item.status}</Badge>
                      <Badge variant="outline">{item.priority}</Badge>
                      {item.blocksDesignReadiness &&
                      item.status !== 'Closed' ? (
                        <Badge variant="destructive">Blocks readiness</Badge>
                      ) : null}
                    </div>
                    <div className="mt-1 text-xs text-muted-foreground">
                      {item.requestedSectionLabel} · due{' '}
                      {formatDate(item.responseDueDate)}
                    </div>
                    <p className="mt-2 whitespace-pre-wrap text-sm">
                      {item.question}
                    </p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {assignedOnly ? (
                      <Button asChild size="sm" variant="outline">
                        <Link href={`/development/projects/${item.projectId}`}>
                          <ExternalLink className="mr-2 h-4 w-4" /> Project
                        </Link>
                      </Button>
                    ) : null}
                    {canRespond && item.status === 'Submitted' ? (
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        onClick={() => {
                          setActiveId(active ? '' : item.id);
                          setReviewReason('');
                        }}
                      >
                        <Send className="mr-2 h-4 w-4" /> Respond
                      </Button>
                    ) : null}
                    {canReview && item.status === 'Answered' ? (
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        onClick={() => {
                          setActiveId(active ? '' : item.id);
                          setResponseText('');
                          setDocumentRecordId('');
                        }}
                      >
                        Review
                      </Button>
                    ) : null}
                  </div>
                </div>

                {latest ? (
                  <div className="mt-3 rounded-md bg-muted/40 p-3 text-sm">
                    <div className="font-medium">
                      Response {latest.responseSequence} ·{' '}
                      {latest.respondedByName}
                    </div>
                    <p className="mt-1 whitespace-pre-wrap">
                      {latest.responseText}
                    </p>
                    <div className="mt-2 text-xs text-muted-foreground">
                      {latest.documentReference} — {latest.documentTitle} v
                      {latest.versionNumber} · {formatDate(latest.respondedAt)}
                    </div>
                  </div>
                ) : null}

                {active && canRespond && item.status === 'Submitted' ? (
                  <div className="mt-3 grid gap-3 lg:grid-cols-[minmax(0,2fr),minmax(240px,1fr),auto]">
                    <Textarea
                      rows={2}
                      value={responseText}
                      onChange={(event) => setResponseText(event.target.value)}
                      placeholder="Section response"
                    />
                    <Select
                      value={documentRecordId || '__none__'}
                      onValueChange={(value) =>
                        setDocumentRecordId(value === '__none__' ? '' : value)
                      }
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="Published DMS evidence" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="__none__">
                          Select evidence
                        </SelectItem>
                        {eligibleDocuments.map((document) => (
                          <SelectItem key={document.id} value={document.id}>
                            {document.documentReference} — {document.title}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <Button
                      type="button"
                      onClick={() => void respond(item)}
                      disabled={loading}
                    >
                      Submit
                    </Button>
                  </div>
                ) : null}

                {active && canReview && item.status === 'Answered' ? (
                  <div className="mt-3 grid gap-3 lg:grid-cols-[minmax(240px,1fr),auto]">
                    <Input
                      value={reviewReason}
                      onChange={(event) => setReviewReason(event.target.value)}
                      placeholder="Review reason"
                    />
                    <div className="flex flex-wrap gap-2">
                      <Button
                        type="button"
                        variant="outline"
                        onClick={() => void review(item, 'Return')}
                        disabled={loading}
                      >
                        <RotateCcw className="mr-2 h-4 w-4" /> Return
                      </Button>
                      <Button
                        type="button"
                        onClick={() => void review(item, 'Accept')}
                        disabled={loading}
                      >
                        <CheckCircle2 className="mr-2 h-4 w-4" /> Accept
                      </Button>
                    </div>
                  </div>
                ) : null}
              </div>
            );
          })
        )}
      </div>
    </div>
  );
}
