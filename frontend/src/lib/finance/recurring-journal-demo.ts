export type DemoRecurringJournal = {
  id: string; templateNumber: string; name: string; description: string; frequency: string;
  schedule: string; nextDue: string; amount: number; status: string; owner: string;
  autoReverse: boolean; referencePattern: string; submittedAt?: string;
};

export const demoRecurringJournals: DemoRecurringJournal[] = [
  { id: 'rent-accrual', templateNumber: 'RJ-2026-0001', name: 'Monthly Office Rent Accrual', description: 'Monthly head-office rent split across operating cost centres.', frequency: 'Monthly', schedule: 'Last business day of every month', nextDue: '2026-07-31', amount: 48000, status: 'Active', owner: 'Akwasi Adu-Kyeremeh', autoReverse: true, referencePattern: 'RENT-{Period}-{Sequence}' },
  { id: 'insurance', templateNumber: 'RJ-2026-0002', name: 'Insurance Prepayment Release', description: 'Straight-line monthly release of annual insurance prepayment.', frequency: 'Monthly', schedule: '1st day of every month', nextDue: '2026-08-01', amount: 12500, status: 'Pending Approval', owner: 'Accounts Officer', autoReverse: false, referencePattern: 'INS-{Period}' },
  { id: 'utilities', templateNumber: 'RJ-2026-0003', name: 'Semi-monthly Utilities Accrual', description: 'Estimated utilities accrual on the 15th and month-end.', frequency: 'Semi-monthly', schedule: '15th and last business day', nextDue: '2026-07-31', amount: 18750, status: 'Draft', owner: 'Senior Accountant', autoReverse: true, referencePattern: 'UTIL-{ScheduledDate}' },
];

export function getDemoTemplates(): DemoRecurringJournal[] {
  if (typeof window === 'undefined') return demoRecurringJournals;
  const saved = window.localStorage.getItem('rhema-recurring-journal-demo');
  return saved ? [...JSON.parse(saved), ...demoRecurringJournals] : demoRecurringJournals;
}

export function saveDemoTemplate(template: DemoRecurringJournal) {
  const saved = typeof window === 'undefined' ? [] : JSON.parse(window.localStorage.getItem('rhema-recurring-journal-demo') || '[]');
  window.localStorage.setItem('rhema-recurring-journal-demo', JSON.stringify([template, ...saved]));
}
