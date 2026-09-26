'use client';

import { useEffect } from 'react';
import type { UseFormReturn } from 'react-hook-form';
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
import { RelationshipField } from '@/components/hr/employee/tabs/address-fields';
import { RELATIONSHIP_SCOPES } from '@/types/hr/relationship-type';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { qualificationService } from '@/services/hr/lookup.service';
import { languageService } from '@/services/hr/language.service';
import { skillService } from '@/services/hr/skill.service';
import { jobCandidateService } from '@/services/hr/recruitment-pipeline.service';
import {
  LANGUAGE_PROFICIENCIES,
  PROFICIENCY_LEVELS,
  QUALIFICATION_TYPES,
  type CandidateInterest,
  type CandidateLanguage,
  type CandidateLanguageForm,
  type CandidateQualification,
  type CandidateReferee,
  type CandidateSkill,
  type CandidateWorkHistory,
} from '@/types/hr/recruitment-pipeline';

/**
 * The six list-shaped collections on a candidate record (languages joined in round 3, lane C2).
 *
 * All five ride `ResourceCollectionTab` — the same table-plus-dialog used by the 13 employee
 * sub-resource tabs. Documents and notes are not here: documents need the multipart upload gate and
 * notes carry a privacy decision, so both have their own panels.
 *
 * ⚠ None of these send a `jobCandidateId`. The server takes the candidate from the route and
 * overwrites whatever the body carried, so sending one would be decorative at best.
 */

// ── qualifications ─────────────────────────────────────────────────────────

const qualificationSchema = z
  .object({
    qualificationType: z.string().min(1, 'Type is required'),
    qualificationId: z.string().optional().nullable(),
    qualificationFreeText: z.string().max(200).optional().nullable(),
    institution: z.string().min(1, 'Institution is required').max(200),
    dateAwarded: z.string().min(1, 'Date awarded is required'),
    grade: z.string().max(100).optional().nullable(),
  })
  .refine((v) => !!v.qualificationId || !!v.qualificationFreeText?.trim(), {
    message: 'Pick a qualification from the lookup or name it',
    path: ['qualificationFreeText'],
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
      dialogHint="Choose the kind first; the lookup narrows to it. Name the award only when it is not listed."
      renderFields={(form) => (
        <QualificationFields
          form={form}
          catalogue={(catalogue.data ?? []) as { id: string; name: string; type: string }[]}
        />
      )}
    />
  );
}

/**
 * Round 3, lane C2 (register row R-3c): the type drives the lookup — an Education row offers the
 * degrees and diplomas, a Certification row the certificates — and the free-text box appears only
 * when nothing listed fits. A catalogue row picked under one type is dropped when the type moves.
 */
