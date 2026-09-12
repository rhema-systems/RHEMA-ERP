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
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import { travelComplianceService } from '@/services/hr/travel-compliance.service';
import { staffLevelService } from '@/services/hr/staff-level.service';
import type { StaffTravelPolicy } from '@/types/hr/travel-compliance';

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
  advanceBookingDaysFlight: z.coerce.number().int().min(0).max(365),
  advanceBookingDaysHotel: z.coerce.number().int().min(0).max(365),
  requiresCheapestFare: z.boolean(),
  preferredVendorMandatory: z.boolean(),
  maxSingleTripBudget: z.coerce.number().min(0),
  maxAnnualTravelBudget: z.coerce.number().min(0),
  receiptRequiredAbove: z.coerce.number().min(0),
  expenseSubmissionDays: z.coerce.number().int().min(0).max(365),
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
 * ⚠ **Three fields are the server's and are absent here on purpose.** `versionNumber` is set at
 * creation and never edited; `isCurrentVersion` and `approvedById`/`approvedAt` are set by
 * approving, because a policy becomes current *by being approved* and "approved but not in force"
 * is a state nobody asked for. The API ignores all three on the way in.
 *
 * ⚠ **An approved policy cannot be edited, and this form is not shown for one.** Changing what
 * everyone may spend without anyone approving the change is precisely what approval exists to
 * prevent; the answer is a new version. The API answers 400 either way.
 *
 * ⚠ **The hotel caps carry no currency.** `StaffTravelPolicy` stores none alongside them, so they
 * are read in the tenant's own currency and a booking in another one is compared after
 * conversion. Labelled as "per night" rather than with a currency symbol that would be a guess.
 */
export function TravelPolicyForm({ policy }: { policy?: StaffTravelPolicy }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const isEdit = !!policy;

  const { data: units } = useQuery({
    queryKey: ['organization-units'],
    queryFn: () => organizationUnitService.getAll(),
    staleTime: 5 * 60 * 1000,
  });

  /**
   * ⚠ **`AppliesToLevelFromId` points at `StaffLevel`, not a salary level.** The field says
   * "level" and the obvious reading is the pay structure; the constraint targets the staff-level
   * band. A salary-level id passes TypeScript, fails the foreign key and 500s naming nothing —
   * the shape that broke every equipment-tool save in area 17.
   */
  const { data: levels } = useQuery({
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
      advanceBookingDaysFlight: policy?.advanceBookingDaysFlight ?? 14,
      advanceBookingDaysHotel: policy?.advanceBookingDaysHotel ?? 7,
      requiresCheapestFare: policy?.requiresCheapestFare ?? false,
      preferredVendorMandatory: policy?.preferredVendorMandatory ?? false,
      maxSingleTripBudget: policy?.maxSingleTripBudget ?? 0,
      maxAnnualTravelBudget: policy?.maxAnnualTravelBudget ?? 0,
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
      router.push(`/administration/hr/travel/policies/${saved.id}`);
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: isEdit ? 'Could not save the policy' : 'Could not draft the policy',
        description: e?.response?.data?.message ?? e?.response?.data ?? e?.message,
      }),
  });

  const levelOptions = (levels ?? []).map((l) => ({ value: l.id, label: `${l.name} (${l.code})` }));
  const unitOptions = (units ?? []).map((u) => ({ value: u.id, label: u.name }));

  return (
    <form onSubmit={form.handleSubmit((v) => save.mutate(v as Parsed))} className="space-y-6">
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
          <SelectField
            form={form}
            name="appliesToOrganizationUnitId"
            label="Organisation unit"
            options={unitOptions}
            allowEmpty
            emptyLabel="The whole organisation"
          />
          <p className="text-xs text-muted-foreground">
            Scope is what approval supersedes on: approving this policy stands down whichever other
            policy covers the same unit and level band, so exactly one is ever in force for a
            traveller.
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
            cap is refused unless a travel administrator authorises the breach.
          </p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Budgets and expenses</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <NumberField form={form} name="maxSingleTripBudget" label="Max per trip" />
            <NumberField form={form} name="maxAnnualTravelBudget" label="Max per year" />
          </FieldRow>
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
            name="requiresCheapestFare"
            label="Cheapest fare required"
            description="Travellers must take the lowest available fare that meets the itinerary."
          />
          <SwitchField
            form={form}
            name="preferredVendorMandatory"
            label="Preferred vendors only"
            description="Bookings must go through a vendor on the approved list."
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
