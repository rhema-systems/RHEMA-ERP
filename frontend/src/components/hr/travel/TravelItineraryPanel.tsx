'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarRange, Plus, MapPin, Loader2, Star, CheckCheck } from 'lucide-react';
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
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
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
import type { StaffTravelItineraryLeg } from '@/types/hr/travel-bookings';

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

// ── Dialogs ──────────────────────────────────────────────────────────────────

const itinerarySchema = z.object({
  title: z.string().min(1, 'Required').max(300),
  totalTravelDays: z.coerce.number().min(0).max(365),
  totalWorkingDays: z.coerce.number().min(0).max(365),
  totalWeekendDays: z.coerce.number().min(0).max(365),
  summaryNotes: z.string().max(2000).optional(),
});

function ItineraryDialog({
  requestId, nextVersion, open, onOpenChange,
}: {
  requestId: string;
  nextVersion: number;
  open: boolean;
  onOpenChange: (v: boolean) => void;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const form = useForm<z.input<typeof itinerarySchema>>({
    resolver: zodResolver(itinerarySchema),
    defaultValues: {
      title: '', totalTravelDays: 0, totalWorkingDays: 0, totalWeekendDays: 0,
    },
  });

  const save = useMutation({
    mutationFn: (values: z.input<typeof itinerarySchema>) => {
      const v = itinerarySchema.parse(values);
      return travelBookingsService.createItinerary({
        ...v,
        staffTravelRequestId: requestId,
        versionNumber: nextVersion,
        // A new version does not take over on its own — promoting it is a separate, deliberate act.
        isCurrentVersion: nextVersion === 1,
      });
    },
    onSuccess: async () => {
      toast({ title: 'Itinerary created' });
      onOpenChange(false);
      form.reset();
      await queryClient.invalidateQueries({ queryKey: ['travel-itineraries', requestId] });
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not create the itinerary', description: e.message }),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>New itinerary — version {nextVersion}</DialogTitle>
          <DialogDescription>
            {nextVersion === 1
              ? 'The first version becomes the current one.'
              : 'Created alongside the current version; promote it when it is ready.'}
          </DialogDescription>
        </DialogHeader>
        <form
          id="itinerary-form"
          className="space-y-4"
          onSubmit={form.handleSubmit((v) => save.mutate(v))}
        >
          <TextField form={form} name="title" label="Title" required />
          <FieldRow>
            <NumberField form={form} name="totalTravelDays" label="Travel days" required />
            <NumberField form={form} name="totalWorkingDays" label="Working days" />
          </FieldRow>
          <NumberField form={form} name="totalWeekendDays" label="Weekend days" />
          <TextareaField form={form} name="summaryNotes" label="Summary" />
        </form>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button type="submit" form="itinerary-form" disabled={save.isPending}>
            {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Create
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
  notes: z.string().max(2000).optional(),
});

function LegDialog({
  itineraryId, requestId, nextOrder, countryOptions, open, onOpenChange,
}: {
  itineraryId: string;
  requestId: string;
  nextOrder: number;
  countryOptions: { value: string; label: string }[];
  open: boolean;
  onOpenChange: (v: boolean) => void;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const form = useForm<z.input<typeof legSchema>>({
    resolver: zodResolver(legSchema),
    defaultValues: { sequenceOrder: nextOrder, legType: 'Departure', legDate: '' },
  });

  const save = useMutation({
    mutationFn: (values: z.input<typeof legSchema>) => {
      const v = legSchema.parse(values);
      return travelBookingsService.addLeg(itineraryId, {
        ...v,
        staffTravelItineraryId: itineraryId,
        originCountryId: v.originCountryId || null,
        destinationCountryId: v.destinationCountryId || null,
        transportMode: v.transportMode ?? null,
        departureDatetime: orNull(v.departureDatetime),
        arrivalDatetime: orNull(v.arrivalDatetime),
      });
    },
    onSuccess: async () => {
      toast({ title: 'Leg added' });
      onOpenChange(false);
      form.reset({ sequenceOrder: nextOrder + 1, legType: 'Transit', legDate: '' });
      await queryClient.invalidateQueries({ queryKey: ['travel-itinerary', itineraryId] });
      await queryClient.invalidateQueries({ queryKey: ['travel-itineraries', requestId] });
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not add the leg', description: e.message }),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Add a leg</DialogTitle>
          <DialogDescription>One movement or stay in the plan.</DialogDescription>
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
          <TextareaField form={form} name="notes" label="Notes" />
        </form>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button type="submit" form="leg-form" disabled={save.isPending}>
            {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Add leg
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
  leg, itineraryId, open, onOpenChange,
}: {
  leg: StaffTravelItineraryLeg | null;
  itineraryId: string;
  open: boolean;
  onOpenChange: (v: boolean) => void;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const form = useForm<z.input<typeof activitySchema>>({
    resolver: zodResolver(activitySchema),
    defaultValues: { activityType: 'Meeting', title: '', isMandatory: false },
  });

  const save = useMutation({
    mutationFn: (values: z.input<typeof activitySchema>) => {
      if (!leg) throw new Error('No leg selected');
      const v = activitySchema.parse(values);
      return travelBookingsService.addActivity(leg.id, {
        ...v,
        staffTravelItineraryLegId: leg.id,
        // An empty string is not a missing email; the server validates the format of what it gets,
        // so `[EmailAddress]` would refuse `""` rather than treat it as "none given".
        contactEmail: v.contactEmail || null,
        contactPhone: v.contactPhone || null,
        startDatetime: orNull(v.startDatetime),
        endDatetime: orNull(v.endDatetime),
      });
    },
    onSuccess: async () => {
      toast({ title: 'Activity added' });
      onOpenChange(false);
      form.reset({ activityType: 'Meeting', title: '', isMandatory: false });
      await queryClient.invalidateQueries({ queryKey: ['travel-itinerary', itineraryId] });
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not add the activity', description: e.message }),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Add an activity</DialogTitle>
          <DialogDescription>
            {leg
              ? `On the ${humanize(leg.legTypeName).toLowerCase()} leg of ${fmtDate(leg.legDate)}.`
              : 'What the traveller is there to do.'}
          </DialogDescription>
        </DialogHeader>
        <form
          id="activity-form"
          className="space-y-4"
          onSubmit={form.handleSubmit((v) => save.mutate(v))}
        >
          <FieldRow>
            <SelectField
              form={form} name="activityType" label="Activity" required
              options={options(ACTIVITY_TYPES)}
            />
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
            Add activity
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── The panel ────────────────────────────────────────────────────────────────

/**
 * The plan for a trip: what the traveller does, day by day.
 *
 * <b>Itineraries are versioned, and versions are the point.</b> A trip is re-planned — a flight
 * moves, a meeting is added — and the desk needs to see what was agreed before, not just what is
 * agreed now. So a new version is created alongside the current one and <i>promoted</i> as a
 * separate act; promoting supersedes its predecessor rather than overwriting it. This panel shows
 * one version at a time and says plainly which is in force.
 *
 * A leg may point at a booking already recorded against the request — that is where the plan and
 * what was actually reserved meet.
 */
export function TravelItineraryPanel({ request }: { request: StaffTravelRequest }) {
  const requestId = request.id;
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [showItineraryDialog, setShowItineraryDialog] = useState(false);
  const [showLegDialog, setShowLegDialog] = useState(false);
  const [activityLeg, setActivityLeg] = useState<StaffTravelItineraryLeg | null>(null);

  const { data: versions, isLoading } = useQuery({
    queryKey: ['travel-itineraries', requestId],
    queryFn: () => travelBookingsService.getItinerariesByRequest(requestId),
  });

  const { data: countries } = useQuery({
    queryKey: ['countries', 'active'],
    queryFn: () => countryService.getActive(),
  });

  const ordered = [...(versions ?? [])].sort((a, b) => b.versionNumber - a.versionNumber);
  const current = ordered.find((v) => v.isCurrentVersion);
  const viewingId = selectedId ?? current?.id ?? ordered[0]?.id ?? null;

  const { data: itinerary } = useQuery({
    queryKey: ['travel-itinerary', viewingId],
    queryFn: () => travelBookingsService.getItinerary(viewingId as string),
    enabled: !!viewingId,
  });

  const promote = useMutation({
    mutationFn: (id: string) => travelBookingsService.setCurrentItinerary(id),
    onSuccess: async () => {
      toast({ title: 'This version is now in force' });
      await queryClient.invalidateQueries({ queryKey: ['travel-itineraries', requestId] });
      await queryClient.invalidateQueries({ queryKey: ['travel-itinerary', viewingId] });
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not promote the version', description: e.message }),
  });

  const countryOptions = (countries ?? []).map((c) => ({ value: c.id, label: c.name }));
  const legs = [...(itinerary?.legs ?? [])].sort((a, b) => a.sequenceOrder - b.sequenceOrder);
  const nextVersion = (ordered[0]?.versionNumber ?? 0) + 1;
  const nextOrder = (legs[legs.length - 1]?.sequenceOrder ?? 0) + 1;
  const viewing = ordered.find((v) => v.id === viewingId);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-10">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (ordered.length === 0) {
    return (
      <>
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={CalendarRange}
              title="No itinerary yet"
              description="Plan the trip day by day once the request has been approved."
            />
            <div className="flex justify-center pb-6">
              <Button onClick={() => setShowItineraryDialog(true)}>
                <Plus className="mr-2 h-4 w-4" /> Create an itinerary
              </Button>
            </div>
          </CardContent>
        </Card>
        <ItineraryDialog
          requestId={requestId}
          nextVersion={1}
          open={showItineraryDialog}
          onOpenChange={setShowItineraryDialog}
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
            {viewing && <StatusBadge status={humanize(viewing.statusName)} />}
            {viewing?.isCurrentVersion && (
              <span className="flex items-center gap-1 text-xs text-muted-foreground">
                <Star className="h-3.5 w-3.5" /> In force
              </span>
            )}
          </div>
          <div className="flex flex-wrap gap-2">
            {viewing && !viewing.isCurrentVersion && (
              <Button
                variant="outline"
                size="sm"
                onClick={() => promote.mutate(viewing.id)}
                disabled={promote.isPending}
              >
                <CheckCheck className="mr-2 h-4 w-4" /> Make this the current version
              </Button>
            )}
            <Button variant="outline" size="sm" onClick={() => setShowItineraryDialog(true)}>
              <Plus className="mr-2 h-4 w-4" /> New version
            </Button>
          </div>
        </CardContent>
      </Card>

      {itinerary && (
        <Card>
          <CardHeader className="flex flex-row items-center justify-between gap-4 pb-3">
            <div>
              <CardTitle className="text-base">{itinerary.title}</CardTitle>
              <p className="mt-1 text-sm text-muted-foreground">
                {itinerary.totalTravelDays} travel days · {itinerary.totalWorkingDays} working ·{' '}
                {itinerary.totalWeekendDays} weekend
              </p>
            </div>
            <Button variant="outline" size="sm" onClick={() => setShowLegDialog(true)}>
              <Plus className="mr-2 h-4 w-4" /> Add leg
            </Button>
          </CardHeader>
          <CardContent className="space-y-3">
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
                        {(leg.flightBookingId || leg.hotelBookingId || leg.groundTransportId) && (
                          <p className="mt-1 text-xs text-muted-foreground">
                            Linked to a booking on this request.
                          </p>
                        )}
                        {leg.notes && (
                          <p className="mt-1 text-sm whitespace-pre-wrap">{leg.notes}</p>
                        )}
                      </div>
                      <Button variant="ghost" size="sm" onClick={() => setActivityLeg(leg)}>
                        <Plus className="mr-2 h-4 w-4" /> Activity
                      </Button>
                    </div>

                    {leg.activities.length > 0 && (
                      <ul className="mt-3 space-y-2 border-t pt-3">
                        {leg.activities.map((a) => (
                          <li key={a.id} className="text-sm">
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
        nextVersion={nextVersion}
        open={showItineraryDialog}
        onOpenChange={setShowItineraryDialog}
      />
      {viewingId && (
        <LegDialog
          itineraryId={viewingId}
          requestId={requestId}
          nextOrder={nextOrder}
          countryOptions={countryOptions}
          open={showLegDialog}
          onOpenChange={setShowLegDialog}
        />
      )}
      {viewingId && (
        <ActivityDialog
          leg={activityLeg}
          itineraryId={viewingId}
          open={!!activityLeg}
          onOpenChange={(v) => !v && setActivityLeg(null)}
        />
      )}
    </div>
  );
}
