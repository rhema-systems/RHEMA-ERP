'use client';

import Link from 'next/link';
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { ExternalLink, FileCheck2, RefreshCw, Send } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { civilEngineeringDesignService } from '@/services/civil-engineering-design.service';
import type {
  CivilEngineeringDesignDiscipline,
  CivilEngineeringDocument,
  CivilEngineeringDocumentLookups,
  CreateCivilEngineeringDocumentRequest,
} from '@/types/civil-engineering-design';

const disciplines: CivilEngineeringDesignDiscipline[] = [
  'Civil',
  'Structural',
  'Architecture',
  'Geodetic',
  'TownPlanning',
  'Mechanical',
  'Electrical',
];

const emptyLookups: CivilEngineeringDocumentLookups = {
  namingPolicy: 'ProjectDisciplineSequenceRevision',
  allowedFileExtensions: [],
  maximumFileSizeMb: 0,
  metadataTemplateCode: '',
  allowAuthorizedPreview: false,
  allowAuthorizedDownload: false,
  owners: [],
  reviewers: [],
  workPackages: [],
  documents: [],
};

const initialDraft = (): CreateCivilEngineeringDocumentRequest => ({
  clientRequestId: crypto.randomUUID(),
  centralDocumentRecordId: '',
  centralDocumentVersionId: '',
  discipline: 'Civil',
  sequenceNumber: 1,
  revisionNumber: 0,
  ownerUserId: '',
  reviewerUserId: '',
});

const friendlyError = (error: unknown) => {
  const value = error as { response?: { detail?: string }; message?: string };
  return (
    value.response?.detail ||
    value.message ||
    'The engineering document request could not be completed.'
  );
};

const label = (value: string) => value.replace(/([a-z])([A-Z])/g, '$1 $2');

