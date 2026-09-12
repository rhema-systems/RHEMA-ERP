'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Compass, Plus, Trash2 } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { companyGoalService } from '@/services/hr/goals.service';
import { performanceLinkService } from '@/services/hr/performance-links.service';

interface CheckInObjectivesPanelProps {
  checkInId: string;
  /** The cycle whose company objectives are on offer. */
  cycleId?: string | null;
  readOnly?: boolean;
}

/**
 * Which yearly objectives a check-in was actually about.
 *
 * Recording this is what lets a year of one-to-ones be read back against the strategy they were
 * meant to serve — otherwise the link between "we talked every fortnight" and "this is what the
 * company was trying to do" exists only in people's heads.
 *
 * ⚠ Replace-set, as with required skills: each save sends the whole list, so the panel edits a
 * local copy and commits it entire.
 */
export function CheckInObjectivesPanel({
  checkInId,
  cycleId,
  readOnly = false,
}: CheckInObjectivesPanelProps) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [selected, setSelected] = useState<string[]>([]);
  const [dirty, setDirty] = useState(false);
  const [adding, setAdding] = useState('');

  const { data: linked, isLoading } = useQuery({
    queryKey: ['hr', 'check-in-objectives', checkInId],
    queryFn: () => performanceLinkService.getCheckInObjectives(checkInId),
    enabled: !!checkInId,
  });

  const { data: companyGoals } = useQuery({
    queryKey: ['hr', 'company-goals', cycleId],
    queryFn: () => companyGoalService.getByCycle(cycleId ?? ''),
    enabled: !!cycleId,
  });

  useEffect(() => {
    if (!linked || dirty) return;
    setSelected(linked.map((l) => l.companyGoalId));
  }, [linked, dirty]);

  const save = useMutation({
    mutationFn: () => performanceLinkService.setCheckInObjectives(checkInId, selected),
    onSuccess: () => {
      setDirty(false);
      queryClient.invalidateQueries({ queryKey: ['hr', 'check-in-objectives', checkInId] });
      toast({ title: 'Objectives linked' });
    },
    onError: (err: unknown) =>
      toast({
        variant: 'destructive',
        title: 'Could not save the linked objectives',
        description: (err as Error)?.message ?? 'Please try again.',
      }),
  });

  const titleFor = (id: string) =>
    companyGoals?.find((g) => g.id === id)?.title ??
    linked?.find((l) => l.companyGoalId === id)?.objectiveTitle ??
    'Unknown objective';

  const available = (companyGoals ?? []).filter((g) => !selected.includes(g.id));

  if (isLoading) return <Skeleton className="h-40 w-full" />;

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-4">
        <CardTitle className="text-base">Objectives this was about</CardTitle>
        {dirty && !readOnly && (
          <Button size="sm" onClick={() => save.mutate()} disabled={save.isPending}>
            {save.isPending ? 'Saving…' : 'Save changes'}
          </Button>
        )}
      </CardHeader>
      <CardContent className="space-y-4">
        {selected.length === 0 ? (
          <EmptyState
            icon={Compass}
            title="No objectives linked"
            description="Link the company objectives this conversation served, so the year reads back against the strategy."
          />
        ) : (
          <ul className="space-y-2">
            {selected.map((id) => (
              <li key={id} className="flex items-center justify-between gap-2 rounded-lg border p-3">
                <span className="text-sm">{titleFor(id)}</span>
                {!readOnly && (
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={`Unlink ${titleFor(id)}`}
                    onClick={() => {
                      setSelected((prev) => prev.filter((x) => x !== id));
                      setDirty(true);
                    }}
                  >
                    <Trash2 className="h-4 w-4" />
                  </Button>
                )}
              </li>
            ))}
          </ul>
        )}

        {!readOnly && cycleId && (
          <div className="flex items-end gap-2">
            <div className="flex-1 space-y-2">
              <Label htmlFor="add-objective">Link an objective</Label>
              <Select value={adding} onValueChange={setAdding}>
                <SelectTrigger id="add-objective">
                  <SelectValue placeholder={available.length ? 'Choose…' : 'All linked'} />
                </SelectTrigger>
                <SelectContent>
                  {available.map((g) => (
                    <SelectItem key={g.id} value={g.id}>
                      {g.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <Button
              variant="outline"
              disabled={!adding}
              onClick={() => {
                setSelected((prev) => [...prev, adding]);
                setAdding('');
                setDirty(true);
              }}
            >
              <Plus className="mr-2 h-4 w-4" />
              Link
            </Button>
          </div>
        )}

        {dirty && !readOnly && (
          <Alert>
            <AlertDescription>
              Unsaved changes. Saving replaces the whole list.
            </AlertDescription>
          </Alert>
        )}
      </CardContent>
    </Card>
  );
}
