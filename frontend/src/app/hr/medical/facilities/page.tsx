'use client';

import Link from 'next/link';
import { Badge } from '@/components/ui/badge';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import { useAuth } from '@/hooks/use-auth';
import {
  FacilityFormFields,
  facilitySchema,
  facilityFormToRequest,
  emptyFacility,
  type FacilityForm,
} from '@/components/hr/medical/facility-form';
import { medicalFacilityService } from '@/services/hr/medical-reference.service';
import { HEALTH_FACILITY_TYPE_OPTIONS } from '@/types/hr/medical';
import type { HealthcareFacilitySummary } from '@/types/hr/medical';

/**
 * The register of hospitals, clinics and pharmacies the organisation deals with.
 *
 * Everything downstream in this module points at a facility — a medical exam names where it was
 * done, and an expense claim cannot be filed without one — so this register is the first thing
 * to populate. Reading it does not need medical permissions (employees file their own claims and
 * must be able to pick a facility); creating and editing does, and deleting needs admin.
 *
 * ⚠ **Create only.** Editing lives on the facility detail screen, reached by the name in the first
 * column. The rows here are `HealthcareFacilitySummary` and that payload carries ten keys — measured
 * in areas 19-23 slice 9 — none of which is `physicalAddress`, a required field. A dialog seeded
 * from a row therefore opened with it blank and made the user retype an address the screen could not
 * show them. The detail screen loads the whole record, so nothing is missing there.
 *
 * ⚠ Deleting is refused while the facility still lists services, with a 422 naming the count. The
 * delete is a soft delete, so nothing cascades and no foreign key objects; before slice 9 the
 * services silently stopped being listable while continuing to exist.
 */
const typeLabel = (v: string) =>
  HEALTH_FACILITY_TYPE_OPTIONS.find((o) => o.value === v)?.label ?? v;

export default function HealthcareFacilitiesPage() {
  const { hasAnyPermission } = useAuth();
  const canWrite = hasAnyPermission(['HR.Medical.Write', 'HR.Medical.Admin']);
  const canAdmin = hasAnyPermission(['HR.Medical.Admin']);
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Healthcare Facilities"
        description="Hospitals, clinics, laboratories and pharmacies the organisation deals with. Medical exams, appointments and expense claims all name a facility from this register."
        backHref="/hr/medical"
      />

      <ResourceListPanel<HealthcareFacilitySummary, FacilityForm>
        title="facilities"
        singular="facility"
        queryKey={['hr', 'medical-facilities']}
        dialogHint="The facility code is how staff will recognise it on a claim — keep it short and stable."
        emptyDescription="No facilities have been registered yet. Add the hospitals and clinics staff actually attend; claims cannot be filed without one."
        list={() => medicalFacilityService.getFacilities()}
        create={(values) => medicalFacilityService.createFacility(facilityFormToRequest(values))}
        // Never called: `allowUpdate` is false, and editing is the detail screen's job. Kept
        // pointing at the real endpoint so the prop cannot quietly become a lie if that changes.
        update={(id, values) =>
          medicalFacilityService.updateFacility(id, { id, ...facilityFormToRequest(values) })
        }
        allowUpdate={false}
        // 2026-09-03: the reads are open to every internal user by design (a claimant must be able
        // to name a facility), so the write affordances follow the API's tiers instead of
        // rendering for everyone and refusing on click: create is HR.Medical.Write, delete is Admin.
        allowCreate={canWrite}
        remove={canAdmin ? (id) => medicalFacilityService.removeFacility(id) : undefined}
        getId={(f) => f.id}
        columns={[
          {
            header: 'Facility',
            cell: (f) => (
              <Link
                href={`/hr/medical/facilities/${f.id}`}
                className="font-medium text-primary hover:underline"
              >
                {f.facilityName}
              </Link>
            ),
          },
          {
            header: 'Code',
            cell: (f) => <span className="font-mono text-sm text-muted-foreground">{f.facilityCode}</span>,
          },
          { header: 'Type', cell: (f) => typeLabel(f.facilityType) },
          { header: 'City', cell: (f) => f.city || '—' },
          { header: 'Phone', cell: (f) => f.primaryPhone || '—' },
          {
            header: 'Emergency',
            cell: (f) => (f.hasEmergencyServices ? <Badge variant="secondary">A&E</Badge> : '—'),
          },
          {
            header: 'NHIS',
            cell: (f) => (f.acceptsNHIS ? <Badge variant="secondary">Accepted</Badge> : '—'),
          },
          { header: 'Status', cell: (f) => <StatusBadge active={f.isActive} /> },
        ]}
        schema={facilitySchema as any}
        emptyForm={emptyFacility}
        // Only reached by the create path, which starts from `emptyFacility`; `allowUpdate` is off.
        toForm={() => emptyFacility}
        renderFields={(form) => <FacilityFormFields form={form} />}
      />
    </div>
  );
}