export function CivilEngineeringDocumentRegisterPanel({
  designCaseId,
  canManage,
  canApprove,
  onWorkflowChanged,
}: {
  designCaseId: string;
  canManage: boolean;
  canApprove: boolean;
  onWorkflowChanged: () => Promise<void>;
}) {
  const { user } = useAuth();
  const { toast } = useToast();
  const [lookups, setLookups] = useState(emptyLookups);
  const [documents, setDocuments] = useState<CivilEngineeringDocument[]>([]);
  const [draft, setDraft] = useState<CreateCivilEngineeringDocumentRequest>(
    () => initialDraft()
  );
  const [reviewReasons, setReviewReasons] = useState<Record<string, string>>(
    {}
  );
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    if (!designCaseId) return;
    setBusy(true);
    try {
      const [lookupValues, documentValues] = await Promise.all([
        civilEngineeringDesignService.documentLookups(designCaseId),
        civilEngineeringDesignService.listDocuments(designCaseId),
      ]);
      setLookups(lookupValues);
      setDocuments(documentValues);
      setDraft((current) => ({
        ...current,
        ownerUserId:
          current.ownerUserId ||
          lookupValues.owners.find((item) => item.userId === user?.id)
            ?.userId ||
          '',
      }));
    } catch (error) {
      toast({
        title: 'Engineering document register unavailable',
        description: friendlyError(error),
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  }, [designCaseId, toast, user?.id]);

  useEffect(() => {
    void load();
  }, [load]);

  const eligibleVersions = useMemo(() => {
    if (!draft.supersedesDocumentId) return lookups.documents;
    const prior = documents.find(
      (item) => item.id === draft.supersedesDocumentId
    );
    return prior
      ? lookups.documents.filter(
          (item) =>
            item.centralDocumentRecordId === prior.centralDocumentRecordId &&
            item.centralDocumentVersionId !== prior.centralDocumentVersionId
        )
      : lookups.documents;
  }, [documents, draft.supersedesDocumentId, lookups.documents]);

  const selectPredecessor = (value: string) => {
    const prior = documents.find((item) => item.id === value);
    setDraft((current) =>
      prior
        ? {
            ...current,
            supersedesDocumentId: prior.id,
            projectPackageId: prior.projectPackageId || undefined,
            discipline: prior.discipline,
            sequenceNumber: prior.sequenceNumber,
            revisionNumber: prior.revisionNumber + 1,
            ownerUserId: prior.ownerUserId,
            reviewerUserId: prior.reviewerUserId,
            centralDocumentRecordId: prior.centralDocumentRecordId,
            centralDocumentVersionId: '',
          }
        : {
            ...current,
            supersedesDocumentId: undefined,
            centralDocumentRecordId: '',
            centralDocumentVersionId: '',
          }
    );
  };

  const selectDmsVersion = (versionId: string) => {
    const version = lookups.documents.find(
      (item) => item.centralDocumentVersionId === versionId
    );
    setDraft((current) => ({
      ...current,
      centralDocumentVersionId: version?.centralDocumentVersionId || '',
      centralDocumentRecordId: version?.centralDocumentRecordId || '',
    }));
  };

  const create = async () => {
    if (
      !draft.centralDocumentVersionId ||
      !draft.ownerUserId ||
      !draft.reviewerUserId ||
      (lookups.namingPolicy === 'ProjectWorkPackageSequenceRevision' &&
        !draft.projectPackageId)
    ) {
      toast({
        title: 'Complete the controlled selections',
        description:
          'Select the DMS version, owner, reviewer, and required work package.',
        variant: 'destructive',
      });
      return;
    }
    setBusy(true);
    try {
      await civilEngineeringDesignService.createDocument(designCaseId, draft);
      setDraft(initialDraft());
      toast({ title: 'Engineering document registered' });
      await load();
      await onWorkflowChanged();
    } catch (error) {
      toast({
        title: 'Document registration failed',
        description: friendlyError(error),
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  };

  const submit = async (document: CivilEngineeringDocument) => {
    setBusy(true);
    try {
      await civilEngineeringDesignService.submitDocument(document.id, {
        clientRequestId: crypto.randomUUID(),
        rowVersion: document.rowVersion,
      });
      toast({ title: 'Document submitted for review' });
      await load();
      await onWorkflowChanged();
    } catch (error) {
      toast({
        title: 'Document submission failed',
        description: friendlyError(error),
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  };

  const decide = async (
    document: CivilEngineeringDocument,
    approve: boolean
  ) => {
    const reason = reviewReasons[document.id]?.trim() || '';
    if (reason.length < 5) {
      toast({
        title: 'Review reason required',
        description: 'Enter at least 5 characters.',
        variant: 'destructive',
      });
      return;
    }
    setBusy(true);
    try {
      await civilEngineeringDesignService.reviewDocument(document.id, {
        clientRequestId: crypto.randomUUID(),
        rowVersion: document.rowVersion,
        approve,
        reason,
      });
      setReviewReasons((current) => ({ ...current, [document.id]: '' }));
      toast({
        title: approve
          ? 'Engineering document approved'
          : 'Engineering document returned',
      });
      await load();
      await onWorkflowChanged();
    } catch (error) {
      toast({
        title: 'Document review failed',
        description: friendlyError(error),
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card>
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between gap-3">
          <CardTitle className="text-base">
            Engineering document register
          </CardTitle>
          <Button
            type="button"
            size="sm"
            variant="outline"
            onClick={() => void load()}
            disabled={busy}
          >
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {canManage ? (
          <div className="space-y-4 rounded-lg border p-4">
            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
              <div className="space-y-2 xl:col-span-2">
                <Label>Published DMS file version</Label>
                <Select
                  value={draft.centralDocumentVersionId || '__none__'}
                  onValueChange={selectDmsVersion}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select DMS version" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__none__">Select DMS version</SelectItem>
                    {eligibleVersions.map((item) => (
                      <SelectItem
                        key={item.centralDocumentVersionId}
                        value={item.centralDocumentVersionId}
                      >
                        {item.documentReference} · {item.fileName} ·{' '}
                        {item.versionNumber}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Discipline</Label>
                <Select
                  value={draft.discipline}
                  onValueChange={(value) =>
                    setDraft((current) => ({
                      ...current,
                      discipline: value as CivilEngineeringDesignDiscipline,
                    }))
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {disciplines.map((item) => (
                      <SelectItem key={item} value={item}>
                        {label(item)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Supersedes</Label>
                <Select
                  value={draft.supersedesDocumentId || '__none__'}
                  onValueChange={selectPredecessor}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="First revision" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__none__">First revision</SelectItem>
                    {documents
                      .filter((item) => item.status === 'Approved')
                      .map((item) => (
                        <SelectItem key={item.id} value={item.id}>
                          {item.documentReference} · R{item.revisionNumber}
                        </SelectItem>
                      ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
              {lookups.namingPolicy === 'ProjectWorkPackageSequenceRevision' ? (
                <div className="space-y-2">
                  <Label>Work package</Label>
                  <Select
                    value={draft.projectPackageId || '__none__'}
                    onValueChange={(value) =>
                      setDraft((current) => ({
                        ...current,
                        projectPackageId:
                          value === '__none__' ? undefined : value,
                      }))
                    }
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select package" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="__none__">Select package</SelectItem>
                      {lookups.workPackages.map((item) => (
                        <SelectItem key={item.id} value={item.id}>
                          {item.code} · {item.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              ) : null}
              <div className="space-y-2">
                <Label>Sequence</Label>
                <Input
                  type="number"
                  min={1}
                  max={999999}
                  value={draft.sequenceNumber}
                  onChange={(event) =>
                    setDraft((current) => ({
                      ...current,
                      sequenceNumber: Number(event.target.value),
                    }))
                  }
                />
              </div>
              <div className="space-y-2">
                <Label>Revision</Label>
                <Input
                  type="number"
                  min={0}
                  max={9999}
                  value={draft.revisionNumber}
                  onChange={(event) =>
                    setDraft((current) => ({
                      ...current,
                      revisionNumber: Number(event.target.value),
                    }))
                  }
                />
              </div>
              <div className="space-y-2">
                <Label>Owner</Label>
                <Select
                  value={draft.ownerUserId || '__none__'}
                  onValueChange={(value) =>
                    setDraft((current) => ({
                      ...current,
                      ownerUserId: value === '__none__' ? '' : value,
                    }))
                  }
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select owner" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__none__">Select owner</SelectItem>
                    {lookups.owners.map((item) => (
                      <SelectItem key={item.userId} value={item.userId}>
                        {item.displayName} · {label(item.roleName)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Reviewer</Label>
                <Select
                  value={draft.reviewerUserId || '__none__'}
                  onValueChange={(value) =>
                    setDraft((current) => ({
                      ...current,
                      reviewerUserId: value === '__none__' ? '' : value,
                    }))
                  }
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select reviewer" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__none__">Select reviewer</SelectItem>
                    {lookups.reviewers
                      .filter((item) => item.userId !== draft.ownerUserId)
                      .map((item) => (
                        <SelectItem key={item.userId} value={item.userId}>
                          {item.displayName} · {label(item.roleName)}
                        </SelectItem>
                      ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="flex justify-end">
              <Button
                type="button"
                onClick={() => void create()}
                disabled={busy}
              >
                <FileCheck2 className="mr-2 h-4 w-4" />
                Register version
              </Button>
            </div>
          </div>
        ) : null}

        {documents.length === 0 ? (
          <div className="rounded-lg border border-dashed p-4 text-sm text-muted-foreground">
            No governed engineering file version has been registered for this
            design case.
          </div>
        ) : (
          documents.map((document) => (
            <div key={document.id} className="space-y-3 rounded-lg border p-4">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-medium">
                      {document.documentReference}
                    </span>
                    <Badge variant="outline">
                      {label(document.discipline)}
                    </Badge>
                    <Badge variant="secondary">
                      {label(document.fileCategory)}
                    </Badge>
                    <Badge>{label(document.status)}</Badge>
                  </div>
                  <div className="mt-1 text-sm text-muted-foreground">
                    {document.documentTitle} · {document.dmsVersion} · revision{' '}
                    {document.revisionNumber}
                  </div>
                  <div className="mt-1 text-xs text-muted-foreground">
                    Owner: {document.ownerName} · Reviewer:{' '}
                    {document.reviewerName}
                  </div>
                </div>
                {lookups.allowAuthorizedPreview ? (
                  <Button asChild type="button" size="sm" variant="outline">
                    <Link
                      href={`/document-management/records/${document.centralDocumentRecordId}`}
                    >
                      <ExternalLink className="mr-2 h-4 w-4" />
                      Open DMS
                    </Link>
                  </Button>
                ) : null}
              </div>
              {document.reviewReason ? (
                <p className="text-sm text-muted-foreground">
                  {document.reviewReason}
                </p>
              ) : null}
              {canManage &&
              document.ownerUserId === user?.id &&
              (document.status === 'Draft' ||
                document.status === 'Returned') ? (
                <div className="flex justify-end">
                  <Button
                    type="button"
                    size="sm"
                    onClick={() => void submit(document)}
                    disabled={busy}
                  >
                    <Send className="mr-2 h-4 w-4" />
                    Submit for review
                  </Button>
                </div>
              ) : null}
              {canApprove &&
              document.reviewerUserId === user?.id &&
              document.status === 'ForReview' ? (
                <div className="space-y-2 rounded-md bg-muted/40 p-3">
                  <Label>Review reason</Label>
                  <Textarea
                    value={reviewReasons[document.id] || ''}
                    onChange={(event) =>
                      setReviewReasons((current) => ({
                        ...current,
                        [document.id]: event.target.value,
                      }))
                    }
                  />
                  <div className="flex justify-end gap-2">
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      onClick={() => void decide(document, false)}
                      disabled={busy}
                    >
                      Return
                    </Button>
                    <Button
                      type="button"
                      size="sm"
                      onClick={() => void decide(document, true)}
                      disabled={busy}
                    >
                      Approve
                    </Button>
                  </div>
                </div>
              ) : null}
            </div>
          ))
        )}
      </CardContent>
    </Card>
  );
}
