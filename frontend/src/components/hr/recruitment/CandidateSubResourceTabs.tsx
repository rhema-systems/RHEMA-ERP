'use client';

import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import {
  DateField,
  FieldRow,
  NumberField,
  SelectField,
  SwitchField,
  TextField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { qualificationService } from '@/services/hr/lookup.service';
import { skillService } from '@/services/hr/skill.service';
import { jobCandidateService } from '@/services/hr/recruitment-pipeline.service';
import {
  PROFICIENCY_LEVELS,
  QUALIFICATION_TYPES,
  type CandidateInterest,
  type CandidateQualification,
  type CandidateReferee,
  type CandidateSkill,
  type CandidateWorkHistory,
} from '@/types/hr/recruitment-pipeline';

/**
 * The five list-shaped collections on a candidate record.
 *
 * All five ride `ResourceCollectionTab` — the same table-plus-dialog used by the 13 employee
 * sub-resource tabs. Documents and notes are not here: documents need the multipart upload gate and
 * notes carry a privacy decision, so both have their own panels.
 *
 * ⚠ None of these send a `jobCandidateId`. The server takes the candidate from the route and
 * overwrites whatever the body carried, so sending one would be decorative at best.
 */

// ── qualifications ─────────────────────────────────────────────────────────

const qualificationSchema = z.object({
  qualificationType: z.string().min(1, 'Type is required'),
  qualificationId: z.string().optional().nullable(),
  qualificationFreeText: z.string().max(200).optional().nullable(),
  institution: z.string().min(1, 'Institution is required').max(200),
  dateAwarded: z.string().min(1, 'Date awarded is required'),
  grade: z.string().max(100).optional().nullable(),
});
type QualificationForm = z.infer<typeof qualificationSchema>;

export function CandidateQualificationsTab({ candidateId }: { candidateId: string }) {
  const catalogue = useQuery({
    queryKey: ['hr', 'qualifications', 'active'],
    queryFn: () => qualificationService.getActive(),
  });

  return (
    <ResourceCollectionTab<CandidateQualification, QualificationForm>
      parentId={candidateId}
      title="qualifications"
      singular="qualification"
      queryKey={['hr', 'candidate-qualifications', candidateId]}
      invalidateKeys={[['hr', 'candidate-detail', candidateId]]}
      list={(id) => jobCandidateService.getQualifications(id)}
      create={(id, values) => jobCandidateService.addQualification(id, normaliseQualification(values))}
      update={(id, qid, values) =>
        jobCandidateService.updateQualification(id, qid, normaliseQualification(values))
      }
      remove={(_id, qid) => jobCandidateService.deleteQualification(qid)}
      getId={(q) => q.id}
      columns={[
        { header: 'Qualification', cell: (q) => q.qualificationName || '—' },
        { header: 'Type', cell: (q) => humanizeEnum(q.qualificationType) },
        { header: 'Institution', cell: (q) => q.institution },
        { header: 'Awarded', cell: (q) => formatDate(q.dateAwarded) },
        { header: 'Grade', cell: (q) => q.grade ?? '—' },
      ]}
      schema={qualificationSchema}
      emptyForm={{
        qualificationType: 'Education',
        qualificationId: null,
        qualificationFreeText: null,
        institution: '',
        dateAwarded: '',
        grade: null,
      }}
      toForm={(q) => ({
        qualificationType: q.qualificationType,
        qualificationId: q.qualificationId ?? null,
        // The read DTO collapses catalogue and free-text into one name, so an award that is not in
        // the catalogue comes back only as `qualificationName`.
        qualificationFreeText: q.qualificationId ? null : q.qualificationName,
        institution: q.institution,
        dateAwarded: q.dateAwarded,
        grade: q.grade ?? null,
      })}
      dialogHint="Pick from the qualifications lookup, or name the award if it is not listed."
      renderFields={(form) => (
        <div className="space-y-4">
          <FieldRow>
            <SelectField
              form={form}
              name="qualificationType"
              label="Type"
              required
              options={QUALIFICATION_TYPES.map((t) => ({ value: t, label: humanizeEnum(t) }))}
            />
            <SelectField
              form={form}
              name="qualificationId"
              label="From the lookup"
              allowEmpty
              emptyLabel="Not listed"
              options={(catalogue.data ?? []).map((q: any) => ({ value: q.id, label: q.name }))}
            />
          </FieldRow>
          <TextField
            form={form}
            name="qualificationFreeText"
            label="Qualification name (if not listed)"
          />
          <FieldRow>
            <TextField form={form} name="institution" label="Institution" required />
            <DateField form={form} name="dateAwarded" label="Date awarded" required />
          </FieldRow>
          <TextField form={form} name="grade" label="Grade / class" />
        </div>
      )}
    />
  );
}

function normaliseQualification(values: QualificationForm) {
  return {
    ...values,
    qualificationType: values.qualificationType as CandidateQualification['qualificationType'],
    qualificationId: values.qualificationId || null,
    qualificationFreeText: values.qualificationFreeText || null,
    grade: values.grade || null,
  };
}

// ── work history ───────────────────────────────────────────────────────────

const workHistorySchema = z
  .object({
    institutionName: z.string().min(1, 'Employer is required').max(200),
    positionHeld: z.string().min(1, 'Position is required').max(200),
    startDate: z.string().min(1, 'Start date is required'),
    endDate: z.string().optional().nullable(),
    responsibilities: z.string().max(4000).optional().nullable(),
    reasonForLeaving: z.string().max(200).optional().nullable(),
  })
  .refine((v) => !v.endDate || v.endDate >= v.startDate, {
    message: 'End date cannot be before the start date',
    path: ['endDate'],
  });
type WorkHistoryForm = z.infer<typeof workHistorySchema>;

export function CandidateWorkHistoryTab({ candidateId }: { candidateId: string }) {
  return (
    <ResourceCollectionTab<CandidateWorkHistory, WorkHistoryForm>
      parentId={candidateId}
      title="work history"
      singular="role"
      queryKey={['hr', 'candidate-work-history', candidateId]}
      invalidateKeys={[['hr', 'candidate-detail', candidateId]]}
      list={(id) => jobCandidateService.getWorkHistory(id)}
      create={(id, values) => jobCandidateService.addWorkHistory(id, cleanWorkHistory(values))}
      update={(id, wid, values) => jobCandidateService.updateWorkHistory(id, wid, cleanWorkHistory(values))}
      remove={(_id, wid) => jobCandidateService.deleteWorkHistory(wid)}
      getId={(w) => w.id}
      columns={[
        { header: 'Employer', cell: (w) => w.institutionName },
        { header: 'Position', cell: (w) => w.positionHeld },
        {
          header: 'Period',
          cell: (w) => `${formatDate(w.startDate)} – ${w.endDate ? formatDate(w.endDate) : 'present'}`,
        },
        { header: 'Reason for leaving', cell: (w) => w.reasonForLeaving ?? '—' },
      ]}
      schema={workHistorySchema}
      emptyForm={{
        institutionName: '',
        positionHeld: '',
        startDate: '',
        endDate: null,
        responsibilities: null,
        reasonForLeaving: null,
      }}
      toForm={(w) => ({
        institutionName: w.institutionName,
        positionHeld: w.positionHeld,
        startDate: w.startDate,
        endDate: w.endDate ?? null,
        responsibilities: w.responsibilities ?? null,
        reasonForLeaving: w.reasonForLeaving ?? null,
      })}
      dialogHint="Leave the end date blank for the role the candidate is in now."
      renderFields={(form) => (
        <div className="space-y-4">
          <FieldRow>
            <TextField form={form} name="institutionName" label="Employer" required />
            <TextField form={form} name="positionHeld" label="Position" required />
          </FieldRow>
          <FieldRow>
            <DateField form={form} name="startDate" label="Start date" required />
            <DateField form={form} name="endDate" label="End date" />
          </FieldRow>
          <TextareaField form={form} name="responsibilities" label="Responsibilities" />
          <TextField form={form} name="reasonForLeaving" label="Reason for leaving" />
        </div>
      )}
    />
  );
}

function cleanWorkHistory(values: WorkHistoryForm) {
  return {
    ...values,
    endDate: values.endDate || null,
    responsibilities: values.responsibilities || null,
    reasonForLeaving: values.reasonForLeaving || null,
  };
}

// ── referees ───────────────────────────────────────────────────────────────

const refereeSchema = z.object({
  fullName: z.string().min(1, 'Name is required').max(200),
  position: z.string().min(1, 'Position is required').max(200),
  organization: z.string().min(1, 'Organization is required').max(200),
  email: z.string().min(1, 'Email is required').email('Enter a valid email'),
  phone: z.string().min(1, 'Phone is required').max(20),
  relationship: z.string().min(1, 'Relationship is required').max(100),
  yearsKnown: z.coerce.number().int().min(0).max(60),
});
type RefereeForm = z.infer<typeof refereeSchema>;

export function CandidateRefereesTab({ candidateId }: { candidateId: string }) {
  return (
    <ResourceCollectionTab<CandidateReferee, RefereeForm>
      parentId={candidateId}
      title="referees"
      singular="referee"
      queryKey={['hr', 'candidate-referees', candidateId]}
      invalidateKeys={[['hr', 'candidate-detail', candidateId]]}
      list={(id) => jobCandidateService.getReferees(id)}
      create={(id, values) => jobCandidateService.addReferee(id, values)}
      update={(id, rid, values) => jobCandidateService.updateReferee(id, rid, values)}
      remove={(_id, rid) => jobCandidateService.deleteReferee(rid)}
      getId={(r) => r.id}
      columns={[
        { header: 'Name', cell: (r) => r.fullName },
        { header: 'Position', cell: (r) => r.position },
        { header: 'Organization', cell: (r) => r.organization },
        { header: 'Relationship', cell: (r) => r.relationship },
        { header: 'Years known', cell: (r) => r.yearsKnown },
        {
          header: 'Contact',
          cell: (r) => (
            <span className="text-xs text-muted-foreground">
              {r.email}
              <br />
              {r.phone}
            </span>
          ),
        },
      ]}
      schema={refereeSchema}
      emptyForm={{
        fullName: '',
        position: '',
        organization: '',
        email: '',
        phone: '',
        relationship: '',
        yearsKnown: 0,
      }}
      toForm={(r) => ({
        fullName: r.fullName,
        position: r.position,
        organization: r.organization,
        email: r.email,
        phone: r.phone,
        relationship: r.relationship,
        yearsKnown: r.yearsKnown,
      })}
      renderFields={(form) => (
        <div className="space-y-4">
          <FieldRow>
            <TextField form={form} name="fullName" label="Full name" required />
            <TextField form={form} name="position" label="Position" required />
          </FieldRow>
          <FieldRow>
            <TextField form={form} name="organization" label="Organization" required />
            <TextField form={form} name="relationship" label="Relationship" required />
          </FieldRow>
          <FieldRow>
            <TextField form={form} name="email" label="Email" type="email" required />
            <TextField form={form} name="phone" label="Phone" type="tel" required />
          </FieldRow>
          <NumberField form={form} name="yearsKnown" label="Years known" />
        </div>
      )}
    />
  );
}

// ── skills ─────────────────────────────────────────────────────────────────

const skillSchema = z.object({
  skillId: z.string().optional().nullable(),
  skillName: z.string().min(1, 'Skill is required').max(200),
  proficiency: z.string().optional().nullable(),
  yearsOfExperience: z.coerce.number().int().min(0).max(50).optional().nullable(),
  isCertified: z.boolean(),
  certificationName: z.string().max(200).optional().nullable(),
});
type SkillForm = z.infer<typeof skillSchema>;

export function CandidateSkillsTab({ candidateId }: { candidateId: string }) {
  const catalogue = useQuery({
    queryKey: ['hr', 'skills', 'active'],
    queryFn: () => skillService.getActive(),
  });

  return (
    <ResourceCollectionTab<CandidateSkill, SkillForm>
      parentId={candidateId}
      title="skills"
      singular="skill"
      queryKey={['hr', 'candidate-skills', candidateId]}
      invalidateKeys={[['hr', 'candidate-detail', candidateId]]}
      list={(id) => jobCandidateService.getSkills(id)}
      create={(id, values) => jobCandidateService.addSkill(id, cleanSkill(values))}
      update={(id, sid, values) => jobCandidateService.updateSkill(id, sid, cleanSkill(values))}
      remove={(_id, sid) => jobCandidateService.deleteSkill(sid)}
      getId={(s) => s.id}
      columns={[
        { header: 'Skill', cell: (s) => s.skillCatalogueName ?? s.skillName },
        { header: 'Proficiency', cell: (s) => (s.proficiency ? humanizeEnum(s.proficiency) : '—') },
        { header: 'Years', cell: (s) => s.yearsOfExperience ?? '—' },
        {
          header: 'Certified',
          cell: (s) =>
            s.isCertified ? <StatusBadge status={s.certificationName || 'Active'} /> : '—',
        },
      ]}
      schema={skillSchema}
      emptyForm={{
        skillId: null,
        skillName: '',
        proficiency: null,
        yearsOfExperience: null,
        isCertified: false,
        certificationName: null,
      }}
      toForm={(s) => ({
        skillId: s.skillId ?? null,
        skillName: s.skillName,
        proficiency: s.proficiency ?? null,
        yearsOfExperience: s.yearsOfExperience ?? null,
        isCertified: s.isCertified,
        certificationName: s.certificationName ?? null,
      })}
      dialogHint="The free-text name is always stored; linking to the skills catalogue is optional."
      renderFields={(form) => (
        <div className="space-y-4">
          <FieldRow>
            <TextField form={form} name="skillName" label="Skill" required />
            <SelectField
              form={form}
              name="skillId"
              label="Catalogue skill"
              allowEmpty
              emptyLabel="Not listed"
              options={(catalogue.data ?? []).map((s: any) => ({ value: s.id, label: s.name }))}
            />
          </FieldRow>
          <FieldRow>
            <SelectField
              form={form}
              name="proficiency"
              label="Proficiency"
              allowEmpty
              options={PROFICIENCY_LEVELS.map((p) => ({ value: p, label: humanizeEnum(p) }))}
            />
            <NumberField form={form} name="yearsOfExperience" label="Years of experience" />
          </FieldRow>
          <SwitchField form={form} name="isCertified" label="Certified" />
          <TextField form={form} name="certificationName" label="Certification name" />
        </div>
      )}
    />
  );
}

function cleanSkill(values: SkillForm) {
  return {
    ...values,
    skillId: values.skillId || null,
    proficiency: (values.proficiency || null) as CandidateSkill['proficiency'],
    yearsOfExperience: values.yearsOfExperience ?? null,
    certificationName: values.certificationName || null,
  };
}

// ── interests ──────────────────────────────────────────────────────────────

const interestSchema = z.object({
  detail: z.string().min(1, 'Say what the interest is').max(500),
});
type InterestForm = z.infer<typeof interestSchema>;

/** ⚠ No update endpoint — interests are added and removed, never edited in place. */
export function CandidateInterestsTab({ candidateId }: { candidateId: string }) {
  return (
    <ResourceCollectionTab<CandidateInterest, InterestForm>
      parentId={candidateId}
      title="interests"
      singular="interest"
      queryKey={['hr', 'candidate-interests', candidateId]}
      invalidateKeys={[['hr', 'candidate-detail', candidateId]]}
      list={(id) => jobCandidateService.getInterests(id)}
      create={(id, values) => jobCandidateService.addInterest(id, values.detail)}
      update={async () => undefined}
      allowUpdate={false}
      remove={(_id, iid) => jobCandidateService.deleteInterest(iid)}
      getId={(i) => i.id}
      columns={[{ header: 'Interest', cell: (i) => i.detail }]}
      schema={interestSchema}
      emptyForm={{ detail: '' }}
      toForm={(i) => ({ detail: i.detail })}
      renderFields={(form) => <TextareaField form={form} name="detail" label="Interest" />}
    />
  );
}
