'use client';

import { Badge } from '@/components/ui/badge';

/**
 * Risk-level badges for the two SHE scales (hazard-register 6-level, RA-line 5-level), plus the
 * client-side mirrors of the server's fixed banding — used ONLY to preview a score while typing;
 * the stored score and level always come back computed from the server.
 */
export const riskTone = (level: string) =>
  level === 'Critical' || level === 'VeryHigh' || level === 'High'
    ? 'destructive'
    : level === 'Medium'
      ? 'secondary'
      : 'outline';

export function RiskBadge({ level, label }: { level: string; label?: string }) {
  return <Badge variant={riskTone(level)}>{label ?? level}</Badge>;
}

/** Hazard-register bands: ≤3 VeryLow, ≤6 Low, ≤10 Medium, ≤15 High, ≤20 VeryHigh, else Critical. */
export const previewHazardLevel = (score: number) =>
  score <= 3 ? 'Very Low' : score <= 6 ? 'Low' : score <= 10 ? 'Medium' : score <= 15 ? 'High' : score <= 20 ? 'Very High' : 'Critical';

/** RA-line bands: ≤4 Negligible, ≤8 Low, ≤12 Medium, ≤16 High, else Critical. */
export const previewRaLevel = (score: number) =>
  score <= 4 ? 'Negligible' : score <= 8 ? 'Low' : score <= 12 ? 'Medium' : score <= 16 ? 'High' : 'Critical';

export const LIKELIHOOD_OPTIONS = [
  { value: '1', label: '1 — Rare' },
  { value: '2', label: '2 — Unlikely' },
  { value: '3', label: '3 — Possible' },
  { value: '4', label: '4 — Likely' },
  { value: '5', label: '5 — Almost certain' },
];

export const SEVERITY_OPTIONS = [
  { value: '1', label: '1 — Insignificant' },
  { value: '2', label: '2 — Minor' },
  { value: '3', label: '3 — Moderate' },
  { value: '4', label: '4 — Major' },
  { value: '5', label: '5 — Catastrophic' },
];
