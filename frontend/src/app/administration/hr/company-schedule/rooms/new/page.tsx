'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  RoomFormFields,
  emptyRoomForm,
  toRoomPayload,
  useRoomForm,
} from '@/components/hr/company-schedule/RoomForm';
import { meetingRoomService } from '@/services/hr/company-schedule.service';

export default function NewMeetingRoomPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [saving, setSaving] = useState(false);
  const form = useRoomForm(emptyRoomForm);

  const onSubmit = form.handleSubmit(async (values) => {
    setSaving(true);
    try {
      const created = await meetingRoomService.create(toRoomPayload(values));
      await queryClient.invalidateQueries({ queryKey: ['hr', 'company-schedule', 'rooms'] });
      toast({ title: 'Room added', description: created.roomName });
      router.push('/administration/hr/company-schedule/rooms');
    } catch (error: any) {
      toast({
        title: 'Could not add the room',
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
        title="New meeting room"
        description="Rooms belong to a site, and only bookable rooms appear when someone books."
        backHref="/administration/hr/company-schedule/rooms"
      />
      <RoomFormFields form={form} />
      <div className="flex justify-end gap-2">
        <Button
          type="button"
          variant="outline"
          onClick={() => router.push('/administration/hr/company-schedule/rooms')}
          disabled={saving}
        >
          Cancel
        </Button>
        <Button type="submit" disabled={saving}>
          {saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
          Add room
        </Button>
      </div>
    </form>
  );
}
