'use client';

import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { leaveTypeService } from '@/services/hr/leave-type.service';
import { organizationLevelService } from '@/services/hr/organization-level.service';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { GENDER_OPTIONS } from '@/types/hr/employee';
import { LEAVE_ELIGIBILITY_TYPE_OPTIONS, type LeaveTypeEligibility } from '@/types/hr/leave';
import { SelectField } from '@/components/hr/employee/tabs/fields';
import { OrganizationUnitPickerField } from '@/components/hr/common/OrganizationUnitPickerField';

const schema = z
  .object({
    eligibilityType: z.enum(['Gender', 'OrganizationLevel', 'OrganizationUnit', 'Position']),
    gender: z.string().optional().or(z.literal('')),
    organizationLevelId: z.string().optional().or(z.literal('')),
    organizationUnitId: z.string().optional().or(z.literal('')),
    positionId: z.string().optional().or(z.literal('')),
  })
  // Each rule kind carries exactly one target; require the one that matches.
  .superRefine((v, ctx) => {
    const required: Record<typeof v.eligibilityType, keyof typeof v> = {
      Gender: 'gender',
      OrganizationLevel: 'organizationLevelId',
      OrganizationUnit: 'organizationUnitId',
      Position: 'positionId',
    };
    const field = required[v.eligibilityType];
    if (!v[field]) {
      ctx.addIssue({ code: z.ZodIssueCode.custom, path: [field], message: 'Required for this rule type' });
    }
  });

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  eligibilityType: 'Gender',
  gender: '',
  organizationLevelId: '',
  organizationUnitId: '',
  positionId: '',
};

const describe = (e: LeaveTypeEligibility) =>
  e.gender ||
  e.organizationLevelName ||
  e.organizationUnitName ||
  e.positionName ||
  '—';

/**
 * Who may take this leave type. Rules are add/remove only — the backend has no update
 * endpoint for them, so the collection is rendered without an edit affordance.
 */
export function LeaveEligibilityTab({ leaveTypeId }: { leaveTypeId: string }) {
  const { data: levels } = useQuery({
    queryKey: ['hr', 'organization-levels', 'all'],
    queryFn: () => organizationLevelService.getAll(),
  });
  const { data: positions } = useQuery({
    queryKey: ['hr', 'employee-positions'],
    queryFn: () => employeePositionService.getAll(),
  });

  return (
    <ResourceCollectionTab<LeaveTypeEligibility, FormValues>
      parentId={leaveTypeId}
      title="eligibility rules"
      singular="eligibility rule"
      queryKey={['hr', 'leave-types', leaveTypeId, 'eligibility']}
      invalidateKeys={[['hr', 'leave-types', leaveTypeId, 'detail']]}
      dialogHint="Restrict who may take this leave type."
      emptyDescription="With no rules, the leave type is available to everyone."
      getId={(e) => e.id}
      list={leaveTypeService.getEligibilityRules.bind(leaveTypeService)}
      create={(id, v) =>
        leaveTypeService.createEligibilityRule({
          leaveTypeId: id,
          eligibilityType: v.eligibilityType,
          gender: v.eligibilityType === 'Gender' ? v.gender || null : null,
          organizationLevelId:
            v.eligibilityType === 'OrganizationLevel' ? v.organizationLevelId || null : null,
          organizationUnitId:
            v.eligibilityType === 'OrganizationUnit' ? v.organizationUnitId || null : null,
          positionId: v.eligibilityType === 'Position' ? v.positionId || null : null,
        })
      }
      // The backend has no update endpoint for eligibility rules — add and remove only,
      // so `update` is never reached.
      allowUpdate={false}
      update={async () => undefined}
      remove={(_id, ruleId) => leaveTypeService.removeEligibilityRule(ruleId)}
      columns={[
        {
          header: 'Rule',
          cell: (e) =>
            LEAVE_ELIGIBILITY_TYPE_OPTIONS.find((o) => o.value === e.eligibilityType)?.label ??
            e.eligibilityType,
        },
        { header: 'Applies to', cell: describe },
      ]}
      schema={schema}
      emptyForm={empty}
      toForm={(e) => ({
        eligibilityType: e.eligibilityType,
        gender: e.gender ?? '',
        organizationLevelId: e.organizationLevelId ?? '',
        organizationUnitId: e.organizationUnitId ?? '',
        positionId: e.positionId ?? '',
      })}
      renderFields={(form) => {
        const kind = form.watch('eligibilityType');
        return (
          <>
            <SelectField
              form={form}
              name="eligibilityType"
              label="Rule type"
              required
              options={LEAVE_ELIGIBILITY_TYPE_OPTIONS}
            />
            {kind === 'Gender' && (
              <SelectField
                form={form}
                name="gender"
                label="Gender"
                required
                options={GENDER_OPTIONS}
              />
            )}
            {kind === 'OrganizationLevel' && (
              <SelectField
                form={form}
                name="organizationLevelId"
                label="Organization level"
                required
                options={(levels ?? []).map((l) => ({ value: l.id, label: l.name }))}
              />
            )}
            {kind === 'OrganizationUnit' && (
              <OrganizationUnitPickerField form={form} name="organizationUnitId" label="Organization unit" required />
            )}
            {kind === 'Position' && (
              <SelectField
                form={form}
                name="positionId"
                label="Position"
                required
                options={(positions ?? []).map((p) => ({ value: p.id, label: p.title }))}
              />
            )}
          </>
        );
      }}
    />
  );
}
