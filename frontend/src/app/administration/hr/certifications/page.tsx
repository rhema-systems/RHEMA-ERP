'use client';

/**
 * The certification catalogue — the credentials each certifying body issues (demo feedback
 * round 2, lane C2; plan § 1.3 and § 6.3).
 *
 * A credential is a compliance object, a qualification is an education object: a licence expires,
 * is renewed, is revoked, and its lapse is a safety or regulatory event. That is why this is its
 * own catalogue under the body, referenced from three places — the skill that needs it, the
 * position that requires it, the employee who holds it — rather than a qualification type.
 *
 * ⚠ `Qualification` rows of type Certification / Licence are untouched. The data pass that may
 * turn them into catalogue rows is the plan's Q-6 and needs a look at the real data first.
 */

import { useMemo, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  TextField,
  TextareaField,
  SelectField,
  SwitchField,
  NumberField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { referenceDimensionService } from '@/services/hr/lookup.service';
import { certificationService } from '@/services/hr/certification.service';
import { CERTIFICATION_KIND_OPTIONS, type Certification } from '@/types/hr/certification';

const ANY = '__any__';

const schema = z.object({
  certifyingBodyId: z.string().min(1, 'Choose the certifying body'),
  name: z.string().min(1, 'A name is required').max(200),
  code: z.string().max(50).optional(),
  kind: z.enum(['Certification', 'Licence', 'Permit', 'Registration']),
  description: z.string().max(1000).optional(),
  validityMonths: z.string().optional(),
  renewalRequired: z.boolean(),
  expiryNotificationLeadDays: z.string().optional(),
  isActive: z.boolean(),
});

type Form = z.input<typeof schema>;

const empty: Form = {
  certifyingBodyId: '',
  name: '',
  code: '',
  kind: 'Certification',
  description: '',
  validityMonths: '',
  renewalRequired: false,
  expiryNotificationLeadDays: '',
  isActive: true,
};

const toInt = (v?: string) => (v && v.trim() ? Number(v) : null);

function toPayload(values: Form) {
  const parsed = schema.parse(values);
  return {
    certifyingBodyId: parsed.certifyingBodyId,
    name: parsed.name,
    code: parsed.code || null,
    kind: parsed.kind,
    description: parsed.description || null,
    validityMonths: toInt(parsed.validityMonths),
    renewalRequired: parsed.renewalRequired,
    expiryNotificationLeadDays: toInt(parsed.expiryNotificationLeadDays),
    isActive: parsed.isActive,
  };
}

export default function CertificationsPage() {
  const searchParams = useSearchParams();
  const [bodyId, setBodyId] = useState<string>(searchParams?.get('bodyId') || ANY);

  const { data: bodies } = useQuery({
    queryKey: ['hr', 'certifying-bodies'],
    queryFn: () => referenceDimensionService.getCertifyingBodies(),
  });

  const bodyOptions = useMemo(
    () =>
      (bodies ?? [])
        .filter((b) => b.isActive)
        .map((b) => ({ value: b.id, label: b.abbreviation ? `${b.name} (${b.abbreviation})` : b.name })),
    [bodies],
  );
  const bodyName = bodies?.find((b) => b.id === bodyId)?.name;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Certifications"
        description="The credentials each certifying body issues — what a skill needs, a position requires, and an employee holds."
        backHref="/administration/hr"
      />

      <div className="max-w-sm space-y-2">
        <Label htmlFor="bodyFilter">Certifying body</Label>
        <Select value={bodyId} onValueChange={setBodyId}>
          <SelectTrigger id="bodyFilter">
            <SelectValue placeholder="Every body" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ANY}>Every body</SelectItem>
            {(bodies ?? []).map((b) => (
              <SelectItem key={b.id} value={b.id}>
                {b.abbreviation ? `${b.name} (${b.abbreviation})` : b.name}
                {b.isActive ? '' : ' (retired)'}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <ResourceListPanel<Certification, Form>
        title={bodyName ? `certifications issued by ${bodyName}` : 'certifications'}
        singular="certification"
        queryKey={['hr', 'certifications', bodyId]}
        dialogHint="Validity is a default for new credentials — a body may issue one for longer. Lead days left blank use the tenant's policy default."
        list={() => certificationService.getAll(bodyId === ANY ? {} : { bodyId })}
        create={(values) => certificationService.create(toPayload(values))}
        update={(id, values) => certificationService.update(id, toPayload(values))}
        remove={(id) => certificationService.remove(id)}
        getId={(c) => c.id}
        columns={[
          {
            header: 'Certification',
            cell: (c) => (
              <div>
                <div className="font-medium">{c.name}</div>
                {c.code && <div className="text-xs text-muted-foreground">{c.code}</div>}
              </div>
            ),
          },
          { header: 'Body', cell: (c) => c.certifyingBodyAbbreviation || c.certifyingBodyName },
          { header: 'Kind', cell: (c) => <Badge variant="outline">{c.kind}</Badge> },
          {
            header: 'Validity',
            cell: (c) => (c.validityMonths ? `${c.validityMonths} months` : 'Does not expire'),
          },
          {
            header: 'Warn',
            cell: (c) => (c.expiryNotificationLeadDays != null ? `${c.expiryNotificationLeadDays}d` : 'policy'),
          },
          {
            header: 'Cited by',
            // What a delete would sever; the server refuses the delete while any of these is non-zero.
            cell: (c) => (
              <span className="text-xs text-muted-foreground">
                {c.skillCount} skill{c.skillCount === 1 ? '' : 's'} · {c.positionCount} position
                {c.positionCount === 1 ? '' : 's'} · {c.holderCount} holder{c.holderCount === 1 ? '' : 's'}
              </span>
            ),
          },
          { header: 'Status', cell: (c) => <StatusBadge active={c.isActive} /> },
        ]}
        schema={schema as any}
        emptyForm={{ ...empty, certifyingBodyId: bodyId === ANY ? '' : bodyId }}
        toForm={(c) => ({
          certifyingBodyId: c.certifyingBodyId,
          name: c.name,
          code: c.code ?? '',
          kind: c.kind,
          description: c.description ?? '',
          validityMonths: c.validityMonths != null ? String(c.validityMonths) : '',
          renewalRequired: c.renewalRequired,
          expiryNotificationLeadDays:
            c.expiryNotificationLeadDays != null ? String(c.expiryNotificationLeadDays) : '',
          isActive: c.isActive,
        })}
        renderFields={(form) => (
          <>
            <SelectField
              form={form}
              name="certifyingBodyId"
              label="Certifying body"
              required
              options={bodyOptions}
            />
            <FieldRow>
              <TextField form={form} name="name" label="Name" required placeholder="Forklift Operator Licence" />
              <TextField form={form} name="code" label="Code" placeholder="FLT-1" />
            </FieldRow>
            <FieldRow>
              <SelectField form={form} name="kind" label="Kind" required options={CERTIFICATION_KIND_OPTIONS} />
              <NumberField form={form} name="validityMonths" label="Valid for (months)" />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="expiryNotificationLeadDays" label="Warn before expiry (days)" />
              <SwitchField
                form={form}
                name="renewalRequired"
                label="Renewal required"
                description="The holder must renew rather than re-sit."
              />
            </FieldRow>
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="A retired credential stops being offered on new skills, positions and employee records; those already citing it keep it."
            />
          </>
        )}
      />
    </div>
  );
}
