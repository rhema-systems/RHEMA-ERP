'use client';

import { useState } from 'react';
import { z } from 'zod';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { MilestoneFilesDialog } from '@/components/hr/company-schedule/MilestoneFilesDialog';
import { ordinal } from '@/components/hr/company-schedule/milestoneWords';
import { useAuth } from '@/hooks/use-auth';
import {
  DateField,
  FieldRow,
  SelectField,
  SwitchField,
  TextField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { companyMilestoneService } from '@/services/hr/company-schedule.service';
import { MILESTONE_CATEGORIES } from '@/types/hr/company-schedule';
import type { CompanyMilestone } from '@/types/hr/company-schedule';

const spaced = (s?: string | null) => (s ? s.replace(/([a-z])([A-Z])/g, '$1 $2') : '—');
const orNull = (s?: string) => (s && s.trim() ? s.trim() : null);

const schema = z.object({
  title: z.string().min(1, 'Title is required').max(200),
  description: z.string().max(1000).optional().or(z.literal('')),
  category: z.string().min(1, 'Category is required'),
  milestoneDate: z.string().min(1, 'Date is required'),
  isRecurringAnnually: z.boolean(),
  showOnCalendar: z.boolean(),
  significance: z.string().max(1000).optional().or(z.literal('')),
  relatedDocuments: z.string().max(1000).optional().or(z.literal('')),
});

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  title: '',
  description: '',
  category: 'Achievement',
  milestoneDate: new Date().toISOString().slice(0, 10),
  isRecurringAnnually: false,
  showOnCalendar: true,
  significance: '',
  relatedDocuments: '',
};

/**
 * Company milestones — anniversaries, achievements, launches, targets and certifications that
 * appear on the company calendar. Reference data, so it lives under Administration.
 *
 * Lane 4a: a milestone's files open from its row ("Files") — real files through the upload gate (D-3); the free text is
 * "References". A yearly milestone shows its next occurrence and which anniversary it is. Delete is offered only with
 * `HR.Company.Admin`, which it needs.
 */
export default function CompanyMilestonesPage() {
  const { hasPermission } = useAuth();
  const [filesFor, setFilesFor] = useState<CompanyMilestone | null>(null);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Company milestones"
        description="Anniversaries, achievements and other dates worth marking on the company calendar."
        backHref="/administration/hr/company-schedule"
      />

      <ResourceCollectionTab<CompanyMilestone, FormValues>
        // Milestones are tenant-wide rather than nested under a parent, so the id is a constant.
        parentId="all"
        title="milestones"
        singular="milestone"
        queryKey={['hr', 'company-schedule', 'milestones']}
        dialogHint="A date the organisation wants remembered."
        emptyDescription="No milestones recorded yet."
        list={() => companyMilestoneService.getAll()}
        create={(_p, v) =>
          companyMilestoneService.create({
            title: v.title.trim(),
            description: orNull(v.description),
            category: v.category as CompanyMilestone['category'],
            milestoneDate: v.milestoneDate,
            isRecurringAnnually: v.isRecurringAnnually,
            showOnCalendar: v.showOnCalendar,
            significance: orNull(v.significance),
            relatedDocuments: orNull(v.relatedDocuments),
          })
        }
        update={(_p, id, v) =>
          companyMilestoneService.update(id, {
            id,
            title: v.title.trim(),
            description: orNull(v.description),
            category: v.category as CompanyMilestone['category'],
            milestoneDate: v.milestoneDate,
            isRecurringAnnually: v.isRecurringAnnually,
            showOnCalendar: v.showOnCalendar,
            significance: orNull(v.significance),
            relatedDocuments: orNull(v.relatedDocuments),
          })
        }
        remove={(_p, id) => companyMilestoneService.remove(id)}
        allowRemove={hasPermission('HR.Company.Admin')}
        onOpenItem={(m) => setFilesFor(m)}
        openItemLabel="Files"
        getId={(m) => m.id}
        columns={[
          { header: 'Title', cell: (m) => m.title },
          { header: 'Category', cell: (m) => spaced(m.category) },
          { header: 'Date', cell: (m) => m.milestoneDate.slice(0, 10) },
          {
            header: 'Next',
            cell: (m) =>
              m.nextOccurrence
                ? `${m.nextOccurrence.slice(0, 10)}${m.isRecurringAnnually && m.yearsSince > 0 ? ` · ${ordinal(m.yearsSince)}` : ''}`
                : 'Past',
          },
          { header: 'Repeats', cell: (m) => (m.isRecurringAnnually ? 'Every year' : 'Once') },
          { header: 'On calendar', cell: (m) => (m.showOnCalendar ? 'Yes' : 'Hidden') },
          { header: 'Files', cell: (m) => (m.documentCount > 0 ? m.documentCount : '—') },
        ]}
        schema={schema}
        emptyForm={empty}
        toForm={(m) => ({
          title: m.title,
          description: m.description ?? '',
          category: m.category,
          milestoneDate: m.milestoneDate.slice(0, 10),
          isRecurringAnnually: m.isRecurringAnnually,
          showOnCalendar: m.showOnCalendar,
          significance: m.significance ?? '',
          relatedDocuments: m.relatedDocuments ?? '',
        })}
        renderFields={(form) => (
          <>
            <TextField form={form} name="title" label="Title" required />
            <FieldRow>
              <SelectField
                form={form}
                name="category"
                label="Category"
                required
                options={MILESTONE_CATEGORIES.map((c) => ({ value: c, label: spaced(c) }))}
              />
              <DateField form={form} name="milestoneDate" label="Date" required />
            </FieldRow>
            <TextareaField form={form} name="description" label="Description" />
            <TextareaField form={form} name="significance" label="Why it matters" />
            {/* Lane 4a (D-3): references only — the files themselves open from the row's "Files". */}
            <TextField form={form} name="relatedDocuments" label="References" />
            <FieldRow>
              <SwitchField form={form} name="isRecurringAnnually" label="Repeats every year" />
              <SwitchField form={form} name="showOnCalendar" label="Show on the calendar" />
            </FieldRow>
          </>
        )}
      />

      <MilestoneFilesDialog milestone={filesFor} onClose={() => setFilesFor(null)} />
    </div>
  );
}
