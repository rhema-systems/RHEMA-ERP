'use client';

import Link from 'next/link';
import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, CheckCircle2, ExternalLink, Pencil, Plus, RefreshCw, ShieldCheck, Trash2, Users } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { canActivateCommittee, canCheckAccessCapability, roleRequiresWarehouseScope, validateResponsibilityAssignment } from '@/lib/procurement-access-control';
import { procurementAccessControlService } from '@/services/procurement-access-control.service';
import type {
  ProcurementAccessCapabilityRequest, ProcurementCommittee, ProcurementCommitteeMember,
  ProcurementCommitteeMemberKind, ProcurementCommitteeStatus, ProcurementResponsibilityAssignment,
  ProcurementLocationScopeMode, ProcurementWarehouseScopeMode, SaveProcurementCommitteeMember, SaveProcurementResponsibilityAssignment,
  UpdateProcurementCommittee,
} from '@/types/procurement-access-control';

const today = () => new Date().toISOString().slice(0, 10);
const dateValue = (value?: string) => value?.slice(0, 10) ?? '';
const dateTime = (value: string) => new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
const emptyAssignment = (): SaveProcurementResponsibilityAssignment => ({
  userId: '', roleName: '', warehouseScopeMode: 'None', warehouseIds: [], effectiveFrom: today(),
  locationScopeMode: 'None', locationIds: [], isActive: true, reason: '',
});

const emptyCapability = (): ProcurementAccessCapabilityRequest => ({
  permissionCode: '', sourceType: 'ProcurementAdministration', sourceReference: 'ACCESS-CHECK',
});

