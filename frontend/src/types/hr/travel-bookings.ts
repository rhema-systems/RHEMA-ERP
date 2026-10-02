/**
 * Staff travel — the itinerary (versioned, with legs and activities) and the four kinds of booking
 * that hang off a travel request: flights with their segments, hotels, ground transport and car
 * rentals.
 *
 * Backend routes: `api/staff-travel/itineraries` and `api/staff-travel/bookings`.
 *
 * ⚠ **Several fields on these records are server-assigned, and since the travel closure's lane 5 they
 * are on the read types only — the write types no longer carry them:**
 *
 * | field | who decides |
 * |---|---|
 * | `status` | the booking's verbs (hold, confirm, ticket, cancel, no-show, complete) — a create is Pending |
 * | `policyAllowedClass`, `policyMaxRatePerNight` | the travel policy in force for the traveller |
 * | `numberOfNights`, hotel `totalCost`, car-rental `totalCost` | the dates and the rate |
 * | segment `durationMinutes` | the two datetimes |
 * | `bookedAt`, `cancelledAt`, `cancellationFee` | confirm and cancel |
 *
 * The two `*ExceptionApproved` flags ARE inputs, but they ASK for an exception (with the reason) rather
 * than grant it (lane 4, D-8): the booking waits for another travel administrator's authorisation.
 */

import type { AuditFields } from './common';

// ── Itineraries ──────────────────────────────────────────────────────────────

export type TravelItineraryStatus =
  | 'Draft'
  | 'PendingReview'
  | 'Approved'
  | 'Active'
  | 'Completed'
  | 'Cancelled'
  | 'Superseded';

export type TravelItineraryLegType =
  | 'Departure'
  | 'Transit'
  | 'Arrival'
  | 'Stay'
  | 'DayTrip'
  | 'Return';

export type StaffTravelTransportMode =
  | 'Flight'
  | 'Train'
  | 'Bus'
  | 'Car'
  | 'Ferry'
  | 'Helicopter'
  | 'Motorcycle'
  | 'Walk';

export type StaffTravelActivityType =
  | 'Meeting'
  | 'Conference'
  | 'Training'
  | 'SiteVisit'
  | 'ClientDinner'
  | 'FreeTime'
  | 'TransitLayover'
  | 'Other';

export interface StaffTravelItineraryActivity extends AuditFields {
  staffTravelItineraryLegId: string;
  activityType: StaffTravelActivityType;
  activityTypeName: string;
  title: string;
  description?: string | null;
  locationName?: string | null;
  locationAddress?: string | null;
  startDatetime?: string | null;
  endDatetime?: string | null;
  contactName?: string | null;
  contactEmail?: string | null;
  contactPhone?: string | null;
  isMandatory: boolean;
}

export interface StaffTravelItineraryLeg extends AuditFields {
  staffTravelItineraryId: string;
  sequenceOrder: number;
  legType: TravelItineraryLegType;
  legTypeName: string;
  legDate: string;
  originCity?: string | null;
  originCountryId?: string | null;
  originCountryName?: string | null;
  destinationCity?: string | null;
  destinationCountryId?: string | null;
  destinationCountryName?: string | null;
  transportMode?: StaffTravelTransportMode | null;
  transportModeName?: string | null;
  departureDatetime?: string | null;
  arrivalDatetime?: string | null;
  /** Links the leg to a booking already recorded against the request. */
  flightBookingId?: string | null;
  hotelBookingId?: string | null;
  groundTransportId?: string | null;
  notes?: string | null;
  activities: StaffTravelItineraryActivity[];
}

export interface StaffTravelItinerarySummary {
  id: string;
  versionNumber: number;
  isCurrentVersion: boolean;
  status: TravelItineraryStatus;
  statusName: string;
  title: string;
  totalTravelDays: number;
  legCount: number;
  finalizedAt?: string | null;
}

export interface StaffTravelItinerary extends AuditFields {
  staffTravelRequestId: string;
  requestNumber?: string | null;
  versionNumber: number;
  isCurrentVersion: boolean;
  status: TravelItineraryStatus;
  statusName: string;
  title: string;
  totalTravelDays: number;
  totalWorkingDays: number;
  totalWeekendDays: number;
  summaryNotes?: string | null;
  finalizedAt?: string | null;
  legs: StaffTravelItineraryLeg[];
}

export interface CreateStaffTravelItinerary {
  staffTravelRequestId: string;
  versionNumber?: number;
  isCurrentVersion?: boolean;
  title: string;
  totalTravelDays: number;
  totalWorkingDays: number;
  totalWeekendDays: number;
  summaryNotes?: string | null;
}

