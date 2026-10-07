import type { PersonalScheduleEntry } from '@/types/hr/company-schedule';

/** `yyyy-mm-dd` plus whole days, in UTC — the diary's days are calendar days, not instants. */
export function addDay(day: string, n = 1): string {
  const d = new Date(`${day.slice(0, 10)}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + n);
  return d.toISOString().slice(0, 10);
}

/**
 * Every day of [from, to] an entry covers (company-schedule lane 5b, F-23, R4-10A.1/10B.2).
 *
 * The diaries put an entry on its START day only: a week's leave read as Monday's, a three-day course as its first day,
 * and on the team grid an entry that began before the range was not drawn at all. The server sends leave, travel,
 * holidays, closures and a course without sessions as ONE spanning entry (events it already splits per day), so the
 * page spreads it: from its first day or the range's start, whichever is later, to its last day or the range's end,
 * whichever is earlier. The last day is the END's date — a closure ends at midnight at the start of its last day, leave at
 * 23:59 of it; both name the right day.
 */
export function entryDays(entry: PersonalScheduleEntry, from: string, to: string): string[] {
  const first = entry.start.slice(0, 10) > from ? entry.start.slice(0, 10) : from;
  const end = (entry.end || entry.start).slice(0, 10);
  const last = end < to ? end : to;
  const days: string[] = [];
  for (let d = first; d <= last && days.length < 400; d = addDay(d)) days.push(d);
  return days;
}

/** The entries by day — each under every day of the range it covers. */
export function byDay(entries: PersonalScheduleEntry[], from: string, to: string): Map<string, PersonalScheduleEntry[]> {
  const map = new Map<string, PersonalScheduleEntry[]>();
  for (const e of entries) {
    for (const day of entryDays(e, from, to)) map.set(day, [...(map.get(day) ?? []), e]);
  }
  return map;
}

/** True when an entry runs over more than the one day it is drawn on — the page says "continues" / "since". */
export function spansDays(entry: PersonalScheduleEntry): boolean {
  return entry.start.slice(0, 10) !== (entry.end || entry.start).slice(0, 10);
}
