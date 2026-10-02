import { apiService } from '../api.service';
import type {
  StaffTravelItinerary,
  StaffTravelItinerarySummary,
  StaffTravelItineraryLeg,
  StaffTravelItineraryActivity,
  CreateStaffTravelItinerary,
  UpdateStaffTravelItinerary,
  CreateStaffTravelItineraryLeg,
  UpdateStaffTravelItineraryLeg,
  CreateStaffTravelItineraryActivity,
  UpdateStaffTravelItineraryActivity,
  StaffTravelFlightBooking,
  StaffTravelFlightBookingSummary,
  StaffTravelFlightSegment,
  CreateStaffTravelFlightBooking,
  UpdateStaffTravelFlightBooking,
  CreateStaffTravelFlightSegment,
  UpdateStaffTravelFlightSegment,
  StaffTravelHotelBooking,
  StaffTravelHotelBookingSummary,
  CreateStaffTravelHotelBooking,
  UpdateStaffTravelHotelBooking,
  StaffTravelGroundTransport,
  CreateStaffTravelGroundTransport,
  UpdateStaffTravelGroundTransport,
  StaffTravelCarRentalBooking,
  CreateStaffTravelCarRentalBooking,
  UpdateStaffTravelCarRentalBooking,
  TravelBookingStatus,
  TravelBookingExceptionState,
  StaffTravelBookingException,
} from '@/types/hr/travel-bookings';

/**
 * A travel request's itinerary and its bookings.
 *
 * <b>Two controllers, one concern.</b> `api/staff-travel/itineraries` holds the plan — what the
 * traveller will do, day by day — and `api/staff-travel/bookings` holds what has actually been
 * reserved. A leg can point at a booking, which is how the two meet.
 *
 * ⚠ <b>Reads are `HR.Travel.Read`, writes and the status verbs are `HR.Travel.Write`, and every delete
 * is `HR.Travel.Admin`</b> — which HR holds since the travel closure's lane 4 (D-3).
 *
 * ⚠ <b>Bookings live on an approved trip (lane 5, D-23).</b> A create is refused unless the trip is
 * Approved or under way, and is saved Pending; the status moves only by the verbs below (hold, confirm,
 * ticket, cancel, no-show, complete) — an edit never changes it. Only a Pending booking is deleted.
 *
 * ⚠ <b>Booking above a policy cap is refused</b> unless the matching `*ExceptionApproved` flag ASKS
 * for an exception with a reason (lane 4, D-8); the booking then waits for another travel
 * administrator's authorisation before it can be confirmed. Every refusal carries a message worth
 * showing verbatim — the cap is not something the screen can predict.
 */
class TravelBookingsService {
  private readonly itineraries = '/staff-travel/itineraries';
  private readonly bookings = '/staff-travel/bookings';

  // ── Itineraries ────────────────────────────────────────────────────────────

  getItinerary(id: string) {
    return apiService.get<StaffTravelItinerary>(`${this.itineraries}/${id}`);
  }

  /** Every version raised for a request, newest version first is NOT guaranteed — sort on read. */
  getItinerariesByRequest(requestId: string) {
    return apiService.get<StaffTravelItinerarySummary[]>(
      `${this.itineraries}/request/${requestId}`);
  }

  /** The version in force. Null when a request has no itinerary yet — not an error. */
  getCurrentItinerary(requestId: string) {
    return apiService.get<StaffTravelItinerary | null>(
      `${this.itineraries}/request/${requestId}/current`);
  }

  createItinerary(payload: CreateStaffTravelItinerary) {
    return apiService.post<StaffTravelItinerary>(this.itineraries, payload);
  }

  updateItinerary(payload: UpdateStaffTravelItinerary) {
    return apiService.put<StaffTravelItinerary>(`${this.itineraries}/${payload.id}`, payload);
  }

  /** Promotes a version to current; the one it replaces becomes Superseded. */
  setCurrentItinerary(id: string) {
    return apiService.post<void>(`${this.itineraries}/${id}/set-current`, {});
  }

  /** Finalises the version in force (it needs a leg): Approved and stamped — after that a change is a new version. */
  finaliseItinerary(id: string) {
    return apiService.post<StaffTravelItinerary>(`${this.itineraries}/${id}/finalise`, {});
  }

  deleteItinerary(id: string) {
    return apiService.delete<void>(`${this.itineraries}/${id}`);
  }

  // ── Legs and activities ────────────────────────────────────────────────────

  getLegs(itineraryId: string) {
    return apiService.get<StaffTravelItineraryLeg[]>(`${this.itineraries}/${itineraryId}/legs`);
  }

