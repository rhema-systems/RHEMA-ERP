import { describe, expect, it } from 'vitest';
import {
  getJournalAuditActorLine,
  getJournalAuditLocationLabel,
} from './journal-entry-audit';

describe('journal entry audit presentation', () => {
  it('does not present a persisted Unknown placeholder as a location', () => {
    expect(
      getJournalAuditActorLine({
        username: 'finance.demo.controller',
        ipAddress: 'Unknown',
      })
    ).toBe('finance.demo.controller');
    expect(getJournalAuditLocationLabel(' unknown ')).toBe('');
  });

  it.each(['127.0.0.1', '::1', 'localhost', '::ffff:127.0.0.1'])(
    'presents loopback address %s as a local device',
    (ipAddress) => {
      expect(getJournalAuditActorLine({ username: 'admin', ipAddress })).toBe(
        'admin from local device'
      );
    }
  );

  it('preserves a real captured client address', () => {
    expect(
      getJournalAuditActorLine({
        username: 'admin',
        ipAddress: '192.0.2.25',
      })
    ).toBe('admin from 192.0.2.25');
  });

  it('uses an explicit actor fallback without inventing a location', () => {
    expect(getJournalAuditActorLine({ username: '', ipAddress: null })).toBe(
      'Unknown user'
    );
  });
});
