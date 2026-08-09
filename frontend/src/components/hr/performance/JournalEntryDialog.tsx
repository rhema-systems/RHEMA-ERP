'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { BookLock, Share2 } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
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
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { CycleSelect } from '@/components/hr/performance/CycleSelect';
import { useToast } from '@/hooks/use-toast';
import { employeeGoalService } from '@/services/hr/goals.service';
import { performanceJournalService } from '@/services/hr/journal.service';
import type { PerformanceJournalEntry } from '@/types/hr/journal';

/** Radix Select refuses an empty-string item value, so "no selection" needs a sentinel. */
const NONE = '__none__';

interface JournalEntryDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Null for a new entry. */
  entry: PerformanceJournalEntry | null;
  defaultCycleId?: string;
  defaultSubjectId?: string;
  reportOptions: { id: string; name: string }[];
  onSaved: () => void;
}

/**
 * Write or amend a journal entry.
 *
 * **Private is the default**, matching the server. The switch spells out the consequence either
 * way rather than labelling itself "private" and leaving the reader to infer who that excludes —
 * the answer ("not even HR") is unusual enough to be worth stating.
 *
 * ⚠ A private entry is refused with 422 when the chosen cycle has `enablePrivateJournal` switched
 * off. That is a policy decision on the cycle, so the message is surfaced verbatim rather than
 * being reworded as a failure.
 */
export function JournalEntryDialog({
  open,
  onOpenChange,
  entry,
  defaultCycleId,
  defaultSubjectId,
  reportOptions,
  onSaved,
}: JournalEntryDialogProps) {
  const { toast } = useToast();
  const [form, setForm] = useState({
    appraisalCycleId: '',
    subjectEmployeeId: '',
    relatedGoalId: '',
    title: '',
    body: '',
    isPrivate: true,
  });

  useEffect(() => {
    if (!open) return;
    setForm({
      appraisalCycleId: entry?.appraisalCycleId ?? defaultCycleId ?? '',
      subjectEmployeeId: entry?.subjectEmployeeId ?? defaultSubjectId ?? '',
      relatedGoalId: entry?.relatedGoalId ?? '',
      title: entry?.title ?? '',
      body: entry?.body ?? '',
      isPrivate: entry?.isPrivate ?? true,
    });
  }, [open, entry, defaultCycleId, defaultSubjectId]);

  // Goals to link against: the subject's when writing about a report, otherwise the author's own.
  // Only fetched once a cycle is chosen, since goals are cycle-scoped.
  const { data: goals } = useQuery({
    queryKey: ['hr', 'journal-goal-options', form.subjectEmployeeId, form.appraisalCycleId],
    queryFn: () =>
      employeeGoalService.getByEmployee(form.subjectEmployeeId, form.appraisalCycleId || undefined),
    enabled: open && !!form.subjectEmployeeId && !!form.appraisalCycleId,
  });

  const failed = (err: unknown) =>
    toast({
      variant: 'destructive',
      title: 'Could not save the entry',
      description: (err as Error)?.message ?? 'Please try again.',
    });

  const save = useMutation({
    mutationFn: () => {
      const payload = {
        appraisalCycleId: form.appraisalCycleId,
        subjectEmployeeId: form.subjectEmployeeId || null,
        relatedGoalId: form.relatedGoalId || null,
        title: form.title.trim(),
        body: form.body.trim(),
        isPrivate: form.isPrivate,
      };
      return entry
        ? performanceJournalService.update(entry.id, { ...payload, id: entry.id })
        : performanceJournalService.create(payload);
    },
    onSuccess: () => {
      onSaved();
      onOpenChange(false);
      toast({ title: entry ? 'Entry updated' : 'Entry saved' });
    },
    onError: failed,
  });

  const canSave =
    !!form.appraisalCycleId && form.title.trim().length > 0 && form.body.trim().length > 0;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{entry ? 'Edit journal entry' : 'New journal entry'}</DialogTitle>
          <DialogDescription>
            Something worth remembering at appraisal time — a win, a setback, feedback given or
            received.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="journal-cycle">Appraisal cycle</Label>
            <CycleSelect
              value={form.appraisalCycleId}
              onChange={(v) => setForm((p) => ({ ...p, appraisalCycleId: v }))}
              standalone={false}
            />
          </div>

          {reportOptions.length > 0 && (
            <div className="space-y-2">
              <Label htmlFor="journal-subject">About</Label>
              <Select
                value={form.subjectEmployeeId || NONE}
                onValueChange={(v) =>
                  setForm((p) => ({
                    ...p,
                    subjectEmployeeId: v === NONE ? '' : v,
                    // The goal list is scoped to whoever the entry is about.
                    relatedGoalId: '',
                  }))
                }
              >
                <SelectTrigger id="journal-subject">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NONE}>Myself</SelectItem>
                  {reportOptions.map((r) => (
                    <SelectItem key={r.id} value={r.id}>
                      {r.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}

          {(goals ?? []).length > 0 && (
            <div className="space-y-2">
              <Label htmlFor="journal-goal">Related goal (optional)</Label>
              <Select
                value={form.relatedGoalId || NONE}
                onValueChange={(v) =>
                  setForm((p) => ({ ...p, relatedGoalId: v === NONE ? '' : v }))
                }
              >
                <SelectTrigger id="journal-goal">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NONE}>No specific goal</SelectItem>
                  {(goals ?? []).map((g) => (
                    <SelectItem key={g.id} value={g.id}>
                      {g.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}

          <div className="space-y-2">
            <Label htmlFor="journal-title">Title</Label>
            <Input
              id="journal-title"
              maxLength={300}
              value={form.title}
              onChange={(e) => setForm((p) => ({ ...p, title: e.target.value }))}
              placeholder="What happened, in a few words"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="journal-body">Notes</Label>
            <Textarea
              id="journal-body"
              rows={8}
              maxLength={4000}
              value={form.body}
              onChange={(e) => setForm((p) => ({ ...p, body: e.target.value }))}
              placeholder="The detail you will want at appraisal time and will not remember by then."
            />
            <p className="text-xs text-muted-foreground">{form.body.length} / 4000</p>
          </div>

          <div className="flex items-center justify-between rounded-lg border p-4">
            <div className="space-y-0.5">
              <Label htmlFor="journal-private" className="flex items-center gap-2">
                {form.isPrivate ? (
                  <BookLock className="h-4 w-4" />
                ) : (
                  <Share2 className="h-4 w-4" />
                )}
                {form.isPrivate ? 'Private to me' : 'Shared with my manager'}
              </Label>
              <p className="text-sm text-muted-foreground">
                {form.isPrivate
                  ? 'Nobody else can read this — not your line manager, not HR.'
                  : 'Your line manager and HR will be able to read this.'}
              </p>
            </div>
            <Switch
              id="journal-private"
              checked={form.isPrivate}
              onCheckedChange={(v) => setForm((p) => ({ ...p, isPrivate: v }))}
            />
          </div>

          {form.isPrivate && (
            <Alert>
              <AlertDescription className="text-xs">
                If this cycle has private journalling switched off, saving will be refused and the
                reason shown. Share the entry instead, or pick another cycle.
              </AlertDescription>
            </Alert>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={() => save.mutate()} disabled={!canSave || save.isPending}>
            {save.isPending ? 'Saving…' : entry ? 'Save changes' : 'Add entry'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
