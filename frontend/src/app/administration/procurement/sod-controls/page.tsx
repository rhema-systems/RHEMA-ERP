'use client';

import Link from 'next/link';
import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, CheckCircle2, ExternalLink, RefreshCw, ShieldAlert, ShieldCheck } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import {
  canRunProcurementSodCheck,
  createProcurementSodGuardRequest,
  isProcurementSodActionAvailable,
  procurementSodDecisionTone,
  requiredProcurementSodControlCodes,
} from '@/lib/procurement-sod';
import { procurementPolicyService } from '@/services/procurement-policy.service';
import { procurementSodService } from '@/services/procurement-sod.service';
import type { ProcurementSodGuardForm } from '@/types/procurement-sod';

const initialGuardForm = (): ProcurementSodGuardForm => ({
  controlCode: requiredProcurementSodControlCodes[0],
  sourceType: 'ProcurementTransaction',
  sourceReference: 'SOD-CAPABILITY-CHECK',
  prohibitedActorUserIds: '',
});

const formatDateTime = (value?: string) => value
  ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
  : '—';

export default function ProcurementSodControlsPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [guardForm, setGuardForm] = useState<ProcurementSodGuardForm>(initialGuardForm);
  const [draftPolicyId, setDraftPolicyId] = useState('');
  const [provisionReason, setProvisionReason] = useState('Apply the six mandatory TDC SRS segregation-of-duties controls.');

  const coverage = useQuery({
    queryKey: ['procurement-sod-coverage'],
    queryFn: () => procurementSodService.coverage(),
  });
  const drafts = useQuery({
    queryKey: ['procurement-policy-drafts-for-sod'],
    queryFn: () => procurementPolicyService.list({ status: 'Draft', page: 1, pageSize: 100 }),
  });
  const attempts = useQuery({
    queryKey: ['procurement-sod-blocked-attempts'],
    queryFn: () => procurementSodService.blockedAttempts(50),
  });
  const check = useMutation({
    mutationFn: () => procurementSodService.check(createProcurementSodGuardRequest(guardForm)),
    onError: (error: Error) => toast({ title: 'Capability check failed', description: error.message, variant: 'destructive' }),
  });
  const provision = useMutation({
    mutationFn: () => procurementSodService.applyRequired(draftPolicyId, provisionReason),
    onSuccess: result => {
      toast({ title: 'Required controls applied', description: `${result.createdCount} created; ${result.existingCount} already existed.` });
      queryClient.invalidateQueries({ queryKey: ['procurement-policy-drafts-for-sod'] });
      queryClient.invalidateQueries({ queryKey: ['procurement-sod-coverage'] });
    },
    onError: (error: Error) => toast({ title: 'Unable to apply controls', description: error.message, variant: 'destructive' }),
  });

  const coverageData = coverage.data;
  const result = check.data;
  const protectedActionAvailable = isProcurementSodActionAvailable(result);
  const selectedControl = coverageData?.controls.find(item => item.code === guardForm.controlCode);
  const draftItems = drafts.data?.items ?? [];

  return (
    <div className="space-y-6">
      <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-start">
        <div>
          <h1 className="text-3xl font-bold">Procurement SOD controls</h1>
          <p className="mt-1 max-w-4xl text-muted-foreground">Administer and verify the six mandatory TDC segregation-of-duties hard stops over the effective executable policy.</p>
        </div>
        <Button variant="outline" onClick={() => Promise.all([coverage.refetch(), attempts.refetch(), drafts.refetch()])} disabled={coverage.isFetching || attempts.isFetching}>
          <RefreshCw className={`mr-2 h-4 w-4 ${coverage.isFetching ? 'animate-spin' : ''}`} />Refresh controls
        </Button>
      </div>

      <Alert className="border-amber-500/40 bg-amber-500/5">
        <ShieldAlert className="h-4 w-4" />
        <AlertTitle>Fail-closed shared guard</AlertTitle>
        <AlertDescription>Protected APIs must pass their recorded prior-participant IDs to the reusable guard. Missing, ambiguous, incomplete, weakened, or conflicting policy resolves to a hard stop. Blocked enforcement attempts are written to the existing audit log before the API returns 403.</AlertDescription>
      </Alert>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <div className="rounded-lg border p-4"><p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Coverage</p><p className="mt-2 text-2xl font-semibold">{coverageData?.controls.filter(item => item.isHardStop).length ?? 0}/6</p><p className="text-sm text-muted-foreground">effective hard stops</p></div>
        <div className="rounded-lg border p-4"><p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Status</p><p className="mt-2 text-2xl font-semibold">{coverageData?.status ?? (coverage.isLoading ? 'Loading' : 'Unavailable')}</p><p className="text-sm text-muted-foreground">fail-closed selection</p></div>
        <div className="rounded-lg border p-4"><p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Effective policy</p><p className="mt-2 truncate text-lg font-semibold">{coverageData?.policyCode ?? 'None'}</p><p className="text-sm text-muted-foreground">{coverageData?.policyVersion ? `Version ${coverageData.policyVersion}` : 'No selectable Published policy'}</p></div>
        <div className="rounded-lg border p-4"><p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Blocked attempts</p><p className="mt-2 text-2xl font-semibold">{attempts.data?.length ?? 0}</p><p className="text-sm text-muted-foreground">latest tenant audit entries</p></div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Shared-control workspace</CardTitle>
          <CardDescription>Provision Draft policy rules, inspect effective coverage, verify UI capabilities, and review attempted bypasses without duplicating policy, workflow, or evidence administration.</CardDescription>
        </CardHeader>
        <CardContent>
          <Tabs defaultValue="controls">
            <TabsList className="grid w-full grid-cols-3"><TabsTrigger value="controls">Required controls</TabsTrigger><TabsTrigger value="capability">Capability check</TabsTrigger><TabsTrigger value="audit">Blocked attempts</TabsTrigger></TabsList>

            <TabsContent value="controls" className="space-y-5 pt-4">
              <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-end">
                <div className="space-y-2">
                  <Label>Draft executable policy</Label>
                  <Select value={draftPolicyId || 'none'} onValueChange={value => setDraftPolicyId(value === 'none' ? '' : value)}>
                    <SelectTrigger><SelectValue placeholder={drafts.isLoading ? 'Loading Draft policies…' : 'Select a Draft policy'} /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">Select a Draft policy</SelectItem>
                      {draftItems.map(policy => <SelectItem key={policy.id} value={policy.id}>{policy.code} · v{policy.version} · {policy.ruleCount} rules</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <AlertDialog>
                  <AlertDialogTrigger asChild><Button disabled={!draftPolicyId || provision.isPending}><ShieldCheck className="mr-2 h-4 w-4" />Apply required controls</Button></AlertDialogTrigger>
                  <AlertDialogContent>
                    <AlertDialogHeader><AlertDialogTitle>Apply all six required SOD controls?</AlertDialogTitle><AlertDialogDescription>Only missing controls are added through the existing Draft policy lifecycle. Existing matching codes are preserved for explicit review.</AlertDialogDescription></AlertDialogHeader>
                    <div className="space-y-2"><Label>Audit reason *</Label><Textarea value={provisionReason} onChange={event => setProvisionReason(event.target.value)} /></div>
                    <AlertDialogFooter><AlertDialogCancel>Cancel</AlertDialogCancel><AlertDialogAction onClick={() => provision.mutate()} disabled={!provisionReason.trim()}>Apply six controls</AlertDialogAction></AlertDialogFooter>
                  </AlertDialogContent>
                </AlertDialog>
              </div>

              {coverage.isError && <Alert variant="destructive"><AlertTriangle className="h-4 w-4" /><AlertTitle>Coverage unavailable</AlertTitle><AlertDescription>The effective SOD policy could not be loaded.</AlertDescription></Alert>}
              <div className="divide-y rounded-lg border">
                {(coverageData?.controls ?? []).map(control => (
                  <div key={control.code} className="grid gap-3 p-4 lg:grid-cols-[minmax(0,1.25fr)_minmax(0,1fr)_auto] lg:items-center">
                    <div><div className="flex flex-wrap items-center gap-2"><span className="font-medium">{control.name}</span><Badge variant="outline">{control.code}</Badge><Badge variant={control.isHardStop ? 'default' : 'destructive'}>{control.isHardStop ? 'Effective HardStop' : control.isConfigured ? 'Invalid configuration' : 'Missing'}</Badge></div><p className="mt-2 text-sm text-muted-foreground">{control.explanation}</p></div>
                    <div className="text-sm"><p><strong>{control.initiatorRole}</strong> cannot act as <strong>{control.conflictingRole}</strong></p><p className="mt-1 text-muted-foreground">{control.entityType} · {control.action} · {control.sourceRequirement} · {control.sourceDecisionKey}</p>{control.configurationIssue && <p className="mt-1 text-destructive">{control.configurationIssue}</p>}</div>
                    {coverageData?.policySetId ? <Button asChild variant="ghost" size="sm"><Link href={`/administration/procurement/policy-sets/${coverageData.policySetId}`}>Open policy<ExternalLink className="ml-2 h-3.5 w-3.5" /></Link></Button> : <Badge variant="outline">Fail closed</Badge>}
                  </div>
                ))}
                {!coverage.isLoading && (coverageData?.controls.length ?? 0) === 0 && <p className="p-8 text-center text-sm text-muted-foreground">No control definitions were returned.</p>}
              </div>
            </TabsContent>

            <TabsContent value="capability" className="space-y-5 pt-4">
              <div className="grid gap-5 lg:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)]">
                <div className="grid gap-4 rounded-lg border p-4 sm:grid-cols-2">
                  <div className="space-y-2 sm:col-span-2"><Label>Required control</Label><Select value={guardForm.controlCode} onValueChange={value => { setGuardForm(current => ({ ...current, controlCode: value })); check.reset(); }}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{(coverageData?.controls ?? []).map(control => <SelectItem key={control.code} value={control.code}>{control.name}</SelectItem>)}</SelectContent></Select></div>
                  <div className="space-y-2"><Label>Source type *</Label><Input value={guardForm.sourceType} onChange={event => setGuardForm(current => ({ ...current, sourceType: event.target.value }))} /></div>
                  <div className="space-y-2"><Label>Source reference *</Label><Input value={guardForm.sourceReference} onChange={event => setGuardForm(current => ({ ...current, sourceReference: event.target.value }))} /></div>
                  <div className="space-y-2 sm:col-span-2"><div className="flex items-center justify-between gap-3"><Label>Recorded prior-participant user IDs *</Label><Button type="button" variant="ghost" size="sm" onClick={() => setGuardForm(current => ({ ...current, prohibitedActorUserIds: coverageData?.currentActorUserId ?? '' }))} disabled={!coverageData?.currentActorUserId}>Use signed-in actor</Button></div><Textarea rows={4} value={guardForm.prohibitedActorUserIds} onChange={event => setGuardForm(current => ({ ...current, prohibitedActorUserIds: event.target.value }))} placeholder="Comma or line separated user IDs recorded on the source transaction" /></div>
                  <Button className="sm:col-span-2" onClick={() => check.mutate()} disabled={!canRunProcurementSodCheck(guardForm) || check.isPending}>{check.isPending ? 'Checking shared guard…' : 'Check current actor capability'}</Button>
                </div>

                <div className={`rounded-lg border p-5 ${procurementSodDecisionTone(result)}`}>
                  {!result ? <div className="flex min-h-52 flex-col items-center justify-center text-center text-muted-foreground"><ShieldCheck className="mb-3 h-9 w-9" /><p className="font-medium">No capability checked</p><p className="mt-1 max-w-md text-sm">The check endpoint is read-only. Protected runtime endpoints use the enforce endpoint, which audits every denied attempt.</p></div> : <div className="space-y-4"><div className="flex items-center gap-2">{result.allowed ? <CheckCircle2 className="h-5 w-5" /> : <ShieldAlert className="h-5 w-5" />}<span className="text-lg font-semibold">{result.allowed ? 'Action available' : 'Action blocked'}</span><Badge variant="outline">{result.code}</Badge></div><p className="text-sm">{result.message}</p><dl className="grid gap-2 text-sm sm:grid-cols-2"><div><dt className="text-muted-foreground">Actor</dt><dd className="font-mono text-xs">{result.actorUserId}</dd></div><div><dt className="text-muted-foreground">Rule</dt><dd>{result.ruleCode ?? 'Fail-closed policy state'}</dd></div><div><dt className="text-muted-foreground">Policy</dt><dd>{result.policyCode ? `${result.policyCode} · v${result.policyVersion}` : 'No unique effective policy'}</dd></div><div><dt className="text-muted-foreground">Correlation</dt><dd className="font-mono text-xs">{result.correlationId}</dd></div></dl></div>}
                  <div className="mt-6 border-t pt-4"><Button className="w-full" disabled={!protectedActionAvailable} title={protectedActionAvailable ? 'Capability passed; execution belongs to the owning transaction screen.' : 'Hidden or disabled because the shared SOD guard has not allowed this action.'}>{protectedActionAvailable ? 'Protected action available' : 'Protected action unavailable'}</Button><p className="mt-2 text-center text-xs text-muted-foreground">This administration page never executes the business transaction.</p></div>
                </div>
              </div>
              {selectedControl && <Alert><ShieldCheck className="h-4 w-4" /><AlertTitle>{selectedControl.code}</AlertTitle><AlertDescription>{selectedControl.initiatorRole} → {selectedControl.conflictingRole}; {selectedControl.entityType}/{selectedControl.action}. UI consumers must hide or disable their action when this capability result is blocked.</AlertDescription></Alert>}
            </TabsContent>

            <TabsContent value="audit" className="pt-4">
              <div className="divide-y rounded-lg border">
                {(attempts.data ?? []).map(item => <div key={item.id} className="grid gap-2 p-4 md:grid-cols-[180px_minmax(0,1fr)_minmax(0,1fr)]"><div><p className="text-sm font-medium">{item.actorName}</p><p className="text-xs text-muted-foreground">{formatDateTime(item.timestamp)}</p></div><div><div className="flex flex-wrap gap-2"><Badge variant="destructive">Blocked</Badge><Badge variant="outline">{item.controlCode}</Badge></div><p className="mt-2 text-sm">{item.message}</p></div><div className="text-sm"><p>{item.sourceType} · {item.sourceReference}</p><p className="mt-1 font-mono text-xs text-muted-foreground">{item.correlationId}</p></div></div>)}
                {attempts.isLoading && <p className="p-8 text-center text-sm text-muted-foreground">Loading attempted-bypass audit…</p>}
                {!attempts.isLoading && (attempts.data?.length ?? 0) === 0 && <p className="p-8 text-center text-sm text-muted-foreground">No blocked SOD enforcement attempts are recorded for this tenant.</p>}
              </div>
            </TabsContent>
          </Tabs>
        </CardContent>
      </Card>
    </div>
  );
}
