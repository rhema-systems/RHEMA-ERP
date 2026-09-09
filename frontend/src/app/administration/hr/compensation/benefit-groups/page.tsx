'use client';

/**
 * Benefit groups — a named bundle of benefit policies attached to a position in one move
 * (demo feedback round 2, lane C3; register row P-3; plan § 6.4).
 *
 * ⚠ A member carries the policy and NOTHING else: no amount, no expiry. A post that needs its own
 * figure for a benefit takes that benefit individually instead, and the position form refuses to
 * hold it both ways (plan Q-5, and the duplicate rule of § 6.4.2).
 */

import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { SelectField } from '@/components/hr/employee/tabs/fields';
import {
  NamedSetPage,
  toNamedSetPayload,
  type NamedSetForm,
} from '@/components/hr/named-sets/NamedSetPage';
import { namedSetService } from '@/services/hr/named-set.service';
import { benefitPolicyService } from '@/services/hr/benefits.service';
import type { BenefitGroup, BenefitGroupMember } from '@/types/hr/named-sets';

const memberSchema = z.object({ policyId: z.string().min(1, 'Choose a benefit') });
type MemberForm = z.input<typeof memberSchema>;

export default function BenefitGroupsPage() {
  const { data: policies } = useQuery({
    queryKey: ['hr', 'benefit-policies', 'active'],
    queryFn: () => benefitPolicyService.getActive(),
  });

  const policyOptions = useMemo(
    () =>
      (policies ?? []).map((p) => ({
        value: p.id,
        label: p.policyCode ? `${p.policyName} (${p.policyCode})` : p.policyName,
      })),
    [policies],
  );

  return (
    <NamedSetPage<BenefitGroup>
      title="Benefit groups"
      description="A named bundle of benefits, attached to a position in one move instead of one at a time."
      singular="benefit group"
      memberNoun="benefit"
      queryKey={['hr', 'benefit-groups']}
      list={() => namedSetService.getBenefitGroups()}
      create={(v: NamedSetForm) => namedSetService.createBenefitGroup(toNamedSetPayload(v))}
      update={(id, v: NamedSetForm) => namedSetService.updateBenefitGroup(id, toNamedSetPayload(v))}
      remove={(id) => namedSetService.removeBenefitGroup(id)}
      renderMembers={(group) => (
        <ResourceCollectionTab<BenefitGroupMember, MemberForm>
          parentId={group.id}
          title={`benefits in ${group.name}`}
          singular="benefit"
          queryKey={['hr', 'benefit-groups', group.id, 'members']}
          invalidateKeys={[['hr', 'benefit-groups']]}
          list={(id) => namedSetService.getBenefitGroupMembers(id)}
          create={(id, v) => namedSetService.addBenefitGroupMember(id, { policyId: v.policyId })}
          update={(id, memberId, v) =>
            namedSetService.updateBenefitGroupMember(id, memberId, { policyId: v.policyId })
          }
          remove={(id, memberId) => namedSetService.removeBenefitGroupMember(id, memberId)}
          getId={(m) => m.id}
          columns={[
            {
              header: 'Benefit',
              cell: (m) => (
                <div>
                  <div className="font-medium">{m.policyName}</div>
                  {m.policyCode && <div className="text-xs text-muted-foreground">{m.policyCode}</div>}
                </div>
              ),
            },
            {
              header: 'Status',
              cell: (m) =>
                m.policyIsActive ? (
                  <Badge variant="outline">Active</Badge>
                ) : (
                  <Badge variant="secondary">Retired benefit</Badge>
                ),
            },
          ]}
          schema={memberSchema as never}
          emptyForm={{ policyId: '' }}
          toForm={(m) => ({ policyId: m.policyId })}
          renderFields={(form) => (
            <SelectField form={form} name="policyId" label="Benefit" required options={policyOptions} />
          )}
        />
      )}
    />
  );
}
