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
import type {
  CreateProcurementConfigurationProfileRequest,
  ProcurementConfigurationProfileStatus,
  ProcurementConfigurationProfileSummary,
} from '@/types/procurement-configuration';

type TableCellProps<T> = { row: { original: T } };

const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString() : 'Open-ended';

export default function ProcurementPolicyProfilesPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [status, setStatus] = useState<ProcurementConfigurationProfileStatus | 'All'>('All');
  const [createOpen, setCreateOpen] = useState(false);
  const [form, setForm] = useState<CreateProcurementConfigurationProfileRequest>({
    profileCode: 'TDC-PROCUREMENT',
    name: 'TDC Procurement Policy',
    effectiveFrom: new Date().toISOString().slice(0, 10),
    isDefault: false,
  });

  const profiles = useQuery({
    queryKey: ['procurement-configuration-profiles', status],
    queryFn: () => procurementConfigurationService.list({ status: status === 'All' ? undefined : status, page: 1, pageSize: 100 }),
  });

  const create = useMutation({
    mutationFn: () => procurementConfigurationService.create({
      ...form,
      profileCode: form.profileCode.trim(),
      name: form.name.trim(),
      changeSummary: form.changeSummary?.trim() || undefined,
      effectiveTo: form.effectiveTo || undefined,
    }),
    onSuccess: async created => {
      setCreateOpen(false);
      await queryClient.invalidateQueries({ queryKey: ['procurement-configuration-profiles'] });
      toast({ title: 'Draft created', description: 'The profile and all 14 governed decisions were created.', variant: 'success' });
      router.push(`/administration/procurement/policy-profiles/${created.id}`);
    },
    onError: (error: Error) => toast({ title: 'Unable to create profile', description: error.message, variant: 'destructive' }),
  });

  const columns = useMemo<Array<DataTableColumn<ProcurementConfigurationProfileSummary>>>(() => [
    {
      id: 'profile',
      header: 'Profile / version',
      accessorKey: 'profileCode',
      cell: ({ row }: TableCellProps<ProcurementConfigurationProfileSummary>) => (
        <button className="text-left" onClick={() => router.push(`/administration/procurement/policy-profiles/${row.original.id}`)}>
          <span className="block font-medium text-primary hover:underline">{row.original.name}</span>
          <span className="text-xs text-muted-foreground">{row.original.profileCode} · v{row.original.version}</span>
        </button>
      ),
    },
    {
      id: 'status',
      header: 'Lifecycle',
      accessorKey: 'lifecycleStatus',
      cell: ({ row }: TableCellProps<ProcurementConfigurationProfileSummary>) => <Badge variant={row.original.lifecycleStatus === 'Published' ? 'default' : 'secondary'}>{row.original.lifecycleStatus}</Badge>,
    },
    {
      id: 'effective',
      header: 'Effective period',
      cell: ({ row }: TableCellProps<ProcurementConfigurationProfileSummary>) => `${formatDate(row.original.effectiveFrom)} – ${formatDate(row.original.effectiveTo)}`,
    },
    {
      id: 'completion',
      header: 'Decision readiness',
      cell: ({ row }: TableCellProps<ProcurementConfigurationProfileSummary>) => (
        <div><span className="font-medium">{row.original.completeDecisionCount}/{row.original.totalDecisionCount}</span><span className="block text-xs text-muted-foreground">{row.original.isComplete ? 'Ready for publication validation' : 'Action required'}</span></div>
      ),
    },
    {
      id: 'updated',
      header: 'Last activity',
      cell: ({ row }: TableCellProps<ProcurementConfigurationProfileSummary>) => <div>{new Date(row.original.updatedAt).toLocaleString()}<span className="block text-xs text-muted-foreground">{row.original.updatedBy || 'System'}</span></div>,
    },
  ], [router]);

  return (
    <div className="space-y-6">
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-start">
        <div>
          <h1 className="text-3xl font-bold">Procurement policy profiles</h1>
          <p className="mt-1 text-muted-foreground">Govern DEC-001 through DEC-014 as versioned, tenant-scoped policy configuration.</p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => profiles.refetch()} disabled={profiles.isFetching}><RefreshCw className={`mr-2 h-4 w-4 ${profiles.isFetching ? 'animate-spin' : ''}`} />Refresh</Button>
          <Button onClick={() => setCreateOpen(true)}><Plus className="mr-2 h-4 w-4" />New draft</Button>
        </div>
      </div>

      <div className="flex max-w-xs flex-col gap-2">
        <Label>Status filter</Label>
        <Select value={status} onValueChange={next => setStatus(next as ProcurementConfigurationProfileStatus | 'All')}>
          <SelectTrigger><SelectValue /></SelectTrigger>
          <SelectContent><SelectItem value="All">All statuses</SelectItem><SelectItem value="Draft">Draft</SelectItem><SelectItem value="Published">Published</SelectItem><SelectItem value="Retired">Retired</SelectItem></SelectContent>
        </Select>
      </div>

      <DataTable
        compact
        title="Version history"
        description="Every policy family is displayed newest-first; published and retired versions remain read-only."
        data={profiles.data?.items ?? []}
        columns={columns}
        loading={profiles.isLoading}
        error={profiles.error ? 'Failed to load policy profiles.' : null}
        enableSearch
        enablePagination
        pageSize={20}
        emptyStateMessage="No procurement policy profiles exist for this tenant."
        onRowDoubleClick={row => router.push(`/administration/procurement/policy-profiles/${row.original.id}`)}
        rowActions={[{ id: 'open', label: 'Open details', icon: Eye, onClick: row => router.push(`/administration/procurement/policy-profiles/${row.original.id}`) }]}
      />

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="sm:max-w-2xl">
          <DialogHeader><DialogTitle>Create procurement policy draft</DialogTitle><DialogDescription>A version-one draft is seeded with the registered DEC-001 through DEC-014 controls.</DialogDescription></DialogHeader>
          <div className="grid gap-4 py-2 sm:grid-cols-2">
            <div className="space-y-2"><Label>Profile code</Label><Input value={form.profileCode} onChange={event => setForm(current => ({ ...current, profileCode: event.target.value }))} /></div>
            <div className="space-y-2"><Label>Name</Label><Input value={form.name} onChange={event => setForm(current => ({ ...current, name: event.target.value }))} /></div>
            <div className="space-y-2"><Label>Effective from</Label><Input type="date" value={form.effectiveFrom} onChange={event => setForm(current => ({ ...current, effectiveFrom: event.target.value }))} /></div>
            <div className="space-y-2"><Label>Effective to</Label><Input type="date" value={form.effectiveTo ?? ''} onChange={event => setForm(current => ({ ...current, effectiveTo: event.target.value }))} /></div>
            <div className="space-y-2 sm:col-span-2"><Label>Change summary</Label><Textarea value={form.changeSummary ?? ''} onChange={event => setForm(current => ({ ...current, changeSummary: event.target.value }))} /></div>
            <div className="flex items-center gap-3 sm:col-span-2"><Switch checked={form.isDefault} onCheckedChange={next => setForm(current => ({ ...current, isDefault: next }))} /><span className="text-sm">Default policy family for this tenant</span></div>
          </div>
          <DialogFooter><Button variant="outline" onClick={() => setCreateOpen(false)}>Cancel</Button><Button onClick={() => create.mutate()} disabled={create.isPending || !form.profileCode.trim() || !form.name.trim() || !form.effectiveFrom}>{create.isPending ? 'Creating…' : 'Create draft'}</Button></DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
