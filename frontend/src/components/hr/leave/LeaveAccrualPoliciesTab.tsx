'use client';

import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { useLeavePermissions } from '@/components/hr/leave/use-leave-permissions';
import { leaveTypeService } from '@/services/hr/leave-type.service';
import {
  ACCRUAL_FREQUENCY_OPTIONS,
  ACCRUAL_FREQUENCY_NOT_OFFERED,
  ACCRUAL_FREQUENCY_DISPLAY,
  ACCRUAL_MODE_OPTIONS,
  type LeaveAccrualPolicy,
} from '@/types/hr/leave';
import {
  FieldRow,
  NumberField,
  SelectField,
  SwitchField,
} from '@/components/hr/employee/tabs/fields';

const schema = z.object({
  frequency: z.enum(['None', 'Monthly', 'Annual', 'PerPayPeriod', 'Quarterly', 'SemiAnnual']),
  mode: z.enum(['AccrueIncrementally', 'FullGrantOnEligibility']),
  accrualRate: z.coerce.number().min(0, 'Cannot be negative'),
  minServiceMonths: z.string().optional().or(z.literal('')),
  proRateOnJoin: z.boolean(),
  proRateOnExit: z.boolean(),
  isActive: z.boolean(),
});

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  frequency: 'Monthly',
  mode: 'AccrueIncrementally',
  accrualRate: 0,
  minServiceMonths: '',
  proRateOnJoin: true,
  proRateOnExit: true,
  isActive: true,
};

const toPayload = (leaveTypeId: string, v: FormValues) => ({
  leaveTypeId,
  frequency: v.frequency,
  mode: v.mode,
  accrualRate: v.accrualRate,
  minServiceMonths: v.minServiceMonths ? Number(v.minServiceMonths) : null,
  proRateOnJoin: v.proRateOnJoin,
  proRateOnExit: v.proRateOnExit,
  isActive: v.isActive,
});

const label = (opts: { value: string; label: string }[], v: string) =>
  opts.find((o) => o.value === v)?.label ?? v;