export interface UpdateStaffTravelItinerary {
  id: string;
  status: TravelItineraryStatus;
  title: string;
  isCurrentVersion: boolean;
  totalTravelDays: number;
  totalWorkingDays: number;
  totalWeekendDays: number;
  summaryNotes?: string | null;
  finalizedAt?: string | null;
}

export interface CreateStaffTravelItineraryLeg {
  staffTravelItineraryId: string;
  sequenceOrder: number;
  legType: TravelItineraryLegType;
  legDate: string;
  originCity?: string | null;
  originCountryId?: string | null;
  destinationCity?: string | null;
  destinationCountryId?: string | null;
  transportMode?: StaffTravelTransportMode | null;
  departureDatetime?: string | null;
  arrivalDatetime?: string | null;
  flightBookingId?: string | null;
  hotelBookingId?: string | null;
  groundTransportId?: string | null;
  notes?: string | null;
}

export type UpdateStaffTravelItineraryLeg =
  Omit<CreateStaffTravelItineraryLeg, 'staffTravelItineraryId'> & { id: string };

export interface CreateStaffTravelItineraryActivity {
  staffTravelItineraryLegId: string;
  activityType: StaffTravelActivityType;
  title: string;
  description?: string | null;
  locationName?: string | null;
  locationAddress?: string | null;
  startDatetime?: string | null;
  endDatetime?: string | null;
  contactName?: string | null;
  contactEmail?: string | null;
  contactPhone?: string | null;
  isMandatory: boolean;
}

export type UpdateStaffTravelItineraryActivity =
  Omit<CreateStaffTravelItineraryActivity, 'staffTravelItineraryLegId'> & { id: string };

// ── Bookings, shared ─────────────────────────────────────────────────────────

export type TravelBookingStatus =
  | 'Pending'
  | 'Confirmed'
  | 'Ticketed'
  | 'Cancelled'
  | 'Refunded'
  | 'NoShow'
  | 'Completed'
  | 'OnHold';

export type TravelBookingChannel =
  | 'SelfService'
  | 'TravelDesk'
  | 'TravelAgency'
  | 'DirectAirline'
  | 'DirectHotel'
  | 'OnlinePortal';

/** Ordered Economy → First; the policy cap is a comparison against this ordering. */
export type FlightCabinClass = 'Economy' | 'PremiumEconomy' | 'Business' | 'First';

/**
 * Where an above-cap booking's exception stands (travel final closure D-8, lane 4). `None` on every
 * booking made before migration batch 1.
 */
export type TravelBookingExceptionState = 'None' | 'Pending' | 'Authorised' | 'Refused';

export type GroundTransportType =
  | 'Taxi'
  | 'Rideshare'
  | 'Bus'
  | 'Train'
  | 'Metro'
  | 'CompanyVehicle'
  | 'PrivateCarHire'
  | 'Shuttle'
  | 'Motorcycle'
  | 'Ferry';

export type VehicleCategory =
  | 'Economy'
  | 'Compact'
  | 'Intermediate'
  | 'FullSize'
  | 'Suv'
  | 'Luxury'
  | 'Minivan'
  | 'Truck';

// ── Flights ──────────────────────────────────────────────────────────────────

export interface StaffTravelFlightSegment extends AuditFields {
  staffTravelFlightBookingId: string;
  segmentOrder: number;
  flightNumber: string;
  operatingCarrier: string;
  originAirport: string;
  destinationAirport: string;
  departureDatetime: string;
  arrivalDatetime: string;
  departureTerminal?: string | null;
  arrivalTerminal?: string | null;
  /** Server-derived from the two datetimes. */
  durationMinutes: number;
  aircraftType?: string | null;
  seatNumber?: string | null;
  isLayover: boolean;
  layoverDurationMinutes?: number | null;
  baggageAllowanceKg?: number | null;
}

export interface StaffTravelFlightBookingSummary {
  id: string;
  bookingReference?: string | null;
  airlineName?: string | null;
  bookingClass: FlightCabinClass;
  bookingClassName: string;
  totalFare: number;
  currencyCode: string;
  status: TravelBookingStatus;
  statusName: string;
  ticketNumber?: string | null;
  segmentCount: number;
  vendorName?: string | null;
  /** Lane 4, D-8: a breach of the policy waits for a different travel administrator. */
  exceptionState: TravelBookingExceptionState;
  exceptionStateName: string;
}

/**
 * Who asked for a booking's policy exception and who decided it (lane 4, D-8). `exceptionAuthorisedBy*` is the
 * DECIDER — the state says whether they authorised or refused.
 */
