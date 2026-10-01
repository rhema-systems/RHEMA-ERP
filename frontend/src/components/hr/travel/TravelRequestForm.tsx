'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Globe, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { useToast } from '@/hooks/use-toast';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  DateField,
  FieldRow,
  NumberField,
  SelectField,
  SwitchField,
  TextField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { CurrencyField } from '@/components/hr/common/CurrencyPicker';
import { OrganizationUnitPickerField } from '@/components/hr/common/OrganizationUnitPickerField';
import { countryService } from '@/services/hr/country.service';
import { travelService } from '@/services/hr/travel.service';
import type { StaffTravelRequest } from '@/types/hr/travel';
import { TravelQueryError } from './TravelQueryError';
import {
  TRAVEL_INITIATOR_ROLES_A_PERSON_CHOOSES,
  TRAVEL_INITIATOR_ROLE_LABELS,
  TRAVEL_PRIORITY_LABELS,
  TRAVEL_PURPOSE_LABELS,
  TRAVEL_RISK_LEVEL_LABELS,
  TRAVEL_TYPE_LABELS,
  enumOptions,
  enumValues,
} from './travel-enums';
import {
  buildTravelRequestCreate,
  buildTravelRequestFields,
  buildTravelRequestUpdate,
  isInternationalTrip,
} from './travel-request-payload';

/**
 * ⚠ Every member of every enum, from `travel-enums.ts`, which a test holds to the C# enums. These
 * lists used to be typed by hand: they offered `Negotiation` and `Extreme`, which the API refuses
 * with a 400, and hid `Emergency` travel, five purposes and the `Critical` and `Prohibited` risk
 * levels, so a request carrying one of those could not be saved from this form (travel final
 * closure, lane 0 — finding A8). The schema accepts every member, including the initiator role
 * `System`; only the *Raised as* picker leaves that one out.
 */
const schema = z
  .object({
    employeeId: z.string().optional(),
    initiatedByRole: z.enum(enumValues(TRAVEL_INITIATOR_ROLE_LABELS)).optional(),
    travelType: z.enum(enumValues(TRAVEL_TYPE_LABELS)),
    travelPurpose: z.enum(enumValues(TRAVEL_PURPOSE_LABELS)),
    purposeDescription: z.string().max(2000).optional(),
    priority: z.enum(enumValues(TRAVEL_PRIORITY_LABELS)),
    riskLevel: z.enum(enumValues(TRAVEL_RISK_LEVEL_LABELS)),
    originCountryId: z.string().min(1, 'Select a country'),
    originCity: z.string().min(1, 'Required').max(100),
    destinationCountryId: z.string().min(1, 'Select a country'),
    destinationCity: z.string().min(1, 'Required').max(100),
    travelStartDate: z.string().min(1, 'Required'),
    travelEndDate: z.string().min(1, 'Required'),
    estimatedTotalCost: z.coerce.number().min(0, 'Cannot be negative'),
    currencyCode: z.string().min(1, 'Select a currency'),
    organizationUnitId: z.string().optional(),
    requiresVisa: z.boolean(),
    requiresHealthClearance: z.boolean(),
    amendmentReason: z.string().max(1000).optional(),
  })
  .refine((v) => !v.travelEndDate || !v.travelStartDate || v.travelEndDate >= v.travelStartDate, {
    message: 'The return date cannot be before departure',
    path: ['travelEndDate'],
  });

type FormValues = z.input<typeof schema>;

const toDateInput = (v?: string | null) => (v ? v.slice(0, 10) : '');

/**
 * The one travel-request form, used by all four routes: the desk's create and edit, and the
 * employee's own.
 *
 * <b>The surfaces differ in what they are allowed to say, not in layout.</b> On `self` the traveller
 * and the initiator are the token — the fields are absent, not disabled, because the self-service
 * endpoint overwrites them server-side and a disabled control would imply the value was sent.
 *
 * ⚠ `currencyCode` is bound to `api/hr/currencies`, Finance's master read through HR's own door.
 * The form used to read `api/finance/currencies`, which needs a Finance permission: it answered 403
 * to HR officers and to travellers alike, so the dropdown was empty and no request could be saved
 * by the people who raise them (travel final closure, lane 0 — finding O-19). The server refuses a
 * code Finance does not hold, so this stays a list, never a free-text box.
 *
 * `isInternational` is derived from the two countries rather than asked for. It was a free boolean
 * on the DTO, which let a request claim a domestic trip between two countries; there is no case
 * where the answer is not already on the form. (The server still trusts the payload until lane 1.)
 *
 * ⚠ An edit REPLACES the record. `buildTravelRequestUpdate` sends back the three fields this form
 * does not show — see `travel-request-payload.ts`.
 */