/** How entitlement builds up over time for this leave type. */
export function LeaveAccrualPoliciesTab({ leaveTypeId }: { leaveTypeId: string }) {
  const { canAdminister } = useLeavePermissions();

  return (
    <ResourceCollectionTab<LeaveAccrualPolicy, FormValues>
      parentId={leaveTypeId}
      title="accrual policies"
      singular="accrual policy"
      queryKey={['hr', 'leave-types', leaveTypeId, 'accrual-policies']}
      invalidateKeys={[['hr', 'leave-types', leaveTypeId, 'detail']]}
      dialogHint="Define how entitlement accrues for this leave type."
      emptyDescription="Without a policy, entitlement is granted from the allocation alone."
      getId={(p) => p.id}
      list={leaveTypeService.getAccrualPolicies.bind(leaveTypeService)}
      create={(id, v) => leaveTypeService.createAccrualPolicy(toPayload(id, v))}
      update={(id, policyId, v) =>
        leaveTypeService.updateAccrualPolicy(policyId, toPayload(id, v))
      }
      allowRemove={canAdminister}
      remove={(_id, policyId) => leaveTypeService.removeAccrualPolicy(policyId)}
      columns={[
        { header: 'Frequency', cell: (p) => label(ACCRUAL_FREQUENCY_DISPLAY, p.frequency) },
        { header: 'Mode', cell: (p) => label(ACCRUAL_MODE_OPTIONS, p.mode) },
        {
          // ⚠ A rate of 0 does NOT mean "accrues nothing" — it means the engine derives the rate
          // from each employee's own entitlement, so a Junior on 15 days and a Manager on 30
          // accrue toward their own figures off one policy. Shown in words, because the digit 0
          // says the opposite of what it does (entitlement plan B4).
          header: 'Rate',
          cell: (p) =>
            Number(p.accrualRate) > 0 ? (
              p.accrualRate
            ) : (
              <span className="text-muted-foreground">derived from entitlement</span>
            ),
        },
        { header: 'Min service (months)', cell: (p) => p.minServiceMonths ?? '—' },
        {
          header: 'Pro-rate',
          cell: (p) => {
            const parts = [p.proRateOnJoin && 'join', p.proRateOnExit && 'exit'].filter(Boolean);
            return parts.length ? parts.join(' & ') : '—';
          },
        },
        {
          header: 'Status',
          cell: (p) =>
            !p.isActive ? (
              <Badge variant="outline">Inactive</Badge>
            ) : p.frequency === 'None' ? (
              // L-92: in force, it accrues nothing and holds the type's one in-force place.
              <Badge variant="destructive">In force — accrues nothing</Badge>
            ) : (
              <Badge variant="secondary">Active</Badge>
            ),
        },
      ]}
      schema={schema}
      emptyForm={empty}
      toForm={(p) => ({
        frequency: p.frequency,
        mode: p.mode,
        accrualRate: p.accrualRate,
        minServiceMonths: p.minServiceMonths != null ? String(p.minServiceMonths) : '',
        proRateOnJoin: p.proRateOnJoin,
        proRateOnExit: p.proRateOnExit,
        isActive: p.isActive,
      })}
      renderFields={(form) => (
        <>
          <FieldRow>
            {/*
              ⚠ `PerPayPeriod` (retired, decision D-6) and `None` (refused, L-92) are not offered —
              but a policy that already carries one must stay editable, or dropping the option
              would strand it: the select would render blank and every save would be refused. So
              the value is added back for exactly the row that has it, and for no other.
            */}
            <SelectField
              form={form}
              name="frequency"
              label="Frequency"
              required
              options={[
                ...ACCRUAL_FREQUENCY_OPTIONS,
                ...ACCRUAL_FREQUENCY_NOT_OFFERED.filter((o) => o.value === form.watch('frequency')),
              ].filter(
                // Round 5, lane N2: incremental Annual credits the whole year on its last day, so
                // it is not offered — kept only for the row already set to it, like PerPayPeriod.
                (o) =>
                  o.value !== 'Annual' ||
                  form.watch('mode') !== 'AccrueIncrementally' ||
                  form.watch('frequency') === 'Annual',
              )}
            />
            <SelectField
              form={form}
              name="mode"
              label="Mode"
              required
              options={ACCRUAL_MODE_OPTIONS}
            />
          </FieldRow>
          <FieldRow>
            <NumberField
              form={form}
              name="accrualRate"
              label="Accrual rate"
              step="0.01"
              placeholder="0 = derive from entitlement"
              required
            />
            <NumberField form={form} name="minServiceMonths" label="Min service (months)" />
          </FieldRow>

          {/*
            ⚠ Entitlement plan B4. This is the single most useful thing on the tab and the digit 0
            says the opposite of it, so it is spelled out rather than left to be discovered.
          */}
          {form.watch('frequency') === 'None' ? (
            <p className="text-sm text-muted-foreground">
              <strong>A frequency of None accrues nothing</strong>, and while this policy is in
              force no other can be. Switch it off or remove it: a leave type with no policy grants
              its entitlement in full. It can no longer be chosen.
            </p>
          ) : Number(form.watch('accrualRate')) > 0 ? (
            <p className="text-sm text-muted-foreground">
              Every employee on this leave type accrues{' '}
              <strong>{form.watch('accrualRate')}</strong> day(s) per period, whatever they are
              entitled to. ⚠ Somebody entitled to fewer days than that adds up to will reach their
              cap early; somebody entitled to more will <strong>never reach it</strong>.{' '}
              <strong>Set the rate to 0</strong> to derive it from each person&apos;s own
              entitlement instead.
            </p>
          ) : (
            <p className="text-sm text-muted-foreground">
              <strong>The rate is derived from each employee&apos;s own entitlement</strong> — their
              annual figure divided by the periods in a year. A junior on 15 days accrues 1.25 a
              month; a manager on 30 accrues 2.5, from this one policy. That is how an accrual rate
              varies by staff level, and it is what a rate of 0 means here — not that nothing
              accrues.
            </p>
          )}
          <FieldRow>
            <SwitchField
              form={form}
              name="proRateOnJoin"
              label="Pro-rate on join"
              description="On: a joiner accrues only from the date they qualified, so a mid-year start earns part of the year. Off: once they qualify at all, they accrue on the company's leave year like everybody else."
            />
            <SwitchField
              form={form}
              name="proRateOnExit"
              label="Pro-rate on exit"
              description="On: a leaver stops accruing on their last day, and for annual leave their final settlement counts only the days built up by then. Off: they accrue to the end of the leave year, and the settlement counts the whole year. Incremental accrual only — a full grant is not scaled down."
            />
          </FieldRow>
          {/* Round 5, lane N1: the switch existed on the policy with nothing to set it. */}
          <SwitchField
            form={form}
            name="isActive"
            label="In force"
            description="Off: the policy is kept, nothing accrues under it, and another can be switched on. A leave type can have one policy in force."
          />
        </>
      )}
    />
  );
}
