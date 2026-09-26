'use client';

import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { useLeavePermissions } from '@/components/hr/leave/use-leave-permissions';
import { leaveTypeService } from '@/services/hr/leave-type.service';
import type { LeaveSubType } from '@/types/hr/leave';
import {
  FieldRow,
  NumberField,
  SwitchField,
  TextField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';

const schema = z.object({
  subTypeName: z.string().min(1, 'Name is required').max(100),
  description: z.string().max(500).optional().or(z.literal('')),
  maxDaysAllowed: z.string().optional().or(z.literal('')),
  isActive: z.boolean(),
});

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  subTypeName: '',
  description: '',
  maxDaysAllowed: '',
  isActive: true,
};

const toPayload = (leaveTypeId: string, v: FormValues) => ({
  leaveTypeId,
  subTypeName: v.subTypeName,
  description: v.description || null,
  maxDaysAllowed: v.maxDaysAllowed ? Number(v.maxDaysAllowed) : null,
  isActive: v.isActive,
});

export function LeaveSubTypesTab({ leaveTypeId }: { leaveTypeId: string }) {
  const { canAdminister } = useLeavePermissions();

  return (
    <ResourceCollectionTab<LeaveSubType, FormValues>
      parentId={leaveTypeId}
      title="sub-types"
      singular="sub-type"
      queryKey={['hr', 'leave-types', leaveTypeId, 'sub-types']}
      invalidateKeys={[['hr', 'leave-types', leaveTypeId, 'detail']]}
      dialogHint="Add a sub-type to this leave type."
      emptyDescription="Sub-types break a leave type into named variants, each with its own cap."
      getId={(s) => s.id}
      list={leaveTypeService.getSubTypes.bind(leaveTypeService)}
      create={(id, v) => leaveTypeService.createSubType(toPayload(id, v))}
      update={(id, subTypeId, v) => leaveTypeService.updateSubType(subTypeId, toPayload(id, v))}
      allowRemove={canAdminister}
      remove={(_id, subTypeId) => leaveTypeService.removeSubType(subTypeId)}
      columns={[
        { header: 'Sub-type', cell: (s) => s.subTypeName },
        { header: 'Description', cell: (s) => s.description || '—' },
        { header: 'Max days', cell: (s) => s.maxDaysAllowed ?? '—' },
        {
          header: 'Status',
          cell: (s) =>
            s.isActive ? <Badge variant="secondary">Active</Badge> : <Badge variant="outline">Inactive</Badge>,
        },
      ]}
      schema={schema}
      emptyForm={empty}
      toForm={(s) => ({
        subTypeName: s.subTypeName,
        description: s.description ?? '',
        maxDaysAllowed: s.maxDaysAllowed != null ? String(s.maxDaysAllowed) : '',
        isActive: s.isActive,
      })}
      renderFields={(form) => (
        <>
          <FieldRow>
            <TextField
              form={form}
              name="subTypeName"
              label="Sub-type name"
              placeholder="Maternity"
              required
            />
            <NumberField form={form} name="maxDaysAllowed" label="Max days allowed" />
          </FieldRow>
          <TextareaField form={form} name="description" label="Description" />
          <SwitchField form={form} name="isActive" label="Active" />
        </>
      )}
    />
  );
}
