'use client';

import { useEffect, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, CheckCircle2, ClipboardCheck, Loader2, PenLine, Save, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
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
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import {
  TextField,
  TextareaField,
  DateField,
  TimeField,
  SelectField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { safetyInspectionService } from '@/services/hr/safety-inspection.service';
import { locationService } from '@/services/hr/location.service';
import { SHE_RISK_LEVEL_OPTIONS } from '@/types/hr/safety-hazards';
import type {
  SafetyInspection,
  SafetyInspectionItem,
  SheComplianceStatus,
  SheInspectionChecklistField,
  SheInspectionChecklistSignatory,
} from '@/types/hr/safety-inspections';
import type { SheRiskLevel } from '@/types/hr/safety-hazards';
import {
  CRITICAL_CHOICES,
  answerFromItem,
  computeLocalScore,
  fmtPct,
  standardChoices,
  type LocalAnswer,
} from './checklist-scoring';
import { cn } from '@/lib/utils';
import { OrganizationUnitPickerField } from '@/components/hr/common/OrganizationUnitPickerField';

/**
 * The walk (docs/HR/HR-SHE-INSPECTION-CHECKLIST-BUILDER-DESIGN.md §5): header fields, every section
 * with a C / NC / NA (or Yes / No) control per item and a remarks box, a live score, the outcome
 * + Complete gate, and the signature block. Answers are held locally and saved in one call; the
 * server recomputes everything on Complete. Once completed the answers are read-only.
 */
interface Props {
  inspection: SafetyInspection;
  onChanged: () => Promise<unknown> | void;
}

const REFERENCE_KINDS = new Set(['Employee', 'Location', 'OrganizationUnit']);
const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '');

const initialFieldForm = (inspection: SafetyInspection): Record<string, string> => {
  const out: Record<string, string> = {};
  for (const f of inspection.checklist?.fields ?? []) {
    const v = inspection.fieldValues.find((x) => x.checklistFieldId === f.id);
    out[f.id] = (REFERENCE_KINDS.has(f.fieldType) ? v?.valueReferenceId : v?.valueText) ?? '';
  }
  return out;
};

export function InspectionChecklistRun({ inspection, onChanged }: Props) {
  const { toast } = useToast();
  // The page only renders this tab when the inspection carries a template.
  const checklist = inspection.checklist as NonNullable<SafetyInspection['checklist']>;
  const locked = !!inspection.completedAt || inspection.status === 'Closed';
  const choices = standardChoices(checklist.allowPartialCompliance);

  // ── local answers ──
  const materialised = useMemo(() => inspection.items.filter((i) => i.checklistItemId), [inspection.items]);
  const byTemplateItem = useMemo(
    () => new Map(materialised.map((i) => [i.checklistItemId ?? '', i])),
    [materialised],
  );
  const [answers, setAnswers] = useState<Record<string, LocalAnswer>>({});
  const [answersDirty, setAnswersDirty] = useState(false);
  useEffect(() => {
    setAnswers(Object.fromEntries(materialised.map((i) => [i.id, answerFromItem(i)])));
    setAnswersDirty(false);
  }, [materialised, inspection.updatedAt]);

  const setAnswer = (item: SafetyInspectionItem, patch: Partial<LocalAnswer>) => {
    setAnswers((prev) => ({ ...prev, [item.id]: { ...(prev[item.id] ?? answerFromItem(item)), ...patch } }));
    setAnswersDirty(true);
  };

  // ── header fields ──
  const fieldForm = useForm<Record<string, string>>({ defaultValues: initialFieldForm(inspection) });
  useEffect(() => {
    fieldForm.reset(initialFieldForm(inspection));
  }, [inspection.id, inspection.updatedAt]);
  const watchedFields = fieldForm.watch();
  const fieldValuesForScore = useMemo(() => {
    const out: Record<string, { text: string; ref: string }> = {};
    for (const f of checklist.fields) {
      const v = watchedFields[f.id] ?? '';
      out[f.id] = REFERENCE_KINDS.has(f.fieldType) ? { text: '', ref: v } : { text: v, ref: '' };
    }
    return out;
  }, [checklist.fields, watchedFields]);

  const needsLocations = checklist.fields.some((f) => f.fieldType === 'Location');
  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
    enabled: needsLocations,
  });

  const score = computeLocalScore(inspection, answers, fieldValuesForScore);

  // ── saves ──
  const [savingFields, setSavingFields] = useState(false);
  const [savingAnswers, setSavingAnswers] = useState(false);
  const fail = (e: any, fallback: string) =>
    toast({ title: 'Refused', description: e?.message || fallback, variant: 'destructive' });

  const saveFields = async () => {
    setSavingFields(true);
    try {
      const values = fieldForm.getValues();
      await safetyInspectionService.saveFieldValues(
        inspection.id,
        checklist.fields.map((f) => {
          const v = (values[f.id] ?? '').trim();
          return REFERENCE_KINDS.has(f.fieldType)
            ? { checklistFieldId: f.id, valueText: null, valueReferenceId: v || null }
            : { checklistFieldId: f.id, valueText: v || null, valueReferenceId: null };
        }),
      );
      toast({ title: 'Saved', description: 'Header fields saved.' });
      await onChanged();
    } catch (e: any) {
      fail(e, 'Failed to save the header fields.');
    } finally {
      setSavingFields(false);
    }
  };

  const saveAnswers = async () => {
    setSavingAnswers(true);
    try {
      await safetyInspectionService.saveResponses(
        inspection.id,
        materialised.map((i) => {
          const a = answers[i.id] ?? answerFromItem(i);
          return {
            itemId: i.id,
            status: a.status,
            deficiencyNoted: a.deficiencyNoted.trim() || null,
            actionRequired: a.actionRequired.trim() || null,
            riskLevel: a.riskLevel === '' ? null : a.riskLevel,
          };
        }),
      );
      toast({ title: 'Saved', description: 'Answers saved.' });
      await onChanged();
    } catch (e: any) {
      fail(e, 'Failed to save the answers.');
    } finally {
      setSavingAnswers(false);
    }
  };

  // ── complete ──
  const [completeOpen, setCompleteOpen] = useState(false);
  const [completing, setCompleting] = useState(false);
  const completeForm = useForm<{
    outcomeId: string;
    outcomeOverrideReason: string;
    subjectComments: string;
    findingsAndObservations: string;
    recommendedActions: string;
    overallRiskRating: string;
    nextInspectionDueDate: string;
  }>({
    defaultValues: {
      outcomeId: '',
      outcomeOverrideReason: '',
      subjectComments: inspection.subjectComments ?? '',
      findingsAndObservations: inspection.findingsAndObservations ?? '',
      recommendedActions: inspection.recommendedActions ?? '',
      overallRiskRating: inspection.overallRiskRating ?? '',
      nextInspectionDueDate: inspection.nextInspectionDueDate?.slice(0, 10) ?? '',
    },
  });
  const openComplete = () => {
    completeForm.reset({
      outcomeId: score?.recommendedOutcomeId ?? '',
      outcomeOverrideReason: '',
      subjectComments: inspection.subjectComments ?? '',
      findingsAndObservations: inspection.findingsAndObservations ?? '',
      recommendedActions: inspection.recommendedActions ?? '',
      overallRiskRating: inspection.overallRiskRating ?? '',
      nextInspectionDueDate: inspection.nextInspectionDueDate?.slice(0, 10) ?? '',
    });
    setCompleteOpen(true);
  };
  const chosenOutcomeId = completeForm.watch('outcomeId');
  const overriding = !!score?.recommendedOutcomeId && !!chosenOutcomeId && chosenOutcomeId !== score.recommendedOutcomeId;

  const submitComplete = completeForm.handleSubmit(async (v) => {
    setCompleting(true);
    try {
      const done = await safetyInspectionService.complete(inspection.id, {
        outcomeId: v.outcomeId || null,
        outcomeOverrideReason: v.outcomeOverrideReason.trim() || null,
        subjectComments: v.subjectComments.trim() || null,
        findingsAndObservations: v.findingsAndObservations.trim() || null,
        recommendedActions: v.recommendedActions.trim() || null,
        overallRiskRating: (v.overallRiskRating || null) as SheRiskLevel | null,
        nextInspectionDueDate: v.nextInspectionDueDate || null,
      });
      setCompleteOpen(false);
      toast({
        title: 'Inspection completed',
        description: `${done.inspectionNumber}: ${done.compliancePercentage != null ? `${fmtPct(done.compliancePercentage)} · ` : ''}${done.outcomeLabel ?? done.statusName}`,
      });
      await onChanged();
    } catch (e: any) {
      fail(e, 'The inspection could not be completed.');
    } finally {
      setCompleting(false);
    }
  });

  // ── signatures ──
  const [signing, setSigning] = useState<SheInspectionChecklistSignatory | null>(null);
  const [signBusy, setSignBusy] = useState(false);
  const [removingSignature, setRemovingSignature] = useState<string | null>(null);
  const signForm = useForm<{ signedName: string; signedAt: string; notes: string }>({
    defaultValues: { signedName: '', signedAt: '', notes: '' },
  });
  const openSign = (s: SheInspectionChecklistSignatory) => {
    signForm.reset({ signedName: '', signedAt: new Date().toISOString().slice(0, 10), notes: '' });
    setSigning(s);
  };
  const submitSign = signForm.handleSubmit(async (v) => {
    if (!signing) return;
    setSignBusy(true);
    try {
      await safetyInspectionService.addSignature(inspection.id, {
        checklistSignatoryId: signing.id,
        signedName: signing.kind === 'External' ? v.signedName.trim() : null,
        signedAt: v.signedAt ? new Date(v.signedAt).toISOString() : null,
        notes: v.notes.trim() || null,
      });
      setSigning(null);
      toast({ title: 'Signed', description: `${signing.roleLabel} recorded.` });
      await onChanged();
    } catch (e: any) {
      fail(e, 'The signature was not recorded.');
    } finally {
      setSignBusy(false);
    }
  });
  const removeSignature = async () => {
    if (!removingSignature) return;
    try {
      await safetyInspectionService.removeSignature(removingSignature);
      setRemovingSignature(null);
      await onChanged();
    } catch (e: any) {
      fail(e, 'The signature was not removed.');
    }
  };

  const canSign = inspection.status !== 'Scheduled' && inspection.status !== 'Closed';

  // ── render helpers ──
  const renderField = (f: SheInspectionChecklistField) => {
    const name = f.id;
    const label = f.isRequired ? `${f.label} *` : f.label;
    const existing = inspection.fieldValues.find((x) => x.checklistFieldId === f.id);
    switch (f.fieldType) {
      case 'LongText':
        return <TextareaField key={f.id} form={fieldForm} name={name} label={label} rows={2} />;
      case 'Date':
        return <DateField key={f.id} form={fieldForm} name={name} label={label} />;
      case 'Time':
        return <TimeField key={f.id} form={fieldForm} name={name} label={label} />;
      case 'YesNo':
        return (
          <SelectField key={f.id} form={fieldForm} name={name} label={label} allowEmpty options={[{ value: 'true', label: 'Yes' }, { value: 'false', label: 'No' }]} />
        );
      case 'Choice':
        return (
          <SelectField key={f.id} form={fieldForm} name={name} label={label} allowEmpty options={f.choices.map((c) => ({ value: c, label: c }))} />
        );
      case 'Employee':
        return <EmployeePickerField key={f.id} form={fieldForm} name={name} label={label} initialLabel={existing?.valueDisplay ?? null} />;
      case 'Location':
        return (
          <SelectField key={f.id} form={fieldForm} name={name} label={label} allowEmpty options={locations.map((l) => ({ value: l.id, label: l.name }))} />
        );
      case 'OrganizationUnit':
        return (
          <OrganizationUnitPickerField key={f.id} form={fieldForm} name={name} label={label} allowEmpty />
        );
      default:
        return <TextField key={f.id} form={fieldForm} name={name} label={label} placeholder={f.helpText ?? undefined} />;
    }
  };

  const ChoiceButtons = ({
    item,
    options,
  }: {
    item: SafetyInspectionItem;
    options: { value: SheComplianceStatus; label: string; short: string }[];
  }) => {
    const current = answers[item.id]?.status ?? item.status;
    return (
      <div className="flex gap-1">
        {options.map((o) => {
          const selected = current === o.value;
          const tone =
            o.value === 'Compliant' ? 'bg-green-600 hover:bg-green-600 text-white' :
            o.value === 'NonCompliant' ? 'bg-red-600 hover:bg-red-600 text-white' :
            o.value === 'PartiallyCompliant' ? 'bg-amber-500 hover:bg-amber-500 text-white' :
            'bg-neutral-500 hover:bg-neutral-500 text-white';
          return (
            <Button
              key={o.value}
              type="button"
              size="sm"
              variant="outline"
              title={o.label}
              disabled={locked}
              className={cn('min-w-[2.75rem] tabular-nums', selected && tone)}
              onClick={() => setAnswer(item, { status: selected ? 'NotAssessed' : o.value })}
            >
              {o.short}
            </Button>
          );
        })}
      </div>
    );
  };

  return (
    <div className="space-y-6">
      {locked ? (
        <p className="rounded-md bg-green-50 p-3 text-sm text-green-900 dark:bg-green-950 dark:text-green-200">
          <CheckCircle2 className="mr-1 inline h-4 w-4" />
          Completed {fmtDateTime(inspection.completedAt)}
          {inspection.completedByName ? ` by ${inspection.completedByName}` : ''}
          {inspection.compliancePercentage != null ? ` · ${fmtPct(inspection.compliancePercentage)} compliant` : ''}
          {inspection.outcomeLabel ? ` · ${inspection.outcomeLabel}` : ''}. Answers are locked; non-conformities are tracked on the Findings tab.
        </p>
      ) : null}

      <div className="grid gap-6 lg:grid-cols-3">
        {/* ── Header fields ── */}
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="text-base">Document information</CardTitle>
            <CardDescription>The form&apos;s header. Fields marked * must be filled before the inspection can be completed.</CardDescription>
          </CardHeader>
          <CardContent>
            {checklist.fields.length === 0 ? (
              <p className="text-muted-foreground text-sm">This template has no header fields.</p>
            ) : (
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  void saveFields();
                }}
                className="space-y-4"
              >
                <fieldset disabled={locked} className="grid gap-4 md:grid-cols-2">
                  {checklist.fields.map(renderField)}
                </fieldset>
                {!locked && (
                  <div className="flex justify-end">
                    <Button type="submit" variant="outline" size="sm" disabled={savingFields}>
                      {savingFields ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                      Save fields
                    </Button>
                  </div>
                )}
              </form>
            )}
          </CardContent>
        </Card>

        {/* ── Score ── */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Score</CardTitle>
            <CardDescription>
              {checklist.scoringMode === 'CompliancePercentage'
                ? 'Compliant ÷ applicable. N/A items are excluded; partial counts as not compliant.'
                : checklist.scoringMode === 'QualitativeRating'
                  ? 'The inspector chooses the overall rating on completion.'
                  : 'This template records findings without a score.'}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            {score ? (
              <>
                <Row label="Items" value={`${score.totalItems}`} />
                <Row label="Applicable" value={`${score.totalApplicableItems}`} />
                <Row label="Compliant" value={`${score.totalCompliantItems}`} />
                <Row label="Non-compliant" value={`${score.totalNonCompliantItems}${score.totalPartiallyCompliantItems ? ` (+${score.totalPartiallyCompliantItems} partial)` : ''}`} />
                <Row label="Not applicable" value={`${score.totalNotApplicableItems}`} />
                <Row label="Not yet assessed" value={`${score.totalNotAssessedItems}`} emphasis={score.totalNotAssessedItems > 0} />
                {checklist.sections.some((s) => s.kind === 'Critical') ? (
                  <Row label="Critical non-conformities" value={`${score.criticalNonConformityCount}`} emphasis={score.criticalNonConformityCount > 0} />
                ) : null}
                {checklist.scoringMode === 'CompliancePercentage' ? (
                  <div className="mt-2 flex items-baseline justify-between border-t pt-2">
                    <span className="text-muted-foreground">Compliance</span>
                    <span className="text-2xl font-semibold tabular-nums">{fmtPct(score.compliancePercentage)}</span>
                  </div>
                ) : null}
                {score.isDisqualified ? (
                  <p className="flex items-start gap-1 text-red-700 dark:text-red-400">
                    <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                    Critical non-conformity recorded — the outcome is forced to {score.recommendedOutcomeLabel ?? 'the disqualifying outcome'}.
                  </p>
                ) : score.recommendedOutcomeLabel ? (
                  <Row label="Recommended" value={score.recommendedOutcomeLabel} emphasis />
                ) : null}
                {score.missingRequiredFields.length > 0 ? (
                  <p className="text-muted-foreground">Required fields missing: {score.missingRequiredFields.join(', ')}</p>
                ) : null}
                {!locked && (
                  <div className="pt-2">
                    <Button
                      className="w-full"
                      disabled={!score.isReadyToComplete || answersDirty || fieldForm.formState.isDirty}
                      onClick={openComplete}
                      title={answersDirty || fieldForm.formState.isDirty ? 'Save your changes first' : undefined}
                    >
                      <ClipboardCheck className="mr-2 h-4 w-4" />
                      Complete inspection
                    </Button>
                    {(answersDirty || fieldForm.formState.isDirty) && (
                      <p className="text-muted-foreground mt-1 text-center text-xs">Save your answers and fields first.</p>
                    )}
                  </div>
                )}
              </>
            ) : null}
          </CardContent>
        </Card>
      </div>

      {/* ── Sections ── */}
      {checklist.sections.map((section) => {
        const critical = section.kind === 'Critical';
        const options = critical ? CRITICAL_CHOICES : choices;
        return (
          <Card key={section.id || section.title}>
            <CardHeader>
              <CardTitle className="text-base">
                {section.code ? `${section.code}. ` : ''}
                {section.title}
                {critical ? (
                  <Badge variant="destructive" className="ml-2 align-middle">
                    Critical — any Yes disqualifies
                  </Badge>
                ) : null}
              </CardTitle>
              {(section.description || (critical && checklist.criticalSectionNote)) && (
                <CardDescription>{section.description ?? checklist.criticalSectionNote}</CardDescription>
              )}
            </CardHeader>
            <CardContent className="space-y-3">
              {section.items.map((templateItem) => {
                const item = byTemplateItem.get(templateItem.id);
                if (!item) return null;
                const a = answers[item.id] ?? answerFromItem(item);
                const flagged = a.status === 'NonCompliant' || a.status === 'PartiallyCompliant';
                return (
                  <div key={item.id} className="grid gap-2 border-b pb-3 last:border-b-0 md:grid-cols-[3rem_1fr_auto]">
                    <div className="text-muted-foreground pt-1.5 text-sm tabular-nums">{critical ? '' : item.itemNumber || ''}</div>
                    <div className="space-y-2">
                      <div className="pt-1.5 text-sm">
                        {item.itemDescription}
                        {templateItem.regulatoryReference ? (
                          <span className="text-muted-foreground"> · {templateItem.regulatoryReference}</span>
                        ) : null}
                      </div>
                      <div className="grid gap-2 md:grid-cols-[1fr_1fr_10rem]">
                        <Input
                          placeholder="Remarks"
                          value={a.deficiencyNoted}
                          disabled={locked}
                          onChange={(e) => setAnswer(item, { deficiencyNoted: e.target.value })}
                        />
                        {flagged ? (
                          <>
                            <Input
                              placeholder="Action required"
                              value={a.actionRequired}
                              disabled={locked}
                              onChange={(e) => setAnswer(item, { actionRequired: e.target.value })}
                            />
                            <Select
                              value={a.riskLevel || '__none__'}
                              disabled={locked}
                              onValueChange={(v) => setAnswer(item, { riskLevel: v === '__none__' ? '' : (v as SheRiskLevel) })}
                            >
                              <SelectTrigger>
                                <SelectValue placeholder="Risk" />
                              </SelectTrigger>
                              <SelectContent>
                                <SelectItem value="__none__">Risk: not set</SelectItem>
                                {SHE_RISK_LEVEL_OPTIONS.map((o) => (
                                  <SelectItem key={o.value} value={o.value}>
                                    {o.label}
                                  </SelectItem>
                                ))}
                              </SelectContent>
                            </Select>
                          </>
                        ) : null}
                      </div>
                    </div>
                    <div className="pt-0.5">
                      <ChoiceButtons item={item} options={options} />
                    </div>
                  </div>
                );
              })}
            </CardContent>
          </Card>
        );
      })}

      {!locked && materialised.length > 0 && (
        <div className="sticky bottom-4 flex justify-end">
          <Button onClick={saveAnswers} disabled={savingAnswers || !answersDirty} className="shadow-lg">
            {savingAnswers ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
            Save answers
          </Button>
        </div>
      )}

      {/* ── Signatures ── */}
      {checklist.signatories.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Signatures</CardTitle>
            <CardDescription>
              System users sign as themselves; external parties are recorded by name. Signing opens once the walk has started.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-2">
            {checklist.signatories.map((s) => {
              const sig = inspection.signatures.find((x) => x.checklistSignatoryId === s.id);
              return (
                <div key={s.id} className="flex flex-wrap items-center justify-between gap-2 border-b py-2 text-sm last:border-b-0">
                  <div>
                    <span className="font-medium">{s.roleLabel}</span>{' '}
                    <span className="text-muted-foreground">
                      · {s.kind === 'SystemUser' ? 'system user' : 'external'}
                      {s.isRequired ? ' · required' : ''}
                    </span>
                    {sig ? (
                      <div className="text-muted-foreground">
                        Signed by <b className="text-foreground">{sig.signedName}</b> on {fmtDateTime(sig.signedAt)}
                        {sig.notes ? ` — ${sig.notes}` : ''}
                      </div>
                    ) : (
                      <div className="text-muted-foreground">Not signed</div>
                    )}
                  </div>
                  {sig ? (
                    inspection.status !== 'Closed' ? (
                      <Button variant="ghost" size="sm" className="text-red-600" onClick={() => setRemovingSignature(sig.id)}>
                        <Trash2 className="mr-1 h-4 w-4" />
                        Remove
                      </Button>
                    ) : null
                  ) : (
                    <Button variant="outline" size="sm" disabled={!canSign} onClick={() => openSign(s)}>
                      <PenLine className="mr-1 h-4 w-4" />
                      {s.kind === 'SystemUser' ? 'Sign as me' : 'Record signature'}
                    </Button>
                  )}
                </div>
              );
            })}
          </CardContent>
        </Card>
      )}

      {/* ── Complete dialog ── */}
      <Dialog open={completeOpen} onOpenChange={(o) => !completing && setCompleteOpen(o)}>
        <DialogContent className="max-w-2xl">
          <form onSubmit={submitComplete}>
            <DialogHeader>
              <DialogTitle>Complete {inspection.inspectionNumber}</DialogTitle>
              <DialogDescription>
                {checklist.scoringMode === 'CompliancePercentage'
                  ? `Score ${fmtPct(score?.compliancePercentage)}${score?.recommendedOutcomeLabel ? ` — the bands recommend “${score.recommendedOutcomeLabel}”.` : '.'} Confirm the outcome or give a reason to record another.`
                  : checklist.scoringMode === 'QualitativeRating'
                    ? 'Choose the overall rating and record the closing comments.'
                    : 'Record the closing comments. Non-conformities become findings to close out.'}
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-4 py-4">
              {checklist.scoringMode !== 'None' && (
                <SelectField
                  form={completeForm}
                  name="outcomeId"
                  label={checklist.scoringMode === 'QualitativeRating' ? 'Overall rating' : 'Outcome'}
                  required
                  options={checklist.outcomes.map((o) => ({
                    value: o.id,
                    label: `${o.label}${o.id === score?.recommendedOutcomeId ? ' (recommended)' : ''}${o.reinspectionWithinDays ? ` · re-inspect within ${o.reinspectionWithinDays} days` : ''}`,
                  }))}
                />
              )}
              {overriding && (
                <TextField form={completeForm} name="outcomeOverrideReason" label="Reason for not following the recommendation" required />
              )}
              <TextareaField form={completeForm} name="findingsAndObservations" label="Inspector's comments" rows={3} />
              <TextareaField form={completeForm} name="recommendedActions" label="Recommended actions" rows={2} />
              <TextareaField form={completeForm} name="subjectComments" label="Inspected party's comments (vendor / operator)" rows={2} />
              <FieldRow>
                <SelectField form={completeForm} name="overallRiskRating" label="Overall risk" allowEmpty options={SHE_RISK_LEVEL_OPTIONS} />
                <DateField form={completeForm} name="nextInspectionDueDate" label="Next inspection due" />
              </FieldRow>
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setCompleteOpen(false)} disabled={completing}>
                Cancel
              </Button>
              <Button type="submit" disabled={completing}>
                {completing && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Complete inspection
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── Sign dialog ── */}
      <Dialog open={!!signing} onOpenChange={(o) => !o && !signBusy && setSigning(null)}>
        <DialogContent>
          <form onSubmit={submitSign}>
            <DialogHeader>
              <DialogTitle>{signing?.roleLabel}</DialogTitle>
              <DialogDescription>
                {signing?.kind === 'SystemUser'
                  ? 'You sign as yourself — the logged-in employee is recorded with the time.'
                  : 'Record the external party who signed the paper form.'}
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-4 py-4">
              {signing?.kind === 'External' && (
                <TextField form={signForm} name="signedName" label="Name of signatory" required />
              )}
              <DateField form={signForm} name="signedAt" label="Date signed" />
              <div className="space-y-2">
                <Label htmlFor="sign-notes">Notes</Label>
                <Textarea id="sign-notes" rows={2} {...signForm.register('notes')} />
              </div>
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setSigning(null)} disabled={signBusy}>
                Cancel
              </Button>
              <Button type="submit" disabled={signBusy}>
                {signBusy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {signing?.kind === 'SystemUser' ? 'Sign' : 'Record'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={!!removingSignature}
        onOpenChange={(o) => !o && setRemovingSignature(null)}
        title="Remove this signature?"
        description="The signatory can sign again afterwards."
        confirmText="Remove"
        variant="destructive"
        onConfirm={removeSignature}
      />
    </div>
  );
}

function Row({ label, value, emphasis }: { label: string; value: string; emphasis?: boolean }) {
  return (
    <div className="flex items-baseline justify-between gap-4 border-b py-1">
      <span className="text-muted-foreground">{label}</span>
      <span className={cn('tabular-nums', emphasis && 'font-semibold')}>{value}</span>
    </div>
  );
}
