'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { FileText, Loader2, Pencil, Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { successionService } from '@/services/hr/succession.service';
import type { CompetencyLookup, SuccessionCompetencyRequirement } from '@/types/hr/succession';

const spaced = (v?: string | null) => (v ? v.replace(/([a-z])([A-Z0-9])/g, '$1 $2') : '—');

/**
 * What the post demands of whoever fills it — the standard a candidate is measured against.
 *
 * ⚠ **The scale maximum travels with the row.** `requiredLevel` is meaningless without it and it
 * is not always 5: each competency in the catalogue carries its own `proficiencyScaleMax`, so the
 * level input is bounded by the competency the user picked, not by a constant.
 *
 * ⚠ **Removing a requirement is a soft delete, and re-requiring the same competency revives that
 * row rather than inserting a second one** — the unique index over (plan, competency) counts
 * deleted rows, so an insert would have hit it and 500'd naming nothing. The level typed on the
 * re-add wins. The panel does not need to know which happened; it is recorded here because the
 * two paths return the same shape and only the `createdAt` distinguishes them.
 */
export function PlanCompetencyRequirementsPanel({
  planId,
  canWrite,
  canDelete,
}: {
  planId: string;
  canWrite: boolean;
  canDelete: boolean;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const queryKey = ['succession-plans', planId, 'competency-requirements'];
  const { data: requirements, isLoading } = useQuery({
    queryKey,
    queryFn: () => successionService.getCompetencyRequirements(planId),
    enabled: !!planId,
  });

  const { data: catalogue } = useQuery({
    queryKey: ['succession-competency-lookup'],
    queryFn: () => successionService.getCompetencyLookup(),
    staleTime: 5 * 60 * 1000,
  });

  const [adding, setAdding] = useState(false);
  const [editing, setEditing] = useState<SuccessionCompetencyRequirement | null>(null);
  const [pendingDelete, setPendingDelete] = useState<SuccessionCompetencyRequirement | null>(null);
  const [competencyId, setCompetencyId] = useState('');
  const [requiredLevel, setRequiredLevel] = useState('3');

  const rows = requirements ?? [];
  const taken = useMemo(() => new Set(rows.map((r) => r.competencyId)), [rows]);

  /** Only what the plan does not already require — re-adding a live one is refused server-side. */
  const selectable = (catalogue ?? []).filter((c) => !taken.has(c.id));
  const chosen: CompetencyLookup | undefined = (catalogue ?? []).find((c) => c.id === competencyId);
  const scaleMax = editing ? editing.proficiencyScaleMax : (chosen?.proficiencyScaleMax ?? 5);

  const levelValue = Number(requiredLevel);
  const levelValid = Number.isInteger(levelValue) && levelValue >= 1 && levelValue <= scaleMax;

  const fail = (title: string) => (e: any) =>
    toast({
      title,
      description:
        e?.response?.data?.message ?? e?.response?.data ?? e?.message ?? 'Please try again.',
      variant: 'destructive',
    });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey });
    // The plan's own detail read nests this collection and drives the tab count.
    await queryClient.invalidateQueries({ queryKey: ['succession-plans', planId] });
  };

  const add = useMutation({
    mutationFn: () =>
      successionService.addCompetencyRequirement(planId, {
        competencyId,
        requiredLevel: levelValue,
      }),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Competency required' });
      setAdding(false);
    },
    onError: fail('Could not add the requirement'),
  });

  const update = useMutation({
    mutationFn: ({ id }: { id: string }) =>
      successionService.updateCompetencyRequirement(id, {
        id,
        requiredLevel: levelValue,
      }),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Required level updated' });
      setEditing(null);
    },
    onError: fail('Could not update the requirement'),
  });

  const remove = useMutation({
    mutationFn: (row: SuccessionCompetencyRequirement) =>
      successionService.removeCompetencyRequirement(row.id),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Requirement removed' });
      setPendingDelete(null);
    },
    onError: fail('Could not remove the requirement'),
  });

  const openAdd = () => {
    setCompetencyId('');
    setRequiredLevel('3');
    setAdding(true);
  };

  const openEdit = (row: SuccessionCompetencyRequirement) => {
    setRequiredLevel(String(row.requiredLevel));
    setEditing(row);
  };

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0">
        <CardTitle className="text-base">Competency requirements</CardTitle>
        {canWrite && (
          <Button size="sm" onClick={openAdd} disabled={!catalogue || selectable.length === 0}>
            <Plus className="mr-2 h-4 w-4" />
            Require a competency
          </Button>
        )}
      </CardHeader>
      <CardContent className="p-0">
        {isLoading ? (
          <div className="flex justify-center py-10">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : rows.length === 0 ? (
          <div className="px-6 pb-6">
            <EmptyState
              icon={FileText}
              title="No competency requirements"
              description={
                catalogue && catalogue.length === 0
                  ? 'The competency catalogue is empty, so there is nothing to require against yet.'
                  : 'Nothing is required of whoever fills this post. Candidate gap analysis has nothing to measure until something is.'
              }
            />
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Competency</TableHead>
                <TableHead>Category</TableHead>
                <TableHead className="text-right">Required level</TableHead>
                <TableHead className="w-24 text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((r) => (
                <TableRow key={r.id}>
                  <TableCell>
                    <div className="font-medium">{r.competencyName}</div>
                    <div className="text-xs text-muted-foreground">{r.competencyCode}</div>
                  </TableCell>
                  <TableCell>
                    <Badge variant="outline">{spaced(r.competencyCategory)}</Badge>
                  </TableCell>
                  <TableCell className="text-right">
                    {/* The scale max travels with the row — never assume it is 5. */}
                    {r.requiredLevel} / {r.proficiencyScaleMax}
                  </TableCell>
                  <TableCell className="text-right">
                    <div className="flex justify-end gap-1">
                      {canWrite && (
                        <Button variant="ghost" size="sm" onClick={() => openEdit(r)}>
                          <Pencil className="h-4 w-4" />
                          <span className="sr-only">Edit</span>
                        </Button>
                      )}
                      {canDelete && (
                        <Button
                          variant="ghost"
                          size="sm"
                          className="text-red-600"
                          onClick={() => setPendingDelete(r)}
                        >
                          <Trash2 className="h-4 w-4" />
                          <span className="sr-only">Remove</span>
                        </Button>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>

      <Dialog open={adding} onOpenChange={setAdding}>
        <DialogContent className="sm:max-w-[520px]">
          <DialogHeader>
            <DialogTitle>Require a competency</DialogTitle>
            <DialogDescription>
              The standard whoever fills this post has to meet. Candidate gaps are measured against
              it.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="cr-competency">
                Competency<span className="ml-0.5 text-red-500">*</span>
              </Label>
              <Select value={competencyId} onValueChange={setCompetencyId}>
                <SelectTrigger id="cr-competency">
                  <SelectValue placeholder="Pick from the catalogue" />
                </SelectTrigger>
                <SelectContent>
                  {selectable.map((c) => (
                    <SelectItem key={c.id} value={c.id}>
                      {c.name} · {c.code}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                Competencies the plan already requires are not offered — edit the row instead.
              </p>
            </div>

            <div className="space-y-2">
              <Label htmlFor="cr-level">
                Required level<span className="ml-0.5 text-red-500">*</span>
              </Label>
              <Input
                id="cr-level"
                type="number"
                min={1}
                max={scaleMax}
                value={requiredLevel}
                onChange={(e) => setRequiredLevel(e.target.value)}
              />
              <p className={`text-xs ${levelValid ? 'text-muted-foreground' : 'text-red-500'}`}>
                {chosen
                  ? `1 to ${scaleMax} on this competency's own scale.`
                  : 'Pick a competency first — each one carries its own scale.'}
              </p>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setAdding(false)} disabled={add.isPending}>
              Cancel
            </Button>
            <Button
              onClick={() => add.mutate()}
              disabled={!competencyId || !levelValid || add.isPending}
            >
              {add.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Require it
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={editing !== null} onOpenChange={(o) => !o && setEditing(null)}>
        <DialogContent className="sm:max-w-[420px]">
          <DialogHeader>
            <DialogTitle>{editing?.competencyName}</DialogTitle>
            <DialogDescription>
              Only the required level is editable — a different competency is a different
              requirement.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-2">
            <Label htmlFor="cr-edit-level">Required level</Label>
            <Input
              id="cr-edit-level"
              type="number"
              min={1}
              max={scaleMax}
              value={requiredLevel}
              onChange={(e) => setRequiredLevel(e.target.value)}
            />
            <p className={`text-xs ${levelValid ? 'text-muted-foreground' : 'text-red-500'}`}>
              1 to {scaleMax} on this competency&apos;s own scale.
            </p>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setEditing(null)} disabled={update.isPending}>
              Cancel
            </Button>
            <Button
              onClick={() => editing && update.mutate({ id: editing.id })}
              disabled={!levelValid || update.isPending}
            >
              {update.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={pendingDelete !== null}
        onOpenChange={(o) => !o && setPendingDelete(null)}
        title="Remove this requirement?"
        description={`"${pendingDelete?.competencyName ?? ''}" will no longer be required of this post. It can be required again later.`}
        confirmText="Remove"
        variant="destructive"
        isLoading={remove.isPending}
        onConfirm={async () => {
          if (pendingDelete) await remove.mutateAsync(pendingDelete);
        }}
      />
    </Card>
  );
}
