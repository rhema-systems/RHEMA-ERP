'use client';

import { use, useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  EventFormActions,
  EventFormFields,
  emptyEventForm,
  eventToForm,
  toUpdatePayload,
  useEventForm,
  windowChanged,
} from '@/components/hr/company-schedule/EventForm';
import { describeReach, describeSeriesChange } from '@/components/hr/company-schedule/noticeReach';
import { Card, CardContent } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { companyEventService } from '@/services/hr/company-schedule.service';
import { SERIES_SCOPE_LABELS, SERIES_SCOPES } from '@/types/hr/company-schedule';
import type { SeriesScope } from '@/types/hr/company-schedule';

export default function EditCompanyEventPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [saving, setSaving] = useState(false);
  // Lane 2f-2b: on a recurring event, which dates the edit reaches.
  const [seriesScope, setSeriesScope] = useState<SeriesScope>('ThisOccurrence');

  const { data: event, isLoading } = useQuery({
    queryKey: ['hr', 'company-schedule', 'events', id],
    queryFn: () => companyEventService.getById(id),
  });
  const inSeries = !!event?.recurrenceSeriesId;

  const form = useEventForm(emptyEventForm);

  // Seed once the record arrives — the form is mounted before the query resolves.
  useEffect(() => {
    if (event) form.reset(eventToForm(event));
  }, [event]);

  const onSubmit = form.handleSubmit(async (values) => {
    // ⚠ Moving the dates is a reschedule (F-37): everybody invited is told why, so the reason is required.
    const moved = !!event && windowChanged(event, values);
    if (moved && !values.rescheduleReason?.trim()) {
      form.setError('rescheduleReason', { message: 'Give the reason for the change — everybody invited is told it.' });
      return;
    }
    setSaving(true);
    try {
      const saved = await companyEventService.update(id, {
        ...toUpdatePayload(id, values),
        // Lane 2f-2b: on a series, the other dates take only what this edit changed.
        ...(inSeries ? { seriesScope } : {}),
      });
      const warnings = (saved.warnings ?? []).map((w) => `⚠ ${w}`).join(' ');
      // Lane 2e-2 (R4-6.3): who the move, postponement or new venue/link reached — counted, not assumed.
      const told = saved.told?.issued ? describeReach(saved.told, 'Guests told') : undefined;
      await queryClient.invalidateQueries({ queryKey: ['hr', 'company-schedule', 'events'] });
      toast(
        saved.series
          ? {
              title: moved ? 'Dates moved' : 'Dates updated',
              description: [describeSeriesChange(saved.series), warnings].filter(Boolean).join(' '),
            }
          : moved
          ? {
              title: 'Event moved',
              description: [
                'Accepted replies are asked again, and its room bookings moved with it.',
                told,
                warnings,
              ].filter(Boolean).join(' '),
            }
          : { title: 'Event updated', description: [told, warnings].filter(Boolean).join(' ') || undefined },
      );
      router.push(`/hr/company-schedule/events/${id}`);
    } catch (error: any) {
      toast({
        title: 'Could not update the event',
        description: error?.response?.data?.detail ?? error?.message ?? 'Please try again.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <form onSubmit={onSubmit} className="space-y-6 p-6">
      <PageHeader
        title={`Edit ${event?.eventName ?? 'event'}`}
        description="Recurrence is set when the event is created and cannot be changed here."
        backHref={`/hr/company-schedule/events/${id}`}
      />
      <EventFormFields form={form} mode="edit" event={event} />
      {inSeries && (
        <Card>
          <CardContent className="grid gap-2 pt-6 sm:grid-cols-2">
            <div className="space-y-2">
              <Label>Which dates</Label>
              <Select value={seriesScope} onValueChange={(v) => v && setSeriesScope(v as SeriesScope)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {SERIES_SCOPES.map((s) => (
                    <SelectItem key={s} value={s}>{SERIES_SCOPE_LABELS[s]}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <p className="self-end text-xs text-muted-foreground">
              This is occurrence {event?.occurrenceNumber} of {event?.occurrenceCount}. The other dates take only what
              you change here; new dates or times move each by the same amount. A date that has started, been completed
              or been cancelled is left as it is, and each guest is told once.
            </p>
          </CardContent>
        </Card>
      )}
      <EventFormActions
        saving={saving}
        label="Save changes"
        onCancel={() => router.push(`/hr/company-schedule/events/${id}`)}
      />
    </form>
  );
}
