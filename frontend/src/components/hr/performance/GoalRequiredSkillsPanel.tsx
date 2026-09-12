'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Lightbulb, Plus, Sparkles, Trash2 } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { appraisalCriteriaService } from '@/services/hr/appraisal.service';
import { performanceLinkService } from '@/services/hr/performance-links.service';
import type { SetGoalRequiredSkill } from '@/types/hr/performance-links';

interface GoalRequiredSkillsPanelProps {
  goalId: string;
  /** A locked or closed goal is read-only, matching every other write on it. */
  readOnly?: boolean;
}

/**
 * The competencies a goal actually requires, and which of those are gaps.
 *
 * **The "development needed" flag is the working part.** Anything ticked here surfaces on the
 * employee's development plan as a suggestion, with the goals that asked for it — so a development
 * plan is assembled from what this year's goals demand rather than from a blank page. A skill
 * listed but not ticked records that the goal needs it and the employee already has it.
 *
 * ⚠ The API is **replace-set**: every save sends the whole list. The panel therefore edits a local
 * copy and saves it entire, which is also why removing a row is not itself a request.
 */
export function GoalRequiredSkillsPanel({ goalId, readOnly = false }: GoalRequiredSkillsPanelProps) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [rows, setRows] = useState<SetGoalRequiredSkill[]>([]);
  const [dirty, setDirty] = useState(false);
  const [adding, setAdding] = useState('');

  const { data: saved, isLoading } = useQuery({
    queryKey: ['hr', 'goal-required-skills', goalId],
    queryFn: () => performanceLinkService.getGoalRequiredSkills(goalId),
    enabled: !!goalId,
  });

  const { data: competencies } = useQuery({
    queryKey: ['hr', 'appraisal-competencies'],
    queryFn: () => appraisalCriteriaService.getAll(),
    staleTime: 5 * 60 * 1000,
  });

  useEffect(() => {
    if (!saved || dirty) return;
    setRows(
      saved.map((s) => ({
        competencyId: s.competencyId,
        developmentNeeded: s.developmentNeeded,
        note: s.note ?? null,
      })),
    );
  }, [saved, dirty]);

  const save = useMutation({
    mutationFn: () => performanceLinkService.setGoalRequiredSkills(goalId, rows),
    onSuccess: () => {
      setDirty(false);
      queryClient.invalidateQueries({ queryKey: ['hr', 'goal-required-skills', goalId] });
      // The development-plan suggestions are derived from these, so they are now stale.
      queryClient.invalidateQueries({ queryKey: ['hr', 'skill-suggestions'] });
      toast({ title: 'Required skills saved' });
    },
    onError: (err: unknown) =>
      toast({
        variant: 'destructive',
        title: 'Could not save the required skills',
        description: (err as Error)?.message ?? 'Please try again.',
      }),
  });

  const nameFor = (id: string) =>
    competencies?.find((c) => c.id === id)?.criteriaName ??
    saved?.find((s) => s.competencyId === id)?.competencyName ??
    'Unknown competency';

  const available = (competencies ?? []).filter(
    (c) => !rows.some((r) => r.competencyId === c.id),
  );

  const add = () => {
    if (!adding) return;
    setRows((prev) => [...prev, { competencyId: adding, developmentNeeded: true, note: null }]);
    setAdding('');
    setDirty(true);
  };

  const patch = (competencyId: string, changes: Partial<SetGoalRequiredSkill>) => {
    setRows((prev) =>
      prev.map((r) => (r.competencyId === competencyId ? { ...r, ...changes } : r)),
    );
    setDirty(true);
  };

  const remove = (competencyId: string) => {
    setRows((prev) => prev.filter((r) => r.competencyId !== competencyId));
    setDirty(true);
  };

  if (isLoading) return <Skeleton className="h-40 w-full" />;

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-4">
        <CardTitle className="text-base">Skills this goal needs</CardTitle>
        {dirty && !readOnly && (
          <Button size="sm" onClick={() => save.mutate()} disabled={save.isPending}>
            {save.isPending ? 'Saving…' : 'Save changes'}
          </Button>
        )}
      </CardHeader>
      <CardContent className="space-y-4">
        {rows.length === 0 ? (
          <EmptyState
            icon={Sparkles}
            title="No skills recorded"
            description="Naming the competencies a goal needs is what turns the development plan into something built from real work."
          />
        ) : (
          <div className="space-y-3">
            {rows.map((row) => (
              <div key={row.competencyId} className="space-y-2 rounded-lg border p-3">
                <div className="flex items-start justify-between gap-2">
                  <p className="font-medium">{nameFor(row.competencyId)}</p>
                  {!readOnly && (
                    <Button
                      variant="ghost"
                      size="icon"
                      aria-label={`Remove ${nameFor(row.competencyId)}`}
                      onClick={() => remove(row.competencyId)}
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  )}
                </div>

                <div className="flex items-center gap-2">
                  <Checkbox
                    id={`dev-${row.competencyId}`}
                    checked={row.developmentNeeded}
                    disabled={readOnly}
                    onCheckedChange={(v) =>
                      patch(row.competencyId, { developmentNeeded: v === true })
                    }
                  />
                  <Label htmlFor={`dev-${row.competencyId}`} className="text-sm font-normal">
                    Development needed — suggest this on the development plan
                  </Label>
                </div>

                <Input
                  aria-label={`Note for ${nameFor(row.competencyId)}`}
                  placeholder="Why this goal needs it (optional)"
                  disabled={readOnly}
                  value={row.note ?? ''}
                  onChange={(e) => patch(row.competencyId, { note: e.target.value || null })}
                />
              </div>
            ))}
          </div>
        )}

        {!readOnly && (
          <div className="flex items-end gap-2">
            <div className="flex-1 space-y-2">
              <Label htmlFor="add-competency">Add a competency</Label>
              <Select value={adding} onValueChange={setAdding}>
                <SelectTrigger id="add-competency">
                  <SelectValue placeholder={available.length ? 'Choose…' : 'All added'} />
                </SelectTrigger>
                <SelectContent>
                  {available.map((c) => (
                    <SelectItem key={c.id} value={c.id}>
                      {c.criteriaName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <Button variant="outline" onClick={add} disabled={!adding}>
              <Plus className="mr-2 h-4 w-4" />
              Add
            </Button>
          </div>
        )}

        {dirty && !readOnly && (
          <Alert>
            <Lightbulb className="h-4 w-4" />
            <AlertDescription>
              Unsaved changes. Saving replaces the whole list — anything removed here is unlinked.
            </AlertDescription>
          </Alert>
        )}
      </CardContent>
    </Card>
  );
}
