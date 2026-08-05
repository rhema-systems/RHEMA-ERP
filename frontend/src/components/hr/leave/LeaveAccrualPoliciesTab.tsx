'use client';

import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { leaveTypeService } from '@/services/hr/leave-type.service';
import {
  ACCRUAL_FREQUENCY_OPTIONS,
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
});

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  frequency: 'Monthly',
  mode: 'AccrueIncrementally',
  accrualRate: 0,
  minServiceMonths: '',
  proRateOnJoin: true,
  proRateOnExit: true,
};

const toPayload = (leaveTypeId: string, v: FormValues) => ({
  leaveTypeId,
  frequency: v.frequency,
  mode: v.mode,
  accrualRate: v.accrualRate,
  minServiceMonths: v.minServiceMonths ? Number(v.minServiceMonths) : null,
  proRateOnJoin: v.proRateOnJoin,
  proRateOnExit: v.proRateOnExit,
});

const label = (opts: { value: string; label: string }[], v: string) =>
  opts.find((o) => o.value === v)?.label ?? v;

/** How entitlement builds up over time for this leave type. */
export function LeaveAccrualPoliciesTab({ leaveTypeId }: { leaveTypeId: string }) {
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
      remove={(_id, policyId) => leaveTypeService.removeAccrualPolicy(policyId)}
      columns={[
        { header: 'Frequency', cell: (p) => label(ACCRUAL_FREQUENCY_OPTIONS, p.frequency) },
        { header: 'Mode', cell: (p) => label(ACCRUAL_MODE_OPTIONS, p.mode) },
        { header: 'Rate', cell: (p) => p.accrualRate },
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
            p.isActive ? <Badge variant="secondary">Active</Badge> : <Badge variant="outline">Inactive</Badge>,
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
      })}
      renderFields={(form) => (
        <>
          <FieldRow>
            <SelectField
              form={form}
              name="frequency"
              label="Frequency"
              required
              options={ACCRUAL_FREQUENCY_OPTIONS}
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
              placeholder="days per period"
              required
            />
            <NumberField form={form} name="minServiceMonths" label="Min service (months)" />
          </FieldRow>
          <FieldRow>
            <SwitchField
              form={form}
              name="proRateOnJoin"
              label="Pro-rate on join"
              description="Part-period entitlement in the year of joining."
            />
            <SwitchField
              form={form}
              name="proRateOnExit"
              label="Pro-rate on exit"
              description="Part-period entitlement in the year of leaving."
            />
          </FieldRow>
        </>
      )}
    />
  );
}
