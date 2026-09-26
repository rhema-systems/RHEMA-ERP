/** DOCX section 16.4 is the default QS experience. Existing extension data and APIs remain intact. */
export const qsOptionalFeatures = [
  'joint-measurements',
  'daywork',
  'subcontracts',
  'escalation',
  'design-impact',
  'tender-exchange',
  'external-submissions',
  'advance-recovery',
  'final-accounts',
] as const;
export type QsOptionalFeature = (typeof qsOptionalFeatures)[number];

// Explicit deployment opt-in, never an authorization grant. All server permissions still apply.
export function isQsOptionalFeatureEnabled(
  feature: QsOptionalFeature,
  configured = process.env.NEXT_PUBLIC_QS_OPTIONAL_FEATURES ?? ''
): boolean {
  return configured
    .split(',')
    .map((value) => value.trim())
    .includes(feature);
}

export function isQsExtensionDecision(key: string): boolean {
  return ['QS-DEC-006', 'QS-DEC-013', 'QS-DEC-014', 'QS-DEC-017'].includes(key);
}
