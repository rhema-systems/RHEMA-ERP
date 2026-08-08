'use client';

import Link from 'next/link';
import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Pencil, Plus, RefreshCw, ShieldCheck, ShieldOff } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { financeAccessScopeService } from '@/services/finance-access-scope.service';
import type {
  FinanceAccessLevel,
  FinanceAccessScopeGrant,
  FinanceAccessScopeType,
  SaveFinanceAccessScopeGrant,
} from '@/types/finance-access-scope';

const today = () => new Date().toISOString().slice(0, 10);
const dateValue = (value?: string) => value?.slice(0, 10) ?? '';
const emptyGrant = (): SaveFinanceAccessScopeGrant => ({
  userId: '',
  scopeType: 'BankAccount',
  scopeValue: undefined,
  accessLevel: 'Read',
  effectiveFrom: today(),
  effectiveTo: undefined,
  isActive: true,
  reason: '',
});

export default function FinanceAccessScopesPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState<string>();
  const [form, setForm] = useState<SaveFinanceAccessScopeGrant>(emptyGrant);
  const [deactivateTarget, setDeactivateTarget] = useState<FinanceAccessScopeGrant>();
  const [deactivateReason, setDeactivateReason] = useState('');

  const grants = useQuery({ queryKey: ['finance-access-scope-grants'], queryFn: () => financeAccessScopeService.grants() });
  const users = useQuery({ queryKey: ['finance-access-scope-users'], queryFn: financeAccessScopeService.users });
  const banks = useQuery({ queryKey: ['finance-access-scope-banks'], queryFn: financeAccessScopeService.bankAccounts });

  const refresh = async () => Promise.all([grants.refetch(), users.refetch(), banks.refetch()]);
  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['finance-access-scope-grants'] });

  const saveGrant = useMutation({
    mutationFn: () => financeAccessScopeService.save(editingId, form),
    onSuccess: async () => {
      setDialogOpen(false);
      await invalidate();
      toast({ title: 'Finance scope saved', description: 'The effective-dated access boundary is now recorded and auditable.' });
    },
    onError: (error: Error) => toast({ title: 'Unable to save Finance scope', description: error.message, variant: 'destructive' }),
  });

  const deactivateGrant = useMutation({
    mutationFn: () => {
      if (!deactivateTarget) throw new Error('Select a grant to deactivate.');
      return financeAccessScopeService.deactivate(deactivateTarget, deactivateReason.trim());
    },
    onSuccess: async () => {
      setDeactivateTarget(undefined);
      setDeactivateReason('');
      await invalidate();
      toast({ title: 'Finance scope deactivated', description: 'Historical evidence was retained; the grant will no longer authorize access.' });
    },
    onError: (error: Error) => toast({ title: 'Unable to deactivate Finance scope', description: error.message, variant: 'destructive' }),
  });

  const openNew = () => {
    setEditingId(undefined);
    setForm(emptyGrant());
    setDialogOpen(true);
  };
  const openEdit = (grant: FinanceAccessScopeGrant) => {
    setEditingId(grant.id);
    setForm({
      userId: grant.userId,
      scopeType: grant.scopeType,
      scopeValue: grant.scopeValue,
      accessLevel: grant.accessLevel,
      effectiveFrom: dateValue(grant.effectiveFrom),
      effectiveTo: dateValue(grant.effectiveTo) || undefined,
      isActive: grant.isActive,
      reason: grant.reason,
      rowVersion: grant.rowVersion,
    });
    setDialogOpen(true);
  };

  const grantError = !form.userId
    ? 'Select a tenant user.'
    : form.scopeType === 'BankAccount' && !form.scopeValue
      ? 'Select a bank account.'
      : form.reason.trim().length < 10
        ? 'Provide an audit reason of at least 10 characters.'
        : undefined;
  const activeCount = (grants.data ?? []).filter(item => item.isActive).length;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-3xl font-bold tracking-tight"><ShieldCheck className="h-8 w-8" /> Finance Access Scopes</h1>
          <p className="text-muted-foreground">Restrict Finance operations by tenant or bank account without duplicating the role-permission system.</p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => refresh()}><RefreshCw className="mr-2 h-4 w-4" /> Refresh</Button>
          <Button onClick={openNew}><Plus className="mr-2 h-4 w-4" /> Add Grant</Button>
        </div>
      </div>

      <Alert>
        <ShieldCheck className="h-4 w-4" />
        <AlertTitle>Permission plus data scope</AlertTitle>
        <AlertDescription>
          A role still determines which action a user may perform. These grants determine which Finance data that action may touch. Create and review grants here, then enable enforcement in <Link className="font-medium underline" href="/administration/finance/settings">Finance Settings</Link>.
        </AlertDescription>
      </Alert>

      <div className="grid gap-4 md:grid-cols-3">
        <Metric label="Recorded grants" value={String(grants.data?.length ?? 0)} />
        <Metric label="Active grants" value={String(activeCount)} />
        <Metric label="Assignable bank accounts" value={String((banks.data ?? []).filter(item => item.isActive).length)} />
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Effective grants</CardTitle>
          <CardDescription>Expired and deactivated records remain visible so access decisions can be reconstructed.</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto rounded-md border">
            <table className="w-full text-sm">
              <thead className="bg-muted/40 text-left text-xs uppercase text-muted-foreground">
                <tr><th className="p-3">User</th><th className="p-3">Scope</th><th className="p-3">Level</th><th className="p-3">Effective</th><th className="p-3">Status</th><th className="p-3 text-right">Actions</th></tr>
              </thead>
              <tbody>
                {(grants.data ?? []).map(grant => (
                  <tr key={grant.id} className="border-t align-top">
                    <td className="p-3"><p className="font-medium">{grant.userDisplayName || grant.username}</p><p className="text-xs text-muted-foreground">{grant.username}</p></td>
                    <td className="p-3"><p className="font-medium">{grant.scopeDisplayName || 'All Finance data in tenant'}</p><p className="text-xs text-muted-foreground">{grant.scopeType}</p><p className="mt-1 max-w-sm text-xs text-muted-foreground">{grant.reason}</p></td>
                    <td className="p-3"><Badge variant="outline">{grant.accessLevel}</Badge></td>
                    <td className="p-3">{new Date(grant.effectiveFrom).toLocaleDateString()}<br /><span className="text-xs text-muted-foreground">to {grant.effectiveTo ? new Date(grant.effectiveTo).toLocaleDateString() : 'open-ended'}</span></td>
                    <td className="p-3"><Badge variant={grant.isActive ? 'default' : 'secondary'}>{grant.isActive ? 'Active' : 'Inactive'}</Badge></td>
                    <td className="p-3"><div className="flex justify-end gap-1"><Button variant="ghost" size="sm" onClick={() => openEdit(grant)}><Pencil className="mr-2 h-4 w-4" /> Edit</Button>{grant.isActive && <Button variant="ghost" size="sm" onClick={() => setDeactivateTarget(grant)}><ShieldOff className="mr-2 h-4 w-4" /> Deactivate</Button>}</div></td>
                  </tr>
                ))}
                {!grants.isLoading && !(grants.data?.length) && <tr><td colSpan={6} className="p-10 text-center text-muted-foreground">No Finance scope grants have been configured.</td></tr>}
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader><DialogTitle>{editingId ? 'Edit Finance scope' : 'Add Finance scope'}</DialogTitle><DialogDescription>Assign the least access level needed. Operate covers entry/posting; Approve is required for posted-payment reversal.</DialogDescription></DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2 sm:col-span-2"><Label>User *</Label><Select value={form.userId || 'none'} onValueChange={value => setForm(current => ({ ...current, userId: value === 'none' ? '' : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">Select user</SelectItem>{(users.data ?? []).map(user => <SelectItem key={user.userId} value={user.userId}>{user.displayName || user.username} · {user.username}</SelectItem>)}</SelectContent></Select></div>
            <div className="space-y-2"><Label>Scope *</Label><Select value={form.scopeType} onValueChange={value => setForm(current => ({ ...current, scopeType: value as FinanceAccessScopeType, scopeValue: undefined }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="BankAccount">Specific bank account</SelectItem><SelectItem value="Tenant">All Finance data in tenant</SelectItem></SelectContent></Select></div>
            <div className="space-y-2"><Label>Access level *</Label><Select value={form.accessLevel} onValueChange={value => setForm(current => ({ ...current, accessLevel: value as FinanceAccessLevel }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{(['Read', 'Operate', 'Approve', 'Administer'] as FinanceAccessLevel[]).map(level => <SelectItem key={level} value={level}>{level}</SelectItem>)}</SelectContent></Select></div>
            {form.scopeType === 'BankAccount' && <div className="space-y-2 sm:col-span-2"><Label>Bank account *</Label><Select value={form.scopeValue || 'none'} onValueChange={value => setForm(current => ({ ...current, scopeValue: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">Select bank account</SelectItem>{(banks.data ?? []).filter(bank => bank.isActive).map(bank => <SelectItem key={bank.bankAccountId} value={bank.bankAccountId}>{bank.bankName} · {bank.accountName} · {bank.accountNumber} ({bank.currency})</SelectItem>)}</SelectContent></Select></div>}
            <div className="space-y-2"><Label>Effective from *</Label><Input type="date" value={form.effectiveFrom} onChange={event => setForm(current => ({ ...current, effectiveFrom: event.target.value }))} /></div>
            <div className="space-y-2"><Label>Effective to</Label><Input type="date" value={form.effectiveTo ?? ''} onChange={event => setForm(current => ({ ...current, effectiveTo: event.target.value || undefined }))} /></div>
            <label className="flex items-center gap-2 text-sm sm:col-span-2"><Checkbox checked={form.isActive} onCheckedChange={checked => setForm(current => ({ ...current, isActive: checked === true }))} />Active grant</label>
            <div className="space-y-2 sm:col-span-2"><Label>Audit reason *</Label><Textarea value={form.reason} onChange={event => setForm(current => ({ ...current, reason: event.target.value }))} placeholder="Why this Finance access is needed or being changed" rows={4} /></div>
            {grantError && <p className="text-sm text-destructive sm:col-span-2">{grantError}</p>}
          </div>
          <DialogFooter><Button variant="outline" onClick={() => setDialogOpen(false)}>Cancel</Button><Button onClick={() => saveGrant.mutate()} disabled={Boolean(grantError) || saveGrant.isPending}>Save Grant</Button></DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={Boolean(deactivateTarget)} onOpenChange={open => !open && setDeactivateTarget(undefined)}>
        <DialogContent>
          <DialogHeader><DialogTitle>Deactivate Finance scope?</DialogTitle><DialogDescription>The record and its audit history remain; it will stop authorizing future Finance operations.</DialogDescription></DialogHeader>
          <div className="space-y-2"><Label>Deactivation reason *</Label><Textarea value={deactivateReason} onChange={event => setDeactivateReason(event.target.value)} rows={4} /></div>
          <DialogFooter><Button variant="outline" onClick={() => setDeactivateTarget(undefined)}>Cancel</Button><Button variant="destructive" onClick={() => deactivateGrant.mutate()} disabled={deactivateReason.trim().length < 10 || deactivateGrant.isPending}>Deactivate</Button></DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return <Card><CardContent className="p-4"><p className="text-xs font-medium uppercase text-muted-foreground">{label}</p><p className="mt-2 text-2xl font-semibold">{value}</p></CardContent></Card>;
}
