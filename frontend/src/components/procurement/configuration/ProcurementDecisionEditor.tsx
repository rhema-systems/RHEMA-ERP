'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { FileUp, Link2, Save, Trash2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import {
  normalizeDecisionFormValue,
  procurementDecisionFormRegistry,
  procurementDecisionOwnerOptions,
} from '@/lib/procurement-configuration-registry';
import { fileUploadService } from '@/services/fileUploadService';
import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import { procurementConfigurationService } from '@/services/procurement-configuration.service';
import { workflowApiService } from '@/services/workflow-api.service';
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
  const isSupplierFeeDecision = decision.decisionKey === 'DEC-007';

  const accountLookup = useQuery({
    queryKey: ['procurement-configuration', 'dec-007', 'accounts'],
    queryFn: () => financeDataService.getAccounts({ status: 'Active', pageSize: 1000 }),
    enabled: isSupplierFeeDecision,
    staleTime: 5 * 60 * 1000,
  });
  const paymentMethodLookup = useQuery({
    queryKey: ['procurement-configuration', 'dec-007', 'payment-methods'],
    queryFn: () => cashManagementDataService.getActivePaymentMethods(),
    enabled: isSupplierFeeDecision,
    staleTime: 5 * 60 * 1000,
  });
  const workflowLookup = useQuery({
    queryKey: ['procurement-configuration', 'dec-007', 'workflows'],
    queryFn: () => workflowApiService.getWorkflowDefinitions({
      page: 1,
      pageSize: 1000,
      isActive: true,
      sortBy: 'name',
      sortDescending: false,
    }),
    enabled: isSupplierFeeDecision,
    staleTime: 5 * 60 * 1000,
  });

  const postingAccounts = useMemo(
    () => (accountLookup.data ?? []).filter(account =>
      account.status === 'Active' && account.allowDirectPosting && account.isPostingAllowed !== false),
    [accountLookup.data],
  );
  const revenueAccounts = useMemo(
    () => postingAccounts.filter(account => account.accountType === 'Revenue'),
    [postingAccounts],
  );
  const taxLiabilityAccounts = useMemo(
    () => postingAccounts.filter(account => account.accountType === 'Liability'),
    [postingAccounts],
  );
  const paymentMethods = useMemo(
    () => (paymentMethodLookup.data ?? []).filter(method => method.isActive && Boolean(method.code?.trim())),
    [paymentMethodLookup.data],
  );
  const publishedWorkflows = useMemo(
    () => (workflowLookup.data?.data ?? []).filter(workflow => workflow.isActive && workflow.lifecycleStatus === 'Published'),
    [workflowLookup.data],
  );

  const missingRequired = useMemo(() => {
    const missing = definition.fields.filter(field => {
    if (!field.required) return false;
    const current = value[field.key];
    if (Array.isArray(current)) return current.length === 0;
    return current === undefined || current === null || current === '';
    });

    if (isSupplierFeeDecision && value.mode === 'paid') {
      if (!Array.isArray(value.paymentChannels) || value.paymentChannels.length === 0) {
        missing.push({ key: 'paymentChannels', label: 'Allowed payment methods', type: 'textList', required: true });
      }
      if (!value.revenueAccountId) {
        missing.push({ key: 'revenueAccountId', label: 'Fee revenue account', type: 'text', required: true });
      }
      if (Number(value.taxPercent ?? 0) > 0 && !value.taxAccountId) {
        missing.push({ key: 'taxAccountId', label: 'Tax liability account', type: 'text', required: true });
      }
    }

    return missing;
  }, [definition.fields, isSupplierFeeDecision, value]);

  const save = useMutation({
    mutationFn: () => {
      const nextStatus = isSuperAdmin ? status : status === 'Approved' ? 'Proposed' : status;
      const request: SaveProcurementConfigurationDecisionRequest = {
        schemaVersion: decision.schemaVersion,
        ownerGroup,
        status: nextStatus,
        approvalStatus: nextStatus === 'Approved' ? 'Approved' : nextStatus === 'Rejected' ? 'Rejected' : 'Pending',
        value: normalizeDecisionFormValue(decision.decisionKey, value),
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
  const togglePaymentMethod = (code: string, checked: boolean) => {
    const selected = Array.isArray(value.paymentChannels)
      ? value.paymentChannels.filter((item): item is string => typeof item === 'string')
      : [];
    setField('paymentChannels', checked
      ? Array.from(new Set([...selected, code]))
      : selected.filter(item => item !== code));
  };

  return (
    <div className="space-y-5">
      <div className="grid gap-4 md:grid-cols-2">
        {definition.fields.map(field => {
          const current = value[field.key];
          const isPaymentMethods = isSupplierFeeDecision && field.key === 'paymentChannels';
          const isAccount = isSupplierFeeDecision && (field.key === 'revenueAccountId' || field.key === 'taxAccountId');
          const isExemptionWorkflow = isSupplierFeeDecision && field.key === 'exemptionWorkflowDefinitionId';
          const accountOptions = field.key === 'revenueAccountId' ? revenueAccounts : taxLiabilityAccounts;
          return (
            <div key={field.key} className={field.type === 'textarea' || field.type === 'textList' ? 'space-y-2 md:col-span-2' : 'space-y-2'}>
              <Label htmlFor={`${decision.decisionKey}-${field.key}`}>{field.label}{field.required ? ' *' : ''}</Label>
              {isPaymentMethods ? (
                <div className="rounded-md border p-3">
                  {paymentMethodLookup.isLoading ? (
                    <p className="text-sm text-muted-foreground">Loading active payment methods…</p>
                  ) : paymentMethodLookup.isError ? (
                    <div className="flex items-center justify-between gap-3">
                      <p className="text-sm text-destructive">Active payment methods could not be loaded.</p>
                      <Button type="button" variant="outline" size="sm" onClick={() => paymentMethodLookup.refetch()}>Retry</Button>
                    </div>
                  ) : paymentMethods.length === 0 ? (
                    <p className="text-sm text-muted-foreground">No active coded payment methods are configured in Finance.</p>
                  ) : (
                    <div className="grid gap-2 sm:grid-cols-2">
                      {paymentMethods.map(method => {
                        const code = method.code?.trim().toUpperCase();
                        if (!code) return null;
                        const checked = Array.isArray(current) && current.includes(code);
                        return (
                          <Label key={method.id} className="flex cursor-pointer items-start gap-3 rounded-md border p-3 font-normal hover:bg-muted/50">
                            <Checkbox
                              checked={checked}
                              disabled={!editable}
                              onCheckedChange={next => togglePaymentMethod(code, next === true)}
                              aria-label={`${method.name} (${code})`}
                            />
                            <span className="min-w-0">
                              <span className="block text-sm font-medium">{method.name}</span>
                              <span className="block text-xs text-muted-foreground">{code}</span>
                            </span>
                          </Label>
                        );
                      })}
                    </div>
                  )}
                </div>
              ) : isAccount ? (
                <>
                  <Select
                    disabled={!editable || accountLookup.isLoading || accountLookup.isError}
                    value={String(current || '__none__')}
                    onValueChange={next => setField(field.key, next === '__none__' ? undefined : next)}
                  >
                    <SelectTrigger id={`${decision.decisionKey}-${field.key}`}>
                      <SelectValue placeholder={accountLookup.isLoading ? 'Loading accounts…' : 'Select a GL account'} />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="__none__">Not selected</SelectItem>
                      {accountOptions.map(account => (
                        <SelectItem key={account.id} value={account.id}>
                          {account.accountNumber || account.accountCode} — {account.accountName}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  {accountLookup.isError && (
                    <div className="flex items-center justify-between gap-3 rounded-md border border-destructive/30 bg-destructive/5 p-2">
                      <p className="text-xs text-destructive">Finance GL accounts could not be loaded.</p>
                      <Button type="button" variant="outline" size="sm" onClick={() => accountLookup.refetch()}>Retry</Button>
                    </div>
                  )}
                  {!accountLookup.isLoading && !accountLookup.isError && accountOptions.length === 0 && (
                    <p className="text-xs text-muted-foreground">No eligible active direct-posting {field.key === 'revenueAccountId' ? 'revenue' : 'liability'} account is configured.</p>
                  )}
                </>
              ) : isExemptionWorkflow ? (
                <>
                  <Select
                    disabled={!editable || workflowLookup.isLoading || workflowLookup.isError}
                    value={String(current || '__none__')}
                    onValueChange={next => setField(field.key, next === '__none__' ? undefined : next)}
                  >
                    <SelectTrigger id={`${decision.decisionKey}-${field.key}`}>
                      <SelectValue placeholder={workflowLookup.isLoading ? 'Loading workflows…' : 'Select a published workflow'} />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="__none__">Not selected</SelectItem>
                      {publishedWorkflows.map(workflow => (
                        <SelectItem key={workflow.id} value={workflow.id}>
                          {workflow.name} — v{workflow.version}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  {workflowLookup.isError && (
                    <div className="flex items-center justify-between gap-3 rounded-md border border-destructive/30 bg-destructive/5 p-2">
                      <p className="text-xs text-destructive">Published workflow definitions could not be loaded.</p>
                      <Button type="button" variant="outline" size="sm" onClick={() => workflowLookup.refetch()}>Retry</Button>
                    </div>
                  )}
                  {!workflowLookup.isLoading && !workflowLookup.isError && publishedWorkflows.length === 0 && (
                    <p className="text-xs text-muted-foreground">No active Published workflow definition is available.</p>
                  )}
                </>
              ) : field.type === 'select' ? (
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
                <Input id={`${decision.decisionKey}-${field.key}`} disabled={!editable} type={field.type} min={field.min} max={field.max} step={field.step} value={String(current ?? '')} onChange={event => setField(field.key, field.type === 'number' ? (event.target.value === '' ? undefined : Number(event.target.value)) : field.type === 'date' && event.target.value === '' ? undefined : event.target.value)} placeholder={field.placeholder} />
              )}
              {isSupplierFeeDecision && field.key === 'receiptNumberFormat' && (
                <p className="text-xs text-muted-foreground">Include <code>{'{SEQ}'}</code> or a hash sequence such as <code>{'{######}'}</code>. Example: <code>SUP-REC-{'{YYYY}'}-{'{######}'}</code>.</p>
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
                {status === 'Withdrawn' && <SelectItem value="Withdrawn" disabled>Withdrawn · inactive</SelectItem>}
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
