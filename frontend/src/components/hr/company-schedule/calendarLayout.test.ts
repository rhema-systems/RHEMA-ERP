import { describe, expect, it } from 'vitest';
import { entrySpan, layoutWeek } from './calendarLayout';

// A Monday-to-Sunday week (5–11 Oct 2026).
const week = ['2026-10-05', '2026-10-06', '2026-10-07', '2026-10-08', '2026-10-09', '2026-10-10', '2026-10-11'];
const allDay = (start: string, end: string) => ({ start: `${start}T00:00:00`, end: `${end}T23:59:59`, isAllDay: true });
const timed = (day: string, from: string, to: string) => ({ start: `${day}T${from}:00`, end: `${day}T${to}:00`, isAllDay: false });

describe('the calendar\'s bands (lane 7)', () => {
  it('draws a multi-day entry as one band across its days', () => {
    const [band] = layoutWeek([allDay('2026-10-06', '2026-10-08')], week);
    expect([band.col, band.width, band.before, band.after]).toEqual([1, 3, false, false]);
  });

  it('breaks a band at the end of the week, marking that it goes on', () => {
    const [band] = layoutWeek([allDay('2026-10-09', '2026-10-14')], week);
    expect([band.col, band.width, band.before, band.after]).toEqual([4, 3, false, true]);
  });

  it('starts a band that began last week at Monday, marking that it came from before', () => {
    const [band] = layoutWeek([allDay('2026-10-01', '2026-10-06')], week);
    expect([band.col, band.width, band.before, band.after]).toEqual([0, 2, true, false]);
  });

  it('leaves out what is not in the week', () => {
    expect(layoutWeek([allDay('2026-10-12', '2026-10-13'), allDay('2026-09-28', '2026-10-04')], week)).toHaveLength(0);
  });

  it('puts overlapping bands in separate lanes and reuses a lane once it is free', () => {
    const placed = layoutWeek(
      [allDay('2026-10-05', '2026-10-07'), allDay('2026-10-06', '2026-10-06'), timed('2026-10-09', '09:00', '10:00')],
      week,
    );
    expect(placed.map((p) => [p.col, p.lane])).toEqual([[0, 0], [1, 1], [4, 0]]);
  });

  it('puts the longer and the all-day entry above a meeting on the same day', () => {
    const placed = layoutWeek([timed('2026-10-07', '09:00', '10:00'), allDay('2026-10-07', '2026-10-07')], week);
    expect(placed[0].entry.isAllDay).toBe(true);
    expect(placed[0].lane).toBe(0);
  });

  it('reads an end before the start as the start (a closure ending at midnight of its last day stays on it)', () => {
    expect(entrySpan({ start: '2026-10-07T00:00:00', end: '2026-10-07T00:00:00' })).toEqual({ first: '2026-10-07', last: '2026-10-07' });
    expect(entrySpan({ start: '2026-10-07T10:00:00', end: '' })).toEqual({ first: '2026-10-07', last: '2026-10-07' });
  });
});
