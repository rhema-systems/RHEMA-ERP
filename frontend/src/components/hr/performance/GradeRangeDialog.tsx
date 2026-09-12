'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Plus, Trash2, TriangleAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';
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
import { useToast } from '@/components/ui/use-toast';
import { appraisalTemplateService } from '@/services/hr/appraisal.service';

interface DraftRange {
  gradeDefinitionId: string;
  lowScore: number;
  highScore: number;
}

/**
 * The score bands for one template item: which grade a score of 0–100 earns.
 *
 * Bands are stored replace-all, so this dialog always sends the complete set — removing a row
 * and saving is how a band is deleted. The API rejects overlaps, a repeated grade and an empty
 * set, and the same checks run here first so the common mistakes are caught without a round
 * trip. Gaps are *not* rejected by either side, but a gap means a score in it grades to
 * nothing, so it is flagged as a warning.
 *
 * An item with no bands blocks the whole template from being activated or submitted, which is
 * why the template editor shows a band count on every row.
 */
export function GradeRangeDialog({
  itemId,
  itemLabel,
  open,
  onOpenChange,
  onSaved,
}: {
  itemId: string | null;
  itemLabel: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved?: () => void;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [draft, setDraft] = useState<DraftRange[]>([]);

  const { data: grades } = useQuery({
    queryKey: ['hr', 'appraisal-grade-definitions', 'active'],
    queryFn: () => appraisalTemplateService.getActiveGradeDefinitions(),
    staleTime: 5 * 60 * 1000,
  });

  // `itemId` is null only while the dialog is closed, which `enabled` already covers; the
  // fallback keeps the query function total rather than asserting.
  const { data: existing, isLoading } = useQuery({
    queryKey: ['hr', 'template-item-grade-ranges', itemId],
    queryFn: () => appraisalTemplateService.getItemGradeRanges(itemId ?? ''),
    enabled: open && !!itemId,
  });

  useEffect(() => {
    if (!open) return;
    setDraft(
      (existing ?? []).map((r) => ({
        gradeDefinitionId: r.gradeDefinitionId,
        lowScore: r.lowScore,
        highScore: r.highScore,
      })),
    );
  }, [open, existing]);

  const gradeOptions = grades ?? [];
  const gradeName = (id: string) => gradeOptions.find((g) => g.id === id)?.gradeName ?? 'Unknown';

  const errors: string[] = [];
  const warnings: string[] = [];

  if (draft.length === 0) {
    errors.push('At least one band is required.');
  }
  if (draft.some((r) => !r.gradeDefinitionId)) {
    errors.push('Every band needs a grade.');
  }
  if (draft.some((r) => r.lowScore > r.highScore)) {
    errors.push('A band’s low score cannot be above its high score.');
  }
  if (draft.some((r) => r.lowScore < 0 || r.highScore > 100)) {
    errors.push('Scores must be between 0 and 100.');
  }
  const usedGrades = draft.map((r) => r.gradeDefinitionId).filter(Boolean);
  if (new Set(usedGrades).size !== usedGrades.length) {
    errors.push('Each grade may appear only once.');
  }

  const sorted = [...draft].sort((a, b) => a.lowScore - b.lowScore);
  for (let i = 0; i < sorted.length - 1; i++) {
    if (sorted[i].highScore >= sorted[i + 1].lowScore) {
      errors.push(
        `Bands overlap: ${sorted[i].lowScore}–${sorted[i].highScore} and ${sorted[i + 1].lowScore}–${sorted[i + 1].highScore}.`,
      );
      break;
    }
    if (sorted[i + 1].lowScore > sorted[i].highScore + 1) {
      warnings.push(
        `Nothing covers ${sorted[i].highScore + 1}–${sorted[i + 1].lowScore - 1}; a score there grades to nothing.`,
      );
    }
  }

  const save = useMutation({
    mutationFn: () => {
      // The save button is disabled without an item, so this is narrowing, not a branch.
      if (!itemId) throw new Error('No template item selected.');
      return appraisalTemplateService.saveItemGradeRanges(itemId, {
        ranges: draft.map((r) => ({
          gradeDefinitionId: r.gradeDefinitionId,
          lowScore: Math.round(r.lowScore),
          highScore: Math.round(r.highScore),
        })),
      });
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: ['hr', 'template-item-grade-ranges', itemId],
      });
      toast({ title: 'Saved', description: 'Grade bands updated.' });
      onSaved?.();
      onOpenChange(false);
    },
    onError: (e: any) =>
      toast({
        title: 'Error',
        description: e?.message || 'Failed to save the grade bands.',
        variant: 'destructive',
      }),
  });

  const patch = (index: number, changes: Partial<DraftRange>) =>
    setDraft((rows) => rows.map((r, i) => (i === index ? { ...r, ...changes } : r)));

  /** New rows start where the last one left off, which is what is usually wanted. */
  const addRow = () => {
    const highest = draft.reduce((max, r) => Math.max(max, r.highScore), -1);
    const low = Math.min(highest + 1, 100);
    setDraft((rows) => [...rows, { gradeDefinitionId: '', lowScore: low, highScore: 100 }]);
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[640px]">
        <DialogHeader>
          <DialogTitle>Grade bands</DialogTitle>
          <DialogDescription>
            Which grade a score earns on “{itemLabel}”. Bands are saved as a complete set —
            remove a row and save to delete it.
          </DialogDescription>
        </DialogHeader>

        <div className="max-h-[55vh] space-y-3 overflow-y-auto py-2">
          {isLoading ? (
            <div className="flex items-center justify-center py-8">
              <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
            </div>
          ) : (
            <>
              {draft.length > 0 && (
                <div className="grid grid-cols-[1fr_5rem_5rem_2.5rem] items-center gap-2 text-xs text-muted-foreground">
                  <Label>Grade</Label>
                  <Label>From</Label>
                  <Label>To</Label>
                  <span />
                </div>
              )}

              {draft.map((row, index) => (
                <div
                  key={index}
                  className="grid grid-cols-[1fr_5rem_5rem_2.5rem] items-center gap-2"
                >
                  <Select
                    value={row.gradeDefinitionId || ''}
                    onValueChange={(value) => patch(index, { gradeDefinitionId: value })}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select a grade…" />
                    </SelectTrigger>
                    <SelectContent>
                      {gradeOptions.map((g) => (
                        <SelectItem key={g.id} value={g.id}>
                          {g.gradeName}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <Input
                    type="number"
                    min={0}
                    max={100}
                    value={row.lowScore}
                    onChange={(e) => patch(index, { lowScore: Number(e.target.value) })}
                  />
                  <Input
                    type="number"
                    min={0}
                    max={100}
                    value={row.highScore}
                    onChange={(e) => patch(index, { highScore: Number(e.target.value) })}
                  />
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    onClick={() => setDraft((rows) => rows.filter((_, i) => i !== index))}
                    aria-label={`Remove ${gradeName(row.gradeDefinitionId)} band`}
                  >
                    <Trash2 className="h-4 w-4 text-red-600" />
                  </Button>
                </div>
              ))}

              <Button type="button" variant="outline" size="sm" onClick={addRow}>
                <Plus className="mr-2 h-4 w-4" />
                Add band
              </Button>

              {gradeOptions.length === 0 && (
                <p className="text-sm text-amber-600 dark:text-amber-500">
                  No active grade definitions exist yet — add some under Grade Definitions first.
                </p>
              )}

              {errors.map((message) => (
                <p key={message} className="flex items-start gap-2 text-sm text-red-600">
                  <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0" />
                  {message}
                </p>
              ))}
              {warnings.map((message) => (
                <p
                  key={message}
                  className="flex items-start gap-2 text-sm text-amber-600 dark:text-amber-500"
                >
                  <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0" />
                  {message}
                </p>
              ))}
            </>
          )}
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            type="button"
            onClick={() => save.mutate()}
            disabled={save.isPending || errors.length > 0 || !itemId}
          >
            {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Save bands
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
