/**
 * Money on the staff-travel screens.
 *
 * ⚠ It never invents a currency. Ten copies of a formatter fell back to `'GHS'` when a row carried
 * no code (travel final closure, lane 0 — finding F4), labelling a figure in a currency nobody
 * chose. Every travel money field carries its own code; when one does not, the number shows bare.
 */
const plain = new Intl.NumberFormat(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export function fmtTravelMoney(amount?: number | null, currency?: string | null): string {
  if (amount === null || amount === undefined) return '—';
  if (!currency) return plain.format(amount);
  try {
    return new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency,
      currencyDisplay: 'code',
    }).format(amount);
  } catch {
    // An ISO code Intl does not know still shows, as the code beside the number.
    return `${currency} ${plain.format(amount)}`;
  }
}
