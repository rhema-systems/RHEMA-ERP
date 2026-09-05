'use client';

import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  FieldRow,
  NumberField,
  SelectField,
  SwitchField,
  TextareaField,
  TextField,
} from '@/components/hr/employee/tabs/fields';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import {
  REPORTING_RELATIONSHIP_TYPES,
  type JobReportingRelationship,
  type ReportingRelationshipType,
} from '@/types/hr/job-architecture';
import { childKey, idOrNull, labelFor, optionalNumber, type ChildPanelProps } from './shared';

const schema = z.object({
  relationshipType: z.string().min(1, 'Choose a relationship'),
  titleOrRole: z.string().trim().min(1, 'Name the role or party').max(200),
  employeeOrPositionId: z.string().optional(),
  description: z.string().trim().min(1, 'Describe the working relationship').max(1000),
  numberOfDirectReports: optionalNumber(0),
  isPrimarySupervisor: z.boolean(),
});

type Form = z.infer<typeof schema>;

const empty: Form = {
  relationshipType: 'CollaboratesWith',
  titleOrRole: '',
  employeeOrPositionId: '',
  description: '',
  numberOfDirectReports: null,
  isPrimarySupervisor: false,
};

/**
 * The working relationships the job sits inside — who it deals with, internally and outside.
 *
 * ⚠ **This is deliberately not the reporting line.** The C# enum's own remarks say so: the position
 * a holder reports to and the positions they supervise live on the position record, and
 * `ReportingRelationshipType` omits "ReportsTo" and "Supervises" on purpose. What is here is the
 * lateral map — collaborators, internal and external customers, vendors, regulators, matrix
 * reports. Anyone looking for the org chart is on the wrong screen, so the panel says as much
 * rather than leaving them to conclude the data is missing.
 *
 * ⚠ **`employeeOrPositionId` is a POSITION id despite the name.** The FK targets
 * `EmployeePosition`, and the read DTO calls what comes back `relatedPositionTitle`. Putting an
 * employee id there is a foreign-key failure, not a silent mismatch.
 */
export function ReportingRelationshipsPanel({
  jobDescriptionId,
  canAuthor,
  canDelete,
  invalidateKeys,
}: ChildPanelProps) {
  const { data: positions } = useQuery({
    queryKey: ['positions', 'all'],
    queryFn: () => employeePositionService.getAll(),
    staleTime: 5 * 60 * 1000,
  });

  const positionOptions = (positions ?? []).map((p) => ({
    value: p.id,
    label: p.organizationUnitName ? `${p.title} — ${p.organizationUnitName}` : p.title,
  }));

  return (
    <ResourceCollectionTab<JobReportingRelationship, Form>
      parentId={jobDescriptionId}
      title="working relationships"
      singular="working relationship"
      queryKey={childKey(jobDescriptionId, 'reporting-relationships')}
      invalidateKeys={invalidateKeys}
      readOnly={!canAuthor}
      dialogHint="A party the holder works with. The reporting line itself lives on the position."
      emptyDescription="Record who the holder works with — colleagues, customers, vendors, regulators."
      dialogClassName="sm:max-w-[640px]"
      list={(id) => jobArchitectureService.getReportingRelationships(id)}
      create={(id, v) =>
        jobArchitectureService.addReportingRelationship(id, {
          relationshipType: v.relationshipType as ReportingRelationshipType,
          titleOrRole: v.titleOrRole.trim(),
          employeeOrPositionId: idOrNull(v.employeeOrPositionId),
          description: v.description.trim(),
          numberOfDirectReports: v.numberOfDirectReports,
          isPrimarySupervisor: v.isPrimarySupervisor,
        })
      }
      update={(_id, relationshipId, v) =>
        jobArchitectureService.updateReportingRelationship(relationshipId, {
          relationshipType: v.relationshipType as ReportingRelationshipType,
          titleOrRole: v.titleOrRole.trim(),
          employeeOrPositionId: idOrNull(v.employeeOrPositionId),
          description: v.description.trim(),
          numberOfDirectReports: v.numberOfDirectReports,
          isPrimarySupervisor: v.isPrimarySupervisor,
        })
      }
      remove={
        canDelete
          ? (_id, relationshipId) => jobArchitectureService.deleteReportingRelationship(relationshipId)
          : undefined
      }
      getId={(r) => r.id}
      columns={[
        { header: 'Relationship', cell: (r) => labelFor(REPORTING_RELATIONSHIP_TYPES, r.relationshipType) },
        { header: 'Role or party', cell: (r) => r.titleOrRole },
        {
          header: 'Linked position',
          cell: (r) => <span className="text-muted-foreground">{r.relatedPositionTitle || '—'}</span>,
        },
        { header: 'Description', cell: (r) => r.description },
        {
          header: 'Reports',
          cell: (r) => (
            <div className="flex flex-wrap items-center gap-1">
              {r.numberOfDirectReports != null && <span>{r.numberOfDirectReports}</span>}
              {r.isPrimarySupervisor && <Badge variant="outline">Primary</Badge>}
            </div>
          ),
        },
      ]}
      schema={schema}
      emptyForm={empty}
      toForm={(r) => ({
        relationshipType: r.relationshipType,
        titleOrRole: r.titleOrRole,
        employeeOrPositionId: r.employeeOrPositionId ?? '',
        description: r.description,
        numberOfDirectReports: r.numberOfDirectReports ?? null,
        isPrimarySupervisor: r.isPrimarySupervisor,
      })}
      renderFields={(form) => (
        <>
          <SelectField
            form={form}
            name="relationshipType"
            label="Relationship"
            required
            options={REPORTING_RELATIONSHIP_TYPES}
          />
          <TextField
            form={form}
            name="titleOrRole"
            label="Role or party"
            required
            placeholder="e.g. Finance Manager, Ghana Revenue Authority"
          />
          <SelectField
            form={form}
            name="employeeOrPositionId"
            label="Linked position"
            allowEmpty
            emptyLabel="Not a position on our structure"
            placeholder="Optional — link to a position"
            options={positionOptions}
          />
          <TextareaField
            form={form}
            name="description"
            label="How they work together"
            rows={2}
            placeholder="e.g. Agrees the monthly stock valuation before the ledger is closed."
          />
          <FieldRow>
            <NumberField
              form={form}
              name="numberOfDirectReports"
              label="Number of people"
              placeholder="Leave blank if not applicable"
            />
            <div className="flex items-end">
              <div className="w-full">
                <SwitchField
                  form={form}
                  name="isPrimarySupervisor"
                  label="Primary supervisor"
                  description="The person this holder answers to day to day."
                />
              </div>
            </div>
          </FieldRow>
        </>
      )}
    />
  );
}
