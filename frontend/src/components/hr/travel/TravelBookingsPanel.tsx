'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useForm, type FieldValues, type Path, type PathValue, type UseFormReturn } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plane, BedDouble, Car, Bus, Plus, ShieldAlert, Loader2, MoreHorizontal, Trash2 } from 'lucide-react';
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
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
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
import { CurrencyField } from '@/components/hr/common/CurrencyPicker';
import { SupplierPicker } from '@/components/hr/common/SupplierPicker';
import { Badge } from '@/components/ui/badge';
import { useToast } from '@/hooks/use-toast';
import { countryService } from '@/services/hr/country.service';
import { travelBookingsService, type TravelBookingKind } from '@/services/hr/travel-bookings.service';
import { TravelQueryError } from './TravelQueryError';
import { useTravelAccess } from './useTravelAccess';
import { fmtTravelMoney as fmtMoney } from './travel-format';
import type { StaffTravelRequest } from '@/types/hr/travel';
import type { TravelBookingExceptionState, TravelBookingStatus } from '@/types/hr/travel-bookings';

const CABIN_CLASSES = ['Economy', 'PremiumEconomy', 'Business', 'First'] as const;
const CHANNELS = [
  'SelfService', 'TravelDesk', 'TravelAgency', 'DirectAirline', 'DirectHotel', 'OnlinePortal',
] as const;
const GROUND_TYPES = [
  'Taxi', 'Rideshare', 'Bus', 'Train', 'Metro', 'CompanyVehicle', 'PrivateCarHire', 'Shuttle',
  'Motorcycle', 'Ferry',
] as const;
const VEHICLE_CATEGORIES = [
  'Economy', 'Compact', 'Intermediate', 'FullSize', 'Suv', 'Luxury', 'Minivan', 'Truck',
] as const;
const STAR_RATINGS = ['1', '2', '3', '4', '5', '6', '7'] as const;

const humanize = (v: string) => v.replace(/([a-z])([A-Z])/g, '$1 $2');
const options = (values: readonly string[]) => values.map((v) => ({ value: v, label: humanize(v) }));

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * An untouched `DateTimeField` registers as `''`, which the server's `DateTime` binder rejects with
 * a 400 the form cannot attribute to a field. An optional datetime must be absent, not empty.
 *
 * ⚠ The value is sent as the **local wall-clock string the user typed**, not converted to UTC. A
 * pick-up at 09:00 in Nairobi is 09:00 there whatever the browser's timezone is, and converting
 * through `toIsoInstant` would shift it by the offset between the traveller and whoever is doing
 * the booking. The same reasoning applies to flight segment times server-side.
 */
const orNull = (v?: string | null) => (v && v.trim() ? v : null);

/** A stored local datetime ("2026-11-09T09:00:00") as the `datetime-local` control holds it. */
const toLocalInput = (v?: string | null) => (v ? v.slice(0, 16) : '');

/** Where a booking still stands with its supplier — what an edit or a cancel acts on (lane 5). */
const isLive = (s?: TravelBookingStatus) => s === 'Pending' || s === 'OnHold' || s === 'Confirmed' || s === 'Ticketed';

/** The query each booking kind's table reads, so a verb refreshes the right one. */
const KIND_QUERY: Record<TravelBookingKind, string> = {
  Flight: 'travel-flights',
  Hotel: 'travel-hotels',
  Ground: 'travel-ground',
  CarRental: 'travel-car-rentals',
};

/**
 * The supplier a booking is made with (lane 4, D-1) — required when the trip's policy books through preferred
 * vendors only, which the server says in its refusal. HR's own supplier list (`api/hr/suppliers`).
 */
function VendorField<T extends FieldValues>({ form }: { form: UseFormReturn<T> }) {
  const name = 'vendorId' as Path<T>;
  const value = form.watch(name) as unknown as string | undefined;
  return (
    <SupplierPicker
      label="Supplier"
      value={value || null}
      onChange={(id) => form.setValue(name, (id ?? '') as PathValue<T, Path<T>>, { shouldDirty: true })}
      noneLabel="No supplier named"
    />
  );
}

/** Where a booking's policy exception stands (lane 4, D-8); nothing for a booking within the policy. */
function ExceptionBadge({ state }: { state?: TravelBookingExceptionState }) {
  if (!state || state === 'None') return null;
  const label = state === 'Pending' ? 'Exception awaiting authorisation'
    : state === 'Authorised' ? 'Exception authorised' : 'Exception refused';
  return (
    <Badge variant={state === 'Refused' ? 'destructive' : state === 'Pending' ? 'outline' : 'secondary'} className="ml-2">
      {label}
    </Badge>
  );
}

/** A booking refused for breaching the policy explains itself; the cap is not ours to predict. */
function useBookingToast() {
  const { toast } = useToast();
  return {
    saved: (title = 'Booking saved') => toast({ title }),
    refused: (e: Error) =>
      toast({
        variant: 'destructive',
        title: 'The booking was refused',
        description: e.message ?? 'Please check the details and try again.',
      }),
  };
}

/** After a booking changes: its table, the budget's committed spend and the trip's notes (a cancel adds one). */
function useInvalidateBookings(requestId: string) {
  const queryClient = useQueryClient();
  return (kind: TravelBookingKind) => Promise.all([
    queryClient.invalidateQueries({ queryKey: [KIND_QUERY[kind], requestId] }),
    queryClient.invalidateQueries({ queryKey: ['travel-budget', requestId] }),
    queryClient.invalidateQueries({ queryKey: ['travel-request-comments', requestId] }),
  ]);
}

/** The full record behind a row, loaded when its dialog opens to edit it. */
function useEditRecord<T>(kind: string, editId: string | null, open: boolean, load: (id: string) => Promise<T>) {
  return useQuery({
    queryKey: ['travel-booking-edit', kind, editId],
    queryFn: () => load(editId as string),
    enabled: open && !!editId,
  });
}

// ── Flights ──────────────────────────────────────────────────────────────────

const flightSchema = z.object({
  bookingReference: z.string().max(50).optional(),
  airlineCode: z.string().max(2).optional(),
  airlineName: z.string().max(200).optional(),
  bookingClass: z.enum(CABIN_CLASSES),
  classExceptionApproved: z.boolean(),
  classExceptionReason: z.string().max(1000).optional(),
  bookedBy: z.enum(CHANNELS),
  totalFare: z.coerce.number().min(0),
  taxesAndFees: z.coerce.number().min(0),
  currencyCode: z.string().min(1, 'Select a currency'),
  ticketNumber: z.string().max(50).optional(),
  vendorId: z.string().optional(),
});
type FlightForm = z.input<typeof flightSchema>;

