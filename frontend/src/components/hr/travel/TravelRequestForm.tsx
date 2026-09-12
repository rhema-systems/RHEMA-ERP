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
import { countryService } from '@/services/hr/country.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import { travelService } from '@/services/hr/travel.service';
import type { StaffTravelRequest } from '@/types/hr/travel';

/**
 * ⚠ All ten, not the four this list used to carry. `StaffTravelType` in C# has ten members and
 * the TypeScript union had four, so the form offered four kinds of trip, the zod enum refused the
 * other six, and a request created elsewhere with one of them could not be edited here. The union
 * was written from examples; this is written from the enum.
 */
const TRAVEL_TYPES = [
  'Domestic',
  'International',
  'CrossBorder',
  'Regional',
  'OverseasAssignment',
  'FieldVisit',
  'Training',
  'Conference',
  'ClientVisit',
  'GovernmentDuty',
] as const;
const PURPOSES = [
  'BusinessDevelopment',
  'ClientMeeting',
  'Conference',
  'Training',
  'SiteVisit',
  'Audit',
  'Negotiation',
  'Other',
] as const;
const PRIORITIES = ['Routine', 'Urgent', 'Emergency'] as const;
const RISK_LEVELS = ['Low', 'Medium', 'High', 'Extreme'] as const;
const INITIATOR_ROLES = ['Employee', 'Manager', 'HrAdmin', 'TravelDesk'] as const;

const label = (v: string) => v.replace(/([a-z])([A-Z])/g, '$1 $2');
const options = (values: readonly string[]) =>
  values.map((v) => ({ value: v, label: label(v) }));

const schema = z
  .object({
    employeeId: z.string().optional(),
    initiatedByRole: z.enum(INITIATOR_ROLES).optional(),
    travelType: z.enum(TRAVEL_TYPES),
    travelPurpose: z.enum(PURPOSES),
    purposeDescription: z.string().max(2000).optional(),
    priority: z.enum(PRIORITIES),
    riskLevel: z.enum(RISK_LEVELS),
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
 * ⚠ `currencyCode` is bound to Finance's currency list. Travel keeps no currency table of its own,
 * and the server refuses a code Finance does not hold, so a free-text box here would produce a 422
 * the form could not attribute to a field.
 *
 * `isInternational` is derived from the two countries rather than asked for. It was a free boolean
 * on the DTO, which let a request claim a domestic trip between two countries; there is no case
 * where the answer is not already on the form.
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

  const { data: countries } = useQuery({
    queryKey: ['countries', 'active'],
    queryFn: () => countryService.getActive(),
  });

  const { data: currencies } = useQuery({
    queryKey: ['finance', 'currencies', 'active'],
    queryFn: () => financeDataService.getCurrencies({ isActive: true }),
  });

  // Only the desk chooses a unit — an employee's own trip is charged to their own unit server-side.
  const { data: units } = useQuery({
    queryKey: ['organization-units', 'summary'],
    queryFn: () => organizationUnitService.getSummary(),
    enabled: isDesk,
  });

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
  const isInternational = !!originCountryId && !!destinationCountryId
    && originCountryId !== destinationCountryId;

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
  const currencyOptions = useMemo(
    () =>
      (currencies ?? []).map((c) => ({
        value: c.currencyCode,
        label: `${c.currencyCode} — ${c.currencyName}`,
      })),
    [currencies],
  );
  const unitOptions = useMemo(
    () => (units ?? []).map((u) => ({ value: u.id, label: u.name })),
    [units],
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
      const shared = {
        travelType: parsed.travelType,
        travelPurpose: parsed.travelPurpose,
        purposeDescription: parsed.purposeDescription || undefined,
        organizationUnitId: parsed.organizationUnitId || null,
        priority: parsed.priority,
        destinationCountryId: parsed.destinationCountryId,
        destinationCity: parsed.destinationCity,
        originCountryId: parsed.originCountryId,
        originCity: parsed.originCity,
        travelStartDate: parsed.travelStartDate,
        travelEndDate: parsed.travelEndDate,
        estimatedTotalCost: parsed.estimatedTotalCost,
        currencyCode: parsed.currencyCode,
        isInternational,
        requiresVisa: parsed.requiresVisa,
        requiresHealthClearance: parsed.requiresHealthClearance,
        riskLevel: parsed.riskLevel,
        amendmentReason: isEdit ? parsed.amendmentReason || null : null,
      };

      let saved: StaffTravelRequest;
      if (isEdit && existing) {
        saved = isDesk
          ? await travelService.update({ ...shared, id: existing.id })
          : await travelService.updateMine({ ...shared, id: existing.id });
      } else if (isDesk) {
        saved = await travelService.create({
          ...shared,
          employeeId: travellerId ?? '',
          // No initiator id: the desk raises travel for other people, and who raised it is stamped
          // from the token. Only the ROLE it was raised under is a real input.
          initiatedByRole: parsed.initiatedByRole ?? 'TravelDesk',
        });
      } else {
        saved = await travelService.createMine(shared);
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
              options={options(INITIATOR_ROLES)}
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
              options={options(TRAVEL_TYPES)}
            />
            <SelectField
              form={form}
              name="travelPurpose"
              label="Purpose"
              required
              options={options(PURPOSES)}
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
            <SelectField
              form={form}
              name="currencyCode"
              label="Currency"
              required
              options={currencyOptions}
              placeholder={currencyOptions.length ? 'Select…' : 'No currencies configured'}
            />
          </FieldRow>

          <FieldRow>
            <SelectField
              form={form}
              name="priority"
              label="Priority"
              required
              options={options(PRIORITIES)}
            />
            <SelectField
              form={form}
              name="riskLevel"
              label="Risk level"
              required
              options={options(RISK_LEVELS)}
            />
          </FieldRow>

          {isDesk && (
            <SelectField
              form={form}
              name="organizationUnitId"
              label="Organisation unit"
              options={unitOptions}
              allowEmpty
              emptyLabel="Not specified"
            />
          )}

          <SwitchField
            form={form}
            name="requiresVisa"
            label="Requires a visa"
            description="Turns on the visa tracking for this trip."
          />
          <SwitchField
            form={form}
            name="requiresHealthClearance"
            label="Requires health clearance"
            description="Vaccination or fitness-to-travel evidence must be recorded before departure."
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
