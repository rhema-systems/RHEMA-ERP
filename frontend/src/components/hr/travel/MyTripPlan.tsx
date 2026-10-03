'use client';

import { useQuery } from '@tanstack/react-query';
import { BedDouble, Car, CarFront, Loader2, Map as MapIcon, Plane } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { travelService } from '@/services/hr/travel.service';
import type { StaffTravelRequest } from '@/types/hr/travel';
import { TravelQueryError } from './TravelQueryError';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtDateTime = (v?: string | null) =>
  (v ? new Date(v).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' }) : '—');
const humanize = (v?: string | null) => (v ? v.replace(/([a-z])([A-Z])/g, '$1 $2') : '—');

/**
 * Where the traveller is going and what is booked (travel final closure, lane 7, slice 7c1 — E7).
 *
 * The request's own read carries the itinerary and the bookings as summaries — no legs, no flight times — so this reads
 * their detail from `/me`. The itinerary is the version in force only, the one the desk finalised (D-42): while the
 * desk drafts one the tab says so rather than showing a plan that may change. Bookings arrive without the policy
 * exception's decision — who asked, who authorised it and why are the desk's (P3).
 */
export function MyTripPlan({ request }: { request: StaffTravelRequest }) {
  const itinerary = useQuery({
    queryKey: ['my-travel-itinerary', request.id],
    queryFn: () => travelService.getMyItinerary(request.id),
  });
  const bookings = useQuery({
    queryKey: ['my-travel-bookings', request.id],
    queryFn: () => travelService.getMyBookings(request.id),
  });

  const plan = itinerary.data?.inForce;
  const b = bookings.data;
  const nothingBooked = b && b.flights.length + b.hotels.length + b.groundTransports.length + b.carRentals.length === 0;

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="flex items-center gap-2 text-base">
            <MapIcon className="h-4 w-4" /> Itinerary
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {itinerary.isError && !itinerary.data ? (
            <TravelQueryError error={itinerary.error} what="your itinerary" />
          ) : itinerary.isLoading ? (
            <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
          ) : !plan ? (
            <p className="text-sm text-muted-foreground">
              {itinerary.data?.beingPlanned
                ? 'The travel desk is planning your itinerary. It shows here once they have finalised it.'
                : 'No itinerary has been made for this trip yet.'}
            </p>
          ) : (
            <>
              <p className="text-sm">
                <span className="font-medium">{plan.title}</span>
                <span className="text-muted-foreground">
                  {' '}· version {plan.versionNumber} · {plan.totalTravelDays} day{plan.totalTravelDays === 1 ? '' : 's'}
                  {plan.finalizedAt && ` · finalised ${fmtDate(plan.finalizedAt)}`}
                </span>
              </p>
              {plan.summaryNotes && <p className="whitespace-pre-line text-sm">{plan.summaryNotes}</p>}
              <ol className="space-y-2">
                {plan.legs.slice().sort((x, y) => x.sequenceOrder - y.sequenceOrder).map((leg) => (
                  <li key={leg.id} className="rounded-md border p-3">
                    <div className="flex flex-wrap items-baseline justify-between gap-2">
                      <p className="text-sm font-medium">
                        {fmtDate(leg.legDate)} · {humanize(leg.legTypeName)}
                        {(leg.originCity || leg.destinationCity) && (
                          <span className="font-normal"> — {leg.originCity ?? '…'} → {leg.destinationCity ?? '…'}</span>
                        )}
                      </p>
                      {leg.transportModeName && (
                        <span className="text-xs text-muted-foreground">{humanize(leg.transportModeName)}</span>
                      )}
                    </div>
                    {(leg.departureDatetime || leg.arrivalDatetime) && (
                      <p className="text-xs text-muted-foreground">
                        {leg.departureDatetime && `Leaves ${fmtDateTime(leg.departureDatetime)}`}
                        {leg.departureDatetime && leg.arrivalDatetime && ' · '}
                        {leg.arrivalDatetime && `arrives ${fmtDateTime(leg.arrivalDatetime)}`}
                      </p>
                    )}
                    {leg.linkedBooking && <p className="text-xs text-muted-foreground">Booked: {leg.linkedBooking}</p>}
                    {leg.notes && <p className="mt-1 whitespace-pre-line text-sm">{leg.notes}</p>}
                    {leg.activities.length > 0 && (
                      <ul className="mt-2 space-y-1 border-l pl-3">
                        {leg.activities.map((a) => (
                          <li key={a.id} className="text-sm">
                            {a.startDatetime && <span className="text-muted-foreground">{fmtDateTime(a.startDatetime)} · </span>}
                            {a.title}
                            {a.locationName && <span className="text-muted-foreground"> — {a.locationName}</span>}
                            {a.contactName && (
                              <span className="text-muted-foreground">
                                {' '}(contact {a.contactName}{a.contactPhone ? `, ${a.contactPhone}` : ''})
                              </span>
                            )}
                          </li>
                        ))}
                      </ul>
                    )}
                  </li>
                ))}
              </ol>
            </>
          )}
        </CardContent>
      </Card>

      {bookings.isError && !bookings.data ? (
        <TravelQueryError error={bookings.error} what="your bookings" />
      ) : bookings.isLoading ? (
        <div className="flex justify-center p-6"><Loader2 className="h-5 w-5 animate-spin text-muted-foreground" /></div>
      ) : nothingBooked ? (
        <Card>
          <CardContent className="p-6 text-sm text-muted-foreground">
            Nothing is booked for this trip yet. The travel desk books flights, hotels and transport once it is approved.
          </CardContent>
        </Card>
      ) : b && (
        <>
          {b.flights.length > 0 && (
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="flex items-center gap-2 text-base"><Plane className="h-4 w-4" /> Flights</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                {b.flights.map((f) => (
                  <div key={f.id} className="rounded-md border p-3">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <p className="text-sm font-medium">
                        {f.airlineName ?? f.vendorName ?? 'Flight'}
                        <span className="font-normal text-muted-foreground">
                          {' '}· {humanize(f.bookingClassName)}
                          {f.bookingReference && ` · booking ${f.bookingReference}`}
                          {f.ticketNumber && ` · ticket ${f.ticketNumber}`}
                        </span>
                      </p>
                      <StatusBadge status={humanize(f.statusName)} />
                    </div>
                    {f.segments.length === 0 ? (
                      <p className="mt-1 text-xs text-muted-foreground">The flight times are not recorded yet.</p>
                    ) : (
                      <ul className="mt-2 space-y-1">
                        {f.segments.map((s) => (
                          <li key={s.id} className="text-sm">
                            <span className="font-mono">{s.flightNumber}</span> {s.originAirport} → {s.destinationAirport}
                            <span className="text-muted-foreground">
                              {' '}· {fmtDateTime(s.departureDatetime)}
                              {s.departureTerminal && ` (terminal ${s.departureTerminal})`} – {fmtDateTime(s.arrivalDatetime)}
                              {s.seatNumber && ` · seat ${s.seatNumber}`}
                              {s.baggageAllowanceKg != null && ` · ${s.baggageAllowanceKg} kg baggage`}
                            </span>
                          </li>
                        ))}
                      </ul>
                    )}
                  </div>
                ))}
              </CardContent>
            </Card>
          )}

          {b.hotels.length > 0 && (
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="flex items-center gap-2 text-base"><BedDouble className="h-4 w-4" /> Hotels</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                {b.hotels.map((h) => (
                  <div key={h.id} className="rounded-md border p-3">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <p className="text-sm font-medium">
                        {h.hotelName}
                        <span className="font-normal text-muted-foreground">
                          {' '}· {h.city}{h.bookingReference && ` · booking ${h.bookingReference}`}
                        </span>
                      </p>
                      <StatusBadge status={humanize(h.statusName)} />
                    </div>
                    <p className="text-xs text-muted-foreground">
                      {fmtDate(h.checkInDate)} – {fmtDate(h.checkOutDate)} · {h.numberOfNights} night{h.numberOfNights === 1 ? '' : 's'}
                      {h.roomType && ` · ${h.roomType}`}
                    </p>
                    {h.hotelAddress && <p className="mt-1 text-sm">{h.hotelAddress}</p>}
                    {h.cancellationPolicy && (
                      <p className="mt-1 text-xs text-muted-foreground">Cancellation: {h.cancellationPolicy}</p>
                    )}
                  </div>
                ))}
              </CardContent>
            </Card>
          )}

          {b.groundTransports.length > 0 && (
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="flex items-center gap-2 text-base"><CarFront className="h-4 w-4" /> Ground transport</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                {b.groundTransports.map((g) => (
                  <div key={g.id} className="rounded-md border p-3">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <p className="text-sm font-medium">
                        {humanize(g.transportTypeName)}
                        <span className="font-normal text-muted-foreground">
                          {' '}· {g.pickupLocation ?? '…'} → {g.dropoffLocation ?? '…'}
                        </span>
                      </p>
                      <StatusBadge status={humanize(g.statusName)} />
                    </div>
                    <p className="text-xs text-muted-foreground">
                      {g.pickupDatetime ? `Pick-up ${fmtDateTime(g.pickupDatetime)}` : 'Pick-up time to be confirmed'}
                      {g.vehicleName && ` · ${g.vehicleName}`}{g.vehiclePlate && ` (${g.vehiclePlate})`}
                      {g.driverName && ` · driver ${g.driverName}`}
                      {g.vendorName && ` · ${g.vendorName}`}
                      {g.bookingReference && ` · booking ${g.bookingReference}`}
                    </p>
                    {g.notes && <p className="mt-1 whitespace-pre-line text-sm">{g.notes}</p>}
                  </div>
                ))}
              </CardContent>
            </Card>
          )}

          {b.carRentals.length > 0 && (
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="flex items-center gap-2 text-base"><Car className="h-4 w-4" /> Car rental</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                {b.carRentals.map((c) => (
                  <div key={c.id} className="rounded-md border p-3">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <p className="text-sm font-medium">
                        {c.vendorName ?? 'Car rental'}
                        <span className="font-normal text-muted-foreground">
                          {' '}· {c.vehicleModel ?? humanize(c.vehicleCategoryName)}
                          {c.bookingReference && ` · booking ${c.bookingReference}`}
                        </span>
                      </p>
                      <StatusBadge status={humanize(c.statusName)} />
                    </div>
                    <p className="text-xs text-muted-foreground">
                      {c.pickupLocation ?? 'Pick-up'} {fmtDateTime(c.pickupDatetime)} → {c.dropoffLocation ?? 'drop-off'}{' '}
                      {fmtDateTime(c.dropoffDatetime)}
                      {c.driverLicenseRequired && ' · bring your driving licence'}
                    </p>
                  </div>
                ))}
              </CardContent>
            </Card>
          )}
        </>
      )}
    </div>
  );
}
