'use client';

import { useState } from 'react';
import Link from 'next/link';
import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { CalendarClock } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { medicalHealthService } from '@/services/hr/medical-health.service';
import { medicalFacilityService } from '@/services/hr/medical-reference.service';
import {
  BLOOD_GROUP_OPTIONS,
  DISABILITY_STATUS_OPTIONS,
  MEDICAL_EXAM_RESULT_OPTIONS,
} from '@/types/hr/medical';
import type { EmployeeHealthProfile, EmployeeMedicalExamSummary } from '@/types/hr/medical';

/**
 * Employee health profiles — one per employee, and the anchor every condition, allergy and
 * examination hangs off.
 *
 * Everything on this screen is special-category personal data and requires medical permissions;
 * an employee reads their own file through self-service, not here.
 */
const profileSchema = z.object({
  employeeId: z.string().min(1, 'Choose an employee'),
  bloodGroup: z.enum([
    'APositive',
    'ANegative',
    'BPositive',
    'BNegative',
    'ABPositive',
    'ABNegative',
    'OPositive',
    'ONegative',
    'Unknown',
  ]),
  heightCm: z.coerce.number().min(0).optional(),
  weightKg: z.coerce.number().min(0).optional(),
  disabilityStatus: z.enum(['None', 'Mild', 'Moderate', 'Severe']),
  disabilityDescription: z.string().max(500).optional(),
  emergencyContactName: z.string().max(200).optional(),
  emergencyContactPhone: z.string().max(50).optional(),
  emergencyContactRelationship: z.string().max(100).optional(),
  preferredFacilityId: z.string().optional(),
  notes: z.string().max(2000).optional(),
});

type ProfileForm = z.input<typeof profileSchema>;

