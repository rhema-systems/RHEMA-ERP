'use client';

import { use, useState } from 'react';
import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { AttachmentsPanel } from '@/components/hr/common/AttachmentsPanel';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { medicalHealthService } from '@/services/hr/medical-health.service';
import { medicalFacilityService } from '@/services/hr/medical-reference.service';
import {
  BLOOD_GROUP_OPTIONS,
  HEALTH_CONDITION_SEVERITY_OPTIONS,
  HEALTH_CONDITION_STATUS_OPTIONS,
  ALLERGY_TYPE_OPTIONS,
  ALLERGY_SEVERITY_OPTIONS,
  MEDICAL_EXAM_RESULT_OPTIONS,
} from '@/types/hr/medical';
import type {
  EmployeeHealthCondition,
  EmployeeAllergy,
  EmployeeMedicalExamSummary,
} from '@/types/hr/medical';

/**
 * One employee's health file.
 *
 * Allergies lead deliberately: an anaphylactic allergy is the single most consequential thing on
 * this screen in an emergency, so it is surfaced on the header rather than buried in a tab.
 */
const conditionSchema = z.object({
  conditionName: z.string().min(1, 'Required').max(200),
  icdCode: z.string().max(20).optional(),
  severity: z.enum(['Mild', 'Moderate', 'Severe', 'Critical']),
  status: z.enum(['Active', 'Managed', 'Resolved', 'InRemission']),
  diagnosedDate: z.string().optional(),
  resolvedDate: z.string().optional(),
  treatmentSummary: z.string().max(1000).optional(),
  notes: z.string().max(1000).optional(),
});
type ConditionForm = z.input<typeof conditionSchema>;
const emptyCondition: ConditionForm = {
  conditionName: '',
  icdCode: '',
  severity: 'Mild',
  status: 'Active',
  diagnosedDate: '',
  resolvedDate: '',
  treatmentSummary: '',
  notes: '',
};

const allergySchema = z.object({
  allergen: z.string().min(1, 'Required').max(200),
  allergyType: z.enum(['Drug', 'Food', 'Environmental', 'Latex', 'Insect', 'Other']),
  severity: z.enum(['Mild', 'Moderate', 'Severe', 'Anaphylactic']),
  reactionDescription: z.string().max(1000).optional(),
  managementPlan: z.string().max(1000).optional(),
  isActive: z.boolean(),
  notes: z.string().max(1000).optional(),
});
type AllergyForm = z.input<typeof allergySchema>;
const emptyAllergy: AllergyForm = {
  allergen: '',
  allergyType: 'Drug',
  severity: 'Mild',
  reactionDescription: '',
  managementPlan: '',
  isActive: true,
  notes: '',
};

