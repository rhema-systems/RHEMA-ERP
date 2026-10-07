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
  type RoomFormValues,
} from '@/components/hr/company-schedule/RoomForm';
import { RoomRetireDialog, roomErrorText } from '@/components/hr/company-schedule/RoomRetireDialog';
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

  // D-18 (lane 3a): switching an active room off goes through the retirement dialog, which lists its bookings still to
  // come and asks before they are cancelled; the form's values wait here meanwhile.
  const [retiring, setRetiring] = useState<RoomFormValues | null>(null);

  const save = async (values: RoomFormValues, cancelFutureBookings = false) => {
    setSaving(true);
    try {
      await meetingRoomService.update(id, { id, ...toRoomPayload(values), cancelFutureBookings });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'company-schedule'] });
      toast({ title: 'Room updated' });
      router.push('/administration/hr/company-schedule/rooms');
    } catch (error: any) {
      toast({ title: 'Could not update the room', description: roomErrorText(error, 'change'), variant: 'destructive' });
    } finally {
      setSaving(false);
    }
  };

  const onSubmit = form.handleSubmit(async (values) => {
    if (room?.isActive && !values.isActive) {
      setRetiring(values);
      return;
    }
    await save(values);
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
      <RoomRetireDialog
        room={retiring && room ? { id: room.id, roomName: room.roomName } : null}
        intent={retiring ? 'deactivate' : null}
        onClose={() => setRetiring(null)}
        deactivate={async (cancelFutureBookings) => {
          if (retiring) await save(retiring, cancelFutureBookings);
        }}
      />
    </form>
  );
}