  addLeg(itineraryId: string, payload: CreateStaffTravelItineraryLeg) {
    return apiService.post<StaffTravelItineraryLeg>(
      `${this.itineraries}/${itineraryId}/legs`, payload);
  }

  updateLeg(payload: UpdateStaffTravelItineraryLeg) {
    return apiService.put<StaffTravelItineraryLeg>(
      `${this.itineraries}/legs/${payload.id}`, payload);
  }

  deleteLeg(legId: string) {
    return apiService.delete<void>(`${this.itineraries}/legs/${legId}`);
  }

  getActivities(legId: string) {
    return apiService.get<StaffTravelItineraryActivity[]>(
      `${this.itineraries}/legs/${legId}/activities`);
  }

  addActivity(legId: string, payload: CreateStaffTravelItineraryActivity) {
    return apiService.post<StaffTravelItineraryActivity>(
      `${this.itineraries}/legs/${legId}/activities`, payload);
  }

  updateActivity(payload: UpdateStaffTravelItineraryActivity) {
    return apiService.put<StaffTravelItineraryActivity>(
      `${this.itineraries}/activities/${payload.id}`, payload);
  }

  deleteActivity(activityId: string) {
    return apiService.delete<void>(`${this.itineraries}/activities/${activityId}`);
  }

  // ── Flights ────────────────────────────────────────────────────────────────

  getFlight(id: string) {
    return apiService.get<StaffTravelFlightBooking>(`${this.bookings}/flights/${id}`);
  }

  getFlightsByRequest(requestId: string) {
    return apiService.get<StaffTravelFlightBookingSummary[]>(
      `${this.bookings}/flights/request/${requestId}`);
  }

  /** The desk's cross-request queue — e.g. everything still Pending. */
  getFlightsByStatus(status: TravelBookingStatus) {
    return apiService.get<StaffTravelFlightBookingSummary[]>(
      `${this.bookings}/flights/status/${status}`);
  }

  createFlight(payload: CreateStaffTravelFlightBooking) {
    return apiService.post<StaffTravelFlightBooking>(`${this.bookings}/flights`, payload);
  }

  updateFlight(payload: UpdateStaffTravelFlightBooking) {
    return apiService.put<StaffTravelFlightBooking>(
      `${this.bookings}/flights/${payload.id}`, payload);
  }

  deleteFlight(id: string) {
    return apiService.delete<void>(`${this.bookings}/flights/${id}`);
  }

  getSegments(flightBookingId: string) {
    return apiService.get<StaffTravelFlightSegment[]>(
      `${this.bookings}/flights/${flightBookingId}/segments`);
  }

  addSegment(flightBookingId: string, payload: CreateStaffTravelFlightSegment) {
    return apiService.post<StaffTravelFlightSegment>(
      `${this.bookings}/flights/${flightBookingId}/segments`, payload);
  }

  updateSegment(payload: UpdateStaffTravelFlightSegment) {
    return apiService.put<StaffTravelFlightSegment>(
      `${this.bookings}/segments/${payload.id}`, payload);
  }

  deleteSegment(segmentId: string) {
    return apiService.delete<void>(`${this.bookings}/segments/${segmentId}`);
  }

  // ── Hotels ─────────────────────────────────────────────────────────────────

  getHotel(id: string) {
    return apiService.get<StaffTravelHotelBooking>(`${this.bookings}/hotels/${id}`);
  }

  getHotelsByRequest(requestId: string) {
    return apiService.get<StaffTravelHotelBookingSummary[]>(
      `${this.bookings}/hotels/request/${requestId}`);
  }

  createHotel(payload: CreateStaffTravelHotelBooking) {
    return apiService.post<StaffTravelHotelBooking>(`${this.bookings}/hotels`, payload);
  }

  updateHotel(payload: UpdateStaffTravelHotelBooking) {
    return apiService.put<StaffTravelHotelBooking>(
      `${this.bookings}/hotels/${payload.id}`, payload);
  }

  deleteHotel(id: string) {
    return apiService.delete<void>(`${this.bookings}/hotels/${id}`);
  }

  // ── Policy exceptions on bookings (lane 4, D-8) ────────────────────────────

  /** The policy-breach register: flight and hotel bookings that breach their trip's policy, pending first. */
  getBookingExceptions(state?: TravelBookingExceptionState) {
    return apiService.get<StaffTravelBookingException[]>(
      `${this.bookings}/exceptions${state ? `?state=${state}` : ''}`);
  }

