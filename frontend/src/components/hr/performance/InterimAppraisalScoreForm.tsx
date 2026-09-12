'use client';

import { useMemo, useState } from 'react';
import { Gauge } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { humanizeEnum } from '@/lib/hr/attendance-format';
import type {
  FinalizeFullInterimAppraisal,
  InterimGoalScore,
} from '@/types/hr/interim-reviews';

interface InterimAppraisalScoreFormProps {
  goals: InterimGoalScore[];
  /** Read-only once the event is completed — the period score is then the record. */
  readOnly?: boolean;
  submitting?: boolean;
  onSubmit: (payload: FinalizeFullInterimAppraisal) => void;
}

/**
 * Scores the period's goals for a full interim appraisal.
 *
 * **The preview is the same arithmetic the server runs**: a weighted mean over the goals being
 * scored, falling back to a plain average when every weight is zero. Showing it here means the
 * manager sees the number before committing rather than discovering it afterwards — and if the
 * preview and the stored `overallPeriodScore` ever disagree, one of the two is wrong and it is
 * worth knowing which.
 *
 * ⚠ Weights are relative **to the goals in this list**, not to 100. A period with two goals
 * weighted 30 and 10 divides by 40, not by 100, so a single goal scored 80 yields 80 rather than
 * a diluted 24.
 *
 * ⚠ Finalizing also moves each goal's own percent and execution status. It is not a scratch pad.
 */
export function InterimAppraisalScoreForm({
  goals,
  readOnly = false,
  submitting = false,
  onSubmit,
}: InterimAppraisalScoreFormProps) {
  const [scores, setScores] = useState<Record<string, string>>(() =>
    Object.fromEntries(
      goals.map((g) => [g.employeeGoalId, g.existingScore != null ? String(g.existingScore) : '']),
    ),
  );
  const [notes, setNotes] = useState<Record<string, string>>({});
  const [managerNotes, setManagerNotes] = useState('');

  // flatMap rather than map-then-filter so the value is a number by construction — a filtered
  // array of `number | null` still reads as nullable to the type checker.
  const scored = useMemo(
    () =>
      goals.flatMap((goal) => {
        const raw = (scores[goal.employeeGoalId] ?? '').trim();
        if (raw === '') return [];
        const value = Number(raw);
        return Number.isFinite(value) ? [{ goal, value }] : [];
      }),
    [goals, scores],
  );

  const allScored = scored.length === goals.length && goals.length > 0;
  const outOfRange = scored.some((p) => p.value < 0 || p.value > 100);

  // Mirrors FinalizeFullAppraisalAsync: weighted mean, plain average when weights are all zero.
  const preview = useMemo(() => {
    if (scored.length === 0) return null;
    const totalWeight = scored.reduce((sum, p) => sum + p.goal.weight, 0);
    if (totalWeight > 0) {
      return scored.reduce((sum, p) => sum + p.value * p.goal.weight, 0) / totalWeight;
    }
    return scored.reduce((sum, p) => sum + p.value, 0) / scored.length;
  }, [scored]);

  if (goals.length === 0) {
    return (
      <EmptyState
        icon={Gauge}
        title="No goals to score"
        description="This employee has no goals in the review's cycle, so there is nothing for a full interim appraisal to score."
      />
    );
  }

  const submit = () => {
    onSubmit({
      scores: scored.map((p) => ({
        employeeGoalId: p.goal.employeeGoalId,
        score: p.value,
        note: notes[p.goal.employeeGoalId]?.trim() || null,
      })),
      managerNotes: managerNotes.trim() || null,
    });
  };

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-4">
        <CardTitle>Score the period&apos;s goals</CardTitle>
        {preview != null && (
          <div className="text-right">
            <p className="text-xs text-muted-foreground">Period score</p>
            <p className="text-2xl font-semibold tabular-nums">{preview.toFixed(2)}</p>
          </div>
        )}
      </CardHeader>
      <CardContent className="space-y-4">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Goal</TableHead>
              <TableHead className="w-24">Period</TableHead>
              <TableHead className="w-20">Weight</TableHead>
              <TableHead className="w-28">Progress</TableHead>
              <TableHead className="w-28">Score</TableHead>
              <TableHead>Note</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {goals.map((g) => (
              <TableRow key={g.employeeGoalId}>
                <TableCell className="font-medium">{g.title}</TableCell>
                <TableCell className="text-muted-foreground">{humanizeEnum(g.period)}</TableCell>
                <TableCell className="tabular-nums">{g.weight}</TableCell>
                <TableCell className="tabular-nums text-muted-foreground">
                  {g.currentProgress != null ? `${g.currentProgress}%` : '—'}
                </TableCell>
                <TableCell>
                  <Input
                    type="number"
                    min={0}
                    max={100}
                    inputMode="decimal"
                    aria-label={`Score for ${g.title}`}
                    disabled={readOnly}
                    value={scores[g.employeeGoalId] ?? ''}
                    onChange={(e) =>
                      setScores((prev) => ({ ...prev, [g.employeeGoalId]: e.target.value }))
                    }
                  />
                </TableCell>
                <TableCell>
                  <Input
                    aria-label={`Note for ${g.title}`}
                    placeholder="Optional"
                    disabled={readOnly}
                    value={notes[g.employeeGoalId] ?? ''}
                    onChange={(e) =>
                      setNotes((prev) => ({ ...prev, [g.employeeGoalId]: e.target.value }))
                    }
                  />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>

        {!readOnly && (
          <>
            <div className="space-y-2">
              <Label htmlFor="interim-manager-notes">Manager notes</Label>
              <Textarea
                id="interim-manager-notes"
                rows={3}
                value={managerNotes}
                onChange={(e) => setManagerNotes(e.target.value)}
                placeholder="What the period looked like overall, and what changes for the next one."
              />
            </div>

            {outOfRange && (
              <Alert variant="destructive">
                <AlertTitle>Scores must be between 0 and 100</AlertTitle>
                <AlertDescription>
                  The server rejects anything outside that range.
                </AlertDescription>
              </Alert>
            )}

            {!allScored && scored.length > 0 && (
              <Alert>
                <AlertTitle>
                  {goals.length - scored.length} goal
                  {goals.length - scored.length === 1 ? '' : 's'} unscored
                </AlertTitle>
                <AlertDescription>
                  Finalizing now closes the review and computes the period score from the{' '}
                  {scored.length} scored goal{scored.length === 1 ? '' : 's'} only. Unscored goals
                  are left untouched.
                </AlertDescription>
              </Alert>
            )}

            <div className="flex justify-end">
              <Button onClick={submit} disabled={submitting || scored.length === 0 || outOfRange}>
                {submitting ? 'Finalizing…' : 'Finalize interim appraisal'}
              </Button>
            </div>
          </>
        )}
      </CardContent>
    </Card>
  );
}
