'use client';

import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Badge } from '@/components/ui/badge';
import { skillService } from '@/services/hr/skill.service';
import { referenceDimensionService } from '@/services/hr/lookup.service';
import { employeeService } from '@/services/hr/employee.service';
import { SKILL_LEVEL_OPTIONS } from '@/types/hr/position';
import type { EmployeeSkill } from '@/types/hr/employee-subresources';
import { EmployeeSubResourceTab } from './EmployeeSubResourceTab';
import { DateField, FieldRow, SelectField, TextField, TextareaField } from './fields';

const schema = z.object({
  skillId: z.string().min(1, 'Select a skill'),
  skillLevel: z.enum(['Beginner', 'Intermediate', 'Advanced', 'Expert', 'Master']),
  acquiredDate: z.string().optional().or(z.literal('')),
  certificationDate: z.string().optional().or(z.literal('')),
  certificationExpiryDate: z.string().optional().or(z.literal('')),
  certificationNumber: z.string().max(100).optional().or(z.literal('')),
  /**
   * The catalogued certifier. Empty means "not one of ours".
   *
   * ⚠ Kept ALONGSIDE the free-text field below rather than replacing it. Existing rows are free
   * text, and a genuinely one-off certifier does not deserve a catalogue row — the catalogue makes
   * the common case consistent, the text box carries the tail.
   */
  certifyingBodyId: z.string().optional().or(z.literal('')),
  certifyingBody: z.string().max(200).optional().or(z.literal('')),
  notes: z.string().max(1000).optional().or(z.literal('')),
});

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  skillId: '',
  skillLevel: 'Beginner',
  acquiredDate: '',
  certificationDate: '',
  certificationExpiryDate: '',
  certificationNumber: '',
  certifyingBodyId: '',
  certifyingBody: '',
  notes: '',
};

export function SkillsTab({ employeeId }: { employeeId: string }) {
  const { data: skills } = useQuery({
    queryKey: ['hr', 'skills', 'active'],
    queryFn: () => skillService.getActive(),
  });

  const skillOptions = (skills ?? []).map((s) => ({
    value: s.id,
    label: s.category ? `${s.name} (${s.category})` : s.name,
  }));

  // Active bodies only — a retired one stops being offered on new rows, which is what retiring it
  // means. A skill already citing a retired body keeps it: the value is on the record.
  const { data: bodies } = useQuery({
    queryKey: ['hr', 'certifying-bodies', 'active'],
    queryFn: () => referenceDimensionService.getCertifyingBodies(true),
  });
  const bodyOptions = (bodies ?? []).map((b) => ({
    value: b.id,
    label: b.abbreviation ? `${b.name} (${b.abbreviation})` : b.name,
  }));

  return (
    <EmployeeSubResourceTab<EmployeeSkill, FormValues>
      employeeId={employeeId}
      title="skills"
      singular="skill"
      queryKey="skills"
      getId={(s) => s.id}
      list={employeeService.getEmployeeSkills.bind(employeeService)}
      create={(id, v) =>
        employeeService.addEmployeeSkill(id, {
          employeeId: id,
          skillId: v.skillId,
          skillLevel: v.skillLevel,
          acquiredDate: v.acquiredDate || null,
          certificationDate: v.certificationDate || null,
          certificationExpiryDate: v.certificationExpiryDate || null,
          certificationNumber: v.certificationNumber || null,
          certifyingBodyId: v.certifyingBodyId || null,
          certifyingBody: v.certifyingBody || null,
          notes: v.notes || null,
        })
      }
      // The update DTO does not accept skillId — the skill itself is fixed once added.
      update={(id, skillRowId, v) =>
        employeeService.updateEmployeeSkill(id, skillRowId, {
          id: skillRowId,
          skillLevel: v.skillLevel,
          acquiredDate: v.acquiredDate || null,
          certificationDate: v.certificationDate || null,
          certificationExpiryDate: v.certificationExpiryDate || null,
          certificationNumber: v.certificationNumber || null,
          // Sent even when empty: the service applies this one unconditionally so a wrongly
          // chosen body can be cleared. Every other field here treats null as "unchanged".
          certifyingBodyId: v.certifyingBodyId || null,
          certifyingBody: v.certifyingBody || null,
          notes: v.notes || null,
        })
      }
      remove={employeeService.removeEmployeeSkill.bind(employeeService)}
      actions={[
        {
          label: (s) => (s.isVerified ? 'Mark unverified' : 'Mark verified'),
          run: (s) =>
            s.isVerified
              ? employeeService.unverifyEmployeeSkill(employeeId, s.id)
              : employeeService.verifyEmployeeSkill(employeeId, s.id),
        },
      ]}
      columns={[
        { header: 'Skill', cell: (s) => s.skillName },
        { header: 'Category', cell: (s) => s.skillCategory || '—' },
        { header: 'Level', cell: (s) => s.skillLevel },
        {
          header: 'Certifying body',
          // The catalogued name where there is one, the free text otherwise. Showing only the
          // former would blank every row recorded before the catalogue existed.
          cell: (s) => s.certifyingBodyName || s.certifyingBody || '—',
        },
        {
          header: 'Certification expiry',
          cell: (s) =>
            s.certificationExpiryDate ? (
              <span className={s.isCertificationExpired ? 'text-red-600' : undefined}>
                {s.certificationExpiryDate.slice(0, 10)}
              </span>
            ) : (
              '—'
            ),
        },
        {
          header: 'Status',
          cell: (s) => (
            <div className="flex gap-1">
              {s.isVerified && <Badge variant="secondary">Verified</Badge>}
              {s.isCertificationExpired && <Badge variant="outline">Expired</Badge>}
            </div>
          ),
        },
      ]}
      schema={schema}
      emptyForm={empty}
      toForm={(s) => ({
        skillId: s.skillId,
        skillLevel: s.skillLevel,
        acquiredDate: s.acquiredDate?.slice(0, 10) ?? '',
        certificationDate: s.certificationDate?.slice(0, 10) ?? '',
        certificationExpiryDate: s.certificationExpiryDate?.slice(0, 10) ?? '',
        certificationNumber: s.certificationNumber ?? '',
        certifyingBodyId: s.certifyingBodyId ?? '',
        certifyingBody: s.certifyingBody ?? '',
        notes: s.notes ?? '',
      })}
      renderFields={(form) => (
        <>
          <SelectField
            form={form}
            name="skillId"
            label="Skill"
            required
            options={skillOptions}
          />
          <FieldRow>
            <SelectField
              form={form}
              name="skillLevel"
              label="Proficiency"
              required
              options={SKILL_LEVEL_OPTIONS}
            />
            <DateField form={form} name="acquiredDate" label="Acquired" />
          </FieldRow>
          <FieldRow>
            <DateField form={form} name="certificationDate" label="Certified on" />
            <DateField form={form} name="certificationExpiryDate" label="Certification expires" />
          </FieldRow>
          <FieldRow>
            <TextField form={form} name="certificationNumber" label="Certification number" />
            <SelectField
              form={form}
              name="certifyingBodyId"
              label="Certifying body"
              options={bodyOptions}
              allowEmpty
              emptyLabel="Not in the catalogue"
            />
          </FieldRow>
          <TextField
            form={form}
            name="certifyingBody"
            label="Certifying body (if not catalogued)"
          />
          <TextareaField form={form} name="notes" label="Notes" />
        </>
      )}
    />
  );
}
