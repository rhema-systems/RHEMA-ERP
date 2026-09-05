'use client';

import { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { PipelineStagesPanel } from '@/components/hr/recruitment/PipelineStagesPanel';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { recruitmentPipelineService } from '@/services/hr/recruitment-pipeline.service';

export default function RecruitmentPipelineDetailPage() {
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { hasAnyRole } = useAuth();
  const isHr = hasAnyRole(['SuperAdmin', 'HR']);

  const { data, isLoading, isError } = useQuery({
    queryKey: ['hr', 'recruitment-pipeline', id],
    queryFn: () => recruitmentPipelineService.getById(id),
    enabled: !!id,
  });

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [isDefault, setIsDefault] = useState(false);
  const [isActive, setIsActive] = useState(true);
  const [targetDays, setTargetDays] = useState('');

  useEffect(() => {
    if (!data) return;
    setName(data.name);
    setDescription(data.description ?? '');
    setIsDefault(data.isDefault);
    setIsActive(data.isActive);
    setTargetDays(data.defaultTimeToCompleteDays?.toString() ?? '');
  }, [data]);

  const save = useMutation({
    mutationFn: () =>
      recruitmentPipelineService.update(id, {
        id,
        name: name.trim(),
        description: description.trim() || null,
        isDefault,
        isActive,
        defaultTimeToCompleteDays: targetDays === '' ? null : Number(targetDays),
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'recruitment-pipeline', id] });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'recruitment-pipelines'] });
      toast({ title: 'Pipeline saved' });
    },
    onError: (e: any) => toast({ title: 'Could not save', description: e?.message, variant: 'destructive' }),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !data) {
    return <EmptyState title="Pipeline not found" description="It may have been removed." />;
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={data.name}
        description="Stages are applied in order; the rules on each one are enforced on every application move."
        backHref="/administration/hr/recruitment/pipelines"
        actions={
          isHr ? (
            <Button onClick={() => save.mutate()} disabled={!name.trim() || save.isPending}>
              {save.isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Save className="mr-2 h-4 w-4" />
              )}
              Save
            </Button>
          ) : undefined
        }
      />

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Pipeline</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-2">
          <div className="space-y-1.5">
            <Label>Name</Label>
            <Input value={name} onChange={(e) => setName(e.target.value)} disabled={!isHr} />
          </div>
          <div className="space-y-1.5">
            <Label>Target days to complete</Label>
            <Input
              type="number"
              value={targetDays}
              onChange={(e) => setTargetDays(e.target.value)}
              disabled={!isHr}
              placeholder="Optional"
            />
          </div>
          <div className="space-y-1.5 md:col-span-2">
            <Label>Description</Label>
            <Textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={2}
              disabled={!isHr}
            />
          </div>
          <div className="flex items-center justify-between rounded-md border p-3">
            <div>
              <Label>Default pipeline</Label>
              <p className="text-xs text-muted-foreground">Used when a vacancy does not pick one.</p>
            </div>
            <Switch checked={isDefault} onCheckedChange={setIsDefault} disabled={!isHr} />
          </div>
          <div className="flex items-center justify-between rounded-md border p-3">
            <div>
              <Label>Active</Label>
              <p className="text-xs text-muted-foreground">
                Inactive pipelines stay attached to existing vacancies.
              </p>
            </div>
            <Switch checked={isActive} onCheckedChange={setIsActive} disabled={!isHr} />
          </div>
        </CardContent>
      </Card>

      <div className="space-y-3">
        <h2 className="text-lg font-semibold">Stages</h2>
        <PipelineStagesPanel pipelineId={id} canEdit={isHr} />
      </div>
    </div>
  );
}
