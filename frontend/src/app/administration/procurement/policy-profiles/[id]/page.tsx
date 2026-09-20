'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams, useRouter } from 'next/navigation';
import { ArrowLeft, CheckCircle2, Copy, Edit, Eye, FileCheck2, RefreshCw, ShieldCheck, Trash2 } from 'lucide-react';

import { ProcurementDecisionEditor } from '@/components/procurement/configuration/ProcurementDecisionEditor';
import { WithdrawSupplierPolicyDecision } from '@/components/procurement/configuration/WithdrawSupplierPolicyDecision';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription,
  AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Progress } from '@/components/ui/progress';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { procurementConfigurationService } from '@/services/procurement-configuration.service';
import type { ProcurementConfigurationDecision, UpdateProcurementConfigurationProfileRequest } from '@/types/procurement-configuration';

type ConfirmAction = 'publish' | 'retire' | 'delete' | null;

const errorMessage = (error: unknown) => {
  const candidate = error as { message?: string; response?: { detail?: string; title?: string } };
  return candidate.response?.detail || candidate.response?.title || candidate.message || 'The request failed.';
};

const statusBadge = (decision: ProcurementConfigurationDecision) => {
  if (decision.status === 'Withdrawn') return <Badge variant="secondary">Withdrawn · inactive</Badge>;
  if (decision.isComplete) return <Badge><CheckCircle2 className="mr-1 h-3 w-3" />Complete</Badge>;
  if (decision.status === 'Rejected') return <Badge variant="destructive">Rejected</Badge>;
  return <Badge variant="secondary">{decision.status}</Badge>;
};

export default function ProcurementPolicyProfileDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { hasRole, hasPermission } = useAuth();
  const { toast } = useToast();
  const isSuperAdmin = hasRole('SuperAdmin');
  const canManagePolicy = isSuperAdmin || hasPermission('procurement.access.manage');
  const [selectedDecisionKey, setSelectedDecisionKey] = useState<string>();
  const [profileEditorOpen, setProfileEditorOpen] = useState(false);
  const [cloneOpen, setCloneOpen] = useState(false);
  const [confirmAction, setConfirmAction] = useState<ConfirmAction>(null);
  const [reason, setReason] = useState('');
  const [changeSummary, setChangeSummary] = useState('');
  const [conflictMessage, setConflictMessage] = useState<string>();
  const [editForm, setEditForm] = useState<UpdateProcurementConfigurationProfileRequest>();

  const profileQuery = useQuery({
    queryKey: ['procurement-configuration-profile', id],
    queryFn: () => procurementConfigurationService.get(id),
    enabled: Boolean(id),
  });
  const profile = profileQuery.data;
  const editable = profile?.lifecycleStatus === 'Draft';
  const selectedDecision = useMemo(() => profile?.decisions.find(item => item.decisionKey === selectedDecisionKey), [profile, selectedDecisionKey]);
  const refresh = async () => {
    setConflictMessage(undefined);
    await queryClient.invalidateQueries({ queryKey: ['procurement-configuration-profile', id] });
    await queryClient.invalidateQueries({ queryKey: ['procurement-configuration-profiles'] });
  };

  const validate = useMutation({
    mutationFn: () => procurementConfigurationService.validate(id),
    onSuccess: async result => {
      toast({ title: result.isValid ? 'Validation passed' : 'Validation needs attention', description: result.isValid ? 'All publication gates passed.' : `${result.errors.length} blocking issue(s) remain.`, variant: result.isValid ? 'success' : 'destructive' });
      await refresh();
    },
    onError: error => toast({ title: 'Validation failed', description: errorMessage(error), variant: 'destructive' }),
  });

  const updateProfile = useMutation({
    mutationFn: () => {
      if (!editForm) throw new Error('Profile edit data is unavailable.');
      return procurementConfigurationService.update(id, { ...editForm, effectiveTo: editForm.effectiveTo || undefined, reason: editForm.reason?.trim() || undefined });
    },
    onSuccess: async () => { setProfileEditorOpen(false); toast({ title: 'Profile updated', description: 'Metadata and audit history were updated.', variant: 'success' }); await refresh(); },
    onError: error => {
      if ((error as { status?: number }).status === 409) setConflictMessage('This draft was changed by another user. Reload it before applying further changes.');
      toast({ title: 'Unable to update profile', description: errorMessage(error), variant: 'destructive' });
    },
  });

  const clone = useMutation({
    mutationFn: () => procurementConfigurationService.cloneDraft(id, changeSummary.trim() || undefined),
    onSuccess: async result => { setCloneOpen(false); toast({ title: 'New draft version created', description: 'Decision values were copied; approvals and evidence were reset.', variant: 'success' }); await refresh(); router.push(`/administration/procurement/policy-profiles/${result.id}`); },
    onError: error => toast({ title: 'Unable to clone profile', description: errorMessage(error), variant: 'destructive' }),
  });

  const lifecycle = useMutation({
    mutationFn: async () => {
      if (!profile || !confirmAction) throw new Error('No lifecycle action is selected.');
      const request = { rowVersion: profile.rowVersion, reason: reason.trim() || undefined };
      if (confirmAction === 'publish') return procurementConfigurationService.publish(id, request);
      if (confirmAction === 'retire') return procurementConfigurationService.retire(id, request);
      await procurementConfigurationService.deleteDraft(id, request);
      return null;
    },
    onSuccess: async result => {
      const completed = confirmAction;
      setConfirmAction(null);
      setReason('');
      toast({ title: completed === 'publish' ? 'Profile published' : completed === 'retire' ? 'Profile retired' : 'Draft deleted', description: completed === 'publish' ? 'This version is now the tenant’s effective governed policy.' : 'The lifecycle action completed and was audited.', variant: 'success' });
      await refresh();
      if (!result) router.push('/administration/procurement/policy-profiles');
    },
    onError: error => {
      if ((error as { status?: number }).status === 409) setConflictMessage('The profile changed while this lifecycle action was being prepared. Reload and revalidate it.');
      toast({ title: 'Lifecycle action rejected', description: errorMessage(error), variant: 'destructive' });
    },
  });

  if (profileQuery.isLoading) return <div className="flex min-h-[240px] items-center justify-center"><RefreshCw className="h-6 w-6 animate-spin" /></div>;
  if (!profile) return <Alert variant="destructive"><AlertTitle>Profile unavailable</AlertTitle><AlertDescription>{profileQuery.error ? errorMessage(profileQuery.error) : 'The profile was not found for this tenant.'}</AlertDescription></Alert>;

  const completion = profile.totalDecisionCount ? (profile.completeDecisionCount / profile.totalDecisionCount) * 100 : 0;
  const openProfileEditor = () => {
    setEditForm({ name: profile.name, effectiveFrom: profile.effectiveFrom.slice(0, 10), effectiveTo: profile.effectiveTo?.slice(0, 10), changeSummary: profile.changeSummary, isDefault: profile.isDefault, rowVersion: profile.rowVersion, reason: '' });
    setProfileEditorOpen(true);
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-start">
        <div>
          <Button variant="ghost" className="mb-2 -ml-3" onClick={() => router.push('/administration/procurement/policy-profiles')}><ArrowLeft className="mr-2 h-4 w-4" />Policy profiles</Button>
          <div className="flex flex-wrap items-center gap-2"><h1 className="text-3xl font-bold">{profile.name}</h1><Badge variant={profile.lifecycleStatus === 'Published' ? 'default' : 'secondary'}>{profile.lifecycleStatus}</Badge></div>
          <p className="mt-1 text-muted-foreground">{profile.profileCode} · version {profile.version} · {new Date(profile.effectiveFrom).toLocaleDateString()} – {profile.effectiveTo ? new Date(profile.effectiveTo).toLocaleDateString() : 'open-ended'}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={() => profileQuery.refetch()}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button>
          {editable && <Button variant="outline" onClick={openProfileEditor}><Edit className="mr-2 h-4 w-4" />Edit profile</Button>}
          {!editable && <Button variant="outline" onClick={() => { setChangeSummary(''); setCloneOpen(true); }}><Copy className="mr-2 h-4 w-4" />Clone draft</Button>}
          {editable && <Button variant="outline" onClick={() => validate.mutate()} disabled={validate.isPending}><ShieldCheck className="mr-2 h-4 w-4" />Validate</Button>}
          {editable && isSuperAdmin && <Button onClick={() => setConfirmAction('publish')}><FileCheck2 className="mr-2 h-4 w-4" />Publish</Button>}
          {profile.lifecycleStatus === 'Published' && isSuperAdmin && <Button variant="destructive" onClick={() => setConfirmAction('retire')}>Retire</Button>}
          {editable && <Button variant="destructive" onClick={() => setConfirmAction('delete')}><Trash2 className="mr-2 h-4 w-4" />Delete draft</Button>}
        </div>
      </div>

      {profile.lifecycleStatus !== 'Draft' && <Alert><Eye className="h-4 w-4" /><AlertTitle>Controlled policy version</AlertTitle><AlertDescription>Published values and evidence are read-only. Clone this version for revisions. Authorized administrators can separately withdraw the optional supplier policy with an audited reason.</AlertDescription></Alert>}
      {!isSuperAdmin && editable && <Alert><ShieldCheck className="h-4 w-4" /><AlertTitle>Preparation access</AlertTitle><AlertDescription>Tenant administrators can prepare and validate drafts. Only a SuperAdmin can approve decisions or publish the profile.</AlertDescription></Alert>}
      {conflictMessage && <Alert variant="destructive"><AlertTitle>Concurrent change detected</AlertTitle><AlertDescription className="flex flex-wrap items-center justify-between gap-3"><span>{conflictMessage}</span><Button variant="outline" size="sm" onClick={() => refresh()}>Reload profile</Button></AlertDescription></Alert>}

      <Tabs defaultValue="decisions" className="space-y-4">
        <TabsList className="h-auto flex-wrap"><TabsTrigger value="overview">Overview</TabsTrigger><TabsTrigger value="decisions">Decisions ({profile.completeDecisionCount}/{profile.totalDecisionCount})</TabsTrigger><TabsTrigger value="validation">Validation ({profile.validation.errors.length})</TabsTrigger><TabsTrigger value="evidence">Evidence</TabsTrigger><TabsTrigger value="history">History</TabsTrigger></TabsList>

        <TabsContent value="overview" className="space-y-4">
          <div className="grid gap-4 md:grid-cols-3">
            <Card><CardHeader className="pb-2"><CardDescription>Decision readiness</CardDescription><CardTitle>{profile.completeDecisionCount}/{profile.totalDecisionCount}</CardTitle></CardHeader><CardContent><Progress value={completion} /><p className="mt-2 text-xs text-muted-foreground">Approved, effective, and evidenced decisions</p></CardContent></Card>
            <Card><CardHeader className="pb-2"><CardDescription>Validation</CardDescription><CardTitle>{profile.validation.isValid ? 'Passed' : `${profile.validation.errors.length} issue(s)`}</CardTitle></CardHeader><CardContent className="text-sm text-muted-foreground">{profile.validation.warnings.length} warning(s)</CardContent></Card>
            <Card><CardHeader className="pb-2"><CardDescription>Latest activity</CardDescription><CardTitle className="text-base">{new Date(profile.updatedAt).toLocaleString()}</CardTitle></CardHeader><CardContent className="text-sm text-muted-foreground">{profile.updatedBy || 'System'}</CardContent></Card>
          </div>
          <Card><CardHeader><CardTitle>Profile lineage</CardTitle></CardHeader><CardContent className="grid gap-4 text-sm md:grid-cols-2"><div><p className="text-muted-foreground">Change summary</p><p>{profile.changeSummary || 'No summary supplied.'}</p></div><div><p className="text-muted-foreground">Supersedes</p><p>{profile.supersedesProfileId || 'Initial version'}</p></div><div><p className="text-muted-foreground">Published</p><p>{profile.publishedAt ? new Date(profile.publishedAt).toLocaleString() : 'Not published'}</p></div><div><p className="text-muted-foreground">Default family</p><p>{profile.isDefault ? 'Yes' : 'No'}</p></div></CardContent></Card>
        </TabsContent>

        <TabsContent value="decisions">
          <Card>
            <CardHeader><CardTitle>Decision register</CardTitle><CardDescription>Explicit typed editors, ownership, approval, effective dating, evidence, and lineage for DEC-001 through DEC-014.</CardDescription></CardHeader>
            <CardContent className="divide-y p-0">
              {profile.decisions.map(decision => (
                <button key={decision.id} className="flex w-full flex-col gap-3 px-6 py-4 text-left hover:bg-muted/50 sm:flex-row sm:items-center sm:justify-between" onClick={() => setSelectedDecisionKey(decision.decisionKey)}>
                  <div><div className="flex flex-wrap items-center gap-2"><span className="font-mono text-xs font-semibold text-primary">{decision.decisionKey}</span><span className="font-medium">{decision.displayName}</span></div><p className="mt-1 text-sm text-muted-foreground">{decision.ownerGroup} · approval {decision.approvalStatus.toLowerCase()} · evidence {decision.evidenceStatus.toLowerCase()}</p></div>
                  <div className="flex items-center gap-2">{statusBadge(decision)}<Eye className="h-4 w-4 text-muted-foreground" /></div>
                </button>
              ))}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="validation" className="space-y-4">
          {profile.validation.isValid && <Alert><CheckCircle2 className="h-4 w-4" /><AlertTitle>Publication validation passed</AlertTitle><AlertDescription>All DEC-001 through DEC-014 gates currently pass.</AlertDescription></Alert>}
          {profile.validation.errors.map((issue, index) => <Alert key={`${issue.code}-${index}`} variant="destructive"><AlertTitle>{issue.decisionKey ? `${issue.decisionKey}: ` : ''}{issue.code}</AlertTitle><AlertDescription>{issue.message}</AlertDescription></Alert>)}
          {profile.validation.warnings.map((issue, index) => <Alert key={`${issue.code}-warning-${index}`}><AlertTitle>{issue.decisionKey ? `${issue.decisionKey}: ` : ''}{issue.code}</AlertTitle><AlertDescription>{issue.message}</AlertDescription></Alert>)}
        </TabsContent>

        <TabsContent value="evidence">
          <Card><CardHeader><CardTitle>Evidence register</CardTitle><CardDescription>References to the existing shared upload store and external records; no duplicate document store is created.</CardDescription></CardHeader><CardContent className="divide-y p-0">{profile.decisions.map(decision => <div key={decision.id} className="flex items-center justify-between gap-4 px-6 py-4"><div><p className="font-medium">{decision.decisionKey} · {decision.displayName}</p><p className="text-sm text-muted-foreground">{decision.evidence.length ? decision.evidence.map(item => item.originalFileName || item.externalReference || item.evidenceType).join(', ') : 'No evidence linked'}</p></div><Badge variant={decision.evidence.length ? 'default' : 'secondary'}>{decision.evidenceStatus}</Badge></div>)}</CardContent></Card>
        </TabsContent>

        <TabsContent value="history">
          <Card><CardHeader><CardTitle>Immutable revision history</CardTitle><CardDescription>Actor, role, reason, result, and correlation context for every configuration action.</CardDescription></CardHeader><CardContent className="divide-y p-0">{profile.recentHistory.map(item => <div key={item.id} className="grid gap-2 px-6 py-4 text-sm md:grid-cols-[180px_160px_1fr]"><div><p className="font-medium">{item.action}</p><p className="text-xs text-muted-foreground">{item.result}</p></div><div><p>{item.actorName}</p><p className="text-xs text-muted-foreground">{item.actorRoles || 'System'}</p></div><div><p>{item.reason || 'No reason supplied'}</p><p className="mt-1 break-all text-xs text-muted-foreground">{new Date(item.timestamp).toLocaleString()} · {item.correlationId}</p></div></div>)}</CardContent></Card>
        </TabsContent>
      </Tabs>

      <Dialog open={Boolean(selectedDecision)} onOpenChange={open => { if (!open) setSelectedDecisionKey(undefined); }}>
        <DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-4xl">
          {selectedDecision && <><DialogHeader><DialogTitle>{selectedDecision.decisionKey} · {selectedDecision.displayName}</DialogTitle><DialogDescription>{selectedDecision.description}</DialogDescription></DialogHeader><ProcurementDecisionEditor key={`${selectedDecision.id}-${selectedDecision.rowVersion}`} profileId={id} decision={selectedDecision} editable={editable} isSuperAdmin={isSuperAdmin} onChanged={refresh} /></>}
          {selectedDecision && <WithdrawSupplierPolicyDecision profileId={id} profileStatus={profile.lifecycleStatus} decision={selectedDecision} canManage={canManagePolicy} onChanged={refresh} />}
        </DialogContent>
      </Dialog>

      <Dialog open={profileEditorOpen} onOpenChange={setProfileEditorOpen}>
        <DialogContent className="sm:max-w-2xl"><DialogHeader><DialogTitle>Edit draft profile</DialogTitle><DialogDescription>Changing profile dates revalidates every effective-dated decision.</DialogDescription></DialogHeader>{editForm && <div className="grid gap-4 sm:grid-cols-2"><div className="space-y-2 sm:col-span-2"><Label>Name</Label><Input value={editForm.name} onChange={event => setEditForm(current => current && ({ ...current, name: event.target.value }))} /></div><div className="space-y-2"><Label>Effective from</Label><Input type="date" value={editForm.effectiveFrom} onChange={event => setEditForm(current => current && ({ ...current, effectiveFrom: event.target.value }))} /></div><div className="space-y-2"><Label>Effective to</Label><Input type="date" value={editForm.effectiveTo ?? ''} onChange={event => setEditForm(current => current && ({ ...current, effectiveTo: event.target.value }))} /></div><div className="space-y-2 sm:col-span-2"><Label>Change summary</Label><Textarea value={editForm.changeSummary ?? ''} onChange={event => setEditForm(current => current && ({ ...current, changeSummary: event.target.value }))} /></div><div className="flex items-center gap-3 sm:col-span-2"><Switch checked={editForm.isDefault} onCheckedChange={next => setEditForm(current => current && ({ ...current, isDefault: next }))} /><span className="text-sm">Default profile family</span></div><div className="space-y-2 sm:col-span-2"><Label>Change reason</Label><Input value={editForm.reason ?? ''} onChange={event => setEditForm(current => current && ({ ...current, reason: event.target.value }))} /></div></div>}<DialogFooter><Button variant="outline" onClick={() => setProfileEditorOpen(false)}>Cancel</Button><Button onClick={() => updateProfile.mutate()} disabled={updateProfile.isPending}>{updateProfile.isPending ? 'Saving…' : 'Save changes'}</Button></DialogFooter></DialogContent>
      </Dialog>

      <Dialog open={cloneOpen} onOpenChange={setCloneOpen}><DialogContent><DialogHeader><DialogTitle>Create next draft version</DialogTitle><DialogDescription>Values and lineage are copied. Approval and evidence gates reset so the revision must be governed independently.</DialogDescription></DialogHeader><div className="space-y-2"><Label>Change summary</Label><Textarea value={changeSummary} onChange={event => setChangeSummary(event.target.value)} /></div><DialogFooter><Button variant="outline" onClick={() => setCloneOpen(false)}>Cancel</Button><Button onClick={() => clone.mutate()} disabled={clone.isPending}>{clone.isPending ? 'Creating…' : 'Create draft'}</Button></DialogFooter></DialogContent></Dialog>

      <AlertDialog open={Boolean(confirmAction)} onOpenChange={open => { if (!open) setConfirmAction(null); }}>
        <AlertDialogContent><AlertDialogHeader><AlertDialogTitle>{confirmAction === 'publish' ? 'Publish this policy version?' : confirmAction === 'retire' ? 'Retire this published version?' : 'Delete this draft?'}</AlertDialogTitle><AlertDialogDescription>{confirmAction === 'publish' ? 'Publication requires all 14 decisions to be approved, effective, evidenced, and valid. The current published version is retired atomically.' : confirmAction === 'delete' ? 'Deletion is allowed only when the draft has no evidence or workflow dependencies.' : 'Retirement makes this version historical and read-only.'}</AlertDialogDescription></AlertDialogHeader><div className="space-y-2"><Label>Reason</Label><Textarea value={reason} onChange={event => setReason(event.target.value)} placeholder="Required for the audit trail" /></div><AlertDialogFooter><AlertDialogCancel>Cancel</AlertDialogCancel><AlertDialogAction onClick={event => { event.preventDefault(); lifecycle.mutate(); }} disabled={lifecycle.isPending || !reason.trim()}>{lifecycle.isPending ? 'Applying…' : 'Confirm'}</AlertDialogAction></AlertDialogFooter></AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
