'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plane, BedDouble, Car, Bus, Plus, ShieldAlert, Loader2 } from 'lucide-react';
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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
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
import { useToast } from '@/hooks/use-toast';
import { countryService } from '@/services/hr/country.service';
import { travelBookingsService } from '@/services/hr/travel-bookings.service';
import { TravelQueryError } from './TravelQueryError';
import { fmtTravelMoney as fmtMoney } from './travel-format';
import type { StaffTravelRequest } from '@/types/hr/travel';

const CABIN_CLASSES = ['Economy', 'PremiumEconomy', 'Business', 'First'] as const;
const CHANNELS = [
  'SelfService', 'TravelDesk', 'TravelAgency', 'DirectAirline', 'DirectHotel', 'OnlinePortal',
] as const;
const BOOKING_STATUSES = [
  'Pending', 'Confirmed', 'Ticketed', 'Cancelled', 'Refunded', 'NoShow', 'Completed', 'OnHold',
] as const;
const GROUND_TYPES = [
  'Taxi', 'Rideshare', 'Bus', 'Train', 'Metro', 'CompanyVehicle', 'PrivateCarHire', 'Shuttle',
  'Motorcycle', 'Ferry',
] as const;
const VEHICLE_CATEGORIES = [
  'Economy', 'Compact', 'Intermediate', 'FullSize', 'Suv', 'Luxury', 'Minivan', 'Truck',
] as const;

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

/** A booking refused for breaching the policy explains itself; the cap is not ours to predict. */
function useBookingToast() {
  const { toast } = useToast();
  return {
    saved: () => toast({ title: 'Booking saved' }),
    refused: (e: Error) =>
      toast({
        variant: 'destructive',
        title: 'The booking was refused',
        description: e.message ?? 'Please check the details and try again.',
      }),
  };
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
  status: z.enum(BOOKING_STATUSES),
});

function FlightDialog({
  requestId, open, onOpenChange, defaultCurrency,
}: {
  requestId: string;
  open: boolean;
  onOpenChange: (v: boolean) => void;
  defaultCurrency: string;
}) {
  const queryClient = useQueryClient();
  const notify = useBookingToast();

  const form = useForm<z.input<typeof flightSchema>>({
    resolver: zodResolver(flightSchema),
    defaultValues: {
      bookingClass: 'Economy', bookedBy: 'TravelDesk', status: 'Pending',
      classExceptionApproved: false, totalFare: 0, taxesAndFees: 0,
      currencyCode: defaultCurrency,
    },
  });

  const save = useMutation({
    mutationFn: (values: z.input<typeof flightSchema>) => {
      const v = flightSchema.parse(values);
      return travelBookingsService.createFlight({ ...v, staffTravelRequestId: requestId });
    },
    onSuccess: async () => {
      notify.saved();
      onOpenChange(false);
      form.reset();
      await queryClient.invalidateQueries({ queryKey: ['travel-flights', requestId] });
    },
    onError: (e: Error) => notify.refused(e),
  });

  const wantsException = !!form.watch('classExceptionApproved');

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Add a flight booking</DialogTitle>
          <DialogDescription>
            The cabin class is checked against the travel policy for this trip.
          </DialogDescription>
        </DialogHeader>
        <form
          id="flight-form"
          className="space-y-4"
          onSubmit={form.handleSubmit((v) => save.mutate(v))}
        >
          <FieldRow>
            <TextField form={form} name="airlineName" label="Airline" />
            <TextField form={form} name="airlineCode" label="Airline code" placeholder="KQ" />
          </FieldRow>
          <FieldRow>
            <TextField form={form} name="bookingReference" label="Booking reference" />
            <TextField form={form} name="ticketNumber" label="Ticket number" />
          </FieldRow>
          <FieldRow>
            <SelectField
              form={form} name="bookingClass" label="Cabin class" required
              options={options(CABIN_CLASSES)}
            />
            <SelectField
              form={form} name="bookedBy" label="Booked through" required options={options(CHANNELS)}
            />
          </FieldRow>
          <FieldRow>
            <NumberField form={form} name="totalFare" label="Fare" required />
            <NumberField form={form} name="taxesAndFees" label="Taxes and fees" />
          </FieldRow>
          <FieldRow>
            <CurrencyField form={form} name="currencyCode" label="Currency" required />
            <SelectField
              form={form} name="status" label="Status" required options={options(BOOKING_STATUSES)}
            />
          </FieldRow>

          {/*
            Shown to everyone, because the screen cannot know the caller's permissions and hiding it
            would turn "you may not do this" into "this does not exist". A caller without
            HR.Travel.Admin gets a 403 that says so.
          */}
          <SwitchField
            form={form}
            name="classExceptionApproved"
            label="Authorise a booking above the policy cap"
            description="Travel administrators only. Without this, a class above the cap is refused."
          />
          {wantsException && (
            <TextareaField
              form={form}
              name="classExceptionReason"
              label="Why the exception is granted"
              placeholder="Required — kept on the booking."
            />
          )}
        </form>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button type="submit" form="flight-form" disabled={save.isPending}>
            {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Add booking
          </Button>
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
    checkInDate: z.string().min(1, 'Required'),
    checkOutDate: z.string().min(1, 'Required'),
    roomType: z.string().max(100).optional(),
    ratePerNight: z.coerce.number().min(0),
    currencyCode: z.string().min(1, 'Select a currency'),
    rateExceptionApproved: z.boolean(),
    rateExceptionReason: z.string().max(1000).optional(),
    bookedBy: z.enum(CHANNELS),
    status: z.enum(BOOKING_STATUSES),
    bookingReference: z.string().max(100).optional(),
    cancellationPolicy: z.string().max(1000).optional(),
  })
  .refine((v) => !v.checkOutDate || !v.checkInDate || v.checkOutDate >= v.checkInDate, {
    message: 'Check-out cannot be before check-in',
    path: ['checkOutDate'],
  });

