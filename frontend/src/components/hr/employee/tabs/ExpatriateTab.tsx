'use client';

import { useState } from 'react';
import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Users } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { ExpatriateFamilyPanel } from '../ExpatriateFamilyPanel';
import { countryService } from '@/services/hr/country.service';
import { employeeService } from '@/services/hr/employee.service';
import type { ExpatriateAssignment } from '@/types/hr/employee-subresources';
import { EmployeeSubResourceTab } from './EmployeeSubResourceTab';
import {
  DateField,
  FieldRow,
  NumberField,
  SelectField,
  SwitchField,
  TextField,
  TextareaField,
} from './fields';

const schema = z
  .object({
    homeCountryId: z.string().min(1, 'Home country is required'),
    startDate: z.string().min(1, 'Start date is required'),
    endDate: z.string().optional().or(z.literal('')),
    relocationAllowance: z.string().optional().or(z.literal('')),
    relocationDate: z.string().optional().or(z.literal('')),
    familyAccompanying: z.boolean(),
    assignmentObjective: z.string().max(2000).optional().or(z.literal('')),
    visaType: z.string().max(100).optional().or(z.literal('')),
    visaIssueDate: z.string().optional().or(z.literal('')),
    visaExpiryDate: z.string().optional().or(z.literal('')),
    workPermitNumber: z.string().max(100).optional().or(z.literal('')),
    workPermitIssueDate: z.string().optional().or(z.literal('')),
    workPermitExpiryDate: z.string().optional().or(z.literal('')),
    residentPermitNumber: z.string().max(100).optional().or(z.literal('')),
    residentPermitIssueDate: z.string().optional().or(z.literal('')),
    residentPermitExpiryDate: z.string().optional().or(z.literal('')),
  })
  .refine((v) => !v.endDate || v.endDate >= v.startDate, {
    message: 'End date cannot be before the start date',
    path: ['endDate'],
  });

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  homeCountryId: '',
  startDate: '',
  endDate: '',
  relocationAllowance: '',
  relocationDate: '',
  familyAccompanying: false,
  assignmentObjective: '',
  visaType: '',
  visaIssueDate: '',
  visaExpiryDate: '',
  workPermitNumber: '',
  workPermitIssueDate: '',
  workPermitExpiryDate: '',
  residentPermitNumber: '',
  residentPermitIssueDate: '',
  residentPermitExpiryDate: '',
};

const toPayload = (employeeId: string, v: FormValues) => ({
  employeeId,
  homeCountryId: v.homeCountryId,
  startDate: v.startDate,
  endDate: v.endDate || null,
  relocationAllowance: v.relocationAllowance ? Number(v.relocationAllowance) : null,
  relocationDate: v.relocationDate || null,
  familyAccompanying: v.familyAccompanying,
  assignmentObjective: v.assignmentObjective || null,
  visaType: v.visaType || null,
  visaIssueDate: v.visaIssueDate || null,
  visaExpiryDate: v.visaExpiryDate || null,
  workPermitNumber: v.workPermitNumber || null,
  workPermitIssueDate: v.workPermitIssueDate || null,
  workPermitExpiryDate: v.workPermitExpiryDate || null,
  // ⚠ A different instrument from the work permit, on a different authority's clock: the work
  // permit says you may be employed, the residence permit says you may live here.
  residentPermitNumber: v.residentPermitNumber || null,
  residentPermitIssueDate: v.residentPermitIssueDate || null,
  residentPermitExpiryDate: v.residentPermitExpiryDate || null,
});

const expiringSoon = (date?: string | null) => {
  if (!date) return false;
  const days = (new Date(date).getTime() - Date.now()) / 86_400_000;
  return days < 90;
};

