'use client';

import type { FieldValues } from 'react-hook-form';
import { useEmployeeProfile } from '@/components/hr/employee/EmployeeProfileContext';
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
  'parentId' | 'queryKey' | 'invalidateKeys' | 'dialogHint' | 'subjectLabel'
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
  // The name comes by context from the profile page (round 3, lane P1); a tab mounted anywhere
  // else still renders, with the anonymous wording it always had.
  const subject = useEmployeeProfile();
  const subjectLabel = subject && subject.id === employeeId ? subject.fullName : null;
  return (
    <ResourceCollectionTab<TItem, TForm>
      {...rest}
      singular={singular}
      parentId={employeeId}
      queryKey={['hr', 'employees', employeeId, queryKey]}
      invalidateKeys={[['hr', 'employees', employeeId, 'details']]}
      subjectLabel={subjectLabel}
      dialogHint={
        subjectLabel
          ? `Add a new ${singular} to ${subjectLabel}'s profile.`
          : `Add a new ${singular} to this employee's profile.`
      }
    />
  );
}
