'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Loader2, Save } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import { salaryGradeService } from '@/services/hr/salary-grade.service';
import type { JobValuationSummary } from '@/types/hr/job-architecture';

const NONE = '__suggested__';

const fmt = (v?: number | null) =>
  v == null ? '—' : new Intl.NumberFormat('en-GH', { style: 'currency', currency: 'GHS' }).format(v);

/**
 * Three grades, kept apart on purpose (round 3, lane J2; decision D-11):
 *
 *  - the SUGGESTED grade — the matcher's output from the derived value and the benchmark; read-only;
 *  - the PROPOSED grade — the author's answer to it, defaulted to the suggestion when the valuation
 *    is stored and theirs to change, with a note saying why;
 *  - the POSITION's grade — payroll's fact, set on the position, which neither of the above assigns.
 *
 * The matcher's sentence is shown verbatim: which band contains the midpoint and on what basis, or
 * that none does and which is nearest. A dash where a grade would be is an answer, not a gap.
 */
export function ProposedGradeCard({
  jobDescriptionId,
  valuation,
  canAuthor,
  onSaved,
}: {
  jobDescriptionId: string;
  valuation: JobValuationSummary;
  canAuthor: boolean;
  onSaved: () => void;
}) {
  const { data: grades } = useQuery({
    queryKey: ['hr', 'salary-grades', 'active'],
    queryFn: () => salaryGradeService.getActive(),
    enabled: canAuthor,
  });

  const [gradeId, setGradeId] = useState<string>(valuation.proposedSalaryGradeId ?? NONE);
  const [note, setNote] = useState(valuation.proposedSalaryGradeNote ?? '');
  useEffect(() => {
    setGradeId(valuation.proposedSalaryGradeId ?? NONE);
    setNote(valuation.proposedSalaryGradeNote ?? '');
  }, [valuation.proposedSalaryGradeId, valuation.proposedSalaryGradeNote]);

  const dirty =
    gradeId !== (valuation.proposedSalaryGradeId ?? NONE) || note !== (valuation.proposedSalaryGradeNote ?? '');

  const save = useMutation({
    mutationFn: () =>
      jobArchitectureService.setProposedGrade(jobDescriptionId, {
        proposedSalaryGradeId: gradeId === NONE ? null : gradeId,
        proposedSalaryGradeNote: note.trim() || null,
      }),
    onSuccess: () => {
      toast.success('Proposed grade saved');
      onSaved();
    },
    onError: (e: unknown) => toast.error(e instanceof Error ? e.message : 'The proposal was refused.'),
  });

  const differs =
    valuation.proposedSalaryGradeId && valuation.proposedSalaryGradeId !== valuation.suggestedSalaryGradeId;

  return (
    <Card data-testid="proposed-grade-card">
      <CardHeader>
        <CardTitle className="text-base">Salary grade</CardTitle>
        <CardDescription>
          The suggestion is worked out from the job; the proposal is yours; the post&rsquo;s actual grade is
          set on the position and read by payroll.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <dl className="grid gap-3 sm:grid-cols-3">
          <div>
            <dt className="text-xs text-muted-foreground">Suggested</dt>
            <dd className="text-sm font-medium" data-testid="suggested-grade">
              {valuation.suggestedSalaryGradeName ?? '—'}
              {valuation.suggestedGradeMinSalary != null && (
                <span className="block text-xs font-normal text-muted-foreground">
                  {fmt(valuation.suggestedGradeMinSalary)} – {fmt(valuation.suggestedGradeMaxSalary)}
                  {valuation.suggestedGradeBasis === 'Notches' ? ' · from notches' : ''}
                </span>
              )}
            </dd>
          </div>
          <div>
            <dt className="text-xs text-muted-foreground">Proposed</dt>
            <dd className="text-sm font-medium" data-testid="proposed-grade">
              {valuation.proposedSalaryGradeName ?? (valuation.suggestedSalaryGradeName ? 'Same as suggested' : '—')}
              {differs && <span className="block text-xs font-normal text-amber-700">Differs from the suggestion</span>}
            </dd>
          </div>
          <div>
            <dt className="text-xs text-muted-foreground">On the position</dt>
            <dd className="text-sm font-medium" data-testid="position-grade">
              {valuation.positionSalaryGradeName ?? 'Not set on the position'}
            </dd>
          </div>
        </dl>

        {valuation.suggestedGradeNote && (
          <p className="text-sm text-muted-foreground" data-testid="suggested-grade-note">
            {valuation.suggestedGradeNote}
          </p>
        )}

        {canAuthor && (
          <div className="grid gap-3 rounded-md border p-3 sm:grid-cols-[1fr_1fr_auto]">
            <div className="space-y-1.5">
              <Label htmlFor="proposed-grade">Propose a grade</Label>
              <Select value={gradeId} onValueChange={setGradeId}>
                <SelectTrigger id="proposed-grade">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NONE}>Follow the suggestion</SelectItem>
                  {(grades ?? []).map((g) => (
                    <SelectItem key={g.id} value={g.id}>
                      {g.code} · {g.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="proposed-grade-note">Why</Label>
              <Input
                id="proposed-grade-note"
                value={note}
                onChange={(e) => setNote(e.target.value)}
                maxLength={500}
                placeholder="Market pressure, scarcity, a comparator post…"
              />
            </div>
            <div className="flex items-end">
              <Button size="sm" disabled={!dirty || save.isPending} onClick={() => save.mutate()}>
                {save.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                Save
              </Button>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
