'use client';

import { useQuery } from '@tanstack/react-query';
import { useForm, type UseFormReturn } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  FieldRow,
  NumberField,
  SelectField,
  SwitchField,
  TextField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { locationService } from '@/services/hr/location.service';
import { siteOptions } from '@/components/hr/company-schedule/siteOptions';
import { ROOM_TYPES } from '@/types/hr/company-schedule';
import type { CreateMeetingRoom, MeetingRoom } from '@/types/hr/company-schedule';

/**
 * Meeting-room authoring, shared by create and edit.
 *
 * ⚠ **"Site" and "Location" are two different things here** and both are required. `locationId` is
 * the site the room belongs to, picked from the location tree; `location` is free text saying where
 * in that site the room is. The API named them that way, so the labels say which is which rather
 * than leaving the user to guess.
 */

const schema = z.object({
  roomCode: z.string().max(50).optional().or(z.literal('')),
  roomName: z.string().min(1, 'Room name is required').max(100),
  description: z.string().max(500).optional().or(z.literal('')),
  locationId: z.string().min(1, 'Pick the site this room is on'),
  location: z.string().min(1, 'Say where in the site the room is').max(200),
  floor: z.string().max(50).optional().or(z.literal('')),
  building: z.string().max(50).optional().or(z.literal('')),
  capacity: z.coerce.number().int().min(1, 'Capacity must be at least 1'),
  type: z.string().min(1, 'Room type is required'),
  hasProjector: z.boolean(),
  hasWhiteboard: z.boolean(),
  hasVideoConference: z.boolean(),
  hasAudioSystem: z.boolean(),
  hasAirConditioning: z.boolean(),
  otherFacilities: z.string().max(500).optional().or(z.literal('')),
  isActive: z.boolean(),
  requiresApproval: z.boolean(),
  isBookable: z.boolean(),
  maxBookingDurationHours: z.coerce.number().int().min(0).optional(),
  advanceBookingDays: z.coerce.number().int().min(0).optional(),
});

export type RoomFormValues = z.infer<typeof schema>;

export const emptyRoomForm: RoomFormValues = {
  roomCode: '',
  roomName: '',
  description: '',
  locationId: '',
  location: '',
  floor: '',
  building: '',
  capacity: 8,
  type: 'Conference',
  hasProjector: false,
  hasWhiteboard: false,
  hasVideoConference: false,
  hasAudioSystem: false,
  hasAirConditioning: false,
  otherFacilities: '',
  isActive: true,
  requiresApproval: false,
  isBookable: true,
  maxBookingDurationHours: undefined,
  advanceBookingDays: undefined,
};

export function roomToForm(r: MeetingRoom): RoomFormValues {
  return {
    roomCode: r.roomCode ?? '',
    roomName: r.roomName,
    description: r.description ?? '',
    locationId: r.locationId,
    location: r.location,
    floor: r.floor ?? '',
    building: r.building ?? '',
    capacity: r.capacity,
    type: r.type,
    hasProjector: r.hasProjector,
    hasWhiteboard: r.hasWhiteboard,
    hasVideoConference: r.hasVideoConference,
    hasAudioSystem: r.hasAudioSystem,
    hasAirConditioning: r.hasAirConditioning,
    otherFacilities: r.otherFacilities ?? '',
    isActive: r.isActive,
    requiresApproval: r.requiresApproval,
    isBookable: r.isBookable,
    maxBookingDurationHours: r.maxBookingDurationHours ?? undefined,
    advanceBookingDays: r.advanceBookingDays ?? undefined,
  };
}

const orNull = (s?: string) => (s && s.trim() ? s.trim() : null);
const numOrNull = (n?: number) => (n === undefined || Number.isNaN(n) ? null : n);

export function toRoomPayload(v: RoomFormValues): CreateMeetingRoom {
  return {
    roomCode: (v.roomCode ?? '').trim(),
    roomName: v.roomName.trim(),
    description: orNull(v.description),
    locationId: v.locationId,
    location: v.location.trim(),
    floor: orNull(v.floor),
    building: orNull(v.building),
    capacity: v.capacity,
    type: v.type as CreateMeetingRoom['type'],
    hasProjector: v.hasProjector,
    hasWhiteboard: v.hasWhiteboard,
    hasVideoConference: v.hasVideoConference,
    hasAudioSystem: v.hasAudioSystem,
    hasAirConditioning: v.hasAirConditioning,
    otherFacilities: orNull(v.otherFacilities),
    isActive: v.isActive,
    requiresApproval: v.requiresApproval,
    isBookable: v.isBookable,
    maxBookingDurationHours: numOrNull(v.maxBookingDurationHours),
    advanceBookingDays: numOrNull(v.advanceBookingDays),
  };
}

export function useRoomForm(initial: RoomFormValues) {
  return useForm<RoomFormValues>({ resolver: zodResolver(schema) as any, defaultValues: initial });
}

export function RoomFormFields({ form }: { form: UseFormReturn<RoomFormValues> }) {
  const { data: locations, isLoading } = useQuery({
    queryKey: ['hr', 'locations', 'all'],
    queryFn: () => locationService.getAll(),
  });

  const bookable = form.watch('isBookable');

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader><CardTitle>The room</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <TextField form={form} name="roomName" label="Room name" required />
            <TextField form={form} name="roomCode" label="Room code" placeholder="Generated if left blank" />
          </FieldRow>
          <FieldRow>
            <SelectField
              form={form}
              name="locationId"
              label="Site"
              required
              placeholder={isLoading ? 'Loading sites…' : 'Select…'}
              options={siteOptions(locations)}
            />
            <SelectField
              form={form}
              name="type"
              label="Room type"
              required
              options={ROOM_TYPES.map((t) => ({ value: t, label: t }))}
            />
          </FieldRow>
          <TextField
            form={form}
            name="location"
            label="Where in the site"
            required
            placeholder="e.g. East wing, past reception"
          />
          <FieldRow>
            <TextField form={form} name="building" label="Building" />
            <TextField form={form} name="floor" label="Floor" />
          </FieldRow>
          <FieldRow>
            <NumberField form={form} name="capacity" label="Seats" required />
            <div />
          </FieldRow>
          <TextareaField form={form} name="description" label="Description" />
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Facilities</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <SwitchField form={form} name="hasProjector" label="Projector" />
            <SwitchField form={form} name="hasWhiteboard" label="Whiteboard" />
          </FieldRow>
          <FieldRow>
            <SwitchField form={form} name="hasVideoConference" label="Video conferencing" />
            <SwitchField form={form} name="hasAudioSystem" label="Audio system" />
          </FieldRow>
          <SwitchField form={form} name="hasAirConditioning" label="Air conditioning" />
          <TextareaField form={form} name="otherFacilities" label="Anything else" />
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Booking rules</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <SwitchField form={form} name="isActive" label="Active" />
            <SwitchField form={form} name="isBookable" label="Can be booked" />
          </FieldRow>
          {bookable && (
            <>
              <SwitchField
                form={form}
                name="requiresApproval"
                label="Bookings need approval"
                description="A booking stays Tentative until somebody approves it."
              />
              <FieldRow>
                <NumberField
                  form={form}
                  name="maxBookingDurationHours"
                  label="Longest booking (hours)"
                  placeholder="No limit"
                />
                <NumberField
                  form={form}
                  name="advanceBookingDays"
                  label="Book up to (days ahead)"
                  placeholder="No limit"
                />
              </FieldRow>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