const examSchema = z.object({
  examDate: z.string().min(1, 'Required'),
  facilityId: z.string().optional(),
  physicianId: z.string().optional(),
  heightCm: z.coerce.number().min(0).optional(),
  weightKg: z.coerce.number().min(0).optional(),
  bloodPressure: z.string().max(20).optional(),
  visionResult: z.string().max(200).optional(),
  hearingResult: z.string().max(200).optional(),
  result: z.enum([
    'Fit',
    'FitWithRestrictions',
    'TemporarilyUnfit',
    'Unfit',
    'RequiresFurtherInvestigation',
  ]),
  findings: z.string().max(1000).optional(),
  recommendations: z.string().max(1000).optional(),
  restrictions: z.string().max(500).optional(),
  nextExamDueDate: z.string().optional(),
  notes: z.string().max(2000).optional(),
});
type ExamForm = z.input<typeof examSchema>;
const emptyExam: ExamForm = {
  examDate: '',
  facilityId: '',
  physicianId: '',
  heightCm: undefined,
  weightKg: undefined,
  bloodPressure: '',
  visionResult: '',
  hearingResult: '',
  result: 'Fit',
  findings: '',
  recommendations: '',
  restrictions: '',
  nextExamDueDate: '',
  notes: '',
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const label = (opts: { value: string; label: string }[], v: string) =>
  opts.find((o) => o.value === v)?.label ?? v;

export default function EmployeeHealthProfilePage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = use(params);
  const [selectedExam, setSelectedExam] = useState<EmployeeMedicalExamSummary | null>(null);

  const { data: profile } = useQuery({
    queryKey: ['hr', 'medical-profiles', id],
    queryFn: () => medicalHealthService.getProfile(id),
  });

  const { data: allergies = [] } = useQuery({
    queryKey: ['hr', 'medical-profiles', id, 'allergies'],
    queryFn: () => medicalHealthService.getAllergies(id),
  });

  const { data: facilities = [] } = useQuery({
    queryKey: ['hr', 'medical-facilities'],
    queryFn: () => medicalFacilityService.getFacilities(),
  });

  const { data: physicians = [] } = useQuery({
    queryKey: ['hr', 'medical-physicians'],
    queryFn: () => medicalFacilityService.getPhysicians(),
  });

  // "Not recorded" is SelectField's allowEmpty item: Radix throws on an item whose value is ''.
  const facilityOptions = facilities.map((f) => ({ value: f.id, label: f.facilityName }));
  const physicianOptions = physicians.map((p) => ({ value: p.id, label: p.fullName }));

  const critical = allergies.filter(
    (a) => a.isActive && (a.severity === 'Anaphylactic' || a.severity === 'Severe'),
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={profile?.employeeName ?? 'Health profile'}
        description={
          profile
            ? `${profile.employeeNumber ?? 'No employee number'} · blood group ${label(BLOOD_GROUP_OPTIONS, profile.bloodGroup)}`
            : 'Loading…'
        }
        backHref="/hr/medical/health"
      />

      {/* Severe and anaphylactic allergies are the one thing on this file that someone may need
          in seconds. They stay on the header, not behind a tab. */}
      {critical.length > 0 && (
        <Card className="border-destructive">
          <CardContent className="flex flex-wrap items-center gap-2 p-4">
            <span className="text-sm font-medium text-destructive">Severe allergies:</span>
            {critical.map((a) => (
              <Badge key={a.id} variant="destructive">
                {a.allergen} · {a.severity}
              </Badge>
            ))}
          </CardContent>
        </Card>
      )}

      {profile && (
        <Card>
          <CardContent className="grid gap-4 p-6 sm:grid-cols-2 lg:grid-cols-4">
            <Detail label="Emergency contact" value={profile.emergencyContactName} />
            <Detail label="Contact phone" value={profile.emergencyContactPhone} />
            <Detail label="Relationship" value={profile.emergencyContactRelationship} />
            <Detail label="Preferred facility" value={profile.preferredFacilityName} />
            <Detail
              label="Height / weight"
              value={
                profile.heightCm || profile.weightKg
                  ? `${profile.heightCm ?? '—'} cm / ${profile.weightKg ?? '—'} kg`
                  : null
              }
            />
            <Detail label="Disability" value={profile.disabilityStatus} />
            <Detail label="Last updated" value={fmtDate(profile.lastUpdated)} />
          </CardContent>
        </Card>
      )}

      <Tabs defaultValue="conditions">
        <TabsList>
          <TabsTrigger value="conditions">Conditions</TabsTrigger>
          <TabsTrigger value="allergies">Allergies ({allergies.length})</TabsTrigger>
          <TabsTrigger value="exams">Examinations</TabsTrigger>
        </TabsList>

        <TabsContent value="conditions" className="mt-4">
          <ResourceCollectionTab<EmployeeHealthCondition, ConditionForm>
            parentId={id}
            title="conditions"
            singular="condition"
            queryKey={['hr', 'medical-profiles', id, 'conditions']}
            emptyDescription="No conditions recorded on this file."
            list={(profileId) => medicalHealthService.getConditions(profileId)}
            create={(profileId, values) => {
              const v = conditionSchema.parse(values);
              return medicalHealthService.addCondition({
                ...v,
                healthProfileId: profileId,
                icdCode: blank(v.icdCode),
                diagnosedDate: blank(v.diagnosedDate),
                resolvedDate: blank(v.resolvedDate),
                treatmentSummary: blank(v.treatmentSummary),
                notes: blank(v.notes),
              });
            }}
            update={(profileId, conditionId, values) => {
              const v = conditionSchema.parse(values);
              return medicalHealthService.updateCondition(conditionId, {
                id: conditionId,
                ...v,
                healthProfileId: profileId,
                icdCode: blank(v.icdCode),
                diagnosedDate: blank(v.diagnosedDate),
                resolvedDate: blank(v.resolvedDate),
                treatmentSummary: blank(v.treatmentSummary),
                notes: blank(v.notes),
              });
            }}
            remove={(_p, conditionId) => medicalHealthService.removeCondition(conditionId)}
            getId={(c) => c.id}
            columns={[
              { header: 'Condition', cell: (c) => <span className="font-medium">{c.conditionName}</span> },
              { header: 'ICD', cell: (c) => c.icdCode || '—' },
              { header: 'Severity', cell: (c) => label(HEALTH_CONDITION_SEVERITY_OPTIONS, c.severity) },
              {
                header: 'Status',
                cell: (c) =>
                  c.status === 'Active' ? (
                    <Badge variant="outline">Active</Badge>
                  ) : (
                    label(HEALTH_CONDITION_STATUS_OPTIONS, c.status)
                  ),
              },
              { header: 'Diagnosed', cell: (c) => fmtDate(c.diagnosedDate) },
            ]}
            schema={conditionSchema as any}
            emptyForm={emptyCondition}
            toForm={(c) => ({
              conditionName: c.conditionName,
              icdCode: c.icdCode ?? '',
              severity: c.severity,
              status: c.status,
              diagnosedDate: c.diagnosedDate ?? '',
              resolvedDate: c.resolvedDate ?? '',
              treatmentSummary: c.treatmentSummary ?? '',
              notes: c.notes ?? '',
            })}
            renderFields={(form) => (
              <>
                <FieldRow>
                  <TextField form={form} name="conditionName" label="Condition" required />
                  <TextField form={form} name="icdCode" label="ICD code" />
                </FieldRow>
                <FieldRow>
                  <SelectField
                    form={form}
                    name="severity"
                    label="Severity"
                    options={HEALTH_CONDITION_SEVERITY_OPTIONS}
                  />
                  <SelectField
                    form={form}
                    name="status"
                    label="Status"
                    options={HEALTH_CONDITION_STATUS_OPTIONS}
                  />
                </FieldRow>
                <FieldRow>
                  <DateField form={form} name="diagnosedDate" label="Diagnosed" />
                  <DateField form={form} name="resolvedDate" label="Resolved" />
                </FieldRow>
                <TextareaField form={form} name="treatmentSummary" label="Treatment" rows={2} />
                <TextareaField form={form} name="notes" label="Notes" rows={2} />
              </>
            )}
          />
        </TabsContent>

        <TabsContent value="allergies" className="mt-4">
          <ResourceCollectionTab<EmployeeAllergy, AllergyForm>
            parentId={id}
            title="allergies"
            singular="allergy"
            queryKey={['hr', 'medical-profiles', id, 'allergies']}
            dialogHint="Record the management plan for anything severe — it is what someone reads in an emergency."
            emptyDescription="No allergies recorded on this file."
            list={(profileId) => medicalHealthService.getAllergies(profileId)}
            create={(profileId, values) => {
              const v = allergySchema.parse(values);
              return medicalHealthService.addAllergy({
                ...v,
                healthProfileId: profileId,
                reactionDescription: blank(v.reactionDescription),
                managementPlan: blank(v.managementPlan),
                notes: blank(v.notes),
              });
            }}
            update={(profileId, allergyId, values) => {
              const v = allergySchema.parse(values);
              return medicalHealthService.updateAllergy(allergyId, {
                id: allergyId,
                ...v,
                healthProfileId: profileId,
                reactionDescription: blank(v.reactionDescription),
                managementPlan: blank(v.managementPlan),
                notes: blank(v.notes),
              });
            }}
            remove={(_p, allergyId) => medicalHealthService.removeAllergy(allergyId)}
            getId={(a) => a.id}
            columns={[
              { header: 'Allergen', cell: (a) => <span className="font-medium">{a.allergen}</span> },
              { header: 'Type', cell: (a) => label(ALLERGY_TYPE_OPTIONS, a.allergyType) },
              {
                header: 'Severity',
                cell: (a) =>
                  a.severity === 'Anaphylactic' || a.severity === 'Severe' ? (
                    <Badge variant="destructive">{a.severity}</Badge>
                  ) : (
                    label(ALLERGY_SEVERITY_OPTIONS, a.severity)
                  ),
              },
              { header: 'Reaction', cell: (a) => a.reactionDescription || '—' },
              { header: 'Status', cell: (a) => <StatusBadge active={a.isActive} /> },
            ]}
            schema={allergySchema as any}
            emptyForm={emptyAllergy}
            toForm={(a) => ({
              allergen: a.allergen,
              allergyType: a.allergyType,
              severity: a.severity,
              reactionDescription: a.reactionDescription ?? '',
              managementPlan: a.managementPlan ?? '',
              isActive: a.isActive,
              notes: a.notes ?? '',
            })}
            renderFields={(form) => (
              <>
                <TextField form={form} name="allergen" label="Allergen" required />
                <FieldRow>
                  <SelectField
                    form={form}
                    name="allergyType"
                    label="Type"
                    options={ALLERGY_TYPE_OPTIONS}
                  />
                  <SelectField
                    form={form}
                    name="severity"
                    label="Severity"
                    options={ALLERGY_SEVERITY_OPTIONS}
                  />
                </FieldRow>
                <TextareaField
                  form={form}
                  name="reactionDescription"
                  label="Reaction"
                  rows={2}
                />
                <TextareaField
                  form={form}
                  name="managementPlan"
                  label="Management plan"
                  rows={2}
                />
                <SwitchField form={form} name="isActive" label="Currently applies" />
                <TextareaField form={form} name="notes" label="Notes" rows={2} />
              </>
            )}
          />
        </TabsContent>

        <TabsContent value="exams" className="mt-4 space-y-6">
          <ResourceCollectionTab<EmployeeMedicalExamSummary, ExamForm>
            parentId={id}
            title="examinations"
            singular="examination"
            queryKey={['hr', 'medical-profiles', id, 'exams']}
            invalidateKeys={[['hr', 'medical-exams-due', 90]]}
            dialogHint="Set the next due date to put this employee on the recall list."
            emptyDescription="No examinations recorded on this file."
            list={(profileId) => medicalHealthService.getExamsByProfile(profileId)}
            create={(profileId, values) => {
              const v = examSchema.parse(values);
              return medicalHealthService.createExam({
                ...v,
                healthProfileId: profileId,
                facilityId: blank(v.facilityId),
                physicianId: blank(v.physicianId),
                heightCm: v.heightCm ?? null,
                weightKg: v.weightKg ?? null,
                bloodPressure: blank(v.bloodPressure),
                visionResult: blank(v.visionResult),
                hearingResult: blank(v.hearingResult),
                findings: blank(v.findings),
                recommendations: blank(v.recommendations),
                restrictions: blank(v.restrictions),
                nextExamDueDate: blank(v.nextExamDueDate),
                notes: blank(v.notes),
              });
            }}
            update={(profileId, examId, values) => {
              const v = examSchema.parse(values);
              return medicalHealthService.updateExam(examId, {
                id: examId,
                ...v,
                healthProfileId: profileId,
                facilityId: blank(v.facilityId),
                physicianId: blank(v.physicianId),
                heightCm: v.heightCm ?? null,
                weightKg: v.weightKg ?? null,
                bloodPressure: blank(v.bloodPressure),
                visionResult: blank(v.visionResult),
                hearingResult: blank(v.hearingResult),
                findings: blank(v.findings),
                recommendations: blank(v.recommendations),
                restrictions: blank(v.restrictions),
                nextExamDueDate: blank(v.nextExamDueDate),
                notes: blank(v.notes),
              });
            }}
            remove={(_p, examId) => medicalHealthService.removeExam(examId)}
            getId={(e) => e.id}
            actions={[
              {
                label: 'Documents',
                run: async (e) => setSelectedExam(e),
              },
            ]}
            columns={[
              { header: 'Exam date', cell: (e) => fmtDate(e.examDate) },
              { header: 'Facility', cell: (e) => e.facilityName || '—' },
              {
                header: 'Result',
                cell: (e) =>
                  e.result === 'Unfit' ? (
                    <Badge variant="destructive">Unfit</Badge>
                  ) : e.result === 'Fit' ? (
                    <Badge variant="secondary">Fit</Badge>
                  ) : (
                    <Badge variant="outline">{label(MEDICAL_EXAM_RESULT_OPTIONS, e.result)}</Badge>
                  ),
              },
              { header: 'Next due', cell: (e) => fmtDate(e.nextExamDueDate) },
            ]}
            schema={examSchema as any}
            emptyForm={emptyExam}
            toForm={(e) => ({
              ...emptyExam,
              examDate: e.examDate?.slice(0, 10) ?? '',
              result: e.result,
              nextExamDueDate: e.nextExamDueDate?.slice(0, 10) ?? '',
            })}
            renderFields={(form) => (
              <>
                <FieldRow>
                  <DateField form={form} name="examDate" label="Examination date" required />
                  <SelectField
                    form={form}
                    name="result"
                    label="Result"
                    required
                    options={MEDICAL_EXAM_RESULT_OPTIONS}
                  />
                </FieldRow>
                <FieldRow>
                  <SelectField
                    form={form}
                    name="facilityId"
                    label="Facility"
                    options={facilityOptions}
                    allowEmpty
                    emptyLabel="Not recorded"
                  />
                  <SelectField
                    form={form}
                    name="physicianId"
                    label="Examining physician"
                    options={physicianOptions}
                    allowEmpty
                    emptyLabel="Not recorded"
                  />
                </FieldRow>
                <FieldRow>
                  <NumberField form={form} name="heightCm" label="Height (cm)" />
                  <NumberField form={form} name="weightKg" label="Weight (kg)" />
                </FieldRow>
                <FieldRow>
                  <TextField form={form} name="bloodPressure" label="Blood pressure" placeholder="120/80" />
                  <DateField form={form} name="nextExamDueDate" label="Next examination due" />
                </FieldRow>
                <FieldRow>
                  <TextField form={form} name="visionResult" label="Vision" />
                  <TextField form={form} name="hearingResult" label="Hearing" />
                </FieldRow>
                <TextareaField form={form} name="findings" label="Findings" rows={2} />
                <TextareaField form={form} name="recommendations" label="Recommendations" rows={2} />
                <TextareaField
                  form={form}
                  name="restrictions"
                  label="Work restrictions"
                  rows={2}
                />
                <TextareaField form={form} name="notes" label="Notes" rows={2} />
              </>
            )}
          />

          {selectedExam && (
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <p className="text-sm text-muted-foreground">
                  Documents for the examination on {fmtDate(selectedExam.examDate)}
                </p>
                <Button variant="ghost" size="sm" onClick={() => setSelectedExam(null)}>
                  Close
                </Button>
              </div>
              <AttachmentsPanel
                title="Examination documents"
                note="Uploaded through the controlled gate — scanned and stored outside the web root. Only medical permission holders can retrieve them."
                queryKey={['hr', 'medical-exam-documents', selectedExam.id]}
                list={() => medicalHealthService.getExamDocuments(selectedExam.id)}
                upload={(file, description) =>
                  medicalHealthService.uploadExamDocument(selectedExam.id, file, description)
                }
                download={(doc) => medicalHealthService.downloadExamDocument(doc)}
                remove={(docId) => medicalHealthService.removeExamDocument(docId)}
                emptyDescription="No documents attached to this examination yet."
              />
            </div>
          )}
        </TabsContent>
      </Tabs>
    </div>
  );
}

function Detail({ label: title, value }: { label: string; value?: string | null }) {
  return (
    <div>
      <p className="text-sm text-muted-foreground">{title}</p>
      <p className="text-sm">{value || '—'}</p>
    </div>
  );
}