export default function ProcurementAccessControlsPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [assignmentOpen, setAssignmentOpen] = useState(false);
  const [assignmentId, setAssignmentId] = useState<string>();
  const [assignmentForm, setAssignmentForm] = useState<SaveProcurementResponsibilityAssignment>(emptyAssignment);
  const [committeeEdit, setCommitteeEdit] = useState<ProcurementCommittee>();
  const [committeeForm, setCommitteeForm] = useState<UpdateProcurementCommittee>();
  const [memberCommittee, setMemberCommittee] = useState<ProcurementCommittee>();
  const [memberForm, setMemberForm] = useState<SaveProcurementCommitteeMember>({ assignmentId: '', memberKind: 'VotingMember', isVoting: true, effectiveFrom: today(), reason: '' });
  const [removeTarget, setRemoveTarget] = useState<{ committee: ProcurementCommittee; member: ProcurementCommitteeMember }>();
  const [removeReason, setRemoveReason] = useState('');
  const [capability, setCapability] = useState<ProcurementAccessCapabilityRequest>(emptyCapability);

  const readiness = useQuery({ queryKey: ['procurement-access-readiness'], queryFn: procurementAccessControlService.readiness });
  const roles = useQuery({ queryKey: ['procurement-access-roles'], queryFn: procurementAccessControlService.roles });
  const permissions = useQuery({ queryKey: ['procurement-access-permissions'], queryFn: procurementAccessControlService.permissions });
  const users = useQuery({ queryKey: ['procurement-access-users'], queryFn: procurementAccessControlService.users });
  const warehouses = useQuery({ queryKey: ['procurement-access-warehouses'], queryFn: procurementAccessControlService.warehouses });
  const locations = useQuery({ queryKey: ['procurement-access-locations'], queryFn: procurementAccessControlService.locations });
  const assignments = useQuery({ queryKey: ['procurement-access-assignments'], queryFn: procurementAccessControlService.assignments });
  const committees = useQuery({ queryKey: ['procurement-access-committees'], queryFn: procurementAccessControlService.committees });
  const workflows = useQuery({ queryKey: ['procurement-access-workflows'], queryFn: procurementAccessControlService.workflows });
  const audit = useQuery({ queryKey: ['procurement-access-audit'], queryFn: () => procurementAccessControlService.audit(100) });

  const refresh = () => Promise.all([
    readiness.refetch(), roles.refetch(), permissions.refetch(), users.refetch(), warehouses.refetch(), locations.refetch(),
    assignments.refetch(), committees.refetch(), workflows.refetch(), audit.refetch(),
  ]);
  const invalidateControls = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['procurement-access-readiness'] }),
      queryClient.invalidateQueries({ queryKey: ['procurement-access-assignments'] }),
      queryClient.invalidateQueries({ queryKey: ['procurement-access-committees'] }),
      queryClient.invalidateQueries({ queryKey: ['procurement-access-audit'] }),
    ]);
  };

  const selectedRole = roles.data?.find(role => role.code === assignmentForm.roleName);
  const warehouseScoped = roleRequiresWarehouseScope(selectedRole, permissions.data ?? []);
  const selectedUser = users.data?.find(user => user.userId === assignmentForm.userId);
  const contextualRoleCodes = useMemo(() => new Set([
    ...(committees.data ?? []).map(committee => committee.requiredRoleName),
    'TDC_OBSERVER',
    'TDC_INTERNAL_AUDIT',
  ]), [committees.data]);
  const contextualRoles = useMemo(() => (roles.data ?? []).filter(role =>
    contextualRoleCodes.has(role.code) || roleRequiresWarehouseScope(role, permissions.data ?? [])),
  [contextualRoleCodes, permissions.data, roles.data]);
  const assignableRoles = contextualRoles.filter(role => selectedUser?.roleNames?.includes(role.code));
  const assignmentError = validateResponsibilityAssignment(assignmentForm, warehouseScoped);
  const selectedPermission = permissions.data?.find(permission => permission.code === capability.permissionCode);
  const capabilityReady = canCheckAccessCapability(capability, Boolean(selectedPermission?.isWarehouseScoped));

  const saveAssignment = useMutation({
    mutationFn: () => procurementAccessControlService.saveAssignment(assignmentId, assignmentForm),
    onSuccess: async () => { setAssignmentOpen(false); await invalidateControls(); toast({ title: 'Responsibility saved', description: 'Tenant, warehouse, and location scope were recorded with an audit entry.' }); },
    onError: (error: Error) => toast({ title: 'Unable to save responsibility', description: error.message, variant: 'destructive' }),
  });
  const saveCommittee = useMutation({
    mutationFn: () => {
      if (!committeeEdit || !committeeForm) throw new Error('Select a committee before saving.');
      return procurementAccessControlService.updateCommittee(committeeEdit.id, committeeForm);
    },
    onSuccess: async () => { setCommitteeEdit(undefined); await invalidateControls(); toast({ title: 'Committee updated' }); },
    onError: (error: Error) => toast({ title: 'Unable to update committee', description: error.message, variant: 'destructive' }),
  });
  const addMember = useMutation({
    mutationFn: () => {
      if (!memberCommittee) throw new Error('Select a committee before adding a member.');
      return procurementAccessControlService.addCommitteeMember(memberCommittee.id, memberForm);
    },
    onSuccess: async () => { setMemberCommittee(undefined); await invalidateControls(); toast({ title: 'Committee member added' }); },
    onError: (error: Error) => toast({ title: 'Unable to add committee member', description: error.message, variant: 'destructive' }),
  });
  const removeMember = useMutation({
    mutationFn: () => {
      if (!removeTarget) throw new Error('Select a committee member before removing.');
      return procurementAccessControlService.removeCommitteeMember(
        removeTarget.committee.id, removeTarget.member.id, removeReason, removeTarget.member.rowVersion);
    },
    onSuccess: async () => { setRemoveTarget(undefined); setRemoveReason(''); await invalidateControls(); toast({ title: 'Committee member removed' }); },
    onError: (error: Error) => toast({ title: 'Unable to remove committee member', description: error.message, variant: 'destructive' }),
  });
  const capabilityCheck = useMutation({
    mutationFn: () => procurementAccessControlService.checkCapability(capability),
    onError: (error: Error) => toast({ title: 'Capability check failed', description: error.message, variant: 'destructive' }),
  });

  const eligibleAssignments = useMemo(() => (assignments.data ?? []).filter(item => !memberCommittee ||
    item.roleName === memberCommittee.requiredRoleName || item.roleName === 'TDC_OBSERVER' || item.roleName === 'TDC_INTERNAL_AUDIT'),
  [assignments.data, memberCommittee]);

  const openAssignment = (item?: ProcurementResponsibilityAssignment) => {
    setAssignmentId(item?.id);
    setAssignmentForm(item ? {
      userId: item.userId, roleName: item.roleName, warehouseScopeMode: item.warehouseScopeMode,
      warehouseIds: item.warehouses.map(scope => scope.warehouseId), effectiveFrom: dateValue(item.effectiveFrom),
      locationScopeMode: item.locationScopeMode, locationIds: item.locations.map(scope => scope.locationId),
      effectiveTo: dateValue(item.effectiveTo) || undefined, isActive: item.isActive, reason: '', rowVersion: item.rowVersion,
    } : emptyAssignment());
    setAssignmentOpen(true);
  };

  const openCommittee = (item: ProcurementCommittee) => {
    setCommitteeEdit(item);
    setCommitteeForm({ name: item.name, description: item.description, requiredQuorum: item.requiredQuorum,
      status: item.status, effectiveFrom: dateValue(item.effectiveFrom), effectiveTo: dateValue(item.effectiveTo) || undefined,
      reason: '', rowVersion: item.rowVersion });
  };

  const openMember = (item: ProcurementCommittee) => {
    setMemberCommittee(item);
    setMemberForm({ assignmentId: '', memberKind: 'VotingMember', isVoting: true, effectiveFrom: today(), reason: '' });
  };

  const loadingFailed = [readiness, roles, permissions, users, warehouses, locations, assignments, committees, workflows, audit].some(query => query.isError);
  const summary = readiness.data;

  return (
    <div className="space-y-6">
      <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-start">
        <div><h1 className="text-3xl font-bold">Procurement scopes & committees</h1><p className="mt-1 max-w-4xl text-muted-foreground">Apply warehouse and location boundaries, constitute TDC committees, and inspect procurement control readiness. User roles and privileges remain centrally managed in Security.</p></div>
        <Button variant="outline" onClick={refresh} disabled={readiness.isFetching}><RefreshCw className={`mr-2 h-4 w-4 ${readiness.isFetching ? 'animate-spin' : ''}`} />Refresh controls</Button>
      </div>

      <Alert className="border-blue-500/40 bg-blue-500/5"><ShieldCheck className="h-4 w-4" /><AlertTitle>Security is the access authority</AlertTitle><AlertDescription className="space-y-3"><p>Assign users to roles and grant role privileges only in the shared Security workspace. This page cannot grant a base privilege; it only narrows an existing Security role by warehouse/location or attaches it to a procurement committee.</p><div className="flex flex-wrap gap-2"><Button asChild size="sm" variant="outline"><Link href="/administration/identity-management/users">Manage users</Link></Button><Button asChild size="sm" variant="outline"><Link href="/administration/identity-management/roles">Manage roles & privileges</Link></Button></div></AlertDescription></Alert>
      <Alert className="border-amber-500/40 bg-amber-500/5"><ShieldCheck className="h-4 w-4" /><AlertTitle>Configuration boundary</AlertTitle><AlertDescription>Workflow routes are unapproved Draft templates. Publish them only through the shared workflow designer after DEC-003 and DEC-004 approval. This workspace does not execute PR, PO, receiving, or inventory transactions.</AlertDescription></Alert>
      {loadingFailed && <Alert variant="destructive"><AlertTriangle className="h-4 w-4" /><AlertTitle>Some access data could not be loaded</AlertTitle><AlertDescription>Refresh after checking API connectivity and administrator authorization.</AlertDescription></Alert>}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
        <Metric label="Security roles" value={`${summary?.configuredRoleCount ?? 0}/${summary?.requiredRoleCount ?? 19}`} detail="central role definitions" ok={summary?.configuredRoleCount === summary?.requiredRoleCount} />
        <Metric label="Security privileges" value={`${summary?.configuredPermissionCount ?? 0}/${summary?.requiredPermissionCount ?? 39}`} detail="central privilege registry" ok={summary?.configuredPermissionCount === summary?.requiredPermissionCount} />
        <Metric label="Context scopes" value={String(summary?.activeAssignmentCount ?? 0)} detail="warehouse or committee duties" ok={(summary?.activeAssignmentCount ?? 0) > 0} />
        <Metric label="Committees" value={`${summary?.readyCommitteeCount ?? 0}/${summary?.requiredCommitteeCount ?? 4}`} detail="active and at quorum" ok={summary?.readyCommitteeCount === summary?.requiredCommitteeCount} />
        <Metric label="Internal Audit" value={summary?.internalAuditIsReadOnly ? 'Read only' : 'Review'} detail="no mutation grant" ok={summary?.internalAuditIsReadOnly} />
      </div>

      {summary?.issues.length ? <Alert><AlertTriangle className="h-4 w-4" /><AlertTitle>UAT readiness remains incomplete</AlertTitle><AlertDescription><ul className="mt-2 list-disc space-y-1 pl-5">{summary.issues.map(issue => <li key={issue}>{issue}</li>)}</ul></AlertDescription></Alert> : null}

      <Card><CardHeader><CardTitle>Shared-control workspace</CardTitle><CardDescription>All writes require an audit reason and remain tenant-scoped.</CardDescription></CardHeader><CardContent>
        <Tabs defaultValue="scopes">
          <TabsList className="grid h-auto w-full grid-cols-2 lg:grid-cols-5"><TabsTrigger value="scopes">Scopes & duties</TabsTrigger><TabsTrigger value="committees">Committees</TabsTrigger><TabsTrigger value="workflows">Workflows</TabsTrigger><TabsTrigger value="capability">Capability</TabsTrigger><TabsTrigger value="audit">Audit</TabsTrigger></TabsList>

          <TabsContent value="scopes" className="space-y-4 pt-4">
            <Alert><ShieldCheck className="h-4 w-4" /><AlertTitle>Context only</AlertTitle><AlertDescription>A scope or committee duty is effective only while the user still holds the matching role in Security. Removing that Security role immediately removes the underlying privilege.</AlertDescription></Alert>
            <div className="flex justify-end"><Button onClick={() => openAssignment()}><Plus className="mr-2 h-4 w-4" />Add contextual scope or duty</Button></div>
            <div className="divide-y rounded-lg border">{(assignments.data ?? []).map(item => <div key={item.id} className="grid gap-3 p-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_minmax(0,.8fr)_auto] lg:items-center"><div><p className="font-medium">{item.userDisplayName || item.username}</p><p className="text-xs text-muted-foreground">{item.username}</p></div><div><p className="font-medium">{item.roleDisplayName}</p><div className="mt-1 flex flex-wrap gap-2"><Badge variant={item.isActive ? 'default' : 'outline'}>{item.isActive ? 'Active' : 'Inactive'}</Badge><Badge variant="outline">Warehouse: {item.warehouseScopeMode}</Badge><Badge variant="outline">Location: {item.locationScopeMode}</Badge></div></div><div className="text-sm text-muted-foreground"><p>{item.warehouses.length ? item.warehouses.map(scope => scope.code).join(', ') : item.warehouseScopeMode === 'All' ? 'All warehouses' : 'No warehouse scope'}</p><p>{item.locations.length ? item.locations.map(scope => `${scope.warehouseCode}/${scope.code}`).join(', ') : item.locationScopeMode === 'All' ? 'All locations' : 'No location scope'}</p><p>{dateValue(item.effectiveFrom)} → {dateValue(item.effectiveTo) || 'Open'}</p></div><Button variant="ghost" size="sm" onClick={() => openAssignment(item)}><Pencil className="mr-2 h-4 w-4" />Edit</Button></div>)}{!assignments.isLoading && !(assignments.data?.length) && <Empty text="No tenant responsibility assignments have been recorded." />}</div>
          </TabsContent>

          <TabsContent value="committees" className="space-y-4 pt-4">{(committees.data ?? []).map(item => <Card key={item.id}><CardHeader className="pb-3"><div className="flex flex-col justify-between gap-3 md:flex-row"><div><div className="flex flex-wrap items-center gap-2"><CardTitle className="text-lg">{item.name}</CardTitle><Badge variant="outline">{item.code}</Badge><Badge variant={item.meetsQuorum ? 'default' : 'destructive'}>{item.activeVotingMemberCount}/{item.requiredQuorum} voting</Badge><Badge variant="outline">{item.status}</Badge></div><CardDescription className="mt-2">Required responsibility: {item.requiredRoleName}</CardDescription></div><div className="flex gap-2"><Button variant="outline" size="sm" onClick={() => openMember(item)} disabled={item.status === 'Retired'}><Users className="mr-2 h-4 w-4" />Add member</Button><Button variant="outline" size="sm" onClick={() => openCommittee(item)}><Pencil className="mr-2 h-4 w-4" />Configure</Button></div></div></CardHeader><CardContent><div className="divide-y rounded-md border">{item.members.map(member => <div key={member.id} className="grid gap-2 p-3 text-sm md:grid-cols-[minmax(0,1fr)_180px_120px_auto] md:items-center"><div><p className="font-medium">{member.userDisplayName || member.username}</p><p className="text-xs text-muted-foreground">{member.roleName}</p></div><span>{member.memberKind.replace(/([A-Z])/g, ' $1').trim()}</span><Badge variant={member.isVoting ? 'default' : 'outline'}>{member.isVoting ? 'Voting' : 'Non-voting'}</Badge><Button variant="ghost" size="sm" onClick={() => { setRemoveTarget({ committee: item, member }); setRemoveReason(''); }} disabled={item.status === 'Retired'}><Trash2 className="mr-2 h-4 w-4" />Remove</Button></div>)}{!item.members.length && <Empty text="No members assigned." />}</div></CardContent></Card>)}{!committees.isLoading && !(committees.data?.length) && <Empty text="Committee templates have not been seeded." />}</TabsContent>

          <TabsContent value="workflows" className="space-y-4 pt-4"><Alert><ShieldCheck className="h-4 w-4" /><AlertTitle>Shared workflow definitions only</AlertTitle><AlertDescription>These templates remain Draft and inactive. Review, version, publish, and retire them through the existing workflow administration after approved routing decisions.</AlertDescription></Alert><div className="divide-y rounded-lg border">{(workflows.data ?? []).map(item => <div key={item.templateCode} className="grid gap-3 p-4 lg:grid-cols-[minmax(0,1fr)_200px_140px_auto] lg:items-center"><div><p className="font-medium">{item.templateName}</p><p className="text-sm text-muted-foreground">{item.initiatorRoleName} → {item.approvalRoleName}</p><p className="font-mono text-xs text-muted-foreground">{item.entityTypeCode} · {item.templateCode}</p></div><span className="text-sm">{item.sourceDecisionKeys}</span><Badge variant={item.isPublished ? 'default' : 'outline'}>{item.status}{item.version ? ` · v${item.version}` : ''}</Badge><Button asChild variant="ghost" size="sm"><Link href="/administration/workflow">Shared designer<ExternalLink className="ml-2 h-3.5 w-3.5" /></Link></Button></div>)}</div></TabsContent>

          <TabsContent value="capability" className="pt-4"><div className="grid gap-5 lg:grid-cols-2"><div className="space-y-4 rounded-lg border p-4"><div className="space-y-2"><Label>Permission</Label><Select value={capability.permissionCode || 'none'} onValueChange={value => { setCapability(current => ({ ...current, permissionCode: value === 'none' ? '' : value, warehouseId: undefined })); capabilityCheck.reset(); }}><SelectTrigger><SelectValue placeholder="Select permission" /></SelectTrigger><SelectContent><SelectItem value="none">Select permission</SelectItem>{(permissions.data ?? []).map(item => <SelectItem key={item.code} value={item.code}>{item.name}</SelectItem>)}</SelectContent></Select></div>{selectedPermission?.isWarehouseScoped && <div className="space-y-2"><Label>Warehouse</Label><Select value={capability.warehouseId || 'none'} onValueChange={value => setCapability(current => ({ ...current, warehouseId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">Select warehouse</SelectItem>{(warehouses.data ?? []).map(item => <SelectItem key={item.warehouseId} value={item.warehouseId}>{item.code} · {item.name}</SelectItem>)}</SelectContent></Select></div>}<div className="space-y-2"><Label>Committee (optional)</Label><Select value={capability.committeeCode || 'none'} onValueChange={value => setCapability(current => ({ ...current, committeeCode: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No committee constraint</SelectItem>{(committees.data ?? []).map(item => <SelectItem key={item.code} value={item.code}>{item.name}</SelectItem>)}</SelectContent></Select></div><div className="grid gap-4 sm:grid-cols-2"><div className="space-y-2"><Label>Source type *</Label><Input value={capability.sourceType} onChange={event => setCapability(current => ({ ...current, sourceType: event.target.value }))} /></div><div className="space-y-2"><Label>Source reference *</Label><Input value={capability.sourceReference} onChange={event => setCapability(current => ({ ...current, sourceReference: event.target.value }))} /></div></div><Button className="w-full" onClick={() => capabilityCheck.mutate()} disabled={!capabilityReady || capabilityCheck.isPending}>Check signed-in actor</Button></div><div className={`rounded-lg border p-5 ${capabilityCheck.data?.allowed ? 'border-emerald-500/40 bg-emerald-500/5' : capabilityCheck.data ? 'border-destructive/40 bg-destructive/5' : ''}`}>{capabilityCheck.data ? <div className="space-y-3"><div className="flex items-center gap-2">{capabilityCheck.data.allowed ? <CheckCircle2 className="h-5 w-5 text-emerald-700" /> : <AlertTriangle className="h-5 w-5 text-destructive" />}<p className="text-lg font-semibold">{capabilityCheck.data.allowed ? 'Allowed' : 'Denied'}</p><Badge variant="outline">{capabilityCheck.data.code}</Badge></div><p>{capabilityCheck.data.message}</p><p className="text-sm">Matched roles: {capabilityCheck.data.matchedRoles.join(', ') || 'None'}</p><p className="break-all font-mono text-xs text-muted-foreground">{capabilityCheck.data.correlationId}</p></div> : <div className="flex min-h-60 flex-col items-center justify-center text-center text-muted-foreground"><ShieldCheck className="mb-3 h-9 w-9" /><p className="font-medium">No capability checked</p><p className="mt-1 max-w-sm text-sm">This preview is read-only. Owning transaction APIs must call the enforce route before protected actions.</p></div>}</div></div></TabsContent>

          <TabsContent value="audit" className="pt-4"><div className="divide-y rounded-lg border">{(audit.data ?? []).map(item => <div key={item.id} className="grid gap-2 p-4 text-sm lg:grid-cols-[180px_230px_minmax(0,1fr)]"><div><p className="font-medium">{item.actorName}</p><p className="text-xs text-muted-foreground">{dateTime(item.timestamp)}</p></div><div><Badge variant="outline">{item.action}</Badge><p className="mt-2">{item.resource} · {item.resourceId}</p></div><details><summary className="cursor-pointer text-muted-foreground">Audit payload</summary><pre className="mt-2 max-h-40 overflow-auto whitespace-pre-wrap break-all rounded bg-muted p-2 text-xs">{item.newValues ?? item.oldValues ?? 'No payload'}</pre></details></div>)}{!audit.isLoading && !(audit.data?.length) && <Empty text="No access-control audit entries have been recorded." />}</div></TabsContent>
        </Tabs>
      </CardContent></Card>

      <Dialog open={assignmentOpen} onOpenChange={setAssignmentOpen}>
        <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
          <DialogHeader><DialogTitle>{assignmentId ? 'Edit contextual scope or duty' : 'Add contextual scope or duty'}</DialogTitle><DialogDescription>First grant the role in Security. This record adds only its effective period, warehouse/location boundary, or committee eligibility.</DialogDescription></DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2"><Label>User *</Label><Select value={assignmentForm.userId || 'none'} onValueChange={value => setAssignmentForm(current => ({ ...current, userId: value === 'none' ? '' : value, roleName: '', warehouseScopeMode: 'None', warehouseIds: [], locationScopeMode: 'None', locationIds: [] }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">Select user</SelectItem>{(users.data ?? []).map(item => <SelectItem key={item.userId} value={item.userId}>{item.displayName || item.username}</SelectItem>)}</SelectContent></Select></div>
            <div className="space-y-2"><Label>Existing Security role *</Label><Select value={assignmentForm.roleName || 'none'} onValueChange={value => { const roleName = value === 'none' ? '' : value; const role = roles.data?.find(item => item.code === roleName); const requiresWarehouse = roleRequiresWarehouseScope(role, permissions.data ?? []); setAssignmentForm(current => ({ ...current, roleName, warehouseScopeMode: requiresWarehouse ? 'Restricted' : 'None', warehouseIds: [], locationScopeMode: requiresWarehouse ? 'Restricted' : 'None', locationIds: [] })); }}><SelectTrigger><SelectValue placeholder={selectedUser ? 'Select contextual role' : 'Select a user first'} /></SelectTrigger><SelectContent><SelectItem value="none">{selectedUser ? 'Select contextual role' : 'Select a user first'}</SelectItem>{assignableRoles.map(item => <SelectItem key={item.code} value={item.code}>{item.name}</SelectItem>)}</SelectContent></Select>{selectedUser && !assignableRoles.length && <p className="text-xs text-amber-700">This user has no warehouse-scoped or committee role. Grant the required role in Security first.</p>}</div>
            {warehouseScoped && <div className="space-y-2 sm:col-span-2"><Label>Warehouse scope *</Label><Select value={assignmentForm.warehouseScopeMode} onValueChange={value => setAssignmentForm(current => ({ ...current, warehouseScopeMode: value as ProcurementWarehouseScopeMode, warehouseIds: value === 'Restricted' ? current.warehouseIds : [], locationIds: value === 'Restricted' ? current.locationIds.filter(id => locations.data?.some(location => location.locationId === id && current.warehouseIds.includes(location.warehouseId))) : current.locationIds }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="All">All tenant warehouses</SelectItem><SelectItem value="Restricted">Restricted warehouses</SelectItem></SelectContent></Select></div>}
            {assignmentForm.warehouseScopeMode === 'Restricted' && <div className="space-y-2 sm:col-span-2"><Label>Assigned warehouses *</Label><div className="grid gap-2 rounded-md border p-3 sm:grid-cols-2">{(warehouses.data ?? []).map(item => <label key={item.warehouseId} className="flex items-center gap-2 text-sm"><Checkbox checked={assignmentForm.warehouseIds.includes(item.warehouseId)} onCheckedChange={checked => setAssignmentForm(current => ({ ...current, warehouseIds: checked ? [...current.warehouseIds, item.warehouseId] : current.warehouseIds.filter(id => id !== item.warehouseId), locationIds: checked ? current.locationIds : current.locationIds.filter(id => locations.data?.find(location => location.locationId === id)?.warehouseId !== item.warehouseId) }))} />{item.code} · {item.name}</label>)}</div></div>}
            {warehouseScoped && <div className="space-y-2 sm:col-span-2"><Label>Location scope *</Label><Select value={assignmentForm.locationScopeMode} onValueChange={value => setAssignmentForm(current => ({ ...current, locationScopeMode: value as ProcurementLocationScopeMode, locationIds: value === 'Restricted' ? current.locationIds : [] }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="All">All locations in allowed warehouses</SelectItem><SelectItem value="Restricted">Restricted locations</SelectItem></SelectContent></Select></div>}
            {assignmentForm.locationScopeMode === 'Restricted' && <div className="space-y-2 sm:col-span-2"><Label>Assigned locations *</Label><div className="grid max-h-56 gap-2 overflow-y-auto rounded-md border p-3 sm:grid-cols-2">{(locations.data ?? []).filter(item => assignmentForm.warehouseScopeMode === 'All' || assignmentForm.warehouseIds.includes(item.warehouseId)).map(item => <label key={item.locationId} className="flex items-center gap-2 text-sm"><Checkbox checked={assignmentForm.locationIds.includes(item.locationId)} onCheckedChange={checked => setAssignmentForm(current => ({ ...current, locationIds: checked ? [...current.locationIds, item.locationId] : current.locationIds.filter(id => id !== item.locationId) }))} /><span>{item.warehouseCode} / {item.code}<span className="block text-xs text-muted-foreground">{item.name}</span></span></label>)}</div></div>}
            <div className="space-y-2"><Label>Effective from *</Label><Input type="date" value={assignmentForm.effectiveFrom} onChange={event => setAssignmentForm(current => ({ ...current, effectiveFrom: event.target.value }))} /></div>
            <div className="space-y-2"><Label>Effective to</Label><Input type="date" value={assignmentForm.effectiveTo ?? ''} onChange={event => setAssignmentForm(current => ({ ...current, effectiveTo: event.target.value || undefined }))} /></div>
            <label className="flex items-center gap-2 text-sm sm:col-span-2"><Checkbox checked={assignmentForm.isActive} onCheckedChange={checked => setAssignmentForm(current => ({ ...current, isActive: checked === true }))} />Active assignment</label>
            <div className="space-y-2 sm:col-span-2"><Label>Audit reason *</Label><Textarea value={assignmentForm.reason} onChange={event => setAssignmentForm(current => ({ ...current, reason: event.target.value }))} placeholder="Why this responsibility is being assigned or changed" /></div>
            {assignmentError && <p className="text-sm text-destructive sm:col-span-2">{assignmentError}</p>}
          </div>
          <DialogFooter><Button variant="outline" onClick={() => setAssignmentOpen(false)}>Cancel</Button><Button onClick={() => saveAssignment.mutate()} disabled={Boolean(assignmentError) || saveAssignment.isPending}>Save contextual scope</Button></DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={Boolean(committeeEdit)} onOpenChange={open => !open && setCommitteeEdit(undefined)}><DialogContent><DialogHeader><DialogTitle>Configure {committeeEdit?.name}</DialogTitle><DialogDescription>Activation is rejected until effective voting membership reaches quorum.</DialogDescription></DialogHeader>{committeeForm && <div className="space-y-4"><div className="space-y-2"><Label>Name *</Label><Input value={committeeForm.name} onChange={event => setCommitteeForm(current => current && ({ ...current, name: event.target.value }))} /></div><div className="space-y-2"><Label>Description</Label><Textarea value={committeeForm.description ?? ''} onChange={event => setCommitteeForm(current => current && ({ ...current, description: event.target.value }))} /></div><div className="grid gap-4 sm:grid-cols-2"><div className="space-y-2"><Label>Required quorum *</Label><Input type="number" min={1} max={50} value={committeeForm.requiredQuorum} onChange={event => setCommitteeForm(current => current && ({ ...current, requiredQuorum: Number(event.target.value) }))} /></div><div className="space-y-2"><Label>Status *</Label><Select value={committeeForm.status} onValueChange={value => setCommitteeForm(current => current && ({ ...current, status: value as ProcurementCommitteeStatus }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="Draft">Draft</SelectItem><SelectItem value="Active" disabled={!committeeEdit || !canActivateCommittee({ activeVotingMemberCount: committeeEdit.activeVotingMemberCount, requiredQuorum: committeeForm.requiredQuorum })}>Active</SelectItem><SelectItem value="Retired">Retired</SelectItem></SelectContent></Select></div><div className="space-y-2"><Label>Effective from *</Label><Input type="date" value={committeeForm.effectiveFrom} onChange={event => setCommitteeForm(current => current && ({ ...current, effectiveFrom: event.target.value }))} /></div><div className="space-y-2"><Label>Effective to</Label><Input type="date" value={committeeForm.effectiveTo ?? ''} onChange={event => setCommitteeForm(current => current && ({ ...current, effectiveTo: event.target.value || undefined }))} /></div></div><div className="space-y-2"><Label>Audit reason *</Label><Textarea value={committeeForm.reason} onChange={event => setCommitteeForm(current => current && ({ ...current, reason: event.target.value }))} /></div></div>}<DialogFooter><Button variant="outline" onClick={() => setCommitteeEdit(undefined)}>Cancel</Button><Button onClick={() => saveCommittee.mutate()} disabled={!committeeForm?.name.trim() || !committeeForm?.reason.trim() || saveCommittee.isPending}>Save committee</Button></DialogFooter></DialogContent></Dialog>

      <Dialog open={Boolean(memberCommittee)} onOpenChange={open => !open && setMemberCommittee(undefined)}><DialogContent><DialogHeader><DialogTitle>Add {memberCommittee?.name} member</DialogTitle><DialogDescription>Voting and administrative members require {memberCommittee?.requiredRoleName}. TDC observers and Internal Audit may be added only as observers.</DialogDescription></DialogHeader><div className="space-y-4"><div className="space-y-2"><Label>Responsibility assignment *</Label><Select value={memberForm.assignmentId || 'none'} onValueChange={value => setMemberForm(current => ({ ...current, assignmentId: value === 'none' ? '' : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">Select assigned user</SelectItem>{eligibleAssignments.map(item => <SelectItem key={item.id} value={item.id}>{item.userDisplayName || item.username} · {item.roleDisplayName}</SelectItem>)}</SelectContent></Select></div><div className="space-y-2"><Label>Member kind *</Label><Select value={memberForm.memberKind} onValueChange={value => { const kind = value as ProcurementCommitteeMemberKind; setMemberForm(current => ({ ...current, memberKind: kind, isVoting: kind !== 'Observer' && current.isVoting })); }}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{(['Chair','VotingMember','NonVotingMember','Observer','Secretary'] as ProcurementCommitteeMemberKind[]).map(kind => <SelectItem key={kind} value={kind}>{kind.replace(/([A-Z])/g, ' $1').trim()}</SelectItem>)}</SelectContent></Select></div><label className="flex items-center gap-2 text-sm"><Checkbox checked={memberForm.isVoting} disabled={memberForm.memberKind === 'Observer'} onCheckedChange={checked => setMemberForm(current => ({ ...current, isVoting: checked === true }))} />Voting member</label><div className="grid gap-4 sm:grid-cols-2"><div className="space-y-2"><Label>Effective from *</Label><Input type="date" value={memberForm.effectiveFrom} onChange={event => setMemberForm(current => ({ ...current, effectiveFrom: event.target.value }))} /></div><div className="space-y-2"><Label>Effective to</Label><Input type="date" value={memberForm.effectiveTo ?? ''} onChange={event => setMemberForm(current => ({ ...current, effectiveTo: event.target.value || undefined }))} /></div></div><div className="space-y-2"><Label>Audit reason *</Label><Textarea value={memberForm.reason} onChange={event => setMemberForm(current => ({ ...current, reason: event.target.value }))} /></div></div><DialogFooter><Button variant="outline" onClick={() => setMemberCommittee(undefined)}>Cancel</Button><Button onClick={() => addMember.mutate()} disabled={!memberForm.assignmentId || !memberForm.reason.trim() || addMember.isPending}>Add member</Button></DialogFooter></DialogContent></Dialog>

      <Dialog open={Boolean(removeTarget)} onOpenChange={open => !open && setRemoveTarget(undefined)}><DialogContent><DialogHeader><DialogTitle>Remove committee member?</DialogTitle><DialogDescription>The API will reject removal from an active committee when it would place voting membership below quorum.</DialogDescription></DialogHeader><div className="space-y-2"><Label>Audit reason *</Label><Textarea value={removeReason} onChange={event => setRemoveReason(event.target.value)} /></div><DialogFooter><Button variant="outline" onClick={() => setRemoveTarget(undefined)}>Cancel</Button><Button variant="destructive" onClick={() => removeMember.mutate()} disabled={!removeReason.trim() || removeMember.isPending}>Remove member</Button></DialogFooter></DialogContent></Dialog>
    </div>
  );
}

function Metric({ label, value, detail, ok }: { label: string; value: string; detail: string; ok?: boolean }) {
  return <div className="rounded-lg border p-4"><div className="flex items-center justify-between gap-2"><p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{label}</p><span className={`h-2.5 w-2.5 rounded-full ${ok ? 'bg-emerald-500' : 'bg-amber-500'}`} /></div><p className="mt-2 text-2xl font-semibold">{value}</p><p className="text-sm text-muted-foreground">{detail}</p></div>;
}

function Empty({ text }: { text: string }) { return <p className="p-8 text-center text-sm text-muted-foreground">{text}</p>; }
