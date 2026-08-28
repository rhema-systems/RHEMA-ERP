'use client';

import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import {
  DateField,
  FieldRow,
  SelectField,
  SwitchField,
  TextField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { businessClosureService } from '@/services/hr/company-schedule.service';
import { locationService } from '@/services/hr/location.service';
import { siteOptions } from '@/components/hr/company-schedule/siteOptions';
import { departmentService } from '@/services/hr/lookup.service';
import { CLOSURE_TYPES } from '@/types/hr/company-schedule';
import type { BusinessClosure } from '@/types/hr/company-schedule';

const spaced = (s?: string | null) => (s ? s.replace(/([a-z])([A-Z])/g, '$1 $2') : '—');
const orNull = (s?: string) => (s && s.trim() ? s.trim() : null);

const schema = z
  .object({
    title: z.string().min(1, 'Title is required').max(200),
    reason: z.string().max(1000).optional().or(z.literal('')),
    startDate: z.string().min(1, 'Start date is required'),
    endDate: z.string().min(1, 'End date is required'),
    type: z.string().min(1, 'Type is required'),
    affectsAllStations: z.boolean(),
    locationId: z.string().optional().or(z.literal('')),
    departmentId: z.string().optional().or(z.literal('')),
    isPaidClosure: z.boolean(),
    countsAsWorkingDay: z.boolean(),
    communicationNotes: z.string().max(1000).optional().or(z.literal('')),
  })
  .refine((v) => v.endDate >= v.startDate, {
    message: 'The end cannot be before the start',
    path: ['endDate'],
  })
  .refine((v) => v.affectsAllStations || !!v.locationId || !!v.departmentId, {
    message: 'Pick a site or a department, or mark it company-wide',
    path: ['locationId'],
  });

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  title: '',
  reason: '',
  startDate: new Date().toISOString().slice(0, 10),
  endDate: new Date().toISOString().slice(0, 10),
  type: 'FullClosure',
  affectsAllStations: true,
  locationId: '',
  departmentId: '',
  isPaidClosure: true,
  countsAsWorkingDay: false,
  communicationNotes: '',
};

/**
 * Business closures — the days the organisation is shut. Sits under Administration alongside
 * holiday calendars, because the same question ("is this a working day?") is answered from both.
 *
 * ⚠ **No announced-by field** — the API records the announcer from the token.
 */
