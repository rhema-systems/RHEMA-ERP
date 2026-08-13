'use client';

import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Badge } from '@/components/ui/badge';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  TextField,
  NumberField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyPpeService } from '@/services/hr/safety-ppe.service';
import type { JobRolePpeRequirement } from '@/types/hr/safety-ppe';

/**
 * The job-role PPE requirement matrix (FR-SHE-133) — which PPE each job role must be issued,
 * how many, and how often it is replaced. The pair (job role, PPE type) is unique; adding a
 * duplicate is refused. Role code and PPE type are fixed once created — remove and re-add to
 * change them.
 */
const requirementSchema = z.object({
  jobRoleCode: z.string().min(1, 'A role code is required').max(50),
  jobRoleName: z.string().min(1, 'A role name is required').max(150),
  ppeTypeId: z.string().min(1, 'A PPE type is required'),
  quantity: z.coerce.number().min(1).max(100),
  replacementFrequencyMonths: z.coerce.number().min(0).max(600),
  isMandatory: z.boolean(),
});

type RequirementForm = z.input<typeof requirementSchema>;

const emptyRequirement: RequirementForm = {
  jobRoleCode: '',
  jobRoleName: '',
  ppeTypeId: '',
  quantity: 1,
  replacementFrequencyMonths: 12,
  isMandatory: true,
};

export default function SafetyPpeRequirementsPage() {
  const { data: types = [] } = useQuery({
    queryKey: ['hr', 'safety-ppe', 'types', 'active'],
    queryFn: () => safetyPpeService.getTypes(true),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Job-Role PPE Requirements"
        description="The matrix behind compliant issuance — what each job role must be issued, in what quantity, and how often it is replaced."
        backHref="/administration/hr/safety"
      />

      <ResourceListPanel<JobRolePpeRequirement, RequirementForm>
        title="job-role PPE requirements"
        singular="requirement"
        queryKey={['hr', 'safety-ppe', 'requirements']}
        dialogHint="The role code and PPE type are fixed once created."
        list={() => safetyPpeService.getRequirements()}
        create={(values) => {
          const v = requirementSchema.parse(values);
          return safetyPpeService.addRequirement({
            jobRoleCode: v.jobRoleCode,
            jobRoleName: v.jobRoleName,
            ppeTypeId: v.ppeTypeId,
            quantity: v.quantity,
            replacementFrequencyMonths: v.replacementFrequencyMonths,
            isMandatory: v.isMandatory,
          });
        }}
        update={(id, values) => {
          const v = requirementSchema.parse(values);
          return safetyPpeService.updateRequirement(id, {
            id,
            jobRoleName: v.jobRoleName,
            quantity: v.quantity,
            replacementFrequencyMonths: v.replacementFrequencyMonths,
            isMandatory: v.isMandatory,
          });
        }}
        remove={(id) => safetyPpeService.removeRequirement(id)}
        getId={(r) => r.id}
        emptyDescription="No requirements yet. Without the matrix there is no definition of compliant issuance per role."
        columns={[
          {
            header: 'Job role',
            cell: (r) => (
              <span>
                <span className="font-medium">{r.jobRoleName}</span>{' '}
                <span className="font-mono text-muted-foreground text-xs">{r.jobRoleCode}</span>
              </span>
            ),
          },
          { header: 'PPE type', cell: (r) => r.ppeTypeName },
          { header: 'Qty', cell: (r) => r.quantity },
          {
            header: 'Replace every',
            cell: (r) =>
              r.replacementFrequencyMonths > 0 ? (
                `${r.replacementFrequencyMonths} mo`
              ) : (
                <span className="text-muted-foreground">—</span>
              ),
          },
          {
            header: 'Mandatory',
            cell: (r) =>
              r.isMandatory ? (
                <Badge variant="secondary">Mandatory</Badge>
              ) : (
                <Badge variant="outline">Optional</Badge>
              ),
          },
        ]}
        schema={requirementSchema}
        emptyForm={emptyRequirement}
        toForm={(r) => ({
          jobRoleCode: r.jobRoleCode,
          jobRoleName: r.jobRoleName,
          ppeTypeId: r.ppeTypeId,
          quantity: r.quantity,
          replacementFrequencyMonths: r.replacementFrequencyMonths,
          isMandatory: r.isMandatory,
        })}
        renderFields={(form, editing) => (
          <div className="space-y-4">
            {editing ? (
              <p className="text-muted-foreground text-sm">
                Role <span className="font-mono">{form.getValues('jobRoleCode')}</span> — the code
                and PPE type are fixed at creation.
              </p>
            ) : (
              <FieldRow>
                <TextField form={form} name="jobRoleCode" label="Role code" required />
              </FieldRow>
            )}
            <TextField form={form} name="jobRoleName" label="Role name" required />
            {!editing && (
              <SelectField
                form={form}
                name="ppeTypeId"
                label="PPE type"
                required
                options={types.map((t) => ({ value: t.id, label: `${t.code} — ${t.name}` }))}
              />
            )}
            <FieldRow>
              <NumberField form={form} name="quantity" label="Quantity" required />
              <NumberField
                form={form}
                name="replacementFrequencyMonths"
                label="Replacement frequency (months, 0 = none)"
              />
            </FieldRow>
            <SwitchField
              form={form}
              name="isMandatory"
              label="Mandatory"
              description="Mandatory items define the compliance bar for the role; optional ones are issued on request."
            />
          </div>
        )}
      />
    </div>
  );
}
