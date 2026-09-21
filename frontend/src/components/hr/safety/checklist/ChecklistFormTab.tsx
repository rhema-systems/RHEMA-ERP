'use client';

import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useToast } from '@/components/ui/use-toast';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyChecklistService } from '@/services/hr/safety-checklist.service';
import {
  SHE_INSPECTION_TYPE_OPTIONS,
  SHE_CHECKLIST_SCORING_MODE_OPTIONS,
  type SheInspectionChecklist,
} from '@/types/hr/safety-inspections';

/**
 * The template header: name / description / active (always editable) and the structural columns —
 * type, version, scoring mode, partial-compliance, print texts — which freeze once published. On a
 * locked template those are shown read-only and re-sent unchanged so the server accepts the update.
 */
const schema = z.object({
  name: z.string().min(1, 'A name is required').max(200),
  description: z.string().max(500).optional().or(z.literal('')),
  type: z.enum(['Routine', 'Planned', 'Unplanned', 'FollowUp', 'PreTask', 'PostIncident', 'Regulatory', 'Management']),
  version: z.coerce.number().min(1).max(999),
  isActive: z.boolean(),
  scoringMode: z.enum(['None', 'CompliancePercentage', 'QualitativeRating']),
  allowPartialCompliance: z.boolean(),
  printTitle: z.string().max(200).optional().or(z.literal('')),
  printSubtitle: z.string().max(200).optional().or(z.literal('')),
  instructions: z.string().max(4000).optional().or(z.literal('')),
  criticalSectionNote: z.string().max(500).optional().or(z.literal('')),
});
type Form = z.input<typeof schema>;

const blank = (v?: string) => (v && v.length > 0 ? v : null);

const toForm = (c: SheInspectionChecklist): Form => ({
  name: c.name,
  description: c.description ?? '',
  type: c.type,
  version: c.version,
  isActive: c.isActive,
  scoringMode: c.scoringMode,
  allowPartialCompliance: c.allowPartialCompliance,
  printTitle: c.printTitle ?? '',
  printSubtitle: c.printSubtitle ?? '',
  instructions: c.instructions ?? '',
  criticalSectionNote: c.criticalSectionNote ?? '',
});

function ReadOnlyRow({ label, value }: { label: string; value?: string | number | boolean | null }) {
  const text = value === true ? 'Yes' : value === false ? 'No' : value == null || value === '' ? '—' : String(value);
  return (
    <div className="flex items-baseline justify-between gap-4 border-b py-1.5 text-sm">
      <span className="text-muted-foreground">{label}</span>
      <span className="text-right font-medium whitespace-pre-wrap">{text}</span>
    </div>
  );
}

export function ChecklistFormTab({ checklist, onSaved }: { checklist: SheInspectionChecklist; onSaved: () => void }) {
  const { toast } = useToast();
  const [saving, setSaving] = useState(false);
  const locked = checklist.isStructureLocked;
  const form = useForm<Form>({ resolver: zodResolver(schema) as any, defaultValues: toForm(checklist) });

  useEffect(() => {
    form.reset(toForm(checklist));
  }, [checklist.id, checklist.updatedAt]);

  const onSubmit = async (values: Form) => {
    const v = schema.parse(values);
    setSaving(true);
    try {
      await safetyChecklistService.update(checklist.id, {
        id: checklist.id,
        name: v.name,
        description: blank(v.description),
        isActive: v.isActive,
        // Structural columns: what the form holds on a draft, what the server holds on a locked row.
        type: locked ? checklist.type : v.type,
        version: locked ? checklist.version : v.version,
        scoringMode: locked ? checklist.scoringMode : v.scoringMode,
        allowPartialCompliance: locked ? checklist.allowPartialCompliance : v.allowPartialCompliance,
        printTitle: locked ? checklist.printTitle ?? null : blank(v.printTitle),
        printSubtitle: locked ? checklist.printSubtitle ?? null : blank(v.printSubtitle),
        instructions: locked ? checklist.instructions ?? null : blank(v.instructions),
        criticalSectionNote: locked ? checklist.criticalSectionNote ?? null : blank(v.criticalSectionNote),
      });
      toast({ title: 'Saved', description: `${checklist.checklistNumber} v${checklist.version} updated.` });
      onSaved();
    } catch (e: any) {
      toast({ title: 'Error', description: e?.message || 'Failed to save the template.', variant: 'destructive' });
    } finally {
      setSaving(false);
    }
  };

  const scoringName = SHE_CHECKLIST_SCORING_MODE_OPTIONS.find((o) => o.value === checklist.scoringMode)?.label;

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Template</CardTitle>
            <CardDescription>Name, description and whether it is offered for new inspections.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <TextField form={form} name="name" label="Name" required />
            <TextareaField form={form} name="description" label="Description" rows={2} />
            {locked ? (
              <>
                <ReadOnlyRow label="Inspection type" value={checklist.typeName} />
                <ReadOnlyRow label="Version" value={`v${checklist.version}`} />
              </>
            ) : (
              <FieldRow>
                <SelectField form={form} name="type" label="Inspection type" required options={SHE_INSPECTION_TYPE_OPTIONS} />
                <NumberField form={form} name="version" label="Version" required />
              </FieldRow>
            )}
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="Inactive templates stay on past inspections but are not offered for new ones."
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Scoring</CardTitle>
            <CardDescription>
              How a run is summarised. Percentage mode needs outcome bands; qualitative mode needs at least two outcomes.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {locked ? (
              <>
                <ReadOnlyRow label="Scoring mode" value={scoringName} />
                <ReadOnlyRow label="Allow “Partially compliant”" value={checklist.allowPartialCompliance} />
              </>
            ) : (
              <>
                <SelectField form={form} name="scoringMode" label="Scoring mode" required options={SHE_CHECKLIST_SCORING_MODE_OPTIONS} />
                <SwitchField
                  form={form}
                  name="allowPartialCompliance"
                  label="Allow “Partially compliant”"
                  description="Adds a P column. A partial answer counts as not compliant in the percentage and is reported separately."
                />
              </>
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Printed form</CardTitle>
          <CardDescription>What the paper says at the top and the bottom. See the Preview tab for the result.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {locked ? (
            <>
              <ReadOnlyRow label="Title" value={checklist.printTitle} />
              <ReadOnlyRow label="Subtitle" value={checklist.printSubtitle} />
              <ReadOnlyRow label="Critical section note" value={checklist.criticalSectionNote} />
              <ReadOnlyRow label="Instructions / footer" value={checklist.instructions} />
            </>
          ) : (
            <>
              <FieldRow>
                <TextField form={form} name="printTitle" label="Title" placeholder="e.g. FOOD VENDOR SCREENING & INSPECTION CHECKLIST" />
                <TextField form={form} name="printSubtitle" label="Subtitle" placeholder="e.g. Community 27 Construction Site" />
              </FieldRow>
              <TextField form={form} name="criticalSectionNote" label="Critical section note" placeholder="e.g. Tick where applicable." />
              <TextareaField
                form={form}
                name="instructions"
                label="Instructions / footer"
                rows={5}
              />
            </>
          )}
        </CardContent>
      </Card>

      <div className="flex justify-end">
        <Button type="submit" disabled={saving}>
          {saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
          Save
        </Button>
      </div>
    </form>
  );
}
