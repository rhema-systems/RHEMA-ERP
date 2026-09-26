'use client';

/**
 * Skill sets — a named bundle of skills attached to a position in one move
 * (demo feedback round 2, lane C3; register row S-3; plan § 6.4).
 *
 * A member carries the same three things the individual position row carries — required level,
 * required or preferred, priority — so attaching the set is exactly equivalent to attaching its
 * rows one at a time. That equivalence is what lets the position form refuse an individual row a
 * set already provides.
 *
 * ⚠ Where a post ends up requiring the same skill from two sources, the STRONGEST wins: the highest
 * required level, and required beats preferred. The effective read on the position shows the result
 * and names every source.
 */

import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { SelectField, SwitchField, NumberField, FieldRow } from '@/components/hr/employee/tabs/fields';
import {
  NamedSetPage,
  toNamedSetPayload,
  type NamedSetForm,
} from '@/components/hr/named-sets/NamedSetPage';
import { namedSetService } from '@/services/hr/named-set.service';
import { skillService } from '@/services/hr/skill.service';
import { SKILL_LEVEL_OPTIONS, type SkillLevel } from '@/types/hr/position';
import type { SkillSet, SkillSetMember } from '@/types/hr/named-sets';

const memberSchema = z.object({
  skillId: z.string().min(1, 'Choose a skill'),
  requiredLevel: z.enum(['Beginner', 'Intermediate', 'Advanced', 'Expert', 'Master']),
  isRequired: z.boolean(),
  priority: z.string().optional(),
});
type MemberForm = z.input<typeof memberSchema>;

const empty: MemberForm = { skillId: '', requiredLevel: 'Beginner', isRequired: true, priority: '1' };

const toPayload = (v: MemberForm) => {
  const parsed = memberSchema.parse(v);
  return {
    skillId: parsed.skillId,
    requiredLevel: parsed.requiredLevel as SkillLevel,
    isRequired: parsed.isRequired,
    priority: parsed.priority && parsed.priority.trim() ? Number(parsed.priority) : 1,
  };
};

export default function SkillSetsPage() {
  const { data: skills } = useQuery({
    queryKey: ['hr', 'skills', 'active'],
    queryFn: () => skillService.getActive(),
  });

  const skillOptions = useMemo(
    () =>
      (skills ?? []).map((s) => ({
        value: s.id,
        label: s.category ? `${s.name} (${s.category})` : s.name,
      })),
    [skills],
  );

  return (
    <NamedSetPage<SkillSet>
      title="Skill sets"
      description="A named bundle of skills, attached to a position in one move instead of one at a time."
      singular="skill set"
      memberNoun="skill"
      queryKey={['hr', 'skill-sets']}
      list={() => namedSetService.getSkillSets()}
      create={(v: NamedSetForm) => namedSetService.createSkillSet(toNamedSetPayload(v))}
      update={(id, v: NamedSetForm) => namedSetService.updateSkillSet(id, toNamedSetPayload(v))}
      remove={(id) => namedSetService.removeSkillSet(id)}
      renderMembers={(set) => (
        <ResourceCollectionTab<SkillSetMember, MemberForm>
          parentId={set.id}
          title={`skills in ${set.name}`}
          singular="skill"
          queryKey={['hr', 'skill-sets', set.id, 'members']}
          invalidateKeys={[['hr', 'skill-sets']]}
          list={(id) => namedSetService.getSkillSetMembers(id)}
          create={(id, v) => namedSetService.addSkillSetMember(id, toPayload(v))}
          update={(id, memberId, v) => namedSetService.updateSkillSetMember(id, memberId, toPayload(v))}
          remove={(id, memberId) => namedSetService.removeSkillSetMember(id, memberId)}
          getId={(m) => m.id}
          columns={[
            {
              header: 'Skill',
              cell: (m) => (
                <div>
                  <div className="font-medium">{m.skillName}</div>
                  {m.skillCategory && <div className="text-xs text-muted-foreground">{m.skillCategory}</div>}
                </div>
              ),
            },
            { header: 'Level', cell: (m) => <Badge variant="outline">{m.requiredLevel}</Badge> },
            {
              header: 'Weight',
              cell: (m) => (
                <span className="text-xs text-muted-foreground">
                  {m.isRequired ? 'Required' : 'Preferred'} · priority {m.priority}
                </span>
              ),
            },
          ]}
          schema={memberSchema as never}
          emptyForm={empty}
          toForm={(m) => ({
            skillId: m.skillId,
            requiredLevel: m.requiredLevel,
            isRequired: m.isRequired,
            priority: String(m.priority),
          })}
          renderFields={(form) => (
            <>
              <SelectField form={form} name="skillId" label="Skill" required options={skillOptions} />
              <FieldRow>
                <SelectField
                  form={form}
                  name="requiredLevel"
                  label="Required level"
                  required
                  options={SKILL_LEVEL_OPTIONS}
                />
                <NumberField form={form} name="priority" label="Priority" />
              </FieldRow>
              <SwitchField
                form={form}
                name="isRequired"
                label="Required"
                description="Off means preferred — the post is better with it, but it is not a bar."
              />
            </>
          )}
        />
      )}
    />
  );
}
