export function createSupportReference(
  now: number = Date.now(),
  random: number = Math.random(),
): string {
  const timestamp = Math.max(0, Math.trunc(now)).toString(36).toUpperCase();
  const boundedRandom = Math.max(0, Math.min(0.999999, random));
  const suffix = Math.trunc(boundedRandom * 1_679_616).toString(36).toUpperCase().padStart(4, "0");
  return `MOBILE-${timestamp}-${suffix}`;
}
