'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, ListChecks, Loader2, Pencil, Plus, Trash2 } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { jobVacancyService } from '@/services/hr/recruitment.service';
import { skillService } from '@/services/hr/skill.service';
import { qualificationService } from '@/services/hr/lookup.service';
import {
  MANDATORY_MATCH_MODES,
  SHORTLISTING_COMPARISON_OPERATORS,
  SHORTLISTING_CRITERIA_TYPES,
  VALUE_MATCH_STRATEGIES,
} from '@/types/hr/recruitment';
import type {
  MandatoryMatchMode,
  ShortlistingComparisonOperator,
  ShortlistingCriteria,
  ShortlistingCriteriaForm,
  ShortlistingCriteriaType,
  ValueMatchStrategy,
} from '@/types/hr/recruitment';

// ── what each criterion type actually uses ──────────────────────────────────
// Read off JobApplicationService.EvaluateCriterion rather than guessed. A form that offers an
// input the engine never reads for the chosen type is a form that lies about what it will do.
type Shape = {
  label: string;
  /** Numeric arm — min/max and a comparison operator. */
  numeric?: boolean;
  /** Comma-separated list of required values, matched with mode + strategy. */
  list?: boolean;
  /** A single free-text value (no list semantics). */
  single?: boolean;
  /** Which catalogue, if any, gives an ID-first exact match. */
  catalogue?: 'skill' | 'qualification';
  /** Operators the engine honours for this type; undefined = all of them. */
  operators?: readonly ShortlistingComparisonOperator[];
  hint: string;
};

const SHAPES: Record<ShortlistingCriteriaType, Shape> = {
  Qualification: {
    label: 'Qualification',
    list: true,
    catalogue: 'qualification',
    hint: 'Pick a catalogue qualification for an exact match, or list names to match on text — a candidate holding the catalogue one passes immediately.',
  },
  EducationLevel: {
    label: 'Education level',
    list: true,
    catalogue: 'qualification',
    hint: 'Scored exactly like Qualification: catalogue match first, then the listed names.',
  },
  Skill: {
    label: 'Skill',
    list: true,
    catalogue: 'skill',
    hint: 'Pick a catalogue skill for an exact match, or list skill names to match on text.',
  },
  Certification: { label: 'Certification', list: true, hint: 'List the certifications by name.' },
  Language: { label: 'Language', list: true, hint: 'List the languages. No list means every candidate passes.' },
  YearsOfExperience: {
    label: 'Years of experience',
    numeric: true,
    operators: ['GreaterThanOrEqual', 'GreaterThan', 'LessThan', 'LessThanOrEqual', 'Equals', 'Between'],
    hint: 'Between uses both bounds; the greater/less operators use one. A near miss still scores partially.',
  },
  Age: {
    label: 'Age',
    numeric: true,
    operators: ['GreaterThanOrEqual', 'GreaterThan', 'LessThan', 'LessThanOrEqual', 'Equals', 'Between'],
    hint: 'Computed from date of birth at the time of scoring.',
  },
  Gender: { label: 'Gender', single: true, hint: 'A gender name, or “any”. Anything left blank passes everyone.' },
  Location: {
    label: 'Location',
    single: true,
    operators: ['Equals', 'Contains'],
    hint: 'Matched against the candidate’s city. Contains is the default; Equals demands the whole city name.',
  },
  Other: {
    label: 'Other',
    hint: '⚠ Not auto-evaluated. Every candidate passes this criterion with a neutral score — use it only for something a person judges off-system.',
  },
};

const blank = (): ShortlistingCriteriaForm => ({
  criteriaName: '',
  description: '',
  type: 'Qualification',
  requiredValue: '',
  minValue: null,
  maxValue: null,
  isMandatory: true,
  matchMode: 'AnyMatched',
  matchStrategy: 'Contains',
  requiredSkillId: null,
  requiredQualificationId: null,
  // ⚠ Never null. `Weight` is a non-nullable int on both DTOs, so `null` is rejected 400 by the
  // JSON reader before the handler runs — which is exactly what the old form did.
  weight: 1,
  comparisonOperator: null,
});

const NONE = '__none__';

const toForm = (c: ShortlistingCriteria): ShortlistingCriteriaForm => ({
  criteriaName: c.criteriaName,
  description: c.description ?? '',
  // A legacy `0` row has no valid type; make the editor demand one rather than resend the 0.
  type: (typeof c.type === 'string' ? c.type : 'Qualification') as ShortlistingCriteriaType,
  requiredValue: c.requiredValue ?? '',
  minValue: c.minValue ?? null,
  maxValue: c.maxValue ?? null,
  isMandatory: c.isMandatory,
  matchMode: c.matchMode ?? 'AnyMatched',
  matchStrategy: c.matchStrategy ?? 'Contains',
  requiredSkillId: c.requiredSkillId ?? null,
  requiredQualificationId: c.requiredQualificationId ?? null,
  weight: c.weight ?? 1,
  comparisonOperator: c.comparisonOperator ?? null,
});

