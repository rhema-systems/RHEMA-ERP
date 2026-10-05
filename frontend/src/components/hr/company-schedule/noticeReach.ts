import type { CompanyEventNoticeResult } from '@/types/hr/company-schedule';

/**
 * What a company-schedule notice did, as one sentence for a toast (lane 2e-2, R4-6.3): "3 of 4 reached — 2 by
 * email, 3 in the app. 1 not reached: …". The counts are of people — a person reached both ways counts once —
 * and come from the email result and the in-app notice, never from the attempt.
 *
 * @param label Who it was for, when the toast tells more than one thing: "Guests told".
 */
export function describeReach(r?: CompanyEventNoticeResult | null, label?: string): string | undefined {
  if (!r) return undefined;
  const lead = label ? `${label}: ` : '';
  if (r.issued === 0) return `${lead}nobody to tell.`;

  const how = `${r.emailed} by email, ${r.toldInApp} in the app`;
  if (r.notReached === 0) return `${lead}all ${r.issued} reached — ${how}.`;

  const why = r.mailServerSetUp
    ? 'the mail server took no email for them (or they have no address)'
    : 'no mail server is set up';
  return `${lead}${r.reached} of ${r.issued} reached — ${how}. ${r.notReached} not reached: ${why}, and they have no login to be told in the app.`;
}
