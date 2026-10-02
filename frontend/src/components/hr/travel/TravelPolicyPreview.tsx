'use client';

import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, Loader2, ShieldCheck } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { travelService } from '@/services/hr/travel.service';
import { TravelQueryError } from './TravelQueryError';
import { FLIGHT_CABIN_CLASS_LABELS, enumLabel } from './travel-enums';
import { fmtTravelMoney } from './travel-format';

/**
 * The approved travel policy a trip will be checked against, and its limits — on the request form,
 * before anything is saved (travel final closure, lane 1 — finding T-16).
 *
 * The server resolves it exactly as it does at submission: the traveller's own organisation unit
 * (from their employee record — the form no longer offers a unit), the two countries, and the
 * departure date. The single-trip limit is enforced at submission, so the warning here is a preview
 * of a refusal, not the rule itself; a trip costed in another currency is compared at Finance's rate
 * there and only flagged here.
 */
export function TravelPolicyPreview({
  surface,
  employeeId,
  departure,
  originCountryId,
  destinationCountryId,
  estimatedTotalCost,
  currencyCode,
}: {
  surface: 'desk' | 'self';
  /** The traveller, on the desk; the self-service read takes the caller from the token. */
  employeeId?: string;
  departure?: string;
  originCountryId?: string;
  destinationCountryId?: string;
  estimatedTotalCost: number;
  currencyCode?: string;
}) {
  const ready = !!departure && (surface === 'self' || !!employeeId);
  const geography = {
    originCountryId: originCountryId || undefined,
    destinationCountryId: destinationCountryId || undefined,
  };

  const { data, isLoading, isError, error } = useQuery({
    queryKey: [
      'travel-policy-preview', surface, employeeId ?? null, departure ?? null,
      originCountryId ?? null, destinationCountryId ?? null,
    ],
    queryFn: () =>
      surface === 'desk'
        ? travelService.getPolicyPreview({ employeeId: employeeId ?? '', departure: departure ?? '', ...geography })
        : travelService.getMyPolicyPreview({ departure: departure ?? '', ...geography }),
    enabled: ready,
    staleTime: 60_000,
  });

  const limit = data?.hasPolicy ? data.maxSingleTripBudget : null;
  const sameCurrency = !!currencyCode && currencyCode === data?.currencyCode;
  const overLimit = limit != null && sameCurrency && estimatedTotalCost > limit;
  const comparedAtRate = limit != null && !!currencyCode && !!data?.currencyCode && !sameCurrency;
  const whose = surface === 'self' ? 'your' : "the traveller's";

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="flex items-center gap-2 text-base">
          <ShieldCheck className="h-4 w-4" /> Policy and limits
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-2 text-sm">
        {!ready ? (
          <p className="text-muted-foreground">
            {surface === 'desk' && !employeeId
              ? 'Choose the traveller and the departure date to see the policy that applies.'
              : 'Set the departure date to see the policy in force then.'}
          </p>
        ) : isLoading ? (
          <p className="flex items-center gap-2 text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" /> Finding the policy…
          </p>
        ) : isError && !data ? (
          <TravelQueryError error={error} what="the travel policy" />
        ) : data ? (
          <>
            <p>
              <span className="text-muted-foreground">Organisation unit: </span>
              {data.organizationUnitName ?? 'none on the employee record'}
              <span className="text-xs text-muted-foreground"> — from {whose} employee record</span>
            </p>
            {!data.hasPolicy ? (
              <p className="text-muted-foreground">
                No approved travel policy covers this trip, so no limits apply to it.
              </p>
            ) : (
              <>
                <p>
                  <span className="text-muted-foreground">Policy: </span>
                  {data.policyName}
                  {data.versionNumber ? ` (version ${data.versionNumber})` : ''}
                </p>
                <ul className="list-disc space-y-0.5 pl-5">
                  <li>
                    {limit != null
                      ? `A single trip may be estimated at up to ${fmtTravelMoney(limit, data.currencyCode)}.`
                      : 'No limit on what a single trip may be estimated at.'}
                  </li>
                  {data.maxFlightClass && (
                    <li>
                      Flights up to {enumLabel(FLIGHT_CABIN_CLASS_LABELS, data.maxFlightClass)} on{' '}
                      {data.isInternational ? 'an international' : 'a domestic'} trip.
                    </li>
                  )}
                  {data.maxHotelRatePerNight != null && (
                    <li>Hotels up to {fmtTravelMoney(data.maxHotelRatePerNight, data.currencyCode)} a night.</li>
                  )}
                </ul>
                {overLimit && (
                  <p role="alert" className="flex items-start gap-2 text-destructive">
                    <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                    The estimate is above the single-trip limit, so the request will be refused when it
                    is submitted.
                  </p>
                )}
                {comparedAtRate && (
                  <p className="text-xs text-muted-foreground">
                    The estimate is in {currencyCode} and the limit in {data.currencyCode}; they are
                    compared at Finance&apos;s rate when the request is submitted.
                  </p>
                )}
              </>
            )}
          </>
        ) : null}
      </CardContent>
    </Card>
  );
}