export function TravelRequestForm({
  surface,
  existing,
}: {
  surface: 'desk' | 'self';
  existing?: StaffTravelRequest;
}) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [saving, setSaving] = useState(false);

  const isEdit = !!existing;
  const isDesk = surface === 'desk';
  const listHref = isDesk ? '/hr/travel' : '/me/travel';

  const { data: countries, isError: countriesFailed, error: countriesError } = useQuery({
    queryKey: ['countries', 'active'],
    queryFn: () => countryService.getActive(),
  });

  // Only the desk chooses a unit. The traveller's own form sends none, and until lane 1 the server
  // does not fill it in from the traveller either — so a self-raised trip carries no unit, and a
  // unit-scoped travel policy never applies to it (findings A8 and O-5).

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      employeeId: existing?.employeeId ?? '',
      initiatedByRole: existing?.initiatedByRole ?? 'TravelDesk',
      travelType: existing?.travelType ?? 'Domestic',
      travelPurpose: existing?.travelPurpose ?? 'ClientMeeting',
      purposeDescription: existing?.purposeDescription ?? '',
      priority: existing?.priority ?? 'Routine',
      riskLevel: existing?.riskLevel ?? 'Low',
      originCountryId: existing?.originCountryId ?? '',
      originCity: existing?.originCity ?? '',
      destinationCountryId: existing?.destinationCountryId ?? '',
      destinationCity: existing?.destinationCity ?? '',
      travelStartDate: toDateInput(existing?.travelStartDate),
      travelEndDate: toDateInput(existing?.travelEndDate),
      estimatedTotalCost: existing?.estimatedTotalCost ?? 0,
      currencyCode: existing?.currencyCode ?? '',
      organizationUnitId: existing?.organizationUnitId ?? '',
      requiresVisa: existing?.requiresVisa ?? false,
      requiresHealthClearance: existing?.requiresHealthClearance ?? false,
      amendmentReason: '',
    },
  });

  const originCountryId = form.watch('originCountryId');
  const destinationCountryId = form.watch('destinationCountryId');
  const isInternational = isInternationalTrip(originCountryId, destinationCountryId);

  // A trip that crosses a border almost always needs a visa decision made deliberately, so the
  // switch is turned ON once and then left alone — re-forcing it would fight the user.
  useEffect(() => {
    if (isInternational && !form.getValues('requiresVisa')) {
      form.setValue('requiresVisa', true);
    }
  }, [isInternational, form]);

  const countryOptions = useMemo(
    () => (countries ?? []).map((c) => ({ value: c.id, label: c.name })),
    [countries],
  );

  const onSubmit = async (values: FormValues) => {
    const parsed = schema.parse(values);

    // Only the desk names a traveller, so the requirement cannot live in the shared schema.
    const travellerId = parsed.employeeId;
    if (isDesk && !isEdit && !travellerId) {
      form.setError('employeeId', { message: 'Select who is travelling' });
      return;
    }

    setSaving(true);
    try {
      let saved: StaffTravelRequest;
      if (isEdit && existing) {
        const payload = buildTravelRequestUpdate(parsed, existing);
        saved = isDesk
          ? await travelService.update(payload)
          : await travelService.updateMine(payload);
      } else if (isDesk) {
        saved = await travelService.create(buildTravelRequestCreate(parsed, travellerId ?? ''));
      } else {
        saved = await travelService.createMine(buildTravelRequestFields(parsed, false));
      }

      await queryClient.invalidateQueries({ queryKey: ['travel-requests'] });
      await queryClient.invalidateQueries({ queryKey: ['my-travel-requests'] });
      await queryClient.invalidateQueries({ queryKey: ['travel-request', saved.id] });
      toast({ title: isEdit ? 'Travel request updated' : 'Travel request created' });
      router.push(isDesk ? `/hr/travel/${saved.id}` : `/me/travel/${saved.id}`);
    } catch (error) {
      toast({
        variant: 'destructive',
        title: isEdit ? 'Could not save the request' : 'Could not create the request',
        description: (error as Error)?.message ?? 'Please try again.',
      });
    } finally {
      setSaving(false);
    }
  };

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
      {/* Both country pickers are required, so an unread list blocks the save — say why. */}
      {countriesFailed && !countries && <TravelQueryError error={countriesError} what="the country list" />}

      {isDesk && !isEdit && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Who is travelling</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {/* Create-only: the update payload carries no EmployeeId, so a trip cannot be
                reassigned to a different traveller — cancel and raise a new one. */}
            <EmployeePickerField form={form} name="employeeId" label="Traveller" required />
            <SelectField
              form={form}
              name="initiatedByRole"
              label="Raised as"
              options={enumOptions(TRAVEL_INITIATOR_ROLE_LABELS, TRAVEL_INITIATOR_ROLES_A_PERSON_CHOOSES)}
            />
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">The trip</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <SelectField
              form={form}
              name="travelType"
              label="Travel type"
              required
              options={enumOptions(TRAVEL_TYPE_LABELS)}
            />
            <SelectField
              form={form}
              name="travelPurpose"
              label="Purpose"
              required
              options={enumOptions(TRAVEL_PURPOSE_LABELS)}
            />
          </FieldRow>

          <TextareaField
            form={form}
            name="purposeDescription"
            label="Justification"
            placeholder="Why this trip is necessary, and what it should achieve."
          />

          <FieldRow>
            <SelectField
              form={form}
              name="originCountryId"
              label="From (country)"
              required
              options={countryOptions}
            />
            <TextField form={form} name="originCity" label="From (city)" required />
          </FieldRow>

          <FieldRow>
            <SelectField
              form={form}
              name="destinationCountryId"
              label="To (country)"
              required
              options={countryOptions}
            />
            <TextField form={form} name="destinationCity" label="To (city)" required />
          </FieldRow>

          {isInternational && (
            <p className="flex items-center gap-2 rounded-md border border-dashed p-3 text-sm text-muted-foreground">
              <Globe className="h-4 w-4" />
              This is an international trip — it crosses a border, so compliance documents and visa
              checks apply.
            </p>
          )}

          <FieldRow>
            <DateField form={form} name="travelStartDate" label="Departure" required />
            <DateField form={form} name="travelEndDate" label="Return" required />
          </FieldRow>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">Cost, risk and requirements</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <NumberField form={form} name="estimatedTotalCost" label="Estimated cost" required />
            <CurrencyField form={form} name="currencyCode" label="Currency" required />
          </FieldRow>

          <FieldRow>
            <SelectField
              form={form}
              name="priority"
              label="Priority"
              required
              options={enumOptions(TRAVEL_PRIORITY_LABELS)}
            />
            <SelectField
              form={form}
              name="riskLevel"
              label="Risk level"
              required
              options={enumOptions(TRAVEL_RISK_LEVEL_LABELS)}
            />
          </FieldRow>

          {isDesk && (
            <OrganizationUnitPickerField form={form} name="organizationUnitId" label="Organisation unit" allowEmpty emptyLabel="Not specified" />
          )}

          {/* ⚠ Both switches RECORD a need; neither enforces one. The visa section on the
              Compliance tab shows whatever this says, and nothing yet refuses a booking or a
              departure without a visa or a clearance (findings E4, T-24, T-25 — lanes 5 and 7). */}
          <SwitchField
            form={form}
            name="requiresVisa"
            label="Requires a visa"
            description="Records that this trip needs a visa. The application is tracked on the trip's Compliance tab."
          />
          <SwitchField
            form={form}
            name="requiresHealthClearance"
            label="Requires health clearance"
            description="Records that vaccination or fitness-to-travel evidence is needed. The travel desk confirms it; the system does not check it."
          />

          {isEdit && (
            <TextareaField
              form={form}
              name="amendmentReason"
              label="Reason for the change"
              placeholder="Kept on the record — say what changed and why."
            />
          )}
        </CardContent>
      </Card>

      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" onClick={() => router.push(listHref)}>
          Cancel
        </Button>
        <Button type="submit" disabled={saving}>
          {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
          {isEdit ? 'Save changes' : 'Create request'}
        </Button>
      </div>
    </form>
  );
}
