import type { EmployeeDetail } from '@/types/hr/employee';

/**
 * The employee profile's navigation, as one data table (round 3, lane T1; decision D-4).
 *
 * The left rail on wide screens, the grouped select below that, and the `?tab=` deep link all read
 * THIS table and nothing else — so a tab added in lane T2/T3 is one row here plus one `TabsContent`
 * on the page, and it appears in every navigation surface at once. The demo's complaint was
 * nineteen tabs in one scrolling strip; the answer is eight groups the eye can scan.
 *
 * `available` hides a tab that has nothing to say for this employee (the expatriate tab on a
 * national). Hidden ≠ forbidden: the tab's own reads still gate on the server.
 *
 * ⚠ Keys are URL-visible (`?tab=salary`) and referenced from other screens — the approved
 * salary-review proposal links to `?tab=salary`. Rename a key and you break a link.
 */
export interface ProfileTab {
  key: string;
  label: string;
  available?: (e: EmployeeDetail) => boolean;
}

export interface ProfileTabGroup {
  key: string;
  label: string;
  tabs: ProfileTab[];
}

export const PROFILE_TAB_GROUPS: ProfileTabGroup[] = [
  {
    key: 'personal',
    label: 'Personal',
    tabs: [
      { key: 'overview', label: 'Overview' },
      { key: 'contacts', label: 'Addresses' },
      { key: 'emergency', label: 'Emergency contacts' },
      { key: 'dependents', label: 'Dependents' },
      { key: 'identification', label: 'Identification' },
      { key: 'expatriate', label: 'Expatriate', available: (e) => e.isExpatriate },
    ],
  },
  {
    key: 'employment',
    label: 'Employment',
    tabs: [
      { key: 'contracts', label: 'Contracts' },
      { key: 'position-history', label: 'Position history' },
      // Lane T2 record tabs — read-only views over the owning module's by-employee read.
      { key: 'movements', label: 'Movements' },
      { key: 'probation', label: 'Probation' },
      { key: 'teams', label: 'Teams' },
      { key: 'relievers', label: 'Relievers' },
      { key: 'separation', label: 'Separation' },
    ],
  },
  {
    key: 'pay',
    label: 'Pay & benefits',
    tabs: [
      { key: 'salary', label: 'Salary' },
      { key: 'salary-changes', label: 'Salary changes' },
      { key: 'bank', label: 'Bank' },
      { key: 'benefits', label: 'Benefits' },
    ],
  },
  {
    key: 'capability',
    label: 'Capability',
    tabs: [
      { key: 'qualifications', label: 'Qualifications' },
      { key: 'skills', label: 'Skills' },
      { key: 'certifications', label: 'Certifications' },
      { key: 'work-history', label: 'Work history' },
      { key: 'training', label: 'Training' },
    ],
  },
  {
    // Lane T3 record tabs.
    key: 'performance',
    label: 'Performance & conduct',
    tabs: [
      { key: 'appraisals', label: 'Appraisals & goals' },
      { key: 'discipline', label: 'Discipline' },
      { key: 'awards', label: 'Awards' },
    ],
  },
  {
    key: 'time',
    label: 'Time & leave',
    tabs: [
      { key: 'leave', label: 'Leave' },
      { key: 'attendance', label: 'Attendance' },
    ],
  },
  {
    key: 'welfare',
    label: 'Welfare & travel',
    tabs: [
      { key: 'medical', label: 'Medical' },
      { key: 'travel', label: 'Travel' },
      { key: 'assets', label: 'Assets' },
    ],
  },
  {
    key: 'records',
    label: 'Records',
    tabs: [
      { key: 'documents', label: 'Documents' },
      { key: 'referees', label: 'Referees' },
      { key: 'guarantors', label: 'Guarantors' },
      { key: 'orientation', label: 'Orientation' },
      { key: 'succession', label: 'Succession' },
    ],
  },
];

export const DEFAULT_PROFILE_TAB = 'overview';

/** Every tab key in table order, filtered for this employee. */
export function visibleProfileTabs(e: EmployeeDetail): ProfileTabGroup[] {
  return PROFILE_TAB_GROUPS.map((g) => ({
    ...g,
    tabs: g.tabs.filter((t) => !t.available || t.available(e)),
  })).filter((g) => g.tabs.length > 0);
}

/** The group a tab belongs to, or null for a key the table does not know. */
export function groupOfTab(key: string): ProfileTabGroup | null {
  return PROFILE_TAB_GROUPS.find((g) => g.tabs.some((t) => t.key === key)) ?? null;
}

/** A `?tab=` value from the URL becomes a tab only if the table knows it and it is visible. */
export function resolveProfileTab(requested: string | null | undefined, e: EmployeeDetail): string {
  if (!requested) return DEFAULT_PROFILE_TAB;
  const known = visibleProfileTabs(e).some((g) => g.tabs.some((t) => t.key === requested));
  return known ? requested : DEFAULT_PROFILE_TAB;
}
