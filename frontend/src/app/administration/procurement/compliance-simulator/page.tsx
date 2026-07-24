'use client';

import { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { AlertTriangle, CheckCircle2, ClipboardCheck, RefreshCw, ShieldAlert } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import {
  complianceOutcomeTone,
  createProcurementComplianceRequest,
  procurementComplianceCategoryOptions,
  procurementComplianceMethodOptions,
} from '@/lib/procurement-compliance';
import { procurementComplianceService } from '@/services/procurement-compliance.service';
import type {
  ProcurementComplianceFinding,
  ProcurementComplianceSimulatorForm,
} from '@/types/procurement-compliance';

const today = () => new Date().toISOString().slice(0, 10);

const initialForm = (): ProcurementComplianceSimulatorForm => ({
  policySetId: '',
  category: 'Goods',
  serviceClass: '',
  amount: '0',
  currencyCode: 'GHS',
  requestedMethod: 'Auto',
  sourceType: 'PurchaseRequisition',
  sourceReference: 'SIMULATION',
  atDate: today(),
  actorUserId: '',
  actorRoles: '',
  sourceOwnerUserId: '',
  sourceOwnerRoles: '',
  entityType: 'PurchaseRequisition',
  action: 'Submit',
  exceptionType: '',
  justificationProvided: false,
  exceptionApprovalReference: '',
  evidenceReferenceKeys: '',
});

const FindingList = ({ items, empty }: { items: ProcurementComplianceFinding[]; empty: string }) => (
  items.length === 0
    ? <p className="text-sm text-muted-foreground">{empty}</p>
    : <div className="space-y-2">{items.map((item, index) => (
      <div key={`${item.code}-${item.ruleId ?? index}`} className="rounded-md border p-3">
        <div className="flex flex-wrap items-center gap-2"><Badge variant="outline">{item.code}</Badge>{item.ruleCode && <Badge variant="secondary">{item.ruleCode}</Badge>}</div>
        <p className="mt-2 text-sm">{item.message}</p>
        {item.sourceDecisionKey && <p className="mt-1 text-xs text-muted-foreground">Governed by {item.sourceDecisionKey}</p>}
      </div>
    ))}</div>
);

export default function ProcurementComplianceSimulatorPage() {
  const { toast } = useToast();
  const [form, setForm] = useState<ProcurementComplianceSimulatorForm>(initialForm);
  const policyDate = form.atDate ? new Date(`${form.atDate}T12:00:00.000Z`).toISOString() : undefined;
  const policies = useQuery({
    queryKey: ['procurement-compliance-policy-options', policyDate],
    queryFn: () => procurementComplianceService.getPolicyOptions(policyDate),
  });
  const evaluate = useMutation({
    mutationFn: () => procurementComplianceService.evaluate(createProcurementComplianceRequest(form)),
    onError: (error: Error) => toast({
      title: 'Unable to evaluate policy',
      description: error.message,
      variant: 'destructive',
    }),
  });

  const selectedPolicy = policies.data?.find(item => item.policySetId === form.policySetId);
  const amount = Number(form.amount);
  const canEvaluate = Number.isFinite(amount) && amount >= 0 && /^[A-Za-z]{3}$/.test(form.currencyCode.trim()) &&
    Boolean(form.sourceType.trim() && form.sourceReference.trim() && form.atDate && form.entityType.trim() && form.action.trim());
  const result = evaluate.data;

  return (
    <div className="space-y-6">
      <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-start">
        <div>
          <h1 className="text-3xl font-bold">Procurement policy simulator</h1>
          <p className="mt-1 max-w-4xl text-muted-foreground">Resolve the effective category, method, threshold, authority, evidence, exception, and SOD rules before any transaction integration is enabled.</p>
        </div>
        <Button variant="outline" onClick={() => policies.refetch()} disabled={policies.isFetching}>
          <RefreshCw className={`mr-2 h-4 w-4 ${policies.isFetching ? 'animate-spin' : ''}`} />Refresh policies
        </Button>
      </div>

      <Alert className="border-blue-500/40 bg-blue-500/5">
        <ClipboardCheck className="h-4 w-4" />
        <AlertTitle>Read-only shared-control decision</AlertTitle>
        <AlertDescription>This page does not update requisitions, orders, receipts, payments, inventory, workflows, or evidence records. Workflow definitions and shared evidence requirement keys are returned as references only; protected runtime actions must invoke the reusable SOD guard.</AlertDescription>
      </Alert>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,0.92fr)_minmax(0,1.08fr)]">
        <Card className="h-fit">
          <CardHeader>
            <CardTitle>Decision context</CardTitle>
            <CardDescription>Choose an effective policy or allow the tenant default to resolve deterministically.</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2 sm:col-span-2">
              <Label>Effective policy</Label>
              <Select value={form.policySetId || 'default'} onValueChange={value => {
                const policySetId = value === 'default' ? '' : value;
                const policy = policies.data?.find(item => item.policySetId === policySetId);
                setForm(current => ({ ...current, policySetId, currencyCode: policy?.currencyCode ?? current.currencyCode }));
              }}>
                <SelectTrigger><SelectValue placeholder={policies.isLoading ? 'Loading effective policies…' : 'Tenant default / deterministic selection'} /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="default">Tenant default / deterministic selection</SelectItem>
                  {(policies.data ?? []).map(policy => <SelectItem key={policy.policySetId} value={policy.policySetId}>{policy.policyCode} · v{policy.version}{policy.isDefault ? ' · default' : ''}</SelectItem>)}
                </SelectContent>
              </Select>
              {policies.isError && <p className="text-xs text-destructive">Effective policies could not be loaded.</p>}
              {selectedPolicy && <p className="text-xs text-muted-foreground">{selectedPolicy.policyName} · {selectedPolicy.scopeType}</p>}
            </div>

            <div className="space-y-2"><Label>Policy date *</Label><Input aria-label="Policy date" type="date" value={form.atDate} onChange={event => setForm(current => ({ ...current, atDate: event.target.value, policySetId: '' }))} /></div>
            <div className="space-y-2"><Label>Category *</Label><Select value={form.category} onValueChange={value => setForm(current => ({ ...current, category: value as ProcurementComplianceSimulatorForm['category'] }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{procurementComplianceCategoryOptions.map(option => <SelectItem key={option.value} value={option.value}>{option.label}</SelectItem>)}</SelectContent></Select></div>
            <div className="space-y-2"><Label>Service class</Label><Input value={form.serviceClass} onChange={event => setForm(current => ({ ...current, serviceClass: event.target.value }))} placeholder="Optional governed subclass" /></div>
            <div className="space-y-2"><Label>Amount *</Label><Input aria-label="Amount" type="number" min="0" step="0.01" value={form.amount} onChange={event => setForm(current => ({ ...current, amount: event.target.value }))} /></div>
            <div className="space-y-2"><Label>Currency *</Label><Input aria-label="Currency" maxLength={3} value={form.currencyCode} onChange={event => setForm(current => ({ ...current, currencyCode: event.target.value }))} /></div>
            <div className="space-y-2"><Label>Procurement method</Label><Select value={form.requestedMethod} onValueChange={value => setForm(current => ({ ...current, requestedMethod: value as ProcurementComplianceSimulatorForm['requestedMethod'] }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="Auto">Auto-select from policy</SelectItem>{procurementComplianceMethodOptions.map(option => <SelectItem key={option.value} value={option.value}>{option.label}</SelectItem>)}</SelectContent></Select></div>
            <div className="space-y-2"><Label>Source type *</Label><Input value={form.sourceType} onChange={event => setForm(current => ({ ...current, sourceType: event.target.value }))} /></div>
            <div className="space-y-2 sm:col-span-2"><Label>Source reference *</Label><Input value={form.sourceReference} onChange={event => setForm(current => ({ ...current, sourceReference: event.target.value }))} /></div>

            <div className="space-y-2"><Label>Actor user ID</Label><Input value={form.actorUserId} onChange={event => setForm(current => ({ ...current, actorUserId: event.target.value }))} placeholder="Blank uses signed-in user" /></div>
            <div className="space-y-2"><Label>Source owner user ID</Label><Input value={form.sourceOwnerUserId} onChange={event => setForm(current => ({ ...current, sourceOwnerUserId: event.target.value }))} placeholder="Blank uses actor" /></div>
            <div className="space-y-2"><Label>Actor roles</Label><Textarea rows={2} value={form.actorRoles} onChange={event => setForm(current => ({ ...current, actorRoles: event.target.value }))} placeholder="Comma separated; blank uses signed-in roles" /></div>
            <div className="space-y-2"><Label>Source owner roles</Label><Textarea rows={2} value={form.sourceOwnerRoles} onChange={event => setForm(current => ({ ...current, sourceOwnerRoles: event.target.value }))} placeholder="Comma separated; blank uses actor roles" /></div>
            <div className="space-y-2"><Label>SOD entity type *</Label><Input value={form.entityType} onChange={event => setForm(current => ({ ...current, entityType: event.target.value }))} /></div>
            <div className="space-y-2"><Label>SOD action *</Label><Input value={form.action} onChange={event => setForm(current => ({ ...current, action: event.target.value }))} /></div>

            <div className="space-y-2"><Label>Exception type</Label><Input value={form.exceptionType} onChange={event => setForm(current => ({ ...current, exceptionType: event.target.value }))} placeholder="Optional configured type" /></div>
            <div className="space-y-2"><Label>Approval reference</Label><Input value={form.exceptionApprovalReference} onChange={event => setForm(current => ({ ...current, exceptionApprovalReference: event.target.value }))} /></div>
            <label className="flex items-center gap-3 text-sm sm:col-span-2"><Switch checked={form.justificationProvided} onCheckedChange={value => setForm(current => ({ ...current, justificationProvided: value }))} />Exception justification has been supplied</label>
            <div className="space-y-2 sm:col-span-2"><Label>Shared evidence requirement keys</Label><Textarea rows={3} value={form.evidenceReferenceKeys} onChange={event => setForm(current => ({ ...current, evidenceReferenceKeys: event.target.value }))} placeholder="Comma or line separated keys; no evidence is uploaded here" /></div>

            <Button className="sm:col-span-2" onClick={() => evaluate.mutate()} disabled={!canEvaluate || evaluate.isPending}>
              {evaluate.isPending ? 'Resolving effective rules…' : 'Evaluate compliance decision'}
            </Button>
          </CardContent>
        </Card>

        <Card className="min-h-[36rem]">
          <CardHeader>
            <CardTitle>Explainable decision</CardTitle>
            <CardDescription>Every output is tied to immutable policy/rule identifiers and DEC-001 through DEC-014 lineage.</CardDescription>
          </CardHeader>
          <CardContent>
            {!result ? (
              <div className="flex min-h-80 flex-col items-center justify-center rounded-lg border border-dashed p-8 text-center text-muted-foreground">
                <ClipboardCheck className="mb-3 h-10 w-10" /><p className="font-medium">No decision evaluated yet</p><p className="mt-1 max-w-md text-sm">Complete the context and run the simulator to see required authority, evidence, route, declarative hard stops, and matched rules.</p>
              </div>
            ) : (
              <div className="space-y-5">
                <div className={`rounded-lg border p-4 ${complianceOutcomeTone[result.outcome]}`}>
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div className="flex items-center gap-2">{result.outcome === 'Allowed' ? <CheckCircle2 className="h-5 w-5" /> : result.outcome === 'Blocked' ? <ShieldAlert className="h-5 w-5" /> : <AlertTriangle className="h-5 w-5" />}<span className="text-lg font-semibold">{result.outcome === 'ReviewRequired' ? 'Review required' : result.outcome}</span></div>
                    <Badge variant="outline">Evaluation only</Badge>
                  </div>
                  <p className="mt-2 text-sm">{result.policy.policyCode} · v{result.policy.version} · {result.policy.selectionReason}</p>
                  <div className="mt-3 grid gap-2 text-sm sm:grid-cols-3"><span><strong>Method:</strong> {result.selectedMethod ?? 'Unresolved'}</span><span><strong>Hard stops:</strong> {result.hardStops.length}</span><span><strong>Matched rules:</strong> {result.matchedRules.length}</span></div>
                </div>

                <Tabs defaultValue="decision">
                  <TabsList className="grid w-full grid-cols-3"><TabsTrigger value="decision">Decision</TabsTrigger><TabsTrigger value="controls">Route & evidence</TabsTrigger><TabsTrigger value="explainability">Explainability</TabsTrigger></TabsList>
                  <TabsContent value="decision" className="space-y-5 pt-3">
                    <section><h3 className="mb-2 font-semibold">Hard stops</h3><FindingList items={result.hardStops} empty="No declarative hard stop matched." /></section>
                    <section><h3 className="mb-2 font-semibold">Required reviews</h3><FindingList items={result.reviewRequirements} empty="No review requirement matched." /></section>
                    <section><h3 className="mb-2 font-semibold">Warnings</h3><FindingList items={result.warnings} empty="No advisory warning matched." /></section>
                    <section><h3 className="mb-2 font-semibold">Method candidates</h3><div className="space-y-2">{result.methodCandidates.map(candidate => <div key={candidate.method} className="rounded-md border p-3 text-sm"><div className="flex flex-wrap items-center gap-2"><span className="font-medium">{candidate.method}</span><Badge variant={candidate.matchesAmount && candidate.isAllowed ? 'default' : 'secondary'}>{candidate.matchesAmount && candidate.isAllowed ? 'Viable' : 'Not selected'}</Badge></div><p className="mt-1 text-muted-foreground">{candidate.explanation}</p><p className="mt-1 text-xs">{candidate.methodRuleCode}{candidate.thresholdRuleCode ? ` · ${candidate.thresholdRuleCode}` : ''}</p></div>)}</div></section>
                  </TabsContent>
                  <TabsContent value="controls" className="space-y-5 pt-3">
                    <section><h3 className="mb-2 font-semibold">Ordered shared workflow route</h3>{result.route.length === 0 ? <p className="text-sm text-muted-foreground">No route was resolved.</p> : <div className="space-y-2">{result.route.map(step => <div key={`${step.stepType}-${step.ruleId}`} className="grid gap-1 rounded-md border p-3 text-sm sm:grid-cols-[3rem_1fr_auto]"><Badge variant="outline">{step.sequence}</Badge><div><p className="font-medium">{step.name}</p><p className="text-muted-foreground">{step.responsibleRole} · quorum {step.quorum}</p></div><div className="text-right text-xs"><p>{step.ruleCode}</p><p className="text-muted-foreground">{step.sourceDecisionKey}</p>{step.workflowDefinitionId && <p className="text-muted-foreground">Workflow {step.workflowDefinitionId}</p>}</div></div>)}</div>}</section>
                    <section><h3 className="mb-2 font-semibold">Required evidence</h3>{result.requiredEvidence.length === 0 ? <p className="text-sm text-muted-foreground">No evidence rule applied.</p> : <div className="space-y-2">{result.requiredEvidence.map(item => <div key={item.ruleId} className="rounded-md border p-3 text-sm"><div className="flex flex-wrap items-center gap-2"><span className="font-medium">{item.evidenceName}</span>{item.isMandatory && <Badge>Mandatory</Badge>}{item.requiresVerification && <Badge variant="outline">Shared verification</Badge>}</div><p className="mt-1 text-muted-foreground">{item.stage} · key {item.sharedRequirementKey ?? item.ruleCode}</p><p className="mt-1 text-xs">{item.ruleCode} · {item.sourceDecisionKey}</p></div>)}</div>}</section>
                    <section><h3 className="mb-2 font-semibold">Applicable exceptions</h3>{result.applicableExceptions.length === 0 ? <p className="text-sm text-muted-foreground">No exception option applies.</p> : <div className="space-y-2">{result.applicableExceptions.map(item => <div key={item.ruleId} className="rounded-md border p-3 text-sm"><div className="flex flex-wrap items-center gap-2"><span className="font-medium">{item.exceptionName}</span><Badge variant="outline">{item.disposition}</Badge></div><p className="mt-1 text-muted-foreground">{item.exceptionType} · approver {item.approverRole}</p><p className="mt-1 text-xs">{item.ruleCode} · {item.sourceDecisionKey}</p></div>)}</div>}</section>
                  </TabsContent>
                  <TabsContent value="explainability" className="space-y-5 pt-3">
                    <section><h3 className="mb-2 font-semibold">Matched immutable rules</h3><div className="space-y-2">{result.matchedRules.map(rule => <div key={rule.ruleId} className="rounded-md border p-3 text-sm"><div className="flex flex-wrap items-center gap-2"><Badge>{rule.ruleKind}</Badge><span className="font-medium">{rule.ruleCode}</span><Badge variant="outline">{rule.sourceDecisionKey}</Badge>{rule.overrideAction !== 'Add' && <Badge variant="secondary">{rule.overrideAction}</Badge>}</div><p className="mt-1">{rule.ruleName}</p><p className="mt-1 text-muted-foreground">{rule.matchReason}</p><p className="mt-1 break-all text-xs text-muted-foreground">Policy {rule.policyCode}/v{rule.policyVersion} · rule {rule.ruleId}{rule.sourceRuleId ? ` · source ${rule.sourceRuleId}` : ''}</p></div>)}</div></section>
                    <section><h3 className="mb-2 font-semibold">Resolution trace</h3><ol className="space-y-2">{result.trace.map(step => <li key={step.sequence} className="rounded-md border p-3 text-sm"><div className="flex items-center gap-2"><Badge variant="outline">{step.sequence}</Badge><span className="font-medium">{step.stage}</span></div><p className="mt-2 text-muted-foreground">{step.result}</p>{step.ruleCodes.length > 0 && <p className="mt-1 text-xs">Rules: {step.ruleCodes.join(', ')}</p>}</li>)}</ol></section>
                    <div className="rounded-md bg-muted p-3 text-xs text-muted-foreground"><p>Evaluation ID: {result.evaluationId}</p><p>Correlation ID: {result.correlationId}</p><p>Evaluated: {new Date(result.evaluatedAtUtc).toLocaleString()}</p></div>
                  </TabsContent>
                </Tabs>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
