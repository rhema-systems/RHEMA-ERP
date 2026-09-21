'use client';

import Link from 'next/link';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
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
 * The inspection checklist templates. The rows here are headers — items are authored on the
 * checklist's own page, and the number is unique per tenant (a duplicate is refused with 422).
 */
const checklistSchema = z.object({
  checklistNumber: z.string().min(1, 'A number is required').max(30),
  name: z.string().min(1, 'A name is required').max(200),
  description: z.string().max(500).optional().or(z.literal('')),
  type: z.enum(['Routine', 'Planned', 'Unplanned', 'FollowUp', 'PreTask', 'PostIncident', 'Regulatory', 'Management']),
  version: z.coerce.number().min(1).max(999),
  isActive: z.boolean(),
  scoringMode: z.enum(['None', 'CompliancePercentage', 'QualitativeRating']),
  printTitle: z.string().max(200).optional().or(z.literal('')),
});

type ChecklistForm = z.input<typeof checklistSchema>;

const emptyChecklist: ChecklistForm = {
  checklistNumber: '',
  name: '',
  description: '',
  type: 'Routine',
  version: 1,
  isActive: true,
  scoringMode: 'CompliancePercentage',
  printTitle: '',
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);

export default function SafetyChecklistsPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Inspection Checklists"
        description="Reusable checklist templates that inspections are conducted against. Open a checklist to author its items."
        backHref="/administration/safety"
      />

      <ResourceListPanel<SheInspectionChecklist, ChecklistForm>
        title="checklists"
        singular="checklist"
        queryKey={['hr', 'safety-checklists']}
        dialogHint="The number is fixed once created."
        list={() => safetyChecklistService.getAll()}
        create={(values) => {
          const v = checklistSchema.parse(values);
          return safetyChecklistService.create({
            checklistNumber: v.checklistNumber,
            name: v.name,
            description: blank(v.description),
            type: v.type,
            version: v.version,
            isActive: v.isActive,
            scoringMode: v.scoringMode,
            printTitle: blank(v.printTitle),
          });
        }}
        // Editing happens in the builder (open the row): the header form there knows which columns
        // are frozen on a published version, this dialog does not.
        allowUpdate={false}
        update={() => Promise.resolve()}
        remove={(id) => safetyChecklistService.remove(id)}
        getId={(c) => c.id}
        emptyDescription="No checklists yet — create the first template for a routine inspection."
        columns={[
          {
            header: 'Number',
            cell: (c) => (
              <Link
                href={`/administration/safety/checklists/${c.id}`}
                className="font-mono hover:underline"
              >
                {c.checklistNumber}
              </Link>
            ),
          },
          {
            header: 'Name',
            cell: (c) => (
              <Link
                href={`/administration/safety/checklists/${c.id}`}
                className="font-medium hover:underline"
              >
                {c.name}
              </Link>
            ),
          },
          { header: 'Type', cell: (c) => c.typeName },
          { header: 'Version', cell: (c) => <span className="tabular-nums">v{c.version}</span> },
          { header: 'Items', cell: (c) => <span className="tabular-nums">{c.itemCount}</span> },
          {
            header: 'Scoring',
            cell: (c) =>
              c.scoringMode === 'CompliancePercentage'
                ? 'Percentage bands'
                : c.scoringMode === 'QualitativeRating'
                  ? 'Qualitative rating'
                  : 'None',
          },
          {
            header: 'Status',
            cell: (c) => (
              <div className="flex gap-1">
                <StatusBadge status={c.statusName} />
                {!c.isActive && <StatusBadge status="Inactive" />}
              </div>
            ),
          },
          {
            header: '',
            cell: (c) => (
              <Button asChild variant="outline" size="sm">
                <Link href={`/administration/safety/checklists/${c.id}`}>
                  {c.status === 'Draft' ? 'Build' : 'Open'}
                </Link>
              </Button>
            ),
          },
        ]}
        schema={checklistSchema}
        emptyForm={emptyChecklist}
        toForm={(c) => ({
          checklistNumber: c.checklistNumber,
          name: c.name,
          description: c.description ?? '',
          type: c.type,
          version: c.version,
          isActive: c.isActive,
          scoringMode: c.scoringMode,
          printTitle: c.printTitle ?? '',
        })}
        renderFields={(form, editing) => (
          <div className="space-y-4">
            {editing ? (
              <p className="text-muted-foreground text-sm">
                Number <span className="font-mono">{form.getValues('checklistNumber')}</span> —
                fixed at creation.
              </p>
            ) : (
              <TextField form={form} name="checklistNumber" label="Number" required placeholder="e.g. CHK-002" />
            )}
            <TextField form={form} name="name" label="Name" required />
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <FieldRow>
              <SelectField
                form={form}
                name="type"
                label="Inspection type"
                required
                options={SHE_INSPECTION_TYPE_OPTIONS}
              />
              <NumberField form={form} name="version" label="Version" required />
            </FieldRow>
            <SelectField
              form={form}
              name="scoringMode"
              label="Scoring"
              required
              options={SHE_CHECKLIST_SCORING_MODE_OPTIONS}
            />
            <TextField form={form} name="printTitle" label="Printed title" placeholder="e.g. CAFETERIA INSPECTION CHECKLIST" />
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="Inactive checklists stay on past inspections but are not offered for new ones."
            />
            <p className="text-muted-foreground text-sm">
              The template starts as a draft. Open it to add header fields, sections and items, outcomes and
              signatories, then publish it.
            </p>
          </div>
        )}
      />
    </div>
  );
}
