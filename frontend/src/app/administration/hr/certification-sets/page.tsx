'use client';

/**
 * Certification sets — a named bundle of credentials attached to a position in one move
 * (demo feedback round 2, lane C3; plan § 1.5, § 6.4).
 *
 * The case for these is the regulator: "driver — licence class C, defensive driving, first aid";
 * "site engineer — professional registration, working at height, confined space". When the
 * regulator changes the bundle it is changed here once, and every post that holds the set follows.
 *
 * ⚠ Mandatory anywhere is mandatory: if one attached set marks a credential mandatory, the post
 * requires it, whatever another set says.
 */

import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { SelectField, SwitchField } from '@/components/hr/employee/tabs/fields';
import {
  NamedSetPage,
  toNamedSetPayload,
  type NamedSetForm,
} from '@/components/hr/named-sets/NamedSetPage';
import { namedSetService } from '@/services/hr/named-set.service';
import { certificationService } from '@/services/hr/certification.service';
import type { CertificationSet, CertificationSetMember } from '@/types/hr/named-sets';

const memberSchema = z.object({
  certificationId: z.string().min(1, 'Choose a credential'),
  isMandatory: z.boolean(),
});
type MemberForm = z.input<typeof memberSchema>;

const empty: MemberForm = { certificationId: '', isMandatory: true };

export default function CertificationSetsPage() {
  const { data: certifications } = useQuery({
    queryKey: ['hr', 'certifications', 'active'],
    queryFn: () => certificationService.getAll({ activeOnly: true }),
  });

  const certificationOptions = useMemo(
    () =>
      (certifications ?? []).map((c) => ({
        value: c.id,
        label: `${c.name} — ${c.certifyingBodyAbbreviation || c.certifyingBodyName}`,
      })),
    [certifications],
  );

  return (
    <NamedSetPage<CertificationSet>
      title="Certification sets"
      description="A named bundle of credentials, attached to a position in one move instead of one at a time."
      singular="certification set"
      memberNoun="credential"
      queryKey={['hr', 'certification-sets']}
      list={() => namedSetService.getCertificationSets()}
      create={(v: NamedSetForm) => namedSetService.createCertificationSet(toNamedSetPayload(v))}
      update={(id, v: NamedSetForm) => namedSetService.updateCertificationSet(id, toNamedSetPayload(v))}
      remove={(id) => namedSetService.removeCertificationSet(id)}
      renderMembers={(set) => (
        <ResourceCollectionTab<CertificationSetMember, MemberForm>
          parentId={set.id}
          title={`credentials in ${set.name}`}
          singular="credential"
          queryKey={['hr', 'certification-sets', set.id, 'members']}
          invalidateKeys={[['hr', 'certification-sets']]}
          list={(id) => namedSetService.getCertificationSetMembers(id)}
          create={(id, v) =>
            namedSetService.addCertificationSetMember(id, {
              certificationId: v.certificationId,
              isMandatory: v.isMandatory,
            })
          }
          update={(id, memberId, v) =>
            namedSetService.updateCertificationSetMember(id, memberId, {
              certificationId: v.certificationId,
              isMandatory: v.isMandatory,
            })
          }
          remove={(id, memberId) => namedSetService.removeCertificationSetMember(id, memberId)}
          getId={(m) => m.id}
          columns={[
            {
              header: 'Credential',
              cell: (m) => (
                <div>
                  <div className="font-medium">{m.certificationName}</div>
                  <div className="text-xs text-muted-foreground">{m.certifyingBodyName}</div>
                </div>
              ),
            },
            {
              header: 'Weight',
              cell: (m) =>
                m.isMandatory ? <Badge variant="outline">Mandatory</Badge> : <Badge variant="secondary">One of</Badge>,
            },
            {
              header: 'Status',
              cell: (m) =>
                m.certificationIsActive ? (
                  <span className="text-xs text-muted-foreground">Active</span>
                ) : (
                  <Badge variant="secondary">Retired credential</Badge>
                ),
            },
          ]}
          schema={memberSchema as never}
          emptyForm={empty}
          toForm={(m) => ({ certificationId: m.certificationId, isMandatory: m.isMandatory })}
          renderFields={(form) => (
            <>
              <SelectField
                form={form}
                name="certificationId"
                label="Credential"
                required
                options={certificationOptions}
              />
              <SwitchField
                form={form}
                name="isMandatory"
                label="Mandatory"
                description="Off means any one accepted credential in the set will do."
              />
            </>
          )}
        />
      )}
    />
  );
}
