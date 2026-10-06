'use client';

import { useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  EventFormActions,
  EventFormFields,
  emptyEventForm,
  toCreatePayload,
  useEventForm,
} from '@/components/hr/company-schedule/EventForm';
import { companyEventService } from '@/services/hr/company-schedule.service';

const isDay = (s: string | null): s is string => !!s && /^\d{4}-\d{2}-\d{2}$/.test(s) && !Number.isNaN(Date.parse(s));
const isId = (s: string | null): s is string => !!s && /^[0-9a-f-]{36}$/i.test(s);

export default function NewCompanyEventPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [saving, setSaving] = useState(false);
  // Lane 5b (R4-10B.1): "Schedule for this unit" on the team schedule opens this form for the unit and the day —
  // `?scope=Department&unit=<id>&date=<yyyy-mm-dd>`. Anything else in the query is ignored.
  const params = useSearchParams();
  const unit = params.get('unit');
  const date = params.get('date');
  const form = useEventForm({
    ...emptyEventForm,
    ...(params.get('scope') === 'Department' && isId(unit) ? { scope: 'Department', organizationUnitId: unit } : {}),
    ...(isDay(date) ? { startDate: date, endDate: date } : {}),
  });

  const onSubmit = form.handleSubmit(async (values) => {
    setSaving(true);
    try {
      const created = await companyEventService.create(toCreatePayload(values));
      await queryClient.invalidateQueries({ queryKey: ['hr', 'company-schedule', 'events'] });
      toast({
        // Lane 2f-1: a recurring event is made as its whole series now.
        title: created.occurrenceCount ? `Series scheduled — ${created.occurrenceCount} occurrences` : 'Event scheduled',
        // An audience that reaches nobody is said, not refused (lane 2c, D-16); an occurrence on a day off is flagged (D-12).
        description: [
          `${created.eventNumber} — ${created.eventName}${created.occurrenceCount ? ', the first of the series' : ''}.`,
          ...(created.warnings ?? []).map((w) => `⚠ ${w}`),
        ].join(' '),
      });
      router.push(`/hr/company-schedule/events/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Could not schedule the event',
        description: error?.response?.data?.detail ?? error?.message ?? 'Please try again.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  });

  return (
    <form onSubmit={onSubmit} className="space-y-6 p-6">
      <PageHeader
        title="New event"
        description="You are recorded as the organiser."
        backHref="/hr/company-schedule/events"
      />
      <EventFormFields form={form} mode="create" />
      <EventFormActions
        saving={saving}
        label="Schedule event"
        onCancel={() => router.push('/hr/company-schedule/events')}
      />
    </form>
  );
}