function QualificationFields({
  form,
  catalogue,
}: {
  form: UseFormReturn<QualificationForm>;
  catalogue: { id: string; name: string; type: string }[];
}) {
  const type = form.watch('qualificationType');
  const qualificationId = form.watch('qualificationId') || '';
  // Experience and TechnicalSkills are not catalogue kinds; the whole list is offered for them.
  const typed = catalogue.filter((q) => q.type === type);
  const offered = typed.length > 0 ? typed : catalogue;
  const listed = !!qualificationId && offered.some((q) => q.id === qualificationId);

  useEffect(() => {
    if (qualificationId && !listed && catalogue.length > 0) {
      form.setValue('qualificationId', null, { shouldDirty: true });
    }
    // form is stable; re-running on every keystroke elsewhere is not wanted.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [qualificationId, listed, catalogue.length]);

  return (
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
          options={offered.map((q) => ({ value: q.id, label: q.name }))}
        />
      </FieldRow>
      {!listed && (
        <TextField form={form} name="qualificationFreeText" label="Qualification name" required />
      )}
      <FieldRow>
        <TextField form={form} name="institution" label="Institution" required />
        <DateField form={form} name="dateAwarded" label="Date awarded" required />
      </FieldRow>
      <TextField form={form} name="grade" label="Grade / class" />
    </div>
  );
}

function normaliseQualification(values: QualificationForm) {
  return {
    ...values,
    qualificationType: values.qualificationType as CandidateQualification['qualificationType'],
    qualificationId: values.qualificationId || null,
    // With a catalogue row the name comes from the row; typed text is only kept when there is none.
    qualificationFreeText: values.qualificationId ? null : values.qualificationFreeText || null,
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
  relationshipTypeId: z.string().optional().or(z.literal('')),
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
        relationshipTypeId: '',
        yearsKnown: 0,
      }}
      toForm={(r) => ({
        fullName: r.fullName,
        position: r.position,
        organization: r.organization,
        email: r.email,
        phone: r.phone,
        relationship: r.relationship,
        relationshipTypeId: r.relationshipTypeId ?? '',
        yearsKnown: r.yearsKnown,
      })}
      renderFields={(form) => (
        <div className="space-y-4">
          <FieldRow>
            <TextField form={form} name="fullName" label="Full name" required />
            <TextField form={form} name="position" label="Position" required />
          </FieldRow>
          <TextField form={form} name="organization" label="Organization" required />
          {/*
            ⚠ Professional and other only (round 2, lane D2). A candidate may name a pastor, a
            lecturer or a family friend; they may not name their mother, and the server refuses it.
          */}
          <RelationshipField
            form={form}
            typeIdName="relationshipTypeId"
            textName="relationship"
            categories={RELATIONSHIP_SCOPES.professionalReferee}
            required
          />
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
  certificationNumber: z.string().max(100).optional().nullable(),
  certifyingBody: z.string().max(200).optional().nullable(),
  certificationExpiryDate: z.string().optional().nullable(),
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
            s.isCertified ? (
              <span className="text-sm">
                <StatusBadge status={s.certificationName || 'Active'} />
                {(s.certificationNumber || s.certifyingBody) && (
                  <span className="ml-2 text-xs text-muted-foreground">
                    {[s.certificationNumber, s.certifyingBody].filter(Boolean).join(' · ')}
                  </span>
                )}
              </span>
            ) : (
              '—'
            ),
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
        certificationNumber: null,
        certifyingBody: null,
        certificationExpiryDate: null,
      }}
      toForm={(s) => ({
        skillId: s.skillId ?? null,
        skillName: s.skillName,
        proficiency: s.proficiency ?? null,
        yearsOfExperience: s.yearsOfExperience ?? null,
        isCertified: s.isCertified,
        certificationName: s.certificationName ?? null,
        certificationNumber: s.certificationNumber ?? null,
        certifyingBody: s.certifyingBody ?? null,
        certificationExpiryDate: s.certificationExpiryDate?.slice(0, 10) ?? null,
      })}
      dialogHint="Pick from the skills catalogue, or name the skill if it is not listed. Certificate details appear once the skill is marked certified."
      renderFields={(form) => (
        <SkillFields form={form} catalogue={(catalogue.data ?? []) as { id: string; name: string }[]} />
      )}
    />
  );
}

/**
 * Round 3, lane C2 (register row R-3d): a catalogue pick mirrors its name into the stored text
 * (the server keeps both), the free-text box is offered only when nothing listed fits, and the
 * four certificate fields appear only under the tick — the server clears them together anyway.
 */
