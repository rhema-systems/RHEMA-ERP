'use client';

import { useRouter } from 'next/navigation';
import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  TextField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { holidayCalendarService } from '@/services/hr/attendance-setup.service';
import { countryService } from '@/services/hr/country.service';
import { useQuery } from '@tanstack/react-query';
import type { HolidayCalendarSummary } from '@/types/hr/attendance';

/**
 * Holiday calendars group public holidays by country or region. An employee's calendar
 * decides which days are non-working for them, which in turn drives the PublicHoliday
 * attendance status. The holidays themselves are managed on each calendar's own page.
 */
const calendarSchema = z.object({
  calendarName: z.string().min(1, 'A name is required').max(150),
  description: z.string().max(500).optional(),
  countryId: z.string().optional(),
  region: z.string().max(100).optional(),
  isDefault: z.boolean(),
  isActive: z.boolean(),
});

type CalendarForm = z.input<typeof calendarSchema>;

const emptyCalendar: CalendarForm = {
  calendarName: '',
  description: '',
  countryId: '',
  region: '',
  isDefault: false,
  isActive: true,
};

export default function HolidayCalendarsPage() {
  const router = useRouter();

  const { data: countries } = useQuery({
    queryKey: ['hr', 'countries', 'all'],
    queryFn: () => countryService.getAll(),
  });

  const countryOptions = (countries ?? []).map((c) => ({ value: c.id, label: c.name }));

  const toPayload = (values: CalendarForm) => {
    const v = calendarSchema.parse(values);
    return {
      ...v,
      description: v.description || null,
      region: v.region || null,
      countryId: v.countryId || null,
    };
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Holiday Calendars"
        description="Public-holiday sets that decide which days are non-working for each employee."
        backHref="/administration/hr/attendance"
      />

      <ResourceListPanel<HolidayCalendarSummary, CalendarForm>
        title="holiday calendars"
        singular="calendar"
        queryKey={['hr', 'holiday-calendars']}
        dialogHint="Mark one calendar as default for employees with no specific assignment."
        list={() => holidayCalendarService.getAll()}
        create={(values) => holidayCalendarService.create(toPayload(values))}
        update={(id, values) => holidayCalendarService.update(id, { id, ...toPayload(values) })}
        remove={(id) => holidayCalendarService.remove(id)}
        getId={(c) => c.id}
        actions={[
          {
            label: 'Manage holidays',
            run: async (c) => {
              router.push(`/administration/hr/attendance/holiday-calendars/${c.id}`);
            },
          },
        ]}
        columns={[
          {
            header: 'Calendar',
            cell: (c) => (
              <div className="flex items-center gap-2">
                <span className="font-medium">{c.calendarName}</span>
                {c.isDefault && <Badge variant="outline">Default</Badge>}
              </div>
            ),
          },
          { header: 'Country', cell: (c) => c.countryName || '—' },
          { header: 'Region', cell: (c) => c.region || '—' },
          { header: 'Holidays', cell: (c) => c.holidayCount, className: 'text-right' },
          { header: 'Status', cell: (c) => <StatusBadge active={c.isActive} /> },
          {
            header: '',
            cell: (c) => (
              <Button
                variant="link"
                size="sm"
                className="h-auto p-0"
                onClick={(e) => {
                  e.stopPropagation();
                  router.push(`/administration/hr/attendance/holiday-calendars/${c.id}`);
                }}
              >
                Holidays
              </Button>
            ),
          },
        ]}
        schema={calendarSchema as any}
        emptyForm={emptyCalendar}
        toForm={(c) => ({
          ...emptyCalendar,
          calendarName: c.calendarName,
          region: c.region ?? '',
          isDefault: c.isDefault,
          isActive: c.isActive,
        })}
        renderFields={(form) => (
          <>
            <TextField form={form} name="calendarName" label="Calendar name" required />
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <FieldRow>
              <SelectField
                form={form}
                name="countryId"
                label="Country"
                options={countryOptions}
                allowEmpty
              />
              <TextField form={form} name="region" label="Region" />
            </FieldRow>
            <SwitchField
              form={form}
              name="isDefault"
              label="Default calendar"
              description="Used for employees with no specific calendar."
            />
            <SwitchField form={form} name="isActive" label="Active" />
          </>
        )}
      />
    </div>
  );
}
