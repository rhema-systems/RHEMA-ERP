'use client';

import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  DateField, FieldRow, SelectField, SwitchField, TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { medicalInsuranceService, medicalFacilityService } from '@/services/hr/medical-reference.service';
import type { MedicalInsuranceProviderFacility } from '@/types/hr/medical';

const toInput = (v?: string | null) => (v ? v.slice(0, 10) : '');
const orNull = (v?: string | null) => {
  const t = (v ?? '').trim();
  return t.length > 0 ? t : null;
};

const schema = z
  .object({
    facilityId: z.string().min(1, 'Choose the facility'),
    effectiveDate: z.string().min(1, 'When does the network cover begin?'),
    expiryDate: z.string().optional(),
    isPreferredProvider: z.boolean(),
    isActive: z.boolean(),
    notes: z.string().max(1000).optional(),
  })
  .refine((v) => !v.expiryDate || v.expiryDate > v.effectiveDate, {
    message: 'Cover cannot expire before it starts',
    path: ['expiryDate'],
  });

type Form = z.infer<typeof schema>;

const empty: Form = {
  facilityId: '',
  effectiveDate: new Date().toISOString().slice(0, 10),
  expiryDate: '',
  isPreferredProvider: false,
  isActive: true,
  notes: '',
};

/**
 * The facilities inside this provider's network.
 *
 * ⚠ **The facility and the effective date are fixed at creation.** `AddMedicalInsuranceProviderFacilityDto`
 * carries both; the update DTO carries neither — only the expiry, the two flags and notes. So the
 * facility picker and the start date are hidden once the row exists, because rendering them on
 * edit would offer a change the API silently discards. Repointing means removing and re-adding.
 *
 * ⚠ **`isPreferredProvider` was in the closure ledger's "no form can set" table.** It is the field
 * that decides which in-network facility an employee is steered to first, so a network with no
 * preferred provider is a network nobody is directed through.
 *
 * ⚠ **`isActive` is on the update only.** A new row is active by construction; deactivating is how
 * you take a facility out of network without losing the history that it was once in it — which is
 * why the panel prefers it to the Admin-tier delete.
 */
export function NetworkFacilitiesPanel({
  providerId,
  canWrite,
  canDelete,
}: {
  providerId: string;
  canWrite: boolean;
  canDelete: boolean;
}) {
  const { data: facilities } = useQuery({
    queryKey: ['hr', 'medical-facilities'],
    queryFn: () => medicalFacilityService.getFacilities(),
    staleTime: 5 * 60 * 1000,
  });

  // ⚠ The summary calls it `facilityName`, not `name`.
  const facilityOptions = (facilities ?? [])
    .filter((f) => f.isActive)
    .map((f) => ({
      value: f.id,
      label: f.city ? `${f.facilityName} — ${f.city}` : f.facilityName,
    }));

  return (
    <ResourceCollectionTab<MedicalInsuranceProviderFacility, Form>
      parentId={providerId}
      title="network facilities"
      singular="network facility"
      queryKey={['hr', 'medical-providers', providerId, 'network-facilities']}
      readOnly={!canWrite}
      dialogClassName="sm:max-w-[600px]"
      dialogHint="A facility an employee may use on this provider's cover."
      emptyDescription="This provider has no facilities in network. Employees have nowhere they can be treated on it."
      list={(id) => medicalInsuranceService.getNetworkFacilities(id)}
      create={(id, v) =>
        medicalInsuranceService.addNetworkFacility(id, {
          facilityId: v.facilityId,
          effectiveDate: v.effectiveDate,
          expiryDate: orNull(v.expiryDate),
          isPreferredProvider: v.isPreferredProvider,
          notes: orNull(v.notes),
        })
      }
      update={(_id, rowId, v) =>
        medicalInsuranceService.updateNetworkFacility(rowId, {
          expiryDate: orNull(v.expiryDate),
          isPreferredProvider: v.isPreferredProvider,
          isActive: v.isActive,
          notes: orNull(v.notes),
        })
      }
      remove={canDelete ? (_id, rowId) => medicalInsuranceService.removeNetworkFacility(rowId) : undefined}
      getId={(f) => f.id}
      columns={[
        { header: 'Facility', cell: (f) => f.facilityName },
        { header: 'City', cell: (f) => <span className="text-muted-foreground">{f.facilityCity || '—'}</span> },
        { header: 'From', cell: (f) => toInput(f.effectiveDate) || '—' },
        { header: 'Until', cell: (f) => toInput(f.expiryDate) || 'Open-ended' },
        {
          header: '',
          cell: (f) => (
            <div className="flex flex-wrap gap-1">
              {f.isPreferredProvider && <Badge variant="outline">Preferred</Badge>}
              {!f.isActive && <Badge variant="secondary">Out of network</Badge>}
            </div>
          ),
        },
      ]}
      schema={schema}
      emptyForm={empty}
      toForm={(f) => ({
        facilityId: f.facilityId,
        effectiveDate: toInput(f.effectiveDate),
        expiryDate: toInput(f.expiryDate),
        isPreferredProvider: f.isPreferredProvider,
        isActive: f.isActive,
        notes: f.notes ?? '',
      })}
      renderFields={(form, editing) => (
        <>
          {/* Create-only: neither field is on the update DTO. */}
          {!editing ? (
            <>
              <SelectField
                form={form}
                name="facilityId"
                label="Facility"
                required
                placeholder="Choose from the facility register"
                options={facilityOptions}
              />
              <DateField form={form} name="effectiveDate" label="In network from" required />
            </>
          ) : (
            <p className="text-xs text-muted-foreground">
              The facility and its start date were set when it joined the network and cannot be
              changed. To point this row at a different facility, remove it and add another.
            </p>
          )}

          <FieldRow>
            <DateField form={form} name="expiryDate" label="In network until" />
            <div className="flex items-end">
              <div className="w-full">
                <SwitchField
                  form={form}
                  name="isPreferredProvider"
                  label="Preferred provider"
                  description="Employees are steered here first."
                />
              </div>
            </div>
          </FieldRow>

          {editing && (
            <SwitchField
              form={form}
              name="isActive"
              label="Currently in network"
              description="Turn off to take the facility out of network while keeping the record that it was in it."
            />
          )}

          <TextareaField form={form} name="notes" label="Notes" rows={2} />
        </>
      )}
    />
  );
}