/**
 * What an application is scored against for this vacancy.
 *
 * ⚠ Changing the criteria **marks every already-scored application stale** server-side, so the
 * scores are not silently left standing against a different bar. Worth knowing before editing a
 * vacancy that already has applicants — which is why the panel says so rather than leaving it to
 * be discovered.
 *
 * ⚠ Rebuilt 2026-09-01 (lane 5b). The previous version sent `minimumScore` and `displayOrder`,
 * neither of which exists on the DTO, and never sent `type` at all — so every criterion it made
 * was stored as the undefined enum `0` and passed every candidate. Those rows are flagged below.
 */
export function VacancyCriteriaPanel({
  vacancyId,
  canManage,
  canRemove,
}: {
  vacancyId: string;
  canManage: boolean;
  /**
   * Removing a criterion is a tier above adding one.
   *
   * DELETE api/job-vacancies/criteria/{id} is gated on RecruitmentAdminPolicy while the POST and
   * PUT beside it take RecruitmentWritePolicy, so an HR user who can add a criterion cannot
   * delete one. The panel used to offer Remove to anyone who could add, and it answered 403 with
   * a toast saying nothing about why (probed 2026-09-01, lane 5b). Hide it instead.
   */
  canRemove: boolean;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<ShortlistingCriteria | null>(null);
  const [form, setForm] = useState<ShortlistingCriteriaForm>(blank);

  const criteria = useQuery({
    queryKey: ['hr', 'vacancy-criteria', vacancyId],
    queryFn: () => jobVacancyService.getCriteria(vacancyId),
    enabled: !!vacancyId,
  });

  const shape = SHAPES[form.type];

  // Only fetched when a catalogue-backed type is selected — a vacancy screen should not pull
  // 186 qualifications to render a years-of-experience criterion.
  const skills = useQuery({
    queryKey: ['hr', 'skills', 'active'],
    queryFn: () => skillService.getActive(),
    enabled: open && shape.catalogue === 'skill',
  });
  const qualifications = useQuery({
    queryKey: ['hr', 'qualifications', 'active'],
    queryFn: () => qualificationService.getActive(),
    enabled: open && shape.catalogue === 'qualification',
  });

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'vacancy-criteria', vacancyId] });

  const payload = (): ShortlistingCriteriaForm => ({
    ...form,
    criteriaName: form.criteriaName.trim(),
    description: form.description?.trim() || null,
    // Send only what this type's arm of the engine reads, so a leftover value from a type the
    // user changed away from cannot sit in the row misrepresenting the rule.
    requiredValue: shape.list || shape.single ? form.requiredValue?.trim() || null : null,
    minValue: shape.numeric ? form.minValue : null,
    maxValue: shape.numeric ? form.maxValue : null,
    requiredSkillId: shape.catalogue === 'skill' ? form.requiredSkillId : null,
    requiredQualificationId: shape.catalogue === 'qualification' ? form.requiredQualificationId : null,
    comparisonOperator: shape.operators || shape.numeric ? form.comparisonOperator : null,
    weight: Number.isFinite(form.weight) && form.weight > 0 ? form.weight : 1,
  });

  const save = useMutation({
    mutationFn: () =>
      editing
        ? jobVacancyService.updateCriteria(editing.id, payload())
        : jobVacancyService.addCriteria(vacancyId, payload()),
    onSuccess: async () => {
      await refresh();
      setOpen(false);
      setEditing(null);
      setForm(blank());
      toast({ title: editing ? 'Criterion updated' : 'Criterion added' });
    },
    onError: (e: any) =>
      toast({
        title: editing ? 'Could not update it' : 'Could not add it',
        description: e?.data?.message ?? e?.message,
        variant: 'destructive',
      }),
  });

  const remove = useMutation({
    mutationFn: (criteriaId: string) => jobVacancyService.deleteCriteria(criteriaId),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Criterion removed' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not remove it', description: e?.data?.message ?? e?.message, variant: 'destructive' }),
  });

  const rows = criteria.data ?? [];
  const untyped = useMemo(() => rows.filter((c) => typeof c.type !== 'string'), [rows]);

  const startAdd = () => {
    setEditing(null);
    setForm(blank());
    setOpen(true);
  };

  const startEdit = (c: ShortlistingCriteria) => {
    setEditing(c);
    setForm(toForm(c));
    setOpen(true);
  };

  /** What the criterion measures, in the row — the column the panel never used to have. */
  const measures = (c: ShortlistingCriteria) => {
    if (typeof c.type !== 'string') return 'Not set';
    const s = SHAPES[c.type];
    if (s.numeric) {
      const op = c.comparisonOperatorName ?? 'Between';
      return `${s.label} · ${op} ${c.minValue ?? '—'}${c.maxValue != null ? `–${c.maxValue}` : ''}`;
    }
    if (c.requiredQualificationId || c.requiredSkillId) return `${s.label} · from the catalogue`;
    if (c.requiredValue) return `${s.label} · ${c.requiredValue}`;
    return s.label;
  };

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
          <Button size="sm" onClick={startAdd}>
            <Plus className="mr-2 h-4 w-4" /> Add a criterion
          </Button>
        )}
      </CardHeader>
      <CardContent className="p-0">
        {untyped.length > 0 && (
          <div className="px-6 pb-3">
            <Alert variant="destructive">
              <AlertTriangle className="h-4 w-4" />
              <AlertDescription>
                {untyped.length === 1 ? 'One criterion does' : `${untyped.length} criteria do`} not
                say what {untyped.length === 1 ? 'it measures' : 'they measure'}, so{' '}
                {untyped.length === 1 ? 'it is' : 'they are'} scored as “not auto-evaluated” and{' '}
                <strong>every candidate passes</strong>. Open{' '}
                {untyped.length === 1 ? 'it' : 'each one'} and choose what it measures.
              </AlertDescription>
            </Alert>
          </div>
        )}

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
                <TableHead>Measures</TableHead>
                <TableHead>Matching</TableHead>
                <TableHead className="text-right">Weight</TableHead>
                <TableHead>Mandatory</TableHead>
                {canManage && <TableHead className="w-20" />}
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((c) => (
                <TableRow key={c.id}>
                  <TableCell className="font-medium">
                    {c.criteriaName}
                    {c.description && (
                      <span className="block text-sm font-normal text-muted-foreground">
                        {c.description}
                      </span>
                    )}
                  </TableCell>
                  <TableCell className="text-sm">
                    {typeof c.type === 'string' ? (
                      measures(c)
                    ) : (
                      <Badge variant="destructive">Not set — passes everyone</Badge>
                    )}
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {typeof c.type === 'string' && SHAPES[c.type].list
                      ? `${c.matchMode === 'AllRequired' ? 'All required' : 'Any is enough'} · ${c.matchStrategy}`
                      : '—'}
                  </TableCell>
                  <TableCell className="text-right tabular-nums">{c.weight}</TableCell>
                  <TableCell>
                    {c.isMandatory ? <Badge>Mandatory</Badge> : <Badge variant="outline">Desirable</Badge>}
                  </TableCell>
                  {canManage && (
                    <TableCell className="whitespace-nowrap">
                      <Button variant="ghost" size="icon" onClick={() => startEdit(c)}>
                        <Pencil className="h-4 w-4" />
                      </Button>
                      {canRemove && (
                        <Button
                          variant="ghost"
                          size="icon"
                          onClick={() => remove.mutate(c.id)}
                          disabled={remove.isPending}
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      )}
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>

      <Dialog open={open} onOpenChange={(v) => { setOpen(v); if (!v) setEditing(null); }}>
        <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>
              {editing ? 'Edit shortlisting criterion' : 'Add a shortlisting criterion'}
            </DialogTitle>
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
              <Label htmlFor="criteriaType">What it measures</Label>
              <Select
                value={form.type}
                onValueChange={(v) =>
                  setForm({ ...form, type: v as ShortlistingCriteriaType, comparisonOperator: null })
                }
              >
                <SelectTrigger id="criteriaType">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {SHORTLISTING_CRITERIA_TYPES.map((t) => (
                    <SelectItem key={t} value={t}>
                      {SHAPES[t].label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">{shape.hint}</p>
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

            {shape.catalogue === 'skill' && (
              <div className="space-y-1.5">
                <Label htmlFor="requiredSkillId">Catalogue skill</Label>
                <Select
                  value={form.requiredSkillId ?? NONE}
                  onValueChange={(v) => setForm({ ...form, requiredSkillId: v === NONE ? null : v })}
                >
                  <SelectTrigger id="requiredSkillId">
                    <SelectValue placeholder={skills.isLoading ? 'Loading…' : 'None — match on the names below'} />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={NONE}>None — match on the names below</SelectItem>
                    {(skills.data ?? []).map((s) => (
                      <SelectItem key={s.id} value={s.id}>
                        {s.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}

            {shape.catalogue === 'qualification' && (
              <div className="space-y-1.5">
                <Label htmlFor="requiredQualificationId">Catalogue qualification</Label>
                <Select
                  value={form.requiredQualificationId ?? NONE}
                  onValueChange={(v) =>
                    setForm({ ...form, requiredQualificationId: v === NONE ? null : v })
                  }
                >
                  <SelectTrigger id="requiredQualificationId">
                    <SelectValue
                      placeholder={qualifications.isLoading ? 'Loading…' : 'None — match on the names below'}
                    />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={NONE}>None — match on the names below</SelectItem>
                    {(qualifications.data ?? []).map((q) => (
                      <SelectItem key={q.id} value={q.id}>
                        {q.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}

            {(shape.list || shape.single) && (
              <div className="space-y-1.5">
                <Label htmlFor="requiredValue">
                  {shape.list ? 'Accepted values' : 'Required value'}
                </Label>
                <Input
                  id="requiredValue"
                  value={form.requiredValue ?? ''}
                  onChange={(e) => setForm({ ...form, requiredValue: e.target.value })}
                  placeholder={shape.list ? 'BSc Civil Engineering, HND Building Technology' : ''}
                />
                {shape.list && (
                  // ⚠ SplitValues splits on a COMMA. Any other separator becomes one long value
                  // that matches nothing.
                  <p className="text-xs text-muted-foreground">Separate several with commas.</p>
                )}
              </div>
            )}

            {shape.list && (
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1.5">
                  <Label htmlFor="matchMode">How many must match</Label>
                  <Select
                    value={form.matchMode}
                    onValueChange={(v) => setForm({ ...form, matchMode: v as MandatoryMatchMode })}
                  >
                    <SelectTrigger id="matchMode">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {MANDATORY_MATCH_MODES.map((m) => (
                        <SelectItem key={m} value={m}>
                          {m === 'AllRequired' ? 'All of them' : 'Any one of them'}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="matchStrategy">How each is compared</Label>
                  <Select
                    value={form.matchStrategy}
                    onValueChange={(v) => setForm({ ...form, matchStrategy: v as ValueMatchStrategy })}
                  >
                    <SelectTrigger id="matchStrategy">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {VALUE_MATCH_STRATEGIES.map((m) => (
                        <SelectItem key={m} value={m}>
                          {m === 'Exact' ? 'Exactly' : m === 'Contains' ? 'Contains' : 'Fuzzy (close enough)'}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>
            )}

            {shape.numeric && (
              <div className="grid grid-cols-3 gap-3">
                <div className="space-y-1.5">
                  <Label htmlFor="minValue">Minimum</Label>
                  <Input
                    id="minValue"
                    type="number"
                    value={form.minValue ?? ''}
                    onChange={(e) =>
                      setForm({ ...form, minValue: e.target.value ? Number(e.target.value) : null })
                    }
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="maxValue">Maximum</Label>
                  <Input
                    id="maxValue"
                    type="number"
                    value={form.maxValue ?? ''}
                    onChange={(e) =>
                      setForm({ ...form, maxValue: e.target.value ? Number(e.target.value) : null })
                    }
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="weight">Weight</Label>
                  <Input
                    id="weight"
                    type="number"
                    min={1}
                    max={100}
                    value={form.weight}
                    onChange={(e) => setForm({ ...form, weight: Number(e.target.value) || 1 })}
                  />
                </div>
              </div>
            )}

            {(shape.numeric || shape.operators) && (
              <div className="space-y-1.5">
                <Label htmlFor="comparisonOperator">Comparison</Label>
                <Select
                  value={form.comparisonOperator ?? NONE}
                  onValueChange={(v) =>
                    setForm({
                      ...form,
                      comparisonOperator: v === NONE ? null : (v as ShortlistingComparisonOperator),
                    })
                  }
                >
                  <SelectTrigger id="comparisonOperator">
                    <SelectValue placeholder="Default" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={NONE}>
                      {shape.numeric ? 'Default — between the bounds' : 'Default — contains'}
                    </SelectItem>
                    {(shape.operators ?? SHORTLISTING_COMPARISON_OPERATORS).map((op) => (
                      <SelectItem key={op} value={op}>
                        {op.replace(/([a-z])([A-Z])/g, '$1 $2')}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}

            {!shape.numeric && (
              <div className="space-y-1.5">
                <Label htmlFor="weightAlt">Weight</Label>
                <Input
                  id="weightAlt"
                  type="number"
                  min={1}
                  max={100}
                  value={form.weight}
                  onChange={(e) => setForm({ ...form, weight: Number(e.target.value) || 1 })}
                />
                <p className="text-xs text-muted-foreground">
                  How much this counts towards the composite score, 1–100.
                </p>
              </div>
            )}

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
            <Button
              onClick={() => save.mutate()}
              disabled={!form.criteriaName.trim() || save.isPending}
            >
              {save.isPending ? 'Saving…' : editing ? 'Save' : 'Add'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
