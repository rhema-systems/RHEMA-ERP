'use client';

import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  CalendarRange, Plus, MapPin, Loader2, Star, CheckCheck, Pencil, Trash2, MoreHorizontal, Stamp, AlertTriangle, Link2,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import {
  DateField,
  DateTimeField,
  FieldRow,
  NumberField,
  SelectField,
  SwitchField,
  TextField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { useToast } from '@/hooks/use-toast';
import { countryService } from '@/services/hr/country.service';
import { travelBookingsService } from '@/services/hr/travel-bookings.service';
import type { StaffTravelRequest } from '@/types/hr/travel';
import type {
  StaffTravelItinerary, StaffTravelItineraryActivity, StaffTravelItineraryLeg, TravelItineraryStatus,
} from '@/types/hr/travel-bookings';
import { TravelQueryError } from './TravelQueryError';
import { useTravelAccess } from './useTravelAccess';

const LEG_TYPES = ['Departure', 'Transit', 'Arrival', 'Stay', 'DayTrip', 'Return'] as const;
const TRANSPORT_MODES = [
  'Flight', 'Train', 'Bus', 'Car', 'Ferry', 'Helicopter', 'Motorcycle', 'Walk',
] as const;
const ACTIVITY_TYPES = [
  'Meeting', 'Conference', 'Training', 'SiteVisit', 'ClientDinner', 'FreeTime', 'TransitLayover',
  'Other',
] as const;

const humanize = (v: string) => v.replace(/([a-z])([A-Z])/g, '$1 $2');
const options = (values: readonly string[]) => values.map((v) => ({ value: v, label: humanize(v) }));

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtTime = (v?: string | null) =>
  v ? new Date(v).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : null;

/**
 * An untouched `DateTimeField` registers as `''`, which the server's `DateTime` binder rejects with
 * a 400 the form cannot attribute to a field. An optional datetime must be absent, not empty.
 * Sent as the local wall-clock string typed, not converted — a leg departs at the time it departs,
 * wherever the person booking it happens to be sitting.
 */
const orNull = (v?: string | null) => (v && v.trim() ? v : null);
const toLocalInput = (v?: string | null) => (v ? v.slice(0, 16) : '');

/** The trip states a plan is made and changed in (lane 5, slice 5b) — from its draft until it is under way. */
const PLANNABLE = new Set(['Draft', 'Submitted', 'ReturnedForRevision', 'Approved', 'InProgress']);
/** A version still being written; a finalised, superseded or cancelled one is the record of a plan (D-25). */
const isEditable = (s?: TravelItineraryStatus) => s === 'Draft' || s === 'PendingReview';
const describe = (s?: string) => (s === 'Approved' ? 'Finalised' : humanize(s ?? ''));

function useItineraryToast() {
  const { toast } = useToast();
  return {
    done: (title: string) => toast({ title }),
    failed: (title: string) => (e: Error) => toast({ variant: 'destructive', title, description: e.message }),
  };
}

// ── Dialogs ──────────────────────────────────────────────────────────────────

const itinerarySchema = z.object({
  title: z.string().min(1, 'Required').max(300),
  summaryNotes: z.string().max(2000).optional(),
  makeCurrent: z.boolean(),
});

/** A new version, or a draft version's words. The days follow the trip; the status is the server's (D-25). */
function ItineraryDialog({
  requestId, editing, hasVersions, open, onOpenChange,
}: {
  requestId: string;
  /** The draft version to change; null for a new version. */
  editing: StaffTravelItinerary | null;
  hasVersions: boolean;
  open: boolean;
  onOpenChange: (v: boolean) => void;
}) {
  const queryClient = useQueryClient();
  const notify = useItineraryToast();

  const form = useForm<z.input<typeof itinerarySchema>>({
    resolver: zodResolver(itinerarySchema),
    defaultValues: { title: '', summaryNotes: '', makeCurrent: false },
  });
  useEffect(() => {
    if (!open) return;
    form.reset(editing
      ? { title: editing.title, summaryNotes: editing.summaryNotes ?? '', makeCurrent: false }
      : { title: '', summaryNotes: '', makeCurrent: false });
  }, [open, editing, form]);

  const save = useMutation({
    mutationFn: (values: z.input<typeof itinerarySchema>) => {
      const v = itinerarySchema.parse(values);
      return editing
        ? travelBookingsService.updateItinerary({ id: editing.id, title: v.title, summaryNotes: v.summaryNotes || null })
        : travelBookingsService.createItinerary({
            staffTravelRequestId: requestId, title: v.title, summaryNotes: v.summaryNotes || null,
            // A trip's first version is current whatever this says; a later one takes over only when asked.
            isCurrentVersion: v.makeCurrent,
          });
    },
    onSuccess: async () => {
      notify.done(editing ? 'Itinerary saved' : 'Itinerary created');
      onOpenChange(false);
      await queryClient.invalidateQueries({ queryKey: ['travel-itineraries', requestId] });
      if (editing) await queryClient.invalidateQueries({ queryKey: ['travel-itinerary', editing.id] });
    },
    onError: notify.failed(editing ? 'Could not save the itinerary' : 'Could not create the itinerary'),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{editing ? `Version ${editing.versionNumber}` : 'New itinerary version'}</DialogTitle>
          <DialogDescription>
            The travel, working and weekend days are worked out from the trip&apos;s dates.
            {!editing && (hasVersions
              ? ' A new version sits beside the one in force until you make it current.'
              : ' The first version is the current one.')}
          </DialogDescription>
        </DialogHeader>
        <form id="itinerary-form" className="space-y-4" onSubmit={form.handleSubmit((v) => save.mutate(v))}>
          <TextField form={form} name="title" label="Title" required />
          <TextareaField form={form} name="summaryNotes" label="Summary" />
          {!editing && hasVersions && (
            <SwitchField
              form={form} name="makeCurrent" label="Make it the current version now"
              description="The version in force is then superseded — kept as the record of the earlier plan."
            />
          )}
        </form>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button type="submit" form="itinerary-form" disabled={save.isPending}>
            {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {editing ? 'Save' : 'Create'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

const legSchema = z.object({
  sequenceOrder: z.coerce.number().min(1),
  legType: z.enum(LEG_TYPES),
  legDate: z.string().min(1, 'Required'),
  originCity: z.string().max(100).optional(),
  originCountryId: z.string().optional(),
  destinationCity: z.string().max(100).optional(),
  destinationCountryId: z.string().optional(),
  transportMode: z.enum(TRANSPORT_MODES).optional(),
  departureDatetime: z.string().optional(),
  arrivalDatetime: z.string().optional(),
  /** "flight:<id>", "hotel:<id>" or "ground:<id>" — one booking per leg, sent as the matching field. */
  linkedBooking: z.string().optional(),
  notes: z.string().max(2000).optional(),
});

/** Adds or changes a leg; the booking it points at must be this trip's (Q3), its date compared with the leg's (T-19). */
function LegDialog({
  itineraryId, requestId, editing, nextOrder, countryOptions, bookingOptions, open, onOpenChange,
}: {
  itineraryId: string;
  requestId: string;
  editing: StaffTravelItineraryLeg | null;
  nextOrder: number;
  countryOptions: { value: string; label: string }[];
  bookingOptions: { value: string; label: string }[];
  open: boolean;
  onOpenChange: (v: boolean) => void;
}) {
  const queryClient = useQueryClient();
  const notify = useItineraryToast();

  const form = useForm<z.input<typeof legSchema>>({
    resolver: zodResolver(legSchema),
    defaultValues: { sequenceOrder: nextOrder, legType: 'Departure', legDate: '' },
  });
  useEffect(() => {
    if (!open) return;
    if (!editing) {
      form.reset({ sequenceOrder: nextOrder, legType: 'Departure', legDate: '', linkedBooking: '' });
      return;
    }
    form.reset({
      sequenceOrder: editing.sequenceOrder, legType: editing.legType, legDate: editing.legDate.slice(0, 10),
      originCity: editing.originCity ?? '', originCountryId: editing.originCountryId ?? '',
      destinationCity: editing.destinationCity ?? '', destinationCountryId: editing.destinationCountryId ?? '',
      transportMode: editing.transportMode ?? undefined,
      departureDatetime: toLocalInput(editing.departureDatetime), arrivalDatetime: toLocalInput(editing.arrivalDatetime),
      linkedBooking: editing.flightBookingId ? `flight:${editing.flightBookingId}`
        : editing.hotelBookingId ? `hotel:${editing.hotelBookingId}`
          : editing.groundTransportId ? `ground:${editing.groundTransportId}` : '',
      notes: editing.notes ?? '',
    });
  }, [open, editing, nextOrder, form]);

  const save = useMutation({
    mutationFn: (values: z.input<typeof legSchema>) => {
      const v = legSchema.parse(values);
      const [kind, bookingId] = (v.linkedBooking ?? '').split(':');
      const payload = {
        sequenceOrder: v.sequenceOrder, legType: v.legType, legDate: v.legDate,
        originCity: v.originCity, destinationCity: v.destinationCity, notes: v.notes,
        originCountryId: v.originCountryId || null,
        destinationCountryId: v.destinationCountryId || null,
        transportMode: v.transportMode ?? null,
        departureDatetime: orNull(v.departureDatetime),
        arrivalDatetime: orNull(v.arrivalDatetime),
        flightBookingId: kind === 'flight' ? bookingId : null,
        hotelBookingId: kind === 'hotel' ? bookingId : null,
        groundTransportId: kind === 'ground' ? bookingId : null,
      };
      return editing
        ? travelBookingsService.updateLeg({ ...payload, id: editing.id })
        : travelBookingsService.addLeg(itineraryId, { ...payload, staffTravelItineraryId: itineraryId });
    },
    onSuccess: async () => {
      notify.done(editing ? 'Leg saved' : 'Leg added');
      onOpenChange(false);
      await queryClient.invalidateQueries({ queryKey: ['travel-itinerary', itineraryId] });
      await queryClient.invalidateQueries({ queryKey: ['travel-itineraries', requestId] });
    },
    onError: notify.failed(editing ? 'Could not save the leg' : 'Could not add the leg'),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{editing ? 'Change the leg' : 'Add a leg'}</DialogTitle>
          <DialogDescription>
            One movement or stay in the plan, inside the trip&apos;s dates (a day either side).
          </DialogDescription>
        </DialogHeader>
        <form id="leg-form" className="space-y-4" onSubmit={form.handleSubmit((v) => save.mutate(v))}>
          <FieldRow>
            <SelectField form={form} name="legType" label="Leg" required options={options(LEG_TYPES)} />
            <NumberField form={form} name="sequenceOrder" label="Order" required />
          </FieldRow>
          <FieldRow>
            <DateField form={form} name="legDate" label="Date" required />
            <SelectField
              form={form} name="transportMode" label="Transport" options={options(TRANSPORT_MODES)}
              allowEmpty emptyLabel="Not applicable"
            />
          </FieldRow>
          <FieldRow>
            <TextField form={form} name="originCity" label="From (city)" />
            <SelectField
              form={form} name="originCountryId" label="From (country)" options={countryOptions}
              allowEmpty emptyLabel="Not specified"
            />
          </FieldRow>
          <FieldRow>
            <TextField form={form} name="destinationCity" label="To (city)" />
            <SelectField
              form={form} name="destinationCountryId" label="To (country)" options={countryOptions}
              allowEmpty emptyLabel="Not specified"
            />
          </FieldRow>
          <FieldRow>
            <DateTimeField form={form} name="departureDatetime" label="Departs" />
            <DateTimeField form={form} name="arrivalDatetime" label="Arrives" />
          </FieldRow>
          <SelectField
            form={form} name="linkedBooking" label="Linked booking" options={bookingOptions}
            allowEmpty emptyLabel="None"
            description="A flight, hotel or ground booking of this trip. If its dates and the leg's differ, the leg is flagged."
          />
          <TextareaField form={form} name="notes" label="Notes" />
        </form>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button type="submit" form="leg-form" disabled={save.isPending}>
            {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {editing ? 'Save leg' : 'Add leg'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

const activitySchema = z.object({
  activityType: z.enum(ACTIVITY_TYPES),
  title: z.string().min(1, 'Required').max(300),
  description: z.string().max(2000).optional(),
  locationName: z.string().max(300).optional(),
  locationAddress: z.string().max(500).optional(),
  startDatetime: z.string().optional(),
  endDatetime: z.string().optional(),
  contactName: z.string().max(200).optional(),
  contactEmail: z.string().max(200).optional(),
  contactPhone: z.string().max(50).optional(),
  isMandatory: z.boolean(),
});

function ActivityDialog({
  leg, editing, itineraryId, open, onOpenChange,
}: {
  leg: StaffTravelItineraryLeg | null;
  editing: StaffTravelItineraryActivity | null;
  itineraryId: string;
  open: boolean;
  onOpenChange: (v: boolean) => void;
}) {
  const queryClient = useQueryClient();
  const notify = useItineraryToast();

  const form = useForm<z.input<typeof activitySchema>>({
    resolver: zodResolver(activitySchema),
    defaultValues: { activityType: 'Meeting', title: '', isMandatory: false },
  });
  useEffect(() => {
    if (!open) return;
    form.reset(editing
      ? {
          activityType: editing.activityType, title: editing.title, description: editing.description ?? '',
          locationName: editing.locationName ?? '', locationAddress: editing.locationAddress ?? '',
          startDatetime: toLocalInput(editing.startDatetime), endDatetime: toLocalInput(editing.endDatetime),
          contactName: editing.contactName ?? '', contactEmail: editing.contactEmail ?? '',
          contactPhone: editing.contactPhone ?? '', isMandatory: editing.isMandatory,
        }
      : { activityType: 'Meeting', title: '', isMandatory: false });
  }, [open, editing, form]);

  const save = useMutation({
    mutationFn: (values: z.input<typeof activitySchema>) => {
      const v = activitySchema.parse(values);
      const payload = {
        ...v,
        // An empty string is not a missing email; the server validates the format of what it gets,
        // so `[EmailAddress]` would refuse `""` rather than treat it as "none given".
        contactEmail: v.contactEmail || null,
        contactPhone: v.contactPhone || null,
        startDatetime: orNull(v.startDatetime),
        endDatetime: orNull(v.endDatetime),
      };
      if (editing) return travelBookingsService.updateActivity({ ...payload, id: editing.id });
      if (!leg) throw new Error('No leg selected');
      return travelBookingsService.addActivity(leg.id, { ...payload, staffTravelItineraryLegId: leg.id });
    },
    onSuccess: async () => {
      notify.done(editing ? 'Activity saved' : 'Activity added');
      onOpenChange(false);
      await queryClient.invalidateQueries({ queryKey: ['travel-itinerary', itineraryId] });
    },
    onError: notify.failed(editing ? 'Could not save the activity' : 'Could not add the activity'),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{editing ? 'Change the activity' : 'Add an activity'}</DialogTitle>
          <DialogDescription>
            {leg
              ? `On the ${humanize(leg.legTypeName).toLowerCase()} leg of ${fmtDate(leg.legDate)}.`
              : 'What the traveller is there to do.'}
          </DialogDescription>
        </DialogHeader>
        <form id="activity-form" className="space-y-4" onSubmit={form.handleSubmit((v) => save.mutate(v))}>
          <FieldRow>
            <SelectField form={form} name="activityType" label="Activity" required options={options(ACTIVITY_TYPES)} />
            <TextField form={form} name="title" label="Title" required />
          </FieldRow>
          <TextareaField form={form} name="description" label="Description" />
          <FieldRow>
            <TextField form={form} name="locationName" label="Location" />
            <TextField form={form} name="locationAddress" label="Address" />
          </FieldRow>
          <FieldRow>
            <DateTimeField form={form} name="startDatetime" label="Starts" />
            <DateTimeField form={form} name="endDatetime" label="Ends" />
          </FieldRow>
          <FieldRow>
            <TextField form={form} name="contactName" label="Contact" />
            <TextField form={form} name="contactEmail" label="Contact email" type="email" />
          </FieldRow>
          <TextField form={form} name="contactPhone" label="Contact phone" type="tel" />
          <SwitchField
            form={form}
            name="isMandatory"
            label="Mandatory"
            description="The traveller is expected to attend this."
          />
        </form>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button type="submit" form="activity-form" disabled={save.isPending}>
            {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {editing ? 'Save activity' : 'Add activity'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── The panel ────────────────────────────────────────────────────────────────

type Removing =
  | { kind: 'version'; id: string; label: string }
  | { kind: 'leg'; id: string; label: string }
  | { kind: 'activity'; id: string; label: string };

/**
 * The plan for a trip: what the traveller does, day by day.
 *
 * <b>Itineraries are versioned, and versions are the point.</b> A trip is re-planned — a flight
 * moves, a meeting is added — and the desk needs to see what was agreed before, not just what is
 * agreed now. So a new version is created alongside the current one and <i>promoted</i> as a
 * separate act; promoting supersedes its predecessor rather than overwriting it. This panel shows
 * one version at a time and says plainly which is in force.
 *
 * <b>The server owns the status (lane 5, D-25).</b> A version is a Draft while it is written; <i>Finalise</i> marks the
 * version in force Approved — the agreed plan — after which it, its legs and its activities are not changed (a new
 * version is); a version replaced as current is Superseded, and the trip's cancel marks the current one Cancelled.
 * The days are worked out from the trip's dates. The version in force is not deleted. A plan is made while the trip
 * is open — from its draft until it is under way.
 *
 * A leg may point at a booking of the same trip — that is where the plan and what was actually reserved meet — and is
 * flagged when the two disagree on the date (T-19).
 */
export function TravelItineraryPanel({ request }: { request: StaffTravelRequest }) {
  const requestId = request.id;
  const queryClient = useQueryClient();
  const notify = useItineraryToast();
  const { canWrite, canAdmin } = useTravelAccess();
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [versionDialog, setVersionDialog] = useState<{ editing: StaffTravelItinerary | null } | null>(null);
  const [legDialog, setLegDialog] = useState<{ editing: StaffTravelItineraryLeg | null } | null>(null);
  const [activityDialog, setActivityDialog] =
    useState<{ leg: StaffTravelItineraryLeg | null; editing: StaffTravelItineraryActivity | null } | null>(null);
  const [removing, setRemoving] = useState<Removing | null>(null);

  const plannable = PLANNABLE.has(request.status);
  const mayPlan = canWrite && plannable;

  const { data: versions, isLoading, isError, error } = useQuery({
    queryKey: ['travel-itineraries', requestId],
    queryFn: () => travelBookingsService.getItinerariesByRequest(requestId),
  });

  const { data: countries } = useQuery({
    queryKey: ['countries', 'active'],
    queryFn: () => countryService.getActive(),
  });
  // The trip's bookings, for the leg's link — the same queries the Bookings tab reads.
  const { data: flights } = useQuery({
    queryKey: ['travel-flights', requestId],
    queryFn: () => travelBookingsService.getFlightsByRequest(requestId),
  });
  const { data: hotels } = useQuery({
    queryKey: ['travel-hotels', requestId],
    queryFn: () => travelBookingsService.getHotelsByRequest(requestId),
  });
  const { data: grounds } = useQuery({
    queryKey: ['travel-ground', requestId],
    queryFn: () => travelBookingsService.getGroundTransportsByRequest(requestId),
  });

  const ordered = [...(versions ?? [])].sort((a, b) => b.versionNumber - a.versionNumber);
  const current = ordered.find((v) => v.isCurrentVersion);
  const viewingId = selectedId ?? current?.id ?? ordered[0]?.id ?? null;

  const {
    data: itinerary, isError: itineraryFailed, error: itineraryError,
  } = useQuery({
    queryKey: ['travel-itinerary', viewingId],
    queryFn: () => travelBookingsService.getItinerary(viewingId as string),
    enabled: !!viewingId,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['travel-itineraries', requestId] });
    await queryClient.invalidateQueries({ queryKey: ['travel-itinerary', viewingId] });
  };
  const promote = useMutation({
    mutationFn: (id: string) => travelBookingsService.setCurrentItinerary(id),
    onSuccess: async () => { notify.done('This version is now in force'); await refresh(); },
    onError: notify.failed('Could not promote the version'),
  });
  const finalise = useMutation({
    mutationFn: (id: string) => travelBookingsService.finaliseItinerary(id),
    onSuccess: async () => { notify.done('Itinerary finalised'); await refresh(); },
    onError: notify.failed('Could not finalise the itinerary'),
  });
  const remove = useMutation({
    mutationFn: (r: Removing) =>
      r.kind === 'version' ? travelBookingsService.deleteItinerary(r.id)
        : r.kind === 'leg' ? travelBookingsService.deleteLeg(r.id)
          : travelBookingsService.deleteActivity(r.id),
    onSuccess: async (_d, r) => {
      notify.done(r.kind === 'version' ? 'Version deleted' : r.kind === 'leg' ? 'Leg removed' : 'Activity removed');
      setRemoving(null);
      if (r.kind === 'version') setSelectedId(null);
      await refresh();
    },
    onError: notify.failed('Could not remove it'),
  });

  const countryOptions = (countries ?? []).map((c) => ({ value: c.id, label: c.name }));
  const bookingOptions = [
    ...(flights ?? []).map((f) => ({ value: `flight:${f.id}`, label: `Flight ${f.bookingReference || f.airlineName || ''} (${humanize(f.statusName).toLowerCase()})` })),
    ...(hotels ?? []).map((h) => ({ value: `hotel:${h.id}`, label: `${h.hotelName}, ${fmtDate(h.checkInDate)}–${fmtDate(h.checkOutDate)} (${humanize(h.statusName).toLowerCase()})` })),
    ...(grounds ?? []).map((g) => ({ value: `ground:${g.id}`, label: `${humanize(g.transportTypeName)} ${g.bookingReference ?? ''} (${humanize(g.statusName).toLowerCase()})` })),
  ];
  const legs = [...(itinerary?.legs ?? [])].sort((a, b) => a.sequenceOrder - b.sequenceOrder);
  const nextOrder = (legs[legs.length - 1]?.sequenceOrder ?? 0) + 1;
  const viewing = ordered.find((v) => v.id === viewingId);
  const editable = mayPlan && isEditable(itinerary?.status);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-10">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  // A failed read is not "no itinerary yet" — offering to create version 1 over a list that
  // could not be read invites a duplicate.
  if (isError && !versions) return <TravelQueryError error={error} what="the itinerary" />;

  if (ordered.length === 0) {
    return (
      <>
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={CalendarRange}
              title="No itinerary yet"
              description={plannable
                ? 'Plan the trip day by day — while it is open, from its draft until it is under way.'
                : `This trip is ${humanize(request.status).toLowerCase()}, so no itinerary is planned for it.`}
            />
            {mayPlan && (
              <div className="flex justify-center pb-6">
                <Button onClick={() => setVersionDialog({ editing: null })}>
                  <Plus className="mr-2 h-4 w-4" /> Create an itinerary
                </Button>
              </div>
            )}
          </CardContent>
        </Card>
        <ItineraryDialog
          requestId={requestId} editing={null} hasVersions={false}
          open={!!versionDialog} onOpenChange={(v) => { if (!v) setVersionDialog(null); }}
        />
      </>
    );
  }

  return (
    <div className="space-y-4">
      <Card>
        <CardContent className="flex flex-wrap items-center justify-between gap-3 p-4">
          <div className="flex items-center gap-3">
            <Select value={viewingId ?? ''} onValueChange={setSelectedId}>
              <SelectTrigger className="w-72">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {ordered.map((v) => (
                  <SelectItem key={v.id} value={v.id}>
                    Version {v.versionNumber}
                    {v.isCurrentVersion ? ' — in force' : ''} · {v.title}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {viewing && <StatusBadge status={describe(viewing.statusName)} />}
            {viewing?.isCurrentVersion && (
              <span className="flex items-center gap-1 text-xs text-muted-foreground">
                <Star className="h-3.5 w-3.5" /> In force
              </span>
            )}
          </div>
          {mayPlan && (
            <div className="flex flex-wrap gap-2">
              {viewing && !viewing.isCurrentVersion && viewing.status !== 'Superseded' && viewing.status !== 'Cancelled' && (
                <Button variant="outline" size="sm" onClick={() => promote.mutate(viewing.id)} disabled={promote.isPending}>
                  <CheckCheck className="mr-2 h-4 w-4" /> Make this the current version
                </Button>
              )}
              {viewing?.isCurrentVersion && isEditable(viewing.status) && (
                <Button
                  variant="outline" size="sm" onClick={() => finalise.mutate(viewing.id)}
                  disabled={finalise.isPending || legs.length === 0}
                  title={legs.length === 0 ? 'Add a leg first' : undefined}
                >
                  <Stamp className="mr-2 h-4 w-4" /> Finalise
                </Button>
              )}
              {itinerary && isEditable(itinerary.status) && (
                <Button variant="outline" size="sm" onClick={() => setVersionDialog({ editing: itinerary })}>
                  <Pencil className="mr-2 h-4 w-4" /> Edit
                </Button>
              )}
              {viewing && !viewing.isCurrentVersion && canAdmin && (
                <Button
                  variant="outline" size="sm"
                  onClick={() => setRemoving({ kind: 'version', id: viewing.id, label: `version ${viewing.versionNumber}` })}
                >
                  <Trash2 className="mr-2 h-4 w-4" /> Delete
                </Button>
              )}
              <Button variant="outline" size="sm" onClick={() => setVersionDialog({ editing: null })}>
                <Plus className="mr-2 h-4 w-4" /> New version
              </Button>
            </div>
          )}
        </CardContent>
      </Card>

      {itineraryFailed && !itinerary && <TravelQueryError error={itineraryError} what="this itinerary version" />}

      {itinerary && (
        <Card>
          <CardHeader className="flex flex-row items-center justify-between gap-4 pb-3">
            <div>
              <CardTitle className="text-base">{itinerary.title}</CardTitle>
              <p className="mt-1 text-sm text-muted-foreground">
                {itinerary.totalTravelDays} travel days · {itinerary.totalWorkingDays} working ·{' '}
                {itinerary.totalWeekendDays} weekend <span className="text-xs">(from the trip&apos;s dates)</span>
                {itinerary.finalizedAt ? ` · finalised ${fmtDate(itinerary.finalizedAt)}` : ''}
              </p>
            </div>
            {editable && (
              <Button variant="outline" size="sm" onClick={() => setLegDialog({ editing: null })}>
                <Plus className="mr-2 h-4 w-4" /> Add leg
              </Button>
            )}
          </CardHeader>
          <CardContent className="space-y-3">
            {!isEditable(itinerary.status) && (
              <p className="text-xs text-muted-foreground">
                This version is {describe(itinerary.statusName).toLowerCase()} — the record of a plan, not changed. Make a
                new version to change it.
              </p>
            )}
            {itinerary.summaryNotes && (
              <p className="text-sm whitespace-pre-wrap text-muted-foreground">
                {itinerary.summaryNotes}
              </p>
            )}

            {legs.length === 0 ? (
              <EmptyState title="No legs" description="Add the movements that make up this trip." />
            ) : (
              <ol className="space-y-3">
                {legs.map((leg) => (
                  <li key={leg.id} className="rounded-md border p-3">
                    <div className="flex flex-wrap items-start justify-between gap-3">
                      <div>
                        <p className="flex items-center gap-2 text-sm font-medium">
                          <MapPin className="h-3.5 w-3.5 text-muted-foreground" />
                          {humanize(leg.legTypeName)} · {fmtDate(leg.legDate)}
                          {leg.transportModeName && (
                            <span className="text-muted-foreground">
                              by {humanize(leg.transportModeName).toLowerCase()}
                            </span>
                          )}
                        </p>
                        <p className="mt-1 text-sm text-muted-foreground">
                          {leg.originCity || '—'}
                          {leg.originCountryName ? `, ${leg.originCountryName}` : ''} →{' '}
                          {leg.destinationCity || '—'}
                          {leg.destinationCountryName ? `, ${leg.destinationCountryName}` : ''}
                          {fmtTime(leg.departureDatetime) && (
                            <> · {fmtTime(leg.departureDatetime)}–{fmtTime(leg.arrivalDatetime) ?? '—'}</>
                          )}
                        </p>
                        {leg.linkedBooking && (
                          <p className="mt-1 flex items-center gap-1.5 text-xs text-muted-foreground">
                            <Link2 className="h-3.5 w-3.5" />
                            {leg.linkedBooking}{leg.linkedBookingDates ? ` — ${leg.linkedBookingDates}` : ''}
                          </p>
                        )}
                        {leg.linkedBookingDateMismatch && (
                          <p className="mt-1 flex items-center gap-1.5 text-xs text-destructive">
                            <AlertTriangle className="h-3.5 w-3.5" />
                            The leg&apos;s date is not one of its booking&apos;s — one of the two has moved.
                          </p>
                        )}
                        {leg.notes && (
                          <p className="mt-1 text-sm whitespace-pre-wrap">{leg.notes}</p>
                        )}
                      </div>
                      {editable && (
                        <div className="flex items-center gap-1">
                          <Button variant="ghost" size="sm" onClick={() => setActivityDialog({ leg, editing: null })}>
                            <Plus className="mr-2 h-4 w-4" /> Activity
                          </Button>
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button variant="ghost" size="sm" aria-label="Leg actions"><MoreHorizontal className="h-4 w-4" /></Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              <DropdownMenuItem onClick={() => setLegDialog({ editing: leg })}>Edit leg</DropdownMenuItem>
                              {canAdmin && (
                                <DropdownMenuItem
                                  className="text-destructive"
                                  onClick={() => setRemoving({ kind: 'leg', id: leg.id, label: `the ${humanize(leg.legTypeName).toLowerCase()} leg of ${fmtDate(leg.legDate)}` })}
                                >
                                  Remove leg
                                </DropdownMenuItem>
                              )}
                            </DropdownMenuContent>
                          </DropdownMenu>
                        </div>
                      )}
                    </div>

                    {leg.activities.length > 0 && (
                      <ul className="mt-3 space-y-2 border-t pt-3">
                        {leg.activities.map((a) => (
                          <li key={a.id} className="flex items-start justify-between gap-2 text-sm">
                            <div>
                              <span className="font-medium">{a.title}</span>
                              <span className="text-muted-foreground">
                                {' '}· {humanize(a.activityTypeName)}
                                {a.locationName ? ` · ${a.locationName}` : ''}
                                {fmtTime(a.startDatetime) ? ` · ${fmtTime(a.startDatetime)}` : ''}
                                {a.isMandatory ? ' · mandatory' : ''}
                              </span>
                              {a.description && (
                                <p className="text-muted-foreground">{a.description}</p>
                              )}
                            </div>
                            {editable && (
                              <div className="flex shrink-0 gap-1">
                                <Button
                                  variant="ghost" size="sm" aria-label="Edit activity"
                                  onClick={() => setActivityDialog({ leg, editing: a })}
                                >
                                  <Pencil className="h-3.5 w-3.5" />
                                </Button>
                                {canAdmin && (
                                  <Button
                                    variant="ghost" size="sm" aria-label="Remove activity"
                                    onClick={() => setRemoving({ kind: 'activity', id: a.id, label: `“${a.title}”` })}
                                  >
                                    <Trash2 className="h-3.5 w-3.5" />
                                  </Button>
                                )}
                              </div>
                            )}
                          </li>
                        ))}
                      </ul>
                    )}
                  </li>
                ))}
              </ol>
            )}
          </CardContent>
        </Card>
      )}

      <ItineraryDialog
        requestId={requestId}
        editing={versionDialog?.editing ?? null}
        hasVersions
        open={!!versionDialog}
        onOpenChange={(v) => { if (!v) setVersionDialog(null); }}
      />
      {viewingId && (
        <LegDialog
          itineraryId={viewingId}
          requestId={requestId}
          editing={legDialog?.editing ?? null}
          nextOrder={nextOrder}
          countryOptions={countryOptions}
          bookingOptions={bookingOptions}
          open={!!legDialog}
          onOpenChange={(v) => { if (!v) setLegDialog(null); }}
        />
      )}
      {viewingId && (
        <ActivityDialog
          leg={activityDialog?.leg ?? null}
          editing={activityDialog?.editing ?? null}
          itineraryId={viewingId}
          open={!!activityDialog}
          onOpenChange={(v) => { if (!v) setActivityDialog(null); }}
        />
      )}
      <ConfirmationDialog
        open={!!removing}
        onOpenChange={(v) => { if (!v) setRemoving(null); }}
        title="Remove it?"
        description={`${removing?.label ? removing.label[0].toUpperCase() + removing.label.slice(1) : 'It'} is removed for good.${removing?.kind === 'version' ? ' Only a version that is not in force can be.' : ''}`}
        confirmText="Remove"
        variant="destructive"
        isLoading={remove.isPending}
        onConfirm={() => { if (removing) remove.mutate(removing); return false; }}
      />
    </div>
  );
}