const emptyProfile: ProfileForm = {
  employeeId: '',
  bloodGroup: 'Unknown',
  heightCm: undefined,
  weightKg: undefined,
  disabilityStatus: 'None',
  disabilityDescription: '',
  emergencyContactName: '',
  emergencyContactPhone: '',
  emergencyContactRelationship: '',
  preferredFacilityId: '',
  notes: '',
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const bloodLabel = (v: string) => BLOOD_GROUP_OPTIONS.find((o) => o.value === v)?.label ?? v;
const resultLabel = (v: string) =>
  MEDICAL_EXAM_RESULT_OPTIONS.find((o) => o.value === v)?.label ?? v;

/** Red for unfit, amber for anything conditional — a clean "Fit" needs no colour. */
function ExamResultBadge({ result }: { result: string }) {
  if (result === 'Fit') return <Badge variant="secondary">Fit</Badge>;
  if (result === 'Unfit') return <Badge variant="destructive">Unfit</Badge>;
  return <Badge variant="outline">{resultLabel(result)}</Badge>;
}

function ExamsDue({ items }: { items: EmployeeMedicalExamSummary[] }) {
  if (items.length === 0) {
    return (
      <EmptyState
        title="Nothing falling due"
        description="No examinations are due for recall in this window."
        icon={CalendarClock}
      />
    );
  }
  return (
    <Card>
      <CardContent className="p-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Exam date</TableHead>
              <TableHead>Facility</TableHead>
              <TableHead>Result</TableHead>
              <TableHead>Next due</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((e) => (
              <TableRow key={e.id}>
                <TableCell>{fmtDate(e.examDate)}</TableCell>
                <TableCell>{e.facilityName || '—'}</TableCell>
                <TableCell>
                  <ExamResultBadge result={e.result} />
                </TableCell>
                <TableCell className="font-medium">{fmtDate(e.nextExamDueDate)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}

export default function EmployeeHealthPage() {
  const [tab, setTab] = useState('profiles');

  const { data: due = [] } = useQuery({
    queryKey: ['hr', 'medical-exams-due', 90],
    queryFn: () => medicalHealthService.getExamsDue(90),
  });

  const { data: facilities = [] } = useQuery({
    queryKey: ['hr', 'medical-facilities'],
    queryFn: () => medicalFacilityService.getFacilities(),
  });

  // The "no facility" choice is SelectField's allowEmpty item: Radix throws on an item whose value is ''.
  const facilityOptions = facilities.map((f) => ({ value: f.id, label: f.facilityName }));

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Health Records"
        description="Employee health profiles, conditions, allergies and medical examinations. Special-category personal data — access requires medical permissions."
        backHref="/hr/medical"
      />

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          <TabsTrigger value="profiles">Profiles</TabsTrigger>
          <TabsTrigger value="due">Exams due ({due.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="profiles" className="mt-4">
          <ResourceListPanel<EmployeeHealthProfile, ProfileForm>
            title="health profiles"
            singular="health profile"
            queryKey={['hr', 'medical-profiles']}
            dialogHint="One profile per employee. Conditions, allergies and examinations are added inside it."
            emptyDescription="No health profiles have been created yet. Start with employees who have had a pre-employment or periodic examination."
            list={() => medicalHealthService.getProfiles()}
            create={(values) => {
              const v = profileSchema.parse(values);
              return medicalHealthService.createProfile({
                ...v,
                heightCm: v.heightCm ?? null,
                weightKg: v.weightKg ?? null,
                disabilityDescription: blank(v.disabilityDescription),
                emergencyContactName: blank(v.emergencyContactName),
                emergencyContactPhone: blank(v.emergencyContactPhone),
                emergencyContactRelationship: blank(v.emergencyContactRelationship),
                preferredFacilityId: blank(v.preferredFacilityId),
                notes: blank(v.notes),
              });
            }}
            update={(id, values) => {
              const v = profileSchema.parse(values);
              // employeeId is intentionally absent — a profile cannot be moved to another person.
              return medicalHealthService.updateProfile(id, {
                id,
                bloodGroup: v.bloodGroup,
                heightCm: v.heightCm ?? null,
                weightKg: v.weightKg ?? null,
                disabilityStatus: v.disabilityStatus,
                disabilityDescription: blank(v.disabilityDescription),
                emergencyContactName: blank(v.emergencyContactName),
                emergencyContactPhone: blank(v.emergencyContactPhone),
                emergencyContactRelationship: blank(v.emergencyContactRelationship),
                preferredFacilityId: blank(v.preferredFacilityId),
                notes: blank(v.notes),
              });
            }}
            remove={(id) => medicalHealthService.removeProfile(id)}
            getId={(p) => p.id}
            columns={[
              {
                header: 'Employee',
                cell: (p) => (
                  <Link
                    href={`/hr/medical/health/${p.id}`}
                    className="font-medium text-primary hover:underline"
                  >
                    {p.employeeName}
                  </Link>
                ),
              },
              {
                header: 'Number',
                cell: (p) => (
                  <span className="font-mono text-sm text-muted-foreground">
                    {p.employeeNumber || '—'}
                  </span>
                ),
              },
              { header: 'Blood group', cell: (p) => bloodLabel(p.bloodGroup) },
              {
                header: 'Emergency contact',
                cell: (p) =>
                  p.emergencyContactName
                    ? `${p.emergencyContactName}${p.emergencyContactPhone ? ` · ${p.emergencyContactPhone}` : ''}`
                    : '—',
              },
              {
                header: 'Disability',
                cell: (p) =>
                  p.disabilityStatus === 'None' ? (
                    '—'
                  ) : (
                    <Badge variant="outline">{p.disabilityStatus}</Badge>
                  ),
              },
              { header: 'Updated', cell: (p) => fmtDate(p.lastUpdated) },
            ]}
            schema={profileSchema as any}
            emptyForm={emptyProfile}
            toForm={(p) => ({
              ...emptyProfile,
              employeeId: p.employeeId,
              bloodGroup: p.bloodGroup,
              heightCm: p.heightCm ?? undefined,
              weightKg: p.weightKg ?? undefined,
              disabilityStatus: p.disabilityStatus,
              disabilityDescription: p.disabilityDescription ?? '',
              emergencyContactName: p.emergencyContactName ?? '',
              emergencyContactPhone: p.emergencyContactPhone ?? '',
              emergencyContactRelationship: p.emergencyContactRelationship ?? '',
              preferredFacilityId: p.preferredFacilityId ?? '',
              notes: p.notes ?? '',
            })}
            renderFields={(form, editing) => (
              <>
                {/* The employee is fixed once the profile exists — the update DTO has no field
                    for it, so offering the picker on edit would imply a move that cannot happen. */}
                {!editing && (
                  <div className="space-y-2">
                    <label className="text-sm font-medium">Employee</label>
                    <EmployeePicker
                      value={form.watch('employeeId') || null}
                      onChange={(id) => form.setValue('employeeId', id ?? '')}
                    />
                    {form.formState.errors.employeeId && (
                      <p className="text-sm text-destructive">Choose an employee</p>
                    )}
                  </div>
                )}
                <FieldRow>
                  <SelectField
                    form={form}
                    name="bloodGroup"
                    label="Blood group"
                    options={BLOOD_GROUP_OPTIONS}
                  />
                  <SelectField
                    form={form}
                    name="disabilityStatus"
                    label="Disability"
                    options={DISABILITY_STATUS_OPTIONS}
                  />
                </FieldRow>
                <TextField
                  form={form}
                  name="disabilityDescription"
                  label="Disability description"
                />
                <FieldRow>
                  <NumberField form={form} name="heightCm" label="Height (cm)" />
                  <NumberField form={form} name="weightKg" label="Weight (kg)" />
                </FieldRow>

                <p className="pt-2 text-sm font-medium">Emergency contact</p>
                <FieldRow>
                  <TextField form={form} name="emergencyContactName" label="Name" />
                  <TextField form={form} name="emergencyContactPhone" label="Phone" />
                </FieldRow>
                <TextField
                  form={form}
                  name="emergencyContactRelationship"
                  label="Relationship"
                  placeholder="e.g. Spouse, Parent"
                />

                <SelectField
                  form={form}
                  name="preferredFacilityId"
                  label="Preferred facility"
                  options={facilityOptions}
                  allowEmpty
                  emptyLabel="No preferred facility"
                />
                <TextareaField form={form} name="notes" label="Notes" rows={2} />
              </>
            )}
          />
        </TabsContent>

        <TabsContent value="due" className="mt-4">
          <p className="mb-3 text-sm text-muted-foreground">
            Examinations with a next-due date inside the next 90 days. Recalls for SHE health
            surveillance are tracked separately under Safety.
          </p>
          <ExamsDue items={due} />
        </TabsContent>
      </Tabs>
    </div>
  );
}