export default function BusinessClosuresPage() {
  const { data: locations } = useQuery({
    queryKey: ['hr', 'locations', 'all'],
    queryFn: () => locationService.getAll(),
  });
  const { data: departments } = useQuery({
    queryKey: ['hr', 'departments', 'all'],
    queryFn: () => departmentService.getAll(),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Business closures"
        description="Days the organisation is closed, company-wide or for one site or department."
        backHref="/administration/hr/company-schedule"
      />

      <ResourceCollectionTab<BusinessClosure, FormValues>
        parentId="all"
        title="closures"
        singular="closure"
        queryKey={['hr', 'company-schedule', 'closures']}
        dialogHint="You are recorded as the person announcing it."
        emptyDescription="No closures recorded yet."
        list={() => businessClosureService.getAll()}
        create={(_p, v) =>
          businessClosureService.create({
            title: v.title.trim(),
            reason: orNull(v.reason),
            startDate: v.startDate,
            endDate: v.endDate,
            type: v.type as BusinessClosure['type'],
            affectsAllStations: v.affectsAllStations,
            locationId: v.affectsAllStations ? null : orNull(v.locationId),
            departmentId: v.affectsAllStations ? null : orNull(v.departmentId),
            isPaidClosure: v.isPaidClosure,
            countsAsWorkingDay: v.countsAsWorkingDay,
            communicationNotes: orNull(v.communicationNotes),
          })
        }
        update={(_p, id, v) =>
          businessClosureService.update(id, {
            id,
            title: v.title.trim(),
            reason: orNull(v.reason),
            startDate: v.startDate,
            endDate: v.endDate,
            type: v.type as BusinessClosure['type'],
            affectsAllStations: v.affectsAllStations,
            locationId: v.affectsAllStations ? null : orNull(v.locationId),
            departmentId: v.affectsAllStations ? null : orNull(v.departmentId),
            isPaidClosure: v.isPaidClosure,
            countsAsWorkingDay: v.countsAsWorkingDay,
            communicationNotes: orNull(v.communicationNotes),
          })
        }
        remove={(_p, id) => businessClosureService.remove(id)}
        getId={(c) => c.id}
        dialogClassName="sm:max-w-[640px]"
        columns={[
          { header: 'Title', cell: (c) => c.title },
          { header: 'Type', cell: (c) => spaced(c.type) },
          {
            header: 'When',
            cell: (c) =>
              c.startDate.slice(0, 10) === c.endDate.slice(0, 10)
                ? c.startDate.slice(0, 10)
                : `${c.startDate.slice(0, 10)} → ${c.endDate.slice(0, 10)}`,
          },
          {
            header: 'Applies to',
            cell: (c) =>
              c.affectsAllStations
                ? 'Whole company'
                : [c.locationName, c.departmentName].filter(Boolean).join(' · ') || '—',
          },
          { header: 'Paid', cell: (c) => <StatusBadge status={c.isPaidClosure ? 'Paid' : 'Unpaid'} /> },
          { header: 'Working day', cell: (c) => (c.countsAsWorkingDay ? 'Counts' : 'Does not count') },
          { header: 'Announced by', cell: (c) => c.announcedByName || '—' },
        ]}
        schema={schema}
        emptyForm={empty}
        toForm={(c) => ({
          title: c.title,
          reason: c.reason ?? '',
          startDate: c.startDate.slice(0, 10),
          endDate: c.endDate.slice(0, 10),
          type: c.type,
          affectsAllStations: c.affectsAllStations,
          locationId: c.locationId ?? '',
          departmentId: c.departmentId ?? '',
          isPaidClosure: c.isPaidClosure,
          countsAsWorkingDay: c.countsAsWorkingDay,
          communicationNotes: c.communicationNotes ?? '',
        })}
        renderFields={(form) => {
          const companyWide = form.watch('affectsAllStations');
          return (
            <>
              <TextField form={form} name="title" label="Title" required />
              <FieldRow>
                <SelectField
                  form={form}
                  name="type"
                  label="Type"
                  required
                  options={CLOSURE_TYPES.map((t) => ({ value: t, label: spaced(t) }))}
                />
                <div />
              </FieldRow>
              <FieldRow>
                <DateField form={form} name="startDate" label="From" required />
                <DateField form={form} name="endDate" label="To" required />
              </FieldRow>
              <SwitchField
                form={form}
                name="affectsAllStations"
                label="Affects the whole company"
                description="Turn this off to close a single site or department."
              />
              {!companyWide && (
                <FieldRow>
                  <SelectField
                    form={form}
                    name="locationId"
                    label="Site"
                    allowEmpty
                    emptyLabel="Any site"
                    options={siteOptions(locations)}
                  />
                  <SelectField
                    form={form}
                    name="departmentId"
                    label="Department"
                    allowEmpty
                    emptyLabel="Any department"
                    options={(departments ?? []).map((d) => ({ value: d.id, label: d.name }))}
                  />
                </FieldRow>
              )}
              <FieldRow>
                <SwitchField form={form} name="isPaidClosure" label="Staff are paid" />
                <SwitchField
                  form={form}
                  name="countsAsWorkingDay"
                  label="Counts as a working day"
                  description="Affects leave and attendance calculations."
                />
              </FieldRow>
              <TextareaField form={form} name="reason" label="Reason" />
              <TextareaField form={form} name="communicationNotes" label="How it was communicated" />
            </>
          );
        }}
      />
    </div>
  );
}
