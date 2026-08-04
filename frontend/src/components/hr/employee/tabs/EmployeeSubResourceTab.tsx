'use client';

import type { FieldValues } from 'react-hook-form';
import {
  ResourceCollectionTab,
  type CollectionAction,
  type CollectionColumn,
  type ResourceCollectionTabProps,
} from '@/components/hr/common/ResourceCollectionTab';

/** Re-exported under the names the employee tabs already use. */
export type SubResourceColumn<TItem> = CollectionColumn<TItem>;
export type SubResourceAction<TItem> = CollectionAction<TItem>;

type Inherited<TItem, TForm extends FieldValues> = Omit<
  ResourceCollectionTabProps<TItem, TForm>,
  'parentId' | 'queryKey' | 'invalidateKeys' | 'dialogHint'
>;

export interface EmployeeSubResourceTabProps<TItem, TForm extends FieldValues>
  extends Inherited<TItem, TForm> {
  employeeId: string;
  /** Key suffix under ['hr','employees',employeeId]. */
  queryKey: string;
}

/**
 * Employee-profile flavour of {@link ResourceCollectionTab}: it fixes the query key to
 * the employee's namespace and refreshes the employee detail alongside, since the header
 * can surface values derived from these rows.
 */
export function EmployeeSubResourceTab<TItem, TForm extends FieldValues>({
  employeeId,
  queryKey,
  singular,
  ...rest
}: EmployeeSubResourceTabProps<TItem, TForm>) {
  return (
    <ResourceCollectionTab<TItem, TForm>
      {...rest}
      singular={singular}
      parentId={employeeId}
      queryKey={['hr', 'employees', employeeId, queryKey]}
      invalidateKeys={[['hr', 'employees', employeeId, 'details']]}
      dialogHint={`Add a new ${singular} to this employee's profile.`}
    />
  );
}
