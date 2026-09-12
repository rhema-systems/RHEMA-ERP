'use client';

import { useMemo } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  TextField,
  TextareaField,
  SelectField,
  SwitchField,
} from '@/components/hr/employee/tabs/fields';
import { goalLibraryService } from '@/services/hr/goals.service';
import { organizationLevelService } from '@/services/hr/organization-level.service';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import { employeePositionService } from '@/services/hr/employee-position.service';
import type { GoalLibraryItem } from '@/types/hr/goals';

/**
 * Goal library — reusable goal wording, so the same objective is not rewritten from scratch
 * for every employee who carries it.
 *
 * Scope is a hint, not a rule: a template restricted to a level, unit or position is offered
 * first when someone in that scope creates a goal, but nothing stops a goal being written
 * without a template. Templates with no scope are offered everywhere.
 *
 * Copying is a snapshot — editing a template does not reach goals already created from it.
 * The usage figures on each row's detail page are how you see what a change would not affect.
 */
const librarySchema = z.object({
  title: z.string().min(1, 'Required').max(300),
  description: z.string().max(2000).optional(),
  successCriteria: z.string().max(1000).optional(),
  organizationLevelId: z.string().optional(),
  organizationUnitId: z.string().optional(),
  positionId: z.string().optional(),
  isActive: z.boolean(),
});

type LibraryForm = z.input<typeof librarySchema>;

const emptyLibrary: LibraryForm = {
  title: '',
  description: '',
  successCriteria: '',
  organizationLevelId: '',
  organizationUnitId: '',
  positionId: '',
  isActive: true,
};

const toPayload = (values: LibraryForm) => {
  const v = librarySchema.parse(values);
  return {
    title: v.title,
    description: v.description || null,
    successCriteria: v.successCriteria || null,
    organizationLevelId: v.organizationLevelId || null,
    organizationUnitId: v.organizationUnitId || null,
    positionId: v.positionId || null,
    isActive: v.isActive,
  };
};

/** Mirrors the `scopeSummary` the selector endpoint renders, so both views read the same. */
function scopeLabel(item: GoalLibraryItem): string {
  if (item.positionTitle) return `Position: ${item.positionTitle}`;
  if (item.organizationUnitName) return `Unit: ${item.organizationUnitName}`;
  if (item.organizationLevelName) return `Level: ${item.organizationLevelName}`;
  return 'Global';
}

export default function GoalLibraryPage() {
  const { data: levels } = useQuery({
    queryKey: ['hr', 'organization-levels'],
    queryFn: () => organizationLevelService.getAll(),
  });
  const { data: units } = useQuery({
    queryKey: ['hr', 'organization-units'],
    queryFn: () => organizationUnitService.getAll(),
  });
  const { data: positions } = useQuery({
    queryKey: ['hr', 'positions', 'active'],
    queryFn: () => employeePositionService.getActive(),
  });

  const levelOptions = useMemo(
    () => (levels ?? []).map((l) => ({ value: l.id, label: l.name })),
    [levels],
  );
  const unitOptions = useMemo(
    () => (units ?? []).map((u) => ({ value: u.id, label: u.name })),
    [units],
  );
  const positionOptions = useMemo(
    () => (positions ?? []).map((p) => ({ value: p.id, label: p.title })),
    [positions],
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Goal Library"
        description="Reusable goal templates. Their wording is copied into employee goals and stays editable there."
        backHref="/administration/hr/performance"
      />

      <ResourceListPanel<GoalLibraryItem, LibraryForm>
        title="templates"
        singular="template"
        queryKey={['hr', 'goal-library']}
        dialogHint="Leave every scope blank to offer the template across the whole organisation."
        emptyDescription="Add the goals your managers reach for most often."
        list={() => goalLibraryService.getAll()}
        create={(values) => goalLibraryService.create(toPayload(values))}
        update={(id, values) => goalLibraryService.update(id, { id, ...toPayload(values) })}
        remove={(id) => goalLibraryService.remove(id)}
        getId={(r) => r.id}
        actions={[
          {
            label: (r) => (r.isActive ? 'Deactivate' : 'Activate'),
            run: (r) => goalLibraryService.setActive(r.id, !r.isActive),
          },
        ]}
        columns={[
          {
            header: 'Title',
            cell: (r) => (
              <Link
                href={`/administration/hr/performance/goal-library/${r.id}`}
                className="font-medium hover:underline"
              >
                {r.title}
              </Link>
            ),
          },
          {
            header: 'Scope',
            cell: (r) => <Badge variant="outline">{scopeLabel(r)}</Badge>,
          },
          {
            header: 'Success criteria',
            cell: (r) => (
              <span className="line-clamp-1 text-muted-foreground">{r.successCriteria || '—'}</span>
            ),
          },
          { header: 'Status', cell: (r) => <StatusBadge active={r.isActive} /> },
          {
            header: '',
            cell: (r) => (
              <Button variant="ghost" size="sm" asChild>
                <Link href={`/administration/hr/performance/goal-library/${r.id}`}>Usage</Link>
              </Button>
            ),
          },
        ]}
        schema={librarySchema as any}
        emptyForm={emptyLibrary}
        toForm={(r) => ({
          title: r.title,
          description: r.description ?? '',
          successCriteria: r.successCriteria ?? '',
          organizationLevelId: r.organizationLevelId ?? '',
          organizationUnitId: r.organizationUnitId ?? '',
          positionId: r.positionId ?? '',
          isActive: r.isActive,
        })}
        renderFields={(form) => (
          <>
            <TextField
              form={form}
              name="title"
              label="Title"
              required
              placeholder="e.g. Close month-end within five working days"
            />
            <TextareaField form={form} name="description" label="Description" rows={3} />
            <TextareaField
              form={form}
              name="successCriteria"
              label="Success criteria"
              rows={2}
              placeholder="What good looks like — copied into every goal made from this template."
            />
            <SelectField
              form={form}
              name="organizationLevelId"
              label="Organisation level"
              options={levelOptions}
              allowEmpty
              emptyLabel="Any level"
            />
            <SelectField
              form={form}
              name="organizationUnitId"
              label="Organisation unit"
              options={unitOptions}
              allowEmpty
              emptyLabel="Any unit"
            />
            <SelectField
              form={form}
              name="positionId"
              label="Position"
              options={positionOptions}
              allowEmpty
              emptyLabel="Any position"
            />
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="Inactive templates disappear from the picker; goals already made from them are untouched."
            />
          </>
        )}
      />
    </div>
  );
}
