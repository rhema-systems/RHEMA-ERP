'use client';

import { Suspense, useState } from 'react';
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
import { FieldRow, SelectField } from '@/components/hr/employee/tabs/fields';
import { CurrencyField } from '@/components/hr/common/CurrencyPicker';
import { TravelQueryError } from '@/components/hr/travel/TravelQueryError';
import { useToast } from '@/hooks/use-toast';
import { travelService } from '@/services/hr/travel.service';
import { travelFinanceService } from '@/services/hr/travel-finance.service';
import type { TravelClaimType } from '@/types/hr/travel-finance';

const CLAIM_TYPES: TravelClaimType[] = [
  'PostTravel', 'AdvanceSettlement', 'PartialClaim', 'Amendment',
];

const humanize = (v: string) => v.replace(/([a-z])([A-Z])/g, '$1 $2');
const options = (values: readonly string[]) => values.map((v) => ({ value: v, label: humanize(v) }));

const schema = z.object({
  claimType: z.enum(CLAIM_TYPES as [string, ...string[]]),
  currencyCode: z.string().min(1, 'Select a currency'),
  travelAdvanceId: z.string().optional(),
});

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
    defaultValues: { claimType: 'PostTravel', currencyCode: '', travelAdvanceId: '' },
  });

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

  // Only an advance with something still outstanding can be settled against.
  const advanceOptions = (advances ?? [])
    .filter((a) => a.unsettledAmount > 0)
    .map((a) => ({
      value: a.id,
      label: `${a.advanceNumber} — ${a.unsettledAmount} ${a.currencyCode} outstanding`,
    }));

  const onSubmit = async (values: z.input<typeof schema>) => {
    const v = schema.parse(values);
    setSaving(true);
    try {
      const claim = await travelFinanceService.createClaim({
        staffTravelRequestId: requestId,
        employeeId: request.employeeId,
        claimType: v.claimType as TravelClaimType,
        currencyCode: v.currencyCode,
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
            <FieldRow>
              <SelectField
                form={form} name="claimType" label="Type" required options={options(CLAIM_TYPES)}
              />
              <CurrencyField form={form} name="currencyCode" label="Claim currency" required />
            </FieldRow>

            <SelectField
              form={form}
              name="travelAdvanceId"
              label="Settle against an advance"
              options={advanceOptions}
              allowEmpty
              emptyLabel={advanceOptions.length ? 'No advance' : 'No outstanding advance on this trip'}
            />
            <p className="text-xs text-muted-foreground">
              Linking an advance recovers it automatically when the claim is paid — the traveller
              is paid only the balance. Without the link, an advance already given stays
              outstanding and the claim pays out in full.
            </p>
          </CardContent>
        </Card>

        <p className="text-sm text-muted-foreground">
          The claim is created empty. Add the individual expenses on the next screen, then submit
          it — each one is converted and checked on its own.
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