function FlightDialog({
  requestId, editId, open, onOpenChange, defaultCurrency,
}: {
  requestId: string;
  /** The booking to change; null to add one. */
  editId: string | null;
  open: boolean;
  onOpenChange: (v: boolean) => void;
  defaultCurrency: string;
}) {
  const notify = useBookingToast();
  const invalidate = useInvalidateBookings(requestId);
  const blank: FlightForm = {
    bookingClass: 'Economy', bookedBy: 'TravelDesk', classExceptionApproved: false, totalFare: 0,
    taxesAndFees: 0, currencyCode: defaultCurrency, vendorId: '',
  };
  const form = useForm<FlightForm>({ resolver: zodResolver(flightSchema), defaultValues: blank });
  const { data: existing } = useEditRecord('flight', editId, open, (id) => travelBookingsService.getFlight(id));

  useEffect(() => {
    if (!open) return;
    if (!editId) { form.reset(blank); return; }
    if (existing) {
      form.reset({
        bookingReference: existing.bookingReference ?? '', airlineCode: existing.airlineCode ?? '',
        airlineName: existing.airlineName ?? '', bookingClass: existing.bookingClass,
        // A booking that already carries an exception keeps asking for it; dropping the switch would make the
        // server refuse the edit as an unasked breach.
        classExceptionApproved: !!existing.exceptionState && existing.exceptionState !== 'None',
        classExceptionReason: existing.classExceptionReason ?? '', bookedBy: existing.bookedBy,
        totalFare: existing.totalFare, taxesAndFees: existing.taxesAndFees, currencyCode: existing.currencyCode,
        ticketNumber: existing.ticketNumber ?? '', vendorId: existing.vendorId ?? '',
      });
    }
  }, [open, editId, existing, form]);

  const save = useMutation({
    mutationFn: (values: FlightForm) => {
      const v = flightSchema.parse(values);
      const payload = { ...v, vendorId: v.vendorId || null };
      return editId
        ? travelBookingsService.updateFlight({ ...payload, id: editId })
        : travelBookingsService.createFlight({ ...payload, staffTravelRequestId: requestId });
    },
    onSuccess: async () => {
      notify.saved();
      onOpenChange(false);
      await invalidate('Flight');
    },
    onError: (e: Error) => notify.refused(e),
  });

  const wantsException = !!form.watch('classExceptionApproved');

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{editId ? 'Change the flight booking' : 'Add a flight booking'}</DialogTitle>
          <DialogDescription>
            The cabin class, how far ahead it is booked and the supplier are checked against the trip&apos;s
            travel policy. {editId ? 'The status is changed from the row’s menu, not here.' : 'It is saved Pending; confirm it from the row’s menu.'}
          </DialogDescription>
        </DialogHeader>
        {editId && !existing ? (
          <div className="flex justify-center p-6"><Loader2 className="h-5 w-5 animate-spin text-muted-foreground" /></div>
        ) : (
          <form id="flight-form" className="space-y-4" onSubmit={form.handleSubmit((v) => save.mutate(v))}>
            <FieldRow>
              <TextField form={form} name="airlineName" label="Airline" />
              <TextField form={form} name="airlineCode" label="Airline code" placeholder="KQ" />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="bookingReference" label="Booking reference" />
              {editId && <TextField form={form} name="ticketNumber" label="Ticket number" />}
            </FieldRow>
            <FieldRow>
              <SelectField form={form} name="bookingClass" label="Cabin class" required options={options(CABIN_CLASSES)} />
              <SelectField form={form} name="bookedBy" label="Booked through" required options={options(CHANNELS)} />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="totalFare" label="Fare" required />
              <NumberField form={form} name="taxesAndFees" label="Taxes and fees" />
            </FieldRow>
            <CurrencyField form={form} name="currencyCode" label="Currency" required />

            <VendorField form={form} />

            {/*
              Lane 4, D-8: asking for an exception, not granting one. A breaching flight is saved awaiting
              authorisation; a travel administrator other than the booker authorises it on Staff Travel →
              Policy breaches before it can be confirmed or ticketed.
            */}
            <SwitchField
              form={form}
              name="classExceptionApproved"
              label="Ask for an exception to the policy"
              description="For a class above the cap, or a flight booked later than the policy asks. Saved awaiting authorisation by another travel administrator; until then it cannot be confirmed."
            />
            {wantsException && (
              <TextareaField
                form={form}
                name="classExceptionReason"
                label="Why the exception is needed"
                placeholder="Required — kept on the booking and shown to whoever decides it."
              />
            )}
          </form>
        )}
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button type="submit" form="flight-form" disabled={save.isPending || (!!editId && !existing)}>
            {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {editId ? 'Save changes' : 'Add booking'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── Flight segments (lane 5, D3 — the door nobody had) ──────────────────────

const segmentSchema = z
  .object({
    segmentOrder: z.coerce.number().int().min(1),
    flightNumber: z.string().min(1, 'Required').max(10),
    operatingCarrier: z.string().min(2, 'Two letters').max(2, 'Two letters'),
    originAirport: z.string().min(3, 'Three letters').max(3, 'Three letters'),
    destinationAirport: z.string().min(3, 'Three letters').max(3, 'Three letters'),
    departureDatetime: z.string().min(1, 'Required'),
    arrivalDatetime: z.string().min(1, 'Required'),
    seatNumber: z.string().max(10).optional(),
    isLayover: z.boolean(),
  });
type SegmentForm = z.input<typeof segmentSchema>;

function FlightSegmentsDialog({
  requestId, flightId, open, onOpenChange, writable, canAdmin,
}: {
  requestId: string;
  flightId: string | null;
  open: boolean;
  onOpenChange: (v: boolean) => void;
  /** The flight is live on an approved trip and the caller may write. */
  writable: boolean;
  canAdmin: boolean;
}) {
  const queryClient = useQueryClient();
  const notify = useBookingToast();
  const { data: segments, isLoading } = useQuery({
    queryKey: ['travel-flight-segments', flightId],
    queryFn: () => travelBookingsService.getSegments(flightId as string),
    enabled: open && !!flightId,
  });
  const ordered = [...(segments ?? [])].sort((a, b) => a.segmentOrder - b.segmentOrder);
  const form = useForm<SegmentForm>({
    resolver: zodResolver(segmentSchema),
    defaultValues: { segmentOrder: 1, flightNumber: '', operatingCarrier: '', originAirport: '', destinationAirport: '', departureDatetime: '', arrivalDatetime: '', isLayover: false },
  });
  useEffect(() => {
    if (open) form.reset({ ...form.getValues(), segmentOrder: ordered.length + 1, flightNumber: '', seatNumber: '' });
  }, [open, segments?.length]);

  const refresh = () => Promise.all([
    queryClient.invalidateQueries({ queryKey: ['travel-flight-segments', flightId] }),
    queryClient.invalidateQueries({ queryKey: ['travel-flights', requestId] }),
  ]);
  const add = useMutation({
    mutationFn: (values: SegmentForm) => {
      const v = segmentSchema.parse(values);
      return travelBookingsService.addSegment(flightId as string, {
        ...v, staffTravelFlightBookingId: flightId as string,
        operatingCarrier: v.operatingCarrier.toUpperCase(), originAirport: v.originAirport.toUpperCase(),
        destinationAirport: v.destinationAirport.toUpperCase(), seatNumber: v.seatNumber || null,
      });
    },
    onSuccess: async () => { notify.saved('Segment added'); await refresh(); },
    onError: (e: Error) => notify.refused(e),
  });
  const remove = useMutation({
    mutationFn: (id: string) => travelBookingsService.deleteSegment(id),
    onSuccess: async () => { notify.saved('Segment removed'); await refresh(); },
    onError: (e: Error) => notify.refused(e),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] max-w-3xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Flight segments</DialogTitle>
          <DialogDescription>
            Each leg the flight flies, in order. Times are local to their airports; a segment falls inside the
            trip&apos;s dates, allowing the day before and the day after.
          </DialogDescription>
        </DialogHeader>
        {isLoading ? (
          <div className="flex justify-center p-6"><Loader2 className="h-5 w-5 animate-spin text-muted-foreground" /></div>
        ) : ordered.length === 0 ? (
          <p className="text-sm text-muted-foreground">No segments yet.</p>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-10">#</TableHead>
                <TableHead>Flight</TableHead>
                <TableHead>Route</TableHead>
                <TableHead>Departs</TableHead>
                <TableHead>Arrives</TableHead>
                <TableHead className="w-12" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {ordered.map((s) => (
                <TableRow key={s.id}>
                  <TableCell>{s.segmentOrder}</TableCell>
                  <TableCell className="font-medium">{s.operatingCarrier} {s.flightNumber}</TableCell>
                  <TableCell>{s.originAirport} → {s.destinationAirport}</TableCell>
                  <TableCell className="whitespace-nowrap">{toLocalInput(s.departureDatetime).replace('T', ' ')}</TableCell>
                  <TableCell className="whitespace-nowrap">{toLocalInput(s.arrivalDatetime).replace('T', ' ')}</TableCell>
                  <TableCell>
                    {writable && canAdmin && (
                      <Button
                        variant="ghost" size="sm" aria-label="Remove segment"
                        disabled={remove.isPending} onClick={() => remove.mutate(s.id)}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
        {writable && (
          <form
            id="segment-form" className="space-y-3 rounded-md border p-3"
            onSubmit={form.handleSubmit((v) => add.mutate(v))}
          >
            <p className="text-sm font-medium">Add a segment</p>
            <FieldRow>
              <NumberField form={form} name="segmentOrder" label="Order" required />
              <TextField form={form} name="operatingCarrier" label="Carrier" required placeholder="KQ" />
              <TextField form={form} name="flightNumber" label="Flight number" required placeholder="511" />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="originAirport" label="From" required placeholder="ACC" />
              <TextField form={form} name="destinationAirport" label="To" required placeholder="LOS" />
              <TextField form={form} name="seatNumber" label="Seat" />
            </FieldRow>
            <FieldRow>
              <DateTimeField form={form} name="departureDatetime" label="Departs (local)" required />
              <DateTimeField form={form} name="arrivalDatetime" label="Arrives (local)" required />
            </FieldRow>
            <SwitchField form={form} name="isLayover" label="A connection — the traveller changes planes after it" />
          </form>
        )}
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Close</Button>
          {writable && (
            <Button type="submit" form="segment-form" disabled={add.isPending}>
              {add.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Add segment
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── Hotels ───────────────────────────────────────────────────────────────────

const hotelSchema = z
  .object({
    hotelName: z.string().min(1, 'Required').max(300),
    hotelChain: z.string().max(200).optional(),
    hotelAddress: z.string().max(500).optional(),
    city: z.string().min(1, 'Required').max(100),
    countryId: z.string().min(1, 'Select a country'),
    starRating: z.string().optional(),
    checkInDate: z.string().min(1, 'Required'),
    checkOutDate: z.string().min(1, 'Required'),
    roomType: z.string().max(100).optional(),
    ratePerNight: z.coerce.number().min(0),
    currencyCode: z.string().min(1, 'Select a currency'),
    rateExceptionApproved: z.boolean(),
    rateExceptionReason: z.string().max(1000).optional(),
    bookedBy: z.enum(CHANNELS),
    bookingReference: z.string().max(100).optional(),
    cancellationPolicy: z.string().max(1000).optional(),
    vendorId: z.string().optional(),
  })
  .refine((v) => !v.checkOutDate || !v.checkInDate || v.checkOutDate >= v.checkInDate, {
    message: 'Check-out cannot be before check-in',
    path: ['checkOutDate'],
  });
type HotelForm = z.input<typeof hotelSchema>;

function HotelDialog({
  requestId, editId, open, onOpenChange, countryOptions, defaultCurrency, defaultCountryId,
}: {
  requestId: string;
  editId: string | null;
  open: boolean;
  onOpenChange: (v: boolean) => void;
  countryOptions: { value: string; label: string }[];
  defaultCurrency: string;
  defaultCountryId: string;
}) {
  const notify = useBookingToast();
  const invalidate = useInvalidateBookings(requestId);
  const blank: HotelForm = {
    hotelName: '', city: '', countryId: defaultCountryId, starRating: '', checkInDate: '', checkOutDate: '',
    ratePerNight: 0, currencyCode: defaultCurrency, rateExceptionApproved: false, bookedBy: 'TravelDesk', vendorId: '',
  };
  const form = useForm<HotelForm>({ resolver: zodResolver(hotelSchema), defaultValues: blank });
  const { data: existing } = useEditRecord('hotel', editId, open, (id) => travelBookingsService.getHotel(id));

  useEffect(() => {
    if (!open) return;
    if (!editId) { form.reset(blank); return; }
    if (existing) {
      form.reset({
        hotelName: existing.hotelName, hotelChain: existing.hotelChain ?? '', hotelAddress: existing.hotelAddress ?? '',
        city: existing.city, countryId: existing.countryId,
        starRating: existing.starRating ? String(existing.starRating) : '',
        checkInDate: existing.checkInDate.slice(0, 10), checkOutDate: existing.checkOutDate.slice(0, 10),
        roomType: existing.roomType ?? '', ratePerNight: existing.ratePerNight, currencyCode: existing.currencyCode,
        rateExceptionApproved: !!existing.exceptionState && existing.exceptionState !== 'None',
        rateExceptionReason: existing.rateExceptionReason ?? '', bookedBy: existing.bookedBy,
        bookingReference: existing.bookingReference ?? '', cancellationPolicy: existing.cancellationPolicy ?? '',
        vendorId: existing.vendorId ?? '',
      });
    }
  }, [open, editId, existing, form]);

  const save = useMutation({
    mutationFn: (values: HotelForm) => {
      const v = hotelSchema.parse(values);
      const payload = { ...v, vendorId: v.vendorId || null, starRating: v.starRating ? Number(v.starRating) : null };
      return editId
        ? travelBookingsService.updateHotel({ ...payload, id: editId })
        : travelBookingsService.createHotel({ ...payload, staffTravelRequestId: requestId });
    },
    onSuccess: async () => {
      notify.saved();
      onOpenChange(false);
      await invalidate('Hotel');
    },
    onError: (e: Error) => notify.refused(e),
  });

  const checkIn = form.watch('checkInDate');
  const checkOut = form.watch('checkOutDate');
  const rate = Number(form.watch('ratePerNight')) || 0;
  // Previewed with the same arithmetic the server runs. Two independent computations make a
  // disagreement visible instead of silent — the lesson from the appraisal scoring model.
  const nights = checkIn && checkOut
    ? Math.max(0, Math.round(
        (new Date(checkOut).getTime() - new Date(checkIn).getTime()) / 86_400_000))
    : 0;
  const wantsException = !!form.watch('rateExceptionApproved');

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{editId ? 'Change the hotel booking' : 'Add a hotel booking'}</DialogTitle>
          <DialogDescription>
            The nightly rate (converted into the policy&apos;s currency), how far ahead it is booked and the
            supplier are checked against the trip&apos;s travel policy; the stay falls inside the trip&apos;s dates.
          </DialogDescription>
        </DialogHeader>
        {editId && !existing ? (
          <div className="flex justify-center p-6"><Loader2 className="h-5 w-5 animate-spin text-muted-foreground" /></div>
        ) : (
          <form id="hotel-form" className="space-y-4" onSubmit={form.handleSubmit((v) => save.mutate(v))}>
            <FieldRow>
              <TextField form={form} name="hotelName" label="Hotel" required />
              <TextField form={form} name="hotelChain" label="Chain" />
            </FieldRow>
            <TextField form={form} name="hotelAddress" label="Address" />
            <FieldRow>
              <TextField form={form} name="city" label="City" required />
              <SelectField form={form} name="countryId" label="Country" required options={countryOptions} />
            </FieldRow>
            <FieldRow>
              <DateField form={form} name="checkInDate" label="Check in" required />
              <DateField form={form} name="checkOutDate" label="Check out" required />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="ratePerNight" label="Rate per night" required />
              <CurrencyField form={form} name="currencyCode" label="Currency" required />
            </FieldRow>

            <p className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
              {nights > 0
                ? `${nights} night${nights === 1 ? '' : 's'} — ${fmtMoney(rate * nights, form.watch('currencyCode'))} in total.`
                : 'Choose the dates to see the length of stay and the total.'}
              {' '}Both are worked out from the dates and the rate; the server does the same.
            </p>

            <FieldRow>
              <TextField form={form} name="roomType" label="Room type" />
              <SelectField
                form={form} name="starRating" label="Star rating" allowEmpty emptyLabel="Not rated"
                options={STAR_RATINGS.map((s) => ({ value: s, label: `${s} star${s === '1' ? '' : 's'}` }))}
              />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="bookingReference" label="Booking reference" />
              <SelectField form={form} name="bookedBy" label="Booked through" required options={options(CHANNELS)} />
            </FieldRow>
            <TextareaField form={form} name="cancellationPolicy" label="Cancellation policy" />
            <VendorField form={form} />

            {/* Lane 4, D-8 — asking, not granting; see the flight dialog. */}
            <SwitchField
              form={form}
              name="rateExceptionApproved"
              label="Ask for an exception to the policy"
              description="For a rate above the cap, or a stay booked later than the policy asks. Saved awaiting authorisation by another travel administrator; until then it cannot be confirmed."
            />
            {wantsException && (
              <TextareaField
                form={form}
                name="rateExceptionReason"
                label="Why the exception is needed"
                placeholder="Required — kept on the booking and shown to whoever decides it."
              />
            )}
          </form>
        )}
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button type="submit" form="hotel-form" disabled={save.isPending || (!!editId && !existing)}>
            {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {editId ? 'Save changes' : 'Add booking'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── Ground transport ─────────────────────────────────────────────────────────

const groundSchema = z
  .object({
    transportType: z.enum(GROUND_TYPES),
    vehicleAssetId: z.string().optional(),
    driverEmployeeId: z.string().optional(),
    fleetTripDestinationId: z.string().optional(),
    bookingReference: z.string().max(100).optional(),
    pickupLocation: z.string().max(300).optional(),
    dropoffLocation: z.string().max(300).optional(),
    pickupDatetime: z.string().optional(),
    dropoffDatetime: z.string().optional(),
    estimatedCost: z.coerce.number().min(0).optional(),
    actualCost: z.string().optional(),
    currencyCode: z.string().min(1, 'Select a currency'),
    notes: z.string().max(2000).optional(),
    vendorId: z.string().optional(),
  })
  .refine((v) => v.transportType !== 'CompanyVehicle' || !!v.vehicleAssetId, {
    message: 'Choose the company vehicle to reserve',
    path: ['vehicleAssetId'],
  })
  .refine((v) => v.transportType !== 'CompanyVehicle' || (!!v.pickupDatetime && !!v.dropoffDatetime), {
    message: 'A company vehicle is reserved from a pick-up to a drop-off time — give both',
    path: ['dropoffDatetime'],
  })
  .refine((v) => !v.actualCost || (!Number.isNaN(Number(v.actualCost)) && Number(v.actualCost) >= 0), {
    message: 'A cost of 0 or more',
    path: ['actualCost'],
  });
type GroundForm = z.input<typeof groundSchema>;

/**
 * The company vehicle, its driver and Fleet's destination, chosen from what travel reads from Fleet (lane 6, FX-4) —
 * each vehicle and driver saying why it is not available over the leg's window. The server refuses an unavailable
 * one on save; the picker says so first.
 */
function FleetFields({
  form, requestId, excludeFleetTripId, applyDefaults,
}: {
  form: UseFormReturn<GroundForm>;
  requestId: string;
  excludeFleetTripId: string | null;
  applyDefaults: boolean;
}) {
  const pickup = form.watch('pickupDatetime');
  const dropoff = form.watch('dropoffDatetime');
  const { data: opts, isLoading } = useQuery({
    queryKey: ['travel-fleet-options', requestId, pickup, dropoff, excludeFleetTripId],
    queryFn: () => travelBookingsService.getFleetOptions(requestId, orNull(pickup), orNull(dropoff), excludeFleetTripId),
  });
  useEffect(() => {
    if (!applyDefaults || !opts) return;
    if (!form.getValues('vehicleAssetId') && opts.defaultVehicleAssetId)
      form.setValue('vehicleAssetId', opts.defaultVehicleAssetId);
    if (!form.getValues('driverEmployeeId') && opts.defaultDriverEmployeeId)
      form.setValue('driverEmployeeId', opts.defaultDriverEmployeeId);
  }, [opts, applyDefaults, form]);

  const vehicleId = form.watch('vehicleAssetId');
  const driverId = form.watch('driverEmployeeId');
  const vehicle = opts?.vehicles.find((v) => v.vehicleAssetId === vehicleId);
  const driver = opts?.drivers.find((d) => d.employeeId === driverId);
  if (isLoading) return <div className="flex justify-center p-3"><Loader2 className="h-4 w-4 animate-spin text-muted-foreground" /></div>;
  if (!opts) return null;

  return (
    <div className="space-y-3 rounded-md border p-3">
      {opts.vehicles.length === 0 ? (
        <p className="text-sm text-muted-foreground">Fleet holds no active vehicle to reserve.</p>
      ) : (
        <SelectField
          form={form} name="vehicleAssetId" label="Vehicle" required
          options={opts.vehicles.map((v) => ({
            value: v.vehicleAssetId,
            label: `${v.name}${v.licensePlate ? ` · ${v.licensePlate}` : ''}${v.assignedTo ? ` · ${v.assignedTo}` : ''}${v.available ? '' : ' — not available'}`,
          }))}
        />
      )}
      {vehicle && !vehicle.available && (
        <p className="flex items-start gap-1.5 text-xs text-destructive">
          <ShieldAlert className="mt-0.5 h-3.5 w-3.5 shrink-0" />
          {[...vehicle.overlaps.map((o) => `Planned: ${o}`), ...vehicle.blockingCompliance].join(' · ')}
        </p>
      )}
      <SelectField
        form={form} name="driverEmployeeId" label="Driver" allowEmpty emptyLabel="Fleet's assigned driver, or none"
        options={opts.drivers.map((d) => ({
          value: d.employeeId,
          label: `${d.name}${d.employeeNumber ? ` (${d.employeeNumber})` : ''}${d.flags.length ? ' — ⚠' : ''}`,
        }))}
        description="Drivers with a verified licence valid through the trip."
      />
      {driver && driver.flags.length > 0 && (
        <p className="text-xs text-destructive">{driver.flags.join(' · ')}</p>
      )}
      {opts.destinationRequired && (
        <SelectField
          form={form} name="fleetTripDestinationId" label="Fleet destination" required
          options={opts.destinations.map((d) => ({ value: d.id, label: d.name }))}
          description="Fleet's settings ask a predefined destination for every trip."
        />
      )}
      <p className="text-xs text-muted-foreground">
        {opts.approvalRoutePublished
          ? 'The trip is sent to the transport office for approval; the leg shows Fleet’s decision.'
          : 'Fleet publishes no approval route yet, so the trip is reserved as a draft in Fleet and the vehicle is not held until it does.'}
      </p>
    </div>
  );
}

function GroundDialog({
  requestId, editId, open, onOpenChange, defaultCurrency,
}: {
  requestId: string;
  editId: string | null;
  open: boolean;
  onOpenChange: (v: boolean) => void;
  defaultCurrency: string;
}) {
  const notify = useBookingToast();
  const invalidate = useInvalidateBookings(requestId);
  const blank: GroundForm = {
    transportType: 'Taxi', currencyCode: defaultCurrency, estimatedCost: 0, actualCost: '', vendorId: '',
    vehicleAssetId: '', driverEmployeeId: '', fleetTripDestinationId: '',
  };
  const form = useForm<GroundForm>({ resolver: zodResolver(groundSchema), defaultValues: blank });
  const { data: existing } = useEditRecord('ground', editId, open, (id) => travelBookingsService.getGroundTransport(id));

  useEffect(() => {
    if (!open) return;
    if (!editId) { form.reset(blank); return; }
    if (existing) {
      form.reset({
        transportType: existing.transportType, bookingReference: existing.bookingReference ?? '',
        pickupLocation: existing.pickupLocation ?? '', dropoffLocation: existing.dropoffLocation ?? '',
        pickupDatetime: toLocalInput(existing.pickupDatetime), dropoffDatetime: toLocalInput(existing.dropoffDatetime),
        estimatedCost: existing.estimatedCost ?? 0,
        actualCost: existing.actualCost == null ? '' : String(existing.actualCost),
        currencyCode: existing.currencyCode, notes: existing.notes ?? '', vendorId: existing.vendorId ?? '',
        vehicleAssetId: existing.vehicleAssetId ?? '', driverEmployeeId: existing.driverEmployeeId ?? '',
        fleetTripDestinationId: '',
      });
    }
  }, [open, editId, existing, form]);

  const save = useMutation({
    mutationFn: (values: GroundForm) => {
      const v = groundSchema.parse(values);
      const common = {
        transportType: v.transportType, bookingReference: v.bookingReference, pickupLocation: v.pickupLocation,
        dropoffLocation: v.dropoffLocation, estimatedCost: v.estimatedCost, currencyCode: v.currencyCode,
        notes: v.notes, vendorId: v.vendorId || null,
        actualCost: v.actualCost ? Number(v.actualCost) : null,
        pickupDatetime: orNull(v.pickupDatetime), dropoffDatetime: orNull(v.dropoffDatetime),
        // Lane 6: a company vehicle's fleet trip — ignored by the server on any other kind of leg.
        vehicleAssetId: v.vehicleAssetId || null,
        driverEmployeeId: v.driverEmployeeId || null,
        fleetTripDestinationId: v.fleetTripDestinationId || null,
      };
      return editId
        ? travelBookingsService.updateGroundTransport({ ...common, id: editId })
        : travelBookingsService.createGroundTransport({ ...common, staffTravelRequestId: requestId });
    },
    onSuccess: async () => {
      notify.saved();
      onOpenChange(false);
      await invalidate('Ground');
    },
    onError: (e: Error) => notify.refused(e),
  });

  const isCompanyVehicle = form.watch('transportType') === 'CompanyVehicle';

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{editId ? 'Change the ground transport' : 'Add ground transport'}</DialogTitle>
          <DialogDescription>
            A company vehicle is reserved in Fleet — the vehicle and driver checked for the trip&apos;s days — rather
            than noted here. Pick-up and drop-off fall inside the trip&apos;s dates.
          </DialogDescription>
        </DialogHeader>
        {editId && !existing ? (
          <div className="flex justify-center p-6"><Loader2 className="h-5 w-5 animate-spin text-muted-foreground" /></div>
        ) : (
          <form id="ground-form" className="space-y-4" onSubmit={form.handleSubmit((v) => save.mutate(v))}>
            <FieldRow>
              {/* Lane 6: a leg does not turn into a company vehicle or out of one — the fleet trip would be orphaned. */}
              <SelectField
                form={form} name="transportType" label="Transport" required options={options(GROUND_TYPES)}
                disabled={!!editId && existing?.transportType === 'CompanyVehicle'}
              />
              <TextField form={form} name="bookingReference" label="Booking reference" />
            </FieldRow>

            <FieldRow>
              <TextField form={form} name="pickupLocation" label="Pick up" />
              <TextField form={form} name="dropoffLocation" label="Drop off" />
            </FieldRow>
            <FieldRow>
              <DateTimeField form={form} name="pickupDatetime" label="Pick-up time" required={isCompanyVehicle} />
              <DateTimeField form={form} name="dropoffDatetime" label="Drop-off time" required={isCompanyVehicle} />
            </FieldRow>

            {isCompanyVehicle && (
              <FleetFields
                form={form} requestId={requestId} excludeFleetTripId={existing?.fleetTripId ?? null}
                applyDefaults={!editId}
              />
            )}
            {/* Lane 6 (FX-6): a company vehicle's cost is what Fleet books against its trip — the budget reads that. */}
            {isCompanyVehicle ? (
              <p className="text-xs text-muted-foreground">
                The vehicle&apos;s costs — fuel, tolls and the like — are what Fleet records against its trip; the
                trip&apos;s budget counts those.
              </p>
            ) : (
              <FieldRow>
                <NumberField form={form} name="estimatedCost" label="Estimated cost" />
                <NumberField
                  form={form} name="actualCost" label="Actual cost"
                  description="What it came to, once known — the budget counts it in place of the estimate."
                />
              </FieldRow>
            )}
            <CurrencyField form={form} name="currencyCode" label="Currency" required />
            {/* A company vehicle has no supplier; the policy's preferred-vendor rule does not apply to it. */}
            {!isCompanyVehicle && <VendorField form={form} />}
            <TextareaField form={form} name="notes" label="Notes" />
          </form>
        )}
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button type="submit" form="ground-form" disabled={save.isPending || (!!editId && !existing)}>
            {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {editId ? 'Save changes' : 'Add transport'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── Car rentals ──────────────────────────────────────────────────────────────

const carSchema = z
  .object({
    bookingReference: z.string().max(100).optional(),
    pickupLocation: z.string().max(300).optional(),
    dropoffLocation: z.string().max(300).optional(),
    pickupDatetime: z.string().min(1, 'Required'),
    dropoffDatetime: z.string().min(1, 'Required'),
    vehicleCategory: z.enum(VEHICLE_CATEGORIES),
    vehicleModel: z.string().max(100).optional(),
    dailyRate: z.coerce.number().min(0),
    currencyCode: z.string().min(1, 'Select a currency'),
    insuranceIncluded: z.boolean(),
    fuelPolicy: z.string().max(100).optional(),
    driverLicenseRequired: z.boolean(),
    vendorId: z.string().optional(),
  })
  .refine((v) => !v.dropoffDatetime || !v.pickupDatetime || v.dropoffDatetime >= v.pickupDatetime, {
    message: 'Drop-off cannot be before pick-up',
    path: ['dropoffDatetime'],
  });
type CarForm = z.input<typeof carSchema>;

function CarRentalDialog({
  requestId, editId, open, onOpenChange, defaultCurrency,
}: {
  requestId: string;
  editId: string | null;
  open: boolean;
  onOpenChange: (v: boolean) => void;
  defaultCurrency: string;
}) {
  const notify = useBookingToast();
  const invalidate = useInvalidateBookings(requestId);
  const blank: CarForm = {
    pickupDatetime: '', dropoffDatetime: '', vehicleCategory: 'Economy', dailyRate: 0,
    currencyCode: defaultCurrency, insuranceIncluded: true, driverLicenseRequired: true, vendorId: '',
  };
  const form = useForm<CarForm>({ resolver: zodResolver(carSchema), defaultValues: blank });
  const { data: existing } = useEditRecord('car', editId, open, (id) => travelBookingsService.getCarRental(id));

  useEffect(() => {
    if (!open) return;
    if (!editId) { form.reset(blank); return; }
    if (existing) {
      form.reset({
        bookingReference: existing.bookingReference ?? '', pickupLocation: existing.pickupLocation ?? '',
        dropoffLocation: existing.dropoffLocation ?? '', pickupDatetime: toLocalInput(existing.pickupDatetime),
        dropoffDatetime: toLocalInput(existing.dropoffDatetime), vehicleCategory: existing.vehicleCategory,
        vehicleModel: existing.vehicleModel ?? '', dailyRate: existing.dailyRate, currencyCode: existing.currencyCode,
        insuranceIncluded: existing.insuranceIncluded, fuelPolicy: existing.fuelPolicy ?? '',
        driverLicenseRequired: existing.driverLicenseRequired, vendorId: existing.vendorId ?? '',
      });
    }
  }, [open, editId, existing, form]);

  const save = useMutation({
    mutationFn: (values: CarForm) => {
      const v = carSchema.parse(values);
      const payload = { ...v, vendorId: v.vendorId || null };
      return editId
        ? travelBookingsService.updateCarRental({ ...payload, id: editId })
        : travelBookingsService.createCarRental({ ...payload, staffTravelRequestId: requestId });
    },
    onSuccess: async () => {
      notify.saved();
      onOpenChange(false);
      await invalidate('CarRental');
    },
    onError: (e: Error) => notify.refused(e),
  });

  const pickup = form.watch('pickupDatetime');
  const dropoff = form.watch('dropoffDatetime');
  const rate = Number(form.watch('dailyRate')) || 0;
  // A part-day counts as a day, which is how hire is charged — and what the server computes.
  const days = pickup && dropoff
    ? Math.max(1, Math.ceil(
        (new Date(dropoff).getTime() - new Date(pickup).getTime()) / 86_400_000))
    : 0;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{editId ? 'Change the car rental' : 'Add a car rental'}</DialogTitle>
          <DialogDescription>
            The total is worked out from the rate and the hire period, which falls inside the trip&apos;s dates.
          </DialogDescription>
        </DialogHeader>
        {editId && !existing ? (
          <div className="flex justify-center p-6"><Loader2 className="h-5 w-5 animate-spin text-muted-foreground" /></div>
        ) : (
          <form id="car-form" className="space-y-4" onSubmit={form.handleSubmit((v) => save.mutate(v))}>
            <FieldRow>
              <SelectField form={form} name="vehicleCategory" label="Category" required options={options(VEHICLE_CATEGORIES)} />
              <TextField form={form} name="vehicleModel" label="Model" />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="pickupLocation" label="Pick up" />
              <TextField form={form} name="dropoffLocation" label="Drop off" />
            </FieldRow>
            <FieldRow>
              <DateTimeField form={form} name="pickupDatetime" label="Pick-up time" required />
              <DateTimeField form={form} name="dropoffDatetime" label="Drop-off time" required />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="dailyRate" label="Daily rate" required />
              <CurrencyField form={form} name="currencyCode" label="Currency" required />
            </FieldRow>

            <p className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
              {days > 0
                ? `${days} day${days === 1 ? '' : 's'} — ${fmtMoney(rate * days, form.watch('currencyCode'))} in total. A part-day counts as a day.`
                : 'Choose the pick-up and drop-off times to see the total.'}
            </p>

            <FieldRow>
              <TextField form={form} name="fuelPolicy" label="Fuel policy" />
              <TextField form={form} name="bookingReference" label="Booking reference" />
            </FieldRow>
            <VendorField form={form} />
            <SwitchField form={form} name="insuranceIncluded" label="Insurance included" />
            <SwitchField form={form} name="driverLicenseRequired" label="Driving licence required" />
          </form>
        )}
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button type="submit" form="car-form" disabled={save.isPending || (!!editId && !existing)}>
            {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {editId ? 'Save changes' : 'Add rental'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── Cancelling and ticketing (lane 5, D1) ────────────────────────────────────

interface CancelTarget {
  kind: TravelBookingKind;
  id: string;
  label: string;
  currencyCode: string;
}

/** A cancellation's reason (an internal note on the trip) and, on a flight or hotel, the supplier's fee. */
function CancelBookingDialog({
  requestId, target, onClose,
}: {
  requestId: string;
  target: CancelTarget | null;
  onClose: () => void;
}) {
  const notify = useBookingToast();
  const invalidate = useInvalidateBookings(requestId);
  const [reason, setReason] = useState('');
  const [fee, setFee] = useState('');
  const feeAllowed = target?.kind === 'Flight' || target?.kind === 'Hotel';

  useEffect(() => { setReason(''); setFee(''); }, [target?.id]);

  const cancel = useMutation({
    mutationFn: (t: CancelTarget) => travelBookingsService.cancelBooking(
      t.kind, t.id, reason.trim(), feeAllowed && fee.trim() ? Number(fee) : null),
    onSuccess: async (_data, t) => {
      notify.saved('Booking cancelled');
      onClose();
      await invalidate(t.kind);
    },
    onError: (e: Error) => notify.refused(e),
  });
  const feeValid = !fee.trim() || (!Number.isNaN(Number(fee)) && Number(fee) >= 0);

  return (
    <Dialog open={!!target} onOpenChange={(v) => { if (!v) onClose(); }}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Cancel the {target?.label}</DialogTitle>
          <DialogDescription>
            The booking stays on the trip as cancelled, with your reason kept as an internal note.
            {feeAllowed
              ? ' Record what the supplier charges for cancelling — the budget counts it as committed spend.'
              : ' Ground transport and car rentals keep no cancellation fee; mention any charge in the reason.'}
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-3">
          <div className="space-y-2">
            <Label htmlFor="booking-cancel-reason">Reason</Label>
            <Textarea
              id="booking-cancel-reason" rows={3} maxLength={1000} value={reason}
              onChange={(e) => setReason(e.target.value)} placeholder="At least five characters."
            />
          </div>
          {feeAllowed && (
            <div className="space-y-2">
              <Label htmlFor="booking-cancel-fee">Cancellation fee ({target?.currencyCode})</Label>
              <Input
                id="booking-cancel-fee" type="number" min={0} step="0.01" value={fee}
                onChange={(e) => setFee(e.target.value)} placeholder="0.00 — leave empty if none"
              />
            </div>
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Not now</Button>
          <Button
            variant="destructive"
            disabled={cancel.isPending || reason.trim().length < 5 || !feeValid || !target}
            onClick={() => { if (target) cancel.mutate(target); }}
          >
            {cancel.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Cancel the booking
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** Tickets a confirmed flight; the server refuses while the trip needs a visa that is not approved (T-24). */
function TicketDialog({
  requestId, flightId, onClose,
}: {
  requestId: string;
  flightId: string | null;
  onClose: () => void;
}) {
  const notify = useBookingToast();
  const invalidate = useInvalidateBookings(requestId);
  const [number, setNumber] = useState('');
  useEffect(() => { setNumber(''); }, [flightId]);

  const ticket = useMutation({
    mutationFn: () => travelBookingsService.ticketFlight(flightId as string, number.trim()),
    onSuccess: async () => {
      notify.saved('Flight ticketed');
      onClose();
      await invalidate('Flight');
    },
    onError: (e: Error) => notify.refused(e),
  });

  return (
    <Dialog open={!!flightId} onOpenChange={(v) => { if (!v) onClose(); }}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Ticket the flight</DialogTitle>
          <DialogDescription>
            A trip that needs a visa is ticketed only once a visa application on it is approved, or the visa is
            recorded as not required — the fare is spent once the ticket is issued.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-2">
          <Label htmlFor="flight-ticket-number">Ticket number</Label>
          <Input id="flight-ticket-number" maxLength={50} value={number} onChange={(e) => setNumber(e.target.value)} />
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Not now</Button>
          <Button disabled={ticket.isPending || !number.trim()} onClick={() => ticket.mutate()}>
            {ticket.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Ticket it
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── The row's actions ────────────────────────────────────────────────────────

type SimpleVerb = 'hold' | 'confirm' | 'no-show' | 'complete';

/**
 * What can be done to one booking now — drawn from its status and the trip's, the same table the server keeps
 * (`StaffTravelBookingRules.Next`): edit and hold/confirm/ticket on an approved trip; cancel on any trip not
 * closed; no-show and complete once the trip has started; delete only a Pending booking (travel administrators).
 */
function BookingActions({
  kind, status, fleetStatus, bookable, started, closed, canWrite, canAdmin,
  onEdit, onSegments, onMove, onTicket, onCancel, onDelete,
}: {
  kind: TravelBookingKind;
  status?: TravelBookingStatus;
  /** A company vehicle's leg: its fleet trip's status in Fleet (lane 6). */
  fleetStatus?: string | null;
  bookable: boolean;
  started: boolean;
  closed: boolean;
  canWrite: boolean;
  canAdmin: boolean;
  onEdit: () => void;
  onSegments?: () => void;
  onMove: (verb: SimpleVerb) => void;
  onTicket?: () => void;
  onCancel: () => void;
  onDelete: () => void;
}) {
  if (!canWrite) return onSegments ? (
    <Button variant="ghost" size="sm" onClick={onSegments}>Segments</Button>
  ) : null;
  const live = isLive(status);
  const items: React.ReactNode[] = [];

  // Lane 6 (R3): a company vehicle's leg follows its fleet trip — the transport office approves, dispatches and
  // completes it in Fleet. Here it is changed while Fleet allows, cancelled until the vehicle is out, and deleted only
  // as a draft (or a trip Fleet rejected).
  if (fleetStatus) {
    const ending: React.ReactNode[] = [];
    if (bookable && ['Draft', 'Rejected', 'Approved'].includes(fleetStatus))
      items.push(<DropdownMenuItem key="edit" onClick={onEdit}>Edit</DropdownMenuItem>);
    if (!closed && ['Draft', 'Submitted', 'Approved', 'Rejected'].includes(fleetStatus) && status !== 'Cancelled')
      ending.push(<DropdownMenuItem key="cancel" onClick={onCancel}>Cancel booking…</DropdownMenuItem>);
    if (canAdmin && !closed && ['Draft', 'Rejected'].includes(fleetStatus))
      ending.push(<DropdownMenuItem key="delete" className="text-destructive" onClick={onDelete}>Delete</DropdownMenuItem>);
    if (items.length === 0 && ending.length === 0) return null;
    return (
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" size="sm" aria-label="Booking actions"><MoreHorizontal className="h-4 w-4" /></Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          {items}
          {items.length > 0 && ending.length > 0 && <DropdownMenuSeparator />}
          {ending}
        </DropdownMenuContent>
      </DropdownMenu>
    );
  }

  if (live && bookable) items.push(<DropdownMenuItem key="edit" onClick={onEdit}>Edit</DropdownMenuItem>);
  if (onSegments) items.push(<DropdownMenuItem key="seg" onClick={onSegments}>Segments</DropdownMenuItem>);
  if (bookable && status === 'Pending')
    items.push(<DropdownMenuItem key="hold" onClick={() => onMove('hold')}>Put on hold</DropdownMenuItem>);
  if (bookable && (status === 'Pending' || status === 'OnHold'))
    items.push(<DropdownMenuItem key="confirm" onClick={() => onMove('confirm')}>Confirm</DropdownMenuItem>);
  if (bookable && kind === 'Flight' && status === 'Confirmed' && onTicket)
    items.push(<DropdownMenuItem key="ticket" onClick={onTicket}>Ticket…</DropdownMenuItem>);
  if (started && (status === 'Confirmed' || status === 'Ticketed')) {
    items.push(<DropdownMenuItem key="complete" onClick={() => onMove('complete')}>Mark completed</DropdownMenuItem>);
    items.push(<DropdownMenuItem key="noshow" onClick={() => onMove('no-show')}>Record a no-show</DropdownMenuItem>);
  }
  const ending: React.ReactNode[] = [];
  if (live && !closed) ending.push(<DropdownMenuItem key="cancel" onClick={onCancel}>Cancel booking…</DropdownMenuItem>);
  if (status === 'Pending' && canAdmin && !closed)
    ending.push(<DropdownMenuItem key="delete" className="text-destructive" onClick={onDelete}>Delete</DropdownMenuItem>);
  if (items.length === 0 && ending.length === 0) return null;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="sm" aria-label="Booking actions"><MoreHorizontal className="h-4 w-4" /></Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        {items}
        {items.length > 0 && ending.length > 0 && <DropdownMenuSeparator />}
        {ending}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

// ── The panel ────────────────────────────────────────────────────────────────

function Section({
  title, icon: Icon, onAdd, addLabel, canAdd, children,
}: {
  title: string;
  icon: typeof Plane;
  onAdd: () => void;
  addLabel: string;
  canAdd: boolean;
  children: React.ReactNode;
}) {
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-4 pb-3">
        <CardTitle className="flex items-center gap-2 text-base">
          <Icon className="h-4 w-4" />
          {title}
        </CardTitle>
        {canAdd && (
          <Button variant="outline" size="sm" onClick={onAdd}>
            <Plus className="mr-2 h-4 w-4" />
            {addLabel}
          </Button>
        )}
      </CardHeader>
      <CardContent className="p-0">{children}</CardContent>
    </Card>
  );
}

type DialogState =
  | { kind: 'flight' | 'hotel' | 'ground' | 'car'; editId: string | null }
  | null;

/**
 * What has actually been reserved for a trip: flights, hotels, ground transport and car rentals.
 *
 * ⚠ **Bookings live on an approved trip (lane 5, D-23).** A booking is added and changed while the trip is
 * Approved or under way; it is saved Pending, and its status moves only by the row's menu — hold, confirm,
 * ticket, cancel (with the supplier's fee on a flight or hotel), no-show, complete. Only a Pending booking is
 * deleted; anything further along is cancelled, so the record of what was booked stays.
 *
 * ⚠ **A booking that breaches the travel policy is refused by the server unless it asks for an
 * exception, and the refusal names the cap.** The screen cannot pre-empt that — the cap depends on the
 * traveller's staff level, their organisation unit and whether the trip crosses a border — so the
 * refusal is surfaced verbatim rather than guessed at. The flight and hotel switches ASK for an exception
 * (lane 4, D-8): the booking waits for a travel administrator other than the booker to authorise it on
 * Staff Travel → Policy breaches before it can be confirmed.
 *
 * Nothing here computes a stored total. Nights, hire days and segment durations are all worked out
 * server-side; where a figure is previewed it is labelled as a preview and uses the same arithmetic,
 * so a disagreement would be visible rather than silent.
 */
export function TravelBookingsPanel({ request }: { request: StaffTravelRequest }) {
  const requestId = request.id;
  const { canWrite, canAdmin } = useTravelAccess();
  const notify = useBookingToast();
  const invalidate = useInvalidateBookings(requestId);
  const [dialog, setDialog] = useState<DialogState>(null);
  const [segmentsOf, setSegmentsOf] = useState<{ id: string; writable: boolean } | null>(null);
  const [ticketing, setTicketing] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState<CancelTarget | null>(null);
  const [deleting, setDeleting] = useState<{ kind: TravelBookingKind; id: string; label: string } | null>(null);

  const bookable = request.status === 'Approved' || request.status === 'InProgress';
  const closed = request.status === 'Closed';
  const started = (request.status === 'Approved' || request.status === 'InProgress' || request.status === 'Completed')
    && new Date(request.travelStartDate.slice(0, 10)) <= new Date(new Date().toISOString().slice(0, 10));
  const canAdd = canWrite && bookable;

  // ⚠ The currency list is read through `api/hr/currencies` inside each dialog's CurrencyField.
  // This panel read `api/finance/currencies`, which answers 403 without a Finance permission, so
  // no booking could be saved by the HR desk in any currency (travel final closure, lane 0 — O-19).
  const { data: countries } = useQuery({
    queryKey: ['countries', 'active'],
    queryFn: () => countryService.getActive(),
  });

  const {
    data: flights, isLoading: loadingFlights, isError: flightsFailed, error: flightsError,
  } = useQuery({
    queryKey: ['travel-flights', requestId],
    queryFn: () => travelBookingsService.getFlightsByRequest(requestId),
  });
  const { data: hotels, isError: hotelsFailed, error: hotelsError } = useQuery({
    queryKey: ['travel-hotels', requestId],
    queryFn: () => travelBookingsService.getHotelsByRequest(requestId),
  });
  const { data: ground, isError: groundFailed, error: groundError } = useQuery({
    queryKey: ['travel-ground', requestId],
    queryFn: () => travelBookingsService.getGroundTransportsByRequest(requestId),
  });
  const { data: cars, isError: carsFailed, error: carsError } = useQuery({
    queryKey: ['travel-car-rentals', requestId],
    queryFn: () => travelBookingsService.getCarRentalsByRequest(requestId),
  });

  const move = useMutation({
    mutationFn: ({ kind, id, verb }: { kind: TravelBookingKind; id: string; verb: SimpleVerb }) =>
      travelBookingsService.moveBooking(kind, id, verb),
    onSuccess: async (_data, { kind, verb }) => {
      notify.saved(verb === 'hold' ? 'Booking put on hold' : verb === 'confirm' ? 'Booking confirmed'
        : verb === 'complete' ? 'Booking marked completed' : 'No-show recorded');
      await invalidate(kind);
    },
    onError: (e: Error) => notify.refused(e),
  });
  const remove = useMutation({
    mutationFn: ({ kind, id }: { kind: TravelBookingKind; id: string }) =>
      kind === 'Flight' ? travelBookingsService.deleteFlight(id)
        : kind === 'Hotel' ? travelBookingsService.deleteHotel(id)
          : kind === 'Ground' ? travelBookingsService.deleteGroundTransport(id)
            : travelBookingsService.deleteCarRental(id),
    onSuccess: async (_data, { kind }) => {
      notify.saved('Booking deleted');
      setDeleting(null);
      await invalidate(kind);
    },
    onError: (e: Error) => notify.refused(e),
  });

  const countryOptions = (countries ?? []).map((c) => ({ value: c.id, label: c.name }));

  const actions = (kind: TravelBookingKind, id: string, status: TravelBookingStatus | undefined, label: string,
    currencyCode: string, dialogKind: 'flight' | 'hotel' | 'ground' | 'car', fleetStatus?: string | null) => (
    <BookingActions
      kind={kind} status={status} fleetStatus={fleetStatus} bookable={bookable} started={started} closed={closed}
      canWrite={canWrite} canAdmin={canAdmin}
      onEdit={() => setDialog({ kind: dialogKind, editId: id })}
      onSegments={kind === 'Flight' ? () => setSegmentsOf({ id, writable: canWrite && bookable && isLive(status) }) : undefined}
      onMove={(verb) => move.mutate({ kind, id, verb })}
      onTicket={kind === 'Flight' ? () => setTicketing(id) : undefined}
      onCancel={() => setCancelling({ kind, id, label, currencyCode })}
      onDelete={() => setDeleting({ kind, id, label })}
    />
  );

  if (loadingFlights) {
    return (
      <div className="flex items-center justify-center p-10">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {!bookable && canWrite && (
        <p className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
          Bookings are made once the trip is approved, and while it is under way — this one is{' '}
          {humanize(request.status).toLowerCase()}. A live booking can still be cancelled
          {closed ? ' until the trip is closed' : ''}.
        </p>
      )}

      <Section
        title="Flights" icon={Plane} addLabel="Add flight" canAdd={canAdd}
        onAdd={() => setDialog({ kind: 'flight', editId: null })}
      >
        {flightsFailed && !flights ? (
          <div className="p-4"><TravelQueryError error={flightsError} what="the flight bookings" /></div>
        ) : (flights ?? []).length === 0 ? (
          <EmptyState title="No flights" description="Nothing has been booked for this trip." />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Airline</TableHead>
                <TableHead>Reference</TableHead>
                <TableHead>Class</TableHead>
                <TableHead className="text-right">Fare</TableHead>
                <TableHead className="w-24">Segments</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="w-12" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {(flights ?? []).map((f) => (
                <TableRow key={f.id}>
                  <TableCell className="font-medium">{f.airlineName || '—'}</TableCell>
                  <TableCell>{f.bookingReference || f.ticketNumber || '—'}</TableCell>
                  <TableCell>{humanize(f.bookingClassName)}</TableCell>
                  <TableCell className="text-right whitespace-nowrap">
                    {fmtMoney(f.totalFare, f.currencyCode)}
                  </TableCell>
                  <TableCell>{f.segmentCount}</TableCell>
                  <TableCell className="whitespace-nowrap">
                    <StatusBadge status={humanize(f.statusName)} />
                    <ExceptionBadge state={f.exceptionState} />
                  </TableCell>
                  <TableCell>
                    {actions('Flight', f.id, f.status, `flight ${f.bookingReference || f.airlineName || ''}`.trim(),
                      f.currencyCode, 'flight')}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Section>

      <Section
        title="Hotels" icon={BedDouble} addLabel="Add hotel" canAdd={canAdd}
        onAdd={() => setDialog({ kind: 'hotel', editId: null })}
      >
        {hotelsFailed && !hotels ? (
          <div className="p-4"><TravelQueryError error={hotelsError} what="the hotel bookings" /></div>
        ) : (hotels ?? []).length === 0 ? (
          <EmptyState title="No hotels" description="No accommodation has been booked." />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Hotel</TableHead>
                <TableHead>City</TableHead>
                <TableHead>Dates</TableHead>
                <TableHead className="w-20">Nights</TableHead>
                <TableHead className="text-right">Total</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="w-12" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {(hotels ?? []).map((h) => (
                <TableRow key={h.id}>
                  <TableCell className="font-medium">{h.hotelName}</TableCell>
                  <TableCell>{h.city}</TableCell>
                  <TableCell className="whitespace-nowrap">
                    {fmtDate(h.checkInDate)} – {fmtDate(h.checkOutDate)}
                  </TableCell>
                  <TableCell>{h.numberOfNights}</TableCell>
                  <TableCell className="text-right whitespace-nowrap">
                    {fmtMoney(h.totalCost, h.currencyCode)}
                  </TableCell>
                  <TableCell className="whitespace-nowrap">
                    <StatusBadge status={humanize(h.statusName)} />
                    <ExceptionBadge state={h.exceptionState} />
                  </TableCell>
                  <TableCell>{actions('Hotel', h.id, h.status, `hotel booking at ${h.hotelName}`, h.currencyCode, 'hotel')}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Section>

      <Section
        title="Ground transport" icon={Bus} addLabel="Add transport" canAdd={canAdd}
        onAdd={() => setDialog({ kind: 'ground', editId: null })}
      >
        {groundFailed && !ground ? (
          <div className="p-4"><TravelQueryError error={groundError} what="the ground transport" /></div>
        ) : (ground ?? []).length === 0 ? (
          <EmptyState title="No ground transport" description="Nothing arranged on the ground." />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Type</TableHead>
                <TableHead>Route</TableHead>
                <TableHead className="text-right">Cost</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="w-12" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {(ground ?? []).map((g) => (
                <TableRow key={g.id}>
                  <TableCell className="font-medium">
                    <span className="flex items-center gap-1.5">
                      {humanize(g.transportTypeName)}
                    </span>
                    {/* Lane 6 (FX-3): the vehicle, driver and trip as Fleet holds them. */}
                    {g.fleetTripId && (
                      <span className="block text-xs font-normal text-muted-foreground">
                        {g.vehicleName ?? 'Fleet vehicle'}{g.vehiclePlate ? ` · ${g.vehiclePlate}` : ''}
                        {g.driverName ? ` · driver ${g.driverName}` : ''}
                        {g.dispatchedAt ? ` · out ${fmtDate(g.dispatchedAt)}` : ''}
                        {g.returnedAt ? ` · back ${fmtDate(g.returnedAt)}` : ''}
                        {g.distance != null ? ` · ${g.distance.toLocaleString()} driven` : ''}
                      </span>
                    )}
                    {g.fleetNote && <span className="block text-xs font-normal text-muted-foreground">{g.fleetNote}</span>}
                  </TableCell>
                  <TableCell>
                    {g.pickupLocation || '—'} → {g.dropoffLocation || '—'}
                  </TableCell>
                  <TableCell className="text-right whitespace-nowrap">
                    {g.fleetTripId
                      ? <span className="text-xs text-muted-foreground">Fleet&apos;s costs</span>
                      : <>
                          {fmtMoney(g.actualCost ?? g.estimatedCost, g.currencyCode)}
                          {g.actualCost != null && <span className="ml-1 text-xs text-muted-foreground">actual</span>}
                        </>}
                  </TableCell>
                  <TableCell className="whitespace-nowrap">
                    <StatusBadge status={humanize(g.statusName)} />
                    {g.fleetStatus && <span className="ml-2 text-xs text-muted-foreground">Fleet: {g.fleetStatus}</span>}
                  </TableCell>
                  <TableCell>
                    {actions('Ground', g.id, g.status, `${humanize(g.transportTypeName).toLowerCase()} booking`, g.currencyCode, 'ground', g.fleetStatus)}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Section>

      <Section
        title="Car rentals" icon={Car} addLabel="Add rental" canAdd={canAdd}
        onAdd={() => setDialog({ kind: 'car', editId: null })}
      >
        {carsFailed && !cars ? (
          <div className="p-4"><TravelQueryError error={carsError} what="the car rentals" /></div>
        ) : (cars ?? []).length === 0 ? (
          <EmptyState title="No car rentals" description="No vehicle has been hired." />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Category</TableHead>
                <TableHead>Route</TableHead>
                <TableHead>Dates</TableHead>
                <TableHead className="text-right">Total</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="w-12" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {(cars ?? []).map((c) => (
                <TableRow key={c.id}>
                  <TableCell className="font-medium">{humanize(c.vehicleCategoryName)}</TableCell>
                  <TableCell>{c.pickupLocation || '—'} → {c.dropoffLocation || '—'}</TableCell>
                  <TableCell className="whitespace-nowrap">
                    {fmtDate(c.pickupDatetime)} – {fmtDate(c.dropoffDatetime)}
                  </TableCell>
                  <TableCell className="text-right whitespace-nowrap">
                    {fmtMoney(c.totalCost, c.currencyCode)}
                  </TableCell>
                  <TableCell><StatusBadge status={humanize(c.statusName)} /></TableCell>
                  <TableCell>{actions('CarRental', c.id, c.status, 'car rental', c.currencyCode, 'car')}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Section>

      {[...(flights ?? []), ...(hotels ?? [])].some((b) => b.exceptionState && b.exceptionState !== 'None') && (
        <p className="flex items-start gap-2 text-xs text-muted-foreground">
          <ShieldAlert className="mt-0.5 h-3.5 w-3.5 shrink-0" />
          <span>
            A booking that breaches the travel policy is kept with the cap that applied and the reason
            given. It cannot be confirmed or ticketed until a travel administrator other than the booker
            authorises the exception on{' '}
            <Link href="/hr/travel/breaches" className="underline">Policy breaches</Link>.
          </span>
        </p>
      )}

      <FlightDialog
        requestId={requestId}
        editId={dialog?.kind === 'flight' ? dialog.editId : null}
        open={dialog?.kind === 'flight'}
        onOpenChange={(v) => { if (!v) setDialog(null); }}
        defaultCurrency={request.currencyCode}
      />
      <HotelDialog
        requestId={requestId}
        editId={dialog?.kind === 'hotel' ? dialog.editId : null}
        open={dialog?.kind === 'hotel'}
        onOpenChange={(v) => { if (!v) setDialog(null); }}
        countryOptions={countryOptions}
        defaultCurrency={request.currencyCode}
        defaultCountryId={request.destinationCountryId}
      />
      <GroundDialog
        requestId={requestId}
        editId={dialog?.kind === 'ground' ? dialog.editId : null}
        open={dialog?.kind === 'ground'}
        onOpenChange={(v) => { if (!v) setDialog(null); }}
        defaultCurrency={request.currencyCode}
      />
      <CarRentalDialog
        requestId={requestId}
        editId={dialog?.kind === 'car' ? dialog.editId : null}
        open={dialog?.kind === 'car'}
        onOpenChange={(v) => { if (!v) setDialog(null); }}
        defaultCurrency={request.currencyCode}
      />
      <FlightSegmentsDialog
        requestId={requestId}
        flightId={segmentsOf?.id ?? null}
        open={!!segmentsOf}
        onOpenChange={(v) => { if (!v) setSegmentsOf(null); }}
        writable={!!segmentsOf?.writable}
        canAdmin={canAdmin}
      />
      <TicketDialog requestId={requestId} flightId={ticketing} onClose={() => setTicketing(null)} />
      <CancelBookingDialog requestId={requestId} target={cancelling} onClose={() => setCancelling(null)} />
      <ConfirmationDialog
        open={!!deleting}
        onOpenChange={(v) => { if (!v) setDeleting(null); }}
        title="Delete the booking?"
        description={`The ${deleting?.label ?? 'booking'} is pending and is removed for good. A booking that was held, confirmed or ticketed is cancelled instead, so the record stays.`}
        confirmText="Delete"
        variant="destructive"
        isLoading={remove.isPending}
        onConfirm={() => { if (deleting) remove.mutate({ kind: deleting.kind, id: deleting.id }); return false; }}
      />
    </div>
  );
}
