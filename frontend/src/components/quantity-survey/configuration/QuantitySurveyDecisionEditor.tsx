'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import {
  CheckCircle2,
  FileCheck2,
  Save,
  Send,
  Trash2,
  XCircle,
} from 'lucide-react';

import { ControlledDecisionFields } from '@/components/configuration/ControlledDecisionFields';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
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
import { documentManagementService } from '@/services/document-management.service';
import { quantitySurveyConfigurationService } from '@/services/quantity-survey-configuration.service';
import type {
  QsDecision,
  QsDecisionSchema,
  QsLookups,
} from '@/types/quantity-survey-configuration';

type Props = {
  profileId: string;
  decision: QsDecision;
  schema: QsDecisionSchema;
  lookups: QsLookups;
  editable: boolean;
  canApprove: boolean;
  onChanged: () => Promise<void> | void;
};
const evidenceTypes = [
  'Policy',
  'Committee minute',
  'Technical standard',
  'Contract clause',
  'Approval memorandum',
];
const message = (error: unknown) =>
  (error as { response?: { detail?: string }; message?: string }).response
    ?.detail ||
  (error as Error)?.message ||
  'The request failed.';

export function QuantitySurveyDecisionEditor({
  profileId,
  decision,
  schema,
  lookups,
  editable,
  canApprove,
  onChanged,
}: Props) {
  const { toast } = useToast();
  const [value, setValue] = useState<Record<string, unknown>>(() => ({
    ...decision.value,
  }));
  const [sourceLineage, setSourceLineage] = useState(
    decision.sourceLineage ?? ''
  );
  const [notes, setNotes] = useState(decision.notes ?? '');
  const [reason, setReason] = useState('');
  const [approvalReference, setApprovalReference] = useState('');
  const [evidenceType, setEvidenceType] = useState(evidenceTypes[0]);
  const [documentRecordId, setDocumentRecordId] = useState('');
  const [externalReference, setExternalReference] = useState('');

  const documents = useQuery({
    queryKey: ['qs-configuration', 'central-dms-records'],
    queryFn: () => documentManagementService.getRecords(),
    staleTime: 60_000,
  });
  const eligibleDocuments = useMemo(
    () =>
      (documents.data ?? []).filter(
        (item) =>
          item.lifecycleStatus === 'Active' &&
          item.versionStatus === 'Published' &&
          Boolean(item.currentVersion)
      ),
    [documents.data]
  );
  const missing = schema.fields.filter((field) => {
    if (!field.required) return false;
    if (!Object.prototype.hasOwnProperty.call(value, field.name)) return true;
    const current = value[field.name];
    return (
      current === undefined ||
      current === null ||
      current === '' ||
      (Array.isArray(current) && current.length === 0)
    );
  });

  const save = useMutation({
    mutationFn: () =>
      quantitySurveyConfigurationService.saveDecision(
        profileId,
        decision.decisionKey,
        {
          schemaVersion: schema.schemaVersion,
          value,
          sourceLineage: sourceLineage.trim() || undefined,
          notes: notes.trim() || undefined,
          rowVersion: decision.rowVersion,
          reason: reason.trim() || undefined,
        }
      ),
    onSuccess: async () => {
      toast({
        title: 'Decision saved',
        description: 'The controlled value and revision history were updated.',
        variant: 'success',
      });
      await onChanged();
    },
    onError: (error) =>
      toast({
        title: 'Unable to save decision',
        description: message(error),
        variant: 'destructive',
      }),
  });
  const submit = useMutation({
    mutationFn: () =>
      quantitySurveyConfigurationService.submitDecision(
        profileId,
        decision.decisionKey,
        { rowVersion: decision.rowVersion, reason: reason.trim() || undefined }
      ),
    onSuccess: async () => {
      toast({
        title: 'Decision submitted',
        description: 'The decision now awaits independent approval.',
        variant: 'success',
      });
      await onChanged();
    },
    onError: (error) =>
      toast({
        title: 'Unable to submit decision',
        description: message(error),
        variant: 'destructive',
      }),
  });
  const decide = useMutation({
    mutationFn: (approve: boolean) =>
      (approve
        ? quantitySurveyConfigurationService.approveDecision
        : quantitySurveyConfigurationService.rejectDecision)(
        profileId,
        decision.decisionKey,
        {
          rowVersion: decision.rowVersion,
          approvalReference: approvalReference.trim(),
          reason: reason.trim() || undefined,
        }
      ),
    onSuccess: async (_, approve) => {
      toast({
        title: approve ? 'Decision approved' : 'Decision rejected',
        description: approve
          ? 'Evidence was verified and the decision is now immutable in this draft.'
          : 'The preparer can correct and resubmit the rejected decision.',
        variant: approve ? 'success' : 'destructive',
      });
      await onChanged();
    },
    onError: (error) =>
      toast({
        title: 'Approval action failed',
        description: message(error),
        variant: 'destructive',
      }),
  });
  const linkEvidence = useMutation({
    mutationFn: async () => {
      const detail =
        await documentManagementService.getRecord(documentRecordId);
      const current = detail?.versions.find(
        (version) =>
          version.status === 'Published' &&
          version.versionNumber === detail.record.currentVersion &&
          Boolean(version.publishedAt)
      );
      if (!detail || !current)
        throw new Error(
          'The selected document no longer has a current published version. Refresh the DMS record first.'
        );
      return quantitySurveyConfigurationService.linkEvidence(
        profileId,
        decision.decisionKey,
        {
          evidenceType,
          centralDocumentRecordId: detail.record.id,
          centralDocumentVersionId: current.id,
          externalReference: externalReference.trim() || undefined,
          decisionRowVersion: decision.rowVersion,
          reason: reason.trim() || 'Link controlled QS evidence',
        }
      );
    },
    onSuccess: async () => {
      setDocumentRecordId('');
      setExternalReference('');
      toast({
        title: 'DMS evidence linked',
        description:
          'The current published document version is now part of this decision.',
        variant: 'success',
      });
      await onChanged();
    },
    onError: (error) =>
      toast({
        title: 'Unable to link evidence',
        description: message(error),
        variant: 'destructive',
      }),
  });
  const unlink = useMutation({
    mutationFn: (evidenceId: string) =>
      quantitySurveyConfigurationService.unlinkEvidence(
        profileId,
        decision.decisionKey,
        evidenceId,
        decision.rowVersion,
        reason.trim() || 'Unlink QS evidence'
      ),
    onSuccess: async () => {
      toast({
        title: 'Evidence unlinked',
        description: 'The central DMS document itself was preserved.',
        variant: 'success',
      });
      await onChanged();
    },
    onError: (error) =>
      toast({
        title: 'Unable to unlink evidence',
        description: message(error),
        variant: 'destructive',
      }),
  });

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center gap-2 text-sm">
        <Badge variant="outline">{schema.configurationKey}</Badge>
        <Badge variant="secondary">Owner: {schema.ownerGroup}</Badge>
        <Badge variant={decision.isComplete ? 'default' : 'secondary'}>
          {decision.status}
        </Badge>
      </div>
      <ControlledDecisionFields
        fields={schema.fields}
        value={value}
        lookups={lookups.sources}
        disabled={!editable || decision.status === 'Approved'}
        onChange={setValue}
      />

      <div className="grid gap-4 border-t pt-5 md:grid-cols-2">
        <div className="space-y-2">
          <Label>Source lineage</Label>
          <Input
            disabled={!editable || decision.status === 'Approved'}
            value={sourceLineage}
            onChange={(event) => setSourceLineage(event.target.value)}
            placeholder="Policy, contract clause, minute or standard reference"
          />
        </div>
        <div className="space-y-2">
          <Label>Change reason</Label>
          <Input
            disabled={!editable || decision.status === 'Approved'}
            value={reason}
            onChange={(event) => setReason(event.target.value)}
            placeholder="Recorded in immutable history"
          />
        </div>
        <div className="space-y-2 md:col-span-2">
          <Label>Notes</Label>
          <Textarea
            disabled={!editable || decision.status === 'Approved'}
            value={notes}
            onChange={(event) => setNotes(event.target.value)}
          />
        </div>
      </div>
      {editable && decision.status !== 'Approved' && (
        <div className="flex flex-wrap items-center justify-between gap-3">
          <p className="text-xs text-muted-foreground">
            {missing.length
              ? `${missing.length} required controlled value(s) remain.`
              : 'All required controls have selections; server validation remains authoritative.'}
          </p>
          <div className="flex gap-2">
            <Button
              variant="outline"
              onClick={() => save.mutate()}
              disabled={save.isPending || missing.length > 0}
            >
              <Save className="mr-2 h-4 w-4" />
              Save draft
            </Button>
            {decision.status === 'Draft' && (
              <Button
                onClick={() => submit.mutate()}
                disabled={submit.isPending || decision.evidence.length === 0}
              >
                <Send className="mr-2 h-4 w-4" />
                Submit
              </Button>
            )}
          </div>
        </div>
      )}

      <div className="space-y-3 border-t pt-5">
        <div>
          <h3 className="font-semibold">Central DMS evidence</h3>
          <p className="text-xs text-muted-foreground">
            Only a current published central-document version can be linked.
            Direct file paths and duplicate upload stores are not accepted.
          </p>
        </div>
        {decision.evidence.length === 0 ? (
          <p className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
            No evidence linked.
          </p>
        ) : (
          decision.evidence.map((item) => (
            <div
              key={item.id}
              className="flex items-center justify-between gap-3 rounded-md border p-3"
            >
              <div className="min-w-0">
                <p className="truncate text-sm font-medium">
                  {item.documentReference} · {item.documentTitle}
                </p>
                <p className="text-xs text-muted-foreground">
                  {item.evidenceType} · {item.versionNumber} ·{' '}
                  {new Date(item.linkedAt).toLocaleString()}
                </p>
              </div>
              {editable && decision.status !== 'Approved' && (
                <Button
                  size="icon"
                  variant="ghost"
                  onClick={() => unlink.mutate(item.id)}
                >
                  <Trash2 className="h-4 w-4" />
                </Button>
              )}
            </div>
          ))
        )}
        {editable && decision.status !== 'Approved' && (
          <div className="grid gap-3 md:grid-cols-2">
            <div className="space-y-2">
              <Label>Evidence type</Label>
              <Select value={evidenceType} onValueChange={setEvidenceType}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {evidenceTypes.map((item) => (
                    <SelectItem key={item} value={item}>
                      {item}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Published DMS document</Label>
              <Select
                value={documentRecordId || '__none__'}
                onValueChange={(next) =>
                  setDocumentRecordId(next === '__none__' ? '' : next)
                }
                disabled={documents.isLoading}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select a current document" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__none__">Not selected</SelectItem>
                  {eligibleDocuments.map((item) => (
                    <SelectItem key={item.id} value={item.id}>
                      {item.documentReference} — {item.title} (
                      {item.currentVersion})
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label>Official external reference (optional)</Label>
              <Input
                type="url"
                value={externalReference}
                onChange={(event) => setExternalReference(event.target.value)}
                placeholder="https://..."
              />
            </div>
            <div className="md:col-span-2">
              <Button
                variant="outline"
                onClick={() => linkEvidence.mutate()}
                disabled={!documentRecordId || linkEvidence.isPending}
              >
                <FileCheck2 className="mr-2 h-4 w-4" />
                Link current version
              </Button>
            </div>
          </div>
        )}
      </div>

      {canApprove && decision.status === 'Proposed' && (
        <Alert>
          <CheckCircle2 className="h-4 w-4" />
          <AlertTitle>Independent decision</AlertTitle>
          <AlertDescription className="space-y-3">
            <div className="space-y-2">
              <Label>Approval or rejection reference *</Label>
              <Input
                value={approvalReference}
                onChange={(event) => setApprovalReference(event.target.value)}
                placeholder="Workflow, committee minute or authority reference"
              />
            </div>
            <div className="flex gap-2">
              <Button
                onClick={() => decide.mutate(true)}
                disabled={!approvalReference.trim() || decide.isPending}
              >
                <CheckCircle2 className="mr-2 h-4 w-4" />
                Approve
              </Button>
              <Button
                variant="destructive"
                onClick={() => decide.mutate(false)}
                disabled={!approvalReference.trim() || decide.isPending}
              >
                <XCircle className="mr-2 h-4 w-4" />
                Reject
              </Button>
            </div>
          </AlertDescription>
        </Alert>
      )}
    </div>
  );
}
