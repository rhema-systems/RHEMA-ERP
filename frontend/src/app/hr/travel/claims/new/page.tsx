'use client';

import { Suspense, useEffect, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { SelectField } from '@/components/hr/employee/tabs/fields';
import { TravelQueryError } from '@/components/hr/travel/TravelQueryError';
import { fmtTravelMoney as fmtMoney } from '@/components/hr/travel/travel-format';
import { useTravelAccess } from '@/components/hr/travel/useTravelAccess';
import { useToast } from '@/hooks/use-toast';
import { travelService } from '@/services/hr/travel.service';
import { travelFinanceService } from '@/services/hr/travel-finance.service';
import type { TravelClaimType } from '@/types/hr/travel-finance';

const CLAIM_TYPES: TravelClaimType[] = [
  'PostTravel', 'AdvanceSettlement', 'PartialClaim', 'Amendment',
];

const humanize = (v: string) => v.replace(/([a-z])([A-Z])/g, '$1 $2');
const options = (values: readonly string[]) => values.map((v) => ({ value: v, label: humanize(v) }));

// Lane 3: no currency to choose — a claim is kept in the base currency, set by the server (B11).
const schema = z.object({
  claimType: z.enum(CLAIM_TYPES as [string, ...string[]]),
  travelAdvanceId: z.string().optional(),
});

/** A trip takes claims once it is approved — under way and completed too (lane 3, B3). */
const TAKES_CLAIMS = ['Approved', 'InProgress', 'Completed'];
/** Advance cash still with the traveller — what a claim recovers when it is paid. */
const CASH_OUT = ['Disbursed', 'PartiallySettled', 'Overdue'];

/**
 * Files an empty claim against a trip; the expenses are added on the claim itself.
 *
 * Two steps rather than one long form, because the server computes every total from the lines and
 * an expense can be refused on its own (a currency Finance has no rate for that day). Collecting
 * ten expenses into one submit would mean discarding nine good ones to fix the tenth.
 */
function NewTravelClaimForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const requestId = searchParams.get('requestId');
  const { toast } = useToast();
  const access = useTravelAccess();
  const [saving, setSaving] = useState(false);

  const { data: request, isLoading, isError, error } = useQuery({
    queryKey: ['travel-request', requestId],
    queryFn: () => travelService.getById(requestId as string),
    enabled: !!requestId,
  });

  // ⚠ The currency list is read through `api/hr/currencies` (inside CurrencyField). This page read
  // `api/finance/currencies`, which answers 403 without a Finance permission, so the dropdown was
  // empty for the HR desk and no claim could be filed (travel final closure, lane 0 — O-19).

  // Only an advance on THIS trip can be settled by this claim.
  const { data: advances } = useQuery({
    queryKey: ['travel-advances', requestId],
    queryFn: () => travelFinanceService.getAdvancesByRequest(requestId as string),
    enabled: !!requestId,
  });

  const form = useForm<z.input<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: { claimType: 'PostTravel', travelAdvanceId: '' },
  });

  // Only advance cash still with the traveller can be recovered (lane 3: a requested or approved advance owes
  // nothing yet, and listed it as "outstanding").
  const cashOut = (advances ?? []).filter((a) => CASH_OUT.includes(a.status) && a.unsettledAmount > 0);

  // O-2 (T-57): the advance the traveller holds is preselected. "No advance" with cash out was the leak — the
  // claim paid in full and the advance stayed outstanding — and payment now refuses it without a recorded reason.
  const firstCashOut = cashOut[0]?.id;
  useEffect(() => {
    if (firstCashOut && !form.getValues('travelAdvanceId')) form.setValue('travelAdvanceId', firstCashOut);
  }, [firstCashOut, form]);

  if (!requestId) {
    return (
      <div className="p-6">
        <EmptyState
          title="No trip chosen"
          description="Open a travel request and file the claim from its Finance tab."
        />
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-10">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError && !request) {
    return (
      <div className="p-6">
        <TravelQueryError error={error} what="the travel request" />
      </div>
    );
  }

  if (!request) {
    return (
      <div className="p-6">
        <EmptyState title="Not found" description="That travel request does not exist." />
      </div>
    );
  }

  if (!access.canWrite) {
    return (
      <div className="p-6">
        <EmptyState title="Not available" description="Filing an expense claim needs travel desk access." />
      </div>
    );
  }

  if (!TAKES_CLAIMS.includes(request.status)) {
    return (
      <div className="p-6">
        <EmptyState
          title="This trip takes no claims yet"
          description={`An expense claim is filed once the trip is approved; ${request.requestNumber} is ${humanize(request.status).toLowerCase()}.`}
        />
      </div>
    );
  }

  const advanceOptions = cashOut.map((a) => ({
    value: a.id,
    label: `${a.advanceNumber} — ${fmtMoney(a.unsettledAmount, a.currencyCode)} still with the traveller`,
  }));

  const onSubmit = async (values: z.input<typeof schema>) => {
    const v = schema.parse(values);
    setSaving(true);
    try {
      const claim = await travelFinanceService.createClaim({
        staffTravelRequestId: requestId,
        claimType: v.claimType as TravelClaimType,
        travelAdvanceId: v.travelAdvanceId || null,
        lines: [],
      });
      toast({ title: 'Claim created', description: 'Add the expenses, then submit it.' });
      router.push(`/hr/travel/claims/${claim.id}`);
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Could not create the claim',
        description: (error as Error)?.message ?? 'Please try again.',
      });
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="File an expense claim"
        description={`${request.employeeName} · ${request.requestNumber} · ${request.originCity} → ${request.destinationCity}`}
        backHref={`/hr/travel/${requestId}`}
      />

      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">The claim</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <SelectField
              form={form} name="claimType" label="Type" required options={options(CLAIM_TYPES)}
            />

            <SelectField
              form={form}
              name="travelAdvanceId"
              label="Settle against an advance"
              options={advanceOptions}
              allowEmpty
              emptyLabel={advanceOptions.length ? 'No advance' : 'No advance cash with the traveller on this trip'}
            />
            <p className="text-xs text-muted-foreground">
              Linking an advance recovers it when the claim is paid — the traveller is paid only the
              balance. A claim that leaves out advance cash the traveller holds on this trip cannot be
              paid in full unless the officer paying it records why.
            </p>
          </CardContent>
        </Card>

        <p className="text-sm text-muted-foreground">
          The claim is created empty and kept in the organisation&apos;s base currency. Add the
          expenses on the next screen — each in the currency it was spent in, converted at
          Finance&apos;s rate for its date — then submit it.
        </p>

        <div className="flex justify-end gap-2">
          <Button
            type="button"
            variant="outline"
            onClick={() => router.push(`/hr/travel/${requestId}`)}
          >
            Cancel
          </Button>
          <Button type="submit" disabled={saving}>
            {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Create the claim
          </Button>
        </div>
      </form>
    </div>
  );
}

export default function NewTravelClaimPage() {
  // `useSearchParams` needs a Suspense boundary under the App Router.
  return (
    <Suspense
      fallback={
        <div className="flex items-center justify-center p-10">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      }
    >
      <NewTravelClaimForm />
    </Suspense>
  );
}
