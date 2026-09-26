'use client';

/**
 * The contract-kind vocabulary — what an appointment letter calls the engagement.
 *
 * ⚠ **A kind is not an employment type.** `EmploymentType` is the system's fixed enum and code
 * branches on it: the probation rule is keyed to `Permanent`, and which staff-number register issues
 * a number is chosen by it. This table is the organisation's own naming, and it carries the one
 * thing the enum cannot — how long that kind of engagement runs, which is what dates a fixed-term
 * contract's end. The two coexist because they answer different questions.
 *
 * ⚠ **There is no delete.** Contracts name their kind by foreign key. A kind no longer offered is
 * retired, and every contract already written against it keeps resolving; deleting it would either
 * break the constraint or leave live contracts pointing at a word nothing can look up.
 *
 * This table was seeded with TDC's seven kinds in 2026 and then reachable from nowhere at all — no
 * DTO, no service, no controller, no screen. Surfaced in round-2 lane D1 (question Q-4).
 */

import { z } from 'zod';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  TextField,
  NumberField,
  TextareaField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { contractTypeService } from '@/services/hr/contract-type.service';
import { contractTypeDuration, type ContractType } from '@/types/hr/contract-type';

const schema = z.object({
  name: z.string().min(1, 'A name is required').max(100),
  code: z.string().max(50).optional(),
  description: z.string().max(1000).optional(),
  duration: z.coerce.number().int('Whole months only').min(0).max(600),
  isActive: z.boolean(),
});

type ContractTypeForm = z.input<typeof schema>;

const empty: ContractTypeForm = {
  name: '',
  code: '',
  description: '',
  duration: 0,
  isActive: true,
};

/** Blank optionals must reach the API as null, not as empty strings. */
function toPayload(values: ContractTypeForm) {
  const parsed = schema.parse(values);
  return {
    ...parsed,
    code: parsed.code || null,
    description: parsed.description || null,
  };
}

export default function ContractTypesPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Contract Types"
        description="The kinds of engagement contracts are written under, and how long each normally runs."
        backHref="/administration/hr"
      />

      <ResourceListPanel<ContractType, ContractTypeForm>
        title="contract types"
        singular="contract type"
        queryKey={['hr', 'contract-types']}
        // The picker on the employee create form and the contracts tab both read the active list.
        invalidateKeys={[['hr', 'contract-types', 'active']]}
        dialogHint="Duration is in months and dates the contract's scheduled end. Zero means open-ended — a permanent appointment has no end date."
        getId={(t) => t.id}
        list={() => contractTypeService.getAll()}
        create={(values) => contractTypeService.create(toPayload(values) as any)}
        update={(id, values) => contractTypeService.update(id, toPayload(values) as any)}
        // ⚠ Deliberately no `remove`: retiring is the only withdrawal this table supports.
        columns={[
          { header: 'Kind', cell: (t) => <span className="font-medium">{t.name}</span> },
          { header: 'Code', cell: (t) => t.code || '—' },
          {
            header: 'Runs for',
            cell: (t) => contractTypeDuration(t.duration),
            className: 'text-right',
          },
          {
            header: 'Contracts',
            // ⚠ Zero is not decoration. A kind nothing is written under can be retired freely; one
            // that carries contracts is a word those documents still have to resolve.
            cell: (t) =>
              t.contractCount > 0 ? (
                t.contractCount
              ) : (
                <span className="text-muted-foreground">none</span>
              ),
            className: 'text-right',
          },
          { header: 'Status', cell: (t) => <StatusBadge active={t.isActive} /> },
        ]}
        schema={schema as any}
        emptyForm={empty}
        toForm={(t) => ({
          name: t.name,
          code: t.code ?? '',
          description: t.description ?? '',
          duration: t.duration,
          isActive: t.isActive,
        })}
        renderFields={(form) => (
          <>
            <FieldRow>
              <TextField form={form} name="name" label="Name" required />
              <TextField form={form} name="code" label="Code" placeholder="PERM, CONT, NSS" />
            </FieldRow>
            <NumberField
              form={form}
              name="duration"
              label="Duration (months)"
              required
            />
            <TextareaField
              form={form}
              name="description"
              label="Description"
              rows={3}
              placeholder="What this kind of appointment is, in the words the letter uses."
            />
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="A retired kind stops being offered on new contracts; those already written under it keep it."
            />
          </>
        )}
      />
    </div>
  );
}