function SkillFields({
  form,
  catalogue,
}: {
  form: UseFormReturn<SkillForm>;
  catalogue: { id: string; name: string }[];
}) {
  const skillId = form.watch('skillId') || '';
  const isCertified = form.watch('isCertified');
  const picked = catalogue.find((s) => s.id === skillId);

  useEffect(() => {
    if (!picked) return;
    if (form.getValues('skillName') !== picked.name) {
      form.setValue('skillName', picked.name, { shouldValidate: true, shouldDirty: true });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [picked?.id]);

  return (
    <div className="space-y-4">
      <FieldRow>
        <SelectField
          form={form}
          name="skillId"
          label="Skill (from the catalogue)"
          allowEmpty
          emptyLabel="Not listed"
          options={catalogue.map((s) => ({ value: s.id, label: s.name }))}
        />
        {picked ? (
          <div className="space-y-2 text-sm">
            <span className="text-xs text-muted-foreground">Stored as</span>
            <p className="pt-2 font-medium">{picked.name}</p>
          </div>
        ) : (
          <TextField form={form} name="skillName" label="Skill name" required />
        )}
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
      {isCertified && (
        <div className="space-y-4 rounded-md border bg-muted/30 p-3">
          <FieldRow>
            <TextField form={form} name="certificationName" label="Certification name" required />
            <TextField form={form} name="certificationNumber" label="Certificate number" />
          </FieldRow>
          <FieldRow>
            <TextField form={form} name="certifyingBody" label="Certifying body" />
            <DateField form={form} name="certificationExpiryDate" label="Expires" />
          </FieldRow>
        </div>
      )}
    </div>
  );
}

function cleanSkill(values: SkillForm) {
  const certified = values.isCertified;
  return {
    ...values,
    skillId: values.skillId || null,
    proficiency: (values.proficiency || null) as CandidateSkill['proficiency'],
    yearsOfExperience: values.yearsOfExperience ?? null,
    certificationName: certified ? values.certificationName || null : null,
    certificationNumber: certified ? values.certificationNumber || null : null,
    certifyingBody: certified ? values.certifyingBody || null : null,
    certificationExpiryDate: certified ? values.certificationExpiryDate || null : null,
  };
}

// ── languages ──────────────────────────────────────────────────────────────

const languageSchema = z
  .object({
    languageId: z.string().optional().nullable(),
    languageName: z.string().max(100).optional().nullable(),
    proficiency: z.string().min(1, 'Proficiency is required'),
  })
  .refine((v) => !!v.languageId || !!v.languageName?.trim(), {
    message: 'Pick a language from the catalogue or name it',
    path: ['languageName'],
  });
type LanguageForm = z.infer<typeof languageSchema>;

/**
 * Round 3, lane C2 (register row R-3e). HR had no view of a candidate's languages at all while the
 * Language criterion scored them; the door arrived in C1, this is the tab. A catalogue row or a
 * typed name — the server mirrors the name from the row.
 */
export function CandidateLanguagesTab({ candidateId }: { candidateId: string }) {
  const catalogue = useQuery({
    queryKey: ['hr', 'languages', 'active'],
    queryFn: () => languageService.getActive(),
  });

  return (
    <ResourceCollectionTab<CandidateLanguage, LanguageForm>
      parentId={candidateId}
      title="languages"
      singular="language"
      queryKey={['hr', 'candidate-languages', candidateId]}
      invalidateKeys={[['hr', 'candidate-detail', candidateId]]}
      list={(id) => jobCandidateService.getLanguages(id)}
      create={(id, values) => jobCandidateService.addLanguage(id, cleanLanguage(values))}
      update={(id, lid, values) => jobCandidateService.updateLanguage(id, lid, cleanLanguage(values))}
      remove={(_id, lid) => jobCandidateService.deleteLanguage(lid)}
      getId={(l) => l.id}
      columns={[
        {
          header: 'Language',
          cell: (l) => (
            <span>
              {l.languageName}
              {l.languageCode && <span className="ml-2 text-xs text-muted-foreground">{l.languageCode}</span>}
            </span>
          ),
        },
        { header: 'Proficiency', cell: (l) => humanizeEnum(l.proficiency) },
        { header: 'From the catalogue', cell: (l) => (l.languageId ? 'Yes' : 'Typed') },
      ]}
      schema={languageSchema}
      emptyForm={{ languageId: null, languageName: null, proficiency: 'ProfessionalWorking' }}
      toForm={(l) => ({
        languageId: l.languageId ?? null,
        languageName: l.languageId ? null : l.languageName,
        proficiency: l.proficiency,
      })}
      dialogHint="Pick from the language catalogue, or type the name if it is not listed."
      renderFields={(form) => (
        <LanguageFields form={form} catalogue={(catalogue.data ?? []).map((l) => ({ id: l.id, name: l.name }))} />
      )}
    />
  );
}

function LanguageFields({
  form,
  catalogue,
}: {
  form: UseFormReturn<LanguageForm>;
  catalogue: { id: string; name: string }[];
}) {
  const languageId = form.watch('languageId') || '';
  const picked = catalogue.find((l) => l.id === languageId);
  return (
    <div className="space-y-4">
      <FieldRow>
        <SelectField
          form={form}
          name="languageId"
          label="Language (from the catalogue)"
          allowEmpty
          emptyLabel="Not listed"
          options={catalogue.map((l) => ({ value: l.id, label: l.name }))}
        />
        {picked ? (
          <div className="space-y-2 text-sm">
            <span className="text-xs text-muted-foreground">Stored as</span>
            <p className="pt-2 font-medium">{picked.name}</p>
          </div>
        ) : (
          <TextField form={form} name="languageName" label="Language name" required />
        )}
      </FieldRow>
      <SelectField
        form={form}
        name="proficiency"
        label="Proficiency"
        required
        options={LANGUAGE_PROFICIENCIES.map((p) => ({ value: p, label: humanizeEnum(p) }))}
      />
    </div>
  );
}

function cleanLanguage(values: LanguageForm): CandidateLanguageForm {
  return {
    languageId: values.languageId || null,
    languageName: values.languageId ? null : values.languageName?.trim() || null,
    proficiency: values.proficiency as CandidateLanguageForm['proficiency'],
  };
}

// ── interests ──────────────────────────────────────────────────────────────

const interestSchema = z.object({
  detail: z.string().min(1, 'Say what the interest is').max(500),
});
type InterestForm = z.infer<typeof interestSchema>;

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
      update={(id, iid, values) => jobCandidateService.updateInterest(id, iid, values.detail)}
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
