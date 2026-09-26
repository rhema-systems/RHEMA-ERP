'use client';

/**
 * The shared shell for the three named-set masters (demo feedback round 2, lane C3, plan § 6.4):
 * benefit groups, skill sets, certification sets.
 *
 * All three are the same screen — a list of sets on the left, the selected set's members on the
 * right — and only the member panel differs, so only the member panel is passed in. The header
 * form (name, code, description, active) is identical in all three and lives here once.
 *
 * ⚠ Retire, do not delete. The server refuses a delete while any position holds the set, and says
 * how many; retiring stops it being offered on new posts and leaves the posts that hold it working.
 */

import { useState, type ReactNode } from 'react';
import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import { TextField, TextareaField, SwitchField, FieldRow } from '@/components/hr/employee/tabs/fields';
import type { NamedSetRequest } from '@/types/hr/named-sets';

export const namedSetSchema = z.object({
  name: z.string().min(1, 'A name is required').max(150),
  code: z.string().max(50).optional(),
  description: z.string().max(1000).optional(),
  isActive: z.boolean(),
});

export type NamedSetForm = z.input<typeof namedSetSchema>;

export const emptyNamedSet: NamedSetForm = { name: '', code: '', description: '', isActive: true };

export function toNamedSetPayload(values: NamedSetForm): NamedSetRequest {
  const parsed = namedSetSchema.parse(values);
  return {
    name: parsed.name,
    code: parsed.code || null,
    description: parsed.description || null,
    isActive: parsed.isActive,
  };
}

/** The header shape the three masters share. */
export interface NamedSetRow {
  id: string;
  name: string;
  code?: string | null;
  description?: string | null;
  isActive: boolean;
  memberCount: number;
  positionCount: number;
}

interface Props<TSet extends NamedSetRow> {
  title: string;
  description: string;
  /** "benefit group" — used in the panel title, the dialog and the empty state. */
  singular: string;
  /** What a member is called: "benefit", "skill", "credential". */
  memberNoun: string;
  queryKey: unknown[];
  list: () => Promise<TSet[]>;
  create: (values: NamedSetForm) => Promise<unknown>;
  update: (id: string, values: NamedSetForm) => Promise<unknown>;
  remove: (id: string) => Promise<unknown>;
  /** The member collection for the selected set. Rendered only once a set is chosen. */
  renderMembers: (set: TSet) => ReactNode;
  /** Optional extra guidance under the header. */
  hint?: string;
}

export function NamedSetPage<TSet extends NamedSetRow>({
  title,
  description,
  singular,
  memberNoun,
  queryKey,
  list,
  create,
  update,
  remove,
  renderMembers,
  hint,
}: Props<TSet>) {
  const [selected, setSelected] = useState<TSet | null>(null);

  return (
    <div className="space-y-6 p-6">
      <PageHeader title={title} description={description} backHref="/administration/hr" />

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
        <ResourceListPanel<TSet, NamedSetForm>
          title={`${singular}s`}
          singular={singular}
          queryKey={queryKey}
          dialogHint={
            hint ??
            `A ${singular} is attached to a position in one move instead of ${memberNoun} by ${memberNoun}. ` +
              `A position cannot then list the same ${memberNoun} individually as well.`
          }
          list={list}
          create={create}
          update={update}
          remove={remove}
          getId={(s) => s.id}
          columns={[
            {
              header: 'Name',
              cell: (s) => (
                <button
                  type="button"
                  onClick={() => setSelected(s)}
                  className={`text-left font-medium hover:underline ${selected?.id === s.id ? 'text-primary' : ''}`}
                >
                  <div>{s.name}</div>
                  {s.code && <div className="text-xs text-muted-foreground">{s.code}</div>}
                </button>
              ),
            },
            {
              header: 'Members',
              cell: (s) => (
                <Badge variant="outline">
                  {s.memberCount} {memberNoun}
                  {s.memberCount === 1 ? '' : 's'}
                </Badge>
              ),
            },
            {
              header: 'Used by',
              // What a delete would sever; the server refuses while this is non-zero.
              cell: (s) => (
                <span className="text-xs text-muted-foreground">
                  {s.positionCount} position{s.positionCount === 1 ? '' : 's'}
                </span>
              ),
            },
            { header: 'Status', cell: (s) => <StatusBadge active={s.isActive} /> },
          ]}
          schema={namedSetSchema as never}
          emptyForm={emptyNamedSet}
          toForm={(s) => ({
            name: s.name,
            code: s.code ?? '',
            description: s.description ?? '',
            isActive: s.isActive,
          })}
          renderFields={(form) => (
            <>
              <FieldRow>
                <TextField form={form} name="name" label="Name" required placeholder="Site safety core" />
                <TextField form={form} name="code" label="Code" placeholder="SAFETY-CORE" />
              </FieldRow>
              <TextareaField form={form} name="description" label="Description" rows={2} />
              <SwitchField
                form={form}
                name="isActive"
                label="Active"
                description={`A retired ${singular} stops being offered on new positions; posts already holding it keep it.`}
              />
            </>
          )}
        />

        <div className="space-y-2">
          <h2 className="text-lg font-semibold">
            {`${memberNoun.charAt(0).toUpperCase()}${memberNoun.slice(1)}s`}
            {selected ? ` — ${selected.name}` : ''}
          </h2>
          {!selected ? (
            <p className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
              {`Choose a ${singular} on the left to see and edit what is in it.`}
            </p>
          ) : (
            renderMembers(selected)
          )}
        </div>
      </div>
    </div>
  );
}
