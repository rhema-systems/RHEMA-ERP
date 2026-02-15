import { format } from 'date-fns';

function toDate(value?: string | Date | null): Date | null {
  if (!value) return null;
  const d = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(d.getTime())) return null;
  return d;
}

export function formatFleetDate(value?: string | Date | null, fallback = '—'): string {
  const d = toDate(value);
  if (!d) return fallback;
  return format(d, 'dd MMM yyyy');
}

export function formatFleetDateTime(value?: string | Date | null, fallback = '—'): string {
  const d = toDate(value);
  if (!d) return fallback;
  return format(d, 'dd MMM yyyy, HH:mm');
}

