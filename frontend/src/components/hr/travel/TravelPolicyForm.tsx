'use client';

import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, ShieldCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  DateField,
  FieldRow,
  NumberField,
  SelectField,
  SwitchField,
  TextField,
} from '@/components/hr/employee/tabs/fields';
import { useToast } from '@/hooks/use-toast';
import { travelComplianceService } from '@/services/hr/travel-compliance.service';
import { staffLevelService } from '@/services/hr/staff-level.service';
import type { StaffTravelPolicy } from '@/types/hr/travel-compliance';
import { OrganizationUnitPickerField } from '@/components/hr/common/OrganizationUnitPickerField';
import { CurrencyField } from '@/components/hr/common/CurrencyPicker';
import { TravelQueryError } from './TravelQueryError';

const CABIN_CLASSES = ['Economy', 'PremiumEconomy', 'Business', 'First'] as const;
const spaced = (v: string) => v.replace(/([a-z])([A-Z])/g, '$1 $2');

const schema = z.object({
  policyName: z.string().min(1, 'Give the policy a name').max(200),
  appliesToLevelFromId: z.string().optional(),
  appliesToLevelToId: z.string().optional(),
  appliesToOrganizationUnitId: z.string().optional(),
  effectiveFrom: z.string().min(1, 'A policy has to start somewhere'),
  effectiveTo: z.string().optional(),
  maxFlightClassDomestic: z.enum(CABIN_CLASSES),
  maxFlightClassInternational: z.enum(CABIN_CLASSES),
  maxHotelRateDomestic: z.coerce.number().min(0),
  maxHotelRateInternational: z.coerce.number().min(0),
  currencyCode: z.string().optional(),
  advanceBookingDaysFlight: z.coerce.number().int().min(0).max(365),
  advanceBookingDaysHotel: z.coerce.number().int().min(0).max(365),
  preferredVendorMandatory: z.boolean(),
  maxSingleTripBudget: z.coerce.number().min(0),
  receiptRequiredAbove: z.coerce.number().min(0),
  expenseSubmissionDays: z.coerce.number().int().min(0).max(365),
}).refine((v) => !v.effectiveTo || v.effectiveTo >= v.effectiveFrom, {
  message: 'The policy cannot end before it starts',
  path: ['effectiveTo'],
});

/**
 * ⚠ `z.input`, not `z.infer`. `z.coerce.number()` has an input type of `unknown` and an output
 * type of `number`, so a form typed on the OUTPUT disagrees with the resolver about what the
 * fields hold and every `form={form}` prop fails to type-check. The sibling travel form does the
 * same thing for the same reason.
 */
type Values = z.input<typeof schema>;
type Parsed = z.output<typeof schema>;

/** `<input type="date">` wants yyyy-mm-dd; the API's DateOnly serialises that way already. */
const toDateInput = (v?: string | null) => (v ? String(v).slice(0, 10) : '');

/**
 * Drafts or corrects a travel policy — the caps that actually refuse bookings.
 *
 * ⚠ **Three fields are the server's and are absent here on purpose.** `versionNumber` is the next for
 * the policy's name (lane 4, T-50); `isCurrentVersion` and `approvedById`/`approvedAt` are set by
 * approving, because a policy comes into force *by being approved*. The API no longer accepts them.
 *
 * ⚠ **An approved policy cannot be edited, and this form is not shown for one.** Changing what
 * everyone may spend without anyone approving the change is precisely what approval exists to
 * prevent; the answer is a new version. The API answers 400 either way. Whoever drafts or last
 * changes a draft cannot approve it (lane 4, C3).
 *
 * **The money limits are in the policy's currency** (lane 4, C3/T-9) — left empty, the base
 * currency. A hotel booked in another currency is converted at Finance's rate before it is compared.
 */
