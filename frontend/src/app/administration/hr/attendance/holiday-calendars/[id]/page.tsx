'use client';

import { useState } from 'react';
import { useParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2 } from 'lucide-react';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  TextField,
  NumberField,
  DateField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { holidayCalendarService } from '@/services/hr/attendance-setup.service';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { HOLIDAY_OBSERVANCE_TYPE_OPTIONS } from '@/types/hr/attendance';
import type { PublicHolidaySummary } from '@/types/hr/attendance';

/**
 * Holidays inside one calendar, filtered by year.
 *
 * `dateFrom`/`dateTo` are a range rather than a single day so multi-day observances (and
 * their substitute days) fit one row. Recurring holidays repeat annually on the same dates;
 * moveable feasts have to be entered per year.
 */
const holidaySchema = z
  .object({
    holidayName: z.string().min(1, 'A name is required').max(200),
    description: z.string().max(1000).optional(),
    dateFrom: z.string().min(1, 'Required'),
    dateTo: z.string().min(1, 'Required'),
    observanceType: z.enum(['Mandatory', 'Optional', 'SubstituteDay']),
    substitutionDate: z.string().optional(),
    attractsHolidayPay: z.boolean(),
    holidayPayMultiplier: z.coerce.number().min(0.1).max(10).optional(),
    isRecurringAnnually: z.boolean(),
    isActive: z.boolean(),
  })
  .refine((v) => v.dateTo >= v.dateFrom, {
    message: 'The end date cannot be before the start date',
    path: ['dateTo'],
  });

type HolidayForm = z.input<typeof holidaySchema>;

const emptyHoliday: HolidayForm = {
  holidayName: '',
  description: '',
  dateFrom: '',
  dateTo: '',
  observanceType: 'Mandatory',
  substitutionDate: '',
  attractsHolidayPay: true,
  holidayPayMultiplier: undefined,
  isRecurringAnnually: true,
  isActive: true,
};

const currentYear = new Date().getFullYear();
const years = [currentYear + 1, currentYear, currentYear - 1, currentYear - 2];

export default function HolidayCalendarDetailPage() {
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const [year, setYear] = useState(String(currentYear));

  const { data: calendar, isLoading, isError } = useQuery({
    queryKey: ['hr', 'holiday-calendars', id],
    queryFn: () => holidayCalendarService.getById(id),
    enabled: !!id,
  });

  const toPayload = (values: HolidayForm) => {
    const v = holidaySchema.parse(values);
    return {
      ...v,
      description: v.description || null,
      substitutionDate: v.substitutionDate || null,
      holidayPayMultiplier: v.attractsHolidayPay ? (v.holidayPayMultiplier ?? null) : null,
    };
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !calendar) {
    return (
      <div className="p-6">
        <EmptyState title="Calendar not found" description="It may have been removed." />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={calendar.calendarName}
        description={
          [calendar.countryName, calendar.region].filter(Boolean).join(' · ') ||
          'Public holidays for this calendar.'
        }
        backHref="/administration/hr/attendance/holiday-calendars"
        actions={
          <div className="flex items-center gap-2">
            <StatusBadge active={calendar.isActive} />
            <Select value={year} onValueChange={setYear}>
              <SelectTrigger className="w-[110px]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {years.map((y) => (
                  <SelectItem key={y} value={String(y)}>
                    {y}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        }
      />

      <ResourceCollectionTab<PublicHolidaySummary, HolidayForm>
        parentId={id}
        title="holidays"
        singular="holiday"
        // The year is part of the key so switching it refetches rather than showing stale rows.
        queryKey={['hr', 'holiday-calendars', id, 'holidays', year]}
        invalidateKeys={[['hr', 'holiday-calendars']]}
        dialogHint="Recurring holidays repeat on the same dates each year; moveable feasts need an entry per year."
        emptyDescription={`No holidays recorded for ${year}.`}
        list={(calendarId) => holidayCalendarService.getHolidays(calendarId, Number(year))}
        create={(calendarId, values) =>
          holidayCalendarService.addHoliday(calendarId, {
            ...toPayload(values),
            holidayCalendarId: calendarId,
          })
        }
        update={(calendarId, holidayId, values) =>
          holidayCalendarService.updateHoliday(calendarId, holidayId, {
            id: holidayId,
            ...toPayload(values),
          })
        }
        remove={(calendarId, holidayId) =>
          holidayCalendarService.removeHoliday(calendarId, holidayId)
        }
        getId={(h) => h.id}
        columns={[
          { header: 'Holiday', cell: (h) => <span className="font-medium">{h.holidayName}</span> },
          { header: 'From', cell: (h) => formatDate(h.dateFrom) },
          { header: 'To', cell: (h) => formatDate(h.dateTo) },
          { header: 'Observance', cell: (h) => humanizeEnum(h.observanceType) },
          { header: 'Holiday pay', cell: (h) => (h.attractsHolidayPay ? 'Yes' : 'No') },
          { header: 'Status', cell: (h) => <StatusBadge active={h.isActive} /> },
        ]}
        schema={holidaySchema as any}
        emptyForm={{ ...emptyHoliday, dateFrom: `${year}-01-01`, dateTo: `${year}-01-01` }}
        toForm={(h) => ({
          ...emptyHoliday,
          holidayName: h.holidayName,
          dateFrom: h.dateFrom,
          dateTo: h.dateTo,
          observanceType: h.observanceType,
          attractsHolidayPay: h.attractsHolidayPay,
          isActive: h.isActive,
        })}
        renderFields={(form) => (
          <>
            <TextField form={form} name="holidayName" label="Holiday name" required />
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <FieldRow>
              <DateField form={form} name="dateFrom" label="From" required />
              <DateField form={form} name="dateTo" label="To" required />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={form}
                name="observanceType"
                label="Observance"
                required
                options={HOLIDAY_OBSERVANCE_TYPE_OPTIONS}
              />
              <DateField form={form} name="substitutionDate" label="Substitute day" />
            </FieldRow>
            <SwitchField
              form={form}
              name="attractsHolidayPay"
              label="Attracts holiday pay"
              description="Employees who work the day are paid at the multiplier below."
            />
            {!!form.watch('attractsHolidayPay') && (
              <NumberField
                form={form}
                name="holidayPayMultiplier"
                label="Pay multiplier"
                step="0.1"
                placeholder="e.g. 2 for double time"
              />
            )}
            <SwitchField
              form={form}
              name="isRecurringAnnually"
              label="Recurs annually"
              description="Turn off for moveable feasts that need entering each year."
            />
            <SwitchField form={form} name="isActive" label="Active" />
          </>
        )}
      />
    </div>
  );
}
