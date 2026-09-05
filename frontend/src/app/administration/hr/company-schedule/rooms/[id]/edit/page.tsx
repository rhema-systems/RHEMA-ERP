'use client';

import { use, useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  RoomFormFields,
  emptyRoomForm,
  roomToForm,
  toRoomPayload,
  useRoomForm,
} from '@/components/hr/company-schedule/RoomForm';
import { meetingRoomService } from '@/services/hr/company-schedule.service';

export default function EditMeetingRoomPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [saving, setSaving] = useState(false);

  const { data: room, isLoading } = useQuery({
    queryKey: ['hr', 'company-schedule', 'rooms', id],
    queryFn: () => meetingRoomService.getById(id),
  });

  const form = useRoomForm(emptyRoomForm);

  useEffect(() => {
    if (room) form.reset(roomToForm(room));
  }, [room]);

  const onSubmit = form.handleSubmit(async (values) => {
    setSaving(true);
    try {
      await meetingRoomService.update(id, { id, ...toRoomPayload(values) });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'company-schedule', 'rooms'] });
      toast({ title: 'Room updated' });
      router.push('/administration/hr/company-schedule/rooms');
    } catch (error: any) {
      toast({
        title: 'Could not update the room',
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
        title={`Edit ${room?.roomName ?? 'room'}`}
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
          Save changes
        </Button>
      </div>
    </form>
  );
}