export interface TravelBookingExceptionFields {
  exceptionState: TravelBookingExceptionState;
  exceptionStateName: string;
  exceptionRequestedById?: string | null;
  exceptionRequestedByName?: string | null;
  exceptionAuthorisedById?: string | null;
  exceptionAuthorisedByName?: string | null;
  exceptionAuthorisedAt?: string | null;
}

/** One row of the policy-breach register (lane 4, D-8). */
export interface StaffTravelBookingException {
  bookingId: string;
  kind: 'Flight' | 'Hotel';
  staffTravelRequestId: string;
  requestNumber: string;
  travellerName: string;
  travelStartDate: string;
  booking: string;
  policyCap?: string | null;
  reason?: string | null;
  bookingStatus: TravelBookingStatus;
  bookingStatusName: string;
  exceptionState: TravelBookingExceptionState;
  exceptionStateName: string;
  requestedByName?: string | null;
  decidedByName?: string | null;
  decidedAt?: string | null;
  createdAt: string;
}

export interface StaffTravelFlightBooking extends AuditFields, TravelBookingExceptionFields {
  staffTravelRequestId: string;
  bookingReference?: string | null;
  airlineCode?: string | null;
  airlineName?: string | null;
  bookingClass: FlightCabinClass;
  bookingClassName: string;
  /** Server-assigned from the applicable travel policy. */
  policyAllowedClass: FlightCabinClass;
  policyAllowedClassName: string;
  classExceptionApproved: boolean;
  classExceptionReason?: string | null;
  bookedBy: TravelBookingChannel;
  bookedByName: string;
  vendorId?: string | null;
  vendorName?: string | null;
  totalFare: number;
  taxesAndFees: number;
  currencyCode: string;
  ticketNumber?: string | null;
  status: TravelBookingStatus;
  statusName: string;
  bookedAt?: string | null;
  cancelledAt?: string | null;
  cancellationFee?: number | null;
  segments: StaffTravelFlightSegment[];
}

export interface CreateStaffTravelFlightBooking {
  staffTravelRequestId: string;
  bookingReference?: string | null;
  airlineCode?: string | null;
  airlineName?: string | null;
  bookingClass: FlightCabinClass;
  /** ASKS for a policy exception, with the reason (lane 4, D-8). */
  classExceptionApproved: boolean;
  classExceptionReason?: string | null;
  bookedBy: TravelBookingChannel;
  vendorId?: string | null;
  totalFare: number;
  taxesAndFees: number;
  currencyCode: string;
  ticketNumber?: string | null;
}

export type UpdateStaffTravelFlightBooking =
  Omit<CreateStaffTravelFlightBooking, 'staffTravelRequestId'> & { id: string };

export interface CreateStaffTravelFlightSegment {
  staffTravelFlightBookingId: string;
  segmentOrder: number;
  flightNumber: string;
  operatingCarrier: string;
  originAirport: string;
  destinationAirport: string;
  departureDatetime: string;
  arrivalDatetime: string;
  departureTerminal?: string | null;
  arrivalTerminal?: string | null;
  aircraftType?: string | null;
  seatNumber?: string | null;
  isLayover: boolean;
  layoverDurationMinutes?: number | null;
  baggageAllowanceKg?: number | null;
}

export type UpdateStaffTravelFlightSegment =
  Omit<CreateStaffTravelFlightSegment, 'staffTravelFlightBookingId'> & { id: string };

// ── Hotels ───────────────────────────────────────────────────────────────────

export interface StaffTravelHotelBookingSummary {
  id: string;
  hotelName: string;
  city: string;
  checkInDate: string;
  checkOutDate: string;
  numberOfNights: number;
  totalCost: number;
  currencyCode: string;
  status: TravelBookingStatus;
  statusName: string;
  vendorName?: string | null;
  exceptionState: TravelBookingExceptionState;
  exceptionStateName: string;
}

export interface StaffTravelHotelBooking extends AuditFields, TravelBookingExceptionFields {
  staffTravelRequestId: string;
  bookingReference?: string | null;
  hotelName: string;
  hotelChain?: string | null;
  hotelAddress?: string | null;
  city: string;
  countryId: string;
  countryName?: string | null;
  starRating?: number | null;
  checkInDate: string;
  checkOutDate: string;
  /** Server-derived from the two dates. */
  numberOfNights: number;
  roomType?: string | null;
  ratePerNight: number;
  /** Server-derived: rate × nights. */
  totalCost: number;
  currencyCode: string;
  /** Server-assigned from the applicable travel policy. */
  policyMaxRatePerNight?: number | null;
  rateExceptionApproved: boolean;
  rateExceptionReason?: string | null;
  vendorId?: string | null;
  vendorName?: string | null;
  bookedBy: TravelBookingChannel;
  bookedByName: string;
  status: TravelBookingStatus;
  statusName: string;
  cancellationPolicy?: string | null;
  bookedAt?: string | null;
  cancelledAt?: string | null;
  cancellationFee?: number | null;
}