  /**
   * A travel administrator who neither booked it nor asked for the exception, and is not the traveller, decides
   * it. A refusal needs a reason of five characters or more, kept on the trip as an internal note.
   */
  decideBookingException(kind: 'Flight' | 'Hotel', id: string, authorise: boolean, reason?: string) {
    const path = `${this.bookings}/${kind === 'Flight' ? 'flights' : 'hotels'}/${id}/exception/${authorise ? 'authorise' : 'refuse'}`;
    return apiService.post<StaffTravelFlightBooking | StaffTravelHotelBooking>(
      path, authorise ? {} : { reason: reason ?? '' });
  }

  // ── Ground transport ───────────────────────────────────────────────────────

  getGroundTransport(id: string) {
    return apiService.get<StaffTravelGroundTransport>(`${this.bookings}/ground-transport/${id}`);
  }

  getGroundTransportsByRequest(requestId: string) {
    return apiService.get<StaffTravelGroundTransport[]>(
      `${this.bookings}/ground-transport/request/${requestId}`);
  }

  /**
   * ⚠ A `CompanyVehicle` leg must carry `vehicleAssetId` — it reserves a real vehicle through
   * Fleet. Fleet's own refusals ("Selected asset is not a vehicle") come back as 422 with the
   * reason; show it rather than a generic failure.
   */
  createGroundTransport(payload: CreateStaffTravelGroundTransport) {
    return apiService.post<StaffTravelGroundTransport>(
      `${this.bookings}/ground-transport`, payload);
  }

  updateGroundTransport(payload: UpdateStaffTravelGroundTransport) {
    return apiService.put<StaffTravelGroundTransport>(
      `${this.bookings}/ground-transport/${payload.id}`, payload);
  }

  deleteGroundTransport(id: string) {
    return apiService.delete<void>(`${this.bookings}/ground-transport/${id}`);
  }

  // ── Car rentals ────────────────────────────────────────────────────────────

  getCarRental(id: string) {
    return apiService.get<StaffTravelCarRentalBooking>(`${this.bookings}/car-rentals/${id}`);
  }

  getCarRentalsByRequest(requestId: string) {
    return apiService.get<StaffTravelCarRentalBooking[]>(
      `${this.bookings}/car-rentals/request/${requestId}`);
  }

  createCarRental(payload: CreateStaffTravelCarRentalBooking) {
    return apiService.post<StaffTravelCarRentalBooking>(`${this.bookings}/car-rentals`, payload);
  }

  updateCarRental(payload: UpdateStaffTravelCarRentalBooking) {
    return apiService.put<StaffTravelCarRentalBooking>(
      `${this.bookings}/car-rentals/${payload.id}`, payload);
  }

  deleteCarRental(id: string) {
    return apiService.delete<void>(`${this.bookings}/car-rentals/${id}`);
  }

  // ── Status verbs (lane 5, D1) ──────────────────────────────────────────────

  /**
   * Hold (Pending → On hold), confirm (Pending or On hold → Confirmed — a breach's exception must be
   * authorised), no-show or complete (Confirmed or Ticketed, once the trip has started).
   */
  moveBooking(kind: TravelBookingKind, id: string, verb: 'hold' | 'confirm' | 'no-show' | 'complete') {
    return apiService.post<unknown>(`${this.bookings}/${BOOKING_KIND_PATH[kind]}/${id}/${verb}`, {});
  }

  /**
   * Cancels a live booking: a reason (five characters or more, kept on the trip as an internal note) and,
   * on a flight or hotel only, the supplier's cancellation fee — which the budget counts as committed.
   */
  cancelBooking(kind: TravelBookingKind, id: string, reason: string, cancellationFee?: number | null) {
    return apiService.post<unknown>(`${this.bookings}/${BOOKING_KIND_PATH[kind]}/${id}/cancel`,
      { reason, cancellationFee: cancellationFee ?? null });
  }

  /** Tickets a confirmed flight; refused while the trip needs a visa that is not approved (T-24). */
  ticketFlight(id: string, ticketNumber: string) {
    return apiService.post<StaffTravelFlightBooking>(`${this.bookings}/flights/${id}/ticket`, { ticketNumber });
  }
}

export type TravelBookingKind = 'Flight' | 'Hotel' | 'Ground' | 'CarRental';

const BOOKING_KIND_PATH: Record<TravelBookingKind, string> = {
  Flight: 'flights',
  Hotel: 'hotels',
  Ground: 'ground-transport',
  CarRental: 'car-rentals',
};

export const travelBookingsService = new TravelBookingsService();