function HotelDialog({
  requestId, open, onOpenChange, countryOptions, defaultCurrency, defaultCountryId,
}: {
  requestId: string;
  open: boolean;
  onOpenChange: (v: boolean) => void;
  countryOptions: { value: string; label: string }[];
  defaultCurrency: string;
  defaultCountryId: string;
}) {
  const queryClient = useQueryClient();
  const notify = useBookingToast();

  const form = useForm<z.input<typeof hotelSchema>>({
    resolver: zodResolver(hotelSchema),
    defaultValues: {
      hotelName: '', city: '', countryId: defaultCountryId, checkInDate: '', checkOutDate: '',
      ratePerNight: 0, currencyCode: defaultCurrency, rateExceptionApproved: false,
      bookedBy: 'TravelDesk', status: 'Pending',
    },
  });

  const save = useMutation({
    mutationFn: (values: z.input<typeof hotelSchema>) => {
      const v = hotelSchema.parse(values);
      return travelBookingsService.createHotel({ ...v, staffTravelRequestId: requestId });
    },
    onSuccess: async () => {
      notify.saved();
      onOpenChange(false);
      form.reset();
      await queryClient.invalidateQueries({ queryKey: ['travel-hotels', requestId] });
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
          <DialogTitle>Add a hotel booking</DialogTitle>
          <DialogDescription>
            The nightly rate is checked against the travel policy for this trip.
          </DialogDescription>
        </DialogHeader>
        <form
          id="hotel-form"
          className="space-y-4"
          onSubmit={form.handleSubmit((v) => save.mutate(v))}
        >
          <FieldRow>
            <TextField form={form} name="hotelName" label="Hotel" required />
            <TextField form={form} name="hotelChain" label="Chain" />
          </FieldRow>
          <TextField form={form} name="hotelAddress" label="Address" />
          <FieldRow>
            <TextField form={form} name="city" label="City" required />
            <SelectField
              form={form} name="countryId" label="Country" required options={countryOptions}
            />
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
            <TextField form={form} name="bookingReference" label="Booking reference" />
          </FieldRow>
          <FieldRow>
            <SelectField
              form={form} name="bookedBy" label="Booked through" required options={options(CHANNELS)}
            />
            <SelectField
              form={form} name="status" label="Status" required options={options(BOOKING_STATUSES)}
            />
          </FieldRow>
          <TextareaField form={form} name="cancellationPolicy" label="Cancellation policy" />

          <SwitchField
            form={form}
            name="rateExceptionApproved"
            label="Authorise a rate above the policy cap"
            description="Travel administrators only. Without this, a rate above the cap is refused."
          />
          {wantsException && (
            <TextareaField
              form={form}
              name="rateExceptionReason"
              label="Why the exception is granted"
              placeholder="Required — kept on the booking."
            />
          )}
        </form>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button type="submit" form="hotel-form" disabled={save.isPending}>
            {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Add booking
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
    bookingReference: z.string().max(100).optional(),
    pickupLocation: z.string().max(300).optional(),
    dropoffLocation: z.string().max(300).optional(),
    pickupDatetime: z.string().optional(),
    dropoffDatetime: z.string().optional(),
    estimatedCost: z.coerce.number().min(0).optional(),
    currencyCode: z.string().min(1, 'Select a currency'),
    status: z.enum(BOOKING_STATUSES),
    notes: z.string().max(2000).optional(),
  })
  .refine((v) => v.transportType !== 'CompanyVehicle' || !!v.vehicleAssetId, {
    message: 'A company vehicle must be named — a draft trip is created for it in Fleet',
    path: ['vehicleAssetId'],
  });

function GroundDialog({
  requestId, open, onOpenChange, defaultCurrency,
}: {
  requestId: string;
  open: boolean;
  onOpenChange: (v: boolean) => void;
  defaultCurrency: string;
}) {
  const queryClient = useQueryClient();
  const notify = useBookingToast();

  const form = useForm<z.input<typeof groundSchema>>({
    resolver: zodResolver(groundSchema),
    defaultValues: {
      transportType: 'Taxi', currencyCode: defaultCurrency, status: 'Pending', estimatedCost: 0,
    },
  });

  const save = useMutation({
    mutationFn: (values: z.input<typeof groundSchema>) => {
      const v = groundSchema.parse(values);
      return travelBookingsService.createGroundTransport({
        ...v,
        staffTravelRequestId: requestId,
        vehicleAssetId: v.vehicleAssetId || null,
        pickupDatetime: orNull(v.pickupDatetime),
        dropoffDatetime: orNull(v.dropoffDatetime),
      });
    },
    onSuccess: async () => {
      notify.saved();
      onOpenChange(false);
      form.reset();
      await queryClient.invalidateQueries({ queryKey: ['travel-ground', requestId] });
    },
    onError: (e: Error) => notify.refused(e),
  });

  const isCompanyVehicle = form.watch('transportType') === 'CompanyVehicle';

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Add ground transport</DialogTitle>
          <DialogDescription>
            A company vehicle is recorded in Fleet as a draft trip rather than as a note here.
          </DialogDescription>
        </DialogHeader>
        <form
          id="ground-form"
          className="space-y-4"
          onSubmit={form.handleSubmit((v) => save.mutate(v))}
        >
          <FieldRow>
            <SelectField
              form={form} name="transportType" label="Transport" required
              options={options(GROUND_TYPES)}
            />
            <TextField form={form} name="bookingReference" label="Booking reference" />
          </FieldRow>

          {isCompanyVehicle && (
            <div className="space-y-2">
              <TextField
                form={form}
                name="vehicleAssetId"
                label="Vehicle (Fleet asset id)"
                required
                placeholder="Fleet asset identifier"
              />
              <p className="text-xs text-muted-foreground">
                This creates a draft trip in Fleet for the vehicle. It is not sent to the transport
                office for approval, so the vehicle is not held for these dates. Fleet refuses an
                asset that is not a vehicle, and the reason is shown here.
              </p>
            </div>
          )}

          <FieldRow>
            <TextField form={form} name="pickupLocation" label="Pick up" />
            <TextField form={form} name="dropoffLocation" label="Drop off" />
          </FieldRow>
          <FieldRow>
            <DateTimeField form={form} name="pickupDatetime" label="Pick-up time" />
            <DateTimeField form={form} name="dropoffDatetime" label="Drop-off time" />
          </FieldRow>
          <FieldRow>
            <NumberField form={form} name="estimatedCost" label="Estimated cost" />
            <CurrencyField form={form} name="currencyCode" label="Currency" required />
          </FieldRow>
          <SelectField
            form={form} name="status" label="Status" required options={options(BOOKING_STATUSES)}
          />
          <TextareaField form={form} name="notes" label="Notes" />
        </form>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button type="submit" form="ground-form" disabled={save.isPending}>
            {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Add transport
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
    status: z.enum(BOOKING_STATUSES),
  })
  .refine((v) => !v.dropoffDatetime || !v.pickupDatetime || v.dropoffDatetime >= v.pickupDatetime, {
    message: 'Drop-off cannot be before pick-up',
    path: ['dropoffDatetime'],
  });

function CarRentalDialog({
  requestId, open, onOpenChange, defaultCurrency,
}: {
  requestId: string;
  open: boolean;
  onOpenChange: (v: boolean) => void;
  defaultCurrency: string;
}) {
  const queryClient = useQueryClient();
  const notify = useBookingToast();

  const form = useForm<z.input<typeof carSchema>>({
    resolver: zodResolver(carSchema),
    defaultValues: {
      pickupDatetime: '', dropoffDatetime: '', vehicleCategory: 'Economy', dailyRate: 0,
      currencyCode: defaultCurrency, insuranceIncluded: true, driverLicenseRequired: true,
      status: 'Pending',
    },
  });

  const save = useMutation({
    mutationFn: (values: z.input<typeof carSchema>) => {
      const v = carSchema.parse(values);
      return travelBookingsService.createCarRental({ ...v, staffTravelRequestId: requestId });
    },
    onSuccess: async () => {
      notify.saved();
      onOpenChange(false);
      form.reset();
      await queryClient.invalidateQueries({ queryKey: ['travel-car-rentals', requestId] });
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
          <DialogTitle>Add a car rental</DialogTitle>
          <DialogDescription>The total is worked out from the rate and the hire period.</DialogDescription>
        </DialogHeader>
        <form id="car-form" className="space-y-4" onSubmit={form.handleSubmit((v) => save.mutate(v))}>
          <FieldRow>
            <SelectField
              form={form} name="vehicleCategory" label="Category" required
              options={options(VEHICLE_CATEGORIES)}
            />
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
          <SelectField
            form={form} name="status" label="Status" required options={options(BOOKING_STATUSES)}
          />
          <SwitchField form={form} name="insuranceIncluded" label="Insurance included" />
          <SwitchField form={form} name="driverLicenseRequired" label="Driving licence required" />
        </form>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button type="submit" form="car-form" disabled={save.isPending}>
            {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Add rental
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── The panel ────────────────────────────────────────────────────────────────

function Section({
  title, icon: Icon, onAdd, addLabel, children,
}: {
  title: string;
  icon: typeof Plane;
  onAdd: () => void;
  addLabel: string;
  children: React.ReactNode;
}) {
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-4 pb-3">
        <CardTitle className="flex items-center gap-2 text-base">
          <Icon className="h-4 w-4" />
          {title}
        </CardTitle>
        <Button variant="outline" size="sm" onClick={onAdd}>
          <Plus className="mr-2 h-4 w-4" />
          {addLabel}
        </Button>
      </CardHeader>
      <CardContent className="p-0">{children}</CardContent>
    </Card>
  );
}

/**
 * What has actually been reserved for a trip: flights, hotels, ground transport and car rentals.
 *
 * ⚠ **A booking above the travel policy's cap is refused by the server, and the refusal names the
 * cap.** The screen cannot pre-empt that — the cap depends on the traveller's staff level, their
 * organisation unit and whether the trip crosses a border — so the refusal is surfaced verbatim
 * rather than guessed at. The two "authorise above the cap" switches are shown to everyone and
 * answer 403 for a caller without `HR.Travel.Admin`: hiding them would turn "you may not do this"
 * into "this does not exist", which is a worse thing to tell someone.
 *
 * Nothing here computes a stored total. Nights, hire days and segment durations are all worked out
 * server-side; where a figure is previewed it is labelled as a preview and uses the same arithmetic,
 * so a disagreement would be visible rather than silent.
 */
export function TravelBookingsPanel({ request }: { request: StaffTravelRequest }) {
  const requestId = request.id;
  const [dialog, setDialog] = useState<'flight' | 'hotel' | 'ground' | 'car' | null>(null);

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

  const countryOptions = (countries ?? []).map((c) => ({ value: c.id, label: c.name }));

  if (loadingFlights) {
    return (
      <div className="flex items-center justify-center p-10">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <Section
        title="Flights" icon={Plane} addLabel="Add flight" onAdd={() => setDialog('flight')}
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
                  <TableCell><StatusBadge status={humanize(f.statusName)} /></TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Section>

      <Section title="Hotels" icon={BedDouble} addLabel="Add hotel" onAdd={() => setDialog('hotel')}>
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
                  <TableCell><StatusBadge status={humanize(h.statusName)} /></TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Section>

      <Section
        title="Ground transport" icon={Bus} addLabel="Add transport"
        onAdd={() => setDialog('ground')}
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
              </TableRow>
            </TableHeader>
            <TableBody>
              {(ground ?? []).map((g) => (
                <TableRow key={g.id}>
                  <TableCell className="font-medium">
                    <span className="flex items-center gap-1.5">
                      {humanize(g.transportTypeName)}
                      {g.fleetTripId && (
                        <span className="text-xs text-muted-foreground">(Fleet trip)</span>
                      )}
                    </span>
                  </TableCell>
                  <TableCell>
                    {g.pickupLocation || '—'} → {g.dropoffLocation || '—'}
                  </TableCell>
                  <TableCell className="text-right whitespace-nowrap">
                    {fmtMoney(g.actualCost ?? g.estimatedCost, g.currencyCode)}
                  </TableCell>
                  <TableCell><StatusBadge status={humanize(g.statusName)} /></TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Section>

      <Section title="Car rentals" icon={Car} addLabel="Add rental" onAdd={() => setDialog('car')}>
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
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Section>

      {(flights ?? []).some((f) => f.bookingClass !== 'Economy') && (
        <p className="flex items-start gap-2 text-xs text-muted-foreground">
          <ShieldAlert className="mt-0.5 h-3.5 w-3.5 shrink-0" />
          A booking above the travel policy&apos;s cap is stored with the cap that applied and the
          reason the exception was granted.
        </p>
      )}

      <FlightDialog
        requestId={requestId}
        open={dialog === 'flight'}
        onOpenChange={(v) => setDialog(v ? 'flight' : null)}
        defaultCurrency={request.currencyCode}
      />
      <HotelDialog
        requestId={requestId}
        open={dialog === 'hotel'}
        onOpenChange={(v) => setDialog(v ? 'hotel' : null)}
        countryOptions={countryOptions}
        defaultCurrency={request.currencyCode}
        defaultCountryId={request.destinationCountryId}
      />
      <GroundDialog
        requestId={requestId}
        open={dialog === 'ground'}
        onOpenChange={(v) => setDialog(v ? 'ground' : null)}
        defaultCurrency={request.currencyCode}
      />
      <CarRentalDialog
        requestId={requestId}
        open={dialog === 'car'}
        onOpenChange={(v) => setDialog(v ? 'car' : null)}
        defaultCurrency={request.currencyCode}
      />
    </div>
  );
}
