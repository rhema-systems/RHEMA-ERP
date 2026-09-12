/** Preview only: the server independently calculates and validates the saved term. */
export function calculateBidValidity(
  closing?: string,
  days?: number | null
): string | undefined {
  if (!closing || !days || !Number.isSafeInteger(days) || days <= 0)
    return undefined;
  const instant = new Date(closing).getTime();
  const expiry = new Date(instant + days * 86_400_000);
  if (
    !Number.isFinite(instant) ||
    !Number.isFinite(expiry.getTime()) ||
    expiry.getUTCFullYear() > 9999
  )
    return undefined;
  return expiry.toISOString();
}
