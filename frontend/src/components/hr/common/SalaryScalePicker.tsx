'use client';

import { useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { salaryGradeService } from '@/services/hr/salary-grade.service';
import { policySettingsService } from '@/services/hr/policy-settings.service';
import type { SalaryNotch } from '@/types/hr/salary';

export interface SalaryScaleSelection {
  gradeId: string;
  levelId: string;
  notchId: string;
}

const NONE = '__none__';

/**
 * Grade → (level) → notch on the salary scale — the one cascade, extracted from the employee's
 * salary-assignments tab so the manpower budget line can use it too (round 2b, R3).
 *
 * The level is shown only when the tenant is three-tier (`salaryStructureTiers ===
 * 'GradeLevelAndNotch'`, lane G). In two-tier the grade's one implicit level is resolved the
 * moment the grade's levels arrive so the notch list can load, and never asked for — the server
 * does the same resolution for a caller that sends no level.
 *
 * `onResolved` reports what the chosen place on the scale is WORTH — the notch amount, else the
 * level's mid-point, else the grade's minimum — so a host can fill a figure from it and say where
 * it came from. That mirrors the server's own resolution order; the server remains the authority.
 */
export function SalaryScalePicker({
  value,
  onChange,
  onResolved,
  disabled,
  idPrefix = 'scale',
  gradeLabel = 'Salary grade',
  allowNone = 'No grade',
}: {
  value: SalaryScaleSelection;
  onChange: (next: SalaryScaleSelection) => void;
  onResolved?: (r: { amount: number | null; source: 'Notch' | 'LevelMidpoint' | 'GradeMinimum' | null; caption: string }) => void;
  disabled?: boolean;
  idPrefix?: string;
  gradeLabel?: string;
  allowNone?: string | false;
}) {
  const { data: grades } = useQuery({
    queryKey: ['hr', 'salary-grades', 'active'],
    queryFn: () => salaryGradeService.getActive(),
    staleTime: 5 * 60 * 1000,
  });
  const { data: policy } = useQuery({
    queryKey: ['hr', 'policy-settings'],
    queryFn: () => policySettingsService.get(),
    staleTime: 5 * 60 * 1000,
  });
  const threeTier = policy?.salaryStructureTiers === 'GradeLevelAndNotch';

  const { data: levels } = useQuery({
    queryKey: ['hr', 'salary-grades', value.gradeId, 'levels'],
    queryFn: () => salaryGradeService.getLevels(value.gradeId),
    enabled: !!value.gradeId,
  });

  // Two-tier: the level exists in the data but not in the user's head. Resolve it silently.
  useEffect(() => {
    if (threeTier || !value.gradeId || !levels || levels.length === 0) return;
    const sole = levels.find((l) => l.isActive) ?? levels[0];
    if (sole && value.levelId !== sole.id) onChange({ ...value, levelId: sole.id, notchId: '' });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [threeTier, levels, value.gradeId, value.levelId]);

  const { data: notches } = useQuery({
    queryKey: ['hr', 'salary-levels', value.levelId, 'notches'],
    queryFn: () => salaryGradeService.getNotches(value.levelId),
    enabled: !!value.levelId,
  });

  // What the chosen place is worth, reported to the host whenever it changes.
  useEffect(() => {
    if (!onResolved) return;
    const grade = (grades ?? []).find((g) => g.id === value.gradeId);
    const level = (levels ?? []).find((l) => l.id === value.levelId);
    const notch = (notches ?? []).find((n: SalaryNotch) => n.id === value.notchId);
    const levelIsReal = !!level && !!grade && level.code.toLowerCase() !== grade.code.toLowerCase();
    if (notch) {
      onResolved({
        amount: notch.salaryAmount,
        source: 'Notch',
        caption: levelIsReal
          ? `notch ${notch.notchNumber}, level ${level!.code} of ${grade?.code ?? 'the grade'}`
          : `notch ${notch.notchNumber} of ${grade?.code ?? 'the grade'}`,
      });
    } else if (level && threeTier) {
      onResolved({ amount: level.midSalary, source: 'LevelMidpoint', caption: `mid-point of level ${level.code} of ${grade?.code ?? 'the grade'}` });
    } else if (grade) {
      onResolved({ amount: grade.minSalary, source: 'GradeMinimum', caption: `minimum of grade ${grade.code}` });
    } else {
      onResolved({ amount: null, source: null, caption: '' });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value.gradeId, value.levelId, value.notchId, grades, levels, notches, threeTier]);

  return (
    <div className="grid gap-4 sm:grid-cols-2">
      <div className="space-y-2 sm:col-span-2">
        <Label htmlFor={`${idPrefix}-grade`}>{gradeLabel}</Label>
        <Select
          value={value.gradeId || (allowNone ? NONE : undefined)}
          onValueChange={(v) => onChange({ gradeId: v === NONE ? '' : v, levelId: '', notchId: '' })}
          disabled={disabled}
        >
          <SelectTrigger id={`${idPrefix}-grade`}>
            <SelectValue placeholder="Choose a grade" />
          </SelectTrigger>
          <SelectContent>
            {allowNone && <SelectItem value={NONE}>{allowNone}</SelectItem>}
            {(grades ?? []).map((g) => (
              <SelectItem key={g.id} value={g.id}>
                {g.code} — {g.name}
                <span className="ml-2 text-xs text-muted-foreground">
                  {g.minSalary.toLocaleString()} – {g.maxSalary.toLocaleString()}
                </span>
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {threeTier && (
        <div className="space-y-2">
          <Label htmlFor={`${idPrefix}-level`}>Level</Label>
          <Select
            value={value.levelId || NONE}
            onValueChange={(v) => onChange({ ...value, levelId: v === NONE ? '' : v, notchId: '' })}
            disabled={disabled || !value.gradeId}
          >
            <SelectTrigger id={`${idPrefix}-level`}>
              <SelectValue placeholder="Level" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={NONE}>No level</SelectItem>
              {(levels ?? []).map((l) => (
                <SelectItem key={l.id} value={l.id}>{l.code || l.name}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      )}

      <div className="space-y-2">
        <Label htmlFor={`${idPrefix}-notch`}>Notch</Label>
        <Select
          value={value.notchId || NONE}
          onValueChange={(v) => onChange({ ...value, notchId: v === NONE ? '' : v })}
          disabled={disabled || !value.levelId}
        >
          <SelectTrigger id={`${idPrefix}-notch`}>
            <SelectValue placeholder="Notch" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={NONE}>No notch</SelectItem>
            {(notches ?? []).map((n: SalaryNotch) => (
              <SelectItem key={n.id} value={n.id}>
                {n.notchNumber} — {n.salaryAmount.toLocaleString()}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <p className="text-xs text-muted-foreground sm:col-span-2">
        {threeTier
          ? 'Grade, then level, then notch. The notch amount is the basic pay.'
          : 'Grade, then notch. The notch amount is the basic pay.'}
      </p>
    </div>
  );
}
