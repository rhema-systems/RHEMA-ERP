'use client';

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { AlertCircle, ArrowLeft, Loader2, Plus, RefreshCw, ShieldAlert } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { financeDataService } from '@/services/finance/finance-data.service';
import { getAccountingBookApplicabilityAccess } from '@/components/finance/accounting-books/accounting-book-applicability-access';
import type { AccountingBookApplicabilityEligibleBook, AccountingBookApplicabilityPolicy, AccountingBookPostingIdentity, AccountingBookSelection, SaveAccountingBookApplicabilityPolicy, SaveAccountingBookApplicabilityRule } from '@/types/finance';

const blankRule = (): SaveAccountingBookApplicabilityRule => ({
  ruleCode: '',
  priority: 100,
  originatingModuleCode: '',
  sourceDocumentType: '',
  postingAction: '',
  sortOrder: 1,
  accountingBookIds: [],
});
const blankPolicy = (): SaveAccountingBookApplicabilityPolicy => ({
  policyCode: '',
  name: '',
  description: '',
  effectiveFrom: new Date().toISOString().slice(0, 10),
  effectiveTo: null,
  reason: '',
  rowVersion: null,
  rules: [blankRule()],
});
const utcDate = (value: string) => (value ? `${value.slice(0, 10)}T00:00:00.000Z` : '');

const getRuleIssues = (
  rule: SaveAccountingBookApplicabilityRule,
  identities: AccountingBookPostingIdentity[]
) => {
  const issues: string[] = [];
  if (!rule.ruleCode.trim()) issues.push('rule code');
  if (rule.priority < 0 || rule.priority > 1000) issues.push('priority between 0 and 1000');
  if (!rule.originatingModuleCode) issues.push('origin module');
  if (!rule.sourceDocumentType) issues.push('document type');
  if (!rule.postingAction) issues.push('posting action');
  if (
    rule.originatingModuleCode &&
    rule.sourceDocumentType &&
    rule.postingAction &&
    !identities.some(
      (item) =>
        item.originatingModuleCode === rule.originatingModuleCode &&
        item.sourceDocumentType === rule.sourceDocumentType &&
        item.postingAction === rule.postingAction
    )
  )
    issues.push('registered transaction identity');
  if (rule.accountingBookIds.length === 0) issues.push('at least one destination book');
  return issues;
};