export function TravelPolicyForm({
  policy,
  onSaved,
}: {
  policy?: StaffTravelPolicy;
  /** Called after a save instead of navigating — for a host page that renders the form in place. */
  onSaved?: () => void;
}) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const isEdit = !!policy;


  /**
   * ⚠ **`AppliesToLevelFromId` points at `StaffLevel`, not a salary level.** The field says
   * "level" and the obvious reading is the pay structure; the constraint targets the staff-level
   * band. A salary-level id passes TypeScript, fails the foreign key and 500s naming nothing —
   * the shape that broke every equipment-tool save in area 17.
   */
  const { data: levels, isError: levelsFailed, error: levelsError } = useQuery({
    queryKey: ['staff-levels', 'active'],
    queryFn: () => staffLevelService.getActive(),
    staleTime: 5 * 60 * 1000,
  });

  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: {
      policyName: policy?.policyName ?? '',
      appliesToLevelFromId: policy?.appliesToLevelFromId ?? '',
      appliesToLevelToId: policy?.appliesToLevelToId ?? '',
      appliesToOrganizationUnitId: policy?.appliesToOrganizationUnitId ?? '',
      effectiveFrom: toDateInput(policy?.effectiveFrom) || toDateInput(new Date().toISOString()),
      effectiveTo: toDateInput(policy?.effectiveTo),
      maxFlightClassDomestic: policy?.maxFlightClassDomestic ?? 'Economy',
      maxFlightClassInternational: policy?.maxFlightClassInternational ?? 'Economy',
      maxHotelRateDomestic: policy?.maxHotelRateDomestic ?? 0,
      maxHotelRateInternational: policy?.maxHotelRateInternational ?? 0,
      currencyCode: policy?.currencyCode ?? '',
      advanceBookingDaysFlight: policy?.advanceBookingDaysFlight ?? 14,
      advanceBookingDaysHotel: policy?.advanceBookingDaysHotel ?? 7,
      preferredVendorMandatory: policy?.preferredVendorMandatory ?? false,
      maxSingleTripBudget: policy?.maxSingleTripBudget ?? 0,
      receiptRequiredAbove: policy?.receiptRequiredAbove ?? 0,
      expenseSubmissionDays: policy?.expenseSubmissionDays ?? 14,
    },
  });

  const save = useMutation({
    mutationFn: (values: Parsed) => {
      // An untouched optional id is '' from the select; the API's Guid? binder wants null.
      const payload = {
        ...values,
        appliesToLevelFromId: values.appliesToLevelFromId || null,
        appliesToLevelToId: values.appliesToLevelToId || null,
        appliesToOrganizationUnitId: values.appliesToOrganizationUnitId || null,
        effectiveTo: values.effectiveTo || null,
        currencyCode: values.currencyCode || null,
      };
      return isEdit
        ? travelComplianceService.updatePolicy({ ...payload, id: policy.id })
        : travelComplianceService.createPolicy(payload);
    },
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: ['travel-policies'] });
      toast({
        title: isEdit ? 'Policy updated' : 'Policy drafted',
        description: isEdit
          ? undefined
          : 'It caps nothing until a travel administrator approves it.',
      });
      // ⚠ The edit form is rendered INSIDE the policy's own page, so pushing to that page's URL
      // changed nothing and the form stayed open after "Policy updated" (finding F4). The host
      // closes it instead; only a new policy navigates.
      if (onSaved) onSaved();
      else router.push(`/administration/hr/travel/policies/${saved.id}`);
    },
    onError: (e: Error) =>
      toast({
        variant: 'destructive',
        title: isEdit ? 'Could not save the policy' : 'Could not draft the policy',
        description: e?.message,
      }),
  });

  const levelOptions = (levels ?? []).map((l) => ({ value: l.id, label: `${l.name} (${l.code})` }));

  return (
    <form onSubmit={form.handleSubmit((v) => save.mutate(v as Parsed))} className="space-y-6">
      {/* An empty band picker would otherwise read as "no staff levels exist". */}
      {levelsFailed && !levels && <TravelQueryError error={levelsError} what="the staff levels" />}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <ShieldCheck className="h-4 w-4" />
            Who and when
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <TextField form={form} name="policyName" label="Policy name" required />
          <FieldRow>
            <SelectField
              form={form}
              name="appliesToLevelFromId"
              label="From staff level"
              options={levelOptions}
              allowEmpty
              emptyLabel="Any level"
            />
            <SelectField
              form={form}
              name="appliesToLevelToId"
              label="To staff level"
              options={levelOptions}
              allowEmpty
              emptyLabel="Any level"
            />
          </FieldRow>
          <OrganizationUnitPickerField form={form} name="appliesToOrganizationUnitId" label="Organisation unit" allowEmpty emptyLabel="The whole organisation" />
          <p className="text-xs text-muted-foreground">
            A policy for a unit covers the units under it too; the nearest unit&apos;s policy wins.
            Approving a policy makes room for it among the others for the same unit and level band
            by date: one that started earlier stays in force until the day before this one starts,
            and one that starts on or after it is replaced.
          </p>
          <FieldRow>
            <DateField form={form} name="effectiveFrom" label="Effective from" required />
            <DateField form={form} name="effectiveTo" label="Effective to" />
          </FieldRow>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">The caps that refuse a booking</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <SelectField
              form={form}
              name="maxFlightClassDomestic"
              label="Max cabin — domestic"
              required
              options={CABIN_CLASSES.map((c) => ({ value: c, label: spaced(c) }))}
            />
            <SelectField
              form={form}
              name="maxFlightClassInternational"
              label="Max cabin — international"
              required
              options={CABIN_CLASSES.map((c) => ({ value: c, label: spaced(c) }))}
            />
          </FieldRow>
          <CurrencyField
            form={form}
            name="currencyCode"
            label="Currency of the money limits"
            allowEmpty
            emptyLabel="The base currency"
            hint="The hotel rates, the per-trip limit and the receipt threshold are read in this currency."
          />
          <FieldRow>
            <NumberField
              form={form}
              name="maxHotelRateDomestic"
              label="Max hotel rate — domestic, per night"
            />
            <NumberField
              form={form}
              name="maxHotelRateInternational"
              label="Max hotel rate — international, per night"
            />
          </FieldRow>
          <p className="text-xs text-muted-foreground">
            Whether a trip is domestic or international is the travel request&apos;s answer, derived
            from its two countries — a booking does not get a second opinion. A booking above either
            cap is refused unless a travel administrator authorises the breach; a hotel booked in
            another currency is converted at Finance&apos;s rate first.
          </p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Budgets and expenses</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {/* Finding C1 / D-1: each of these binds where it says, once the policy is approved. The
              cheapest-fare switch and the annual budget had no reader and left the form (lane 4). */}
          <p className="text-sm text-muted-foreground">
            Once the policy is approved: a trip estimated above the per-trip limit is refused at
            submission; an expense above the receipt threshold needs a receipt, and a claim is
            submitted within the days allowed after the trip ends.
          </p>
          <p className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-100">
            Not yet enforced: the days ahead a flight or hotel must be booked, and preferred vendors.
          </p>
          <NumberField form={form} name="maxSingleTripBudget" label="Max per trip (0 = no limit)" />
          <FieldRow>
            <NumberField
              form={form}
              name="receiptRequiredAbove"
              label="Receipt required above"
            />
            <NumberField
              form={form}
              name="expenseSubmissionDays"
              label="Days to submit expenses"
            />
          </FieldRow>
          <FieldRow>
            <NumberField
              form={form}
              name="advanceBookingDaysFlight"
              label="Book flights this many days ahead"
            />
            <NumberField
              form={form}
              name="advanceBookingDaysHotel"
              label="Book hotels this many days ahead"
            />
          </FieldRow>
          <SwitchField
            form={form}
            name="preferredVendorMandatory"
            label="Preferred vendors only"
            description="Records that bookings should go through an approved vendor. Not checked on any booking yet."
          />
        </CardContent>
      </Card>

      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" onClick={() => router.back()}>
          Cancel
        </Button>
        <Button type="submit" disabled={save.isPending}>
          {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
          {isEdit ? 'Save changes' : 'Save as draft'}
        </Button>
      </div>
    </form>
  );
}
