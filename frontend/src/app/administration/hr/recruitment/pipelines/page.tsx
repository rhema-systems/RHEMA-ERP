'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Copy, Loader2, Plus, Workflow } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
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
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { recruitmentPipelineService } from '@/services/hr/recruitment-pipeline.service';
import type { RecruitmentPipelineSummary } from '@/types/hr/recruitment-pipeline';

/**
 * Recruitment pipelines — the stage definitions every application progresses through.
 *
 * Setup, so it lives under Administration. A pipeline's stages carry the transition rules the
 * server enforces on every move (order, repeat, attempt limits), which is why **Duplicate** is
 * offered as prominently as edit: changing a pipeline that vacancies are already running against
 * changes the rules mid-flight, and cloning gives you somewhere safe to work.
 */
export default function RecruitmentPipelinesPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { hasAnyRole } = useAuth();
  const isHr = hasAnyRole(['SuperAdmin', 'HR']);

  const [creating, setCreating] = useState(false);
  const [cloning, setCloning] = useState<RecruitmentPipelineSummary | null>(null);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [isDefault, setIsDefault] = useState(false);

  const { data, isLoading, isError } = useQuery({
    queryKey: ['hr', 'recruitment-pipelines'],
    queryFn: () => recruitmentPipelineService.getAll(),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'recruitment-pipelines'] });

  const create = useMutation({
    mutationFn: () =>
      recruitmentPipelineService.create({
        name: name.trim(),
        description: description.trim() || null,
        isDefault,
        isActive: true,
        defaultTimeToCompleteDays: null,
        // Stages are added on the detail page — a pipeline with no stages is legal and inert.
        stages: [],
      }),
    onSuccess: async () => {
      await refresh();
      setCreating(false);
      resetForm();
      toast({ title: 'Pipeline created', description: 'Add its stages next.' });
    },
    onError: (e: any) => toast({ title: 'Could not create', description: e?.message, variant: 'destructive' }),
  });

  const clone = useMutation({
    mutationFn: () =>
      cloning
        ? recruitmentPipelineService.clone(cloning.id, name.trim())
        : Promise.reject(new Error('No pipeline selected.')),
    onSuccess: async () => {
      await refresh();
      setCloning(null);
      resetForm();
      toast({ title: 'Pipeline duplicated', description: 'All its stages were copied.' });
    },
    onError: (e: any) => toast({ title: 'Could not duplicate', description: e?.message, variant: 'destructive' }),
  });

  const resetForm = () => {
    setName('');
    setDescription('');
    setIsDefault(false);
  };

  const rows = data ?? [];

  return (
    <div className="space-y-6">
      <PageHeader
        title="Recruitment pipelines"
        description="The stages applications move through, and the rules governing those moves."
        backHref="/administration/hr"
        actions={
          isHr ? (
            <Button onClick={() => { resetForm(); setCreating(true); }}>
              <Plus className="mr-2 h-4 w-4" />
              New pipeline
            </Button>
          ) : undefined
        }
      />

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : isError ? (
            <EmptyState title="Could not load pipelines" description="Try again in a moment." />
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Workflow}
              title="No pipelines yet"
              description="A vacancy needs a pipeline before applications can be moved through stages."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Description</TableHead>
                  <TableHead className="w-24 text-right">Stages</TableHead>
                  <TableHead className="w-28">Default</TableHead>
                  <TableHead className="w-28">Status</TableHead>
                  <TableHead className="w-36 text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((p) => (
                  <TableRow key={p.id}>
                    <TableCell className="font-medium">
                      <Link
                        href={`/administration/hr/recruitment/pipelines/${p.id}`}
                        className="hover:underline"
                      >
                        {p.name}
                      </Link>
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">{p.description ?? '—'}</TableCell>
                    <TableCell className="text-right tabular-nums">{p.stageCount}</TableCell>
                    <TableCell>{p.isDefault ? <StatusBadge status="Default" /> : '—'}</TableCell>
                    <TableCell>
                      <StatusBadge active={p.isActive} />
                    </TableCell>
                    <TableCell className="text-right">
                      {isHr && (
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => {
                            setCloning(p);
                            setName(`${p.name} (copy)`);
                          }}
                        >
                          <Copy className="mr-2 h-3.5 w-3.5" />
                          Duplicate
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Dialog open={creating} onOpenChange={(o) => !o && setCreating(false)}>
        <DialogContent className="sm:max-w-[520px]">
          <DialogHeader>
            <DialogTitle>New pipeline</DialogTitle>
            <DialogDescription>
              Create the pipeline, then add its stages. Marking it default makes it the one new
              vacancies start with.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-1.5">
              <Label>Name</Label>
              <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="Standard hiring pipeline" />
            </div>
            <div className="space-y-1.5">
              <Label>Description</Label>
              <Textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={3} />
            </div>
            <div className="flex items-center justify-between rounded-md border p-3">
              <div>
                <Label>Default pipeline</Label>
                <p className="text-xs text-muted-foreground">Used when a vacancy does not pick one.</p>
              </div>
              <Switch checked={isDefault} onCheckedChange={setIsDefault} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreating(false)}>
              Cancel
            </Button>
            <Button disabled={!name.trim() || create.isPending} onClick={() => create.mutate()}>
              {create.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Create
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!cloning} onOpenChange={(o) => !o && setCloning(null)}>
        <DialogContent className="sm:max-w-[480px]">
          <DialogHeader>
            <DialogTitle>Duplicate &ldquo;{cloning?.name}&rdquo;</DialogTitle>
            <DialogDescription>
              Copies the pipeline and all {cloning?.stageCount ?? 0} of its stages. The copy is
              independent — editing it leaves in-flight applications on the original untouched.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-1.5">
            <Label>New name</Label>
            <Input value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCloning(null)}>
              Cancel
            </Button>
            <Button disabled={!name.trim() || clone.isPending} onClick={() => clone.mutate()}>
              {clone.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Duplicate
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