export function ExpatriateTab({ employeeId }: { employeeId: string }) {
  const { data: countries } = useQuery({
    queryKey: ['hr', 'countries', 'active'],
    queryFn: () => countryService.getActive(),
  });

  const countryOptions = (countries ?? []).map((c) => ({ value: c.id, label: c.name }));

  // ⚠ The family panel hangs off a ROW rather than the form: members are their own endpoints, not
  // part of the assignment payload, and they only mean anything for an assignment that exists.
  const [familyFor, setFamilyFor] = useState<ExpatriateAssignment | null>(null);

  return (
    <>
    <EmployeeSubResourceTab<ExpatriateAssignment, FormValues>
      employeeId={employeeId}
      title="expatriate assignments"
      singular="expatriate assignment"
      queryKey="expatriate-assignments"
      emptyDescription="Only applies to employees on an international assignment."
      getId={(a) => a.id}
      list={employeeService.getExpatriateAssignments.bind(employeeService)}
      create={(id, v) => employeeService.addExpatriateAssignment(id, toPayload(id, v))}
      update={(id, assignmentId, v) =>
        employeeService.updateExpatriateAssignment(id, assignmentId, {
          id: assignmentId,
          ...toPayload(id, v),
        })
      }
      remove={employeeService.removeExpatriateAssignment.bind(employeeService)}
      columns={[
        { header: 'Home country', cell: (a) => a.homeCountryName || '—' },
        { header: 'From', cell: (a) => a.startDate?.slice(0, 10) || '—' },
        { header: 'To', cell: (a) => a.endDate?.slice(0, 10) || 'Open-ended' },
        { header: 'Visa', cell: (a) => a.visaType || '—' },
        {
          header: 'Visa expiry',
          cell: (a) =>
            a.visaExpiryDate ? (
              <span className={expiringSoon(a.visaExpiryDate) ? 'text-red-600' : undefined}>
                {a.visaExpiryDate.slice(0, 10)}
              </span>
            ) : (
              '—'
            ),
        },
        {
          // ⚠ Shown beside the work permit because they are different instruments on different
          // clocks — a record that shows only one can hide an unlawful residence.
          header: 'Residence permit',
          cell: (a) =>
            a.residentPermitExpiryDate ? (
              <span className={expiringSoon(a.residentPermitExpiryDate) ? 'text-red-600' : undefined}>
                {a.residentPermitExpiryDate.slice(0, 10)}
              </span>
            ) : (
              '—'
            ),
        },
        {
          header: 'Family',
          cell: (a) => (
            <Button variant="outline" size="sm" onClick={() => setFamilyFor(a)}>
              <Users className="mr-2 h-3.5 w-3.5" />
              {a.familyAccompanying ? 'Accompanying' : 'None'}
            </Button>
          ),
        },
        {
          header: 'Work permit expiry',
          cell: (a) =>
            a.workPermitExpiryDate ? (
              <span className={expiringSoon(a.workPermitExpiryDate) ? 'text-red-600' : undefined}>
                {a.workPermitExpiryDate.slice(0, 10)}
              </span>
            ) : (
              '—'
            ),
        },
        {
          header: 'Family',
          cell: (a) => (a.familyAccompanying ? <Badge variant="secondary">Accompanying</Badge> : '—'),
        },
      ]}
      schema={schema}
      emptyForm={empty}
      dialogClassName="sm:max-w-[640px]"
      toForm={(a) => ({
        homeCountryId: a.homeCountryId,
        startDate: a.startDate?.slice(0, 10) ?? '',
        endDate: a.endDate?.slice(0, 10) ?? '',
        relocationAllowance: a.relocationAllowance != null ? String(a.relocationAllowance) : '',
        relocationDate: a.relocationDate?.slice(0, 10) ?? '',
        familyAccompanying: a.familyAccompanying,
        assignmentObjective: a.assignmentObjective ?? '',
        visaType: a.visaType ?? '',
        visaIssueDate: a.visaIssueDate?.slice(0, 10) ?? '',
        visaExpiryDate: a.visaExpiryDate?.slice(0, 10) ?? '',
        workPermitNumber: a.workPermitNumber ?? '',
        workPermitIssueDate: a.workPermitIssueDate?.slice(0, 10) ?? '',
        workPermitExpiryDate: a.workPermitExpiryDate?.slice(0, 10) ?? '',
        residentPermitNumber: a.residentPermitNumber ?? '',
        residentPermitIssueDate: a.residentPermitIssueDate?.slice(0, 10) ?? '',
        residentPermitExpiryDate: a.residentPermitExpiryDate?.slice(0, 10) ?? '',
      })}
      renderFields={(form) => (
        <>
          <SelectField
            form={form}
            name="homeCountryId"
            label="Home country"
            required
            options={countryOptions}
          />
          <FieldRow>
            <DateField form={form} name="startDate" label="Assignment start" required />
            <DateField form={form} name="endDate" label="Assignment end" />
          </FieldRow>
          <FieldRow>
            <NumberField
              form={form}
              name="relocationAllowance"
              label="Relocation allowance"
              step="0.01"
            />
            <DateField form={form} name="relocationDate" label="Relocation date" />
          </FieldRow>
          {/* ⚠ Every permit carried an expiry and no ISSUE date, so the record could never answer
              "how long was this granted for" — the question asked when a renewal comes back
              shortened or refused. */}
          <FieldRow>
            <TextField form={form} name="visaType" label="Visa type" />
            <DateField form={form} name="visaIssueDate" label="Visa issued" />
            <DateField form={form} name="visaExpiryDate" label="Visa expiry" />
          </FieldRow>
          <FieldRow>
            <TextField form={form} name="workPermitNumber" label="Work permit number" />
            <DateField form={form} name="workPermitIssueDate" label="Work permit issued" />
            <DateField form={form} name="workPermitExpiryDate" label="Work permit expiry" />
          </FieldRow>
          {/* ⚠ A separate instrument from the work permit, issued by a different authority on a
              different clock. Recording only one and calling it "the permit" is how somebody ends
              up lawfully employed and unlawfully resident, with a record that cannot show it. */}
          <FieldRow>
            <TextField form={form} name="residentPermitNumber" label="Residence permit number" />
            <DateField form={form} name="residentPermitIssueDate" label="Residence permit issued" />
            <DateField form={form} name="residentPermitExpiryDate" label="Residence permit expiry" />
          </FieldRow>
          <SwitchField
            form={form}
            name="familyAccompanying"
            label="Family accompanying"
            description="Affects relocation and benefit entitlements."
          />
          <TextareaField form={form} name="assignmentObjective" label="Assignment objective" />
        </>
      )}
    />

    <Dialog open={familyFor !== null} onOpenChange={(o) => !o && setFamilyFor(null)}>
      <DialogContent className="max-w-3xl">
        <DialogHeader>
          <DialogTitle>
            Family accompanying the {familyFor?.homeCountryName ?? ''} assignment
          </DialogTitle>
          <DialogDescription>
            Recording somebody here also marks the assignment as having family accompanying.
          </DialogDescription>
        </DialogHeader>
        {familyFor && (
          <ExpatriateFamilyPanel employeeId={employeeId} assignmentId={familyFor.id} />
        )}
      </DialogContent>
    </Dialog>
    </>
  );
}
