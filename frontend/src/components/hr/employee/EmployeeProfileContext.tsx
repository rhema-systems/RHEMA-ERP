'use client';

import { createContext, useContext, type ReactNode } from 'react';

/**
 * Who the profile page is about, for every tab beneath it (round 3, lane P1).
 *
 * The demo asked for the employee's name in the header of every sub-detail dialog. The tabs are
 * rendered with an `employeeId` only, and `ResourceCollectionTab` is shared with ~30 screens that
 * have no employee at all — so the name travels by context rather than by a prop threaded through
 * seventeen tabs, and the generic tab receives it as a plain optional `subjectLabel`.
 */
export interface EmployeeProfileSubject {
  id: string;
  fullName: string;
  employeeNumber?: string | null;
}

const EmployeeProfileContext = createContext<EmployeeProfileSubject | null>(null);

export function EmployeeProfileProvider({
  value,
  children,
}: {
  value: EmployeeProfileSubject;
  children: ReactNode;
}) {
  return <EmployeeProfileContext.Provider value={value}>{children}</EmployeeProfileContext.Provider>;
}

/** Null outside a profile page — tabs must still render without it. */
export function useEmployeeProfile(): EmployeeProfileSubject | null {
  return useContext(EmployeeProfileContext);
}
