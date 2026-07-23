'use client';

import { useMemo, useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { FileUp, Link2, Save, Trash2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { procurementDecisionFormRegistry, procurementDecisionOwnerOptions } from '@/lib/procurement-configuration-registry';
import { fileUploadService } from '@/services/fileUploadService';
import { procurementConfigurationService } from '@/services/procurement-configuration.service';
import type {
  ProcurementConfigurationDecision,
  ProcurementConfigurationDecisionStatus,
  SaveProcurementConfigurationDecisionRequest,
} from '@/types/procurement-configuration';

type Props = {
  profileId: string;
  decision: ProcurementConfigurationDecision;
  editable: boolean;
  isSuperAdmin: boolean;
  onChanged: () => void | Promise<void>;
};

const errorMessage = (error: unknown) => {
  const candidate = error as { message?: string; response?: { detail?: string; title?: string } };
  return candidate.response?.detail || candidate.response?.title || candidate.message || 'The request failed.';
};
const isConflict = (error: unknown) => (error as { status?: number }).status === 409;

export function ProcurementDecisionEditor({ profileId, decision, editable, isSuperAdmin, onChanged }: Props) {
  const { toast } = useToast();
  const definition = procurementDecisionFormRegistry[decision.decisionKey];
  const [value, setValue] = useState<Record<string, unknown>>(() => ({ ...definition.defaults, ...decision.value }));
  const [ownerGroup, setOwnerGroup] = useState(decision.ownerGroup);
  const [status, setStatus] = useState<ProcurementConfigurationDecisionStatus>(decision.status);
  const [approvalReference, setApprovalReference] = useState(decision.approvalReference ?? '');
  const [sourceLineage, setSourceLineage] = useState(decision.sourceLineage ?? '');
  const [notes, setNotes] = useState(decision.notes ?? '');
  const [reason, setReason] = useState('');
  const [externalReference, setExternalReference] = useState('');

  const missingRequired = useMemo(() => definition.fields.filter(field => {
    if (!field.required) return false;
    const current = value[field.key];
    if (Array.isArray(current)) return current.length === 0;
    return current === undefined || current === null || current === '';
  }), [definition.fields, value]);

  const save = useMutation({
    mutationFn: () => {
      const nextStatus = isSuperAdmin ? status : status === 'Approved' ? 'Proposed' : status;
      const request: SaveProcurementConfigurationDecisionRequest = {
        schemaVersion: decision.schemaVersion,
        ownerGroup,
        status: nextStatus,
        approvalStatus: nextStatus === 'Approved' ? 'Approved' : nextStatus === 'Rejected' ? 'Rejected' : 'Pending',
        value,
        decisionDate: new Date().toISOString(),
        approvalReference: approvalReference.trim() || undefined,
        sourceLineage: sourceLineage.trim() || undefined,
        notes: notes.trim() || undefined,
        rowVersion: decision.rowVersion,
        reason: reason.trim() || undefined,
      };
      return procurementConfigurationService.saveDecision(profileId, decision.decisionKey, request);
    },
    onSuccess: async () => {
      toast({ title: 'Decision saved', description: `${decision.decisionKey} was saved with a new audit revision.`, variant: 'success' });
      await onChanged();
    },
    onError: error => toast({ title: isConflict(error) ? 'Configuration changed elsewhere' : 'Unable to save decision', description: isConflict(error) ? 'Reload the profile before saving this decision again.' : errorMessage(error), variant: 'destructive' }),
  });

  const linkEvidence = useMutation({
    mutationFn: async (file?: File) => {
      let filePath: string | undefined;
      if (file) {
        const uploaded = await fileUploadService.uploadFile(file, 'procurement-policy-evidence', decision.id);
        filePath = uploaded.filePath;
        if (!filePath) throw new Error('The evidence file was not accepted by the shared file-upload service.');
      }
      if (!filePath && !externalReference.trim()) throw new Error('Choose a file or enter an external evidence reference.');
      return procurementConfigurationService.linkEvidence(profileId, decision.decisionKey, {
        evidenceType: file ? 'SharedUpload' : 'ExternalReference',
        filePath,
        externalReference: externalReference.trim() || undefined,
        decisionRowVersion: decision.rowVersion,
        reason: reason.trim() || 'Attach decision evidence',
      });
    },
    onSuccess: async () => {
      setExternalReference('');
      toast({ title: 'Evidence linked', description: 'The shared evidence reference is now attached.', variant: 'success' });
      await onChanged();
    },
    onError: error => toast({ title: isConflict(error) ? 'Configuration changed elsewhere' : 'Unable to link evidence', description: isConflict(error) ? 'Reload the profile before linking evidence.' : errorMessage(error), variant: 'destructive' }),
  });

  const unlinkEvidence = useMutation({
    mutationFn: (evidenceId: string) => procurementConfigurationService.unlinkEvidence(
      profileId,
      decision.decisionKey,
      evidenceId,
      decision.rowVersion,
      reason.trim() || 'Remove decision evidence link',
    ),
    onSuccess: async () => {
      toast({ title: 'Evidence unlinked', description: 'The reference was removed; the shared file was preserved.', variant: 'success' });
      await onChanged();
    },
    onError: error => toast({ title: 'Unable to unlink evidence', description: errorMessage(error), variant: 'destructive' }),
  });

  if (!definition) return <p className="text-sm text-destructive">No registered editor exists for {decision.decisionKey}.</p>;

  const setField = (key: string, next: unknown) => setValue(current => ({ ...current, [key]: next }));

  return (
    <div className="space-y-5">
      <div className="grid gap-4 md:grid-cols-2">
        {definition.fields.map(field => {
          const current = value[field.key];
          return (
            <div key={field.key} className={field.type === 'textarea' || field.type === 'textList' ? 'space-y-2 md:col-span-2' : 'space-y-2'}>
              <Label htmlFor={`${decision.decisionKey}-${field.key}`}>{field.label}{field.required ? ' *' : ''}</Label>
              {field.type === 'select' ? (
                <Select disabled={!editable} value={String(current ?? '')} onValueChange={next => setField(field.key, next)}>
                  <SelectTrigger id={`${decision.decisionKey}-${field.key}`}><SelectValue placeholder="Select an option" /></SelectTrigger>
                  <SelectContent>{field.options?.map(option => <SelectItem key={option.value} value={option.value}>{option.label}</SelectItem>)}</SelectContent>
                </Select>
              ) : field.type === 'boolean' ? (
                <div className="flex h-10 items-center gap-3 rounded-md border px-3">
                  <Switch id={`${decision.decisionKey}-${field.key}`} disabled={!editable} checked={Boolean(current)} onCheckedChange={next => setField(field.key, next)} />
                  <span className="text-sm text-muted-foreground">{current ? 'Enabled' : 'Disabled'}</span>
                </div>
              ) : field.type === 'textarea' ? (
                <Textarea id={`${decision.decisionKey}-${field.key}`} disabled={!editable} value={String(current ?? '')} onChange={event => setField(field.key, event.target.value)} placeholder={field.placeholder} />
              ) : field.type === 'textList' ? (
                <Textarea id={`${decision.decisionKey}-${field.key}`} disabled={!editable} value={Array.isArray(current) ? current.join('\n') : ''} onChange={event => setField(field.key, event.target.value.split(/[\n,]/).map(item => item.trim()).filter(Boolean))} placeholder={field.placeholder ?? 'One value per line'} />
              ) : (
                <Input id={`${decision.decisionKey}-${field.key}`} disabled={!editable} type={field.type} min={field.min} max={field.max} step={field.step} value={String(current ?? '')} onChange={event => setField(field.key, field.type === 'number' ? (event.target.value === '' ? undefined : Number(event.target.value)) : event.target.value)} placeholder={field.placeholder} />
              )}
            </div>
          );
        })}
      </div>

      <div className="border-t pt-5">
        <h3 className="mb-3 text-sm font-semibold">Ownership and approval</h3>
        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2">
            <Label>Owner group</Label>
            <Select disabled={!editable} value={ownerGroup} onValueChange={setOwnerGroup}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>{procurementDecisionOwnerOptions.map(group => <SelectItem key={group} value={group}>{group}</SelectItem>)}</SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>Decision status</Label>
            <Select disabled={!editable} value={status} onValueChange={next => setStatus(next as ProcurementConfigurationDecisionStatus)}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="Draft">Draft</SelectItem>
                <SelectItem value="Proposed">Proposed</SelectItem>
                {isSuperAdmin && <SelectItem value="Approved">Approved</SelectItem>}
                {isSuperAdmin && <SelectItem value="Rejected">Rejected</SelectItem>}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>Approval reference</Label>
            <Input disabled={!editable} value={approvalReference} onChange={event => setApprovalReference(event.target.value)} placeholder="Committee minute or workflow reference" />
          </div>
          <div className="space-y-2">
            <Label>Source lineage</Label>
            <Input disabled={!editable} value={sourceLineage} onChange={event => setSourceLineage(event.target.value)} placeholder="Policy, law, circular, or source system" />
          </div>
          <div className="space-y-2 md:col-span-2">
            <Label>Notes</Label>
            <Textarea disabled={!editable} value={notes} onChange={event => setNotes(event.target.value)} />
          </div>
          <div className="space-y-2 md:col-span-2">
            <Label>Change reason</Label>
            <Input disabled={!editable} value={reason} onChange={event => setReason(event.target.value)} placeholder="Included in the immutable revision history" />
          </div>
        </div>
        {editable && (
          <div className="mt-4 flex items-center justify-between gap-3">
            <p className="text-xs text-muted-foreground">{missingRequired.length ? `${missingRequired.length} required field(s) still need values.` : 'Required fields have values; server validation remains authoritative.'}</p>
            <Button onClick={() => save.mutate()} disabled={save.isPending || missingRequired.length > 0}>
              <Save className="mr-2 h-4 w-4" />{save.isPending ? 'Saving…' : 'Save decision'}
            </Button>
          </div>
        )}
      </div>

      <div className="border-t pt-5">
        <h3 className="text-sm font-semibold">Shared evidence links</h3>
        <p className="mt-1 text-xs text-muted-foreground">Files remain in the platform upload store; this profile only keeps a governed reference.</p>
        <div className="mt-3 space-y-2">
          {decision.evidence.length === 0 && <p className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">No evidence linked.</p>}
          {decision.evidence.map(item => (
            <div key={item.id} className="flex items-center justify-between gap-3 rounded-md border p-3 text-sm">
              <div className="min-w-0"><p className="truncate font-medium">{item.originalFileName || item.externalReference || item.evidenceType}</p><p className="text-xs text-muted-foreground">{item.virusScanStatus || 'External reference'} · {new Date(item.uploadedAt).toLocaleString()}</p></div>
              {editable && <Button variant="ghost" size="icon" title="Unlink evidence" onClick={() => unlinkEvidence.mutate(item.id)} disabled={unlinkEvidence.isPending}><Trash2 className="h-4 w-4" /></Button>}
            </div>
          ))}
        </div>
        {editable && (
          <div className="mt-4 grid gap-3 md:grid-cols-[1fr_auto]">
            <Input value={externalReference} onChange={event => setExternalReference(event.target.value)} placeholder="External evidence reference or URL" />
            <Button variant="outline" onClick={() => linkEvidence.mutate(undefined)} disabled={linkEvidence.isPending || !externalReference.trim()}><Link2 className="mr-2 h-4 w-4" />Link reference</Button>
            <div className="md:col-span-2">
              <Label className="inline-flex cursor-pointer items-center gap-2 rounded-md border px-3 py-2 text-sm font-medium hover:bg-accent">
                <FileUp className="h-4 w-4" />Upload shared evidence
                <input className="sr-only" type="file" onChange={event => { const file = event.target.files?.[0]; if (file) linkEvidence.mutate(file); event.target.value = ''; }} />
              </Label>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