export default function AccountingBookApplicabilityPage() {
  const { user, hasPermission, isLoading: authLoading, error: authError } = useAuth();
  const access = getAccountingBookApplicabilityAccess(hasPermission);
  const { toast } = useToast();
  const [policies, setPolicies] = useState<AccountingBookApplicabilityPolicy[]>([]);
  const [books, setBooks] = useState<AccountingBookApplicabilityEligibleBook[]>([]);
  const [postingIdentities, setPostingIdentities] = useState<AccountingBookPostingIdentity[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [editor, setEditor] = useState<AccountingBookApplicabilityPolicy | null | undefined>(undefined);
  const [successorSource, setSuccessorSource] = useState<AccountingBookApplicabilityPolicy | null>(null);
  const [form, setForm] = useState<SaveAccountingBookApplicabilityPolicy>(blankPolicy());
  const [saving, setSaving] = useState(false);
  const [decision, setDecision] = useState<{
    policy: AccountingBookApplicabilityPolicy;
    action: 'submit' | 'approve' | 'reject' | 'retire' | 'retire/approve' | 'retire/reject';
  } | null>(null);
  const [decisionReason, setDecisionReason] = useState('');
  const [previewInput, setPreviewInput] = useState({
    effectiveDate: new Date().toISOString().slice(0, 10),
    originatingModuleCode: '',
    sourceDocumentType: '',
    postingAction: '',
  });
  const [preview, setPreview] = useState<AccountingBookSelection | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [previewing, setPreviewing] = useState(false);

  const load = useCallback(async () => {
    if (!access.canRead) return;
    setLoading(true);
    setError(null);
    try {
      const [policyRows, bookRows, identityRows] = await Promise.all([financeDataService.getAccountingBookApplicabilityPolicies(), financeDataService.getAccountingBookApplicabilityEligibleBooks(), financeDataService.getAccountingBookPostingIdentities()]);
      setPolicies([...policyRows].sort((a, b) => a.policyCode.localeCompare(b.policyCode) || b.version - a.version));
      setBooks([...bookRows].sort((a, b) => Number(b.isDefault) - Number(a.isDefault) || a.code.localeCompare(b.code)));
      setPostingIdentities(identityRows);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Applicability policy could not be loaded.');
    } finally {
      setLoading(false);
    }
  }, [access.canRead]);

  const modules = useMemo(() => Array.from(new Map(postingIdentities.map((item) => [item.originatingModuleCode, { code: item.originatingModuleCode, name: item.moduleName }])).values()), [postingIdentities]);
  const documentsFor = (module: string) => Array.from(new Map(postingIdentities.filter((item) => item.originatingModuleCode === module).map((item) => [item.sourceDocumentType, { code: item.sourceDocumentType, name: item.documentTypeName }])).values());
  const actionsFor = (module: string, document: string) => postingIdentities.filter((item) => item.originatingModuleCode === module && item.sourceDocumentType === document);
  const isRegistered = (module: string, document: string, action: string) => postingIdentities.some((item) => item.originatingModuleCode === module && item.sourceDocumentType === document && item.postingAction === action);
  const ruleIssues = useMemo(
    () => form.rules.map((rule) => getRuleIssues(rule, postingIdentities)),
    [form.rules, postingIdentities]
  );
  const draftIssues = useMemo(() => {
    const issues: string[] = [];
    if (!form.policyCode.trim()) issues.push('Enter the stable policy code.');
    if (!form.name.trim()) issues.push('Enter the policy name.');
    ruleIssues.forEach((items, index) => {
      if (items.length > 0) issues.push(`Complete Rule ${index + 1}: ${items.join(', ')}.`);
    });
    if (!form.reason.trim()) issues.push('Enter the reason for creating or changing the policy.');
    return issues;
  }, [form.name, form.policyCode, form.reason, ruleIssues]);

  useEffect(() => {
    if (!authLoading && access.canRead) void load();
    if (!authLoading && !access.canRead) setLoading(false);
  }, [access.canRead, authLoading, load]);

  const openEditor = (policy?: AccountingBookApplicabilityPolicy) => {
    setSuccessorSource(null);
    setEditor(policy ?? null);
    setForm(
      policy
        ? {
            policyCode: policy.policyCode,
            name: policy.name,
            description: policy.description ?? '',
            effectiveFrom: policy.effectiveFrom.slice(0, 10),
            effectiveTo: policy.effectiveTo?.slice(0, 10) ?? null,
            reason: '',
            rowVersion: policy.rowVersion,
            rules: policy.rules.map((rule) => ({
              ruleCode: rule.ruleCode,
              priority: rule.priority,
              originatingModuleCode: rule.originatingModuleCode,
              sourceDocumentType: rule.sourceDocumentType,
              postingAction: rule.postingAction,
              sortOrder: rule.sortOrder,
              accountingBookIds: rule.selectedBooks.map((book) => book.accountingBookId),
            })),
          }
        : blankPolicy()
    );
  };
  const openSuccessorEditor = (policy: AccountingBookApplicabilityPolicy) => {
    setSuccessorSource(policy);
    setEditor(null);
    setForm({
      policyCode: policy.policyCode,
      name: policy.name,
      description: policy.description ?? '',
      effectiveFrom: new Date().toISOString().slice(0, 10),
      effectiveTo: null,
      reason: '',
      rowVersion: null,
      rules: policy.rules.map((rule) => ({
        ruleCode: rule.ruleCode,
        priority: rule.priority,
        originatingModuleCode: rule.originatingModuleCode,
        sourceDocumentType: rule.sourceDocumentType,
        postingAction: rule.postingAction,
        sortOrder: rule.sortOrder,
        accountingBookIds: rule.selectedBooks.map((book) => book.accountingBookId),
      })),
    });
  };
  const updateRule = (index: number, change: Partial<SaveAccountingBookApplicabilityRule>) =>
    setForm((current) => ({
      ...current,
      rules: current.rules.map((rule, itemIndex) => (itemIndex === index ? { ...rule, ...change } : rule)),
    }));
  const toggleBook = (index: number, id: string) => {
    const ids = form.rules[index].accountingBookIds;
    updateRule(index, {
      accountingBookIds: ids.includes(id) ? ids.filter((value) => value !== id) : [...ids, id],
    });
  };
  const save = async () => {
    setSaving(true);
    try {
      const request: SaveAccountingBookApplicabilityPolicy = {
        ...form,
        policyCode: form.policyCode.trim().toUpperCase(),
        effectiveFrom: utcDate(form.effectiveFrom),
        effectiveTo: form.effectiveTo ? utcDate(form.effectiveTo) : null,
        reason: form.reason.trim(),
        rules: form.rules.map((rule) => ({
          ...rule,
          ruleCode: rule.ruleCode.trim().toUpperCase(),
          originatingModuleCode: rule.originatingModuleCode.trim().toUpperCase(),
          sourceDocumentType: rule.sourceDocumentType.trim().toUpperCase(),
          postingAction: rule.postingAction.trim().toUpperCase(),
        })),
      };
      if (editor) await financeDataService.updateAccountingBookApplicabilityPolicy(editor.id, request);
      else await financeDataService.createAccountingBookApplicabilityPolicy(request);
      setEditor(undefined);
      setSuccessorSource(null);
      await load();
      toast({ title: 'Applicability draft saved' });
    } catch (reason) {
      toast({
        variant: 'destructive',
        title: 'Could not save policy',
        description: reason instanceof Error ? reason.message : 'Request failed.',
      });
    } finally {
      setSaving(false);
    }
  };
  const decide = async () => {
    if (!decision) return;
    setSaving(true);
    try {
      await financeDataService.decideAccountingBookApplicabilityPolicy(decision.policy.id, decision.action, {
        reason: decisionReason.trim(),
        rowVersion: decision.policy.rowVersion,
      });
      setDecision(null);
      await load();
      toast({ title: `Policy ${decision.action} completed` });
    } catch (reason) {
      toast({
        variant: 'destructive',
        title: 'Policy action failed',
        description: reason instanceof Error ? reason.message : 'Request failed.',
      });
    } finally {
      setSaving(false);
    }
  };
  const resolve = async () => {
    setPreviewing(true);
    setPreviewError(null);
    setPreview(null);
    try {
      setPreview(
        await financeDataService.resolveAccountingBookApplicability({
          ...previewInput,
          effectiveDate: utcDate(previewInput.effectiveDate),
          originatingModuleCode: previewInput.originatingModuleCode.trim().toUpperCase(),
          sourceDocumentType: previewInput.sourceDocumentType.trim(),
          postingAction: previewInput.postingAction.trim(),
        })
      );
    } catch (reason) {
      setPreviewError(reason instanceof Error ? reason.message : 'Selection preview failed.');
    } finally {
      setPreviewing(false);
    }
  };

  if (authLoading)
    return (
      <div className="flex justify-center p-12">
        <Loader2 className="h-6 w-6 animate-spin" aria-label="Loading applicability access" />
      </div>
    );
  if (authError)
    return (
      <Alert variant="destructive">
        <AlertTitle>Authorization unavailable</AlertTitle>
        <AlertDescription>{authError.message}</AlertDescription>
      </Alert>
    );
  if (!access.canRead)
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Permission required</AlertTitle>
        <AlertDescription>Accounting-book applicability policy read permission is required. Finance report or book access does not substitute for this configuration authority.</AlertDescription>
      </Alert>
    );

  return (
    <div className="space-y-6 p-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <Button asChild variant="ghost" size="sm">
            <Link href="/finance/settings/accounting-books">
              <ArrowLeft className="mr-2 h-4 w-4" />
              Accounting books
            </Link>
          </Button>
          <h1 className="mt-2 text-2xl font-semibold">Accounting-book applicability</h1>
          <p className="text-sm text-muted-foreground">Finance-owned selection policy. Source modules submit one stable identity; Finance determines eligible full books.</p>
        </div>
        {access.canManage && (
          <Button onClick={() => openEditor()}>
            <Plus className="mr-2 h-4 w-4" />
            New policy draft
          </Button>
        )}
      </div>
      <Alert className="border-amber-300 bg-amber-50">
        <ShieldAlert className="h-4 w-4" />
        <AlertTitle>No posting occurs here</AlertTitle>
        <AlertDescription>When no approved rule matches, resolution selects exactly the primary/default full book. Delta books and pseudo selectors are never eligible. C1 still blocks cross-book execution until later orchestration is approved.</AlertDescription>
      </Alert>
      {loading ? (
        <div className="flex justify-center p-12">
          <Loader2 className="h-6 w-6 animate-spin" aria-label="Loading applicability policies" />
        </div>
      ) : error ? (
        <Alert variant="destructive">
          <AlertTitle>Could not load applicability policy</AlertTitle>
          <AlertDescription>
            {error}{' '}
            <Button variant="link" className="h-auto p-0" onClick={() => void load()}>
              <RefreshCw className="mr-1 h-3 w-3" />
              Retry
            </Button>
          </AlertDescription>
        </Alert>
      ) : policies.length === 0 ? (
        <Card>
          <CardContent className="py-10 text-center text-muted-foreground">No applicability policies are configured. Primary-only fallback remains authoritative.</CardContent>
        </Card>
      ) : (
        <div className="grid gap-4">
          {policies.map((policy) => (
            <Card key={policy.id}>
              <CardHeader>
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <CardTitle>
                      {policy.policyCode} · v{policy.version}
                    </CardTitle>
                    <CardDescription>{policy.name}</CardDescription>
                  </div>
                  <Badge variant={policy.status === 'Approved' ? 'default' : 'secondary'}>{policy.status}</Badge>
                </div>
              </CardHeader>
              <CardContent className="space-y-3 text-sm">
                <p>
                  Effective {policy.effectiveFrom.slice(0, 10)} to {policy.effectiveTo?.slice(0, 10) ?? 'open ended'}
                </p>
                {policy.retirementDecisionStatus === 'Pending' && (
                  <Alert className="border-amber-300 bg-amber-50">
                    <AlertTitle>Retirement awaiting checker</AlertTitle>
                    <AlertDescription>{policy.retirementReason}</AlertDescription>
                  </Alert>
                )}
                {policy.rules.map((rule) => (
                  <div key={rule.id} className="rounded border p-3">
                    <div className="font-medium">
                      {rule.ruleCode} · priority {rule.priority}
                    </div>
                    <div>
                      {rule.originatingModuleCode} / {rule.sourceDocumentType} / {rule.postingAction}
                    </div>
                    <div className="text-muted-foreground">Books: {rule.selectedBooks.map((book) => book.accountingBookCode).join(', ')}</div>
                  </div>
                ))}
                <div className="flex flex-wrap gap-2">
                  {access.canManage && policy.status === 'Draft' && (
                    <>
                      <Button size="sm" variant="outline" onClick={() => openEditor(policy)}>
                        Edit draft
                      </Button>
                      <Button
                        size="sm"
                        onClick={() => {
                          setDecision({ policy, action: 'submit' });
                          setDecisionReason('');
                        }}
                      >
                        Submit
                      </Button>
                    </>
                  )}
                  {access.canApprove && policy.status === 'PendingApproval' &&
                    user?.id?.toLowerCase() !== policy.preparedByUserId.toLowerCase() && (
                    <>
                      <Button
                        size="sm"
                        onClick={() => {
                          setDecision({ policy, action: 'approve' });
                          setDecisionReason('');
                        }}
                      >
                        Approve
                      </Button>
                      <Button
                        size="sm"
                        variant="destructive"
                        onClick={() => {
                          setDecision({ policy, action: 'reject' });
                          setDecisionReason('');
                        }}
                      >
                        Reject
                      </Button>
                    </>
                  )}
                  {access.canManage && policy.status === 'Approved' && policy.retirementDecisionStatus !== 'Pending' && (
                    <>
                      <Button size="sm" variant="outline" onClick={() => openSuccessorEditor(policy)}>
                        Create successor version
                      </Button>
                      <Button
                        size="sm"
                        variant="destructive"
                        onClick={() => {
                          setDecision({ policy, action: 'retire' });
                          setDecisionReason('');
                        }}
                      >
                        Request retirement
                      </Button>
                    </>
                  )}
                  {access.canManage && policy.status === 'Retired' && (
                    <Button size="sm" variant="outline" onClick={() => openSuccessorEditor(policy)}>
                      Create successor version
                    </Button>
                  )}
                  {access.canApprove && policy.status === 'Approved' && policy.retirementDecisionStatus === 'Pending' &&
                    user?.id?.toLowerCase() !== policy.retirementRequestedByUserId?.toLowerCase() && (
                    <>
                      <Button
                        size="sm"
                        variant="destructive"
                        onClick={() => {
                          setDecision({ policy, action: 'retire/approve' });
                          setDecisionReason('');
                        }}
                      >
                        Approve retirement
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => {
                          setDecision({ policy, action: 'retire/reject' });
                          setDecisionReason('');
                        }}
                      >
                        Reject retirement
                      </Button>
                    </>
                  )}
                </div>
                {policy.status === 'PendingApproval' && user?.id &&
                  user.id.toLowerCase() === policy.preparedByUserId.toLowerCase() && (
                    <p className="text-xs text-muted-foreground">Awaiting a different authorized checker.</p>
                  )}
                {policy.retirementDecisionStatus === 'Pending' && user?.id &&
                  user.id.toLowerCase() === policy.retirementRequestedByUserId?.toLowerCase() && (
                    <p className="text-xs text-muted-foreground">Retirement is awaiting a different authorized checker.</p>
                  )}
              </CardContent>
            </Card>
          ))}
        </div>
      )}
      {access.canResolve && (
        <Card>
          <CardHeader>
            <CardTitle>Selection preview</CardTitle>
            <CardDescription>Checks which active, ready accounting books would be selected for the registered transaction details you choose. This is a safe preview only—no transaction, journal, or posting is created.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-3 md:grid-cols-4">
              <div>
                <Label htmlFor="resolve-date">Effective date</Label>
                <Input
                  id="resolve-date"
                  type="date"
                  value={previewInput.effectiveDate}
                  onChange={(event) =>
                    setPreviewInput({
                      ...previewInput,
                      effectiveDate: event.target.value,
                    })
                  }
                />
              </div>
              <div>
                <Label htmlFor="resolve-module">Origin module</Label>
                <Select
                  value={previewInput.originatingModuleCode}
                  onValueChange={(value) =>
                    setPreviewInput({
                      ...previewInput,
                      originatingModuleCode: value,
                      sourceDocumentType: '',
                      postingAction: '',
                    })
                  }
                >
                  <SelectTrigger id="resolve-module" aria-label="Preview origin module">
                    <SelectValue placeholder="Select module" />
                  </SelectTrigger>
                  <SelectContent>
                    {modules.map((module) => (
                      <SelectItem key={module.code} value={module.code}>
                        {module.name} ({module.code})
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div>
                <Label htmlFor="resolve-document">Document type</Label>
                <Select
                  disabled={!previewInput.originatingModuleCode}
                  value={previewInput.sourceDocumentType}
                  onValueChange={(value) =>
                    setPreviewInput({
                      ...previewInput,
                      sourceDocumentType: value,
                      postingAction: '',
                    })
                  }
                >
                  <SelectTrigger id="resolve-document" aria-label="Preview document type">
                    <SelectValue placeholder="Select document type" />
                  </SelectTrigger>
                  <SelectContent>
                    {documentsFor(previewInput.originatingModuleCode).map((document) => (
                      <SelectItem key={document.code} value={document.code}>
                        {document.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div>
                <Label htmlFor="resolve-action">Posting action</Label>
                <Select disabled={!previewInput.sourceDocumentType} value={previewInput.postingAction} onValueChange={(value) => setPreviewInput({ ...previewInput, postingAction: value })}>
                  <SelectTrigger id="resolve-action" aria-label="Preview posting action">
                    <SelectValue placeholder="Select posting action" />
                  </SelectTrigger>
                  <SelectContent>
                    {actionsFor(previewInput.originatingModuleCode, previewInput.sourceDocumentType).map((action) => (
                      <SelectItem key={action.postingAction} value={action.postingAction}>
                        {action.postingActionName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <Button disabled={previewing || !previewInput.effectiveDate || !isRegistered(previewInput.originatingModuleCode, previewInput.sourceDocumentType, previewInput.postingAction)} onClick={() => void resolve()}>
              {previewing && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Preview selection
            </Button>
            {previewError && (
              <Alert variant="destructive">
                <AlertTitle>Selection unavailable</AlertTitle>
                <AlertDescription>
                  {previewError}{' '}
                  <Button variant="link" className="h-auto p-0" onClick={() => void resolve()}>
                    Retry
                  </Button>
                </AlertDescription>
              </Alert>
            )}
            {preview && (
              <div className="rounded border p-4 text-sm">
                <div className="flex gap-2">
                  <Badge>{preview.usedPrimaryOnlyFallback ? 'Primary-only fallback' : 'Approved rule'}</Badge>
                  <span aria-label="Selected destination books">
                    {preview.books.map((book) => book.accountingBookCode).join(', ') || 'No eligible books'}
                  </span>
                </div>
                {preview.blockers.length > 0 && (
                  <ul className="mt-3 list-disc pl-5 text-destructive">
                    {preview.blockers.map((blocker) => (
                      <li key={`${blocker.code}-${blocker.accountingBookId ?? ''}`}>
                        {blocker.code}: {blocker.message}
                      </li>
                    ))}
                  </ul>
                )}
                <p className="mt-3 break-all text-xs text-muted-foreground">Selection fingerprint: {preview.selectionFingerprint}</p>
              </div>
            )}
          </CardContent>
        </Card>
      )}
      <Dialog
        open={editor !== undefined}
        onOpenChange={(open) => {
          if (!open) {
            setEditor(undefined);
            setSuccessorSource(null);
          }
        }}
      >
        <DialogContent className="max-h-[90vh] max-w-3xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{editor ? 'Edit policy draft' : successorSource ? 'Create successor policy version' : 'New applicability policy'}</DialogTitle>
            <DialogDescription>Select a Finance-registered transaction identity. Higher priority numbers take precedence; equal-priority overlaps are rejected.</DialogDescription>
          </DialogHeader>
          {editor && (
            <Alert>
              <AlertTitle>Versioned rule structure</AlertTitle>
              <AlertDescription>Change values or replace selected books within this draft. Changing rule or book cardinality requires a new policy version so rows are never physically deleted.</AlertDescription>
            </Alert>
          )}
          {successorSource && (
            <Alert>
              <AlertTitle>Creating {successorSource.policyCode} version {successorSource.version + 1}</AlertTitle>
              <AlertDescription>
                The stable code and existing rules were copied from version {successorSource.version}. Saving creates a new Draft linked to that immutable version; review the effective dates and change reason before submitting it.
              </AlertDescription>
            </Alert>
          )}
          <div className="grid gap-3 md:grid-cols-2">
            <div>
              <Label htmlFor="policy-code">Stable policy code</Label>
              <Input id="policy-code" disabled={Boolean(editor || successorSource)} value={form.policyCode} onChange={(event) => setForm({ ...form, policyCode: event.target.value })} />
            </div>
            <div>
              <Label htmlFor="policy-name">Name</Label>
              <Input id="policy-name" value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} />
            </div>
            <div>
              <Label htmlFor="effective-from">Effective from</Label>
              <Input id="effective-from" type="date" value={form.effectiveFrom.slice(0, 10)} onChange={(event) => setForm({ ...form, effectiveFrom: event.target.value })} />
            </div>
            <div>
              <Label htmlFor="effective-to">Effective to</Label>
              <Input id="effective-to" type="date" value={form.effectiveTo?.slice(0, 10) ?? ''} onChange={(event) => setForm({ ...form, effectiveTo: event.target.value || null })} />
            </div>
            <div className="md:col-span-2">
              <Label htmlFor="policy-description">Description</Label>
              <Textarea id="policy-description" value={form.description ?? ''} onChange={(event) => setForm({ ...form, description: event.target.value })} />
            </div>
            {form.rules.map((rule, index) => (
              <div className="space-y-3 rounded border p-4 md:col-span-2" key={index}>
                <div className="flex items-center justify-between gap-3">
                  <p className="font-medium">Rule {index + 1}</p>
                  {ruleIssues[index].length > 0 && <Badge variant="secondary">Incomplete</Badge>}
                </div>
                <div className="grid gap-3 md:grid-cols-3">
                  <div>
                    <Label>Rule code</Label>
                    <Input aria-label={`Rule ${index + 1} code`} value={rule.ruleCode} onChange={(event) => updateRule(index, { ruleCode: event.target.value })} />
                  </div>
                  <div>
                    <Label>Priority</Label>
                    <Input
                      aria-label={`Rule ${index + 1} priority`}
                      type="number"
                      min={0}
                      max={1000}
                      value={rule.priority}
                      onChange={(event) =>
                        updateRule(index, {
                          priority: Number(event.target.value),
                        })
                      }
                    />
                    <p className="mt-1 text-xs text-muted-foreground">0–1000. Higher numbers take precedence.</p>
                  </div>
                  <div>
                    <Label htmlFor={`rule-${index}-module`}>Origin module</Label>
                    <Select
                      value={rule.originatingModuleCode}
                      onValueChange={(value) =>
                        updateRule(index, {
                          originatingModuleCode: value,
                          sourceDocumentType: '',
                          postingAction: '',
                        })
                      }
                    >
                      <SelectTrigger id={`rule-${index}-module`} aria-label={`Rule ${index + 1} origin module`}>
                        <SelectValue placeholder="Select module" />
                      </SelectTrigger>
                      <SelectContent>
                        {modules.map((module) => (
                          <SelectItem key={module.code} value={module.code}>
                            {module.name} ({module.code})
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div>
                    <Label htmlFor={`rule-${index}-document`}>Document type</Label>
                    <Select
                      disabled={!rule.originatingModuleCode}
                      value={rule.sourceDocumentType}
                      onValueChange={(value) =>
                        updateRule(index, {
                          sourceDocumentType: value,
                          postingAction: '',
                        })
                      }
                    >
                      <SelectTrigger id={`rule-${index}-document`} aria-label={`Rule ${index + 1} document type`}>
                        <SelectValue placeholder="Select document type" />
                      </SelectTrigger>
                      <SelectContent>
                        {documentsFor(rule.originatingModuleCode).map((document) => (
                          <SelectItem key={document.code} value={document.code}>
                            {document.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div>
                    <Label htmlFor={`rule-${index}-action`}>Posting action</Label>
                    <Select disabled={!rule.sourceDocumentType} value={rule.postingAction} onValueChange={(value) => updateRule(index, { postingAction: value })}>
                      <SelectTrigger id={`rule-${index}-action`} aria-label={`Rule ${index + 1} posting action`}>
                        <SelectValue placeholder="Select posting action" />
                      </SelectTrigger>
                      <SelectContent>
                        {actionsFor(rule.originatingModuleCode, rule.sourceDocumentType).map((action) => (
                          <SelectItem key={action.postingAction} value={action.postingAction}>
                            {action.postingActionName}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                </div>
                {rule.originatingModuleCode && rule.sourceDocumentType && rule.postingAction && !isRegistered(rule.originatingModuleCode, rule.sourceDocumentType, rule.postingAction) && (
                  <Alert variant="destructive">
                    <AlertTitle>Unregistered transaction identity</AlertTitle>
                    <AlertDescription>Select a registered module, document type and posting action before saving.</AlertDescription>
                  </Alert>
                )}
                <div>
                  <Label>Selected full books</Label>
                  <div className="mt-2 grid gap-2 md:grid-cols-2">
                    {books.map((book) => (
                      <label className="flex items-start gap-2 rounded border p-2" key={book.accountingBookId}>
                        <input type="checkbox" checked={rule.accountingBookIds.includes(book.accountingBookId)} onChange={() => toggleBook(index, book.accountingBookId)} />
                        <span>
                          {book.code} — {book.name}
                          <span className="block text-xs text-muted-foreground">
                            {book.bookType}; {book.lifecycleStatus}; {book.initializationReconciled && book.mappingClassificationReady ? 'ready evidence' : 'readiness blockers'}
                          </span>
                        </span>
                      </label>
                    ))}
                  </div>
                </div>
                {!editor && form.rules.length > 1 && (
                  <Button
                    variant="outline"
                    size="sm"
                    aria-label={`Remove rule ${index + 1}`}
                    onClick={() =>
                      setForm((current) => ({
                        ...current,
                        rules: current.rules.filter((_, itemIndex) => itemIndex !== index),
                      }))
                    }
                  >
                    Remove rule {index + 1}
                  </Button>
                )}
              </div>
            ))}
            {!editor && (
              <Button
                variant="outline"
                className="md:col-span-2"
                disabled={ruleIssues.some((items) => items.length > 0)}
                onClick={() =>
                  setForm((current) => ({
                    ...current,
                    rules: [...current.rules, { ...blankRule(), sortOrder: current.rules.length + 1 }],
                  }))
                }
              >
                Add rule
              </Button>
            )}
            <div className="md:col-span-2">
              <Label htmlFor="policy-reason">Reason</Label>
              <Textarea id="policy-reason" value={form.reason} onChange={(event) => setForm({ ...form, reason: event.target.value })} />
            </div>
          </div>
          {draftIssues.length > 0 && (
            <Alert>
              <AlertCircle className="h-4 w-4" />
              <AlertTitle>Complete the draft before saving</AlertTitle>
              <AlertDescription>
                <ul className="mt-1 list-disc space-y-1 pl-5" aria-label="Draft requirements">
                  {draftIssues.map((issue) => (
                    <li key={issue}>{issue}</li>
                  ))}
                </ul>
              </AlertDescription>
            </Alert>
          )}
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => {
                setEditor(undefined);
                setSuccessorSource(null);
              }}
            >
              Cancel
            </Button>
            <Button disabled={saving || draftIssues.length > 0} onClick={() => void save()}>
              {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Save draft
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <Dialog
        open={decision !== null}
        onOpenChange={(open) => {
          if (!open) setDecision(null);
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{decision?.action.replace('/', ' ')} applicability policy</DialogTitle>
            <DialogDescription>Every lifecycle change is audited. Retirement is requested by a maker and decided by an independent checker.</DialogDescription>
          </DialogHeader>
          <div>
            <Label htmlFor="decision-reason">Reason</Label>
            <Textarea id="decision-reason" value={decisionReason} onChange={(event) => setDecisionReason(event.target.value)} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDecision(null)}>
              Cancel
            </Button>
            <Button disabled={saving || !decisionReason.trim()} onClick={() => void decide()}>
              Confirm {decision?.action.replace('/', ' ')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
