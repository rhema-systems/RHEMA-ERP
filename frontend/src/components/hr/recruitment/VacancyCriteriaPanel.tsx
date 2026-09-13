'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, ListChecks, Loader2, Pencil, Plus, ShieldAlert, Trash2, X } from 'lucide-react';
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
import { certificationService } from '@/services/hr/certification.service';
import { languageService } from '@/services/hr/language.service';
import { jobVacancyService } from '@/services/hr/recruitment.service';
import { skillService } from '@/services/hr/skill.service';
import { qualificationService } from '@/services/hr/lookup.service';
import { GENDERS } from '@/types/hr/recruitment-pipeline';
import {
  MANDATORY_MATCH_MODES,
  SHORTLISTING_CRITERIA_TYPES,
  VALUE_MATCH_STRATEGIES,
} from '@/types/hr/recruitment';
import type {
  MandatoryMatchMode,
  ShortlistingComparisonOperator,
  ShortlistingCriteria,
  ShortlistingCriteriaForm,
  ShortlistingCriteriaShape,
  ShortlistingCriteriaType,
  ShortlistingCriteriaValueInput,
  ValueMatchStrategy,
} from '@/types/hr/recruitment';

// ── what each criterion type is made of ─────────────────────────────────────
// Round 3, lane K: read from the SERVER (`GET job-vacancies/criteria/shapes`), the same table
// the service enforces, so this panel cannot drift from the engine again. The map below is only
// the label fallback while the shapes load.
const TYPE_LABELS: Record<ShortlistingCriteriaType, string> = {
  Qualification: 'Qualification',
  EducationLevel: 'Education level',
  Skill: 'Skill',
  Certification: 'Certification',
  Language: 'Language',
  YearsOfExperience: 'Years of experience',
  Age: 'Age',
  Gender: 'Gender',
  Location: 'Location',
  Other: 'Other',
};

/** A picked value in the editor: a catalogue row (id + name) or typed text. */
type PickedValue = { referenceId: string | null; label: string };

const blank = (): ShortlistingCriteriaForm => ({
  criteriaName: '',
  description: '',
  type: 'Qualification',
  requiredValue: '',
  minValue: null,
  maxValue: null,
  isMandatory: true,
  matchMode: 'AnyMatched',
  // ⚠ Exact, matching the server's default (R-5 fix 9). The panel used to default to Contains
  // while the service defaulted to Exact, so a row saved without touching the dropdown was
  // scored one way and displayed another.
  matchStrategy: 'Exact',
  requiredSkillId: null,
  requiredQualificationId: null,
  // ⚠ Never null. `Weight` is a non-nullable int on both DTOs, so `null` is rejected 400 by the
  // JSON reader before the handler runs — which is exactly what the old form did.
  weight: 1,
  comparisonOperator: null,
  values: [],
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
  matchStrategy: c.matchStrategy ?? 'Exact',
  requiredSkillId: c.requiredSkillId ?? null,
  requiredQualificationId: c.requiredQualificationId ?? null,
  weight: c.weight ?? 1,
  comparisonOperator: c.comparisonOperator ?? null,
  values: c.values ?? [],
});

/** The rows the editor starts from: the value rows, or the legacy comma list when there are none. */
const initialPicked = (c: ShortlistingCriteria | null): PickedValue[] => {
  if (!c) return [];
  if (c.values && c.values.length > 0) {
    return c.values.map((v) => ({ referenceId: v.referenceId ?? null, label: v.label }));
  }
  return (c.requiredValue ?? '')
    .split(',')
    .map((v) => v.trim())
    .filter(Boolean)
    .map((label) => ({ referenceId: null, label }));
};

/**
 * What an application is scored against for this vacancy.
 *
 * ⚠ Changing the criteria **marks every already-scored application stale** server-side, so the
 * scores are not silently left standing against a different bar. Worth knowing before editing a
 * vacancy that already has applicants — which is why the panel says so rather than leaving it to
 * be discovered.
 *
 * Round 3, lane K (register rows R-5, R-8; decision D-7): the accepted values come from the HR
 * setups — the skill, qualification, certification and language catalogues, the gender register
 * — as rows, not a comma-separated box; Gender and Age can never be mandatory and the panel says
 * why; Other is not auto-evaluated and cannot be mandatory either.
 */
