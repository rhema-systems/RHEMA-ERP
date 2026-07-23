'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { Eye, Plus, RefreshCw } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { procurementConfigurationService } from '@/services/procurement-configuration.service';
import { procurementPolicyService } from '@/services/procurement-policy.service';
import type {
  CreateProcurementPolicySetRequest,
  ProcurementPolicyLifecycleStatus,
  ProcurementPolicySetSummary,
} from '@/types/procurement-policy';

type TableCellProps<T> = { row: { original: T } };
const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString() : 'Open-ended';

export default function ProcurementPolicySetsPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [status, setStatus] = useState<ProcurementPolicyLifecycleStatus | 'All'>('All');
  const [search, setSearch] = useState('');
  const [createOpen, setCreateOpen] = useState(false);
  const [form, setForm] = useState<CreateProcurementPolicySetRequest>({
    sourceConfigurationProfileId: '', code: 'TDC-EXECUTABLE', name: 'TDC Executable Procurement Policy',
    scopeType: 'TenantBaseline', defaultCurrencyCode: 'GHS', effectiveFrom: new Date().toISOString().slice(0, 10), isDefault: false,
  });

  const policies = useQuery({
    queryKey: ['procurement-policy-sets', status, search],
    queryFn: () => procurementPolicyService.list({ search: search.trim() || undefined, status: status === 'All' ? undefined : status, page: 1, pageSize: 100 }),
  });
  const sources = useQuery({
    queryKey: ['procurement-configuration-profiles', 'immutable-sources'],
    queryFn: async () => {
      const [published, retired] = await Promise.all([
        procurementConfigurationService.list({ status: 'Published', page: 1, pageSize: 100 }),
        procurementConfigurationService.list({ status: 'Retired', page: 1, pageSize: 100 }),
      ]);
      return [...published.items, ...retired.items];
    },
    enabled: createOpen,
  });
  const immutablePolicies = (policies.data?.items ?? []).filter(item => item.lifecycleStatus !== 'Draft');

  const create = useMutation({
    mutationFn: () => procurementPolicyService.create({
      ...form,
      code: form.code.trim().toUpperCase(), name: form.name.trim(), description: form.description?.trim() || undefined,
      basePolicySetId: form.scopeType === 'TenantOverride' ? form.basePolicySetId : undefined,
      defaultCurrencyCode: form.defaultCurrencyCode.trim().toUpperCase(), effectiveTo: form.effectiveTo || undefined,
      changeSummary: form.changeSummary?.trim() || undefined,
    }),
    onSuccess: async created => {
      setCreateOpen(false);
      await queryClient.invalidateQueries({ queryKey: ['procurement-policy-sets'] });
      toast({ title: 'Executable policy draft created', description: 'Approved configuration decisions were materialized into normalized rules.', variant: 'success' });
      router.push(`/administration/procurement/policy-sets/${created.id}`);
    },
    onError: (error: Error) => toast({ title: 'Unable to create policy', description: error.message, variant: 'destructive' }),
  });

  const columns = useMemo<Array<DataTableColumn<ProcurementPolicySetSummary>>>(() => [
    {
      id: 'policy', header: 'Policy / version', accessorKey: 'code',
      cell: ({ row }: TableCellProps<ProcurementPolicySetSummary>) => (
        <button className="text-left" onClick={() => router.push(`/administration/procurement/policy-sets/${row.original.id}`)}>
          <span className="block font-medium text-primary hover:underline">{row.original.name}</span>
          <span className="text-xs text-muted-foreground">{row.original.code} · v{row.original.version} · {row.original.scopeType === 'TenantOverride' ? 'tenant override' : 'tenant baseline'}</span>
        </button>
      ),
    },
    { id: 'status', header: 'Lifecycle', accessorKey: 'lifecycleStatus', cell: ({ row }: TableCellProps<ProcurementPolicySetSummary>) => <Badge variant={row.original.lifecycleStatus === 'Published' ? 'default' : 'secondary'}>{row.original.lifecycleStatus}</Badge> },
    { id: 'source', header: 'Configuration source', cell: ({ row }: TableCellProps<ProcurementPolicySetSummary>) => <div>{row.original.sourceConfigurationProfileCode}<span className="block text-xs text-muted-foreground">version {row.original.sourceConfigurationProfileVersion}</span></div> },
    { id: 'effective', header: 'Effective period', cell: ({ row }: TableCellProps<ProcurementPolicySetSummary>) => `${formatDate(row.original.effectiveFrom)} – ${formatDate(row.original.effectiveTo)}` },
    { id: 'readiness', header: 'Rule readiness', cell: ({ row }: TableCellProps<ProcurementPolicySetSummary>) => <div><span className="font-medium">{row.original.ruleFamilyCount}/7 families · {row.original.ruleCount} rules</span><span className="block text-xs text-muted-foreground">{row.original.isComplete ? 'Publication validation passed' : 'Action required'}</span></div> },
    { id: 'updated', header: 'Last activity', cell: ({ row }: TableCellProps<ProcurementPolicySetSummary>) => <div>{new Date(row.original.updatedAt).toLocaleString()}<span className="block text-xs text-muted-foreground">{row.original.updatedBy || 'System'}</span></div> },
  ], [router]);

  const canCreate = form.sourceConfigurationProfileId && form.code.trim() && form.name.trim() && form.effectiveFrom &&
    (form.scopeType === 'TenantBaseline' || form.basePolicySetId);

  return (
    <div className="space-y-6">
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-start">
        <div><h1 className="text-3xl font-bold">Executable procurement policies</h1><p className="mt-1 text-muted-foreground">Manage normalized category, method, threshold, authority, evidence, exception, and segregation-of-duties rules.</p></div>
        <div className="flex gap-2"><Button variant="outline" onClick={() => policies.refetch()} disabled={policies.isFetching}><RefreshCw className={`mr-2 h-4 w-4 ${policies.isFetching ? 'animate-spin' : ''}`} />Refresh</Button><Button onClick={() => setCreateOpen(true)}><Plus className="mr-2 h-4 w-4" />New policy</Button></div>
      </div>

      <div className="grid max-w-2xl gap-3 sm:grid-cols-2">
        <div className="space-y-2"><Label>Search policy history</Label><Input value={search} onChange={event => setSearch(event.target.value)} placeholder="Code or name" /></div>
        <div className="space-y-2"><Label>Lifecycle</Label><Select value={status} onValueChange={next => setStatus(next as ProcurementPolicyLifecycleStatus | 'All')}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="All">All statuses</SelectItem><SelectItem value="Draft">Draft</SelectItem><SelectItem value="Published">Published</SelectItem><SelectItem value="Retired">Retired</SelectItem></SelectContent></Select></div>
      </div>

      <DataTable compact title="Policy version history" description="Newest activity first. Published and retired rule sets remain immutable and can only be cloned." data={policies.data?.items ?? []} columns={columns} loading={policies.isLoading} error={policies.error ? 'Failed to load executable policies.' : null} enablePagination pageSize={20} emptyStateMessage="No executable procurement policies exist for this tenant." onRowDoubleClick={row => router.push(`/administration/procurement/policy-sets/${row.original.id}`)} rowActions={[{ id: 'open', label: 'Open policy', icon: Eye, onClick: row => router.push(`/administration/procurement/policy-sets/${row.original.id}`) }]} />

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader><DialogTitle>Create executable policy draft</DialogTitle><DialogDescription>Select an immutable, approved DEC-001 through DEC-014 profile. Its governed values are materialized into relational rules; no transaction behavior changes here.</DialogDescription></DialogHeader>
          <div className="grid gap-4 py-2 sm:grid-cols-2">
            <div className="space-y-2 sm:col-span-2"><Label>Approved configuration source *</Label><Select value={form.sourceConfigurationProfileId} onValueChange={next => setForm(current => ({ ...current, sourceConfigurationProfileId: next }))}><SelectTrigger><SelectValue placeholder={sources.isLoading ? 'Loading approved versions…' : 'Select immutable configuration'} /></SelectTrigger><SelectContent>{(sources.data ?? []).map(source => <SelectItem key={source.id} value={source.id}>{source.profileCode} · v{source.version} · {source.lifecycleStatus}</SelectItem>)}</SelectContent></Select></div>
            <div className="space-y-2"><Label>Policy code *</Label><Input value={form.code} onChange={event => setForm(current => ({ ...current, code: event.target.value }))} /></div>
            <div className="space-y-2"><Label>Name *</Label><Input value={form.name} onChange={event => setForm(current => ({ ...current, name: event.target.value }))} /></div>
            <div className="space-y-2"><Label>Scope</Label><Select value={form.scopeType} onValueChange={next => setForm(current => ({ ...current, scopeType: next as CreateProcurementPolicySetRequest['scopeType'], basePolicySetId: undefined }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="TenantBaseline">Tenant baseline</SelectItem><SelectItem value="TenantOverride">Tenant override</SelectItem></SelectContent></Select></div>
            <div className="space-y-2"><Label>Default currency</Label><Input maxLength={3} value={form.defaultCurrencyCode} onChange={event => setForm(current => ({ ...current, defaultCurrencyCode: event.target.value }))} /></div>
            {form.scopeType === 'TenantOverride' && <div className="space-y-2 sm:col-span-2"><Label>Immutable base policy *</Label><Select value={form.basePolicySetId ?? ''} onValueChange={next => setForm(current => ({ ...current, basePolicySetId: next }))}><SelectTrigger><SelectValue placeholder="Select published or retired base" /></SelectTrigger><SelectContent>{immutablePolicies.map(policy => <SelectItem key={policy.id} value={policy.id}>{policy.code} · v{policy.version} · {policy.lifecycleStatus}</SelectItem>)}</SelectContent></Select></div>}
            <div className="space-y-2"><Label>Effective from *</Label><Input type="date" value={form.effectiveFrom} onChange={event => setForm(current => ({ ...current, effectiveFrom: event.target.value }))} /></div>
            <div className="space-y-2"><Label>Effective to</Label><Input type="date" value={form.effectiveTo ?? ''} onChange={event => setForm(current => ({ ...current, effectiveTo: event.target.value }))} /></div>
            <div className="space-y-2 sm:col-span-2"><Label>Description</Label><Textarea value={form.description ?? ''} onChange={event => setForm(current => ({ ...current, description: event.target.value }))} /></div>
            <div className="space-y-2 sm:col-span-2"><Label>Change summary</Label><Textarea value={form.changeSummary ?? ''} onChange={event => setForm(current => ({ ...current, changeSummary: event.target.value }))} /></div>
            <div className="flex items-center gap-3 sm:col-span-2"><Switch checked={form.isDefault} onCheckedChange={next => setForm(current => ({ ...current, isDefault: next }))} /><span className="text-sm">Default executable policy family</span></div>
          </div>
          <DialogFooter><Button variant="outline" onClick={() => setCreateOpen(false)}>Cancel</Button><Button onClick={() => create.mutate()} disabled={create.isPending || !canCreate}>{create.isPending ? 'Materializing…' : 'Create policy draft'}</Button></DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