export interface CreateStaffTravelHotelBooking {
  staffTravelRequestId: string;
  bookingReference?: string | null;
  hotelName: string;
  hotelChain?: string | null;
  hotelAddress?: string | null;
  city: string;
  countryId: string;
  starRating?: number | null;
  checkInDate: string;
  checkOutDate: string;
  roomType?: string | null;
  ratePerNight: number;
  currencyCode: string;
  /** ASKS for a policy exception, with the reason (lane 4, D-8). */
  rateExceptionApproved: boolean;
  rateExceptionReason?: string | null;
  vendorId?: string | null;
  bookedBy: TravelBookingChannel;
  cancellationPolicy?: string | null;
}

export type UpdateStaffTravelHotelBooking =
  Omit<CreateStaffTravelHotelBooking, 'staffTravelRequestId'> & { id: string };

// ── Ground transport ─────────────────────────────────────────────────────────

export interface StaffTravelGroundTransport extends AuditFields {
  staffTravelRequestId: string;
  /** The Fleet trip reserving a company vehicle; null for external transport. */
  fleetTripId?: string | null;
  transportType: GroundTransportType;
  transportTypeName: string;
  vendorId?: string | null;
  vendorName?: string | null;
  bookingReference?: string | null;
  pickupLocation?: string | null;
  dropoffLocation?: string | null;
  pickupDatetime?: string | null;
  dropoffDatetime?: string | null;
  estimatedCost?: number | null;
  actualCost?: number | null;
  currencyCode: string;
  status: TravelBookingStatus;
  statusName: string;
  notes?: string | null;
}

/**
 * ⚠ `vehicleAssetId` is **required when `transportType` is `CompanyVehicle`** — that mode reserves a
 * real vehicle through Fleet rather than recording a note, and the server refuses without it.
 */
export interface CreateStaffTravelGroundTransport {
  staffTravelRequestId: string;
  transportType: GroundTransportType;
  vehicleAssetId?: string | null;
  driverEmployeeId?: string | null;
  vendorId?: string | null;
  bookingReference?: string | null;
  pickupLocation?: string | null;
  dropoffLocation?: string | null;
  pickupDatetime?: string | null;
  dropoffDatetime?: string | null;
  estimatedCost?: number | null;
  actualCost?: number | null;
  currencyCode: string;
  notes?: string | null;
}

export type UpdateStaffTravelGroundTransport =
  Omit<CreateStaffTravelGroundTransport, 'staffTravelRequestId' | 'vehicleAssetId' | 'driverEmployeeId'>
  & { id: string };

// ── Car rentals ──────────────────────────────────────────────────────────────

export interface StaffTravelCarRentalBooking extends AuditFields {
  staffTravelRequestId: string;
  vendorId?: string | null;
  vendorName?: string | null;
  bookingReference?: string | null;
  pickupLocation?: string | null;
  dropoffLocation?: string | null;
  pickupDatetime: string;
  dropoffDatetime: string;
  vehicleCategory: VehicleCategory;
  vehicleCategoryName: string;
  vehicleModel?: string | null;
  dailyRate: number;
  /** Server-derived: daily rate × hire days, a part-day counting as a day. */
  totalCost: number;
  currencyCode: string;
  insuranceIncluded: boolean;
  fuelPolicy?: string | null;
  driverLicenseRequired: boolean;
  status: TravelBookingStatus;
  statusName: string;
  bookedAt?: string | null;
}

export interface CreateStaffTravelCarRentalBooking {
  staffTravelRequestId: string;
  vendorId?: string | null;
  bookingReference?: string | null;
  pickupLocation?: string | null;
  dropoffLocation?: string | null;
  pickupDatetime: string;
  dropoffDatetime: string;
  vehicleCategory: VehicleCategory;
  vehicleModel?: string | null;
  dailyRate: number;
  currencyCode: string;
  insuranceIncluded: boolean;
  fuelPolicy?: string | null;
  driverLicenseRequired: boolean;
}

export type UpdateStaffTravelCarRentalBooking =
  Omit<CreateStaffTravelCarRentalBooking, 'staffTravelRequestId'> & { id: string };
