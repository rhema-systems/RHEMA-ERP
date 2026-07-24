import { describe, expect, it } from 'vitest';

import {
  procurementCalendarOccurrenceActions,
  procurementCalendarProfileActions,
  validateProcurementCalendarProfile,
} from './procurement-calendar';
import type {
  ProcurementCalendarEventType,
  SaveProcurementCalendarProfile,
} from '@/types/procurement-calendar';

const eventTypes: ProcurementCalendarEventType[] = [
  'AppPreparation',
  'AppSubmission',
  'MidYearReview',
  'CycleCount',
  'YearEndClose',
  'Renewal',
  'GhanepsDeadline',
];

const valid = (): SaveProcurementCalendarProfile => ({
  profileCode: 'TDC-ANNUAL-CALENDAR',
  name: 'TDC annual procurement calendar',
  timeZoneId: 'Greenwich Standard Time',
  generationHorizonDays: 365,
  catchUpDays: 30,
  effectiveFromUtc: '2026-07-22T00:00:00.000Z',
  changeSummary: 'Approved annual obligation configuration.',
  rules: eventTypes.map((eventType) => ({
    eventType,
    title: `${eventType} obligation`,
    dueMonth: 12,
    dueDay: 1,
    dueLocalTime: '09:00:00',
    reminderLeadDays: 14,
    escalationAfterDays: 1,
    ownerRoleName: 'TDC_PROCUREMENT_OFFICER',
    escalationRoleName: 'TDC_HEAD_OF_PROCUREMENT',
    statutoryReference: `TDC-SOURCE-${eventType}`,
    isEnabled: true,
  })),
});

describe('procurement annual calendar presentation controls', () => {
  it('keeps profile lifecycle actions status-specific', () => {
    expect(procurementCalendarProfileActions({ status: 'Draft' })).toEqual({
      canEdit: true,
      canPublish: true,
      canDelete: true,
      canClone: false,
      canRetire: false,
    });
    expect(
      procurementCalendarProfileActions({ status: 'Published' })
    ).toMatchObject({
      canEdit: false,
      canClone: true,
      canRetire: true,
    });
    expect(
      procurementCalendarProfileActions({ status: 'Retired' })
    ).toMatchObject({
      canClone: true,
      canRetire: false,
    });
  });

  it('prevents terminal task mutation and restricts cancellation to managers', () => {
    expect(
      procurementCalendarOccurrenceActions({ status: 'Due' }, false)
    ).toMatchObject({
      canAcknowledge: true,
      canComplete: true,
      canCancel: false,
    });
    expect(
      procurementCalendarOccurrenceActions({ status: 'Acknowledged' }, true)
    ).toMatchObject({
      canAcknowledge: false,
      canComplete: true,
      canCancel: true,
    });
    expect(
      procurementCalendarOccurrenceActions({ status: 'Escalated' }, true)
        .canAcknowledge
    ).toBe(false);
    expect(
      procurementCalendarOccurrenceActions({ status: 'Completed' }, true).isOpen
    ).toBe(false);
  });

  it('requires explicit time zone, ownership, source lineage, and unique events', () => {
    expect(validateProcurementCalendarProfile(valid())).toBeUndefined();
    const missingReference = valid();
    missingReference.rules[0].statutoryReference = '';
    expect(validateProcurementCalendarProfile(missingReference)).toContain(
      'source/reference'
    );
    const duplicate = valid();
    duplicate.rules[1].eventType = duplicate.rules[0].eventType;
    expect(validateProcurementCalendarProfile(duplicate)).toContain('Only one');
  });
});
