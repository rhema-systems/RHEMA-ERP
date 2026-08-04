/**
 * Formatting helpers for the Attendance & Time screens.
 *
 * The API sends `TimeSpan` and `TimeOnly` as `"HH:mm:ss"` and `DateOnly` as `"YYYY-MM-DD"`.
 * Both are already in the tenant's local terms, so they are trimmed rather than parsed —
 * running them through `Date` would drag the browser's timezone into a value that has none.
 */

/** `"08:30:00"` → `"08:30"`. Returns an em dash for null/blank. */
export function formatTime(value?: string | null): string {
  if (!value) return '—';
  return value.slice(0, 5);
}

/** `"2026-08-04"` → `"04 Aug 2026"`. Accepts a full DateTime and ignores its time part. */
export function formatDate(value?: string | null): string {
  if (!value) return '—';
  const [y, m, d] = value.slice(0, 10).split('-');
  if (!y || !m || !d) return value;
  const months = [
    'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
    'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
  ];
  const monthName = months[Number(m) - 1] ?? m;
  return `${d} ${monthName} ${y}`;
}

/**
 * A DateTime like `"2026-08-04T14:05:00Z"` → `"04 Aug 2026 14:05"`.
 * Unlike the two above this really is an instant, so it is converted to local time.
 */
export function formatDateTime(value?: string | null): string {
  if (!value) return '—';
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return value;
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${formatDate(
    `${parsed.getFullYear()}-${pad(parsed.getMonth() + 1)}-${pad(parsed.getDate())}`,
  )} ${pad(parsed.getHours())}:${pad(parsed.getMinutes())}`;
}

/** Decimal hours → `"7h 30m"`. Null/undefined become an em dash. */
export function formatHours(value?: number | null): string {
  if (value === null || value === undefined) return '—';
  const totalMinutes = Math.round(value * 60);
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  if (hours === 0) return `${minutes}m`;
  if (minutes === 0) return `${hours}h`;
  return `${hours}h ${minutes}m`;
}

/** Splits a PascalCase enum name into words: `"MissingCheckIn"` → `"Missing check in"`. */
export function humanizeEnum(value?: string | null): string {
  if (!value) return '—';
  const spaced = value.replace(/([a-z0-9])([A-Z])/g, '$1 $2');
  return spaced.charAt(0).toUpperCase() + spaced.slice(1).toLowerCase();
}

/** `0.9234` is a *percentage* field here, already 0-100 — render one decimal place. */
export function formatPercent(value?: number | null): string {
  if (value === null || value === undefined) return '—';
  return `${value.toFixed(1)}%`;
}

/** Today as `YYYY-MM-DD` in the browser's timezone, for date-input defaults. */
export function today(): string {
  const now = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

/** `offsetDays` days from today as `YYYY-MM-DD`; negative goes back. */
export function dateOffset(offsetDays: number): string {
  const now = new Date();
  now.setDate(now.getDate() + offsetDays);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

/** Money for the consultant billing screens; the currency is per-client, not global. */
export function formatMoney(value?: number | null, currency = 'GHS'): string {
  if (value === null || value === undefined) return '—';
  return `${currency} ${value.toLocaleString(undefined, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;
}
