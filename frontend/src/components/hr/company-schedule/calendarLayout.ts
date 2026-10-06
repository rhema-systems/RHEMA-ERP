import type { CalendarEntry } from '@/types/hr/company-schedule';

/** The calendar days an entry covers, its first to its last (an end before its start is read as the start). */
export function entrySpan(e: Pick<CalendarEntry, 'start' | 'end'>): { first: string; last: string } {
  const first = e.start.slice(0, 10);
  const end = (e.end || e.start).slice(0, 10);
  return { first, last: end < first ? first : end };
}

/** One band in one week row: its first column, how many columns it spans, its lane, and whether it runs on either side. */
export interface PlacedBand<T> {
  entry: T;
  col: number;
  width: number;
  lane: number;
  /** It began before this week (drawn with a square left end and ◂). */
  before: boolean;
  /** It goes on after this week (a square right end and ▸). */
  after: boolean;
}

/**
 * One week's bands (company-schedule lane 7, the user's ruling: an entry over several days is ONE band, broken at the end
 * of each week). Each entry is clipped to the week, then packed into the first lane free from its first column — the
 * earlier, then the longer, then the all-day first, so a closure is not pushed below a meeting.
 */
export function layoutWeek<T extends Pick<CalendarEntry, 'start' | 'end' | 'isAllDay'>>(entries: T[], days: string[]): PlacedBand<T>[] {
  if (days.length === 0) return [];
  const first = days[0];
  const last = days[days.length - 1];
  const items = entries
    .map((entry) => ({ entry, ...entrySpan(entry) }))
    .filter((x) => x.last >= first && x.first <= last)
    .map((x) => {
      const from = x.first < first ? first : x.first;
      const to = x.last > last ? last : x.last;
      return {
        entry: x.entry,
        col: days.indexOf(from),
        width: days.indexOf(to) - days.indexOf(from) + 1,
        before: x.first < first,
        after: x.last > last,
      };
    })
    .sort((a, b) =>
      a.col - b.col
      || b.width - a.width
      || Number(b.entry.isAllDay) - Number(a.entry.isAllDay)
      || a.entry.start.localeCompare(b.entry.start));
  const laneEnds: number[] = [];
  return items.map((x) => {
    let lane = laneEnds.findIndex((end) => end < x.col);
    if (lane === -1) {
      lane = laneEnds.length;
      laneEnds.push(-1);
    }
    laneEnds[lane] = x.col + x.width - 1;
    return { ...x, lane };
  });
}