export function VacancyCriteriaPanel({
  vacancyId,
  canManage,
  canRemove,
  usesProtectedCharacteristic = false,
}: {
  vacancyId: string;
  canManage: boolean;
  /**
   * Removing a criterion is a tier above adding one.
   *
   * DELETE api/job-vacancies/criteria/{id} is gated on RecruitmentAdminPolicy while the POST and
   * PUT beside it take RecruitmentWritePolicy, so an HR user who can add a criterion cannot
   * delete one. Hide Remove rather than answer 403 with a toast explaining nothing.
   */
  canRemove: boolean;
  /** The vacancy read's derived flag (D-7): a Gender or Age criterion is on file. */
  usesProtectedCharacteristic?: boolean;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<ShortlistingCriteria | null>(null);
  const [form, setForm] = useState<ShortlistingCriteriaForm>(blank);
  const [picked, setPicked] = useState<PickedValue[]>([]);
  const [typedValue, setTypedValue] = useState('');
  const [pickerChoice, setPickerChoice] = useState<string>('');

  const criteria = useQuery({
    queryKey: ['hr', 'vacancy-criteria', vacancyId],
    queryFn: () => jobVacancyService.getCriteria(vacancyId),
    enabled: !!vacancyId,
  });

  const shapes = useQuery({
    queryKey: ['hr', 'vacancy-criteria', 'shapes'],
    queryFn: () => jobVacancyService.getCriteriaShapes(),
    staleTime: 60 * 60 * 1000,
  });
  const shapeOf = (type: ShortlistingCriteriaType | 0): ShortlistingCriteriaShape | undefined =>
    typeof type === 'string' ? shapes.data?.find((s) => s.type === type) : undefined;
  const shape = shapeOf(form.type);
  const valueKind = shape?.valueKind ?? null;

  // Only fetched when a catalogue-backed kind is selected — a vacancy screen should not pull
  // 186 qualifications to render a years-of-experience criterion.
  const skills = useQuery({
    queryKey: ['hr', 'skills', 'active'],
    queryFn: () => skillService.getActive(),
    enabled: open && valueKind === 'Skill',
  });
  const qualifications = useQuery({
    queryKey: ['hr', 'qualifications', 'active'],
    queryFn: () => qualificationService.getActive(),
    enabled: open && valueKind === 'Qualification',
  });
  const certifications = useQuery({
    queryKey: ['hr', 'certifications', 'active'],
    queryFn: () => certificationService.getAll({ activeOnly: true }),
    enabled: open && valueKind === 'Certification',
  });
  const languages = useQuery({
    queryKey: ['hr', 'languages', 'active'],
    queryFn: () => languageService.getActive(),
    enabled: open && valueKind === 'Language',
  });

  const catalogue: { id: string; name: string }[] = useMemo(() => {
    switch (valueKind) {
      case 'Skill':
        return (skills.data ?? []).map((s) => ({ id: s.id, name: s.name }));
      case 'Qualification':
        return (qualifications.data ?? []).map((q) => ({ id: q.id, name: q.name }));
      case 'Certification':
        return (certifications.data ?? []).map((c) => ({ id: c.id, name: c.name }));
      case 'Language':
        return (languages.data ?? []).map((l) => ({ id: l.id, name: l.name }));
      default:
        return [];
    }
  }, [valueKind, skills.data, qualifications.data, certifications.data, languages.data]);
  const catalogueLoading =
    (valueKind === 'Skill' && skills.isLoading) ||
    (valueKind === 'Qualification' && qualifications.isLoading) ||
    (valueKind === 'Certification' && certifications.isLoading) ||
    (valueKind === 'Language' && languages.isLoading);

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'vacancy-criteria', vacancyId] });

  const addPicked = (value: PickedValue) => {
    const label = value.label.trim();
    if (!label && !value.referenceId) return;
    setPicked((rows) =>
      rows.some((r) => (value.referenceId ? r.referenceId === value.referenceId : r.label.toLowerCase() === label.toLowerCase()))
        ? rows
        : [...rows, { referenceId: value.referenceId, label }],
    );
  };
  const removePicked = (index: number) => setPicked((rows) => rows.filter((_, i) => i !== index));

  const payload = (): ShortlistingCriteriaForm => {
    const isNumeric = !!shape?.isNumeric;
    const values: ShortlistingCriteriaValueInput[] = isNumeric
      ? []
      : picked.map((v) => ({ referenceId: v.referenceId, label: v.referenceId ? null : v.label }));
    return {
      ...form,
      criteriaName: form.criteriaName.trim(),
      description: form.description?.trim() || null,
      // The value rows are the truth; the server mirrors their labels into requiredValue.
      requiredValue: null,
      values,
      minValue: isNumeric ? form.minValue : null,
      maxValue: isNumeric ? form.maxValue : null,
      // The legacy single-id columns are superseded by the value rows; never resend them.
      requiredSkillId: null,
      requiredQualificationId: null,
      comparisonOperator: (shape?.operators?.length ?? 0) > 0 ? form.comparisonOperator : null,
      // A type that may not be mandatory is saved as desirable whatever the box held.
      isMandatory: shape?.allowsMandatory === false ? false : form.isMandatory,
      weight: Number.isFinite(form.weight) && form.weight > 0 ? form.weight : 1,
    };
  };

  const save = useMutation({
    mutationFn: () =>
      editing
        ? jobVacancyService.updateCriteria(editing.id, payload())
        : jobVacancyService.addCriteria(vacancyId, payload()),
    onSuccess: async () => {
      await refresh();
      await queryClient.invalidateQueries({ queryKey: ['hr', 'vacancies', vacancyId] });
      setOpen(false);
      setEditing(null);
      setForm(blank());
      setPicked([]);
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
      await queryClient.invalidateQueries({ queryKey: ['hr', 'vacancies', vacancyId] });
      toast({ title: 'Criterion removed' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not remove it', description: e?.data?.message ?? e?.message, variant: 'destructive' }),
  });

  const rows = criteria.data ?? [];
  const untyped = useMemo(() => rows.filter((c) => typeof c.type !== 'string'), [rows]);
  const protectedRows = useMemo(
    () => rows.filter((c) => shapeOf(c.type)?.isProtectedCharacteristic),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [rows, shapes.data],
  );
  const showProtectedNote = usesProtectedCharacteristic || protectedRows.length > 0;

  const startAdd = () => {
    setEditing(null);
    setForm(blank());
    setPicked([]);
    setTypedValue('');
    setPickerChoice('');
    setOpen(true);
  };

  const startEdit = (c: ShortlistingCriteria) => {
    setEditing(c);
    setForm(toForm(c));
    setPicked(initialPicked(c));
    setTypedValue('');
    setPickerChoice('');
    setOpen(true);
  };

  /** What the criterion measures, in the row — the column the panel never used to have. */
  const measures = (c: ShortlistingCriteria) => {
    if (typeof c.type !== 'string') return 'Not set';
    const s = shapeOf(c.type);
    const label = s?.label ?? TYPE_LABELS[c.type];
    if (s?.isNumeric) {
      const op = c.comparisonOperatorName ?? 'Between';
      return `${label} · ${op} ${c.minValue ?? '—'}${c.maxValue != null ? `–${c.maxValue}` : ''}`;
    }
    const labels = c.values?.length ? c.values.map((v) => v.label) : (c.requiredValue ?? '').split(',').map((v) => v.trim()).filter(Boolean);
    if (labels.length > 0) return `${label} · ${labels.join(', ')}`;
    if (c.requiredQualificationId || c.requiredSkillId) return `${label} · from the catalogue`;
    return label;
  };

  const valuesAreCatalogue = valueKind === 'Skill' || valueKind === 'Qualification' || valueKind === 'Certification' || valueKind === 'Language';
  const mandatoryBlocked = shape?.allowsMandatory === false;

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-3">
        <div>
          <CardTitle className="text-base">Shortlisting criteria</CardTitle>
          <p className="mt-1 text-sm text-muted-foreground">
            Changing these marks any application already scored as stale, so nothing is judged
            against a bar that has since moved. A vacancy with no criteria scores nobody, and
            nobody is auto-shortlisted from it.
          </p>
        </div>
        {canManage && (
          <Button size="sm" onClick={startAdd}>
            <Plus className="mr-2 h-4 w-4" /> Add a criterion
          </Button>
        )}
      </CardHeader>
      <CardContent className="p-0">
        {showProtectedNote && (
          <div className="px-6 pb-3">
            {/* Decision D-7: the derived flag the vacancy read carries, said in words. */}
            <Alert>
              <ShieldAlert className="h-4 w-4" />
              <AlertDescription>
                <strong>This vacancy uses a protected-characteristic criterion</strong> (gender or
                age). Such a criterion may inform a score; it can never be mandatory and never
                disqualifies anyone. Keep a lawful, job-related reason on file for it.
              </AlertDescription>
            </Alert>
          </div>
        )}
        {untyped.length > 0 && (
          <div className="px-6 pb-3">
            <Alert variant="destructive">
              <AlertTriangle className="h-4 w-4" />
              <AlertDescription>
                {untyped.length === 1 ? 'One criterion does' : `${untyped.length} criteria do`} not
                say what {untyped.length === 1 ? 'it measures' : 'they measure'}, so{' '}
                {untyped.length === 1 ? 'it is' : 'they are'} not auto-evaluated and{' '}
                <strong>{untyped.length === 1 ? 'it counts' : 'they count'} for nothing</strong>. Open{' '}
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
                      <span>
                        {measures(c)}
                        {shapeOf(c.type)?.isProtectedCharacteristic && (
                          <Badge variant="outline" className="ml-2">Protected characteristic</Badge>
                        )}
                        {shapeOf(c.type)?.isAutoEvaluated === false && (
                          <Badge variant="outline" className="ml-2">Not auto-evaluated</Badge>
                        )}
                      </span>
                    ) : (
                      <Badge variant="destructive">Not set — counts for nothing</Badge>
                    )}
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {typeof c.type === 'string' && shapeOf(c.type)?.isList && shapeOf(c.type)?.valueKind !== 'Gender'
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
                onValueChange={(v) => {
                  const next = v as ShortlistingCriteriaType;
                  const nextShape = shapeOf(next);
                  setForm({
                    ...form,
                    type: next,
                    comparisonOperator: null,
                    isMandatory: nextShape?.allowsMandatory === false ? false : form.isMandatory,
                  });
                  // Values belong to a kind; a new kind starts empty.
                  if (nextShape?.valueKind !== valueKind) setPicked([]);
                  setPickerChoice('');
                }}
              >
                <SelectTrigger id="criteriaType">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {SHORTLISTING_CRITERIA_TYPES.map((t) => (
                    <SelectItem key={t} value={t}>
                      {shapeOf(t)?.label ?? TYPE_LABELS[t]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {shape?.hint && <p className="text-xs text-muted-foreground">{shape.hint}</p>}
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

            {/* ── accepted values (R-8): from the setup for a catalogue kind, the register for
                   Gender, typed for Location and Other ─────────────────────────────────── */}
            {shape && !shape.isNumeric && (
              <div className="space-y-2 rounded-md border p-3">
                <Label>
                  Accepted values
                  {shape.requiresValues && <span className="ml-0.5 text-red-500">*</span>}
                </Label>
                {picked.length > 0 && (
                  <div className="flex flex-wrap gap-1.5">
                    {picked.map((v, i) => (
                      <Badge key={`${v.referenceId ?? 'text'}:${v.label}`} variant="secondary" className="gap-1 pr-1">
                        {v.label}
                        <button
                          type="button"
                          className="rounded-full hover:bg-muted"
                          aria-label={`Remove ${v.label}`}
                          onClick={() => removePicked(i)}
                        >
                          <X className="h-3 w-3" />
                        </button>
                      </Badge>
                    ))}
                  </div>
                )}
                {valuesAreCatalogue && (
                  <Select
                    value={pickerChoice || NONE}
                    onValueChange={(v) => {
                      if (v === NONE) return;
                      const row = catalogue.find((c) => c.id === v);
                      if (row) addPicked({ referenceId: row.id, label: row.name });
                      setPickerChoice('');
                    }}
                  >
                    <SelectTrigger id="criteriaValuePicker">
                      <SelectValue placeholder={catalogueLoading ? 'Loading the catalogue…' : `Add a ${shape.label.toLowerCase()} from the setup`} />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value={NONE}>{catalogueLoading ? 'Loading…' : `Add a ${shape.label.toLowerCase()} from the setup`}</SelectItem>
                      {catalogue
                        .filter((c) => !picked.some((p) => p.referenceId === c.id))
                        .map((c) => (
                          <SelectItem key={c.id} value={c.id}>
                            {c.name}
                          </SelectItem>
                        ))}
                    </SelectContent>
                  </Select>
                )}
                {valueKind === 'Gender' && (
                  <div className="flex flex-wrap gap-4">
                    {[...GENDERS, 'Any'].map((g) => {
                      const on = picked.some((p) => p.label.toLowerCase() === g.toLowerCase());
                      return (
                        <label key={g} className="flex items-center gap-2 text-sm">
                          <Checkbox
                            checked={on}
                            onCheckedChange={(c) =>
                              c === true
                                ? addPicked({ referenceId: null, label: g })
                                : setPicked((rows) => rows.filter((r) => r.label.toLowerCase() !== g.toLowerCase()))
                            }
                          />
                          {g === 'PreferNotToSay' ? 'Prefer not to say' : g}
                        </label>
                      );
                    })}
                  </div>
                )}
                {valueKind === 'Text' && (
                  <div className="flex gap-2">
                    <Input
                      id="criteriaTypedValue"
                      value={typedValue}
                      placeholder={form.type === 'Location' ? 'A city, e.g. Kumasi' : 'A value'}
                      onChange={(e) => setTypedValue(e.target.value)}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter') {
                          e.preventDefault();
                          addPicked({ referenceId: null, label: typedValue });
                          setTypedValue('');
                        }
                      }}
                    />
                    <Button
                      type="button"
                      variant="outline"
                      onClick={() => {
                        addPicked({ referenceId: null, label: typedValue });
                        setTypedValue('');
                      }}
                      disabled={!typedValue.trim()}
                    >
                      Add
                    </Button>
                  </div>
                )}
                {shape.requiresValues && picked.length === 0 && (
                  <p className="text-xs text-muted-foreground">
                    At least one value is needed — a blank criterion would pass every candidate.
                  </p>
                )}
              </div>
            )}

            {shape?.isList && valueKind !== 'Gender' && valueKind !== 'Text' && (
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
                  <Label htmlFor="matchStrategy">How a typed name is compared</Label>
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
                  <p className="text-xs text-muted-foreground">A catalogue link matches by row first; this decides the name fallback.</p>
                </div>
              </div>
            )}

            {shape?.isNumeric && (
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

            {(shape?.operators?.length ?? 0) > 0 && (
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
                      {shape?.isNumeric ? 'Default — between the bounds' : 'Default — contains'}
                    </SelectItem>
                    {(shape?.operators ?? []).map((op) => (
                      <SelectItem key={op} value={op}>
                        {op.replace(/([a-z])([A-Z])/g, '$1 $2')}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}

            {!shape?.isNumeric && (
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
                  {shape?.isAutoEvaluated === false && ' Not applied — this type is not auto-evaluated.'}
                </p>
              </div>
            )}

            <div className="space-y-1.5">
              <div className="flex items-center gap-2">
                <Checkbox
                  id="isMandatory"
                  checked={mandatoryBlocked ? false : form.isMandatory}
                  disabled={mandatoryBlocked}
                  onCheckedChange={(c) => setForm({ ...form, isMandatory: c === true })}
                />
                <Label htmlFor="isMandatory" className="font-normal">
                  Mandatory — an application that fails this is not shortlistable
                </Label>
              </div>
              {mandatoryBlocked && (
                <p className="text-xs text-muted-foreground">{shape?.mandatoryRefusal}</p>
              )}
              {shape?.isProtectedCharacteristic && (
                <p className="text-xs text-muted-foreground">
                  A protected characteristic. It informs the score only, and the vacancy will show
                  that it uses one.
                </p>
              )}
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => save.mutate()}
              disabled={
                !form.criteriaName.trim() ||
                save.isPending ||
                (!!shape?.requiresValues && picked.length === 0) ||
                (!!shape?.isNumeric && form.minValue == null && form.maxValue == null)
              }
            >
              {save.isPending ? 'Saving…' : editing ? 'Save' : 'Add'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
