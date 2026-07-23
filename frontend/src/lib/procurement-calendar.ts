import type {
  ProcurementCalendarEventType,
  ProcurementCalendarOccurrence,
  ProcurementCalendarProfile,
  SaveProcurementCalendarProfile,
} from '@/types/procurement-calendar';

export const procurementCalendarOpenStatuses = new Set([
  'Upcoming',
  'Due',
  'Acknowledged',
  'Escalated',
]);

export const procurementCalendarProfileActions = (
  profile: Pick<ProcurementCalendarProfile, 'status'>
) => ({
  canEdit: profile.status === 'Draft',
  canPublish: profile.status === 'Draft',
  canDelete: profile.status === 'Draft',
  canClone: profile.status !== 'Draft',
  canRetire: profile.status === 'Published',
});

export const procurementCalendarOccurrenceActions = (
  occurrence: Pick<ProcurementCalendarOccurrence, 'status'>,
  canManage: boolean
) => {
  const isOpen = procurementCalendarOpenStatuses.has(occurrence.status);
  return {
    isOpen,
    canAcknowledge:
      occurrence.status === 'Upcoming' || occurrence.status === 'Due',
    canComplete: isOpen,
    canCancel: isOpen && canManage,
  };
};

export const validateProcurementCalendarProfile = (
  profile: SaveProcurementCalendarProfile
) => {
  if (!profile.profileCode.trim()) return 'Profile code is required.';
  if (!profile.name.trim()) return 'Profile name is required.';
  if (!profile.timeZoneId.trim()) return 'An explicit time zone is required.';
  if (!profile.changeSummary.trim()) return 'Change summary is required.';
  if (profile.generationHorizonDays < 1 || profile.generationHorizonDays > 730)
    return 'Generation horizon must be between 1 and 730 days.';
  if (profile.catchUpDays < 0 || profile.catchUpDays > 365)
    return 'Catch-up window must be between 0 and 365 days.';
  if (
    profile.effectiveToUtc &&
    new Date(profile.effectiveToUtc) < new Date(profile.effectiveFromUtc)
  )
    return 'Effective-to date cannot precede effective-from date.';
  const eventTypes = new Set<ProcurementCalendarEventType>();
  for (const rule of profile.rules) {
    if (eventTypes.has(rule.eventType))
      return `Only one ${rule.eventType} obligation is allowed.`;
    eventTypes.add(rule.eventType);
    if (!rule.title.trim()) return `Title is required for ${rule.eventType}.`;
    if (!rule.statutoryReference.trim())
      return `Approved source/reference is required for ${rule.eventType}.`;
    if (!rule.ownerRoleName || !rule.escalationRoleName)
      return `Owner and escalation responsibility are required for ${rule.eventType}.`;
  }
  return undefined;
};
