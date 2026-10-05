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
import { companyEventService } from '@/services/hr/company-schedule.service';

export default function EditCompanyEventPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [saving, setSaving] = useState(false);

  const { data: event, isLoading } = useQuery({
    queryKey: ['hr', 'company-schedule', 'events', id],
    queryFn: () => companyEventService.getById(id),
  });

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
      await companyEventService.update(id, toUpdatePayload(id, values));
      await queryClient.invalidateQueries({ queryKey: ['hr', 'company-schedule', 'events'] });
      toast(
        moved
          ? {
              title: 'Event moved',
              description:
                'Everybody invited is told. Accepted replies are asked again, and its room bookings moved with it.',
            }
          : { title: 'Event updated' },
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
      <EventFormActions
        saving={saving}
        label="Save changes"
        onCancel={() => router.push(`/hr/company-schedule/events/${id}`)}
      />
    </form>
  );
}
