'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { Eye, Plus, RefreshCw } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { civilEngineeringConfigurationService } from '@/services/civil-engineering-configuration.service';
import type {
  CreateCivilProfileRequest,
  CivilProfileStatus,
  CivilProfileSummary,
} from '@/types/civil-engineering-configuration';

type Cell<T> = { row: { original: T } };

export default function CivilEngineeringConfigurationPage() {
  const router = useRouter();
  const client = useQueryClient();
  const { toast } = useToast();
  const { hasPermission } = useAuth();
  const canManage = hasPermission('civil-engineering.configuration.manage');
  const [status, setStatus] = useState<CivilProfileStatus | 'All'>('All');
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState<CreateCivilProfileRequest>({
    name: 'TDC Civil Engineering Configuration',
    effectiveFrom: new Date().toISOString().slice(0, 10),
    isDefault: true,
  });
  const profiles = useQuery({
    queryKey: ['civil-engineering-configuration-profiles', status],
    queryFn: () =>
      civilEngineeringConfigurationService.list({
        status: status === 'All' ? undefined : status,
        pageSize: 100,
      }),
  });
  const create = useMutation({
    mutationFn: () =>
      civilEngineeringConfigurationService.create({
        ...form,
        name: form.name.trim(),
        effectiveTo: form.effectiveTo || undefined,
        changeSummary: form.changeSummary?.trim() || undefined,
      }),
    onSuccess: async (result) => {
      setOpen(false);
      await client.invalidateQueries({
        queryKey: ['civil-engineering-configuration-profiles'],
      });
      toast({
        title: 'Civil Engineering draft created',
        description: 'All 13 controlled decisions were initialized.',
        variant: 'success',
      });
      router.push(
        `/administration/project-management/civil-engineering-config/${result.id}`
      );
    },
    onError: (error: Error) =>
      toast({
        title: 'Unable to create draft',
        description: error.message,
        variant: 'destructive',
      }),
  });
  const columns = useMemo<Array<DataTableColumn<CivilProfileSummary>>>(
    () => [
      {
        id: 'profile',
        header: 'Configuration / version',
        accessorKey: 'name',
        cell: ({ row }: Cell<CivilProfileSummary>) => (
          <button
            className="text-left"
            onClick={() =>
              router.push(
                `/administration/project-management/civil-engineering-config/${row.original.id}`
              )
            }
          >
            <span className="block font-medium text-primary hover:underline">
              {row.original.name}
            </span>
            <span className="text-xs text-muted-foreground">
              {row.original.profileCode} · v{row.original.version}
            </span>
          </button>
        ),
      },
      {
        id: 'status',
        header: 'Lifecycle',
        accessorKey: 'lifecycleStatus',
        cell: ({ row }: Cell<CivilProfileSummary>) => (
          <Badge
            variant={
              row.original.lifecycleStatus === 'Published'
                ? 'default'
                : 'secondary'
            }
          >
            {row.original.lifecycleStatus}
          </Badge>
        ),
      },
      {
        id: 'period',
        header: 'Effective period',
        cell: ({ row }: Cell<CivilProfileSummary>) =>
          `${new Date(row.original.effectiveFrom).toLocaleDateString()} – ${row.original.effectiveTo ? new Date(row.original.effectiveTo).toLocaleDateString() : 'Open-ended'}`,
      },
      {
        id: 'readiness',
        header: 'Decision readiness',
        cell: ({ row }: Cell<CivilProfileSummary>) => (
          <div>
            <span className="font-medium">
              {row.original.completeDecisionCount}/
              {row.original.totalDecisionCount}
            </span>
            <span className="block text-xs text-muted-foreground">
              {row.original.isComplete
                ? 'Ready to validate'
                : 'Action required'}
            </span>
          </div>
        ),
      },
      {
        id: 'updated',
        header: 'Last activity',
        cell: ({ row }: Cell<CivilProfileSummary>) =>
          new Date(row.original.updatedAt).toLocaleString(),
      },
    ],
    [router]
  );
  return (
    <div className="space-y-6">
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-start">
        <div>
          <h1 className="text-3xl font-bold">
            Civil engineering configuration
          </h1>
          <p className="mt-1 text-muted-foreground">
            Versioned, effective-dated Civil Engineering controls.
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => profiles.refetch()}>
            <RefreshCw
              className={`mr-2 h-4 w-4 ${profiles.isFetching ? 'animate-spin' : ''}`}
            />
            Refresh
          </Button>
          {canManage && (
            <Button onClick={() => setOpen(true)}>
              <Plus className="mr-2 h-4 w-4" />
              New draft
            </Button>
          )}
        </div>
      </div>
      <div className="max-w-xs space-y-2">
        <Label>Lifecycle</Label>
        <Select
          value={status}
          onValueChange={(next) =>
            setStatus(next as CivilProfileStatus | 'All')
          }
        >
          <SelectTrigger>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {['All', 'Draft', 'Published', 'Retired'].map((item) => (
              <SelectItem key={item} value={item}>
                {item === 'All' ? 'All statuses' : item}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <DataTable
        compact
        title="Civil Engineering configuration history"
        description="Governed profile versions and their publication readiness."
        data={profiles.data?.items ?? []}
        columns={columns}
        loading={profiles.isLoading}
        error={
          profiles.error
            ? 'Failed to load Civil Engineering configuration profiles.'
            : null
        }
        enableSearch
        enablePagination
        pageSize={20}
        emptyStateMessage="No Civil Engineering configuration profile exists for this tenant."
        onRowDoubleClick={(row) =>
          router.push(
            `/administration/project-management/civil-engineering-config/${row.original.id}`
          )
        }
        rowActions={[
          {
            id: 'open',
            label: 'Open details',
            icon: Eye,
            onClick: (row) =>
              router.push(
                `/administration/project-management/civil-engineering-config/${row.original.id}`
              ),
          },
        ]}
      />
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              Create Civil Engineering configuration draft
            </DialogTitle>
            <DialogDescription>
              The profile code is governed as TDC-CIVIL-ENGINEERING and cannot
              be entered manually.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2 sm:col-span-2">
              <Label>Name</Label>
              <Input
                value={form.name}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    name: event.target.value,
                  }))
                }
              />
            </div>
            <div className="space-y-2">
              <Label>Effective from</Label>
              <Input
                type="date"
                value={form.effectiveFrom}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    effectiveFrom: event.target.value,
                  }))
                }
              />
            </div>
            <div className="space-y-2">
              <Label>Effective to (optional)</Label>
              <Input
                type="date"
                value={form.effectiveTo ?? ''}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    effectiveTo: event.target.value || undefined,
                  }))
                }
              />
            </div>
            <div className="space-y-2 sm:col-span-2">
              <Label>Change summary</Label>
              <Textarea
                value={form.changeSummary ?? ''}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    changeSummary: event.target.value,
                  }))
                }
              />
            </div>
            <div className="flex items-center gap-3 sm:col-span-2">
              <Switch
                checked={form.isDefault}
                onCheckedChange={(next) =>
                  setForm((current) => ({ ...current, isDefault: next }))
                }
              />
              <span className="text-sm">
                Default Civil Engineering configuration family
              </span>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => create.mutate()}
              disabled={
                create.isPending || !form.name.trim() || !form.effectiveFrom
              }
            >
              {create.isPending ? 'Creating…' : 'Create draft'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
