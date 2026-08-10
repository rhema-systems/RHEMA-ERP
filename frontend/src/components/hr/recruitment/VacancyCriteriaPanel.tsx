'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ListChecks, Loader2, Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
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
import { Textarea } from '@/components/ui/textarea';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { jobVacancyService } from '@/services/hr/recruitment.service';
import type { ShortlistingCriteriaForm } from '@/types/hr/recruitment';

const blank = (): ShortlistingCriteriaForm => ({
  criteriaName: '',
  description: '',
  isMandatory: true,
  weight: null,
  minimumScore: null,
  displayOrder: null,
});

/**
 * What an application is scored against for this vacancy.
 *
 * ⚠ Changing the criteria **marks every already-scored application stale** server-side, so the
 * scores are not silently left standing against a different bar. Worth knowing before editing a
 * vacancy that already has applicants — which is why the panel says so rather than leaving it to
 * be discovered.
 */
export function VacancyCriteriaPanel({
  vacancyId,
  canManage,
}: {
  vacancyId: string;
  canManage: boolean;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState<ShortlistingCriteriaForm>(blank);

  const criteria = useQuery({
    queryKey: ['hr', 'vacancy-criteria', vacancyId],
    queryFn: () => jobVacancyService.getCriteria(vacancyId),
    enabled: !!vacancyId,
  });

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'vacancy-criteria', vacancyId] });

  const add = useMutation({
    mutationFn: () =>
      jobVacancyService.addCriteria(vacancyId, {
        ...form,
        criteriaName: form.criteriaName.trim(),
        description: form.description?.trim() || null,
      }),
    onSuccess: async () => {
      await refresh();
      setOpen(false);
      setForm(blank());
      toast({ title: 'Criterion added' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not add it', description: e?.message, variant: 'destructive' }),
  });

  const remove = useMutation({
    mutationFn: (criteriaId: string) => jobVacancyService.deleteCriteria(criteriaId),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Criterion removed' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not remove it', description: e?.message, variant: 'destructive' }),
  });

  const rows = criteria.data ?? [];

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-3">
        <div>
          <CardTitle className="text-base">Shortlisting criteria</CardTitle>
          <p className="mt-1 text-sm text-muted-foreground">
            Changing these marks any application already scored as stale, so nothing is judged
            against a bar that has since moved.
          </p>
        </div>
        {canManage && (
          <Button size="sm" onClick={() => setOpen(true)}>
            <Plus className="mr-2 h-4 w-4" /> Add a criterion
          </Button>
        )}
      </CardHeader>
      <CardContent className="p-0">
        {criteria.isLoading ? (
          <div className="flex items-center justify-center py-10">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : rows.length === 0 ? (
          <div className="py-8">
            <EmptyState
              icon={ListChecks}
              title="No criteria yet"
              description="Qualifications, experience and skills an application is measured against."
            />
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Criterion</TableHead>
                <TableHead>Description</TableHead>
                <TableHead className="text-right">Weight</TableHead>
                <TableHead className="text-right">Minimum</TableHead>
                <TableHead>Mandatory</TableHead>
                {canManage && <TableHead className="w-10" />}
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((c) => (
                <TableRow key={c.id}>
                  <TableCell className="font-medium">{c.criteriaName}</TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {c.description || '—'}
                  </TableCell>
                  <TableCell className="text-right tabular-nums">{c.weight ?? '—'}</TableCell>
                  <TableCell className="text-right tabular-nums">{c.minimumScore ?? '—'}</TableCell>
                  <TableCell>
                    {c.isMandatory ? <Badge>Mandatory</Badge> : <Badge variant="outline">Desirable</Badge>}
                  </TableCell>
                  {canManage && (
                    <TableCell>
                      <Button
                        variant="ghost"
                        size="icon"
                        onClick={() => remove.mutate(c.id)}
                        disabled={remove.isPending}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add a shortlisting criterion</DialogTitle>
            <DialogDescription>
              Mandatory criteria must be met; desirable ones only contribute to the score.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-2">
            <div className="space-y-1.5">
              <Label htmlFor="criteriaName">Criterion</Label>
              <Input
                id="criteriaName"
                value={form.criteriaName}
                onChange={(e) => setForm({ ...form, criteriaName: e.target.value })}
                placeholder="e.g. Professional accounting qualification"
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="criteriaDescription">Description</Label>
              <Textarea
                id="criteriaDescription"
                rows={2}
                value={form.description ?? ''}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
              />
            </div>

            <div className="grid grid-cols-3 gap-3">
              <div className="space-y-1.5">
                <Label htmlFor="weight">Weight</Label>
                <Input
                  id="weight"
                  type="number"
                  value={form.weight ?? ''}
                  onChange={(e) =>
                    setForm({ ...form, weight: e.target.value ? Number(e.target.value) : null })
                  }
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="minimumScore">Minimum score</Label>
                <Input
                  id="minimumScore"
                  type="number"
                  value={form.minimumScore ?? ''}
                  onChange={(e) =>
                    setForm({ ...form, minimumScore: e.target.value ? Number(e.target.value) : null })
                  }
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="displayOrder">Order</Label>
                <Input
                  id="displayOrder"
                  type="number"
                  value={form.displayOrder ?? ''}
                  onChange={(e) =>
                    setForm({ ...form, displayOrder: e.target.value ? Number(e.target.value) : null })
                  }
                />
              </div>
            </div>

            <div className="flex items-center gap-2">
              <Checkbox
                id="isMandatory"
                checked={form.isMandatory}
                onCheckedChange={(c) => setForm({ ...form, isMandatory: c === true })}
              />
              <Label htmlFor="isMandatory" className="font-normal">
                Mandatory — an application that fails this is not shortlistable
              </Label>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => add.mutate()} disabled={!form.criteriaName.trim() || add.isPending}>
              {add.isPending ? 'Saving…' : 'Add'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
