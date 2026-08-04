'use client';

import type { FieldValues } from 'react-hook-form';
import {
  ResourceCollectionTab,
  type ResourceCollectionTabProps,
} from '@/components/hr/common/ResourceCollectionTab';

/**
 * A top-level (parent-less) resource list with the same table-and-dialog behaviour as
 * {@link ResourceCollectionTab}.
 *
 * The nested collections that component was extracted for always hang off a parent record,
 * so its callbacks take a `parentId` and its query is disabled until one exists. Plenty of
 * the Attendance & Time setup screens — work schedules, geofence zones, pay periods, devices,
 * alert rules — are the same shape without a parent, so this wrapper supplies a constant
 * stand-in id and hands callers callbacks with the parameter already dropped.
 *
 * Use this for a standalone resource; use `ResourceCollectionTab` directly when the rows
 * genuinely belong to a parent record.
 */

/** Non-empty so `ResourceCollectionTab`'s `enabled: !!parentId` guard still lets the query run. */
const NO_PARENT = '__root__';

export interface ResourceListPanelProps<TItem, TForm extends FieldValues>
  extends Omit<
    ResourceCollectionTabProps<TItem, TForm>,
    'parentId' | 'list' | 'create' | 'update' | 'remove'
  > {
  list: () => Promise<TItem[]>;
  create: (values: TForm) => Promise<unknown>;
  update: (id: string, values: TForm) => Promise<unknown>;
  remove?: (id: string) => Promise<unknown>;
}

export function ResourceListPanel<TItem, TForm extends FieldValues>({
  list,
  create,
  update,
  remove,
  ...rest
}: ResourceListPanelProps<TItem, TForm>) {
  return (
    <ResourceCollectionTab<TItem, TForm>
      {...rest}
      parentId={NO_PARENT}
      list={() => list()}
      create={(_parentId, values) => create(values)}
      update={(_parentId, id, values) => update(id, values)}
      remove={remove ? (_parentId, id) => remove(id) : undefined}
    />
  );
}
