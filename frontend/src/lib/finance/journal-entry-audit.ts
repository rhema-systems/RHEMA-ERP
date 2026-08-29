export interface JournalAuditActorContext {
  username?: string | null;
  ipAddress?: string | null;
}

export function getJournalAuditLocationLabel(
  ipAddress?: string | null
): string {
  const value = ipAddress?.trim();
  if (!value) return '';

  const normalizedIp = value.toLowerCase();

  // Historical and service-hosted events can contain this persistence
  // placeholder. It is not a real client location and must not be presented
  // to users as though it were one.
  if (normalizedIp === 'unknown') return '';

  if (
    normalizedIp === '::1' ||
    normalizedIp === '127.0.0.1' ||
    normalizedIp === 'localhost' ||
    normalizedIp.startsWith('::ffff:127.0.0.1')
  ) {
    return 'local device';
  }

  return value;
}

export function getJournalAuditActorLine(
  event: JournalAuditActorContext
): string {
  const username = event.username?.trim() || 'Unknown user';
  const location = getJournalAuditLocationLabel(event.ipAddress);

  return location ? `${username} from ${location}` : username;
}
