'use client';

import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Badge } from '@/components/ui/badge';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { organizationLevelService } from '@/services/hr/organization-level.service';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import { locationService } from '@/services/hr/location.service';
import { employeeService } from '@/services/hr/employee.service';
import {
  POSITION_CHANGE_REASON_OPTIONS,
  type EmployeePositionHistory,
} from '@/types/hr/employee-subresources';
import { EmployeeSubResourceTab } from './EmployeeSubResourceTab';
import { DateField, FieldRow, SelectField, TextareaField } from './fields';

const schema = z
  .object({
    positionId: z.string().min(1, 'Position is required'),
    organizationLevelId: z.string().min(1, 'Organization level is required'),
    organizationUnitId: z.string().optional().or(z.literal('')),
    locationId: z.string().optional().or(z.literal('')),
    startDate: z.string().min(1, 'Start date is required'),
    endDate: z.string().optional().or(z.literal('')),
    changeReason: z.enum([
      'InitialAssignment',
      'Promotion',
      'Demotion',
      'Transfer',
      'Restructure',
      'Termination',
      'Other',
    ]),
    notes: z.string().max(1000).optional().or(z.literal('')),
  })
  .refine((v) => !v.endDate || v.endDate >= v.startDate, {
    message: 'End date cannot be before the start date',
    path: ['endDate'],
  });

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  positionId: '',
  organizationLevelId: '',
  organizationUnitId: '',
  locationId: '',
  startDate: '',
  endDate: '',
  changeReason: 'InitialAssignment',
  notes: '',
};

// This resource takes DateTime, not DateOnly — send a full ISO timestamp.
const toIso = (date: string) => new Date(`${date}T00:00:00`).toISOString();

const toPayload = (employeeId: string, v: FormValues) => ({
  employeeId,
  positionId: v.positionId,
  organizationLevelId: v.organizationLevelId,
  organizationUnitId: v.organizationUnitId || null,
  locationId: v.locationId || null,
  startDate: toIso(v.startDate),
  endDate: v.endDate ? toIso(v.endDate) : null,
  changeReason: v.changeReason,
  notes: v.notes || null,
});

export function PositionHistoryTab({ employeeId }: { employeeId: string }) {
  const { data: positions } = useQuery({
    queryKey: ['hr', 'employee-positions'],
    queryFn: () => employeePositionService.getAll(),
  });
  const { data: levels } = useQuery({
    queryKey: ['hr', 'organization-levels', 'all'],
    queryFn: () => organizationLevelService.getAll(),
  });
  const { data: units } = useQuery({
    queryKey: ['hr', 'organization-units', 'summary'],
    queryFn: () => organizationUnitService.getSummary(),
  });
  const { data: locations } = useQuery({
    queryKey: ['hr', 'locations', 'all'],
    queryFn: () => locationService.getAll(),
  });

  return (
    <EmployeeSubResourceTab<EmployeePositionHistory, FormValues>
      employeeId={employeeId}
      title="position records"
      singular="position record"
      queryKey="position-histories"
      emptyDescription="The employee's position history is recorded here."
      getId={(p) => p.id}
      list={employeeService.getPositionHistories.bind(employeeService)}
      create={(id, v) => employeeService.addPositionHistory(id, toPayload(id, v))}
      update={(id, historyId, v) =>
        employeeService.updatePositionHistory(id, historyId, {
          id: historyId,
          ...toPayload(id, v),
        })
      }
      remove={employeeService.removePositionHistory.bind(employeeService)}
      columns={[
        { header: 'Position', cell: (p) => p.positionTitle || '—' },
        { header: 'Reason', cell: (p) => p.changeReason },
        { header: 'From', cell: (p) => p.startDate?.slice(0, 10) || '—' },
        { header: 'To', cell: (p) => p.endDate?.slice(0, 10) || '—' },
        {
          header: 'Current',
          cell: (p) => (p.isCurrent ? <Badge variant="secondary">Current</Badge> : '—'),
        },
      ]}
      schema={schema}
      emptyForm={empty}
      dialogClassName="sm:max-w-[640px]"
      toForm={(p) => ({
        positionId: p.positionId,
        organizationLevelId: p.organizationLevelId ?? '',
        organizationUnitId: p.organizationUnitId ?? '',
        locationId: p.locationId ?? '',
        startDate: p.startDate?.slice(0, 10) ?? '',
        endDate: p.endDate?.slice(0, 10) ?? '',
        changeReason: p.changeReason,
        notes: p.notes ?? '',
      })}
      renderFields={(form) => {
        const levelId = form.watch('organizationLevelId');
        const unitOptions = (units ?? [])
          .filter((u) => !levelId || u.organizationLevelId === levelId)
          .map((u) => ({ value: u.id, label: u.name }));

        return (
          <>
            <SelectField
              form={form}
              name="positionId"
              label="Position"
              required
              options={(positions ?? []).map((p) => ({ value: p.id, label: p.title }))}
            />
            <FieldRow>
              <SelectField
                form={form}
                name="organizationLevelId"
                label="Organization level"
                required
                options={(levels ?? []).map((l) => ({ value: l.id, label: l.name }))}
              />
              <SelectField
                form={form}
                name="organizationUnitId"
                label="Organization unit"
                options={unitOptions}
                allowEmpty
              />
            </FieldRow>
            <SelectField
              form={form}
              name="locationId"
              label="Location"
              options={(locations ?? []).map((l) => ({ value: l.id, label: l.name }))}
              allowEmpty
            />
            <FieldRow>
              <DateField form={form} name="startDate" label="Start date" required />
              <DateField form={form} name="endDate" label="End date" />
            </FieldRow>
            <SelectField
              form={form}
              name="changeReason"
              label="Change reason"
              required
              options={POSITION_CHANGE_REASON_OPTIONS}
            />
            <TextareaField form={form} name="notes" label="Notes" />
          </>
        );
      }}
    />
  );
}
