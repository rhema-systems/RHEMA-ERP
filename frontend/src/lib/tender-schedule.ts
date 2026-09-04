export const TENDER_OPENING_SEQUENCE_ERROR =
  'Opening date cannot be earlier than the submission deadline';

export function getTenderScheduleError(
  submissionDeadline?: string | null,
  openingDate?: string | null
): string | null {
  if (!submissionDeadline || !openingDate) return null;

  const deadline = new Date(submissionDeadline);
  const opening = new Date(openingDate);

  if (Number.isNaN(deadline.getTime()) || Number.isNaN(opening.getTime())) {
    return 'Enter valid submission and opening dates';
  }

  return opening < deadline ? TENDER_OPENING_SEQUENCE_ERROR : null;
}
